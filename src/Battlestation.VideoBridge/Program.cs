using System.Buffers.Binary;
using System.IO.Pipes;
using System.Threading.Channels;
using Battlestation;

// Chrome native messaging uses stdout as a binary transport. Never log there,
// or persist received messages. Only this extension's public origin is accepted.
// Frames are decoded here (base64 is imposed by native messaging) and forwarded
// to the desktop as bounded binary records; everything else is relayed verbatim.
// A named pipe is accepted after the origin for the protocol tests; Chrome also
// appends its own --parent-window flag there, which is never a pipe name.
if(args.Length==0||args[0]!="chrome-extension://bbbkiomcecimmpndgliccmeagfhbednp/")return;
var pipeName="Battlestation.Video.v1";
for(int index=1;index<args.Length;index++)if(!args[index].StartsWith("--",StringComparison.Ordinal)){pipeName=args[index];break;}
using var lifetime=new CancellationTokenSource();
var queue=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=true});
var input=Console.OpenStandardInput();var output=Console.OpenStandardOutput();
var receive=Task.Run(async()=>{
    try
    {
        var header=new byte[4];
        while(!lifetime.IsCancellationRequested)
        {
            await input.ReadExactlyAsync(header,lifetime.Token);int length=BinaryPrimitives.ReadInt32LittleEndian(header);
            if(length<=0||length>4*1024*1024)break;
            var bytes=new byte[length];await input.ReadExactlyAsync(bytes,lifetime.Token);
            queue.Writer.TryWrite(FramePacker.Pack(bytes));
        }
    }catch(Exception e) when(e is IOException or OperationCanceledException){}
    finally {lifetime.Cancel();queue.Writer.TryComplete();}
});
while(!lifetime.IsCancellationRequested)
{
    using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);
    using var connected=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    try
    {
        await pipe.ConnectAsync(2000,lifetime.Token);
        var toDesktop=Task.Run(async()=>{
            while(await queue.Reader.WaitToReadAsync(connected.Token))
            {
                while(queue.Reader.TryRead(out var message))
                {
                    var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,message.Length);
                    await pipe.WriteAsync(header,connected.Token);await pipe.WriteAsync(message,connected.Token);await pipe.FlushAsync(connected.Token);
                }
            }
        },connected.Token);
        var toBrowser=Task.Run(async()=>{
            var header=new byte[4];
            while(!connected.IsCancellationRequested)
            {
                await pipe.ReadExactlyAsync(header,connected.Token);int length=BinaryPrimitives.ReadInt32LittleEndian(header);
                if(length<=0||length>8192)throw new IOException();
                var message=new byte[length];await pipe.ReadExactlyAsync(message,connected.Token);
                await output.WriteAsync(header,connected.Token);await output.WriteAsync(message,connected.Token);await output.FlushAsync(connected.Token);
            }
        },connected.Token);
        await Task.WhenAny(toDesktop,toBrowser);connected.Cancel();pipe.Dispose();
        try {await Task.WhenAll(toDesktop,toBrowser);}catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException){}
    }
    catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException){}
    if(!lifetime.IsCancellationRequested)try{await Task.Delay(1000,lifetime.Token);}catch(OperationCanceledException){}
}
try{await receive;}catch(OperationCanceledException){}
