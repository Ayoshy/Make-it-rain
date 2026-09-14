using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Terminal.Wpf;

namespace Battlestation;
internal static class TerminalHost
{
    // Protocol-scoped endpoint: a pre-glass host may stay alive with user sessions.
    // Never reconnect new desktop chrome to that incompatible host.
    public const string PipeName="Battlestation.NativeTerminal.v1";
    public static void Run(string root)
    {
        using var single=new Mutex(true,"Local\\"+PipeName,out bool created);if(!created)return;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.DispatcherUnhandledException+=(_,e)=>{File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Battlestation/terminal-error.txt"),e.Exception.ToString());};
        using var host=new NativeTerminalWindow(root);
        host.LastTabClosed+=()=>app.Dispatcher.BeginInvoke(()=>{if(!host.HasTabs)app.Shutdown();});
        using var control=new ControlPipe(app.Dispatcher,host.Command,PipeName);
        app.Run();
    }
}
internal sealed class NativeTerminalWindow : IDisposable
{
    sealed record Tab(Guid Id,string Title,TerminalView View);
    readonly Window window;
    readonly Grid content=new();
    readonly TerminalTabs header=new();
    bool externalChrome;
    readonly List<Tab> tabs=[];
    readonly string root,projectRoot,codex;
    readonly TerminalClipboard clipboard;
    DesktopPlacement? placement;
    Tab? active;
    bool disposed;
    int shellNumber;
    public bool HasTabs=>tabs.Count>0;
    public event Action? LastTabClosed;
    nint Handle=>new WindowInteropHelper(window).Handle;
    static SolidColorBrush B(string value)=>new((Color)ColorConverter.ConvertFromString(value));
    static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
    public NativeTerminalWindow(string sourceRoot)
    {
        root=sourceRoot;
        var settings=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"desk/settings.json"))).RootElement;
        projectRoot=Environment.ExpandEnvironmentVariables(settings.GetProperty("projectRoot").GetString()!);
        codex=Environment.ExpandEnvironmentVariables(settings.GetProperty("codex").GetString()!);
        Environment.SetEnvironmentVariable("NO_COLOR",null);Environment.SetEnvironmentVariable("TERM","xterm-256color");Environment.SetEnvironmentVariable("COLORTERM","truecolor");
        var layout=new DockPanel();header.Margin=new Thickness(8,6,8,6);DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);layout.Children.Add(content);
        header.Selected+=id=>Select(FindTab(id));header.Closed+=id=>CloseTab(FindTab(id));header.Added+=()=>Add();header.PasteRequested+=Paste;
        window=new Window{Title="Battlestation · Terminal",Width=1000,Height=600,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Background=B("#21182B"),Foreground=B("#DAD2E7"),Content=layout};
        new WindowInteropHelper(window).EnsureHandle();
        window.Closing+=(_,e)=>{if(!disposed){e.Cancel=true;window.Hide();}};
        clipboard=new TerminalClipboard(()=>Handle,Paste);
        window.PreviewKeyDown+=(_,e)=>{if(Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&e.Key==Key.C){Copy();e.Handled=true;}if(Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&e.Key==Key.V){Paste();e.Handled=true;}};
    }
    static uint ColorValue(string value){var c=(Color)ColorConverter.ConvertFromString(value);return (uint)(c.R|(c.G<<8)|(c.B<<16));}
    static TerminalTheme Theme()
    {
        uint Color(string color)=>ColorValue(color);
        return new TerminalTheme{DefaultBackground=Color("#21182B"),DefaultForeground=Color("#DAD2E7"),DefaultSelectionBackground=Color("#665077"),CursorStyle=CursorStyle.BlinkingBar,
            ColorTable=new[]{"#45475A","#F38BA8","#A6E3A1","#F9E2AF","#89B4FA","#F5C2E7","#94E2D5","#BAC2DE","#585B70","#F38BA8","#A6E3A1","#F9E2AF","#89B4FA","#F5C2E7","#94E2D5","#CDD6F4"}.Select(Color).ToArray()};
    }
    void Add(string? project=null)
    {
        string directory=project??projectRoot;
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException("Projet introuvable.");
        var file=project is null?"Start-Shell.ps1":"Start-Codex.ps1";
        var command="powershell.exe -NoLogo -NoProfile "+(project is null?"-NoExit ":"")+"-File "+Quote(Path.Combine(root,"terminal",file))+" -CodexPath "+Quote(codex)+(project is null?"":" -ProjectPath "+Quote(directory));
        var view=new TerminalView(command,directory,Theme());
        var tab=new Tab(Guid.NewGuid(),project is null?"PowerShell "+(++shellNumber):string.Equals(project,root,StringComparison.OrdinalIgnoreCase)?"Battlestation":Path.GetFileName(project),view);
        tabs.Add(tab);content.Children.Add(view);Select(tab);
    }
    void Select(Tab tab)
    {
        active=tab;foreach(var item in tabs)item.View.Visibility=item==tab?Visibility.Visible:Visibility.Collapsed;
        RefreshTabs();window.Dispatcher.BeginInvoke(()=>tab.View.Terminal.Focus(),System.Windows.Threading.DispatcherPriority.Input);
    }
    Tab FindTab(Guid id)=>tabs.FirstOrDefault(tab=>tab.Id==id)??throw new InvalidOperationException("Cet onglet n'existe plus.");
    void CloseTab(Tab tab)
    {
        tabs.Remove(tab);content.Children.Remove(tab.View);tab.View.Session.Close();
        if(tabs.Count>0)Select(tabs[^1]);else{active=null;RefreshTabs();LastTabClosed?.Invoke();}
    }
    void RefreshTabs()=>header.SetTabs(tabs.Select(tab=>new TerminalTabInfo(tab.Id,tab.Title,tab==active)));
    void SetExternalChrome(bool external){externalChrome=external;header.Visibility=external?Visibility.Collapsed:Visibility.Visible;}
    void Copy(){var text=active?.View.Terminal.GetSelectedText();if(!string.IsNullOrEmpty(text))Clipboard.SetText(text);}
    void Paste()
    {
        if(active?.View.Session is not {} term||!term.Ready.IsCompletedSuccessfully||!term.Ready.Result)return;
        if(Clipboard.ContainsImage()||Native.IsClipboardFormatAvailable(Native.RegisterClipboardFormat("PNG")))term.WriteInput("\u0016");
        else if(Clipboard.ContainsText()){var text=Clipboard.GetText();term.WriteInput(term.BracketedPaste?"\x1b[200~"+text+"\x1b[201~":text);}
        active.View.Terminal.Focus();
    }
    void Place(int x,int y,int width,int height)
    {
        if(width<200||height<100||width>5120||height>1440)throw new ArgumentException("Dimensions invalides");
        window.WindowStyle=WindowStyle.None;window.ResizeMode=ResizeMode.NoResize;window.ShowInTaskbar=false;
        window.Left=x;window.Top=y;window.Width=width;window.Height=height;
        placement??=new DesktopPlacement(window.Dispatcher);
        if(!attached){placement.Add(window,true);attached=true;}
    }
    bool attached;
    object Inspect()=>new{chromeVersion=1,externalChrome,headerVisible=header.Visibility==Visibility.Visible,hostPid=Environment.ProcessId,pid=tabs.Count==0?0:Environment.ProcessId,status=tabs.Count==0?"Terminal natif prêt":$"{tabs.Count} onglet{(tabs.Count>1?"s":"")} · {active?.Title}",hwnd=(long)Handle,visible=window.IsVisible,
        sessions=tabs.Select(t=>new{id=t.Id,title=t.Title,pid=t.View.Session.Pid,ready=t.View.Session.Ready.IsCompletedSuccessfully&&t.View.Session.Ready.Result,error=t.View.Session.Error,input=t.View.Session.InputCharacters,output=t.View.Session.OutputCharacters,columns=t.View.Terminal.Columns,rows=t.View.Terminal.Rows,active=t==active}).ToArray()};
    public string Command(string command)
    {
        if(command=="inspect")return JsonSerializer.Serialize(Inspect());
        if(command=="chrome:external"||command=="chrome:internal"){SetExternalChrome(command.EndsWith("external"));return "OK";}
        if(command.StartsWith("select:")&&Guid.TryParse(command[7..],out var select)){Select(FindTab(select));return "OK";}
        if(command.StartsWith("close-tab:")&&Guid.TryParse(command[10..],out var close)){CloseTab(FindTab(close));return "OK";}
        if(command.StartsWith("place:")){var p=command.Split(':');if(p.Length!=5)throw new ArgumentException();Place(int.Parse(p[1]),int.Parse(p[2]),int.Parse(p[3]),int.Parse(p[4]));return "OK";}
        if(command=="show"){window.Show();placement?.Arrange();return "OK";}
        if(command=="hide"){window.Hide();return "OK";}
        if(command=="detach"){SetExternalChrome(false);placement?.Dispose();placement=null;attached=false;window.WindowStyle=WindowStyle.SingleBorderWindow;window.ResizeMode=ResizeMode.CanResize;window.ShowInTaskbar=true;if(tabs.Count>0)window.Show();return "OK";}
        if(command=="start"){if(tabs.Count==0)Add();window.Show();return "OK";}
        if(command.StartsWith("codex:")){Add(command[6..]);window.Show();return "OK";}
        if(command.StartsWith("command:"))
        {
            switch(command[8..]){
                case "Start":if(tabs.Count==0)Add();break;
                case "NewShell":Add();break;
                case "Paste":Paste();break;
                case "PreviousTab":if(tabs.Count>0)Select(tabs[(tabs.IndexOf(active!)+tabs.Count-1)%tabs.Count]);break;
                case "NextTab":if(tabs.Count>0)Select(tabs[(tabs.IndexOf(active!)+1)%tabs.Count]);break;
                default:throw new InvalidOperationException("Commande inconnue");
            }
            return "OK";
        }
        throw new InvalidOperationException("Commande inconnue");
    }
    public void Dispose(){disposed=true;clipboard.Dispose();placement?.Dispose();foreach(var tab in tabs)tab.View.Session.Close();window.Close();}
}
