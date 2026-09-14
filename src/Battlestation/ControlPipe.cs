using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class ControlPipe : IDisposable
{
    const string Name="Battlestation.Control";
    readonly CancellationTokenSource stop=new();
    readonly Task server;
    public ControlPipe(Dispatcher dispatcher,Func<string,string> command,string pipeName=Name)
    {
        server=Task.Run(async()=>{
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(stop.Token);
                    using var request=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);request.CancelAfter(TimeSpan.FromSeconds(2));
                    string? line=await ReadLine(pipe,256,request.Token);if(line is null)continue;
                    string result;
                    try{result=await dispatcher.InvokeAsync(()=>command(line),DispatcherPriority.Normal,request.Token);}catch(Exception e){result="Error: "+e.Message;}
                    // Raw async writes have no StreamWriter.Dispose flush that
                    // could outlive the per-request deadline after cancellation.
                    await pipe.WriteAsync(Encoding.UTF8.GetBytes(result+"\n"),request.Token);
                }
                catch(OperationCanceledException){if(stop.IsCancellationRequested)break;}
                catch(Exception e) when(e is IOException or ObjectDisposedException or DecoderFallbackException){}
            }
        });
    }
    internal static async Task<string?> ReadLine(Stream stream,int limit,CancellationToken cancellation)
    {
        var text=new StringBuilder();var bytes=new byte[Math.Min(limit+1,4096)];var chars=new char[bytes.Length+1];var decoder=new UTF8Encoding(false,true).GetDecoder();
        while(true)
        {
            int count=await stream.ReadAsync(bytes,cancellation).ConfigureAwait(false);if(count==0)return text.Length==0?null:text.ToString().TrimEnd('\r');
            int end=Array.IndexOf(bytes,(byte)'\n',0,count);int used=end<0?count:end;
            int decoded=decoder.GetChars(bytes,0,used,chars,0,end>=0);if(text.Length+decoded>limit)throw new IOException("Control message too long");
            text.Append(chars,0,decoded);if(end>=0)return text.ToString().TrimEnd('\r');
        }
    }
    public static string Send(string command,string pipeName=Name,int timeout=3000)=>SendAsync(command,pipeName,timeout).GetAwaiter().GetResult();
    static async Task<string> SendAsync(string command,string pipeName,int timeout)
    {
        using var cancel=new CancellationTokenSource(timeout);
        using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(cancel.Token).ConfigureAwait(false);await pipe.WriteAsync(Encoding.UTF8.GetBytes(command+"\n"),cancel.Token).ConfigureAwait(false);
        return await ReadLine(pipe,1024*1024,cancel.Token).ConfigureAwait(false)??"No response";
    }
    public void Dispose()=>stop.Cancel();
}
