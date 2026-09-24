using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Battlestation;
// Shared WPF chrome. The desktop material itself is drawn by NativeBackground.
internal static class DockAppearance
{
    public const string TextFont="Segoe UI Variable Text",NumberFont="Bahnschrift",HeaderFont="GTAArtDeco";
    public static FontFamily UiFont {get;}=new(TextFont);
    internal const string Ink="#DAD2E7",Muted="#AE9FBD",ButtonRim="#22DACDEC";
    internal const double HeaderPoints=14,HeaderTracking=1.8,PanelRadius=24,ButtonRadius=15;
    public static readonly LinearGradientBrush ButtonFill=Fill(false),ButtonHover=Fill(true);
    static readonly Pen pointerRim=new(PointerLight(),1.5);
    static RadialGradientBrush PointerLight()
    {
        var light=new RadialGradientBrush{MappingMode=BrushMappingMode.Absolute,Center=new Point(),GradientOrigin=new Point(),RadiusX=160,RadiusY=100};
        var stop=new GradientStop{Offset=0};
        BindingOperations.SetBinding(stop,GradientStop.ColorProperty,new Binding("Color"){Source=DesktopTheme.Brush("#8CFFFAFF")});
        light.GradientStops.Add(stop);light.GradientStops.Add(new GradientStop(Colors.Transparent,1));return light;
    }
    // One retained light; translating the drawing keeps each reflection centred on
    // its pointer without allocating a gradient or starting an animation per move.
    internal static void Reflection(DrawingContext drawing,Rect bounds,Point pointer,double radius)
    {
        if(!bounds.Contains(pointer))return;
        bounds.Offset(-pointer.X,-pointer.Y);
        drawing.PushTransform(new TranslateTransform(pointer.X,pointer.Y));
        drawing.DrawRoundedRectangle(null,pointerRim,bounds,radius,radius);drawing.Pop();
    }
    static LinearGradientBrush Fill(bool hover)
    {
        var brush=DesktopTheme.Gradient(hover?"#26EEE1F8":"#14DED2EE",hover?"#18B8A8D4":"#0CB29CCC",90);
        return brush;
    }
}

// Native WPF buttons (the terminal strip) share the drawn dock buttons' hover.
internal sealed class GlassHoverBorder : Border
{
    Point pointer=new(-1,-1);
    readonly Pen rim=new(DesktopTheme.Brush("#BE81FF"),1);
    public GlassHoverBorder(){}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);pointer=e.GetPosition(this);InvalidateVisual();}
    protected override void OnMouseLeave(MouseEventArgs e){base.OnMouseLeave(e);pointer=new(-1,-1);InvalidateVisual();}
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if(!IsEnabled||!IsMouseOver)return;
        var bounds=new Rect(.5,.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1));
        double radius=Math.Min(CornerRadius.TopLeft,ActualHeight/2);
        drawing.DrawRoundedRectangle(DesktopTheme.Brush("#14FFFFFF"),rim,bounds,radius,radius);
        DockAppearance.Reflection(drawing,bounds,pointer,radius);
    }
}
