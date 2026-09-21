using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Battlestation;

static class TerminalCacheTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Equal(object? expected, object? actual, string message) => Check(Equals(expected, actual), message + " (attendu " + expected + ", obtenu " + actual + ")");
    static readonly Dispatcher Ui = StartUi();
    static Dispatcher StartUi()
    {
        var ready = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() => { var dispatcher = Dispatcher.CurrentDispatcher; ready.SetResult(dispatcher); Dispatcher.Run(); }) { IsBackground = true, Name = "Terminal cache test UI" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return ready.Task.GetAwaiter().GetResult();
    }
    [STAThread] static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "battlestation-terminal-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            DisplayRule(root);
            DeepSeekHitRate();
            AccentPrecedence(root);
            RolloutSelection(root);
            CommandLines();
            TabRendering();
            Console.WriteLine("PASS : regle d'affichage, taux de hit DS, priorite de couleur, lecture des rollouts, rendu des onglets.");
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    // 1. Regle d'affichage sur horloge fixe.
    static void DisplayRule(string root)
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(2));
        var fresh = PromptCache.FromActivity(now, now.AddMinutes(-1));
        Equal(PromptCache.Hourglass + " 29 min", fresh.Badge, "1 min ecoulee : 29 min restantes");
        Check(fresh.Hint == TerminalCacheHint.Fresh, "plus de 20 min restantes : teinte verte");
        var aging = PromptCache.FromActivity(now, now.AddMinutes(-15));
        Equal(PromptCache.Hourglass + " 15 min", aging.Badge, "15 min ecoulees : 15 min restantes");
        Check(aging.Hint == TerminalCacheHint.Aging, "10 a 20 min restantes : teinte ambre");
        var expiring = PromptCache.FromActivity(now, now.AddMinutes(-27));
        Equal(PromptCache.Hourglass + " 3 min", expiring.Badge, "27 min ecoulees : 3 min restantes");
        Check(expiring.Hint == TerminalCacheHint.Expiring, "moins de 10 min restantes : teinte rouge");
        var partial = PromptCache.FromRemaining(TimeSpan.FromSeconds(20));
        Equal(PromptCache.Hourglass + " 1 min", partial.Badge, "la precision a la minute n'affiche jamais zero");
        Check(partial.Hint == TerminalCacheHint.Expiring, "une minute restante reste rouge");
        var expired = PromptCache.FromActivity(now, now.AddMinutes(-31));
        Equal(PromptCache.Expired, expired.Badge, "31 min ecoulees : le cache est expire");
        Check(expired.Hint == TerminalCacheHint.Expired, "cache expire : teinte grise");
        Check(PromptCache.FromRemaining(TimeSpan.Zero).Hint == TerminalCacheHint.Expired, "un restant nul est expire");
        Check(PromptCache.FromRemaining(TimeSpan.FromMinutes(20)).Hint == TerminalCacheHint.Aging, "20 min restantes restent ambre");
        Check(PromptCache.FromRemaining(TimeSpan.FromMinutes(10)).Hint == TerminalCacheHint.Aging, "10 min restantes restent ambre");
        Check(PromptCache.FromRemaining(TimeSpan.FromMinutes(20.1)).Hint == TerminalCacheHint.Fresh, "au dela de 20 min la teinte repasse au vert");
        Check(TerminalCacheState.None.Badge is null && TerminalCacheState.None.Hint == TerminalCacheHint.None, "sans activite, aucun badge n'est produit");
    }

    // 2. Taux de hit du cache DeepSeek.
    static void DeepSeekHitRate()
    {
        Equal("cache 78 %", PromptCache.Hit(78, 100).Badge, "78 jetons en cache sur 100 donnent 78 %");
        Equal("cache 33,3 %", PromptCache.Hit(1, 3).Badge, "un tiers s'affiche au dixieme");
        Equal("cache 66,6 %", PromptCache.Hit(2, 3).Badge, "deux tiers ne sont pas surestimes");
        Equal("cache 82,2 %", PromptCache.Hit(8224, 10000).Badge, "un tour froid reste lisible");
        Equal("cache 100 %", PromptCache.Hit(150, 100).Badge, "un cache superieur a l'entree reste borne a 100 %");
        Equal("cache 0 %", PromptCache.Hit(-4, 10).Badge, "un compteur negatif ne produit pas de pourcentage negatif");
        Check(PromptCache.Hit(0, 0).Badge is null, "une entree nulle ne divise pas par zero");
        Check(PromptCache.Hit(5, 0).Badge is null, "des jetons caches sans entree ne produisent aucun taux");
        var badge = PromptCache.Hit(78, 100);
        Check(badge.Hint == TerminalCacheHint.None, "le taux de hit ne teinte pas l'onglet");
        Check(!PromptCache.Hit(0, 0).HasBadge, "sans compteur, l'onglet DS reste nu");
    }

    // 3. La couleur choisie reste prioritaire sur la teinte automatique.
    static void AccentPrecedence(string root)
    {
        Equal("#ED9EC8", TerminalTabs.EffectiveAccent("#ED9EC8", TerminalCacheHint.Expired), "une couleur choisie n'est pas remplacee par la teinte");
        Equal("#7FD6A6", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Fresh), "sans couleur choisie, la teinte verte s'applique");
        Equal("#E9BE81", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Aging), "sans couleur choisie, la teinte ambre s'applique");
        Equal("#EB9B9B", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Expiring), "sans couleur choisie, la teinte rouge s'applique");
        Equal("#948CA0", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Expired), "le cache expire glisse vers le gris");
        Check(TerminalTabs.EffectiveAccent(null, TerminalCacheHint.None) is null, "sans badge ni couleur, l'onglet garde le verre commun");
        var preferences = new TerminalTabPreferences(Path.Combine(root, "terminal-tabs.json"));
        var id = Guid.NewGuid();
        preferences.Set(id, new TerminalTabPreference(Color: "#ED9EC8"));
        var metadata = new ConsoleTitleInfo(1, "Battlestation | Working | fil | projet", true);
        var decorated = preferences.Decorate(new TerminalTabInfo(id, "Battlestation", true), metadata, new(PromptCache.Hourglass + " 3 min", TerminalCacheHint.Expiring));
        Equal("#ED9EC8", decorated.Accent, "la preference de couleur survit a la decoration");
        Equal(PromptCache.Hourglass + " 3 min", decorated.Badge, "le compte a rebours s'ajoute au titre de l'onglet");
        Check(decorated.CacheHint == TerminalCacheHint.Expiring, "le niveau de teinte accompagne le badge");
        var plain = preferences.Decorate(new TerminalTabInfo(Guid.NewGuid(), "PowerShell 2", false), new ConsoleTitleInfo(2, "PowerShell 2", false));
        Check(plain.Badge is null && plain.CacheHint == TerminalCacheHint.None, "un onglet sans activite Codex reste sans badge");
    }

    // 4. Lecture du rollout le plus recent par projet, sans contenu de message.
    static void RolloutSelection(string root)
    {
        var sessions = Path.Combine(root, "sessions");
        var early = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2));
        Session(sessions, "2026/09/20", "rollout-kash.jsonl", @"C:\projets\Kash", "openai", early);
        Session(sessions, "2026/09/21", "rollout-ds.jsonl", @"C:\projets\Battlestation", "deepseek", early.AddHours(2));
        var openai = Session(sessions, "2026/09/21", "rollout-openai.jsonl", @"C:\projets\Battlestation", "openai", early.AddHours(3));
        var reader = new CodexRolloutReader(sessions);
        var codex = Guid.NewGuid(); var deepSeek = Guid.NewGuid(); var missing = Guid.NewGuid(); var kash = Guid.NewGuid();
        var byName = Guid.NewGuid();
        var resolved = reader.Resolve([new(codex, @"C:\projets\Battlestation", false), new(deepSeek, @"C:\projets\Battlestation", true),
            new(missing, @"C:\projets\Absent", false), new(kash, @"C:\projets\Kash", false), new(byName, "Battlestation", null)]);
        Check(resolved.ContainsKey(codex) && Path.GetFileName(resolved[codex].Path) == "rollout-openai.jsonl", "l'onglet Codex lit le rollout ChatGPT le plus recent du projet");
        Equal(early.AddHours(3), resolved[codex].Last, "l'horodatage lu est celui du dernier evenement");
        Check(resolved.ContainsKey(deepSeek) && Path.GetFileName(resolved[deepSeek].Path) == "rollout-ds.jsonl", "l'onglet Codex (DS) lit sa propre variante");
        Check(!resolved.ContainsKey(missing), "un projet sans rollout ne produit aucun badge");
        Check(resolved.ContainsKey(kash) && Path.GetFileName(resolved[kash].Path) == "rollout-kash.jsonl", "chaque projet garde son propre rollout");
        Check(resolved[codex].Path != resolved[deepSeek].Path && !resolved[codex].Provider.Equals("deepseek", StringComparison.OrdinalIgnoreCase) && resolved[deepSeek].Provider == "deepseek", "deux onglets du meme projet gardent chacun leur variante");
        Equal(100L, resolved[deepSeek].InputTokens, "les jetons d'entree de la session sont lus dans le rollout");
        Equal(78L, resolved[deepSeek].CachedInputTokens, "les jetons en cache de la session sont lus dans le rollout");
        Equal("cache 78 %", PromptCache.Hit(resolved[deepSeek].CachedInputTokens, resolved[deepSeek].InputTokens).Badge, "le taux de hit porte sur la session de l'onglet");
        var quiet = Guid.NewGuid();
        Session(sessions, "2026/09/21", "rollout-quiet.jsonl", @"C:\projets\Quiet", "deepseek", early.AddHours(7), false);
        var without = new CodexRolloutReader(sessions).Resolve([new(quiet, @"C:\projets\Quiet", true)]);
        Check(without.ContainsKey(quiet) && without[quiet].InputTokens == 0, "une session sans compteur reste sans taux");
        Check(!PromptCache.Hit(without[quiet].CachedInputTokens, without[quiet].InputTokens).HasBadge, "sans compteur de session, l'onglet DS reste nu");
        // Le dernier tour decrit ce qui vient de se passer, pas la moyenne du jour.
        var turn = Guid.NewGuid();
        var turnPath = Session(sessions, "2026/09/21", "rollout-turn.jsonl", @"C:\projets\Turn", "deepseek", early.AddHours(8));
        File.AppendAllText(turnPath, Usage(early.AddHours(8).AddMinutes(1), 100, 78, 5, 10, 4) + "\n", new UTF8Encoding(false));
        var turnOnly = new CodexRolloutReader(sessions).Resolve([new(turn, @"C:\projets\Turn", true)]);
        Equal(10L, turnOnly[turn].InputTokens, "le taux lit le dernier tour de la session");
        Equal("cache 40 %", PromptCache.Hit(turnOnly[turn].CachedInputTokens, turnOnly[turn].InputTokens).Badge, "un tour froid fait descendre le badge");
        Check(resolved.ContainsKey(byName) && Path.GetFileName(resolved[byName].Path) == "rollout-openai.jsonl", "un nom de projet suffit quand le CLI est lance depuis un onglet nu");
        var solo = Guid.NewGuid();
        Session(sessions, "2026/09/21", "rollout-solo.jsonl", @"C:\projets\Solo", "deepseek", early.AddHours(5));
        var deep = new CodexRolloutReader(sessions).Resolve([new(solo, "Solo", null)]);
        Check(deep.ContainsKey(solo) && deep[solo].Provider == "deepseek", "sans variante connue, le fournisseur du rollout le plus recent decide");
        // La date de modification d'une session en cours peut retarder sur son contenu.
        var shift = Guid.NewGuid();
        var active = Session(sessions, "2026/09/21", "rollout-actif.jsonl", @"C:\projets\Shift", "openai", early.AddHours(6));
        var closed = Session(sessions, "2026/09/21", "rollout-clos.jsonl", @"C:\projets\Shift", "deepseek", early.AddHours(4));
        File.SetLastWriteTimeUtc(active, early.AddHours(1).UtcDateTime);
        File.SetLastWriteTimeUtc(closed, early.AddHours(5).UtcDateTime);
        var latest = new CodexRolloutReader(sessions).Resolve([new(shift, "Shift", null)]);
        Check(latest.ContainsKey(shift) && Path.GetFileName(latest[shift].Path) == "rollout-actif.jsonl", "le dernier evenement l'emporte sur une date de modification retardee");
        // Une ligne de sortie volumineuse ne doit pas masquer le dernier evenement.
        var long1 = Session(sessions, "2026/09/21", "rollout-long.jsonl", @"C:\projets\Long", "openai", early);
        File.AppendAllText(long1, Event(early.AddHours(1), new string('x', 300 * 1024)) + "\n", new UTF8Encoding(false));
        File.AppendAllText(long1, Event(early.AddHours(4), "dernier") + "\n", new UTF8Encoding(false));
        var longTab = Guid.NewGuid();
        var again = new CodexRolloutReader(sessions).Resolve([new RolloutQuery(longTab, @"C:\projets\Long", false)]);
        Check(again.ContainsKey(longTab), "un rollout a ligne volumineuse reste lisible");
        Check(again[longTab].Last >= early.AddHours(4), "l'horodatage final est retrouve malgre la ligne volumineuse");
        Equal(0, reader.Resolve([]).Count, "aucune requete ne produit aucun resultat");
        Equal(0, reader.Resolve([new RolloutQuery(Guid.NewGuid(), "   ", false)]).Count, "un nom de projet vide ne produit aucun resultat");
    }
    static string Session(string root, string day, string name, string cwd, string provider, DateTimeOffset stamp, bool counters = true)
    {
        var directory = Path.Combine(root, day.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        var line = "{\"timestamp\":" + JsonSerializer.Serialize(stamp.ToString("o")) + ",\"ordinal\":0,\"type\":\"session_meta\",\"payload\":{\"id\":\"" + Guid.NewGuid().ToString("N") + "\",\"cwd\":" + JsonSerializer.Serialize(cwd) + ",\"model_provider\":\"" + provider + "\"}}";
        File.WriteAllText(path, line + "\n" + Event(stamp, "session") + "\n" + (counters ? Usage(stamp, 100, 78, 5) + "\n" : ""), new UTF8Encoding(false));
        // Le fichier le plus recemment modifie est celui de la session la plus recente.
        File.SetLastWriteTimeUtc(path, stamp.UtcDateTime);
        return path;
    }
    static string Event(DateTimeOffset stamp, string text)
        => "{\"timestamp\":" + JsonSerializer.Serialize(stamp.ToString("o")) + ",\"type\":\"event_msg\",\"payload\":{\"text\":" + JsonSerializer.Serialize(text) + "}}";
    static string Usage(DateTimeOffset stamp, long input, long cached, long output, long? turnInput = null, long? turnCached = null)
        => "{\"timestamp\":" + JsonSerializer.Serialize(stamp.ToString("o")) + ",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{"
            + (turnInput is { } recent ? "\"last_token_usage\":{\"input_tokens\":" + recent + ",\"cached_input_tokens\":" + (turnCached ?? 0) + ",\"output_tokens\":1,\"total_tokens\":" + (recent + 1) + "}," : "")
            + "\"total_token_usage\":{\"input_tokens\":" + input + ",\"cached_input_tokens\":" + cached + ",\"output_tokens\":" + output + ",\"total_tokens\":" + (input + output) + "}}}}";

    // 4 bis. Variante et projet lus sur la ligne de commande du CLI.
    static void CommandLines()
    {
        const string deepSeek = "\"C:\\Codex\\codex.exe\" -c tui.animations=false -c model_provider=\"deepseek\" -c model=\"deepseek-flash\" -c model_providers.deepseek.base_url=\"https://api.deepseek.com/\" -C C:\\projets\\Battlestation";
        var variant = CodexCommand.Parse(deepSeek);
        Check(variant.DeepSeek, "model_provider=deepseek est reconnu comme la variante DeepSeek");
        Equal(@"C:\projets\Battlestation", variant.Project, "l'argument -C donne le projet suivi");
        var chatgpt = CodexCommand.Parse("\"C:\\Codex\\codex.exe\" -c tui.animations=false -c tui.terminal_title=['run-state','activity'] -C \"C:\\projets\\Battlestation 2\"");
        Check(!chatgpt.DeepSeek, "un CLI sans model_provider n'est pas la variante DeepSeek");
        Equal(@"C:\projets\Battlestation 2", chatgpt.Project, "un chemin entre guillemets reste entier");
        var url = CodexCommand.Parse("\"C:\\Codex\\codex.exe\" -c model_providers.deepseek.base_url=\"https://api.deepseek.com/\" --cd C:\\projets\\Kash");
        Check(!url.DeepSeek, "une URL DeepSeek n'est pas la variante DeepSeek");
        Equal(@"C:\projets\Kash", url.Project, "la forme longue --cd est acceptee");
        var bare = CodexCommand.Parse("\"C:\\Codex\\codex.exe\"");
        Check(!bare.DeepSeek && bare.Project is null, "une ligne de commande sans option ne dit rien");
        Check(!CodexCommand.Parse(null).DeepSeek && CodexCommand.Parse("").Project is null, "une ligne de commande absente ne dit rien");
    }

    // 5. Rendu reel de la barre d'onglets : badge present, teinte automatique,
    // couleur choisie intacte.
    static void TabRendering()
    {
        var plain = new TerminalTabInfo(Guid.NewGuid(), "PowerShell 2", false);
        var codex = new TerminalTabInfo(Guid.NewGuid(), "Battlestation", true, Activity: TerminalActivity.Ready);
        var manual = new TerminalTabInfo(Guid.NewGuid(), "Battlestation", true, Accent: "#ED9EC8", Activity: TerminalActivity.Ready);
        var badged = Render([plain, codex with { Badge = PromptCache.Hourglass + " 12 min", CacheHint = TerminalCacheHint.Aging }]);
        var bare = Render([plain, codex]);
        Check(!Same(badged, bare), "le badge de cache change le rendu de l'onglet Codex");
        Check(Same(badged, bare, 110), "l'onglet PowerShell nu ne porte aucun badge");
        var fresh = Render([codex with { CacheHint = TerminalCacheHint.Fresh }]);
        var expired = Render([codex with { CacheHint = TerminalCacheHint.Expired }]);
        Check(!Same(fresh, expired), "la teinte automatique suit le restant du cache");
        var manualFresh = Render([manual with { CacheHint = TerminalCacheHint.Fresh }]);
        var manualExpired = Render([manual with { CacheHint = TerminalCacheHint.Expired }]);
        Check(Same(manualFresh, manualExpired), "une couleur choisie n'est pas teintee par le cache");
        // Un titre long ne doit pas repousser le compte a rebours hors de l'onglet.
        var long1 = new TerminalTabInfo(Guid.NewGuid(), "Implémenter l'indicateur de cache | Battlestation", true, Activity: TerminalActivity.Ready);
        var longBadged = Render([long1 with { Badge = PromptCache.Hourglass + " 12 min", CacheHint = TerminalCacheHint.Aging }]);
        Check(Tinted(longBadged, 233, 190, 129) > Tinted(Render([long1 with { CacheHint = TerminalCacheHint.Aging }]), 233, 190, 129), "le texte du compte a rebours reste peint malgre un titre long");
        Check(Tinted(Render([long1]), 233, 190, 129) < 10, "sans badge, la teinte ambre n'apparait pas sur l'onglet");
    }
    static int Tinted((byte[] Pixels, int Width, int Height) render, byte red, byte green, byte blue)
    {
        int count = 0;
        // Pbgra32 : bleu, vert, rouge, alpha.
        for (int index = 0; index + 3 < render.Pixels.Length; index += 4)
            if (Math.Abs(render.Pixels[index] - blue) <= 12 && Math.Abs(render.Pixels[index + 1] - green) <= 12 && Math.Abs(render.Pixels[index + 2] - red) <= 12) count++;
        return count;
    }
    static (byte[] Pixels, int Width, int Height) Render(TerminalTabInfo[] tabs) => Ui.Invoke(() =>
    {
        var strip = new TerminalTabs(); strip.SetTabs(tabs);
        var host = new Grid { Width = 900, Height = 48, Background = new SolidColorBrush(Color.FromRgb(30, 20, 44)) };
        host.Children.Add(strip); host.Measure(new Size(900, 48)); host.Arrange(new Rect(0, 0, 900, 48)); host.UpdateLayout();
        var image = new RenderTargetBitmap(900, 48, 96, 96, PixelFormats.Pbgra32); image.Render(host);
        var pixels = new byte[900 * 48 * 4]; image.CopyPixels(pixels, 900 * 4, 0);
        return (pixels, 900, 48);
    });
    static bool Same((byte[] Pixels, int Width, int Height) left, (byte[] Pixels, int Width, int Height) right, int? columns = null)
    {
        int width = columns ?? left.Width;
        for (int row = 0; row < left.Height; row++)
            for (int column = 0; column < width; column++)
                for (int channel = 0; channel < 4; channel++)
                    if (left.Pixels[(row * left.Width + column) * 4 + channel] != right.Pixels[(row * right.Width + column) * 4 + channel]) return false;
        return true;
    }
}
