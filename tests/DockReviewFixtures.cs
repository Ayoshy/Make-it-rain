using System.Windows;
using System.Windows.Controls;
using Battlestation.Shopping;
namespace Battlestation;

// Render fixtures: no backend, terminal, save files or GPU worker is started.
internal sealed record DockApp(string Name,string Path);
internal sealed record ReminderItem(string Text);
internal sealed class TerminalSession
{
    // Only the tab titles reach a dock; no host, session or command is involved.
    internal string[] Titles{get;set;}=[];
    internal bool HasTabNamed(string name)=>name.Length>1&&Titles.Any(title=>title.Contains(name,StringComparison.OrdinalIgnoreCase));
    internal void OpenCodex(string project)=>throw new InvalidOperationException("No terminal during render tests");
    internal void OpenKilo(string project)=>throw new InvalidOperationException("No terminal during render tests");
    internal void OpenCodexDeepSeek(string project)=>throw new InvalidOperationException("No terminal during render tests");
}
internal sealed class Station
{
    public string Root {get;set;}="";
    public string Assets=>System.IO.Path.Combine(Root,"assets");
    public string Data=>System.IO.Path.Combine(Root,"artifacts/validation/network-fixture");
    internal DesktopSettings Settings {get;private set;}=new(Environment.CurrentDirectory,"Aix",43.5,5.4);
    internal string TargetDate=>"2026-11-19T00:00:00+01:00";
    internal string ProjectRoot=>Settings.ProjectRoot;
    internal void ApplySettings(DesktopSettings settings,string target)=>Settings=settings;
    public List<DockApp> Apps {get;set;}=[];
    public event Action? DockChanged;
    public List<ReminderItem> Reminders {get;set;}=[];
    public DesktopLayout Layout {get;}=new();
    internal ProjectSignals Projects {get;}=new();
    internal TerminalSession? Terminal {get;set;}
    // Fixture only: the desk supplies the track artwork, there is no media session here.
    internal System.Windows.Media.Imaging.BitmapSource? Cover=>CoverArt;
    internal System.Windows.Media.Imaging.BitmapSource? CoverArt{get;set;}
    internal float[] MusicBands{get;set;}=new float[12];
    internal double AudioIntensity=>.55;
    public void SaveReminders(IEnumerable<ReminderItem> reminders)=>Reminders=reminders.ToList();
    public void SaveApps(List<DockApp> apps){Apps=apps;DockChanged?.Invoke();}
    public void Launch(DockApp app)=>throw new InvalidOperationException("No application launch during render tests");
    internal void ShowReserve()=>throw new InvalidOperationException("No reserve window during render tests");
    internal void OpenKilo(string project)=>throw new InvalidOperationException("No terminal during render tests");
    internal void OpenCodexDeepSeek(string project)=>throw new InvalidOperationException("No terminal during render tests");
    // Radar d'achat : moteur simulé, aucune requête réseau ni fenêtre pendant le rendu.
    internal static readonly List<string> Opened=[];
    internal ShoppingRadar Shopping{get;}=new(new ShoppingFixtureEngine(),System.IO.Path.GetTempPath(),
        System.Windows.Threading.Dispatcher.CurrentDispatcher,url=>Opened.Add(url));
}

/// <summary>Moteur d'achat simulé : deux offres fixes, une veille, aucune source réelle.</summary>
internal sealed class ShoppingFixtureEngine : IShoppingEngine
{
    static readonly Product Fridge=new("fixture:1","fixture","https://exemple.fr/frigo-1","Réfrigérateur combiné LISTO RCDL180 300 L NoFrost blanc","LISTO","RCDL180","","Boulanger",349m);
    static readonly Product Top=new("fixture:2","fixture","https://exemple.fr/frigo-2","Réfrigérateur top ESSENTIELB ERT85 250 L","ESSENTIELB","ERT85","","Rue du Commerce",429m);
    readonly List<WatchedItem> watches=[];
    public bool Busy{get;private set;}
    public IReadOnlyList<SourceReport> Sources{get;private set;}=[];
    public IReadOnlyList<WatchedItem> Watchlist{get=>watches;}
    public ShoppingSettings Settings{get;}=ShoppingSettings.Default;
    public event Action? Changed;
    public event Action<ShoppingAlert>? Alert;
    public Task<SearchOutcome> SearchAsync(string request,CancellationToken cancellation)
    {
        Sources=[new("fixture","Boutique simulée",SourceState.Ok,2,"2 offres")];
        var spec=new ShoppingSpec("réfrigérateur",[],800m,["no frost","300 L","blanc"],[],request);
        ShoppingHit[] hits=
        [
            new(Fridge,new PricePoint(Fridge.Id,DateTimeOffset.Now,349m,"EUR",true,"fixture"),
                new Verdict(VerdictDecision.Acheter,.82,["349,00 € chez Boulanger · plancher 90 j 359,00 €","Dans le budget"],["https://exemple.fr/frigo-1"],356m),[Alternative()],3,3),
            new(Top,new PricePoint(Top.Id,DateTimeOffset.Now,429m,"EUR",true,"fixture"),
                new Verdict(VerdictDecision.Attendre,.6,["429,00 € chez Rue du Commerce · plancher 90 j 379,00 €","Soldes d'été dans 9 j"],["https://exemple.fr/frigo-2"],386m),[],1,3)
        ];
        Changed?.Invoke();
        return Task.FromResult(new SearchOutcome(hits,Sources,spec,request));
    }
    static string Alternative()=>"Rue du Commerce 379,00 €";
    public void Watch(Product product,decimal? targetPrice,string request)
    {
        watches.Add(new(new WatchItem($"watch:{product.Id}",request,product.Id,targetPrice,DateTimeOffset.Now,DateTimeOffset.Now,DateTimeOffset.Now.AddMinutes(30),0,false,product.Price,product.Title,product.Shop),product));
        Changed?.Invoke();
    }
    public void Unwatch(string watchId){watches.RemoveAll(item=>item.Watch.Id==watchId);Changed?.Invoke();}
    public bool IsWatched(string productId)=>watches.Any(item=>item.Product.Id==productId);
    public Task CheckWatchesAsync(CancellationToken cancellation)
    {
        if(watches.Count>0)Alert?.Invoke(new ShoppingAlert("Prix cible atteint · Boulanger","Réfrigérateur combiné LISTO · 349,00 € (achète)","https://exemple.fr/frigo-1",349m));
        return Task.CompletedTask;
    }
    public void Apply(ShoppingSettings settings){_=settings;Changed?.Invoke();}
    public void Start(){}
    public void Stop(){}
    public void Dispose(){}
}
internal sealed class DesktopLayout
{
    public static double DockHeight(int apps)=>Math.Max(1,Math.Ceiling(apps/6d))*88+28;
    public void Save()=>throw new InvalidOperationException("No disk writes during render tests");
}
internal static class Native
{
    internal static readonly Dictionary<int,Rect> Panels=[];
    internal static readonly Dictionary<string,string> Values=new(StringComparer.OrdinalIgnoreCase);
    internal static readonly List<string> Commands=[];
    internal static void BackgroundPanel(int slot,float x,float y,float w,float h)=>Panels[slot]=new Rect(x,y,w,h);
    internal static string Read(string key)=>Values.GetValueOrDefault(key,"");
    internal static void DeskCommand(string command)=>Commands.Add(command);
    internal static void BackgroundAudio(float low,float mid,float high,float intensity){}
}
internal static class OverlayStyle
{
    public static Button Button(string text,Action action)=>new(){Content=text};
    public static TextBlock Text(string text,double size,string? color=null)=>new(){Text=text,FontSize=size};
    public static Border Frame(UIElement child)=>new(){Child=child};
    public static void Apply(Window window)=>throw new InvalidOperationException("No modal during render tests");
    public static void Reveal(Window window,bool animate)=>throw new InvalidOperationException("No modal during render tests");
    public static void Place(Window window)=>throw new InvalidOperationException("No modal during render tests");
}
