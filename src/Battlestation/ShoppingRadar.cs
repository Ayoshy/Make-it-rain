using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using Battlestation.Shopping;

namespace Battlestation;

/// <summary>Une offre prête à dessiner : le verdict, son prix et l'aperçu téléchargé.</summary>
internal sealed record RadarRow(ShoppingHit Hit,string ImagePath,bool Watched)
{
    public Product Product=>Hit.Product;
    public string Title=>Hit.Product.Brand.Length>0&&Hit.Product.Model.Length>0
        ?$"{Hit.Product.Brand} {Hit.Product.Model.ToUpperInvariant()}{(ProductIdentity.Variant(Hit.Product.Title) is {Length:>0} variant?" · "+variant:"")}":Hit.Product.Title;
    public string Shop=>Hit.Product.Shop;
    public decimal? Price=>Hit.Amount;
    public ProductFit Fit=>Hit.Assessment?.Fit??ProductFit.Unknown;
    public string FitLabel=>Hit.Assessment?.Label??"À vérifier";
    public string Reason=>string.IsNullOrWhiteSpace(Hit.Assessment?.Reason)?"Analyse indisponible":Hit.Assessment.Reason;
    public string Caveat=>Hit.Assessment?.Caveats.FirstOrDefault()??"";
    public int? CompliancePercent=>Hit.Assessment?.Criteria is {Count:>0} criteria&&criteria.Any(item=>item.State!=CriterionState.Unknown)
        ?(int)Math.Round(100d*criteria.Count(item=>item.State==CriterionState.Confirmed)/criteria.Count):null;

    /// <summary>Le lien exact de l'annonce, pour l'ouverture et pour la veille.</summary>
    public string Url=>Hit.Product.Url;
}

/// <summary>Une veille affichée : l'article suivi, son dernier prix et son image.</summary>
internal sealed record WatchRow(WatchedItem Item,string ImagePath)
{
    public Product Product=>Item.Product;
    public string Title=>Item.Product.Title;
    public string Shop=>Item.Product.Shop;
    public decimal? Price=>Item.Watch.LastPrice??Item.Product.Price;
}

/// <summary>
/// État du dock d'achat : demande en cours, offres, veille, aperçus téléchargés en
/// arrière-plan et ouverture des annonces. Le travail lourd reste dans Battlestation.Shopping.
/// </summary>
internal sealed class ShoppingRadar : IDisposable
{
    static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(12)};
    readonly IShoppingEngine engine;
    readonly string imageDirectory;
    readonly Action<string> open;
    readonly Dispatcher dispatcher;
    readonly Lock gate=new();
    readonly Lock watchGate=new();
    IReadOnlyList<RadarRow> results=[];
    IReadOnlyList<WatchRow> watching=[];
    string query="",status="Aucune recherche",error="";
    DateTimeOffset? searchEndedAt;
    long revision;
    bool searching;
    volatile bool disposed;

    public event Action? Changed;
    public event Action<ShoppingAlert>? Alert;

    public ShoppingRadar(IShoppingEngine engine,string dataDirectory,Dispatcher dispatcher,Action<string>? openUrl=null)
    {
        this.engine=engine;this.dispatcher=dispatcher;
        imageDirectory=Path.Combine(dataDirectory,"shopping-images");
        open=openUrl??DefaultOpen;
        engine.Changed+=EngineChanged;
        engine.Alert+=OnAlert;
        Reload();
    }

    public IReadOnlyList<RadarRow> Results{get{lock(gate)return results;}}
    public IReadOnlyList<WatchRow> Watchlist{get{lock(gate)return watching;}}
    public string Query{get{lock(gate)return query;}}
    public string Status
    {
        get
        {
            var activity=engine.Activity;
            lock(gate)return searching&&!string.IsNullOrWhiteSpace(activity)?activity:status;
        }
    }
    public string Error{get{lock(gate)return error;}}
    public IReadOnlyList<ShoppingProgress> Progress=>engine.Progress;
    public DateTimeOffset? SearchStartedAt=>engine.SearchStartedAt;
    public TimeSpan Elapsed
    {
        get
        {
            if(SearchStartedAt is not {} start)return TimeSpan.Zero;
            DateTimeOffset end;
            lock(gate)end=searchEndedAt??DateTimeOffset.Now;
            return end>start?end-start:TimeSpan.Zero;
        }
    }
    public bool Busy{get{lock(gate)return searching||engine.Busy;}}
    public long Revision{get{lock(gate)return revision;}}
    public ShoppingSettings Settings=>engine.Settings;

    /// <summary>Une recherche : la demande part au moteur, les aperçus suivent en fond.</summary>
    public async Task SearchAsync(string request)
    {
        if(string.IsNullOrWhiteSpace(request))return;
        request=request.Trim();
        lock(gate)
        {
            if(searching||disposed)return;
            searching=true;
            searchEndedAt=null;
            query=request;
            error="";
            status="Recherche en cours…";
            revision++;
        }
        Notify();
        try
        {
            // Même le début synchrone du moteur (cache, SQLite, analyse locale)
            // reste hors du fil WPF, ainsi que la lecture de la veille.
            var outcome=await Task.Run(()=>engine.SearchAsync(request,CancellationToken.None)).ConfigureAwait(false);
            await UpdateAsync(outcome:outcome).ConfigureAwait(false);
        }
        catch(Exception e) when(e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            lock(gate){error=e.Message;status="La recherche a échoué";revision++;}
        }
        finally
        {
            lock(gate){searching=false;searchEndedAt=DateTimeOffset.Now;revision++;}
            Notify();
        }
        _=Task.Run(LoadImages);
    }

    static string Describe(SearchOutcome outcome,int count)
    {
        var head=outcome.AnalysisNote.Length>0?outcome.AnalysisNote:count switch{0=>"Aucune offre",1=>"1 offre",_=>$"{count} offres"};
        var criteria=outcome.Spec.Required.Count>0?$" · {string.Join(", ",outcome.Spec.Required)}":"";
        return $"{head}{criteria}\n{string.Join(" · ",outcome.Sources.Select(report=>$"{report.Name} : {report.Detail}"))}";
    }

    /// <summary>Recharge la veille enregistrée pour l'afficher dès l'ouverture du dock.</summary>
    public void Reload()=>_=UpdateAsync();

    // Les lectures et mutations de veille sont sérialisées en fond : aucun verrou
    // de base n'empêche Paint de lire le dernier instantané disponible.
    Task UpdateAsync(Action? change=null,SearchOutcome? outcome=null)=>Task.Run(()=>
    {
        try
        {
            lock(watchGate)
            {
                if(disposed)return;
                change?.Invoke();
                var items=engine.Watchlist;
                var watched=items.Select(item=>item.Product.Id).ToHashSet(StringComparer.Ordinal);
                var rows=items.Select(item=>new WatchRow(item,ImageOf(item.Product.Image))).ToArray();
                lock(gate)
                {
                    watching=rows;
                    results=outcome is null
                        ?results.Select(row=>row with{Watched=watched.Contains(row.Product.Id)}).ToArray()
                        :outcome.Hits.Select(hit=>new RadarRow(hit,ImageOf(hit.Product.Image),watched.Contains(hit.Product.Id))).ToArray();
                    if(outcome is not null){error=outcome.Error;status=Describe(outcome,results.Count);}
                    revision++;
                }
            }
        }
        catch(Exception e) when(e is IOException or InvalidOperationException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            lock(gate){error=e.Message;if(outcome is not null)status="La recherche a échoué";revision++;}
        }
        Notify();
    });

    /// <summary>Bascule la veille d'une offre : le moteur la revérifiera en fond.</summary>
    public Task Watch(RadarRow row)
    {
        if(row.Watched)return Unwatch(row.Product.Id);
        var request=Query;
        return UpdateAsync(()=>engine.Watch(row.Product,row.Hit.Verdict.TargetPrice??row.Price,request));
    }

    public Task Unwatch(string productId)=>UpdateAsync(()=>
    {
        WatchRow? item;
        lock(gate)item=watching.FirstOrDefault(candidate=>candidate.Product.Id==productId);
        if(item is not null)engine.Unwatch(item.Item.Watch.Id);
    });

    public void Open(RadarRow row)=>Open(row.Url);
    public void Open(WatchRow row)=>Open(row.Product.Url);

    void Open(string url)
    {
        if(!url.StartsWith("http",StringComparison.OrdinalIgnoreCase))return;
        try{open(url);}
        catch(Exception e) when(e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            lock(gate){error=e.Message;revision++;}
            Notify();
        }
    }

    public void Apply(ShoppingSettings settings)
    {
        _=UpdateAsync(()=>engine.Apply(settings));
    }

    public void Start()
    {
        engine.Start();
        Reload();
    }

    /// <summary>Revérifie tout de suite les articles surveillés, sans attendre la cadence.</summary>
    public async Task RecheckAsync()
    {
        await Task.Run(()=>engine.CheckWatchesAsync(CancellationToken.None,force:true)).ConfigureAwait(false);
        await UpdateAsync().ConfigureAwait(false);
    }

    static void DefaultOpen(string url)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});

    string ImageOf(string imageUrl)=>imageUrl.Length==0?"":Path.Combine(imageDirectory,$"{Hash(imageUrl)}.img");

    /// <summary>Aperçus téléchargés une fois, plafonnés, sans jamais bloquer le fil d'interface.</summary>
    async Task LoadImages()
    {
        var wanted=Results.Where(row=>row.Product.Image.Length>0)
            .Select(row=>(Url:row.Product.Image,Path:ImageOf(row.Product.Image)))
            .Where(item=>!File.Exists(item.Path))
            .DistinctBy(item=>item.Path)
            .Take(6).ToArray();
        if(wanted.Length==0)return;
        Directory.CreateDirectory(imageDirectory);
        foreach(var (url,path) in wanted)
        {
            try
            {
                var bytes=await Http.GetByteArrayAsync(url);
                if(bytes.Length is >0 and <3*1024*1024)
                {
                    await File.WriteAllBytesAsync(path+".tmp",bytes);
                    File.Move(path+".tmp",path,true);
                    lock(gate)revision++;
                    Notify();
                }
            }
            catch(Exception e) when(e is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException or NotSupportedException)
            {
                // Un aperçu manquant ne remplace pas l'offre.
            }
        }
        lock(gate)revision++;
        Notify();
    }

    static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    void Notify()=>Post(()=>Changed?.Invoke());
    void EngineChanged()
    {
        // Les étapes de recherche n'attendent pas la prochaine lecture SQLite.
        lock(gate)revision++;
        Notify();
        Reload();
    }
    void OnAlert(ShoppingAlert alert)=>Post(()=>Alert?.Invoke(alert));

    /// <summary>Les événements du moteur arrivent de fils de fond : ils reviennent sur le fil WPF.</summary>
    void Post(Action action)
    {
        if(disposed)return;
        if(dispatcher.CheckAccess())action();
        else if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{if(!disposed)action();}));
    }

    public void Dispose()
    {
        disposed=true;
        engine.Changed-=EngineChanged;
        engine.Alert-=OnAlert;
        engine.Dispose();
    }
}
