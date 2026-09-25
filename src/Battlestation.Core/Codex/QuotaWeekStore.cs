using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexUsageTray;

/// <summary>One quota sample: the percentages Codex returned and the local token counters.</summary>
public sealed record QuotaWeekLimit(string Id, string? Name, int? Minutes, long? ResetsAt, double UsedPercent);

public sealed record QuotaWeekModels(string Model, long TotalTokens, decimal? Dollars);

public sealed record QuotaWeekSample(DateTimeOffset At, List<QuotaWeekLimit> Limits, List<QuotaWeekModels> Models);

/// <summary>
/// A quota window: from one reset to the next, whether the reset was automatic or a
/// banked reset consumed early. Holds what was observed inside it.
/// </summary>
public sealed record QuotaWeekWindow
{
    public string LimitId { get; init; } = string.Empty;

    public string? LimitName { get; init; }

    public DateTimeOffset WindowStart { get; init; }

    public DateTimeOffset ResetsAt { get; init; }

    public double PeakUsedPercent { get; init; }

    public double LatestUsedPercent { get; init; }

    public long MeasuredTokens { get; init; }

    public double ObservedPercent { get; init; }

    /// <summary>API-equivalent cost and tokens of the models that have a published price.</summary>
    public decimal PricedDollars { get; init; }

    public long PricedTokens { get; init; }

    public Dictionary<string, long> TokensByModel { get; init; } = new();

    public Dictionary<string, long> ModelRatioTokens { get; init; } = new();

    public Dictionary<string, double> ModelRatioPercent { get; init; } = new();

    public int Samples { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    [JsonIgnore]
    public double? TokensPerHundredPercent =>
        ObservedPercent > 0 ? MeasuredTokens / ObservedPercent * 100d : null;

    /// <summary>
    /// Prix API d'un quota plein : le prix moyen des modèles tarifés, appliqué aux
    /// tokens d'un quota plein. Les modèles sans tarif public sont donc hors calcul.
    /// </summary>
    [JsonIgnore]
    public decimal? DollarsPerHundredPercent =>
        PricedTokens > 0 && TokensPerHundredPercent is { } tokens
            ? PricedDollars / PricedTokens * (decimal)tokens
            : null;
}

/// <summary>What one quota window is worth, as the dock shows it.</summary>
public sealed record QuotaWindowValue(
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset ResetsAt,
    bool Current,
    string? LimitName,
    double UsedPercent,
    long MeasuredTokens,
    double ObservedPercent,
    int Samples,
    double? TokensPerHundredPercent,
    decimal? DollarsPerHundredPercent,
    double PricedShare,
    bool InitialEstimate,
    double? ChangePercent);

public sealed record QuotaWeekReport(
    DateTimeOffset? RecordedSince,
    IReadOnlyList<QuotaWindowValue> Windows,
    IReadOnlyList<QuotaWeekModel> Models)
{
    public static readonly QuotaWeekReport Empty = new(null, [], []);

    public QuotaWindowValue? Current => Windows.Count > 0 ? Windows[0] : null;
}

/// <summary>How many tokens of one model a full quota is worth, when it was measured alone.</summary>
public sealed record QuotaWeekModel(string Model, long Tokens, double? TokensPerHundredPercent);

public sealed class QuotaWeekFile
{
    public int Version { get; set; } = 1;

    public List<QuotaWeekSample> Entries { get; set; } = [];

    public List<QuotaWeekWindow> Weeks { get; set; } = [];
}

/// <summary>
/// Records the quota percentage read at every refresh next to the tokens counted in the
/// local Codex sessions, so each window between two resets can tell what 100% of it is
/// worth — in tokens and in API-equivalent price — and how that value moves from one
/// window to the next. Only percentages, reset dates, token totals and costs are stored.
/// </summary>
public sealed class QuotaWeekStore
{
    public const int RetentionDays = 90;
    public const int MaximumWindows = 40;
    public const string MainLimit = "codex";

    static readonly TimeSpan Fallback = TimeSpan.FromDays(7);
    static readonly TimeSpan MaximumRatioInterval = TimeSpan.FromHours(3);
    static readonly TimeSpan MaximumDrift = TimeSpan.FromHours(6);
    const double DominantShare = .9;

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    readonly string path;
    QuotaWeekFile? state;

    public QuotaWeekStore(string path) => this.path = path;

    public string Path => path;

    /// <summary>Appends one refresh and returns what the dock draws.</summary>
    public QuotaWeekReport Record(UsageSnapshot snapshot)
    {
        var current = state ??= Load();
        current.Entries.Add(Sample(snapshot));
        Rebuild(current, snapshot);
        Save(current);
        return Report(current, snapshot);
    }

    /// <summary>Reads the recorded windows without adding a sample.</summary>
    public QuotaWeekReport Read() => Report(state ??= Load(), null);

    static QuotaWeekSample Sample(UsageSnapshot snapshot)
    {
        var limits = new List<QuotaWeekLimit>();
        foreach (var pair in UsageFormatter.OrderedLimits(snapshot.RateLimitResponse))
        {
            if (pair.Value.Primary is not { UsedPercent: { } used } window) continue;
            limits.Add(new QuotaWeekLimit(pair.Key, pair.Value.LimitName, window.WindowDurationMins, window.ResetsAt, Math.Clamp(used, 0, 100)));
        }

        var models = snapshot.ApiEquivalent?.Models
            .Where(item => ModelFamily.Of(item.Model) == "openai")
            .GroupBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
            .Select(group => new QuotaWeekModels(
                group.Key.ToLowerInvariant(),
                group.Sum(item => item.TotalTokens),
                group.All(item => item.DollarAmount is not null) ? group.Sum(item => item.DollarAmount ?? 0m) : null))
            .OrderBy(item => item.Model, StringComparer.Ordinal)
            .ToList() ?? [];
        return new QuotaWeekSample(snapshot.FetchedAt, limits, models);
    }

    static void Rebuild(QuotaWeekFile file, UsageSnapshot snapshot)
    {
        file.Entries = file.Entries
            .Where(entry => entry.At >= snapshot.FetchedAt - TimeSpan.FromDays(RetentionDays))
            .OrderBy(entry => entry.At)
            .ToList();

        var rebuilt = Aggregate(file.Entries);
        var oldest = file.Entries.Count > 0 ? file.Entries[0].At : snapshot.FetchedAt;
        foreach (var previous in file.Weeks.Where(week => week.ResetsAt <= oldest))
        {
            if (!rebuilt.Any(week => week.LimitId.Equals(previous.LimitId, StringComparison.OrdinalIgnoreCase)
                && SameWindow(week.ResetsAt, previous.ResetsAt, Drift(week.ResetsAt - week.WindowStart))))
            {
                rebuilt.Add(previous);
            }
        }

        file.Weeks = rebuilt.OrderBy(week => week.ResetsAt).TakeLast(MaximumWindows).ToList();
    }

    static List<QuotaWeekWindow> Aggregate(IReadOnlyList<QuotaWeekSample> samples)
    {
        var windows = new List<Builder>();
        var active = new Dictionary<string, Builder>(StringComparer.OrdinalIgnoreCase);
        QuotaWeekSample? previous = null;
        foreach (var sample in samples)
        {
            foreach (var limit in sample.Limits)
            {
                if (limit.ResetsAt is not > 0) continue;
                var builder = Match(windows, active, limit);
                builder.Samples++;
                builder.Latest = limit.UsedPercent;
                builder.Peak = Math.Max(builder.Peak, limit.UsedPercent);
                builder.UpdatedAt = sample.At;
            }

            if (previous is not null && sample.At > previous.At) Interval(active, previous, sample);
            previous = sample;
        }

        var built = windows.Where(builder => builder.Samples > 0).Select(builder => builder.Build()).ToList();

        // An unused window says nothing about a value, except the main quota window in
        // progress, which keeps its reset date visible until the window is used.
        var main = built
            .Where(week => week.LimitId.Equals(MainLimit, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(week => week.ResetsAt)
            .FirstOrDefault();
        return built
            .Where(week => week.PeakUsedPercent > 0 || week.MeasuredTokens > 0 || ReferenceEquals(week, main))
            .OrderBy(week => week.ResetsAt)
            .ToList();
    }

    /// <summary>Codex reports a reset date that drifts, so samples stay in the same window while it is close.</summary>
    static Builder Match(List<Builder> windows, Dictionary<string, Builder> active, QuotaWeekLimit limit)
    {
        var resetsAt = Moment(limit.ResetsAt!.Value);
        if (active.TryGetValue(limit.Id, out var current) && SameWindow(current.ResetsAt, resetsAt, Drift(current.ResetsAt - current.Start)))
        {
            return current;
        }

        var span = limit.Minutes is > 0 ? TimeSpan.FromMinutes(limit.Minutes.Value) : Fallback;
        var builder = new Builder(limit.Id, limit.Name, resetsAt - span, resetsAt, limit.UsedPercent);
        windows.Add(builder);
        active[limit.Id] = builder;
        return builder;
    }

    static void Interval(Dictionary<string, Builder> active, QuotaWeekSample previous, QuotaWeekSample current)
    {
        var moved = new Dictionary<string, long>(StringComparer.Ordinal);
        var cost = 0m;
        var pricedTokens = 0L;
        foreach (var model in current.Models)
        {
            // Also exclude external/unknown families from already stored raw samples.
            if (ModelFamily.Of(model.Model) != "openai") continue;
            var before = previous.Models.FirstOrDefault(item => item.Model.Equals(model.Model, StringComparison.OrdinalIgnoreCase));
            var delta = model.TotalTokens - (before?.TotalTokens ?? 0);
            // The local counters shrank (session removed or cache reset): skip this interval.
            if (delta < 0) return;
            if (delta <= 0) continue;
            moved[model.Model] = delta;
            if (model.Dollars is { } dollars && before?.Dollars is { } previousDollars && dollars >= previousDollars)
            {
                cost += dollars - previousDollars;
                pricedTokens += delta;
            }
        }

        var tokens = moved.Values.Sum();
        if (tokens <= 0) return;
        var clean = current.At - previous.At <= MaximumRatioInterval;
        foreach (var limit in current.Limits)
        {
            if (limit.ResetsAt is not > 0 || !active.TryGetValue(limit.Id, out var builder)) continue;
            var before = previous.Limits.FirstOrDefault(item => item.Id.Equals(limit.Id, StringComparison.OrdinalIgnoreCase));
            if (before?.ResetsAt is not > 0 || !SameWindow(builder.ResetsAt, Moment(before.ResetsAt.Value), Drift(builder.ResetsAt - builder.Start))) continue;

            var percent = limit.UsedPercent - before.UsedPercent;
            if (percent <= 0) continue;
            builder.Tokens += tokens;
            builder.Percent += percent;
            builder.Dollars += cost;
            builder.PricedTokens += pricedTokens;
            foreach (var pair in moved) builder.ByModel[pair.Key] = builder.ByModel.GetValueOrDefault(pair.Key) + pair.Value;

            if (!clean) continue;
            var dominant = moved.OrderByDescending(pair => pair.Value).First();
            if (dominant.Value / (double)tokens < DominantShare) continue;
            builder.RatioTokens[dominant.Key] = builder.RatioTokens.GetValueOrDefault(dominant.Key) + dominant.Value;
            builder.RatioPercent[dominant.Key] = builder.RatioPercent.GetValueOrDefault(dominant.Key) + percent;
        }
    }

    static QuotaWeekReport Report(QuotaWeekFile file, UsageSnapshot? snapshot)
    {
        var weeks = file.Weeks.OrderBy(week => week.ResetsAt).ToArray();
        var current = weeks
            .Where(week => week.LimitId.Equals(MainLimit, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(week => week.ResetsAt)
            .FirstOrDefault() ?? weeks.LastOrDefault();

        var values = new List<QuotaWindowValue>();
        foreach (var week in weeks.Reverse())
        {
            var isCurrent = ReferenceEquals(week, current);
            // Une fenêtre se termine au reset suivant : un reset banked consommé plus tôt
            // raccourcit donc la fenêtre précédente au lieu de la prolonger.
            var next = weeks.FirstOrDefault(candidate => candidate.LimitId.Equals(week.LimitId, StringComparison.OrdinalIgnoreCase) && candidate.ResetsAt > week.ResetsAt);
            var end = next is null || next.WindowStart >= week.ResetsAt ? week.ResetsAt : next.WindowStart;
            var tokens = week.TokensPerHundredPercent;
            var initial = false;
            if (tokens is null && isCurrent && week.LatestUsedPercent > 0 && snapshot is not null)
            {
                var spent = SessionTokens(snapshot.ApiEquivalent?.DailyUsage, week.WindowStart, week.ResetsAt);
                if (spent > 0)
                {
                    tokens = spent / week.LatestUsedPercent * 100d;
                    initial = true;
                }
            }

            var previous = weeks
                .Where(candidate => candidate.LimitId.Equals(week.LimitId, StringComparison.OrdinalIgnoreCase)
                    && candidate.ResetsAt < week.ResetsAt && candidate.TokensPerHundredPercent is > 0)
                .OrderByDescending(candidate => candidate.ResetsAt)
                .FirstOrDefault();
            var change = tokens is > 0 && previous?.TokensPerHundredPercent is > 0
                ? (tokens.Value / previous.TokensPerHundredPercent!.Value - 1d) * 100d
                : (double?)null;

            values.Add(new QuotaWindowValue(
                week.WindowStart,
                end,
                week.ResetsAt,
                isCurrent,
                week.LimitName,
                week.LatestUsedPercent,
                week.MeasuredTokens,
                week.ObservedPercent,
                week.Samples,
                tokens,
                initial ? null : week.DollarsPerHundredPercent,
                week.MeasuredTokens > 0 ? week.PricedTokens / (double)week.MeasuredTokens : 0d,
                initial,
                change));
        }

        var models = weeks
            .SelectMany(week => week.ModelRatioTokens.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(model =>
            {
                var tokens = weeks.Sum(week => week.ModelRatioTokens.GetValueOrDefault(model));
                var percent = weeks.Sum(week => week.ModelRatioPercent.GetValueOrDefault(model));
                return new QuotaWeekModel(model, tokens, percent > 0 ? tokens / percent * 100d : null);
            })
            .Where(model => model.TokensPerHundredPercent is not null)
            .OrderByDescending(model => model.TokensPerHundredPercent)
            .ToArray();

        return new QuotaWeekReport(
            file.Entries.Count > 0 ? file.Entries.Min(entry => entry.At) : null,
            values,
            models);
    }

    /// <summary>Tokens counted in the local sessions between two resets.</summary>
    static long SessionTokens(IReadOnlyList<DailyModelTokens>? daily, DateTimeOffset start, DateTimeOffset end)
    {
        if (daily is null or { Count: 0 }) return 0;
        long total = 0;
        foreach (var day in daily)
        {
            if (ModelFamily.Of(day.Model) != "openai") continue;
            // A daily counter only knows its date, so it is placed at the end of that day.
            var moment = Moment(day.Day.AddDays(1));
            if (moment >= start && moment < end) total += day.TotalTokens;
        }

        return total;
    }

    static DateTimeOffset Moment(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();

    static DateTimeOffset Moment(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToLocalTime();

    static bool SameWindow(DateTimeOffset left, DateTimeOffset right, TimeSpan drift) =>
        (left - right).Duration() <= drift;

    static TimeSpan Drift(TimeSpan window)
    {
        var quarter = window > TimeSpan.Zero ? window * .25 : MaximumDrift;
        return quarter < MaximumDrift ? quarter : MaximumDrift;
    }

    QuotaWeekFile Load()
    {
        try
        {
            if (!File.Exists(path)) return new QuotaWeekFile();
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<QuotaWeekFile>(stream, Json) ?? new QuotaWeekFile();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new QuotaWeekFile();
        }
    }

    void Save(QuotaWeekFile file)
    {
        var folder = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(folder)) return;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Atomic replace: a crash mid-write never leaves a truncated history behind.
            Directory.CreateDirectory(folder);
            File.WriteAllText(temporary, JsonSerializer.Serialize(file, Json));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Recording stays in memory when its file cannot be written.
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    sealed class Builder(string limitId, string? name, DateTimeOffset start, DateTimeOffset resetsAt, double usedPercent)
    {
        public string LimitId { get; } = limitId;
        public string? Name { get; } = name;
        public DateTimeOffset Start { get; } = start;
        public DateTimeOffset ResetsAt { get; } = resetsAt;
        public double Peak { get; set; } = usedPercent;
        public double Latest { get; set; } = usedPercent;
        public long Tokens { get; set; }
        public double Percent { get; set; }
        public decimal Dollars { get; set; }
        public long PricedTokens { get; set; }
        public int Samples { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public Dictionary<string, long> ByModel { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> RatioTokens { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, double> RatioPercent { get; } = new(StringComparer.Ordinal);

        public QuotaWeekWindow Build() => new()
        {
            LimitId = LimitId,
            LimitName = Name,
            WindowStart = Start,
            ResetsAt = ResetsAt,
            PeakUsedPercent = Peak,
            LatestUsedPercent = Latest,
            MeasuredTokens = Tokens,
            ObservedPercent = Percent,
            PricedDollars = Dollars,
            PricedTokens = PricedTokens,
            TokensByModel = new Dictionary<string, long>(ByModel, StringComparer.Ordinal),
            ModelRatioTokens = new Dictionary<string, long>(RatioTokens, StringComparer.Ordinal),
            ModelRatioPercent = new Dictionary<string, double>(RatioPercent, StringComparer.Ordinal),
            Samples = Samples,
            UpdatedAt = UpdatedAt
        };
    }
}
