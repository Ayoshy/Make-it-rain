using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Battlestation;
/// <summary>Keeps interactive top-level surfaces in the desktop layer without reparenting WPF into Explorer.</summary>
internal sealed class DesktopPlacement : IDisposable
{
    readonly List<nint> handles=[];
    readonly HashSet<nint> interactive=[];
    readonly List<HwndSource> sources=[];
    readonly Native.WinEvent callback;
    readonly Native.SubclassProc nativeCallback;
    readonly List<nint> nativeHandles=[];
    readonly Dispatcher dispatcher;
    readonly nint hook;
    readonly nint desktopReference;
    readonly DispatcherTimer desktopTimer;
    nint externalWindow,externalFrame;
    bool? desktopRaised;
    bool arranging,disposed;
    public DesktopPlacement(Dispatcher ui)
    {
        dispatcher=ui;callback=OnForeground;nativeCallback=NativeMessage;
        // Keep an unmoving reference in the normal desktop band. On Windows 11
        // Show Desktop can move Progman above this band without WS_EX_TOPMOST.
        desktopReference=Native.CreateWindowEx(0x08000080,"STATIC","Battlestation desktop reference",0x80000000,0,0,0,0,0,0,0,0);
        if(desktopReference==0)throw new InvalidOperationException("Impossible de créer la référence du bureau.");
        Native.SetWindowPos(desktopReference,1,0,0,0,0,0x613);
        Native.SetWindowSubclass(desktopReference,nativeCallback,1,0);
        hook=Native.SetWinEventHook(3,3,0,callback,0,0,0);
        // Explorer can finish raising the desktop after the foreground event.
        // Also check while the terminal owns focus: focus is not desktop state.
        desktopTimer=new DispatcherTimer(TimeSpan.FromMilliseconds(100),DispatcherPriority.Background,(_,_)=>Arrange(),ui);
    }
    public void Add(Window window,bool acceptsFocus=false)
    {
        var handle=new WindowInteropHelper(window).Handle;
        var source=HwndSource.FromHwnd(handle);source.AddHook(WindowMessage);sources.Add(source);handles.Add(handle);
        if(acceptsFocus)interactive.Add(handle);
        var style=Native.GetWindowLongPtr(handle,-20);Native.SetWindowLongPtr(handle,-20,(style|0x80)&~0x40000);
        desktopRaised=null;Arrange();
    }
    public void AddNative(nint handle){if(handle!=0&&!handles.Contains(handle)){handles.Add(handle);nativeHandles.Add(handle);Native.SetWindowSubclass(handle,nativeCallback,1,0);desktopRaised=null;Arrange();}}
    // The stable terminal may run an older build. Only place its existing HWND;
    // never subclass across processes, change its parent, or restart its sessions.
    public void SetExternalWindow(nint handle,nint frame=default)
    {
        if(externalWindow==handle&&externalFrame==frame)return;
        externalWindow=handle;externalFrame=frame;desktopRaised=null;Arrange();
    }
    IEnumerable<nint> Windows=>handles.Append(externalWindow).Where(h=>h!=0&&Native.IsWindow(h)).Distinct();
    nint NativeMessage(nint hwnd,uint msg,nint w,nint l,nuint id,nuint data)
    {
        bool handled=false;if(msg!=0x21)WindowMessage(hwnd,(int)msg,w,l,ref handled);
        return handled?0:Native.DefSubclassProc(hwnd,msg,w,l);
    }
    nint WindowMessage(nint hwnd,int msg,nint w,nint l,ref bool handled)
    {
        if(msg==0x21&&!interactive.Contains(hwnd)){handled=true;return 3;}
        if(msg==0x46&&!arranging)
        {
            var position=Marshal.PtrToStructure<Native.WindowPos>(l);position.Flags|=0x4;Marshal.StructureToPtr(position,l,false);
        }
        if(msg==0x112&&((long)w&0xfff0)==0xf020){handled=true;return 0;}
        return 0;
    }
    void OnForeground(nint hook,uint ev,nint hwnd,int objectId,int child,uint thread,uint time)
    {
        if(disposed)return;
        dispatcher.BeginInvoke(()=>{if(!disposed)Arrange();},DispatcherPriority.Background);
    }
    public void Arrange()=>Arrange(Native.DesktopIconsHost());
    internal void RaiseWithinDesktop(Window window)
    {
        Arrange();var target=new WindowInteropHelper(window).Handle;
        var highest=Windows.Where(h=>h!=target&&Native.IsWindowVisible(h)).Aggregate((nint)0,(first,h)=>first==0||Above(h,first)?h:first);
        if(highest==0||Above(target,highest))return;
        var previous=Native.GetWindow(highest,3);
        arranging=true;try{Native.SetWindowPos(target,previous,0,0,0,0,0x613);}finally{arranging=false;}
    }
    internal void Arrange(nint desktopHost)
    {
        if(disposed||arranging||desktopHost==0)return;
        // The shell's position relative to a fixed reference is the desktop state;
        // its topmost style and keyboard focus do not reliably describe Win+D.
        bool showingDesktop=Native.IsWindowVisible(desktopHost)&&Above(desktopHost,desktopReference);
        bool changed=desktopRaised!=showingDesktop;
        desktopRaised=showingDesktop;
        arranging=true;
        try
        {
            foreach(var hwnd in Windows)
            {
                bool topmost=(Native.GetWindowLongPtr(hwnd,-20)&0x8)!=0;
                // Avoid continuously reordering windows or stealing focus.
                // Repair an older terminal host that demotes itself on activation.
                bool belowDesktop=false;
                if(showingDesktop&&topmost)
                    for(var previous=Native.GetWindow(hwnd,3);previous!=0;previous=Native.GetWindow(previous,3))
                        if(previous==desktopHost){belowDesktop=true;break;}
                if(changed||topmost!=showingDesktop||belowDesktop)
                    Native.SetWindowPos(hwnd,showingDesktop?-1:1,0,0,0,0,0x1|0x2|0x10|0x200|0x400);
            }
            // The layered WPF frame covers the entire terminal rectangle, even
            // where its fill looks transparent. Keep the native input surface
            // immediately above that frame, while remaining below ordinary apps.
            // Repair this independently of shell transitions: the stable host
            // can move itself to the bottom after a layout/focus change.
            if(Native.IsWindow(externalWindow)&&Native.IsWindow(externalFrame)&&
                Native.IsWindowVisible(externalWindow)&&Native.IsWindowVisible(externalFrame)&&
                !Above(externalWindow,externalFrame))
            {
                var previous=Native.GetWindow(externalFrame,3);
                if(!showingDesktop&&previous!=0&&(Native.GetWindowLongPtr(previous,-20)&8)!=0)previous=0;
                Native.SetWindowPos(externalWindow,previous,0,0,0,0,0x613);
            }
        }
        finally{arranging=false;}
    }
    static bool Above(nint window,nint other)
    {
        for(var previous=Native.GetWindow(other,3);previous!=0;previous=Native.GetWindow(previous,3))
            if(previous==window)return true;
        return false;
    }
    public object Inspect()=>Windows.Select(h=>{Native.GetWindowRect(h,out var r);return new{hwnd=(long)h,style=(long)Native.GetWindowLongPtr(h,-16),exStyle=(long)Native.GetWindowLongPtr(h,-20),x=r.Left,y=r.Top,width=r.Right-r.Left,height=r.Bottom-r.Top};}).ToArray();
    public void Dispose()
    {
        disposed=true;desktopTimer.Stop();if(hook!=0)Native.UnhookWinEvent(hook);
        // A detached terminal becomes an ordinary window again.
        if(desktopRaised==true)
            foreach(var hwnd in Windows)Native.SetWindowPos(hwnd,-2,0,0,0,0,0x613);
        foreach(var source in sources)source.RemoveHook(WindowMessage);
        foreach(var hwnd in nativeHandles)if(Native.IsWindow(hwnd))Native.RemoveWindowSubclass(hwnd,nativeCallback,1);
        Native.RemoveWindowSubclass(desktopReference,nativeCallback,1);Native.DestroyWindow(desktopReference);
    }
}
