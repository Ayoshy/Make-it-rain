using System.Diagnostics;
namespace Battlestation;
internal record AudioOutput(string Id,string Name,bool Selected);
internal record AudioApp(string Key,string Name,float Volume,bool Muted,float Peak);
internal sealed class AudioMixer:IDisposable
{
    internal static readonly ManualResetEventSlim Entered=new(false),Release=new(false);
    internal static readonly List<int> Threads=[];
    internal static int Writes;
    public IReadOnlyList<AudioOutput> Outputs=>[];
    public IReadOnlyList<AudioApp> Apps=>[];
    public string OutputName=>"Fixture";
    public string? OutputId=>"fixture";
    public string MicrophoneName=>"Fixture mic";
    public float? Volume{get;private set;}=.5f;
    public bool? Muted=>false;
    public bool? MicrophoneMuted=>false;
    public string Error=>"";
    internal void Poll(){Threads.Add(Environment.CurrentManagedThreadId);Entered.Set();Release.Wait();}
    internal void SetVolume(string? key,float value){Threads.Add(Environment.CurrentManagedThreadId);Writes++;Volume=value;}
    internal void ToggleMute(string? key)=>throw new Exception("Unexpected mute");
    internal void ToggleMicrophone()=>throw new Exception("Unexpected mic write");
    internal void SelectOutput(string id)=>throw new Exception("Unexpected device write");
    public void Dispose()=>Threads.Add(Environment.CurrentManagedThreadId);
}
internal static class AudioWorkerTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Main()
    {
        int ui=Environment.CurrentManagedThreadId;
        using var worker=new AudioMixerWorker();worker.Poll();Check(AudioMixer.Entered.Wait(3000),"Worker began the slow driver call");
        try
        {
            var clock=Stopwatch.StartNew();worker.Poll();worker.SetVolume(null,.1f);worker.SetVolume(null,.9f);var output=worker.OutputName;
            Check(clock.ElapsedMilliseconds<100,"UI reads and requests must not wait for the blocked audio driver");
            AudioMixer.Release.Set();Check(SpinWait.SpinUntil(()=>worker.Volume==.9f,3000),"Latest slider value reaches the worker snapshot");
            worker.SetActive(false);
            Check(AudioMixer.Writes==1,"Rapid slider updates coalesce before reaching the driver");
            Check(AudioMixer.Threads.All(t=>t!=ui)&&AudioMixer.Threads.Distinct().Count()==1,"One worker owns all audio calls");
            var timer=Stopwatch.StartNew();worker.Dispose();Check(timer.ElapsedMilliseconds<100,"Disposal never stalls the UI");
        }
        finally{AudioMixer.Release.Set();}
        Console.WriteLine("PASS: blocked audio driver never blocks UI reads or commands, slider coalescing, single-thread audio ownership and nonblocking shutdown. Fixture only, no system audio writes.");
    }
}
