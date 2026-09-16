using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
namespace Battlestation;
internal sealed class NetworkSurface : Surface,IDisposable
{
    internal NetworkSampler Sampler {get;}
    internal NetworkDetailClient Details {get;}=new();
    bool active,detailView;
    double shownDown,shownUp;
    TimeSpan lastFrame;
    NetworkSnapshot? previous;
    NetworkDetailFrame? previousDetail;
    NetworkSettingsWindow? settingsWindow;
    internal NetworkSurface(Station station):base(station,14){Width=720;Height=336;Sampler=new(Path.Combine(station.Data,"network.json"));}
    internal void SetActive(bool value)
    {
        if(active==value)return;active=value;Sampler.SetActive(value);Details.SetActive(value);lastFrame=default;
        if(value)CompositionTarget.Rendering+=Frame;else CompositionTarget.Rendering-=Frame;
    }
    internal void Poll(){if(active&&Details.Connected)Details.Poll();}
    void Frame(object? sender,EventArgs args)
    {
        if(args is not RenderingEventArgs frame||frame.RenderingTime==lastFrame)return;
        double dt=lastFrame==default?1/30d:(frame.RenderingTime-lastFrame).TotalSeconds;
        if(dt<1/30d-.001)return;lastFrame=frame.RenderingTime;dt=Math.Min(.15,dt);
        var snapshot=Sampler.Snapshot;var current=snapshot.History.LastOrDefault();
        shownDown+=((current?.Received??0)-shownDown)*(1-Math.Exp(-dt*8));
        shownUp+=((current?.Sent??0)-shownUp)*(1-Math.Exp(-dt*8));
        if(detailView&&!Details.Starting&&!Details.Connected)detailView=false;
        bool changed=!ReferenceEquals(previous,snapshot)||!ReferenceEquals(previousDetail,Details.Frame);
        previous=snapshot;previousDetail=Details.Frame;
        if(changed||!detailView&&snapshot.History.Any(s=>s.Received.HasValue))Refresh();
    }
    internal static string Rate(double? value)=>value is null?"—":value>=1_000_000?$"{value/1_000_000:0.0} Mo/s":value>=1000?$"{value/1000:0.0} Ko/s":$"{value:0} o/s";
    protected override void Paint()
    {
        var snapshot=Sampler.Snapshot;var current=snapshot.History.LastOrDefault();
        Header("RÉSEAU");Button("NetworkSettings","⋯",Width-58,9,34,28,OpenSettings,14);
        Text(snapshot.Name,24,45,9,Muted,width:Width-180);
        Text(snapshot.Latency is null?"— ms":snapshot.Latency<1?"< 1 ms":$"{snapshot.Latency:0} ms",Width-24,44,10,snapshot.Latency is null?Muted:Ink,align:"right");
        ToolTip=$"Aller-retour ICMP vers {snapshot.Target} · latence de cette cible, pas du serveur de jeu.";
        if(detailView)Applications();
        else
        {
            Text("↓ RÉCEPTION",24,72,8,Muted);Text("↑ ENVOI",Width/2+12,72,8,Muted);
            Text(Rate(current?.Received is null?null:shownDown),24,90,19,Ink,font:DockAppearance.NumberFont);
            Text(Rate(current?.Sent is null?null:shownUp),Width/2+12,90,19,Ink,font:DockAppearance.NumberFont);
            var chart=new Rect(24,132,Width-48,Math.Max(32,Height-200));
            Box(chart.X,chart.Y,chart.Width,chart.Height,"#102D203B",radius:8);
            double max=Math.Max(1024,snapshot.History.Select(s=>Math.Max(s.Received??0,s.Sent??0)).DefaultIfEmpty().Max()*1.12);
            Curve(snapshot.History,chart,max,true,DesktopTheme.Current.Active[3]);
            Curve(snapshot.History,chart,max,false,DesktopTheme.Current.Active[0]);
            Text("60 s",chart.X,chart.Bottom+5,7,Muted);
            Text(snapshot.Status.Length>0?snapshot.Status:Details.Frame.Status=="Détail inactif"?"":Details.Frame.Status,chart.Right,chart.Bottom+5,7,Muted,align:"right",width:chart.Width-70);
        }
        Button("NetworkApplications",detailView?"Courbes":"Détail par application",24,Height-42,Width-48,28,ToggleDetails,9);
    }
    void Curve(NetworkSample[] history,Rect rect,double max,bool down,string color)
    {
        var geometry=new StreamGeometry();bool started=false;long now=Environment.TickCount64;
        using(var path=geometry.Open())foreach(var item in history)
        {
            double? value=down?item.Received:item.Sent;
            if(value is null){started=false;continue;}
            double x=rect.Right-(now-item.At)/60000d*rect.Width;
            if(x<rect.Left){started=false;continue;}
            var point=new Point(Math.Min(rect.Right,x),rect.Bottom-4-Math.Clamp(value.Value/max,0,1)*(rect.Height-8));
            if(!started){path.BeginFigure(point,false,false);started=true;}else path.LineTo(point,true,false);
        }
        geometry.Freeze();D.PushClip(new RectangleGeometry(rect,8,8));D.DrawGeometry(null,new Pen(B(color),1.8),geometry);D.Pop();
    }
    void Applications()
    {
        var frame=Details.Frame;
        Text("APPLICATIONS · TRAFIC DU PC",24,72,8,Muted);
        if(!frame.Active||frame.Apps.Length==0){Text(frame.Active?"Aucun trafic attribué":frame.Status,Width/2,Height/2,11,Muted,align:"center",width:Width-48);return;}
        double rowHeight=Math.Min(44,(Height-132)/5),max=frame.Apps.Max(a=>a.Received+a.Sent);
        for(int i=0;i<frame.Apps.Length;i++)
        {
            var item=frame.Apps[i];double y=96+i*rowHeight;
            Box(24,y-3,(Width-48)*Math.Clamp((item.Received+item.Sent)/Math.Max(1,max),0,1),rowHeight-5,"#186E4093",radius:6);
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
        if(!Details.Connected)Details.Enable();else Details.Poll();
        Refresh();
    }
    void OpenSettings()
    {
        if(settingsWindow is not null){settingsWindow.Activate();return;}
        settingsWindow=new NetworkSettingsWindow(Sampler);settingsWindow.Closed+=(_,_)=>settingsWindow=null;
        OverlayStyle.Reveal(settingsWindow,Station.Settings.AnimateBackground);
    }
    internal object Inspect()=>new{active,snapshot=Sampler.Snapshot,preferences=Sampler.Preferences,detail=Details.Frame,helperPid=Details.HelperPid,Details.Starting,detailView};
    public void Dispose(){CompositionTarget.Rendering-=Frame;settingsWindow?.Close();Sampler.Dispose();Details.Dispose();}
}
