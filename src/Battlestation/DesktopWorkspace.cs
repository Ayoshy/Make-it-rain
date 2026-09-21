using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Battlestation;
internal sealed partial class DesktopWorkspace : IDisposable
{
    readonly Application app;
    readonly Station station;
    readonly DesktopPlacement placement;
    readonly Dictionary<string,Surface> surfaces;
    readonly Dictionary<string,Window> windows=[];
    readonly Dictionary<string,Border> overlays=[];
    readonly DispatcherTimer timer,audio;
    readonly ControlPipe control;
    readonly Forms.NotifyIcon tray;
    readonly Window toolbar;
    bool editing,disposed;
    SceneTransition scene=null!;
    int ticks;
    readonly Dictionary<string,long> renderRevisions=[];
    ulong coverRevision=ulong.MaxValue;
    readonly DesktopVisibility visibility=new();
    readonly HashSet<string> exposed=[];
    int monitorMask=3;
    public DesktopWorkspace(Application application,Station state)
    {
        app=application;station=state;station.Terminal=new TerminalSession(station);
        placement=new DesktopPlacement(app.Dispatcher);
        surfaces=new(){["clock"]=new DeskSurface(station,DeskWidget.Clock),["weather"]=new DeskSurface(station,DeskWidget.Weather),
            ["apps"]=new DockSurface(station),["music"]=new DeskSurface(station,DeskWidget.Music),["projects"]=new DeskSurface(station,DeskWidget.Projects),
            ["terminal"]=new TerminalSurface(station),["countdown"]=new CountdownSurface(station),
            ["hardware"]=new DashboardSurface(station,true),["usage"]=new DashboardSurface(station,false),["reminders"]=new ReminderSurface(station),["video"]=new VideoSurface(station),["audio"]=new AudioSurface(station),["bluetooth"]=new BluetoothSurface(station),["dualsense"]=new DualSenseSurface(station),["network"]=new NetworkSurface(station),
            ["aquarium"]=new AquariumSurface(station),["ocean"]=new OceanSurface(station),["lol"]=new LolSurface(station),["shopping"]=new ShoppingSurface(station)};
        for(int i=0;i<DesktopTheme.Definitions.Length;i++)
        {
            var theme=DesktopTheme.Definitions[i];
            Native.BackgroundPalette(i,new[]{theme.Base,theme.Light,theme.Secondary,theme.Glass,theme.Rim,theme.Edge}.Select(c=>Convert.ToUInt32(c[1..],16)).ToArray());
        }
        Native.BackgroundTheme(Array.FindIndex(DesktopTheme.Definitions,t=>t.Id==station.Settings.ThemeId),1);
        Native.BackgroundStart(Native.DesktopParent(),Path.Combine(station.Assets,"Images"));
        Native.BackgroundAppearance(station.Settings.AnimateBackground?1:0,(float)station.Settings.GlassOpacity);
        CreateEditGrids();
        foreach(var block in station.Layout.Blocks)CreateWindow(block);
        scene=new SceneTransition(app.Dispatcher,Docks,alpha=>Native.BackgroundSceneFade((float)alpha),()=>station.Terminal?.SetVisible(false));
        station.DockChanged+=()=>Apply("apps");
        toolbar=CreateToolbar();
        tray=new Forms.NotifyIcon{Text="Battlestation",Icon=System.Drawing.SystemIcons.Application,Visible=true};
        // Le radar d'achat survit au dock masqué : la veille continue et prévient
        // par la zone de notification, jamais par un achat ou un message envoyé.
        station.Shopping.Alert+=alert=>app.Dispatcher.BeginInvoke(()=>tray.ShowBalloonTip(8000,alert.Title,alert.Message,Forms.ToolTipIcon.Info));
        station.Shopping.Start();
        var menu=new Forms.ContextMenuStrip();
        InitializeCommands();
        menu.Items.Add("Palette · Ctrl+Espace",null,(_,_)=>TogglePalette());
        menu.Items.Add("Réglages",null,(_,_)=>ShowSettings());
        menu.Items.Add("À regarder, à écouter",null,(_,_)=>ShowReserve());
        var modes=new Forms.ToolStripMenuItem("Scènes");
        modes.DropDownOpening+=(_,_)=>{modes.DropDownItems.Clear();foreach(string name in profiles.AllNames){var item=new Forms.ToolStripMenuItem(name){Checked=profiles.Current==name};item.Click+=(_,_)=>SelectProfile(name);modes.DropDownItems.Add(item);}modes.DropDownItems.Add(new Forms.ToolStripSeparator());modes.DropDownItems.Add("Rétablir le modèle",null,(_,_)=>ResetSceneTemplate());};menu.Items.Add(modes);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Réorganiser",null,(_,_)=>SetEditing(!editing));
        var add=new Forms.ToolStripMenuItem("Ajouter un bloc");
        add.DropDownOpening+=(_,_)=>{add.DropDownItems.Clear();foreach(var b in station.Layout.Blocks.Where(b=>!b.Visible))add.DropDownItems.Add(b.Title,null,(_,_)=>ShowBlock(b.Id));};
        menu.Items.Add(add);menu.Items.Add("Rétablir le modèle",null,(_,_)=>ResetSceneTemplate());
        menu.Items.Add("Recharger",null,(_,_)=>Reload());
        menu.Items.Add("Quitter",null,(_,_)=>app.Shutdown());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>SetEditing(!editing);
        ApplyAll();
        station.Terminal.HeaderChanged+=()=>Apply("terminal");
        timer=new DispatcherTimer(TimeSpan.FromMilliseconds(250),DispatcherPriority.Background,(_,_)=>Tick(),app.Dispatcher);
        audio=new DispatcherTimer(TimeSpan.FromMilliseconds(33),DispatcherPriority.Render,(_,_)=>((DeskSurface)surfaces["music"]).TickAudio(station.ReactiveAudio&&monitorMask!=0,exposed.Contains("music")),app.Dispatcher);
        Microsoft.Win32.SystemEvents.SessionSwitch+=SessionSwitch;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplaySettingsChanged;
        placement.VisibilityChanged+=UpdateVisibility;
        control=new ControlPipe(app.Dispatcher,Command);Tick();
    }
    static SolidColorBrush Brush(string color)=>DesktopTheme.Brush(color);
    Window CreateToolbar()
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        var frame=OverlayStyle.Frame(row);frame.Padding=new Thickness(10,7,10,7);
        var bar=new Window{Title="Battlestation · Réorganiser",Width=860,Height=64,Left=3460,Top=744,ShowInTaskbar=false,Topmost=true,Content=frame};
        OverlayStyle.Apply(bar);
        var add=new Button{Content="+ Ajouter",Padding=new Thickness(14,8,14,8),Margin=new Thickness(3)};
        add.Click+=(_,_)=>{var menu=new ContextMenu();foreach(var b in station.Layout.Blocks.Where(b=>!b.Visible)){var item=new MenuItem{Header=b.Title};item.Click+=(_,_)=>ShowBlock(b.Id);menu.Items.Add(item);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="Tous les blocs sont présents",IsEnabled=false});menu.PlacementTarget=add;menu.IsOpen=true;};
        var done=new Button{Content="Terminer",Padding=new Thickness(18,8,18,8),Margin=new Thickness(3)};done.Click+=(_,_)=>SetEditing(false);
        undoButton=OverlayStyle.Button("↶",()=>History(false));undoButton.ToolTip="Annuler · Ctrl+Z";
        redoButton=OverlayStyle.Button("↷",()=>History(true));redoButton.ToolTip="Rétablir · Ctrl+Y";
        row.Children.Add(add);row.Children.Add(undoButton);row.Children.Add(redoButton);
        linkDocksToggle=new CheckBox{Content="Lier les docks",IsChecked=station.Settings.LinkDocks,Foreground=Brush("#EFE4FF"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,12,0),ToolTip="Maj au début du geste inverse temporairement ce choix."};
        linkDocksToggle.Click+=(_,_)=>{
            EndGesture(false);
            try{station.ApplySettings(station.Settings with{LinkDocks=linkDocksToggle.IsChecked==true},station.TargetDate);}
            catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException){linkDocksToggle.IsChecked=station.Settings.LinkDocks;MessageBox.Show(e.Message,"Liaison des docks",MessageBoxButton.OK,MessageBoxImage.Information);}
        };row.Children.Add(linkDocksToggle);
        Button? layouts=null;layouts=OverlayStyle.Button("Scènes",()=>{
            var menu=new ContextMenu{PlacementTarget=layouts};
            foreach(string name in profiles.AllNames){
                var item=new MenuItem{Header=name,IsCheckable=true,IsChecked=profiles.Current==name};item.Click+=(_,_)=>SelectProfile(name);menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());var reset=new MenuItem{Header="Rétablir le modèle"};reset.Click+=(_,_)=>ResetSceneTemplate();menu.Items.Add(reset);
            menu.IsOpen=true;
        });row.Children.Add(layouts);row.Children.Add(OverlayStyle.Button("Sauver sous…",SaveUserProfile));
        row.Children.Add(OverlayStyle.Button("Réglages",ShowSettings));row.Children.Add(done);
        bar.PreviewKeyDown+=EditKey;UpdateHistory();
        bar.Closing+=(_,e)=>{if(!disposed){e.Cancel=true;SetEditing(false);}};
        return bar;
    }
    void CreateWindow(DesktopBlock block)
    {
        var surface=surfaces[block.Id];var grid=new System.Windows.Controls.Grid();
        // The aquarium water is its own layer under the drawings, so the shader runs
        // on the water alone and never touches the fish or the plants.
        if(surface is AquariumSurface tank)grid.Children.Add(tank.WaterLayer);
        // The diorama water is its own layer under the dock: the shader runs on the
        // water alone, and the block itself adds no panel over it.
        if(surface is OceanSurface ocean)grid.Children.Add(ocean.WaterLayer);
        grid.Children.Add(surface);
        var overlay=new Border{Background=Brush("#38251936"),BorderBrush=Brush("#DAC19BEA"),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(24),Cursor=Cursors.SizeAll,Visibility=Visibility.Collapsed};
        var header=new DockPanel{VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(14)};
        var remove=new Button{Content="×",Width=30,Height=30,ToolTip="Retirer ce bloc"};DockPanel.SetDock(remove,Dock.Right);remove.Click+=(_,_)=>HideBlock(block.Id);header.Children.Add(remove);
        header.Children.Add(new TextBlock{Text="⠿  "+block.Title,Foreground=Brush("#EFE4FF"),FontSize=17,VerticalAlignment=VerticalAlignment.Center});overlay.Child=header;grid.Children.Add(overlay);
        var window=new Window{Title="Battlestation · "+block.Title,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Width=block.Width,Height=block.Height,Left=block.X,Top=block.Y,Content=grid};
        var menu=new ContextMenu();menu.Opened+=(_,_)=>{
            menu.Items.Clear();var organize=new MenuItem{Header=editing?"Terminer":"Réorganiser"};organize.Click+=(_,_)=>SetEditing(!editing);menu.Items.Add(organize);
            if(block.Id=="terminal")
            {
                foreach(var (label,command) in new[]{("Coller","Paste"),("Onglet précédent","PreviousTab"),("Onglet suivant","NextTab"),("Nouvel onglet","NewShell")})
                {
                    var action=new MenuItem{Header=label};action.Click+=(_,_)=>station.Terminal?.Command(command);menu.Items.Add(action);
                }
                menu.Items.Add(new Separator());
            }
            var hide=new MenuItem{Header="Retirer ce bloc"};hide.Click+=(_,_)=>HideBlock(block.Id);menu.Items.Add(hide);
            var settings=new MenuItem{Header="Réglages"};settings.Click+=(_,_)=>ShowSettings();menu.Items.Add(settings);
            var add=new MenuItem{Header="Ajouter un bloc"};foreach(var missing in station.Layout.Blocks.Where(b=>!b.Visible)){var item=new MenuItem{Header=missing.Title};item.Click+=(_,_)=>ShowBlock(missing.Id);add.Items.Add(item);}add.IsEnabled=add.Items.Count>0;menu.Items.Add(add);
            menu.Items.Add(new Separator());
            var reload=new MenuItem{Header="Recharger"};reload.Click+=(_,_)=>Reload();menu.Items.Add(reload);
            var quit=new MenuItem{Header="Quitter"};quit.Click+=(_,_)=>app.Shutdown();menu.Items.Add(quit);
        };window.ContextMenu=menu;
        WireEdit(block.Id,overlay,header);
        window.Show();placement.Add(window);windows[block.Id]=window;overlays[block.Id]=overlay;
    }
    void ClearGlass(string id)
    {
        int[] slots=id switch{"clock"=>[0],"weather"=>[1],"apps"=>[2],"music"=>[3],"projects"=>[4,8],"terminal"=>[5],"hardware"=>[6],"usage"=>[7],"video"=>[9],"audio"=>[10],"reminders"=>[11],"bluetooth"=>[12],"dualsense"=>[13],"network"=>[14],"aquarium"=>[15],"lol"=>[16],"ocean"=>[17],"shopping"=>[18],_=>[]};
        foreach(int slot in slots)Native.BackgroundPanel(slot,0,0,0,0);
    }
    void Apply(string id,bool arrange=true,bool refresh=true)
    {
        renderRevisions.Remove(id);
        var block=station.Layout[id];var window=windows[id];var surface=surfaces[id];
        surface.SetDisplayed(block.Visible);
        window.Left=surface.DesktopX=block.X;window.Top=surface.DesktopY=block.Y;
        window.Width=surface.Width=block.Width;window.Height=surface.Height=block.Height;
        if(block.Visible){if(!window.IsVisible)window.Show();if(refresh)surface.Refresh();else surface.UpdateGlassBounds();}else{window.Hide();ClearGlass(id);}
        if(surface is VideoSurface video)video.SetActive(block.Visible,editing);
        if(surface is AudioSurface mixer)mixer.SetActive(block.Visible&&!editing);
        if(surface is DualSenseSurface controller)controller.SetActive(block.Visible&&!editing&&exposed.Contains(id));
        if(surface is NetworkSurface network)network.SetActive(block.Visible&&!editing&&exposed.Contains(id));
        if(surface is AquariumSurface tank)tank.SetActive(block.Visible&&!editing&&exposed.Contains(id));
        if(surface is OceanSurface diorama)diorama.SetActive(block.Visible&&!editing&&exposed.Contains(id));
        if(surface is LolSurface league)league.SetActive(block.Visible&&!editing&&exposed.Contains(id));
        if(id=="projects"){Native.DeskProjectsActive(block.Visible?1:0);station.Projects.Watch(station.ProjectRoot,block.Visible,name=>Native.DeskCommand("ProjectChanged:"+name));}
        if(id=="terminal")
        {
            int top=station.Terminal!.UseGlassTabs?60:12;
            station.Terminal.Place(0,(int)block.X+12,(int)block.Y+top,(int)block.Width-24,(int)block.Height-top-12);
            station.Terminal.SetVisible(block.Visible&&!editing);
        }
        if(arrange)placement.Arrange();
    }
    void ApplyAll(){foreach(string id in surfaces.Keys)Apply(id,false);placement.Arrange();}
    void HideBlock(string id){ClearEditHistory();station.Layout.SetVisible(id,false);Apply(id);station.Layout.Save();UpdateEditGrids();if(id=="music")((DeskSurface)surfaces[id]).Audio.Stop();}
    void ShowBlock(string id){ClearEditHistory();if(station.Layout.SetVisible(id,true)){Apply(id);station.Layout.Save();UpdateEditGrids();}}
    void Reset(){ClearEditHistory();station.Layout.Reset(station.Apps.Count);ApplyAll();station.Layout.Save();UpdateEditGrids();}
    void SetEditing(bool value)
    {
        EndGesture(false);
        if(value&&!editing)foreach(var dashboard in surfaces.Values.OfType<DashboardSurface>())dashboard.CloseDetails();
        editing=value;foreach(var overlay in overlays.Values)overlay.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        CompositionTarget.Rendering-=RenderGesture;if(value)CompositionTarget.Rendering+=RenderGesture;
        UpdateEditGrids();
        ((VideoSurface)surfaces["video"]).SetActive(station.Layout["video"].Visible,value);
        ((AudioSurface)surfaces["audio"]).SetActive(station.Layout["audio"].Visible&&!value);
        ((DualSenseSurface)surfaces["dualsense"]).SetActive(exposed.Contains("dualsense")&&!value);
        ((NetworkSurface)surfaces["network"]).SetActive(exposed.Contains("network")&&!value);
        ((AquariumSurface)surfaces["aquarium"]).SetActive(exposed.Contains("aquarium")&&!value);
        ((OceanSurface)surfaces["ocean"]).SetActive(exposed.Contains("ocean")&&!value);
        ((LolSurface)surfaces["lol"]).SetActive(exposed.Contains("lol")&&!value);
        station.Terminal?.SetVisible(station.Layout["terminal"].Visible&&!editing);
        if(value){var screen=station.Layout.AvailableScreens.Last();toolbar.Left=screen.Left+(screen.Width-toolbar.Width)/2;toolbar.Top=744;toolbar.Show();toolbar.Activate();}else{toolbar.Hide();station.Layout.Save();}
    }
    object Inspect()=>new{pid=Environment.ProcessId,mode="desktop",editing,profile=profiles.Current,scene=new{phase=scene.State,busy=scene.Busy,alpha=scene.GlassAlpha,applyMs=Math.Round(scene.LastApplyMilliseconds,1)},displays=new{connectedScreens,singleScreen=station.Layout.SingleScreen,profiles.ReturnScene,error=displayError},theme=DesktopTheme.Current.Id,terminalThemeSupported=station.Terminal?.ThemeSupported,rendering=new{monitorMask,exposed=exposed.ToArray(),counts=surfaces.ToDictionary(p=>p.Key,p=>p.Value.RenderCount)},layoutOptions=new{station.Settings.GridEnabled,station.Settings.GridStep,station.Settings.LinkDocks},terminalPid=station.Terminal?.Pid,palette=new{open=palette?.IsVisible==true,hotkeyRegistered=paletteHotkey.Registered,hotkeyError=paletteHotkey.Error},settingsOpen=settingsWindow?.IsVisible==true,weather=new{temperature=Native.Read("weatherTemp"),condition=Native.Read("weatherCondition"),at=Native.Read("weatherTime"),hours=Native.Read("weatherHours"),rain=Native.Read("weatherRain")},lol=((LolSurface)surfaces["lol"]).Inspect(),aquarium=((AquariumSurface)surfaces["aquarium"]).Inspect(),ocean=((OceanSurface)surfaces["ocean"]).Inspect(),blocks=station.Layout.Blocks,windows=placement.Inspect(),hits=surfaces.ToDictionary(p=>p.Key,p=>p.Value.InspectHits())};
    string Command(string command)
    {
        if(command=="inspect")return JsonSerializer.Serialize(Inspect());
        if(command=="terminal-tabs-inspect")return JsonSerializer.Serialize(station.Terminal?.InspectTabMetadata());
        if(command=="audio-inspect"){((AudioSurface)surfaces["audio"]).Mixer.Poll();return JsonSerializer.Serialize(new{mixer=((AudioSurface)surfaces["audio"]).Mixer,spectrum=new{((DeskSurface)surfaces["music"]).Audio.Running,((DeskSurface)surfaces["music"]).Audio.DeviceId,((DeskSurface)surfaces["music"]).Audio.Error,bands=((DeskSurface)surfaces["music"]).Audio.Bands},reactive=station.ReactiveAudio,intensity=station.AudioIntensity});}
        if(command=="video-inspect")return JsonSerializer.Serialize(((VideoSurface)surfaces["video"]).Inspect());
        if(command=="network-inspect")return JsonSerializer.Serialize(((NetworkSurface)surfaces["network"]).Inspect());
        if(command=="dualsense-inspect")return JsonSerializer.Serialize(((DualSenseSurface)surfaces["dualsense"]).Inspect());
        if(command=="bluetooth-inspect")return JsonSerializer.Serialize(((BluetoothSurface)surfaces["bluetooth"]).Inspect());
        if(command=="apps-inspect")return JsonSerializer.Serialize(((DockSurface)surfaces["apps"]).InspectActivity());
        if(command=="shopping-inspect")return JsonSerializer.Serialize(new
        {
            query=station.Shopping.Query,busy=station.Shopping.Busy,status=station.Shopping.Status,error=station.Shopping.Error,revision=station.Shopping.Revision,
            results=station.Shopping.Results.Select(row=>new{title=row.Title,shop=row.Shop,price=row.Price,url=row.Url,watched=row.Watched,
                verdict=row.Hit.Verdict.Label,decision=row.Hit.Verdict.Decision.ToString(),confidence=Math.Round(row.Hit.Verdict.Confidence,2),
                target=row.Hit.Verdict.TargetPrice,criteria=row.Hit.CriteriaText,reasons=row.Hit.Verdict.Reasons,image=row.ImagePath}),
            watchlist=station.Shopping.Watchlist.Select(row=>new{title=row.Title,shop=row.Shop,price=row.Price,target=row.Item.Watch.TargetPrice,
                nextCheck=row.Item.Watch.NextCheck,failures=row.Item.Watch.Failures,url=row.Product.Url})
        });
        if(command.StartsWith("shopping-search:"))
        {
            var request=command[16..].Trim();
            if(request.Length==0)throw new ArgumentException("Demande vide");
            _=station.Shopping.SearchAsync(request);
            return "OK";
        }
        if(command=="shopping-recheck"){_=station.Shopping.RecheckAsync();return "OK";}
        if(command.StartsWith("gpu-settings:"))
        {
            var parts=command.Split(':');
            if(parts.Length!=4||!int.TryParse(parts[1],out int manual)||manual is <0 or >1||!int.TryParse(parts[2],out int fan)||fan is <0 or >100||!int.TryParse(parts[3],out int thermal)||thermal is <0 or >100)throw new ArgumentException("Consignes GPU invalides");
            station.Command($"Apply:{manual}:{fan}:{thermal}");return "OK";
        }
        if(command=="video-start"){((VideoSurface)surfaces["video"]).StartMirror();return "OK";}
        if(command=="video-stop"){((VideoSurface)surfaces["video"]).StopMirror();return "OK";}
        if(command=="video-toggle"){((VideoSurface)surfaces["video"]).TogglePlayback();return "OK";}
        if(command.StartsWith("video-source:")){((VideoSurface)surfaces["video"]).Select(command[13..]);return "OK";}
        if(command=="palette"){TogglePalette();return "OK";}
        if(command=="settings"){ShowSettings();return "OK";}
        if(command=="close")
        {
            station.Terminal?.Detach();
            var close=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
            close.Tick+=(_,_)=>{close.Stop();app.Shutdown();};close.Start();return "OK";
        }
        if(command=="terminal-start"){ShowBlock("terminal");station.Terminal!.Start();return "OK";}
        if(command=="terminal-detach"){station.Terminal!.Detach();return "OK";}
        if(command=="organize:on"||command=="organize:off"){SetEditing(command.EndsWith("on"));return "OK";}
        if(command=="layout-reset"){ResetSceneTemplate();return "OK";}
        if(command.StartsWith("layout-profile:")){SelectProfile(command[15..]);return "OK";}
        if(command.StartsWith("layout-hide:")){HideBlock(command[12..]);return "OK";}
        if(command.StartsWith("layout-show:")){ShowBlock(command[12..]);return "OK";}
        if(command.StartsWith("layout-move:"))
        {
            var parts=command.Split(':');if(parts.Length!=4)throw new ArgumentException("Position invalide");
            if(!station.Layout.Move(parts[1],double.Parse(parts[2],System.Globalization.CultureInfo.InvariantCulture),double.Parse(parts[3],System.Globalization.CultureInfo.InvariantCulture)))return "FULL";
            Apply(parts[1]);station.Layout.Save();return "OK";
        }
        if(command.StartsWith("drawer:")&&int.TryParse(command[7..],out int drawer)&&drawer is >=1 and <=5){((DashboardSurface)surfaces[drawer<=2?"hardware":"usage"]).Toggle(drawer);return "OK";}
        if(command=="capture"){Native.BackgroundCapture();return "OK";}
        if(command.StartsWith("ocean-stir:"))
        {
            var parts=command.Split(':');
            if(parts.Length!=4||!double.TryParse(parts[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double x)
                ||!double.TryParse(parts[2],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double z)
                ||!double.TryParse(parts[3],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double amplitude))
                throw new ArgumentException("Perturbation invalide");
            ((OceanSurface)surfaces["ocean"]).Stir(x,z,amplitude);return "OK";
        }
        throw new InvalidOperationException("Commande inconnue");
    }
    void Tick()
    {
        if(ticks%4==0)UpdateVisibility();
        // App activity is independent of exposure: an occluded but visible dock still reports
        // real process presence. DockSurface throttles and runs the snapshot off the UI thread.
        if(station.Layout["apps"].Visible)((DockSurface)surfaces["apps"]).Poll();
        if(exposed.Contains("projects"))station.Projects.Poll(((DeskSurface)surfaces["projects"]).VisibleProjects());
        if(exposed.Contains("audio")&&!editing)((AudioSurface)surfaces["audio"]).Poll();
        var controller=(DualSenseSurface)surfaces["dualsense"];var bluetooth=(BluetoothSurface)surfaces["bluetooth"];
        bool batteryNeeded=exposed.Contains("dualsense")&&controller.Reader.Snapshot.State.Transport==2&&controller.Reader.Snapshot.State.BatteryPercent is null;
        if(!editing&&(exposed.Contains("bluetooth")||batteryNeeded))bluetooth.Poll(controllerOnly:!exposed.Contains("bluetooth"));
        if(batteryNeeded)controller.SetBluetoothBattery(bluetooth.ControllerBattery);else controller.SetBluetoothBattery(null);
        if(ticks%4==0&&exposed.Contains("network"))((NetworkSurface)surfaces["network"]).Poll();
        if(!editing&&exposed.Contains("lol"))((LolSurface)surfaces["lol"]).Poll();
        if(++ticks%4==0){if(station.Layout["music"].Visible){var revision=Native.DeskRevision(3);if(revision!=coverRevision){coverRevision=revision;station.RefreshCover();}}station.Terminal?.Update();}
        placement.SetExternalWindow(station.Terminal?.RemoteHandle??0,new WindowInteropHelper(windows["terminal"]).Handle);
        foreach(var pair in surfaces)
        {
            if(!exposed.Contains(pair.Key))continue;
            long revision=pair.Key switch{
                "clock" or "countdown"=>DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                "weather"=>(long)Native.DeskRevision(1),
                "music"=>(long)Native.DeskRevision(2),
                "projects"=>HashCode.Combine(Native.DeskRevision(0),station.Projects.Revision,DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60),
                "hardware"=>station.Backend.Revision(true),"usage"=>station.Backend.Revision(false),"reminders"=>station.Reminders.GetHashCode(),
                "terminal"=>HashCode.Combine(station.Terminal?.Revision,station.Terminal?.RemoteHandle,station.Terminal?.UseGlassTabs),
                "apps"=>station.Apps.GetHashCode(),_=>0};
            if(pair.Key is "audio" or "video" or "dualsense" or "network" or "aquarium" or "ocean" or "lol")continue;
            if(!renderRevisions.TryGetValue(pair.Key,out var previous)||revision!=previous){renderRevisions[pair.Key]=revision;pair.Value.Refresh();}
        }
        if(ticks%20==0)Battlestation.Core.DiagnosticFile.Write(Path.Combine(station.Data,"host-state.json"),JsonSerializer.Serialize(Inspect()));
    }
    void SessionSwitch(object sender,Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if(e.Reason is not (Microsoft.Win32.SessionSwitchReason.SessionLock or Microsoft.Win32.SessionSwitchReason.SessionUnlock))return;
        app.Dispatcher.BeginInvoke(()=>{if(disposed)return;visibility.Locked=e.Reason==Microsoft.Win32.SessionSwitchReason.SessionLock;UpdateVisibility();});
    }
    void UpdateVisibility()
    {
        visibility.Update(station.Terminal?.RemoteHandle??0,windows.Values.Select(w=>new WindowInteropHelper(w).Handle));monitorMask=visibility.MonitorMask&connectedMask;Native.BackgroundVisibility(monitorMask);
        foreach(var pair in surfaces){var block=station.Layout[pair.Key];bool active=block.Visible&&(connectedScreens>1||DesktopLayout.Screens[0].IntersectsWith(block.Bounds))&&visibility.Exposed(new Rect(windows[pair.Key].Left,windows[pair.Key].Top,windows[pair.Key].Width,windows[pair.Key].Height));
            if(active){if(exposed.Add(pair.Key))renderRevisions.Remove(pair.Key);}else exposed.Remove(pair.Key);
            pair.Value.Visibility=active?Visibility.Visible:Visibility.Hidden;
        }
        bool projects=exposed.Contains("projects");Native.DeskProjectsActive(projects?1:0);station.Projects.Watch(station.ProjectRoot,projects,name=>Native.DeskCommand("ProjectChanged:"+name));
        ((AudioSurface)surfaces["audio"]).SetActive(exposed.Contains("audio")&&!editing);
        ((DualSenseSurface)surfaces["dualsense"]).SetActive(exposed.Contains("dualsense")&&!editing);
        ((NetworkSurface)surfaces["network"]).SetActive(exposed.Contains("network")&&!editing);
        ((AquariumSurface)surfaces["aquarium"]).SetActive(exposed.Contains("aquarium")&&!editing);
        ((OceanSurface)surfaces["ocean"]).SetActive(exposed.Contains("ocean")&&!editing);
        ((LolSurface)surfaces["lol"]).SetActive(exposed.Contains("lol")&&!editing);
        ((VideoSurface)surfaces["video"]).SetOccluded(!exposed.Contains("video"));
        bool listen=station.ReactiveAudio&&monitorMask!=0||exposed.Contains("music")&&Native.Read("playing")=="1";
        if(listen){if(!audio.IsEnabled)audio.Start();}else{audio.Stop();((DeskSurface)surfaces["music"]).Audio.Stop();Native.BackgroundAudio(0,0,0,0);}
    }
    public void Dispose()
    {
        EndGesture(false);CompositionTarget.Rendering-=RenderGesture;foreach(var grid in editGrids)grid.Close();
        scene.Dispose();
        disposed=true;displayChange?.Stop();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplaySettingsChanged;placement.VisibilityChanged-=UpdateVisibility;Microsoft.Win32.SystemEvents.SessionSwitch-=SessionSwitch;control.Dispose();timer.Stop();audio.Stop();palette?.Dismiss(false);settingsWindow?.Close();paletteHotkey.Dispose();tray.Dispose();toolbar.Close();
        placement.Dispose();((DockSurface)surfaces["apps"]).Dispose();foreach(var desk in surfaces.Values.OfType<DeskSurface>())desk.Audio.Dispose();
        ((VideoSurface)surfaces["video"]).Dispose();
        ((AudioSurface)surfaces["audio"]).Dispose();((BluetoothSurface)surfaces["bluetooth"]).Dispose();
        ((DualSenseSurface)surfaces["dualsense"]).Dispose();((NetworkSurface)surfaces["network"]).Dispose();
        ((AquariumSurface)surfaces["aquarium"]).Dispose();((OceanSurface)surfaces["ocean"]).Dispose();((LolSurface)surfaces["lol"]).Dispose();
        mediaClipboard.Dispose();reserveWindow?.Close();
        Native.BackgroundStop();station.Terminal?.Detach();
    }
}
