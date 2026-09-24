using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace Battlestation;
internal abstract class Surface : FrameworkElement
{
    protected readonly Station Station;
    protected DrawingContext D=null!;
    readonly List<(Rect Rect,Action Action,string Name)> hits=[];
    readonly Dictionary<string,BitmapImage> images=[];
    readonly HashSet<string> missingImages=[];
    static readonly Dictionary<(string,double),Pen> pens=[];
    readonly FontFamily artDeco;
    readonly int dockSlot;
    readonly HashSet<int> glassSlots=[];
    readonly Dictionary<(string Text,double Points,string Color,string Font,bool Bold,double Width,double Dpi),FormattedText> textCache=[];
    bool displayed=true;
    string previousImageTheme=DesktopTheme.Current.Id;
    static readonly DependencyProperty ThemeBlendProperty=DependencyProperty.Register("ThemeBlend",typeof(double),typeof(Surface),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
    void ThemeChanged()
    {
        previousImageTheme=DesktopTheme.PreviousId;
        BeginAnimation(ThemeBlendProperty,null);
        if(displayed)BeginAnimation(ThemeBlendProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(240)){FillBehavior=FillBehavior.Stop});
        InvalidateVisual();
    }
    internal long RenderCount {get;private set;}
    internal virtual void SetDisplayed(bool value)
    {
        displayed=value;
        if(!value)BeginAnimation(ThemeBlendProperty,null);
        if(!value){hits.Clear();foreach(int slot in glassSlots)Native.BackgroundPanel(slot,0,0,0,0);}
    }
    protected Point Pointer=new(-1,-1);
    protected Rect? InteractionClip;
    public double DesktopX {get;set;}
    public double DesktopY {get;set;}
    protected virtual double HitOffsetX=>0;
    protected virtual double HitOffsetY=>0;
    protected void Glass(int slot,double x,double y,double w,double h)
    {
        glassSlots.Add(slot);
        if(displayed){var rect=DesktopScreens.ToPixels(new Rect(DesktopX+x,DesktopY+y,w,h));Native.BackgroundPanel(slot,(float)rect.Left,(float)rect.Top,(float)rect.Width,(float)rect.Height);}
    }
    protected static readonly CultureInfo French=CultureInfo.GetCultureInfo("fr-FR");
    protected const string Ink=DockAppearance.Ink,Muted=DockAppearance.Muted,Pink="#FF6FD3",Purple="#BE81FF";
    public Surface(Station station,int dockSlot=-1){Station=station;DesktopTheme.Changed+=ThemeChanged;this.dockSlot=dockSlot;artDeco=new FontFamily(new Uri("pack://application:,,,/"),"./Assets/Fonts/#GTAArtDeco Condensed");SnapsToDevicePixels=true;FocusVisualStyle=null;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);}
    public void Refresh()=>InvalidateVisual();
    internal void UpdateGlassBounds()
    {
        if(dockSlot<0||!displayed)return;
        var rect=DesktopScreens.ToPixels(new Rect(DesktopX,DesktopY,Width,Height));
        Native.BackgroundPanel(dockSlot,(float)rect.Left,(float)rect.Top,(float)rect.Width,(float)rect.Height);
    }
    protected override void OnRender(DrawingContext dc)
    {
        D=dc;hits.Clear();if(!displayed)return;RenderCount++;
        // One frame for every dock; widgets supply only content and interactions.
        if(dockSlot>=0){if(DrawsPanel)Panel();Glass(dockSlot,0,0,Width,Height);}
        Paint();
    }
    protected abstract void Paint();
    // A dock whose content is drawn by its own layer (the diorama water) keeps the
    // native glass frame and registers its slot, but adds no panel over it.
    protected virtual bool DrawsPanel=>true;
    protected static SolidColorBrush B(string color)=>DesktopTheme.Brush(color);
    static Pen Stroke(string color,double width){if(pens.TryGetValue((color,width),out var p))return p;p=new Pen(B(color),width);if(pens.Count>=256)pens.Clear();return pens[(color,width)]=p;}
    protected void Box(double x,double y,double w,double h,string fill,string stroke="#00000000",double radius=0,double thickness=1)=>D.DrawRoundedRectangle(B(fill),Stroke(stroke,thickness),new Rect(x,y,Math.Max(0,w),Math.Max(0,h)),radius,radius);
    protected void Line(double x,double y,double x2,double y2,string color,double width=1)=>D.DrawLine(Stroke(color,width),new Point(x,y),new Point(x2,y2));
    protected void Text(string text,double x,double y,double points=11,string color=Ink,string font=DockAppearance.TextFont,string align="left",bool bold=false,double width=0,double tracking=0)
    {
        double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var key=(text,points,color,font,bold,width,dpi);
        if(!textCache.TryGetValue(key,out var ft))
        {
            var face=new Typeface(font=="GTAArtDeco"?artDeco:new FontFamily(font),FontStyles.Normal,bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal);
            ft=new FormattedText(text,French,FlowDirection.LeftToRight,face,points*96/72,B(color),dpi);
            if(width>0){ft.MaxTextWidth=width;ft.MaxLineCount=1;ft.Trimming=TextTrimming.CharacterEllipsis;}
            if(textCache.Count>=256)textCache.Clear();textCache[key]=ft;
        }
        double total=ft.WidthIncludingTrailingWhitespace+Math.Max(0,text.Length-1)*tracking;
        if(align=="center")x-=total/2;else if(align=="right")x-=total;
        if(tracking==0||width>0){D.DrawText(ft,new Point(x,y));return;}
        foreach(var rune in text.EnumerateRunes())
        {
            var glyphKey=(rune.ToString(),points,color,font,bold,0d,dpi);
            if(!textCache.TryGetValue(glyphKey,out var glyph)){
                var glyphFace=new Typeface(font=="GTAArtDeco"?artDeco:new FontFamily(font),FontStyles.Normal,bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal);
                glyph=new FormattedText(rune.ToString(),French,FlowDirection.LeftToRight,glyphFace,points*96/72,B(color),dpi);
                if(textCache.Count>=256)textCache.Clear();textCache[glyphKey]=glyph;
            }
            D.DrawText(glyph,new Point(x,y));x+=glyph.WidthIncludingTrailingWhitespace+tracking;
        }
    }
    protected void Image(string path,double x,double y,double w,double h,double opacity=1)
    {
        path=Path.GetFullPath(path);
        var icons=Path.Combine(Station.Root,"dock","icons")+Path.DirectorySeparatorChar;
        if(path.StartsWith(icons,StringComparison.OrdinalIgnoreCase)&&!Path.GetRelativePath(icons,path).StartsWith("themes"))
        {
            string Themed(string id)=>id=="vice-city"?path:Path.Combine(icons,"themes",id,Path.GetRelativePath(icons,path));
            double mix=(double)GetValue(ThemeBlendProperty);
            if(mix<1)DrawImageFile(Themed(previousImageTheme),x,y,w,h,opacity*(1-mix));
            DrawImageFile(Themed(DesktopTheme.Current.Id),x,y,w,h,opacity*mix);return;
        }
        DrawImageFile(path,x,y,w,h,opacity);
    }
    void DrawImageFile(string path,double x,double y,double w,double h,double opacity)
    {
        if(missingImages.Contains(path))return;
        if(!images.TryGetValue(path,out var image)){if(!File.Exists(path)){missingImages.Add(path);return;}image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(path);image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();images[path]=image;}
        D.PushOpacity(opacity);D.DrawImage(image,new Rect(x,y,w,h));D.Pop();
    }
    void Panel()=>Box(0,0,Width,Height,"#0A201133","#2AE7D3FF",DockAppearance.PanelRadius);
    protected void Header(string title,double y=18)=>Text(title,24,y,DockAppearance.HeaderPoints,Muted);
    protected void Hit(string name,double x,double y,double w,double h,Action action)
    {
        var rect=new Rect(x+HitOffsetX,y+HitOffsetY,w,h);
        if(InteractionClip is {} clip)rect.Intersect(clip);
        if(!rect.IsEmpty&&rect.Width>0&&rect.Height>0)hits.Add((rect,action,name));
    }
    protected FormattedText Paragraph(string text,double points,string color,double width)
    {
        width=Math.Max(1,width);double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var key=(text,points,color,DockAppearance.TextFont,false,-width,dpi);
        if(!textCache.TryGetValue(key,out var formatted))
        {
            formatted=new FormattedText(text,French,FlowDirection.LeftToRight,new Typeface(DockAppearance.UiFont,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),points*96/72,B(color),dpi)
                {MaxTextWidth=width,MaxLineCount=int.MaxValue,Trimming=TextTrimming.None};
            if(textCache.Count>=256)textCache.Clear();textCache[key]=formatted;
        }
        return formatted;
    }
    protected void Button(string name,string label,double x,double y,double w,double h,Action action,double size=10,string color=Ink,bool enabled=true,double radius=DockAppearance.ButtonRadius,bool hitTest=true)
    {
        var rect=new Rect(x,y,w,h);bool hover=rect.Contains(Pointer)&&enabled&&hitTest;
        var fill=hover?DockAppearance.ButtonHover:DockAppearance.ButtonFill;
        D.PushOpacity(enabled?1:.4);D.DrawRoundedRectangle(fill,Stroke(DockAppearance.ButtonRim,1),rect,Math.Min(radius,h/2),Math.Min(radius,h/2));
        Text(label,x+w/2,y+(h-size*96/72)/2-1,size,color,align:"center");D.Pop();
        if(enabled&&hitTest)Hit(name,x,y,w,h,action);
    }
    protected void Track(double x,double y,double w,double value,string color,double height=3){Box(x,y,w,height,"#5A603D6F");if(double.IsFinite(value))Box(x,y,w*Math.Clamp(value/100,0,1),height,color);}
    protected override void OnMouseMove(MouseEventArgs e){var p=e.GetPosition(this);Cursor=hits.Any(h=>h.Rect.Contains(p))?Cursors.Hand:Cursors.Arrow;Pointer=p;OnPointer(e);InvalidateVisual();}
    protected virtual void OnPointer(MouseEventArgs e){}
    protected override void OnMouseLeave(MouseEventArgs e){Pointer=new(-1,-1);InvalidateVisual();}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){var p=e.GetPosition(this);var hit=hits.LastOrDefault(h=>h.Rect.Contains(p));if(hit.Action is not null){hit.Action();e.Handled=true;}InvalidateVisual();}
    internal object InspectHits()=>hits.Select(h=>new{name=h.Name,x=h.Rect.X,y=h.Rect.Y,width=h.Rect.Width,height=h.Rect.Height}).ToArray();
}
