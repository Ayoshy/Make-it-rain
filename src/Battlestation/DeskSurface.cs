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
    // Le morceau habille le verre : pochette floutée en surimpression, égaliseur au
    // premier plan. Seule l'image de la piste courante est calculée puis gardée.
    BitmapSource? musicBackdrop,musicBackdropSource;
    Color musicAccent=Color.FromRgb(190,129,255);
    Brush[] ribbonBrushes=[],peakBrushes=[],tintBrushes=[],glowBrushes=[];
    Pen[] rimPens=[];
    Brush progressBrush=Brushes.Transparent;
    readonly RadialGradientBrush shade=new();
    readonly float[] peaks=new float[96];
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
        // Attaque immédiate, retombée lente : les crêtes survivent aux barres.
        for(int i=0;i<peaks.Length;i++)
        {
            float level=playing?MusicLevel(i/(double)(peaks.Length-1)):0;
            float kept=level>peaks[i]?level:Math.Max(0,peaks[i]-.010f);
            changed|=Math.Abs(kept-peaks[i])>.001;peaks[i]=kept;
        }
        Station.MusicBands=bands;
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
        // The strip only appears when the dock is tall enough for it; below that the
        // dock keeps exactly its former rendering.
        bool tall=Height>=190;
        double y=tall?18:(Height-112)/2;bool compact=Width<600;string rain=Native.Read("weatherRain");
        Image(System.IO.Path.Combine(Station.Assets,"Desk/Icons",Native.Read("weatherIcon")+".png"),20,y+20,48,48);
        Hit("WeatherRefresh",16,y+14,56,62,()=>Native.DeskCommand("WeatherRefresh"));
        Text(Native.Read("weatherTemp"),80,y+18,32,font:DockAppearance.NumberFont);
        double x=compact?166:240;
        Text(Native.Read("weatherCity"),x,y+17,12,width:Width-x-(compact?20:160));
        Text(Native.Read("weatherCondition"),x,y+44,10,Muted,width:Width-x-(compact?20:160));
        if(compact)Text(Native.Read("weatherRange")+" · "+Native.Read("weatherWind")+(rain.Length==0?"":" · "+rain),24,y+82,9,"#B8A8C5",width:Width-48);
        else
        {
            Text(Native.Read("weatherRange"),Width-24,y+25,10,"#B8A8C5",align:"right");
            Text(Native.Read("weatherWind"),Width-24,y+54,10,Muted,align:"right");
            if(rain.Length>0)Text(rain,Width-24,y+80,10,"#A9F8FF",align:"right");
        }
        if(tall)Hourly(y+112);
    }
    // Six hours, same source as the current conditions, drawn under the block.
    void Hourly(double top)
    {
        var hours=Native.Read("weatherHours").Split(';',StringSplitOptions.RemoveEmptyEntries);
        if(hours.Length==0)return;
        if(hours.Length>6)hours=hours[..6];
        double cell=(Width-48)/hours.Length;
        for(int i=0;i<hours.Length;i++)
        {
            var parts=hours[i].Split(':');
            if(parts.Length<4||!int.TryParse(parts[2],out int code))continue;
            bool day=int.TryParse(parts[0],out int hour)&&hour>=7&&hour<=20;
            double center=24+i*cell+cell/2;
            Text(parts[0].PadLeft(2,'0')+" h",center,top+10,9,Muted,align:"center");
            Image(System.IO.Path.Combine(Station.Assets,"Desk/Icons",WeatherIconName(code,day)+".png"),center-10,top+20,20,20);
            double.TryParse(parts[3],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var chance);
            // A high probability is readable in the temperature itself, no extra legend.
            Text(parts[1]+"°",center,top+42,11,chance>=60?"#72E7D5FA":Ink,DockAppearance.NumberFont,align:"center");
        }
    }
    // Same mapping as the native weather icons, expressed in file names.
    static string WeatherIconName(int code,bool day)=>code switch
    {
        0=>day?"weather-sun":"weather-moon",
        >=1 and <=3=>"weather-cloud",
        45 or 48=>"weather-fog",
        >=51 and <=67 or >=80 and <=82=>"weather-rain",
        >=71 and <=77 or 85 or 86=>"weather-snow",
        >=95 and <=99=>"weather-storm",
        _=>"weather-unknown"
    };
    void MediaCommand(string command,string capability){if(Native.Read(capability)=="1")Native.DeskCommand(command);}
    // Le verre porte le morceau : la pochette le teinte et l'égaliseur l'anime.
    static readonly Color Night=Color.FromRgb(7,5,18);
    static readonly Color FallbackAccent=Color.FromRgb(190,129,255);
    static readonly LinearGradientBrush BottomWash=Wash(new Point(0,1),new Point(0,0),(0,Fade(Night,.34)),(.46,Fade(Night,.10)),(1,Fade(Night,0)));
    static readonly LinearGradientBrush FallbackGlass=Wash(new Point(0,0),new Point(1,1),(0,Color.FromRgb(0x28,0x1D,0x40)),(1,Color.FromRgb(0x0C,0x09,0x18)));
    static readonly Brush[] FlareBrushes=LevelBrushes(Colors.White,.08,.30);
    static readonly SolidColorBrush TrackRail=new(Color.FromArgb(0x5A,0x60,0x3D,0x6F));
    static Color Fade(Color color,double alpha)=>Color.FromArgb((byte)Math.Clamp(alpha*255,0,255),color.R,color.G,color.B);
    static LinearGradientBrush Wash(Point start,Point end,params (double Offset,Color Color)[] stops)
    {
        var brush=new LinearGradientBrush{StartPoint=start,EndPoint=end};
        foreach(var (offset,color) in stops)brush.GradientStops.Add(new GradientStop(color,offset));
        brush.Freeze();return brush;
    }
    static Brush[] LevelBrushes(Color color,double floor,double ceiling)
    {
        const int levels=14;var brushes=new Brush[levels];
        for(int i=0;i<levels;i++)
        {
            var brush=new SolidColorBrush(Fade(color,floor+(ceiling-floor)*i/(levels-1d)));brush.Freeze();brushes[i]=brush;
        }
        return brushes;
    }
    // Barres pleines en bas, fondu vers le haut : le ruban garde une base solide.
    static Brush[] BarBrushes(Color color,double floor,double ceiling,double head)
    {
        const int levels=18;var brushes=new Brush[levels];
        for(int i=0;i<levels;i++)
        {
            double alpha=floor+(ceiling-floor)*i/(levels-1d);
            var brush=new LinearGradientBrush{StartPoint=new Point(.5,1),EndPoint=new Point(.5,0)};
            brush.GradientStops.Add(new GradientStop(Fade(color,alpha),0));
            brush.GradientStops.Add(new GradientStop(Fade(color,alpha*head),.72));
            brush.GradientStops.Add(new GradientStop(Fade(color,0),1));
            brush.Freeze();brushes[i]=brush;
        }
        return brushes;
    }
    static Pen[] RimPens(Color color)
    {
        const int levels=16;var pens=new Pen[levels];
        for(int i=0;i<levels;i++)
        {
            double level=i/(levels-1d);
            var pen=new Pen(new SolidColorBrush(Fade(color,.14+.52*level)),1+1.6*level);pen.Freeze();pens[i]=pen;
        }
        return pens;
    }
    static int LevelIndex(double level,IReadOnlyList<Brush> palette)=>Math.Clamp((int)Math.Round(Math.Clamp(level,0,1)*(palette.Count-1)),0,palette.Count-1);
    static int LevelIndex(double level,Pen[] palette)=>Math.Clamp((int)Math.Round(Math.Clamp(level,0,1)*(palette.Length-1)),0,palette.Length-1);
    void SetAccent(Color accent)
    {
        musicAccent=accent;
        ribbonBrushes=BarBrushes(accent,.30,1,.72);
        glowBrushes=BarBrushes(accent,.05,.34,.22);
        peakBrushes=LevelBrushes(accent,.45,.96);
        tintBrushes=LevelBrushes(accent,.06,.20);
        rimPens=RimPens(accent);
        var read=Color.FromRgb((byte)(accent.R+(255-accent.R)*.32),(byte)(accent.G+(255-accent.G)*.32),(byte)(accent.B+(255-accent.B)*.32));
        var progress=new SolidColorBrush(Fade(read,.94));progress.Freeze();progressBrush=progress;
    }
    // Un halo doux derrière le texte suffit : pas de bandeau opaque sur la pochette.
    void Verse(Point center,double radiusX,double radiusY)
    {
        if(shade.GradientStops.Count==0)
        {
            shade.MappingMode=BrushMappingMode.Absolute;
            shade.GradientStops.Add(new GradientStop(Fade(Night,.58),0));
            shade.GradientStops.Add(new GradientStop(Fade(Night,.28),.55));
            shade.GradientStops.Add(new GradientStop(Fade(Night,0),1));
        }
        shade.Center=center;shade.GradientOrigin=center;shade.RadiusX=radiusX;shade.RadiusY=radiusY;
        D.DrawRectangle(shade,null,new Rect(0,0,Width,Height));
    }
    // Courbe commune au ruban et aux crêtes : les basses occupent le premier tiers.
    float MusicLevel(double normalized)
    {
        double scaled=Math.Pow(Math.Clamp(normalized,0,1),1.35)*11;
        int low=Math.Clamp((int)scaled,0,11),high=Math.Min(11,low+1);double mix=scaled-low;
        return (float)(bands[low]*(1-mix)+bands[high]*mix);
    }
    float Bass=>(bands[0]+bands[1]+bands[2]+bands[3]+bands[4])/5;
    float PeakAt(double normalized)=>peaks[(int)Math.Round(Math.Clamp(normalized,0,1)*(peaks.Length-1))];
    void EnsureMusicArt(BitmapSource? cover)
    {
        if(cover is null)
        {
            var themed=DesktopTheme.Color(Purple);
            if(musicBackdrop is null&&themed==musicAccent)return;
            musicBackdrop=null;musicBackdropSource=null;SetAccent(themed);return;
        }
        // Le flou est calculé une fois par piste : un redimensionnement l'étire, sans le refaire.
        if(ReferenceEquals(cover,musicBackdropSource))return;
        musicBackdropSource=cover;
        SetAccent(Dominant(cover));
        musicBackdrop=Blurred(cover,new Size(Width,Height));
    }
    // La pochette est cadrée comme le bloc, débordante, puis floutée une seule fois.
    BitmapSource Blurred(BitmapSource cover,Size size)
    {
        var dpi=VisualTreeHelper.GetDpi(this);
        var visual=new DrawingVisual{Effect=new BlurEffect{Radius=24,RenderingBias=RenderingBias.Quality,KernelType=KernelType.Gaussian}};
        var target=new Rect(-size.Width*.08,-size.Height*.08,size.Width*1.16,size.Height*1.16);
        using(var artwork=visual.RenderOpen())artwork.DrawImage(cover,Fill(cover,target));
        var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(size.Width*dpi.DpiScaleX)),Math.Max(1,(int)Math.Ceiling(size.Height*dpi.DpiScaleY)),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
        bitmap.Render(visual);bitmap.Freeze();return bitmap;
    }
    static Rect Fill(BitmapSource image,Rect target)
    {
        double scale=Math.Max(target.Width/image.PixelWidth,target.Height/image.PixelHeight);
        double width=image.PixelWidth*scale,height=image.PixelHeight*scale;
        return new Rect(target.X+(target.Width-width)/2,target.Y+(target.Height-height)/2,width,height);
    }
    // Couleur dominante : les tons saturés et moyens portent l'identité de la piste.
    static Color Dominant(BitmapSource cover)
    {
        const int side=24;
        try
        {
            var visual=new DrawingVisual();
            using(var artwork=visual.RenderOpen())artwork.DrawImage(cover,Fill(cover,new Rect(0,0,side,side)));
            var bitmap=new RenderTargetBitmap(side,side,96,96,PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels=new byte[side*side*4];bitmap.CopyPixels(pixels,side*4,0);
            double red=0,green=0,blue=0,total=0;
            for(int i=0;i+3<pixels.Length;i+=4)
            {
                double b=pixels[i]/255d,g=pixels[i+1]/255d,r=pixels[i+2]/255d;
                double high=Math.Max(r,Math.Max(g,b)),low=Math.Min(r,Math.Min(g,b));
                double luminance=.2126*r+.7152*g+.0722*b;
                double weight=(.06+high-low)*(.2+Math.Min(luminance,.85));
                red+=r*weight;green+=g*weight;blue+=b*weight;total+=weight;
            }
            if(total<=0)return FallbackAccent;
            red/=total;green/=total;blue/=total;
            double mean=.2126*red+.7152*green+.0722*blue;
            red=mean+(red-mean)*1.5;green=mean+(green-mean)*1.5;blue=mean+(blue-mean)*1.5;
            double peak=Math.Max(red,Math.Max(green,blue));
            if(peak<.6){double gain=.6/Math.Max(.02,peak);red*=gain;green*=gain;blue*=gain;}
            return Color.FromRgb((byte)(Math.Clamp(red,0,1)*255),(byte)(Math.Clamp(green,0,1)*255),(byte)(Math.Clamp(blue,0,1)*255));
        }
        catch{return FallbackAccent;}
    }
    void Music()
    {
        EnsureMusicArt(Station.Cover);
        if(ribbonBrushes.Length==0)SetAccent(musicAccent);
        bool playing=Native.Read("playing")=="1";double bass=Math.Clamp(Bass,0,1);
        // L'égaliseur tient le bas du verre sur toute la largeur ; le texte et les
        // commandes se placent juste au-dessus, l'artwork gardant le reste.
        // Le bloc texte a la priorité : l'égaliseur prend le bas jusqu'à la moitié du verre.
        double baseline=Height-8;
        double span=Math.Clamp(Math.Min(Height*.48,baseline-109),40,300);
        double y=Math.Clamp(baseline-span-12-168,0,Math.Max(0,Height-168)),text=Math.Max(140,Width-268);
        D.PushClip(new RectangleGeometry(new Rect(0,0,Width,Height),DockAppearance.PanelRadius,DockAppearance.PanelRadius));
        if(musicBackdrop is {} artwork)
        {
            // La pochette remplace la teinte du verre : elle le traverse plus qu'elle ne le couvre.
            double breathe=1+.045*bass;
            D.PushTransform(new ScaleTransform(breathe,breathe,Width/2,Height/2));
            D.PushOpacity(.5);D.DrawImage(artwork,new Rect(0,0,Width,Height));D.Pop();D.Pop();
        }
        else D.DrawRectangle(FallbackGlass,null,new Rect(0,0,Width,Height));
        D.DrawRectangle(tintBrushes[LevelIndex(bass,tintBrushes)],null,new Rect(0,0,Width,Height));
        D.DrawRectangle(BottomWash,null,new Rect(0,0,Width,Height));
        Verse(new Point(32+text/2,y+78),Math.Max(200,text*.62),210);
        // Égaliseur bord à bord : barres pleines en bas, fondu vers le haut, crêtes qui retombent.
        int count=Math.Clamp((int)(Width/11),16,96);double slot=Width/(double)count;
        for(int i=0;i<count;i++)
        {
            double place=(i+.5)/count;float level=playing?MusicLevel(place):0;
            // Au repos les barres se rejoignent ; en lecture elles se séparent en peignes.
            double bar=Math.Max(2,slot*(level<.08?.86:.46)),height=2.5+span*level,x=i*slot+(slot-bar)/2;
            D.DrawRectangle(ribbonBrushes[LevelIndex(level,ribbonBrushes)],null,new Rect(x,baseline-height,bar,height));
            // Pointe chaude : la crête du niveau sature vers le blanc.
            if(level>.32)D.DrawRectangle(FlareBrushes[LevelIndex((level-.32)/.68,FlareBrushes)],null,new Rect(x,baseline-height,bar,Math.Min(7,height)));
            float crest=PeakAt(place);
            if(crest>.06)D.DrawRectangle(peakBrushes[LevelIndex(crest,peakBrushes)],null,new Rect(x,baseline-(2.5+span*crest)-3.5,bar,2.5));
        }
        D.DrawRectangle(glowBrushes[LevelIndex(bass,glowBrushes)],null,new Rect(0,Height-70,Width,70));
        // La barre de progression sert de socle à l'égaliseur et garde la recherche au clic.
        D.DrawRectangle(TrackRail,null,new Rect(16,baseline,Width-32,2.2));
        D.DrawRectangle(tintBrushes[^1],null,new Rect(0,baseline,Width,1));
        double.TryParse(Native.Read("mediaProgress"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var ratio);
        if(double.IsFinite(ratio))D.DrawRectangle(progressBrush,null,new Rect(16,baseline,Math.Clamp(ratio,0,1)*(Width-32),2.2));
        D.DrawRoundedRectangle(null,rimPens[LevelIndex(bass,rimPens)],new Rect(.75,.75,Width-1.5,Height-1.5),DockAppearance.PanelRadius-1,DockAppearance.PanelRadius-1);
        D.Pop();
        Text(Native.Read("source"),32,y+11,10,"#A99DBC",width:text);Hit("Source",32,y+7,text,28,()=>Native.DeskCommand("Source"));
        Button("MediaReserve","+",Width-62,y+9,38,29,Station.ShowReserve,13);
        Text(Native.Read("title"),32,y+33,16,width:text);
        Text(Native.Read("artist"),32,y+57,11,"#ABA2BB",width:text);
        if(Width>=620)Text(Native.Read("mediaTime"),32,y+76,10,"#ABA2BB",width:text);
        MediaButton("Previous","previous",Width-206,y+105,48,36,"canPrevious");
        MediaButton("Play",Native.Read("playing")=="1"?"pause":"play",Width-145,y+101,58,44,"canPlay");
        MediaButton("Next","next",Width-74,y+105,48,36,"canNext");
        Hit("Seek",16,baseline-6,Width-32,14,()=>{if(Native.Read("canSeek")=="1")Native.DeskCommand("Seek:"+Math.Clamp((Pointer.X-16)/(Width-32),0,1).ToString(System.Globalization.CultureInfo.InvariantCulture));});
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
            if(HasAgentTab(Native.Read($"project:{i}:path")))D.DrawEllipse(B("#8CFFFAFF"),null,new Point(x+cell-14,yy+15),3.4,3.4);
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
    // Heuristic badge: an open terminal tab carrying the project folder name.
    bool HasAgentTab(string path)
    {
        var name=System.IO.Path.GetFileName(path.TrimEnd('\\','/'));
        return name.Length>1&&Station.Terminal?.HasTabNamed(name)==true;
    }
    string ProjectName(string path,string name)=>string.Equals(path.TrimEnd('\\','/'),Station.Root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)?"Battlestation":name;
    static string ProjectStatus(ProjectSignal signal)=>string.Join(" · ",new[]{signal.Branch,signal.Changes.HasValue?(signal.Changes==0?"propre":$"{signal.Changes} modif."):null,Divergence(signal),CommitAge(signal.LastCommit)}.Where(x=>x is not null));
    static string? Divergence(ProjectSignal signal)=>signal.Ahead is null&&signal.Behind is null?null:$"↑{signal.Ahead??0} ↓{signal.Behind??0}";
    static string? CommitAge(DateTimeOffset? commit)
    {
        if(commit is not{} moment)return null;
        var minutes=(DateTimeOffset.Now-moment).TotalMinutes;
        return minutes<1?"à l'instant":minutes<60?$"{(int)minutes} min":minutes<1440?$"{(int)(minutes/60)} h":$"{(int)(minutes/1440)} j";
    }
    static string ProjectStatusColor(ProjectSignal signal)=>signal.Changes switch{0=>"#A6DFC0",>0=>"#F0C38B",_=>"#A89AB9"};
}
