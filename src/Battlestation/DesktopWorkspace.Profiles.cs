using System.IO;
using System.Windows;

namespace Battlestation;
internal sealed partial class DesktopWorkspace
{
    DesktopProfiles profiles=null!;
    IReadOnlyList<SceneDock> Docks()=>windows.Select(pair=>new SceneDock(pair.Value,pair.Value.IsVisible)).ToArray();
    // La refonte de format 6 remplace une fois les agencements enregistrés ; la copie
    // horodatée garde l'état précédent. Un échec disque laisse le bureau démarrer et
    // la refonte retentera au prochain lancement.
    void RedesignScenesOnce()
    {
        if(!profiles.NeedsRedesign)return;
        try
        {
            string backup=Path.Combine(station.Data,"scene-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(backup);
            foreach(string name in new[]{"profiles.json","layout.json","preferences.json"}){
                string file=Path.Combine(station.Data,name);
                if(File.Exists(file))File.Copy(file,Path.Combine(backup,name));
            }
            var next=profiles.RedesignAll(station.Layout,station.Settings,station.Apps.Count);
            station.ApplyAppearance(next);ApplyAll();station.Layout.Save();
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
    }
    // A scene is applied while the desktop is dissolved: the request only states the
    // destination, and a choice made during the exit replaces the previous one.
    void SelectProfile(string name)=>scene.Request(()=>SwitchScene(name));
    void SwitchScene(string name)
    {
        try
        {
            ClearEditHistory();
            settingsWindow?.Close();
            var next=profiles.Switch(name,station.Layout,station.Settings);
            station.ApplyAppearance(next);SetEditing(false);ApplyAll();station.Layout.Save();
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
        {MessageBox.Show(e.Message,"Scène",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void SaveUserProfile()
    {
        var owner=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive);
        var name=ProfileNameDialog.Show(owner,"Sauver la scène");if(string.IsNullOrWhiteSpace(name))return;
        ClearEditHistory();
        try{profiles.SaveUser(name,station.Layout,station.Settings);SetEditing(false);}
        catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Scène",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void ResetSceneTemplate()=>scene.Request(ResetSceneTemplateNow);
    void ResetSceneTemplateNow()
    {
        settingsWindow?.Close();palette?.Dismiss(false);
        try{ClearEditHistory();var next=profiles.ResetTemplate(station.Layout,station.Settings,station.Apps.Count);station.ApplyAppearance(next);SetEditing(false);ApplyAll();station.Layout.Save();}
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Scène",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void DeleteUserProfile(string name)=>scene.Request(()=>DeleteUserProfileNow(name));
    void DeleteUserProfileNow(string name)
    {
        try{if(profiles.Current==name)SwitchScene("Bureau");if(profiles.Current!=name)profiles.DeleteUser(name);}
        catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Scène",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
}
