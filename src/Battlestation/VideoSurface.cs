using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class VideoSurface : Surface,IDisposable
{
    readonly VideoCapture capture=new();
    readonly VideoBrowserBridge browser=new();
    readonly DispatcherTimer timer;
    readonly string preferences;
    VideoWindowLease? lease;
    VideoSource? source,stremio,leasedSource;
    StremioControlState controls=new(false,null);
    string mode="auto",kind="",wanted="",transportError="";
    bool visible,editing,armed,disposed,readingControls,transportBusy,occluded;
    public void SetOccluded(bool value){if(occluded==value)return;occluded=value;if(value){timer.Stop();Release();}else if(visible&&!editing){timer.Start();nextScan=0;Update();}}
    long nextScan,lastBrowserFrame;
    long nextBrowserRetry;
    long browserDrawn,lastDrawn;
    int renderQueued;
    public VideoSurface(Station station):base(station,9)
    {
        preferences=Path.Combine(station.Data,"video.json");
        browser.FrameAvailable+=BrowserFrame;
        capture.FrameAvailable+=BrowserFrame;
        try {if(File.Exists(preferences)){var saved=JsonSerializer.Deserialize<string>(File.ReadAllText(preferences));if(saved is "auto" or "youtube" or "stremio")mode=saved;}}catch(JsonException){}
        timer=new DispatcherTimer(TimeSpan.FromMilliseconds(1000),DispatcherPriority.Background,(_,_)=>Update(),Dispatcher);timer.Stop();
    }
    void BrowserFrame()
    {
        if(Interlocked.Exchange(ref renderQueued,1)!=0)return;
        Dispatcher.BeginInvoke(()=>{Interlocked.Exchange(ref renderQueued,0);if(!disposed&&visible&&!editing&&armed){if(kind=="stremio")capture.Read();Refresh();}},DispatcherPriority.Render);
    }
    public void SetActive(bool value,bool organizing)
    {
        if(visible==value&&editing==organizing)return;
        visible=value;editing=organizing;if(!visible)armed=false;
        if(visible&&!editing&&!occluded){timer.Start();nextScan=0;Update();}else{timer.Stop();Release();}
    }
    void Release(bool keepPlayer=false)
    {
        browser.Stop();capture.Stop();
        if(!keepPlayer){lease?.Dispose();lease=null;leasedSource=null;}
        source=null;kind="";lastBrowserFrame=0;browserDrawn=lastDrawn=0;transportError="";
    }
    public void StartMirror(){armed=true;Release();nextScan=0;Update();}
    public void StopMirror(){armed=false;Release();nextScan=0;Update();}
    public void Select(string value)
    {
        if(value is not ("auto" or "youtube" or "stremio"))throw new ArgumentException("Source vidéo inconnue");
        mode=value;lease?.ProtectFocusTransition();DesktopSettings.Write(preferences,JsonSerializer.Serialize(mode));nextScan=0;Update();
    }
    string Resolve(BrowserVideoState web)
    {
        if(mode!="auto")return mode;
        bool youtube=web.Connected&&web.Tabs.Any(t=>t.Ready);
        if(stremio?.Foreground==true)return "stremio";
        if(youtube&&VideoSources.BraveForeground())return "youtube";
        if(kind=="youtube"&&youtube||kind=="stremio"&&stremio is not null)return kind;
        return youtube?"youtube":stremio is not null?"stremio":"";
    }
    void Update()
    {
        if(disposed||!visible||editing||occluded)return;
        var dpi=VisualTreeHelper.GetDpi(this);int captureWidth=(int)Math.Ceiling(Math.Max(160,Width-24)*dpi.DpiScaleX),captureHeight=(int)Math.Ceiling(Math.Max(90,Height-68)*dpi.DpiScaleY);
        capture.Configure(captureWidth,captureHeight);browser.Configure(captureWidth,captureHeight);
        PlaceBackingWindow();lease?.Maintain();bool scanned=false;var web=browser.State;
        if(Environment.TickCount64>=nextScan)
        {
            nextScan=Environment.TickCount64+1000;scanned=true;
            if(web.Tabs.Length==0||!web.Tabs.Any(t=>t.Ready))browser.Discover();
            var candidates=VideoSources.Find().Where(s=>s.Kind=="stremio").ToArray();
            stremio=candidates.FirstOrDefault(s=>s.Foreground)??candidates.FirstOrDefault(s=>s.Handle==source?.Handle)??candidates.FirstOrDefault();
            wanted=Resolve(web);if(stremio is not null&&!readingControls)_=ReadControls(stremio);
            if(armed)Reconcile(web);
        }
        if(source is not null&&(!Native.IsWindow(source.Handle)||!Native.IsWindowVisible(source.Handle))){Release();nextScan=0;}
        bool fresh=kind=="stremio"&&capture.Read();
        if(kind=="stremio"&&capture.Age>1500&&controls.Playing==true)lease?.ParkForMirror();
        if(kind=="youtube"){web=browser.State;fresh=web.Frames!=lastBrowserFrame;lastBrowserFrame=web.Frames;}
        if(capture.Error<0){lease?.Dispose();lease=null;}
        timer.Interval=TimeSpan.FromMilliseconds(1000);if(fresh||scanned)Refresh();
    }
    void PlaceBackingWindow()
    {
        var window=Window.GetWindow(this);if(window is null)return;
        // Keep the source window AND its DWM shadow strictly inside the opaque
        // video well, including the rounded corners. Its original size is leased.
        lease?.Place(new WindowInteropHelper(window).Handle,new Rect(DesktopX+48,DesktopY+76,Math.Max(120,Width-96),Math.Max(80,Height-108)));
    }
    void Reconcile(BrowserVideoState web)
    {
        if(wanted=="youtube")
        {
            var tabs=web.Tabs.Where(t=>t.Ready).ToArray();
            var tab=VideoSources.BraveForeground()?tabs.FirstOrDefault(t=>t.Active):tabs.FirstOrDefault(t=>t.Id==web.TabId);
            tab??=tabs.OrderByDescending(t=>t.Playing).ThenByDescending(t=>t.Used).FirstOrDefault();
            if(!web.Connected||tab is null){if(kind!="")Release(true);return;}
            if(kind!="youtube"||web.TabId!=tab.Id){Release(true);kind="youtube";browser.Start(tab.Id);nextBrowserRetry=Environment.TickCount64+5000;}
            else if(web.Error!=""&&web.Error!="play-blocked"&&Environment.TickCount64>=nextBrowserRetry){browser.Start(tab.Id,true);nextBrowserRetry=Environment.TickCount64+5000;}
        }
        else if(wanted=="stremio"&&stremio is not null)
        {
            if(kind=="stremio"&&source?.Handle==stremio.Handle&&source.Pid==stremio.Pid)return;
            Release(true);kind="stremio";source=stremio;
            if(lease is null||leasedSource is null||leasedSource.Handle!=source.Handle||leasedSource.Pid!=source.Pid){lease?.Dispose();lease=new VideoWindowLease(source);leasedSource=source;}
            PlaceBackingWindow();
            if(lease.Prepare()){capture.Start(source.Handle);if(capture.Running)lease.Adopt();}
            if(!capture.Running){lease.Dispose();lease=null;}
        }
        else if(kind!="")Release(true);
    }
    async Task ReadControls(VideoSource target)
    {
        readingControls=true;
        try{var result=await Task.Run(()=>StremioControls.Read(target));if(!disposed&&stremio?.Handle==target.Handle){controls=result;Refresh();}}
        finally{readingControls=false;}
    }
    public async void TogglePlayback()
    {
        if(!armed||Picture is null){StartMirror();return;}
        if(kind=="youtube"){browser.Toggle();return;}
        if(source is null||transportBusy)return;
        var target=source;transportBusy=true;lease?.ProtectFocusTransition();
        try{bool sent=await Task.Run(()=>StremioControls.Toggle(target));if(!disposed){transportError=sent?"":"Commande indisponible";nextScan=0;Update();}}
        finally{transportBusy=false;}
    }
    void ReturnToSource()
    {
        if(kind=="youtube"){browser.Focus();return;}
        if(lease is not null){lease.Reveal();return;}
        var target=source??stremio;if(target is null||!Native.IsWindow(target.Handle))return;
        Native.GetWindowThreadProcessId(target.Handle,out uint pid);if(pid!=target.Pid)return;
        if(VideoSources.IsIconic(target.Handle))Native.ShowWindow(target.Handle,9);
        Native.SetForegroundWindow(target.Handle);
    }
    BitmapSource? Picture=>kind=="youtube"?browser.State.Image:capture.Error==0?capture.Image:null;
    bool? Playing=>kind=="youtube"?browser.State.Playing:kind=="stremio"?controls.Playing:null;
    public object Inspect()
    {
        var web=browser.State;
        return new{mode,visible,armed,kind,wanted,sourceHwnd=(long)(source?.Handle??0),sourcePid=source?.Pid,maintaining=lease?.Maintaining==true,nativeFrames=capture.Frames,nativeWidth=capture.Image?.PixelWidth,nativeHeight=capture.Image?.PixelHeight,nativeAgeMs=capture.Age,nativeError=capture.Error,
            browser=new{web.Connected,web.TabId,web.Frames,drawnFrames=browserDrawn,web.DecodedFrames,web.EncodeMs,web.Visibility,web.Version,web.Diagnostic,width=web.Image?.PixelWidth,height=web.Image?.PixelHeight,frameAgeMs=web.LastFrame==0?(long?)null:Environment.TickCount64-web.LastFrame,web.Playing,web.Error,available=web.Tabs.Count(t=>t.Ready)},playing=Playing,transportError};
    }
    protected override void Paint()
    {
        double w=ActualWidth>0?ActualWidth:576,h=ActualHeight>0?ActualHeight:372;
        Header("VIDÉO");double x=92;
        foreach(var (key,label,width) in new[]{("auto","Auto",54d),("youtube","YouTube",72d),("stremio","Stremio",72d)})
        {
            string chosen=key;Button("VideoSource:"+key,label,x,12,width,32,()=>Select(chosen),10,mode==key?"#F6EFFF":Muted);
            if(mode==key)Line(x+16,43,x+width-16,43,"#CFB6E3",2);x+=width+8;
        }
        if(armed)Button("VideoStop","■",w-100,12,32,32,StopMirror,10);
        if(kind!="")Button("VideoReturn","↗",w-58,12,34,32,ReturnToSource,15);
        var screen=new Rect(12,56,w-24,h-68);D.PushClip(new RectangleGeometry(screen,16,16));D.DrawRectangle(B("#FF100E16"),null,screen);
        if(Picture is {} picture)
        {
            if(kind=="youtube"){long frame=browser.State.Frames;if(frame!=lastDrawn){lastDrawn=frame;browserDrawn++;}}
            double scale=Math.Min(screen.Width/picture.PixelWidth,screen.Height/picture.PixelHeight);
            var bounds=new Rect(screen.X+(screen.Width-picture.PixelWidth*scale)/2,screen.Y+(screen.Height-picture.PixelHeight*scale)/2,picture.PixelWidth*scale,picture.PixelHeight*scale);D.DrawImage(picture,bounds);
            if(screen.Contains(Pointer)||Playing==false){Box(w/2-27,screen.Y+screen.Height/2-27,54,54,"#A0282032","#60EEE4FF",27);Text(Playing==false?"▶":"Ⅱ",w/2,screen.Y+screen.Height/2-17,19,"#F8F2FF",align:"center");}
            bool stale=kind=="stremio"?capture.Age>2500:browser.State.LastFrame>0&&Environment.TickCount64-browser.State.LastFrame>2500;
            if(stale&&Playing!=false){Box(screen.X+12,screen.Bottom-36,112,24,"#D5181420",radius:12);Text("Image arrêtée",screen.X+68,screen.Bottom-33,9,Muted,align:"center");}
            Hit("VideoPlayPause",screen.X,screen.Y,screen.Width,screen.Height,TogglePlayback);
        }
        else
        {
            var web=browser.State;
            string text=!armed?"Activer le miroir":wanted=="youtube"&&!web.Connected?"Brave non connecté":wanted=="youtube"&&web.Error!=""?"Capture YouTube indisponible":wanted==""?"Ouvrir une vidéo":capture.Error<0?"Capture indisponible":"En attente du lecteur";
            Text("▷",w/2,screen.Y+screen.Height/2-52,30,"#CBBFDA",align:"center");Text(text,w/2,screen.Y+screen.Height/2+8,12,Ink,align:"center");
            Hit("VideoStart",screen.X,screen.Y,screen.Width,screen.Height,StartMirror);
        }
        if(transportError!="")Text(transportError,w/2,screen.Bottom-25,9,Muted,align:"center");D.Pop();
    }
    public void Dispose(){disposed=true;timer.Stop();browser.FrameAvailable-=BrowserFrame;capture.FrameAvailable-=BrowserFrame;Release();capture.Dispose();browser.Dispose();}
}
