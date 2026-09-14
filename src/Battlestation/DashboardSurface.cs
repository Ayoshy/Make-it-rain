using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Windows.Media.Effects;

namespace Battlestation;
internal sealed class DashboardSurface : Surface
{
    int drawer,detail,offset,quotaOffset;
    bool closing,manual,pending;
    double ch=218,mh=209,fromCh,fromMh,toCh,toMh,progress=1,fan=50,thermal=83;
    DateTime transition;
    readonly DispatcherTimer animation;
    readonly DrawingVisual details=new();
    readonly BlurEffect detailBlur=new(){Radius=0,RenderingBias=RenderingBias.Performance};
    protected override int VisualChildrenCount=>1;
    protected override Visual GetVisualChild(int index)=>index==0?details:throw new ArgumentOutOfRangeException(nameof(index));
    string? drag;
    public int Drawer=>drawer;
    readonly bool sensors;
    bool drawingDetails;
    internal double RequestedHeight {get;set;}
    internal bool DetailsAbove {get;set;}
    internal double SummaryInset=>DetailsAbove?RequestedHeight-(sensors?218:209):0;
    double DetailsShift=>sensors?(DetailsAbove?-443:0):(DetailsAbove?0:443);
    public Func<double,bool>? ResizeRequested {get;set;}
    protected override double OffsetY=>SummaryInset-(sensors?706:940);
    protected override double HitOffsetY=>OffsetY+(drawingDetails?DetailsShift:0);
    public DashboardSurface(Station station,bool hardware):base(station)
    {
        sensors=hardware;Width=779;Height=RequestedHeight=hardware?218:209;DetailsAbove=!hardware;AddVisualChild(details);details.Effect=detailBlur;
        animation=new DispatcherTimer(TimeSpan.FromMilliseconds(22),DispatcherPriority.Render,(_,_)=>Animate(),Dispatcher);animation.Stop();
    }
    public void Toggle(int next)
    {
        if(drawer!=next&&ResizeRequested?.Invoke(443)==false)return;
        int old=drawer;drawer=drawer==next?0:next;closing=drawer==0;detail=closing?old:drawer;
        if(drawer==2){pending=true;Station.Command("GpuRead");}
        fromCh=ch;fromMh=mh;toCh=drawer is 1 or 2?443:218;toMh=drawer is 3 or 4?443:209;
        transition=DateTime.UtcNow;progress=0;animation.Start();Refresh();
    }
    void Animate(){progress=Math.Clamp((DateTime.UtcNow-transition).TotalMilliseconds/308,0,1);var p=1-Math.Pow(1-progress,3);ch=fromCh+(toCh-fromCh)*p;mh=fromMh+(toMh-fromMh)*p;if(progress==1){animation.Stop();if(closing){detail=0;ResizeRequested?.Invoke(sensors?218:209);}}Refresh();}
    protected override void Paint()
    {
        int mode=closing&&progress<1?detail:drawer;
        bool sensor=sensors,codex=!sensors;
        if(sensor)Conrad();if(codex)Codex();
        double panelTop=(sensors?706:940)-(DetailsAbove?(sensors?ch-218:mh-209):0);
        Glass(sensors?6:7,0,panelTop,779,sensors?ch:mh);
        details.Transform=new TranslateTransform(0,OffsetY+DetailsShift);
        if(pending&&Station.M("controlAvailable")=="1")
        {
            fan=Station.N("fanTarget");thermal=Station.N("thermalTarget");manual=Station.M("fanAuto")=="0";pending=false;
        }
        var mainContext=D;var mainPointer=Pointer;
        using(var content=details.RenderOpen())
        {
            if(detail!=0)
            {
                double reveal=Math.Clamp((progress-.1)/.9,0,1);reveal=reveal*reveal*(3-2*reveal);if(closing)reveal=1-reveal;
                double height=detail<=2?ch-218:mh-209;
                double top=DetailsAbove?(detail<=2?1149:940)-height:detail<=2?924:706;
                details.Clip=new RectangleGeometry(new Rect(0,top,779,Math.Max(0,height)));
                details.Opacity=reveal*reveal;details.Offset=new Vector(0,(DetailsAbove?14:-14)*(1-reveal));detailBlur.Radius=12*(1-reveal);
                D=content;drawingDetails=true;Pointer=new Point(Pointer.X,Pointer.Y-DetailsShift);
                switch(detail){case 1:Cores();break;case 2:Cooling();break;case 3:Quotas();break;case 4:Models();break;}
            }
        }
        D=mainContext;drawingDetails=false;Pointer=mainPointer;
    }
    void Conrad()
    {
        const double y=706;DashboardPanel(y,ch,218);
        Text("●  CONRAD SENSOR",24,y+18,8.3,font:"Arial",bold:true,tracking:1.45);
        Text(Station.M("sensorStatus"),755,y+18,8,align:"right");
        for(int i=0;i<2;i++)
        {
            string key=i==0?"cpu":"gpu",color=i==0?Pink:Purple;double x=24+i*271;
            Text(key.ToUpperInvariant(),x,y+52,9.2,bold:true,tracking:1.7);
            Text(Station.M(key),x,y+70,36,color,"Bahnschrift",tracking:-1);
            Text(Station.M(key+"Load"),x+240,y+52,8,align:"right");Track(x,y+129,243,Station.N(key),color);
        }
        Text("VENTILATEUR",594,y+52,7,tracking:.94);Text(Station.M("fan"),594,y+72,16,font:"Bahnschrift");
        Text("PUISSANCE",594,y+107,7,tracking:.94);Text(Station.M("watts"),594,y+123,16,font:"Bahnschrift");
        SmallButton("Cores","6 CŒURS                 +",24,y+165,238,()=>Toggle(1));
        SmallButton("Cooling","REFROIDISSEMENT        ⚙",275,y+165,238,()=>Toggle(2));
        SmallButton("Heatwave",Station.M("heatwave")=="1"?"♨  CANICULE ACTIVE":"♨  CANICULE",526,y+165,229,()=>Station.Command("Heatwave"));
    }
    void SmallButton(string name,string label,double x,double y,double w,Action action,bool enabled=true)=>Button(name,label,x,y,w,32,action,8.5,"#E0B8EC",enabled,6);
    void Codex()
    {
        const double y=940;DashboardPanel(y,mh,209);
        Text("●  CODEX METER",24,y+18,8.3,font:"Arial",bold:true,tracking:1.45);
        Text(Station.M("codexStatus"),755,y+18,8,align:"right");
        Text(Station.M("quotaLabel"),24,y+51,9);Text(Station.M("remaining"),24,y+70,32,Purple,"Bahnschrift",tracking:-1);
        Text("RESTANT",153,y+100,7);Track(24,y+127,390,Station.N("remaining"),Purple);Text(Station.M("reset"),24,y+140,8);
        Text("TOKENS DU JOUR",448,y+51,8);Text(Station.M("today"),448,y+69,17,font:"Bahnschrift");
        Text("TOKENS CUMULÉS",448,y+108,8);Text(Station.M("total"),448,y+125,17,font:"Bahnschrift");
        SmallButton("Quotas","QUOTAS                 +",24,y+165,229,()=>Toggle(3));
        SmallButton("Models","MODÈLES                +",275,y+165,229,()=>Toggle(4));
        SmallButton("Refresh","ACTUALISER             ↻",526,y+165,229,()=>Station.Command("Refresh"));
    }
    void DashboardPanel(double summaryY,double height,double collapsedHeight)
    {
        double top=summaryY-(DetailsAbove?height-collapsedHeight:0);
        // The native wallpaper sits below sibling WPF windows. A dense local
        // material prevents their labels (or an underlying terminal) leaking
        // through the transient drawer; the collapsed widget stays clear.
        if(height>collapsedHeight+.1)
        {
            var material=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#FC34233E"),(Color)ColorConverter.ConvertFromString("#FE21152E"),65);
            D.DrawRoundedRectangle(material,new Pen(B("#52DAC9EC"),1),new Rect(0,top,779,height),24,24);
        }
        else Panel(0,top,779,height);
    }
    void Cores()
    {
        const double y=706;Text("INTEL CORE i5-9600KF",24,y+236,10.5,bold:true,tracking:.5);
        Text("MAX  "+Station.M("hottest"),750,y+233,14,"#E4ABFA","Bahnschrift","right");
        for(int i=0;i<6;i++)
        {
            double x=24+i%3*249,yy=y+273+i/3*65;Box(x,yy,232,56,"#186E4093","#41B47FDB",10);
            Text("CŒUR "+(i+1),x+14,yy+20,9,"#CCB1DC",bold:true,tracking:.8);
            Text(Station.M("core"+i),x+215,yy+10,22,"#F0D5FD","Bahnschrift","right");
        }
    }
    void Cooling()
    {
        const double y=706;bool available=Station.M("controlAvailable")=="1";
        Text("RTX 2060 SUPER",24,y+232,11,bold:true,tracking:.6);Text("Mode du ventilateur",24,y+261,11);
        SmallButton("FanMode",manual?"MANUEL":"AUTO",613,y+253,142,()=>manual=!manual,available);
        Text("VITESSE",24,y+293,8.5,bold:true,tracking:.8);Text(manual?fan+" %":"AUTO",750,y+286,15,font:"Bahnschrift",align:"right");
        Text("CIBLE THERMIQUE",24,y+345,8.5,bold:true,tracking:.8);Text(double.IsFinite(thermal)?thermal+"°":"—",750,y+338,15,font:"Bahnschrift",align:"right");
        Slider("fan",1021,fan,Station.N("fanMin"),Station.N("fanMax"),available&&manual);
        Slider("thermal",1074,thermal,Station.N("thermalMin"),Station.N("thermalMax"),available);
        SmallButton("Apply","APPLIQUER AU GPU",24,1097,724,()=>Station.Command($"Apply:{(manual?1:0)}:{fan:0}:{thermal:0}"),available);
        Text(Station.Error!=""?Station.Error:Station.M("sensorError"),24,1131,8,"#FF95C1",width:725);
    }
    void Slider(string name,double y,double value,double min,double max,bool enabled)
    {
        Box(24,y,725,10,"#C85C3570",radius:5);double ratio=double.IsFinite(min+max+value)&&max>min?Math.Clamp((value-min)/(max-min),0,1):0;
        D.DrawEllipse(B(enabled?Purple:Muted),null,new Point(24+ratio*725,y+5),8,8);
        if(enabled)Hit("Slider:"+name,16,y-8,741,26,()=>SetSlider(name,Pointer.X));
    }
    void SetSlider(string name,double x){double min=Station.N(name=="fan"?"fanMin":"thermalMin"),max=Station.N(name=="fan"?"fanMax":"thermalMax");if(!double.IsFinite(min+max)||max<=min)return;double value=Math.Round(min+Math.Clamp((x-24)/725,0,1)*(max-min));if(name=="fan")fan=value;else thermal=value;Refresh();}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){if(drawer==2&&Station.M("controlAvailable")=="1"){var p=e.GetPosition(this);p.Y-=OffsetY+DetailsShift;if(p.X>=16&&p.X<=757){if(p.Y>=1013&&p.Y<=1039&&manual)drag="fan";if(p.Y>=1066&&p.Y<=1092)drag="thermal";}if(drag is not null){CaptureMouse();SetSlider(drag,p.X);}}base.OnMouseLeftButtonDown(e);}
    protected override void OnPointer(MouseEventArgs e){if(drag is not null&&e.LeftButton==MouseButtonState.Pressed)SetSlider(drag,Pointer.X);}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){if(drag is not null){drag=null;ReleaseMouseCapture();e.Handled=true;}else base.OnMouseLeftButtonUp(e);}
    List<(string Name,string Remaining,string Duration,string Reset)> QuotaRows()
    {
        var rows=new List<(string,string,string,string)>();string name="";
        foreach(var line in Station.M("quotaDetails").Split('\n'))
        {
            if(line.Length==0){name="";continue;}if(name==""){name=line;continue;}
            foreach(var item in line.Split('·')){var parts=item.Split(" / ");if(parts.Length==3)rows.Add((name,parts[0].Trim(),parts[1].Trim(),parts[2].Trim()));}
        }
        return rows;
    }
    void Quotas()
    {
        var rows=QuotaRows();quotaOffset=Math.Clamp(quotaOffset,0,Math.Max(0,rows.Count-3));
        if(rows.Count==0)Text("Quotas indisponibles",24,720,10);
        for(int i=0;i<3&&quotaOffset+i<rows.Count;i++)
        {
            var row=rows[quotaOffset+i];double y=720+i*62;Box(24,y,731,54,"#12804DAE","#37B77DDF",10);
            Text(row.Name=="codex"?"Codex":row.Name,38,y+5,11,bold:true);Text("Reset  "+row.Reset,38,y+27,8.5,"#B8A6CD");
            Text(row.Duration.ToUpperInvariant(),588,y+9,9,"#D8B8EB",align:"right",bold:true,tracking:.5);
            var match=Regex.Match(row.Remaining,@"(\d+)%");double percent=match.Success?double.Parse(match.Groups[1].Value):double.NaN;
            Text(match.Success?percent+"%":"N/D",737,y+4,20,"#CD9CFF","Bahnschrift","right");Track(38,y+48,699,percent,"#BEC083F7",2);
        }
        Text(Station.M("credits"),24,917,8.5,"#B5A1CC");
    }
    void Models()
    {
        const double y=706;Text("COMPTE · TOKENS CUMULÉS",24,y+12,7.5,"#B8A4CD",bold:true,tracking:.7);Text(Station.M("total"),24,y+27,17,font:"Bahnschrift");
        Text("ÉQUIVALENT API LOCAL",750,y+12,7.5,"#B8A4CD",align:"right",bold:true,tracking:.7);Text(Station.M("totalCost"),750,y+27,17,"#96E7B2","Bahnschrift","right");
        Text("MODÈLE",38,y+61,7.5,Muted,bold:true);Text("EFFORT",422,y+61,7.5,Muted,align:"center",bold:true);Text("TOKENS",565,y+61,7.5,Muted,align:"right",bold:true);Text("ESTIMÉ",735,y+61,7.5,Muted,align:"right",bold:true);
        for(int i=0;i<4;i++)
        {
            double yy=y+81+i*30;Box(24,yy,731,28,i%2==0?"#0EB483D8":"#04B483D8",radius:6);
            var pieces=Station.M($"model:{offset+i}:name").Split("  ");string name=pieces[0]=="unknown"?"Modèle inconnu":pieces[0],effort=pieces.Length>1&&pieces[1]!="unspecified"?pieces[1].ToUpperInvariant():"";
            Text(name,38,yy+4,10.5,bold:true,width:322);Text(effort,422,yy+7,8,"#C79FE2",align:"center",bold:true,tracking:.6);
            Text(Station.M($"model:{offset+i}:tokens"),565,yy+3,12,"#E6DBF2","Bahnschrift","right");Text(Station.M($"model:{offset+i}:cost"),735,yy+3,12,"#96E7B2","Bahnschrift","right");
        }
        Text("Historique local · estimation partielle, pas une facture · molette pour parcourir",24,y+211,7.5,"#A792BD");
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        int delta=e.Delta>0?-1:1;
        if(drawer==4)offset=Math.Clamp(offset+delta,0,Math.Max(0,(int)Station.N("modelsCount")-4));
        if(drawer==3)quotaOffset=Math.Clamp(quotaOffset+delta,0,Math.Max(0,QuotaRows().Count-3));
        Refresh();e.Handled=true;
    }
}
