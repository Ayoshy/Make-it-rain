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
                migrationPending=saved.Version<3;NeedsRedesign=saved.Version<7;
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
    void Persist(string current,Dictionary<string,DesktopProfile> next,string? returnScene)=>DesktopSettings.Write(path,JsonSerializer.Serialize(new ProfileFile(current,next,7,returnScene),new JsonSerializerOptions{WriteIndented=true}));
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
            // Refonte du 25 septembre : Disques, Terminal, Projets et Vidéo dans chaque
            // scène ; en Jeu, le principal (écran de jeu) ne porte que des blocs sans
            // rapport avec le jeu, les blocs utiles en partie vivent sur le secondaire.
            "Bureau"=>[
                ("terminal",24,24,1280,948),("projects",24,996,1280,420),
                ("notes",1328,24,560,324),("reminders",1328,372,560,132),
                ("gmail",1328,528,560,444),("atelier",1328,996,560,420),
                ("video",2584,24,1440,864),("apps",2584,912,1440,160),("network",2584,1096,592,320),
                ("clock",4504,24,592,156),("weather",4504,204,592,204),
                ("disks",4504,432,592,384),("hardware",4504,840,592,224),("usage",4504,1088,592,224)],
            "Jeu"=>[
                ("apps",24,24,2512,160),
                ("terminal",24,208,1500,852),("projects",24,1084,1500,332),
                ("disks",1548,208,988,428),("usage",1548,660,988,300),("bluetooth",1548,984,988,192),
                ("video",2584,24,1560,876),("audio",2584,924,768,300),("hardware",3376,924,768,300),
                ("clock",2584,1248,768,168),("music",3376,1248,768,168),
                ("countdown",4168,24,928,396),("dualsense",4168,444,928,444),
                ("lol",4168,912,928,192),("network",4168,1128,928,288)],
            "Multimédia"=>[
                ("terminal",24,24,1500,1032),("projects",24,1080,1500,336),
                ("music",1548,24,988,600),("audio",1548,648,988,432),("bluetooth",1548,1104,988,312),
                ("video",2584,24,1896,1128),("apps",2584,1176,1896,160),
                ("clock",4504,24,592,168),("weather",4504,216,592,240),("disks",4504,480,592,456)],
            Mono=>[
                ("clock",24,24,512,160),("apps",560,24,1976,160),
                ("terminal",24,208,1320,800),("projects",24,1032,1320,384),
                ("video",1368,208,1168,656),("disks",1368,888,572,528),
                ("audio",1964,888,572,252),("music",1964,1164,572,252)],
            _=>throw new ArgumentException("Scène inconnue.")
        };
        return original.Select(block=>{
            var cell=cells.FirstOrDefault(c=>c.Id==block.Id);
            return cell.Id is null?block with{Visible=false}:block with{X=cell.X,Y=cell.Y,Width=cell.W,Height=cell.H,Visible=true};
        }).ToList();
    }
}
