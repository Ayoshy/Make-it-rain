using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Battlestation;
internal sealed record AudioOutput(string Id,string Name,bool Selected);
internal sealed record AudioApp(string Key,string Name,float Volume,bool Muted,float Peak);
internal sealed class AudioMixer : IDisposable
{
    readonly MMDeviceEnumerator devices=new();
    readonly List<MMDevice> endpoints=[];
    readonly Dictionary<string,List<AudioSessionControl>> sessions=[];
    MMDevice? output,microphone;
    long nextRefresh;
    public IReadOnlyList<AudioOutput> Outputs {get;private set;}=[];
    public IReadOnlyList<AudioApp> Apps {get;private set;}=[];
    public string OutputName {get;private set;}="Sortie indisponible";
    public string? OutputId {get;private set;}
    public float? Volume {get;private set;}
    public bool? Muted {get;private set;}
    public bool? MicrophoneMuted {get;private set;}
    public string MicrophoneName {get;private set;}="Micro indisponible";
    public string Error {get;private set;}="";
    static bool Recoverable(Exception e)=>e is COMException or InvalidOperationException or ArgumentException;
    internal void Poll(bool force=false)
    {
        try
        {
            if(force||Environment.TickCount64>=nextRefresh)
            {
                nextRefresh=Environment.TickCount64+5000;ReleaseEndpoints();
                output=Default(DataFlow.Render,Role.Multimedia);microphone=Default(DataFlow.Capture,Role.Communications);OutputId=output?.ID;
                MicrophoneName=microphone?.FriendlyName??"Micro indisponible";
                var rows=new List<AudioOutput>();
                foreach(var endpoint in devices.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active))
                {
                    endpoints.Add(endpoint);
                    try
                    {
                        string id=endpoint.ID;rows.Add(new(id,endpoint.FriendlyName,id==OutputId));
                        var collection=endpoint.AudioSessionManager.Sessions;
                        for(int i=0;i<collection.Count;i++)
                        {
                            var session=collection[i];
                            try
                            {
                                if(session.State==AudioSessionState.AudioSessionStateExpired){session.Dispose();continue;}
                                string name;
                                if(session.IsSystemSoundsSession)name="Système";
                                else{using var process=Process.GetProcessById((int)session.GetProcessID);name=process.ProcessName;}
                                if(!sessions.TryGetValue(name,out var group))sessions[name]=group=[];
                                group.Add(session);
                            }
                            catch(Exception e) when(Recoverable(e)){session.Dispose();}
                        }
                    }
                    catch(Exception e) when(Recoverable(e)){Error="Une sortie audio est devenue indisponible.";}
                }
                Outputs=rows.OrderByDescending(x=>x.Selected).ThenBy(x=>x.Name).ToArray();
                OutputName=Outputs.FirstOrDefault(x=>x.Selected)?.Name??output?.FriendlyName??"Sortie indisponible";
            }
            Volume=output?.AudioEndpointVolume.MasterVolumeLevelScalar;Muted=output?.AudioEndpointVolume.Mute;
            MicrophoneMuted=microphone?.AudioEndpointVolume.Mute;
            var apps=new List<AudioApp>();
            foreach(var pair in sessions)
            {
                var live=new List<(float Volume,bool Mute,float Peak)>();
                foreach(var session in pair.Value)
                    try{if(session.State!=AudioSessionState.AudioSessionStateExpired)live.Add((session.SimpleAudioVolume.Volume,session.SimpleAudioVolume.Mute,session.AudioMeterInformation.MasterPeakValue));}
                    catch(Exception e) when(Recoverable(e)){nextRefresh=0;}
                if(live.Count>0)apps.Add(new(pair.Key,DisplayName(pair.Key),live.Max(s=>s.Volume),live.All(s=>s.Mute),live.Max(s=>s.Peak)));
            }
            Apps=apps.OrderBy(s=>s.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        catch(Exception e) when(Recoverable(e))
        {
            Error="Audio indisponible · nouvelle tentative en cours";Volume=null;Muted=null;MicrophoneMuted=null;Apps=[];
        }
    }
    MMDevice? Default(DataFlow flow,Role role){try{return devices.GetDefaultAudioEndpoint(flow,role);}catch(COMException){return null;}}
    static string DisplayName(string name)=>name.ToLowerInvariant() switch{"stremio-shell-ng" or "stremio"=>"Stremio","brave"=>"Brave","chrome"=>"Chrome","msedge"=>"Edge","spotify"=>"Spotify","discord"=>"Discord",_=>name};
    internal void SetVolume(string? key,float value)=>Act(()=>
    {
        if(!float.IsFinite(value))throw new ArgumentException();value=Math.Clamp(value,0,1);
        if(key is null){if(output is null)throw new InvalidOperationException();output.AudioEndpointVolume.MasterVolumeLevelScalar=value;}
        else foreach(var session in Group(key))session.SimpleAudioVolume.Volume=value;
    });
    internal void ToggleMute(string? key)=>Act(()=>
    {
        if(key is null){if(output is null)throw new InvalidOperationException();output.AudioEndpointVolume.Mute=!output.AudioEndpointVolume.Mute;}
        else{var group=Group(key);bool mute=!group.All(s=>s.SimpleAudioVolume.Mute);foreach(var session in group)session.SimpleAudioVolume.Mute=mute;}
    });
    internal void ToggleMicrophone()=>Act(()=>{if(microphone is null)throw new InvalidOperationException();microphone.AudioEndpointVolume.Mute=!microphone.AudioEndpointVolume.Mute;});
    List<AudioSessionControl> Group(string key)=>sessions.TryGetValue(key,out var group)&&group.Count>0?group:throw new InvalidOperationException();
    internal void SelectOutput(string id)=>Act(()=>
    {
        using var endpoint=devices.GetDevice(id);
        if(endpoint.State!=DeviceState.Active||endpoint.DataFlow!=DataFlow.Render)throw new InvalidOperationException();
        var previous=Enum.GetValues<Role>().Select(role=>{using var device=Default(DataFlow.Render,role);return (role,id:device?.ID);}).ToArray();
        try
        {
            foreach(var role in Enum.GetValues<Role>())AudioPolicy.SetDefault(id,role);
            foreach(var role in Enum.GetValues<Role>()){using var actual=Default(DataFlow.Render,role);if(actual?.ID!=id)throw new InvalidOperationException();}
        }
        catch
        {
            foreach(var old in previous)if(old.id is not null)try{AudioPolicy.SetDefault(old.id,old.role);}catch(COMException){}
            throw;
        }
        nextRefresh=0;
    });
    void Act(Action action)
    {
        try{action();Error="";Poll();}
        catch(Exception e) when(Recoverable(e)){Error="Action audio indisponible · réessaie";nextRefresh=0;}
    }
    void ReleaseEndpoints()
    {
        foreach(var group in sessions.Values)foreach(var session in group)try{session.Dispose();}catch(COMException){}
        sessions.Clear();
        foreach(var endpoint in endpoints){try{endpoint.AudioSessionManager.Dispose();}catch(COMException){}endpoint.Dispose();}
        endpoints.Clear();output?.Dispose();microphone?.Dispose();output=null;microphone=null;
    }
    public void Dispose(){ReleaseEndpoints();devices.Dispose();}
}
