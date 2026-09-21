using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Battlestation;
internal sealed partial class DesktopWorkspace
{
    DispatcherTimer? displayChange;
    int connectedScreens=2,connectedMask=3;
    string? displayError;
    void ReadDisplays()
    {
        string previous=DesktopScreens.Signature;
        var screens=DesktopScreens.Read();
        connectedScreens=screens.Count;
        connectedMask=connectedScreens==1?1:3;
        // The arrangement was written for another monitor set: fit it into this
        // one before the docks are placed.
        station.Layout.SetScreens(screens.Select(screen=>screen.Dip).ToArray(),true);
        if(DesktopScreens.Signature==previous)return;
        var canvas=DesktopScreens.Canvas();
        Native.BackgroundCanvas(canvas.Left,canvas.Top,canvas.Width,canvas.Height,canvas.Seam);
    }
    void DisplaySettingsChanged(object? sender,EventArgs args)
    {
        app.Dispatcher.BeginInvoke(()=>{
            if(disposed)return;
            displayChange??=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
            displayChange.Tick-=ApplyDisplayChange;displayChange.Tick+=ApplyDisplayChange;
            displayChange.Stop();displayChange.Start();
        });
    }
    void ApplyDisplayChange(object? sender,EventArgs args)
    {
        displayChange?.Stop();
        if(disposed)return;
        scene.Request(ApplyDisplayChangeNow);
    }
    void ApplyDisplayChangeNow()
    {
        // Cancel an unfinished drag or appearance preview before snapshotting the old scene.
        ClearEditHistory();settingsWindow?.Close();palette?.Dismiss(false);
        SetEditing(false);ReadDisplays();
        try
        {
            if(profiles.MatchDisplays(connectedScreens==1,station.Layout,station.Settings) is {} next)
                station.ApplyAppearance(next);
            station.Layout.Save();displayError=null;
        }
        catch(Exception e) when(e is InvalidOperationException or ArgumentException or System.IO.IOException or UnauthorizedAccessException)
        {
            displayError=e.Message;
            tray.ShowBalloonTip(5000,"Disposition des écrans",e.Message,Forms.ToolTipIcon.Warning);
        }
        // Windows may have repositioned a dock while removing a monitor.
        ApplyAll();UpdateEditGrids();UpdateVisibility();
    }
}
