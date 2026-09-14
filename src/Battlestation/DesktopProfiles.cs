using System.IO;
using System.Text.Json;

namespace Battlestation;
internal sealed record DesktopProfile(List<DesktopBlock> Blocks,bool Animate,bool Reactive,double Intensity,double Glass);
internal sealed record ProfileFile(string Current,Dictionary<string,DesktopProfile> Profiles);
internal sealed class DesktopProfiles
{
    readonly string path;
    Dictionary<string,DesktopProfile> profiles=[];
    internal string Current {get;private set;}="Personnel";
    internal static readonly string[] Presets=["Jeu","Création","Cinéma","Focus","Multimédia","Double écran"];
    internal static readonly string[] Names=["Personnel",..Presets];
    internal IReadOnlyList<string> UserNames=>profiles.Keys.Where(name=>name!="Personnel"&&!Presets.Contains(name)).OrderBy(name=>name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    internal IReadOnlyList<string> AllNames=>Names.Concat(UserNames).ToArray();
    internal DesktopProfiles(string file)
    {
        path=file;
        try
        {
            if(File.Exists(file)&&JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(file)) is {} saved&&((Names.Contains(saved.Current))||saved.Profiles?.ContainsKey(saved.Current)==true))
            {Current=saved.Current;profiles=(saved.Profiles??[]).Where(p=>(Names.Contains(p.Key)||!string.IsNullOrWhiteSpace(p.Key))&&p.Value?.Blocks is not null).ToDictionary(p=>p.Key,p=>p.Value);}
        }
        catch(Exception e) when(e is JsonException or IOException){}
    }
    internal DesktopProfile Switch(string name,DesktopLayout layout,DesktopSettings settings)
    {
        if(!AllNames.Contains(name))throw new ArgumentException("Disposition inconnue.");
        var current=new DesktopProfile(layout.Blocks.ToList(),settings.AnimateBackground,settings.ReactiveAudio,settings.AudioIntensity,settings.GlassOpacity);
        var destination=name=="Personnel"
            ? (name==Current?current:profiles.GetValueOrDefault(name)??Create(name,current))
            : Presets.Contains(name)?Create(name,current):profiles[name];
        _=(settings with{AnimateBackground=destination.Animate,ReactiveAudio=destination.Reactive,AudioIntensity=destination.Intensity,GlassOpacity=destination.Glass}).Validate(false);
        if(!layout.Restore(destination.Blocks))throw new InvalidOperationException("Cette disposition ne tient plus. Ajuste les blocs avant de la réutiliser.");
        var next=new Dictionary<string,DesktopProfile>(profiles);
        if(Current=="Personnel"||!Presets.Contains(Current))next[Current]=current;
        next[name]=destination;
        try{DesktopSettings.Write(path,JsonSerializer.Serialize(new ProfileFile(name,next),new JsonSerializerOptions{WriteIndented=true}));}
        catch{layout.Restore(current.Blocks);throw;}
        profiles=next;Current=name;return destination;
    }
    internal void SaveUser(string name,DesktopLayout layout,DesktopSettings settings)
    {
        name=NormalizeUserName(name);var snapshot=new DesktopProfile(layout.Blocks.ToList(),settings.AnimateBackground,settings.ReactiveAudio,settings.AudioIntensity,settings.GlassOpacity);
        if(Current=="Personnel"||!profiles.ContainsKey("Personnel"))profiles["Personnel"]=snapshot;
        profiles[name]=snapshot;Current=name;Write();
    }
    internal void DeleteUser(string name)
    {
        if(!UserNames.Contains(name))throw new ArgumentException("Disposition personnelle inconnue.");
        profiles.Remove(name);if(Current==name)Current="Personnel";Write();
    }
    static string NormalizeUserName(string name)
    {
        name=name.Trim();if(name.Length is <2 or >40||name.Any(char.IsControl))throw new ArgumentException("Le nom doit contenir entre 2 et 40 caractères.");
        if(Names.Contains(name,StringComparer.CurrentCultureIgnoreCase))throw new ArgumentException("Ce nom est réservé à une disposition intégrée.");
        return name;
    }
    void Write()=>DesktopSettings.Write(path,JsonSerializer.Serialize(new ProfileFile(Current,profiles),new JsonSerializerOptions{WriteIndented=true}));
    static DesktopProfile Create(string name,DesktopProfile baseline)
    {
        if(Presets.Contains(name))return baseline with{Blocks=Preset(name,baseline.Blocks),Reactive=name!="Cinéma",Intensity=name=="Jeu"?.35:name=="Création"?.55:.15};
        string[] keep=name switch
        {
            "Jeu"=>["clock","apps","music","hardware","audio","countdown","video"],
            "Création"=>["clock","apps","projects","terminal","usage","audio","music"],
            "Cinéma"=>["music","audio","video","reserve"],
            _=>baseline.Blocks.Where(b=>b.Visible).Select(b=>b.Id).ToArray()
        };
        // First visit preserves all positions and only hides irrelevant blocks.
        // Later visits restore the user's own arrangement for that mode.
        return baseline with{Blocks=baseline.Blocks.Select(b=>b with{Visible=b.Visible&&keep.Contains(b.Id)}).ToList(),Reactive=name!="Cinéma",Intensity=name=="Jeu"?.35:name=="Création"?.55:.15};
    }
    internal static List<DesktopBlock> Preset(string name,IEnumerable<DesktopBlock> original)
    {
        (string Id,double X,double Y,double W,double H)[] cells=name switch
        {
            "Jeu"=>[
                ("clock",2584,24,760,164),("apps",3368,24,920,164),
                ("countdown",2584,212,1704,840),("hardware",4300,212,820,218),
                ("usage",4300,454,820,209),("music",4300,687,820,168),
                ("weather",4300,879,820,112),("audio",2584,1064,2536,336)],
            "Création"=>[
                ("clock",2584,24,520,164),("apps",3128,24,1200,164),
                ("terminal",2584,212,1744,1216),("projects",4352,212,744,560),
                ("usage",4352,784,744,209),("music",4352,1017,744,168)],
            "Cinéma"=>[
                ("video",2584,24,1704,840),("music",4300,24,820,336),
                ("audio",4300,372,820,400),("clock",2584,876,820,164),
                ("apps",3416,876,868,164),("weather",4300,1064,820,112)],
            "Focus"=>[
                ("clock",2584,24,520,232),("apps",3128,24,1200,232),
                ("terminal",2584,280,1744,1136),("projects",4352,24,744,464),
                ("hardware",4352,512,744,218),("usage",4352,754,744,209),
                ("music",4352,987,744,168),("weather",4352,1179,744,112)],
            "Multimédia"=>[
                ("video",2584,24,1728,1056),("clock",4336,24,760,164),
                ("music",4336,212,760,168),("audio",4336,404,760,400),
                ("apps",4336,828,760,116),("weather",4336,968,760,112)],
            "Double écran"=>[
                ("projects",24,24,1400,240),("clock",1448,24,1088,240),
                ("terminal",24,288,2512,1128),("video",2584,24,1728,960),
                ("countdown",4336,24,760,552),("hardware",4336,600,760,218),
                ("usage",4336,842,760,218),("audio",4336,1084,760,336),
                ("apps",2584,1008,1728,116),("music",2584,1148,1104,168),
                ("weather",3712,1148,600,168)],
            _=>throw new ArgumentException("Disposition prête à l’emploi inconnue.")
        };
        return original.Select(block=>{
            var cell=cells.FirstOrDefault(c=>c.Id==block.Id);
            return cell.Id is null?block with{Visible=false}:block with{X=cell.X,Y=cell.Y,Width=cell.W,Height=cell.H,Visible=true};
        }).ToList();
    }
}
