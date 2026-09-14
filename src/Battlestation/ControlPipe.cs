using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class ControlPipe : IDisposable
{
    const string Name="Battlestation.Control";
    readonly CancellationTokenSource stop=new();
    readonly Task server;
    public ControlPipe(Dispatcher dispatcher,Func<string,string> command,string pipeName=Name)
    {
        server=Task.Run(async()=>
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(stop.Token);
                    using var reader=new StreamReader(pipe,leaveOpen:true);using var writer=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};
                    var line=await reader.ReadLineAsync(stop.Token);if(line is null||line.Length>256)continue;
                    string result;
                    try{result=await dispatcher.InvokeAsync(()=>command(line));}catch(Exception e){result="Error: "+e.Message;}
                    await writer.WriteLineAsync(result);
                }
                catch(OperationCanceledException){break;}
                catch(IOException){ }
            }
        });
    }
    public static string Send(string command,string pipeName=Name,int timeout=3000)
    {
        using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.CurrentUserOnly);pipe.Connect(timeout);
        using var writer=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};using var reader=new StreamReader(pipe,leaveOpen:true);
        writer.WriteLine(command);
        var response=reader.ReadLineAsync();
        if(!response.Wait(timeout))throw new TimeoutException("Le terminal ne répond pas.");
        return response.Result??"No response";
    }
    public void Dispose()=>stop.Cancel();
}
