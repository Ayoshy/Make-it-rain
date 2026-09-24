using System.IO;

namespace Battlestation;

internal sealed class NotesFile(string path)
{
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal Task<string> ReadAsync() => Task.Run(() => File.Exists(path) ? File.ReadAllText(path) : "");

    // Serialize snapshots so an older write can never replace newer text.
    // DesktopSettings.Write replaces the file only after the temporary file is complete.
    internal Task SaveAsync(string text) => Pending = Pending.ContinueWith(
        _ => DesktopSettings.Write(path, text), CancellationToken.None,
        TaskContinuationOptions.None, TaskScheduler.Default);
}
