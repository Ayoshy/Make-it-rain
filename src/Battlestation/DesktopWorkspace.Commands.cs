using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Battlestation;
internal sealed partial class DesktopWorkspace
{
    PaletteHotkey paletteHotkey=null!;
    CommandPaletteWindow? palette;
    SettingsWindow? settingsWindow;
    void InitializeCommands()
    {
        paletteHotkey=new PaletteHotkey(TogglePalette);
        station.SettingsChanged+=()=>{foreach(var surface in surfaces.Values)surface.Refresh();};
    }
    bool ChangeVisibility(string id,bool visible)
    {
        if(!station.Layout.SetVisible(id,visible))return false;
        Apply(id);station.Layout.Save();if(id=="music"&&!visible)((DeskSurface)surfaces[id]).Audio.Stop();return true;
    }
    void ShowSettings()
    {
        palette?.Dismiss(false);
        if(settingsWindow is not null){if(settingsWindow.WindowState==WindowState.Minimized)settingsWindow.WindowState=WindowState.Normal;settingsWindow.Activate();return;}
        settingsWindow=new SettingsWindow(station,paletteHotkey,ChangeVisibility,()=>SetEditing(true),owner=>((DockSurface)surfaces["apps"]).OpenEditor(owner));
        settingsWindow.Closed+=(_,_)=>settingsWindow=null;
        OverlayStyle.Reveal(settingsWindow,station.Settings.AnimateBackground);
    }
    void TogglePalette()
    {
        if(palette is not null){palette.Dismiss(true);return;}
        var entries=new List<PaletteEntry>();
        foreach(var app in station.Apps)entries.Add(new("app:"+app.Path,app.Name,"Application","\uE71D",()=>{station.Launch(app);if(station.Error!="")throw new InvalidOperationException(station.Error);}));
        entries.Add(new("settings","Réglages","Paramètres · settings · transparence · météo","\uE713",ShowSettings,true));
        entries.Add(new("organize",editing?"Terminer la réorganisation":"Réorganiser le bureau","Déplacer les blocs sur la grille","\uE8A9",()=>SetEditing(!editing),true));
        entries.Add(new("terminal","Ouvrir le terminal","Retrouver les sessions actives","\uE756",()=>{if(!ChangeVisibility("terminal",true))throw new InvalidOperationException("Pas assez d’espace libre pour le terminal.");SetEditing(false);station.Terminal!.Start();},true));
        foreach(var block in station.Layout.Blocks)
        {
            bool show=!block.Visible;entries.Add(new("block:"+block.Id,(show?"Afficher ":"Masquer ")+block.Title,"Bloc du bureau","\uE8A9",()=>{if(!ChangeVisibility(block.Id,show))throw new InvalidOperationException("Pas assez d’espace libre pour ce bloc.");},true));
        }
        var window=new CommandPaletteWindow(entries);palette=window;
        window.Closed+=(_,_)=>{if(palette==window)palette=null;};
        PaletteEntry Project(string path)=>new("project:"+path,Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),"Projet","\uE8B7",()=>window.ProjectActions(Path.GetFileName(path),
            ()=>{if(!Directory.Exists(path))throw new DirectoryNotFoundException("Ce projet a été déplacé ou supprimé.");Process.Start(new ProcessStartInfo(path){UseShellExecute=true})?.Dispose();},
            ()=>{if(!Directory.Exists(path))throw new DirectoryNotFoundException("Ce projet a été déplacé ou supprimé.");if(!ChangeVisibility("terminal",true))throw new InvalidOperationException("Pas assez d’espace libre pour le terminal.");SetEditing(false);station.Terminal!.OpenCodex(path);}));
        var recent=Enumerable.Range(0,6).Select(i=>Native.Read($"project:{i}:path")).Where(p=>!string.IsNullOrWhiteSpace(p)).Select(Project).ToArray();window.AddProjects(recent);
        OverlayStyle.Reveal(window,station.Settings.AnimateBackground);
        // Index only directory names; no file contents, credentials or terminal output.
        string root=station.ProjectRoot;
        _=AddProjectDirectories();
        async Task AddProjectDirectories()
        {
            string[] paths=await Task.Run(()=>{try{return Directory.EnumerateDirectories(root).Where(p=>!Path.GetFileName(p).StartsWith('.')&&!Path.GetFileName(p).EndsWith("_stable",StringComparison.OrdinalIgnoreCase)).OrderBy(Path.GetFileName,StringComparer.CurrentCultureIgnoreCase).Take(500).ToArray();}catch(Exception e) when(e is IOException or UnauthorizedAccessException){return [];}});
            if(!disposed&&palette==window)window.AddProjects(paths.Select(Project));
        }
    }
}
