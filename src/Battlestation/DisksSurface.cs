using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Battlestation;

internal sealed class DisksSurface : Surface,IDisposable
{
    readonly DiskIndex index;
    readonly DiskActivity activity;
    // Charge d'E/S de chaque volume, versée comme de l'eau dans sa carte. Mesuré sur
    // ce PC : C: oscille entre 0 et 6 % au repos (sessions Claude Code), ce qui tenait
    // l'onde éveillée la moitié du temps. Sous 8 % le disque est calme ; ensuite, seuls
    // 5 points d'écart relancent une vague.
    const double QuietLoad=.08,PourThreshold=.05;
    readonly Dictionary<string,QuotaLiquid> liquids=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,double> poured=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Rect> wells=new(StringComparer.OrdinalIgnoreCase);
    readonly List<(string Path,double X,double Y,string Align)> loadLabels=new();
    string loadText="";
    TimeSpan liquidFrame;
    bool liquidHooked;
    int activityQueued;
    readonly DispatcherTimer settle=new(){Interval=TimeSpan.FromMilliseconds(380)};
    readonly Stack<string?> history=new();
    DrawingGroup? drawing,outgoing;
    DiskTile[] tiles=[];
    DiskNode? shown;
    string? path,root;
    string theme="";
    DrawingGroup? emblem;
    Brush[] colors=[];
    bool active,disposed,animating;
    int queued;
    Size size;
    bool PathNeedsLine=>path is not null&&Paragraph(path,10,Ink,Math.Max(1,Width-292)).Height>20;
    double ContentTop=>DockAppearance.HeaderContentTop+(PathNeedsLine?DockAppearance.HeaderSecondaryLine:0);
    Rect MapBounds=>new(20,ContentTop,Math.Max(1,Width-40),Math.Max(1,Height-ContentTop-(path is null?20:46)));
    internal DisksSurface(Station station,DiskIndex? index=null,DiskActivity? activity=null):base(station,21)
    {
        this.index=index??new();
        this.activity=activity??new(()=>this.index.Volumes);
        Width=560;Height=600;this.index.Changed+=Changed;this.activity.Changed+=ActivityChanged;
        settle.Tick+=(_,_)=>{settle.Stop();animating=false;outgoing=null;drawing=null;UpdateTree();Refresh();};
        SizeChanged+=(_,_)=>{StopAnimation();drawing=null;Refresh();};
    }
    internal void SetActive(bool value)
    {
        if(active==value)return;active=value;index.SetActive(value);activity.SetActive(value);
        if(!value){StopAnimation();SleepLiquids(true);}else{UpdateTree();Refresh();}
    }
    void ActivityChanged()
    {
        if(disposed||Interlocked.Exchange(ref activityQueued,1)!=0)return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>{Interlocked.Exchange(ref activityQueued,0);if(!disposed&&active)PourActivity();});
    }
    internal void PourActivity()
    {
        foreach(var (volume,well) in wells)
        {
            double load=activity.Load(volume) is {} value&&value>=QuietLoad?value:0;
            double last=poured.GetValueOrDefault(volume);
            if(Math.Abs(load-last)<PourThreshold&&!(load==0&&last>0))continue;
            poured[volume]=load;Liquid(volume).Layout(well,16,load*100,WaterColor);
        }
        WakeLiquids();
        string text=string.Join('|',loadLabels.Select(label=>LoadText(label.Path)));
        if(text!=loadText){loadText=text;Refresh();}
    }
    QuotaLiquid Liquid(string volume){if(!liquids.TryGetValue(volume,out var liquid))liquids[volume]=liquid=new();return liquid;}
    static Color WaterColor=>DesktopTheme.Color("#8EC5EA");
    string LoadText(string volume)=>activity.Load(volume) is {} load?$"Activité {load*100:0} %":"";
    void WakeLiquids()
    {
        if(!liquids.Values.Any(liquid=>liquid.Awake)||liquidHooked)return;
        if(!active||path is not null||animating){SleepLiquids(true);return;}
        liquidHooked=true;liquidFrame=default;CompositionTarget.Rendering+=LiquidFrame;
    }
    void SleepLiquids(bool calm)
    {
        if(liquidHooked){CompositionTarget.Rendering-=LiquidFrame;liquidHooked=false;}
        if(calm)foreach(var liquid in liquids.Values)liquid.Settle();
    }
    void LiquidFrame(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs frame||frame.RenderingTime==liquidFrame)return;
        // 30 images/s suffisent à une surface d'eau ; les autres passages ne calculent rien.
        if(liquidFrame!=default&&(frame.RenderingTime-liquidFrame).TotalSeconds<1/32d)return;
        double elapsed=liquidFrame==default?1/30d:(frame.RenderingTime-liquidFrame).TotalSeconds;
        liquidFrame=frame.RenderingTime;
        if(!active||path is not null||animating){SleepLiquids(true);return;}
        bool awake=false;
        foreach(var volume in wells.Keys)awake|=Liquid(volume).Step(elapsed);
        if(!awake)SleepLiquids(false);
    }
    protected override void OnPointer(MouseEventArgs e)=>StirLiquids(e.GetPosition(this));
    protected override void OnMouseLeave(MouseEventArgs e){StirLiquids(new(-1,-1));base.OnMouseLeave(e);}
    void StirLiquids(Point pointer)
    {
        if(path is not null||animating)return;
        long now=Environment.TickCount64;
        foreach(var volume in wells.Keys)Liquid(volume).Stir(pointer,now);
        WakeLiquids();
    }
    void Changed()
    {
        if(disposed||Interlocked.Exchange(ref queued,1)!=0)return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>{Interlocked.Exchange(ref queued,0);if(disposed||!active)return;if(!animating){if(path is null)drawing=null;UpdateTree();}Refresh();});
    }
    void UpdateTree()
    {
        if(path is null)return;
        var tree=index.Snapshot;
        var next=tree?.Find(path);
        if(next is null&&tree is not null&&tree.Path==root&&index.Status.Length==0)
        {
            while(next is null&&history.Count>0){path=history.Pop();next=path is null?null:tree.Find(path);if(path is null)break;}
        }
        if(!ReferenceEquals(shown,next)){shown=next;drawing=null;}
    }
    void StopAnimation(){settle.Stop();animating=false;outgoing=null;drawing=null;}
    static Color ColorOf(string value)=> (Color)ColorConverter.ConvertFromString(value);
    void Artwork()
    {
        if(theme==DesktopTheme.Current.Id)return;theme=DesktopTheme.Current.Id;var palette=DesktopTheme.Current;drawing=null;
        colors=Enumerable.Range(0,6).Select(i=>
        {
            var pearl=ColorOf(palette.Pearl[i%palette.Pearl.Length]);var accent=ColorOf(palette.Active[i%palette.Active.Length]);
            var color=Color.FromRgb((byte)(pearl.R*.30+accent.R*.16+22),(byte)(pearl.G*.30+accent.G*.16+22),(byte)(pearl.B*.30+accent.B*.16+25));
            var brush=new LinearGradientBrush(color,Color.FromRgb((byte)(color.R*.53),(byte)(color.G*.53),(byte)(color.B*.57)),75);brush.Freeze();return (Brush)brush;
        }).ToArray();
        var metal=new LinearGradientBrush(ColorOf(palette.Pearl[0]),ColorOf(palette.Pearl[2]),55);metal.GradientStops.Insert(1,new(ColorOf(palette.Pearl[3]),.5));metal.Freeze();
        var rim=new Pen(B("#A8FFFFFF"),.8);emblem=new();
        using(var dc=emblem.Open())
        {
            dc.DrawRoundedRectangle(B("#40211B31"),new Pen(B(palette.Active[0]),.8),new(9,18,66,49),11,11);
            dc.DrawRoundedRectangle(metal,rim,new(9,10,66,49),11,11);
            dc.DrawRoundedRectangle(B("#C826263A"),null,new(19,22,46,23),5,5);
            dc.DrawLine(new Pen(B(palette.Pearl[1]),1.2),new(26,30),new(49,30));
            dc.DrawLine(new Pen(B(palette.Pearl[3]),1.2),new(26,36),new(42,36));
            for(int i=0;i<5;i++)dc.DrawRoundedRectangle(B(palette.Pearl[0]),null,new(25+i*7,61,3,5),1,1);
            dc.DrawEllipse(B(palette.Active[3]),null,new(65,51),2,2);
            dc.DrawLine(new Pen(B("#DFFFFFFF"),1),new(17,16),new(40,16));
        }
        emblem=emblem.CloneCurrentValue();emblem.Freeze();
    }
    internal static string Bytes(long bytes)=>bytes>=1L<<40?$"{bytes/(double)(1L<<40):0.00} To":bytes>=1L<<30?$"{bytes/(double)(1L<<30):0.0} Go":bytes>=1L<<20?$"{bytes/(double)(1L<<20):0.0} Mo":bytes>=1024?$"{bytes/1024d:0.0} Ko":$"{bytes} o";
    protected override void Paint()
    {
        Artwork();Header("DISQUES");
        if(path is not null)
        {
            Button("DisksBack","‹",Width-136,12,32,28,Back,17,enabled:!animating);
            Button("DisksHome","⌂",Width-98,12,32,28,Home,13,enabled:!animating);
            Button("DisksRefresh","↻",Width-60,12,32,28,()=>index.Rescan(),12,enabled:!animating);
            if(PathNeedsLine)Text(path,24,48,10,Ink,width:Width-48);
            else Text(path,144,19,10,Ink,width:Width-292);
        }
        else Text(index.Volumes.Length==0?"":$"{Bytes(index.Volumes.Sum(v=>v.Free))} libres sur {Bytes(index.Volumes.Sum(v=>v.Total))}",Width-24,19,9,Muted,align:"right",width:Width-170);
        if(size!=new Size(Width,Height)){size=new(Width,Height);drawing=null;}
        if(drawing is null)drawing=BuildDrawing();
        D.PushClip(new RectangleGeometry(MapBounds,12,12));
        if(outgoing is not null)D.DrawDrawing(outgoing);
        D.DrawDrawing(drawing);D.Pop();
        if(!animating)
        {
            foreach(var label in loadLabels)Text(LoadText(label.Path),label.X,label.Y,8,Muted,align:label.Align);
            foreach(var tile in tiles)
            {
                if(tile.Bounds.Width<3||tile.Bounds.Height<3)continue;
                if(path is null)Hit("Disk:"+tile.Node.Path,tile.Bounds.X,tile.Bounds.Y,tile.Bounds.Width,tile.Bounds.Height,()=>EnterDrive(tile));
                else if(tile.Node.Path.Length>0)Hit("DiskFolder:"+tile.Node.Path,tile.Bounds.X,tile.Bounds.Y,tile.Bounds.Width,tile.Bounds.Height,()=>OpenFolder(tile.Node));
                if(tile.Bounds.Contains(Pointer))HoverGlass(Inset(tile.Bounds,2),10,DesktopTheme.Current.Pearl[tile.Color%DesktopTheme.Current.Pearl.Length]);
            }
        }
        var hover=tiles.FirstOrDefault(t=>t.Bounds.Contains(Pointer));
        string footer=path is null?"":index.Status.Length>0?$"{index.Status}  {index.Scanned:N0} éléments":shown is null?"Chargement…":$"{Bytes(shown.Bytes)} analysés"+(shown.Partial?" · analyse partielle":"");
        if(path is not null&&hover.Node is not null)footer=hover.Node.Name+" · "+Bytes(hover.Node.Bytes)+(hover.Node.Partial?" · partiel":"");
        Text(footer,24,Height-31,8,Muted,width:Width-48);
    }
    static Rect Inset(Rect r,double n)=>new(r.X+n,r.Y+n,Math.Max(0,r.Width-n*2),Math.Max(0,r.Height-n*2));
    DrawingGroup BuildDrawing()
    {
        var group=new DrawingGroup();var before=D;
        wells.Clear();loadLabels.Clear();
        using(var dc=group.Open())
        {
            D=dc;
            if(path is null)DrawDrives();
            else
            {
                tiles=shown is null?[]:DiskMap.Layout(shown.Children,MapBounds);
                foreach(var tile in tiles)DrawTile(tile);
                if(tiles.Length==0){Text(shown is null?"Analyse du disque…":shown.Partial?"Contenu inaccessible":"Dossier vide",Width/2,MapBounds.Y+MapBounds.Height/2-12,13,Muted,align:"center");}
            }
            D=before;
        }
        return group;
    }
    void DrawDrives()
    {
        var drives=index.Volumes;var bounds=MapBounds;int columns=bounds.Width<650||bounds.Height<250?1:Math.Min(Math.Max(1,(int)(bounds.Width/210)),Math.Max(1,drives.Length));int rows=Math.Max(1,(int)Math.Ceiling(drives.Length/(double)columns));
        double w=(bounds.Width-(columns-1)*12)/columns,h=(bounds.Height-(rows-1)*12)/rows;
        var result=new List<DiskTile>();
        for(int i=0;i<drives.Length;i++)
        {
            var drive=drives[i];var rect=new Rect(bounds.X+i%columns*(w+12),bounds.Y+i/columns*(h+12),w,h);
            result.Add(new(new(drive.Path,drive.Name,drive.Total,true,false,[]),rect,i));
            D.DrawRoundedRectangle(DockAppearance.ButtonFill,new Pen(B(DockAppearance.ButtonRim),1),rect,16,16);
            // Le groupe du liquide est retenu : chaque image ne réécrit que lui, pas la carte.
            var liquid=Liquid(drive.Path);wells[drive.Path]=rect;
            liquid.Layout(rect,16,poured.GetValueOrDefault(drive.Path)*100,WaterColor);D.DrawDrawing(liquid.Drawing);
            double used=Math.Clamp(1-drive.Free/(double)Math.Max(1,drive.Total),0,1);
            string accent=UsageAccent(used);
            if(h>=250&&w>=200)DriveColumn(drive,rect,used,accent);
            else DriveRow(drive,rect,used,accent);
        }
        tiles=result.ToArray();
        if(drives.Length==0)Text("Recherche des disques…",bounds.X+16,bounds.Y+24,12,Muted);
    }
    void DriveIcon(double x,double y,double size)
    {
        D.PushTransform(new TranslateTransform(x,y));D.PushTransform(new ScaleTransform(size/84,size/84));D.DrawDrawing(emblem);D.Pop();D.Pop();
    }
    void Occupancy(Point center,double radius,double used,string accent,double thickness)
    {
        var halo=ColorOf(accent);halo.A=28;
        var glow=new RadialGradientBrush(halo,Colors.Transparent);glow.Freeze();
        D.DrawEllipse(glow,null,center,radius+17,radius+17);
        D.DrawEllipse(null,new Pen(B("#20E6DDEC"),thickness),center,radius,radius);
        if(used<=0)return;
        double angle=Math.Min(used,.99999)*Math.PI*2;
        var geometry=new StreamGeometry();
        using(var context=geometry.Open())
        {
            context.BeginFigure(new(center.X,center.Y-radius),false,false);
            context.ArcTo(new(center.X+Math.Sin(angle)*radius,center.Y-Math.Cos(angle)*radius),new(radius,radius),0,angle>Math.PI,SweepDirection.Clockwise,true,false);
        }
        geometry.Freeze();
        var fill=new LinearGradientBrush(ColorOf(accent),ColorOf(DesktopTheme.Current.Pearl[0]),55);fill.Freeze();
        D.DrawGeometry(null,new Pen(fill,thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},geometry);
        D.DrawEllipse(B(DesktopTheme.Current.Pearl[0]),null,new(center.X+Math.Sin(angle)*radius,center.Y-Math.Cos(angle)*radius),thickness*.24,thickness*.24);
    }
    void DriveRow(DiskVolume drive,Rect r,double used,string accent)
    {
        double cy=r.Y+r.Height/2,radius=Math.Clamp((r.Height-24)/2,18,42),left=r.X+radius+17;
        Occupancy(new(left,cy),radius,used,accent,radius<25?3:4);
        double icon=radius*1.4;DriveIcon(left-icon/2,cy-icon*.48,icon);
        double x=left+radius+18;
        bool roomy=r.Width>=470&&r.Height>=68;
        double freeWidth=roomy?215:165,detailsWidth=Math.Max(20,r.Right-freeWidth-22-x);
        double titleSize=r.Height>=100?13:10;
        Text(drive.Path.TrimEnd('\\')+"  "+drive.Name,x,cy-23,titleSize,Ink,bold:true,width:detailsWidth);
        Text($"{used*100:0} % occupés",x,cy-3,9,accent,width:detailsWidth);
        Text(ScanLabel(index.Info(drive.Path)),x,cy+15,7,Muted,width:detailsWidth);
        Text(Capacity(drive),r.Right-18,cy-10,roomy?15:11,accent,font:DockAppearance.NumberFont,align:"right");
        loadLabels.Add((drive.Path,r.Right-18,cy+12,"right"));
    }
    static string Capacity(DiskVolume drive)=>$"{Bytes(drive.Total-drive.Free)} / {Bytes(drive.Total)}";
    static string UsageAccent(double used)
    {
        var from=ColorOf(used<=.7?"#ADD4DF":used<=.85?"#AFD6BF":"#DCC18F");
        var to=ColorOf(used<=.7?"#AFD6BF":used<=.85?"#DCC18F":"#E2A092");
        double t=Math.Clamp(used<=.7?used/.7:used<=.85?(used-.7)/.15:(used-.85)/.15,0,1);
        return Color.FromRgb((byte)(from.R+(to.R-from.R)*t),(byte)(from.G+(to.G-from.G)*t),(byte)(from.B+(to.B-from.B)*t)).ToString();
    }
    internal static string ScanLabel(DiskScanInfo? scan)
    {
        if(scan is null)return "En attente";
        if(scan.Scanning)return "Analyse…";
        if(scan.Status.Length>0)return scan.Status;
        // Une réconciliation différée n'est pas une attente visible : les deltas gardent
        // les données fraîches. De même, les zones système refusées (présentes sur tout
        // volume NTFS) restent au survol et dans l'inspection, pas sur la carte.
        if(scan.CompletedAt is not {} completed)return scan.Pending?"En attente":"Non analysé";
        var local=completed.ToLocalTime();
        string stamp=local.Date==DateTime.Today?local.ToString("HH:mm"):local.ToString("dd/MM HH:mm");
        return $"Scan · {stamp}";
    }
    void DriveColumn(DiskVolume drive,Rect r,double used,string accent)
    {
        Text(drive.Path.TrimEnd('\\'),r.X+20,r.Y+17,22,Ink,font:DockAppearance.NumberFont);
        Text(drive.Name,r.X+78,r.Y+24,11,Muted,width:r.Width-98);
        Text(ScanLabel(index.Info(drive.Path)),r.X+20,r.Y+48,8,Muted,width:r.Width-40);
        loadLabels.Add((drive.Path,r.X+20,r.Y+62,"left"));
        double radius=Math.Clamp(Math.Min(r.Width*.32,(r.Height-178)/2),26,124);
        double cx=r.X+r.Width/2,cy=r.Y+(r.Height-16)/2;
        Occupancy(new(cx,cy),radius,used,accent,Math.Clamp(radius*.068,4,8));
        double icon=radius*.86;DriveIcon(cx-icon/2,cy-radius*.67,icon);
        Text($"{used*100:0} %",cx,cy+radius*.22,Math.Clamp(radius*.22,12,25),Ink,font:DockAppearance.NumberFont,align:"center");
        if(radius>=50)Text("occupés",cx,cy+radius*.58,8,Muted,align:"center");
        Text(Capacity(drive),cx,r.Bottom-53,Math.Clamp((r.Width-40)/14,11,24),accent,font:DockAppearance.NumberFont,align:"center");
    }
    void DrawTile(DiskTile tile)
    {
        var r=Inset(tile.Bounds,2);if(r.Width<1||r.Height<1)return;
        D.DrawRoundedRectangle(colors[tile.Color],new Pen(B("#34EEE8F5"),.7),r,Math.Min(9,r.Width/5),Math.Min(9,r.Height/5));
        if(tile.Node.Directory&&r.Width>72&&r.Height>64)
        {
            var inner=new Rect(r.X+5,r.Y+43,r.Width-10,r.Height-48);
            foreach(var child in DiskMap.Layout(tile.Node.Children,inner))
            {var box=Inset(child.Bounds,1);if(box.Width>1&&box.Height>1){D.PushOpacity(.24);D.DrawRoundedRectangle(colors[child.Color],new Pen(B("#55FFFFFF"),.6),box,3,3);D.Pop();}}
        }
        if(r.Width>58&&r.Height>24)
        {
            Text((tile.Node.Directory?"▱ ":"")+tile.Node.Name,r.X+9,r.Y+6,9,Ink,bold:tile.Node.Directory,width:r.Width-18);
            if(r.Height>46)Text(Bytes(tile.Node.Bytes),r.X+9,r.Y+25,8,Muted,width:r.Width-18);
        }
    }
    void EnterDrive(DiskTile tile)
    {
        if(animating)return;history.Clear();history.Push(null);root=tile.Node.Path;index.Select(root);Navigate(root,tile.Bounds,true);
    }
    void Navigate(string? next,Rect focus,bool forward)
    {
        string? previous=path;outgoing=drawing??BuildDrawing();path=next;shown=next is null?null:index.Snapshot?.Find(next);drawing=BuildDrawing();
        if(!forward){var target=tiles.FirstOrDefault(t=>t.Node.Path==previous);focus=target.Node is not null?target.Bounds:MapBounds;}
        Animate(outgoing,focus,forward,false);
        Animate(drawing,focus,forward,true);
        animating=true;settle.Stop();settle.Start();Refresh();
    }
    void Animate(DrawingGroup group,Rect focus,bool forward,bool incoming)
    {
        // Incoming expands from the chosen tile; outgoing uses the inverse camera transform.
        var viewport=MapBounds;
        bool small=forward==incoming;
        double sx=small?focus.Width/viewport.Width:viewport.Width/Math.Max(1,focus.Width),sy=small?focus.Height/viewport.Height:viewport.Height/Math.Max(1,focus.Height);
        double tx=small?focus.X-viewport.X*sx:viewport.X-focus.X*sx,ty=small?focus.Y-viewport.Y*sy:viewport.Y-focus.Y*sy;
        double sx0=incoming?sx:1,sy0=incoming?sy:1,tx0=incoming?tx:0,ty0=incoming?ty:0,sx1=incoming?1:sx,sy1=incoming?1:sy,tx1=incoming?0:tx,ty1=incoming?0:ty;
        var scale=new ScaleTransform(sx1,sy1);var move=new TranslateTransform(tx1,ty1);var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(move);group.Transform=transforms;
        DoubleAnimation Tween(double a,double b)=>new(a,b,TimeSpan.FromMilliseconds(350)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,Tween(sx0,sx1));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Tween(sy0,sy1));move.BeginAnimation(TranslateTransform.XProperty,Tween(tx0,tx1));move.BeginAnimation(TranslateTransform.YProperty,Tween(ty0,ty1));
        group.Opacity=incoming?1:0;group.BeginAnimation(DrawingGroup.OpacityProperty,Tween(incoming?0:1,incoming?1:0));
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled=true;if(animating)return;
        if(e.Delta<0){Back();return;}
        var tile=tiles.FirstOrDefault(t=>t.Bounds.Contains(e.GetPosition(this)));
        if(tile.Node is null||tile.Bounds.Width<3||tile.Bounds.Height<3)return;
        if(path is null){EnterDrive(tile);return;}
        if(!tile.Node.Directory||tile.Node.Path.Length==0)return;
        history.Push(path);Navigate(tile.Node.Path,tile.Bounds,true);
    }
    void Back()
    {
        if(animating||history.Count==0)return;string? parent=history.Pop();
        Navigate(parent,MapBounds,false);
        if(parent is null)index.Select(null);
    }
    void Home(){if(animating)return;history.Clear();Navigate(null,MapBounds,false);index.Select(null);}
    void OpenFolder(DiskNode node)
    {
        string? folder=node.Directory?node.Path:Path.GetDirectoryName(node.Path);
        if(folder is null)return;
        _=Task.Run(()=>{try{Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={folder},UseShellExecute=false});}catch(Exception e) when(e is IOException or System.ComponentModel.Win32Exception){}});
    }
    internal object Inspect()=>new{active,path,animating,liquidHooked,
        activity=wells.Keys.Select(volume=>new{path=volume,load=activity.Load(volume),poured=poured.GetValueOrDefault(volume),level=Liquid(volume).Level,awake=Liquid(volume).Awake}),status=index.Status,scanned=index.Scanned,bytes=shown?.Bytes,partial=shown?.Partial,volumes=index.Volumes,indexes=index.InspectIndexes(),tiles=tiles.Select(t=>new{t.Node.Path,t.Node.Bytes,t.Node.Directory,t.Bounds})};
    public void Dispose(){disposed=true;StopAnimation();SleepLiquids(false);index.Changed-=Changed;index.Dispose();activity.Changed-=ActivityChanged;activity.Dispose();}
}
