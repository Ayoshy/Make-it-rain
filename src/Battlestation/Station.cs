using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Battlestation.Core;
using Battlestation.Shopping;

namespace Battlestation;
internal sealed record DockApp(string Name,string Path);
internal sealed record ReminderItem(string Text);
internal sealed class Station : IDisposable
{
    public string Root {get;}
    public string Data {get;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Battlestation");
    public string Assets=>Path.Combine(Root,"assets");
    public DesktopBackend Backend {get;}
    public List<DockApp> Apps {get;private set;}=[];
    public List<ReminderItem> Reminders {get;private set;}=[];
    public DesktopLayout Layout {get;}
    /// <summary>Radar d'achat : recherche multi-boutiques, verdict et veille des prix.</summary>
    internal ShoppingRadar Shopping {get;}
    readonly ShoppingStore shoppingStore;
    internal ProjectSignals Projects {get;}
    internal MediaReserve Reserve {get;}
    internal event Action? ReserveRequested;
    internal bool ClipboardRegistered {get;set;}
    internal void ShowReserve()=>ReserveRequested?.Invoke();
    public string Codex {get;}
    public string Kilo {get;}
    public string ProjectRoot=>Settings.ProjectRoot;
    public DesktopSettings Settings {get;private set;}
    internal bool? PreviewReactiveAudio {get;set;}
    internal double? PreviewAudioIntensity {get;set;}
    // Published by the music dock: the aquarium follows the same spectrum instead
    // of opening a second audio capture.
    internal float[] MusicBands{get;set;}=new float[12];
    internal bool ReactiveAudio=>PreviewReactiveAudio??Settings.ReactiveAudio;
    internal double AudioIntensity=>PreviewAudioIntensity??Settings.AudioIntensity;
    public string TargetDate {get;private set;}="";
    public string Error {get;private set;}="";
    public BitmapSource? Cover {get;private set;}
    byte[]? coverBytes;
    public event Action? DockChanged;
    public event Action? SettingsChanged;
    public TerminalSession? Terminal {get;set;}
    public Station(string root)
    {
        Root=root;Directory.CreateDirectory(Data);
        Projects=new ProjectSignals();
        Reserve=new MediaReserve(Path.Combine(Data,"media-reserve.json"));
        using var settings=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"desk/settings.json")));
        var s=settings.RootElement;Codex=Environment.ExpandEnvironmentVariables(s.GetProperty("codex").GetString()!);
        Kilo=s.TryGetProperty("kilo",out var kilo)?Environment.ExpandEnvironmentVariables(kilo.GetString()!):"kilo";
        var weather=s.GetProperty("weather");
        var defaults=new DesktopSettings(Environment.ExpandEnvironmentVariables(s.GetProperty("projectRoot").GetString()!),weather.GetProperty("city").GetString()!,weather.GetProperty("latitude").GetDouble(),weather.GetProperty("longitude").GetDouble());
        Settings=DesktopSettings.Load(Path.Combine(Data,"preferences.json"),defaults);
        DesktopTheme.Select(Settings.ThemeId,false);
        File.WriteAllLines(Path.Combine(Data,"desk.ini"),Settings.DeskIni(),Encoding.Unicode);
        Native.DeskStart(Path.Combine(Data,"desk.ini"));
        Backend=new DesktopBackend(Data);
        var targetPath=Path.Combine(Data,"target.txt");
        if(!File.Exists(targetPath))
        {
            if(s.TryGetProperty("targetDate",out var target)&&target.ValueKind==JsonValueKind.String)
                File.WriteAllText(targetPath,target.GetString()!);
        }
        TargetDate=File.Exists(targetPath)?File.ReadAllText(targetPath):"";
        Backend.Command("Target:"+TargetDate);
        var appsPath=Path.Combine(Data,"apps.json");
        if(!File.Exists(appsPath))File.Copy(Path.Combine(root,"dock/apps.json"),appsPath);
        Apps=JsonSerializer.Deserialize<List<DockApp>>(File.ReadAllText(appsPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
        var remindersPath=Path.Combine(Data,"reminders.json");
        if(File.Exists(remindersPath))
            try{Reminders=JsonSerializer.Deserialize<List<ReminderItem>>(File.ReadAllText(remindersPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];}
            catch(JsonException){Reminders=[];}
        Layout=new DesktopLayout(Path.Combine(Data,"layout.json"),Apps.Count);
        shoppingStore=new ShoppingStore(Path.Combine(Data,"shopping.db"));
        var shoppingFile=Path.Combine(Data,ShoppingSettingsStore.FileName);
        // Le fichier existe dès le premier lancement : les seuils et le fournisseur
        // se règlent sans chercher où ils sont écrits.
        try{if(!File.Exists(shoppingFile))ShoppingSettingsStore.Save(shoppingFile,ShoppingSettings.Default);}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
        var shoppingSettings=ShoppingSettingsStore.Load(shoppingFile);
        var http=new HttpClient{Timeout=Timeout.InfiniteTimeSpan};
        var options=new PriceSourceOptions(Path.Combine(Data,"shopping-cache"));
        List<IPriceSource> sources=shoppingSettings.Enabled
            ?[new RueDuCommerceSource(http,options),new BoulangerSource(http,options)]
            :[];
        Shopping=new ShoppingRadar(new ShoppingService(shoppingStore,sources,shoppingSettings,Data,http),Data,Dispatcher.CurrentDispatcher);
    }
    public string M(string metric)=>Backend.Read(metric);
    internal void ApplyAppearance(DesktopProfile profile)
    {
        var next=(Settings with{AnimateBackground=profile.Animate,ReactiveAudio=profile.Reactive,AudioIntensity=profile.Intensity,GlassOpacity=profile.Glass,ThemeId=profile.ThemeId}).Validate(false);
        next.Save(Path.Combine(Data,"preferences.json"));Settings=next;
        PreviewAppearance(next.ThemeId,next.AnimateBackground,next.GlassOpacity);SettingsChanged?.Invoke();
    }
    public void ApplySettings(DesktopSettings next,string target)
    {
        next=next.Validate();
        if(!DateTimeOffset.TryParse(target,CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException("Choisis une date et une heure valides pour le compteur.");
        bool deskChanged=next.ProjectRoot!=Settings.ProjectRoot||next.WeatherCity!=Settings.WeatherCity||next.Latitude!=Settings.Latitude||next.Longitude!=Settings.Longitude;
        next.Save(Path.Combine(Data,"preferences.json"));
        if(target!=TargetDate){DesktopSettings.Write(Path.Combine(Data,"target.txt"),target);TargetDate=target;Backend.Command("Target:"+target);}
        Settings=next;
        if(deskChanged)
        {
            Native.DeskStop();
            try{File.WriteAllLines(Path.Combine(Data,"desk.ini"),next.DeskIni(),Encoding.Unicode);}
            finally{Native.DeskStart(Path.Combine(Data,"desk.ini"));}
        }
        PreviewAppearance(next.ThemeId,next.AnimateBackground,next.GlassOpacity);
        SettingsChanged?.Invoke();
    }
    internal void PreviewAppearance(string themeId,bool animate,double opacity)
    {
        DesktopTheme.Select(themeId);
        Native.BackgroundTheme(Array.FindIndex(DesktopTheme.Definitions,t=>t.Id==themeId),0);
        Native.BackgroundAppearance(animate?1:0,(float)opacity);
    }
    public double N(string metric)=>double.TryParse(M(metric).TrimEnd('%','°','W',' '),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:double.NaN;
    public void Command(string command){try{Backend.Command(command);Error="";}catch(Exception e){Error=e.Message;}}
    readonly AppLauncher launcher=new();
    public void Launch(DockApp app){try{launcher.Open(app);Error="";}catch(Exception e){Error=e.Message;}}
    public void OpenKilo(string project)
    {
        if(!Directory.Exists(project))throw new DirectoryNotFoundException("Ce projet a été déplacé ou supprimé.");
        DesktopSettings.Write(Path.Combine(Data,"kilo-request.json"),JsonSerializer.Serialize(new{project,command=Kilo}));
        Terminal?.OpenKilo();
    }
    public void OpenCodexDeepSeek(string project)
    {
        if(!Directory.Exists(project))throw new DirectoryNotFoundException("Ce projet a été déplacé ou supprimé.");
        DesktopSettings.Write(Path.Combine(Data,"codex-ds-request.json"),JsonSerializer.Serialize(new{project}));
        Terminal?.OpenCodexDeepSeek();
    }
    public void SaveApps(List<DockApp> apps)
    {
        if(apps.Count>12||apps.Any(a=>string.IsNullOrWhiteSpace(a.Name)||string.IsNullOrWhiteSpace(a.Path)))throw new ArgumentException("Le dock accepte jusqu’à 12 applications nommées.");
        DesktopSettings.Write(Path.Combine(Data,"apps.json"),JsonSerializer.Serialize(apps,new JsonSerializerOptions{WriteIndented=true}));Apps=apps.ToList();Layout.Save();DockChanged?.Invoke();
    }
    public void SaveReminders(IEnumerable<ReminderItem> reminders)
    {
        var next=reminders.Where(r=>!string.IsNullOrWhiteSpace(r.Text)).Select(r=>new ReminderItem(r.Text.Trim())).Take(32).ToList();
        DesktopSettings.Write(Path.Combine(Data,"reminders.json"),JsonSerializer.Serialize(next,new JsonSerializerOptions{WriteIndented=true}));
        Reminders=next;
    }
    public void RefreshCover()
    {
        var size=Native.DeskCover(null,0);if(size<=0){Cover=null;coverBytes=null;return;}
        if(size>4*1024*1024)return;
        var bytes=new byte[size];if(Native.DeskCover(bytes,size)!=size)return;
        if(coverBytes is not null&&bytes.AsSpan().SequenceEqual(coverBytes))return;
        try{using var stream=new MemoryStream(bytes);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();Cover=image;coverBytes=bytes;}catch{Cover=null;coverBytes=null;}
    }
    public void Dispose(){Shopping.Dispose();shoppingStore.Dispose();Reserve.Dispose();Projects.Dispose();Terminal?.Dispose();Native.DeskStop();Backend.Dispose();}
}
