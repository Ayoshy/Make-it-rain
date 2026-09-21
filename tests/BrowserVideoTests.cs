using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Battlestation;

static class BrowserVideoTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static async Task Until(Func<bool> condition)
    {
        for(int i=0;i<100;i++){if(condition())return;await Task.Delay(30);}throw new Exception("Bridge state did not arrive");
    }
    // WPF bitmaps are thread-affine: the presentation surface is created and read
    // on one dedicated dispatcher thread, like the desktop window thread.
    static readonly Dispatcher Ui=StartUi();
    static Dispatcher StartUi()
    {
        var ready=new TaskCompletionSource<Dispatcher>();
        var thread=new Thread(()=>{var dispatcher=Dispatcher.CurrentDispatcher;ready.SetResult(dispatcher);Dispatcher.Run();}){IsBackground=true,Name="Browser video test UI"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return ready.Task.GetAwaiter().GetResult();
    }
    static (bool Presented,WriteableBitmap? Surface) Present(VideoBrowserBridge bridge)=>Ui.Invoke(()=>{bool presented=bridge.Read();return (presented,bridge.State.Image as WriteableBitmap);});
    static bool SameImage(VideoBrowserBridge bridge,WriteableBitmap? surface)=>Ui.Invoke(()=>ReferenceEquals(bridge.State.Image,surface));
    static bool SurfaceIs(VideoBrowserBridge bridge,int width,int height)=>Ui.Invoke(()=>bridge.State.Image is WriteableBitmap surface&&surface.PixelWidth==width&&surface.PixelHeight==height);
    // The bridge process is the only hop that still sees the base64 JSON envelope:
    // the extension-to-bridge protocol is unchanged, the bridge-to-desktop one is binary.
    const string ExtensionOrigin="chrome-extension://bbbkiomcecimmpndgliccmeagfhbednp/";
    static void PackerChecks(byte[] jpeg)
    {
        var capture=Guid.NewGuid();
        var message=JsonSerializer.SerializeToUtf8Bytes(new{type="frame",tabId=7,kind="twitch",captureId=capture.ToString("N"),sequence=9,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg),decodedFrames=12,encodeMs=2.5,visibility="visible",version="0.4.0"});
        var record=FramePacker.Pack(message);
        Check(record.Length>0&&record[0]==VideoFrameRecord.Marker,"Bridge converts a browser frame into a binary record");
        Check(VideoFrameRecord.TryRead(record,out var header)&&header.TabId==7&&header.CaptureId==capture&&header.Kind==VideoFrameRecord.KindTwitch&&header.Sequence==9&&header.Width==3&&header.Height==2&&header.Playing,"Bridge record keeps the frame identity");
        Check(record.AsSpan(header.JpegOffset,header.JpegLength).SequenceEqual(jpeg),"Bridge record carries the exact JPEG bytes");
        Check(JsonDocument.Parse(record.AsMemory(header.ExtraOffset,header.ExtraLength)).RootElement.GetProperty("version").GetString()=="0.4.0","Bridge record keeps the frame diagnostics");
        var control=JsonSerializer.SerializeToUtf8Bytes(new{type="tabs",tabs=Array.Empty<object>()});
        Check(ReferenceEquals(control,FramePacker.Pack(control)),"Bridge relays control messages verbatim");
        var unknown=JsonSerializer.SerializeToUtf8Bytes(new{type="frame",tabId=7,kind="vimeo",captureId=capture.ToString("N"),sequence=1,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        Check(ReferenceEquals(unknown,FramePacker.Pack(unknown)),"Bridge relays an unknown source kind verbatim");
        var frameless=JsonSerializer.SerializeToUtf8Bytes(new{type="frame",tabId=7,kind="twitch",captureId=capture.ToString("N"),sequence=1,width=3,height=2,playing=true});
        Check(ReferenceEquals(frameless,FramePacker.Pack(frameless)),"Bridge relays a frameless message verbatim");
    }
    // End to end: the desktop bridge class against the real bridge process, on an
    // isolated pipe. Commands travel one way as JSON, images the other way as
    // binary records, exactly like a live mirror without touching the desktop.
    static async Task BridgeEndToEndCheck(string pipeName,byte[] jpeg)
    {
        var executable=BridgeExecutable();
        using var process=Process.Start(new ProcessStartInfo(executable,new[]{ExtensionOrigin,pipeName}){RedirectStandardInput=true,RedirectStandardOutput=true,UseShellExecute=false})??throw new Exception("Bridge process did not start");
        using var bridge=new VideoBrowserBridge(pipeName);
        try
        {
            await NativeMessage(process.StandardInput.BaseStream,JsonSerializer.SerializeToUtf8Bytes(new{type="tabs",tabs=new[]{new{id=21,ready=true,playing=true,active=true,used=1L}}}));
            await Until(()=>bridge.State.Connected&&bridge.State.Tabs.Length==1);
            bridge.Start(21);
            using var start=await ReadNativeUntil(process.StandardOutput.BaseStream,"start");
            Check(start.RootElement.GetProperty("tabId").GetInt32()==21,"Browser start travels through the bridge");
            string epoch=start.RootElement.GetProperty("captureId").GetString()!;
            await NativeMessage(process.StandardInput.BaseStream,JsonSerializer.SerializeToUtf8Bytes(new{type="frame",tabId=21,kind="youtube",captureId=epoch,sequence=1,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)}));
            await Until(()=>bridge.State.Frames==1);
            var presented=Present(bridge);
            Check(presented.Presented&&presented.Surface is not null,"Frame crosses the real bridge and presents");
            Check(SurfaceIs(bridge,3,2),"Presented frame keeps its size");
            bridge.Toggle();
            using var toggle=await ReadNativeUntil(process.StandardOutput.BaseStream,"toggle");
            Check(toggle.RootElement.GetProperty("type").GetString()=="toggle","Playback control is relayed");
        }
        finally { bridge.Dispose();try{process.Kill(true);}catch(InvalidOperationException){} }
    }
    static async Task<JsonDocument> ReadNativeUntil(Stream standardOutput,string type)
    {
        for(int i=0;i<16;i++)
        {
            var document=JsonDocument.Parse(await ReadNative(standardOutput));
            if(document.RootElement.TryGetProperty("type",out var value)&&value.GetString()==type)return document;
            document.Dispose();
        }
        throw new Exception("Bridge did not relay "+type);
    }
    static async Task<string> ReadNative(Stream standardOutput)
    {
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var header=new byte[4];await standardOutput.ReadExactlyAsync(header,limit.Token);
        var message=new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];await standardOutput.ReadExactlyAsync(message,limit.Token);return System.Text.Encoding.UTF8.GetString(message);
    }
    static async Task BridgeProcessCheck(string pipeName,byte[] jpeg)
    {
        var executable=BridgeExecutable();
        using var server=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        using var process=Process.Start(new ProcessStartInfo(executable,new[]{ExtensionOrigin,pipeName}){RedirectStandardInput=true,RedirectStandardOutput=true,UseShellExecute=false})??throw new Exception("Bridge process did not start");
        try
        {
            await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var capture=Guid.NewGuid();
            var message=JsonSerializer.SerializeToUtf8Bytes(new{type="frame",tabId=7,kind="youtube",captureId=capture.ToString("N"),sequence=4,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
            await NativeMessage(process.StandardInput.BaseStream,message);
            var record=await ReadPipe(server);
            Check(VideoFrameRecord.TryRead(record,out var header)&&header.CaptureId==capture&&record.AsSpan(header.JpegOffset,header.JpegLength).SequenceEqual(jpeg),"Bridge process forwards a binary frame");
            var control=JsonSerializer.SerializeToUtf8Bytes(new{type="list"});
            await NativeMessage(process.StandardInput.BaseStream,control);
            Check((await ReadPipe(server)).SequenceEqual(control),"Bridge process relays control messages verbatim");
        }
        finally { try{process.Kill(true);}catch(InvalidOperationException){} }
    }
    static async Task NativeMessage(Stream standardInput,byte[] message)
    {
        var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,message.Length);
        await standardInput.WriteAsync(header);await standardInput.WriteAsync(message);await standardInput.FlushAsync();
    }
    static async Task<byte[]> ReadPipe(Stream pipe)
    {
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var header=new byte[4];await pipe.ReadExactlyAsync(header,limit.Token);
        var message=new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];await pipe.ReadExactlyAsync(message,limit.Token);return message;
    }
    static string BridgeExecutable()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"src","Battlestation.VideoBridge","Battlestation.VideoBridge.csproj")))directory=directory.Parent;
        var root=directory?.FullName??throw new Exception("Project root not found");
        foreach(var configuration in new[]{"Release","Debug"})
        {
            var candidate=Path.Combine(root,"src","Battlestation.VideoBridge","bin",configuration,"net10.0-windows","Battlestation.VideoBridge.exe");
            if(File.Exists(candidate))return candidate;
        }
        throw new Exception("Build Battlestation.VideoBridge before running the protocol test");
    }
    [STAThread] static void Main()=>Run().GetAwaiter().GetResult();
    static async Task Run()
    {
        var bitmap=BitmapSource.Create(3,2,96,96,PixelFormats.Bgr32,null,new byte[24],12);
        var encoder=new JpegBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var memory=new MemoryStream();encoder.Save(memory);var jpeg=memory.ToArray();
        Check(VideoBrowserBridge.ValidJpeg(jpeg,3,2),"Valid bounded JPEG");
        Check(!VideoBrowserBridge.ValidJpeg(jpeg,3,3),"Header dimensions must match packet");
        Check(!VideoBrowserBridge.ValidJpeg(jpeg,8000,2),"Oversized frames rejected before decode");
        Check(!VideoBrowserBridge.ValidJpeg([255,216,255,224,0,255,0,0,0,0,0,0],3,2),"Truncated segment rejected");
        var youtube=new VideoTab(1,true,true,false,10,"youtube");var twitch=new VideoTab(2,true,true,true,20,"twitch");
        var selection=new BrowserVideoState(true,[youtube,twitch],null,null,null,0,0,"");
        Check(VideoSelection.Resolve("youtube",selection,true,false,false,"")=="youtube","Manual source selection is stable");
        Check(VideoSelection.Resolve("auto",selection,true,false,false,"")=="twitch","Auto follows the active Twitch tab kind");
        Check(VideoSelection.Resolve("auto",selection,false,false,false,"twitch")=="twitch","Auto keeps a stable Twitch source");
        Check(VideoSelection.SelectBrowserTab(selection,"twitch",true)?.Id==2,"Manual Twitch picks the matching tab");
        Check(!VideoBrowserBridge.IsSupportedKind("vimeo"),"Unknown browser kind is rejected");
        PackerChecks(jpeg);
        await BridgeProcessCheck("Battlestation.Video.Protocol."+Guid.NewGuid().ToString("N"),jpeg);
        await BridgeEndToEndCheck("Battlestation.Video.Loop."+Guid.NewGuid().ToString("N"),jpeg);
        string name="Battlestation.Video.Test."+Guid.NewGuid().ToString("N");
        using var bridge=new VideoBrowserBridge(name);
        using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
        await client.ConnectAsync(3000);
        async Task Send(object value)
        {
            var bytes=JsonSerializer.SerializeToUtf8Bytes(value);var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);
            await client.WriteAsync(header);await client.WriteAsync(bytes);await client.FlushAsync();
        }
        async Task SendBinary(byte[] value)
        {
            var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,value.Length);
            await client.WriteAsync(header);await client.WriteAsync(value);await client.FlushAsync();
        }
        async Task<JsonElement> Read()
        {
            using var limit=new CancellationTokenSource(3000);var header=new byte[4];await client.ReadExactlyAsync(header,limit.Token);
            int length=BinaryPrimitives.ReadInt32LittleEndian(header);var bytes=new byte[length];await client.ReadExactlyAsync(bytes,limit.Token);
            using var document=JsonDocument.Parse(bytes);return document.RootElement.Clone();
        }
        Check((await Read()).GetProperty("type").GetString()=="list","Connection requests source metadata, not pixels");
        await Send(new{type="tabs",tabs=new[]{new{id=7,ready=true,playing=true,active=true,used=1L}}});
        await Until(()=>bridge.State.Tabs.Length==1);Check(bridge.State.Tabs[0].Kind=="youtube","Old tab metadata defaults to YouTube");Check(bridge.State.Image is null,"No capture without explicit start");
        await Send(new{type="tabs",tabs=new[]{new{id=8,kind="vimeo",ready=true,playing=true,active=true,used=2L}}});
        await Task.Delay(80);Check(bridge.State.Tabs.Length==1&&bridge.State.Tabs[0].Id==7,"Unknown tab kind is rejected without replacing availability");
        bridge.Start(7);var start=await Read();string epoch=start.GetProperty("captureId").GetString()!;
        Check(start.GetProperty("type").GetString()=="start"&&start.GetProperty("tabId").GetInt32()==7,"Start is scoped to selected tab");
        await Send(new{type="frame",tabId=8,captureId=epoch,sequence=1,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        await Task.Delay(80);Check(bridge.State.Image is null,"Other tab frames are rejected");
        await Send(new{type="frame",tabId=7,captureId=epoch,sequence=2,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        await Until(()=>bridge.State.Frames==1);Check(bridge.State.Image?.PixelWidth==3,"Decoded frame is available");
        Check((await Read()).GetProperty("type").GetString()=="ack","Backpressure acknowledges consumed frame");
        bridge.Toggle();Check((await Read()).GetProperty("type").GetString()=="toggle","Play/pause stays on selected tab");
        bridge.Stop();Check((await Read()).GetProperty("type").GetString()=="stop","Stop is sent to source");
        await Send(new{type="tabs",tabs=new[]{new{id=9,kind="twitch",ready=true,playing=true,active=true,used=3L}}});
        await Until(()=>bridge.State.Tabs.Length==1&&bridge.State.Tabs[0].Kind=="twitch");
        bridge.Start(9,"twitch");var twitchStart=await Read();string twitchEpoch=twitchStart.GetProperty("captureId").GetString()!;
        Check(twitchStart.GetProperty("kind").GetString()=="twitch","Twitch start is typed");
        await Send(new{type="frame",tabId=9,kind="youtube",captureId=twitchEpoch,sequence=3,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        await Task.Delay(80);Check(bridge.State.Frames==0,"Wrong frame kind is rejected");
        await Send(new{type="frame",tabId=9,kind="twitch",captureId=twitchEpoch,sequence=4,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        await Until(()=>bridge.State.Frames==1);Check((await Read()).GetProperty("kind").GetString()=="twitch","Twitch frame acknowledgement stays typed");
        bridge.Toggle();var toggle=await Read();Check(toggle.GetProperty("type").GetString()=="toggle"&&toggle.GetProperty("kind").GetString()=="twitch","Twitch playback control is targeted");
        bridge.Focus();var focus=await Read();Check(focus.GetProperty("type").GetString()=="focus"&&focus.GetProperty("kind").GetString()=="twitch","Twitch return control is targeted");
        // Binary records: same identity checks, no JSON or base64 per image in the desktop.
        var twitchIdentity=Guid.ParseExact(twitchEpoch,"N");
        Check(VideoFrameRecord.TryBuild(9,twitchIdentity,VideoFrameRecord.KindTwitch,5,3,2,true,jpeg,[],out var record),"Binary frame record builds");
        Check(VideoFrameRecord.TryRead(record,out var parsed)&&parsed.TabId==9&&parsed.Width==3&&parsed.Height==2&&parsed.Sequence==5&&parsed.JpegLength==jpeg.Length,"Binary header round-trips");
        await SendBinary(record);
        await Until(()=>bridge.State.Frames==2);
        var first=Present(bridge);
        Check(first.Presented&&first.Surface is not null,"Binary frame presents on the UI thread");
        Check(SurfaceIs(bridge,3,2),"Binary frame decodes into a reused surface");
        Check(!Present(bridge).Presented,"No repeated presentation without a new frame");
        Check((await Read()).GetProperty("type").GetString()=="ack","Binary frame is acknowledged");
        var diagnostics=JsonSerializer.SerializeToUtf8Bytes(new{decodedFrames=42,encodeMs=3.5,visibility="visible",version="0.4.0"});
        Check(VideoFrameRecord.TryBuild(9,twitchIdentity,VideoFrameRecord.KindTwitch,6,3,2,true,jpeg,diagnostics,out var next),"Binary record carries diagnostics");
        await SendBinary(next);
        await Until(()=>bridge.State.Frames==3);
        Check(Present(bridge).Presented,"Second binary frame presents");
        Check(SameImage(bridge,first.Surface),"Same frame size reuses the surface instance");
        Check(bridge.State.DecodedFrames==42&&bridge.State.EncodeMs==3.5&&bridge.State.Visibility=="visible"&&bridge.State.Version=="0.4.0","Binary diagnostics are published");
        Check((await Read()).GetProperty("type").GetString()=="ack","Second binary frame is acknowledged");
        Check(!VideoFrameRecord.TryRead([1,2,3],out _),"Truncated binary record is rejected");
        Check(VideoFrameRecord.TryBuild(9,Guid.NewGuid(),VideoFrameRecord.KindTwitch,7,3,2,true,jpeg,[],out var stale),"Stale binary record builds");
        await SendBinary(stale);
        await Task.Delay(80);Check(bridge.State.Frames==3,"Stale binary epoch is rejected");
        var crossKind=new byte[record.Length];record.CopyTo(crossKind,0);crossKind[21]=VideoFrameRecord.KindYoutube;
        await SendBinary(crossKind);
        await Task.Delay(80);Check(bridge.State.Frames==3,"Wrong binary kind is rejected");
        await Send(new{type="tabs",tabs=Array.Empty<object>()});
        await Until(()=>bridge.State.Tabs.Length==0);
        Check(bridge.State.Image is null,"Discovery gap clears the unavailable image");
        bridge.Stop();Check((await Read()).GetProperty("kind").GetString()=="twitch","Twitch stop remains typed across a discovery gap");
        await Send(new{type="frame",tabId=9,kind="twitch",captureId=twitchEpoch,sequence=5,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        await Task.Delay(80);Check(bridge.State.Image is null&&bridge.State.Frames==0,"Late frames after stop are discarded");
        client.Dispose();await Until(()=>!bridge.State.Connected);Check(bridge.State.Tabs.Length==0,"Disconnect clears source availability");
        Console.WriteLine("PASS: bounded JPEG validation, bridge packing, bridge process relay, end-to-end command/image loop, private isolated pipe, no implicit capture, selected-tab identity, JSON and binary frame delivery/acknowledgement, reused surface, diagnostics, targeted controls, stop and disconnect.");
    }
}
