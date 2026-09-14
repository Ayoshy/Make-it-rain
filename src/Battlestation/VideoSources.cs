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
    internal static bool IsPictureInPictureTitle(string title)
    {
        title=title.Replace('\u00a0',' ');
        return title.Equals("Picture in picture",StringComparison.OrdinalIgnoreCase)||
            title.Equals("Picture-in-Picture",StringComparison.OrdinalIgnoreCase)||
            title.Equals("Mode PIP (Picture-in-Picture)",StringComparison.OrdinalIgnoreCase)||
            title.Equals("Image dans l’image",StringComparison.OrdinalIgnoreCase)||
            title.Equals("Image dans l'image",StringComparison.OrdinalIgnoreCase);
    }
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int size);
    internal static VideoSource? Select(IReadOnlyList<VideoSource> sources,string mode,nint current)
    {
        var available=sources.Where(s=>mode=="auto"||s.Kind==mode).ToArray();
        return available.FirstOrDefault(s=>s.Foreground&&!s.Minimized)
            ??available.FirstOrDefault(s=>s.Handle==current&&!s.Minimized)
            ??available.FirstOrDefault(s=>!s.Minimized)
            ??available.FirstOrDefault(s=>s.Handle==current)??available.FirstOrDefault();
    }
    internal static List<VideoSource> Find()
    {
        var processes=new Dictionary<int,string>();
        foreach(var process in Process.GetProcesses())
        {
            using(process)try
            {
                string name=process.ProcessName;
                if(name.Equals("brave",StringComparison.OrdinalIgnoreCase))processes[process.Id]="youtube";
                else if(name.Equals("stremio-shell-ng",StringComparison.OrdinalIgnoreCase)||name.Equals("stremio",StringComparison.OrdinalIgnoreCase))processes[process.Id]="stremio";
            }catch(InvalidOperationException){}
        }
        var result=new List<VideoSource>();var foreground=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(foreground,out uint foregroundPid);
        Native.EnumWindows((window,_)=>
        {
            if(!Native.IsWindowVisible(window))return true;
            Native.GetWindowThreadProcessId(window,out uint pid);
            if(!processes.TryGetValue((int)pid,out string? kind))return true;
            if(DwmGetWindowAttribute(window,14,out int cloaked,4)==0&&cloaked!=0)return true;
            Native.GetWindowRect(window,out var bounds);
            if(!IsIconic(window)&&(bounds.Right-bounds.Left<120||bounds.Bottom-bounds.Top<80))return true;
            var text=new StringBuilder(256);Native.GetWindowText(window,text,text.Capacity);
            if(kind=="youtube")
            {
                // Only Chromium's dedicated video PiP is eligible. Never capture
                // the normal browser window, its other tabs or private pages.
                var title=text.ToString();
                if(Native.Class(window)!="Chrome_WidgetWin_1"||!IsPictureInPictureTitle(title))return true;
            }
            else if(!text.ToString().Contains("Stremio",StringComparison.OrdinalIgnoreCase)||Native.GetWindow(window,4)!=0)return true;
            result.Add(new(window,(int)pid,kind,IsIconic(window),foreground==window||pid==foregroundPid));return true;
        },0);
        return result;
    }
}
