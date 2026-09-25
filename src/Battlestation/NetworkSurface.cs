using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Battlestation;
internal sealed class NetworkSurface : Surface,IDisposable
{
    internal NetworkSampler Sampler {get;}
    internal NetworkDetailClient Details {get;}=new();
    bool active,detailView;
    double shownDown,shownUp,fromDown,fromUp,targetDown,targetUp;
    TimeSpan lastFrame,tweenStart;
    bool animating;
    NetworkSnapshot? previous;
    NetworkDetailFrame? previousDetail;
    NetworkSettingsWindow? settingsWindow;
    // Les courbes défilent par une transformation animée : WPF les recompose à
    // 30 i/s sans rappeler Paint, qui ne reconstruit les tracés qu'à chaque relevé.
    readonly TranslateTransform scroll=new();
    readonly ScaleTransform scale=new();
    readonly TransformGroup chartMotion=new();
    NetworkSnapshot? charted;
    long chartedAt;
    double chartedMax;
    internal NetworkSurface(Station station):base(station,14)
    {
        Width=720;Height=336;Sampler=new(Path.Combine(station.Data,"network.json"));
        chartMotion.Children.Add(scale);chartMotion.Children.Add(scroll);
        Sampler.Sampled+=()=>Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,(Action)Wake);
    }
    internal void SetActive(bool value)
    {
        if(active==value)return;active=value;Sampler.SetActive(value);Details.SetActive(value);
        if(value)Wake();else{Sleep();StopChart();}
    }
    internal void Poll(){if(active&&Details.Connected)Details.Poll();}
    // Un relevé par seconde : la boucle d'images ne tourne que pendant la courte
    // transition des débits, puis WPF peut cesser de rendre ce dock.
    void Wake()
    {
        if(!active||animating)return;
        animating=true;lastFrame=default;CompositionTarget.Rendering+=Frame;
    }
    void Sleep(){if(!animating)return;animating=false;CompositionTarget.Rendering-=Frame;}
    void Frame(object? sender,EventArgs args)
    {
        if(args is not RenderingEventArgs frame||frame.RenderingTime==lastFrame)return;
        if(lastFrame!=default&&(frame.RenderingTime-lastFrame).TotalSeconds<1/30d-.001)return;
        lastFrame=frame.RenderingTime;
        var snapshot=Sampler.Snapshot;var current=snapshot.History.LastOrDefault();
        if(!ReferenceEquals(previous,snapshot))
        {
            fromDown=shownDown;fromUp=shownUp;targetDown=current?.Received??0;targetUp=current?.Sent??0;tweenStart=frame.RenderingTime;
        }
        double t=Math.Clamp((frame.RenderingTime-tweenStart).TotalSeconds/.25,0,1),ease=1-Math.Pow(1-t,3);
        shownDown=fromDown+(targetDown-fromDown)*ease;shownUp=fromUp+(targetUp-fromUp)*ease;
        if(detailView&&!Details.Starting&&!Details.Connected)detailView=false;
        bool changed=!ReferenceEquals(previous,snapshot)||!ReferenceEquals(previousDetail,Details.Frame);
        previous=snapshot;previousDetail=Details.Frame;
        bool moving=t<1&&!detailView&&(fromDown!=targetDown||fromUp!=targetUp);
        if(changed||moving)Refresh();
        if(!moving)Sleep();
    }
    internal static string Rate(double? value)=>value is null?"—":value>=1_000_000?$"{value/1_000_000:0.0} Mo/s":value>=1000?$"{value/1000:0.0} Ko/s":$"{value:0} o/s";
    protected override void Paint()
    {
        var snapshot=Sampler.Snapshot;var current=snapshot.History.LastOrDefault();
        Header("RÉSEAU");Button("NetworkSettings","⋯",Width-58,9,34,28,OpenSettings,14);
        Text(snapshot.Name,140,19,9,Muted,width:Width-294);
        Text(snapshot.Latency is null?"— ms":snapshot.Latency<1?"< 1 ms":$"{snapshot.Latency:0} ms",Width-76,19,9,snapshot.Latency is null?Muted:Ink,align:"right");
        if(detailView){StopChart();Applications();}
        else
        {
            var downColor=DesktopTheme.Current.Active[3];var upColor=DesktopTheme.Current.Active[0];
            double top=DockAppearance.HeaderContentTop+2;
            Text("↓ RÉCEPTION",24,top,8,downColor);Text("↑ ENVOI",Width/2+12,top,8,upColor);
            Text(Rate(current?.Received is null?null:shownDown),24,top+18,19,downColor,font:DockAppearance.NumberFont);
            Text(Rate(current?.Sent is null?null:shownUp),Width/2+12,top+18,19,upColor,font:DockAppearance.NumberFont);
            var chart=new Rect(24,top+60,Width-48,Math.Max(32,Height-top-128));
            Box(chart.X,chart.Y,chart.Width,chart.Height,"#102D203B",radius:8);
            double max=Math.Max(1024,snapshot.History.Select(s=>Math.Max(s.Received??0,s.Sent??0)).DefaultIfEmpty().Max()*1.12);
            if(!ReferenceEquals(charted,snapshot))StartChart(snapshot,chart,max);
            Curve(snapshot.History,chart,true,downColor);
            Curve(snapshot.History,chart,false,upColor);
            Text("60 s",chart.X,chart.Bottom+5,7,Muted);
            Text(snapshot.Status.Length>0?snapshot.Status:Details.Frame.Status=="Détail inactif"?"":Details.Frame.Status,chart.Right,chart.Bottom+5,7,Muted,align:"right",width:chart.Width-70);
        }
        Button("NetworkApplications",detailView?"Courbes":"Détail par application",24,Height-42,Width-48,28,ToggleDetails,9);
    }
    // Chaque relevé repart de la position affichée : le défilement continue sans
    // saut et un changement d'échelle se fond au lieu de sauter.
    void StartChart(NetworkSnapshot snapshot,Rect rect,double max)
    {
        double shownMax=chartedMax>0?chartedMax/scale.ScaleY:max;
        charted=snapshot;chartedAt=Environment.TickCount64;chartedMax=max;
        scroll.BeginAnimation(TranslateTransform.XProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
        scroll.X=0;scale.ScaleY=1;scale.CenterY=rect.Bottom-4;
        if(!active)return;
        var slide=new DoubleAnimation(0,-rect.Width*2,TimeSpan.FromSeconds(120));Timeline.SetDesiredFrameRate(slide,30);
        scroll.BeginAnimation(TranslateTransform.XProperty,slide);
        if(Math.Abs(max/shownMax-1)<.001)return;
        var settle=new DoubleAnimation(max/shownMax,1,TimeSpan.FromMilliseconds(450)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        Timeline.SetDesiredFrameRate(settle,30);scale.BeginAnimation(ScaleTransform.ScaleYProperty,settle);
    }
    // Masquage, édition ou détail figent l'image sur place ; le prochain affichage
    // des courbes repart d'un tracé neuf.
    void StopChart()
    {
        if(charted is null)return;
        double x=scroll.X,y=scale.ScaleY;charted=null;
        scroll.BeginAnimation(TranslateTransform.XProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
        scroll.X=x;scale.ScaleY=y;
    }
    // Décalé d'un relevé vers la droite : le dernier segment entre par le bord
    // pendant la seconde suivante au lieu d'apparaître d'un coup.
    void Curve(NetworkSample[] history,Rect rect,bool down,string color)
    {
        var geometry=new StreamGeometry{Transform=chartMotion};bool started=false;
        using(var path=geometry.Open())foreach(var item in history)
        {
            double? value=down?item.Received:item.Sent;
            if(value is null){started=false;continue;}
            double x=rect.Right+(item.At+1000-chartedAt)/60000d*rect.Width;
            var point=new Point(x,rect.Bottom-4-Math.Clamp(value.Value/chartedMax,0,1)*(rect.Height-8));
            if(!started){path.BeginFigure(point,false,false);started=true;}else path.LineTo(point,true,false);
        }
        D.PushClip(new RectangleGeometry(rect,8,8));D.DrawGeometry(null,new Pen(B(color),1.8),geometry);D.Pop();
    }
    void Applications()
    {
        var frame=Details.Frame;
        double top=DockAppearance.HeaderContentTop+2;
        Text("APPLICATIONS · TRAFIC DU PC",24,top,8,Muted);
        if(!frame.Active||frame.Apps.Length==0){Text(frame.Active?"Aucun trafic attribué":frame.Status,Width/2,Height/2,11,Muted,align:"center",width:Width-48);return;}
        double rowHeight=Math.Min(44,(Height-top-72)/5),max=frame.Apps.Max(a=>a.Received+a.Sent);
        for(int i=0;i<frame.Apps.Length;i++)
        {
            var item=frame.Apps[i];double y=top+24+i*rowHeight;
            Box(24,y-3,(Width-48)*Math.Clamp((item.Received+item.Sent)/Math.Max(1,max),0,1),rowHeight-5,"#706E4093",radius:6);
            Text(item.Name,34,y,10,Ink,width:Math.Max(80,Width-310));
            Text("↓ "+Rate(item.Received),Width-154,y,9,DesktopTheme.Current.Active[3],align:"right");
            Text("↑ "+Rate(item.Sent),Width-32,y,9,DesktopTheme.Current.Active[0],align:"right");
        }
        if(frame.LostEvents>0)Text("Collecte partielle",24,Height-62,8,"#F4BD8C");
    }
    void ToggleDetails()
    {
        if(detailView){detailView=false;Refresh();return;}
        detailView=true;
        if(!Details.Connected)Details.Enable();
        Refresh();
    }
    void OpenSettings()
    {
        if(settingsWindow is not null){settingsWindow.Activate();return;}
        settingsWindow=new NetworkSettingsWindow(Sampler);settingsWindow.Closed+=(_,_)=>settingsWindow=null;
        OverlayStyle.Reveal(settingsWindow,Station.Settings.AnimateBackground);
    }
    internal object Inspect()=>new{active,snapshot=Sampler.Snapshot,preferences=Sampler.Preferences,detail=Details.Frame,helperPid=Details.HelperPid,Details.Starting,detailView};
    public void Dispose(){Sleep();StopChart();settingsWindow?.Close();Sampler.Dispose();Details.Dispose();}
}
