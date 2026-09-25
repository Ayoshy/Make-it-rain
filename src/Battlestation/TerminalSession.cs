using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace Battlestation;
// Only position and user actions cross the pipe; terminal I/O stays in its stable host.
internal sealed class TerminalSession : IDisposable
{
    readonly Station station;
    readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
    readonly SemaphoreSlim requests=new(1,1);
    readonly TerminalTabPreferences preferences;
    readonly TerminalMetadataClient metadata;
    readonly CodexRolloutReader rollouts;
    TerminalTabInfo[] rawTabs=[];
    ConsoleTitleInfo[] titles=[];
    Dictionary<Guid,TerminalCacheState> cacheStates=[];
    Dictionary<Guid,ClaudeSessionInfo> claudeStates=[];
    HashSet<Guid> seen=[];
    bool cacheBusy,cachePending;
    int x=2700,y=760,w=1464,h=660;
    bool visible=true,starting,polling,disposed,placing,placementDirty,detached;
    bool visibilityBusy,visibilityDirty;
    bool? sentExternalChrome;
    nint remoteHost;
    public int Pid {get;private set;}
    public nint RemoteHandle=>remoteHost;
    public string Status {get;private set;}="Terminal natif prêt";
    public bool ThemeSupported {get;private set;}
    string? sentTheme;
    public bool UseGlassTabs {get;private set;}
    public IReadOnlyList<TerminalTabInfo> Tabs {get;private set;}=[];
    // Project badge heuristic: a tab whose title carries the folder name means an
    // agent is likely working there. It is a hint, never an assertion.
    public bool HasTabNamed(string name)=>name.Length>1&&Tabs.Any(tab=>tab.Title.Contains(name,StringComparison.OrdinalIgnoreCase));
    public long Revision {get;private set;}
    public event Action? HeaderChanged;
    public TerminalSession(Station s)
    {
        station=s;preferences=new TerminalTabPreferences(Path.Combine(s.Data,"terminal-tabs.json"));
        metadata=new TerminalMetadataClient(Environment.ProcessPath!);
        rollouts=new CodexRolloutReader(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex","sessions"));
    }
    public TerminalTabPreference TabPreference(Guid id)=>preferences.Get(id);
    public void SetTabPreference(Guid id,TerminalTabPreference preference)
    {
        if(!rawTabs.Any(tab=>tab.Id==id))throw new InvalidOperationException("Cet onglet n'existe plus.");
        preferences.Set(id,preference);RefreshTabPresentation();
    }
    Dictionary<Guid,int> shellPids=[];
    void RefreshTabPresentation(){var next=rawTabs.Select(tab=>preferences.Decorate(tab,titles.FirstOrDefault(t=>t.Pid==shellPids.GetValueOrDefault(tab.Id)),cacheStates.GetValueOrDefault(tab.Id),claudeStates.GetValueOrDefault(tab.Id))).ToArray();if(!Tabs.SequenceEqual(next)){Tabs=next;Revision++;}}
    // L'etat Claude est publie par le CLI lui-meme, dans son registre de sessions :
    // la lecture se fait hors du fil d'interface, comme celle des rollouts Codex.
    async Task ReadClaudeStates()
    {
        var queries=new Dictionary<Guid,int>();
        foreach(var tab in rawTabs)
        {
            var metadata=titles.FirstOrDefault(t=>t.Pid==shellPids.GetValueOrDefault(tab.Id));
            if(metadata?.Claude==true&&metadata.ClaudePid>0)queries[tab.Id]=metadata.ClaudePid;
        }
        var next=new Dictionary<Guid,ClaudeSessionInfo>();
        if(queries.Count>0)
        {
            var records=await Task.Run(()=>ClaudeSessions.Read(queries.Values));
            foreach(var (id,pid) in queries)
                if(records.TryGetValue(pid,out var record))next[id]=record;
        }
        if(disposed||claudeStates.Count==next.Count&&next.All(pair=>claudeStates.TryGetValue(pair.Key,out var state)&&state==pair.Value))return;
        claudeStates=next;
    }
    bool TrackTabs()
    {
        var ids=rawTabs.Select(tab=>tab.Id).ToArray();
        bool changed=ids.Length!=seen.Count||ids.Any(id=>!seen.Contains(id));
        foreach(var id in cacheStates.Keys.Except(ids).ToArray())cacheStates.Remove(id);
        seen=[..ids];
        return changed;
    }
    // L'etat de cache est relu a la demande (minuteur de la barre d'onglets) et
    // jamais pendant la peinture du bureau.
    public void RefreshCache()=>_=RefreshCacheAsync();
    async Task RefreshCacheAsync()
    {
        if(disposed)return;
        if(cacheBusy){cachePending=true;return;}
        cacheBusy=true;
        try
        {
            var queries=new List<RolloutQuery>();
            foreach(var tab in rawTabs)
            {
                var metadata=titles.FirstOrDefault(t=>t.Pid==shellPids.GetValueOrDefault(tab.Id));
                if(metadata?.Codex!=true)continue;
                queries.Add(new(tab.Id,metadata.CodexPid));
            }
            var next=new Dictionary<Guid,TerminalCacheState>();
            if(queries.Count>0)
            {
                var resolved=await Task.Run(()=>rollouts.Resolve(queries));
                var now=DateTimeOffset.Now;
                foreach(var query in queries)
                {
                    if(!resolved.TryGetValue(query.Id,out var activity))continue;
                    if(titles.FirstOrDefault(t=>t.Pid==shellPids.GetValueOrDefault(query.Id))?.CodexPid==query.ProcessId)
                        next[query.Id]=activity.State(now);
                }
            }
            if(disposed)return;
            cacheStates=next;RefreshTabPresentation();
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){cacheStates.Clear();RefreshTabPresentation();}
        finally{cacheBusy=false;if(cachePending&&!disposed){cachePending=false;RefreshCache();}}
    }
    public object InspectTabMetadata()=>new{helperPid=metadata.Pid,tabs=Tabs.Select(tab=>new{tab.Id,tab.Title,tab.Accent,activity=tab.Activity.ToString(),tab.AutomaticTitle,tab.Effects,badge=tab.Badge,hint=tab.CacheHint.ToString(),detail=tab.Detail})};
    async Task<string> Send(string command)
    {
        await requests.WaitAsync();
        try{return await Task.Run(()=>ControlPipe.Send(command,TerminalHost.PipeName,1500));}
        finally{requests.Release();}
    }
    public void Place(nint owner,int left,int top,int width,int height)
    {
        detached=false;
        if(remoteHost!=0&&x==left&&y==top&&w==width&&h==height)return;
        x=left;y=top;w=width;h=height;if(remoteHost!=0)_=PlaceAsync();
    }
    async Task PlaceAsync()
    {
        if(placing){placementDirty=true;return;}placing=true;
        try{do{placementDirty=false;bool external=UseGlassTabs;if(sentExternalChrome!=external){await Send(external?"chrome:external":"chrome:internal");sentExternalChrome=external;}await Send($"place:{x}:{y}:{w}:{h}");}while(placementDirty&&!disposed);}catch(Exception e){Status=e.Message;}
        finally{placing=false;}
    }
    public void SetVisible(bool show)
    {
        if(visible==show)return;
        visible=show;
        if(remoteHost==0)return;
        // A scene change hides the terminal while the layout is applied and shows it
        // again at the destination: only the last state must reach the host.
        if(visibilityBusy){visibilityDirty=true;return;}
        _=VisibilityAsync();
    }
    async Task VisibilityAsync()
    {
        visibilityBusy=true;
        try{do{visibilityDirty=false;await Send(visible?"show":"hide");}while(visibilityDirty&&!disposed);}catch{}finally{visibilityBusy=false;}
    }
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
    public void OpenCodexDeepSeek()=>_=EnsureHost("command:NewShell");
    public void OpenKilo()=>_=EnsureHost("command:NewShell");
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
            remoteHost=handle;if(changed){sentExternalChrome=null;sentTheme=null;}Pid=root.GetProperty("pid").GetInt32();Status=root.GetProperty("status").GetString()??"Terminal natif";
            var sessions=root.GetProperty("sessions").EnumerateArray().ToArray();
            rawTabs=sessions.Select(tab=>new TerminalTabInfo(tab.GetProperty("id").GetGuid(),tab.GetProperty("title").GetString()??"Terminal",tab.GetProperty("active").GetBoolean())).ToArray();
            shellPids=sessions.ToDictionary(tab=>tab.GetProperty("id").GetGuid(),tab=>tab.GetProperty("pid").GetInt32());
            bool tabset=TrackTabs();
            var nextTitles=await metadata.Read(shellPids.Values);
            bool processesChanged=!titles.Select(t=>(t.Pid,t.CodexPid,t.ClaudePid)).SequenceEqual(nextTitles.Select(t=>(t.Pid,t.CodexPid,t.ClaudePid)));
            titles=nextTitles;
            if(processesChanged){cacheStates.Clear();claudeStates.Clear();}
            if(disposed)return;RefreshTabPresentation();
            await ReadClaudeStates();
            if(disposed)return;RefreshTabPresentation();
            if(tabset||processesChanged)RefreshCache();
            bool external=root.TryGetProperty("chromeVersion",out var version)&&version.GetInt32()>=1;
            if(UseGlassTabs!=external){UseGlassTabs=external;HeaderChanged?.Invoke();}
            ThemeSupported=root.TryGetProperty("themeVersion",out var themeVersion)&&themeVersion.GetInt32()>=1;
            if(ThemeSupported&&sentTheme!=DesktopTheme.Current.Id){var desired=DesktopTheme.Current.Id;await Send("theme:"+desired);sentTheme=desired;}
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
    public void Dispose(){disposed=true;metadata.Dispose();Detach();}
}
