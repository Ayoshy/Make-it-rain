using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Rectangle=System.Windows.Shapes.Rectangle;

namespace Battlestation;

// Montagne: perpetual snow falling on a snowy peak. The scene is traced per
// pixel on the GPU (Shaders/Montagne.fx); this dock owns the frame, the stir,
// the music envelope and the lifecycle. The flakes fall forever while the block
// is exposed, the pointer pushes them aside as it passes, and the spectrum of
// the music dock raises the wind, the fall and the sparkle on the snow.
internal sealed class MontagneSurface : Surface,IDisposable
{
    const int Slot=15;
    const double Step=1/60d;
    const double SilenceBass=.12,SilenceTreble=.07;
    const double StirDecay=1.35,StirLimit=26;

    readonly Rectangle scene;
    readonly MontagneEffect? effect;
    readonly Random random=new(0x0CEA17);
    Point pointer=new(-10000,-10000);
    Point stir;
    double elapsed,lastFrame=-1,budget,beatGate,slowBass;
    double bass,middle,treble,energy;
    bool active,disposed;

    internal MontagneSurface(Station station):base(station,Slot)
    {
        Width=960;Height=600;
        scene=new Rectangle{Fill=Brushes.Transparent,IsHitTestVisible=false,SnapsToDevicePixels=true};
        if(MontagneEffect.Available)
        {
            effect=new MontagneEffect{Input=System.Windows.Media.Effects.Effect.ImplicitInput};
            scene.Effect=effect;
            Frame();
        }
    }

    internal FrameworkElement SceneLayer=>scene;
    internal bool Animating=>active;
    internal double Elapsed=>elapsed;
    // The native glass already carries the frosted panel and its rim behind the
    // block: the scene must not be veiled by a second layer of glass.
    protected override bool DrawsPanel=>false;

    internal void SetActive(bool value)
    {
        if(active==value||disposed)return;
        active=value;
        if(value)
        {
            lastFrame=-1;budget=0;
            CompositionTarget.Rendering+=Animate;
        }
        else
        {
            CompositionTarget.Rendering-=Animate;
            lastFrame=-1;
            stir=new Point(0,0);
            bass=middle=treble=slowBass=0;
            if(effect is not null){effect.Audio=new Point3D(0,0,0);effect.Stir=new Point(0,0);}
        }
    }

    // One deterministic step: the desktop reaches it through the rendering gate
    // above, the render tests call it directly.
    internal void Advance(double seconds)
    {
        if(!active||disposed||!double.IsFinite(seconds)||seconds<=0)return;
        seconds=Math.Min(seconds,.25);elapsed+=seconds;

        // The spectrum published by the music dock: no second capture, no volume
        // or device change, and nothing is written anywhere. The silence gates
        // read the raw bands, and the scene's intensity scales what the shader
        // receives - scaling first would silence quiet scenes entirely.
        var bands=Station.MusicBands;
        double gain=.5+.5*Math.Clamp(Station.AudioIntensity,0,1);
        double rawBass=Band(bands,0,4),rawTreble=Band(bands,8,4);
        double bassTarget=rawBass<SilenceBass?0:rawBass*gain;
        double midTarget=Band(bands,4,4)*gain;
        double trebleTarget=rawTreble<SilenceTreble?0:rawTreble*gain;
        // Fast attack, slow release: a kick is heard as a kick, not a plateau.
        bass+=(bassTarget-bass)*Math.Min(1,seconds*(bassTarget>bass?10:2.2));
        middle+=(midTarget-middle)*Math.Min(1,seconds*(midTarget>middle?8:2.0));
        treble+=(trebleTarget-treble)*Math.Min(1,seconds*(trebleTarget>treble?8:2.0));
        energy+=(Math.Clamp(bass*1.2+middle*.3+treble*.2,0,1)-energy)*Math.Min(1,seconds*3);

        // A beat, not every notification: the fast bass envelope is compared to
        // its own slow average, so a steady tone stays calm and an impact sends
        // one bounded gust through the flakes.
        slowBass+=(bass-slowBass)*Math.Min(1,seconds*.8);
        beatGate-=seconds;
        if(bass>slowBass*1.30+.05&&bass>.18&&beatGate<=0)
        {
            beatGate=.24;
            var direction=random.NextDouble()*2-1;
            AddStir(direction*22,.30+Math.Min(.7,bass*.7));
        }
        // The stir decays back to rest; a passing cursor leaves only its wake.
        stir=new Point(stir.X*Math.Exp(-StirDecay*seconds),stir.Y*Math.Exp(-StirDecay*seconds));
        if(effect is not null)
        {
            effect.Time=elapsed;
            effect.Audio=new Point3D(bass,middle,treble);
            effect.Stir=stir;
            effect.Pointer=pointer;
        }
        InvalidateScene();
    }

    void Animate(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs frame)return;
        double now=frame.RenderingTime.TotalSeconds;
        if(now==lastFrame)return;
        double delta=lastFrame<0?Step:Math.Clamp(now-lastFrame,0,.2);
        lastFrame=now;budget+=delta;
        while(budget>=Step){budget-=Step;Advance(Step);}
    }
    static double Band(float[] bands,int start,int count)
    {
        if(bands is null)return 0;
        double total=0;int seen=0;
        for(int index=start;index<start+count&&index<bands.Length;index++){total+=bands[index];seen++;}
        double value=seen==0?0:total/seen;
        return double.IsFinite(value)?Math.Clamp(value,0,1):0;
    }
    void AddStir(double x,double weight)
    {
        var next=new Point(stir.X+x*weight,stir.Y);
        double length=Math.Sqrt(next.X*next.X+next.Y*next.Y);
        stir=length>StirLimit?new Point(next.X/length*StirLimit,next.Y/length*StirLimit):next;
    }

    // Passing the cursor stirs the snow: its velocity feeds the shared stir the
    // shader reads, so the flakes give way under the gesture.
    protected override void OnPointer(MouseEventArgs e)
    {
        if(!active||effect is null)return;
        var position=e.GetPosition(this);
        if(!IsMouseOver||position.X<0||position.Y<0||position.X>Width||position.Y>Height)return;
        if(pointer.X>-9000)
        {
            double dx=position.X-pointer.X,dy=position.Y-pointer.Y;
            double distance=Math.Sqrt(dx*dx+dy*dy);
            if(distance>2)AddStir(Math.Clamp(dx*.55,-18,18),Math.Clamp(distance/60,0,1));
        }
        pointer=position;
    }
    protected override void OnMouseLeave(MouseEventArgs e){pointer=new(-10000,-10000);base.OnMouseLeave(e);}

    // The dock owns no text: the panel is the mountain.
    protected override void Paint()
    {
        double w=Width,h=Height;
        if(w<=8||h<=8)return;
        // Transparent but hit-testable over the whole dock, so the pointer stirs
        // the flakes instead of being caught by the glass frame.
        D.DrawRectangle(B("#00000000"),null,new Rect(0,0,w,h));
        // A machine without pixel shader 3 keeps a still painted peak instead of
        // an empty panel: the same sky, moon and ridge, and a few static flakes.
        if(effect is not null)return;
        D.PushClip(new RectangleGeometry(new Rect(8,8,w-16,h-16),17,17));
        double aspect=MontagneCamera.Aspect(w,h),half=aspect*MontagneCamera.WidthPerAspect;
        double ToY(double world)=>((MontagneCamera.YTop-world)/(MontagneCamera.YTop-MontagneCamera.YBottom))*h;
        double ToX(double world)=>(world+half)/(2*half)*w;
        D.DrawRectangle(DesktopTheme.Gradient("#0D1230","#5A5F86",90),null,new Rect(8,8,w-16,h-16));
        D.DrawEllipse(B("#EDF0FF"),null,new Point(ToX(half*.52),ToY(.58)),26,26);
        var ridge=new StreamGeometry();
        using(var context=ridge.Open())
        {
            context.BeginFigure(new Point(0,h),true,true);
            for(int step=0;step<=96;step++)
            {
                double world=-half+2*half*step/96;
                context.LineTo(new Point(ToX(world),ToY(MontagneCamera.Ridge(world))),true,false);
            }
            context.LineTo(new Point(w,h),true,false);
        }
        ridge.Freeze();
        D.DrawGeometry(DesktopTheme.Gradient("#DDE3F4","#2A2F4A",90),null,ridge);
        var flake=new Random(7);
        for(int index=0;index<40;index++)
        {
            double x=8+flake.NextDouble()*(w-16),y=8+flake.NextDouble()*(h-16);
            double size=1+flake.NextDouble()*1.6;
            D.DrawEllipse(B("#B4FFFFFF"),null,new Point(x,y),size,size);
        }
        D.Pop();
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        if(effect is null)return;
        scene.Width=Width;scene.Height=Height;
        Frame();InvalidateScene();
    }
    void Frame()
    {
        if(effect is null)return;
        effect.Resolution=new Point(Math.Max(1,Width),Math.Max(1,Height));
    }
    void InvalidateScene(){scene.InvalidateVisual();}

    // QA hook for the desktop capture: the very same stir the pointer makes,
    // placed explicitly so a still screenshot can prove the reaction.
    internal void Stir(double velocity)
    {
        if(!active||effect is null)return;
        AddStir(Math.Clamp(velocity,-StirLimit,StirLimit),1);
        effect.Stir=stir;InvalidateScene();
    }
    internal object Inspect()=>new
    {
        animating=Animating,active,elapsed=Math.Round(elapsed,2),
        scene=effect is null?"dessinée":"shader",
        energy=Math.Round(energy,3),bass=Math.Round(bass,3),middle=Math.Round(middle,3),treble=Math.Round(treble,3),
        stir=new{X=Math.Round(stir.X,2),Y=Math.Round(stir.Y,2)},
        pointer=new{X=Math.Round(pointer.X,1),Y=Math.Round(pointer.Y,1)}
    };

    public void Dispose()
    {
        disposed=true;active=false;CompositionTarget.Rendering-=Animate;
    }
}
