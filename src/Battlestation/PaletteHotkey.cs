using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Battlestation;
internal sealed class PaletteHotkey : IDisposable
{
    const int Id=0x4253;
    readonly HwndSource source;
    readonly Action summon;
    bool disposed;
    internal bool Registered {get;private set;}
    internal int Error {get;private set;}
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint hwnd,int id);
    internal PaletteHotkey(Action action)
    {
        summon=action;source=new HwndSource(new HwndSourceParameters("Battlestation palette shortcut"){ParentWindow=new nint(-3),Width=0,Height=0,WindowStyle=0});source.AddHook(Message);Retry();
    }
    internal void Retry(){if(Registered)return;Registered=RegisterHotKey(source.Handle,Id,0x4002,0x20);Error=Registered?0:Marshal.GetLastWin32Error();}
    nint Message(nint hwnd,int msg,nint w,nint l,ref bool handled){if(msg==0x312&&w==Id){handled=true;summon();}return 0;}
    public void Dispose(){if(disposed)return;disposed=true;if(Registered)UnregisterHotKey(source.Handle,Id);source.RemoveHook(Message);source.Dispose();Registered=false;}
}
