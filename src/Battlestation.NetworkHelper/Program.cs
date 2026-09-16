using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Battlestation;

if(args.Length!=1||!args[0].StartsWith("Battlestation.Network.",StringComparison.Ordinal))return 2;
if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))return 3;
using var single=new Mutex(true,@"Local\Battlestation.Network.Helper",out bool created);
if(!created)return 4;
try
{
    using var pipe=new NamedPipeClientStream(".",args[0],PipeDirection.InOut,PipeOptions.Asynchronous);
    using var connecting=new CancellationTokenSource(TimeSpan.FromSeconds(20));await pipe.ConnectAsync(connecting.Token);
    using var reader=new StreamReader(pipe,Encoding.UTF8,leaveOpen:true);
    using var writer=new StreamWriter(pipe,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};
    using var capture=new NetworkCapture();
    while(await reader.ReadLineAsync() is {} command)
    {
        if(command=="QUIT")break;
        NetworkDetailFrame result;
        switch(command){
            case "START":capture.Start();result=capture.Read();break;
            case "PAUSE":capture.Stop();result=capture.Read();break;
            case "READ":result=capture.Read();break;
            default:result=new(false,"Commande inconnue",[]);break;
        }
        await writer.WriteLineAsync(JsonSerializer.Serialize(result));
    }
    return 0;
}
catch(Exception e) when(e is IOException or OperationCanceledException or UnauthorizedAccessException){return 1;}

internal sealed class NetworkCapture : IDisposable
{
    internal const string SessionName="Battlestation.Network";
    TraceEventSession? session;
    ETWTraceEventSource? source;
    Task? processing;
    NetworkAppCounters counters=new();
    string error="";
    internal void Start()
    {
        if(session is not null&&processing?.IsCompleted==false)return;
        Stop();
        try
        {
            counters=new();error="";
            // This named realtime session is owned only by this helper. No ETL file.
            var next=new TraceEventSession(SessionName){StopOnDispose=true,BufferSizeMB=16};
            session=next;
            next.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
            var input=next.Source;source=input;var kernel=input.Kernel;
            void Count(TraceEvent data,int bytes,bool incoming)
            {
                // Windows network v1/v2 payloads carry PID first. Never use the raw event-header PID.
                if(data.Version<1||data.EventDataLength<8)return;
                counters.Add(Marshal.ReadInt32(data.DataStart),bytes,incoming);
            }
            kernel.TcpIpSend+=e=>Count(e,e.size,false);
            kernel.TcpIpRecv+=e=>Count(e,e.size,true);
            kernel.TcpIpSendIPV6+=e=>Count(e,e.size,false);
            kernel.TcpIpRecvIPV6+=e=>Count(e,e.size,true);
            kernel.UdpIpSend+=e=>Count(e,e.size,false);
            kernel.UdpIpRecv+=e=>Count(e,e.size,true);
            kernel.UdpIpSendIPV6+=e=>Count(e,e.size,false);
            kernel.UdpIpRecvIPV6+=e=>Count(e,e.size,true);
            processing=Task.Run(()=>{try{input.Process();}catch(Exception e){Volatile.Write(ref error,e.Message);}});
        }
        catch(Exception e){Stop();error=e.Message;}
    }
    internal NetworkDetailFrame Read()
    {
        var current=session;
        if(current is null)return new(false,error.Length>0?error:"Collecte suspendue",[]);
        if(error.Length>0||processing?.IsCompleted==true)return new(false,error.Length>0?error:"Collecte arrêtée",[]);
        long lost=source?.EventsLost??0;
        return new(true,error.Length>0?error:lost>0?"Collecte partielle":"TCP + UDP · IPv4 + IPv6",counters.Drain(),lost);
    }
    internal void Stop()
    {
        var previous=session;session=null;
        var previousSource=source;source=null;
        if(previous is not null){try{previousSource?.StopProcessing();}finally{previous.Dispose();}try{processing?.Wait(TimeSpan.FromSeconds(3));}catch(AggregateException){}}
        processing=null;counters=new();
    }
    public void Dispose()=>Stop();
}
