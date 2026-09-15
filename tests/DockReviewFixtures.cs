using System.Windows;
using System.Windows.Controls;
namespace Battlestation;

// Render fixtures: no backend, terminal, save files or GPU worker is started.
internal sealed record DockApp(string Name,string Path);
internal sealed record ReminderItem(string Text);
internal sealed class Station
{
    public string Root {get;set;}="";
    public List<DockApp> Apps {get;set;}=[];
    public event Action? DockChanged;
    public List<ReminderItem> Reminders {get;set;}=[];
    public DesktopLayout Layout {get;}=new();
    public void SaveReminders(IEnumerable<ReminderItem> reminders)=>Reminders=reminders.ToList();
    public void SaveApps(List<DockApp> apps){Apps=apps;DockChanged?.Invoke();}
    public void Launch(DockApp app)=>throw new InvalidOperationException("No application launch during render tests");
}
internal sealed class DesktopLayout
{
    public static double DockHeight(int apps)=>Math.Max(1,Math.Ceiling(apps/6d))*88+28;
    public void Save()=>throw new InvalidOperationException("No disk writes during render tests");
}
internal static class Native
{
    internal static readonly Dictionary<int,Rect> Panels=[];
    internal static void BackgroundPanel(int slot,float x,float y,float w,float h)=>Panels[slot]=new Rect(x,y,w,h);
}
internal static class OverlayStyle
{
    public static Button Button(string text,Action action)=>new(){Content=text};
    public static TextBlock Text(string text,double size)=>new(){Text=text,FontSize=size};
    public static Border Frame(UIElement child)=>new(){Child=child};
    public static void Apply(Window window)=>throw new InvalidOperationException("No modal during render tests");
    public static void Place(Window window)=>throw new InvalidOperationException("No modal during render tests");
}
