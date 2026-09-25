using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Battlestation.Core;

/// <summary>
/// Fenêtre de limite publiée par l'API de compte Claude. Les montants en dollars ne
/// sont renseignés que pour les comptes facturés ainsi ; sinon l'API les laisse nuls.
/// </summary>
public sealed record ClaudeWindow(string Kind, string Label, double UsedPercent, DateTimeOffset? ResetsAt, string Severity,
    decimal? RemainingDollars = null, decimal? LimitDollars = null);

/// <summary>Crédit d'appoint activé sur l'abonnement ; solde de crédits quand il est publié.</summary>
public sealed record ClaudeSpend(string Currency, decimal Used, decimal Limit, decimal? Balance = null);

/// <summary>
/// Crédit libellé en dollars publié par l'API, avec sa date d'expiration. La clé
/// technique est un nom de code instable : l'identification se fait sur les montants.
/// </summary>
public sealed record ClaudeCredit(string Key, decimal Remaining, decimal? Limit, double? UsedPercent, DateTimeOffset? ExpiresAt);

public sealed record ClaudeAccount(DateTimeOffset FetchedAt, string? Plan, IReadOnlyList<ClaudeWindow> Windows, ClaudeSpend? Spend,
    IReadOnlyList<ClaudeCredit>? Credits = null)
{
    public ClaudeWindow? Primary => Windows.FirstOrDefault(window => window.Kind.Equals("session", StringComparison.OrdinalIgnoreCase))
        ?? Windows.FirstOrDefault();
    public IReadOnlyList<ClaudeCredit> PublishedCredits => Credits ?? [];
}

public sealed record ClaudeState(ClaudeAccount? Snapshot, bool Refreshing, string? Error);

/// <summary>
/// Lit le quota de l'abonnement Claude Code auprès de l'API de compte Anthropic, la même
/// que celle utilisée par la commande de quota de Claude Code. Le jeton OAuth reste en
/// mémoire : lu dans %USERPROFILE%\.claude\.credentials.json, jamais journalisé, copié,
/// conservé ni transmis ailleurs. Un seul GET est envoyé, aucun prompt ni crédit consommé.
/// </summary>
public static class ClaudeUsageReader
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    const string BetaHeader = "oauth-2025-04-20";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string CredentialsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    public static async Task<ClaudeAccount> ReadAsync(CancellationToken cancellationToken, string? credentialsPath = null)
    {
        var credentials = ReadCredentials(credentialsPath ?? CredentialsPath)
            ?? throw new InvalidOperationException("Connexion Claude Code absente.");
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Token);
        request.Headers.TryAddWithoutValidation("anthropic-beta", BetaHeader);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("Jeton Claude refusé. Relancez Claude Code.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"API Claude indisponible ({(int)response.StatusCode}).");
        }
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(payload, DateTimeOffset.Now, credentials.Plan)
            ?? throw new InvalidOperationException("Réponse Claude sans quota.");
    }

    static Credentials? ReadCredentials(string path)
    {
        if (!File.Exists(path)) return null;
        JsonDocument document;
        try { document = JsonDocument.Parse(File.ReadAllText(path)); }
        catch (JsonException) { return null; }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("claudeAiOauth", out var oauth)) return null;
            var token = oauth.TryGetProperty("accessToken", out var access) ? access.GetString() : null;
            if (string.IsNullOrWhiteSpace(token)) return null;
            var plan = oauth.TryGetProperty("subscriptionType", out var subscription) ? subscription.GetString() : null;
            if (oauth.TryGetProperty("expiresAt", out var expires) && expires.TryGetInt64(out var milliseconds) &&
                DateTimeOffset.FromUnixTimeMilliseconds(milliseconds) <= DateTimeOffset.Now.AddMinutes(1))
            {
                // Claude Code renouvelle ce jeton lui-même ; le dock ne l'écrit jamais.
                throw new InvalidOperationException("Session Claude à renouveler. Relancez Claude Code.");
            }
            return new Credentials(token!, string.IsNullOrWhiteSpace(plan) ? null : plan!.Trim());
        }
    }

    public static ClaudeAccount? Parse(string json, DateTimeOffset fetchedAt, string? plan = null)
    {
        UsageResponse? response;
        try { response = JsonSerializer.Deserialize<UsageResponse>(json, Json); }
        catch (JsonException) { return null; }
        if (response is null) return null;

        var windows = new List<ClaudeWindow>();
        foreach (var limit in response.Limits ?? [])
        {
            if (limit.Percent is not { } percent || string.IsNullOrWhiteSpace(limit.Kind)) continue;
            windows.Add(new(limit.Kind!.Trim(), WindowLabel(limit.Kind!), Clamp(percent), Reset(limit.ResetsAt), limit.Severity ?? "",
                limit.RemainingDollars, limit.LimitDollars));
        }
        if (windows.Count == 0)
        {
            if (response.FiveHour?.Utilization is { } five)
                windows.Add(new("session", "5 heures", Clamp(five), Reset(response.FiveHour.ResetsAt), "",
                    response.FiveHour.RemainingDollars, response.FiveHour.LimitDollars));
            if (response.SevenDay?.Utilization is { } seven)
                windows.Add(new("weekly_all", "7 jours", Clamp(seven), Reset(response.SevenDay.ResetsAt), "",
                    response.SevenDay.RemainingDollars, response.SevenDay.LimitDollars));
        }
        return windows.Count == 0 ? null : new ClaudeAccount(fetchedAt, plan, windows, Spend(response), Credits(response));
    }

    /// <summary>
    /// Les fenêtres libellées en dollars arrivent sous des noms de code qui changent :
    /// seuls les blocs publiant un montant restant sont repris comme crédits.
    /// </summary>
    static IReadOnlyList<ClaudeCredit> Credits(UsageResponse response)
    {
        var credits = new List<ClaudeCredit>();
        foreach (var (key, value) in response.Buckets ?? [])
        {
            if (value.ValueKind != JsonValueKind.Object) continue;
            var remaining = Money(value, "remaining_dollars");
            if (remaining is not { } amount) continue;
            credits.Add(new(key, amount, Money(value, "limit_dollars"), Percent(value), Reset(Text(value, "resets_at"))));
        }
        return credits;
    }

    static decimal? Money(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var amount)
            ? amount
            : null;

    static double? Percent(JsonElement element) =>
        element.TryGetProperty("utilization", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var percent)
            ? Math.Clamp(percent, 0, 100)
            : null;

    static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    static ClaudeSpend? Spend(UsageResponse response)
    {
        var used = Amount(response.Spend?.Used);
        var limit = Amount(response.Spend?.Limit);
        var currency = response.Spend?.Used?.Currency ?? response.Spend?.Limit?.Currency ?? "";
        if (used is null && response.ExtraUsage is { IsEnabled: true } extra && extra.UsedCredits is { } credits && extra.MonthlyLimit is { } monthly)
        {
            used = (decimal)credits;
            limit = (decimal)monthly;
            currency = extra.Currency ?? currency;
        }
        return used is not { } spent || limit is not { } ceiling || ceiling <= 0 || response.Spend?.Enabled is false
            ? null
            : new ClaudeSpend(currency.Trim(), spent, ceiling, Amount(response.Spend?.Balance) ?? Amount(response.Spend?.Cap?.Credits));
    }

    static decimal? Amount(MoneyBlock? money) => money?.AmountMinor is { } minor
        ? minor / Pow(money.Exponent ?? 2)
        : null;

    static decimal Pow(int exponent) => exponent switch
    {
        <= 0 => 1m,
        1 => 10m,
        2 => 100m,
        3 => 1000m,
        _ => (decimal)Math.Pow(10, exponent)
    };

    static double Clamp(double percent) => Math.Clamp(percent, 0, 100);

    static DateTimeOffset? Reset(string? text) =>
        DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed) ? parsed.ToLocalTime() : null;

    static string WindowLabel(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "session" => "5 heures",
        "weekly" or "weekly_all" => "7 jours",
        var other when other.StartsWith("weekly_", StringComparison.Ordinal) => "7 jours · " + Capitalize(other[7..]),
        var other => other.Replace('_', ' ')
    };

    static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    sealed record Credentials(string Token, string? Plan);

    sealed class UsageResponse
    {
        [JsonPropertyName("five_hour")] public WindowBlock? FiveHour { get; init; }
        [JsonPropertyName("seven_day")] public WindowBlock? SevenDay { get; init; }
        [JsonPropertyName("limits")] public List<LimitBlock>? Limits { get; init; }
        [JsonPropertyName("spend")] public SpendBlock? Spend { get; init; }
        [JsonPropertyName("extra_usage")] public ExtraUsageBlock? ExtraUsage { get; init; }
        // Blocs à nom de code (crédits en dollars, essais) : repris sans les nommer.
        [JsonExtensionData] public Dictionary<string, JsonElement>? Buckets { get; init; }
    }

    sealed class WindowBlock
    {
        [JsonPropertyName("utilization")] public double? Utilization { get; init; }
        [JsonPropertyName("resets_at")] public string? ResetsAt { get; init; }
        [JsonPropertyName("limit_dollars")] public decimal? LimitDollars { get; init; }
        [JsonPropertyName("remaining_dollars")] public decimal? RemainingDollars { get; init; }
    }

    sealed class LimitBlock
    {
        [JsonPropertyName("kind")] public string? Kind { get; init; }
        [JsonPropertyName("percent")] public double? Percent { get; init; }
        [JsonPropertyName("resets_at")] public string? ResetsAt { get; init; }
        [JsonPropertyName("severity")] public string? Severity { get; init; }
        [JsonPropertyName("limit_dollars")] public decimal? LimitDollars { get; init; }
        [JsonPropertyName("remaining_dollars")] public decimal? RemainingDollars { get; init; }
    }

    sealed class SpendBlock
    {
        [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
        [JsonPropertyName("used")] public MoneyBlock? Used { get; init; }
        [JsonPropertyName("limit")] public MoneyBlock? Limit { get; init; }
        [JsonPropertyName("balance")] public MoneyBlock? Balance { get; init; }
        [JsonPropertyName("cap")] public CapBlock? Cap { get; init; }
    }

    sealed class CapBlock
    {
        [JsonPropertyName("money")] public MoneyBlock? Money { get; init; }
        [JsonPropertyName("credits")] public MoneyBlock? Credits { get; init; }
    }

    sealed class MoneyBlock
    {
        [JsonPropertyName("amount_minor")] public long? AmountMinor { get; init; }
        [JsonPropertyName("currency")] public string? Currency { get; init; }
        [JsonPropertyName("exponent")] public int? Exponent { get; init; }
    }

    sealed class ExtraUsageBlock
    {
        [JsonPropertyName("is_enabled")] public bool IsEnabled { get; init; }
        [JsonPropertyName("monthly_limit")] public double? MonthlyLimit { get; init; }
        [JsonPropertyName("used_credits")] public double? UsedCredits { get; init; }
        [JsonPropertyName("currency")] public string? Currency { get; init; }
    }
}
