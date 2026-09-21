namespace Battlestation.Shopping;

/// <summary>
/// Le moteur de veille : extraction de la demande, recherche multi-boutiques,
/// relevé des prix, verdict argumenté, puis revérification espacée des articles surveillés.
/// Aucun achat, aucun contact : le dernier clic reste à l'utilisateur.
/// </summary>
public sealed class ShoppingService : IShoppingEngine
{
    readonly ShoppingStore store;
    readonly IReadOnlyList<IPriceSource> sources;
    readonly IVerdictEngine engine;
    readonly PromoCalendar calendar;
    readonly HttpClient http;
    readonly string eventPath;
    readonly string settingsPath;
    readonly string ntfyEndpoint;
    readonly Lock gate=new();
    readonly List<SourceReport> reports=[];
    CancellationTokenSource? background;
    ShoppingSpecParser parser;
    ShoppingSettings settings;
    volatile bool running;
    volatile bool busy;

    /// <summary>Une alerte de veille : prix cible atteint, baisse nette ou retour en stock.</summary>
    public event Action<ShoppingAlert>? Alert;
    public event Action? Changed;

    public ShoppingService(
        ShoppingStore store,
        IReadOnlyList<IPriceSource> sources,
        ShoppingSettings settings,
        string dataDirectory,
        HttpClient? http=null,
        IVerdictEngine? engine=null,
        PromoCalendar? calendar=null,
        ShoppingSpecParser? parser=null,
        string ntfyEndpoint="https://ntfy.sh")
    {
        this.store=store;this.sources=sources;this.settings=settings.Validate();
        this.engine=engine??new VerdictEngine();
        this.calendar=calendar??PromoCalendar.Load(Path.Combine(dataDirectory,ShoppingSettingsStore.EventsFileName));
        this.http=http??Shared;
        this.parser=parser??new ShoppingSpecParser(Llm(this.settings,this.http));
        eventPath=Path.Combine(dataDirectory,ShoppingSettingsStore.EventsFileName);
        settingsPath=Path.Combine(dataDirectory,ShoppingSettingsStore.FileName);
        this.ntfyEndpoint=ntfyEndpoint.TrimEnd('/');
    }

    static readonly HttpClient Shared=CreateHttp();

    static HttpClient CreateHttp()
    {
        var client=new HttpClient{Timeout=Timeout.InfiniteTimeSpan};
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>Le modèle ne sert qu'à mieux lire la demande ; sans lui, l'analyse locale suffit.</summary>
    static ILlmClient? Llm(ShoppingSettings settings,HttpClient http)=>settings.Provider switch
    {
        "deepseek"=>DeepSeekClient.Create(http,settings),
        _=>new OllamaClient(http,settings.Endpoint,settings.Model)
    };

    public bool Busy=>busy;
    public ShoppingSettings Settings=>settings;
    public IReadOnlyList<SourceReport> Sources{get{lock(gate)return reports.ToArray();}}
    public IReadOnlyList<WatchedItem> Watchlist=>store.Watches().Select(item=>new WatchedItem(item.Watch,item.Product)).ToArray();

    /// <summary>Applique et conserve un nouveau fournisseur ou de nouveaux seuils, sans redémarrer le bureau.</summary>
    public void Apply(ShoppingSettings next)
    {
        var validated=next.Validate();
        bool switched=validated.Provider!=settings.Provider||validated.Model!=settings.Model||validated.Endpoint!=settings.Endpoint;
        settings=validated;
        if(switched)parser=new ShoppingSpecParser(Llm(settings,http));
        ShoppingSettingsStore.Save(settingsPath,settings);
        Changed?.Invoke();
    }

    /// <summary>Une recherche complète : demande, boutiques, relevés, verdicts, historique enregistré.</summary>
    public async Task<SearchOutcome> SearchAsync(string request,CancellationToken cancellation)
    {
        var spec=await parser.ParseAsync(request,cancellation);
        if(spec.Query.Length==0)
            return SearchOutcome.Failed(request,spec,[new("","Demande",SourceState.Empty,0,"Aucun produit reconnu dans la demande")]);
        busy=true;Changed?.Invoke();
        try
        {
            var found=new List<Product>();
            var status=new List<SourceReport>();
            foreach(var source in sources)
            {
                try
                {
                    var products=await source.SearchAsync(spec,settings.MaxResults*3,cancellation);
                    status.Add(new(source.Id,source.Name,products.Count==0?SourceState.Empty:SourceState.Ok,products.Count,
                        products.Count==0?"aucune offre":$"{products.Count} offres"));
                    foreach(var product in products)found.Add(product);
                }
                catch(PriceSourceUnavailableException e)
                {
                    status.Add(new(source.Id,source.Name,SourceState.Failed,0,e.Message));
                }
                catch(HttpRequestException e)
                {
                    status.Add(new(source.Id,source.Name,SourceState.Failed,0,$"{source.Name} injoignable ({e.StatusCode})"));
                }
            }
            var hits=await Rank(found,spec,cancellation);
            lock(gate)
            {
                reports.Clear();reports.AddRange(status);
                foreach(var promo in calendar.Custom)store.SavePromoEvents([promo]);
            }
            return new(hits,status,spec,request);
        }
        finally
        {
            busy=false;Changed?.Invoke();
        }
    }

    /// <summary>Regroupe les mêmes appareils entre boutiques, garde l'offre la moins chère et note son verdict.</summary>
    async Task<IReadOnlyList<ShoppingHit>> Rank(IReadOnlyList<Product> found,ShoppingSpec spec,CancellationToken cancellation)
    {
        var groups=new Dictionary<string,List<Product>>(StringComparer.Ordinal);
        foreach(var product in found)
        {
            var key=product.Identity;
            if(key.Length==0)key=product.Id;
            if(!groups.TryGetValue(key,out var list))groups[key]=list=[];
            list.Add(product);
        }
        var ranked=new List<(ShoppingHit Hit,decimal Amount)>();
        foreach(var group in groups.Values)
        {
            var best=group.Where(product=>product.Price is>0).OrderBy(product=>product.Price).FirstOrDefault()??group[0];
            var point=await PriceOf(best,cancellation);
            var history=store.History(best.Id,settings.HistoryDays);
            var verdict=engine.Evaluate(best,history,calendar.Upcoming(DateOnly.FromDateTime(DateTime.Today),settings.WaitWindowDays),spec,settings);
            var alternatives=group.Where(product=>product.Id!=best.Id&&product.Price is>0)
                .OrderBy(product=>product.Price)
                .Select(product=>$"{product.Shop} {ShoppingText.Money(product.Price)}")
                .Distinct().ToArray();
            store.SaveProduct(best);
            if(point is not null&&point.Price is not null)store.RecordPrice(point);
            var criteria=Matches(best,spec,out int total);
            ranked.Add((new(best,point??(best.Price is {} price?new PricePoint(best.Id,DateTimeOffset.Now,price,"EUR",true,best.Source):null),verdict,alternatives,criteria,total),
                (point?.Price??best.Price)??decimal.MaxValue));
        }
        // Le verdict d'abord, puis les offres qui respectent le plus de critères, puis le prix.
        return ranked.OrderBy(item=>item.Hit.Verdict.Decision)
            .ThenByDescending(item=>item.Hit.Criteria)
            .ThenBy(item=>item.Amount)
            .Take(settings.MaxResults)
            .Select(item=>item.Hit).ToArray();
    }

    /// <summary>Compte les critères obligatoires présents dans le titre, accents et espaces ignorés.</summary>
    static int Matches(Product product,ShoppingSpec spec,out int total)
    {
        total=spec.Required.Count;
        if(total==0)return 0;
        var title=ShoppingText.Compact(product.Title);
        return spec.Required.Count(term=>title.Contains(ShoppingText.Compact(term),StringComparison.Ordinal));
    }

    /// <summary>Un produit de liste garde le prix de la liste ; la fiche n'est lue qu'en veille.</summary>
    async Task<PricePoint?> PriceOf(Product product,CancellationToken cancellation)
    {
        if(product.Price is>0)return new(product.Id,DateTimeOffset.Now,product.Price,"EUR",true,product.Source);
        var source=sources.FirstOrDefault(candidate=>candidate.Id==product.Source);
        if(source is null)return null;
        try{return await source.GetPriceAsync(product,cancellation);}
        catch(Exception e) when(e is PriceSourceUnavailableException or HttpRequestException){return null;}
    }

    public void Watch(Product product,decimal? targetPrice,string request)
    {
        store.SaveProduct(product);
        store.SaveWatch(new WatchItem(
            $"watch:{product.Id}",request,product.Id,targetPrice,DateTimeOffset.Now,DateTimeOffset.Now,
            DateTimeOffset.Now,0,false,product.Price,product.Title,product.Shop));
        Changed?.Invoke();
    }

    public void Unwatch(string watchId)
    {
        store.RemoveWatch(watchId);
        Changed?.Invoke();
    }

    public bool IsWatched(string productId)=>store.Watches().Any(item=>item.Watch.ProductId==productId);

    /// <summary>Un passage de veille : seuls les articles arrivés à échéance sont revérifiés.</summary>
    public async Task CheckWatchesAsync(CancellationToken cancellation)
    {
        var today=DateOnly.FromDateTime(DateTime.Today);
        foreach(var (watch,product) in store.Watches())
        {
            if(watch.NextCheck>DateTimeOffset.Now)continue;
            var source=sources.FirstOrDefault(candidate=>candidate.Id==product.Source);
            if(source is null)continue;
            PricePoint? point=null;
            try
            {
                point=await source.GetPriceAsync(product,cancellation);
            }
            catch(Exception e) when(e is PriceSourceUnavailableException or HttpRequestException)
            {
                var failures=watch.Failures+1;
                store.SaveWatch(watch with{Failures=failures,LastCheck=DateTimeOffset.Now,NextCheck=DateTimeOffset.Now+Backoff(failures)});
                continue;
            }
            if(point is null)
            {
                store.SaveWatch(watch with{Failures=watch.Failures+1,LastCheck=DateTimeOffset.Now,NextCheck=DateTimeOffset.Now+Backoff(watch.Failures+1)});
                continue;
            }
            var previous=store.LastPrice(product.Id);
            store.RecordPrice(point with{ProductId=product.Id});
            var spec=ShoppingSpecParser.Deterministic(watch.Request);
            var verdict=engine.Evaluate(product with{Price=point.Price},store.History(product.Id,settings.HistoryDays),
                calendar.Upcoming(today,settings.WaitWindowDays),spec,settings);
            bool target=watch.TargetPrice is {} goal&&point.Price is {} price&&price<=goal;
            bool drop=previous?.Price is {} before&&point.Price is {} now&&now<=before*0.95m;
            bool back=previous is not null&&!previous.InStock&&point.InStock;
            if((target||drop||back)&&!watch.Alerted)
            {
                var alert=new ShoppingAlert(
                    target?$"Prix cible atteint · {product.Shop}":back?$"De nouveau en stock · {product.Shop}":$"Baisse de prix · {product.Shop}",
                    $"{product.Title} · {ShoppingText.Money(point.Price)} ({verdict.Label})",
                    product.Url,point.Price);
                store.SaveWatch(watch with{Alerted=true,Failures=0,LastCheck=DateTimeOffset.Now,NextCheck=DateTimeOffset.Now+Cadence,LastPrice=point.Price});
                Notify(alert);
                continue;
            }
            store.SaveWatch(watch with{
                Failures=0,LastCheck=DateTimeOffset.Now,NextCheck=DateTimeOffset.Now+Cadence,
                Alerted=false,LastPrice=point.Price});
        }
        Changed?.Invoke();
    }

    TimeSpan Backoff(int failures)=>TimeSpan.FromTicks(Math.Min(Cadence.Ticks*2L*Math.Max(1,failures),TimeSpan.FromHours(6).Ticks));
    public TimeSpan Cadence=>TimeSpan.FromMinutes(settings.CadenceMinutes);

    void Notify(ShoppingAlert alert)
    {
        Alert?.Invoke(alert);
        if(settings.NtfyTopic.Length>0)Push(alert);
    }

    /// <summary>Notification téléphone facultative : sans sujet configuré, rien n'est envoyé.</summary>
    void Push(ShoppingAlert alert)
    {
        var topic=settings.NtfyTopic.Trim().Trim('/');
        if(topic.Length==0)return;
        _=Task.Run(async()=>
        {
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Post,$"{ntfyEndpoint}/{Uri.EscapeDataString(topic)}")
                {
                    Content=new StringContent(alert.Message,System.Text.Encoding.UTF8,"text/plain")
                };
                request.Headers.TryAddWithoutValidation("Title",Uri.EscapeDataString(alert.Title));
                request.Headers.TryAddWithoutValidation("Click",alert.Url);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var response=await http.SendAsync(request,timeout.Token);
            }
            catch(Exception e) when(e is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
            {
                // Une notification manquée ne doit pas interrompre la veille.
            }
        });
    }

    public void Start()
    {
        if(running)return;
        running=true;
        background=new CancellationTokenSource();
        _=Task.Run(async()=>
        {
            var token=background!.Token;
            try
            {
                await CheckWatchesAsync(token);
                using var timer=new PeriodicTimer(Cadence);
                while(await timer.WaitForNextTickAsync(token))await CheckWatchesAsync(token);
            }
            catch(OperationCanceledException){}
        });
    }

    public void Stop()
    {
        running=false;
        background?.Cancel();
        background?.Dispose();
        background=null;
    }

    public void Dispose()
    {
        Stop();
    }
}
