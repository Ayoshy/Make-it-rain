namespace Battlestation;

// Keep the activity tests independent from the WPF station and native backends.
internal sealed record DockApp(string Name, string Path);
