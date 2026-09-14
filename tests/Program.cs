using System.Text.Json;
using ConradSensor;
using CodexUsageTray;
using Battlestation.Core;

int checks = 0;
void Check(bool passed, string name) { if (!passed) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var missing = JsonSerializer.Deserialize<RateLimitWindow>("{}")!;
Check(missing.UsedPercent is null, "missing percentage remains unknown");
Check(JsonSerializer.Deserialize<RateLimitResetCreditsSummary>("{}")!.AvailableCount is null, "missing reset count remains unknown");
var usage = new UsageSnapshot(DateTimeOffset.Now, new(), null, null);
var projected = MeterUsage.From(usage);
Check(projected.TodayTokens is null && projected.LifetimeTokens is null && projected.ResetCredits is null, "absent account data does not become zero");
Check(projected.Limits[0].Primary is null && projected.Limits[0].Secondary is null, "no fabricated quota windows");
var state = new GpuControlState(true, true, true, true, 42, 38, 100, 83, 65, 88, "fixture");
var low = NativeGpuControlService.NormalizeRequest(new(true, -999, -999), state);
var high = NativeGpuControlService.NormalizeRequest(new(true, 999, 999), state);
Check(low.FanPercent == 38 && low.ThermalLimitCelsius == 65, "GPU lower hardware limits");
Check(high.FanPercent == 100 && high.ThermalLimitCelsius == 88, "GPU upper hardware limits");
var heat = NativeGpuControlService.CreateHeatwaveRequest(state);
Check(heat.FanManual && heat.FanPercent == 100 && heat.ThermalLimitCelsius == 65, "heatwave uses reported hardware limits");
var folder = Path.Combine(Path.GetTempPath(), "Battlestation-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(folder, "sessions"));
var fixture = Path.Combine(folder, "sessions", "rollout-2026-09-13-test.jsonl");
await File.WriteAllTextAsync(fixture,
    "{\"type\":\"turn_context\",\"payload\":{\"model\":\"unknown-model\",\"effort\":\"high\"}}\n" +
    "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"input_tokens\":100,\"cached_input_tokens\":10,\"output_tokens\":20,\"total_tokens\":120}}}}\n");
var estimate = await new ApiEquivalentEstimator(folder, Path.Combine(folder, "cache.json")).EstimateAsync(999999);
Check(estimate is { Models.Count: 1, DollarAmount: null }, "unknown-only history retains models without inventing cost");
Check(estimate!.Models[0].TotalTokens == 120 && estimate.Models[0].DollarAmount is null, "unknown model tokens retained");
Check(estimate.TodayTokens is null, "missing daily timestamps do not become zero");
Check(estimate.ScaleFactor == 1, "no extrapolation to unpriced account tokens");
Check(ApiEquivalentEstimator.CalculateCost("unknown-model", 10, 0, 2) is null, "unknown tariff is nullable");
var yesterday = DateTimeOffset.Now.Date.AddDays(-1).AddHours(23).ToString("o");
var current = DateTimeOffset.Now.ToString("o");
await File.WriteAllTextAsync(fixture,
    "{\"type\":\"turn_context\",\"payload\":{\"model\":\"unknown-model\"}}\n" +
    JsonSerializer.Serialize(new { timestamp = yesterday, type = "event_msg", payload = new { type = "token_count", info = new { total_token_usage = new { input_tokens = 100, cached_input_tokens = 10, output_tokens = 20, total_tokens = 120 } } } }) + "\n" +
    JsonSerializer.Serialize(new { timestamp = current, type = "event_msg", payload = new { type = "token_count", info = new { total_token_usage = new { input_tokens = 150, cached_input_tokens = 10, output_tokens = 30, total_tokens = 180 } } } }) + "\n");
var daily = await new ApiEquivalentEstimator(folder, Path.Combine(folder, "daily-cache.json")).EstimateAsync(null);
Check(daily?.TodayTokens == 60 && daily.ParsedTokens == 180, "session across midnight splits daily token deltas");
Console.WriteLine($"{checks} checks passed; no hardware writes or account requests.");
