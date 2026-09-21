using System.Runtime.InteropServices;
using System.Text;

namespace Battlestation;
internal static class Native
{
    const string G="Battlestation.Graphics.dll", D="Battlestation.Desk.dll";
    [DllImport(G, CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundStart(nint parent,string images);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundStop();
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundVisibility(int monitors);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundCapture();
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundAppearance(int animate,float opacity);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundSceneFade(float alpha);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundPalette(int index,uint[] colors);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundTheme(int index,int immediate);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundAudio(float bass,float middle,float treble,float intensity);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundPanelFront(int slot);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundGlass(float x,float cy,float my,float w,float ch,float mh);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundDock(float x,float y,float w,float h);
    [DllImport(G, CallingConvention=CallingConvention.Cdecl)] internal static extern void BackgroundPanel(int slot,float x,float y,float w,float h);
    [DllImport(D, CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)] internal static extern void DeskStart(string config);
    [DllImport(D, CallingConvention=CallingConvention.Cdecl)] internal static extern void DeskStop();
    [DllImport(D, CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)] static extern int DeskRead(string key,StringBuilder buffer,int capacity);
    [DllImport(D, CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)] internal static extern void DeskCommand(string command);
    [DllImport(D, CallingConvention=CallingConvention.Cdecl)] internal static extern int DeskCover([Out] byte[]? buffer,int capacity);
    [ThreadStatic] static StringBuilder? readBuffer;
    [ThreadStatic] static Dictionary<string,(ulong Revision,string Text)>? readCache;
    internal static string Read(string key)
    {
        int group=key.StartsWith("clock",StringComparison.Ordinal)||key.EndsWith(":age",StringComparison.Ordinal)||key=="mouseX"?-1:key.StartsWith("weather",StringComparison.Ordinal)?1:key.StartsWith("project",StringComparison.Ordinal)||key.StartsWith("selected",StringComparison.Ordinal)?0:2;
        ulong revision=group<0?0:DeskRevision(group);var cache=readCache??=[];
        if(group>=0&&cache.TryGetValue(key,out var old)&&old.Revision==revision)return old.Text;
        var b=readBuffer??=new StringBuilder(512);b.Clear();int count=DeskRead(key,b,b.Capacity);if(count==b.Capacity-1&&b.Capacity<8192){b.EnsureCapacity(8192);b.Clear();DeskRead(key,b,b.Capacity);}string text=b.ToString();
        if(group>=0){if(cache.Count>=1024)cache.Clear();cache[key]=(revision,text);}return text;
    }
    [DllImport(D, CallingConvention=CallingConvention.Cdecl)] internal static extern void DeskProjectsActive(int active);
    [DllImport(D, CallingConvention=CallingConvention.Cdecl)] internal static extern ulong DeskRevision(int group);

    [StructLayout(LayoutKind.Sequential)] internal struct CursorPoint {public int X,Y;}
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out CursorPoint point);
    internal delegate bool EnumWindow(nint window,nint data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindow callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint FindWindow(string? cls,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent,nint after,string? cls,string? title);
    [DllImport("user32.dll")] internal static extern nint SetParent(nint child,nint parent);
    [DllImport("user32.dll")] internal static extern nint SetWindowLongPtr(nint hwnd,int index,nint value);
    [DllImport("user32.dll")] internal static extern nint GetWindowLongPtr(nint hwnd,int index);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll")] internal static extern nint SendMessageTimeout(nint hwnd,uint msg,nint w,nint l,uint flags,uint timeout,out nint result);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern nint CreateWindowEx(uint ex,string cls,string name,uint style,int x,int y,int w,int h,nint parent,nint menu,nint instance,nint data);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd,StringBuilder text,int max);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd,int show);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd,uint command);
    [DllImport("user32.dll")] internal static extern nint SetFocus(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(nint hwnd,StringBuilder text,int capacity);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out WindowRect rect);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    internal delegate void WinEvent(nint hook,uint ev,nint hwnd,int objectId,int child,uint thread,uint time);
    [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint min,uint max,nint module,WinEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);
    internal delegate nint SubclassProc(nint hwnd,uint msg,nint w,nint l,nuint id,nuint data);
    [DllImport("comctl32.dll")] internal static extern bool SetWindowSubclass(nint hwnd,SubclassProc callback,nuint id,nuint data);
    [DllImport("comctl32.dll")] internal static extern bool RemoveWindowSubclass(nint hwnd,SubclassProc callback,nuint id);
    [DllImport("comctl32.dll")] internal static extern nint DefSubclassProc(nint hwnd,uint msg,nint w,nint l);
    internal delegate nint KeyboardProc(int code,nint message,nint data);
    [DllImport("user32.dll")] internal static extern nint SetWindowsHookEx(int id,KeyboardProc callback,nint module,uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool IsChild(nint parent,nint child);
    [DllImport("user32.dll")] internal static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterClipboardFormat(string format);
    [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [StructLayout(LayoutKind.Sequential)] internal struct GuiThreadInfo {public int Size,Flags;public nint Active,Focus,Capture,MenuOwner,MoveSize,Caret;public WindowRect CaretRect;}
    [StructLayout(LayoutKind.Sequential)] internal struct WindowRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] internal struct WindowPos {public nint Window,After;public int X,Y,Width,Height;public uint Flags;}
    internal static string Class(nint hwnd){var b=new StringBuilder(128);GetClassName(hwnd,b,b.Capacity);return b.ToString();}
    internal static nint DesktopIconsHost()
    {
        var shell=FindWindow("Progman",null);
        if(shell==0)return 0;
        if(FindWindowEx(shell,0,"SHELLDLL_DefView",null)!=0)return shell;
        GetWindowThreadProcessId(shell,out var shellPid);
        nint host=0;
        EnumWindows((window,_)=>
        {
            GetWindowThreadProcessId(window,out var pid);
            if(pid==shellPid&&Class(window)=="WorkerW"&&FindWindowEx(window,0,"SHELLDLL_DefView",null)!=0){host=window;return false;}
            return true;
        },0);
        return host;
    }
    internal static nint DesktopParent()
    {
        var progman=FindWindow("Progman",null);if(progman==0)throw new InvalidOperationException("Bureau Windows absent.");
        SendMessageTimeout(progman,0x052c,0xd,1,2,1000,out _);
        var worker=FindWindowEx(progman,0,"WorkerW",null);
        if(worker==0)EnumWindows((w,_)=>{if(FindWindowEx(w,0,"SHELLDLL_DefView",null)!=0){var next=FindWindowEx(0,w,"WorkerW",null);if(next!=0)worker=next;}return true;},0);
        if(worker==0)throw new InvalidOperationException("Surface du bureau indisponible.");
        return worker;
    }
}
