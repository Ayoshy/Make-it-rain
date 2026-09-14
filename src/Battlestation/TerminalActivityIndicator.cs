using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Battlestation;
internal sealed class TerminalActivityIndicator : Grid
{
    readonly Ellipse track=new(){Width=15,Height=15,StrokeThickness=1.5};
    readonly Path arc=new(){Width=16,Height=16,Data=Geometry.Parse("M 8,1 A 7,7 0 1 1 1,8"),StrokeThickness=2.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,RenderTransformOrigin=new Point(.5,.5)};
    readonly TextBlock glyph=new(){FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=15,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    readonly RotateTransform rotation=new();
    TerminalActivity activity;
    bool effects;
    public TerminalActivityIndicator()
    {
        Width=20;Height=22;Margin=new Thickness(0,0,6,0);IsHitTestVisible=false;
        arc.RenderTransform=rotation;Children.Add(track);Children.Add(arc);Children.Add(glyph);
        IsVisibleChanged+=(_,_)=>Animate();Unloaded+=(_,_)=>Stop();SetState(TerminalActivity.Unknown,false);
    }
    static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    public void SetState(TerminalActivity value,bool animate)
    {
        bool changed=activity!=value||effects!=animate;activity=value;effects=animate;
        bool busy=value is TerminalActivity.Thinking or TerminalActivity.Working;
        string color=value switch{TerminalActivity.Working=>"#9FD5F1",TerminalActivity.Ready=>"#A8E5CD",TerminalActivity.Attention=>"#FFCE8C",TerminalActivity.Error=>"#FFA3B4",TerminalActivity.Unknown=>"#B2A8C0",_=>"#D7BFFF"};
        arc.Stroke=Brush(color);track.Stroke=Brush(color);track.Opacity=.22;glyph.Foreground=Brush(color);
        track.Visibility=arc.Visibility=busy?Visibility.Visible:Visibility.Collapsed;glyph.Visibility=busy?Visibility.Collapsed:Visibility.Visible;
        glyph.Text=value switch{TerminalActivity.Ready=>"✓",TerminalActivity.Attention or TerminalActivity.Error=>"!",_=>"?"};
        glyph.Opacity=value==TerminalActivity.Unknown?.65:1;
        if(changed)Animate();
    }
    void Animate()
    {
        Stop();if(!IsVisible||!effects||!SystemParameters.ClientAreaAnimation||activity is not (TerminalActivity.Thinking or TerminalActivity.Working))return;
        var spin=new DoubleAnimation(0,360,TimeSpan.FromSeconds(activity==TerminalActivity.Thinking?1.6:.9)){RepeatBehavior=RepeatBehavior.Forever};
        Timeline.SetDesiredFrameRate(spin,30);rotation.BeginAnimation(RotateTransform.AngleProperty,spin);
    }
    void Stop(){rotation.BeginAnimation(RotateTransform.AngleProperty,null);rotation.Angle=0;}
}
