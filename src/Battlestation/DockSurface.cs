using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class DockSurface : Surface
{
    readonly double[] amount=new double[12],from=new double[12],target=new double[12];
    readonly DateTime[] since=new DateTime[12];
    readonly bool[] press=new bool[12];
    readonly DispatcherTimer animation;
    int hover=-1;
    int rowOffset;
    readonly AppIcons appIcons=new();
    readonly Dictionary<DockApp,AppActivityState> activityStates=[];
    readonly Dictionary<DockApp,AppWindowState> windowStates=[];
    readonly Dictionary<DockApp,double> materialFrom=[];
    readonly Dictionary<DockApp,double> materialTarget=[];
    readonly Dictionary<DockApp,DateTime> materialSince=[];
    AppActivitySnapshot? lastActivity;
    long nextPoll;
    bool pollRunning,disposed;
    bool Column=>Width<256;
    bool FooterAdd=>Column&&Height>=176;
    double ContentHeight=>Height-44-(FooterAdd?48:8);
    double RowPitch=>Math.Min(88,Math.Max(48,ContentHeight));
    double TargetSize=>Math.Min(80,RowPitch);
    int Columns=>Column?1:Math.Max(1,Math.Min(Math.Max(1,Station.Apps.Count),(int)((Width-32)/88)));
    int VisibleRows=>Math.Max(1,(int)(ContentHeight/RowPitch));
    static string Genre(DockApp app)=>Regex.Replace(app.Name.ToLowerInvariant(),"[^a-z0-9]","") switch
    {
        "steam" or "leagueoflegends" or "battlenet" or "epicgames" or "goggalaxy"=>"JEUX",
        "brave" or "edge" or "chrome" or "firefox"=>"WEB",
        "spotify" or "stremio" or "vlc"=>"MÉDIAS",
        "discord" or "slack" or "whatsapp" or "telegram"=>"SOCIAL",
        _=>"OUTILS"
    };
    // Preserve configuration order inside each pack, and fall back to the
    // scrollable grid whenever every pack cannot fit at a useful icon size.
    (int Index,Point Center)[] IconLayout(bool paint=false)
    {
        var groups=Station.Apps.Select((app,index)=>(app,index)).GroupBy(item=>Genre(item.app)).ToArray();
        double required=groups.Sum(group=>Math.Min(2,group.Count())*80+24)+Math.Max(0,groups.Length-1)*12;
        int rows=groups.Length==0?0:groups.Max(group=>(group.Count()+1)/2);
        if(groups.Length>1&&Width-32>=required&&ContentHeight>=rows*80+38)
        {
            var icons=new List<(int,Point)>();double x=16,top=44+(ContentHeight-(rows*80+38))/2;
            double extra=(Width-32-required)/groups.Length;
            foreach(var group in groups)
            {
                int columns=Math.Min(2,group.Count());double w=columns*80+24+extra;
                if(paint){Box(x,top,w,rows*80+38,"#102D203B","#22D2BDDF",16);Text(group.Key,x+16,top+10,8,Muted);}
                int i=0;foreach(var item in group){icons.Add((item.index,new Point(x+w/2+(i%columns-(columns-1)/2d)*80,top+76+i/columns*80)));i++;}
                x+=w+12;
            }
            return icons.ToArray();
        }
        ClampRows();int cols=Columns;double pitch=(Width-32-(Column&&!FooterAdd?48:0))/cols;
        return Enumerable.Range(rowOffset*cols,Math.Max(0,Math.Min(Station.Apps.Count,(rowOffset+VisibleRows)*cols)-rowOffset*cols))
            .Select(i=>(i,new Point(16+(i%cols)*pitch+pitch/2,44+RowPitch/2+(i/cols-rowOffset)*RowPitch))).ToArray();
    }
    void ClampRows()=>rowOffset=Math.Clamp(rowOffset,0,Math.Max(0,(int)Math.Ceiling(Station.Apps.Count/(double)Columns)-VisibleRows));
    public DockSurface(Station s):base(s,2){Width=720;Height=DesktopLayout.DockHeight(s.Apps.Count);animation=new DispatcherTimer(TimeSpan.FromMilliseconds(16),DispatcherPriority.Render,(_,_)=>Animate(),Dispatcher);animation.Stop();Station.DockChanged+=AppsChanged;}
    void Retarget(int i,double value,bool clicked=false){if(i<0||i>=12)return;from[i]=amount[i];target[i]=value;since[i]=DateTime.UtcNow;press[i]=clicked;animation.Start();}
    void Animate(){bool active=false;for(int i=0;i<12;i++){double p=Math.Clamp((DateTime.UtcNow-since[i]).TotalMilliseconds/160,0,1);amount[i]=from[i]+(target[i]-from[i])*(1-Math.Pow(1-p,3));active|=p<1;}foreach(var app in materialTarget.Keys.ToArray()){var p=Math.Clamp((DateTime.UtcNow-materialSince[app]).TotalMilliseconds/260,0,1);_ = ActivityAmount(app);active|=p<1;}Refresh();if(!active)animation.Stop();}
    void AppsChanged()
    {
        nextPoll=0;
        foreach(var app in activityStates.Keys.Where(app=>!Station.Apps.Contains(app)).ToArray())
        {
            activityStates.Remove(app);materialFrom.Remove(app);materialTarget.Remove(app);materialSince.Remove(app);
        }
        foreach(var app in windowStates.Keys.Where(app=>!Station.Apps.Contains(app)).ToArray())windowStates.Remove(app);
        Retarget(hover,0);hover=-1;ToolTip=null;Refresh();
    }
    // Called only by the workspace's UI timer. The continuation returns to that dispatcher.
    internal async void Poll()
    {
        if(disposed||pollRunning||Environment.TickCount64<nextPoll)return;
        var apps=Station.Apps.ToArray();nextPoll=Environment.TickCount64+2000;pollRunning=true;
        try
        {
            AppActivitySnapshot snapshot;
            try{snapshot=await Task.Run(()=>AppActivity.Snapshot(apps));}
            catch(Exception e){snapshot=AppActivity.Unknown(apps,e.GetType().Name);}
            if(disposed)return;
            if(Station.Apps.SequenceEqual(apps))ApplyActivitySnapshot(snapshot);
            else nextPoll=0;
        }
        finally{pollRunning=false;}
    }
    internal void ApplyActivitySnapshot(AppActivitySnapshot snapshot)
    {
        if(disposed||!Station.Apps.SequenceEqual(snapshot.Results.Select(result=>result.App)))return;
        lastActivity=snapshot;
        bool changed=false;
        foreach(var result in snapshot.Results)
        {
            changed|=!activityStates.TryGetValue(result.App,out var previous)||previous!=result.State;
            activityStates[result.App]=result.State;
            changed|=!windowStates.TryGetValue(result.App,out var previousWindow)||previousWindow!=result.Window;
            windowStates[result.App]=result.Window;
            if(result.State is not (AppActivityState.Running or AppActivityState.Stopped))continue;
            var next=result.State==AppActivityState.Running?1d:0d;
            var current=ActivityAmount(result.App);
            if(materialTarget.GetValueOrDefault(result.App)!=next)
            {
                materialFrom[result.App]=current;materialTarget[result.App]=next;materialSince[result.App]=DateTime.UtcNow;animation.Start();
            }
        }
        if(changed){UpdateToolTip();Refresh();}
    }
    double ActivityAmount(DockApp app)
    {
        if(!materialTarget.TryGetValue(app,out var targetValue))return 0;
        var fromValue=materialFrom.TryGetValue(app,out var fromValueFound)?fromValueFound:0;
        var p=Math.Clamp((DateTime.UtcNow-materialSince.GetValueOrDefault(app,DateTime.UtcNow)).TotalMilliseconds/260,0,1);
        var smooth=p*p*(3-2*p);return fromValue+(targetValue-fromValue)*smooth;
    }
    internal object InspectActivity()
    {
        var scan=lastActivity;
        return new
        {
            polling=pollRunning,
            capturedAt=scan?.CapturedAt,
            scanMs=scan?.ScanDuration.TotalMilliseconds,
            processScanSucceeded=scan?.ProcessScanSucceeded,
            error=scan?.Error,
            apps=Station.Apps.Select((app,index)=>
            {
                var result=scan?.ResultFor(app);var rule=scan?.Rules.FirstOrDefault(rule=>rule.ConfigurationPath.Equals(Environment.ExpandEnvironmentVariables(app.Path),StringComparison.OrdinalIgnoreCase));
                return new{name=app.Name,path=app.Path,state=activityStates.TryGetValue(app,out var state)?state.ToString():AppActivityState.Unknown.ToString(),window=windowStates.TryGetValue(app,out var window)?window.ToString():AppWindowState.None.ToString(),material=ActivityAmount(app),matchedPath=result?.MatchedPath,resolution=rule is null?null:new{rule.Kind,rule.Resolved,rule.TargetPath,rule.CandidateNames,rule.ExactPaths,rule.Roots}};
            }).ToArray()
        };
    }
    // The window state only modulates the existing material: vivid at the front,
    // current in the background, dimmed when reduced and pearl with a dot in the tray.
    static string WindowLabel(AppWindowState state)=>state switch{AppWindowState.Foreground=>"Premier plan",AppWindowState.Background=>"Arrière-plan",AppWindowState.Minimized=>"Réduit",AppWindowState.Tray=>"Tray",_=>"État inconnu"};
    static double WindowIntensity(AppWindowState state)=>state switch{AppWindowState.Foreground=>1,AppWindowState.Minimized=>.55,AppWindowState.Tray=>.35,_=>.82};
    internal void Dispose(){disposed=true;Station.DockChanged-=AppsChanged;animation.Stop();}
    void UpdateToolTip()
    {
        if(hover<0||hover>=Station.Apps.Count){ToolTip=null;return;}
        var app=Station.Apps[hover];
        var state=activityStates.GetValueOrDefault(app);
        ToolTip=state==AppActivityState.Running?$"{app.Name} · {WindowLabel(windowStates.GetValueOrDefault(app))}"
            :state==AppActivityState.Unknown?$"{app.Name} · État inconnu":app.Name;
    }
    protected override void OnPointer(MouseEventArgs e)
    {
        int next=-1;
        foreach(var icon in IconLayout())if(new Rect(icon.Center.X-TargetSize/2,icon.Center.Y-TargetSize/2,TargetSize,TargetSize).Contains(Pointer))next=icon.Index;
        if(next!=hover){Retarget(hover,0);hover=next;Retarget(hover,1);UpdateToolTip();}
    }
    protected override void OnMouseLeave(MouseEventArgs e){Retarget(hover,0);hover=-1;ToolTip=null;base.OnMouseLeave(e);}
    protected override void Paint()
    {
        if(Width<200)Text("APPLICATIONS",14,16,11,Ink,DockAppearance.HeaderFont,tracking:1);
        else Header("APPLICATIONS");
        var layout=IconLayout(true);
        foreach(var iconPosition in layout)
        {
            int i=iconPosition.Index;var app=Station.Apps[i];double x=iconPosition.Center.X,y=iconPosition.Center.Y;
            HoverGlass(new Rect(x-TargetSize/2,y-TargetSize/2,TargetSize,TargetSize),18);
            var key=Regex.Replace(app.Name.ToLowerInvariant(),"[^a-z0-9]","");var path=Path.Combine(Station.Root,"dock/icons/neon",key+".png");
            double p=Math.Clamp((DateTime.UtcNow-since[i]).TotalMilliseconds/160,0,1);
            double size=Math.Min(72,TargetSize*.9)*(1+amount[i]/6-(press[i]?Math.Sin(p*Math.PI)*.065:0));y-=amount[i]*3;
            var runningPath=Path.Combine(Station.Root,"dock/icons/running",key+".png");var running=ActivityAmount(app);
            var window=windowStates.GetValueOrDefault(app);var glow=running*WindowIntensity(window);
            if(File.Exists(path)){Image(path,x-size/2,y-size/2,size,size);if(glow>0&&File.Exists(runningPath))Image(runningPath,x-size/2,y-size/2,size,size,glow);}
            else if(glow>0&&File.Exists(runningPath))Image(runningPath,x-size/2,y-size/2,size,size,glow);
            else if(appIcons.Get(app.Path) is {} icon)D.DrawImage(icon,new Rect(x-size/2,y-size/2,size,size));
            else Text(string.IsNullOrEmpty(app.Name)?"?":app.Name[..1],x,y-22,26,align:"center");
            if(running>0&&window==AppWindowState.Foreground)D.DrawRoundedRectangle(null,new Pen(B("#8CFFFAFF"),1.4),new Rect(x-size/2,y-size/2,size,size),18,18);
            if(running>0&&window==AppWindowState.Tray)D.DrawEllipse(B("#DAD2E7"),null,new Point(x+size*.34,y-size*.34),2.6,2.6);
            int index=i;Hit("Launch:"+app.Name,x-TargetSize/2,iconPosition.Center.Y-TargetSize/2,TargetSize,TargetSize,()=>{Retarget(index,hover==index?1:0,true);Station.Launch(app);});
        }
        Button("ManageApps","+",FooterAdd?Width/2-14:Width-44,FooterAdd?Height-40:Column?44:8,28,28,()=>OpenEditor(),14);
        int total=(int)Math.Ceiling(Station.Apps.Count/(double)Columns);if(layout.Length<Station.Apps.Count&&total>VisibleRows){double track=ContentHeight;Box(Width-10,44,3,track,"#305C4868",radius:2);Box(Width-10,44+track*rowOffset/total,3,track*VisibleRows/total,"#A0DAC3E5",radius:2);}
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e){rowOffset+=e.Delta>0?-1:1;ClampRows();Refresh();e.Handled=true;}
    internal void OpenEditor(Window? owner=null)
    {
        var window=new Window{Title="Applications du dock",Width=500,Height=475,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=B("#261A32"),Foreground=B(Ink)};
        OverlayStyle.Apply(window);if(owner is not null){window.Owner=owner;window.WindowStartupLocation=WindowStartupLocation.CenterOwner;}
        var panel=new DockPanel{Margin=new Thickness(18)};var buttons=new WrapPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
        var items=Station.Apps.ToList();var list=new ListBox{DisplayMemberPath="Name",Background=B("#201629"),Foreground=B(Ink),BorderThickness=new Thickness(0)};panel.Children.Add(list);
        void Reload(int index){list.ItemsSource=null;list.ItemsSource=items;list.SelectedIndex=Math.Clamp(index,-1,items.Count-1);}
        void Add(string text,Action action){var b=new Button{Content=text,Margin=new Thickness(3),Padding=new Thickness(10,7,10,7)};b.Click+=(_,_)=>action();buttons.Children.Add(b);}
        Add("Ajouter",()=>{if(items.Count>=12)return;var dialog=new OpenFileDialog{Filter="Applications|*.lnk;*.exe;*.url"};if(dialog.ShowDialog(window)==true){items.Add(new DockApp(Path.GetFileNameWithoutExtension(dialog.FileName),dialog.FileName));Reload(items.Count-1);}});
        Add("Retirer",()=>{int i=list.SelectedIndex;if(i>=0){items.RemoveAt(i);Reload(Math.Min(i,items.Count-1));}});
        Add("↑",()=>{int i=list.SelectedIndex;if(i>0){(items[i-1],items[i])=(items[i],items[i-1]);Reload(i-1);}});
        Add("↓",()=>{int i=list.SelectedIndex;if(i>=0&&i<items.Count-1){(items[i+1],items[i])=(items[i],items[i+1]);Reload(i+1);}});
        Add("Appliquer",()=>{try{Station.SaveApps(items);window.Close();Refresh();}catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException){MessageBox.Show(window,e.Message,"Applications du dock",MessageBoxButton.OK,MessageBoxImage.Information);}});Add("Annuler",window.Close);
        var title=OverlayStyle.Text("Applications du dock",20);title.Margin=new Thickness(0,0,0,15);DockPanel.SetDock(title,Dock.Top);panel.Children.Insert(1,title);
        window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};
        Reload(0);window.Content=OverlayStyle.Frame(panel);window.ShowDialog();
    }
}
