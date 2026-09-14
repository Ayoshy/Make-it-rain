using System.Runtime.InteropServices;
using System.Windows;

namespace Battlestation;
// A captured player must remain drawable. A minimized source is restored into
// the bottom of the window stack without activation. Release restores the user's
// minimized state, unless they have explicitly returned to the player meanwhile.
// Stremio's backing window can temporarily fit behind the dock to keep its GPU
// presentation alive. Original geometry is restored on release or return.
// No reparenting, subclassing, key forwarding or player termination.
internal sealed class VideoWindowLease : IDisposable
{
    readonly VideoSource source;
    readonly bool topmost;
    Native.WindowRect original;
    nint frame;
    nint lastForeground;
    long revealAfter;
    Rect dock;
    bool adopted,restoredMinimized,disposed,parked,wantsDock;
    [StructLayout(LayoutKind.Sequential)] struct Placement {public int Length,Flags,Show;public Native.CursorPoint Min,Max;public Native.WindowRect Normal;}
    [DllImport("user32.dll")] static extern bool GetWindowPlacement(nint hwnd,ref Placement placement);
    [DllImport("user32.dll")] static extern bool SetWindowPlacement(nint hwnd,ref Placement placement);
    public bool Maintaining=>adopted;
    public VideoWindowLease(VideoSource value)
    {
        source=value;topmost=(Native.GetWindowLongPtr(value.Handle,-20)&8)!=0;
        Native.GetWindowRect(value.Handle,out original);
        if(VideoSources.IsIconic(value.Handle)){var placement=new Placement{Length=Marshal.SizeOf<Placement>()};if(GetWindowPlacement(value.Handle,ref placement))original=placement.Normal;}
    }
    bool Valid()
    {
        if(!Native.IsWindow(source.Handle))return false;
        Native.GetWindowThreadProcessId(source.Handle,out uint pid);return pid==source.Pid;
    }
    public void Adopt()
    {
        if(adopted||disposed||!Valid())return;
        if(source.Kind=="youtube"&&Native.GetForegroundWindow()==source.Handle)return;
        adopted=true;
        // The PiP becomes the dock's backing window. Returning or removing the
        // dock gives it back its original floating position and topmost state.
        if(source.Kind=="youtube")Park();
    }
    public bool Prepare()
    {
        if(disposed||!Valid()||!Native.IsWindowVisible(source.Handle))return false;
        ProtectFocusTransition();
        adopted=true;wantsDock=frame!=0;
        if(VideoSources.IsIconic(source.Handle))RestoreBehindDesktop();
        else if(wantsDock)Park();
        return !VideoSources.IsIconic(source.Handle);
    }
    public void ProtectFocusTransition(){lastForeground=Native.GetForegroundWindow();revealAfter=Environment.TickCount64+750;}
    public void Place(nint frameHandle,Rect rectangle)
    {
        frame=frameHandle;dock=rectangle;
        if(adopted&&wantsDock)Park();
    }
    public void ParkForMirror(){if(!adopted||disposed||!Valid()||Native.GetForegroundWindow()==source.Handle)return;wantsDock=true;Park();}
    void Park()
    {
        if(!Valid())return;
        if(frame==0){Native.SetWindowPos(source.Handle,1,0,0,0,0,0x613);return;}
        if(!Native.IsWindow(frame)||!Native.IsWindowVisible(frame))return;
        Native.GetWindowRect(source.Handle,out var current);
        if(current.Left==(int)dock.X&&current.Top==(int)dock.Y&&current.Right-current.Left==(int)dock.Width&&current.Bottom-current.Top==(int)dock.Height&&Native.GetWindow(frame,2)==source.Handle)return;
        parked=true;
        // A per-pixel-alpha WPF frame does not mark the backing PiP as an
        // opaque occlusion in Chromium. Keep the PiP under that frame, rather
        // than under Explorer, so Chromium continues producing video frames.
        Native.SetWindowPos(source.Handle,frame,(int)dock.X,(int)dock.Y,(int)dock.Width,(int)dock.Height,0x610);
    }
    void RestoreBehindDesktop()
    {
        restoredMinimized=true;wantsDock=frame!=0;
        if(wantsDock)
        {
            // Change the restore rectangle while the window is still minimized.
            // SW_SHOWNOACTIVATE will then show the compact backing window, never
            // a full-size Stremio window for one frame during a source switch.
            var placement=new Placement{Length=Marshal.SizeOf<Placement>()};
            if(GetWindowPlacement(source.Handle,ref placement))
            {
                placement.Flags=0;placement.Show=7;
                placement.Normal=new Native.WindowRect{Left=(int)dock.Left,Top=(int)dock.Top,Right=(int)dock.Right,Bottom=(int)dock.Bottom};
                SetWindowPlacement(source.Handle,ref placement);
            }
        }
        Native.SetWindowPos(source.Handle,1,0,0,0,0,0x613);
        Native.ShowWindow(source.Handle,4);
        Native.SetWindowPos(source.Handle,1,0,0,0,0,0x613);
        if(wantsDock||source.Kind=="youtube")Park();
    }
    public void Maintain()
    {
        if(!adopted||disposed||!Valid()||!Native.IsWindowVisible(source.Handle))return;
        var foreground=Native.GetForegroundWindow();
        bool returned=foreground==source.Handle&&lastForeground!=source.Handle&&Environment.TickCount64>=revealAfter;
        lastForeground=foreground;
        // A later, separate taskbar/Alt+Tab activation still returns the real
        // player. Focus notifications from a source switch or transport invoke
        // do not turn that switch into an implicit Reveal action.
        if(returned&&parked){restoredMinimized=false;RestoreGeometry(true);parked=false;wantsDock=false;}
        if(foreground==source.Handle&&!wantsDock)
        {
            restoredMinimized=false;
            if(parked){RestoreGeometry();parked=false;wantsDock=false;}
            else if(!VideoSources.IsIconic(source.Handle))Native.GetWindowRect(source.Handle,out original);
            return;
        }
        if(VideoSources.IsIconic(source.Handle))
        {
            RestoreBehindDesktop();
        }
        else if(wantsDock||source.Kind=="youtube")Park();
    }
    public void Reveal()
    {
        restoredMinimized=false;if(parked){RestoreGeometry(true);parked=false;}wantsDock=false;
        if(!Valid())return;
        if(VideoSources.IsIconic(source.Handle))Native.ShowWindow(source.Handle,9);
        Native.SetForegroundWindow(source.Handle);
    }
    void RestoreGeometry(bool reveal=false)=>Native.SetWindowPos(source.Handle,reveal?(topmost?-1:0):1,original.Left,original.Top,original.Right-original.Left,original.Bottom-original.Top,0x610);
    void Release()
    {
        if(disposed)return;disposed=true;
        if(!adopted||!Valid())return;
        if(restoredMinimized&&Native.IsWindowVisible(source.Handle)&&Native.GetForegroundWindow()!=source.Handle)
        {
            var placement=new Placement{Length=Marshal.SizeOf<Placement>()};
            if(GetWindowPlacement(source.Handle,ref placement)){placement.Show=7;placement.Normal=original;SetWindowPlacement(source.Handle,ref placement);}
        }
        else if(parked)RestoreGeometry();
        if(topmost)Native.SetWindowPos(source.Handle,-1,0,0,0,0,0x613);
    }
    public void Dispose()=>Release();
}
