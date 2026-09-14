using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;
using Microsoft.Terminal.Wpf;

namespace Battlestation;
// Own the console directly. No external window, reparenting, or terminal transcript cache.
internal sealed class ConPtySession(string command,string directory) : ITerminalConnection
{
    readonly object gate=new();
    readonly Channel<string> input=Channel.CreateUnbounded<string>(new UnboundedChannelOptions{SingleReader=true});
    readonly TaskCompletionSource<bool> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    nint console;
    FileStream? reader,writer;
    Process? process;
    int started,closed,columns=100,rows=30;
    public int Pid {get;private set;}
    public bool Exited {get;private set;}
    public bool BracketedPaste {get;private set;}
    public string? Error {get;private set;}
    public Task<bool> Ready=>ready.Task;
    public long InputCharacters,OutputCharacters;
    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;
    public void Start(){if(Volatile.Read(ref closed)!=0){ready.TrySetResult(false);return;}if(Interlocked.Exchange(ref started,1)==0)_=Task.Run(Run);}
    async Task Run()
    {
        try
        {
            using var inputRead=Pipe(out var inputWrite);using var outputRead=Pipe(out var outputWrite);
            using(inputWrite)
            using(outputWrite)
            {
                int hr=CreatePseudoConsole(new Coord((short)columns,(short)rows),inputRead,outputWrite,0,out var handle);
                Marshal.ThrowExceptionForHR(hr);
                lock(gate)console=handle;
                Pid=Launch(command,directory,handle);
                process=Process.GetProcessById(Pid);
                // Transfer duplicated application ends to the streams. The console
                // endpoints above are closed once process creation is complete.
                writer=new FileStream(Duplicate(inputWrite),FileAccess.Write);
                reader=new FileStream(Duplicate(outputRead),FileAccess.Read);
            }
            inputRead.Dispose();outputRead.Dispose();
            TerminalOutput?.Invoke(this,new TerminalOutputEventArgs("\x1b[?9001h"));
            ready.TrySetResult(true);
            _=PumpInput();
            using var text=new StreamReader(reader,new UTF8Encoding(false),false,8192,true);
            var buffer=new char[8192];int length;string tail="";
            while((length=await text.ReadAsync(buffer))>0)
            {
                Interlocked.Add(ref OutputCharacters,length);
                var output=new string(buffer,0,length);var modes=tail+output;
                int on=modes.LastIndexOf("\x1b[?2004h",StringComparison.Ordinal),off=modes.LastIndexOf("\x1b[?2004l",StringComparison.Ordinal);
                if(on>=0||off>=0)BracketedPaste=on>off;
                tail=modes[^Math.Min(8,modes.Length)..];
                TerminalOutput?.Invoke(this,new TerminalOutputEventArgs(output));
            }
            Exited=true;
        }
        catch(Exception e)
        {
            if(Volatile.Read(ref closed)==0){Error=e.Message;TerminalOutput?.Invoke(this,new TerminalOutputEventArgs("\r\nTerminal : "+e.Message+"\r\n"));}
            ready.TrySetResult(false);
        }
        finally
        {
            ready.TrySetResult(false);
            Exited=true;
            // Session cleanup runs outside the dispatcher. ClosePseudoConsole may
            // wait for final output and must never block mouse/keyboard handling.
            Close();
        }
    }
    async Task PumpInput()
    {
        try
        {
            await foreach(var value in input.Reader.ReadAllAsync())
            {
                if(writer is null)break;
                await writer.WriteAsync(Encoding.UTF8.GetBytes(value));await writer.FlushAsync();
                Interlocked.Add(ref InputCharacters,value.Length);
            }
        }
        catch(Exception e){if(Volatile.Read(ref closed)==0)Error=e.Message;}
    }
    public void WriteInput(string value){if(Volatile.Read(ref closed)==0)input.Writer.TryWrite(value);}
    public void Resize(uint height,uint width)
    {
        columns=Math.Clamp((int)width,1,500);rows=Math.Clamp((int)height,1,300);
        lock(gate)if(console!=0)ResizePseudoConsole(console,new Coord((short)columns,(short)rows));
    }
    public void Close()
    {
        if(Interlocked.Exchange(ref closed,1)!=0)return;
        input.Writer.TryComplete();
        if(Volatile.Read(ref started)==0)ready.TrySetResult(false);
        _=Task.Run(async()=>
        {
            await ready.Task;
            try{if(process is {HasExited:false})process.Kill(true);}catch(InvalidOperationException){}
            nint handle;lock(gate){handle=console;console=0;}
            if(handle!=0)ClosePseudoConsole(handle);
            writer?.Dispose();reader?.Dispose();process?.Dispose();Exited=true;
        });
    }
    static SafeFileHandle Pipe(out SafeFileHandle write)
    {
        if(!CreatePipe(out var read,out write,0,0))throw new Win32Exception();return read;
    }
    static SafeFileHandle Duplicate(SafeFileHandle handle)
    {
        var self=GetCurrentProcess();
        if(!DuplicateHandle(self,handle,self,out var copy,0,false,2))throw new Win32Exception();return copy;
    }
    static int Launch(string command,string directory,nint console)
    {
        nuint size=0;InitializeProcThreadAttributeList(0,1,0,ref size);
        var attributes=Marshal.AllocHGlobal((nint)size);
        bool initialized=false;
        try
        {
            if(!InitializeProcThreadAttributeList(attributes,1,0,ref size))throw new Win32Exception();initialized=true;
            if(!UpdateProcThreadAttribute(attributes,0,0x00020016,console,(nuint)nint.Size,0,0))throw new Win32Exception();
            // Null standard handles with STARTF_USESTDHANDLES prevent a launcher
            // with redirected output from leaking its pipes into the new console.
            var info=new StartupInfoEx{StartupInfo=new StartupInfo{Size=Marshal.SizeOf<StartupInfoEx>(),Flags=0x100},Attributes=attributes};
            // Pass the entire STARTUPINFOEX by reference, including its attribute
            // list. Passing only the embedded STARTUPINFO loses the ConPTY link.
            if(!CreateProcessW(null,new StringBuilder(command),0,0,false,0x00080000|0x00000400,0,directory,ref info,out var result))throw new Win32Exception();
            CloseHandle(result.Thread);CloseHandle(result.Process);return result.ProcessId;
        }
        finally{if(initialized)DeleteProcThreadAttributeList(attributes);Marshal.FreeHGlobal(attributes);}
    }
    [StructLayout(LayoutKind.Sequential)] readonly struct Coord(short x,short y){public readonly short X=x,Y=y;}
    [StructLayout(LayoutKind.Sequential)] struct StartupInfo{public int Size;public nint Reserved,Desktop,Title;public int X,Y,XSize,YSize,XChars,YChars,Fill,Flags;public short Show,ReservedSize;public nint ReservedBytes,Input,Output,Error;}
    [StructLayout(LayoutKind.Sequential)] struct StartupInfoEx{public StartupInfo StartupInfo;public nint Attributes;}
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo{public nint Process,Thread;public int ProcessId,ThreadId;}
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CreatePipe(out SafeFileHandle read,out SafeFileHandle write,nint security,uint size);
    [DllImport("kernel32.dll")] static extern int CreatePseudoConsole(Coord size,SafeFileHandle input,SafeFileHandle output,uint flags,out nint console);
    [DllImport("kernel32.dll")] static extern int ResizePseudoConsole(nint console,Coord size);
    [DllImport("kernel32.dll")] static extern void ClosePseudoConsole(nint console);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool InitializeProcThreadAttributeList(nint list,int count,uint flags,ref nuint size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool UpdateProcThreadAttribute(nint list,uint flags,nuint attribute,nint value,nuint size,nint previous,nint returned);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(nint list);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcessW(string? application,StringBuilder command,nint processSecurity,nint threadSecurity,bool inherit,uint flags,nint environment,string directory,ref StartupInfoEx startup,out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool DuplicateHandle(nint sourceProcess,SafeFileHandle source,nint targetProcess,out SafeFileHandle target,uint access,bool inherit,uint options);
}
