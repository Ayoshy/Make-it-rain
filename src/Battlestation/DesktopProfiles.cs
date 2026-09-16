using System.IO;
using System.Text.Json;

namespace Battlestation;
internal sealed record DesktopProfile(List<DesktopBlock> Blocks,bool Animate,bool Reactive,double Intensity,double Glass,string ThemeId="vice-city",string TemplateId="Personnel");
internal sealed record ProfileFile(string Current,Dictionary<string,DesktopProfile> Profiles,int Version=1,string? ReturnScene=null);
internal sealed class DesktopProfiles
{
    readonly string path;
    Dictionary<string,DesktopProfile> profiles=[];
    bool migrationPending=true;
    internal bool NeedsRedesign {get;private set;}=true;
    internal string Current {get;private set;}="Personnel";
    internal const string Mono="Mono écran";
    internal string? ReturnScene {get;private set;}
    bool singleMonitor;
    internal static readonly string[] Presets=["Jeu","Création","Cinéma","Focus","Multimédia","Double écran"];
    internal static readonly string[] Names=["Personnel",..Presets,Mono];
    internal IReadOnlyList<string> UserNames=>profiles.Keys.Where(name=>!Names.Contains(name)).OrderBy(name=>name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    internal IReadOnlyList<string> AllNames=>Names.Concat(UserNames).ToArray();
    internal DesktopProfiles(string file)
    {
        path=file;
        try
        {
            if(File.Exists(file)&&JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(file)) is {} saved)
            {
                migrationPending=saved.Version<3;NeedsRedesign=saved.Version<4;
                profiles=(saved.Profiles??[]).Where(p=>!string.IsNullOrWhiteSpace(p.Key)&&p.Value?.Blocks is not null).ToDictionary(p=>p.Key,p=>saved.Version<2?p.Value with{ThemeId="vice-city",TemplateId=Presets.Contains(p.Key)?p.Key:"Personnel"}:p.Value);
                if(Names.Contains(saved.Current)||profiles.ContainsKey(saved.Current))Current=saved.Current;
                ReturnScene=saved.ReturnScene;

            }
        }
        catch(Exception e) when(e is JsonException or IOException){}
    }
    DesktopProfile Snapshot(DesktopLayout layout,DesktopSettings settings)=>new(layout.Blocks.ToList(),settings.AnimateBackground,settings.ReactiveAudio,settings.AudioIntensity,settings.GlassOpacity,settings.ThemeId,Names.Contains(Current)?Current:profiles.GetValueOrDefault(Current)?.TemplateId??"Personnel");
    // layout.json and preferences.json are the last committed live scene, including on migration.
    internal void SaveCurrent(DesktopLayout layout,DesktopSettings settings)
    {
        var snapshot=Snapshot(layout,settings);
        if(!migrationPending&&profiles.TryGetValue(Current,out var old)&&old with{Blocks=snapshot.Blocks}==snapshot&&old.Blocks.SequenceEqual(snapshot.Blocks))return;
        var next=new Dictionary<string,DesktopProfile>(profiles){[Current]=snapshot};
        Persist(Current,next);profiles=next;migrationPending=false;
    }
    internal IReadOnlyList<DesktopBlock> Preview(string name,DesktopLayout layout,DesktopSettings settings)=>name==Current?layout.Blocks:profiles.GetValueOrDefault(name)?.Blocks??Create(name,Snapshot(layout,settings)).Blocks;
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
    void Persist(string current,Dictionary<string,DesktopProfile> next,string? returnScene)=>DesktopSettings.Write(path,JsonSerializer.Serialize(new ProfileFile(current,next,4,returnScene),new JsonSerializerOptions{WriteIndented=true}));
    internal DesktopProfile RedesignAll(DesktopLayout layout,DesktopSettings settings,int apps)
    {
        var baseline=new DesktopProfile(DesktopLayout.Defaults(apps),true,true,.55,.46);
        var next=new Dictionary<string,DesktopProfile>();
        foreach(string name in AllNames){
            string template=Names.Contains(name)?name:profiles.GetValueOrDefault(name)?.TemplateId??"Personnel";
            if(!Names.Contains(template))template="Personnel";
            var profile=Create(template,baseline);
            if(profile.Blocks.Any(block=>!DesktopLayout.Valid(block,profile.Blocks)))throw new InvalidOperationException("Le modèle "+template+" contient un chevauchement.");
            next[name]=profile;
        }
        var destination=next[Current];var previous=layout.Blocks.ToArray();
        if(!layout.Restore(destination.Blocks))throw new InvalidOperationException("Le nouvel agencement ne tient pas.");
        try{Persist(Current,next);}catch{layout.Restore(previous);throw;}
        profiles=next;NeedsRedesign=false;migrationPending=false;return destination;
    }
    static DesktopProfile Create(string name,DesktopProfile baseline)
    {
        if(!Names.Contains(name))return baseline with{TemplateId="Personnel"};
        if(name==Mono)return baseline with{Blocks=Preset(Mono,baseline.Blocks),TemplateId=Mono};
        string theme=name is "Focus" or "Cinéma"?"obsidienne":name is "Création" or "Multimédia" or "Double écran"?"aurore":"vice-city";
        return baseline with{Blocks=Preset(name,baseline.Blocks),ThemeId=theme,TemplateId=name,Animate=name!="Focus",Reactive=name is not ("Focus" or "Cinéma"),Intensity=name=="Multimédia"?.65:name=="Jeu"?.35:name=="Focus"?0:.4,Glass=name=="Cinéma"?.28:name=="Focus"?.5:.44};
    }
    internal static List<DesktopBlock> Preset(string name,IEnumerable<DesktopBlock> original)
    {
        (string Id,double X,double Y,double W,double H)[] cells=name switch
        {
            Mono=>[
                ("clock",24,24,512,160),("apps",560,24,1976,160),
                ("terminal",24,208,1704,1176),
                ("music",1752,208,784,192),("audio",1752,424,784,352),
                ("bluetooth",1752,800,784,280)],
            "Personnel"=>[
                ("clock",2584,24,512,160),("apps",3120,24,1080,160),
                ("projects",2584,208,584,408),("music",3192,208,1008,192),
                ("weather",3192,424,480,192),("bluetooth",3696,424,504,192),
                ("terminal",2584,640,1616,776),
                ("network",4224,24,872,328),("hardware",4224,376,872,218),
                ("usage",4224,618,872,218),("audio",4224,860,872,300),
                ("reminders",4224,1184,872,232)],
            "Jeu"=>[
                ("clock",2584,24,536,144),("apps",3144,24,1136,144),
                ("dualsense",2584,192,1696,760),
                ("network",4304,24,792,328),("hardware",4304,376,792,218),
                ("usage",4304,618,792,218),("countdown",4304,860,792,556),
                ("audio",2584,976,648,440),("music",3256,976,1024,184),
                ("weather",3256,1184,488,232),("bluetooth",3768,1184,512,232)],
            "Création"=>[
                ("apps",2584,24,1696,144),("clock",4304,24,792,144),
                ("terminal",2584,192,1696,1032),("projects",4304,192,792,504),
                ("usage",4304,720,792,248),("network",4304,992,792,424),
                ("music",2584,1248,1000,168),("reminders",3608,1248,672,168)],
            "Cinéma"=>[
                ("video",2584,24,1888,1062),("clock",4496,24,600,184),
                ("network",4496,232,600,300),("audio",4496,556,600,416),
                ("bluetooth",4496,996,600,420),
                ("music",2584,1110,1104,306),("apps",3712,1110,760,306)],
            "Focus"=>[
                ("clock",2584,24,600,168),("reminders",3208,24,1216,168),
                ("terminal",2584,216,1840,1200),("projects",4448,24,648,504),
                ("network",4448,552,648,320),("usage",4448,896,648,224),
                ("apps",4448,1144,648,272)],
            "Multimédia"=>[
                ("video",2584,24,1568,1128),("apps",2584,1176,1568,240),
                ("clock",4176,24,440,168),("weather",4640,24,456,168),
                ("music",4176,216,920,264),("audio",4176,504,920,420),
                ("bluetooth",4176,948,440,468),("network",4640,948,456,468)],
            "Double écran"=>[
                ("clock",24,24,560,168),("apps",608,24,1928,168),
                ("projects",24,216,832,456),("reminders",24,696,832,216),
                ("network",24,936,832,480),("terminal",880,216,1656,1200),
                ("dualsense",2584,24,1104,720),("video",3712,24,1384,720),
                ("music",2584,768,1104,168),("audio",2584,960,1104,456),
                ("hardware",3712,768,680,260),("usage",4416,768,680,260),
                ("countdown",3712,1052,760,364),("weather",4496,1052,600,164),
                ("bluetooth",4496,1240,600,176)],
            _=>throw new ArgumentException("Scène inconnue.")
        };
        return original.Select(block=>{
            var cell=cells.FirstOrDefault(c=>c.Id==block.Id);
            return cell.Id is null?block with{Visible=false}:block with{X=cell.X,Y=cell.Y,Width=cell.W,Height=cell.H,Visible=true};
        }).ToList();
    }
}
