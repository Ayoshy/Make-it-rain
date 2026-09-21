using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using Rectangle=System.Windows.Shapes.Rectangle;

namespace Battlestation;

// An aquarium that follows the music dock's spectrum. It owns no setting, never
// writes a file and exposes no control: the water, the plants and the fish only
// move while the dock is exposed, exactly like the other animated docks.
internal sealed class AquariumSurface : Surface,IDisposable
{
    const double Frame=1/30d;
    sealed class Fish
    {
        internal double X,Y,Speed,Size,Phase,Drift,Depth,Swim,Pitch,Burst;
        internal string Back="#A9F8FF",Belly="#F2E5FF",Fins="#72E7D5FA";
        internal int Kind;
    }
    // Generated artwork replaces the drawn fish and plants when it exists, under
    // assets/Aquarium/. Without it the same shapes are drawn, so the tank never
    // depends on the files; the folder is read once per process.
    static readonly Dictionary<string,BitmapSource?> artwork=[];
    readonly Random random=new(0x5EA1F0);
    readonly Fish[] fish;
    readonly double[] bubbleX,bubbleY,bubbleSize,bubblePhase,bubbleSpeed;
    readonly Dictionary<string,Brush> skins=[];
    readonly Brush[] blades=[];
    readonly Rectangle water;
    readonly WaterEffect? effect;
    readonly bool backdrop;
    // The side render puts the tank along the bottom of the frame: the shader's
    // surface, the fish and the bubbles follow its waterline instead of the dock's
    // middle. The fraction is the waterline of island.png, measured from the top.
    const double IslandWaterline=0.6697;
    double fishTop=.283,fishBottom=.705,bubbleLine=.20,level=.80,minY=.24,maxY=.86;
    readonly Point3D[] trail=new Point3D[5];
    Point pointer=new(-1,-1);
    double lastTrail;
    double elapsed,budget,lastFrame=-1,energy,current;
    bool active,disposed;
    internal AquariumSurface(Station station):base(station,15)
    {
        Width=700;Height=420;
        (string Back,string Belly,string Fins)[] species=[
            ("#A9F8FF","#F2E5FF","#72E7D5FA"),("#E4ABFA","#EFCBE5","#CDA9F0E8"),("#EF9FDA","#FFF0D9","#FF95C1"),
            ("#C79FE2","#E6D9F0","#9CDDD0EE"),("#F59DDFEC","#DBE9F5","#A6F1E4FF"),("#A2DFDC","#E9E0F2","#BDEDE5"),
            ("#FF95C1","#F0D5FD","#E4C8E0")];
        fish=Enumerable.Range(0,5+random.Next(3)).Select(index=>new Fish{
            X=random.NextDouble(),Y=.18+random.NextDouble()*.62,Speed=.026+random.NextDouble()*.03,
            Size=12+random.NextDouble()*10,Phase=random.NextDouble()*Math.PI*2,Drift=1.1+random.NextDouble()*1.5,
            Depth=.4+random.NextDouble()*.45,Swim=index%2==0?1:-1,Burst=.6+random.NextDouble()*.9,
            Back=species[index%species.Length].Back,Belly=species[index%species.Length].Belly,Fins=species[index%species.Length].Fins,Kind=index%3}).ToArray();
        int count=20;
        bubbleX=new double[count];bubbleY=new double[count];bubbleSize=new double[count];bubblePhase=new double[count];bubbleSpeed=new double[count];
        for(int i=0;i<count;i++){bubbleX[i]=random.NextDouble();bubbleY[i]=random.NextDouble();bubbleSize[i]=1.8+random.NextDouble()*3.8;bubblePhase[i]=random.NextDouble()*Math.PI*2;bubbleSpeed[i]=.028+random.NextDouble()*.075;}
        // Brushes follow the theme through the shared swatches, so they are built once.
        foreach(var swimmer in fish)skins.TryAdd(swimmer.Back,DesktopTheme.Gradient(swimmer.Back,swimmer.Belly,90));
        blades=[DesktopTheme.Gradient("#18B483D8","#A8D5B58C",90),DesktopTheme.Gradient("#41B47FDB","#BDEDE5",90)];
        for(int index=0;index<trail.Length;index++)trail[index]=new Point3D(-1000,-1000,0);
        // The water is its own layer under the drawings: the island render when it
        // exists (assets/Aquarium/island.png), otherwise a themed brush as the base
        // and, when the GPU can run it, the shader that turns it into a liquid.
        var island=Artwork("island");
        backdrop=island is not null;
        water=new Rectangle{Fill=island is null?WaterBrush():BackdropBrush(island),IsHitTestVisible=false,SnapsToDevicePixels=true};
        // WPF only accepts ImplicitInput, a BitmapCacheBrush, a VisualBrush or an
        // ImageBrush as a shader sampler: the shader reads the layer's own fill,
        // which is the island render or the themed water brush.
        if(WaterEffect.Available){effect=new WaterEffect{Input=System.Windows.Media.Effects.Effect.ImplicitInput};water.Effect=effect;}
        Band();
    }
    // The island render fills the whole tank without being stretched: it is scaled
    // uniformly and centred, and the dock crops whatever overflows.
    static Brush BackdropBrush(BitmapSource backdrop)
    {
        var brush=new ImageBrush(backdrop){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};
        brush.Freeze();
        return brush;
    }
    internal FrameworkElement WaterLayer=>water;
    // Base water: the same theme-tinted gradient and floor as before.
    Brush WaterBrush()
    {
        var group=new DrawingGroup();
        // Air stays almost clear above the waterline; the shader paints the volume.
        group.Children.Add(new GeometryDrawing(DesktopTheme.Gradient("#06000000","#1A6E4093",90),null,new RectangleGeometry(new Rect(0,0,1,1))));
        // Not frozen: the theme brushes stay animatable, exactly like the shared chrome.
        return new DrawingBrush(group){Stretch=Stretch.Fill};
    }
    internal bool Animating=>active;
    internal double Elapsed=>elapsed;
    // Diagnostics: the desktop publishes what the tank is doing, nothing more.
    internal object Inspect()=>new{animating=active,elapsed=Math.Round(elapsed,2),energy=Math.Round(energy,3),fish=fish.Length,artwork=Artwork("fish-a") is not null||Artwork("plant-a") is not null,
        water=backdrop?"island":effect is not null?"shader":"dessinée",wake=Math.Round(trail.Max(point=>point.Z),2),pointer=new{pointer.X,pointer.Y}};
    internal void SetActive(bool value)
    {
        if(active==value||disposed)return;
        active=value;
        if(value){lastFrame=-1;budget=0;CompositionTarget.Rendering+=Animate;}
        else CompositionTarget.Rendering-=Animate;
    }
    void Animate(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs frame)return;
        double now=frame.RenderingTime.TotalSeconds;
        if(now==lastFrame)return;
        double delta=lastFrame<0?Frame:Math.Clamp(now-lastFrame,0,.2);
        lastFrame=now;budget+=delta;
        if(budget<Frame)return; // About thirty images per second, never more.
        double step=Math.Min(budget,.2);budget=0;Advance(step);
    }
    // Deterministic step: the desktop only reaches it through the rendering gate
    // above, the render tests call it directly.
    internal void Advance(double seconds)
    {
        if(!active||disposed||!double.IsFinite(seconds)||seconds<=0)return;
        seconds=Math.Min(seconds,.25);elapsed+=seconds;
        var bands=Station.MusicBands;
        double bass=Bands(bands,0,4),middle=Bands(bands,4,4),treble=Bands(bands,8,4);
        energy+=(Math.Clamp(.42+bass*1.5+middle*.25,0,1)-energy)*Math.Min(1,seconds*3);
        Wake(seconds);
        // A slow current runs the whole tank; the school follows it with the music.
        current+=(Math.Sin(elapsed*.23)*.5+.5-current)*Math.Min(1,seconds*.35);
        double center=0;foreach(var swimmer in fish)center+=swimmer.Y;center/=fish.Length;
        foreach(var swimmer in fish)
        {
            // A resting fish still swims: the spectrum adds bursts, never a frozen pose.
            double burst=Math.Max(0,Math.Sin(elapsed*swimmer.Burst+swimmer.Phase*2.3));
            // Slower than the first pass: about fifteen to twenty seconds to cross.
            double speed=.038+swimmer.Speed*(.5+energy*1.0+burst*.4)*(1+current*.35);
            swimmer.X+=swimmer.Swim*speed*seconds*(1+swimmer.Depth*.4);
            // Light cohesion with the school, own vertical drift, and a slow wander.
            double before=swimmer.Y;
            swimmer.Y+=(center-swimmer.Y)*.42*seconds+Math.Sin(elapsed*swimmer.Drift+swimmer.Phase)*.1*seconds*(1+energy*1.4);
            // The free surface stays inside the water: the dock's own level, or the
            // waterline of the rendered side view, cropped by the dock's shape.
            swimmer.Y=Math.Clamp(swimmer.Y,minY,maxY);
            double climb=(swimmer.Y-before)/Math.Max(seconds,.001);
            swimmer.Pitch+=(Math.Clamp(climb*46,-16,16)-swimmer.Pitch)*Math.Min(1,seconds*4);
            // The cursor pushes the water away: a nearby fish gives way and starts up.
            if(pointer.X>=0)
            {
                double dx=swimmer.X*Width-pointer.X,dy=SwimLine(swimmer.Y)*Height-pointer.Y,distance=Math.Sqrt(dx*dx+dy*dy);
                if(distance<170&&distance>1)
                {
                    swimmer.Y=Math.Clamp(swimmer.Y+(dy/distance)*.5*seconds,minY,maxY);
                    swimmer.X+=swimmer.Swim*speed*seconds*.6;
                }
            }
            double margin=swimmer.Size/Width+.02;
            if(swimmer.X>1+margin){swimmer.X=-margin;swimmer.Swim=1;}
            if(swimmer.X<-margin){swimmer.X=1+margin;swimmer.Swim=-1;}
        }
        for(int i=0;i<bubbleY.Length;i++)
        {
            bubbleY[i]-=bubbleSpeed[i]*(1+middle*1.1+treble*.6)*seconds;
            bubbleX[i]+=Math.Sin(elapsed*1.1+bubblePhase[i])*.02*seconds;
            // Bubbles pop at the free surface instead of the top of the glass.
            if(bubbleY[i]<bubbleLine){bubbleY[i]=1.04;bubbleX[i]=random.NextDouble();}
            bubbleX[i]=Math.Clamp(bubbleX[i],.02,.98);
        }
        Refresh();
    }
    static double Bands(float[] bands,int start,int count)
    {
        if(bands is null)return 0;
        double total=0;int seen=0;
        for(int i=start;i<start+count&&i<bands.Length;i++){total+=bands[i];seen++;}
        double value=seen==0?0:total/seen;
        return double.IsFinite(value)?Math.Clamp(value,0,1):0;
    }
    // Where the water sits in the dock, as fractions of its height.
    void Band()
    {
        fishTop=.283;fishBottom=.705;bubbleLine=.20;level=.80;minY=.24;maxY=.86;
        if(!backdrop)return;
        double image=1280/720d,dock=Math.Max(1,Width)/Math.Max(1,Height),line=IslandWaterline;
        if(dock>image)
        {
            // A wider dock crops the image vertically; the waterline moves with it.
            double visible=image/dock,top=(1-visible)/2;
            line=(IslandWaterline-top)/visible;
        }
        line=Math.Clamp(line,.02,.96);
        level=1-line;
        fishTop=Math.Min(.94,line+.035);fishBottom=.94;bubbleLine=Math.Min(.98,line+.008);
        minY=.02;maxY=.98;
    }
    double SwimLine(double y)=>fishTop+y*(fishBottom-fishTop);
    // The cursor drags the water: recent positions decay into the shader wake, and
    // the shader receives the same music bands as the fish.
    void Wake(double seconds)
    {
        for(int index=0;index<trail.Length;index++)
        {
            var point=trail[index];
            if(point.Z<=0)continue;
            double strength=point.Z*Math.Exp(-seconds/.55);
            trail[index]=strength<.04?new Point3D(-1000,-1000,0):new Point3D(point.X,point.Y,strength);
        }
        Band();
        if(effect is null)return;
        var bands=Station.MusicBands;
        effect.Time=elapsed;
        effect.Resolution=new Point(Math.Max(1,Width),Math.Max(1,Height));
        effect.Pointer=pointer;
        effect.Scene=backdrop?1:0;
        effect.Bass=Bands(bands,0,4);effect.Mid=Bands(bands,4,4);effect.Treble=Bands(bands,8,4);effect.Energy=energy;
        effect.Trail0=trail[0];effect.Trail1=trail[1];effect.Trail2=trail[2];effect.Trail3=trail[3];effect.Trail4=trail[4];
        // Soft themed teal for the caustics, near-white only for the highlights.
        // Water is a volume: a light body just under the surface, a dark floor, a
        // thin air tint above. Every colour comes from the running theme.
        effect.Level=level;
        // The rendered scene keeps its own palette: its caustics are jade, not the
        // pale theme tint that was chosen for the painted water.
        effect.Tint=backdrop?Level(DesktopTheme.Color("#FF3FBF9F")):Level(DesktopTheme.Color("#22DACDEC"));
        effect.Light=Level(DesktopTheme.Color("#F8F2FF"));
        effect.Body=Shade(DesktopTheme.Color("#A9F8FF"),.72);
        effect.Deep=Shade(DesktopTheme.Color("#12804DAE"),.78);
        effect.Air=Shade(DesktopTheme.Color("#261B1227"),.55);
    }
    static Point3D Level(Color color)=>new(color.R/255d,color.G/255d,color.B/255d);
    static Point3D Shade(Color color,double factor)=>new(color.R/255d*factor,color.G/255d*factor,color.B/255d*factor);
    protected override void OnPointer(MouseEventArgs e)
    {
        var position=e.GetPosition(this);
        if(!IsMouseOver||position.X<0||position.Y<0||position.X>Width||position.Y>Height)return;
        pointer=position;
        if(trail[0].Z>.5&&elapsed-lastTrail<.05)return; // about twenty drops per second
        lastTrail=elapsed;
        Array.Copy(trail,0,trail,1,trail.Length-1);
        trail[0]=new Point3D(position.X,position.Y,1);
    }
    protected override void OnMouseLeave(MouseEventArgs e){pointer=new Point(-1,-1);base.OnMouseLeave(e);}
    protected override void Paint()
    {
        double w=Width,h=Height;if(w<=8||h<=8)return;
        D.PushClip(new RectangleGeometry(new Rect(0,0,w,h),24,24));
        double bass=Bands(Station.MusicBands,0,4),middle=Bands(Station.MusicBands,4,4),treble=Bands(Station.MusicBands,8,4);
        // Transparent but hit-testable on the whole dock, so the cursor wake works
        // everywhere; the water itself is the layer below, or drawn here without a shader.
        D.DrawRectangle(B("#00000000"),null,new Rect(0,0,w,h));
        if(effect is null&&!backdrop)FallbackWater(w,h,bass,treble);
        Plants(w,h,middle);
        foreach(var swimmer in fish)Swimmer(swimmer,w,h);
        for(int i=0;i<bubbleY.Length;i++)
        {
            double x=bubbleX[i]*w+Math.Sin(elapsed*1.6+bubblePhase[i])*3.4,y=bubbleY[i]*h;
            D.DrawEllipse(B("#60EEE4FF"),null,new Point(x,y),bubbleSize[i],bubbleSize[i]);
        }
        D.Pop();
    }
    // Without a pixel shader the water keeps its former drawn look: the themed
    // column and floor, a waterline and three light ribbons.
    void FallbackWater(double w,double h,double bass,double treble)
    {
        // Same tank without a shader: water up to four fifths, air above.
        double top=h*.2;
        D.DrawRectangle(DesktopTheme.Gradient("#12804DAE","#08B483D8",90),null,new Rect(0,top,w,h-top));
        D.DrawRectangle(DesktopTheme.Gradient("#12804DAE","#08B483D8",90),null,new Rect(0,h-46,w,46));
        Waterline(w,bass);
        for(int band=0;band<3;band++)Caustics(band,w,h,bass,treble);
    }
    void Waterline(double w,double bass)
    {
        var geometry=new StreamGeometry();
        using(var context=geometry.Open())
        {
            context.BeginFigure(new Point(0,0),true,true);
            for(int step=0;step<=18;step++)
            {
                double x=w*step/18;
                context.LineTo(new Point(x,10+Math.Sin(step*.7+elapsed*1.5)*3+bass*10),true,false);
            }
            context.LineTo(new Point(w,0),true,false);
        }
        geometry.Freeze();
        D.DrawGeometry(B("#26EEE1F8"),null,geometry);
    }
    void Caustics(int band,double w,double h,double bass,double treble)
    {
        // Three thin ribbons of light: the amplitude follows the bass, the travel
        // speed a little of the treble. Slow enough to read, never stroboscopic.
        double amplitude=5+bass*26+treble*6,thickness=10+bass*30+treble*6,y=h*(.16+band*.22)+Math.Sin(elapsed*.6+band)*6;
        var geometry=new StreamGeometry();
        using(var context=geometry.Open())
        {
            context.BeginFigure(new Point(0,y),true,true);
            for(int step=0;step<=16;step++)
            {
                double x=w*step/16;
                context.LineTo(new Point(x,y+Math.Sin(step*.8+elapsed*(1.4+band*.35)+band*2)*amplitude),true,false);
            }
            for(int step=16;step>=0;step--)
            {
                double x=w*step/16;
                context.LineTo(new Point(x,y+thickness+Math.Sin(step*.8+elapsed*(1.4+band*.35)+band*2)*amplitude),true,false);
            }
        }
        geometry.Freeze();
        D.DrawGeometry(B(band==1?"#22DACDEC":"#16DACDEC"),null,geometry);
    }
    void Plants(double w,double h,double middle)
    {
        double bottom=h-38;
        for(int cluster=0;cluster<2;cluster++)
        {
            double baseX=w*(cluster==0?.16:.82);
            var sprite=Artwork(cluster==0?"plant-a":"plant-b");
            if(sprite is not null)
            {
                // The rendered tank is short: its plants stop under the waterline.
                double height=h*(backdrop?(fishBottom-fishTop)*.85:.48);
                D.PushTransform(new RotateTransform(Math.Sin(elapsed*.55+cluster*1.7)*(3+middle*7),baseX,bottom));
                D.PushOpacity(.94);
                D.DrawImage(sprite,Fit(sprite,new Rect(baseX-w*.17,bottom-height,w*.34,height)));
                D.Pop();D.Pop();continue;
            }
            for(int blade=0;blade<6;blade++)
            {
                double spread=(blade-2.5)*w*.032,height=h*(.18+((blade*3)%5)*.08),width=8.6-Math.Abs(blade-2.5)*1.1;
                double lean=Math.Sin(elapsed*(.7+blade*.11)+blade*1.3+cluster)*(10+middle*18)+spread*.45;
                var shape=Blade(baseX+spread,bottom,height,lean,width);
                shape.Freeze();
                D.DrawGeometry(blades[blade%2],null,shape);
            }
            for(int leaf=0;leaf<3;leaf++)
            {
                double x=baseX+(leaf-1)*w*.03,size=9+((leaf*5)%3)*3;
                D.DrawEllipse(B("#41B47FDB"),null,new Point(x+Math.Sin(elapsed*.8+leaf)*4,bottom-size*.6),size*.5,size);
            }
        }
    }
    static StreamGeometry Blade(double baseX,double bottom,double height,double lean,double width)
    {
        double tipX=baseX+lean,top=bottom-height;
        var geometry=new StreamGeometry();
        using(var context=geometry.Open())
        {
            context.BeginFigure(new Point(baseX-width,bottom),true,true);
            context.QuadraticBezierTo(new Point(baseX-width*.5+lean*.45,bottom-height*.55),new Point(tipX,top),true,false);
            context.QuadraticBezierTo(new Point(baseX+width*.5+lean*.45,bottom-height*.55),new Point(baseX+width,bottom),true,false);
        }
        return geometry;
    }
    void Swimmer(Fish swimmer,double w,double h)
    {
        double x=swimmer.X*w,y=SwimLine(swimmer.Y)*h,size=swimmer.Size*(1+energy*.1);
        var sprite=Artwork("fish-"+(char)('a'+swimmer.Kind));
        D.PushOpacity(Math.Clamp(swimmer.Depth+energy*.18,.35,1));
        D.PushTransform(new RotateTransform(swimmer.Pitch*.5,x,y));
        if(sprite is not null)
        {
            bool mirror=swimmer.Swim<0;
            if(mirror)D.PushTransform(new ScaleTransform(-1,1,x,y));
            // Static art still breathes: a small squash follows the tail rhythm.
            double beat=Math.Sin(elapsed*(4.6+swimmer.Speed*26)+swimmer.Phase)*.02;
            D.PushTransform(new ScaleTransform(1+beat,1-beat*.6,x,y));
            D.DrawImage(sprite,Fit(sprite,new Rect(x-size*2.1,y-size*1.35,size*4.2,size*2.7)));
            D.Pop();
            if(mirror)D.Pop();
        }
        else Body(swimmer,x,y,size);
        D.Pop();D.Pop();
    }
    void Body(Fish swimmer,double x,double y,double size)
    {
        double body=size,half=size*.5,wag=Math.Sin(elapsed*(4.6+swimmer.Speed*26)+swimmer.Phase)*11*(1+energy*.5);
        var shape=new StreamGeometry();
        using(var context=shape.Open())
        {
            context.BeginFigure(new Point(x-body,y),true,true);
            context.QuadraticBezierTo(new Point(x-body*.30,y-half*1.35),new Point(x+body*.45,y-half*.54),true,false);
            context.QuadraticBezierTo(new Point(x+body*.86,y-half*.20),new Point(x+body,y),true,false);
            context.QuadraticBezierTo(new Point(x+body*.86,y+half*.20),new Point(x+body*.45,y+half*.54),true,false);
            context.QuadraticBezierTo(new Point(x-body*.30,y+half*1.35),new Point(x-body,y),true,false);
        }
        shape.Freeze();
        D.DrawGeometry(skins.GetValueOrDefault(swimmer.Back),null,shape);
        D.PushOpacity(.66);
        // The drawn body faces left; the sprite path supplies art that faces right.
        if(swimmer.Swim>0)D.PushTransform(new ScaleTransform(-1,1,x,y));
        var tail=new StreamGeometry();
        using(var context=tail.Open())
        {
            context.BeginFigure(new Point(x+body*.9,y),true,true);
            context.QuadraticBezierTo(new Point(x+body*1.5,y-half*1.1),new Point(x+body*1.95,y-half*1.3),true,false);
            context.QuadraticBezierTo(new Point(x+body*1.5,y),new Point(x+body*1.95,y+half*1.3),true,false);
            context.QuadraticBezierTo(new Point(x+body*1.5,y+half*1.1),new Point(x+body*.9,y),true,false);
        }
        tail.Freeze();
        D.PushTransform(new RotateTransform(wag,x+body*.92,y));
        D.DrawGeometry(B(swimmer.Fins),null,tail);D.Pop();
        var dorsal=new StreamGeometry();
        using(var context=dorsal.Open())
        {
            context.BeginFigure(new Point(x-body*.15,y-half*.72),true,true);
            context.QuadraticBezierTo(new Point(x+body*.15,y-half*1.6),new Point(x+body*.62,y-half*.42),true,false);
            context.QuadraticBezierTo(new Point(x+body*.2,y-half*.5),new Point(x-body*.15,y-half*.72),true,false);
        }
        dorsal.Freeze();D.DrawGeometry(B(swimmer.Fins),null,dorsal);
        var pectoral=new StreamGeometry();
        using(var context=pectoral.Open())
        {
            context.BeginFigure(new Point(x+body*.02,y+half*.34),true,true);
            context.QuadraticBezierTo(new Point(x-body*.3,y+half*1.5),new Point(x-half*.35,y+half*.62),true,false);
            context.QuadraticBezierTo(new Point(x-body*.1,y+half*.3),new Point(x+body*.02,y+half*.34),true,false);
        }
        pectoral.Freeze();D.DrawGeometry(B(swimmer.Fins),null,pectoral);
        if(swimmer.Swim>0)D.Pop();
        D.Pop();
        D.DrawEllipse(B("#FF100E16"),null,new Point(x-body*.6,y-half*.16),size*.1+1,size*.1+1);
        D.DrawEllipse(B("#F8F2FF"),null,new Point(x-body*.63,y-half*.22),size*.035+.6,size*.035+.6);
    }
    // assets/Aquarium/<name>.png replaces the drawn shape; absence is remembered.
    // Generated art keeps its own proportions: it is fitted inside the box instead
    // of being stretched to it.
    static Rect Fit(BitmapSource sprite,Rect box)
    {
        double aspect=sprite.PixelWidth/(double)sprite.PixelHeight,w=box.Width,h=box.Height;
        if(w/h>aspect)w=h*aspect;else h=w/aspect;
        return new Rect(box.X+(box.Width-w)/2,box.Y+(box.Height-h)/2,w,h);
    }
    BitmapSource? Artwork(string name)
    {
        if(artwork.TryGetValue(name,out var cached))return cached;
        var path=Path.Combine(Station.Assets,"Aquarium",name+".png");
        BitmapSource? image=null;
        if(File.Exists(path))
        {
            try
            {
                var loaded=new BitmapImage();loaded.BeginInit();loaded.UriSource=new Uri(path);loaded.CacheOption=BitmapCacheOption.OnLoad;loaded.EndInit();loaded.Freeze();image=loaded;
            }
            catch(Exception e) when(e is IOException or NotSupportedException or UriFormatException){image=null;}
        }
        artwork[name]=image;return image;
    }
    public void Dispose()
    {
        disposed=true;active=false;CompositionTarget.Rendering-=Animate;
    }
}
