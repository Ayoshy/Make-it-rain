using System.Windows;

namespace Battlestation;
internal static class DashboardBounds
{
    // A drawer is transient overlay geometry, never a persisted layout resize.
    internal static Rect Expand(Rect summary,double height,bool preferAbove,double minimumWidth=0)
    {
        var screen=DesktopLayout.Screens.First(s=>s.Contains(summary));
        double extra=Math.Max(0,height-summary.Height);
        bool above=preferAbove?summary.Top-extra>=screen.Top:summary.Top+height>screen.Bottom;
        double h=Math.Min(screen.Height,Math.Max(summary.Height,height)),w=Math.Min(screen.Width,Math.Max(summary.Width,minimumWidth));
        return new Rect(Math.Clamp(summary.X,screen.Left,screen.Right-w),Math.Clamp(summary.Y-(above?extra:0),screen.Top,screen.Bottom-h),w,h);
    }
}
