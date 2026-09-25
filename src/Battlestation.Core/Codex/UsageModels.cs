using System.Text.Json.Serialization;
using System.Globalization;

namespace CodexUsageTray;

public sealed record UsageSnapshot(
    DateTimeOffset FetchedAt,
    GetAccountRateLimitsResponse RateLimitResponse,
    GetAccountTokenUsageResponse? TokenUsage,
    string? TokenUsageWarning,
    ApiEquivalentEstimate? ApiEquivalent = null);

public sealed record ApiEquivalentEstimate(
    decimal? DollarAmount,
    decimal? TodayDollarAmount,
    long ParsedTokens,
    long? TodayTokens,
    int ParsedSessions,
    double ScaleFactor,
    bool UsesProxyPricing,
    IReadOnlyList<string> UnknownModels,
    IReadOnlyList<ModelUsageBreakdown> Models,
    IReadOnlyList<DailyModelTokens>? DailyUsage = null)
{
    /// <summary>Réunit les compteurs locaux de plusieurs lecteurs (sessions Codex, Claude) en un seul rapport.</summary>
    public static ApiEquivalentEstimate? Combine(params ApiEquivalentEstimate?[] parts)
    {
        var present = parts.Where(part => part is not null).Select(part => part!).ToArray();
        return present.Length switch
        {
            0 => null,
            1 => present[0],
            _ => new ApiEquivalentEstimate(
                present.Any(part => part.DollarAmount.HasValue) ? present.Sum(part => part.DollarAmount ?? 0) : null,
                present.Any(part => part.TodayDollarAmount.HasValue) ? present.Sum(part => part.TodayDollarAmount ?? 0) : null,
                present.Sum(part => part.ParsedTokens),
                present.Any(part => part.TodayTokens.HasValue) ? present.Sum(part => part.TodayTokens ?? 0) : null,
                present.Sum(part => part.ParsedSessions),
                present.Max(part => part.ScaleFactor),
                present.Any(part => part.UsesProxyPricing),
                present.SelectMany(part => part.UnknownModels).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                present.SelectMany(part => part.Models).OrderByDescending(model => model.TotalTokens).ToArray(),
                present.SelectMany(part => part.DailyUsage ?? []).ToArray())
        };
    }
}

public sealed record DailyModelTokens(DateOnly Day, string Model, long TotalTokens,
    long? InputTokens = null, long? CachedInputTokens = null, long? OutputTokens = null, long? CacheCreationTokens = null);

// Model identity is evidence of the model family, not of the account's billing plan.
public static class ModelFamily
{
    public static string Of(string model) => model.ToLowerInvariant() switch
    {
        var name when name.StartsWith("deepseek-") => "deepseek",
        var name when name.StartsWith("gpt-") || name.StartsWith("o1") || name.StartsWith("o3") || name.StartsWith("o4") => "openai",
        var name when name.StartsWith("claude-") => "claude",
        _ => "unknown"
    };
}

public sealed record ModelUsageBreakdown(
    string Model,
    string Effort,
    long InputTokens,
    long CachedInputTokens,
    long OutputTokens,
    long TotalTokens,
    int Sessions,
    decimal? DollarAmount,
    double TokenSharePercent,
    long CacheCreationTokens = 0);

public sealed class GetAccountRateLimitsResponse
{
    [JsonPropertyName("rateLimits")]
    public RateLimitSnapshot RateLimits { get; init; } = new();

    [JsonPropertyName("rateLimitsByLimitId")]
    public Dictionary<string, RateLimitSnapshot>? RateLimitsByLimitId { get; init; }

    [JsonPropertyName("rateLimitResetCredits")]
    public RateLimitResetCreditsSummary? RateLimitResetCredits { get; init; }
}

public sealed class RateLimitSnapshot
{
    [JsonPropertyName("limitId")]
    public string? LimitId { get; init; }

    [JsonPropertyName("limitName")]
    public string? LimitName { get; init; }

    [JsonPropertyName("primary")]
    public RateLimitWindow? Primary { get; init; }

    [JsonPropertyName("secondary")]
    public RateLimitWindow? Secondary { get; init; }

    [JsonPropertyName("credits")]
    public CreditsSnapshot? Credits { get; init; }

    [JsonPropertyName("planType")]
    public string? PlanType { get; init; }
}

public sealed class RateLimitWindow
{
    [JsonPropertyName("usedPercent")]
    public double? UsedPercent { get; init; }

    [JsonPropertyName("windowDurationMins")]
    public int? WindowDurationMins { get; init; }

    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; init; }
}

public sealed class CreditsSnapshot
{
    [JsonPropertyName("hasCredits")]
    public bool HasCredits { get; init; }

    [JsonPropertyName("unlimited")]
    public bool Unlimited { get; init; }

    [JsonPropertyName("balance")]
    public string? Balance { get; init; }
}

public sealed class RateLimitResetCreditsSummary
{
    [JsonPropertyName("availableCount")]
    public long? AvailableCount { get; init; }
}

public sealed class GetAccountTokenUsageResponse
{
    [JsonPropertyName("summary")]
    public AccountTokenUsageSummary Summary { get; init; } = new();

    [JsonPropertyName("dailyUsageBuckets")]
    public List<AccountTokenUsageDailyBucket>? DailyUsageBuckets { get; init; }
}

public sealed class AccountTokenUsageSummary
{
    [JsonPropertyName("lifetimeTokens")]
    public long? LifetimeTokens { get; init; }

    [JsonPropertyName("peakDailyTokens")]
    public long? PeakDailyTokens { get; init; }

    [JsonPropertyName("longestRunningTurnSec")]
    public long? LongestRunningTurnSec { get; init; }

    [JsonPropertyName("currentStreakDays")]
    public long? CurrentStreakDays { get; init; }

    [JsonPropertyName("longestStreakDays")]
    public long? LongestStreakDays { get; init; }
}

public sealed class AccountTokenUsageDailyBucket
{
    [JsonPropertyName("startDate")]
    public string StartDate { get; init; } = string.Empty;

    [JsonPropertyName("tokens")]
    public long Tokens { get; init; }
}

public static class UsageFormatter
{

    public static string CompactNumber(long? value, AppLanguage language = AppLanguage.English)
    {
        if (value is null)
        {
            return "—";
        }

        var culture = AppText.Culture(language);
        var (thousand, million, billion) = language == AppLanguage.French
            ? (" k", " M", " Md")
            : ("K", "M", "B");
        var absolute = Math.Abs((double)value.Value);
        return absolute switch
        {
            >= 1_000_000_000 => $"{(value.Value / 1_000_000_000d).ToString("0.##", culture)}{billion}",
            >= 1_000_000 => $"{(value.Value / 1_000_000d).ToString("0.##", culture)}{million}",
            >= 1_000 => $"{(value.Value / 1_000d).ToString("0.##", culture)}{thousand}",
            _ => value.Value.ToString("N0", culture)
        };
    }

    public static string Dollars(decimal? value, AppLanguage language = AppLanguage.English)
    {
        if (value is null)
        {
            return "—";
        }

        var number = value.Value.ToString(
            value.Value >= 100 ? "#,##0" : "#,##0.00",
            AppText.Culture(language));
        return language == AppLanguage.French ? $"{number} $" : $"${number}";
    }

    public static string WindowLabel(int? minutes, AppLanguage language = AppLanguage.English) => language == AppLanguage.French ? minutes switch
    {
        10080 => "7 jours",
        1440 => "24 heures",
        300 => "5 heures",
        > 0 when minutes % 1440 == 0 => $"{minutes / 1440} jours",
        > 0 when minutes % 60 == 0 => $"{minutes / 60} heures",
        > 0 => $"{minutes} minutes",
        _ => "Fenêtre courante"
    }
        : minutes switch
        {
            10080 => "7 days",
            1440 => "24 hours",
            300 => "5 hours",
            > 0 when minutes % 1440 == 0 => $"{minutes / 1440} days",
            > 0 when minutes % 60 == 0 => $"{minutes / 60} hours",
            > 0 => $"{minutes} minutes",
            _ => AppText.Get(language, TextId.CurrentWindow)
        };

    public static DateTimeOffset? ResetTime(long? unixSeconds)
    {
        if (unixSeconds is null or <= 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).ToLocalTime();
    }

    public static long TokensToday(
        GetAccountTokenUsageResponse? usage,
        DateTimeOffset now,
        long? localFallback = null)
    {
        var today = now.ToString("yyyy-MM-dd");
        var officialBucket = usage?.DailyUsageBuckets?.FirstOrDefault(bucket =>
            bucket.StartDate.StartsWith(today, StringComparison.Ordinal));
        return officialBucket?.Tokens ?? localFallback ?? 0;
    }

    public static IReadOnlyList<KeyValuePair<string, RateLimitSnapshot>> OrderedLimits(
        GetAccountRateLimitsResponse response)
    {
        var limits = response.RateLimitsByLimitId;
        if (limits is null || limits.Count == 0)
        {
            return new[]
            {
                new KeyValuePair<string, RateLimitSnapshot>(response.RateLimits.LimitId ?? "codex", response.RateLimits)
            };
        }

        return limits
            .OrderBy(pair => pair.Key.Equals("codex", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(pair => pair.Value.LimitName ?? pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
