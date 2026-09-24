using System.Windows;
using System.Windows.Media;

namespace Battlestation;

// The same pearl/active palette as the device artwork, retained between frames.
internal sealed class AtelierArtwork
{
    internal DrawingGroup Emblem { get; }
    internal Brush Pearl { get; }
    internal Brush Accent { get; }
    internal Brush Glow { get; }
    internal Pen Rim { get; }
    internal Pen Highlight { get; }
    static Geometry Shape(string value){var shape=Geometry.Parse(value);shape.Freeze();return shape;}
    static readonly Geometry top=Shape("M12,32 L52,10 L92,32 L52,55 Z");
    static readonly Geometry left=Shape("M12,32 L52,55 L52,70 L12,47 Z");
    static readonly Geometry right=Shape("M52,55 L92,32 L92,47 L52,70 Z");
    static readonly Geometry lower=Shape("M12,59 L52,37 L92,59 L92,72 L52,95 L12,72 Z");
    static readonly Geometry bevel=Shape("M18,32 L52,14 L86,32 M13,34 L52,57 L90,36");
    static readonly Geometry etching=Shape("M35,32 L52,23 L69,32 L52,42 Z M52,42 L52,51");
    static readonly Geometry lowerEdge=Shape("M15,67 L52,88 L89,67");

    static Color Color(string value,double alpha=1)
    {
        var color=(Color)ColorConverter.ConvertFromString(value);color.A=(byte)(color.A*alpha);return color;
    }
    static SolidColorBrush Solid(string value,double alpha=1){var b=new SolidColorBrush(Color(value,alpha));b.Freeze();return b;}
    static LinearGradientBrush Gradient(params (double Offset,string Value)[] colors)
    {
        var brush=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1)};
        foreach(var (offset,value) in colors)brush.GradientStops.Add(new(Color(value),offset));
        brush.Freeze();return brush;
    }
    static Pen Stroke(Brush brush,double width)
    {
        var pen=new Pen(brush,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;
    }
    internal AtelierArtwork(ThemeDefinition theme)
    {
        Pearl=Gradient((0,"#FFFFFF"),(.24,theme.Pearl[0]),(.52,theme.Pearl[3]),(.76,theme.Pearl[1]),(1,"#FFFFFF"));
        Accent=Solid(theme.Active[0]);
        var glow=new RadialGradientBrush(Color(theme.Active[1],.22),Colors.Transparent){GradientOrigin=new(.4,.45)};glow.Freeze();Glow=glow;
        Rim=Stroke(Solid(theme.Rim,.24),1);
        Highlight=Stroke(Solid(theme.Pearl[0],.62),1);
        Emblem=new DrawingGroup();
        using(var dc=Emblem.Open())
        {
            dc.DrawEllipse(Glow,null,new(52,56),52,48);
            dc.DrawGeometry(Gradient((0,theme.Pearl[3]),(.5,theme.Glass),(1,theme.Pearl[2])),Rim,lower);
            dc.DrawGeometry(null,Stroke(Solid(theme.Active[0],.12),7),lowerEdge);
            dc.DrawGeometry(null,Stroke(Solid(theme.Active[0],.7),1.3),lowerEdge);
            dc.DrawGeometry(Gradient((0,theme.Pearl[1]),(.7,theme.Pearl[2]),(1,theme.Glass)),Rim,left);
            dc.DrawGeometry(Gradient((0,theme.Pearl[0]),(.4,theme.Pearl[3]),(1,theme.Pearl[2])),Rim,right);
            dc.DrawGeometry(Pearl,Highlight,top);
            dc.DrawGeometry(null,Stroke(Solid("#FFFFFF",.7),1),bevel);
            dc.DrawGeometry(null,Stroke(Solid(theme.Glass,.55),1.8),etching);
            dc.DrawEllipse(Accent,null,new(52,79),2,1.2);
            dc.DrawLine(Stroke(Solid("#FFFFFF",.8),1),new(21,27),new(21,37));
            dc.DrawLine(Stroke(Solid("#FFFFFF",.8),1),new(17,32),new(25,32));
        }
        Emblem.Freeze();
    }
}
