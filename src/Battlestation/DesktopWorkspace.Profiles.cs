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
            var next=profiles.Switch(name,station.Layout,station.Settings);
            station.ApplyAppearance(next);SetEditing(false);ApplyAll();station.Layout.Save();
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
        {MessageBox.Show(e.Message,"Disposition",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void SaveUserProfile()
    {
        var owner=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive);
        var name=ProfileNameDialog.Show(owner,"Sauver la disposition");if(string.IsNullOrWhiteSpace(name))return;
        try{profiles.SaveUser(name,station.Layout,station.Settings);SetEditing(false);}
        catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Disposition",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void DeleteUserProfile(string name)
    {
        try{profiles.DeleteUser(name);if(profiles.Current=="Personnel")SelectProfile("Personnel");}
        catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException){MessageBox.Show(e.Message,"Disposition",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
}
