using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ConradSensor;
using CodexUsageTray;

namespace Battlestation.Core;

internal sealed partial class Backend : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    readonly CancellationTokenSource shutdown = new();
    readonly SemaphoreSlim refresh = new(0, 1), gpuGate = new(1, 1);
    readonly NativeGpuControlService gpu = new();
    readonly Task sensorsTask, codexTask;
    readonly string directory;
    volatile TemperatureSnapshot? sensors;
    volatile MeterState meter = new(null, false, null, 15);
    volatile string? hardwareError, gpuError;
    volatile bool gpuBusy, heatwave;
    public string Display => $"CPU {SensorFormatting.Temperature(sensors?.CpuPackageCelsius)}  ·  GPU {SensorFormatting.Temperature(sensors?.GpuCelsius)}\nCodex {QuotaText()}";
    public Backend(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        sensorsTask = Task.Run(ReadSensors);
        codexTask = Task.Run(ReadCodex);
    }
    string QuotaText()
    {
        var value = meter.Snapshot?.Limits.FirstOrDefault()?.Primary?.UsedPercent;
        return value is { } used && double.IsFinite(used)
            ? Math.Clamp(100 - used, 0, 100).ToString("0", CultureInfo.InvariantCulture) + "%" + (meter.Error is null ? "" : " · périmé") : "indisponible";
    }
    async Task ReadSensors()
    {
        using var hardware = new HardwareSensorReader();
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                try { sensors = hardware.Read(); hardwareError = null; }
                catch (Exception e) { sensors = null; hardwareError = e.GetType().Name; }
                var state = new { processId = Environment.ProcessId, runtime = Environment.Version.ToString(), sampledAt = DateTimeOffset.Now, sensors, hardwareError, codex = meter, gpuControl = controls, heatwaveActive = heatwave, gpuError, transport = "in-process + app-server stdio" };
                var file = Path.Combine(directory, "probe.json");
                File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(state, Json));
                File.Move(file + ".tmp", file, true);
                await Task.Delay(2000, shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        catch (Exception e) { hardwareError = e.GetType().Name; }
    }
    async Task ReadCodex()
    {
        await using var client = new CodexAppServerClient();
        var estimator = new ApiEquivalentEstimator(cachePath: Path.Combine(directory, "api-equivalent-cache-v1.json"));
        try { do { await UpdateCodex(client, estimator); } while (await refresh.WaitAsync(TimeSpan.FromMinutes(15), shutdown.Token) || !shutdown.IsCancellationRequested); }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    }
    async Task UpdateCodex(CodexAppServerClient client, ApiEquivalentEstimator estimator)
    {
        meter = meter with { Refreshing = true, Error = null };
        try
        {
            var usage = await client.ReadUsageAsync(shutdown.Token);
            meter = new(MeterUsage.From(usage), true, null, 15);
            try { usage = usage with { ApiEquivalent = await estimator.EstimateAsync(null, shutdown.Token) }; }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { throw; }
            catch { meter = meter with { Error = "Estimation locale indisponible." }; }
            meter = meter with { Snapshot = MeterUsage.From(usage), Refreshing = false };
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { throw; }
        catch { meter = meter with { Refreshing = false, Error = "Codex indisponible. Dernière mesure conservée." }; }
    }
    public void RequestRefresh() { if (refresh.CurrentCount == 0 && !meter.Refreshing) { try { refresh.Release(); } catch (SemaphoreFullException) { } } }
    public async Task<object> RequestAsync(string channel, string path, JsonElement body)
    {
        if (channel == "codex")
        {
            if (path == "/refresh") RequestRefresh();
            else if (path != "/state") throw new InvalidOperationException("Lecture inconnue.");
            return meter;
        }
        if (channel != "conrad") throw new InvalidOperationException("Canal inconnu.");
        if (path == "/state") return new { snapshot = sensors, heatwaveActive = heatwave, busy = gpuBusy, error = gpuError ?? hardwareError };
        await gpuGate.WaitAsync(shutdown.Token);
        try
        {
            if (path == "/gpu") return await Task.Run(gpu.ReadState);
            if (path != "/command") throw new InvalidOperationException("Commande inconnue.");
            if (Process.GetProcessesByName("ConradSensor").Length != 0)
                throw new InvalidOperationException("Contrôle GPU réservé à Conrad pendant la migration.");
            gpuBusy = true;
            try
            {
                var action = body.GetProperty("action").GetString();
                if (action == "heatwave")
                {
                    var enabled = body.GetProperty("enabled").GetBoolean();
                    await Task.Run(() => gpu.SetHeatwaveMode(enabled)); heatwave = enabled;
                }
                else if (action == "apply")
                {
                    var request = body.GetProperty("settings").Deserialize<GpuControlRequest>(Json) ?? throw new InvalidOperationException();
                    await Task.Run(() => gpu.Apply(request)); heatwave = false;
                }
                else throw new InvalidOperationException("Commande inconnue.");
                gpuError = null;
            }
            catch { gpuError = "Réglage GPU non appliqué."; throw; }
            finally { gpuBusy = false; }
            return new { snapshot = sensors, heatwaveActive = heatwave, busy = false, error = gpuError };
        }
        finally { gpuGate.Release(); }
    }
    public void Dispose()
    {
        shutdown.Cancel();
        try { Task.WaitAll([sensorsTask, codexTask], TimeSpan.FromSeconds(8)); } catch { }
        gpu.Dispose();
    }
}
internal sealed record MeterLimit(string Id, string Name, RateLimitWindow? Primary, RateLimitWindow? Secondary);
internal sealed record MeterState(MeterUsage? Snapshot, bool Refreshing, string? Error, int RefreshIntervalMinutes);
internal sealed record MeterUsage(DateTimeOffset FetchedAt, IReadOnlyList<MeterLimit> Limits,
    long? TodayTokens, long? LifetimeTokens, long? ResetCredits, decimal? TodayApiDollars,
    decimal? TotalApiDollars, bool EstimatedPricing, IReadOnlyList<ModelUsageBreakdown> Models, string TodayTokensSource)
{
    public static MeterUsage From(UsageSnapshot s)
    {
        var today = s.TokenUsage?.DailyUsageBuckets?.FirstOrDefault(b => b.StartDate.StartsWith(s.FetchedAt.ToString("yyyy-MM-dd"), StringComparison.Ordinal))?.Tokens;
        return new(s.FetchedAt, UsageFormatter.OrderedLimits(s.RateLimitResponse).Select(p => new MeterLimit(p.Key, p.Value.LimitName ?? p.Key, p.Value.Primary, p.Value.Secondary)).ToArray(),
            today ?? s.ApiEquivalent?.TodayTokens, s.TokenUsage?.Summary.LifetimeTokens ?? s.ApiEquivalent?.ParsedTokens, s.RateLimitResponse.RateLimitResetCredits?.AvailableCount,
            s.ApiEquivalent?.TodayDollarAmount, s.ApiEquivalent?.DollarAmount,
            s.ApiEquivalent?.UnknownModels.Count > 0, s.ApiEquivalent?.Models ?? [], today.HasValue ? "Codex" : s.ApiEquivalent?.TodayTokens is not null ? "Compteurs locaux du jour" : "Indisponible");
    }
}
