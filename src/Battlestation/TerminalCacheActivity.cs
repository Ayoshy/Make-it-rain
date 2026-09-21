using System.IO;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Battlestation;

// Etat d'onglet derive du cache de prompt. Aucun contenu de session n'est lu :
// seuls l'horodatage du dernier evenement et des compteurs de jetons le sont.
internal enum TerminalCacheHint { None, Fresh, Aging, Expiring, Expired }

internal readonly record struct TerminalCacheState(string? Badge, TerminalCacheHint Hint)
{
    public static readonly TerminalCacheState None = default;
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
}

internal static class PromptCache
{
    // Fenetre annoncee par OpenAI : un prefixe mis en cache reste reutilisable
    // 30 minutes apres sa derniere ecriture ou reutilisation.
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    public const string Expired = "cache expiré";
    public const string Hourglass = "\u23F3";
    public static TerminalCacheState FromRemaining(TimeSpan? remaining)
    {
        if (remaining is not { } value || value <= TimeSpan.Zero) return new(Expired, TerminalCacheHint.Expired);
        var hint = value > TimeSpan.FromMinutes(20) ? TerminalCacheHint.Fresh
            : value >= TimeSpan.FromMinutes(10) ? TerminalCacheHint.Aging
            : TerminalCacheHint.Expiring;
        return new(Hourglass + " " + Math.Max(1, (int)Math.Ceiling(value.TotalMinutes)) + " min", hint);
    }
    public static TerminalCacheState FromActivity(DateTimeOffset now, DateTimeOffset last) => FromRemaining(Lifetime - (now - last));
    // Taux du dernier tour, au dixieme et jamais surestime : la valeur bouge a
    // chaque tour, comme le compte a rebours bouge a chaque evenement.
    public static TerminalCacheState Hit(long cachedInputTokens, long inputTokens)
    {
        if (inputTokens <= 0) return TerminalCacheState.None;
        var cached = Math.Clamp(cachedInputTokens, 0, inputTokens);
        double percent = Math.Floor(cached * 1000d / inputTokens) / 10d;
        string text = percent >= 100 ? "100"
            : percent == Math.Floor(percent) ? percent.ToString("0", CultureInfo.GetCultureInfo("fr-FR"))
            : percent.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR"));
        return new("cache " + text + " %", TerminalCacheHint.None);
    }
}

internal sealed record RolloutActivity(string Path, string Project, string Provider, DateTimeOffset Last, long InputTokens = 0, long CachedInputTokens = 0);

// Le projet suivi vient de l'action demandee au bureau quand elle est connue
// (chemin complet, variante exacte) ; sinon du nom de projet porte par le titre
// de console, comme l'indice de projet des docks.
internal sealed record RolloutQuery(Guid Id, string Hint, bool? DeepSeek);

// Lecteur d'activite Codex : pour chaque onglet, le rollout le plus recent du
// projet suivi. La ligne de session (projet et fournisseur) et l'horodatage du
// dernier evenement sont les seules donnees lues ; le contenu des messages ne
// l'est jamais. Les resultats sont memorises par chemin, taille et date.
internal sealed class CodexRolloutReader
{
    static readonly long[] Windows = [64 * 1024, 1024 * 1024, 8 * 1024 * 1024];
    // Le fichier d'une session en cours garde une date de modification figee :
    // les derniers candidats sont compares par l'horodatage de leur contenu.
    const int Candidates = 6;
    readonly string root;
    readonly Dictionary<string, (long Length, DateTime Write, string? Project, string? Provider)> headers = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (long Length, DateTime Write, DateTimeOffset Last, long Input, long Cached)> tails = new(StringComparer.OrdinalIgnoreCase);
    public CodexRolloutReader(string root) => this.root = root;
    public Dictionary<Guid, RolloutActivity> Resolve(IReadOnlyList<RolloutQuery> tabs)
    {
        var found = new Dictionary<Guid, RolloutActivity>();
        if (tabs.Count == 0 || !Directory.Exists(root)) return found;
        var files = Files();
        foreach (var tab in tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.Hint)) continue;
            RolloutActivity? recent = null; int examined = 0;
            foreach (var file in files)
            {
                if (Head(file) is not { } head) continue;
                if (!Match(head.Provider, tab.DeepSeek) || !Project(head.Project, tab.Hint)) continue;
                if (Tail(file, out var tokens) is not { } last) continue;
                if (recent is null || last > recent.Last) recent = new(file.FullName, head.Project!, head.Provider!, last, tokens.Input, tokens.Cached);
                if (++examined >= Candidates) break;
            }
            if (recent is { } activity) found[tab.Id] = activity;
        }
        return found;
    }
    // Une variante inconnue ne filtre pas : le rollout le plus recent du projet decide.
    static bool Match(string? provider, bool? deepSeek)
        => deepSeek is not { } wanted || wanted == string.Equals(provider, "deepseek", StringComparison.OrdinalIgnoreCase);
    // Un chemin complet se compare au projet du rollout ; un nom seul a son dernier dossier.
    static bool Project(string? project, string hint)
    {
        if (project is null) return false;
        return hint.Contains('\\') || hint.Contains('/')
            ? string.Equals(project, Normalize(hint), StringComparison.OrdinalIgnoreCase)
            : string.Equals(Path.GetFileName(project), hint, StringComparison.OrdinalIgnoreCase);
    }
    List<FileInfo> Files()
    {
        var files = new List<FileInfo>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                var info = new FileInfo(path);
                if (info.Length > 0) files.Add(info);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { }
        files.Sort((left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
        return files;
    }
    (string? Project, string? Provider)? Head(FileInfo file)
    {
        if (headers.TryGetValue(file.FullName, out var cached) && cached.Length == file.Length && cached.Write == file.LastWriteTimeUtc)
            return (cached.Project, cached.Provider);
        var (project, provider) = ReadHead(file.FullName);
        headers[file.FullName] = (file.Length, file.LastWriteTimeUtc, project, provider);
        return (project, provider);
    }
    static (string? Project, string? Provider) ReadHead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, false);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, false);
            if (reader.ReadLine() is not { Length: > 0 } line || line.Length > 4 * 1024 * 1024) return (null, null);
            using var json = JsonDocument.Parse(line);
            if (!json.RootElement.TryGetProperty("payload", out var payload)) return (null, null);
            string? text(string name) => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var project = text("cwd");
            return (string.IsNullOrWhiteSpace(project) ? null : Normalize(project), text("model_provider"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return (null, null); }
    }
    // Horodatage du dernier evenement et compteurs cumules de la session
    // (info.total_token_usage), lus dans le meme bloc de fin de fichier.
    DateTimeOffset? Tail(FileInfo file, out (long Input, long Cached) tokens)
    {
        tokens = default;
        bool known = tails.TryGetValue(file.FullName, out var cached) && cached.Length == file.Length && cached.Write == file.LastWriteTimeUtc;
        if (known)
        {
            tokens = (cached.Input, cached.Cached);
            return cached.Last;
        }
        DateTimeOffset? last = null; long input = 0, cachedInput = 0;
        foreach (var window in Windows)
        {
            var read = ReadTail(file.FullName, file.Length, window, last is null, input == 0);
            last ??= read.Stamp;
            if (input == 0 && read.Input > 0) { input = read.Input; cachedInput = read.Cached; }
            if (last is not null && input > 0) break;
        }
        // Le dernier token_count peut manquer du bloc courant : la mesure
        // precedente de la meme session reste valable.
        if (input == 0) { input = cached.Input; cachedInput = cached.Cached; }
        if (last is { } stamp) tails[file.FullName] = (file.Length, file.LastWriteTimeUtc, stamp, input, cachedInput);
        tokens = (input, cachedInput);
        return last;
    }
    static (DateTimeOffset? Stamp, long Input, long Cached) ReadTail(string path, long length, long window, bool wantStamp, bool wantTokens)
    {
        try
        {
            var start = Math.Max(0, length - window);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, false);
            stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[length - start];
            int read = 0;
            while (read < buffer.Length)
            {
                int count = stream.Read(buffer, read, buffer.Length - read);
                if (count <= 0) break;
                read += count;
            }
            DateTimeOffset? stamp = null; long input = 0, cached = 0;
            var lines = Encoding.UTF8.GetString(buffer, 0, read).Split('\n');
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                // Le premier fragment du bloc peut etre tronque : il est ignore.
                if (start > 0 && index == 0) break;
                var line = lines[index].Trim();
                if (line.Length == 0) continue;
                var (found, tokens, cachedTokens) = Summary(line);
                if (wantStamp && stamp is null) stamp = found;
                if (wantTokens && input == 0 && tokens > 0) { input = tokens; cached = cachedTokens; }
                if ((!wantStamp || stamp is not null) && (!wantTokens || input > 0)) break;
            }
            return (stamp, input, cached);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException or ArgumentException) { }
        return (null, 0, 0);
    }
    // Une seule lecture JSON par ligne : horodatage et compteurs du dernier tour
    // (le total de session sert tant que le tour courant n'est pas compté).
    static (DateTimeOffset? Stamp, long Input, long Cached) Summary(string line)
    {
        try
        {
            using var json = JsonDocument.Parse(line);
            DateTimeOffset? stamp = json.RootElement.TryGetProperty("timestamp", out var value) && value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
            long input = 0, cached = 0;
            if (json.RootElement.TryGetProperty("payload", out var payload) && payload.TryGetProperty("info", out var info))
                if (!Usage(info, "last_token_usage", out input, out cached)) Usage(info, "total_token_usage", out input, out cached);
            return (stamp, input, cached);
        }
        catch (JsonException) { return (null, 0, 0); }
    }
    static bool Usage(JsonElement info, string name, out long input, out long cached)
    {
        input = 0; cached = 0;
        if (!info.TryGetProperty(name, out var usage)) return false;
        if (usage.TryGetProperty("input_tokens", out var tokens) && tokens.TryGetInt64(out var value)) input = value;
        if (usage.TryGetProperty("cached_input_tokens", out var cachedTokens) && cachedTokens.TryGetInt64(out var cachedValue)) cached = cachedValue;
        return input > 0;
    }
    internal static string Normalize(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return path.Trim(); }
    }
}
