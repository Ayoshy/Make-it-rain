using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
namespace Battlestation;
internal sealed class NetworkAppCounters
{
    sealed class Counter {internal long Received,Sent;}
    readonly ConcurrentDictionary<int,Counter> counters=[];
    readonly Dictionary<int,(string Key,string Name,long Seen)> names=[];
    readonly Dictionary<string,(NetworkAppRate Rate,long Seen)> recent=new(StringComparer.OrdinalIgnoreCase);
    string[] order=[];
    long last=Stopwatch.GetTimestamp();
    internal void Add(int pid,int bytes,bool incoming)
    {
        if(pid<=0||bytes<=0)return;
        var counter=counters.GetOrAdd(pid,_=>new());
        if(incoming)Interlocked.Add(ref counter.Received,bytes);else Interlocked.Add(ref counter.Sent,bytes);
    }
    internal NetworkAppRate[] Drain()=>Drain(Stopwatch.GetTimestamp());
    internal NetworkAppRate[] Drain(long now)
    {
        double seconds=Math.Max(1d/Stopwatch.Frequency,(now-last)/(double)Stopwatch.Frequency);last=now;
        var groups=new Dictionary<string,(string Name,long Received,long Sent,int Processes)>(StringComparer.OrdinalIgnoreCase);
        foreach(var (pid,counter) in counters)
        {
            long incoming=Interlocked.Exchange(ref counter.Received,0),outgoing=Interlocked.Exchange(ref counter.Sent,0);
            if(incoming+outgoing==0)continue;
            if(!names.TryGetValue(pid,out var identity)||Environment.TickCount64-identity.Seen>30000)
            {
                string key="pid:"+pid,name="PID "+pid;
                try{using var process=Process.GetProcessById(pid);name=process.ProcessName;key=process.MainModule?.FileName??key;}
                catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
                names[pid]=identity=(key,name,Environment.TickCount64);
            }
            var group=groups.GetValueOrDefault(identity.Key,(identity.Name,0,0,0));
            groups[identity.Key]=(identity.Name,group.Received+incoming,group.Sent+outgoing,group.Processes+1);
        }
        foreach(int pid in names.Where(p=>Environment.TickCount64-p.Value.Seen>60000).Select(p=>p.Key).ToArray()){names.Remove(pid);counters.TryRemove(pid,out _);}
        // Retain identities through short gaps in ETW delivery, never old rates.
        foreach(string key in recent.Keys.ToArray())
        {
            var entry=recent[key];
            if(now-entry.Seen>=4*Stopwatch.Frequency)recent.Remove(key);
            else recent[key]=(entry.Rate with{Received=0,Sent=0},entry.Seen);
        }
        foreach(var (key,group) in groups)
            recent[key]=(new(group.Name,group.Received/seconds,group.Sent/seconds,group.Processes),now);
        var selected=recent.OrderByDescending(p=>p.Value.Rate.Received+p.Value.Rate.Sent)
            .ThenByDescending(p=>order.Contains(p.Key)).ThenByDescending(p=>p.Value.Seen)
            .ThenBy(p=>p.Key,StringComparer.OrdinalIgnoreCase)
            .Take(5).Select(p=>p.Key).ToArray();
        // Rank every measurement by download + upload; retained idle rows stay below activity.
        order=selected;
        return order.Select(key=>recent[key].Rate).ToArray();
    }
}
