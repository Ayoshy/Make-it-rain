using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Battlestation.Core;

internal sealed record DeepSeekBalanceInfo(string Currency, decimal Total, decimal? Granted, decimal? ToppedUp);

internal sealed record DeepSeekBalance(DateTimeOffset FetchedAt, bool Available, IReadOnlyList<DeepSeekBalanceInfo> Infos)
{
    internal DeepSeekBalanceInfo? Primary => Infos.Count == 0 ? null : Infos[0];
}

internal sealed record DeepSeekState(DeepSeekBalance? Snapshot, bool Refreshing, string? Error);

/// <summary>Reads the DeepSeek account balance; the key stays in the request header and is never stored.</summary>
internal static class DeepSeekBalanceReader
{
    internal const string Endpoint = "https://api.deepseek.com/user/balance";
    internal const string KeyVariable = "DEEPSEEK_API_KEY";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    internal static string? ApiKey()
    {
        var value = Environment.GetEnvironmentVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(KeyVariable, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static async Task<DeepSeekBalance> ReadAsync(CancellationToken cancellationToken)
    {
        var key = ApiKey() ?? throw new InvalidOperationException("Clé DeepSeek absente.");
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new InvalidOperationException("Clé DeepSeek refusée.");
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"API DeepSeek indisponible ({(int)response.StatusCode}).");
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(payload, DateTimeOffset.Now) ?? throw new InvalidOperationException("Réponse DeepSeek sans solde.");
    }

    internal static DeepSeekBalance? Parse(string json, DateTimeOffset fetchedAt)
    {
        var response = JsonSerializer.Deserialize<BalanceResponse>(json, Json);
        var infos = new List<DeepSeekBalanceInfo>();
        foreach (var info in response?.BalanceInfos ?? [])
        {
            if (Amount(info.TotalBalance) is not { } total) continue;
            infos.Add(new(info.Currency?.Trim() ?? "", total, Amount(info.GrantedBalance), Amount(info.ToppedUpBalance)));
        }
        return infos.Count == 0 ? null : new DeepSeekBalance(fetchedAt, response?.IsAvailable ?? false, infos);
    }

    static decimal? Amount(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    sealed class BalanceResponse
    {
        [JsonPropertyName("is_available")] public bool IsAvailable { get; init; }
        [JsonPropertyName("balance_infos")] public List<BalanceInfo>? BalanceInfos { get; init; }
    }

    sealed class BalanceInfo
    {
        [JsonPropertyName("currency")] public string? Currency { get; init; }
        [JsonPropertyName("total_balance")] public string? TotalBalance { get; init; }
        [JsonPropertyName("granted_balance")] public string? GrantedBalance { get; init; }
        [JsonPropertyName("topped_up_balance")] public string? ToppedUpBalance { get; init; }
    }
}
