using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Battlestation;

internal readonly record struct SceneDock(Window Window,bool Visible);

// One bounded scene change: the docks and their glass dissolve out, the incoming
// layout settles while nothing is drawn, then the docks come back in a short
// cascade. The docks never travel between two layouts; only the scene dissolves.
internal sealed class SceneTransition
{
    const double Frame=16, OutFade=80, OutSpread=50, InFade=150, InSpread=110, RiseHeight=14;
    readonly DispatcherTimer clock;
    readonly Dispatcher dispatcher;
    readonly Func<IReadOnlyList<SceneDock>> stage;
    readonly Action<double> glass;
    readonly Action conceal;
    readonly Stopwatch watch=new();
    readonly Stopwatch swap=new();
    readonly List<Entry> leaving=[],entering=[];
    readonly record struct Entry(Window Window,double Delay,double From,TranslateTransform? Offset);
    enum Phase{Idle,Out,In}
    Action? apply;
    Phase phase;
    double glassFrom=1,glassAlpha=1;
    bool disposed;

    internal SceneTransition(Dispatcher dispatcher,Func<IReadOnlyList<SceneDock>> stage,Action<double> glass,Action conceal)
    {
        this.stage=stage;this.glass=glass;this.conceal=conceal;
        this.dispatcher=dispatcher;
        clock=new DispatcherTimer(DispatcherPriority.Render,dispatcher){Interval=TimeSpan.FromMilliseconds(Frame)};
        clock.Tick+=(_,_)=>Tick();
    }

    internal bool Busy=>phase!=Phase.Idle;
    internal string State=>phase switch{Phase.Out=>"sortie",Phase.In=>"entree",_=>"repos"};
    internal double GlassAlpha=>glassAlpha;
    internal double LastApplyMilliseconds{get;private set;}

    // Last request wins: a scene asked for during the exit replaces the destination,
    // and one asked for during the entrance simply restarts the exit.
    internal void Request(Action destination)
    {
        if(disposed)return;
        apply=destination;
        if(phase!=Phase.Out)Begin();
    }

    void Begin()
    {
        var docks=stage();
        var ordered=docks.OrderBy(dock=>dock.Window.Top).ThenBy(dock=>dock.Window.Left).ToArray();
        glassFrom=glassAlpha;leaving.Clear();entering.Clear();
        for(int index=0;index<ordered.Length;index++)
        {
            var window=ordered[index].Window;
            var payload=Payload(window);
            if(payload is not null)payload.RenderTransform=null;
            if(!window.IsVisible){window.Opacity=0;continue;}
            window.IsHitTestVisible=false;
            // The wave leaves from the bottom, where the entrance finishes last.
            leaving.Add(new(window,Spread(OutSpread,ordered.Length-1-index,ordered.Length),window.Opacity,null));
        }
        watch.Restart();phase=Phase.Out;clock.Start();Apply(0);
    }

    void Tick()
    {
        double milliseconds=watch.Elapsed.TotalMilliseconds;
        if(phase==Phase.Out)
        {
            if(milliseconds>=OutFade+OutSpread)Swap();else Apply(milliseconds);
            return;
        }
        if(phase!=Phase.In)return;
        if(milliseconds>=InFade+InSpread)Finish();else Apply(milliseconds);
    }

    void Apply(double milliseconds)
    {
        if(phase==Phase.Out)
        {
            foreach(var entry in leaving)entry.Window.Opacity=entry.From*(1-Ease(Progress(entry.Delay,milliseconds,OutFade)));
            SetGlass(glassFrom*(1-Ease(Progress(0,milliseconds,OutFade+OutSpread))));
            return;
        }
        foreach(var entry in entering)
        {
            double value=Ease(Progress(entry.Delay,milliseconds,InFade));
            entry.Window.Opacity=value;
            if(entry.Offset is {} offset)offset.Y=(1-value)*RiseHeight;
        }
        SetGlass(Ease(Progress(0,milliseconds,InFade+InSpread)));
    }

    void Swap()
    {
        var destination=apply;apply=null;
        Exception? failure=null;
        conceal();
        swap.Restart();
        try{destination?.Invoke();}catch(Exception e){failure=e;}
        swap.Stop();LastApplyMilliseconds=swap.Elapsed.TotalMilliseconds;
        var docks=stage();
        var ordered=docks.Where(dock=>dock.Visible).OrderBy(dock=>dock.Window.Top).ThenBy(dock=>dock.Window.Left).ToArray();
        entering.Clear();
        // Every window waits at zero: a dock shown by the incoming layout must not
        // appear before its own entrance.
        foreach(var dock in docks)
        {
            var payload=Payload(dock.Window);
            dock.Window.Opacity=0;
            if(payload is not null)payload.RenderTransform=null;
        }
        for(int index=0;index<ordered.Length;index++)
        {
            var window=ordered[index].Window;
            TranslateTransform? offset=null;
            if(Payload(window) is {} payload){offset=new TranslateTransform(0,RiseHeight);payload.RenderTransform=offset;}
            entering.Add(new(window,Spread(InSpread,index,ordered.Length),0,offset));
        }
        watch.Restart();phase=Phase.In;Apply(0);
        // The timer keeps running whatever happened: a failed change gives the desktop
        // back first, then the failure surfaces through the usual dispatcher path.
        if(failure is not null)dispatcher.BeginInvoke(DispatcherPriority.Normal,new Action(()=>throw failure));
    }

    void Finish()
    {
        foreach(var dock in stage())
        {
            dock.Window.Opacity=1;dock.Window.IsHitTestVisible=true;
            if(Payload(dock.Window) is {} payload)payload.RenderTransform=null;
        }
        SetGlass(1);phase=Phase.Idle;clock.Stop();watch.Reset();
    }

    void SetGlass(double value)
    {
        glassAlpha=value;glass(value);
    }

    static FrameworkElement? Payload(Window window)=>window.Content as FrameworkElement;
    static double Progress(double delay,double milliseconds,double duration)=>duration<=0?1:Math.Clamp((milliseconds-delay)/duration,0,1);
    static double Ease(double progress)=>progress*(2-progress);
    static double Spread(double budget,int index,int count)=>count<2?0:budget*index/(count-1);

    internal void Dispose()
    {
        disposed=true;phase=Phase.Idle;clock.Stop();
        foreach(var dock in stage())
        {
            dock.Window.Opacity=1;dock.Window.IsHitTestVisible=true;
            if(Payload(dock.Window) is {} payload)payload.RenderTransform=null;
        }
        glassAlpha=1;glass(1);
    }
}
