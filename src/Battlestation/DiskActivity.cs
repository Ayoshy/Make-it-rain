using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Battlestation;

// Charge d'E/S par volume, comme le « temps d'activité » du Gestionnaire des tâches :
// part du temps où le volume avait au moins une requête en cours. Les compteurs du
// noyau (IOCTL_DISK_PERFORMANCE) se lisent sans droits ni accès au disque, une fois
// par seconde et seulement quand le dock est exposé.
internal sealed class DiskActivity : IDisposable
{
    const uint DiskPerformance=0x70020;
    const int IdleOffset=32,QueryOffset=56,Size=88;
    readonly Func<DiskVolume[]> volumes;
    readonly Func<string,(long Idle,long Query)?> read;
    readonly ManualResetEventSlim visible=new(false);
    readonly CancellationTokenSource stop=new();
    readonly ConcurrentDictionary<string,double> loads=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,(long Idle,long Query)> previous=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,SafeFileHandle> handles=new(StringComparer.OrdinalIgnoreCase);
    internal event Action? Changed;
    internal DiskActivity(Func<DiskVolume[]> volumes,Func<string,(long Idle,long Query)?>? read=null)
    {
        this.volumes=volumes;this.read=read??ReadCounters;_=Task.Run(Run);
    }
    internal double? Load(string path)=>loads.TryGetValue(path,out double load)?load:null;
    internal void SetActive(bool value)
    {
        try{if(value)visible.Set();else visible.Reset();}catch(ObjectDisposedException){}
    }
    // Part occupée entre deux relevés ; un intervalle nul ou des compteurs remis à zéro ne donnent rien.
    internal static double? Busy((long Idle,long Query) before,(long Idle,long Query) after)
    {
        long query=after.Query-before.Query,idle=after.Idle-before.Idle;
        if(query<=0||idle<0)return null;
        return Math.Clamp(1-idle/(double)query,0,1);
    }
    async Task Run()
    {
        try
        {
            while(true)
            {
                if(!visible.IsSet){lock(previous)previous.Clear();visible.Wait(stop.Token);}
                Sample();
                await Task.Delay(1000,stop.Token);
            }
        }
        catch(OperationCanceledException){}
        finally{foreach(var handle in handles.Values)handle.Dispose();handles.Clear();visible.Dispose();stop.Dispose();}
    }
    internal void Sample()
    {
        bool changed=false;
        lock(previous)foreach(var volume in volumes())
        {
            if(read(volume.Path) is not {} next){changed|=loads.TryRemove(volume.Path,out _);continue;}
            if(previous.TryGetValue(volume.Path,out var before)&&Busy(before,next) is {} busy)
            {
                // Lissage court : la charge reste lisible sans suivre chaque pic d'une seconde.
                double load=loads.TryGetValue(volume.Path,out double last)?last*.4+busy*.6:busy;
                if(load<.005)load=0;
                if(!loads.TryGetValue(volume.Path,out double shown)||Math.Abs(shown-load)>.001){loads[volume.Path]=load;changed=true;}
            }
            previous[volume.Path]=next;
        }
        if(changed)Changed?.Invoke();
    }
    (long Idle,long Query)? ReadCounters(string path)
    {
        string name=path.TrimEnd('\\');
        if(!handles.TryGetValue(name,out var handle))
        {
            handle=CreateFile(@"\\.\"+name,0,3,0,3,0,0);
            if(handle.IsInvalid){handle.Dispose();return null;}
            handles[name]=handle;
        }
        var buffer=new byte[Size];
        if(!DeviceIoControl(handle,DiskPerformance,0,0,buffer,Size,out _,0))
        {
            handle.Dispose();handles.Remove(name);return null;
        }
        return (BitConverter.ToInt64(buffer,IdleOffset),BitConverter.ToInt64(buffer,QueryOffset));
    }
    public void Dispose(){try{stop.Cancel();}catch(ObjectDisposedException){}}
    [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string name,uint access,uint share,nint security,uint creation,uint flags,nint template);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool DeviceIoControl(SafeFileHandle device,uint code,nint input,uint inputSize,byte[] output,uint outputSize,out uint returned,nint overlapped);
}
