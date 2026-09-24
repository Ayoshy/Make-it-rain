using System.IO;
using System.Text.Json;

namespace Battlestation;
internal sealed record DesktopProfile(List<DesktopBlock> Blocks,bool Animate,bool Reactive,double Intensity,double Glass,string ThemeId="aurore",string TemplateId="Bureau");
internal sealed record ProfileFile(string Current,Dictionary<string,DesktopProfile> Profiles,int Version=1,string? ReturnScene=null);
internal sealed class DesktopProfiles
{
    readonly string path;
    Dictionary<string,DesktopProfile> profiles=[];
    bool migrationPending=true;
    internal bool NeedsRedesign {get;private set;}=true;
    internal string Current {get;private set;}="Bureau";
    internal const string Mono="Mono écran";
    internal string? ReturnScene {get;private set;}
    bool singleMonitor;
    internal static readonly string[] Presets=["Jeu","Multimédia"];
    internal static readonly string[] Names=["Bureau",..Presets,Mono];
    static readonly string[] Retired=["Personnel","Création","Focus","Double écran","Cinéma","Try 1"];
    static string Consolidated(string name)=>name switch{"Personnel" or "Création" or "Focus" or "Double écran" or "Try 1"=>"Bureau","Cinéma"=>"Multimédia",_=>name};
    internal IReadOnlyList<string> UserNames=>profiles.Keys.Where(name=>!Names.Contains(name)).OrderBy(name=>name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    internal IReadOnlyList<string> AllNames=>Names.Concat(UserNames).ToArray();
    internal DesktopProfiles(string file)
    {
        path=file;
        try
        {
            if(File.Exists(file)&&JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(file)) is {} saved)
            {
                migrationPending=saved.Version<3;NeedsRedesign=saved.Version<6;
                profiles=(saved.Profiles??[]).Where(p=>!string.IsNullOrWhiteSpace(p.Key)&&p.Value?.Blocks is not null).ToDictionary(p=>p.Key,p=>saved.Version<2?p.Value with{ThemeId="vice-city",TemplateId=Presets.Contains(p.Key)?p.Key:"Personnel"}:p.Value);
                if(Names.Contains(saved.Current)||profiles.ContainsKey(saved.Current))Current=saved.Current;
                ReturnScene=saved.ReturnScene;

            }
        }
        catch(Exception e) when(e is JsonException or IOException){}
    }
    DesktopProfile Snapshot(DesktopLayout layout,DesktopSettings settings)=>new(layout.Blocks.ToList(),settings.AnimateBackground,settings.ReactiveAudio,settings.AudioIntensity,settings.GlassOpacity,settings.ThemeId,Names.Contains(Current)?Current:profiles.GetValueOrDefault(Current)?.TemplateId??"Bureau");
    // layout.json and preferences.json are the last committed live scene, including on migration.
    internal void SaveCurrent(DesktopLayout layout,DesktopSettings settings)
    {
        // A failed migration must remain retryable and must not mark old layouts as format 6.
        if(NeedsRedesign)return;
        var snapshot=Snapshot(layout,settings);
        if(!migrationPending&&profiles.TryGetValue(Current,out var old)&&old with{Blocks=snapshot.Blocks}==snapshot&&old.Blocks.SequenceEqual(snapshot.Blocks))return;
        var next=new Dictionary<string,DesktopProfile>(profiles){[Current]=snapshot};
        Persist(Current,next);profiles=next;migrationPending=false;
    }
    internal IReadOnlyList<DesktopBlock> Preview(string name,DesktopLayout layout,DesktopSettings settings)=>name==Current?layout.Blocks:layout.Adapt(profiles.GetValueOrDefault(name)?.Blocks??Create(name,Snapshot(layout,settings)).Blocks);
    internal DesktopProfile Switch(string name,DesktopLayout layout,DesktopSettings settings)
        =>Switch(name,layout,settings,ReturnScene);
    internal DesktopProfile? MatchDisplays(bool single,DesktopLayout layout,DesktopSettings settings)
    {
        singleMonitor=single;
        if(single&&ReturnScene is null&&Current!=Mono)
            return Switch(Mono,layout,settings,Current);
        if(!single&&ReturnScene is {} previous)
            return Switch(previous,layout,settings,null);
        layout.SingleScreen=single||Snapshot(layout,settings).TemplateId==Mono;
        return null;
    }
    DesktopProfile Switch(string name,DesktopLayout layout,DesktopSettings settings,string? returnScene)
    {
        if(!AllNames.Contains(name))throw new ArgumentException("Scène inconnue.");
        var current=Snapshot(layout,settings);
        var destination=name==Current?current:profiles.GetValueOrDefault(name)??Create(name,current);
        if(name!=Current)destination=destination with{Blocks=layout.Adapt(destination.Blocks)};
        _=(settings with{AnimateBackground=destination.Animate,ReactiveAudio=destination.Reactive,AudioIntensity=destination.Intensity,GlassOpacity=destination.Glass,ThemeId=destination.ThemeId}).Validate(false);
        bool previousLimit=layout.SingleScreen;
        layout.SingleScreen=singleMonitor||destination.TemplateId==Mono;
        if(!layout.Restore(destination.Blocks)){layout.SingleScreen=previousLimit;throw new InvalidOperationException(singleMonitor?"Cette scène utilise le secondaire. Réorganise Mono écran ou rebranche le secondaire.":"Cette scène ne tient plus. Ajuste les blocs avant de la réutiliser.");}
        var next=new Dictionary<string,DesktopProfile>(profiles){[Current]=current,[name]=destination};
        try{Persist(name,next,returnScene);}catch{layout.SingleScreen=previousLimit;layout.Restore(current.Blocks);throw;}
        profiles=next;Current=name;ReturnScene=returnScene;return destination;
    }
    internal DesktopProfile ResetTemplate(DesktopLayout layout,DesktopSettings settings,int apps)
    {
        var current=Snapshot(layout,settings);
        var baseline=new DesktopProfile(DesktopLayout.Defaults(apps),true,true,.55,.46);
        var destination=Create(current.TemplateId,baseline);
        destination=destination with{Blocks=layout.Adapt(destination.Blocks)};
        if(!layout.Restore(destination.Blocks))throw new InvalidOperationException("Le modèle ne tient pas dans le bureau.");
        var next=new Dictionary<string,DesktopProfile>(profiles){[Current]=destination};
        try{Persist(Current,next);}catch{layout.Restore(current.Blocks);throw;}
        profiles=next;return destination;
    }
    internal void SaveUser(string name,DesktopLayout layout,DesktopSettings settings)
    {
        name=name.Trim();
        if(name.Length is <2 or >40||name.Any(char.IsControl))throw new ArgumentException("Le nom doit contenir entre 2 et 40 caractères.");
        if(Names.Contains(name,StringComparer.CurrentCultureIgnoreCase))throw new ArgumentException("Ce nom est réservé à une scène intégrée.");
        if(profiles.Keys.Any(existing=>string.Equals(existing,name,StringComparison.CurrentCultureIgnoreCase)))throw new ArgumentException("Une scène porte déjà ce nom.");
        var snapshot=Snapshot(layout,settings);
        var next=new Dictionary<string,DesktopProfile>(profiles){[Current]=snapshot,[name]=snapshot};
        Persist(name,next);profiles=next;Current=name;
    }
    internal void DeleteUser(string name)
    {
        if(!UserNames.Contains(name)||Current==name||ReturnScene==name)throw new ArgumentException("Cette scène est active ou attend le retour du secondaire.");
        var next=new Dictionary<string,DesktopProfile>(profiles);next.Remove(name);Persist(Current,next);profiles=next;
    }
    void Persist(string current,Dictionary<string,DesktopProfile> next)=>Persist(current,next,ReturnScene);
    void Persist(string current,Dictionary<string,DesktopProfile> next,string? returnScene)=>DesktopSettings.Write(path,JsonSerializer.Serialize(new ProfileFile(current,next,6,returnScene),new JsonSerializerOptions{WriteIndented=true}));
    internal DesktopProfile RedesignAll(DesktopLayout layout,DesktopSettings settings,int apps)
    {
        var existing=new Dictionary<string,DesktopProfile>(profiles){[Current]=Snapshot(layout,settings)};
        var baseline=new DesktopProfile(DesktopLayout.Defaults(apps),settings.AnimateBackground,settings.ReactiveAudio,settings.AudioIntensity,settings.GlassOpacity,settings.ThemeId);
        var next=new Dictionary<string,DesktopProfile>();
        foreach(string name in Names){
            var previousProfile=existing.GetValueOrDefault(name)??existing.GetValueOrDefault(name=="Bureau"?"Personnel":name=="Multimédia"?"Cinéma":name);
            var profile=Create(name,baseline);
            if(previousProfile is not null)profile=profile with{Animate=previousProfile.Animate,Reactive=previousProfile.Reactive,Intensity=previousProfile.Intensity,Glass=previousProfile.Glass};
            if(name==Mono&&previousProfile is not null)profile=profile with{ThemeId=previousProfile.ThemeId};
            if(profile.Blocks.Any(block=>!DesktopLayout.Valid(block,profile.Blocks)))throw new InvalidOperationException("Le modèle "+name+" contient un chevauchement.");
            next[name]=profile;
        }
        // Preserve any other personal copy, remapping only its reset-template reference.
        foreach(var (name,profile) in existing.Where(p=>!Names.Contains(p.Key)&&!Retired.Contains(p.Key)))
            next[name]=profile with{TemplateId=Consolidated(profile.TemplateId)};
        string current=Consolidated(Current);
        if(!next.ContainsKey(current))current="Bureau";
        string? returnScene=ReturnScene is null?null:Consolidated(ReturnScene);
        if(returnScene is not null&&!next.ContainsKey(returnScene))returnScene="Bureau";
        var destination=next[current];var previous=layout.Blocks.ToArray();
        destination=destination with{Blocks=layout.Adapt(destination.Blocks)};
        if(!layout.Restore(destination.Blocks))throw new InvalidOperationException("Le nouvel agencement ne tient pas.");
        next[current]=destination;
        try{Persist(current,next,returnScene);}catch{layout.Restore(previous);throw;}
        profiles=next;Current=current;ReturnScene=returnScene;NeedsRedesign=false;migrationPending=false;return destination;
    }
    static DesktopProfile Create(string name,DesktopProfile baseline)
    {
        if(!Names.Contains(name))return baseline with{TemplateId="Bureau"};
        if(name==Mono)return baseline with{Blocks=Preset(Mono,baseline.Blocks),TemplateId=Mono};
        string theme=name=="Bureau"?"aurore":name=="Multimédia"?"obsidienne":"vice-city";
        return baseline with{Blocks=Preset(name,baseline.Blocks),ThemeId=theme,TemplateId=name};
    }
    internal static List<DesktopBlock> Preset(string name,IEnumerable<DesktopBlock> original)
    {
        (string Id,double X,double Y,double W,double H)[] cells=name switch
        {
            "Bureau"=>[
                ("terminal",32,32,1280,944),("projects",32,1000,1280,344),
                ("notes",1344,32,560,312),("reminders",1344,368,560,168),
                ("atelier",1936,32,592,280),("usage",1936,496,592,224),
                ("hardware",1936,744,592,224),("network",1936,992,592,336),
                ("video",2592,32,1568,936),("apps",2592,992,1568,160),
                ("clock",4448,32,640,160),("weather",4448,216,640,224),
                ("music",4448,464,640,264),("audio",4448,752,640,264),("bluetooth",4448,1040,640,208)],
            "Jeu"=>[
                ("video",2584,24,1568,888),("terminal",2584,936,1568,480),
                ("dualsense",4176,24,920,600),("lol",4176,648,920,264),("network",4176,936,920,480),
                ("apps",24,24,2512,160),("clock",24,208,616,176),("weather",664,208,616,176),
                ("usage",24,408,616,280),("hardware",664,408,616,280),
                ("audio",24,712,1256,280),("bluetooth",24,1016,616,400),("music",664,1016,616,400),
                ("projects",1304,208,1232,560),("countdown",1304,792,1232,624)],
            "Multimédia"=>[
                ("terminal",24,24,1320,1040),("projects",24,1088,1320,328),
                ("music",1368,24,1168,608),("audio",1368,656,1168,472),("bluetooth",1368,1152,1168,264),
                ("video",2584,24,1888,1128),("apps",2584,1176,1888,240),
                ("clock",4496,24,600,200),("weather",4496,248,600,280)],
            Mono=>[
                ("clock",24,24,512,160),("apps",560,24,1976,160),
                ("terminal",24,208,1320,832),("projects",24,1064,1320,352),
                ("video",1368,208,1168,752),("music",1368,984,704,184),
                ("audio",1368,1192,704,224),("bluetooth",2096,984,440,432)],
            _=>throw new ArgumentException("Scène inconnue.")
        };
        return original.Select(block=>{
            var cell=cells.FirstOrDefault(c=>c.Id==block.Id);
            return cell.Id is null?block with{Visible=false}:block with{X=cell.X,Y=cell.Y,Width=cell.W,Height=cell.H,Visible=true};
        }).ToList();
    }
}
