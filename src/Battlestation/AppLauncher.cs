using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Battlestation;

internal sealed class AppLauncher
{
    readonly Dictionary<string,long> launches=new(StringComparer.OrdinalIgnoreCase);

    internal void Open(DockApp app)
    {
        // Always scan live: the dock's activity indicator is a periodic snapshot.
        var processes=AppActivity.RunningProcesses(app);
        var window=FindWindow(processes.Select(process=>process.Pid).ToHashSet());
        if(window!=0)
        {
            Restore(window);
            return;
        }
        if(processes.Count>0)
            throw new InvalidOperationException($"{app.Name} est déjà en cours, sans fenêtre disponible.");

        var path=Environment.ExpandEnvironmentVariables(app.Path);
        // A second click can arrive before a new application's process/window exists.
        if(launches.TryGetValue(path,out var started)&&Stopwatch.GetElapsedTime(started)<TimeSpan.FromSeconds(10))return;
        using var launched=Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
        if(launches.Count>32)launches.Clear();
        launches[path]=Stopwatch.GetTimestamp();
    }

    internal static nint FindWindow(IReadOnlySet<int> processIds)
    {
        nint visible=0,hidden=0;
        Native.EnumWindows((window,_)=>
        {
            Native.GetWindowThreadProcessId(window,out uint pid);
            if(!processIds.Contains((int)pid))return true;
            // Ignore tool windows, owned popups and invisible message-only helpers.
            if(Native.GetWindow(window,4)!=0||((long)Native.GetWindowLongPtr(window,-20)&0x80)!=0)return true;
            var title=new StringBuilder(256);
            if(Native.GetWindowText(window,title,title.Capacity)==0)return true;
            if(DwmGetWindowAttribute(window,14,out int cloaked,4)==0&&cloaked!=0)return true;
            if(Native.IsWindowVisible(window)){visible=window;return false;}
            // Tray apps can have frameless main windows. Reject tiny hidden
            // message helpers without requiring a native title bar.
            Native.GetWindowRect(window,out var bounds);
            if(hidden==0&&bounds.Right-bounds.Left>=120&&bounds.Bottom-bounds.Top>=80)hidden=window;
            return true;
        },0);
        return visible!=0?visible:hidden;
    }

    internal static void Restore(nint window)
    {
        // SW_RESTORE only when minimized: preserve an already maximized window.
        if(IsIconic(window))ShowWindowAsync(window,9);
        else if(!Native.IsWindowVisible(window))ShowWindowAsync(window,5);
        var popup=GetLastActivePopup(window);
        if(popup!=0&&Native.IsWindowVisible(popup))window=popup;
        Native.SetForegroundWindow(window);
        // Focus denial never falls back to launching a duplicate process.
    }

    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(nint window,int command);
    [DllImport("user32.dll")] static extern nint GetLastActivePopup(nint window);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int size);
}
