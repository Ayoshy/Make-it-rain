using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MayhemReader;

static class Native
{
    [DllImport("ReaderNative", CallingConvention=CallingConvention.Cdecl)] internal static extern int ReaderInit();
    [DllImport("ReaderNative", CallingConvention=CallingConvention.Cdecl)] internal static extern int ReaderStage();
    [DllImport("ReaderNative", CallingConvention=CallingConvention.Cdecl)] internal static extern int ReaderStart(nint window);
    [DllImport("ReaderNative", CallingConvention=CallingConvention.Cdecl)] internal static extern void ReaderStop();
    [DllImport("ReaderNative", CallingConvention=CallingConvention.Cdecl)] internal static extern int ReaderFrame(byte[] data,int capacity,out int w,out int h,out long ticks,out int x,out int y,out int cw,out int ch);
    [DllImport("ReaderNative", CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] internal static extern int ReaderOcr(byte[] data,int w,int h,StringBuilder json,int capacity);
    [DllImport("user32")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32")] internal static extern bool ShowWindow(nint hwnd,int command);
    [DllImport("user32")] internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
}
record OcrLine(string text,double x,double y,double w,double h);
record Augment(string Name,int[] Ids);
record Match(string Name,int[] Ids,double Score,double Margin,double Y,double Height);

sealed class Matcher
{
    readonly (Augment Item,string Key)[] names;
    public Matcher(string catalog) => names=JsonDocument.Parse(File.ReadAllText(catalog)).RootElement.EnumerateArray()
        .Select(e=>new {Name=e.GetProperty("nameTRA").GetString()!,Id=e.GetProperty("id").GetInt32()})
        .GroupBy(a=>Normalize(a.Name)).Where(g=>g.Key.Length>=3)
        .Select(g=>(new Augment(g.First().Name,g.Select(a=>a.Id).Distinct().Order().ToArray()),g.Key)).ToArray();
    internal static string Normalize(string text)=>Regex.Replace(text.Normalize(NormalizationForm.FormD).ToLowerInvariant(),@"[^a-z0-9]","");
    static double Similarity(string a,string b)
    {
        if(a==b)return 1;
        if(Math.Abs(a.Length-b.Length)>Math.Max(a.Length,b.Length)*.22)return 0;
        var previous=Enumerable.Range(0,b.Length+1).ToArray();var next=new int[b.Length+1];
        for(int i=1;i<=a.Length;i++){next[0]=i;for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(previous[j]+1,next[j-1]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));(previous,next)=(next,previous);}
        return 1-(double)previous[b.Length]/Math.Max(a.Length,b.Length);
    }
    public Match?[] Read(OcrLine[] lines,int width)
    {
        var result=new Match?[3];
        for(int slot=0;slot<3;slot++)
        {
            var local=lines.Where(l=>(int)((l.x+l.w/2)*3/width)==slot && l.h>=10 && l.w<width*.4).OrderBy(l=>l.y).ToList();
            var candidates=new List<(string Text,double Y,double H)>();
            for(int i=0;i<local.Count;i++){
                var line=local[i];candidates.Add((Normalize(line.text),line.y,line.h));
                if(i+1<local.Count && local[i+1].y-line.y<line.h*2.4)
                    candidates.Add((Normalize(line.text+local[i+1].text),line.y,line.h));
            }
            var scored=names.Select(n=>{
                var best=candidates.Select(c=>(Score:Similarity(c.Text,n.Key),c.Y,c.H)).OrderByDescending(c=>c.Score).FirstOrDefault();
                return new Match(n.Item.Name,n.Item.Ids,best.Score,0,best.Y,best.H);
            }).OrderByDescending(m=>m.Score).Take(2).ToArray();
            if(scored.Length<2)continue;
            var top=scored[0];double margin=top.Score-scored[1].Score;
            if(top.Score>=.88 && margin>=.07)result[slot]=top with {Margin=margin};
        }
        var found=result.Where(m=>m!=null).Select(m=>m!).ToArray();
        // Titles must form one row; uncertain partial readings remain in the raw OCR log.
        if(found.Length==3 && (found.Select(m=>m.Name).Distinct().Count()!=3 || found.Max(m=>m.Y)-found.Min(m=>m.Y)>found.Max(m=>m.Height)*2))
            return new Match?[3];
        return result;
    }
}

record Status(string State,string Detail,int Pid,DateTimeOffset Heartbeat,long Attempts,long Frames,long Errors,long Bytes,
    DateTimeOffset? LastFrame,double MaxGapMs,double CpuSeconds,long MemoryMiB,string[]? CurrentOffer,string? StopReason);

sealed class Collector
{
    public volatile Status Snapshot=new("starting","Initialisation",Environment.ProcessId,DateTimeOffset.UtcNow,0,0,0,0,null,0,0,0,null,null);
    public volatile bool StopRequested;
    readonly string run,catalog;
    readonly int seconds;
    readonly long maxBytes;
    readonly nint fixture;
    readonly Stopwatch elapsed=Stopwatch.StartNew();
    long attempts,frames,errors,bytes,priorTicks;
    double maxGap;
    DateTimeOffset? lastFrame;
    string[]? current;
    string? pending;
    int confirmations,misses;
    public Collector(string run,string catalog,int seconds,long maxBytes,nint fixture=0){this.run=run;this.catalog=catalog;this.seconds=seconds;this.maxBytes=maxBytes;this.fixture=fixture;}
    void Log(object value)
    {
        string line=JsonSerializer.Serialize(value)+"\n";int size=Encoding.UTF8.GetByteCount(line);
        if(bytes+size>maxBytes-65536)throw new StorageLimit();
        File.AppendAllText(Path.Combine(run,"observations.jsonl"),line,Encoding.UTF8);bytes+=size;
    }
    void Publish(string state,string detail,string? stop=null)
    {
        using var process=Process.GetCurrentProcess();
        Snapshot=new(state,detail,Environment.ProcessId,DateTimeOffset.UtcNow,attempts,frames,errors,bytes,lastFrame,maxGap,
            process.TotalProcessorTime.TotalSeconds,process.WorkingSet64/1048576,current,stop);
        string temp=Path.Combine(run,"status.tmp");File.WriteAllText(temp,JsonSerializer.Serialize(Snapshot,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temp,Path.Combine(run,"status.json"),true);
    }
    nint FindGame()
    {
        if(fixture!=0)return Native.IsWindow(fixture)?fixture:0;
        foreach(var p in Process.GetProcessesByName("League of Legends"))using(p){
            var window=p.MainWindowHandle;
            if(window!=0 && Native.GetWindowThreadProcessId(window,out uint id)!=0 && id==p.Id)return window;
        }
        return 0;
    }
    public void Run()
    {
        string reason="duration-limit";nint active=0;bool seen=false;double nextStatus=0;string state="waiting-game",previousState="",detail="En attente de League of Legends";
        try {
            Marshal.ThrowExceptionForHR(Native.ReaderInit());var matcher=new Matcher(catalog);
            Log(new{type="start",utc=DateTimeOffset.UtcNow,pid=Environment.ProcessId,seconds,maxBytes,language="en-US",fixture=fixture!=0,
                roi=new{x=.12,y=.22,w=.76,h=.40},requestedIntervalMs=250,catalogSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(catalog)))});
            byte[] pixels=new byte[4096*2160*4];var text=new StringBuilder(65536);
            var codec=ImageCodecInfo.GetImageEncoders().Single(c=>c.FormatID==ImageFormat.Jpeg.Guid);
            using var quality=new EncoderParameters(1);quality.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,85L);
            while(elapsed.Elapsed.TotalSeconds<seconds && !StopRequested && !File.Exists(Path.Combine(run,"stop.request")))
            {
                var iteration=Stopwatch.StartNew();var window=FindGame();
                if(window==0){
                    if(seen){reason="game-closed";break;}
                    state="waiting-game";detail="En attente de League of Legends";
                }else if(Native.IsIconic(window)){
                    state="minimized";detail="Jeu minimisé : aucune image enregistrée";current=null;pending=null;confirmations=0;
                }else{
                    if(active!=window){int start=Native.ReaderStart(window);if(start<0)throw new Exception($"Capture startup stage {Native.ReaderStage()}: 0x{start:X8}");active=window;seen=true;Log(new{type="window-attached",utc=DateTimeOffset.UtcNow,hwnd=window.ToInt64()});}
                    attempts++;
                    int hr=Native.ReaderFrame(pixels,pixels.Length,out int w,out int h,out long ticks,out int x,out int y,out int cw,out int ch);
                    if(hr<0){errors++;state="capture-error";detail=$"Capture : 0x{hr:X8}";current=null;pending=null;confirmations=0;Log(new{type="capture-error",utc=DateTimeOffset.UtcNow,hr});}
                    else if(hr!=0 || ticks<=priorTicks){
                        state="no-new-frame";detail="Aucune nouvelle image";
                        if(lastFrame==null || DateTimeOffset.UtcNow-lastFrame>TimeSpan.FromSeconds(1)){current=null;pending=null;confirmations=0;}
                    }
                    else {
                        var received=DateTimeOffset.UtcNow;double gap=priorTicks==0?0:(ticks-priorTicks)/10000.0;maxGap=Math.Max(maxGap,gap);priorTicks=ticks;
                        double ageMs=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency-ticks/10000.0;
                        if(ageMs>1000){current=null;pending=null;confirmations=0;state="stale-frame";detail="Image reçue trop ancienne";Log(new{type="stale-frame",utc=received,ageMs,gap});}
                        else {
                            using var bitmap=new Bitmap(w,h,PixelFormat.Format32bppRgb);
                            var locked=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppRgb);
                            try{for(int row=0;row<h;row++)Marshal.Copy(pixels,row*w*4,locked.Scan0+row*locked.Stride,w*4);}finally{bitmap.UnlockBits(locked);}
                            using var image=new MemoryStream();bitmap.Save(image,codec,quality);var jpg=image.ToArray();
                            if(bytes+jpg.Length>maxBytes-131072)throw new StorageLimit();
                            string filename=$"frame-{frames+1:D6}.jpg";File.WriteAllBytes(Path.Combine(run,filename),jpg);bytes+=jpg.Length;frames++;lastFrame=received;
                            int low=255,high=0;
                            for(int sample=0;sample<w*h;sample+=137){int v=(pixels[sample*4]+pixels[sample*4+1]+pixels[sample*4+2])/3;low=Math.Min(low,v);high=Math.Max(high,v);}
                            bool blank=high-low<8;
                            var ocrClock=Stopwatch.StartNew();text.Clear();hr=blank?0:Native.ReaderOcr(pixels,w,h,text,text.Capacity);
                            OcrLine[] lines=[];Match?[] matches=new Match?[3];
                            if(hr<0){errors++;}else if(!blank){lines=JsonSerializer.Deserialize<OcrLine[]>(text.ToString())??[];matches=matcher.Read(lines,w);}
                            bool complete=hr>=0 && matches.All(m=>m!=null);
                            string? key=complete?string.Join("|",matches.Select(m=>m!.Name)):null;
                            if(key!=null){misses=0;confirmations=key==pending?confirmations+1:1;pending=key;
                                if(confirmations>=2 && (current==null||string.Join("|",current)!=key)){
                                    current=matches.Select(m=>m!.Name).ToArray();Log(new{type="offer",utc=DateTimeOffset.UtcNow,frame=frames,names=current,ids=matches.Select(m=>m!.Ids),selection="unknown"});
                                }
                            }else{pending=null;confirmations=0;if(++misses>=2)current=null;}
                            state=hr<0?"ocr-error":blank?"blank-frame":current!=null?"offer":"capturing";
                            detail=hr<0?$"OCR : 0x{hr:X8}":blank?"Image uniforme : capture à vérifier":current!=null?string.Join(" / ",current):"Images reçues — trois offres non confirmées";
                            Log(new{type="frame",frame=frames,file=filename,receivedUtc=received,captureQpc100ns=ticks,captureAgeMs=ageMs,
                                gapMs=gap,crop=new{x,y,w,h,clientWidth=cw,clientHeight=ch},ocrMs=ocrClock.Elapsed.TotalMilliseconds,ocrHresult=hr,blank,lines,matches,complete,
                                readyUtc=DateTimeOffset.UtcNow});
                        }
                    }
                }
                if(state!=previousState){Log(new{type="state",utc=DateTimeOffset.UtcNow,elapsedMs=elapsed.Elapsed.TotalMilliseconds,state,frames});previousState=state;}
                if(elapsed.Elapsed.TotalMilliseconds>=nextStatus){Publish(state,detail);nextStatus=elapsed.Elapsed.TotalMilliseconds+1000;}
                Thread.Sleep(Math.Max(1,250-(int)iteration.ElapsedMilliseconds));
            }
            if(StopRequested||File.Exists(Path.Combine(run,"stop.request")))reason="explicit-stop";
        }catch(StorageLimit){reason="storage-limit";}
        catch(Exception ex){errors++;reason="error";detail=$"{ex.GetType().Name}: {ex.Message}";
            try{Log(new{type="fatal-error",utc=DateTimeOffset.UtcNow,error=detail});}catch{} }
        finally{
            try{Native.ReaderStop();}catch(Exception ex){errors++;detail=ex.Message;reason="cleanup-error";}
            current=null;try{Publish("stopped",detail,reason);}catch{};
        }
    }
    sealed class StorageLimit:Exception;
}

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
        string? Arg(string name)=>Array.IndexOf(args,name) is var i && i>=0 && i+1<args.Length?args[i+1]:null;
        string run=Arg("--run")??Path.Combine(root,"artifacts/validation/lol-reader","run-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(run);
        if(File.Exists(Path.Combine(run,"status.json")))throw new IOException("Use a new run directory");
        string catalog=Path.Combine(AppContext.BaseDirectory,"catalog.json");
        if(args.Contains("--matcher-checks")){Checks(catalog,run);return;}
        bool fixture=args.Contains("--fixture");
        int seconds=int.TryParse(Arg("--seconds"),out int s)?Math.Clamp(s,1,1800):1800;
        long cap=long.TryParse(Arg("--max-mib"),out long mb)?Math.Clamp(mb,1,256)*1048576:256*1048576;
        using var singleton=new Mutex(true,fixture?"Battlestation.MayhemReader.Fixture":"Battlestation.MayhemReader",out bool own);
        if(!own){MessageBox.Show("Un collecteur Mayhem est déjà ouvert.");return;}
        var form=new Form{Text=fixture?"Mayhem — fenêtre de test":"Mayhem — collecte locale",ClientSize=fixture?new Size(1280,720):new Size(570,225),StartPosition=FormStartPosition.CenterScreen};
        var label=new Label{Dock=DockStyle.Fill,Padding=new Padding(16),Font=new Font("Segoe UI",11),AutoSize=false};
        var stop=new Button{Text="Arrêter la collecte",Dock=DockStyle.Bottom,Height=38};
        if(!fixture){form.Controls.Add(label);form.Controls.Add(stop);}
        var fixtureClock=Stopwatch.StartNew();
        if(fixture){form.BackColor=Color.FromArgb(20,25,36);form.Paint+=(_,e)=>{
            using var font=new Font("Segoe UI",24,FontStyle.Bold);using var brush=new SolidBrush(Color.White);
            string[] names=fixtureClock.Elapsed.TotalSeconds<4?["FireFox","All For You","Infinite Recursion"]:["FireFox","Twin Fire","Infinite Recursion"];
            for(int i=0;i<3;i++){var size=e.Graphics.MeasureString(names[i],font);e.Graphics.DrawString(names[i],font,brush,(float)(form.ClientSize.Width*(.3+.2*i))-size.Width/2,(float)(form.ClientSize.Height*.46));}
        };}
        Collector? collector=null;Thread? worker=null;
        form.Shown+=(_,_)=>{collector=new Collector(run,catalog,seconds,cap,fixture?form.Handle:0);worker=new Thread(()=>{Thread.Sleep(700);collector.Run();}){IsBackground=false,Name="Mayhem capture and OCR"};worker.SetApartmentState(ApartmentState.MTA);worker.Start();};
        stop.Click+=(_,_)=>{if(collector!=null)collector.StopRequested=true;};
        form.FormClosing+=(_,e)=>{if(worker?.IsAlive==true){collector!.StopRequested=true;e.Cancel=true;stop.Enabled=false;stop.Text="Arrêt en cours…";}};
        bool madeVisible=false;
        using var timer=new System.Windows.Forms.Timer{Interval=300};timer.Tick+=(_,_)=>{
            if(!madeVisible){Native.ShowWindow(form.Handle,4);madeVisible=true;}
            if(fixture)form.Invalidate();
            if(collector==null)return;var v=collector.Snapshot;
            label.Text=$"{v.State} — {v.Detail}\n\nImages : {v.Frames}   Erreurs : {v.Errors}   Stockage : {v.Bytes/1048576.0:F1} / {cap/1048576} Mio\nDernière image : {(v.LastFrame?.ToLocalTime().ToString("HH:mm:ss")??"aucune")}   Statut : {v.Heartbeat.ToLocalTime():HH:mm:ss}\nCPU cumulé : {v.CpuSeconds:F1} s   Mémoire : {v.MemoryMiB} Mio\nArrêt : {v.StopReason??"—"}";
            if(worker?.IsAlive==false && (fixture||collector.StopRequested))form.Close();
        };timer.Start();Application.Run(form);worker?.Join(TimeSpan.FromSeconds(5));
    }
    static void Checks(string catalog,string run)
    {
        var matcher=new Matcher(catalog);int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
        OcrLine[] Lines(string middle)=>[new("FireFox",30,100,100,24),new(middle,400,100,170,24),new("Infinite Recursion",740,100,240,24)];
        var a=matcher.Read(Lines("All For You"),1000);Check(a.All(m=>m!=null),"three known offers");
        var b=matcher.Read(Lines("Twin Fire"),1000);Check(b[0]!.Name==a[0]!.Name && b[1]!.Name!=a[1]!.Name && b[2]!.Name==a[2]!.Name,"single-card reroll");
        Check(matcher.Read([new("ordinary game text",30,100,150,24)],1000).All(m=>m==null),"non-offer text");
        Check(matcher.Read(Lines("xxyyzzqq"),1000)[1]==null,"unknown stays unknown");
        var misaligned=Lines("Twin Fire");misaligned[1]=misaligned[1] with {y=220};Check(matcher.Read(misaligned,1000).All(m=>m==null),"unrelated rows rejected");
        Check(a[0]!.Ids.Length>0,"catalog IDs retained");
        File.WriteAllText(Path.Combine(run,"checks.txt"),$"MATCHER_CHECKS_PASS {count}\n");
    }
}
