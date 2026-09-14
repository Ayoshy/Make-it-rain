using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class TerminalClipboard : IDisposable
{
    readonly Native.KeyboardProc callback;
    readonly nint hook;
    readonly uint png=Native.RegisterClipboardFormat("PNG");
    readonly Func<nint> host;
    readonly Action paste;
    readonly Dispatcher dispatcher;
    bool imageKey;
    public TerminalClipboard(Func<nint> window,Action action)
    {
        host=window;paste=action;dispatcher=Dispatcher.CurrentDispatcher;callback=OnKey;
        hook=Native.SetWindowsHookEx(13,callback,Native.GetModuleHandle(null),0);
    }
    nint OnKey(int code,nint message,nint data)
    {
        if(code==0&&Marshal.ReadInt32(data)==0x56)
        {
            if((message==0x101||message==0x105)&&imageKey){imageKey=false;return 1;}
            if(message==0x100&&(Native.GetAsyncKeyState(0x11)&0x8000)!=0&&(Native.GetAsyncKeyState(0x12)&0x8000)==0)
            {
                var info=new Native.GuiThreadInfo{Size=Marshal.SizeOf<Native.GuiThreadInfo>()};Native.GetGUIThreadInfo(0,ref info);var window=host();
                if(window!=0&&(info.Focus==window||Native.IsChild(window,info.Focus))&&(Native.IsClipboardFormatAvailable(13)||Native.IsClipboardFormatAvailable(8)||Native.IsClipboardFormatAvailable(17)||Native.IsClipboardFormatAvailable(2)||Native.IsClipboardFormatAvailable(png)))
                {
                    imageKey=true;dispatcher.BeginInvoke(paste);return 1;
                }
            }
        }
        return Native.CallNextHookEx(hook,code,message,data);
    }
    public void Dispose(){if(hook!=0)Native.UnhookWindowsHookEx(hook);}
}
