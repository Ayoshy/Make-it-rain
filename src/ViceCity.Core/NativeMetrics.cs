using System.Globalization;
using System.Text.Json;
using ConradSensor;
using CodexUsageTray;

namespace ViceCity;

internal sealed partial class Backend
{
    volatile GpuControlState? controls;
    int writePending;
    DateTimeOffset target = DateTimeOffset.Parse("2026-11-19T00:00:00+01:00", CultureInfo.InvariantCulture);
    bool targetValid=true;
    static string Number(double? n, string unit = "") => n is { } v && double.IsFinite(v) ? Math.Round(v).ToString(CultureInfo.InvariantCulture) + unit : "—";
    static string Count(long? n) => UsageFormatter.CompactNumber(n, AppLanguage.French);
    static string Money(decimal? n) => UsageFormatter.Dollars(n, AppLanguage.French);
    public string Metric(string key)
    {
        if(!targetValid && key is "days" or "hours" or "minutes" or "seconds" or "date") return "—";
        var s = sensors is { } current && DateTimeOffset.Now - current.FetchedAt < TimeSpan.FromSeconds(15) ? current : null;
        var usage = meter.Snapshot;
        var quota = usage?.Limits.FirstOrDefault(l => l.Id == "codex") ?? usage?.Limits.FirstOrDefault();
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
