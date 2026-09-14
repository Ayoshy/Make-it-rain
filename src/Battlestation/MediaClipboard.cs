using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class MediaClipboard : IDisposable
{
    readonly HwndSource source;
    readonly MediaReserve reserve;
    readonly DispatcherTimer timer;
    readonly Func<bool> enabled;
    int retries;
    uint sequence;
    internal bool Registered {get;}
    [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] static extern nint GetClipboardOwner();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll")] static extern bool OpenClipboard(nint hwnd);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern nint GetClipboardData(uint format);
    [DllImport("kernel32.dll")] static extern nuint GlobalSize(nint data);
    [DllImport("kernel32.dll")] static extern nint GlobalLock(nint data);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(nint data);
    internal MediaClipboard(MediaReserve store,Func<bool> isEnabled)
    {
        reserve=store;enabled=isEnabled;
        source=new HwndSource(new HwndSourceParameters("Battlestation media links"){ParentWindow=new nint(-3),Width=0,Height=0,WindowStyle=0});source.AddHook(Message);
        timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};timer.Tick+=(_,_)=>Read();
        sequence=GetClipboardSequenceNumber();Registered=AddClipboardFormatListener(source.Handle);
    }
    nint Message(nint hwnd,int msg,nint w,nint l,ref bool handled)
    {
        if(msg==0x31D&&enabled()){sequence=GetClipboardSequenceNumber();retries=0;timer.Stop();timer.Start();}
        return 0;
    }
    void Read()
    {
        timer.Stop();if(!enabled()||sequence!=GetClipboardSequenceNumber())return;
        try
        {
            // Never inspect copied terminal or password-manager data.
            GetWindowThreadProcessId(GetClipboardOwner(),out var pid);
            if(pid!=0)
            {
                using var process=Process.GetProcessById((int)pid);
                if(new[]{"Battlestation","WindowsTerminal","OpenConsole","powershell","pwsh","cmd","Codex","1Password","KeePass","KeePassXC","Bitwarden"}.Contains(process.ProcessName,StringComparer.OrdinalIgnoreCase))return;
            }
            MediaLink? link=null;
            if(!OpenClipboard(source.Handle)){if(++retries<3)timer.Start();return;}
            try
            {
                var data=GetClipboardData(13); // CF_UNICODETEXT, bounded before making a managed copy.
                if(data==0)return;var size=GlobalSize(data);if(size<18||size>4098)return;
                var pointer=GlobalLock(data);if(pointer==0)return;
                try
                {
                    string prefix=Marshal.PtrToStringUni(pointer,8)??"";
                    if(!prefix.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&!prefix.StartsWith("http://",StringComparison.OrdinalIgnoreCase))return;
                    int start=prefix.StartsWith("https://",StringComparison.OrdinalIgnoreCase)?8:7,end=start;
                    while(end<(int)size/2&&end-start<32&&Marshal.ReadInt16(pointer,end*2) is not (0 or 47))end++;
                    if(end>=(int)size/2||Marshal.ReadInt16(pointer,end*2)!=47)return;
                    string host=Marshal.PtrToStringUni(pointer+start*2,end-start)!.ToLowerInvariant();
                    if(host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtu.be" or "open.spotify.com" or "spotify.link"))return;
                    string text=Marshal.PtrToStringUni(pointer,(int)size/2)!;int terminator=text.IndexOf('\0');if(terminator>=0)text=text[..terminator];link=MediaLink.Parse(text);
                }
                finally{GlobalUnlock(data);}
            }
            finally{CloseClipboard();}
            if(link is not null)reserve.Add(link);
        }
        catch(COMException){if(++retries<3)timer.Start();}
        catch(ArgumentException){}
    }
    public void Dispose(){timer.Stop();if(Registered)RemoveClipboardFormatListener(source.Handle);source.RemoveHook(Message);source.Dispose();}
}
