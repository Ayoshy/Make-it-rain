using System.Collections.Concurrent;

namespace Battlestation;

internal sealed record AudioMixerSnapshot(IReadOnlyList<AudioOutput> Outputs,IReadOnlyList<AudioApp> Apps,
    string OutputName,string? OutputId,string MicrophoneName,float? Volume,bool? Muted,bool? MicrophoneMuted,string Error);

// All Core Audio COM objects and writes stay on one worker. The WPF thread only
// reads immutable snapshots and queues explicit user actions; polling never waits
// for a driver in the input/render loop.
internal sealed class AudioMixerWorker : IDisposable
{
    readonly AutoResetEvent wake=new(false);
    readonly ConcurrentQueue<Action<AudioMixer>> actions=new();
    readonly ConcurrentDictionary<string,float> volumes=new();
    readonly Thread thread;
    volatile bool active,disposed;
    AudioMixerSnapshot snapshot=new([],[],"Audio…",null,"Micro indisponible",null,null,null,"");
    AudioMixerSnapshot State=>Volatile.Read(ref snapshot);
    public IReadOnlyList<AudioOutput> Outputs=>State.Outputs;
    public IReadOnlyList<AudioApp> Apps=>State.Apps;
    public string OutputName=>State.OutputName;
    public string? OutputId=>State.OutputId;
    public string MicrophoneName=>State.MicrophoneName;
    public float? Volume=>State.Volume;
    public bool? Muted=>State.Muted;
    public bool? MicrophoneMuted=>State.MicrophoneMuted;
    public string Error=>State.Error;
    public long PollCount {get;private set;}
    public double PollMilliseconds {get;private set;}
    public long MeterCount {get;private set;}
    public double MeterMilliseconds {get;private set;}
    internal AudioMixerWorker()
    {
        thread=new Thread(Run){IsBackground=true,Name="Battlestation audio"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();
    }
    internal void SetActive(bool value){if(disposed||active==value)return;active=value;wake.Set();}
    internal void Poll()=>SetActive(true);
    void Queue(Action<AudioMixer> action){if(disposed)return;actions.Enqueue(action);wake.Set();}
    internal void ToggleMute(string? key)=>Queue(m=>m.ToggleMute(key));
    internal void ToggleMicrophone()=>Queue(m=>m.ToggleMicrophone());
    internal void SelectOutput(string id)=>Queue(m=>m.SelectOutput(id));
    internal void SetVolume(string? key,float value)
    {
        if(disposed||!float.IsFinite(value))return;
        value=Math.Clamp(value,0,1);volumes[key??""]=value;wake.Set();
    }
    void Run()
    {
        try
        {
            using var mixer=new AudioMixer();long nextPoll=0,nextMeter=0;
            while(!disposed)
            {
                bool changed=false;
                while(!disposed&&actions.TryDequeue(out var action)){action(mixer);changed=true;}
                foreach(string key in volumes.Keys)if(!disposed&&volumes.TryRemove(key,out float value)){mixer.SetVolume(key==""?null:key,value);changed=true;}
                if(active&&Environment.TickCount64>=nextPoll)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();mixer.Poll();PollMilliseconds+=watch.Elapsed.TotalMilliseconds;PollCount++;
                    changed=true;nextPoll=Environment.TickCount64+500;
                }
                if(active&&Environment.TickCount64>=nextMeter)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();mixer.PollPeaks();MeterMilliseconds+=watch.Elapsed.TotalMilliseconds;MeterCount++;
                    changed=true;nextMeter=Environment.TickCount64+33;
                }
                if(changed)Volatile.Write(ref snapshot,new(mixer.Outputs.ToArray(),mixer.Apps.ToArray(),mixer.OutputName,mixer.OutputId,mixer.MicrophoneName,mixer.Volume,mixer.Muted,mixer.MicrophoneMuted,mixer.Error));
                wake.WaitOne(active?(int)Math.Clamp(Math.Min(nextPoll,nextMeter)-Environment.TickCount64,1,500):Timeout.Infinite);
            }
        }
        catch(Exception e) when(e is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {Volatile.Write(ref snapshot,State with{Error="Audio indisponible",Volume=null,Muted=null,MicrophoneMuted=null});}
        // The event stays valid for an in-flight UI enqueue until this object is
        // collected; Dispose never waits for a slow device on the WPF thread.
    }
    public void Dispose(){disposed=true;wake.Set();}
}
