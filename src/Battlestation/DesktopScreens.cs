using System.Runtime.InteropServices;
using System.Windows;
using Forms=System.Windows.Forms;

namespace Battlestation;

// One real monitor: the bounds Windows reports (physical pixels) and the same
// rectangle in the DIP space the docks are arranged in. Both monitors of the
// authored plan run at 100 %, so a screen at 100 % maps to itself.
internal sealed record DesktopScreen(Rect Dip,Rect Pixels,double Scale,bool Primary)
{
    internal Rect ToPixels(Rect dip)=>new(Pixels.Left+(dip.Left-Dip.Left)*Scale,Pixels.Top+(dip.Top-Dip.Top)*Scale,dip.Width*Scale,dip.Height*Scale);
}

internal static class DesktopScreens
{
    // The plan every saved arrangement and scene preset is authored against
    // (two 2560 x 1440 screens). A real monitor set only translates it; at the
    // authored geometry the translation is the identity.
    internal static readonly Rect[] Reference=[new(0,0,2560,1440),new(2560,0,2560,1440)];
    internal static IReadOnlyList<DesktopScreen> Current{get;private set;}=Fallback();
    internal static Rect[] Rects=>Current.Select(screen=>screen.Dip).ToArray();
    internal static string Signature=>string.Join('|',Current.Select(screen=>$"{screen.Pixels.X:0},{screen.Pixels.Y:0},{screen.Pixels.Width:0},{screen.Pixels.Height:0},{screen.Scale:0.##}"));

    internal static IReadOnlyList<DesktopScreen> Read()
    {
        var screens=new List<DesktopScreen>();
        try
        {
            foreach(var screen in Forms.Screen.AllScreens)
            {
                var bounds=screen.Bounds;double scale=Scale(bounds.X,bounds.Y,bounds.Width,bounds.Height);
                var pixels=new Rect(bounds.X,bounds.Y,bounds.Width,bounds.Height);
                screens.Add(new(new Rect(bounds.X/scale,bounds.Y/scale,bounds.Width/scale,bounds.Height/scale),pixels,scale,screen.Primary));
            }
        }
        catch(Exception e) when(e is InvalidOperationException or TypeInitializationException or System.ComponentModel.Win32Exception){}
        Current=screens.Count==0?Fallback():[..screens.OrderBy(screen=>screen.Dip.Left).ThenBy(screen=>screen.Dip.Top)];
        return Current;
    }

    static IReadOnlyList<DesktopScreen> Fallback()=>[..Reference.Select((rect,index)=>new DesktopScreen(rect,rect,1,index==0))];

    // The native glass layer covers the whole virtual desktop in physical
    // pixels; the seam is the width of its first (leftmost) monitor.
    internal static (int Left,int Top,int Width,int Height,int Seam) Canvas()
    {
        int left=(int)Math.Round(Current.Min(screen=>screen.Pixels.Left)),top=(int)Math.Round(Current.Min(screen=>screen.Pixels.Top));
        int right=(int)Math.Round(Current.Max(screen=>screen.Pixels.Right)),bottom=(int)Math.Round(Current.Max(screen=>screen.Pixels.Bottom));
        return (left,top,right-left,bottom-top,(int)Math.Round(Current[0].Pixels.Width));
    }

    // The native background layer draws in physical pixels. A dock therefore
    // keeps its physical size while WPF owns the DIP box the layout solved in.
    internal static Rect ToPixels(Rect dip)
    {
        var screen=Current.FirstOrDefault(item=>item.Dip.Contains(dip))??Current.FirstOrDefault(item=>item.Dip.IntersectsWith(dip))??Current[0];
        return screen.ToPixels(dip);
    }

    [StructLayout(LayoutKind.Sequential)] struct Point{public int X,Y;}
    [DllImport("user32.dll")] static extern nint MonitorFromPoint(Point point,uint flags);
    [DllImport("Shcore.dll")] static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);

    static double Scale(int x,int y,int width,int height)
    {
        try
        {
            var monitor=MonitorFromPoint(new Point{X=x+width/2,Y=y+height/2},2);
            if(monitor!=0&&GetDpiForMonitor(monitor,0,out uint dpi,out uint _)==0&&dpi>=96)return dpi/96d;
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException){}
        return 1;
    }
}
