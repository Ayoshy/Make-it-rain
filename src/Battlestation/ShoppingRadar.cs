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
    public string Title=>Hit.Product.Title;
    public string Shop=>Hit.Product.Shop;
    public decimal? Price=>Hit.Amount;

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
    IReadOnlyList<RadarRow> results=[];
    IReadOnlyList<WatchRow> watching=[];
    string query="",status="Aucune recherche",error="";
    long revision;
    bool disposed;

    public event Action? Changed;
    public event Action<ShoppingAlert>? Alert;

    public ShoppingRadar(IShoppingEngine engine,string dataDirectory,Dispatcher dispatcher,Action<string>? openUrl=null)
    {
        this.engine=engine;this.dispatcher=dispatcher;
        imageDirectory=Path.Combine(dataDirectory,"shopping-images");
        open=openUrl??DefaultOpen;
        engine.Changed+=Notify;
        engine.Alert+=alert=>Post(()=>Alert?.Invoke(alert));
        Reload();
    }

    public IReadOnlyList<RadarRow> Results{get{lock(gate)return results;}}
    public IReadOnlyList<WatchRow> Watchlist{get{lock(gate)return watching;}}
    public string Query{get{lock(gate)return query;}}
    public string Status{get{lock(gate)return status;}}
    public string Error{get{lock(gate)return error;}}
    public bool Busy=>engine.Busy;
    public long Revision{get{lock(gate)return revision;}}
    public ShoppingSettings Settings=>engine.Settings;

    /// <summary>Une recherche : la demande part au moteur, les aperçus suivent en fond.</summary>
    public async Task SearchAsync(string request)
    {
        if(string.IsNullOrWhiteSpace(request))return;
        lock(gate)
        {
            query=request.Trim();
            error="";
            status="Recherche en cours…";
            revision++;
        }
        Notify();
        try
        {
            var outcome=await engine.SearchAsync(query,CancellationToken.None);
            var rows=outcome.Hits.Select(hit=>new RadarRow(hit,ImageOf(hit.Product.Image),engine.IsWatched(hit.Product.Id))).ToArray();
            lock(gate)
            {
                results=rows;
                error=outcome.Error;
                status=Describe(outcome,rows.Length);
                revision++;
            }
        }
        catch(Exception e) when(e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            lock(gate){error=e.Message;status="La recherche a échoué";revision++;}
        }
        Notify();
        _=LoadImages();
    }

    static string Describe(SearchOutcome outcome,int count)
    {
        var head=count switch{0=>"Aucune offre",1=>"1 offre",_=>$"{count} offres"};
        var criteria=outcome.Spec.Required.Count>0?$" · {string.Join(", ",outcome.Spec.Required)}":"";
        return $"{head}{criteria}\n{string.Join(" · ",outcome.Sources.Select(report=>$"{report.Name} : {report.Detail}"))}";
    }

    /// <summary>Recharge la veille enregistrée pour l'afficher dès l'ouverture du dock.</summary>
    public void Reload()
    {
        lock(gate)
        {
            watching=engine.Watchlist.Select(item=>new WatchRow(item,ImageOf(item.Product.Image))).ToArray();
            revision++;
        }
        Notify();
    }

    /// <summary>Bascule la veille d'une offre : le moteur la revérifiera en fond.</summary>
    public void Watch(RadarRow row)
    {
        if(row.Watched){Unwatch(row.Product.Id);return;}
        engine.Watch(row.Product,row.Hit.Verdict.TargetPrice??row.Price,Query);
        lock(gate)
        {
            results=results.Select(item=>item.Product.Id==row.Product.Id?item with{Watched=true}:item).ToArray();
            revision++;
        }
        Reload();
    }

    public void Unwatch(string productId)
    {
        var item=engine.Watchlist.FirstOrDefault(candidate=>candidate.Product.Id==productId);
        if(item is null)return;
        engine.Unwatch(item.Watch.Id);
        lock(gate)
        {
            results=results.Select(row=>row.Product.Id==productId?row with{Watched=false}:row).ToArray();
            revision++;
        }
        Reload();
    }

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
        engine.Apply(settings);
        Reload();
    }

    public void Start()
    {
        engine.Start();
        Reload();
    }

    /// <summary>Revérifie tout de suite les articles surveillés, sans attendre la cadence.</summary>
    public async Task RecheckAsync()
    {
        await engine.CheckWatchesAsync(CancellationToken.None);
        Reload();
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

    /// <summary>Les événements du moteur arrivent de fils de fond : ils reviennent sur le fil WPF.</summary>
    void Post(Action action)
    {
        if(disposed)return;
        if(dispatcher.CheckAccess())action();
        else dispatcher.BeginInvoke(DispatcherPriority.Background,action);
    }

    public void Dispose()
    {
        disposed=true;
        engine.Dispose();
    }
}
