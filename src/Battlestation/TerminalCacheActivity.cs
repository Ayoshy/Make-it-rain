using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Battlestation;

internal enum TerminalCacheHint { None, Fresh, Aging, Expiring, Uncertain }
internal readonly record struct TerminalCacheState(string? Badge, TerminalCacheHint Hint, string? Detail = null)
{
    public static readonly TerminalCacheState None = default;
    public bool HasBadge => !string.IsNullOrEmpty(Badge);
}

internal static class PromptCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    public const string Uncertain = "cache incertain";
    public const string Hourglass = "\u23F3";
    public static TerminalCacheState FromRemaining(TimeSpan? remaining)
    {
        if (remaining is not { } value || value <= TimeSpan.Zero)
            return new(Uncertain, TerminalCacheHint.Uncertain, "La disponibilité du cache serveur n’est pas connue.");
        value = value > Lifetime ? Lifetime : value;
        var hint = value > TimeSpan.FromMinutes(20) ? TerminalCacheHint.Fresh
            : value >= TimeSpan.FromMinutes(10) ? TerminalCacheHint.Aging : TerminalCacheHint.Expiring;
        return new(Hourglass + " ≈ " + Math.Max(1, (int)Math.Ceiling(value.TotalMinutes)) + " min", hint,
            "Estimation depuis la dernière lecture ou écriture de cache observée. Le prochain appel peut réutiliser un préfixe ; ce n’est pas une garantie.");
    }
    public static bool HasKnownWindow(string? model) => model is "gpt-6-astra" or "gpt-6-astra-latest" or "gpt-5.6"
        or "gpt-5.6-sol" or "gpt-5.6-terra" or "gpt-5.6-luna";
    public static TerminalCacheState FromObservation(DateTimeOffset now, string? model, DateTimeOffset? observed)
        => FromRemaining(HasKnownWindow(model) && observed is { } at && at <= now ? Lifetime - (now - at) : null);
    public static TerminalCacheState Hit(long? cached, long? input)
    {
        if (input is not > 0 || cached is null || cached < 0 || cached > input) return TerminalCacheState.None;
        decimal percent = Math.Floor(cached.Value * 1000m / input.Value) / 10m;
        return new("cache " + percent.ToString("0.#", CultureInfo.GetCultureInfo("fr-FR")) + " %", TerminalCacheHint.None,
            "Part des jetons d’entrée réutilisés dans la dernière réponse mesurée de cette conversation.");
    }
}

internal sealed record RolloutActivity(string Path, string SessionId, string Provider, string? Model,
    DateTimeOffset? CacheObservedAt, long? InputTokens, long? CachedInputTokens)
{
    public TerminalCacheState State(DateTimeOffset now) => Provider switch
    {
        "deepseek" => PromptCache.Hit(CachedInputTokens, InputTokens),
        "openai" => PromptCache.FromObservation(now, Model, CacheObservedAt),
        _ => TerminalCacheState.None
    };
}
internal sealed record RolloutQuery(Guid Id, int ProcessId);

// Only the CLI's open rollout writers are candidates. No project/mtime fallback.
internal sealed class CodexRolloutReader
{
    readonly string root;
    readonly Func<int, string[]> paths;
    readonly Dictionary<string, RolloutFile> files = new(StringComparer.OrdinalIgnoreCase);
    public CodexRolloutReader(string root, Func<int, string[]>? paths = null)
    {
        this.root = root;
        this.paths = paths ?? (pid => TerminalRolloutPath.Find(pid, root));
    }
    public Dictionary<Guid, RolloutActivity> Resolve(IReadOnlyList<RolloutQuery> tabs)
    {
        var result = new Dictionary<Guid, RolloutActivity>();
        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in tabs.Where(t => t.ProcessId > 0).GroupBy(t => t.ProcessId))
        {
            var candidates = new List<RolloutActivity>();
            bool failed = false;
            foreach (string path in paths(group.Key).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (TerminalRolloutPath.ScopedPath(path, root) is not { } full) continue;
                live.Add(full);
                try
                {
                    if (!files.TryGetValue(full, out var file)) files[full] = file = new RolloutFile(full);
                    if (file.Read() is { } activity) candidates.Add(activity);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    files.Remove(full); failed = true;
                }
            }
            // Subagents have their own writers in the same process. Their metadata
            // excludes them; multiple main conversations remain deliberately unknown.
            if (!failed && candidates.Count == 1)
                foreach (var tab in group) result[tab.Id] = candidates[0];
        }
        foreach (var path in files.Keys.Except(live, StringComparer.OrdinalIgnoreCase).ToArray()) files.Remove(path);
        return result;
    }

    sealed class RolloutFile(string path)
    {
        const int MaxLine = 4 * 1024 * 1024;
        readonly byte[] buffer = new byte[64 * 1024];
        readonly MemoryStream pending = new();
        long offset;
        bool skipping, headerKnown, main;
        string? id, provider, model;
        DateTimeOffset? observed;
        Usage? total, last;
        DateTime creation, write;
        void Invalidate() { observed = null; last = null; }
        void Reset()
        {
            offset = 0; pending.SetLength(0); skipping = headerKnown = main = false;
            id = provider = model = null; total = null; Invalidate();
        }
        public RolloutActivity? Read()
        {
            var info = new FileInfo(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            if (length < offset || (offset > 0 && (creation != info.CreationTimeUtc || (length == offset && write != info.LastWriteTimeUtc)))) Reset();
            creation = info.CreationTimeUtc; write = info.LastWriteTimeUtc;
            if (!headerKnown || main)
            {
                stream.Position = offset;
                while (offset < length)
                {
                    int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, length - offset));
                    if (read == 0) break;
                    offset += read;
                    int start = 0;
                    while (start < read)
                    {
                        int end = Array.IndexOf(buffer, (byte)'\n', start, read - start);
                        int count = (end < 0 ? read : end) - start;
                        if (!skipping)
                        {
                            if (pending.Length + count > MaxLine) { pending.SetLength(0); skipping = true; Invalidate(); }
                            else pending.Write(buffer, start, count);
                        }
                        if (end < 0) break;
                        if (!skipping) Accept(pending.GetBuffer().AsSpan(0, (int)pending.Length));
                        pending.SetLength(0); skipping = false; start = end + 1;
                        if (headerKnown && !main) return null;
                    }
                }
            }
            return main && id is not null && provider is not null
                ? new(path, id, provider, model, observed, last?.Input, last?.Cached) : null;
        }

        // Skip message bodies before constructing a JSON document. Only metadata,
        // context model and token counters are interpreted; nothing is persisted.
        void Accept(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty) return;
            try
            {
                var scan = new Utf8JsonReader(bytes);
                string? type = null, eventType = null;
                while (scan.Read())
                {
                    if (scan.TokenType != JsonTokenType.PropertyName) continue;
                    bool rootType = scan.CurrentDepth == 1 && scan.ValueTextEquals("type"u8);
                    bool payloadType = scan.CurrentDepth == 2 && scan.ValueTextEquals("type"u8);
                    bool payload = scan.CurrentDepth == 1 && scan.ValueTextEquals("payload"u8);
                    scan.Read();
                    if (rootType) type = scan.TokenType == JsonTokenType.String ? scan.GetString() : null;
                    else if (payloadType) eventType = scan.TokenType == JsonTokenType.String ? scan.GetString() : null;
                    else if (payload && type == "event_msg") continue;
                    else scan.Skip();
                }
                if (type == "compacted") { Invalidate(); return; }
                if (type == "event_msg" && eventType is "context_compacted" or "model_rerouted") { Invalidate(); return; }
                if (type is not ("session_meta" or "turn_context") && !(type == "event_msg" && eventType == "token_count")) return;
                using var doc = JsonDocument.Parse(bytes.ToArray());
                var record = doc.RootElement;
                var p = Property(record, "payload");
                if (type == "session_meta")
                {
                    headerKnown = true;
                    main = Text(p, "source") == "cli" && Guid.TryParse(Text(p, "id"), out _);
                    id = Text(p, "id"); provider = Text(p, "model_provider");
                    return;
                }
                if (!main) return;
                if (type == "turn_context")
                {
                    string? next = Text(p, "model");
                    if (next != model) Invalidate();
                    model = next;
                    return;
                }
                var usage = Property(p, "info");
                if (usage.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return; // quota-only notification
                var nextTotal = Usage.Read(Property(usage, "total_token_usage"));
                var nextLast = Usage.Read(Property(usage, "last_token_usage"));
                if (nextTotal is null || nextLast is null) { Invalidate(); return; }
                if (nextTotal == total) return; // repeated notification is not a new request
                bool reset = total is { } prior && (nextTotal.Input < prior.Input || nextTotal.Output < prior.Output || nextTotal.Total < prior.Total);
                total = nextTotal;
                if (reset) { Invalidate(); return; }
                last = nextLast;
                observed = (nextLast.Cached > 0 || nextLast.Write > 0)
                    && DateTimeOffset.TryParse(Text(record, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : null;
            }
            catch (JsonException) { Invalidate(); }
        }
        static JsonElement Property(JsonElement element, string name)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
        static string? Text(JsonElement element, string name)
            => Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
        static long? Counter(JsonElement element, string name)
            => Property(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out long n) && n >= 0 ? n : null;
        sealed record Usage(long Input, long? Cached, long? Write, long Output, long Total)
        {
            public static Usage? Read(JsonElement value)
            {
                var input = Counter(value, "input_tokens"); var cached = Counter(value, "cached_input_tokens");
                var write = Counter(value, "cache_write_input_tokens");
                var output = Counter(value, "output_tokens"); var total = Counter(value, "total_tokens");
                if (input is null || output is null || total is null || cached > input || write > input
                    || (cached is { } c && write is { } w && c > input - w)) return null;
                return new(input.Value, cached, write, output.Value, total.Value);
            }
        }
    }
}
