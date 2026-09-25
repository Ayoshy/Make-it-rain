using System.Globalization;

namespace CodexUsageTray;

/// <summary>Presentation counters derived once per local refresh; no additional reader or network request.</summary>
public sealed class AiUsageReport
{
    readonly Dictionary<string, string> metrics = new();
    public static readonly AiUsageReport Empty = new(null, DateTimeOffset.MinValue);
    public DateTimeOffset FetchedAt { get; }
    public string Metric(string key) => metrics.GetValueOrDefault(key, "—");
    static string Count(long value) => UsageFormatter.CompactNumber(value, AppLanguage.French);
    static string Money(decimal? value) => UsageFormatter.Dollars(value, AppLanguage.French);

    public AiUsageReport(ApiEquivalentEstimate? estimate, DateTimeOffset fetchedAt)
    {
        FetchedAt = fetchedAt;
        foreach (var family in new[] { "openai", "deepseek", "claude", "unknown" })
        foreach (var period in new[] { "total", "today" })
        {
            var rows = period == "total"
                ? estimate?.Models.Where(m => ModelFamily.Of(m.Model) == family).ToArray() ?? []
                : estimate?.DailyUsage?.Where(m => m.Day == DateOnly.FromDateTime(fetchedAt.LocalDateTime) && ModelFamily.Of(m.Model) == family)
                    .Select(m => new ModelUsageBreakdown(m.Model, "unspecified", m.InputTokens ?? 0, m.CachedInputTokens ?? 0,
                        m.OutputTokens ?? 0, m.TotalTokens, 0,
                        m.InputTokens is { } input && m.CachedInputTokens is { } cached && m.OutputTokens is { } output
                            ? ApiEquivalentEstimator.CalculateCost(m.Model, input, cached, output, m.CacheCreationTokens ?? 0) : null, 0,
                        m.CacheCreationTokens ?? 0)).ToArray() ?? [];
            rows = rows.OrderByDescending(m => m.TotalTokens).ToArray();
            var prefix = $"ai:{family}:{period}:";
            bool observed = rows.Length > 0;
            metrics[prefix + "count"] = rows.Length.ToString(CultureInfo.InvariantCulture);
            metrics[prefix + "tokens"] = observed ? Count(rows.Sum(m => m.TotalTokens)) : "—";
            metrics[prefix + "cost"] = rows.Any(m => m.DollarAmount.HasValue) ? Money(rows.Sum(m => m.DollarAmount ?? 0)) : "—";
            metrics[prefix + "coverage"] = !observed ? "Aucun compteur local" : rows.Any(m => m.DollarAmount is null) ? "Tarification partielle" : "Modèles observés tarifés";
            metrics[prefix + "input"] = observed ? Count(rows.Sum(m => Math.Max(0, m.InputTokens - m.CachedInputTokens - m.CacheCreationTokens))) : "—";
            metrics[prefix + "cached"] = observed ? Count(rows.Sum(m => m.CachedInputTokens)) : "—";
            metrics[prefix + "write"] = rows.Any(m => m.CacheCreationTokens > 0) ? Count(rows.Sum(m => m.CacheCreationTokens)) : "—";
            metrics[prefix + "output"] = observed ? Count(rows.Sum(m => m.OutputTokens)) : "—";
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                string key = prefix + i + ":";
                metrics[key + "name"] = row.Model == "unknown" ? "Modèle inconnu" : row.Model;
                metrics[key + "effort"] = row.Effort == "unspecified" ? "" : row.Effort;
                metrics[key + "tokens"] = Count(row.TotalTokens);
                metrics[key + "cost"] = row.DollarAmount is null ? "Non tarifé" : Money(row.DollarAmount);
                metrics[key + "cache"] = row.InputTokens > 0
                    ? Math.Clamp(row.CachedInputTokens * 100d / row.InputTokens, 0, 100).ToString("0", CultureInfo.InvariantCulture) + " %"
                    : "—";
            }
        }
    }
}
