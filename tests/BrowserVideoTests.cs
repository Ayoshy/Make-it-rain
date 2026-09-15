using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;

static class BrowserVideoTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static async Task Until(Func<bool> condition)
    {
        for(int i=0;i<100;i++){if(condition())return;await Task.Delay(30);}throw new Exception("Bridge state did not arrive");
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
        string name="Battlestation.Video.Test."+Guid.NewGuid().ToString("N");
        using var bridge=new VideoBrowserBridge(name);
        using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
        await client.ConnectAsync(3000);
        async Task Send(object value)
        {
            var bytes=JsonSerializer.SerializeToUtf8Bytes(value);var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);
            await client.WriteAsync(header);await client.WriteAsync(bytes);await client.FlushAsync();
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
        await Send(new{type="tabs",tabs=Array.Empty<object>()});
        await Until(()=>bridge.State.Tabs.Length==0);
        Check(bridge.State.Image is null,"Discovery gap clears the unavailable image");
        bridge.Stop();Check((await Read()).GetProperty("kind").GetString()=="twitch","Twitch stop remains typed across a discovery gap");
        await Send(new{type="frame",tabId=9,kind="twitch",captureId=twitchEpoch,sequence=5,width=3,height=2,playing=true,jpeg=Convert.ToBase64String(jpeg)});
        await Task.Delay(80);Check(bridge.State.Image is null&&bridge.State.Frames==0,"Late frames after stop are discarded");
        client.Dispose();await Until(()=>!bridge.State.Connected);Check(bridge.State.Tabs.Length==0,"Disconnect clears source availability");
        Console.WriteLine("PASS: bounded JPEG validation, private isolated pipe, no implicit capture, selected-tab identity, frame delivery/acknowledgement, targeted controls, stop and disconnect.");
    }
}
