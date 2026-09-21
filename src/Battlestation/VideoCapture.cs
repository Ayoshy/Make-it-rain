using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Battlestation;
// The worker owns WGC/D3D; WPF only copies the newest completed, bounded frame.
// A WPF bitmap is thread-affine: the copy stays on the UI thread until a
// D3DImage-style shared surface removes it entirely.
internal sealed class VideoCapture : IDisposable
{
    const string Library="Battlestation.Video.dll";
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int VideoStart(nint source);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void VideoStop();
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void VideoSize(int width,int height);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int VideoRead([Out] byte[] buffer,int capacity,out int width,out int height,out ulong serial,out int age);
    [DllImport("combase.dll")] static extern int RoInitialize(uint kind);
    [DllImport("combase.dll")] static extern void RoUninitialize();
    readonly object gate=new();
    readonly AutoResetEvent wake=new(false);
    Thread? worker;
    nint wanted;
    int generation,targetWidth=1280,targetHeight=720,frameWidth,frameHeight;
    byte[] pending=[];
    ulong pendingSerial;
    long lastFrame;
    volatile bool disposed;
    int error;
    public event Action? FrameAvailable;
    public WriteableBitmap? Image {get;private set;}
    public ulong Frames {get;private set;}
    public int Age=>Interlocked.Read(ref lastFrame)==0?int.MaxValue:(int)Math.Min(int.MaxValue,Environment.TickCount64-Interlocked.Read(ref lastFrame));
    public int Error=>Volatile.Read(ref error);
    public bool Running {get{lock(gate)return wanted!=0&&!disposed&&Error>=0;}}
    public void Configure(int width,int height){lock(gate){targetWidth=Math.Clamp(width,160,1920);targetHeight=Math.Clamp(height,90,1080);}wake.Set();}
    public void Start(nint source)
    {
        Stop();if(source==0){Volatile.Write(ref error,unchecked((int)0x80070057));return;}lock(gate){if(disposed)return;wanted=source;generation++;if(worker is null){worker=new Thread(Run){IsBackground=true,Name="Battlestation video",Priority=ThreadPriority.BelowNormal};worker.SetApartmentState(ApartmentState.MTA);worker.Start();}}wake.Set();
    }
    void Run()
    {
        int apartment=RoInitialize(1);int currentGeneration=-1;bool started=false;byte[] buffer=[];
        try
        {
            while(!disposed)
            {
                nint source;int version,w,h;
                lock(gate){source=wanted;version=generation;w=targetWidth;h=targetHeight;}
                if(version!=currentGeneration){VideoStop();started=false;currentGeneration=version;if(source!=0){int result=VideoStart(source);Volatile.Write(ref error,result);started=result>=0;}}
                if(started)
                {
                    VideoSize(w,h);if(buffer.Length<w*h*4)buffer=new byte[w*h*4];
                    int result=VideoRead(buffer,buffer.Length,out int width,out int height,out ulong serial,out int age);
                    bool published=false;
                    lock(gate)
                    {
                        if(version==generation&&wanted!=0&&!disposed){Volatile.Write(ref error,result<0?result:0);if(result==0&&width>0&&height>0){(pending,buffer)=(buffer,pending);frameWidth=width;frameHeight=height;pendingSerial=serial;Interlocked.Exchange(ref lastFrame,Environment.TickCount64-age);published=true;}}
                    }
                    if(published)FrameAvailable?.Invoke();
                }
                wake.WaitOne(source==0?Timeout.Infinite:34);
            }
        }
        catch(Exception e) when(e is ExternalException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException){Volatile.Write(ref error,unchecked((int)0x80004005));}
        finally{VideoStop();if(apartment>=0)RoUninitialize();}
    }
    public bool Read()
    {
        lock(gate)
        {
            if(wanted==0||pendingSerial==0||pendingSerial==Frames)return false;
            if(Image is null||Image.PixelWidth!=frameWidth||Image.PixelHeight!=frameHeight)Image=new WriteableBitmap(frameWidth,frameHeight,96,96,PixelFormats.Bgra32,null);
            Image.WritePixels(new Int32Rect(0,0,frameWidth,frameHeight),pending,frameWidth*4,0);Frames=pendingSerial;return true;
        }
    }
    public void Stop(){lock(gate){wanted=0;generation++;pendingSerial=0;Image=null;Frames=0;Interlocked.Exchange(ref lastFrame,0);Volatile.Write(ref error,0);}wake.Set();}
    public void Dispose(){Stop();disposed=true;wake.Set();}
}
