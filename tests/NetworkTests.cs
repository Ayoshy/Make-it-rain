using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Battlestation;
if(args.Contains("--child")){Thread.Sleep(15000);return 0;}
static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
string temp=Path.Combine(Path.GetTempPath(),"Battlestation-network-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try
{
    var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.ArgumentList.Add("--child");
    using(var child=Process.Start(start)!)
    {
        try{
            Thread.Sleep(200);var counters=new NetworkAppCounters();
            counters.Add(Environment.ProcessId,2048,true);counters.Add(child.Id,1024,true);counters.Add(child.Id,512,false);
            long at=Stopwatch.GetTimestamp();
            var apps=counters.Drain(at);
            Check(apps.Length==1&&apps[0].Processes==2,"Two PIDs of the same executable are grouped");
            Check(Math.Abs(apps[0].Received/apps[0].Sent-6)<.001,"Incoming and outgoing counts keep their direction");
            var idle=counters.Drain(at+Stopwatch.Frequency);
            Check(idle.Length==1&&idle[0].Received==0&&idle[0].Sent==0,"A quiet window retains the row without replaying consumed bytes");
            Check(counters.Drain(at+3*Stopwatch.Frequency).Length==1,"Short ETW gaps do not replace applications with the empty state");
            counters.Add(child.Id,512,false);
            var resumed=counters.Drain(at+4*Stopwatch.Frequency);
            Check(resumed.Length==1&&resumed[0].Received==0&&resumed[0].Sent==512,"Traffic resumes on the same row with only the new measurement");
            Check(counters.Drain(at+8*Stopwatch.Frequency).Length==0,"An application expires after four seconds without attributed traffic");

            // Nonexistent PIDs give distinct identities without launching extra apps.
            var ranking=new NetworkAppCounters();
            for(int pid=1_000_001;pid<=1_000_006;pid++)ranking.Add(pid,pid-1_000_000,true);
            at=Stopwatch.GetTimestamp();var first=ranking.Drain(at);
            Check(first.Length==5&&first.All(app=>app.Name!="PID 1000001"),"Only the five most active applications are selected");
            ranking.Add(1_000_002,1000,true);ranking.Add(1_000_006,1,true);
            var reordered=ranking.Drain(at+Stopwatch.Frequency);
            Check(reordered[0].Name=="PID 1000002"&&reordered[1].Name=="PID 1000006"&&reordered.Skip(2).All(app=>app.Received+app.Sent==0),"Current download plus upload sorts descending, with retained zero rows last");
            Check(first.Select(app=>app.Name).Order().SequenceEqual(reordered.Select(app=>app.Name).Order()),"Equal idle rates retain the selected applications instead of rotating them");
            ranking.Add(1_000_006,2000,false);
            Check(ranking.Drain(at+2*Stopwatch.Frequency)[0].Name=="PID 1000006","Upload alone can move an application to the top");
            ranking.Add(1_000_001,2000,true);
            var newcomer=ranking.Drain(at+3*Stopwatch.Frequency);
            Check(newcomer.Length==5&&newcomer.Any(app=>app.Name=="PID 1000001"),"A newly active application displaces an idle row immediately");
            Check(new NetworkAppCounters().Drain().Length==0,"A new or resumed collection never inherits the old applications");
        }
        finally{if(!child.HasExited){child.Kill();child.WaitForExit();}}
    }
    if(args.Length>0)
    {
        string build=Path.GetFullPath(args[0]);
        NativeLibrary.SetDllImportResolver(typeof(NetworkSampler).Assembly,(name,_,_)=>name=="Battlestation.Desk.dll"?NativeLibrary.Load(Path.Combine(build,name)):0);
        using var sampler=new NetworkSampler(Path.Combine(temp,"network.json"));sampler.SetActive(true);
        await Task.Delay(3300);
        var snapshot=sampler.Snapshot;
        Check(snapshot.InterfaceId.Length>0,"The default route selects an actual interface");
        Check(snapshot.History.LastOrDefault()?.Received is not null,"Interface counters produce a measured rate");
        sampler.Configure(new(snapshot.InterfaceId,"203.0.113.1"));await Task.Delay(2200);
        Check(sampler.Snapshot.Latency is null,"A non-responding target is unavailable, never zero milliseconds");
        sampler.SetActive(false);await Task.Delay(1100);long last=sampler.Snapshot.History.Last().At;await Task.Delay(1200);
        Check(sampler.Snapshot.History.Last().At==last,"Hiding the dock suspends network measurements");
        sampler.SetActive(true);await Task.Delay(2200);Check(sampler.Snapshot.History.Last().At>last,"Measurements resume after showing the dock");
    }
    return 0;
}
finally{Directory.Delete(temp,true);}
