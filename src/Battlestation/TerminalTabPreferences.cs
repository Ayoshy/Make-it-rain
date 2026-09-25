using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlestation;
internal enum TerminalActivity { Unknown,Thinking,Working,Ready,Attention,Error }
internal sealed record TerminalTabPreference(string? Name=null,string? Color=null,bool AutomaticTitle=true,bool Effects=true);
internal sealed class TerminalTabPreferences
{
    readonly string path;
    Dictionary<Guid,TerminalTabPreference> values=[];
    public TerminalTabPreferences(string file)
    {
        path=file;
        try{if(File.Exists(path)&&new FileInfo(path).Length<=1024*1024)values=JsonSerializer.Deserialize<Dictionary<Guid,TerminalTabPreference>>(File.ReadAllText(path))??[];}
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){}
        values=values.Where(pair=>pair.Value is not null).ToDictionary(pair=>pair.Key,pair=>Normalize(pair.Value));
    }
    public TerminalTabPreference Get(Guid id)=>values.GetValueOrDefault(id)??new();
    public void Set(Guid id,TerminalTabPreference value)
    {
        value=Normalize(value);var next=new Dictionary<Guid,TerminalTabPreference>(values){[id]=value};
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);values=next;
    }
    public static string? Color(string? value)=>value is not null&&Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$")?value.ToUpperInvariant():null;
    public static string Clean(string? value)=>new((value??"").Where(c=>!char.IsControl(c)&&c is not ('\u202a' or '\u202b' or '\u202c' or '\u202d' or '\u202e' or '\u2066' or '\u2067' or '\u2068' or '\u2069')).Take(160).ToArray());
    static TerminalTabPreference Normalize(TerminalTabPreference value){string name=Clean(value.Name).Trim();return value with{Name=name.Length==0?null:name,Color=Color(value.Color)};}
    public TerminalTabInfo Decorate(TerminalTabInfo tab,ConsoleTitleInfo? metadata,TerminalCacheState cache=default,ClaudeSessionInfo? claude=null)
    {
        var preference=Get(tab.Id);bool claudeSession=metadata?.Claude==true;
        bool cli=metadata?.Codex==true||metadata?.Kilo==true||claudeSession;
        var parsed=TerminalTitle.Parse(metadata?.Title,metadata?.Codex==true,metadata?.Kilo==true,claudeSession);
        // Claude Code publie lui-même son état : le titre de console ne sert que de repli.
        if(claudeSession&&claude is {} session)
            parsed=(string.IsNullOrWhiteSpace(parsed.Title)&&session.Name is {} name?name:parsed.Title,session.Activity);
        string automatic=!cli||string.IsNullOrWhiteSpace(parsed.Title)?tab.Title:parsed.Title;
        return tab with{Title=preference.AutomaticTitle?automatic:preference.Name??tab.Title,Accent=preference.Color,Activity=parsed.Activity,Effects=preference.Effects,AutomaticTitle=preference.AutomaticTitle,SourceTitle=metadata?.Title,Badge=cache.Badge,CacheHint=cache.Hint,CacheDetail=cache.Detail,Detail=claudeSession?claude?.Detail:null};
    }
}
internal static class TerminalTitle
{
    // Kilo writes "[icon] Kilo CLI | session title"; the icon only appears when
    // tui.title_icon enables it in the user's Kilo configuration.
    static readonly (string Icon,TerminalActivity Activity)[] KiloStatus=
    [
        ("\u25D4",TerminalActivity.Working),("\uD83D\uDCAD",TerminalActivity.Working),
        ("\u26A0",TerminalActivity.Attention),("\uD83D\uDD36",TerminalActivity.Attention),
        ("\u2713",TerminalActivity.Ready),("\u2705",TerminalActivity.Ready),
    ];
    const string KiloBase="Kilo CLI";
    // Claude Code écrit son titre de console comme « <marqueur> <titre> » ; le
    // marqueur peut être l'astérisque de Claude ou une frame d'attente animée.
    static readonly char[] ClaudeMarkers=['\u00b7','\u2722','\u2733','\u2736','\u273b','\u273d','*'];
    public static (string Title,TerminalActivity Activity) Parse(string? title,bool codex,bool kilo=false,bool claude=false)
    {
        string clean=TerminalTabPreferences.Clean(title).Trim();
        if(kilo)return Kilo(clean);
        if(claude)return Claude(clean);
        if(!codex)return(clean,TerminalActivity.Unknown);
        var parts=clean.Split(" | ",StringSplitOptions.TrimEntries);
        if(parts.Length<2)return(clean,TerminalActivity.Unknown);
        int index=Array.FindLastIndex(parts,p=>State(p)!=TerminalActivity.Unknown);
        if(index<0)return(clean,TerminalActivity.Unknown);
        var state=State(parts[index]);
        int attention=Array.FindIndex(parts,p=>State(p)==TerminalActivity.Attention||p.StartsWith("Action required:",StringComparison.OrdinalIgnoreCase));
        if(attention>=0)state=TerminalActivity.Attention;
        string name=string.Join(" | ",parts.Where((part,i)=>i!=index&&i!=attention&&!Spinner(part)));
        return(string.IsNullOrWhiteSpace(name)?clean:name,state);
    }
    static (string Title,TerminalActivity Activity) Kilo(string clean)
    {
        var activity=TerminalActivity.Unknown;string rest=clean;
        foreach(var (icon,state) in KiloStatus)
            if(rest.StartsWith(icon+" ",StringComparison.Ordinal)){activity=state;rest=rest[(icon.Length+1)..];break;}
        if(rest.Equals(KiloBase,StringComparison.Ordinal))return(KiloBase,activity);
        // Ignore shell and launcher titles: only the CLI's own format is used.
        if(!rest.StartsWith(KiloBase+" | ",StringComparison.Ordinal))return(string.Empty,TerminalActivity.Unknown);
        string name=rest[(KiloBase.Length+3)..].Trim();
        return(string.IsNullOrWhiteSpace(name)?KiloBase:name,activity);
    }
    static (string Title,TerminalActivity Activity) Claude(string clean)
    {
        int start=0;
        while(start<clean.Length&&(ClaudeMarkers.Contains(clean[start])||clean[start] is >= '\u2800' and <= '\u28ff'))start++;
        if(start==0)return(clean,TerminalActivity.Unknown);
        int end=start;
        while(end<clean.Length&&char.IsWhiteSpace(clean[end]))end++;
        // Seules les frames d'attente prouvent un travail en cours ; l'astérisque
        // de Claude apparaît aussi au repos, l'état vient du registre des sessions.
        var activity=clean[..start].Any(character=>character is >= '\u2800' and <= '\u28ff')?TerminalActivity.Working:TerminalActivity.Unknown;
        string name=clean[end..].Trim();
        return(string.IsNullOrWhiteSpace(name)?clean:name,activity);
    }
    static bool Spinner(string value)=>value.Length==1&&(value[0] is >= '\u2800' and <= '\u28ff'||"◐◓◑◒◴◷◶◵⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏".Contains(value));
    static TerminalActivity State(string value)=>value.Trim().ToLowerInvariant() switch
        {
            "thinking"=>TerminalActivity.Thinking,
            "working" or "running" or "busy"=>TerminalActivity.Working,
            "ready" or "idle" or "done" or "completed"=>TerminalActivity.Ready,
            "needs input" or "needs-input" or "needs approval" or "needs-approval" or "waiting" or "waiting for input" or "waiting for approval" or "approval requested" or "question pending" or "action required" or "[ ! ] action required" or "input required" or "approval required"=>TerminalActivity.Attention,
            "error" or "failed"=>TerminalActivity.Error,
            _=>TerminalActivity.Unknown
        };
}
