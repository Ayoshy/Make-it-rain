using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Battlestation;
using Battlestation.Core;

internal static class ConsolidationTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [STAThread] static void Main(string[] args)
    {
        var app=new Application();string folder=Path.Combine(Path.GetTempPath(),"Battlestation-consolidation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var pipeName="Battlestation.Test."+Guid.NewGuid().ToString("N");using var server=new ControlPipe(app.Dispatcher,command=>command=="large"?new string('x',500000):"ok:"+command,pipeName);
        app.Startup+=async(_,_)=>{
            int result=1;
            try
            {
                var full=new Rect(0,0,5120,1440);
                Check(!DesktopVisibility.HasUncoveredArea(full,[new(0,0,2560,1440),new(2560,0,2560,1440)]),"Two separately covered monitors suspend the full desktop");
                Check(DesktopVisibility.HasUncoveredArea(full,[new(0,0,2560,1440)]),"Uncovered secondary monitor stays animated");
                Check(DesktopVisibility.HasUncoveredArea(full,[new(2560,0,2560,1440)]),"Uncovered primary monitor stays animated");
                Check(!DesktopVisibility.HasUncoveredArea(new(0,0,2560,1440),[new(0,0,2560,1400),new(0,1400,2560,40)]),"Taskbar plus maximized window covers a monitor");
                Check(DesktopVisibility.HasUncoveredArea(new(0,0,100,100),[new(0,0,99,100)]),"A narrow exposed strip is retained");
                string diagnostic=Path.Combine(folder,"state.json");Directory.CreateDirectory(diagnostic+".tmp");
                Check(!DiagnosticFile.TryWrite(diagnostic,"blocked"),"Diagnostic failures are contained");Directory.Delete(diagnostic+".tmp");
                Check(DiagnosticFile.TryWrite(diagnostic,"recovered")&&File.ReadAllText(diagnostic)=="recovered","Diagnostic writes recover after the obstruction disappears");
                string syncPipe="Battlestation.SyncTest."+Guid.NewGuid().ToString("N");
                var responder=Task.Run(async()=>{using var pipe=new NamedPipeServerStream(syncPipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await pipe.WaitForConnectionAsync();await ControlPipe.ReadLine(pipe,256,CancellationToken.None);await Task.Delay(50);await pipe.WriteAsync("ok\n"u8.ToArray());});
                var context=SynchronizationContext.Current;var tracking=new TrackingContext();SynchronizationContext.SetSynchronizationContext(tracking);
                try{Check(ControlPipe.Send("detach",syncPipe,1000)=="ok","Synchronous terminal detach receives its response");Check(tracking.Posts==0,"Pipe I/O never posts continuations back to a blocked UI context");}
                finally{SynchronizationContext.SetSynchronizationContext(context);}
                await responder;
                using(var idle=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous))
                {
                    await idle.ConnectAsync(1000);await Task.Delay(2300);
                    string reply=await Task.Run(()=>ControlPipe.Send("after-idle",pipeName,1000));Check(reply=="ok:after-idle","Idle connection expires without monopolizing the server");
                }
                using(var excessive=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous))
                {
                    await excessive.ConnectAsync(1000);try{await excessive.WriteAsync(Encoding.UTF8.GetBytes(new string('x',300)));await excessive.FlushAsync();}catch(IOException){}await Task.Delay(100);
                }
                Check(await Task.Run(()=>ControlPipe.Send("after-limit",pipeName,1000))=="ok:after-limit","Overlong unterminated messages are rejected before a newline arrives");
                using(var noReader=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous))
                {
                    await noReader.ConnectAsync(1000);await noReader.WriteAsync("large\n"u8.ToArray());await noReader.FlushAsync();await Task.Delay(2300);
                    Check(await Task.Run(()=>ControlPipe.Send("after-write-timeout",pipeName,1000))=="ok:after-write-timeout","Blocked response writing does not monopolize the next connection");
                }
                if(args.Length>0)await Projects(folder,args[0]);
                if(args.Length>2)await Graphics(folder,args[1],args[2]);
                Console.WriteLine("PASS: dual-screen occlusion, diagnostic failure/recovery, idle/oversized/stalled-write pipe clients; isolated fixtures only.");result=0;
            }
            catch(Exception e){Console.Error.WriteLine(e);}
            finally{app.Shutdown(result);}
        };
        try{Environment.ExitCode=app.Run();}finally{Directory.Delete(folder,true);}
    }
    sealed class TrackingContext:SynchronizationContext
    {
        internal int Posts;
        public override void Post(SendOrPostCallback callback,object? state){Interlocked.Increment(ref Posts);ThreadPool.QueueUserWorkItem(_=>callback(state));}
    }
    static async Task Projects(string folder,string library)
    {
        NativeLibrary.SetDllImportResolver(typeof(Native).Assembly,(name,_,_)=>name=="Battlestation.Desk.dll"?NativeLibrary.Load(Path.GetFullPath(library)):0);
        var root=Path.Combine(folder,"projects");Directory.CreateDirectory(root);
        for(int i=0;i<14;i++){var dir=Path.Combine(root,"Project"+i.ToString("00"));Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"source.txt"),"fixture");File.SetLastWriteTimeUtc(Path.Combine(dir,"source.txt"),DateTime.UtcNow.AddMinutes(i));}
        string config=Path.Combine(folder,"desk.ini");File.WriteAllLines(config,["[Desk]","ProjectRoot="+root,"WeatherCity="],Encoding.Unicode);
        Native.DeskProjectsActive(1);Native.DeskStart(config);
        try
        {
            async Task Until(Func<bool> ready){for(int i=0;i<150;i++){if(ready())return;await Task.Delay(50);}throw new Exception("Project scanner timeout");}
            await Until(()=>Native.Read("projectCount")=="14");Check(Native.Read("project:0:name")=="Project13","All folders are ranked by source modification");
            Native.DeskProjectsActive(0);await Task.Delay(100);ulong revision=Native.DeskRevision(0);
            File.SetLastWriteTimeUtc(Path.Combine(root,"Project00","source.txt"),DateTime.UtcNow.AddHours(1));Native.DeskCommand("ProjectChanged:Project00");await Task.Delay(300);
            Check(Native.DeskRevision(0)==revision,"Retiring the dock suspends recursive project work");Native.DeskProjectsActive(1);
            await Until(()=>Native.Read("project:0:name")=="Project00");Check(Native.Read("projectCount")=="14","Resume refreshes all folders without dropping projects beyond six");
            await Task.Run(()=>Native.DeskStop());Native.DeskProjectsActive(0);
            string empty=Path.Combine(folder,"empty-projects");Directory.CreateDirectory(empty);File.WriteAllLines(config,["[Desk]","ProjectRoot="+empty,"WeatherCity="],Encoding.Unicode);Native.DeskStart(config);
            Check(Native.Read("projectCount")=="0"&&Native.Read("selectedPath")=="","Restart invalidates cached project data even while the scanner is suspended");
            Console.WriteLine("PASS: actual native scanner, 14 folders, source recency, suspension, change notification and resume; temporary projects only.");
        }
        finally{await Task.Run(()=>Native.DeskStop());}
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void VoidCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate ulong Counter();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void VisibilityCall(int mask);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] delegate void StartCall(nint parent,[MarshalAs(UnmanagedType.LPWStr)] string images);
    static async Task Graphics(string folder,string library,string images)
    {
        string? original=Environment.GetEnvironmentVariable("LOCALAPPDATA");Directory.CreateDirectory(Path.Combine(folder,"Battlestation"));Environment.SetEnvironmentVariable("LOCALAPPDATA",folder);
        nint module=NativeLibrary.Load(Path.GetFullPath(library));
        T Get<T>(string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
        using var parent=new HwndSource(new HwndSourceParameters("Battlestation isolated graphics fixture"){PositionX=-20000,PositionY=-20000,Width=1,Height=1,WindowStyle=unchecked((int)0x80000000)});
        var frames=Get<Counter>("BackgroundTestFrames");var losses=Get<Counter>("BackgroundTestLosses");var visible=Get<VisibilityCall>("BackgroundVisibility");
        try
        {
            Get<StartCall>("BackgroundStart")(parent.Handle,Path.GetFullPath(images));
            async Task Until(Func<bool> ready){for(int i=0;i<150;i++){if(ready())return;await Task.Delay(50);}throw new Exception("Graphics recovery timeout");}
            await Until(()=>frames()>2);ulong before=frames();Get<VoidCall>("BackgroundTestLoseTarget")();await Until(()=>losses()>0&&frames()>before+2);
            visible(0);await Task.Delay(150);before=frames();await Task.Delay(250);Check(frames()==before,"Fully covered background submits no frames");
            visible(1);await Until(()=>frames()>before+2);visible(2);before=frames();await Until(()=>frames()>before+2);
            Console.WriteLine("PASS: injected D2DERR_RECREATE_TARGET recovers, zero covered frames, either exposed monitor resumes; isolated native renderer, no GPU reset.");
        }
        catch{Console.Error.WriteLine($"Native fixture frames={frames()}, losses={losses()}");foreach(string error in Directory.EnumerateFiles(Path.Combine(folder,"Battlestation"),"*error*.txt"))Console.Error.WriteLine(File.ReadAllText(error));throw;}
        finally{await Task.Run(()=>Get<VoidCall>("BackgroundStop")());NativeLibrary.Free(module);Environment.SetEnvironmentVariable("LOCALAPPDATA",original);}
    }
}
