using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Battlestation;
internal abstract class Surface : FrameworkElement
{
    protected readonly Station Station;
    protected DrawingContext D=null!;
    readonly List<(Rect Rect,Action Action,string Name)> hits=[];
    readonly Dictionary<string,BitmapImage> images=[];
    readonly FontFamily artDeco;
    protected Point Pointer=new(-1,-1);
    public double DesktopX {get;set;}
    public double DesktopY {get;set;}
    protected virtual double OffsetY=>0;
    protected virtual double HitOffsetY=>OffsetY;
    protected void Glass(int slot,double x,double y,double w,double h)=>Native.BackgroundPanel(slot,(float)(DesktopX+x),(float)(DesktopY+y+OffsetY),(float)w,(float)h);
    protected static readonly CultureInfo French=CultureInfo.GetCultureInfo("fr-FR");
    protected const string Ink="#DAD2E7",Muted="#AE9FBD",Pink="#FF6FD3",Purple="#BE81FF";
    public Surface(Station station){Station=station;artDeco=new FontFamily(new Uri("pack://application:,,,/"),"./Assets/Fonts/#GTAArtDeco Condensed");SnapsToDevicePixels=true;FocusVisualStyle=null;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);}
    public void Refresh()=>InvalidateVisual();
    protected override void OnRender(DrawingContext dc){D=dc;hits.Clear();D.PushTransform(new TranslateTransform(0,OffsetY));Paint();D.Pop();}
    protected abstract void Paint();
    protected static SolidColorBrush B(string color){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();return b;}
    protected void Box(double x,double y,double w,double h,string fill,string stroke="#00000000",double radius=0,double thickness=1)=>D.DrawRoundedRectangle(B(fill),new Pen(B(stroke),thickness),new Rect(x,y,Math.Max(0,w),Math.Max(0,h)),radius,radius);
    protected void Line(double x,double y,double x2,double y2,string color,double width=1)=>D.DrawLine(new Pen(B(color),width),new Point(x,y),new Point(x2,y2));
    protected void Text(string text,double x,double y,double points=11,string color=Ink,string font="Segoe UI Variable Text",string align="left",bool bold=false,double width=0,double tracking=0)
    {
        var face=new Typeface(font=="GTAArtDeco"?artDeco:new FontFamily(font),FontStyles.Normal,bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal);
        var ft=new FormattedText(text,French,FlowDirection.LeftToRight,face,points*96/72,B(color),VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if(width>0){ft.MaxTextWidth=width;ft.MaxLineCount=1;ft.Trimming=TextTrimming.CharacterEllipsis;}
        double total=ft.WidthIncludingTrailingWhitespace+Math.Max(0,text.Length-1)*tracking;
        if(align=="center")x-=total/2;else if(align=="right")x-=total;
        if(tracking==0||width>0){D.DrawText(ft,new Point(x,y));return;}
        foreach(var rune in text.EnumerateRunes())
        {
            var glyph=new FormattedText(rune.ToString(),French,FlowDirection.LeftToRight,face,points*96/72,B(color),VisualTreeHelper.GetDpi(this).PixelsPerDip);
            D.DrawText(glyph,new Point(x,y));x+=glyph.WidthIncludingTrailingWhitespace+tracking;
        }
    }
    protected void Image(string path,double x,double y,double w,double h,double opacity=1)
    {
        if(!File.Exists(path))return;
        if(!images.TryGetValue(path,out var image)){image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(path);image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();images[path]=image;}
        D.PushOpacity(opacity);D.DrawImage(image,new Rect(x,y,w,h));D.Pop();
    }
    protected void Panel(double x,double y,double w,double h,double radius=24)=>Box(x,y,w,h,"#0A201133","#2AE7D3FF",radius);
    protected void Hit(string name,double x,double y,double w,double h,Action action)=>hits.Add((new(x,y+HitOffsetY,w,h),action,name));
    protected void Button(string name,string label,double x,double y,double w,double h,Action action,double size=10,string color=Ink,bool enabled=true,double radius=15,bool hitTest=true)
    {
        var rect=new Rect(x,y,w,h);bool hover=rect.Contains(Pointer)&&enabled&&hitTest;
        var fill=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(hover?"#26EEE1F8":"#14DED2EE"),(Color)ColorConverter.ConvertFromString(hover?"#18B8A8D4":"#0CB29CCC"),90);
        D.PushOpacity(enabled?1:.4);D.DrawRoundedRectangle(fill,new Pen(B("#22DACDEC"),1),rect,Math.Min(radius,h/2),Math.Min(radius,h/2));
        Text(label,x+w/2,y+(h-size*96/72)/2-1,size,color,align:"center");D.Pop();
        if(enabled&&hitTest)Hit(name,x,y,w,h,action);
    }
    protected void Track(double x,double y,double w,double value,string color,double height=3){Box(x,y,w,height,"#5A603D6F");if(double.IsFinite(value))Box(x,y,w*Math.Clamp(value/100,0,1),height,color);}
    protected override void OnMouseMove(MouseEventArgs e){var p=e.GetPosition(this);Cursor=hits.Any(h=>h.Rect.Contains(p))?Cursors.Hand:Cursors.Arrow;Pointer=new Point(p.X,p.Y-OffsetY);OnPointer(e);InvalidateVisual();}
    protected virtual void OnPointer(MouseEventArgs e){}
    protected override void OnMouseLeave(MouseEventArgs e){Pointer=new(-1,-1);InvalidateVisual();}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){var p=e.GetPosition(this);var hit=hits.LastOrDefault(h=>h.Rect.Contains(p));if(hit.Action is not null){hit.Action();e.Handled=true;}InvalidateVisual();}
    internal object InspectHits()=>hits.Select(h=>new{name=h.Name,x=h.Rect.X,y=h.Rect.Y,width=h.Rect.Width,height=h.Rect.Height}).ToArray();
}
