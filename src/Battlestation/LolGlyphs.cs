using System.Windows;
using System.Windows.Media;

namespace Battlestation;

// The small drawings of the LoL dock: player statistics, minions, gold and the
// match objectives. Every shape is one frozen geometry filled with a theme
// colour, so a scene change never leaves a hard-coded palette behind.
internal static class LolGlyphs
{
    internal enum Glyph { Sword,Skull,Spark,Minion,Coin,Dragon,Baron,Turret,Inhibitor,Flame,Drop,Mountain,Cloud,Hex,Flask,Crown }
    // The dragon counter carries the element of the last drake that team took, so
    // the badge says which one it was instead of repeating a generic head.
    internal static Glyph Element(string type)=>type switch
    {
        "Fire" or "Infernal"=>Glyph.Flame,"Water" or "Ocean"=>Glyph.Drop,"Earth" or "Mountain"=>Glyph.Mountain,"Air" or "Cloud"=>Glyph.Cloud,
        "Hextech"=>Glyph.Hex,"Chemtech"=>Glyph.Flask,"Elder"=>Glyph.Crown,_=>Glyph.Dragon
    };
    static readonly Dictionary<Glyph,(Geometry Fill,Geometry? Stroke)> shapes=Build();
    static readonly Dictionary<(string Color,double Width),Pen> pens=[];
    static Geometry G(string path){var shape=Geometry.Parse(path);shape.Freeze();return shape;}
    static Geometry Combine(Geometry first,Geometry second,GeometryCombineMode mode)
    {
        var merged=Geometry.Combine(first,second,mode,null);merged.Freeze();return merged;
    }
    static Dictionary<Glyph,(Geometry Fill,Geometry? Stroke)> Build()
    {
        var shapes=new Dictionary<Glyph,(Geometry,Geometry?)>();
        var sword=new GeometryGroup();
        sword.Children.Add(G("M12,1.4 L14.6,4.2 L14.6,13.2 L9.4,13.2 L9.4,4.2 Z"));
        sword.Children.Add(G("M5.6,14.2 L18.4,14.2 L18.4,16.4 L5.6,16.4 Z"));
        sword.Children.Add(G("M10.9,17 L13.1,17 L13.1,20.4 L10.9,20.4 Z"));
        sword.Children.Add(new EllipseGeometry(new Point(12,21.7),2,2));
        sword.Freeze();shapes[Glyph.Sword]=(sword,null);
        var sockets=new GeometryGroup();
        sockets.Children.Add(new EllipseGeometry(new Point(9.3,10.8),2,2));
        sockets.Children.Add(new EllipseGeometry(new Point(14.7,10.8),2,2));
        sockets.Children.Add(G("M12,12.9 L13.7,15.9 L10.3,15.9 Z"));
        sockets.Freeze();
        shapes[Glyph.Skull]=(Combine(G("M12,2.4 C6.8,2.4 3.4,6.2 3.4,11.2 C3.4,14.5 5,16.8 7.1,17.9 L7.1,20.4 C7.1,21.2 7.8,21.9 8.6,21.9 L15.4,21.9 C16.2,21.9 16.9,21.2 16.9,20.4 L16.9,17.9 C19,16.8 20.6,14.5 20.6,11.2 C20.6,6.2 17.2,2.4 12,2.4 Z"),sockets,GeometryCombineMode.Exclude),null);
        shapes[Glyph.Spark]=(G("M12,1.8 L13.9,9.4 L21.4,11.3 L13.9,13.2 L12,20.8 L10.1,13.2 L2.6,11.3 L10.1,9.4 Z"),null);
        var eyes=new GeometryGroup();
        eyes.Children.Add(new EllipseGeometry(new Point(9.9,14.7),1.05,1.05));
        eyes.Children.Add(new EllipseGeometry(new Point(14.1,14.7),1.05,1.05));
        eyes.Freeze();
        var minion=Combine(G("M12,4.2 C8.4,4.2 6.2,6.8 6.2,10.4 L6.2,20.6 C6.2,21.4 6.9,22 7.7,22 L16.3,22 C17.1,22 17.8,21.4 17.8,20.6 L17.8,10.4 C17.8,6.8 15.6,4.2 12,4.2 Z"),G("M4.6,9.6 L19.4,9.6 L19.4,11.6 L4.6,11.6 Z"),GeometryCombineMode.Union);
        shapes[Glyph.Minion]=(Combine(minion,eyes,GeometryCombineMode.Exclude),null);
        shapes[Glyph.Coin]=(Combine(new EllipseGeometry(new Point(12,12),9.4,9.4),G("M12,8.4 L14.8,12 L12,15.6 L9.2,12 Z"),GeometryCombineMode.Exclude),new EllipseGeometry(new Point(12,12),6.6,6.6));
        var head=Combine(G("M2.6,14 L8.6,7.6 L13.6,6.6 L18.6,9.6 L18.6,11.6 L12.4,12.6 L8.8,14.2 L18.4,15.8 L16,18.6 L9.6,19.4 L4.6,17.8 Z"),G("M14.6,6.8 L19.6,2.4 L19.6,8.6 Z"),GeometryCombineMode.Union);
        shapes[Glyph.Dragon]=(Combine(head,new EllipseGeometry(new Point(12.4,9.6),1.1,1.1),GeometryCombineMode.Exclude),null);
        var baron=Combine(G("M3.6,15.8 C3.6,10.6 7.8,7.4 13,7.4 C18.6,7.4 21.4,10.4 21.4,14.2 C21.4,18 17.6,20.4 12.6,20.4 L7.2,20.4 C5,20.4 3.6,18.8 3.6,15.8 Z"),G("M9,7.6 L5.8,2 L12,6.6 Z"),GeometryCombineMode.Union);
        baron=Combine(baron,G("M15.4,7 L18.6,1.8 L20.2,7.8 Z"),GeometryCombineMode.Union);
        var mouth=new GeometryGroup();
        mouth.Children.Add(new EllipseGeometry(new Point(10.4,13.2),1.3,1.3));
        mouth.Children.Add(new EllipseGeometry(new Point(16.2,13.2),1.3,1.3));
        mouth.Children.Add(G("M8.4,16.4 L10.2,20 L12,16.4 Z"));
        mouth.Children.Add(G("M12.8,16.4 L14.6,20 L16.4,16.4 Z"));
        mouth.Freeze();
        shapes[Glyph.Baron]=(Combine(baron,mouth,GeometryCombineMode.Exclude),null);
        var turret=Combine(G("M7.8,21.4 L16.2,21.4 L14.6,10.4 L9.4,10.4 Z"),G("M7.8,5.2 L16.2,5.2 L16.2,10.4 L7.8,10.4 Z"),GeometryCombineMode.Union);
        turret=Combine(turret,G("M12,1.4 L13.6,5.2 L10.4,5.2 Z"),GeometryCombineMode.Union);
        turret=Combine(turret,G("M5.8,21.4 L18.2,21.4 L18.2,22.6 L5.8,22.6 Z"),GeometryCombineMode.Union);
        shapes[Glyph.Turret]=(Combine(turret,new EllipseGeometry(new Point(12,7.9),.95,.95),GeometryCombineMode.Exclude),null);
        shapes[Glyph.Inhibitor]=(G("M12,1.6 L19.6,8.8 L12,22.4 L4.4,8.8 Z"),G("M4.4,8.8 L19.6,8.8 M8.2,8.8 L12,22.4 M15.8,8.8 L12,22.4"));
        shapes[Glyph.Flame]=(G("M12,1.6 C13.6,6.4 19,8.6 19,14.4 A7,7 0 1 1 5,14.4 C5,9.6 10.4,7.4 12,1.6 Z"),null);
        shapes[Glyph.Drop]=(G("M12,2 L18,11.6 A6,6 0 0 1 6,11.6 Z"),null);
        shapes[Glyph.Mountain]=(G("M2,20 L9.4,6.6 L13.4,13.6 L16,9 L22,20 Z"),null);
        var cloud=Combine(new EllipseGeometry(new Point(8.2,13),4.4,4.4),new EllipseGeometry(new Point(14,10.6),5.2,5.2),GeometryCombineMode.Union);
        cloud=Combine(cloud,new EllipseGeometry(new Point(17.6,13.6),4,4),GeometryCombineMode.Union);
        shapes[Glyph.Cloud]=(Combine(cloud,G("M4.6,12.4 H19.4 V17.6 H4.6 Z"),GeometryCombineMode.Union),null);
        shapes[Glyph.Hex]=(Combine(G("M12,1.6 L21.2,6.9 L21.2,17.1 L12,22.4 L2.8,17.1 L2.8,6.9 Z"),G("M12,7.4 L17.4,10.5 L17.4,15.3 L12,18.4 L6.6,15.3 L6.6,10.5 Z"),GeometryCombineMode.Exclude),null);
        shapes[Glyph.Flask]=(G("M9.4,2.6 H14.6 V8.6 L19.6,18 A2.6,2.6 0 0 1 17.3,21.8 H6.7 A2.6,2.6 0 0 1 4.4,18 L9.4,8.6 Z"),null);
        shapes[Glyph.Crown]=(G("M3.2,19.6 L2.4,7.6 L8,12.4 L12,4.6 L16,12.4 L21.6,7.6 L20.8,19.6 Z"),null);
        return shapes;
    }
    static Pen Stroke(string color,double width)
    {
        if(pens.TryGetValue((color,width),out var pen))return pen;
        if(pens.Count>=64)pens.Clear();
        var made=new Pen(DesktopTheme.Brush(color),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        return pens[(color,width)]=made;
    }
    internal static void Draw(DrawingContext dc,Glyph glyph,double x,double y,double size,string color,double opacity=1)
    {
        var (fill,stroke)=shapes[glyph];
        var brush=DesktopTheme.Brush(color);
        dc.PushOpacity(opacity);
        dc.PushTransform(new TranslateTransform(x,y));
        dc.PushTransform(new ScaleTransform(size/24d,size/24d));
        dc.DrawGeometry(brush,null,fill);
        if(stroke is{} outline)dc.DrawGeometry(null,Stroke(color,1.7),outline);
        dc.Pop();dc.Pop();dc.Pop();
    }
    // Sweep is in degrees, clockwise from the top, so a ring reads as a progress
    // gauge without a second geometry per value.
    internal static void Arc(DrawingContext dc,Point center,double radius,double sweep,string color,double width,double opacity=1)
    {
        sweep=Math.Clamp(sweep,-359.9,359.9);if(Math.Abs(sweep)<1)return;
        double from=-Math.PI/2,to=from+sweep*Math.PI/180;
        var geometry=new StreamGeometry();
        using(var path=geometry.Open())
        {
            path.BeginFigure(new Point(center.X+radius*Math.Cos(from),center.Y+radius*Math.Sin(from)),false,false);
            path.ArcTo(new Point(center.X+radius*Math.Cos(to),center.Y+radius*Math.Sin(to)),new Size(radius,radius),0,Math.Abs(sweep)>180,sweep>0?SweepDirection.Clockwise:SweepDirection.Counterclockwise,true,false);
        }
        geometry.Freeze();
        dc.PushOpacity(opacity);dc.DrawGeometry(null,Stroke(color,width),geometry);dc.Pop();
    }
}
