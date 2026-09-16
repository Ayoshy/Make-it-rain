using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;

// A dedicated drawing of the DualSense front. Geometry and materials are retained;
// only the controls, lighting and short press ripples change during rendering.
internal sealed class DualSenseArtwork
{
    internal const double Width=760,Height=530;
    static Geometry G(string path){var g=Geometry.Parse(path);g.Freeze();return g;}
    static readonly Geometry body=G("M97,53 C129,38 171,29 232,23 C326,13 438,13 529,23 C589,29 630,38 663,53 C688,99 720,194 733,269 C749,365 740,444 718,475 C705,495 685,502 662,494 L634,497 C616,453 596,389 575,354 C560,329 545,323 519,327 C476,333 461,329 441,329 H319 C288,329 275,333 243,327 C217,323 201,331 187,355 C164,393 147,454 129,497 L101,494 C81,502 60,489 48,470 C27,435 21,365 31,284 C42,198 71,98 97,53 Z");
    static readonly Geometry leftShell=G("M97,53 C129,38 175,29 231,23 L249,136 C253,166 238,184 219,208 C177,261 140,333 115,402 L87,481 C81,500 60,488 48,470 C27,435 21,365 31,284 C42,198 71,98 97,53 Z");
    static readonly Geometry touchpad=G("M244,29 C285,17 475,17 516,29 C529,32 534,40 530,54 L510,149 C505,173 494,184 472,187 H288 C266,184 255,173 250,149 L230,54 C226,40 231,32 244,29 Z");
    static readonly Geometry leftBar=G("M232,34 C226,40 227,50 230,64 L248,150 C253,174 266,187 289,189");
    static readonly Geometry gripLight=G("M101,75 C74,145 49,253 50,356 C50,414 57,449 72,467");
    static readonly Geometry dpad=G("M-15,-58 Q0,-65 15,-58 Q20,-56 20,-50 L19,-30 Q18,-23 0,-11 Q-18,-23 -19,-30 L-20,-50 Q-20,-56 -15,-58 Z");
    static readonly Geometry arrow=G("M0,-47 L-6,-39 H6 Z");
    // PlayStation mark, Simple Icons (CC0); source recorded in PROVENANCE.md.
    static readonly Geometry ps=G("M8.984 2.596v17.547l3.915 1.261V6.688c0-.69.304-1.151.794-.991.636.18.76.814.76 1.505v5.875c2.441 1.193 4.362-.002 4.362-3.152 0-3.237-1.126-4.675-4.438-5.827-1.307-.448-3.728-1.186-5.39-1.502zm4.656 16.241l6.296-2.275c.715-.258.826-.625.246-.818-.586-.192-1.637-.139-2.357.123l-4.205 1.5V14.98l.24-.085s1.201-.42 2.913-.615c1.696-.18 3.785.03 5.437.661 1.848.601 2.04 1.472 1.576 2.072-.465.6-1.622 1.036-1.622 1.036l-8.544 3.107V18.86zM1.807 18.6c-1.9-.545-2.214-1.668-1.352-2.32.801-.586 2.16-1.052 2.16-1.052l5.615-2.013v2.313L4.205 17c-.705.271-.825.632-.239.826.586.195 1.637.15 2.343-.12L8.247 17v2.074c-.12.03-.256.044-.39.073-1.939.331-3.996.196-6.038-.479z");
    readonly DrawingGroup chassis=new();
    sealed record Images(BitmapSource Chassis,BitmapSource Stick,BitmapSource PrimaryGlow,BitmapSource SecondaryGlow,BitmapSource Shine,BitmapSource Edge,BitmapSource Lightbar);
    Images? images;
    internal bool CacheReady=>images is not null;
    readonly SolidColorBrush primary,secondary,white=Solid("#F7FCFF"),ink=Solid("#667287"),black=Solid("#090D18");
    readonly LinearGradientBrush cap,button,capSide;
    readonly RadialGradientBrush primaryGlow,secondaryGlow,stickTop,shine;
    readonly Pen rim=new(Solid("#667488"),1.1),fineWhite=new(Solid("#B7FFFFFF"),.9),darkRim=new(Solid("#070A11"),2),glyph=new(Solid("#617084"),2.4);
    readonly Pen primaryLine,secondaryLine,bodyHalo,dpadRim,dpadLit,whiteGlyph=new(Solid("#F7FCFF"),2.4),stickGroove=new(Solid("#697D94"),.9);
    static readonly (int Bit,int Angle)[] directions=[(11,0),(12,180),(13,270),(14,90)];
    readonly Pen[] glowLeft,glowRight;
    readonly Pen[] touchGlow,touchCore;
    readonly DrawingBrush texture;
    readonly Typeface labels=new(DockAppearance.UiFont,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
    readonly Dictionary<string,FormattedText> labelCache=[];
    static SolidColorBrush Solid(string value){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));b.Freeze();return b;}
    static LinearGradientBrush Gradient(params (double Offset,string Color)[] stops)
    {
        var brush=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1)};
        foreach(var (offset,color) in stops)brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(color),offset));
        brush.Freeze();return brush;
    }
    static RadialGradientBrush Glow(Color color,double opacity)
    {
        color.A=(byte)(255*opacity);var brush=new RadialGradientBrush(color,Colors.Transparent);
        brush.Freeze();return brush;
    }
    static Pen[] GlowLines(Color color)=>new[]{(23d,.06),(12d,.14),(5d,.5),(1.8,1d)}.Select(item=>{
        var tint=color;tint.A=(byte)(255*item.Item2);var brush=new SolidColorBrush(tint);brush.Freeze();
        var pen=new Pen(brush,item.Item1){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;
    }).ToArray();
    internal DualSenseArtwork(Action? ready=null):this(DesktopTheme.Current,false)
    {
        var theme=DesktopTheme.Current;var dispatcher=Dispatcher.CurrentDispatcher;
        var worker=new Thread(()=>{
            try{
                var prepared=new DualSenseArtwork(theme,true).images;
                if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(()=>{images=prepared;ready?.Invoke();});
            }
            catch(Exception){/* The retained vector drawing remains available. */}
        }){IsBackground=true,Name="DualSense artwork"};
        worker.SetApartmentState(ApartmentState.STA);worker.Start();
    }
    DualSenseArtwork(ThemeDefinition theme,bool bake)
    {
        primary=Solid(theme.Active[0]);secondary=Solid(theme.Active[3]);
        primaryGlow=Glow(primary.Color,.75);secondaryGlow=Glow(secondary.Color,.7);shine=Glow(Colors.White,.9);
        primaryLine=new Pen(primary,2.6);secondaryLine=new Pen(secondary,2.6);primaryLine.Freeze();secondaryLine.Freeze();
        bodyHalo=new Pen(primary,10);dpadRim=new Pen(ink,2.5);dpadLit=new Pen(white,2);
        foreach(var line in new[]{bodyHalo,dpadRim,dpadLit,whiteGlyph,stickGroove,rim,fineWhite,darkRim,glyph})line.Freeze();
        glowLeft=GlowLines(primary.Color);glowRight=GlowLines(secondary.Color);
        touchGlow=new[]{primary.Color,secondary.Color}.Select(color=>{color.A=70;var brush=new SolidColorBrush(color);brush.Freeze();var pen=new Pen(brush,11){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;}).ToArray();
        touchCore=new[]{primary,secondary}.Select(brush=>{var pen=new Pen(brush,2.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;}).ToArray();
        cap=Gradient((0,"#111723"),(.45,"#445166"),(1,"#111923"));
        capSide=Gradient((0,"#8390A4"),(.2,"#242F40"),(.65,"#0B101A"),(1,"#515D73"));
        button=Gradient((0,"#FBFCFF"),(.45,theme.Pearl[0]),(.85,"#C0C9DB"),(1,"#8E9BB0"));
        stickTop=new RadialGradientBrush();stickTop.GradientOrigin=new(.38,.29);
        foreach(var (offset,color) in new[]{(0d,"#424D60"),(.62,"#242F41"),(.87,"#111B29"),(1d,"#8492A7")})stickTop.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(color),offset));
        stickTop.Freeze();
        var dots=new DrawingGroup();using(var dc=dots.Open()){dc.DrawEllipse(Solid("#30C2D3E9"),null,new(1,1),.5,.5);dc.DrawEllipse(Solid("#18000000"),null,new(2.7,2.7),.6,.6);}
        dots.Freeze();texture=new DrawingBrush(dots){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,ViewboxUnits=BrushMappingMode.Absolute,Viewport=new(0,0,4,4),Viewbox=new(0,0,4,4)};texture.Freeze();
        BuildChassis(theme);chassis.Freeze();
        if(bake){
            // Three samples per model unit cover the maximum dock on Ayo's 1440p screens.
            var bodyImage=Raster((int)Width,(int)Height,dc=>{
                dc.DrawDrawing(chassis);Dpad(dc,new double[17],false);
                Small(dc,210,78,0,-12,false);Small(dc,550,78,0,12,true);
                Face(dc,605,104,3,0,0);Face(dc,657,156,1,0,0);Face(dc,553,156,2,0,0);Face(dc,605,208,0,0,0);
                Well(dc,264,254);Well(dc,496,254);
            },3);
            var stick=Raster(128,128,dc=>{dc.PushTransform(new TranslateTransform(64,64));StickCap(dc);dc.Pop();},3);
            var left=Raster(64,64,dc=>dc.DrawEllipse(primaryGlow,null,new(32,32),32,32));
            var right=Raster(64,64,dc=>dc.DrawEllipse(secondaryGlow,null,new(32,32),32,32));
            var gloss=Raster(64,64,dc=>dc.DrawEllipse(shine,null,new(32,32),32,32));
            var edge=Raster((int)Width,(int)Height,dc=>dc.DrawGeometry(null,bodyHalo,body));
            var lights=Raster(360,210,dc=>{
                dc.PushTransform(new TranslateTransform(-200,0));foreach(var line in glowLeft)dc.DrawGeometry(null,line,leftBar);
                dc.PushTransform(new MatrixTransform(-1,0,0,1,760,0));foreach(var line in glowRight)dc.DrawGeometry(null,line,leftBar);dc.Pop();dc.Pop();
            },3);
            images=new(bodyImage,stick,left,right,gloss,edge,lights);
        }
    }
    static BitmapSource Raster(int width,int height,Action<DrawingContext> draw,int samples=1)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())draw(dc);
        var bitmap=new RenderTargetBitmap(width*samples,height*samples,96*samples,96*samples,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
    }
    void DrawGlow(DrawingContext dc,Point center,double rx,double ry,bool right=false,bool whiteLight=false)
    {
        if(images is {} cached)dc.DrawImage(whiteLight?cached.Shine:right?cached.SecondaryGlow:cached.PrimaryGlow,new Rect(center.X-rx,center.Y-ry,rx*2,ry*2));
        else dc.DrawEllipse(whiteLight?shine:right?secondaryGlow:primaryGlow,null,center,rx,ry);
    }
    void BuildChassis(ThemeDefinition theme)
    {
        using var dc=chassis.Open();
        dc.PushTransform(new TranslateTransform(0,5));dc.DrawGeometry(black,null,body);dc.Pop();
        dc.DrawGeometry(Gradient((0,"#3D4758"),(.28,"#1A2230"),(.63,"#111723"),(1,"#424C60")),rim,body);
        dc.PushClip(body);dc.DrawEllipse(Glow(secondary.Color,.14),null,new(380,289),280,158);dc.Pop();
        var shell=Gradient((0,"#D4DEEF"),(.18,"#FFFFFF"),(.43,theme.Pearl[0]),(.67,"#D9E1F2"),(.86,theme.Pearl[2]),(1,"#687A97"));
        void Grip()
        {
            dc.DrawGeometry(shell,fineWhite,leftShell);
            dc.PushClip(leftShell);dc.DrawEllipse(shine,null,new(90,196),68,242);
            dc.DrawEllipse(Glow(secondary.Color,.12),null,new(210,329),104,248);dc.Pop();
            dc.DrawGeometry(null,new Pen(Solid("#82FFFFFF"),2.5),gripLight);
        }
        Grip();dc.PushTransform(new MatrixTransform(-1,0,0,1,760,0));Grip();dc.Pop();
        dc.PushTransform(new TranslateTransform(0,5));dc.DrawGeometry(black,new Pen(black,7),touchpad);dc.Pop();
        dc.DrawGeometry(Gradient((0,"#FFFFFF"),(.28,"#E8EDF9"),(.58,theme.Pearl[0]),(1,"#A7B4CC")),fineWhite,touchpad);
        dc.PushClip(touchpad);dc.DrawEllipse(Glow(Colors.White,.7),null,new(376,34),220,49);dc.Pop();
        for(int row=0;row<2;row++)for(int dot=0;dot<5-row;dot++)dc.DrawEllipse(black,new Pen(Solid("#627C899C"),.65),new(358+dot*11+row*5.5,205+row*11),3.9,3.9);
        dc.PushTransform(new TranslateTransform(357,235));dc.PushTransform(new ScaleTransform(1.85,1.85));
        dc.DrawGeometry(Solid("#050A12"),new Pen(Solid("#728298"),.55),ps);dc.Pop();dc.Pop();
        dc.DrawRoundedRectangle(Gradient((0,"#667283"),(1,"#171E2C")),darkRim,new(364,293,32,6),3,3);
        dc.DrawLine(new Pen(Solid("#56667C"),1),new(380,306),new(380,315));
        dc.DrawRoundedRectangle(null,new Pen(Solid("#56667C"),1),new(377.7,305,4.6,7),2,2);
    }
    internal void Draw(DrawingContext dc,DualSenseState state,double[] levels,double[] ripples,double[] axes,double seconds,DualSenseTouchTrail? trail=null)
    {
        double energy=Math.Clamp(levels.Sum()/3+state.LT/65534d+state.RT/65534d,0,1),breath=.8+.2*Math.Sin(seconds*1.7);
        if(state.Connected==1){
            dc.PushOpacity(.3+energy*.5);DrawGlow(dc,new(210,477),207,33);DrawGlow(dc,new(550,477),207,33,true);dc.Pop();
            dc.PushOpacity(.08+energy*.1);if(images is {} outline)dc.DrawImage(outline.Edge,new Rect(0,0,Width,Height));else dc.DrawGeometry(null,bodyHalo,body);dc.Pop();
        }
        Trigger(dc,119,state.LT/32767d,levels[9],false);Trigger(dc,546,state.RT/32767d,levels[10],true);
        if(images is {} cached)dc.DrawImage(cached.Chassis,new Rect(0,0,Width,Height));
        else{
            dc.DrawDrawing(chassis);Dpad(dc,new double[17],false);
            Small(dc,210,78,0,-12,false);Small(dc,550,78,0,12,true);
            Face(dc,605,104,3,0,0);Face(dc,657,156,1,0,0);Face(dc,553,156,2,0,0);Face(dc,605,208,0,0,0);
            Well(dc,264,254);Well(dc,496,254);
        }
        double light=state.Connected==1?Math.Min(1,breath*.75+energy*.3):.13;
        dc.PushOpacity(light);
        if(images is {} lights)dc.DrawImage(lights.Lightbar,new Rect(200,0,360,210));
        else{foreach(var line in glowLeft)dc.DrawGeometry(null,line,leftBar);dc.PushTransform(new MatrixTransform(-1,0,0,1,760,0));foreach(var line in glowRight)dc.DrawGeometry(null,line,leftBar);dc.Pop();}
        dc.Pop();
        if(levels[4]>.001)Small(dc,210,78,levels[4],-12,false);
        if(levels[6]>.001)Small(dc,550,78,levels[6],12,true);
        Dpad(dc,levels,true);
        if(levels[3]+ripples[3]>.001)Face(dc,605,104,3,levels[3],ripples[3]);
        if(levels[1]+ripples[1]>.001)Face(dc,657,156,1,levels[1],ripples[1]);
        if(levels[2]+ripples[2]>.001)Face(dc,553,156,2,levels[2],ripples[2]);
        if(levels[0]+ripples[0]>.001)Face(dc,605,208,0,levels[0],ripples[0]);
        Stick(dc,264,254,axes[0],axes[1],levels[7],ripples[7],false);
        Stick(dc,496,254,axes[2],axes[3],levels[8],ripples[8],true);
        if(levels[5]>.01){dc.PushOpacity(levels[5]);DrawGlow(dc,new(380,257),44,35);dc.PushTransform(new TranslateTransform(357,235));dc.PushTransform(new ScaleTransform(1.85,1.85));dc.DrawGeometry(white,null,ps);dc.Pop();dc.Pop();dc.Pop();}
        if(levels[15]>.01){dc.PushOpacity(levels[15]*.35);dc.DrawGeometry(primary,null,touchpad);dc.Pop();}
        if(levels[16]>.01){dc.PushOpacity(levels[16]);dc.DrawRoundedRectangle(primary,primaryLine,new(364,293,32,6),3,3);dc.Pop();}
        if(trail is not null)DrawTouch(dc,trail,seconds);
    }
    static Point ProjectTouch(Point p)=>new(233+21*p.Y+p.X*(294-42*p.Y),30+p.Y*154);
    void DrawTouch(DrawingContext dc,DualSenseTouchTrail trail,double seconds)
    {
        dc.PushClip(touchpad);
        for(int finger=0;finger<2;finger++){
            var points=trail.Points(finger);
            for(int i=1;i<points.Count;i++){
                if(points[i-1].Stroke!=points[i].Stroke)continue;
                double fade=Math.Clamp(1-(seconds-points[i].At)/DualSenseTouchTrail.Lifetime,0,1);fade*=fade;
                var from=ProjectTouch(points[i-1].Position);var to=ProjectTouch(points[i].Position);
                dc.PushOpacity(fade);dc.DrawLine(touchGlow[finger],from,to);dc.DrawLine(touchCore[finger],from,to);dc.Pop();
            }
            if(trail.Down(finger)&&points.Count>0){
                var point=ProjectTouch(points[^1].Position);DrawGlow(dc,point,17,17,finger==1);
                dc.DrawEllipse(finger==0?primary:secondary,null,point,4.5,4.5);dc.DrawEllipse(white,null,point,2,2);
            }
        }
        dc.Pop();
    }
    void Dpad(DrawingContext dc,double[] levels,bool activeOnly)
    {
        foreach(var (bit,angle) in directions)
        {
            if(activeOnly&&levels[bit]<.001)continue;
            dc.PushTransform(new TranslateTransform(155,155));dc.PushTransform(new RotateTransform(angle));
            dc.PushTransform(new TranslateTransform(0,levels[bit]*1.5));
            dc.DrawGeometry(button,dpadRim,dpad);
            dc.PushOpacity(levels[bit]);dc.DrawGeometry(primary,dpadLit,dpad);dc.Pop();
            dc.DrawGeometry(levels[bit]>.4?white:ink,null,arrow);dc.Pop();dc.Pop();dc.Pop();
        }
    }
    void Trigger(DrawingContext dc,double x,double value,double shoulder,bool right)
    {
        var tint=right?secondary:primary;
        dc.PushTransform(new TranslateTransform(x,0));
        dc.PushOpacity(Math.Min(1,value*.75+shoulder*.5));DrawGlow(dc,new(47,32),74,44,right);dc.Pop();
        dc.DrawRoundedRectangle(cap,darkRim,new(0,14+value*8,95,37-value*8),12,9);
        dc.DrawRoundedRectangle(null,fineWhite,new(3,17+value*8,89,30-value*8),10,8);
        dc.PushOpacity(value);dc.DrawRoundedRectangle(tint,null,new(5,18+value*8,85,25-value*8),8,7);dc.Pop();
        Label(dc,right?"R2":"L2",47,20+value*6,12);
        dc.DrawRoundedRectangle(capSide,darkRim,new(4,43,87,13),5,5);
        dc.PushOpacity(shoulder);dc.DrawRoundedRectangle(tint,null,new(5,43,85,12),5,5);dc.Pop();
        Label(dc,right?"R1":"L1",47,43,8);dc.Pop();
    }
    void Label(DrawingContext dc,string text,double x,double y,double size)
    {
        if(!labelCache.TryGetValue(text,out var formatted)){formatted=new FormattedText(text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,labels,size,white,1);labelCache[text]=formatted;}
        dc.DrawText(formatted,new(x-formatted.Width/2,y));
    }
    void Small(DrawingContext dc,double x,double y,double level,double angle,bool options)
    {
        dc.PushTransform(new TranslateTransform(x,y));dc.PushTransform(new RotateTransform(angle));
        dc.DrawRoundedRectangle(button,rim,new(-8,-15,16,32),7,7);
        dc.PushOpacity(level);dc.DrawRoundedRectangle(primary,primaryLine,new(-8,-15,16,32),7,7);dc.Pop();
        if(options)for(int i=0;i<3;i++)dc.DrawLine(glyph,new(-5,-24-i*4),new(5,-24-i*4));
        else for(int i=0;i<3;i++)dc.DrawLine(glyph,new((i-1)*6,-23),new((i-1)*8,-30));
        dc.Pop();dc.Pop();
    }
    void Ripple(DrawingContext dc,Point center,double value,double radius,bool right=false)
    {
        if(value<.01)return;dc.PushOpacity(value*.7);
        dc.DrawEllipse(null,right?secondaryLine:primaryLine,center,radius+(1-value)*28,radius+(1-value)*28);dc.Pop();
    }
    void Face(DrawingContext dc,double x,double y,int bit,double level,double ripple)
    {
        var center=new Point(x,y);Ripple(dc,center,ripple,28);
        dc.PushOpacity(level*.85);DrawGlow(dc,center,58,58);dc.Pop();
        dc.DrawEllipse(black,null,new(x,y+2.2),25,25);dc.DrawEllipse(button,rim,center,24,24);
        dc.PushOpacity(level);dc.DrawEllipse(primary,primaryLine,new(x,y+level*1.5),23,23);dc.Pop();
        dc.PushOpacity(.65);DrawGlow(dc,new(x-6,y-9),15,8,whiteLight:true);dc.Pop();
        dc.PushTransform(new TranslateTransform(x,y+level*1.5));var line=level>.35?whiteGlyph:glyph;
        switch(bit){
            case 0:dc.DrawLine(line,new(-9,-9),new(9,9));dc.DrawLine(line,new(-9,9),new(9,-9));break;
            case 1:dc.DrawEllipse(null,line,new(0,0),12,12);break;
            case 2:dc.DrawRoundedRectangle(null,line,new(-10,-10,20,20),.5,.5);break;
            case 3:dc.DrawLine(line,new(0,-12),new(-12,10));dc.DrawLine(line,new(-12,10),new(12,10));dc.DrawLine(line,new(12,10),new(0,-12));break;
        }
        dc.Pop();
    }
    void Well(DrawingContext dc,double x,double y)
    {
        dc.DrawEllipse(capSide,darkRim,new(x,y),59,59);dc.DrawEllipse(black,fineWhite,new(x,y),52,52);
    }
    void StickCap(DrawingContext dc)
    {
        dc.DrawEllipse(black,null,new(2,4),45,45);dc.DrawEllipse(capSide,fineWhite,new(0,0),45,45);dc.DrawEllipse(stickTop,null,new(0,-1),40,40);
        dc.PushOpacity(.75);dc.DrawEllipse(texture,null,new(0,-1),38,38);dc.Pop();
        dc.DrawEllipse(null,stickGroove,new(0,-1),32,32);
    }
    void Stick(DrawingContext dc,double x,double y,double vx,double vy,double level,double ripple,bool right)
    {
        var center=new Point(x,y);var tint=right?secondary:primary;var line=right?secondaryLine:primaryLine;
        double travel=Math.Min(1,Math.Sqrt(vx*vx+vy*vy));
        dc.PushOpacity(.18+travel*.65+level*.3);DrawGlow(dc,center,85,85,right);dc.DrawEllipse(null,line,center,54,54);dc.Pop();
        Ripple(dc,center,ripple,56,right);
        dc.PushTransform(new TranslateTransform(x+vx*17,y+vy*17+level*2));
        if(images is {} cached)dc.DrawImage(cached.Stick,new Rect(-64,-64,128,128));else StickCap(dc);
        dc.PushOpacity(level*.7);dc.DrawEllipse(tint,null,new(0,-1),39,39);dc.Pop();dc.Pop();
    }
}
