using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--terminal-metadata"){TerminalMetadataWorker.Run();return 0;}
        if(args.Length==2&&args[0]=="--preview-terminal-tabs"){TerminalTabsPreview.Run(args[1]);return 0;}
        if(args.Length==2&&args[0]=="--terminal-host"){TerminalHost.Run(Path.GetFullPath(args[1]));return 0;}
        if(args.Length==2&&args[0]=="--command"){Console.WriteLine(ControlPipe.Send(args[1]));return 0;}
        bool waitingReload=args.Contains("--reload",StringComparer.Ordinal);
        using var single=new Mutex(true,"Local\\Battlestation.Desktop",out bool created);
        if(!created&&!waitingReload)return 0;
        if(!created)
        {
            try{if(!single.WaitOne(TimeSpan.FromSeconds(5)))return 0;}
            catch(AbandonedMutexException){}
        }
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new GlassMenus());
        Station? station=null;DesktopWorkspace? runtime=null;
        app.DispatcherUnhandledException+=(_,e)=>{Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Battlestation"));File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Battlestation/error.txt"),e.Exception.ToString());};
        app.Startup+=(_,_)=>
        {
            int index=Array.IndexOf(args,"--root");var root=index>=0&&index+1<args.Length?Path.GetFullPath(args[index+1]):Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
            station=new Station(root);runtime=new DesktopWorkspace(app,station);
        };
        app.Exit+=(_,_)=>{runtime?.Dispose();station?.Dispose();};
        app.Run();
        // The UI is closed; finish the last note write before releasing the desktop mutex.
        runtime?.PendingNotesSave.GetAwaiter().GetResult();
        runtime?.PendingAtelierSave.GetAwaiter().GetResult();
        return 0;
    }
}
