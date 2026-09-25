using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Battlestation;
internal sealed record VideoSource(nint Handle,int Pid,string Kind,bool Minimized,bool Foreground);
internal static class VideoSources
{
    internal static bool BraveForeground()
    {
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out uint pid);
        try{using var process=Process.GetProcessById((int)pid);return process.ProcessName.Equals("brave",StringComparison.OrdinalIgnoreCase);}
        catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
    }
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int size);
    // Browser sources come from the extension; only the native Stremio client
    // needs a window scan. Callers run it off the UI thread.
    internal static List<VideoSource> FindStremio()
    {
        var processes=new HashSet<int>();
        foreach(var process in Process.GetProcesses())
        {
            using(process)try
            {
                string name=process.ProcessName;
                if(name.Equals("stremio-shell-ng",StringComparison.OrdinalIgnoreCase)||name.Equals("stremio",StringComparison.OrdinalIgnoreCase))processes.Add(process.Id);
            }catch(InvalidOperationException){}
        }
        var result=new List<VideoSource>();if(processes.Count==0)return result;
        var foreground=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(foreground,out uint foregroundPid);
        Native.EnumWindows((window,_)=>
        {
            if(!Native.IsWindowVisible(window))return true;
            Native.GetWindowThreadProcessId(window,out uint pid);
            if(!processes.Contains((int)pid))return true;
            if(DwmGetWindowAttribute(window,14,out int cloaked,4)==0&&cloaked!=0)return true;
            Native.GetWindowRect(window,out var bounds);
            if(!IsIconic(window)&&(bounds.Right-bounds.Left<120||bounds.Bottom-bounds.Top<80))return true;
            var text=new StringBuilder(256);Native.GetWindowText(window,text,text.Capacity);
            if(!text.ToString().Contains("Stremio",StringComparison.OrdinalIgnoreCase)||Native.GetWindow(window,4)!=0)return true;
            result.Add(new(window,(int)pid,"stremio",IsIconic(window),foreground==window||pid==foregroundPid));return true;
        },0);
        return result;
    }
}
