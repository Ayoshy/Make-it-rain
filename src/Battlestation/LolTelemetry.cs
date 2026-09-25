using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace Battlestation;

internal enum LolState { NoGame, Waiting, Live, Unavailable }

internal sealed record LolObjective(string Team,int Dragons,int Barons,int Turrets,int Inhibitors,string Dragon="");
internal sealed record LolPlayer(string Champion,string Id,string Team,int Kills,int Deaths,int Assists,bool Dead);

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
    string[]? Events=null,
    // Everything below is derived from the events actually received; a kill the
    // client did not publish cannot be counted, so the streak never exceeds the
    // kill score it completes.
    string Map="",
    string Team="",
    int Streak=0,
    string MultiKill="",
    double? MultiKillAt=null,
    double? LastKillAt=null,
    double? LastDeathAt=null,
    bool FirstBlood=false,
    double? AceAt=null,
    string ChampionId="",
    LolPlayer[]? Players=null,
    string Inventory="",
    double? GoldExact=null,
    double? GoldPerSecond=null,
    double GoldWindow=0,
    double[]? GoldTrend=null,
    double[]? GoldTrendTimes=null)
{
    // Only the live client API is read; no token, no lockfile and no game file.
    internal static readonly LolSnapshot None=new(LolState.NoGame,"",0,0,0,0,0,0,0,null,null,null,Empty("BLEU"),Empty("ROUGE"),DateTimeOffset.MinValue);
    internal static LolSnapshot Idle(LolState state,string? error=null)=>None with{State=state,Error=error,CapturedAt=DateTimeOffset.UtcNow};
    // Howling Abyss has no dragon and no baron: the counters and timers that
    // describe them must not appear there.
    internal bool Aram=>MapNumber==12||Mode.Contains("ARAM",StringComparison.OrdinalIgnoreCase);
    internal double? Participation=>Players is null||Team.Length==0||Players.Where(p=>p.Team==Team).Sum(p=>p.Kills)==0?null:
        Math.Clamp(100d*(Kills+Assists)/Players.Where(p=>p.Team==Team).Sum(p=>p.Kills),0,100);
    internal double CsPerMinute=>GameTime>0?CreepScore*60/GameTime:0;
    internal double Kda=>(Kills+Assists)/(double)Math.Max(1,Deaths);
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
    readonly LolGoldHistory goldHistory=new();
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
                    Publish(goldHistory.Observe(Parse(body,DateTimeOffset.UtcNow)));live=true;
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
        string name=mine is{} known?Text(known,"summonerName"):"";
        string team=mine is{} side?Text(side,"team"):"";
        string champion=mine is{} found?Text(found,"championName"):"";
        int level=(int)Math.Max(Number(active,"level"),mine is{} player?Number(player,"level"):0);
        bool dead=(mine is{} deadPlayer&&Bool(deadPlayer,"isDead"))||Bool(active,"isDead");
        double? respawn=dead?Math.Max(0,Math.Max(Number(active,"respawnTimer"),mine is{} fallen?Number(fallen,"respawnTimer"):0)):null;
        var (blue,red,feed,dragon,baron)=Events(root,roster,time,identity,name,team);
        return new LolSnapshot(LolState.Live,champion,level,time,scores.Kills,scores.Deaths,scores.Assists,scores.CreepScore,(int)Number(active,"currentGold"),respawn,
            dragon,baron,
            new LolObjective("BLEU",blue.Dragons,blue.Barons,blue.Turrets,blue.Inhibitors,blue.Dragon),
            new LolObjective("ROUGE",red.Dragons,red.Barons,red.Turrets,red.Inhibitors,red.Dragon),
            capturedAt,Error:null,MapNumber:(int)Number(game,"mapNumber"),Mode:Text(game,"gameMode"),Events:EventNames(root),
            Map:Text(game,"mapName"),Team:team,
            Streak:Math.Clamp(feed.Streak,0,scores.Kills),MultiKill:feed.MultiKill,MultiKillAt:At(feed.MultiKillAt),
            LastKillAt:At(feed.LastKillAt),LastDeathAt:At(feed.LastDeathAt),FirstBlood:feed.FirstBlood,AceAt:At(feed.AceAt),
            ChampionId:mine is{} icon?ChampionId(icon):"",
            Players:roster.Select(p=>new LolPlayer(Text(p,"championName"),ChampionId(p),Text(p,"team"),Score(p).Kills,Score(p).Deaths,Score(p).Assists,Bool(p,"isDead"))).ToArray(),
            Inventory:mine is{} owner&&owner.TryGetProperty("items",out var items)?string.Join(";",items.EnumerateArray().Select(i=>$"{Number(i,"itemID")}:{Number(i,"count")}").Order()):"",
            GoldExact:active.TryGetProperty("currentGold",out var gold)&&gold.TryGetDouble(out double amount)?amount:null);
    }
    static string ChampionId(JsonElement player)
    {
        string raw=Text(player,"rawChampionName"),prefix="game_character_displayname_";
        return raw.StartsWith(prefix,StringComparison.Ordinal)?raw[prefix.Length..]:Text(player,"championName").Replace(" ","").Replace("'","").Replace(".","");
    }
    // What the events really carry: the kills the player landed, the death that
    // clears the streak, the first blood and the ace of the player's team.
    readonly record struct Feed(int Streak,string MultiKill,double MultiKillAt,double LastKillAt,double LastDeathAt,bool FirstBlood,double AceAt);
    // Two kills ten seconds apart chain; the fifth keeps the longer window the
    // game allows before declaring the pentakill.
    const double ChainWindow=10,PentaWindow=30;
    static double? At(double value)=>double.IsNaN(value)?null:value;
    static string ChainLabel(int chain)=>chain switch{>=5=>"PENTA KILL",4=>"QUADRA KILL",3=>"TRIPLE KILL",2=>"DOUBLE KILL",_=>""};
    // The event names are published as they are: they are the only honest source
    // for a respawn timer, and a pickup without an event cannot be timed.
    static string[] EventNames(JsonElement root)
    {
        if(!root.TryGetProperty("events",out var events)||!events.TryGetProperty("Events",out var list)||list.ValueKind!=JsonValueKind.Array)return [];
        var names=new List<string>();
        foreach(var item in list.EnumerateArray()){var name=Text(item,"EventName");if(name.Length>0&&!names.Contains(name))names.Add(name);}
        return names.Take(24).ToArray();
    }
    static (LolObjective Blue,LolObjective Red,Feed Feed,double? Dragon,double? Baron) Events(JsonElement root,JsonElement[] roster,double time,string identity,string name,string team)
    {
        int[] dragons=[0,0],barons=[0,0],turrets=[0,0],inhibitors=[0,0];
        string[] element=["",""];
        double dragonAt=double.NaN,baronAt=double.NaN,chainAt=double.NaN,lastKill=double.NaN,lastDeath=double.NaN,multiAt=double.NaN,aceAt=double.NaN;
        int streak=0,chain=0;
        bool firstBlood=false;
        bool mine(string value)=>value.Length>0&&(value==identity||name.Length>0&&value==name);
        if(root.TryGetProperty("events",out var events)&&events.TryGetProperty("Events",out var list)&&list.ValueKind==JsonValueKind.Array)
            foreach(var item in list.EnumerateArray())
            {
                string title=Text(item,"EventName"),killer=Text(item,"KillerName");
                double at=Number(item,"EventTime");
                int side=Side(roster,killer);
                switch(title)
                {
                    case "DragonKill":if(side<0)break;dragons[side]++;element[side]=Text(item,"DragonType");if(double.IsNaN(dragonAt)||at>dragonAt)dragonAt=at;break;
                    case "BaronKill":if(side<0)break;barons[side]++;if(double.IsNaN(baronAt)||at>baronAt)baronAt=at;break;
                    case "TurretKilled":if(side>=0)turrets[side]++;break;
                    case "InhibKilled":if(side>=0)inhibitors[side]++;break;
                    // The streak belongs to the player, not to the team: two kills
                    // ten seconds apart chain, and dying clears the chain.
                    case "ChampionKill":
                        if(mine(killer))
                        {
                            streak++;
                            chain=double.IsNaN(chainAt)||at-chainAt>(chain>=4?PentaWindow:ChainWindow)?1:chain+1;
                            chainAt=at;lastKill=at;
                            if(chain>=2)multiAt=at;
                        }
                        if(mine(Text(item,"VictimName"))){streak=0;chain=0;chainAt=double.NaN;lastDeath=at;}
                        break;
                    case "FirstBlood":if(mine(Text(item,"Recipient"))||mine(killer))firstBlood=true;break;
                    case "Ace":if(team.Length>0&&(Text(item,"Acer")==team||Text(item,"AcingTeam")==team))aceAt=at;break;
                }
            }
        double? next(double last,double interval)=>double.IsNaN(last)?null:Math.Max(0,last+interval-time);
        return (new("BLEU",dragons[0],barons[0],turrets[0],inhibitors[0],element[0]),new("ROUGE",dragons[1],barons[1],turrets[1],inhibitors[1],element[1]),
            new Feed(streak,double.IsNaN(multiAt)?"":ChainLabel(chain),multiAt,lastKill,lastDeath,firstBlood,aceAt),
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

// Wallet gains over a rolling minute, not lifetime earnings. A changed inventory,
// a falling wallet or a sampling gap breaks the estimate instead of turning a
// purchase into negative income (or a sale into a burst of income).
internal sealed class LolGoldHistory
{
    readonly Queue<(double Time,double Gold)> samples=new();
    LolSnapshot? previous;
    internal LolSnapshot Observe(LolSnapshot next)
    {
        var before=previous;previous=next;
        if(next.GoldExact is null||before is null||before.ChampionId!=next.ChampionId||before.Team!=next.Team||
            next.GameTime<=before.GameTime||next.GameTime-before.GameTime>4||
            (next.CapturedAt-before.CapturedAt).TotalSeconds>4||next.Inventory!=before.Inventory||next.GoldExact<before.GoldExact)
            samples.Clear();
        if(next.GoldExact is not{} gold)return next;
        samples.Enqueue((next.GameTime,gold));
        while(samples.Count>1&&samples.Peek().Time<next.GameTime-60)samples.Dequeue();
        var first=samples.Peek();double span=next.GameTime-first.Time;
        var points=samples.ToArray();
        double[] trend=points.Zip(points.Skip(1),(a,b)=>(b.Gold-a.Gold)/(b.Time-a.Time)).ToArray();
        return next with{GoldPerSecond=span>=10?(gold-first.Gold)/span:null,GoldWindow=span,GoldTrend=trend,GoldTrendTimes=points.Skip(1).Select(p=>p.Time).ToArray()};
    }
}
