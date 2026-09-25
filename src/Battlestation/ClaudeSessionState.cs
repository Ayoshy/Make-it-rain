using System.IO;
using System.Text.Json;

namespace Battlestation;

/// <summary>État qu'une session Claude Code publie pour elle-même.</summary>
internal sealed record ClaudeSessionInfo(string? Name, TerminalActivity Activity, string? Detail);

/// <summary>
/// Lit le registre des sessions Claude Code, dans %USERPROFILE%\.claude\sessions. Seuls
/// le nom, l'état et l'attente en cours sont repris : ni conversation, ni identifiant.
/// Le registre est la source de vérité de l'état ; le titre de console ne sert que de
/// repli quand le fichier du processus a disparu.
/// </summary>
internal static class ClaudeSessions
{
    internal const int MaximumRecordBytes = 16 * 1024;

    internal static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "sessions");

    internal static Dictionary<int, ClaudeSessionInfo> Read(IEnumerable<int> pids, string? root = null)
    {
        var records = new Dictionary<int, ClaudeSessionInfo>();
        foreach (var pid in pids.Distinct())
        {
            if (pid <= 0) continue;
            var path = Path.Combine(root ?? Root, pid + ".json");
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length is <= 0 or > MaximumRecordBytes) continue;
                if (Parse(File.ReadAllText(path)) is { } record) records[pid] = record;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return records;
    }

    internal static ClaudeSessionInfo? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var waiting = Text(root, "waitingFor");
            var needs = Text(root, "needs");
            var status = Text(root, "status");
            var tempo = Text(root, "tempo");
            return new ClaudeSessionInfo(Text(root, "name"), Activity(status, tempo, waiting, needs), Detail(waiting, needs));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>État publié par Claude Code : une attente prime sur le statut brut.</summary>
    internal static TerminalActivity Activity(string? status, string? tempo = null, string? waitingFor = null, string? needs = null)
    {
        if (!string.IsNullOrWhiteSpace(waitingFor) || !string.IsNullOrWhiteSpace(needs)) return TerminalActivity.Attention;
        return (status ?? tempo ?? "").Trim().ToLowerInvariant() switch
        {
            "busy" or "working" or "running" or "active" => TerminalActivity.Working,
            "waiting" or "blocked" or "needs_input" or "needs-input" or "requires_action" => TerminalActivity.Attention,
            "idle" or "ready" or "done" or "completed" => TerminalActivity.Ready,
            "error" or "failed" => TerminalActivity.Error,
            _ => TerminalActivity.Unknown
        };
    }

    static string? Detail(string? waitingFor, string? needs)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(waitingFor)) parts.Add("Attente : " + waitingFor);
        if (!string.IsNullOrWhiteSpace(needs)) parts.Add(needs);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = TerminalTabPreferences.Clean(value.GetString()).Trim();
        return text.Length == 0 ? null : text;
    }
}
