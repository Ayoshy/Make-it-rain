using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Battlestation;

internal static class VideoTests
{
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Pump(int ms)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    static void Wait(Func<bool> ready,string reason)
    {
        var deadline=Environment.TickCount64+6000;
        do {Pump(70);if(ready())return;}while(Environment.TickCount64<deadline);
        throw new Exception(reason);
    }
    [STAThread] static void Main(string[] args)
    {
        var yt=new VideoSource(101,1,"youtube",false,false);var st=new VideoSource(102,2,"stremio",false,false);
        Check(VideoSources.Select([yt,st],"auto",102)==st,"Auto retains current source without foreground activity");
        Check(VideoSources.Select([yt with{Foreground=true},st],"auto",102)?.Handle==101,"Foreground source takes priority in auto");
        Check(VideoSources.Select([yt with{Foreground=true},st],"stremio",101)==st,"Manual source overrides foreground");
        Check(VideoSources.Select([yt with{Minimized=true},st],"auto",101)==st,"Available source replaces minimized source");
        Check(VideoSources.Select([st],"youtube",102) is null,"Pinned unavailable source has no unrelated fallback");
        Check(VideoSources.Select([],"auto",101) is null,"Closing sources clears selection");
        Check(VideoSources.IsPictureInPictureTitle("Mode\u00a0PIP (Picture-in-Picture)"),"French Brave PiP title with nonbreaking space");
        Check(!VideoSources.IsPictureInPictureTitle("Picture-in-Picture - YouTube - Brave"),"A normal browser tab with a PiP title is not a capture target");
        if(args.Length==0){Console.WriteLine("PASS: video source selection. Native capture not requested.");return;}
        var dll=Path.GetFullPath(args[0]);NativeLibrary.SetDllImportResolver(typeof(VideoCapture).Assembly,(name,_,_)=>name=="Battlestation.Video.dll"?NativeLibrary.Load(dll):0);
        if(args.Length>1&&args[1]=="--stremio-state")
        {
            foreach(var target in VideoSources.Find().Where(s=>s.Kind=="stremio"))Console.WriteLine(JsonSerializer.Serialize(new{target.Pid,target.Minimized,controls=StremioControls.Read(target)}));return;
        }
        if(args.Length>1&&args[1]=="--windows")
        {
            var ids=System.Diagnostics.Process.GetProcessesByName("brave").Select(p=>{using(p)return p.Id;}).ToHashSet();
            Native.EnumWindows((window,_)=>{
                Native.GetWindowThreadProcessId(window,out uint pid);
                if(ids.Contains((int)pid)&&Native.IsWindowVisible(window))
                {
                    var title=new System.Text.StringBuilder(256);Native.GetWindowText(window,title,title.Capacity);
                    Console.WriteLine(JsonSerializer.Serialize(new{hwnd=(long)window,pid,cls=Native.Class(window),title=title.ToString(),style=(long)Native.GetWindowLongPtr(window,-16),exStyle=(long)Native.GetWindowLongPtr(window,-20)}));
                }
                return true;
            },0);return;
        }
        if(args.Length>1&&(args[1]=="--live"||args[1]=="--live-maintained"||args[1]=="--live-docked"))
        {
            // Explicit live diagnostic: source identity and frame counters only.
            // No media titles, URLs, audio, screenshots or terminal contents.
            var candidates=VideoSources.Find();
            foreach(var candidate in candidates)
            {
                if(args[1]!="--live"&&candidate.Kind!="stremio")continue;
                var foreground=Native.GetForegroundWindow();
                Native.GetWindowRect(candidate.Handle,out var originalBounds);
                var previewImage=new Image();
                Window? preview=null;DesktopPlacement? desktop=null;
                if(args[1]=="--live-docked")
                {
                    preview=new Window{Title="Battlestation video pipeline test",Width=576,Height=372,Left=3396,Top=312,WindowStyle=WindowStyle.None,AllowsTransparency=true,ShowActivated=false,ShowInTaskbar=false,Background=Brushes.Transparent,Content=new Border{Background=new SolidColorBrush(Color.FromArgb(230,16,14,22)),CornerRadius=new CornerRadius(20),Child=previewImage}};
                    preview.Show();desktop=new DesktopPlacement(Dispatcher.CurrentDispatcher);desktop.Add(preview);
                }
                using var lease=args[1]!="--live"?new VideoWindowLease(candidate):null;
                if(preview is not null)lease!.Place(new WindowInteropHelper(preview).Handle,new Rect(3412,372,544,296));
                if(lease is not null&&!lease.Prepare())throw new Exception("Cannot make selected test player drawable");
                using var live=new VideoCapture();live.Start(candidate.Handle);
                if(live.Running)lease?.Adopt();
                if(preview is not null)lease?.ParkForMirror();
                int delivered=0,changes=0;byte[]? previous=null;
                var until=Environment.TickCount64+3000;
                while(Environment.TickCount64<until)
                {
                    Pump(40);lease?.Maintain();if(!live.Read()||live.Image is null)continue;delivered++;if(preview is not null)previewImage.Source=live.Image;
                    var pixels=new byte[live.Image.PixelWidth*live.Image.PixelHeight*4];live.Image.CopyPixels(pixels,live.Image.PixelWidth*4,0);
                    if(previous is not null&&!pixels.AsSpan().SequenceEqual(previous))changes++;previous=pixels;
                }
                int? width=live.Image?.PixelWidth,height=live.Image?.PixelHeight;int age=live.Age,error=live.Error;
                live.Stop();lease?.Dispose();
                desktop?.Dispose();preview?.Close();Native.GetWindowRect(candidate.Handle,out var restoredBounds);
                Console.WriteLine(JsonSerializer.Serialize(new{candidate.Kind,candidate.Pid,hwnd=(long)candidate.Handle,candidate.Minimized,delivered,changes,width,height,age,error,minimizedRestored=VideoSources.IsIconic(candidate.Handle)==candidate.Minimized,boundsRestored=originalBounds.Equals(restoredBounds),focusUnchanged=Native.GetForegroundWindow()==foreground}));
            }
            if(candidates.Count==0)Console.WriteLine("No eligible Brave PiP or Stremio window is open.");
            return;
        }
        var panel=new Border{Background=Brushes.Red};
        var window=new Window{Title="Battlestation synthetic video test",Width=480,Height=300,Left=30,Top=40,WindowStyle=WindowStyle.None,ShowActivated=false,ShowInTaskbar=false,Content=panel};
        var cover=new Window{Title="Battlestation synthetic occluder",Width=500,Height=320,Left=20,Top=30,WindowStyle=WindowStyle.None,ShowActivated=false,ShowInTaskbar=false,Background=Brushes.Blue};
        using var capture=new VideoCapture();
        try
        {
            window.Show();Pump(250);var handle=new WindowInteropHelper(window).Handle;
            capture.Start(handle);Check(capture.Running,$"Native start failed: {capture.Error:X8}");
            Wait(()=>capture.Read()&&capture.Image is not null,$"No first frame ({capture.Error:X8})");
            byte[] Pixel(){var b=new byte[4];capture.Image!.CopyPixels(new Int32Rect(capture.Image.PixelWidth/2,capture.Image.PixelHeight/2,1,1),b,4,0);return b;}
            Check(Pixel()[2]>220&&Pixel()[0]<30,"Real source pixels are red");
            ulong before=capture.Frames;cover.Show();panel.Background=Brushes.Lime;
            Wait(()=>capture.Read()&&capture.Frames>before&&Pixel()[1]>220&&Pixel()[0]<30,"Capture must follow occluded source, never occluder pixels");
            cover.Hide();window.Width=640;window.Height=400;panel.Background=Brushes.Yellow;
            Wait(()=>{capture.Read();return capture.Image!.PixelWidth>=620&&Pixel()[2]>220&&Pixel()[1]>220;},"Capture follows resized source");
            var foreground=Native.GetForegroundWindow();
            Native.SetWindowPos(handle,-1,0,0,0,0,0x613);
            using(var lease=new VideoWindowLease(new(handle,Environment.ProcessId,"youtube",false,false)))
            {
                lease.Adopt();Check((Native.GetWindowLongPtr(handle,-20)&8)==0,"Dock demotes its backing PiP");
                Native.ShowWindow(handle,7);Check(VideoSources.IsIconic(handle),"Test source minimized");
                lease.Maintain();Check(!VideoSources.IsIconic(handle),"Minimized source becomes drawable behind desktop");
                Check(Native.GetForegroundWindow()==foreground,"Maintaining video must not take focus");
                panel.Background=Brushes.Magenta;
                Wait(()=>{capture.Read();return Pixel()[0]>220&&Pixel()[2]>220&&Pixel()[1]<30;},"Frames resume after nonactivating restore");
            }
            Check(VideoSources.IsIconic(handle),"Releasing dock restores user minimized state");
            Check((Native.GetWindowLongPtr(handle,-20)&8)!=0,"Releasing dock restores PiP topmost state");
            Native.ShowWindow(handle,4);Native.SetWindowPos(handle,-2,0,0,0,0,0x613);Pump(100);
            var dockWindow=new Window{Title="Synthetic dock frame",Left=60,Top=60,Width=400,Height=260,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowActivated=false,ShowInTaskbar=false};
            dockWindow.Show();Native.GetWindowRect(handle,out var beforeDock);var beforeDockFocus=Native.GetForegroundWindow();
            try
            {
                using(var backing=new VideoWindowLease(new(handle,Environment.ProcessId,"stremio",false,false)))
                {
                    backing.Place(new WindowInteropHelper(dockWindow).Handle,new Rect(100,100,320,180));
                    Check(backing.Prepare(),"Normal source can prepare directly behind dock");
                    Native.GetWindowRect(handle,out var prepared);Check(prepared.Left==100&&prepared.Top==100&&prepared.Right-prepared.Left==320,"Source switch starts compact, not at original full size");
                    for(int i=0;i<3;i++){backing.Prepare();backing.Maintain();Native.GetWindowRect(handle,out var repeated);Check(prepared.Equals(repeated),"Re-selecting the held source never restores its original window");}
                    Check(Native.GetForegroundWindow()==beforeDockFocus,"Preparing/re-selecting source preserves focus");
                }
                Native.GetWindowRect(handle,out var released);Check(released.Equals(beforeDock),"Final release restores original geometry");
                Native.ShowWindow(handle,7);
                using(var backing=new VideoWindowLease(new(handle,Environment.ProcessId,"stremio",true,false)))
                {
                    backing.Place(new WindowInteropHelper(dockWindow).Handle,new Rect(100,100,320,180));
                    Check(backing.Prepare()&&!VideoSources.IsIconic(handle),"Previously minimized source restores directly into the dock");
                    Native.GetWindowRect(handle,out var compact);Check(compact.Left==100&&compact.Top==100&&compact.Right-compact.Left==320,"Restore rectangle is compact before the source is shown");
                }
                Check(VideoSources.IsIconic(handle),"Initially minimized state survives capture release");
                Native.ShowWindow(handle,4);Native.GetWindowRect(handle,out released);Check(released.Equals(beforeDock),"Original normal bounds survive minimized capture");
            }
            finally{dockWindow.Close();}
            capture.Stop();Check(!capture.Running&&capture.Image is null&&capture.Frames==0,"Stopping releases retained pixels");
            for(int i=0;i<5;i++)
            {
                capture.Start(handle);panel.Background=i%2==0?Brushes.Red:Brushes.Lime;
                Wait(()=>capture.Read(),"Repeated capture startup must deliver frames");capture.Stop();
            }
            capture.Start(0);Check(!capture.Running&&capture.Error<0,"Invalid source fails without a substitute");
            Console.WriteLine("PASS: real WGC pixels, occlusion, resize, compact source preparation/re-selection, minimized restoration without focus, original bounds/minimized/topmost restoration, repeated start/stop and invalid HWND. No user media captured.");
        }
        catch {Console.WriteLine($"capture: {capture.Image?.PixelWidth}x{capture.Image?.PixelHeight}, error={capture.Error:X8}, age={capture.Age}, frames={capture.Frames}");throw;}
        finally {capture.Stop();cover.Close();window.Close();}
    }
}
