using System.Diagnostics;
using System.Text.Json;
using System.Windows.Threading;

namespace Battlestation;
// Only position and user actions cross the pipe; terminal I/O stays in its stable host.
internal sealed class TerminalSession : IDisposable
{
    readonly Station station;
    readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
    readonly SemaphoreSlim requests=new(1,1);
    int x=2700,y=760,w=1464,h=660;
    bool visible=true,starting,polling,disposed,placing,placementDirty,detached;
    nint remoteHost;
    public int Pid {get;private set;}
    public nint RemoteHandle=>remoteHost;
    public string Status {get;private set;}="Terminal natif prêt";
    public bool UseGlassTabs {get;private set;}
    public IReadOnlyList<TerminalTabInfo> Tabs {get;private set;}=[];
    public event Action? HeaderChanged;
    public TerminalSession(Station s){station=s;}
    async Task<string> Send(string command)
    {
        await requests.WaitAsync();
        try{return await Task.Run(()=>ControlPipe.Send(command,TerminalHost.PipeName,1500));}
        finally{requests.Release();}
    }
    public void Place(nint owner,int left,int top,int width,int height)
    {
        detached=false;
        x=left;y=top;w=width;h=height;if(remoteHost!=0)_=PlaceAsync();
    }
    async Task PlaceAsync()
    {
        if(placing){placementDirty=true;return;}placing=true;
        try{do{placementDirty=false;if(UseGlassTabs)await Send("chrome:external");await Send($"place:{x}:{y}:{w}:{h}");await Send(visible?"show":"hide");}while(placementDirty&&!disposed);}catch(Exception e){Status=e.Message;}
        finally{placing=false;}
    }
    public void SetVisible(bool show){visible=show;if(remoteHost!=0)_=VisibilityAsync();}
    async Task VisibilityAsync(){try{await Send(visible?"show":"hide");}catch{}}
    async Task EnsureHost(string command)
    {
        if(disposed||starting)return;starting=true;
        try
        {
            try{await Send("inspect");}
            catch
            {
                var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                info.ArgumentList.Add("--terminal-host");info.ArgumentList.Add(station.Root);Process.Start(info)?.Dispose();
                bool connected=false;
                for(int i=0;i<30&&!connected;i++){await Task.Delay(100);try{await Send("inspect");connected=true;}catch{}}
                if(!connected)throw new InvalidOperationException("Le terminal natif n'a pas démarré.");
            }
            await PlaceAsync();await Send(command);await Poll();
            if(remoteHost!=0)Native.SetForegroundWindow(remoteHost);
        }
        catch(Exception e){Status=e.Message;}
        finally{starting=false;}
    }
    public void Start()=>_=EnsureHost("start");
    public void Command(string command)=>_=EnsureHost("command:"+command);
    public void OpenCodex(string project)=>_=EnsureHost("codex:"+project);
    public void SelectTab(Guid id)=>_=EnsureHost("select:"+id);
    public void CloseTab(Guid id)=>_=EnsureHost("close-tab:"+id);
    public void Update(){if(!polling&&!disposed)_=Poll();}
    async Task Poll()
    {
        polling=true;
        try
        {
            using var json=JsonDocument.Parse(await Send("inspect"));
            if(disposed)return;
            var root=json.RootElement;var handle=(nint)root.GetProperty("hwnd").GetInt64();bool changed=remoteHost!=handle;
            remoteHost=handle;Pid=root.GetProperty("pid").GetInt32();Status=root.GetProperty("status").GetString()??"Terminal natif";
            Tabs=root.GetProperty("sessions").EnumerateArray().Select(tab=>new TerminalTabInfo(tab.GetProperty("id").GetGuid(),tab.GetProperty("title").GetString()??"Terminal",tab.GetProperty("active").GetBoolean())).ToArray();
            bool external=root.TryGetProperty("chromeVersion",out var version)&&version.GetInt32()>=1;
            if(UseGlassTabs!=external){UseGlassTabs=external;HeaderChanged?.Invoke();}
            if(changed)await PlaceAsync();
        }
        catch{if(!starting){Pid=0;remoteHost=0;}}
        finally{polling=false;}
    }
    public void Detach()
    {
        // Keep the terminal and every shell alive when the desktop UI is reloaded.
        if(detached)return;detached=true;remoteHost=0;
        try{ControlPipe.Send("detach",TerminalHost.PipeName,1500);}catch{}
    }
    public void Dispose(){disposed=true;Detach();}
}
