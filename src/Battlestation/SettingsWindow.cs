using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Battlestation;
internal sealed class SettingsWindow : Window
{
    readonly Station station;
    readonly TextBox projectRoot,city,latitude,longitude,time;
    readonly DatePicker date;
    readonly Slider transparency;
    readonly CheckBox animation;
    readonly CheckBox reactiveAudio;
    readonly Slider audioIntensity;
    readonly CheckBox keepMediaLinks;
    readonly CheckBox gridEnabled;
    readonly CheckBox linkDocks;
    readonly TextBox gridStep;
    readonly TextBlock feedback=OverlayStyle.Text("",12,"#F4B7CA");
    readonly TextBlock hotkeyStatus=OverlayStyle.Text("",12,"#BCAACD");
    readonly PaletteHotkey hotkey;
    bool ready;
    internal SettingsWindow(Station state,PaletteHotkey shortcut,Func<string,bool,bool> visibility,Action organize,Action<Window> editApps,Action<string> selectProfile,string currentProfile)
    {
        station=state;hotkey=shortcut;Title="Battlestation · Réglages";Width=760;Height=660;ShowInTaskbar=true;OverlayStyle.Apply(this);
        var root=new DockPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var close=OverlayStyle.Button("×",Close);close.ToolTip="Fermer";DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);var title=OverlayStyle.Text("Réglages",25);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);
        header.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is TextBlock){DragMove();e.Handled=true;}};
        var footer=new DockPanel{Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var save=OverlayStyle.Button("Enregistrer",Save);DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);var cancel=OverlayStyle.Button("Fermer",Close);DockPanel.SetDock(cancel,Dock.Right);footer.Children.Add(cancel);feedback.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(feedback);
        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(150)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});root.Children.Add(body);
        var navigation=new StackPanel{Margin=new Thickness(0,0,18,0)};body.Children.Add(navigation);
        var pageHost=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(3,0,12,0)};Grid.SetColumn(pageHost,1);body.Children.Add(pageHost);
        var pages=new List<(Button Button,StackPanel Page)>();
        StackPanel Page(string name)
        {
            var panel=new StackPanel();Button? button=null;button=OverlayStyle.Button(name,()=>{pageHost.Content=panel;foreach(var p in pages)p.Button.Opacity=p.Button==button?1:.55;feedback.Text="";});button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,0,0,7);navigation.Children.Add(button);pages.Add((button,panel));return panel;
        }
        static void Heading(Panel panel,string title){var text=OverlayStyle.Text(title,19);text.Margin=new Thickness(0,2,0,14);panel.Children.Add(text);}
        static TextBox Field(Panel panel,string label,string value){var text=OverlayStyle.Text(label,12,"#BEADCF");text.Margin=new Thickness(0,12,0,5);panel.Children.Add(text);var field=new TextBox{Text=value};panel.Children.Add(field);System.Windows.Automation.AutomationProperties.SetName(field,label);return field;}
        var desktop=Page("Bureau");Heading(desktop,"Blocs visibles");
        foreach(var block in state.Layout.Blocks)
        {
            var check=new CheckBox{Content=block.Title,IsChecked=block.Visible};bool updating=false;
            check.Click+=(_,_)=>{if(updating)return;if(!visibility(block.Id,check.IsChecked==true)){updating=true;check.IsChecked=state.Layout[block.Id].Visible;updating=false;feedback.Text="Pas assez d’espace libre pour ce bloc.";}};desktop.Children.Add(check);
        }
        desktop.Children.Add(OverlayStyle.Button("Réorganiser les blocs",()=>{Close();organize();}));
        Heading(desktop,"Grille");
        gridEnabled=new CheckBox{Content="Accrocher à la grille",IsChecked=state.Settings.GridEnabled};desktop.Children.Add(gridEnabled);
        gridStep=Field(desktop,"Pas de grille · 4 à 64",state.Settings.GridStep.ToString(CultureInfo.InvariantCulture));
        linkDocks=new CheckBox{Content="Lier les docks · redimensionnement partagé et poussée",IsChecked=state.Settings.LinkDocks,Margin=new Thickness(0,14,0,8)};desktop.Children.Add(linkDocks);
        Heading(desktop,"Disposition · "+currentProfile);var modes=new WrapPanel();desktop.Children.Add(modes);
        foreach(string name in DesktopProfiles.Names)modes.Children.Add(OverlayStyle.Button(name,()=>{Close();selectProfile(name);}));
        var look=Page("Apparence");Heading(look,"Verre et mouvement");look.Children.Add(OverlayStyle.Text("Transparence des panneaux",14));
        var percentage=OverlayStyle.Text("",12,"#BCAACD");transparency=new Slider{Minimum=15,Maximum=95,Value=(1-state.Settings.GlassOpacity)*100,TickFrequency=5,IsSnapToTickEnabled=true,Margin=new Thickness(0,14,0,5)};look.Children.Add(transparency);look.Children.Add(percentage);
        animation=new CheckBox{Content="Animer le fond du bureau",IsChecked=state.Settings.AnimateBackground,Margin=new Thickness(0,25,0,0)};look.Children.Add(animation);
        look.Children.Add(OverlayStyle.Text("Aperçu immédiat · Enregistrer pour conserver",11,"#BCAACD"));
        void Preview(){percentage.Text=$"{transparency.Value:0} %";if(ready)Native.BackgroundAppearance(animation.IsChecked==true?1:0,(float)(1-transparency.Value/100));}
        transparency.ValueChanged+=(_,_)=>Preview();animation.Click+=(_,_)=>Preview();Preview();
        reactiveAudio=new CheckBox{Content="Faire réagir le fond au son",IsChecked=state.Settings.ReactiveAudio,Margin=new Thickness(0,25,0,0)};look.Children.Add(reactiveAudio);
        audioIntensity=new Slider{Minimum=0,Maximum=100,Value=state.Settings.AudioIntensity*100,TickFrequency=5,IsSnapToTickEnabled=true,Margin=new Thickness(0,12,0,5)};look.Children.Add(audioIntensity);
        var audioPercent=OverlayStyle.Text($"Intensité · {audioIntensity.Value:0} %",12,"#BCAACD");look.Children.Add(audioPercent);
        System.Windows.Automation.AutomationProperties.SetName(audioIntensity,"Intensité musicale");
        void PreviewAudio(){station.PreviewReactiveAudio=reactiveAudio.IsChecked==true;station.PreviewAudioIntensity=audioIntensity.Value/100;audioPercent.Text=$"Intensité · {audioIntensity.Value:0} %";}
        reactiveAudio.Click+=(_,_)=>PreviewAudio();audioIntensity.ValueChanged+=(_,_)=>PreviewAudio();
        var apps=Page("Applications");Heading(apps,"Applications du dock");apps.Children.Add(OverlayStyle.Text("Ajouter, retirer et réordonner les raccourcis.",13,"#BCAACD"));apps.Children.Add(OverlayStyle.Button("Modifier les applications",()=>editApps(this)));
        var projects=Page("Projets");Heading(projects,"Dossier des projets");projectRoot=Field(projects,"Dossier",state.ProjectRoot);projects.Children.Add(OverlayStyle.Button("Parcourir…",()=>{var dialog=new OpenFolderDialog{Title="Dossier contenant les projets",InitialDirectory=projectRoot.Text};if(dialog.ShowDialog(this)==true)projectRoot.Text=dialog.FolderName;}));
        var weather=Page("Météo");Heading(weather,"Lieu");city=Field(weather,"Ville",state.Settings.WeatherCity);latitude=Field(weather,"Latitude",state.Settings.Latitude.ToString(CultureInfo.CurrentCulture));longitude=Field(weather,"Longitude",state.Settings.Longitude.ToString(CultureInfo.CurrentCulture));
        var countdown=Page("Compteur");Heading(countdown,"Vice City");countdown.Children.Add(OverlayStyle.Text("Date choisie pour ton compteur",13,"#BCAACD"));
        bool parsed=DateTimeOffset.TryParse(state.TargetDate,CultureInfo.InvariantCulture,DateTimeStyles.None,out var target);
        date=new DatePicker{SelectedDate=parsed?target.LocalDateTime.Date:null,Margin=new Thickness(0,14,0,6)};countdown.Children.Add(date);time=Field(countdown,"Heure locale",parsed?target.LocalDateTime.ToString("HH:mm"):"00:00");
        var shortcuts=Page("Raccourci");Heading(shortcuts,"Palette de commandes");shortcuts.Children.Add(OverlayStyle.Text("Ctrl + Espace",22));hotkeyStatus.Margin=new Thickness(0,14,0,14);shortcuts.Children.Add(hotkeyStatus);shortcuts.Children.Add(OverlayStyle.Button("Réessayer le raccourci",()=>{hotkey.Retry();UpdateHotkey();}));UpdateHotkey();
        var media=Page("Liens médias");Heading(media,"À regarder, à écouter");
        keepMediaLinks=new CheckBox{Content="Garder les liens YouTube et Spotify copiés",IsChecked=state.Settings.KeepMediaLinks};media.Children.Add(keepMediaLinks);
        media.Children.Add(OverlayStyle.Text("100 liens maximum · sauvegarde locale",12,"#BCAACD"));
        media.Children.Add(OverlayStyle.Text(state.ClipboardRegistered?"Détection disponible":"Détection indisponible dans cette session",12,"#BCAACD"));
        media.Children.Add(OverlayStyle.Button("Ouvrir la réserve",state.ShowReserve));
        pageHost.Content=desktop;foreach(var item in pages)item.Button.Opacity=item.Page==desktop?1:.55;
        Content=OverlayStyle.Frame(root);ready=true;
        Closed+=(_,_)=>{station.PreviewReactiveAudio=null;station.PreviewAudioIntensity=null;Native.BackgroundAppearance(station.Settings.AnimateBackground?1:0,(float)station.Settings.GlassOpacity);};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
    }
    void UpdateHotkey()=>hotkeyStatus.Text=hotkey.Registered?"Raccourci actif":"Ctrl + Espace est indisponible. Libère-le dans l’autre application, puis réessaie.";
    static double Number(string text)=>double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out var number)||double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out number)?number:double.NaN;
    void Save()
    {
        try
        {
            if(date.SelectedDate is not {} day||!TimeSpan.TryParseExact(time.Text.Trim(),["h\\:mm","hh\\:mm"],CultureInfo.InvariantCulture,out var hour)||hour.TotalHours>=24)throw new ArgumentException("Choisis une date et une heure au format HH:mm.");
            var local=DateTime.SpecifyKind(day.Date+hour,DateTimeKind.Unspecified);if(TimeZoneInfo.Local.IsInvalidTime(local))throw new ArgumentException("Cette heure n’existe pas lors du changement d’heure.");
            // Preserve the exact existing offset unless the user edited date/time.
            bool same=DateTimeOffset.TryParse(station.TargetDate,CultureInfo.InvariantCulture,DateTimeStyles.None,out var old)&&old.LocalDateTime.Date==day.Date&&old.LocalDateTime.ToString("HH:mm")==time.Text.Trim();
            string target=same?station.TargetDate:new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToString("O",CultureInfo.InvariantCulture);
            if(!int.TryParse(gridStep.Text,out int step)||step is <4 or >64)throw new ArgumentException("Le pas de grille doit être un entier entre 4 et 64.");
            station.ApplySettings(new DesktopSettings(projectRoot.Text,city.Text,Number(latitude.Text),Number(longitude.Text),animation.IsChecked==true,1-transparency.Value/100,reactiveAudio.IsChecked==true,audioIntensity.Value/100,keepMediaLinks.IsChecked==true,gridEnabled.IsChecked==true,step,linkDocks.IsChecked==true),target);feedback.Text="Réglages enregistrés";
        }
        catch(Exception e) when(e is ArgumentException or System.IO.IOException or UnauthorizedAccessException){feedback.Text=e.Message;}
    }
}
