using System.Windows;
using System.Windows.Media;
namespace Battlestation;
internal sealed class DualSenseSurface : Surface,IDisposable
{
    internal DualSenseReader Reader {get;}=new();
    DualSenseSnapshot? displayedSnapshot;
    bool active,details;
    int? bluetoothBattery;
    readonly double[] levels=new double[17],ripples=new double[17],axes=new double[4];
    readonly DualSenseTouchTrail touchTrail=new();
    string modeNotice="";
    uint lastButtons;
    TimeSpan lastFrame;
    double seconds;
    string palette="";
    DualSenseArtwork? artwork;
    public DualSenseSurface(Station station):base(station,13){Width=720;Height=440;}
    internal void SetActive(bool value)
    {
        Reader.SetTouchEnabled(Station.Settings.DualSenseTouchTrail);
        if(active==value)return;active=value;Reader.SetActive(value);
        lastFrame=default;
        if(value)CompositionTarget.Rendering+=Frame;
        else{CompositionTarget.Rendering-=Frame;Array.Clear(levels);Array.Clear(ripples);touchTrail.Clear();lastButtons=0;}
    }
    void Frame(object? sender,EventArgs args)
    {
        if(args is not RenderingEventArgs frame||frame.RenderingTime==lastFrame)return;
        double elapsed=lastFrame==default?1/60d:(frame.RenderingTime-lastFrame).TotalSeconds;
        if(elapsed<1/60d-.0005)return;lastFrame=frame.RenderingTime;elapsed=Math.Min(.1,elapsed);seconds+=elapsed;
        var next=Reader.Snapshot;var state=next.State;
        for(int i=0;i<levels.Length;i++){
            bool down=state.Connected==1&&state.Down(i);
            if(down&&(lastButtons&(1u<<i))==0)ripples[i]=1;
            levels[i]+=((down?1:0)-levels[i])*(1-Math.Exp(-elapsed*(down?36:12)));
            ripples[i]*=Math.Exp(-elapsed*4.8);
        }
        lastButtons=state.Buttons;
        touchTrail.Update(state,seconds,Station.Settings.DualSenseTouchTrail);
        axes[0]+=(DualSenseState.Axis(state.LX)-axes[0])*(1-Math.Exp(-elapsed*32));
        axes[1]+=(DualSenseState.Axis(state.LY)-axes[1])*(1-Math.Exp(-elapsed*32));
        axes[2]+=(DualSenseState.Axis(state.RX)-axes[2])*(1-Math.Exp(-elapsed*32));
        axes[3]+=(DualSenseState.Axis(state.RY)-axes[3])*(1-Math.Exp(-elapsed*32));
        bool changed=!ReferenceEquals(next,displayedSnapshot);displayedSnapshot=next;
        if(state.Connected==1||changed||levels.Any(v=>v>.001)||ripples.Any(v=>v>.001))Refresh();
    }
    internal void SetBluetoothBattery(int? value){if(bluetoothBattery==value)return;bluetoothBattery=value;Refresh();}
    protected override void Paint()
    {
        Reader.SetTouchEnabled(Station.Settings.DualSenseTouchTrail);
        var snapshot=Reader.Snapshot;var state=snapshot.State;
        Header("DUALSENSE");
        int? battery=state.BatteryPercent??(state.Connected==1&&state.Transport==2?bluetoothBattery:null);
        string connection=snapshot.Status+(state.Connected==1?(battery.HasValue?$" · {battery} %":" · batterie —"):"");
        Text(connection,Width-24,18,9,battery is <=20?"#F4BD8C":state.Connected==1?Ink:Muted,align:"right",width:Width-170);
        double bodyHeight=Math.Max(1,(details?Height-218:Height-92)-(modeNotice.Length>0?22:0));
        double scale=Math.Min((Width-48)/DualSenseArtwork.Width,bodyHeight/DualSenseArtwork.Height);
        if(artwork is null||palette!=DesktopTheme.Current.Id){palette=DesktopTheme.Current.Id;artwork=new DualSenseArtwork(Refresh);}
        if(!active){for(int i=0;i<levels.Length;i++)levels[i]=state.Down(i)?1:0;axes[0]=DualSenseState.Axis(state.LX);axes[1]=DualSenseState.Axis(state.LY);axes[2]=DualSenseState.Axis(state.RX);axes[3]=DualSenseState.Axis(state.RY);}
        D.PushOpacity(state.Connected==1?1:.48);
        D.PushTransform(new TranslateTransform((Width-DualSenseArtwork.Width*scale)/2,46+(bodyHeight-DualSenseArtwork.Height*scale)/2));D.PushTransform(new ScaleTransform(scale,scale));
        artwork.Draw(D,state,levels,ripples,axes,seconds,touchTrail);D.Pop();D.Pop();D.Pop();
        if(details)
        {
            double top=Height-164,half=(Width-60)/2;
            AxisDetails("STICK GAUCHE",state.LX,state.LY,state.RawLT,"L2",24,top,half,state.Connected==1);
            AxisDetails("STICK DROIT",state.RX,state.RY,state.RawRT,"R2",36+half,top,half,state.Connected==1);
        }
        else if(state.Connected==0)Text(snapshot.Status=="Déconnectée"?"Appuie sur PS pour connecter la manette":snapshot.Status,Width/2,Height-81,10,Muted,align:"center");
        if(modeNotice.Length>0)Text(modeNotice,Width/2,Height-69,8,Muted,align:"center",width:Width-48);
        Button("DualSenseTrail","Traînée tactile",24,Height-46,220,30,ToggleTouch,10,color:Station.Settings.DualSenseTouchTrail?Pink:Ink);
        Button("DualSenseAxes",details?"Masquer les axes":"Axes bruts",Width-214,Height-46,190,30,()=>{details=!details;Refresh();},10);
    }
    void ToggleTouch()
    {
        bool enabled=!Station.Settings.DualSenseTouchTrail;
        try{
            Station.ApplySettings(Station.Settings with{DualSenseTouchTrail=enabled},Station.TargetDate);
            Reader.SetTouchEnabled(enabled);touchTrail.Clear();
            modeNotice=enabled?"":"Mode compatible : éteins puis rallume la manette.";
        }
        catch(System.IO.IOException){modeNotice="Réglage non enregistré.";}
        Refresh();
    }
    void AxisDetails(string title,int x,int y,int trigger,string triggerName,double left,double top,double width,bool connected)
    {
        Box(left,top,width,100,"#102D203B","#22DACDEC",12);
        Text(title,left+12,top+9,8,Muted);
        Text(connected?$"X {x:+0;-0;0}":"X —",left+12,top+28,9,Ink,font:DockAppearance.NumberFont);
        Text(connected?$"Y {y:+0;-0;0}":"Y —",left+12,top+43,9,Ink,font:DockAppearance.NumberFont);
        Text(connected?$"Écart {DualSenseState.Offset(x,y):0.00} %":"Écart —",left+12,top+63,8,Muted);
        Text(connected?$"{triggerName} {trigger:+0;-0;0}":triggerName+" —",left+12,top+80,7.5,Muted);
        double cx=left+width-40,cy=top+58;
        D.DrawEllipse(null,new Pen(B("#50C4A9D9"),1),new Point(cx,cy),24,24);
        Line(cx-24,cy,cx+24,cy,"#30C9B4DB");Line(cx,cy-24,cx,cy+24,"#30C9B4DB");
        if(connected)D.DrawEllipse(B(Pink),null,new Point(cx+DualSenseState.Axis(x)*24,cy+DualSenseState.Axis(y)*24),3,3);
    }
    internal object Inspect()=>new{cacheReady=artwork?.CacheReady==true,touchEnabled=Station.Settings.DualSenseTouchTrail,Reader.Active,Reader.Running,status=Reader.Snapshot.Status,state=Reader.Snapshot.State,details,bluetoothBattery};
    public void Dispose(){CompositionTarget.Rendering-=Frame;Reader.Dispose();}
}
