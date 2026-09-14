using System.Windows;

namespace Battlestation;
internal static class DashboardBounds
{
    // A drawer is transient overlay geometry, never a persisted layout resize.
    internal static Rect Expand(Rect summary,double height,bool preferAbove)
    {
        var screen=DesktopLayout.Screens.First(s=>s.Contains(summary));
        double extra=Math.Max(0,height-summary.Height);
        bool above=preferAbove?summary.Top-extra>=screen.Top:summary.Top+height>screen.Bottom;
        return new Rect(summary.X,summary.Y-(above?extra:0),summary.Width,Math.Max(summary.Height,height));
    }
}
