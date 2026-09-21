using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Rectangle=System.Windows.Shapes.Rectangle;

namespace Battlestation;

// Diorama Océan: a cut block of water on the desk. The water itself is a real
// scene traced per pixel on the GPU (Shaders/Ocean.fx); this dock owns the frame,
// the camera, the wave packets and the lifecycle.
//
// At rest nothing runs: no packet is alive, the sound is silent, so no shader
// constant changes and the surface keeps the exact image it already has. The
// pointer and the PC audio add bounded wave packets that propagate and damp back
// to that reference shape.
internal sealed class OceanSurface : Surface,IDisposable
{
    const int Slot=17;
    const double Step=1/60d;
    const double RestPoll=1/12d;
    const int MaxPackets=5;
    // Wave packets travel and fade in world units: bounded, never endless.
    const double RingSpeed=.62,RingDecay=1.75;
    const double SilenceBass=.12,SilenceTreble=.07;
    const double MinAmplitude=.0025;

    sealed class Packet
    {
        internal double X,Z,Radius,Amplitude;
    }

    readonly Rectangle water;
    readonly OceanEffect? effect;
    readonly List<Packet> packets=[];
    readonly Random random=new(0x0CEA17);
    readonly DispatcherTimer poll;
    Point3D eye=new(1.4,.9,2),target;
    Point crop;
    Point pointer=new(-1,-1);
    Point3D lastTouch=new(-999,0,-999);
    double elapsed,lastFrame=-1,budget,touchedAt,dropGate,beatGate;
    double bass,middle,treble,slowBass,energy;
    bool active,disposed,sized;

    internal OceanSurface(Station station):base(station,Slot)
    {
        Width=720;Height=480;
        water=new Rectangle{Fill=Brushes.Transparent,IsHitTestVisible=false,SnapsToDevicePixels=true};
        if(OceanEffect.Available)
        {
            var bed=Bed();
            effect=new OceanEffect{Input=System.Windows.Media.Effects.Effect.ImplicitInput,Bed=bed.Brush};
            // The relief switch is set after construction, once the effect exists.
            effect.Relief=bed.Relief;
        }
        if(effect is not null)water.Effect=effect;
        // The audio is read at a slow cadence while the block is exposed; the fast
        // rendering loop only exists while the surface is actually moving.
        poll=new DispatcherTimer(TimeSpan.FromMilliseconds(RestPoll*1000),DispatcherPriority.Background,(_,_)=>Advance(RestPoll),Dispatcher){IsEnabled=false};
    }

    internal FrameworkElement WaterLayer=>water;
    internal bool Animating=>active&&packets.Count>0;
    internal double Elapsed=>elapsed;
    // The native glass already carries the frosted panel and its rim behind the
    // block: the water must not be veiled by a second layer of glass.
    protected override bool DrawsPanel=>false;

    // The bed is the immersed relief baked by the Blender pipeline. Without the
    // file the shader keeps its own procedural relief, so the block never depends
    // on an asset that may be missing.
    static readonly Dictionary<string,Brush?> beds=[];
    static Brush? BedBrush(string? path,out double relief)
    {
        relief=0;
        if(path is null)return null;
        if(beds.TryGetValue(path,out var cached)){relief=cached is null?0:1;return cached;}
        Brush? brush=null;
        if(File.Exists(path))
        {
            try
            {
                var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(path);image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();
                brush=new ImageBrush(image){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.RelativeToBoundingBox};
                brush.Freeze();
            }
            catch(Exception e) when(e is IOException or NotSupportedException or UriFormatException){brush=null;}
        }
        beds[path]=brush;relief=brush is null?0:1;return brush;
    }
    (Brush Brush, double Relief) Bed()
    {
        var brush=BedBrush(Path.Combine(Station.Assets,"Ocean","ocean-bed.png"),out double relief);
        if(brush is not null)return (brush,relief);
        // A flat stand-in keeps the sampler register bound even without the asset,
        // and the procedural relief stays in charge.
        var pixel=new byte[]{0xFF,0xFF,0xFF,0x00};
        var fallback=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,pixel,4);
        fallback.Freeze();
        return (new ImageBrush(fallback),0);
    }

    internal void SetActive(bool value)
    {
        if(active==value||disposed)return;
        active=value;
        if(value)
        {
            lastFrame=-1;budget=0;
            poll.Start();
            Frame();
        }
        else
        {
            poll.Stop();Follow(false);
            packets.Clear();
            bass=middle=treble=slowBass=0;
            if(effect is not null){effect.Audio=new Point3D(0,0,0);Reset();}
        }
    }

    // One deterministic step: the desktop reaches it through the rendering gate
    // above, the render tests call it directly.
    internal void Advance(double seconds)
    {
        if(!active||disposed||!double.IsFinite(seconds)||seconds<=0)return;
        seconds=Math.Min(seconds,.25);elapsed+=seconds;

        // The spectrum published by the music dock: no second capture, no volume or
        // device change, and nothing is written anywhere.
        var bands=Station.MusicBands;
        double intensity=Math.Clamp(Station.AudioIntensity,0,1);
        double bassTarget=Band(bands,0,4)*intensity,midTarget=Band(bands,4,4)*intensity,trebleTarget=Band(bands,8,4)*intensity;
        if(bassTarget<SilenceBass)bassTarget=0;
        if(trebleTarget<SilenceTreble)trebleTarget=0;
        // Fast attack, slow release: a kick is heard as a kick, not as a plateau.
        bass+=(bassTarget-bass)*Math.Min(1,seconds*(bassTarget>bass?10:2.2));
        middle+=(midTarget-middle)*Math.Min(1,seconds*(midTarget>middle?8:2.0));
        treble+=(trebleTarget-treble)*Math.Min(1,seconds*(trebleTarget>treble?8:2.0));
        energy+=(Math.Clamp(bass*1.2+middle*.3+treble*.2,0,1)-energy)*Math.Min(1,seconds*3);

        // A beat, not every notification: a bounded packet, then a refractory pause.
        // The trigger compares the fast bass envelope with its own slow average, so
        // a steady tone stays calm and an impact is heard as one wave.
        slowBass+=(bass-slowBass)*Math.Min(1,seconds*.8);
        beatGate-=seconds;
        if(bass>slowBass*1.30+.05&&bass>.18&&beatGate<=0)
        {
            beatGate=.24;
            Inject((random.NextDouble()*2-1)*.7,(random.NextDouble()*2-1)*.7,Math.Min(.055,.022+bass*.05),.10);
        }
        double damping=Math.Exp(-RingDecay*seconds);
        for(int index=packets.Count-1;index>=0;index--)
        {
            var packet=packets[index];
            packet.Radius+=RingSpeed*seconds;
            packet.Amplitude*=damping;
            if(packet.Amplitude<MinAmplitude||packet.Radius>2.4)packets.RemoveAt(index);
        }
        if(effect is not null)
        {
            effect.Time=elapsed;
            effect.Audio=new Point3D(bass,middle,treble);
            Publish();
        }
        InvalidateWater();
        Follow(packets.Count>0);
    }

    void Frame()
    {
        if(effect is null)return;
        var (frameEye,frameTarget,frameCrop)=OceanCamera.Frame(Width,Height);
        eye=frameEye;target=frameTarget;crop=frameCrop;
        effect.Eye=eye;effect.Target=target;effect.Crop=crop;effect.Fov=OceanCamera.Fov;
        effect.Resolution=new Point(Math.Max(1,Width),Math.Max(1,Height));
        sized=true;
    }
    void Follow(bool fast)
    {
        if(disposed)return;
        if(fast)
        {
            poll.Stop();
            if(lastFrame<0)CompositionTarget.Rendering+=Animate;
        }
        else
        {
            CompositionTarget.Rendering-=Animate;lastFrame=-1;
            if(active)poll.Start();
        }
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
    void Inject(double x,double z,double amplitude,double radius)
    {
        if(packets.Count>=MaxPackets)packets.RemoveAt(0);
        packets.Add(new Packet{X=x,Z=z,Radius=radius,Amplitude=amplitude});
    }
    void Publish()
    {
        if(effect is null)return;
        effect.Ripple0=Ripple(0);effect.Ring0=Ring(0);
        effect.Ripple1=Ripple(1);effect.Ring1=Ring(1);
        effect.Ripple2=Ripple(2);effect.Ring2=Ring(2);
        effect.Ripple3=Ripple(3);effect.Ring3=Ring(3);
        effect.Ripple4=Ripple(4);effect.Ring4=Ring(4);
    }
    Point3D Ripple(int index)=>index<packets.Count?new Point3D(packets[index].X,packets[index].Z,packets[index].Amplitude):new Point3D(0,0,0);
    double Ring(int index)=>index<packets.Count?packets[index].Radius:0;
    void Reset(){Publish();effect!.Time=elapsed;}

    // The pointer touches the water where the ray meets it: the sky, the base and
    // the tray rim are outside the footprint and never make a wave.
    protected override void OnPointer(MouseEventArgs e)
    {
        if(!active||effect is null||!sized)return;
        var position=e.GetPosition(this);
        if(!IsMouseOver||position.X<0||position.Y<0||position.X>Width||position.Y>Height)return;
        pointer=position;
        double u=position.X/Math.Max(1,Width),v=position.Y/Math.Max(1,Height);
        if(!OceanCamera.TryTouch(eye,target,crop,OceanCamera.Aspect(Width,Height),u,v,out var hit)){lastTouch=new(-999,0,-999);return;}
        if(elapsed<dropGate)return;
        bool tracked=lastTouch.X>-900;
        double dx=tracked?hit.X-lastTouch.X:0,dz=tracked?hit.Z-lastTouch.Z:0;
        double distance=Math.Sqrt(dx*dx+dz*dz);
        double dt=Math.Max(.012,elapsed-touchedAt);
        // Speed and direction of the gesture set a bounded amplitude: a slow pass
        // ripples lightly, a fast one leaves a firmer wake, and a still cursor adds
        // nothing at all.
        double speed=tracked?distance/dt:0;
        double amplitude=Math.Clamp(.014+speed*.018,.014,.05);
        if(tracked&&distance<.004)return;
        dropGate=elapsed+.04;
        Inject(hit.X,hit.Z,amplitude,.03);
        lastTouch=hit;touchedAt=elapsed;
        Publish();InvalidateWater();Follow(true);
    }
    protected override void OnMouseLeave(MouseEventArgs e){pointer=new(-1,-1);lastTouch=new(-999,0,-999);base.OnMouseLeave(e);}

    // The dock owns no text: the panel is the object.
    protected override void Paint()
    {
        double w=Width,h=Height;
        if(w<=8||h<=8)return;
        // Transparent but hit-testable over the whole dock, so the pointer is
        // projected on the water instead of being caught by the glass frame.
        D.DrawRectangle(B("#00000000"),null,new Rect(0,0,w,h));
        // A machine without pixel shader 3 keeps a still block instead of an empty
        // panel: the same silhouette, drawn, and nothing else pretends to be water.
        if(effect is not null)return;
        double trayW=Math.Min(w,h*1.5)*.82,trayH=trayW*.66,x=(w-trayW)/2,y=(h-trayH)/2;
        D.DrawRoundedRectangle(B("#2E1C6E63"),null,new Rect(x,y+trayH*.34,trayW,trayH*.66),6,6);
        D.DrawRoundedRectangle(B("#B465B7A8"),null,new Rect(x,y,trayW,trayH*.70),6,6);
        D.DrawRoundedRectangle(B("#B4A06A46"),null,new Rect(x,y+trayH*.70,trayW,trayH*.30),4,4);
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        if(effect is null)return;
        water.Width=Width;water.Height=Height;
        // A resize re-frames the object: the block keeps its proportions.
        Frame();InvalidateWater();
    }
    void InvalidateWater(){water.InvalidateVisual();}

    // QA hook for the desktop capture: the very same wave packet the pointer makes,
    // placed explicitly on the water so a still screenshot can prove the reaction.
    internal void Stir(double x,double z,double amplitude)
    {
        if(!active||effect is null)return;
        Inject(Math.Clamp(x,-.95,.95),Math.Clamp(z,-.95,.95),Math.Clamp(amplitude,MinAmplitude,.08),.05);
        Publish();InvalidateWater();Follow(true);
    }
    internal object Inspect()=>new
    {
        animating=Animating,active,elapsed=Math.Round(elapsed,2),packets=packets.Count,
        water=effect is null?"dessinée":"shader",
        energy=Math.Round(energy,3),bass=Math.Round(bass,3),middle=Math.Round(middle,3),treble=Math.Round(treble,3),
        pointer=new{X=Math.Round(pointer.X,1),Y=Math.Round(pointer.Y,1)},
        eye=new{X=Math.Round(eye.X,2),Y=Math.Round(eye.Y,2),Z=Math.Round(eye.Z,2)},
        crop=new{X=Math.Round(crop.X,3),Y=Math.Round(crop.Y,3)},
        relief=effect?.Relief??0
    };

    public void Dispose()
    {
        disposed=true;active=false;poll.Stop();CompositionTarget.Rendering-=Animate;packets.Clear();
    }
}

