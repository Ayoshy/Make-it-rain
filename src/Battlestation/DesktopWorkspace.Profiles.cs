using System.IO;
using System.Windows;

namespace Battlestation;
internal sealed partial class DesktopWorkspace
{
    DesktopProfiles profiles=null!;
    void SelectProfile(string name)
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
    void ResetSceneTemplate()
    {
        settingsWindow?.Close();palette?.Dismiss(false);
        try{ClearEditHistory();var next=profiles.ResetTemplate(station.Layout,station.Settings,station.Apps.Count);station.ApplyAppearance(next);SetEditing(false);ApplyAll();station.Layout.Save();}
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Scène",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void DeleteUserProfile(string name)
    {
        try{if(profiles.Current==name)SelectProfile("Personnel");profiles.DeleteUser(name);}
        catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Scène",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
}
