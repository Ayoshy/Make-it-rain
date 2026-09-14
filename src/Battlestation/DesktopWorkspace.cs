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
    int ticks;
    public DesktopWorkspace(Application application,Station state)
    {
        app=application;station=state;station.Terminal=new TerminalSession(station);
        surfaces=new(){["clock"]=new DeskSurface(station,DeskWidget.Clock),["weather"]=new DeskSurface(station,DeskWidget.Weather),
            ["apps"]=new DockSurface(station),["music"]=new DeskSurface(station,DeskWidget.Music),["projects"]=new DeskSurface(station,DeskWidget.Projects),
            ["terminal"]=new TerminalSurface(station),["countdown"]=new CountdownSurface(station),
            ["hardware"]=new DashboardSurface(station,true),["usage"]=new DashboardSurface(station,false)};
        placement=new DesktopPlacement(app.Dispatcher);
        Native.BackgroundStart(Native.DesktopParent(),Path.Combine(station.Assets,"Images"));
        Native.BackgroundAppearance(station.Settings.AnimateBackground?1:0,(float)station.Settings.GlassOpacity);
        foreach(var block in station.Layout.Blocks)CreateWindow(block);
        foreach(string id in new[]{"hardware","usage"})
            ((DashboardSurface)surfaces[id]).ResizeRequested=height=>{
                ((DashboardSurface)surfaces[id]).RequestedHeight=height;
                Apply(id);if(height>station.Layout[id].Height){placement.RaiseWithinDesktop(windows[id]);Native.BackgroundPanelFront(id=="hardware"?6:7);}return true;
            };
        station.DockChanged+=()=>{if(station.Layout.Resize("apps",DesktopLayout.DockHeight(station.Apps.Count))){Apply("apps");station.Layout.Save();}};
        toolbar=CreateToolbar();
        tray=new Forms.NotifyIcon{Text="Battlestation",Icon=System.Drawing.SystemIcons.Application,Visible=true};
        var menu=new Forms.ContextMenuStrip();
        InitializeCommands();
        menu.Items.Add("Palette · Ctrl+Espace",null,(_,_)=>TogglePalette());
        menu.Items.Add("Réglages",null,(_,_)=>ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Réorganiser",null,(_,_)=>SetEditing(!editing));
        var add=new Forms.ToolStripMenuItem("Ajouter un bloc");
        add.DropDownOpening+=(_,_)=>{add.DropDownItems.Clear();foreach(var b in station.Layout.Blocks.Where(b=>!b.Visible))add.DropDownItems.Add(b.Title,null,(_,_)=>ShowBlock(b.Id));};
        menu.Items.Add(add);menu.Items.Add("Disposition initiale",null,(_,_)=>Reset());
        menu.Items.Add("Quitter",null,(_,_)=>app.Shutdown());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>SetEditing(!editing);
        ApplyAll();
        station.Terminal.HeaderChanged+=()=>Apply("terminal");
        timer=new DispatcherTimer(TimeSpan.FromMilliseconds(250),DispatcherPriority.Background,(_,_)=>Tick(),app.Dispatcher);
        audio=new DispatcherTimer(TimeSpan.FromMilliseconds(25),DispatcherPriority.Render,(_,_)=>{if(station.Layout["music"].Visible)((DeskSurface)surfaces["music"]).TickAudio();},app.Dispatcher);
        control=new ControlPipe(app.Dispatcher,Command);Tick();
    }
    static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    Window CreateToolbar()
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10)};
        var bar=new Window{Title="Battlestation · Réorganiser",Width=360,Height=64,Left=3460,Top=744,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Background=Brush("#292035"),Content=row};
        var add=new Button{Content="+ Ajouter",Padding=new Thickness(14,8,14,8),Margin=new Thickness(3)};
        add.Click+=(_,_)=>{var menu=new ContextMenu();foreach(var b in station.Layout.Blocks.Where(b=>!b.Visible)){var item=new MenuItem{Header=b.Title};item.Click+=(_,_)=>ShowBlock(b.Id);menu.Items.Add(item);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="Tous les blocs sont présents",IsEnabled=false});menu.PlacementTarget=add;menu.IsOpen=true;};
        var done=new Button{Content="Terminer",Padding=new Thickness(18,8,18,8),Margin=new Thickness(3)};done.Click+=(_,_)=>SetEditing(false);
        row.Children.Add(add);row.Children.Add(done);
        bar.Closing+=(_,e)=>{if(!disposed){e.Cancel=true;SetEditing(false);}};
        return bar;
    }
    void CreateWindow(DesktopBlock block)
    {
        var surface=surfaces[block.Id];var grid=new System.Windows.Controls.Grid();grid.Children.Add(surface);
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
        };window.ContextMenu=menu;
        Point grab=default;bool dragging=false;
        overlay.MouseLeftButtonDown+=(_,e)=>{if(!editing)return;Native.GetCursorPos(out var cursor);var b=station.Layout[block.Id];grab=new(cursor.X-b.X,cursor.Y-b.Y);dragging=overlay.CaptureMouse();e.Handled=true;};
        overlay.MouseMove+=(_,e)=>{if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;Native.GetCursorPos(out var cursor);if(station.Layout.Move(block.Id,cursor.X-grab.X,cursor.Y-grab.Y))Apply(block.Id);e.Handled=true;};
        overlay.MouseLeftButtonUp+=(_,e)=>{if(!dragging)return;dragging=false;overlay.ReleaseMouseCapture();station.Layout.Save();e.Handled=true;};
        overlay.LostMouseCapture+=(_,_)=>{if(dragging){dragging=false;station.Layout.Save();}};
        window.Show();placement.Add(window);windows[block.Id]=window;overlays[block.Id]=overlay;
    }
    void ClearGlass(string id)
    {
        int[] slots=id switch{"clock"=>[0],"weather"=>[1],"apps"=>[2],"music"=>[3],"projects"=>[4,8],"terminal"=>[5],"hardware"=>[6],"usage"=>[7],_=>[]};
        foreach(int slot in slots)Native.BackgroundPanel(slot,0,0,0,0);
    }
    void Apply(string id)
    {
        var block=station.Layout[id];var window=windows[id];var surface=surfaces[id];
        window.Left=surface.DesktopX=block.X;window.Top=surface.DesktopY=block.Y;
        window.Width=surface.Width=block.Width;window.Height=surface.Height=block.Height;
        if(surface is DashboardSurface dashboard)
        {
            var bounds=DashboardBounds.Expand(block.Bounds,dashboard.RequestedHeight,id=="usage");
            dashboard.DetailsAbove=bounds.Top<block.Y;
            window.Top=surface.DesktopY=bounds.Top;window.Height=surface.Height=bounds.Height;
        }
        if(block.Visible){if(!window.IsVisible)window.Show();surface.Refresh();}else{window.Hide();ClearGlass(id);}
        if(id=="terminal")
        {
            int top=station.Terminal!.UseGlassTabs?60:12;
            station.Terminal.Place(0,(int)block.X+12,(int)block.Y+top,(int)block.Width-24,(int)block.Height-top-12);
            station.Terminal.SetVisible(block.Visible&&!editing);
        }
        placement.Arrange();
    }
    void ApplyAll(){foreach(string id in surfaces.Keys)Apply(id);}
    void HideBlock(string id){station.Layout.SetVisible(id,false);Apply(id);station.Layout.Save();if(id=="music")((DeskSurface)surfaces[id]).Audio.Stop();}
    void ShowBlock(string id){if(station.Layout.SetVisible(id,true)){Apply(id);station.Layout.Save();}}
    void Reset(){station.Layout.Reset(station.Apps.Count);ApplyAll();station.Layout.Save();}
    void SetEditing(bool value)
    {
        editing=value;foreach(var overlay in overlays.Values)overlay.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        station.Terminal?.SetVisible(station.Layout["terminal"].Visible&&!editing);
        if(value)toolbar.Show();else{toolbar.Hide();station.Layout.Save();}
    }
    object Inspect()=>new{pid=Environment.ProcessId,mode="desktop",editing,terminalPid=station.Terminal?.Pid,palette=new{open=palette?.IsVisible==true,hotkeyRegistered=paletteHotkey.Registered,hotkeyError=paletteHotkey.Error},settingsOpen=settingsWindow?.IsVisible==true,weather=new{temperature=Native.Read("weatherTemp"),condition=Native.Read("weatherCondition"),at=Native.Read("weatherTime")},blocks=station.Layout.Blocks,windows=placement.Inspect(),hits=surfaces.ToDictionary(p=>p.Key,p=>p.Value.InspectHits())};
    string Command(string command)
    {
        if(command=="inspect")return JsonSerializer.Serialize(Inspect());
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
        if(command=="layout-reset"){Reset();return "OK";}
        if(command.StartsWith("layout-hide:")){HideBlock(command[12..]);return "OK";}
        if(command.StartsWith("layout-show:")){ShowBlock(command[12..]);return "OK";}
        if(command.StartsWith("layout-move:"))
        {
            var parts=command.Split(':');if(parts.Length!=4)throw new ArgumentException("Position invalide");
            if(!station.Layout.Move(parts[1],double.Parse(parts[2],System.Globalization.CultureInfo.InvariantCulture),double.Parse(parts[3],System.Globalization.CultureInfo.InvariantCulture)))return "FULL";
            Apply(parts[1]);station.Layout.Save();return "OK";
        }
        if(command.StartsWith("drawer:")&&int.TryParse(command[7..],out int drawer)&&drawer is >=1 and <=4){((DashboardSurface)surfaces[drawer<=2?"hardware":"usage"]).Toggle(drawer);return "OK";}
        if(command=="capture"){Native.BackgroundCapture();return "OK";}
        throw new InvalidOperationException("Commande inconnue");
    }
    void Tick()
    {
        if(++ticks%4==0){station.RefreshCover();station.Terminal?.Update();}
        placement.SetExternalWindow(station.Terminal?.RemoteHandle??0,new WindowInteropHelper(windows["terminal"]).Handle);
        foreach(var pair in surfaces)if(station.Layout[pair.Key].Visible)pair.Value.Refresh();
        if(ticks%20==0)File.WriteAllText(Path.Combine(station.Data,"host-state.json"),JsonSerializer.Serialize(Inspect()));
    }
    public void Dispose()
    {
        disposed=true;control.Dispose();timer.Stop();audio.Stop();palette?.Dismiss(false);settingsWindow?.Close();paletteHotkey.Dispose();tray.Dispose();toolbar.Close();
        placement.Dispose();foreach(var desk in surfaces.Values.OfType<DeskSurface>())desk.Audio.Dispose();
        Native.BackgroundStop();station.Terminal?.Detach();
    }
}
