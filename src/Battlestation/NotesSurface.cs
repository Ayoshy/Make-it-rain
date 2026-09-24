using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Battlestation;

internal sealed class NotesSurface : Surface, IDisposable
{
    readonly NotesFile file;
    readonly DispatcherTimer saveTimer;
    bool loaded, dirty, disposed;
    int revision;
    string? error;
    internal TextBox Editor { get; } = new()
    {
        AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Background = Brushes.Transparent, BorderThickness = new Thickness(0),
        Margin = new Thickness(22, 48, 18, 18), Padding = new Thickness(2),
        FontFamily = DockAppearance.UiFont, FontSize = 16,
        IsReadOnly = true, IsUndoEnabled = true
    };

    internal NotesSurface(Station station) : base(station, 19)
    {
        Width = 480; Height = 320;
        file = new NotesFile(Path.Combine(station.Data, "notes.txt"));
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        Editor.TextChanged += (_, _) =>
        {
            if (!loaded) return;
            dirty = true; revision++;
            saveTimer.Stop(); saveTimer.Start();
        };
        Editor.LostKeyboardFocus += (_, _) => Save();
        System.Windows.Automation.AutomationProperties.SetName(Editor, "Bloc-notes");
        DesktopTheme.Changed += ApplyTheme;
        ApplyTheme();
        _ = LoadAsync();
    }

    void ApplyTheme()
    {
        Editor.Foreground = B(Ink); Editor.CaretBrush = B(Ink);
        Editor.SelectionBrush = B("#806F9FCE");
    }

    async Task LoadAsync()
    {
        try
        {
            var text = await file.ReadAsync();
            if (disposed) return;
            Editor.IsUndoEnabled = false; Editor.Text = text; Editor.IsUndoEnabled = true;
            loaded = true; Editor.IsReadOnly = false; error = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { error = "Lecture impossible · Réessayer"; Editor.ToolTip = e.Message; }
        if (!disposed) Refresh();
    }

    async void Save()
    {
        saveTimer.Stop();
        if (!loaded || !dirty || disposed) return;
        dirty = false;
        int savingRevision = revision;
        try
        {
            await file.SaveAsync(Editor.Text);
            if (savingRevision == revision) { error = null; Editor.ToolTip = null; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            dirty = true;
            error = "Non enregistré · Réessayer"; Editor.ToolTip = e.Message;
        }
        if (!disposed) Refresh();
    }

    protected override void Paint()
    {
        Header("BLOC-NOTES", 16);
        if (error is not null)
        {
            Text(error, Width - 22, 19, 9, "#FFC98F", align: "right");
            Hit("NotesRetry", 150, 10, Math.Max(0, Width - 166), 30,
                () => { if (loaded) Save(); else _ = LoadAsync(); });
        }
    }

    internal Task PendingSave => file.Pending;
    public void Dispose()
    {
        saveTimer.Stop(); DesktopTheme.Changed -= ApplyTheme;
        if (loaded && dirty) { dirty = false; _ = file.SaveAsync(Editor.Text); }
        disposed = true;
    }
}
