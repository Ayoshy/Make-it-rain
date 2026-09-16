using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace Battlestation;
internal sealed record NetworkPreferences(string InterfaceId="",string Target="1.1.1.1")
{
    internal NetworkPreferences Validate()
    {
        string target=Target.Trim();
        if(target.Length is 0 or >253||Uri.CheckHostName(target)==UriHostNameType.Unknown)throw new ArgumentException("Indique une adresse IP ou un nom d’hôte.");
        return this with{Target=target};
    }
}
internal sealed record NetworkAdapterOption(string Id,string Name,bool Up);
internal sealed record NetworkSample(long At,double? Received,double? Sent);
internal sealed record NetworkSnapshot(string InterfaceId,string Name,NetworkSample[] History,double? Latency,string Target,string Status,NetworkAdapterOption[] Adapters);
internal sealed class NetworkSampler : IDisposable
{
    [DllImport("Battlestation.Desk.dll",CallingConvention=CallingConvention.Cdecl)] static extern int NetworkDefaultInterface();
    readonly string path;
    readonly CancellationTokenSource stop=new();
    readonly SemaphoreSlim wake=new(0,1);
    readonly Task worker;
    volatile bool active;
    NetworkPreferences preferences=new();
    NetworkSnapshot snapshot=new("","Interface",[],null,"1.1.1.1","En veille",[]);
    internal NetworkPreferences Preferences=>Volatile.Read(ref preferences);
    internal NetworkSnapshot Snapshot=>Volatile.Read(ref snapshot);
    internal NetworkSampler(string file)
    {
        path=file;
        try{if(File.Exists(file))preferences=JsonSerializer.Deserialize<NetworkPreferences>(File.ReadAllText(file))?.Validate()??new();}
        catch(Exception e) when(e is JsonException or ArgumentException or IOException){}
        worker=Task.Run(Work);
    }
    internal void Configure(NetworkPreferences value){value=value.Validate();DesktopSettings.Write(path,JsonSerializer.Serialize(value));Volatile.Write(ref preferences,value);Signal();}
    void Signal(){if(wake.CurrentCount==0)try{wake.Release();}catch(SemaphoreFullException){}}
    internal void SetActive(bool value){if(active==value)return;active=value;Signal();}
    static bool Configured(NetworkInterface nic){try{return nic.NetworkInterfaceType!=NetworkInterfaceType.Loopback&&nic.GetIPProperties().UnicastAddresses.Count>0;}catch(NetworkInformationException){return false;}}
    static int Index(NetworkInterface nic){try{return nic.GetIPProperties().GetIPv4Properties()?.Index??nic.GetIPProperties().GetIPv6Properties()?.Index??0;}catch(NetworkInformationException){return 0;}}
    async Task Work()
    {
        var history=new List<NetworkSample>(60);NetworkInterface[] interfaces=[];long previousAt=0;
        long previousReceived=0,previousSent=0;string previousId="";
        try
        {
            while(!stop.IsCancellationRequested)
            {
                if(!active){previousAt=0;history.Clear();await wake.WaitAsync(stop.Token);continue;}
                long started=Stopwatch.GetTimestamp();var settings=Preferences;
                interfaces=NetworkInterface.GetAllNetworkInterfaces().Where(Configured).ToArray();
                var options=interfaces.Select(n=>new NetworkAdapterOption(n.Id,n.Name,n.OperationalStatus==OperationalStatus.Up)).ToArray();
                NetworkInterface? nic=null;double? received=null,sent=null,latency=null;string status="";
                try
                {
                    int route=settings.InterfaceId.Length==0?NetworkDefaultInterface():0;
                    nic=interfaces.FirstOrDefault(n=>settings.InterfaceId.Length>0?n.Id==settings.InterfaceId:Index(n)==route);
                    if(nic?.OperationalStatus!=OperationalStatus.Up)status="Interface indisponible";
                    else
                    {
                        var totals=nic.GetIPStatistics();
                        if(previousAt!=0&&previousId==nic.Id&&totals.BytesReceived>=previousReceived&&totals.BytesSent>=previousSent)
                        {
                            double seconds=(started-previousAt)/(double)Stopwatch.Frequency;
                            received=(totals.BytesReceived-previousReceived)/seconds;sent=(totals.BytesSent-previousSent)/seconds;
                        }
                        if(previousId!=nic.Id)history.Clear();
                        previousAt=started;previousId=nic.Id;previousReceived=totals.BytesReceived;previousSent=totals.BytesSent;
                        using var ping=new Ping();
                        try{var reply=await ping.SendPingAsync(settings.Target,800).WaitAsync(TimeSpan.FromMilliseconds(950),stop.Token);if(reply.Status==IPStatus.Success)latency=reply.RoundtripTime;}
                        catch(Exception e) when(e is PingException or TimeoutException or ArgumentException){}
                    }
                }
                catch(Exception e) when(e is NetworkInformationException or DllNotFoundException or EntryPointNotFoundException){status="Mesures indisponibles";}
                if(nic is null||status.Length>0)previousAt=0;
                history.Add(new(Environment.TickCount64,received,sent));if(history.Count>60)history.RemoveAt(0);
                Volatile.Write(ref snapshot,new(nic?.Id??"",nic?.Name??"Interface",history.ToArray(),latency,settings.Target,status,options));
                int delay=Math.Max(1,1000-(int)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                await wake.WaitAsync(delay,stop.Token);
            }
        }
        catch(OperationCanceledException){}
    }
    public void Dispose(){stop.Cancel();Signal();}
}
