using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Battlestation;
internal sealed partial class DesktopWorkspace
{
    readonly List<Window> editGrids=[];
    readonly Stack<DesktopBlock[]> undo=[],redo=[];
    Button undoButton=null!,redoButton=null!;
    CheckBox? linkDocksToggle;
    LayoutGesture? gesture;
    Border? gestureCapture;
    Point? pendingPointer;
    bool? lastGestureBlocked;
    void CreateEditGrids()
    {
        foreach(var screen in DesktopLayout.Screens)
        {
            var grid=new Window{Title="Battlestation · Grille",Left=screen.X,Top=screen.Y,Width=screen.Width,Height=screen.Height,
                WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,
                ShowInTaskbar=false,ShowActivated=false,IsHitTestVisible=false,Content=new EditGrid(station.Settings.GridStep)};
            new WindowInteropHelper(grid).EnsureHandle();var handle=new WindowInteropHelper(grid).Handle;
            Native.SetWindowLongPtr(handle,-20,Native.GetWindowLongPtr(handle,-20)|0x20|0x08000000);
            placement.Add(grid);editGrids.Add(grid);
        }
        station.SettingsChanged+=()=>UpdateEditGrids();
    }
    void UpdateEditGrids(bool arrange=true)
    {
        if(linkDocksToggle is not null)linkDocksToggle.IsChecked=station.Settings.LinkDocks;
        foreach(var window in editGrids)
        {
            var drawing=(EditGrid)window.Content;drawing.Step=station.Settings.GridStep;
            drawing.Occupied=station.Layout.Blocks.Where(b=>b.Visible).Select(b=>new Rect(b.X-window.Left,b.Y-window.Top,b.Width,b.Height)).ToArray();drawing.InvalidateVisual();
            if(editing&&station.Settings.GridEnabled){if(!window.IsVisible)window.Show();}else if(window.IsVisible)window.Hide();
        }
        if(arrange)placement.Arrange();
    }
    static LayoutEdge Handle(Point p,double w,double h)
    {
        const double margin=14;
        var edge=p.X<margin?LayoutEdge.Left:p.X>w-margin?LayoutEdge.Right:LayoutEdge.Move;
        edge|=p.Y<margin?LayoutEdge.Top:p.Y>h-margin?LayoutEdge.Bottom:LayoutEdge.Move;return edge;
    }
    static Cursor HandleCursor(LayoutEdge edge)=>edge switch
    {
        LayoutEdge.Left or LayoutEdge.Right=>Cursors.SizeWE,
        LayoutEdge.Top or LayoutEdge.Bottom=>Cursors.SizeNS,
        LayoutEdge.Left|LayoutEdge.Top or LayoutEdge.Right|LayoutEdge.Bottom=>Cursors.SizeNWSE,
        LayoutEdge.Right|LayoutEdge.Top or LayoutEdge.Left|LayoutEdge.Bottom=>Cursors.SizeNESW,
        _=>Cursors.SizeAll
    };
    Point DesktopPointer(Border overlay)
    {
        // Mouse.GetPosition is already in WPF units, including during capture.
        var window=Window.GetWindow(overlay)!;var local=Mouse.GetPosition(window);
        return new(window.Left+local.X,window.Top+local.Y);
    }
    void WireEdit(string id,Border overlay,DockPanel header)
    {
        EditOverlay.Mount(overlay,header);
        overlay.MouseLeftButtonDown+=(_,e)=>
        {
            if(!editing||e.OriginalSource is Button)return;
            EndGesture(false);var p=e.GetPosition(overlay);
            bool linked=station.Settings.LinkDocks^Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            gesture=new(station.Layout.Blocks,id,Handle(p,overlay.ActualWidth,overlay.ActualHeight),DesktopPointer(overlay),station.Settings.GridEnabled,station.Settings.GridStep,linked);
            lastGestureBlocked=null;
            gestureCapture=overlay;toolbar.Activate();if(!overlay.CaptureMouse()){gesture=null;gestureCapture=null;return;}
            e.Handled=true;
        };
        overlay.MouseMove+=(_,e)=>
        {
            if(gestureCapture==overlay&&gesture is not null){pendingPointer=DesktopPointer(overlay);e.Handled=true;}
            else overlay.Cursor=HandleCursor(Handle(e.GetPosition(overlay),overlay.ActualWidth,overlay.ActualHeight));
        };
        overlay.MouseLeftButtonUp+=(_,e)=>{if(gestureCapture!=overlay)return;pendingPointer=DesktopPointer(overlay);RenderGesture(null,EventArgs.Empty);EndGesture(true);e.Handled=true;};
        overlay.LostMouseCapture+=(_,_)=>{if(gestureCapture==overlay)EndGesture(false);};
    }
    void RenderGesture(object? sender,EventArgs args)
    {
        if(gesture is null||pendingPointer is not {} pointer)return;pendingPointer=null;
        var preview=gesture.Preview(pointer);
        var old=station.Layout.Blocks.ToArray();
        bool changed=!preview.Blocks.SequenceEqual(old);
        if(changed&&station.Layout.Restore(preview.Blocks))
        {
            foreach(var block in station.Layout.Blocks)
            {
                var previous=old.Single(b=>b.Id==block.Id);
                if(block!=previous)Apply(block.Id,false,block.Width!=previous.Width||block.Height!=previous.Height);
            }
            UpdateEditGrids(false);
        }
        if(changed||lastGestureBlocked!=preview.Blocked)
        {
            lastGestureBlocked=preview.Blocked;
            foreach(var (id,overlay) in overlays)overlay.BorderBrush=Brush(preview.Affected.Contains(id)?preview.Blocked?"#FFF0B77E":"#FFF1DCFF":"#706F6082");
        }
    }
    void EndGesture(bool commit)
    {
        var active=gesture;if(active is null)return;
        gesture=null;pendingPointer=null;lastGestureBlocked=null;var capture=gestureCapture;gestureCapture=null;capture?.ReleaseMouseCapture();
        if(commit)
        {
            if(!active.Start.SequenceEqual(station.Layout.Blocks)){undo.Push(active.Start.ToArray());redo.Clear();station.Layout.Save();}
        }
        else{station.Layout.Restore(active.Start);ApplyAll();UpdateEditGrids();}
        foreach(var overlay in overlays.Values)overlay.BorderBrush=Brush("#DAC19BEA");UpdateHistory();
    }
    void History(bool forward)
    {
        EndGesture(false);var source=forward?redo:undo;var destination=forward?undo:redo;if(source.Count==0)return;
        var before=station.Layout.Blocks.ToArray();if(station.Layout.Restore(source.Peek())){source.Pop();destination.Push(before);ApplyAll();station.Layout.Save();UpdateEditGrids();}UpdateHistory();
    }
    void ClearEditHistory(){EndGesture(false);undo.Clear();redo.Clear();UpdateHistory();}
    void UpdateHistory(){if(undoButton is null)return;undoButton.IsEnabled=undo.Count>0;redoButton.IsEnabled=redo.Count>0;}
    void EditKey(object sender,KeyEventArgs e)
    {
        if(!editing)return;
        if(e.Key==Key.Escape){if(gesture is not null)EndGesture(false);else SetEditing(false);e.Handled=true;}
        else if(Keyboard.Modifiers==ModifierKeys.Control&&(e.Key==Key.Z||e.Key==Key.Y)){History(e.Key==Key.Y);e.Handled=true;}
    }
}
