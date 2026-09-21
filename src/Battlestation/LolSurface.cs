using System.Windows;

namespace Battlestation;

// League of Legends telemetry, read only while a game process is running and the
// dock is exposed. Nothing is written back and no other game data is touched.
internal sealed class LolSurface : Surface,IDisposable
{
    const string Blue="#84D8FF",Red="#FF9DBB",Error="#F4B7CA";
    readonly LolTelemetry telemetry=new();
    LolSnapshot rendered=LolSnapshot.None;
    bool active;
    internal LolSurface(Station station):base(station,16){Width=700;Height=220;}
    internal void SetActive(bool value)
    {
        if(active==value)return;
        active=value;telemetry.SetActive(value);
        if(value)Poll();
    }
    // Called by the desktop timer: the worker fetches, the dock only repaints on a new reading.
    internal void Poll()
    {
        var next=telemetry.Snapshot;
        if(next==rendered)return;
        rendered=next;Refresh();
    }
    internal object Inspect()
    {
        var state=telemetry.Snapshot;
        return new{state=state.State.ToString(),animating=active,champion=state.Champion,level=state.Level,gameTime=Math.Round(state.GameTime,1),
            kills=state.Kills,deaths=state.Deaths,assists=state.Assists,creepScore=state.CreepScore,gold=state.Gold,
            respawn=state.Respawn,dragonIn=state.DragonIn,baronIn=state.BaronIn,blue=state.Blue,red=state.Red,capturedAt=state.CapturedAt,error=state.Error,
            map=state.MapNumber,mode=state.Mode,aram=state.Aram,events=state.Events??[]};
    }
    protected override void Paint()
    {
        var state=telemetry.Snapshot;
        Header("LOL");
        if(state.State!=LolState.Live)
        {
            var (message,color)=state.State switch
            {
                LolState.Waiting=>("En attente de la partie",Muted),
                LolState.Unavailable=>("Télémétrie indisponible",Error),
                _=>("Aucune partie",Muted)
            };
            Text(message,24,58,15,color,width:Width-48);
            return;
        }
        double half=Width/2;
        Text(state.Champion.Length==0?"Champion inconnu":state.Champion,24,40,20,"#F4EDF9",bold:true,width:half-24);
        Text($"Nv {state.Level} · {Gold(state.Gold)}",24,74,11,Muted,width:half-24);
        Text($"{state.Kills} / {state.Deaths} / {state.Assists}",24,100,17,Ink,width:half);
        Text($"{state.CreepScore} cs",24,130,10,"#B8A8C5",width:half);
        Text(Clock(state.GameTime),Width-24,38,22,Ink,DockAppearance.NumberFont,align:"right");
        if(state.Respawn is{} respawn)Text($"Respawn {Math.Max(0,Math.Ceiling(respawn)):0} s",Width-24,72,12,Error,align:"right");
        // Howling Abyss has neither dragon nor baron: nothing to count down there.
        var timers=state.Aram?[]:new[]{Timer("Dragon",state.DragonIn),Timer("Baron",state.BaronIn)}.Where(value=>value.Length>0).ToArray();
        if(timers.Length>0)Text(string.Join(" · ",timers),Width-24,104,11,"#D8C8E3",align:"right");
        double banner=Height-46;
        Line(24,banner-14,Width-24,banner-14,"#30C9B4DB");
        Text(Banner(state.Blue,state.Aram),24,banner,11,Blue,width:half-30);
        Text(Banner(state.Red,state.Aram),Width-24,banner,11,Red,align:"right");
    }
    static string Clock(double seconds)
    {
        int total=(int)Math.Max(0,seconds);return $"{total/60:00}:{total%60:00}";
    }
    static string Timer(string name,double? remaining)
    {
        if(remaining is not{} value||!double.IsFinite(value))return "";
        value=Math.Max(0,value);return value<=0?name+" —":$"{name} {Clock(value)}";
    }
    static string Gold(int gold)=>gold>=1000?$"{(gold/1000d).ToString("0.0",French)} k or":gold+" or";
    static string Banner(LolObjective objective,bool aram)=>aram
        ?$"{objective.Team} · T {objective.Turrets} · I {objective.Inhibitors}"
        :$"{objective.Team} · D {objective.Dragons} · B {objective.Barons} · T {objective.Turrets} · I {objective.Inhibitors}";
    public void Dispose(){telemetry.Dispose();}
}
