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
    [STAThread] static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--hold-rollouts")
        {
            var held = args.Skip(1).Select(p => new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)).ToArray();
            Console.WriteLine("ready"); Console.ReadLine(); foreach (var file in held) file.Dispose(); return;
        }
        var root = Path.Combine(Path.GetTempPath(), "battlestation-terminal-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            DisplayRule(root);
            DeepSeekHitRate();
            AccentPrecedence(root);
            ClaudeTabStatus(root);
            RolloutSelection(root);
            ProcessBinding(root);
            TabRendering();
            Console.WriteLine("PASS : regle d'affichage, taux de hit DS, priorite de couleur, lecture des rollouts, rendu des onglets.");
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    // 1. Regle d'affichage sur horloge fixe.
    static void DisplayRule(string root)
    {
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(2));
        var fresh = PromptCache.FromObservation(now, "gpt-6-astra", now.AddMinutes(-1));
        Equal(PromptCache.Hourglass + " ≈ 29 min", fresh.Badge, "1 min ecoulee : 29 min restantes");
        Check(fresh.Hint == TerminalCacheHint.Fresh, "plus de 20 min restantes : teinte verte");
        var aging = PromptCache.FromObservation(now, "gpt-6-astra", now.AddMinutes(-15));
        Equal(PromptCache.Hourglass + " ≈ 15 min", aging.Badge, "15 min ecoulees : 15 min restantes");
        Check(aging.Hint == TerminalCacheHint.Aging, "10 a 20 min restantes : teinte ambre");
        var expiring = PromptCache.FromObservation(now, "gpt-6-astra", now.AddMinutes(-27));
        Equal(PromptCache.Hourglass + " ≈ 3 min", expiring.Badge, "27 min ecoulees : 3 min restantes");
        Check(expiring.Hint == TerminalCacheHint.Expiring, "moins de 10 min restantes : teinte rouge");
        var partial = PromptCache.FromRemaining(TimeSpan.FromSeconds(20));
        Equal(PromptCache.Hourglass + " ≈ 1 min", partial.Badge, "la precision a la minute n'affiche jamais zero");
        Check(partial.Hint == TerminalCacheHint.Expiring, "une minute restante reste rouge");
        var expired = PromptCache.FromObservation(now, "gpt-6-astra", now.AddMinutes(-31));
        Equal(PromptCache.Uncertain, expired.Badge, "31 min ecoulees : le cache devient incertain");
        Check(expired.Hint == TerminalCacheHint.Uncertain, "cache incertain : teinte grise");
        Check(PromptCache.FromRemaining(TimeSpan.Zero).Hint == TerminalCacheHint.Uncertain, "un restant nul est incertain");
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
        Check(!PromptCache.Hit(150, 100).HasBadge, "un compteur incoherent ne devient pas 100 %");
        Check(!PromptCache.Hit(-4, 10).HasBadge, "un compteur negatif ne devient pas zero");
        Check(PromptCache.Hit(0, 0).Badge is null, "une entree nulle ne divise pas par zero");
        Check(PromptCache.Hit(5, 0).Badge is null, "des jetons caches sans entree ne produisent aucun taux");
        var badge = PromptCache.Hit(78, 100);
        Check(badge.Hint == TerminalCacheHint.None, "le taux de hit ne teinte pas l'onglet");
        Check(!PromptCache.Hit(0, 0).HasBadge, "sans compteur, l'onglet DS reste nu");
    }

    // 3. La couleur choisie reste prioritaire sur la teinte automatique.
    static void AccentPrecedence(string root)
    {
        Equal("#ED9EC8", TerminalTabs.EffectiveAccent("#ED9EC8", TerminalCacheHint.Uncertain), "une couleur choisie n'est pas remplacee par la teinte");
        Equal("#7FD6A6", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Fresh), "sans couleur choisie, la teinte verte s'applique");
        Equal("#E9BE81", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Aging), "sans couleur choisie, la teinte ambre s'applique");
        Equal("#EB9B9B", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Expiring), "sans couleur choisie, la teinte rouge s'applique");
        Equal("#948CA0", TerminalTabs.EffectiveAccent(null, TerminalCacheHint.Uncertain), "le cache incertain glisse vers le gris");
        Check(TerminalTabs.EffectiveAccent(null, TerminalCacheHint.None) is null, "sans badge ni couleur, l'onglet garde le verre commun");
        var preferences = new TerminalTabPreferences(Path.Combine(root, "terminal-tabs.json"));
        var id = Guid.NewGuid();
        preferences.Set(id, new TerminalTabPreference(Color: "#ED9EC8"));
        var metadata = new ConsoleTitleInfo(1, "Battlestation | Working | fil | projet", true);
        var decorated = preferences.Decorate(new TerminalTabInfo(id, "Battlestation", true), metadata, new(PromptCache.Hourglass + " ≈ 3 min", TerminalCacheHint.Expiring));
        Equal("#ED9EC8", decorated.Accent, "la preference de couleur survit a la decoration");
        Equal(PromptCache.Hourglass + " ≈ 3 min", decorated.Badge, "le compte a rebours s'ajoute au titre de l'onglet");
        Check(decorated.CacheHint == TerminalCacheHint.Expiring, "le niveau de teinte accompagne le badge");
        var plain = preferences.Decorate(new TerminalTabInfo(Guid.NewGuid(), "PowerShell 2", false), new ConsoleTitleInfo(2, "PowerShell 2", false));
        Check(plain.Badge is null && plain.CacheHint == TerminalCacheHint.None, "un onglet sans activite Codex reste sans badge");
    }

    // 4. Sessions Claude Code : titre et etat publies par le CLI.
    static void ClaudeTabStatus(string root)
    {
        var titled = TerminalTitle.Parse("✳ Video mirror dock performance", false, false, true);
        Equal("Video mirror dock performance", titled.Title, "le marqueur Claude quitte le titre");
        Check(titled.Activity == TerminalActivity.Unknown, "l'asterisque de Claude n'affirme pas un travail en cours");
        var working = TerminalTitle.Parse("⠐ Review the diff", false, false, true);
        Equal("Review the diff", working.Title, "une frame d'attente quitte le titre");
        Check(working.Activity == TerminalActivity.Working, "une frame d'attente prouve un travail en cours");
        Equal("Claude Code", TerminalTitle.Parse("✳ Claude Code", false, false, true).Title, "un titre sans nom de session reste lisible");
        Check(TerminalTitle.Parse("Battlestation | Working | fil", true).Activity == TerminalActivity.Working, "le lecteur Codex garde son propre format");

        static string Record(string status, string extra = "")
            => "{\"pid\":42,\"name\":\"battlestation-da\",\"status\":\"" + status + "\"" + extra + "}";
        var idle = ClaudeSessions.Parse(Record("idle"));
        Equal("battlestation-da", idle?.Name, "le nom de session Claude est repris");
        Check(idle?.Activity == TerminalActivity.Ready, "une session Claude au repos est prete");
        Check(ClaudeSessions.Parse(Record("busy"))?.Activity == TerminalActivity.Working, "une session occupee travaille");
        var waiting = ClaudeSessions.Parse(Record("waiting", ",\"waitingFor\":\"dialog open\""));
        Check(waiting?.Activity == TerminalActivity.Attention, "une attente demande l'attention");
        Equal("Attente : dialog open", waiting?.Detail, "l'attente en cours alimente l'infobulle");
        Check(ClaudeSessions.Parse(Record("paused"))?.Activity == TerminalActivity.Unknown, "un statut inconnu n'est pas devine");
        Check(ClaudeSessions.Parse("{pas du json") is null, "un registre illisible ne casse pas l'onglet");

        var directory = Path.Combine(root, "claude-sessions");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "42.json"), Record("busy"), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "43.json"), new string('x', ClaudeSessions.MaximumRecordBytes + 1), new UTF8Encoding(false));
        var records = ClaudeSessions.Read([42, 43, 44], directory);
        Check(records.ContainsKey(42) && records[42].Activity == TerminalActivity.Working, "le registre du processus est lu");
        Check(!records.ContainsKey(43) && !records.ContainsKey(44), "un fichier hors bornes ou absent est ignore");
        Equal(0, ClaudeSessions.Read([], directory).Count, "aucun processus Claude : aucune lecture");

        var preferences = new TerminalTabPreferences(Path.Combine(root, "terminal-tabs.json"));
        var metadata = new ConsoleTitleInfo(9, "✳ Video mirror dock performance", false, Claude: true, ClaudePid: 42);
        var decorated = preferences.Decorate(new TerminalTabInfo(Guid.NewGuid(), "PowerShell 3", true), metadata, default, records[42]);
        Equal("Video mirror dock performance", decorated.Title, "le titre Claude remplace le titre du shell");
        Check(decorated.Activity == TerminalActivity.Working, "l'icone d'onglet suit l'etat Claude");
        var orphan = preferences.Decorate(new TerminalTabInfo(Guid.NewGuid(), "PowerShell 3", true), metadata);
        Equal("Video mirror dock performance", orphan.Title, "sans registre, le titre Claude reste affiche");
        Check(orphan.Activity == TerminalActivity.Unknown, "sans registre, aucun etat n'est invente");
        var renamed = preferences.Decorate(new TerminalTabInfo(Guid.NewGuid(), "PowerShell 3", true),
            new ConsoleTitleInfo(9, "✳ Autre tache", false, Claude: true, ClaudePid: 42), default, records[42]);
        Equal("Autre tache", renamed.Title, "chaque session Claude porte son propre titre");
        var named = preferences.Decorate(new TerminalTabInfo(Guid.NewGuid(), "PowerShell 3", true),
            new ConsoleTitleInfo(9, "", false, Claude: true, ClaudePid: 42), default, records[42]);
        Equal("battlestation-da", named.Title, "sans titre de console, le registre nomme l'onglet");
    }

    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-22T12:00:00Z");
    static string Record(DateTimeOffset at, string type, object payload)
        => JsonSerializer.Serialize(new { timestamp = at.ToString("o"), type, payload });
    static string Usage(DateTimeOffset at, long input, long? cached, long totalInput, long? write = null)
    {
        var last = new Dictionary<string, object?> { ["input_tokens"] = input, ["output_tokens"] = 1, ["total_tokens"] = input + 1 };
        if (cached.HasValue) last["cached_input_tokens"] = cached.Value;
        if (write.HasValue) last["cache_write_input_tokens"] = write.Value;
        return Record(at, "event_msg", new { type = "token_count", info = new { last_token_usage = last,
            total_token_usage = new { input_tokens = totalInput, cached_input_tokens = cached ?? 0, output_tokens = totalInput / 100, total_tokens = totalInput + totalInput / 100 } } });
    }
    static string Fixture(string root, string name, string provider = "openai", bool subagent = false)
    {
        Directory.CreateDirectory(root); var file = Path.Combine(root, "rollout-" + name + ".jsonl");
        object source = subagent ? new { subagent = new { thread_spawn = new { parent_thread_id = Guid.NewGuid() } } } : "cli";
        File.WriteAllText(file, Record(Now.AddHours(-1), "session_meta", new { id = Guid.NewGuid(), cwd = @"C:\same-project", source, model_provider = provider }) + "\n", new UTF8Encoding(false));
        Append(file, Record(Now.AddHours(-1), "turn_context", new { model = "gpt-6-astra" }));
        return file;
    }
    static void Append(string path, string record) => File.AppendAllText(path, record + "\n", new UTF8Encoding(false));
    static void RolloutSelection(string root)
    {
        var sessions = Path.Combine(root, "sessions");
        var a = Fixture(sessions, "a", "deepseek"); var b = Fixture(sessions, "b", "deepseek");
        var child = Fixture(sessions, "child", subagent: true);
        Append(a, Usage(Now.AddMinutes(-10), 100, 20, 100)); Append(b, Usage(Now, 100, 90, 100));
        var paths = new Dictionary<int, string[]> { [1] = [a, child], [2] = [b] };
        var reader = new CodexRolloutReader(sessions, pid => paths.GetValueOrDefault(pid) ?? []);
        var tabA = Guid.NewGuid(); var tabB = Guid.NewGuid();
        RolloutActivity ReadA() => reader.Resolve([new(tabA, 1)])[tabA];
        var both = reader.Resolve([new(tabA, 1), new(tabB, 2)]);
        Equal("cache 20 %", both[tabA].State(Now).Badge, "premier onglet DS du meme projet : sa propre mesure");
        Equal("cache 90 %", both[tabB].State(Now).Badge, "second onglet DS du meme projet : sa propre mesure");
        Equal(a, both[tabA].Path, "les sous-agents ne remplacent pas la conversation principale");
        paths[1] = [a, b]; Check(!reader.Resolve([new(tabA, 1)]).ContainsKey(tabA), "deux conversations principales : aucun choix arbitraire");
        paths[1] = []; Check(!reader.Resolve([new(tabA, 1)]).ContainsKey(tabA), "association perdue : aucune mesure d'un autre onglet");
        paths[1] = [a];
        File.SetLastWriteTimeUtc(a, Now.AddDays(-10).UtcDateTime);
        for (int i = 0; i < 8; i++) Fixture(sessions, "unrelated-" + i);
        Equal(a, ReadA().Path, "une session ancienne reste liee malgre huit fichiers plus recents");
        var openai = Fixture(sessions, "openai"); paths[1] = [openai];
        Check(ReadA().State(Now).Hint == TerminalCacheHint.Uncertain, "session sans appel : jamais verte");
        Append(openai, Usage(Now.AddMinutes(-45), 100, 90, 100));
        Append(openai, Record(Now, "event_msg", new { type = "task_started" }));
        Check(ReadA().State(Now).Hint == TerminalCacheHint.Uncertain, "un evenement local ne rechauffe pas un ancien cache");
        Append(openai, Usage(Now.AddMinutes(-5), 100, 90, 200));
        Equal(Now.AddMinutes(-5), ReadA().CacheObservedAt, "une nouvelle reutilisation date l'observation");
        Append(openai, Usage(Now, 100, 90, 200));
        Equal(Now.AddMinutes(-5), ReadA().CacheObservedAt, "une notification dupliquee ne repousse pas l'estimation");
        Append(openai, Record(Now, "event_msg", new { type = "token_count", info = (object?)null }));
        Equal(Now.AddMinutes(-5), ReadA().CacheObservedAt, "info:null n'efface pas la mesure et ne bloque pas le lecteur");
        Append(openai, Record(Now, "compacted", new { }));
        Check(ReadA().CacheObservedAt is null, "une compaction invalide le prefixe");
        Append(openai, Usage(Now, 100, 90, 200));
        Check(ReadA().CacheObservedAt is null, "un doublon apres compaction ne revalide pas le prefixe");
        Append(openai, Usage(Now, 100, 80, 300));
        Check(ReadA().CacheObservedAt == Now, "une vraie mesure apres compaction restaure l'estimation");
        Append(openai, Record(Now, "turn_context", new { model = "gpt-5.6-sol" }));
        Check(ReadA().CacheObservedAt is null, "un changement de modele invalide le prefixe");
        Append(openai, Usage(Now, 100, 0, 400));
        Check(ReadA().CacheObservedAt is null, "un cache miss ne devient pas un cache chaud");
        Append(openai, Usage(Now, 100, 0, 500, 100));
        Check(ReadA().CacheObservedAt == Now, "une ecriture explicite constitue une observation");
        Append(openai, Usage(Now, 100, 90, 100));
        Check(ReadA().CacheObservedAt is null, "un compteur cumule remis a zero ne simule pas un nouvel appel");
        Append(openai, Usage(Now, 100, null, 200));
        Check(ReadA().CachedInputTokens is null && !PromptCache.Hit(ReadA().CachedInputTokens, ReadA().InputTokens).HasBadge, "compteur absent ne signifie pas zero pour cent");
        Append(openai, Usage(Now, 100, 0, 300));
        Equal("cache 0 %", PromptCache.Hit(ReadA().CachedInputTokens, ReadA().InputTokens).Badge, "un zero mesure reste un zero");
        var partial = Usage(Now, 100, 90, 400);
        File.AppendAllText(openai, partial[..(partial.Length / 2)], new UTF8Encoding(false));
        Check(ReadA().CacheObservedAt is null, "une ligne incomplete n'est pas appliquee");
        File.AppendAllText(openai, partial[(partial.Length / 2)..] + "\n", new UTF8Encoding(false));
        Equal(Now, ReadA().CacheObservedAt, "la suite d'une ligne incomplete est relue au prochain passage");
        Append(openai, Record(Now, "response_item", new { text = new string('x', 5 * 1024 * 1024) }));
        Append(openai, Usage(Now, 100, 90, 500));
        Equal(Now, ReadA().CacheObservedAt, "une ligne volumineuse ne masque pas les mesures suivantes");
        File.WriteAllText(openai, Record(Now, "session_meta", new { id = Guid.NewGuid(), source = "cli", model_provider = "openai" }) + "\n");
        Check(ReadA().CacheObservedAt is null, "un fichier tronque perd ses anciennes mesures");
        Check(PromptCache.FromObservation(Now, "modele-inconnu", Now).Hint == TerminalCacheHint.Uncertain, "aucune duree inventee pour un modele inconnu");
        Check(PromptCache.FromObservation(Now, "gpt-5.5", Now).Hint == TerminalCacheHint.Uncertain, "aucune garantie de trente minutes pour un ancien modele");
        Check(PromptCache.FromObservation(Now, "gpt-6-astra", Now.AddMinutes(5)).Hint == TerminalCacheHint.Uncertain, "un horodatage futur n'allonge pas la fenetre");
        Equal(0, reader.Resolve([]).Count, "aucun onglet : aucune mesure");
    }
    static void ProcessBinding(string root)
    {
        var dir = Path.Combine(root, "native");
        var main = Fixture(dir, "main"); var child = Fixture(dir, "child", subagent: true);
        var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true };
        info.ArgumentList.Add("--hold-rollouts"); info.ArgumentList.Add(main); info.ArgumentList.Add(child);
        using var process = System.Diagnostics.Process.Start(info)!;
        try
        {
            Equal("ready", process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(), "processus synthetique pret sans console interactive");
            var paths = TerminalRolloutPath.Find(process.Id, dir);
            Check(paths.Contains(main) && paths.Contains(child), "les handles du processus identifient ses journaux ouverts");
            var id = Guid.NewGuid(); var bound = new CodexRolloutReader(dir).Resolve([new(id, process.Id)]);
            Equal(main, bound[id].Path, "liaison native puis filtrage du sous-agent");
            Equal(0, TerminalRolloutPath.Find(process.Id, Path.Combine(dir, "other")).Length, "aucun fichier hors du dossier de sessions");
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(10000)) { process.Kill(); process.WaitForExit(); }
        }
        Equal(0, TerminalRolloutPath.Find(process.Id, dir).Length, "un processus termine ne garde aucune association");
    }
    // 5. Rendu reel de la barre d'onglets : badge present, teinte automatique,
    // couleur choisie intacte.
    static void TabRendering()
    {
        var plain = new TerminalTabInfo(Guid.NewGuid(), "PowerShell 2", false);
        var codex = new TerminalTabInfo(Guid.NewGuid(), "Battlestation", true, Activity: TerminalActivity.Ready);
        var manual = new TerminalTabInfo(Guid.NewGuid(), "Battlestation", true, Accent: "#ED9EC8", Activity: TerminalActivity.Ready);
        var badged = Render([plain, codex with { Badge = PromptCache.Hourglass + " ≈ 12 min", CacheHint = TerminalCacheHint.Aging }]);
        var bare = Render([plain, codex]);
        Check(!Same(badged, bare), "le badge de cache change le rendu de l'onglet Codex");
        Check(Same(badged, bare, 110), "l'onglet PowerShell nu ne porte aucun badge");
        var fresh = Render([codex with { CacheHint = TerminalCacheHint.Fresh }]);
        var expired = Render([codex with { CacheHint = TerminalCacheHint.Uncertain }]);
        Check(!Same(fresh, expired), "la teinte automatique suit le restant du cache");
        var manualFresh = Render([manual with { CacheHint = TerminalCacheHint.Fresh }]);
        var manualExpired = Render([manual with { CacheHint = TerminalCacheHint.Uncertain }]);
        Check(Same(manualFresh, manualExpired), "une couleur choisie n'est pas teintee par le cache");
        // Un titre long ne doit pas repousser le compte a rebours hors de l'onglet.
        var long1 = new TerminalTabInfo(Guid.NewGuid(), "Implémenter l'indicateur de cache | Battlestation", true, Activity: TerminalActivity.Ready);
        var longBadged = Render([long1 with { Badge = PromptCache.Hourglass + " ≈ 12 min", CacheHint = TerminalCacheHint.Aging }]);
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
