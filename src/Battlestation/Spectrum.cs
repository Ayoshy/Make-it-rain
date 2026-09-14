using NAudio.Wave;
using NAudio.Dsp;

namespace Battlestation;
internal sealed class Spectrum : IDisposable
{
    WasapiLoopbackCapture? capture;
    readonly float[] samples=new float[1024];
    int count;
    public float[] Bands {get;private set;}=new float[12];
    public string? Error {get;private set;}
    public void Start()
    {
        if(capture is not null)return;
        try{capture=new WasapiLoopbackCapture();capture.DataAvailable+=OnAudio;capture.StartRecording();Error=null;}
        catch(Exception e){Error=e.GetType().Name;Stop();}
    }
    void OnAudio(object? sender,WaveInEventArgs e)
    {
        var format=capture!.WaveFormat;int bytes=format.BitsPerSample/8,channels=format.Channels;
        for(int pos=0;pos+format.BlockAlign<=e.BytesRecorded;pos+=format.BlockAlign)
        {
            float sum=0;for(int c=0;c<channels;c++){int i=pos+c*bytes;sum+=bytes==4?BitConverter.ToSingle(e.Buffer,i):bytes==2?BitConverter.ToInt16(e.Buffer,i)/32768f:0;}
            samples[count++]=sum/channels;
            if(count<1024)continue;
            var fft=new Complex[1024];for(int i=0;i<1024;i++)fft[i].X=samples[i]*(float)FastFourierTransform.HannWindow(i,1024);
            FastFourierTransform.FFT(true,10,fft);
            var bands=new float[12];
            for(int band=0;band<12;band++)
            {
                double low=60*Math.Pow(14000d/60,band/12d),high=60*Math.Pow(14000d/60,(band+1)/12d);float magnitude=0;
                for(int i=Math.Max(1,(int)(low*1024/format.SampleRate));i<=Math.Min(511,(int)(high*1024/format.SampleRate));i++)magnitude=Math.Max(magnitude,MathF.Sqrt(fft[i].X*fft[i].X+fft[i].Y*fft[i].Y));
                bands[band]=Math.Clamp((20*MathF.Log10(Math.Max(1e-8f,magnitude))+45)/45,0,1);
            }
            Bands=bands;Array.Copy(samples,512,samples,0,512);count=512;
        }
    }
    public void Stop(){if(capture is not null){capture.DataAvailable-=OnAudio;capture.StopRecording();capture.Dispose();capture=null;}Bands=new float[12];count=0;}
    public void Dispose()=>Stop();
}
