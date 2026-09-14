using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace Battlestation;
internal sealed class Spectrum : IDisposable
{
    volatile WasapiLoopbackCapture? capture;
    readonly AutoResetEvent wake=new(false);
    readonly object workerGate=new();
    Thread? worker;
    volatile bool wanted,disposed;
    readonly AudioEnvelope analysis=new();
    readonly object gate=new();
    float[] bands=new float[12];
    static readonly float[] silence=new float[12];
    long lastAudio,nextDeviceCheck,nextRetry;
    string? deviceId;
    public float[] Bands=>Environment.TickCount64-Interlocked.Read(ref lastAudio)<300?Volatile.Read(ref bands):silence;
    public string? Error {get;private set;}
    public string? DeviceId=>deviceId;
    public bool Running=>capture is not null;
    public void Start()
    {
        if(disposed||wanted)return;
        lock(workerGate){if(disposed)return;wanted=true;if(worker is null){worker=new Thread(Run){IsBackground=true,Name="Battlestation spectrum",Priority=ThreadPriority.BelowNormal};worker.SetApartmentState(ApartmentState.MTA);worker.Start();}wake.Set();}
    }
    void Run()
    {
        try{while(!disposed){if(wanted)EnsureCapture();else StopCapture();wake.WaitOne(wanted?2000:Timeout.Infinite);}}
        finally{StopCapture();}
    }
    void EnsureCapture()
    {
        long now=Environment.TickCount64;
        if(now<nextRetry||capture is not null&&now<nextDeviceCheck)return;
        nextDeviceCheck=now+2000;
        try
        {
            using var enumerator=new MMDeviceEnumerator();
            using var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
            if(capture is not null&&device.ID==deviceId)return;
            StopCapture();deviceId=device.ID;
            capture=new WasapiLoopbackCapture(device);capture.DataAvailable+=OnAudio;capture.RecordingStopped+=OnStopped;
            capture.StartRecording();Error=null;
        }
        catch(Exception e){Error=e.GetType().Name;StopCapture();nextRetry=now+3000;}
    }
    void OnStopped(object? sender,StoppedEventArgs e){if(e.Exception is not null){Error=e.Exception.GetType().Name;nextDeviceCheck=0;deviceId=null;}}
    void OnAudio(object? sender,WaveInEventArgs e)
    {
        lock(gate)
        {
            if(sender is not WasapiLoopbackCapture source||!ReferenceEquals(source,capture))return;
            var format=source.WaveFormat;int bytes=format.BitsPerSample/8,channels=format.Channels;
            bool floating=format.Encoding==WaveFormatEncoding.IeeeFloat||format is WaveFormatExtensible ext&&ext.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71");
            for(int pos=0;pos+format.BlockAlign<=e.BytesRecorded;pos+=format.BlockAlign)
            {
                float sum=0;
                for(int c=0;c<channels;c++)
                {
                    int i=pos+c*bytes;
                    sum+=floating&&bytes==4?BitConverter.ToSingle(e.Buffer,i):bytes==2?BitConverter.ToInt16(e.Buffer,i)/32768f:
                        bytes==3?((e.Buffer[i]<<8|e.Buffer[i+1]<<16|e.Buffer[i+2]<<24)>>8)/8388608f:bytes==4?BitConverter.ToInt32(e.Buffer,i)/2147483648f:0;
                }
                if(analysis.Push(sum/channels,format.SampleRate) is {} next)Volatile.Write(ref bands,next);
            }
            Interlocked.Exchange(ref lastAudio,Environment.TickCount64);
        }
    }
    public void Stop()
    {
        if(!wanted)return;wanted=false;Volatile.Write(ref bands,silence);wake.Set();
    }
    void StopCapture()
    {
        WasapiLoopbackCapture? old;
        lock(gate){old=capture;capture=null;bands=silence;analysis.Reset();deviceId=null;}
        if(old is null)return;
        old.DataAvailable-=OnAudio;old.RecordingStopped-=OnStopped;
        try{old.StopRecording();}catch(Exception e) when(e is System.Runtime.InteropServices.COMException or InvalidOperationException){Error=e.GetType().Name;}
        finally{old.Dispose();}
    }
    public void Dispose(){lock(workerGate){disposed=true;wanted=false;wake.Set();}}
}
