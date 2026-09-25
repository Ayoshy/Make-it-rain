using System.Windows;
using System.Windows.Media;

namespace Battlestation;

// League of Legends telemetry, read only while a game process is running and the
// dock is exposed. Nothing is written back and no other game data is touched.
// The drawing follows the same events: a kill, a death, a multi-kill and an
// objective each carry their own short effect, and nothing animates a state the
// game did not announce.
internal sealed class LolSurface : Surface,IDisposable
{
    const string Blue="#84D8FF",Red="#FF9DBB",Error="#F4B7CA",Gold="#F2C572";
    readonly LolTelemetry telemetry=new();
    readonly LolArtwork artwork;
    Task artworkLoad=Task.CompletedTask;
    LolSnapshot rendered=LolSnapshot.None;
    bool active,looping;
    TimeSpan lastFrame;
    double seconds,killPing,multiFlash,deathFlash,objectiveFlash,streakPing,respawnSpan,pulseFrame;
    int objectiveSide;
    string headline="";
    Point crest=new(54,72);
    double crestRadius=28;
    readonly IncomeVisual incomeChart=new();
    readonly DrawingVisual eventLayer=new();
    static readonly Pen chartGlow=FrozenPen("#24F2C572",10),chartStroke=FrozenPen(Gold,2.6),chartHighlight=FrozenPen("#CFFFF0C8",.9);
    static readonly Brush chartFill=IncomeFill();
    static Brush IncomeFill()
    {
        var brush=new LinearGradientBrush();brush.StartPoint=new(0,0);brush.EndPoint=new(0,1);
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString("#46F2C572"),0));
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString("#04F2C572"),1));brush.Freeze();return brush;
    }
    static Pen FrozenPen(string color,double width){var pen=new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),width);pen.Freeze();return pen;}
    Rect blueBand,redBand;
    internal LolSurface(Station station):base(station,16){Width=700;Height=220;artwork=new(station.Assets);AddVisualChild(incomeChart);AddVisualChild(eventLayer);}
    protected override int VisualChildrenCount=>2;
    protected override Visual GetVisualChild(int index)=>index switch{0=>incomeChart,1=>eventLayer,_=>throw new ArgumentOutOfRangeException(nameof(index))};
    internal override void SetDisplayed(bool value)
    {
        base.SetDisplayed(value);
        if(!value){incomeChart.SetVisible(false);using(eventLayer.RenderOpen()){}}
    }
    internal void SetActive(bool value)
    {
        if(active==value)return;
        active=value;telemetry.SetActive(value);incomeChart.Active=value;lastFrame=default;
        if(value){Poll();if(rendered.State==LolState.Live)Loop(true);}
        else{Loop(false);killPing=multiFlash=deathFlash=objectiveFlash=streakPing=0;}
    }
    // Called by the desktop timer: the worker fetches, the dock only repaints on a new reading.
    internal void Poll()
    {
        var next=telemetry.Snapshot;
        if(next==rendered)return;
        Update(next);Refresh();
        if(active&&next.State==LolState.Live)Loop(true);
    }
    // Hors partie, rien ne bouge : la boucle d'images ne tient pas WPF éveillé.
    void Loop(bool on)
    {
        if(looping==on)return;looping=on;lastFrame=default;
        if(on)CompositionTarget.Rendering+=Frame;else CompositionTarget.Rendering-=Frame;
    }
    // A reading arrives once a second; the effects fade on their own in between,
    // and a dock that shows nothing new does not repaint.
    void Frame(object? sender,EventArgs args)
    {
        if(args is not RenderingEventArgs frame||frame.RenderingTime==lastFrame)return;
        double elapsed=lastFrame==default?1/30d:(frame.RenderingTime-lastFrame).TotalSeconds;
        if(elapsed<1/30d-.001)return;
        lastFrame=frame.RenderingTime;Tick(Math.Min(.2,elapsed));
    }
    // The tests advance the effects through the same path as the render loop.
    internal void Tick(double elapsed)
    {
        seconds+=elapsed;
        bool changed=telemetry.Snapshot!=rendered;
        if(changed)Update(telemetry.Snapshot);
        incomeChart.Tick(elapsed);
        killPing*=Math.Exp(-elapsed*2.4);multiFlash*=Math.Exp(-elapsed*.85);deathFlash*=Math.Exp(-elapsed*1.6);
        objectiveFlash*=Math.Exp(-elapsed*1.6);streakPing*=Math.Exp(-elapsed*2.2);
        bool effect=killPing>.01||multiFlash>.01||deathFlash>.01||objectiveFlash>.01||streakPing>.01;
        if(rendered.State!=LolState.Live&&!effect)Loop(false);
        // An effect deserves every frame it lasts; a resting dock only needs the
        // breathing dot. Only the child chart redraws at 30 Hz between readings.
        bool breathing=rendered.State==LolState.Live&&seconds-pulseFrame>=.2;
        if(!(changed||effect||breathing))return;
        if(breathing)pulseFrame=seconds;
        Refresh();
    }
    // A reading only fires an effect when it really carries a new event. The first
    // live reading of a session never fires one: keeping the dock in front of a
    // game already running does not flash the strikes it missed.
    void Update(LolSnapshot next)
    {
        var previous=rendered;rendered=next;
        if(next.State==LolState.Live)
        {
            if(artworkLoad.IsCompleted)artworkLoad=artwork.Load((next.Players??[]).Select(p=>p.Id).Append(next.ChampionId));
        }
        if(previous.State!=LolState.Live||next.State!=LolState.Live||next.GameTime<previous.GameTime||next.ChampionId!=previous.ChampionId)
        {killPing=multiFlash=deathFlash=objectiveFlash=streakPing=0;respawnSpan=Math.Max(1,next.Respawn??1);return;}
        if(next.LastKillAt is{} kill&&kill!=previous.LastKillAt){killPing=1;streakPing=1;}
        if(next.LastDeathAt is{} death&&death!=previous.LastDeathAt){deathFlash=1;respawnSpan=Math.Max(1,next.Respawn??1);}
        if(next.MultiKill.Length>0&&next.MultiKillAt!=previous.MultiKillAt){headline=next.MultiKill;multiFlash=1;}
        else if(next.FirstBlood&&!previous.FirstBlood){headline="PREMIER SANG";multiFlash=1;}
        else if(next.AceAt is{} ace&&ace!=previous.AceAt){headline="ACE";multiFlash=1;}
        bool blue=Gained(previous.Blue,next.Blue),red=Gained(previous.Red,next.Red);
        if(blue||red){objectiveFlash=1;objectiveSide=red&&!blue?1:0;}
    }
    static bool Gained(LolObjective before,LolObjective after)=>after.Dragons>before.Dragons||after.Barons>before.Barons||after.Turrets>before.Turrets||after.Inhibitors>before.Inhibitors;
    internal object Inspect()
    {
        var state=telemetry.Snapshot;
        return new{state=state.State.ToString(),animating=active,champion=state.Champion,level=state.Level,gameTime=Math.Round(state.GameTime,1),
            kills=state.Kills,deaths=state.Deaths,assists=state.Assists,creepScore=state.CreepScore,gold=state.Gold,
            championId=state.ChampionId,portrait=artwork.Get(state.ChampionId)!=null,goldPerSecond=state.GoldPerSecond,goldWindow=state.GoldWindow,
            participation=state.Participation,csPerMinute=state.CsPerMinute,kda=state.Kda,
            chartFrames=incomeChart.Frames,chartTime=incomeChart.ViewTime,chartOpacity=incomeChart.Opacity,
            respawn=state.Respawn,dragonIn=state.DragonIn,baronIn=state.BaronIn,blue=state.Blue,red=state.Red,capturedAt=state.CapturedAt,error=state.Error,
            map=state.MapNumber,mode=state.Mode,mapName=state.Map,aram=state.Aram,team=state.Team,streak=state.Streak,multiKill=state.MultiKill,
            firstBlood=state.FirstBlood,aceAt=state.AceAt,lastKillAt=state.LastKillAt,lastDeathAt=state.LastDeathAt,events=state.Events??[]};
    }
    protected override void Paint()
    {
        var state=rendered;
        incomeChart.SetVisible(state.State==LolState.Live&&Width>=620&&Height>=380);
        Header("LOL");
        if(state.State!=LolState.Live){using(eventLayer.RenderOpen()){}Waiting(state);return;}
        string accent=state.Team=="CHAOS"?Red:Blue;
        // Three layouts for the same readings: the default wide card, the tall one
        // the game scene uses, and the dense one at the declared minimum size.
        bool compact=Width<620||Height<200;
        if(compact)Compact(state,accent);
        else if(Height>=380)Dashboard(state,accent);
        else Wide(state,accent);
        var canvas=D;
        using(var context=eventLayer.RenderOpen()){D=context;Effects(state);}
        D=canvas;
    }
    void Dashboard(LolSnapshot state,string accent)
    {
        double left=24,right=Width-24,gap=18,heroWidth=Math.Clamp(Width*.29,218,410),x=left+heroWidth+gap;
        bool expansive=Width>=1050;
        double top=48,bottom=Height-(expansive?92:70),bodyHeight=bottom-top,centerX=left+heroWidth/2;
        Live(Pill(70,15,Mode(state),Ink).Right+14,15,accent,true);
        Text(Clock(state.GameTime),right,4,26,Ink,DockAppearance.NumberFont,align:"right");
        Box(left,top,heroWidth,bodyHeight,"#222D203B","#32DACDEC",18);
        var portrait=artwork.Get(state.ChampionId);
        if(portrait!=null)
        {
            D.PushClip(new RectangleGeometry(new Rect(left,top,heroWidth,bodyHeight),18,18));
            double size=Math.Max(heroWidth,bodyHeight);
            D.PushOpacity(.13);D.DrawImage(portrait,new Rect(centerX-size/2,top+(bodyHeight-size)/2,size,size));D.Pop();D.Pop();
        }
        double radius=Math.Clamp(bodyHeight*.19,34,65);
        crest=new Point(centerX,top+radius+22);
        Crest(crest,radius,state,accent);
        double nameY=crest.Y+radius+10;
        Text(Champion(state),centerX,nameY,Math.Clamp(heroWidth/17,15,22),Ink,align:"center",bold:true,width:heroWidth-24);
        Text($"NIVEAU {state.Level}   ·   {MapLabel(state)}",centerX,nameY+34,8,Muted,align:"center",width:heroWidth-24);
        if(bodyHeight>=300)
        {
            Text(GoldLabel(state.Gold),centerX,bottom-84,expansive?29:23,Gold,DockAppearance.NumberFont,align:"center");
            Text("OR DISPONIBLE",centerX,bottom-39,8,Muted,align:"center",tracking:1.5);
        }
        else Text(GoldLabel(state.Gold),left+heroWidth/2,bottom-35,15,Gold,DockAppearance.NumberFont,align:"center");
        if(state.Streak>=2)Pill(left+12,top+12,$"SÉRIE {state.Streak}",Gold,null,8);
        double available=right-x;
        int columns=available>=560?4:2;
        double scoreHeight=columns==4?Math.Clamp(bodyHeight*.24,78,100):112;
        Tiles(state,x,top,right,top+scoreHeight,columns);
        double metricY=top+scoreHeight+12,metricH=Math.Clamp(bodyHeight*.22,58,86);
        double cell=(available-16)/3;
        Metric(x,metricY,cell,metricH,state.Participation is{} kp?$"{kp:0} %":"—","PARTICIPATION",accent);
        Metric(x+cell+8,metricY,cell,metricH,state.Kda.ToString("0.00",French),"KDA",Pink);
        Metric(x+2*(cell+8),metricY,cell,metricH,state.CsPerMinute.ToString("0.0",French),"CS / MIN",Purple);
        double incomeY=metricY+metricH+12,incomeH=bottom-incomeY;
        if(incomeH>=65)Income(state,new Rect(x,incomeY,available,incomeH));
        // The footer has distinct team ends and a shared middle; no divider cuts through badges.
        Line(24,Height-(expansive?80:56),right,Height-(expansive?80:56),"#2AD1B5DB");
        blueBand=Band(24,Height-(expansive?50:43),false,state.Blue,accent==Blue,false);
        redBand=Band(right,Height-(expansive?50:43),true,state.Red,accent==Red,false);
        if(expansive)Roster(state,Width/2,Height-42);
        if(!state.Aram&&Width>=700)Timers(state,Height-(expansive?87:63),true);
    }
    void Metric(double x,double y,double width,double height,string value,string label,string tint)
    {
        Box(x,y,width,height,"#142D203B","#22DACDEC",12);
        Line(x+12,y+12,x+30,y+12,tint,2);
        double points=width>=230&&height>=76?30:height>=70?22:17;
        Text(value,x+12,y+15,points,tint,DockAppearance.NumberFont);
        Text(label,x+12,y+height-20,width>=230?9:7,Muted,width:width-20);
    }
    void Income(LolSnapshot state,Rect area)
    {
        Box(area.X,area.Y,area.Width,area.Height,"#182D203B","#35F2C572",12);
        double top=area.Y+12;
        Text("OR / S ESTIMÉ",area.X+14,top,8,Gold,tracking:1);
        string value=state.GoldPerSecond is{} rate?rate.ToString("0.0",French):"—";
        Text(value,area.X+14,top+20,area.Height>=95?30:22,Gold,DockAppearance.NumberFont);
        Text(state.GoldPerSecond is null?"Mesure en cours…":$"{state.GoldWindow:0} s observées",area.Right-14,top,8,Muted,align:"right");
        double chartX=area.X+Math.Min(156,area.Width*.30),chartY=top+34,chartWidth=area.Right-30-chartX,chartHeight=area.Bottom-24-chartY;
        if(chartHeight<12)return;
        var plot=new Rect(chartX,chartY,chartWidth,chartHeight);
        incomeChart.Update(state,plot);
        for(int i=0;i<3;i++)Line(chartX,chartY+chartHeight*i/2,area.Right-24,chartY+chartHeight*i/2,"#12F2C572");
        if(area.Height>=100)
        {
            Text($"{incomeChart.Ceiling:0}",area.Right-9,chartY-5,7,"#A6D6BF91",align:"right");
            Text("0",area.Right-9,chartY+chartHeight-6,7,"#A6D6BF91",align:"right");
        }
    }
    // Monotone cubic interpolation rounds the joins without adding peaks or
    // changing any measured point. The child visual owns temporal scrolling.
    internal static StreamGeometry IncomePath(double[] values,Rect plot,double ceiling,bool fill)
        =>IncomePath(values,Enumerable.Range(0,values.Length).Select(i=>(double)i).ToArray(),plot,ceiling,fill);
    static StreamGeometry IncomePath(double[] values,double[] times,Rect plot,double ceiling,bool fill)
    {
        var path=new StreamGeometry();
        if(values.Length<2)return path;
        double span=times[^1]-times[0];
        var slope=new double[values.Length];
        slope[0]=(values[1]-values[0])/(times[1]-times[0]);slope[^1]=(values[^1]-values[^2])/(times[^1]-times[^2]);
        for(int i=1;i<values.Length-1;i++)
        {
            double a=(values[i]-values[i-1])/(times[i]-times[i-1]),b=(values[i+1]-values[i])/(times[i+1]-times[i]);
            slope[i]=a*b<=0?0:2*a*b/(a+b);
        }
        Point at(double time,double value)=>new(plot.X+(time-times[0])*plot.Width/span,plot.Bottom-plot.Height*value/ceiling);
        using(var dc=path.Open())
        {
            dc.BeginFigure(at(times[0],values[0]),fill,fill);
            for(int i=0;i<values.Length-1;i++)
            {
                double dt=times[i+1]-times[i];
                dc.BezierTo(at(times[i]+dt/3,values[i]+slope[i]*dt/3),at(times[i]+2*dt/3,values[i+1]-slope[i+1]*dt/3),at(times[i+1],values[i+1]),true,false);
            }
            if(fill){dc.LineTo(new Point(plot.Right,plot.Bottom),true,false);dc.LineTo(new Point(plot.Left,plot.Bottom),true,false);}
        }
        path.Freeze();return path;
    }
    // Same time-based motion as the network dock, isolated in its own visual:
    // the portrait, labels and glass are not repainted for each chart frame.
    internal sealed class IncomeVisual : DrawingVisual
    {
        internal bool Active {get;set;}
        internal double ViewTime {get;private set;}
        internal double Ceiling {get;private set;}=10;
        internal long Frames {get;private set;}
        bool visible;
        Rect plot;
        double[] values=[],times=[];
        double targetCeiling=10,receivedAt,clock;
        LolSnapshot? source;
        readonly Pen glow=chartGlow,stroke=chartStroke,highlight=chartHighlight;
        internal void SetVisible(bool value)
        {
            if(visible==value)return;visible=value;
            if(value)Draw();else using(RenderOpen()){}
        }
        internal void Update(LolSnapshot state,Rect area)
        {
            bool resized=plot!=area;plot=area;
            if(ReferenceEquals(source,state)){if(resized)Draw();return;}
            bool reset=source is null||values.Length<2||state.ChampionId!=source.ChampionId||state.GameTime<source.GameTime||state.GoldWindow<source.GoldWindow-2;
            source=state;receivedAt=clock;
            var previousValues=values;var previousTimes=times;
            values=state.GoldTrend??[];
            times=state.GoldTrendTimes is{} stamps&&stamps.Length==values.Length?stamps:
                Enumerable.Range(0,values.Length).Select(i=>state.GameTime-state.GoldWindow*(values.Length-1-i)/Math.Max(1,values.Length)).ToArray();
            if(!reset&&times.Length>0)
            {
                var tail=Enumerable.Range(0,previousTimes.Length).Where(i=>previousTimes[i]>=ViewTime-61&&previousTimes[i]<times[0]).ToArray();
                if(tail.Length>0){times=tail.Select(i=>previousTimes[i]).Concat(times).ToArray();values=tail.Select(i=>previousValues[i]).Concat(values).ToArray();}
            }
            targetCeiling=Math.Max(10,Math.Ceiling(values.DefaultIfEmpty().Max()*1.1/10)*10);
            if(reset){ViewTime=state.GameTime-1.5;Ceiling=targetCeiling;}
            if(!Active){ViewTime=state.GameTime;Ceiling=targetCeiling;}
            Draw();
        }
        internal void Tick(double elapsed)
        {
            if(!Active||!visible||source is null||values.Length<2)return;
            clock+=elapsed;
            // A small display delay lets the next segment enter from the right
            // using already measured values. Never extrapolate an unseen gain.
            double goal=Math.Min(source.GameTime,source.GameTime+clock-receivedAt-1.5);
            double error=goal-ViewTime;
            ViewTime=Math.Min(source.GameTime,ViewTime+elapsed*Math.Clamp(1+error*.5,.25,2));
            Ceiling+=(targetCeiling-Ceiling)*(1-Math.Exp(-elapsed*3));
            Draw();
        }
        void Draw()
        {
            if(!visible)return;
            using var dc=RenderOpen();Frames++;
            if(values.Length<2||plot.IsEmpty||plot.Width<=0||plot.Height<=0)return;
            var interval=new Rect(plot.Right+(times[0]-ViewTime)*plot.Width/60,plot.Y,(times[^1]-times[0])*plot.Width/60,plot.Height);
            var curve=IncomePath(values,times,interval,Ceiling,false);
            dc.PushClip(new RectangleGeometry(new Rect(plot.X,plot.Y-8,plot.Width,plot.Height+16)));
            dc.DrawGeometry(chartFill,null,IncomePath(values,times,interval,Ceiling,true));
            dc.DrawGeometry(null,glow,curve);dc.DrawGeometry(null,stroke,curve);dc.DrawGeometry(null,highlight,curve);
            dc.Pop();
        }
    }
    void Roster(LolSnapshot state,double center,double y)
    {
        var players=state.Players??[];
        double size=Math.Clamp((Width-840)/16,30,46),step=size+12,scoreGap=64;
        foreach(string side in new[]{"ORDER","CHAOS"})
        {
            var team=players.Where(p=>p.Team==side).Take(5).ToArray();
            double start=side=="ORDER"?center-scoreGap-team.Length*step+12:center+scoreGap;
            for(int i=0;i<team.Length;i++)
            {
                var p=team[i];var image=artwork.Get(p.Id);var box=new Rect(start+i*step,y-size/2,size,size);
                Box(box.X-2,box.Y-2,size+4,size+4,"#222D203B",side=="ORDER"?Blue:Red,9);
                if(image!=null){D.PushOpacity(p.Dead?.25:1);D.PushClip(new RectangleGeometry(box,7,7));D.DrawImage(image,box);D.Pop();D.Pop();}
                if(p.Dead)Line(box.X+5,box.Bottom-5,box.Right-5,box.Y+5,Error,1.5);
            }
        }
        Text($"{players.Where(p=>p.Team=="ORDER").Sum(p=>p.Kills)} : {players.Where(p=>p.Team=="CHAOS").Sum(p=>p.Kills)}",center,y-17,22,Ink,DockAppearance.NumberFont,align:"center");
    }
    static string MapLabel(LolSnapshot state)=>state.Aram?"ARAM":state.MapNumber==11?"FAILLE":state.Map;
    void Wide(LolSnapshot state,string accent)
    {
        double split=Math.Max(300,Width*.43);
        if(Mode(state).Length>0)Live(Pill(70,15,Mode(state),Ink).Right+14,15,accent,true);
        Text(Clock(state.GameTime),Width-24,4,26,Ink,DockAppearance.NumberFont,align:"right");
        crest=new Point(54,72);
        Crest(crest,28,state,accent);
        Text(Champion(state),94,40,20,"#F4EDF9",bold:true,width:split-106);
        Identity(state,accent,94,74,split-12,false,false);
        Text($"KDA {state.Kda.ToString("0.0",French)}  ·  {state.CsPerMinute.ToString("0.0",French)} CS/min",24,Height-94,9,accent);
        Text($"≈ {(state.GoldPerSecond is{} rate?rate.ToString("0.0",French):"—")} or/s  ·  {(state.Participation is{} kp?$"{kp:0} % part.":"— part.")}",24,Height-78,8,Gold);
        Tiles(state,split,42,Width-24,Height-72,2);
        Timers(state,Height-62,false);
        blueBand=Band(24,Height-36,false,state.Blue,accent==Blue,false);
        redBand=Band(Width-24,Height-36,true,state.Red,accent==Red,false);
    }
    void Compact(LolSnapshot state,string accent)
    {
        if(Mode(state).Length>0)Live(Pill(66,15,Mode(state),Ink,null,7.5).Right+12,15,accent,false);
        Text(Clock(state.GameTime),Width-18,4,21,Ink,DockAppearance.NumberFont,align:"right");
        crest=new Point(42,58);
        Crest(crest,19,state,accent);
        Text(Champion(state),68,40,15,"#F4EDF9",bold:true,width:Width-86);
        Identity(state,accent,68,62,Width-18,true,true);
        Tiles(state,24,Height-104,Width-24,Height-64,4);
        Timers(state,Height-60,true);
        blueBand=Band(24,Height-36,false,state.Blue,accent==Blue,true);
        redBand=Band(Width-24,Height-36,true,state.Red,accent==Red,true);
    }
    static string Champion(LolSnapshot state)=>state.Champion.Length==0?"Champion inconnu":state.Champion;
    // Level, gold and the streak share one line: they are three readings of the
    // same player, not three blocks. The map name follows on its own row.
    void Identity(LolSnapshot state,string accent,double x,double y,double right,bool besideStreak,bool besideMap)
    {
        Text($"Nv {state.Level}",x,y,10,Muted);
        double cursor=x+Paragraph($"Nv {state.Level}",10,Ink,400).Width+9;
        if(state.Gold>0)
        {
            Text("·",cursor,y,10,Muted);cursor+=9;
            LolGlyphs.Draw(D,LolGlyphs.Glyph.Coin,cursor,y-1,13,Gold);
            cursor+=17;
            Text(GoldLabel(state.Gold),cursor,y,10,"#F0DFC4");
            cursor+=Paragraph(GoldLabel(state.Gold),10,Ink,400).Width+11;
        }
        bool beside=false;
        if(state.Streak>=2)
        {
            // Three kills without dying is the game's own threshold for a streak.
            string tint=state.Streak>=3?Gold:accent,badge=$"SÉRIE {state.Streak}";
            beside=besideStreak&&cursor+PillWidth(badge,8.5,null)<=right;
            var pill=beside?Pill(cursor,y-5,badge,tint,null,8.5):Pill(x,y+26,badge,tint,null,8.5);
            if(streakPing>.01)Pulse(pill,tint,streakPing);
        }
        if(state.Map.Length==0)return;
        if(besideMap)Text(MapLabel(state),right,y,8.5,Muted,align:"right",width:Math.Max(0,right-x-100));
        else if(state.Streak>=2&&!beside)Text(MapLabel(state),right,y+26,8,Muted,align:"right",width:Math.Max(0,right-x-90));
        else Text(MapLabel(state),x,y+26,8.5,Muted,width:Math.Max(0,right-x));
    }
    // The four counters the game is read for, each with its own drawing so the
    // block is scanned instead of parsed.
    void Tiles(LolSnapshot state,double left,double top,double right,double bottom,int columns)
    {
        var rows=new (LolGlyphs.Glyph Glyph,int Value,string Label,string Tint)[]{
            (LolGlyphs.Glyph.Sword,state.Kills,"ÉLIMINATIONS",DesktopTheme.Current.Active[0]),
            (LolGlyphs.Glyph.Skull,state.Deaths,"MORTS",state.Deaths>0?Error:Muted),
            (LolGlyphs.Glyph.Spark,state.Assists,"AIDES",DesktopTheme.Current.Active[3]),
            (LolGlyphs.Glyph.Minion,state.CreepScore,"CS",DesktopTheme.Current.Active[1])};
        int lines=(int)Math.Ceiling(rows.Length/(double)columns);
        double gap=columns>2?6:8,width=(right-left-(columns-1)*gap)/columns;
        // The block keeps a comfortable tile height and stays centred in the space
        // it was given instead of stretching to the whole card.
        double height=Math.Min(110,(bottom-top-(lines-1)*gap)/lines);
        double start=top+Math.Max(0,(bottom-top-lines*height-(lines-1)*gap)/2);
        for(int i=0;i<rows.Length;i++)
            Tile(left+i%columns*(width+gap),start+i/columns*(height+gap),width,height,rows[i].Glyph,rows[i].Value,columns>2&&height<64?"":rows[i].Label,rows[i].Tint);
    }
    void Tile(double x,double y,double w,double h,LolGlyphs.Glyph glyph,int value,string label,string tint)
    {
        double inner=Math.Clamp(Math.Min(h/55,w/135),1,1.8);
        Box(x,y,w,h,"#102D203B","#22DACDEC",12);
        Box(x+2*inner,y+(h-26*inner)/2,2.5*inner,26*inner,tint,radius:1.5);
        LolGlyphs.Draw(D,glyph,x+16*inner,y+(h-20*inner)/2-(h>=64?7:0),20*inner,tint);
        Text(value.ToString(),x+47*inner,y+(h-27*inner)/2-(h>=64?7:0),19*inner,Ink,DockAppearance.NumberFont,bold:true);
        if(label.Length>0)
        {
            if(h>=64)Text(label,x+16,y+h-22,h>=82?9:7,Muted,width:w-28);
            else if(w>=190)Text(label,x+w-12,y+h/2-4*inner,7*inner,Muted,DockAppearance.TextFont,"right");
        }
    }
    void Crest(Point center,double radius,LolSnapshot state,string accent)
    {
        crestRadius=radius;
        D.DrawEllipse(DesktopTheme.Brush("#16"+accent[1..]),null,center,radius+7,radius+7);
        D.DrawEllipse(DesktopTheme.Brush("#28"+accent[1..]),null,center,radius+3,radius+3);
        LolGlyphs.Arc(D,center,radius-1,360,"#2AD1B5DB",2.4);
        LolGlyphs.Arc(D,center,radius-1,360*Math.Clamp(state.Level/18d,0,1),accent,3.2);
        D.DrawEllipse(DesktopTheme.Brush("#102D203B"),null,center,radius-5,radius-5);
        if(artwork.Get(state.ChampionId) is{} image)
        {
            D.PushClip(new EllipseGeometry(center,radius-5,radius-5));
            D.DrawImage(image,new Rect(center.X-radius+5,center.Y-radius+5,(radius-5)*2,(radius-5)*2));D.Pop();
            return;
        }
        D.DrawEllipse(DesktopTheme.Brush("#12F6EFFF"),null,new Point(center.X-radius*.28,center.Y-radius*.4),radius*.42,radius*.26);
        Text(Monogram(state.Champion),center.X,center.Y-radius*.64,radius*.9,"#F4EDF9",DockAppearance.HeaderFont,align:"center",tracking:1.2);
    }
    static string Monogram(string champion)
    {
        var words=champion.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length>1)return string.Concat(words.Select(word=>char.ToUpperInvariant(word[0])));
        return champion.Length>0?char.ToUpperInvariant(champion[0]).ToString():"?";
    }
    // One badge per objective, the count inside: a zero keeps its place so the band
    // never reflows, and the local player's side carries its own colour.
    Rect Band(double anchor,double y,bool right,LolObjective objective,bool mine,bool compact)
    {
        string tint=objective.Team=="BLEU"?Blue:Red;
        var badges=new (LolGlyphs.Glyph Glyph,int Count,string Color)[]{
            (LolGlyphs.Element(objective.Dragon),objective.Dragons,Element(objective.Dragon,tint)),
            (LolGlyphs.Glyph.Baron,objective.Barons,tint),
            (LolGlyphs.Glyph.Turret,objective.Turrets,tint),
            (LolGlyphs.Glyph.Inhibitor,objective.Inhibitors,tint)};
        if(rendered.Aram)badges=badges.Skip(2).ToArray();
        double badge=compact?26:36,step=compact?30:40,glyph=compact?12:14;
        double label=compact?0:Paragraph(objective.Team,10,Ink,200).Width+22,lead=compact?10:0;
        double width=label+lead+badges.Length*badge+(badges.Length-1)*(step-badge);
        double x=right?anchor-width:anchor;
        var band=new Rect(x-10,y-6,width+20,32);
        if(mine)Box(band.X,band.Y,band.Width,band.Height,"#1E"+tint[1..],"#44"+tint[1..],11);
        D.DrawEllipse(DesktopTheme.Brush(tint),null,new Point(x+3.4,y+5.6),3,3);
        if(compact)x+=lead;else{Text(objective.Team,x+11,y+1,10,mine?tint:Ink,bold:true);x+=label;}
        foreach(var item in badges)
        {
            Box(x,y-3,badge,22,"#102D203B","#2AD1B5DB",11);
            LolGlyphs.Draw(D,item.Glyph,x+4,y+(compact?2.5:1.5),glyph,item.Color,item.Count>0?1:.4);
            Text(item.Count.ToString(),x+badge-5,y+3,compact?9:10,item.Count>0?Ink:Muted,DockAppearance.NumberFont,align:"right",width:14);
            x+=step;
        }
        return band;
    }
    // Dragon and baron are the only timed objectives: both come from the kill
    // events, and Howling Abyss has neither.
    (LolGlyphs.Glyph Glyph,string Text,string Color,double Glow)[] Chips(LolSnapshot state,double points)
    {
        if(state.Aram)return [];
        string fire=Element(state.Blue.Dragon,"") is{Length:>0} blue?blue:Element(state.Red.Dragon,"");
        return [
            (LolGlyphs.Glyph.Dragon,Remaining("Dragon",state.DragonIn),Urgent(state.DragonIn)?Gold:fire.Length>0?fire:"#D8C8E3",Urgency(state.DragonIn)),
            (LolGlyphs.Glyph.Baron,Remaining("Baron",state.BaronIn),Urgent(state.BaronIn)?Gold:"#D8C8E3",Urgency(state.BaronIn))];
    }
    void DrawChip((LolGlyphs.Glyph Glyph,string Text,string Color,double Glow) chip,double x,double y,double glyph,double points)
    {
        double glow=chip.Glow>0?1+.5*Math.Sin(seconds*4.6)*chip.Glow:1;
        LolGlyphs.Draw(D,chip.Glyph,x,y+2,glyph,chip.Color,glow);
        Text(chip.Text,x+glyph+3,y+2,points,chip.Color);
    }
    // The timers sit on the divider between the counters and the team band.
    void Timers(LolSnapshot state,double y,bool compact)
    {
        double points=compact?7.5:9;
        var chips=Chips(state,points);
        if(chips.Length==0){Line(24,y+6,Width-24,y+6,"#2AD1B5DB");return;}
        double gap=compact?10:14,width=chips.Sum(chip=>18+Paragraph(chip.Text,points,Ink,300).Width)+(chips.Length-1)*gap+12,left=Width/2-width/2;
        Line(24,y+6,left-10,y+6,"#2AD1B5DB");
        Line(left+width+10,y+6,Width-24,y+6,"#2AD1B5DB");
        Box(left,y-4,width,20,"#261B1227","#2AD1B5DB",10);
        double cursor=left+10;
        foreach(var chip in chips){DrawChip(chip,cursor,y-2,compact?11:12,points);cursor+=18+Paragraph(chip.Text,points,Ink,300).Width+gap;}
    }
    // Everything the events just announced: the kill ping, the streak pulse, the
    // multi-kill banner, the objective flash and the respawn countdown.
    void Effects(LolSnapshot state)
    {
        if(objectiveFlash>.01)
        {
            string tint=objectiveSide==1?Red:Blue;
            var band=objectiveSide==1?redBand:blueBand;
            D.PushOpacity(.3*objectiveFlash);D.DrawRoundedRectangle(DesktopTheme.Brush(tint),null,band,11,11);D.Pop();
        }
        if(killPing>.01)
        {
            double k=killPing;
            LolGlyphs.Arc(D,crest,crestRadius+7+35*(1-k),360,Gold,3,k*.8);
            LolGlyphs.Arc(D,crest,crestRadius+10+60*(1-k),360,Blue,1.5,k*.5);
            D.PushOpacity(k);
            for(int i=0;i<12;i++)
            {
                double angle=i*Math.PI/6,dist=crestRadius+12+55*(1-k);
                double px=crest.X+Math.Cos(angle)*dist,py=crest.Y+Math.Sin(angle)*dist;
                Line(px,py,px+Math.Cos(angle)*8*k,py+Math.Sin(angle)*8*k,Gold,2);
            }
            D.Pop();
        }
        if(deathFlash>.01)
        {
            D.PushOpacity(.2*deathFlash);D.DrawRoundedRectangle(DesktopTheme.Brush(Error),null,new Rect(1,1,Width-2,Height-2),23,23);D.Pop();
        }
        if(state.Respawn is{} left)
        {
            var center=new Point(Width/2,Height/2);
            double radius=Math.Min(42,Height/2-24);
            D.PushOpacity(.56);D.DrawRoundedRectangle(DesktopTheme.Brush("#261B1227"),null,new Rect(1,1,Width-2,Height-2),23,23);D.Pop();
            LolGlyphs.Arc(D,center,radius,360,"#2AD1B5DB",5,.6);
            LolGlyphs.Arc(D,center,radius,360*Math.Clamp(left/Math.Max(1,respawnSpan),0,1),Error,5);
            Text($"{Math.Max(0,Math.Ceiling(left)):0}",center.X,center.Y-22,26,Error,DockAppearance.NumberFont,align:"center");
            Text("RESPAWN",center.X,center.Y+12,8,"#F4B7CA",align:"center",tracking:2.4);
        }
        if(multiFlash>.01&&headline.Length>0)
        {
            double alpha=Math.Min(1,multiFlash*1.3),middle=Height/2;
            string tint=Headline(headline);
            D.PushOpacity(.5*alpha);D.DrawRectangle(DesktopTheme.Brush("#E62C213B"),null,new Rect(1,middle-28,Width-2,56));D.Pop();
            D.PushOpacity(.18*alpha);D.DrawEllipse(DesktopTheme.Brush(tint),null,new Point(Width/2,middle),200,30);D.Pop();
            for(int i=0;i<3;i++)
            {
                double offset=(1-alpha)*90+i*14,row=middle-12+i*11;
                Line(Width/2-150-offset,row,Width/2-40-offset,row,tint,1.2);
                Line(Width/2+40+offset,row,Width/2+150+offset,row,tint,1.2);
            }
            D.PushOpacity(.3*alpha);Text(headline,Width/2,middle-21,24,tint,DockAppearance.HeaderFont,align:"center",tracking:4.6);D.Pop();
            Text(headline,Width/2,middle-21,24,tint,DockAppearance.HeaderFont,align:"center",tracking:2.8);
        }
    }
    void Waiting(LolSnapshot state)
    {
        var (message,color)=state.State switch
        {
            LolState.Waiting=>("En attente de la partie",Muted),
            LolState.Unavailable=>("Télémétrie indisponible",Error),
            _=>("Aucune partie",Muted)
        };
        // A dock with nothing to read still fills its card: the same crest, sized
        // to the block, and the state in the middle.
        double scale=Math.Clamp(Math.Min(Width/560d,Height/200d),.9,2.4),radius=26*scale;
        double pulse=.5+.5*Math.Sin(seconds*2.2);
        crest=new Point(24+radius,Height/2);
        D.DrawEllipse(DesktopTheme.Brush("#2AD1B5DB"),null,crest,radius+5,radius+5);
        D.DrawEllipse(DesktopTheme.Brush("#102D203B"),null,crest,radius,radius);
        LolGlyphs.Draw(D,LolGlyphs.Glyph.Sword,crest.X-radius*.46,crest.Y-radius*.46,radius*.92,color,state.State==LolState.Waiting?.35+.25*pulse:.4);
        double x=crest.X+radius+16;
        Text(message,x,crest.Y-15*scale,15*scale,color,width:Width-x-24);
        Text("Lecture de l'API locale du client de jeu",x,crest.Y+10*scale,8.5*scale,Muted,width:Width-x-24);
    }
    // A pill hugs its own text: the border is measured, never guessed.
    Rect Pill(double x,double y,string label,string color,LolGlyphs.Glyph? glyph=null,double points=9)
    {
        double width=PillWidth(label,points,glyph);var rect=new Rect(x,y,width,20);
        Box(rect.X,rect.Y,rect.Width,rect.Height,"#102D203B","#2AD1B5DB",10);
        double cursor=x+9;
        if(glyph is{} icon){LolGlyphs.Draw(D,icon,cursor,y+4,12,color);cursor+=18;}
        Text(label,cursor,y+1+(20-points*96/72)/2,points,color);
        return rect;
    }
    double PillWidth(string label,double points,LolGlyphs.Glyph? glyph)=>Paragraph(label,points,Ink,400).Width+18+(glyph is null?0:18);
    void Pulse(Rect rect,string color,double strength)
    {
        D.PushOpacity(.5*strength);
        Box(rect.X-3,rect.Y-3,rect.Width+6,rect.Height+6,"#00000000",color,13,1.4);
        D.Pop();
    }
    void Live(double x,double y,string accent,bool labelled)
    {
        double pulse=.5+.5*Math.Sin(seconds*2.6);
        var center=new Point(x+4,y+5.4);
        D.PushOpacity(.16+.2*pulse);D.DrawEllipse(DesktopTheme.Brush(accent),null,center,7.5+2.4*pulse,7.5+2.4*pulse);D.Pop();
        D.DrawEllipse(DesktopTheme.Brush(accent),null,center,3.4,3.4);
        if(labelled&&x+60<Width-140)Text("EN PARTIE",x+14,y,8.5,Ink);
    }
    static string Mode(LolSnapshot state)=>state.Mode switch
    {
        "CLASSIC"=>"CLASSIQUE","ARAM"=>"ARAM","KIWI"=>"ARAM MAYHEM","URF"=>"URF","ONEFORALL"=>"UNE POUR TOUS",
        "PRACTICETOOL"=>"ENTRAÎNEMENT","TUTORIAL"=>"TUTORIEL","NEXUSBLITZ"=>"BLITZ",
        _=>state.Mode.ToUpperInvariant()
    };
    static string Clock(double seconds)
    {
        int total=(int)Math.Max(0,seconds);return $"{total/60:00}:{total%60:00}";
    }
    static string Remaining(string name,double? left)=>left is not{} value?$"{name} —":value<=0?name+" prêt":$"{name} {Clock(value)}";
    static bool Urgent(double? left)=>left is{} value&&value<=30;
    static double Urgency(double? left)=>Urgent(left)?1:0;
    static string GoldLabel(int gold)=>gold>=1000?$"{(gold/1000d).ToString("0.0",French)} k or":gold+" or";
    static string Headline(string label)=>label switch
    {
        "PENTA KILL"=>Gold,"QUADRA KILL"=>Purple,"TRIPLE KILL"=>Pink,"DOUBLE KILL"=>Blue,"PREMIER SANG"=>Error,_=>Ink
    };
    // The last drake a team took tints its counter: the element is real data, the
    // colour only makes it readable at badge size.
    static string Element(string type,string fallback)=>type switch
    {
        "Fire" or "Infernal"=>"#FF9C5B","Water" or "Ocean"=>"#6FD8FF","Earth" or "Mountain"=>"#C9A46A",
        "Air" or "Cloud"=>"#DCE6F5","Hextech"=>"#9E7BFF","Chemtech"=>"#7FE08A","Elder"=>Gold,_=>fallback
    };
    public void Dispose(){CompositionTarget.Rendering-=Frame;incomeChart.Active=false;telemetry.Dispose();}
}
