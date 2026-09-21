using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace Battlestation;

internal enum LolState { NoGame, Waiting, Live, Unavailable }

internal sealed record LolObjective(string Team,int Dragons,int Barons,int Turrets,int Inhibitors);

internal sealed record LolSnapshot(
    LolState State,
    string Champion,
    int Level,
    double GameTime,
    int Kills,
    int Deaths,
    int Assists,
    int CreepScore,
    int Gold,
    double? Respawn,
    double? DragonIn,
    double? BaronIn,
    LolObjective Blue,
    LolObjective Red,
    DateTimeOffset CapturedAt,
    string? Error=null,
    int MapNumber=0,
    string Mode="",
    string[]? Events=null)
{
    // Only the live client API is read; no token, no lockfile and no game file.
    internal static readonly LolSnapshot None=new(LolState.NoGame,"",0,0,0,0,0,0,0,null,null,null,Empty("BLEU"),Empty("ROUGE"),DateTimeOffset.MinValue);
    internal static LolSnapshot Idle(LolState state,string? error=null)=>None with{State=state,Error=error,CapturedAt=DateTimeOffset.UtcNow};
    // Howling Abyss has no dragon and no baron: the counters and timers that
    // describe them must not appear there.
    internal bool Aram=>MapNumber==12||Mode.Contains("ARAM",StringComparison.OrdinalIgnoreCase);
    static LolObjective Empty(string team)=>new(team,0,0,0,0);
}

// Reads the local League client API. Nothing is stored and nothing is sent back:
// the worker only asks the loopback endpoint for the current game.
internal sealed class LolTelemetry : IDisposable
{
    const string Endpoint="https://127.0.0.1:2999/liveclientdata/allgamedata";
    const double DragonInterval=300,BaronInterval=360,StaleAfter=10;
    readonly CancellationTokenSource stop=new();
    readonly SemaphoreSlim wake=new(0,1);
    readonly Task worker;
    volatile bool active;
    LolSnapshot snapshot=LolSnapshot.None;
    internal LolSnapshot Snapshot=>Volatile.Read(ref snapshot);
    internal LolTelemetry(){worker=Task.Run(Work);}
    internal void SetActive(bool value){if(active==value)return;active=value;Signal();}
    void Signal(){if(wake.CurrentCount==0)try{wake.Release();}catch(SemaphoreFullException){}}
    static HttpClient Client()
    {
        var handler=new HttpClientHandler{
            // The live client serves its own certificate on the loopback only.
            ServerCertificateCustomValidationCallback=(message,_,_,_)=>message.RequestUri is{} uri&&IsLiveClientEndpoint(uri.Host,uri.Port),
        };
        return new HttpClient(handler){Timeout=TimeSpan.FromSeconds(3)};
    }
    internal static bool IsLiveClientEndpoint(string? host,int port)=>string.Equals(host,"127.0.0.1",StringComparison.Ordinal)&&port==2999;
    // Without the game process there is nothing to ask: the portal is not probed
    // again before the next process check, and the dock states that no game runs.
    internal static bool Probe(bool active,bool game)=>active&&game;
    async Task Work()
    {
        using var client=Client();
        bool game=false,live=false;long nextDetect=0;
        try
        {
            while(!stop.IsCancellationRequested)
            {
                if(!active){game=false;live=false;nextDetect=0;await wake.WaitAsync(stop.Token);continue;}
                long now=Environment.TickCount64;
                if(now>=nextDetect){nextDetect=now+5000;game=LeagueRunning();}
                if(!Probe(active,game)){live=false;PublishIdle(LolState.NoGame);await wake.WaitAsync(500,stop.Token);continue;}
                try
                {
                    string body=await client.GetStringAsync(Endpoint,stop.Token);
                    Publish(Parse(body,DateTimeOffset.UtcNow));live=true;
                }
                catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
                catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or FormatException or InvalidOperationException or UriFormatException)
                {
                    var previous=Snapshot;
                    bool recent=previous.State==LolState.Live&&(DateTimeOffset.UtcNow-previous.CapturedAt).TotalSeconds<StaleAfter;
                    if(!recent)PublishIdle(live?LolState.Unavailable:LolState.Waiting,e.GetType().Name);
                }
                await wake.WaitAsync(1000,stop.Token);
            }
        }
        catch(OperationCanceledException){}
    }
    void Publish(LolSnapshot next)
    {
        if(Snapshot!=next)Volatile.Write(ref snapshot,next);
    }
    // A resting dock repaints only when its state really changes: the idle states
    // carry no measurement that would justify a new image every second.
    void PublishIdle(LolState state,string? error=null)
    {
        var current=Snapshot;
        if(current.State==state&&current.Error==error)return;
        Volatile.Write(ref snapshot,LolSnapshot.Idle(state,error));
    }
    static bool LeagueRunning()
    {
        try
        {
            var processes=Process.GetProcessesByName("League of Legends");
            try{return processes.Length>0;}finally{foreach(var process in processes)process.Dispose();}
        }
        catch(Exception e) when(e is InvalidOperationException or System.ComponentModel.Win32Exception){return false;}
    }
    // Pure parsing: the render tests feed a synthetic game and resolve the timers.
    internal static LolSnapshot Parse(string body,DateTimeOffset capturedAt)
    {
        using var document=JsonDocument.Parse(body);
        var root=document.RootElement;
        if(!root.TryGetProperty("gameData",out var game)||!root.TryGetProperty("activePlayer",out var active))throw new FormatException("Données de partie incomplètes");
        double time=Number(game,"gameTime");
        var roster=Roster(root);
        string identity=Text(active,"riotId");
        if(identity.Length==0)identity=Text(active,"summonerName");
        JsonElement? mine=null;
        foreach(var entry in roster)if(Matches(entry,identity)){mine=entry;break;}
        (int Kills,int Deaths,int Assists,int CreepScore) scores=mine is{} self?Score(self):default;
        string champion=mine is{} found?Text(found,"championName"):"";
        int level=(int)Math.Max(Number(active,"level"),mine is{} player?Number(player,"level"):0);
        bool dead=(mine is{} deadPlayer&&Bool(deadPlayer,"isDead"))||Bool(active,"isDead");
        double? respawn=dead?Math.Max(0,Math.Max(Number(active,"respawnTimer"),mine is{} fallen?Number(fallen,"respawnTimer"):0)):null;
        var (blue,red,dragon,baron)=Events(root,roster,time);
        return new LolSnapshot(LolState.Live,champion,level,time,scores.Kills,scores.Deaths,scores.Assists,scores.CreepScore,(int)Number(active,"currentGold"),respawn,
            dragon,baron,
            new LolObjective("BLEU",blue.Dragons,blue.Barons,blue.Turrets,blue.Inhibitors),
            new LolObjective("ROUGE",red.Dragons,red.Barons,red.Turrets,red.Inhibitors),
            capturedAt,Error:null,MapNumber:(int)Number(game,"mapNumber"),Mode:Text(game,"gameMode"),Events:EventNames(root));
    }
    // The event names are published as they are: they are the only honest source
    // for a respawn timer, and a pickup without an event cannot be timed.
    static string[] EventNames(JsonElement root)
    {
        if(!root.TryGetProperty("events",out var events)||!events.TryGetProperty("Events",out var list)||list.ValueKind!=JsonValueKind.Array)return [];
        var names=new List<string>();
        foreach(var item in list.EnumerateArray()){var name=Text(item,"EventName");if(name.Length>0&&!names.Contains(name))names.Add(name);}
        return names.Take(24).ToArray();
    }
    static (LolObjective Blue,LolObjective Red,double? Dragon,double? Baron) Events(JsonElement root,JsonElement[] roster,double time)
    {
        int[] dragons=[0,0],barons=[0,0],turrets=[0,0],inhibitors=[0,0];
        double dragonAt=double.NaN,baronAt=double.NaN;
        if(root.TryGetProperty("events",out var events)&&events.TryGetProperty("Events",out var list)&&list.ValueKind==JsonValueKind.Array)
            foreach(var item in list.EnumerateArray())
            {
                string name=Text(item,"EventName"),killer=Text(item,"KillerName");
                int side=Side(roster,killer);if(side<0)continue;
                double at=Number(item,"EventTime");
                switch(name)
                {
                    case "DragonKill":dragons[side]++;if(double.IsNaN(dragonAt)||at>dragonAt)dragonAt=at;break;
                    case "BaronKill":barons[side]++;if(double.IsNaN(baronAt)||at>baronAt)baronAt=at;break;
                    case "TurretKilled":turrets[side]++;break;
                    case "InhibKilled":inhibitors[side]++;break;
                }
            }
        double? next(double last,double interval)=>double.IsNaN(last)?null:Math.Max(0,last+interval-time);
        return (new("BLEU",dragons[0],barons[0],turrets[0],inhibitors[0]),new("ROUGE",dragons[1],barons[1],turrets[1],inhibitors[1]),
            next(dragonAt,DragonInterval),next(baronAt,BaronInterval));
    }
    static JsonElement[] Roster(JsonElement root)
    {
        if(root.TryGetProperty("allPlayers",out var players)&&players.ValueKind==JsonValueKind.Array)return players.EnumerateArray().ToArray();
        if(root.TryGetProperty("playerList",out var fallback)&&fallback.ValueKind==JsonValueKind.Array)return fallback.EnumerateArray().ToArray();
        return [];
    }
    static bool Matches(JsonElement entry,string identity)=>identity.Length>0&&(Text(entry,"riotId")==identity||Text(entry,"summonerName")==identity);
    static int Side(JsonElement[] roster,string killer)
    {
        if(killer.Length==0)return -1;
        var entry=roster.FirstOrDefault(item=>Matches(item,killer));
        return entry.ValueKind!=JsonValueKind.Object?-1:Text(entry,"team")=="ORDER"?0:Text(entry,"team")=="CHAOS"?1:-1;
    }
    static (int Kills,int Deaths,int Assists,int CreepScore) Score(JsonElement entry)
    {
        if(!entry.TryGetProperty("scores",out var scores))return (0,0,0,0);
        return ((int)Number(scores,"kills"),(int)Number(scores,"deaths"),(int)Number(scores,"assists"),(int)Number(scores,"creepScore"));
    }
    static double Number(JsonElement element,string name)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Number?value.GetDouble():0;
    static bool Bool(JsonElement element,string name)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.True;
    static string Text(JsonElement element,string name)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
    public void Dispose(){stop.Cancel();Signal();}
}
