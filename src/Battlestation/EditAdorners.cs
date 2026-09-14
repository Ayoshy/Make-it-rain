using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Battlestation;
internal static class EditOverlay
{
    internal static void Mount(Border overlay,DockPanel header)
    {
        overlay.Child=null;
        var content=new Grid();content.Children.Add(header);content.Children.Add(new EditHandles());overlay.Child=content;
    }
}
internal sealed class EditGrid(int step):FrameworkElement
{
    internal int Step {get;set;}=step;
    internal Rect[] Occupied {get;set;}=[];
    DrawingBrush? cachedBrush;
    int cachedStep;
    protected override void OnRender(DrawingContext dc)
    {
        if(cachedBrush is null||cachedStep!=Step)
        {
            var dots=new DrawingGroup();using(var tile=dots.Open())tile.DrawEllipse(new SolidColorBrush(Color.FromArgb(55,224,207,242)),null,new Point(.75,.75),.75,.75);
            cachedBrush=new DrawingBrush(dots){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,Step,Step),ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,Step,Step)};cachedBrush.Freeze();cachedStep=Step;
        }
        var bounds=new Rect(0,0,ActualWidth,ActualHeight);
        var clip=new GeometryGroup{FillRule=FillRule.EvenOdd};clip.Children.Add(new RectangleGeometry(bounds));
        foreach(var rect in Occupied){var inside=Rect.Intersect(rect,bounds);if(!inside.IsEmpty)clip.Children.Add(new RectangleGeometry(inside));}
        clip.Freeze();dc.PushClip(clip);dc.DrawRectangle(cachedBrush,null,bounds);dc.Pop();
    }
}
internal sealed class EditHandles:FrameworkElement
{
    public EditHandles(){IsHitTestVisible=false;}
    protected override void OnRender(DrawingContext dc)
    {
        var pearl=new LinearGradientBrush(Color.FromRgb(255,244,255),Color.FromRgb(174,210,233),45);double w=ActualWidth,h=ActualHeight;
        foreach(var p in new[]{new Point(7,7),new Point(w/2,7),new Point(w-7,7),new Point(7,h/2),new Point(w-7,h/2),new Point(7,h-7),new Point(w/2,h-7),new Point(w-7,h-7)})
            dc.DrawRoundedRectangle(pearl,null,new Rect(p.X-3,p.Y-3,6,6),3,3);
    }
}
