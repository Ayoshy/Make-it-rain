global using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;

internal static class NotesTests
{
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }

    [STAThread] static int Main()
    {
        int result = 0;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "Battlestation-notes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "notes.txt");
                var file = new NotesFile(path);
                Check(await file.ReadAsync() == "", "Premier démarrage vide");
                const string text = "À faire\r\nUne deuxième ligne\r\n\tAccents, espaces  et 🎮";
                await file.SaveAsync(text);
                Check(await new NotesFile(path).ReadAsync() == text, "Texte Unicode et espaces conservés après relecture");
                var saves = Enumerable.Range(0, 30).Select(i => file.SaveAsync("Texte " + i)).ToArray();
                await Task.WhenAll(saves);
                Check(await file.ReadAsync() == "Texte 29", "La dernière frappe gagne sur les écritures précédentes");
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try { await file.SaveAsync("Ne doit pas remplacer"); throw new Exception("Écriture attendue en échec"); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                    Check(File.ReadAllText(path) == "Texte 29", "Une erreur d’écriture conserve le fichier précédent");
                }
                await file.SaveAsync(text);
                Check(await file.ReadAsync() == text, "Une sauvegarde peut reprendre après un échec");

                var surface = new NotesSurface(new Station(directory));
                for (int i = 0; i < 100 && surface.Editor.IsReadOnly; i++) await Task.Delay(10);
                Check(!surface.Editor.IsReadOnly && surface.Editor.Text == text, "Le dock charge la note avant d’autoriser la saisie");
                surface.Editor.Text = text + "\r\nDernière frappe avant fermeture";
                surface.Dispose();
                await surface.PendingSave;
                Check(await file.ReadAsync() == surface.Editor.Text, "Fermer avant le délai d’autosauvegarde conserve la dernière frappe");

                var reopened = new NotesSurface(new Station(directory));
                for (int i = 0; i < 100 && reopened.Editor.IsReadOnly; i++) await Task.Delay(10);
                reopened.Editor.Clear();
                await Task.Delay(500); await reopened.PendingSave;
                Check(await file.ReadAsync() == "", "Effacer tout le texte est sauvegardé");
                reopened.Editor.Text = text;
                var grid = new Grid { Width = 480, Height = 320, Background = new SolidColorBrush(Color.FromRgb(27, 24, 37)) };
                grid.Children.Add(reopened); grid.Children.Add(reopened.Editor);
                grid.Measure(new Size(480, 320)); grid.Arrange(new Rect(0, 0, 480, 320)); grid.UpdateLayout();
                var bitmap = new RenderTargetBitmap(480, 320, 96, 96, PixelFormats.Pbgra32); bitmap.Render(grid);
                string output = Path.GetFullPath("artifacts/validation/notes-preview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(output)) encoder.Save(stream);
                Check(reopened.Editor.ActualHeight > 200, "La zone de texte occupe le dock");
                reopened.Width = grid.Width = 320; reopened.Height = grid.Height = 180;
                grid.Measure(new Size(320, 180)); grid.Arrange(new Rect(0, 0, 320, 180)); grid.UpdateLayout();
                Check(reopened.Editor.ActualWidth > 260 && reopened.Editor.ActualHeight > 100, "La saisie reste utilisable à la taille minimale");
                reopened.Dispose(); await reopened.PendingSave;
                Console.WriteLine("NOTES_CHECKS_PASS");
            }
            catch (Exception e) { Console.Error.WriteLine(e); result = 1; }
            finally { Directory.Delete(directory, true); app.Shutdown(); }
        };
        app.Run(); return result;
    }
}

namespace Battlestation
{
    // Isolated UI tests never initialize desktop, hardware or terminal services.
    internal sealed class Station(string directory)
    { internal string Data => directory; internal string Root => directory; }
    internal static class DesktopScreens
    { internal static Rect ToPixels(Rect rect) => rect; }
    internal static class Native
    { internal static void BackgroundPanel(int slot, float x, float y, float w, float h) { } }
}
