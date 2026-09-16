using System.IO;
using System.Text.Json;
using System.Windows;
using Battlestation;

internal static class SingleScreenTests
{
    internal static void Run(string directory,Action<bool,string> check)
    {
        string folder=Path.Combine(directory,"single-screen");Directory.CreateDirectory(folder);
        string profilePath=Path.Combine(folder,"profiles.json"),layoutPath=Path.Combine(folder,"layout.json");
        var layout=new DesktopLayout(layoutPath,11);
        var settings=new DesktopSettings(folder,"Aix",43.5,5.4){ThemeId="aurore",GlassOpacity=.37};
        var profiles=new DesktopProfiles(profilePath);
        profiles.SaveCurrent(layout,settings);var personal=layout.Blocks.ToArray();
        profiles.SaveUser("Bureau perso",layout,settings);layout.SetVisible("countdown",false);layout.Save();profiles.SaveCurrent(layout,settings);
        var dual=layout.Blocks.ToArray();var before=JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(profilePath))!;
        var mono=profiles.MatchDisplays(true,layout,settings);
        check(mono is not null&&profiles.Current==DesktopProfiles.Mono&&profiles.ReturnScene=="Bureau perso","Débranchement : scène mono et retour à la scène personnelle mémorisés");
        check(layout.Blocks.Where(b=>b.Visible).Select(b=>b.Id).ToHashSet().SetEquals(["clock","apps","music","audio","bluetooth","terminal"]),"Mono : seulement les six docks demandés");
        check(layout.Blocks.Where(b=>b.Visible).All(b=>DesktopLayout.Screens[0].Contains(b.Bounds)),"Tous les docks mono tiennent sur le principal");
        check(mono!.ThemeId==settings.ThemeId&&mono.Glass==settings.GlassOpacity,"Le premier passage mono conserve l’apparence choisie");
        check(profiles.MatchDisplays(true,layout,settings) is null&&profiles.ReturnScene=="Bureau perso","Notifications répétées : aucune seconde bascule ni perte du retour");
        var committed=layout.Blocks.ToArray();
        bool rejected=false;try{profiles.Switch("Personnel",layout,settings);}catch(InvalidOperationException){rejected=true;}
        check(rejected&&layout.Blocks.SequenceEqual(committed)&&profiles.Current==DesktopProfiles.Mono,"Une scène du secondaire ne remplace pas le mono sur un seul écran");
        var drag=new LayoutGesture(layout.Blocks,"clock",LayoutEdge.Move,new Point(60,60),true,8,false,layout.AvailableScreens);
        check(drag.Preview(new Point(3000,60)).Blocks.Where(b=>b.Visible).All(b=>DesktopLayout.Screens[0].Contains(b.Bounds)),"Le déplacement ne sort pas vers le secondaire absent");
        layout.SetVisible("clock",false);
        check(layout.SetVisible("weather",true)&&DesktopLayout.Screens[0].Contains(layout["weather"].Bounds),"Ajouter un dock cherche sa place sur le principal");
        profiles.SaveCurrent(layout,settings);layout.Save();var customMono=layout.Blocks.ToArray();
        profiles=new DesktopProfiles(profilePath);layout=new DesktopLayout(layoutPath,11);
        check(profiles.MatchDisplays(true,layout,settings) is null&&layout.SingleScreen&&layout.Blocks.SequenceEqual(customMono)&&profiles.ReturnScene=="Bureau perso","Relancement en mono : modifications et scène de retour conservées");
        profiles.MatchDisplays(false,layout,settings);layout.Save();
        check(!layout.SingleScreen&&profiles.Current=="Bureau perso"&&profiles.ReturnScene is null&&layout.Blocks.SequenceEqual(dual),"Rebranchement : retour exact aux positions, tailles et visibilités précédentes");
        var after=JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(profilePath))!;
        check(before.Profiles.All(p=>JsonSerializer.Serialize(p.Value)==JsonSerializer.Serialize(after.Profiles[p.Key])),"La bascule conserve toutes les scènes existantes");
        profiles.MatchDisplays(true,layout,settings);layout.Save();
        check(layout.Blocks.SequenceEqual(customMono),"Le débranchement suivant retrouve le mono personnalisé");
        profiles=new DesktopProfiles(profilePath);layout=new DesktopLayout(layoutPath,11);
        profiles.MatchDisplays(false,layout,settings);
        check(profiles.Current=="Bureau perso"&&layout.Blocks.SequenceEqual(dual),"Secondaire rebranché pendant l’arrêt : retour exact au démarrage");
        profiles.Switch("Personnel",layout,settings);
        check(layout.Blocks.SequenceEqual(personal),"Personnel reste intact après le cycle complet");
        profiles.Switch(DesktopProfiles.Mono,layout,settings);
        check(layout.SingleScreen&&layout.Blocks.SequenceEqual(customMono),"La scène mono peut aussi être préparée avec deux écrans");
        profiles.ResetTemplate(layout,settings,11);
        check(layout.Blocks.Where(b=>b.Visible).Count()==6&&layout["clock"].Visible&&!layout["weather"].Visible,"Rétablir le modèle mono restaure les six docks sans toucher aux autres scènes");
    }
}
