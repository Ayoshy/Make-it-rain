using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;

internal static class ScenesTests
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static DesktopSettings Settings(DesktopSettings s,DesktopProfile p)=>s with{AnimateBackground=p.Animate,ReactiveAudio=p.Reactive,AudioIntensity=p.Intensity,GlassOpacity=p.Glass,ThemeId=p.ThemeId};
    [STAThread] static int Main(string[] args)
    {
        string temp=Path.Combine(Path.GetTempPath(),"Battlestation-scenes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var settings=new DesktopSettings(temp,"Aix",43.5,5.4);
            var layout=new DesktopLayout(Path.Combine(temp,"layout.json"),11);
            var profiles=new DesktopProfiles(Path.Combine(temp,"profiles.json"));
            profiles.SaveCurrent(layout,settings);var personnel=layout.Blocks.ToArray();
            foreach(string name in DesktopProfiles.Presets)
            {
                settings=Settings(settings,profiles.Switch(name,layout,settings));
                layout.SetVisible("clock",false);settings=settings with{ThemeId="aurore",GlassOpacity=.33,AnimateBackground=false,ReactiveAudio=false,AudioIntensity=.25};
                profiles.SaveCurrent(layout,settings);var modified=layout.Blocks.ToArray();
                settings=Settings(settings,profiles.Switch("Personnel",layout,settings));
                Check(layout.Blocks.SequenceEqual(personnel),"Personnel intact après "+name);
                profiles=new DesktopProfiles(Path.Combine(temp,"profiles.json"));
                var returned=profiles.Switch(name,layout,settings);settings=Settings(settings,returned);
                Check(layout.Blocks.SequenceEqual(modified)&&settings.ThemeId=="aurore"&&settings.GlassOpacity==.33&&!settings.AnimateBackground&&!settings.ReactiveAudio&&settings.AudioIntensity==.25,name+" retrouve disposition, thème et réglages après relecture");
            }
            var origin=profiles.Current;var originBlocks=layout.Blocks.ToArray();profiles.SaveUser("Travail perso",layout,settings);layout.SetVisible("music",false);profiles.SaveCurrent(layout,settings);
            profiles.Switch(origin,layout,settings);Check(layout.Blocks.SequenceEqual(originBlocks),"Sauver sous conserve sa scène d’origine");
            profiles.Switch("Travail perso",layout,settings);var reset=profiles.ResetTemplate(layout,settings,11);
            Check(reset.TemplateId==origin&&reset.ThemeId=="aurore"&&layout["music"].Visible,"Rétablir le modèle utilise le modèle de la scène copiée");
            profiles.Switch("Personnel",layout,settings);Check(layout.Blocks.SequenceEqual(personnel),"Rétablir un modèle conserve Personnel");
            string before=File.ReadAllText(Path.Combine(temp,"profiles.json"));var committed=layout.Blocks.ToArray();layout.SetVisible("clock",false);
            Check(File.ReadAllText(Path.Combine(temp,"profiles.json"))==before,"Un geste intermédiaire ne sauvegarde pas la scène");layout.Restore(committed);
            Check(layout.Blocks.SequenceEqual(personnel),"Annulation du geste restaure le plan enregistré");
            if(args.Length>0)
            {
                string source=Path.GetFullPath(args[0]);File.Copy(Path.Combine(source,"profiles.json"),Path.Combine(temp,"profiles.json"),true);File.Copy(Path.Combine(source,"layout.json"),Path.Combine(temp,"layout.json"),true);
                var old=JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(Path.Combine(temp,"profiles.json")))!;
                layout=new DesktopLayout(Path.Combine(temp,"layout.json"),11);var live=layout.Blocks.ToArray();settings=DesktopSettings.Load(Path.Combine(source,"preferences.json"),settings);
                profiles=new DesktopProfiles(Path.Combine(temp,"profiles.json"));profiles.SaveCurrent(layout,settings);
                var migrated=JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(Path.Combine(temp,"profiles.json")))!;
                Check(profiles.Current==old.Current&&layout.Blocks.SequenceEqual(live)&&migrated.Profiles[old.Current].Blocks.SequenceEqual(live),"Migration réelle : la scène live reste prioritaire");
                Check(migrated.Version==4&&migrated.Profiles.All(p=>p.Key==old.Current?p.Value.ThemeId==settings.ThemeId:p.Value.ThemeId==(old.Version<2?"vice-city":old.Profiles[p.Key].ThemeId)),"Migration réelle : Vice City pour toutes les scènes");
                foreach(var (name,profile) in old.Profiles.Where(p=>p.Key!=old.Current))
                {
                    var compared=name=="Jeu"?profile.Blocks.Where(b=>b.Id!="countdown"&&b.Id!="dualsense"):profile.Blocks;
                    Check(compared.All(b=>migrated.Profiles[name].Blocks.Single(n=>n.Id==b.Id)==b),"Migration conserve le profil "+name+" hors ajout Jeu demandé");
                }
                var returnTo=profiles.Current;settings=Settings(settings,profiles.Switch("Personnel",layout,settings));
                Check(migrated.Profiles["Personnel"].Blocks.All(b=>layout[b.Id]==b)&&(migrated.Profiles["Personnel"].Blocks.Any(b=>b.Id=="dualsense")||!layout["dualsense"].Visible),"Migration réelle : retour intégral à Personnel");
                profiles.Switch(returnTo,layout,settings);Check(layout.Blocks.SequenceEqual(live),"Migration réelle : retour intégral à la scène active");
            }
            var namesBefore=profiles.AllNames.ToArray();string activeBefore=profiles.Current;
            profiles.RedesignAll(layout,settings,11);
            Check(profiles.Current==activeBefore&&profiles.AllNames.SequenceEqual(namesBefore),"Refonte complète conserve les noms et ne crée pas de copies des anciens agencements");
            foreach(string name in profiles.AllNames){
                var redesigned=profiles.Switch(name,layout,settings);
                Check(layout.Blocks.All(block=>DesktopLayout.Valid(block,layout.Blocks)),name+" : disposition valide sur les deux écrans");
                if(name!=DesktopProfiles.Mono)Check(layout["network"].Visible,name+" : réseau intégré");
                Check(layout.Blocks.Where(b=>b.Visible).All(b=>b.Width>=DesktopLayout.Minimum(b.Id).Width&&b.Height>=DesktopLayout.Minimum(b.Id).Height),name+" : tailles minimales respectées");
            }
            var reloaded=new DesktopProfiles(Path.Combine(temp,"profiles.json"));
            Check(!reloaded.NeedsRedesign,"La refonte ne se répète pas au relancement");
            SingleScreenTests.Run(temp,Check);
            var app=new Application();app.Resources.MergedDictionaries.Add(new GlassMenus());
            var brush=DesktopTheme.Brush("#DAD2E7");var error=DesktopTheme.Brush("#F4B7CA");var originalError=error.Color;
            Check(!brush.CanFreeze,"Les styles ne figent pas la palette partagée");
            foreach(var theme in DesktopTheme.Definitions)
            {
                DesktopTheme.Select(theme.Id,false);Check(brush.Color==DesktopTheme.Color("#DAD2E7"),theme.Name+" actualise les brosses existantes");
                Check(error.Color==originalError,theme.Name+" conserve la couleur d’erreur");
                var menu=new ContextMenu();menu.Items.Add(new MenuItem{Header="Scènes"});menu.ApplyTemplate();
                var border=new Border{Width=260,Height=90,Background=DockAppearance.ButtonFill,BorderBrush=brush,BorderThickness=new Thickness(2),Child=new TextBlock{Text=theme.Name,Foreground=brush,FontSize=24,Margin=new Thickness(20)}};
                border.Measure(new Size(260,90));border.Arrange(new Rect(0,0,260,90));border.UpdateLayout();
                var bitmap=new RenderTargetBitmap(260,90,96,96,PixelFormats.Pbgra32);bitmap.Render(border);
                Check(bitmap.PixelWidth==260,theme.Name+" rendu WPF des ressources partagées");
            }
            DesktopTheme.Select("vice-city",false);Check(brush.Color==(Color)ColorConverter.ConvertFromString("#DAD2E7"),"Annulation : couleurs Vice City exactes");
            var state=new Station(new DesktopSettings(temp,"Aix",43.5,5.4),layout);
            var sceneFile=new DesktopProfiles(Path.Combine(temp,"settings-profiles.json"));sceneFile.SaveCurrent(layout,state.Settings);state.Saved=()=>sceneFile.SaveCurrent(layout,state.Settings);
            SettingsWindow SettingsWindow()=>new(state,new PaletteHotkey(),(_,_)=>true,()=>{},_=>{},_=>{},()=>{},_=>{},()=>{},()=>[],"Personnel");
            static IEnumerable<DependencyObject> Tree(DependencyObject item){yield return item;foreach(var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>())foreach(var descendant in Tree(child))yield return descendant;}
            static void Click(SettingsWindow window,string label)=>Tree(window).OfType<Button>().Single(b=>b.Content as string==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            string savedScene=File.ReadAllText(Path.Combine(temp,"settings-profiles.json"));var window=SettingsWindow();Click(window,"Apparence");Click(window,"Aurore");
            Check(DesktopTheme.Current.Id=="aurore"&&state.Settings.ThemeId=="vice-city"&&File.ReadAllText(Path.Combine(temp,"settings-profiles.json"))==savedScene,"Réglages réels : aperçu immédiat sans sauvegarde");
            window.Close();Check(DesktopTheme.Current.Id=="vice-city","Réglages réels : fermeture annule l’aperçu");
            window=SettingsWindow();Click(window,"Apparence");Click(window,"Obsidienne");state.ApplySettings(state.Settings with{DualSenseTouchTrail=true},state.TargetDate);Click(window,"Enregistrer");
            Check(state.Settings.DualSenseTouchTrail,"Enregistrer l’apparence conserve le choix tactile global");
            Check(state.Settings.ThemeId=="obsidienne"&&JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(Path.Combine(temp,"settings-profiles.json")))!.Profiles["Personnel"].ThemeId=="obsidienne","Réglages réels : Enregistrer conserve le thème dans la scène");
            Click(window,"Aurore");window.Close();Check(DesktopTheme.Current.Id=="obsidienne","Après Enregistrer, annuler revient au dernier choix enregistré");
            Console.WriteLine(checks+" checks passed");return 0;
        }
        finally{Directory.Delete(temp,true);}
    }
}
