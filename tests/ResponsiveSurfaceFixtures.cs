using System.Windows.Media.Imaging;
using System.Windows;
namespace Battlestation;

// Fixtures deliberately cannot start a backend, terminal, browser or GPU command.
internal record DockApp(string Name,string Path);
internal sealed class Station
{
    public string Root=>System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../"));
    public string Assets=>System.IO.Path.Combine(Root,"assets");
    public List<DockApp> Apps {get;private set;}=new[]{"Steam","Brave","Discord","Stremio","League","Explorer","One","Two","Three","Four","Five","Six"}.Select(s=>new DockApp(s,"fixture")).ToList();
    public string Error=>"";
    public BitmapSource? Cover=>null;
    public Terminal? Terminal=>null;
    public ProjectSignals Projects {get;}=new();
    public Reserve Reserve {get;}=new();
    public double AudioIntensity=>.5;
    public void ShowReserve(){}
    public void Command(string value){if(value!="GpuRead")throw new Exception("Unexpected hardware or backend command");}
    public void Launch(DockApp app)=>throw new Exception("Unexpected application launch");
    public void SaveApps(List<DockApp> apps)=>Apps=apps;
    public Dictionary<string,string> Metrics {get;}=new();
    public string M(string key)=>Metrics.TryGetValue(key,out var value)?value:key switch
    {
        "cpu"=>"58°","gpu"=>"50°","cpuLoad"=>"48%","gpuLoad"=>"32%","fan"=>"43%","watts"=>"35 W",
        "sensorStatus"=>"EN DIRECT","codexStatus"=>"MAJ 15:00","quotaLabel"=>"7 JOURS","remaining"=>"19%","reset"=>"Reset 19/09 10:10",
        "today"=>"23,76 M","total"=>"16,72 Md","days"=>"65","hours"=>"11","minutes"=>"29","seconds"=>"50","date"=>"19 NOVEMBRE 2026",
        "controlAvailable"=>"0","heatwave"=>"0","hottest"=>"58°",_=>key.StartsWith("core")?"48°":""
    };
    public double N(string key)=>double.TryParse(M(key).TrimEnd('%','°'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var number)?number:double.NaN;
}
internal sealed class Terminal{public void OpenCodex(string path)=>throw new Exception("Unexpected terminal launch");}
internal sealed class Spectrum{public float[] Bands=>new float[12];public void Start(){}public void Stop(){}}
internal sealed record ProjectSignal(string? Branch,int? Changes);
internal sealed class ProjectSignals{public ProjectSignal Read(string path)=>path.EndsWith("Conrad Sensor")?new("main",0):path.EndsWith("Codex Meter")?new(null,null):new("main",2);}
internal sealed class Reserve{public List<string> Items {get;}=[];}
internal static class Native
{
    static int selected;
    static readonly string[] projects=["Battlestation","Conrad Sensor","Codex Meter","Kash","BVX","The New Guy","Seven","Eight","Nine","Ten","Eleven","Twelve","Thirteen","Fourteen"];
    internal static readonly Dictionary<int,Rect> Panels=[];
    internal static string Read(string key)
    {
        if(key=="projectCount")return projects.Length.ToString();if(key=="selectedProject")return projects[selected];if(key=="selectedPath")return "C:/Projects/"+projects[selected];
        if(key.StartsWith("project:")){var p=key.Split(':');int i=int.Parse(p[1]);return p[2] switch{"name"=>projects[i],"path"=>"C:/Projects/"+projects[i],_=>"2 h"};}
        return key switch{"clockHours"=>"15","clockMinutes"=>"30","clockSeconds"=>"09","clockDate"=>"lundi 14 septembre 2026","weatherIcon"=>"clear-day","weatherTemp"=>"29°","weatherCity"=>"Aix-en-Provence","weatherCondition"=>"Ciel dégagé · ancienne mesure","weatherRange"=>"↑ 31° ↓ 13°","weatherWind"=>"Vent · 1 km/h","source"=>"Brave","title"=>"DEBRIEF ZEVENT, ON REACT AUX MEILLEURS MOMENTS","artist"=>"JirayaTV · Replay","mediaTime"=>"06:19 / 180:31","playing"=>"1","canPrevious" or "canPlay" or "canNext" or "canSeek"=>"1","mediaProgress"=>"0.15",_=>""};
    }
    internal static void DeskCommand(string command){if(command.StartsWith("Select:"))selected=int.Parse(command[7..]);else throw new Exception("Unexpected media command");}
    internal static void BackgroundPanel(int slot,float x,float y,float w,float h)=>Panels[slot]=new(x,y,w,h);
    internal static void BackgroundAudio(float a,float b,float c,float d){}

}
internal record AudioOutput(string Id,string Name,bool Selected);
internal record AudioApp(string Key,string Name,float Volume,bool Muted,float Peak);
internal sealed class AudioMixerWorker:IDisposable
{
    public List<AudioOutput> Outputs=>[];
    public List<AudioApp> Apps=>Enumerable.Range(0,9).Select(i=>new AudioApp("app"+i,"Application "+i,.5f,false,.2f)).ToList();
    public string OutputName=>"Haut-parleurs · Realtek Audio";
    public bool? MicrophoneMuted=>false;
    public float? Volume=>.55f;
    public bool? Muted=>false;
    public string Error=>"";
    public void Poll(){}
    public void SetActive(bool value){}
    public void ToggleMicrophone()=>throw new Exception("Unexpected audio write");
    public void SetVolume(string? key,float volume)=>throw new Exception("Unexpected audio write");
    public void ToggleMute(string? key)=>throw new Exception("Unexpected audio write");
    public void SelectOutput(string key)=>throw new Exception("Unexpected audio write");
    public void Dispose(){}
}

internal static class OverlayStyle
{
    internal static void Apply(Window window){}
    internal static System.Windows.Controls.TextBlock Text(string text,double size)=>new(){Text=text,FontSize=size};
    internal static System.Windows.Controls.Border Frame(UIElement content)=>new(){Child=content};
}
