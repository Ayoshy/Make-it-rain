using System.Globalization;
using System.Text.Json;
using ConradSensor;
using CodexUsageTray;

namespace Battlestation.Core;

internal sealed partial class Backend
{
    volatile GpuControlState? controls;
    int writePending;
    DateTimeOffset target = DateTimeOffset.Parse("2026-11-19T00:00:00+01:00", CultureInfo.InvariantCulture);
    bool targetValid=true;
    static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    static string Number(double? n, string unit = "") => n is { } v && double.IsFinite(v) ? Math.Round(v).ToString(CultureInfo.InvariantCulture) + unit : "—";
    static string Count(long? n) => UsageFormatter.CompactNumber(n, AppLanguage.French);
    static string Money(decimal? n) => UsageFormatter.Dollars(n, AppLanguage.French);
    static string Amount(decimal? n) => n is { } v ? v.ToString("#,##0.00", CultureInfo.GetCultureInfo("fr-FR")) : "—";
    // Les fenêtres Claude et les crédits publiés en dollars restent lisibles sur une ligne.
    static string ClaudeWeekly(ClaudeAccount? account)
    {
        var window = account?.Windows.FirstOrDefault(item => item.Kind.StartsWith("weekly", StringComparison.OrdinalIgnoreCase));
        return window is null ? "" : "7 j " + Number(Math.Clamp(100 - window.UsedPercent, 0, 100), "%");
    }
    static string ClaudeCreditShort(ClaudeAccount? account) =>
        account?.PublishedCredits.FirstOrDefault() is { } credit ? "crédit " + Money(credit.Remaining) : "";
    static string ClaudeReset(ClaudeAccount? account)
    {
        if (account?.Primary is not { } primary) return "Reset indisponible";
        // Un reset du jour n'a pas besoin de la date sur la carte.
        var line = primary.ResetsAt is { } reset
            ? "Reset " + reset.ToString(reset.Date == DateTimeOffset.Now.Date ? "HH:mm" : "dd/MM HH:mm")
            : "Reset indisponible";
        return line + (primary.RemainingDollars is { } credit ? " · " + Money(credit) + " restants" : "");
    }
    static string ClaudeResetLine(ClaudeAccount? account) =>
        string.Join(" · ", new[] { ClaudeCreditShort(account), ClaudeReset(account) }.Where(part => part.Length > 0));
    public string Metric(string key)
    {
        if (key.StartsWith("ai:", StringComparison.Ordinal)) return aiUsage.Metric(key);
        if(!targetValid && key is "days" or "hours" or "minutes" or "seconds" or "date") return "—";
        var s = sensors is { } current && DateTimeOffset.Now - current.FetchedAt < TimeSpan.FromSeconds(15) ? current : null;
        var usage = meter.Snapshot;
        var quota = usage?.Limits.FirstOrDefault(l => l.Id == "codex") ?? usage?.Limits.FirstOrDefault();
        var week = weekReport;
        var claudeState = claude;
        var claudeAccount = claudeState.Snapshot;
        var balanceState = deepseek;
        var balance = balanceState.Snapshot;
        var balanceInfo = balance?.Primary;
        var left = target - DateTimeOffset.Now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        if (key.StartsWith("core") && int.TryParse(key[4..], out var core)) return Number(s?.CpuCores.ElementAtOrDefault(core)?.Celsius, "°");
        if (key.StartsWith("model:"))
        {
            var parts = key.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var index) || index < 0) return "—";
            var model = usage?.Models.ElementAtOrDefault(index);
            if (model is null) return "";
            return parts[2] switch { "name" => model.Model + "  " + model.Effort, "tokens" => Count(model.TotalTokens), "cost" => Money(model.DollarAmount), _ => "" };
        }
        if (key.StartsWith("weekWindow:"))
        {
            var parts = key.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var index) || index < 0 || index >= week.Windows.Count) return "—";
            var window = week.Windows[index];
            return parts[2] switch
            {
                "period" => window.Start.ToString("d MMM", French) + " → " + window.End.ToString("d MMM", French),
                "value" => Count(window.TokensPerHundredPercent is { } value ? (long)value : null),
                "cost" => Money(window.DollarsPerHundredPercent),
                "delta" => window.ChangePercent is { } change ? (change >= 0 ? "+" : "") + Math.Round(change).ToString(CultureInfo.InvariantCulture) + " %" : "",
                "current" => window.Current ? "1" : "0",
                "samples" => window.Samples.ToString(),
                _ => ""
            };
        }
        if (key.StartsWith("weekModel:"))
        {
            var parts = key.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var index) || index < 0 || index >= week.Models.Count) return "—";
            var model = week.Models[index];
            return parts[2] switch
            {
                "name" => model.Model,
                "tokens" => Count(model.Tokens),
                "ratio" => Count(model.TokensPerHundredPercent is { } value ? (long)value : null),
                _ => ""
            };
        }
        if (key.StartsWith("claudeWindow:", StringComparison.Ordinal))
        {
            var parts = key.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var index) || index < 0 ||
                claudeAccount is null || index >= claudeAccount.Windows.Count) return "—";
            var window = claudeAccount.Windows[index];
            return parts[2] switch
            {
                "name" => window.Label,
                "remaining" => Number(Math.Clamp(100 - window.UsedPercent, 0, 100), "% restant"),
                "reset" => (window.ResetsAt is { } reset ? reset.ToString("dd/MM HH:mm") : "indisponible")
                    + (window.RemainingDollars is { } remaining ? " · " + Money(remaining) + " restants" : ""),
                "detail" => (window.ResetsAt is { } reset ? "Reset " + reset.ToString("dd/MM HH:mm") : "Reset indisponible")
                    + (window.RemainingDollars is { } remaining ? " · " + Money(remaining) + " restants" : ""),
                "severity" => window.Severity.Equals("normal", StringComparison.OrdinalIgnoreCase) ? "" : window.Severity,
                _ => ""
            };
        }
        if (key.StartsWith("claudeCredit:", StringComparison.Ordinal))
        {
            var parts = key.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var index) || index < 0 ||
                claudeAccount is null || index >= claudeAccount.PublishedCredits.Count) return "—";
            var credit = claudeAccount.PublishedCredits[index];
            var remainingPercent = credit.UsedPercent is { } used
                ? 100 - used
                : credit.Limit is { } limit && limit > 0 ? (double)(credit.Remaining / limit * 100m) : double.NaN;
            return parts[2] switch
            {
                "name" => "Crédit cloud",
                "remaining" => Number(double.IsFinite(remainingPercent) ? remainingPercent : null, "% restant"),
                "detail" => Money(credit.Remaining)
                    + (credit.ExpiresAt is { } expiry ? " · expire " + expiry.ToString("dd/MM HH:mm") : " · sans expiration"),
                _ => ""
            };
        }
        return key switch
        {
            "summary" => Display, "cpu" => Number(s?.CpuPackageCelsius, "°"), "gpu" => Number(s?.GpuCelsius, "°"),
            "cpuLoad" => Number(s?.CpuLoadPercent, "%"), "gpuLoad" => Number(s?.GpuLoadPercent, "%"),
            "fan" => Number(s?.GpuFanPercent, "%"), "watts" => Number(s?.GpuPowerWatts, " W"), "hottest" => Number(s?.CpuHottestCoreCelsius, "°"),
            "sensorStatus" => gpuBusy || Volatile.Read(ref writePending)!=0 ? "APPLICATION…" : gpuError is not null ? "COMMANDE GPU REFUSÉE" : s is null ? "CAPTEURS INDISPONIBLES" : heatwave ? "CANICULE ACTIVE" : "EN DIRECT",
            "sensorError" => gpuError ?? hardwareError ?? "", "heatwave" => heatwave ? "1" : "0",
            "remaining" => Number(quota?.Primary?.UsedPercent is { } used ? Math.Clamp(100-used,0,100) : null, "%"),
            "quotaLabel" => quota?.Primary is { } q ? UsageFormatter.WindowLabel(q.WindowDurationMins, AppLanguage.French).ToUpperInvariant() : "QUOTA",
            "reset" => quota?.Primary?.ResetsAt is { } ts ? "Reset " + DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().ToString("dd/MM HH:mm") : "Reset indisponible",
            "today" => Count(usage?.TodayTokens), "total" => Count(usage?.LifetimeTokens), "todayCost" => Money(usage?.TodayApiDollars), "totalCost" => Money(usage?.TotalApiDollars),
            "credits" => usage?.ResetCredits is { } count ? count + " crédit(s) de reset · lecture seule" : "Crédits indisponibles",
            "codexStatus" => meter.Refreshing ? "ACTUALISATION…" : usage is null ? "INDISPONIBLE" : meter.Error is not null || DateTimeOffset.Now-usage.FetchedAt > TimeSpan.FromMinutes(16) ? "DERNIÈRE MESURE" : "MAJ " + usage.FetchedAt.ToString("HH:mm"),
            "codexError" => meter.Error ?? "", "modelsCount" => (usage?.Models.Count ?? 0).ToString(),
            "aiStatus" => meter.Refreshing ? "ACTUALISATION…" : aiUsage.FetchedAt == DateTimeOffset.MinValue ? "INDISPONIBLE"
                : aiError is not null || DateTimeOffset.Now - aiUsage.FetchedAt > TimeSpan.FromMinutes(16) ? "DERNIÈRE MESURE" : "MAJ " + aiUsage.FetchedAt.ToString("HH:mm"),
            "aiError" => aiError ?? "",
            "weekTitle" => "QUOTA HEBDOMADAIRE",
            "weekUsed" => Number(week.Current?.UsedPercent, " %"),
            "weekReset" => week.Current is { } window ? "Reset " + window.ResetsAt.ToString("ddd d MMM · HH:mm", French) : "Reset indisponible",
            "weekValue" => Count(week.Current?.TokensPerHundredPercent is { } value ? (long?)value : null),
            "weekValueCost" => week.Current?.DollarsPerHundredPercent is not { } price
                ? "—"
                : Money(price) + (week.Current.PricedShare < .95 ? " · partiel" : ""),
            "weekValueState" => week.Current is not { TokensPerHundredPercent: not null } window ? ""
                : window.InitialEstimate
                    ? "estimation initiale · sessions locales"
                    : "mesuré sur " + window.ObservedPercent.ToString("0.#", French) + " % · " + window.Samples + " relevés",
            "weekDelta" => week.Current?.ChangePercent is { } change ? (change >= 0 ? "+" : "") + Math.Round(change).ToString(CultureInfo.InvariantCulture) + " % vs fenêtre préc." : "",
            "weekSince" => week.RecordedSince is { } since ? "relevés depuis le " + since.ToString("d MMM HH:mm", French) : "",
            "weekWindowCount" => week.Windows.Count.ToString(), "weekModelCount" => week.Models.Count.ToString(),
            "deepseekCurrency" => balanceInfo?.Currency is { Length: > 0 } currency ? currency : "—",
            "deepseekTotal" => Amount(balanceInfo?.Total), "deepseekGranted" => Amount(balanceInfo?.Granted), "deepseekToppedUp" => Amount(balanceInfo?.ToppedUp),
            "deepseekAvailable" => balance is null ? "" : balance.Available ? "1" : "0",
            "deepseekStatus" => balanceState.Refreshing ? "ACTUALISATION…" : balance is null ? "INDISPONIBLE" : balanceState.Error is not null || DateTimeOffset.Now - balance.FetchedAt > TimeSpan.FromMinutes(16) ? "DERNIÈRE MESURE" : "MAJ " + balance.FetchedAt.ToString("HH:mm"),
            "deepseekError" => balanceState.Error ?? "",
            "claudeStatus" => claudeState.Refreshing ? "ACTUALISATION…" : claudeAccount is null ? "INDISPONIBLE"
                : claudeState.Error is not null || DateTimeOffset.Now - claudeAccount.FetchedAt > TimeSpan.FromMinutes(16) ? "DERNIÈRE MESURE" : "MAJ " + claudeAccount.FetchedAt.ToString("HH:mm"),
            "claudeError" => claudeState.Error ?? "",
            "claudePlan" => claudeAccount?.Plan is { Length: > 0 } plan ? "Abonnement " + plan : "Abonnement Claude",
            "claudeWindowCount" => (claudeAccount?.Windows.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            "claudeRemaining" => Number(claudeAccount?.Primary is { } primary ? Math.Clamp(100 - primary.UsedPercent, 0, 100) : null, "%"),
            "claudeWindowLabel" => claudeAccount?.Primary?.Label?.ToUpperInvariant() ?? "QUOTA",
            "claudeReset" => ClaudeReset(claudeAccount),
            "claudeResetLine" => ClaudeResetLine(claudeAccount),
            "claudeWeekly" => ClaudeWeekly(claudeAccount),
            "claudeCredit" => ClaudeCreditShort(claudeAccount),
            "claudeCreditCount" => claudeAccount?.PublishedCredits.Count.ToString(CultureInfo.InvariantCulture) ?? "0",
            "claudeSpend" => claudeAccount?.Spend is { } spend
                ? (spend.Balance is { } creditBalance ? "Solde " + Money(creditBalance) + " · " : "")
                    + spend.Used.ToString("#,##0.00", French) + " / " + spend.Limit.ToString("#,##0.00", French) + " " + spend.Currency + " · crédit d'appoint"
                : "Crédit d'appoint indisponible",
            "quotaDetails" => usage is null ? "Indisponible" : string.Join("\n\n", usage.Limits.Select(l => l.Name + "\n" + string.Join("   ·   ", new[] { l.Primary, l.Secondary }.Where(q => q is not null).Select(q => Number(q!.UsedPercent is { } u ? 100-u : null, "% restant") + " / " + UsageFormatter.WindowLabel(q.WindowDurationMins, AppLanguage.French) + " / " + (q.ResetsAt is { } r ? DateTimeOffset.FromUnixTimeSeconds(r).ToLocalTime().ToString("dd/MM HH:mm") : "reset indisponible"))))),
            "fanAuto" => controls is null ? "—" : controls.FanAuto ? "1" : "0",
            "fanTarget" => controls is null ? "—" : controls.FanPercent.ToString(),
            "fanMin" => controls is null ? "—" : controls.FanMinPercent.ToString(),
            "fanMax" => controls is null ? "—" : controls.FanMaxPercent.ToString(),
            "thermalTarget" => controls is null ? "—" : controls.ThermalLimitCelsius.ToString(),
            "thermalMin" => controls is null ? "—" : controls.ThermalMinCelsius.ToString(),
            "thermalMax" => controls is null ? "—" : controls.ThermalMaxCelsius.ToString(),
            "controlAvailable" => controls?.Available == true ? "1" : "0",
            "controlStatus" => controls?.Status ?? "LECTURE…",
            "days" => ((int)left.TotalDays).ToString("00"), "hours" => left.Hours.ToString("00"), "minutes" => left.Minutes.ToString("00"), "seconds" => left.Seconds.ToString("00"),
            "date" => target.ToString("dd MMMM yyyy",CultureInfo.GetCultureInfo("fr-FR")).ToUpperInvariant(),
            _ => "—"
        };
    }
    public void NativeCommand(string command)
    {
        if(command.StartsWith("Target:",StringComparison.Ordinal)){
            targetValid=DateTimeOffset.TryParse(command[7..],CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed);
            if(targetValid)target=parsed;
            return;
        }
        if (command == "Refresh") { RequestRefresh(); return; }
        if (command != "GpuRead" && command != "Heatwave" && !command.StartsWith("Apply:")) return;
        var writing = command != "GpuRead";
        if (writing && Interlocked.Exchange(ref writePending,1)!=0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                if (command == "GpuRead") { controls = (GpuControlState)await RequestAsync("conrad", "/gpu", default); gpuError=null; return; }
                object body;
                if (command == "Heatwave") body = new { action = "heatwave", enabled = !heatwave };
                else
                {
                    var p = command.Split(':');
                    if (p.Length != 4 || !int.TryParse(p[1], out var manual) || manual is < 0 or > 1 || !int.TryParse(p[2], out var fan) || !int.TryParse(p[3], out var thermal)) return;
                    body = new { action = "apply", settings = new GpuControlRequest(manual == 1, fan, thermal) };
                }
                await RequestAsync("conrad", "/command", JsonSerializer.SerializeToElement(body, Json));
                controls = (GpuControlState)await RequestAsync("conrad", "/gpu", default);
            }
            catch (Exception e) { gpuError = e is InvalidOperationException ? e.Message : "Commande GPU indisponible."; }
            finally { if(writing) Interlocked.Exchange(ref writePending,0); }
        });
    }
}
