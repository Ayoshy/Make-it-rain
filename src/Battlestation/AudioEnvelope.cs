using NAudio.Dsp;

namespace Battlestation;
// Only a short in-memory window and aggregate bands. No recording or transcript.
internal sealed class AudioEnvelope
{
    readonly float[] samples=new float[2048];
    readonly Complex[] fft=new Complex[2048];
    static readonly float[] window=Enumerable.Range(0,2048).Select(i=>(float)FastFourierTransform.HannWindow(i,2048)).ToArray();
    int count;
    internal void Reset(){count=0;Array.Clear(samples);}
    internal float[]? Push(float sample,int sampleRate)
    {
        samples[count++]=float.IsFinite(sample)?Math.Clamp(sample,-1,1):0;
        if(count<samples.Length)return null;
        for(int i=0;i<fft.Length;i++){fft[i].X=samples[i]*window[i];fft[i].Y=0;}
        FastFourierTransform.FFT(true,11,fft);
        var bands=new float[12];
        for(int band=0;band<bands.Length;band++)
        {
            double low=40*Math.Pow(16000d/40,band/12d),high=40*Math.Pow(16000d/40,(band+1)/12d);float magnitude=0;
            for(int i=Math.Max(1,(int)Math.Ceiling(low*fft.Length/sampleRate));i<=Math.Min(1023,(int)Math.Ceiling(high*fft.Length/sampleRate));i++)
                magnitude=Math.Max(magnitude,MathF.Sqrt(fft[i].X*fft[i].X+fft[i].Y*fft[i].Y));
            bands[band]=Math.Clamp((20*MathF.Log10(Math.Max(1e-8f,magnitude))+55)/55,0,1);
        }
        Array.Copy(samples,1024,samples,0,1024);count=1024;return bands;
    }
}
