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
    readonly string settingsPath;
    readonly string ntfyEndpoint;
    readonly Lock gate=new();
    readonly SemaphoreSlim watchGate=new(1,1);
    readonly List<SourceReport> reports=[];
    readonly List<ShoppingProgress> progress=[];
    DateTimeOffset? searchStartedAt;
    CancellationTokenSource? background;
    ShoppingSpecParser parser;
    ShoppingAdvisor advisor;
    ShoppingSettings settings;
    readonly ShoppingReasoning reasoning;
    string activity="";
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
        string ntfyEndpoint="https://ntfy.sh",ShoppingAdvisor? advisor=null,ShoppingReasoning? reasoning=null)
    {
        this.store=store;this.sources=sources;this.settings=settings.Validate();
        this.reasoning=reasoning??new();
        this.engine=engine??new VerdictEngine();
        this.calendar=calendar??PromoCalendar.Load(Path.Combine(dataDirectory,ShoppingSettingsStore.EventsFileName));
        this.http=http??Shared;
        this.parser=parser??new ShoppingSpecParser(Llm(this.settings,this.http,this.reasoning));
        this.advisor=advisor??new ShoppingAdvisor(Llm(this.settings,this.http,this.reasoning));
        settingsPath=Path.Combine(dataDirectory,ShoppingSettingsStore.FileName);
        this.ntfyEndpoint=ntfyEndpoint.TrimEnd('/');
        foreach(var source in sources.OfType<IShoppingProgressSource>())source.Progress+=SourceProgress;
    }

    static readonly HttpClient Shared=CreateHttp();

    static HttpClient CreateHttp()
    {
        var client=new HttpClient{Timeout=Timeout.InfiniteTimeSpan};
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>Deux appels distincts : lecture de la demande, puis comparaison des fiches.</summary>
    static ILlmClient? Llm(ShoppingSettings settings,HttpClient http,ShoppingReasoning reasoning)=>settings.Provider switch
    {
        "deepseek"=>DeepSeekClient.Create(http,settings,reasoning),
        _=>new OllamaClient(http,settings.Endpoint,settings.Model)
    };

    public bool Busy=>busy;
    public string Activity{get{lock(gate)return activity;}}
    public IReadOnlyList<ShoppingProgress> Progress{get{lock(gate)return progress.ToArray();}}
    public DateTimeOffset? SearchStartedAt{get{lock(gate)return searchStartedAt;}}
    public ShoppingSettings Settings=>settings;
    public string ReasoningEffort=>reasoning.Effort;
    public void SetReasoningEffort(string effort)
    {
        lock(gate){if(busy)throw new InvalidOperationException("La réflexion ne se change pas pendant une recherche.");reasoning.Effort=effort;}
    }
    public IReadOnlyList<SourceReport> Sources{get{lock(gate)return reports.ToArray();}}
    public IReadOnlyList<WatchedItem> Watchlist=>store.Watches().Select(item=>new WatchedItem(item.Watch,item.Product)).ToArray();

    /// <summary>Applique et conserve un nouveau fournisseur ou de nouveaux seuils, sans redémarrer le bureau.</summary>
    public void Apply(ShoppingSettings next)
    {
        var validated=next.Validate();
        bool switched=validated.Provider!=settings.Provider||validated.Model!=settings.Model||validated.Endpoint!=settings.Endpoint;
        settings=validated;
        if(switched)
        {
            parser=new ShoppingSpecParser(Llm(settings,http,reasoning));
            advisor=new ShoppingAdvisor(Llm(settings,http,reasoning));
        }
        ShoppingSettingsStore.Save(settingsPath,settings);
        Changed?.Invoke();
    }

    /// <summary>Une recherche complète : demande, boutiques, relevés, verdicts, historique enregistré.</summary>
    public async Task<SearchOutcome> SearchAsync(string request,CancellationToken cancellation)
    {
        lock(gate)
        {
            if(busy)throw new InvalidOperationException("Une recherche est déjà en cours.");
            busy=true;
            progress.Clear();searchStartedAt=DateTimeOffset.UtcNow;
        }
        try
        {
            SetActivity("Analyse de la demande…");
            var spec=await parser.ParseAsync(request,cancellation);
            cancellation.ThrowIfCancellationRequested();
            if(spec.Query.Length==0)
            {
                SetActivity("Aucun produit reconnu dans la demande");
                return SearchOutcome.Failed(request,spec,[new("","Demande",SourceState.Empty,0,"Aucun produit reconnu dans la demande")]);
            }
            var found=new List<Product>();
            var status=new List<SourceReport>();
            // La recherche générale pilote la découverte. Les anciens adaptateurs
            // restent disponibles pour les articles déjà en veille.
            var discovery=sources.Any(source=>source.Id=="web")?sources.Where(source=>source.Id=="web"):sources;
            foreach(var source in discovery)
            {
                SetActivity($"Recherche · {source.Name}…");
                try
                {
                    var products=(await source.SearchAsync(spec,settings.MaxResults*3,cancellation))
                        .Where(product=>source.Id=="web"||ShoppingRelevance.Matches(product.Title,spec)).ToArray();
                    status.Add(new(source.Id,source.Name,products.Length==0?SourceState.Empty:SourceState.Ok,products.Length,
                        (products.Length==0?"aucune offre":$"{products.Length} offres")+(source.Notice.Length>0?$" · {source.Notice}":""),Partial:source.Notice.Length>0));
                    foreach(var product in products)found.Add(product);
                }
                catch(PriceSourceUnavailableException e)
                {
                    SetActivity(e.Message);
                    status.Add(new(source.Id,source.Name,SourceState.Failed,0,e.Message));
                }
                catch(HttpRequestException e)
                {
                    SetActivity($"{source.Name} injoignable ({e.StatusCode})");
                    status.Add(new(source.Id,source.Name,SourceState.Failed,0,$"{source.Name} injoignable ({e.StatusCode})"));
                }
            }
            var (hits,note)=await Rank(found,spec,cancellation);
            lock(gate)
            {
                reports.Clear();reports.AddRange(status);
                foreach(var promo in calendar.Custom)store.SavePromoEvents([promo]);
            }
            SetActivity(status.Count>0&&status.All(source=>source.State==SourceState.Failed)?"Recherche arrêtée : source web indisponible"
                :hits.Count==0?"Recherche terminée : aucun produit retenu":$"Recherche terminée : {hits.Count} produit{(hits.Count>1?"s":"")} retenu{(hits.Count>1?"s":"")}");
            return new(hits,status,spec,request,note);
        }
        catch(OperationCanceledException)
        {
            SetActivity("Recherche annulée ou délai dépassé");
            throw;
        }
        catch(Exception)
        {
            SetActivity("La recherche n'a pas abouti");
            throw;
        }
        finally
        {
            lock(gate){busy=false;activity="";}
            Changed?.Invoke();
        }
    }

    /// <summary>Regroupe les mêmes appareils entre boutiques, garde l'offre la moins chère et note son verdict.</summary>
    async Task<(IReadOnlyList<ShoppingHit> Hits,string Note)> Rank(IReadOnlyList<Product> found,ShoppingSpec spec,CancellationToken cancellation)
    {
        var groups=new Dictionary<string,List<Product>>(StringComparer.Ordinal);
        foreach(var product in found)
        {
            var key=product.Identity;
            if(key.Length==0)key=product.Id;
            if(!groups.TryGetValue(key,out var list))groups[key]=list=[];
            list.Add(product);
        }
        // La comparaison reçoit plusieurs boutiques et niveaux de gamme. Le prix
        // ne sert qu'à choisir la meilleure offre du même appareil, pas les appareils.
        var choices=groups.Values.Select(group=>(
            Best:group.Where(product=>product.Price is>0&&(spec.MaxPrice is null||product.Price<=spec.MaxPrice)).OrderBy(product=>product.Price).FirstOrDefault()
                ??group.FirstOrDefault(product=>product.Price is null)??group.OrderBy(product=>product.Price).First(),Group:group))
            .OrderByDescending(choice=>Matches(choice.Best,spec,out _)).ToArray();
        var queues=choices.GroupBy(choice=>choice.Best.Source)
            .Select(group=>new Queue<(Product Best,List<Product> Group)>(group)).ToArray();
        var selected=new List<(Product Best,List<Product> Group)>();
        while(selected.Count<12&&queues.Any(queue=>queue.Count>0))
            foreach(var queue in queues)
                if(queue.Count>0&&selected.Count<12)selected.Add(queue.Dequeue());
        if(selected.Count==0)return ([],"");

        SetActivity($"Lecture de {selected.Count} fiches produit…");
        using var detailsDeadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        detailsDeadline.CancelAfter(TimeSpan.FromSeconds(60));
        var candidates=await Task.WhenAll(selected.Select(async choice=>
        {
            // Le prix le plus intéressant ne doit pas effacer une fiche plus
            // complète pour cette même référence. Son URL reste citée séparément.
            var descriptions=await Task.WhenAll(choice.Group.Take(3).Select(async product=>
            {
                var source=sources.FirstOrDefault(source=>source.Id==product.Source);
                try{return source is null?null:await source.GetDetailsAsync(product,detailsDeadline.Token);}
                catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){return null;}
                catch(Exception e) when(e is PriceSourceUnavailableException or HttpRequestException or IOException or System.Text.Json.JsonException){return null;}
            }));
            var details=descriptions.Where(item=>item is not null).OrderByDescending(item=>item!.Text.Length).FirstOrDefault();
            return new ProductCandidate(choice.Best,details);
        }));
        cancellation.ThrowIfCancellationRequested();
        SetActivity("Comparaison des produits avec DeepSeek…");
        var assessments=await advisor.ReviewAsync(spec,candidates,cancellation);
        // Les pistes sont conservées séparément dans l'interface, sans devenir
        // des recommandations quand leurs critères essentiels restent inconnus.
        var retained=assessments.Where(item=>item.Fit!=ProductFit.Unsuitable)
            .OrderBy(item=>item.Fit switch{ProductFit.Recommended=>0,ProductFit.Possible=>1,_=>2})
            .Take(settings.MaxResults).ToArray();
        var ranked=new List<ShoppingHit>();
        foreach(var assessment in retained)
        {
            var (best,group)=selected.First(choice=>choice.Best.Id==assessment.ProductId);
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
            ranked.Add(new(best,point??(best.Price is {} price?new PricePoint(best.Id,DateTimeOffset.Now,price,"EUR",true,best.Source):null),verdict,alternatives,criteria,total,assessment));
        }
        int pending=retained.Count(item=>item.Fit==ProductFit.Unknown),confirmed=retained.Length-pending;
        return (ranked,retained.Length==0?"Aucun modèle confirmé dans les critères et le budget"
            :pending>0?$"{confirmed} choix confirmé{(confirmed>1?"s":"")} · {pending} piste{(pending>1?"s":"")} à vérifier":"");
    }

    void SetActivity(string value)
    {
        lock(gate)
        {
            activity=value;
            if(progress.Count==0||progress[^1].Message!=value)
            {
                progress.Add(new(DateTimeOffset.UtcNow,value));
            }
        }
        Changed?.Invoke();
    }

    void SourceProgress(string value)
    {
        if(busy)SetActivity(value);
    }

    /// <summary>Compte les critères obligatoires présents dans le titre, accents et espaces ignorés.</summary>
    static int Matches(Product product,ShoppingSpec spec,out int total)
    {
        total=spec.Required.Count;
        if(total==0)return 0;
        var title=ShoppingText.Compact(product.Title);
        return spec.Required.Count(term=>title.Contains(ShoppingText.Compact(term),StringComparison.Ordinal));
    }

    /// <summary>Le prix vient de l'offre lue ; le modèle ne le remplace jamais.</summary>
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
    public async Task CheckWatchesAsync(CancellationToken cancellation,bool force=false)
    {
        await watchGate.WaitAsync(cancellation);
        try{await CheckWatchesCoreAsync(cancellation,force);}
        finally{watchGate.Release();}
    }

    async Task CheckWatchesCoreAsync(CancellationToken cancellation,bool force)
    {
        var today=DateOnly.FromDateTime(DateTime.Today);
        foreach(var (watch,product) in store.Watches())
        {
            if(!force&&watch.NextCheck>DateTimeOffset.Now)continue;
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
            bool target=point.Usable&&watch.TargetPrice is {} goal&&point.Price is {} price&&price<=goal;
            bool drop=point.Usable&&previous?.Price is {} before&&point.Price is {} now&&now<=before*0.95m;
            bool back=previous is not null&&!previous.InStock&&point.InStock;
            // La cible reste atteinte entre deux passages ; baisse et retour en stock
            // sont des transitions, dédupliquées par le relevé précédent.
            if((target&&!watch.Alerted)||drop||back)
            {
                var alert=new ShoppingAlert(
                    target?$"Prix cible atteint · {product.Shop}":back?$"De nouveau en stock · {product.Shop}":$"Baisse de prix · {product.Shop}",
                    $"{product.Title} · {ShoppingText.Money(point.Price)} ({verdict.Label})",
                    product.Url,point.Price);
                store.SaveWatch(watch with{Alerted=target,Failures=0,LastCheck=DateTimeOffset.Now,NextCheck=DateTimeOffset.Now+Cadence,LastPrice=point.Price});
                Notify(alert);
                continue;
            }
            store.SaveWatch(watch with{
                Failures=0,LastCheck=DateTimeOffset.Now,NextCheck=DateTimeOffset.Now+Cadence,
                Alerted=target,LastPrice=point.Price});
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
        foreach(var source in sources.OfType<IShoppingProgressSource>())source.Progress-=SourceProgress;
        Stop();
    }
}
