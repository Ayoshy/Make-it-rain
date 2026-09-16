using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
namespace Battlestation;
internal sealed class NetworkDetailClient : IDisposable
{
    readonly SemaphoreSlim requests=new(1,1);
    NamedPipeServerStream? pipe;
    StreamReader? reader;
    StreamWriter? writer;
    Process? helper;
    volatile bool active,disposed,starting;
    bool? sentActive;
    bool polling;
    NetworkDetailFrame frame=new(false,"Détail inactif",[]);
    internal NetworkDetailFrame Frame=>Volatile.Read(ref frame);
    internal bool Connected=>pipe?.IsConnected==true;
    internal bool Starting=>starting;
    internal int HelperPid=>helper?.Id??0;
    internal void SetActive(bool value){if(active==value)return;active=value;if(!disposed&&Connected)Poll();}
    internal async void Enable()
    {
        if(disposed||starting)return;
        if(Connected){sentActive=null;Poll();return;}
        starting=true;Volatile.Write(ref frame,new(false,"Autorisation Windows…",[]));
        try
        {
            string name="Battlestation.Network."+Guid.NewGuid().ToString("N");
            var connection=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);pipe=connection;
            var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"network-helper","Battlestation.NetworkHelper.exe")){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
            start.ArgumentList.Add(name);
            var launched=await Task.Run(()=>Process.Start(start))??throw new IOException("Helper réseau indisponible.");
            if(disposed){connection.Dispose();launched.Dispose();return;}helper=launched;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await connection.WaitForConnectionAsync(timeout.Token);
            if(disposed)return;
            reader=new StreamReader(connection,Encoding.UTF8,leaveOpen:true);
            writer=new StreamWriter(connection,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};
            sentActive=null;
        }
        catch(Win32Exception e) when(e.NativeErrorCode==1223){Reset();Volatile.Write(ref frame,new(false,"Autorisation Windows refusée",[]));}
        catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException or Win32Exception)
        {Reset();if(!disposed)Volatile.Write(ref frame,new(false,"Détail indisponible : "+e.Message,[]));}
        finally{starting=false;if(!disposed&&Connected)Poll();}
    }
    internal async void Poll()
    {
        if(disposed||polling||!Connected||writer is null||reader is null)return;
        var output=writer;var input=reader;polling=true;
        try
        {
            await requests.WaitAsync();
            try
            {
                bool desired=active;
                string command=sentActive!=desired?(desired?"START":"PAUSE"):"READ";
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
                await output.WriteLineAsync(command.AsMemory(),timeout.Token);
                var line=await input.ReadLineAsync(timeout.Token);
                if(line is null)throw new IOException("Helper réseau fermé.");
                var next=JsonSerializer.Deserialize<NetworkDetailFrame>(line)??throw new IOException("Réponse réseau absente.");
                sentActive=desired;
                Volatile.Write(ref frame,active?next:new(false,"Collecte suspendue",[]));
            }
            finally{requests.Release();}
        }
        catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException or JsonException)
        {Reset();if(!disposed)Volatile.Write(ref frame,new(false,"Détail déconnecté",[]));}
        finally{polling=false;if(!disposed&&Connected&&sentActive!=active)Poll();}
    }
    void Reset()
    {
        sentActive=null;var connection=pipe;var input=reader;var output=writer;pipe=null;reader=null;writer=null;
        connection?.Dispose();
        try{output?.Dispose();input?.Dispose();}catch(Exception e) when(e is IOException or ObjectDisposedException or InvalidOperationException){}
        helper?.Dispose();helper=null;
    }
    public void Dispose(){disposed=true;active=false;Reset();}
}
