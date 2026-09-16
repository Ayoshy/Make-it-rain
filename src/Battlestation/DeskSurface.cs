using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Battlestation;
internal enum DeskWidget {Clock,Weather,Music,Projects}
internal sealed class DeskSurface : Surface
{
    public Spectrum Audio {get;}=new();
    readonly float[] bands=new float[12];
    bool popup;
    DrawingGroup? projectBackdrop;
    BitmapSource? projectFrost;
    readonly DeskWidget widget;
    int projectRow;
    Size cachedSize;
    Rect ProjectPopup=>new(16,48,Width-32,Math.Min(124,Height-60));
    public DeskSurface(Station station,DeskWidget kind):base(station,kind switch{DeskWidget.Clock=>0,DeskWidget.Weather=>1,DeskWidget.Music=>3,_=>4}){widget=kind;Width=720;Height=kind switch{DeskWidget.Clock=>164,DeskWidget.Weather=>112,DeskWidget.Music=>168,_=>293};}
    public void TickAudio(bool reactive=false,bool visible=true)
    {
        bool playing=Native.Read("playing")=="1";
        if(reactive||playing&&visible)Audio.Start();else Audio.Stop();
        var next=Audio.Bands;bool changed=false;
        for(int i=0;i<12;i++){float value=playing?bands[i]+(next[i]-bands[i])*(next[i]>bands[i]?.6f:.12f):0;changed|=Math.Abs(value-bands[i])>.001;bands[i]=value;}
        if(changed&&visible)Refresh();
        if(reactive)Native.BackgroundAudio(next.Take(4).Max(),next.Skip(4).Take(4).Average(),next.Skip(8).Max(),(float)Station.AudioIntensity);
        else Native.BackgroundAudio(0,0,0,0);
    }
    protected override void Paint()
    {
        switch(widget)
        {
            case DeskWidget.Clock: Clock();break;
            case DeskWidget.Weather: Weather();break;
            case DeskWidget.Music: Music();break;
            case DeskWidget.Projects:
                Projects();var rect=ProjectPopup;Glass(8,rect.X,rect.Y,popup?rect.Width:0,popup?rect.Height:0);break;
        }
    }

    void Clock()
    {

        var colors=new[]{"#E3C8E1","#F6E5C9","#C2AFCA"};
        var keys=new[]{"clockHours","clockMinutes","clockSeconds"};
        double size=Math.Min(74.4,Math.Min((Width-48)/6,(Height-44)/1.5)),top=(Height-36-size*1.6)/2;
        for(int i=0;i<3;i++)Text(Native.Read(keys[i]),(i+.5)*Width/3,top,size,colors[i],"GTAArtDeco","center");
        Line(Width/3,top+24,Width/3,top+64,"#2AD1B5DB");Line(Width*2/3,top+24,Width*2/3,top+64,"#2AD1B5DB");
        Text(Native.Read("clockDate"),Width/2,Height-32,12,"#AF9CBA",align:"center");
    }
    void Weather()
    {
        double y=(Height-112)/2;bool compact=Width<600;
        Image(System.IO.Path.Combine(Station.Assets,"Desk/Icons",Native.Read("weatherIcon")+".png"),20,y+20,48,48);
        Hit("WeatherRefresh",16,y+14,56,62,()=>Native.DeskCommand("WeatherRefresh"));
        Text(Native.Read("weatherTemp"),80,y+18,32,font:DockAppearance.NumberFont);
        double x=compact?166:240;
        Text(Native.Read("weatherCity"),x,y+17,12,width:Width-x-(compact?20:160));
        Text(Native.Read("weatherCondition"),x,y+44,10,Muted,width:Width-x-(compact?20:160));
        if(compact)Text(Native.Read("weatherRange")+" · "+Native.Read("weatherWind"),24,y+82,9,"#B8A8C5",width:Width-48);
        else{Text(Native.Read("weatherRange"),Width-24,y+25,10,"#B8A8C5",align:"right");Text(Native.Read("weatherWind"),Width-24,y+54,10,Muted,align:"right");}
    }
    void MediaCommand(string command,string capability){if(Native.Read(capability)=="1")Native.DeskCommand(command);}
    void Music()
    {
        double y=(Height-168)/2;
        var center=new Point(79.2,y+81.6);
        foreach(var r in new[]{50.4,36,22.8,7.2})D.DrawEllipse(B(r==7.2?"#8CAE9CBD":"#64161021"),new Pen(B("#37C3B8D4"),1),center,r,r);
        if(Station.Cover is {} cover){D.PushClip(new EllipseGeometry(center,38.4,38.4));D.DrawImage(cover,new Rect(40.8,y+43.2,76.8,76.8));D.Pop();}
        if(Native.Read("playing")=="1")for(int i=0;i<32;i++)
        {
            var a=i*2*Math.PI/32;double outer=72,inner=outer-Math.Max(2,30*bands[(int)((i%16)*12/16d)]);
            Line(center.X+Math.Sin(a)*inner,center.Y-Math.Cos(a)*inner,center.X+Math.Sin(a)*outer,center.Y-Math.Cos(a)*outer,i%4==0?"#F59DDFEC":"#F0CD ADE0".Replace(" ",""),3.6);
        }
        double content=Width-182;
        Text(Native.Read("source"),158,y+16,10,"#A99DBC",width:content-58);Hit("Source",158,y+12,content-58,28,()=>Native.DeskCommand("Source"));
        Button("MediaReserve","+",Width-62,y+9,38,29,Station.ShowReserve,13);
        Text(Native.Read("title"),158,y+43,16,width:content);
        Text(Native.Read("artist"),158,y+77,11,"#ABA2BB",width:content);
        if(Width>=620)Text(Native.Read("mediaTime"),158,y+118,10,"#ABA2BB",width:content-210);
        MediaButton("Previous","previous",Width-206,y+105,48,36,"canPrevious");
        MediaButton("Play",Native.Read("playing")=="1"?"pause":"play",Width-145,y+101,58,44,"canPlay");
        MediaButton("Next","next",Width-74,y+105,48,36,"canNext");
        double.TryParse(Native.Read("mediaProgress"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var ratio);
        Track(158,y+151,content,ratio*100,"#B6A4C0",2.4);
        Hit("Seek",158,y+145,content,14,()=>{if(Native.Read("canSeek")=="1")Native.DeskCommand("Seek:"+Math.Clamp((Pointer.X-158)/content,0,1).ToString(System.Globalization.CultureInfo.InvariantCulture));});
    }
    void MediaButton(string action,string icon,double x,double y,double w,double h,string capability)
    {
        Button(action,"",x,y,w,h,()=>MediaCommand(action,capability),enabled:Native.Read(capability)=="1");
        Image(System.IO.Path.Combine(Station.Assets,"Desk/Icons",icon+".png"),x+w/2-12,y+h/2-12,24,24,Native.Read(capability)=="1"?.9:.35);
    }
    void Projects()
    {
        Header("PROJETS");
        if(!popup){projectBackdrop=null;projectFrost=null;ProjectCards();return;}
        ProjectGlass();
        var rect=ProjectPopup;
        Text(ProjectName(Native.Read("selectedPath"),Native.Read("selectedProject")),32,rect.Y+12,15,"#F4EDF9",bold:true,width:Width-100);
        string selected=Native.Read("selectedPath");var status=Station.Projects.Read(selected);
        Text(ProjectStatus(status),32,rect.Y+42,9.6,ProjectStatusColor(status),width:Width-98);
        Button("CloseProject","×",Width-62,rect.Y+8,30,30,()=>popup=false);
        double bw=(Width-100)/4;
        Button("Explorer","Explorateur",32,rect.Y+72,bw,36,()=>{Native.DeskCommand("OpenSelected");popup=false;},11,color:"#F4EDF9");
        Button("OpenCodex","Codex CLI (ChatGPT)",44+bw,rect.Y+72,bw,36,()=>{Station.Terminal?.OpenCodex(Native.Read("selectedPath"));popup=false;},11,color:"#F4EDF9");
        Button("OpenCodexDs","Codex CLI (DS)",56+bw*2,rect.Y+72,bw,36,()=>{Station.OpenCodexDeepSeek(Native.Read("selectedPath"));popup=false;},11,color:"#F4EDF9");
        Button("OpenKilo","Kilo CLI (DS)",68+bw*3,rect.Y+72,bw,36,()=>{Station.OpenKilo(Native.Read("selectedPath"));popup=false;},11,color:"#F4EDF9");
    }
    int ProjectCount=>int.TryParse(Native.Read("projectCount"),out int count)?Math.Max(0,count):0;
    internal IEnumerable<string> VisibleProjects(){int cols=Math.Max(1,(int)((Width-36)/330)),rows=Math.Max(1,(int)((Height-60)/73));return Enumerable.Range(Math.Min(projectRow*cols,ProjectCount),Math.Max(0,Math.Min(cols*rows,ProjectCount-projectRow*cols))).Select(i=>Native.Read($"project:{i}:path")).Append(Native.Read("selectedPath")).Where(p=>p!="").ToArray();}
    void ProjectCards(bool interactive=true)
    {
        int cols=Math.Max(1,(int)((Width-36)/330)),rows=Math.Max(1,(int)((Height-60)/73));
        projectRow=Math.Clamp(projectRow,0,Math.Max(0,(int)Math.Ceiling(ProjectCount/(double)cols)-rows));double cell=(Width-48-(cols-1)*12)/cols;
        for(int i=projectRow*cols;i<Math.Min(ProjectCount,(projectRow+rows)*cols);i++)
        {
            double x=24+(i%cols)*(cell+12),yy=52+(i/cols-projectRow)*73;int index=i;
            Button("Project"+i,"",x,yy,cell,62,()=>{Native.DeskCommand("Select:"+index);popup=Native.Read("selectedPath")!="";},hitTest:interactive);
            Text(ProjectName(Native.Read($"project:{i}:path"),Native.Read($"project:{i}:name")),x+14,yy+9,13.2,width:cell-28);
            var state=Station.Projects.Read(Native.Read($"project:{i}:path"));
            Text(state.Branch is null?Native.Read($"project:{i}:age"):ProjectStatus(state),x+14,yy+36,9.6,ProjectStatusColor(state),width:cell-28);
        }
        int total=(int)Math.Ceiling(ProjectCount/(double)cols);if(total>rows){double track=Height-76;Box(Width-12,54,3,track,"#305C4868",radius:2);Box(Width-12,54+track*projectRow/total,3,track*rows/total,"#A0DAC3E5",radius:2);}
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e){if(widget!=DeskWidget.Projects||popup)return;projectRow+=e.Delta>0?-1:1;projectBackdrop=null;Refresh();e.Handled=true;}
    void ProjectGlass()
    {
        // Cache only our own project artwork, never desktop or terminal pixels.
        if(projectBackdrop is null||cachedSize!=new Size(Width,Height))
        {
            cachedSize=new(Width,Height);
            projectBackdrop=new DrawingGroup();var context=D;
            try{using var artwork=projectBackdrop.Open();D=artwork;ProjectCards(false);}finally{D=context;}
            if(projectBackdrop.CanFreeze)projectBackdrop.Freeze();
            var visual=new DrawingVisual{Effect=new BlurEffect{Radius=7,RenderingBias=RenderingBias.Quality}};
            using(var artwork=visual.RenderOpen())artwork.DrawDrawing(projectBackdrop);
            var dpi=VisualTreeHelper.GetDpi(this);
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(Width*dpi.DpiScaleX),(int)Math.Ceiling(Height*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
            bitmap.Render(visual);bitmap.Freeze();projectFrost=bitmap;
        }
        var rect=ProjectPopup;
        var outer=new RectangleGeometry(rect,22,22);
        var outside=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,Width,Height)),outer);
        D.PushClip(outside);D.DrawDrawing(projectBackdrop);D.Pop();
        // A clear centre and progressively stronger lensing in the curved rim.
        for(int band=0;band<4;band++)
        {
            double inset=band*3;var bounds=rect;bounds.Inflate(-inset,-inset);
            Geometry clip=new RectangleGeometry(bounds,22-inset,22-inset);
            if(band<3){var inner=bounds;inner.Inflate(-3,-3);clip=new CombinedGeometry(GeometryCombineMode.Exclude,clip,new RectangleGeometry(inner,19-inset,19-inset));}
            D.PushClip(clip);
            double scale=band==3?1.008:1.055-band*.015;
            D.PushTransform(new ScaleTransform(scale,scale,Width/2,rect.Y+rect.Height/2));
            D.PushOpacity(.48);D.DrawImage(projectFrost,new Rect(0,0,Width,Height));D.Pop();D.Pop();D.Pop();
        }
        var wash=DesktopTheme.Gradient("#12F6EFFF","#0A97C1E1",75);
        D.DrawRoundedRectangle(DesktopTheme.Gradient("#101B1227","#261B1227",90),null,rect,22,22);
        D.DrawRoundedRectangle(wash,null,rect,22,22);
        var rim=new LinearGradientBrush();rim.StartPoint=new Point(0,0);rim.EndPoint=new Point(1,1);
        rim.GradientStops.Add(new GradientStop(DesktopTheme.Color("#96FFF3FF"),0));
        rim.GradientStops.Add(new GradientStop(DesktopTheme.Color("#16E4DAF8"),.4));
        rim.GradientStops.Add(new GradientStop(DesktopTheme.Color("#64B8E0F8"),1));
        D.DrawRoundedRectangle(null,new Pen(rim,1),rect,22,22);
        if(rect.Contains(Pointer))
        {
            var light=new RadialGradientBrush(DesktopTheme.Color("#8CFFFAFF"),Colors.Transparent){MappingMode=BrushMappingMode.Absolute,Center=Pointer,GradientOrigin=Pointer,RadiusX=160,RadiusY=100};
            D.DrawRoundedRectangle(null,new Pen(light,1.5),rect,22,22);
        }
    }
    string ProjectName(string path,string name)=>string.Equals(path.TrimEnd('\\','/'),Station.Root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)?"Battlestation":name;
    static string ProjectStatus(ProjectSignal signal)=>string.Join(" · ",new[]{signal.Branch,signal.Changes.HasValue?(signal.Changes==0?"propre":$"{signal.Changes} modif."):null}.Where(x=>x is not null));
    static string ProjectStatusColor(ProjectSignal signal)=>signal.Changes switch{0=>"#A6DFC0",>0=>"#F0C38B",_=>"#A89AB9"};
}
