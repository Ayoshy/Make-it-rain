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
    public string Error=>"";
    public string M(string key)=>Native.Read(key);
    public double N(string key)=>double.TryParse(M(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double value)?value:double.NaN;
    public void Command(string command)=>throw new InvalidOperationException("No backend commands during render tests");
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
    internal double AudioIntensity=>.55;
    public void SaveReminders(IEnumerable<ReminderItem> reminders)=>Reminders=reminders.ToList();
    public void SaveApps(List<DockApp> apps){Apps=apps;DockChanged?.Invoke();}
    public void Launch(DockApp app)=>throw new InvalidOperationException("No application launch during render tests");
    internal void ShowReserve()=>throw new InvalidOperationException("No reserve window during render tests");
    internal void OpenKilo(string project)=>throw new InvalidOperationException("No terminal during render tests");
    internal void OpenCodexDeepSeek(string project)=>throw new InvalidOperationException("No terminal during render tests");
    // Radar d'achat : moteur simulé, aucune requête réseau ni fenêtre pendant le rendu.
    internal static readonly List<string> Opened=[];
    internal ShoppingFixtureEngine ShoppingEngine{get;}=new();
    internal ShoppingRadar Shopping{get;}
    internal ShoppingTabs ShoppingTabs{get;}
    internal SerperKeyStore Serper{get;}=new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Battlestation-Serper-fixture-"+Guid.NewGuid().ToString("N")));
    internal List<ShoppingFixtureEngine> ExtraShoppingEngines{get;}=[];
    public Station()
    {
        Shopping=new(ShoppingEngine,System.IO.Path.GetTempPath(),System.Windows.Threading.Dispatcher.CurrentDispatcher,url=>Opened.Add(url));
        ShoppingTabs=new(Shopping,()=>{var engine=new ShoppingFixtureEngine();ExtraShoppingEngines.Add(engine);return new(engine,System.IO.Path.GetTempPath(),System.Windows.Threading.Dispatcher.CurrentDispatcher,url=>Opened.Add(url));});
    }
}

/// <summary>Moteur d'achat simulé : trois offres fixes, une veille, aucune source réelle.</summary>
internal sealed class ShoppingFixtureEngine : IShoppingEngine
{
    static readonly Product Fridge=new("fixture:1","fixture","https://exemple.fr/frigo-1","Réfrigérateur combiné LISTO RCDL180 300 L NoFrost blanc","LISTO","RCDL180","","Boulanger",349m);
    static readonly Product Top=new("fixture:2","fixture","https://exemple.fr/frigo-2","Réfrigérateur top ESSENTIELB ERT85 250 L","ESSENTIELB","ERT85","","Rue du Commerce",429m);
    static readonly Product Unchecked=new("fixture:3","fixture","https://exemple.fr/frigo-3","Réfrigérateur LG GBP 300 L blanc","LG","GBP","","Boutique",599m);
    readonly List<WatchedItem> watches=[];
    public bool AssessmentAvailable{get;set;}=true;
    public string? LongReason{get;set;}
    public string SourceNotice{get;set;}="";
    public string ReasoningEffort{get;private set;}="max";
    public void SetReasoningEffort(string effort)=>ReasoningEffort=effort;
    public bool Busy{get;private set;}
    public string Activity{get;private set;}="";
    public IReadOnlyList<ShoppingProgress> Progress{get;private set;}=[];
    public DateTimeOffset? SearchStartedAt{get;private set;}
    public TaskCompletionSource? SearchHold{get;set;}
    public string SearchFailure{get;set;}="";
    public IReadOnlyList<SourceReport> Sources{get;private set;}=[];
    public IReadOnlyList<WatchedItem> Watchlist{get=>watches;}
    public ShoppingSettings Settings{get;}=ShoppingSettings.Default;
    public event Action? Changed;
    public event Action<ShoppingAlert>? Alert;
    public async Task<SearchOutcome> SearchAsync(string request,CancellationToken cancellation)
    {
        Busy=true;SearchStartedAt=DateTimeOffset.Now;Progress=[];
        PublishProgress("Étude de la demande");
        try{if(SearchHold is {} hold)await hold.Task.WaitAsync(cancellation).ConfigureAwait(false);}
        catch{Busy=false;Changed?.Invoke();throw;}
        Sources=[new("fixture","Boutique simulée",SourceState.Ok,3,"3 offres"+(SourceNotice.Length>0?" · "+SourceNotice:""),Partial:SourceNotice.Length>0)];
        var spec=new ShoppingSpec("réfrigérateur",[],800m,["no frost","300 L","blanc"],[],request);
        if(SearchFailure.Length>0)
        {
            Sources=[new("web","Recherche web",SourceState.Failed,0,SearchFailure)];
            PublishProgress(SearchFailure);Busy=false;Changed?.Invoke();
            return new([],Sources,spec,request);
        }
        ShoppingHit[] hits=
        [
            new(Fridge,new PricePoint(Fridge.Id,DateTimeOffset.Now,349m,"EUR",true,"fixture"),
                new Verdict(VerdictDecision.Acheter,.82,["349,00 € chez Boulanger · plancher 90 j 359,00 €","Dans le budget"],["https://exemple.fr/frigo-1"],356m,true),[Alternative()],3,3,
                AssessmentAvailable?new(Fridge.Id,ProductFit.Recommended,LongReason??"300 L, froid ventilé et finition blanche dans le budget.",["La fiche indique 300 L et NoFrost.","Finition blanche sur la fiche Boulanger."],["Dimensions à confirmer avant livraison."],
                    [new("300 L",CriterionState.Confirmed,"La fiche indique 300 L et NoFrost."),new("no frost",CriterionState.Confirmed,"La fiche indique 300 L et NoFrost."),new("blanc",CriterionState.Confirmed,"Finition blanche sur la fiche Boulanger.")]):null),
            new(Top,new PricePoint(Top.Id,DateTimeOffset.Now,429m,"EUR",true,"fixture"),
                new Verdict(VerdictDecision.Attendre,.35,["429,00 € chez Rue du Commerce · historique court (1 relevé)"],["https://exemple.fr/frigo-2"],386m),[],1,3,
                AssessmentAvailable?new(Top.Id,ProductFit.Possible,"Critères vérifiés, entretien à comparer.",["Fiche simulée : critères demandés respectés."],["Niveau sonore non documenté."]):null),
            new(Unchecked,new PricePoint(Unchecked.Id,DateTimeOffset.Now,599m,"EUR",true,"fixture"),Verdict.Unknown,[],1,3,
                AssessmentAvailable?new(Unchecked.Id,ProductFit.Unknown,"Finition confirmée, volume et froid à vérifier.",["Finition blanche."],["Volume et type de froid non documentés."],
                    [new("blanc",CriterionState.Confirmed,"Finition blanche."),new("300 L",CriterionState.Unknown,""),new("no frost",CriterionState.Unknown,"")]):null)
        ];
        PublishProgress("3 produits comparés");Busy=false;Changed?.Invoke();
        return new SearchOutcome(hits,Sources,spec,request);
    }
    public void PublishProgress(string message)
    {
        Activity=message;Progress=Progress.Append(new ShoppingProgress(DateTimeOffset.Now,message)).ToArray();Changed?.Invoke();
    }
    public void SeedProgress(params string[] messages)
    {
        SearchStartedAt=DateTimeOffset.Now.AddSeconds(-messages.Length*3);
        Progress=messages.Select((message,index)=>new ShoppingProgress(SearchStartedAt.Value.AddSeconds(index*3),message)).ToArray();
        Activity=messages[^1];Changed?.Invoke();
    }
    static string Alternative()=>"Rue du Commerce 379,00 €";
    public void Watch(Product product,decimal? targetPrice,string request)
    {
        watches.Add(new(new WatchItem($"watch:{product.Id}",request,product.Id,targetPrice,DateTimeOffset.Now,DateTimeOffset.Now,DateTimeOffset.Now.AddMinutes(30),0,false,product.Price,product.Title,product.Shop),product));
        Changed?.Invoke();
    }
    public void Unwatch(string watchId){watches.RemoveAll(item=>item.Watch.Id==watchId);Changed?.Invoke();}
    public bool IsWatched(string productId)=>watches.Any(item=>item.Product.Id==productId);
    public Task CheckWatchesAsync(CancellationToken cancellation,bool force=false)
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
    public static Button Button(string text,Action action)
    {
        var button=new Button{Content=text};button.Click+=(_,_)=>action();return button;
    }
    public static TextBlock Text(string text,double size,string? color=null)=>new(){Text=text,FontSize=size};
    public static Border Frame(UIElement child)=>new(){Child=child};
    public static void Apply(Window window){window.WindowStartupLocation=WindowStartupLocation.CenterScreen;}
    public static void Reveal(Window window,bool animate)=>throw new InvalidOperationException("No modal during render tests");
    public static void Place(Window window){}
}
