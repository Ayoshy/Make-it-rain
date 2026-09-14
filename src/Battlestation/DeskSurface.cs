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
    const double MusicY=0,ProjectsY=0;
    public DeskSurface(Station station,DeskWidget kind):base(station){widget=kind;Width=720;Height=kind switch{DeskWidget.Clock=>164,DeskWidget.Weather=>112,DeskWidget.Music=>168,_=>293};}
    public void TickAudio()
    {
        bool playing=Native.Read("playing")=="1";
        if(playing)Audio.Start();else Audio.Stop();
        var next=Audio.Bands;bool changed=false;
        for(int i=0;i<12;i++){float value=playing?bands[i]+(next[i]-bands[i])*(next[i]>bands[i]?.6f:.12f):0;changed|=Math.Abs(value-bands[i])>.001;bands[i]=value;}
        if(changed)Refresh();
    }
    protected override void Paint()
    {
        switch(widget)
        {
            case DeskWidget.Clock: Clock();Glass(0,0,0,720,163.2);break;
            case DeskWidget.Weather: Weather();Glass(1,0,0,720,110.4);break;
            case DeskWidget.Music: Music();Glass(3,0,0,720,168);break;
            case DeskWidget.Projects:
                Projects();Glass(4,0,0,720,292.8);Glass(8,48,66,popup?624:0,popup?172.8:0);break;
        }
    }

    void Clock()
    {
        Panel(0,0,720,163.2);
        var colors=new[]{"#E3C8E1","#F6E5C9","#C2AFCA"};
        var keys=new[]{"clockHours","clockMinutes","clockSeconds"};
        for(int i=0;i<3;i++)Text(Native.Read(keys[i]),144+i*216,15.6,74.4,colors[i],"GTAArtDeco","center");
        Line(252,42,252,92.4,"#2AD1B5DB");Line(468,42,468,92.4,"#2AD1B5DB");
        Text(Native.Read("clockDate"),360,129.6,12,"#AF9CBA",align:"center");
    }
    void Weather()
    {
        const double y=0;Panel(0,y,720,110.4);
        Image(System.IO.Path.Combine(Station.Assets,"Desk/Icons",Native.Read("weatherIcon")+".png"),28.8,y+26.4,57.6,57.6);
        Hit("WeatherRefresh",24,y+20,72,72,()=>Native.DeskCommand("WeatherRefresh"));
        Text(Native.Read("weatherTemp"),112.8,y+18,38.4,font:"Bahnschrift");
        Text(Native.Read("weatherCity"),247.2,y+22.8,14.4,width:330);
        Text(Native.Read("weatherCondition"),247.2,y+52.8,12,Muted,width:330);
        Text(Native.Read("weatherRange"),688.8,y+25.2,10.8,"#B8A8C5",align:"right");
        Text(Native.Read("weatherWind"),688.8,y+54,10.8,"#A295B1",align:"right");
    }
    void MediaCommand(string command,string capability){if(Native.Read(capability)=="1")Native.DeskCommand(command);}
    void Music()
    {
        var y=MusicY;Panel(0,y,720,168);
        var center=new Point(79.2,y+81.6);
        foreach(var r in new[]{50.4,36,22.8,7.2})D.DrawEllipse(B(r==7.2?"#8CAE9CBD":"#64161021"),new Pen(B("#37C3B8D4"),1),center,r,r);
        if(Station.Cover is {} cover){D.PushClip(new EllipseGeometry(center,38.4,38.4));D.DrawImage(cover,new Rect(40.8,y+43.2,76.8,76.8));D.Pop();}
        if(Native.Read("playing")=="1")for(int i=0;i<32;i++)
        {
            var a=i*2*Math.PI/32;double outer=72,inner=outer-Math.Max(2,30*bands[(int)((i%16)*12/16d)]);
            Line(center.X+Math.Sin(a)*inner,center.Y-Math.Cos(a)*inner,center.X+Math.Sin(a)*outer,center.Y-Math.Cos(a)*outer,i%4==0?"#F59DDFEC":"#F0CD ADE0".Replace(" ",""),3.6);
        }
        Text(Native.Read("source"),158.4,y+19.2,10.8,"#A99DBC");Hit("Source",158,y+14,215,28,()=>Native.DeskCommand("Source"));
        Text(Native.Read("mediaError"),393.6,y+19.2,9.6,"#CA9BB4",width:276);
        Text(Native.Read("title"),158.4,y+43.2,18,width:528);
        Text(Native.Read("artist"),158.4,y+81.6,12,"#ABA2BB",width:528);
        Text(Native.Read("mediaTime"),158.4,y+115.2,10.8,"#ABA2BB");
        MediaButton("Previous","previous",480,y+102,48,38.4,"canPrevious");
        MediaButton("Play",Native.Read("playing")=="1"?"pause":"play",547.2,y+97.2,57.6,48,"canPlay");
        MediaButton("Next","next",624,y+102,48,38.4,"canNext");
        double.TryParse(Native.Read("mediaProgress"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var ratio);
        Track(158.4,y+151.2,511.2,ratio*100,"#B6A4C0",2.4);
        Hit("Seek",158.4,y+144,511.2,14.4,()=>{if(Native.Read("canSeek")=="1")Native.DeskCommand("Seek:"+Math.Clamp((Pointer.X-158.4)/511.2,0,1).ToString(System.Globalization.CultureInfo.InvariantCulture));});
    }
    void MediaButton(string action,string icon,double x,double y,double w,double h,string capability)
    {
        Button(action,"",x,y,w,h,()=>MediaCommand(action,capability),enabled:Native.Read(capability)=="1");
        Image(System.IO.Path.Combine(Station.Assets,"Desk/Icons",icon+".png"),x+w/2-12,y+h/2-12,24,24,Native.Read(capability)=="1"?.9:.35);
    }
    void Projects()
    {
        double y=ProjectsY;Panel(0,y,720,292.8);Text("PROJETS",28.8,y+22.8,10.8,"#B8ABCA");
        if(!popup){projectBackdrop=null;projectFrost=null;ProjectCards();return;}
        ProjectGlass();
        Text(ProjectName(Native.Read("selectedPath"),Native.Read("selectedProject")),76.8,y+91.2,16.8,"#F4EDF9",bold:true,width:502);
        Button("CloseProject","×",614.4,y+81.6,31.2,31.2,()=>popup=false);
        Button("Explorer","Explorateur",76.8,y+154.8,244.8,50.4,()=>{Native.DeskCommand("OpenSelected");popup=false;},13.2,color:"#F4EDF9");
        Button("OpenCodex","Codex CLI",350.4,y+154.8,292.8,50.4,()=>{Station.Terminal?.OpenCodex(Native.Read("selectedPath"));popup=false;},13.2,color:"#F4EDF9");
    }
    void ProjectCards(bool interactive=true)
    {
        for(int i=0;i<6;i++)
        {
            double x=28.8+(i%2)*345.6,yy=ProjectsY+58.8+(i/2)*73.2;int index=i;
            Button("Project"+i,"",x,yy,316.8,62.4,()=>{Native.DeskCommand("Select:"+index);popup=Native.Read("selectedPath")!="";},hitTest:interactive);
            Text(ProjectName(Native.Read($"project:{i}:path"),Native.Read($"project:{i}:name")),x+14.4,yy+9.6,13.2,width:280.8);
            Text(Native.Read($"project:{i}:age"),x+14.4,yy+36,9.6,"#948BA3");
        }
    }
    void ProjectGlass()
    {
        // Cache only our own project artwork, never desktop or terminal pixels.
        if(projectBackdrop is null)
        {
            projectBackdrop=new DrawingGroup();var context=D;
            try{using var artwork=projectBackdrop.Open();D=artwork;ProjectCards(false);}finally{D=context;}
            projectBackdrop.Freeze();
            var visual=new DrawingVisual{Effect=new BlurEffect{Radius=7,RenderingBias=RenderingBias.Quality}};
            using(var artwork=visual.RenderOpen())artwork.DrawDrawing(projectBackdrop);
            var dpi=VisualTreeHelper.GetDpi(this);
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(720*dpi.DpiScaleX),(int)Math.Ceiling(293*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
            bitmap.Render(visual);bitmap.Freeze();projectFrost=bitmap;
        }
        var rect=new Rect(48,66,624,172.8);
        var outer=new RectangleGeometry(rect,22,22);
        var outside=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,720,293)),outer);
        D.PushClip(outside);D.DrawDrawing(projectBackdrop);D.Pop();
        // A clear centre and progressively stronger lensing in the curved rim.
        for(int band=0;band<4;band++)
        {
            double inset=band*3;var bounds=rect;bounds.Inflate(-inset,-inset);
            Geometry clip=new RectangleGeometry(bounds,22-inset,22-inset);
            if(band<3){var inner=bounds;inner.Inflate(-3,-3);clip=new CombinedGeometry(GeometryCombineMode.Exclude,clip,new RectangleGeometry(inner,19-inset,19-inset));}
            D.PushClip(clip);
            double scale=band==3?1.008:1.055-band*.015;
            D.PushTransform(new ScaleTransform(scale,scale,360,152.4));
            D.PushOpacity(.48);D.DrawImage(projectFrost,new Rect(0,0,720,293));D.Pop();D.Pop();D.Pop();
        }
        var wash=new LinearGradientBrush(Color.FromArgb(18,246,239,255),Color.FromArgb(10,151,193,225),75);
        D.DrawRoundedRectangle(new LinearGradientBrush(Color.FromArgb(16,27,18,39),Color.FromArgb(38,27,18,39),90),null,rect,22,22);
        D.DrawRoundedRectangle(wash,null,rect,22,22);
        var rim=new LinearGradientBrush();rim.StartPoint=new Point(0,0);rim.EndPoint=new Point(1,1);
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(150,255,243,255),0));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(22,228,218,248),.4));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(100,184,224,248),1));
        D.DrawRoundedRectangle(null,new Pen(rim,1),rect,22,22);
        if(rect.Contains(Pointer))
        {
            var light=new RadialGradientBrush(Color.FromArgb(140,255,250,255),Colors.Transparent){MappingMode=BrushMappingMode.Absolute,Center=Pointer,GradientOrigin=Pointer,RadiusX=160,RadiusY=100};
            D.DrawRoundedRectangle(null,new Pen(light,1.5),rect,22,22);
        }
    }
    string ProjectName(string path,string name)=>string.Equals(path.TrimEnd('\\','/'),Station.Root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)?"Battlestation":name;
}
