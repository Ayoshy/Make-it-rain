using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Media.Imaging;

namespace Battlestation;
internal sealed record VideoTab(int Id,bool Ready,bool Playing,bool Active,long Used);
internal sealed record BrowserVideoState(bool Connected,VideoTab[] Tabs,BitmapSource? Image,int? TabId,bool? Playing,long Frames,long LastFrame,string Error,long? DecodedFrames=null,double? EncodeMs=null,string? Visibility=null,string? Version=null,string? Diagnostic=null);
internal sealed class VideoBrowserBridge : IDisposable
{
    readonly object gate=new();
    readonly CancellationTokenSource lifetime=new();
    sealed record Outgoing(byte[] Bytes,TaskCompletionSource<bool>? Sent);
    readonly Channel<Outgoing> outgoing=Channel.CreateBounded<Outgoing>(new BoundedChannelOptions(32){FullMode=BoundedChannelFullMode.Wait});
    Task<bool>? lastStop;
    BrowserVideoState state=new(false,[],null,null,null,0,0,"");
    string captureId="";
    int targetWidth=1280,targetHeight=720;
    long nextDiscovery;
    public void Configure(int width,int height){width=Math.Clamp(width,160,1920);height=Math.Clamp(height,90,1080);lock(gate){if(targetWidth==width&&targetHeight==height)return;targetWidth=width;targetHeight=height;if(state.TabId is int tab)Send(new{type="configure",tabId=tab,width,height});}}
    public void Discover(){lock(gate){if(!state.Connected||Environment.TickCount64<nextDiscovery)return;nextDiscovery=Environment.TickCount64+5000;Send(new{type="list"});}}
    readonly string pipeName;
    public event Action? FrameAvailable;
    public BrowserVideoState State {get{lock(gate)return state;}}
    public VideoBrowserBridge(string pipeName="Battlestation.Video.v1"){this.pipeName=pipeName;_=Run();}
    public void Start(int tab,bool preserveError=false)
    {
        lock(gate){captureId=Guid.NewGuid().ToString("N");state=state with{Image=null,TabId=tab,Frames=0,LastFrame=0,Error=preserveError?state.Error:"",DecodedFrames=null};Send(new{type="start",tabId=tab,captureId,width=targetWidth,height=targetHeight});}
    }
    public void Stop()
    {
        lock(gate)
        {
            if(state.TabId is int tab){var sent=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);lastStop=sent.Task;Send(new{type="stop",tabId=tab},sent);}
            captureId="";state=state with{Image=null,TabId=null,Playing=null,Frames=0,LastFrame=0,Error="",DecodedFrames=null};
        }
    }
    public void Toggle(){lock(gate)if(state.TabId is int tab)Send(new{type="toggle",tabId=tab});}
    public void Focus(){lock(gate)if(state.TabId is int tab)Send(new{type="focus",tabId=tab});}
    void Send(object value,TaskCompletionSource<bool>? sent=null){if(!outgoing.Writer.TryWrite(new(JsonSerializer.SerializeToUtf8Bytes(value),sent)))sent?.TrySetResult(false);}
    async Task Run()
    {
        while(!lifetime.IsCancellationRequested)
        {
            using var connection=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            try
            {
                using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                lock(gate)state=state with{Connected=true};
                // Discard stale commands from the previous connection before handshake.
                while(outgoing.Reader.TryRead(out var stale))stale.Sent?.TrySetResult(false);
                Send(new{type="list"});
                var write=Task.Run(async()=>{
                    await foreach(var message in outgoing.Reader.ReadAllAsync(connection.Token))
                    {
                        var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,message.Bytes.Length);
                        await pipe.WriteAsync(header,connection.Token);await pipe.WriteAsync(message.Bytes,connection.Token);await pipe.FlushAsync(connection.Token);message.Sent?.TrySetResult(true);
                    }
                },connection.Token);
                var read=Task.Run(async()=>{
                    var header=new byte[4];
                    while(!connection.IsCancellationRequested)
                    {
                        await pipe.ReadExactlyAsync(header,connection.Token);int length=BinaryPrimitives.ReadInt32LittleEndian(header);
                        if(length<=0||length>4*1024*1024)throw new IOException("Invalid frame length");
                        var message=new byte[length];await pipe.ReadExactlyAsync(message,connection.Token);Receive(message);
                    }
                },connection.Token);
                await Task.WhenAny(write,read);connection.Cancel();pipe.Dispose();
                try{await Task.WhenAll(write,read);}catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException){}
            }
            catch(Exception e) when(e is IOException or OperationCanceledException or UnauthorizedAccessException){}
            finally {lock(gate){captureId="";state=new(false,[],null,null,null,0,0,"");}}
            if(!lifetime.IsCancellationRequested)try{await Task.Delay(1000,lifetime.Token);}catch(OperationCanceledException){}
        }
    }
    void Receive(byte[] message)
    {
        try
        {
            using var doc=JsonDocument.Parse(message);var root=doc.RootElement;
            string? type=root.GetProperty("type").GetString();
            if(type=="tabs")
            {
                var tabs=root.GetProperty("tabs").EnumerateArray().Take(32).Select(t=>new VideoTab(t.GetProperty("id").GetInt32(),t.GetProperty("ready").GetBoolean(),t.GetProperty("playing").GetBoolean(),t.GetProperty("active").GetBoolean(),t.GetProperty("used").GetInt64())).ToArray();
                lock(gate)
                {
                    var selected=tabs.FirstOrDefault(t=>t.Id==state.TabId);
                    state=state with{Tabs=tabs,Playing=selected?.Playing,Image=selected?.Ready==true?state.Image:null};
                }
                return;
            }
            int tabId=root.GetProperty("tabId").GetInt32();string? epoch=root.GetProperty("captureId").GetString();
            lock(gate)if(tabId!=state.TabId||epoch!=captureId)return;
            if(type=="error"||type=="ended")
            {
                string error=type=="ended"?"video-ended":root.GetProperty("code").GetString()??"capture-unavailable";
                string stage=root.TryGetProperty("stage",out var s)?s.GetString()??"":"",reason=root.TryGetProperty("reason",out var r)?r.GetString()??"":"";
                string Safe(string value)=>value.Length<=40&&value.All(c=>char.IsAsciiLetter(c)||c=='-')?value:"unknown";
                lock(gate)if(epoch==captureId)state=state with{Image=error=="play-blocked"?state.Image:null,Error=error is "play-blocked" or "no-video" or "video-ended"?error:"capture-unavailable",Diagnostic=Safe(stage)+"/"+Safe(reason),Version=root.TryGetProperty("version",out var v)?v.GetString():null};
                return;
            }
            if(type!="frame")return;
            int width=root.GetProperty("width").GetInt32(),height=root.GetProperty("height").GetInt32();
            string jpeg=root.GetProperty("jpeg").GetString()!;if(jpeg.Length>2800000)return;
            byte[] bytes=Convert.FromBase64String(jpeg);if(!ValidJpeg(bytes,width,height))return;
            using var stream=new MemoryStream(bytes);
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();
            lock(gate)
            {
                if(epoch!=captureId||tabId!=state.TabId)return;
                state=state with{Image=image,Playing=root.GetProperty("playing").GetBoolean(),Frames=state.Frames+1,LastFrame=Environment.TickCount64,Error="",Diagnostic=null,DecodedFrames=root.TryGetProperty("decodedFrames",out var decoded)?decoded.GetInt64():null,EncodeMs=root.TryGetProperty("encodeMs",out var encoding)?encoding.GetDouble():null,Visibility=root.TryGetProperty("visibility",out var visibility)?visibility.GetString():null,Version=root.TryGetProperty("version",out var version)?version.GetString():null};
            }
            FrameAvailable?.Invoke();
            Send(new{type="ack",tabId,captureId=epoch,sequence=root.GetProperty("sequence").GetInt64()});
        }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or NotSupportedException or IOException or OverflowException){}
    }
    internal static bool ValidJpeg(byte[] bytes,int width,int height)
    {
        if(width is <1 or >1920||height is <1 or >1080||bytes.Length is <12 or >2097152||bytes[0]!=255||bytes[1]!=216)return false;
        for(int i=2;i+4<bytes.Length;)
        {
            if(bytes[i++]!=255)return false;
            while(i<bytes.Length&&bytes[i]==255)i++;
            if(i>=bytes.Length)return false;int marker=bytes[i++];
            if(marker is 0xd9 or 0xda)return false;
            if(i+2>bytes.Length)return false;int size=(bytes[i]<<8)|bytes[i+1];
            if(size<2||i+size>bytes.Length)return false;
            if(marker is 0xc0 or 0xc2)return size>=8&&bytes[i+2]==8&&((bytes[i+3]<<8)|bytes[i+4])==height&&((bytes[i+5]<<8)|bytes[i+6])==width;
            i+=size;
        }
        return false;
    }
    public void Dispose(){Stop();lastStop?.Wait(500);lifetime.Cancel();}
}
