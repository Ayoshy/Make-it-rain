using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Battlestation;

internal static class DesktopPlacementTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static bool Topmost(nint hwnd)=>(Native.GetWindowLongPtr(hwnd,-20)&8)!=0;
    static bool Above(nint hwnd,nint other)
    {
        for(var previous=Native.GetWindow(other,3);previous!=0;previous=Native.GetWindow(previous,3))
            if(previous==hwnd)return true;
        return false;
    }
    [STAThread] static void Main()
    {
        // Real Win32/WPF windows, offscreen. No shell toggles, input injection,
        // terminal commands or changes to the user's application windows.
        nint Create()=>Native.CreateWindowEx(0x10080,"STATIC","Placement regression",0x86000000,-20000,-20000,120,80,0,0,0,0);
        var shell=Create();var terminal=Create();var normal=Create();
        var widget=new Window{Width=120,Height=80,Left=-20000,Top=-20000,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false,AllowsTransparency=true,Background=System.Windows.Media.Brushes.Transparent};
        try
        {
            widget.Show();var hwnd=new WindowInteropHelper(widget).Handle;
            Native.ShowWindow(shell,4);Native.ShowWindow(terminal,4);Native.ShowWindow(normal,4);
            var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false,DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame);
            using(var placement=new DesktopPlacement(Dispatcher.CurrentDispatcher))
            {
                placement.Add(widget);placement.SetExternalWindow(terminal,hwnd);
                var foreground=Native.GetForegroundWindow();
                Native.GetWindowRect(hwnd,out var initial);
                Native.SetWindowPos(shell,1,0,0,0,0,0x613);
                placement.Arrange(shell);
                Check(!Topmost(hwnd)&&!Topmost(terminal),"Normal desktop must not keep widgets topmost");
                Check(Above(normal,hwnd)&&Above(normal,terminal),"Ordinary applications must cover widgets");
                Check(Above(terminal,hwnd),"Native terminal must be above its input-blocking WPF frame");
                Native.SetWindowPos(terminal,1,0,0,0,0,0x613);
                Check(!Above(terminal,hwnd),"Reproduce a terminal obscured by its frame");
                placement.Arrange(shell);
                Check(Above(terminal,hwnd)&&Above(normal,terminal),"Repair obscured terminal without raising it over applications");

                for(int cycle=0;cycle<3;cycle++)
                {
                    // Windows 11 can raise Progman in the normal band without
                    // WS_EX_TOPMOST. This is the case the original test missed.
                    Native.SetWindowPos(shell,0,0,0,0,0,0x613);
                    Check(!Topmost(shell),"Raised normal shell must not be topmost");
                    placement.Arrange(shell);
                    Check(Topmost(hwnd)&&Topmost(terminal)&&Above(hwnd,shell)&&Above(terminal,shell),"Non-topmost Show Desktop must preserve all content");
                    Native.SetWindowPos(shell,1,0,0,0,0,0x613);
                    placement.Arrange(shell);
                    Check(!Topmost(hwnd)&&!Topmost(terminal)&&Above(normal,hwnd),"Return from non-topmost desktop must restore application layering");
                    Check(Above(terminal,hwnd),"Terminal must stay clickable after a normal-band desktop cycle");
                    // The shell is promoted after any foreground notification.
                    bool moved=Native.SetWindowPos(shell,-1,0,0,0,0,0x613);
                    int error=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    Check(Topmost(shell),$"Test shell must enter topmost band (hwnd={shell}, style={Native.GetWindowLongPtr(shell,-20)}, moved={moved}, error={error})");
                    placement.Arrange(shell);
                    Check(Topmost(hwnd)&&Topmost(terminal),$"Show Desktop must promote both widget and stable terminal (shell={Topmost(shell)}, visible={Native.IsWindowVisible(shell)}, widget={Topmost(hwnd)}, terminal={Topmost(terminal)})");
                    Check(Above(hwnd,shell)&&Above(terminal,shell),"Content must remain above the desktop background");
                    Native.SetWindowPos(terminal,1,0,0,0,0,0x613);
                    placement.Arrange(shell);
                    Check(Topmost(terminal)&&Above(terminal,shell),"Repair demotion by an older terminal host");
                    Native.SetWindowPos(shell,-1,0,0,0,0,0x613);
                    placement.Arrange(shell);
                    Check(Above(hwnd,shell)&&Above(terminal,shell),"Repair a later shell reorder without a foreground change");
                    Native.SetWindowPos(shell,1,0,0,0,0,0x613);
                    placement.Arrange(shell);
                    Check(!Topmost(hwnd)&&!Topmost(terminal),"Returning to applications must remove topmost from every surface");
                    Check(Above(normal,hwnd)&&Above(normal,terminal),"Applications must cover content after returning");
                    Check(Above(terminal,hwnd),"Terminal must stay clickable after a topmost desktop cycle");
                }
                Native.GetWindowRect(hwnd,out var final);
                Check(initial.Equals(final),"Placement must preserve widget geometry");
                Check(Native.GetForegroundWindow()==foreground,"Placement must never take keyboard focus");
                Native.SetWindowPos(shell,-1,0,0,0,0,0x613);
                placement.Arrange(shell);
            }
            Check(Native.IsWindow(terminal),"Disposing desktop placement must preserve the terminal host");
            Check(!Topmost(terminal),"A detached terminal must not stay topmost after disposal");
            Console.WriteLine("PASS: 3 normal-band and 3 topmost desktop transitions, WPF and external host, late shell reorder, old-host demotion, application layering, geometry, focus and host survival.");
        }
        finally{widget.Close();Native.DestroyWindow(shell);Native.DestroyWindow(terminal);Native.DestroyWindow(normal);}
    }
}
