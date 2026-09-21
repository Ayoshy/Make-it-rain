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
                Check(migrated.Version==5&&migrated.Profiles.All(p=>p.Key==old.Current?p.Value.ThemeId==settings.ThemeId:p.Value.ThemeId==(old.Version<2?"vice-city":old.Profiles[p.Key].ThemeId)),"Migration réelle : format 5 et ambiances conservées");
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
                var redesigned=profiles.Switch(name,layout,settings);settings=Settings(settings,redesigned);
                Check(layout.Blocks.All(block=>DesktopLayout.Valid(block,layout.Blocks)),name+" : disposition valide sur les deux écrans");
                Check(layout["network"].Visible!=(redesigned.TemplateId is "Jeu" or DesktopProfiles.Mono),name+" : réseau selon le modèle");
                foreach(string required in new[]{"terminal","projects","video"})Check(layout[required].Visible,name+" : "+required+" visible");
                Check(!layout["aquarium"].Visible&&!layout["ocean"].Visible,name+" : aquarium et océan masqués");
                Check(layout["lol"].Visible==(redesigned.TemplateId=="Jeu"),name+" : LoL seulement en Jeu");
                Check(layout["dualsense"].Visible==(redesigned.TemplateId is "Jeu" or "Double écran"),name+" : DualSense en Jeu et Double écran");
                Check(layout.Blocks.Where(b=>b.Visible).All(b=>b.Width>=DesktopLayout.Minimum(b.Id).Width&&b.Height>=DesktopLayout.Minimum(b.Id).Height),name+" : tailles minimales respectées");
            }
            var reloaded=new DesktopProfiles(Path.Combine(temp,"profiles.json"));
            Check(!reloaded.NeedsRedesign,"La refonte ne se répète pas au relancement");
            // Les deux nouveaux docks arrivent masqués et seuls Jeu et Cinéma les intègrent.
            var models=DesktopLayout.Defaults(11);
            Check(models.Single(b=>b.Id=="aquarium") is{Visible:false,Width:700,Height:420}&&models.Single(b=>b.Id=="lol") is{Visible:false,Width:700,Height:220},"Aquarium et LoL arrivent masqués avec leur taille par défaut");
            Check(DesktopLayout.Minimum("aquarium")==new Size(360,240)&&DesktopLayout.Minimum("lol")==new Size(440,180),"Les minimums de l'Aquarium et de LoL sont déclarés");
            Check(models.Single(b=>b.Id=="ocean") is{Visible:false,Width:720,Height:480},"Le Diorama Océan arrive masqué avec sa taille par défaut");
            Check(DesktopLayout.Minimum("ocean")==new Size(420,300),"Le minimum du Diorama Océan est déclaré");
            foreach(string name in DesktopProfiles.Names)
            {
                var preset=DesktopProfiles.Preset(name,models);
                Check(preset.Single(b=>b.Id=="aquarium") is{Visible:false}&&preset.Single(b=>b.Id=="ocean") is{Visible:false},name+" ne place ni Aquarium ni Diorama Océan");
                Check(preset.Single(b=>b.Id=="lol").Visible==(name=="Jeu"),name+" ne montre LoL qu'en Jeu");
                Check(preset.Single(b=>b.Id=="dualsense").Visible==(name is "Jeu" or "Double écran"),name+" ne montre la DualSense qu'en Jeu et Double écran");
                foreach(string required in new[]{"terminal","projects","video"})Check(preset.Single(b=>b.Id==required).Visible,name+" réserve "+required);
            }
            var committedLayout=layout.Blocks.ToArray();
            Check(layout.Restore(committedLayout.Where(b=>b.Id is not ("aquarium" or "lol")).ToArray())&&!layout["aquarium"].Visible&&!layout["lol"].Visible,"Une scène existante retrouve les nouveaux blocs masqués");
            Check(layout.Restore(committedLayout.Where(b=>b.Id!="ocean").ToArray())&&!layout["ocean"].Visible,"Une scène existante retrouve le Diorama Océan masqué");
            layout.Restore(committedLayout);
            SingleScreenTests.Run(temp,Check);
            SceneTransitionTests.Run(Check);
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
            static void Toggle(SettingsWindow window,string label){var box=Tree(window).OfType<CheckBox>().Single(b=>(b.Content as string)==label);box.IsChecked=!(box.IsChecked==true);box.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));}
            static Slider Value(SettingsWindow window,string name)=>Tree(window).OfType<Slider>().Single(s=>System.Windows.Automation.AutomationProperties.GetName(s)==name);
            string savedScene=File.ReadAllText(Path.Combine(temp,"settings-profiles.json"));var window=SettingsWindow();Click(window,"Apparence");
            Check(!Tree(window).OfType<Button>().Any(b=>b.Content as string is "Vice City" or "Obsidienne" or "Aurore"),"Réglages réels : le sélecteur de thème a disparu");
            double glass=20;Value(window,"Transparence du verre").Value=glass;Toggle(window,"Animer le fond du bureau");Toggle(window,"Faire réagir le fond au son");
            Check(state.PreviewReactiveAudio is false&&state.Settings.ReactiveAudio&&state.Settings.GlassOpacity!=1-glass/100&&File.ReadAllText(Path.Combine(temp,"settings-profiles.json"))==savedScene,"Réglages réels : aperçu immédiat sans sauvegarde");
            window.Close();Check(state.PreviewReactiveAudio is null&&state.PreviewAudioIntensity is null&&state.Settings.GlassOpacity!=1-glass/100,"Réglages réels : fermeture annule l’aperçu");
            window=SettingsWindow();Click(window,"Apparence");glass=30;Value(window,"Transparence du verre").Value=glass;Toggle(window,"Animer le fond du bureau");Toggle(window,"Faire réagir le fond au son");
            state.ApplySettings(state.Settings with{DualSenseTouchTrail=true},state.TargetDate);Click(window,"Enregistrer");
            Check(state.Settings.DualSenseTouchTrail,"Enregistrer l’apparence conserve le choix tactile global");
            Check(state.Settings.GlassOpacity==1-glass/100&&!state.Settings.AnimateBackground&&!state.Settings.ReactiveAudio&&state.Settings.ThemeId=="vice-city","Enregistrer conserve transparence, animation et réactivité musicale");
            window.Close();
            var cinema=profiles.Switch("Cinéma",layout,state.Settings);
            Check(cinema.ThemeId=="obsidienne"&&cinema.Blocks.Single(b=>b.Id=="hardware").Visible&&!cinema.Blocks.Single(b=>b.Id=="usage").Visible,"Cinéma : Matériel seul sur l’écran 1, Codex masqué");
            state.ApplySettings(Settings(state.Settings,cinema),state.TargetDate);
            Check(DesktopTheme.Current.Id=="obsidienne","Le changement de scène applique l’ambiance de la scène");
            Console.WriteLine(checks+" checks passed");return 0;
        }
        finally{Directory.Delete(temp,true);}
    }
}
