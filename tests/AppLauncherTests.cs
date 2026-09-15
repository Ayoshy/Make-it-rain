using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Battlestation;

static class AppLauncherTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Pump()
    {
        var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(TimeSpan.FromMilliseconds(150),DispatcherPriority.Background,(_,_)=>frame.Continue=false,Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame);timer.Stop();
    }
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool IsZoomed(nint window);

    [STAThread] static void Main()
    {
        var original=Native.GetForegroundWindow();
        var window=new Window{Title="Battlestation launcher test",Width=320,Height=160,ShowActivated=false};
        try
        {
            window.Show();Pump();
            var handle=new WindowInteropHelper(window).Handle;
            var app=new DockApp("Launcher test",Environment.ProcessPath!);
            var launcher=new AppLauncher();
            var running=AppActivity.RunningProcesses(app);
            Check(running.Any(process=>process.Pid==Environment.ProcessId),"Live identity must return this PID");
            Check(AppLauncher.FindWindow(new HashSet<int>{Environment.ProcessId})==handle,"Select the existing window");
            Check(AppLauncher.FindWindow(new HashSet<int>{int.MaxValue})==0,"Never select another application's window");
            window.WindowState=WindowState.Minimized;Pump();
            Check(IsIconic(handle),"Minimized fixture");
            launcher.Open(app);Pump();
            Check(!IsIconic(handle)&&Native.IsWindowVisible(handle),"Click restores a minimized running app");
            window.WindowState=WindowState.Maximized;Pump();
            launcher.Open(app);Pump();
            Check(IsZoomed(handle),"An already maximized window stays maximized");
            window.WindowState=WindowState.Normal;window.Hide();Pump();
            launcher.Open(app);Pump();
            Check(Native.IsWindowVisible(handle),"A hidden tray-style main window is shown");
            window.WindowStyle=WindowStyle.None;window.Hide();Pump();
            launcher.Open(app);Pump();
            Check(Native.IsWindowVisible(handle),"A hidden frameless application is shown");
            window.Close();Pump();
            bool refused=false;
            try{launcher.Open(app);}catch(InvalidOperationException){refused=true;}
            Check(refused,"Running without a window must not spawn a duplicate");
            Console.WriteLine("PASS: live PID identity, window ownership, minimized restore, maximized preservation, hidden restore, no-window duplicate prevention");
        }
        finally{window.Close();if(original!=0)Native.SetForegroundWindow(original);}
    }
}
