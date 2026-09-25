using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Battlestation;
internal sealed class AudioSurface : Surface,IDisposable
{
    internal AudioMixerWorker Mixer {get;}=new();
    readonly List<(Rect Bounds,string? Key)> sliders=[];
    int page;
    bool dragging;
    float dragVolume;
    (Rect Bounds,string? Key) drag;
    readonly Dictionary<string,float> levels=[];
    bool active,animating;
    TimeSpan lastFrame;
    internal AudioSurface(Station station):base(station,10){Width=720;Height=264;Mixer.Poll();}
    int lastSnapshot;
    internal void Poll(){if(!dragging)Mixer.Poll();var hash=new HashCode();hash.Add(Mixer.OutputName);hash.Add(Mixer.Volume);hash.Add(Mixer.Muted);hash.Add(Mixer.MicrophoneMuted);hash.Add(Mixer.Error);foreach(var row in Mixer.Apps)hash.Add(row);foreach(var output in Mixer.Outputs)hash.Add(output);int next=hash.ToHashCode();if(next!=lastSnapshot){lastSnapshot=next;Refresh();}if(active&&Mixer.Apps.Any(Sounding))Animating(true);}
    static bool Sounding(AudioApp app)=>!app.Muted&&app.Peak>.0001f;
    // Les vumètres ne tiennent la boucle d'images que tant qu'un son ou sa retombée se voit.
    void Animating(bool on)
    {
        if(animating==on)return;animating=on;lastFrame=default;
        if(on)CompositionTarget.Rendering+=Animate;else CompositionTarget.Rendering-=Animate;
    }
    internal void SetActive(bool value)
    {
        Mixer.SetActive(value);if(active==value)return;active=value;
        if(value)Animating(true);
        else{Animating(false);levels.Clear();}
    }
    void Animate(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs frame||frame.RenderingTime==lastFrame)return;
        // 30 Hz, like Network: redrawing this dock on every 144 Hz frame starved
        // the shared UI thread (84 redraws/s measured) during video mirroring.
        if(lastFrame!=default&&(frame.RenderingTime-lastFrame).TotalSeconds<1/30d-.001)return;
        double elapsed=lastFrame==default?1/60d:Math.Clamp((frame.RenderingTime-lastFrame).TotalSeconds,0,.1);lastFrame=frame.RenderingTime;
        var apps=Mixer.Apps;bool changed=false;
        foreach(var app in apps)
        {
            float previous=levels.GetValueOrDefault(app.Key);
            float next=AudioMeterMotion.Step(previous,app.Muted?0:app.Peak,elapsed);
            changed|=Math.Abs(previous-next)>.00001f;levels[app.Key]=next;
        }
        foreach(string key in levels.Keys.Where(key=>!apps.Any(app=>app.Key==key)).ToArray()){levels.Remove(key);changed=true;}
        if(changed)Refresh();
        else if(!apps.Any(Sounding))Animating(false);
    }
    protected override void Paint()
    {
        sliders.Clear();
        Header("AUDIO");
        Button("AudioOutput","",100,12,Width-234,36,Outputs);
        Text(Mixer.OutputName,112,19,10,width:Width-258);
        Button("Microphone",Mixer.MicrophoneMuted==true?"Micro coupé":"Micro",Width-122,12,98,36,()=>Mixer.ToggleMicrophone(),10,color:Mixer.MicrophoneMuted==true?"#FFA9D8":Ink,enabled:Mixer.MicrophoneMuted.HasValue);
        Row(null,"Volume général",Mixer.Volume,Mixer.Muted??false,0,58);
        Line(24,98,Width-24,98,"#30C9B4DB");
        int count=Math.Max(1,(int)((Height-132)/44)),pages=Math.Max(1,(Mixer.Apps.Count+count-1)/count);page=Math.Clamp(page,0,pages-1);
        var rows=Mixer.Apps.Skip(page*count).Take(count).ToArray();
        for(int i=0;i<rows.Length;i++){var row=rows[i];Row(row.Key,row.Name,row.Volume,row.Muted,levels.GetValueOrDefault(row.Key),110+i*44);}
        if(rows.Length==0)Text("Aucune application audio",24,119,11,Muted);
        Text(Mixer.Error,24,Height-26,9,"#F4B7CA",width:Width-215);
        if(pages>1)
        {
            Button("AudioPrevious","‹",Width-170,Height-32,38,24,()=>page--,enabled:page>0);
            Text($"{page+1} / {pages}",Width-100,Height-29,9,Muted,align:"center");
            Button("AudioNext","›",Width-62,Height-32,38,24,()=>page++,enabled:page+1<pages);
        }
    }
    void Row(string? key,string name,float? volume,bool muted,float peak,double y)
    {
        if(dragging&&drag.Key==key)volume=dragVolume;
        double label=Math.Min(225,Width*.28),left=label+40,track=Width-left-146;
        Text(name,24,y,11,width:label);
        var bounds=new Rect(left,y-3,track,34);if(volume.HasValue)sliders.Add((bounds,key));
        if(volume.HasValue)HoverGlass(bounds);
        Track(left,y+13,track,(volume??float.NaN)*100,muted?"#7E718E":"#DBBFED",4);
        if(volume.HasValue)
        {
            double x=left+track*Math.Clamp(volume.Value,0,1);
            var pearl=DesktopTheme.Gradient("#FFF5FF","#A6CBE4",45);
            D.DrawEllipse(pearl,new Pen(B("#F2E5FF"),1),new Point(x,y+15),7,7);
            Hit("AudioVolume:"+(key??"master"),left,y-3,track,34,()=>Mixer.SetVolume(key,(float)((Pointer.X-left)/track)));
        }
        Text(volume.HasValue?$"{volume.Value*100:0}":"—",Width-103,y,10,Muted,font:DockAppearance.NumberFont,align:"right");
        Button("AudioMute:"+(key??"master"),"",Width-83,y-5,59,34,()=>Mixer.ToggleMute(key),enabled:volume.HasValue);
        Text(muted?"\uE74F":"\uE767",Width-54,y+1,14,muted?"#FFA9D8":Ink,"Segoe Fluent Icons",align:"center");
        if(peak>.001f)Track(24,y+28,label,peak*100,"#A2DFDC",2);
    }
    void Outputs()
    {
        var menu=new ContextMenu();
        foreach(var output in Mixer.Outputs)
        {
            var item=new MenuItem{Header=output.Name,IsCheckable=true,IsChecked=output.Selected};
            item.Click+=(_,_)=>{Mixer.SelectOutput(output.Id);Refresh();};menu.Items.Add(item);
        }
        if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="Aucune sortie disponible",IsEnabled=false});
        menu.PlacementTarget=this;menu.IsOpen=true;
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p=e.GetPosition(this);var slider=sliders.FirstOrDefault(s=>s.Bounds.Contains(p));
        if(slider.Bounds.Width>0){drag=slider;dragging=CaptureMouse();SetDrag(p);e.Handled=true;}
        else base.OnMouseLeftButtonDown(e);
    }
    void SetDrag(Point p){dragVolume=Math.Clamp((float)((p.X-drag.Bounds.X)/drag.Bounds.Width),0,1);Mixer.SetVolume(drag.Key,dragVolume);Refresh();}
    protected override void OnPointer(MouseEventArgs e){if(dragging)SetDrag(e.GetPosition(this));}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(dragging){SetDrag(e.GetPosition(this));dragging=false;ReleaseMouseCapture();e.Handled=true;}else base.OnMouseLeftButtonUp(e);
    }
    protected override void OnLostMouseCapture(MouseEventArgs e){dragging=false;base.OnLostMouseCapture(e);}
    protected override void OnMouseWheel(MouseWheelEventArgs e){page+=e.Delta>0?-1:1;Refresh();e.Handled=true;}
    public void Dispose(){SetActive(false);Mixer.Dispose();}
}
