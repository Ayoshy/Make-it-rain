using System.IO;
using System.Text.Json;
using Battlestation;

internal static class SceneMigrationTests
{
    internal static void Run(string directory,Action<bool,string> check,string[] args)
    {
        string folder=Path.Combine(directory,"migration");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"profiles.json"),layoutFile=Path.Combine(folder,"layout.json");
        var settings=new DesktopSettings(folder,"Aix",43.5,5.4){GlassOpacity=.37,AnimateBackground=false,AudioIntensity=.23};
        var blocks=DesktopLayout.Defaults(11);
        var oldProfile=new DesktopProfile(blocks,false,true,.23,.37,"vice-city","Personnel");
        var old=new Dictionary<string,DesktopProfile>();
        foreach(string name in new[]{"Personnel","Jeu","Création","Cinéma","Focus","Multimédia","Double écran",DesktopProfiles.Mono,"Try 1"})old[name]=oldProfile with{TemplateId=name};
        old["Copie utile"]=oldProfile with{TemplateId="Création"};
        File.WriteAllText(file,JsonSerializer.Serialize(new ProfileFile("Multimédia",old,5)));
        var layout=new DesktopLayout(layoutFile,11);var profiles=new DesktopProfiles(file);
        string before=File.ReadAllText(file);profiles.SaveCurrent(layout,settings);
        check(File.ReadAllText(file)==before&&profiles.NeedsRedesign,"Une sauvegarde prématurée ne marque pas l'ancien format comme migré");
        profiles.RedesignAll(layout,settings,11);
        check(profiles.Current=="Multimédia"&&profiles.AllNames.SequenceEqual(new[]{"Bureau","Jeu","Multimédia",DesktopProfiles.Mono,"Copie utile"}),"Tri : quatre modèles, copie conservée, Try 1 et anciens modèles retirés");
        var migrated=JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(file))!;
        check(migrated.Version==7&&migrated.Profiles["Copie utile"].Blocks.SequenceEqual(blocks)&&migrated.Profiles["Copie utile"].TemplateId=="Bureau","Format 7 : copie personnelle intacte, référence du modèle mise à jour");
        check(migrated.Profiles["Multimédia"] is{ThemeId:"obsidienne",Glass:.37,Animate:false,Intensity:.23},"L'ambiance change sans effacer les réglages de verre et animation");
        layout.SetVisible("weather",false);profiles.SaveCurrent(layout,settings);var edited=layout.Blocks.ToArray();
        var again=new DesktopProfiles(file);check(!again.NeedsRedesign,"La migration ne se répète pas");
        again.Switch("Jeu",layout,settings);again.Switch("Multimédia",layout,settings);
        check(layout.Blocks.SequenceEqual(edited),"Les modifications après migration survivent aux changements de scène");
        File.WriteAllText(file,JsonSerializer.Serialize(new ProfileFile(DesktopProfiles.Mono,old,5,"Focus")));
        profiles=new DesktopProfiles(file);profiles.RedesignAll(layout,settings,11);
        check(profiles.Current==DesktopProfiles.Mono&&profiles.ReturnScene=="Bureau","Migration en mono : scène de retour renommée");
        profiles.MatchDisplays(false,layout,settings);
        check(profiles.Current=="Bureau"&&profiles.ReturnScene is null,"Rebranchement après migration : retour vers Bureau");
        if(args.Length>0){
            string source=Path.GetFullPath(args[0]);
            File.Copy(Path.Combine(source,"profiles.json"),file,true);File.Copy(Path.Combine(source,"layout.json"),layoutFile,true);
            layout=new DesktopLayout(layoutFile,11);settings=DesktopSettings.Load(Path.Combine(source,"preferences.json"),settings);
            profiles=new DesktopProfiles(file);profiles.RedesignAll(layout,settings,11);
            check(profiles.AllNames.SequenceEqual(DesktopProfiles.Names)&&profiles.Current=="Multimédia","Copie des préférences réelles : quatre scènes et Multimédia active");
            foreach(string name in profiles.AllNames){profiles.Switch(name,layout,settings);check(layout.Blocks.All(b=>layout.Valid(b)),"Préférences réelles : "+name+" tient sans chevauchement");}
        }
    }
}
