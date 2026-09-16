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
            var apps=counters.Drain();
            Check(apps.Length==1&&apps[0].Processes==2,"Two PIDs of the same executable are grouped");
            Check(Math.Abs(apps[0].Received/apps[0].Sent-6)<.001,"Incoming and outgoing counts keep their direction");
            Check(counters.Drain().Length==0,"Counters are consumed once per measurement window");
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
