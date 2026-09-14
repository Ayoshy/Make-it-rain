using System.Runtime.InteropServices;
using System.Windows;

namespace Battlestation;
// Conservative visibility: translucent/tool windows do not suppress effects.
internal sealed class DesktopVisibility
{
    readonly List<Rect> covers=[];
    internal bool Locked {get;set;}
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int size);
    internal void Update(nint terminal,IEnumerable<nint> dockWindows)
    {
        covers.Clear();
        var docks=dockWindows.ToHashSet();
        Native.EnumWindows((window,_)=>{
            // Win+D raises the dock band above ordinary windows. A window below
            // that band cannot cover a dock even when Windows keeps it visible.
            if(docks.Contains(window)&&Native.IsWindowVisible(window))return false;
            if(window==terminal||!Native.IsWindowVisible(window)||IsIconic(window))return true;
            Native.GetWindowThreadProcessId(window,out uint pid);if(pid==Environment.ProcessId)return true;
            var cls=Native.Class(window);bool taskbar=cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
            long style=(long)Native.GetWindowLongPtr(window,-20);if(!taskbar&&(style&(0x80000|0x80))!=0)return true;
            if(DwmGetWindowAttribute(window,14,out int cloak,4)==0&&cloak!=0)return true;
            if(cls is "Progman" or "WorkerW")return true;
            if(Native.GetWindowRect(window,out var r)&&r.Right>r.Left&&r.Bottom>r.Top)covers.Add(new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top));
            return true;
        },0);
    }
    internal bool Exposed(Rect rect)=>!Locked&&HasUncoveredArea(rect,covers);
    internal int MonitorMask=>(Exposed(new(0,0,2560,1440))?1:0)|(Exposed(new(2560,0,2560,1440))?2:0);
    internal static bool HasUncoveredArea(Rect rect,IEnumerable<Rect> occluders)
    {
        var remaining=new List<Rect>{rect};
        foreach(var cover in occluders)
        {
            var next=new List<Rect>();
            foreach(var area in remaining)
            {
                var hit=Rect.Intersect(area,cover);if(hit.IsEmpty||hit.Width<=0||hit.Height<=0){next.Add(area);continue;}
                if(hit.Top>area.Top)next.Add(new(area.Left,area.Top,area.Width,hit.Top-area.Top));
                if(hit.Bottom<area.Bottom)next.Add(new(area.Left,hit.Bottom,area.Width,area.Bottom-hit.Bottom));
                if(hit.Left>area.Left)next.Add(new(area.Left,hit.Top,hit.Left-area.Left,hit.Height));
                if(hit.Right<area.Right)next.Add(new(hit.Right,hit.Top,area.Right-hit.Right,hit.Height));
            }
            remaining=next;if(remaining.Count==0)return false;
            if(remaining.Count>256)return true; // Bound geometry work, prefer keeping effects alive.
        }
        return remaining.Count>0;
    }
}
