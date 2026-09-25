using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodexUsageTray;

namespace Battlestation.Core;

/// <summary>
/// Reconstruit la valeur API équivalente des sessions Claude Code locales à partir des
/// compteurs que Claude Code écrit déjà. Aucun contenu de message n'est lu : seuls
/// l'identifiant du message, le modèle, la date et les compteurs de tokens sont retenus.
/// Claude Code écrit une ligne par bloc d'une même réponse : l'identifiant de message
/// évite de compter deux fois les mêmes tokens, y compris après une reprise de session.
/// </summary>
public sealed class ClaudeSessionEstimator
{
    private const int ParserVersion = 1;

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly string _root;
    private readonly string _cachePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, CachedFile>? _cache;

    public ClaudeSessionEstimator(string? claudeHome = null, string? cachePath = null)
    {
        var home = claudeHome ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");
        _root = Path.Combine(home, "projects");
        _cachePath = cachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Battlestation",
            "claude-sessions-cache-v1.json");
    }

    public async Task<ApiEquivalentEstimate?> EstimateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache ??= await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
            var livePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in EnumerateSessions())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cacheKey = CacheKey(path);
                livePaths.Add(cacheKey);

                var info = new FileInfo(path);
                if (_cache.TryGetValue(cacheKey, out var cached) &&
                    cached.Parser == ParserVersion &&
                    cached.Length <= info.Length &&
                    cached.LastWriteUtcTicks == info.LastWriteTimeUtc.Ticks)
                {
                    continue;
                }

                var parsed = await ParseFileAsync(path, info, cached, cancellationToken).ConfigureAwait(false);
                if (parsed is not null)
                {
                    _cache[cacheKey] = parsed;
                }
            }

            foreach (var stalePath in _cache.Keys.Where(path => !livePaths.Contains(path)).ToArray())
            {
                _cache.Remove(stalePath);
            }

            await SaveCacheAsync(_cache, cancellationToken).ConfigureAwait(false);

            return Aggregate(_cache.Values);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ApiEquivalentEstimate? Aggregate(IEnumerable<CachedFile> files)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var models = new Dictionary<string, ModelTotals>(StringComparer.OrdinalIgnoreCase);
        var daily = new Dictionary<(DateOnly Day, string Model), ModelTotals>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        long todayTokens = 0;
        var hasToday = false;
        var pricedSessions = 0;

        foreach (var file in files)
        {
            var sessionModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sessionPriced = false;
            foreach (var entry in file.Entries)
            {
                var parts = entry.Split('|');
                if (parts.Length != 7 || !seen.Add(parts[0])) continue;
                var model = parts[1];
                if (model.Length == 0 ||
                    !DateOnly.TryParseExact(parts[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
                if (!long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var plain) ||
                    !long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var creation) ||
                    !long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cached) ||
                    !long.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var output)) continue;

                var key = day.ToString("yyyy-MM-dd") + "|" + model;
                daily.TryGetValue((day, model), out var dayTotals);
                daily[(day, model)] = (dayTotals ?? new ModelTotals()).Add(plain, creation, cached, output);
                models.TryGetValue(model, out var modelTotals);
                models[model] = (modelTotals ?? new ModelTotals()).Add(plain, creation, cached, output);
                sessionModels.Add(model);
                if (ApiEquivalentEstimator.CalculateCost(model, plain + creation, cached, output, creation) is not null)
                    sessionPriced = true;
                if (day == today)
                {
                    hasToday = true;
                    todayTokens += plain + creation + cached + output;
                }
            }

            foreach (var model in sessionModels)
            {
                models[model].Sessions++;
            }
            if (sessionPriced && sessionModels.Count > 0) pricedSessions++;
        }

        if (models.Count == 0) return null;

        decimal cost = 0, todayCost = 0;
        var priced = false;
        var pricedToday = false;
        var unknown = new List<string>();

        foreach (var (model, totals) in models)
        {
            if (ApiEquivalentEstimator.CalculateCost(model, totals.Input + totals.Creation, totals.Cached, totals.Output, totals.Creation) is { } amount)
            {
                cost += amount;
                priced = true;
            }
            else
            {
                unknown.Add(model);
            }
        }
        foreach (var ((day, model), totals) in daily)
        {
            if (day != today) continue;
            if (ApiEquivalentEstimator.CalculateCost(model, totals.Input + totals.Creation, totals.Cached, totals.Output, totals.Creation) is { } amount)
            {
                todayCost += amount;
                pricedToday = true;
            }
        }

        var observed = models.Values.Sum(totals => totals.Total);
        var breakdown = models
            .Select(pair => new ModelUsageBreakdown(
                Model: pair.Key,
                Effort: "unspecified",
                // L'entrée facturée couvre les trois tranches, comme pour les sessions Codex.
                InputTokens: pair.Value.Input + pair.Value.Cached + pair.Value.Creation,
                CachedInputTokens: pair.Value.Cached,
                OutputTokens: pair.Value.Output,
                TotalTokens: pair.Value.Total,
                Sessions: pair.Value.Sessions,
                DollarAmount: ApiEquivalentEstimator.CalculateCost(pair.Key, pair.Value.Input + pair.Value.Cached + pair.Value.Creation,
                    pair.Value.Cached, pair.Value.Output, pair.Value.Creation),
                TokenSharePercent: observed > 0 ? pair.Value.Total / (double)observed * 100d : 0d,
                CacheCreationTokens: pair.Value.Creation))
            .OrderByDescending(item => item.TotalTokens)
            .ThenBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ApiEquivalentEstimate(
            DollarAmount: priced ? cost : null,
            TodayDollarAmount: pricedToday ? todayCost : null,
            ParsedTokens: observed,
            TodayTokens: hasToday ? todayTokens : null,
            ParsedSessions: pricedSessions,
            ScaleFactor: 1d,
            UsesProxyPricing: false,
            UnknownModels: unknown.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Models: breakdown,
            DailyUsage: daily
                .Select(pair => new DailyModelTokens(pair.Key.Day, pair.Key.Model, pair.Value.Total,
                    pair.Value.Input + pair.Value.Cached + pair.Value.Creation, pair.Value.Cached, pair.Value.Output, pair.Value.Creation))
                .OrderBy(item => item.Day)
                .ThenBy(item => item.Model, StringComparer.Ordinal)
                .ToArray());
    }

    private IEnumerable<string> EnumerateSessions()
    {
        if (!Directory.Exists(_root)) return [];
        try
        {
            return Directory.EnumerateFiles(_root, "*.jsonl", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static async Task<CachedFile?> ParseFileAsync(string path, FileInfo info, CachedFile? previous, CancellationToken cancellationToken)
    {
        var canAppend = previous is { Parser: ParserVersion, Entries.Count: > 0 } &&
                        previous.Length > 0 &&
                        previous.Length < info.Length;
        var startOffset = canAppend ? previous!.Length : 0;
        var entries = canAppend ? new List<string>(previous!.Entries) : new List<string>();

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            useAsync: true);
        stream.Seek(startOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: startOffset == 0,
            bufferSize: 64 * 1024,
            leaveOpen: false);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (!line.Contains("\"type\":\"assistant\"", StringComparison.Ordinal)) continue;
            if (ReadEntry(line) is { } entry) entries.Add(entry);
        }

        var length = stream.Position;
        return new CachedFile { Length = length, LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks, Parser = ParserVersion, Entries = entries };
    }

    /// <summary>Une ligne devient « id|modèle|jour|entrée|écriture de cache|lecture de cache|sortie ».</summary>
    private static string? ReadEntry(string line)
    {
        if (!line.Contains("\"usage\"", StringComparison.Ordinal)) return null;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String ||
                !message.TryGetProperty("usage", out var usage) ||
                !usage.TryGetProperty("input_tokens", out var input) ||
                !input.TryGetInt64(out var plain)) return null;

            var model = message.TryGetProperty("model", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() ?? ""
                : "";
            // Les messages de service n'ont pas de modèle ni de compteur à attribuer.
            if (model.Length == 0 || model.StartsWith('<')) return null;

            var creation = Long(usage, "cache_creation_input_tokens");
            var cached = Long(usage, "cache_read_input_tokens");
            var output = Long(usage, "output_tokens");
            if (plain + creation + cached + output <= 0) return null;

            var day = DateOnly.FromDateTime(root.TryGetProperty("timestamp", out var timestamp) &&
                timestamp.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(timestamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed.ToLocalTime().DateTime
                    : DateTime.Now);
            return string.Join('|', id.GetString(), model.ToLowerInvariant(), day.ToString("yyyy-MM-dd"),
                plain.ToString(CultureInfo.InvariantCulture), creation.ToString(CultureInfo.InvariantCulture),
                cached.ToString(CultureInfo.InvariantCulture), output.ToString(CultureInfo.InvariantCulture));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long Long(JsonElement usage, string property) =>
        usage.TryGetProperty(property, out var value) && value.TryGetInt64(out var parsed) ? parsed : 0;

    private static string CacheKey(string path)
    {
        var normalized = Path.GetFullPath(path).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private async Task<Dictionary<string, CachedFile>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_cachePath)) return [];
            var json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, CachedFile>>(json, CacheJsonOptions) ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private async Task SaveCacheAsync(Dictionary<string, CachedFile> cache, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_cachePath);
            if (directory is not null) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(_cachePath, JsonSerializer.Serialize(cache, CacheJsonOptions), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Le cache est une optimisation ; la lecture reste valable sans lui.
        }
    }

    private sealed class CachedFile
    {
        public long Length { get; set; }
        public long LastWriteUtcTicks { get; set; }
        public int Parser { get; set; }
        public List<string> Entries { get; set; } = [];
    }

    private sealed class ModelTotals
    {
        public long Input { get; private set; }
        public long Creation { get; private set; }
        public long Cached { get; private set; }
        public long Output { get; private set; }
        public long Total => Input + Creation + Cached + Output;
        public int Sessions { get; set; }

        public ModelTotals Add(long input, long creation, long cached, long output)
        {
            Input += input;
            Creation += creation;
            Cached += cached;
            Output += output;
            return this;
        }
    }
}
