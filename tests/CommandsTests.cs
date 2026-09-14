using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;
internal static class CommandsTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [STAThread] static void Main(string[] args)
    {
        int executed=0;
        PaletteEntry[] entries=[new("app:1","Codex Meter","Application","\uE71D",()=>executed=1),new("app:2","Conrad Sensor","Application","\uE71D",()=>executed=2),new("settings","Réglages","Paramètres settings","\uE713",()=>executed=3,true),new("project:1","Badventurers 2","Projet","\uE8B7",()=>{})];
        Check(PaletteSearch.Find(entries,"reglages").Single().Id=="settings","Accents should not block search");
        Check(PaletteSearch.Find(entries,"cdxm").First().Id=="app:1","Subsequence search");
        Check(PaletteSearch.Find(entries,"codex application").Single().Id=="app:1","Tokens may match name and type");
        Check(PaletteSearch.Find(entries,">").Single().Command,"Command mode must exclude apps");
        Check(PaletteSearch.Find(entries,"zzqq").Count==0,"Unknown query must not execute fallback");
        string root=Path.Combine(Path.GetTempPath(),"Battlestation-commands-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var defaults=new DesktopSettings(root,"Aix",43.5,5.4);var saved=defaults with{WeatherCity="Paris",Latitude=48.8,Longitude=2.3,AnimateBackground=false,GlassOpacity=.22,GridEnabled=false,GridStep=24,LinkDocks=true};string file=Path.Combine(root,"preferences.json");saved.Save(file);
            Check(DesktopSettings.Load(file,defaults)==saved,"All preferences must survive restart");
            Check(defaults.Validate().ProjectRoot==root,"Default project root survives validation");
            File.WriteAllText(file,System.Text.Json.JsonSerializer.Serialize(new{defaults.ProjectRoot,defaults.WeatherCity,defaults.Latitude,defaults.Longitude}));
            var legacy=DesktopSettings.Load(file,defaults);Check(legacy.GridEnabled&&legacy.GridStep==8,"Old preferences default to an enabled 8-unit grid");
            Check(!legacy.LinkDocks&&!defaults.LinkDocks,"Existing and new installations opt out of neighbour linkage by default");
            foreach(var bad in new[]{defaults with{GridStep=3},defaults with{GridStep=65},defaults with{Latitude=91},defaults with{Longitude=double.NaN},defaults with{GlassOpacity=2},defaults with{ProjectRoot=root+"\nWeatherCity=other"}})
            {bool rejected=false;try{bad.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid preference accepted");}
            File.WriteAllText(file,"{");Check(DesktopSettings.Load(file,defaults)==defaults,"Corrupt preferences must fall back without changing sources");
        }
        finally{Directory.Delete(root,true);}
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new GlassMenus());
        GlassMenuTests.Run();
        void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false,DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame);}
        // WPF events on isolated windows: no user terminal and no injected keystrokes.
        var palette=new CommandPaletteWindow(entries);OverlayStyle.Place(palette);palette.Show();Pump();
        var input=(TextBox)typeof(CommandPaletteWindow).GetField("search",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(palette)!;
        var results=(ListBox)typeof(CommandPaletteWindow).GetField("results",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(palette)!;
        input.Text="con";Pump();Check(results.Items.Count==1,"Live filtering");input.Text="";Pump();
        var source=PresentationSource.FromVisual(palette)!;
        void Key(Key key){var ev=new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};palette.RaiseEvent(ev);Pump();}
        Key(System.Windows.Input.Key.Down);Key(System.Windows.Input.Key.Enter);Check(executed==2,"Enter must launch the selected row rather than the best match");
        var project=new CommandPaletteWindow(entries);project.Show();Pump();project.ProjectActions("Badventurers 2",()=>{},()=>{});Pump();
        Check(((ListBox)typeof(CommandPaletteWindow).GetField("results",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(project)!).Items.Count==2,"Projects offer two explicit actions");
        if(args.Length>0)
        {
            project.UpdateLayout();var image=new RenderTargetBitmap((int)project.Width,(int)project.Height,96,96,PixelFormats.Pbgra32);image.Render(project);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);using var stream=File.Create(args[0]);png.Save(stream);
        }
        project.Dismiss(false);Pump();
        using(var first=new PaletteHotkey(()=>{}))
        {
            using var second=new PaletteHotkey(()=>{});Check(!second.Registered,"A registered shortcut cannot be silently stolen");
            if(first.Registered){first.Dispose();second.Retry();Check(second.Registered,"Shortcut must be released and recoverable");}
            else Console.WriteLine("NOTE Ctrl+Space already owned externally; no owner was changed.");
        }
        Console.WriteLine("PASS palette search, selected action, project choices, preference validation/persistence and hotkey conflict/release. WPF events, not real clicks.");app.Shutdown();
    }
}
