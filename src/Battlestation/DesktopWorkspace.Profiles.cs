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
}
