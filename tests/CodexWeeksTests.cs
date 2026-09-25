using CodexUsageTray;

static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }

// Chemin réel : app-server Codex + sessions locales + historique. Lecture seule, aucun crédit.
if (args.Contains("--live"))
{
    await using var liveClient = new CodexAppServerClient();
    var live = await liveClient.ReadUsageAsync();
    live = live with { ApiEquivalent = await new ApiEquivalentEstimator().EstimateAsync(null) };
    var liveFile = Path.Combine(Path.GetTempPath(), "Battlestation-weeks-live.json");
    if (File.Exists(liveFile)) File.Delete(liveFile);
    var liveReport = new QuotaWeekStore(liveFile).Record(live);
    var liveNow = liveReport.Current;
    Check(liveNow is not null, "LIVE : la fenêtre en cours est lue");
    Check(liveNow!.UsedPercent is >= 0 and <= 100, "LIVE : le pourcentage consommé est lu");
    Check(liveNow.TokensPerHundredPercent is > 0, "LIVE : 100 % est estimé en tokens");
    Check(liveReport.Windows.Count >= 1 && liveReport.Windows[0].Current, "LIVE : la fenêtre en cours est la plus récente");
    Console.WriteLine($"LIVE used={liveNow.UsedPercent} reset={liveNow.ResetsAt:yyyy-MM-dd HH:mm} tokens100={liveNow.TokensPerHundredPercent:0} dollars100={liveNow.DollarsPerHundredPercent:0.00} initial={liveNow.InitialEstimate} measured={liveNow.MeasuredTokens} samples={liveNow.Samples} windows={liveReport.Windows.Count} models={liveReport.Models.Count}");
    File.Delete(liveFile);
    return;
}

var root = Path.Combine(Path.GetTempPath(), "Battlestation-weeks-" + Guid.NewGuid().ToString("N"));
var file = Path.Combine(root, "quota-weeks.json");
var start = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);
var reset = new DateTimeOffset(2026, 9, 24, 15, 0, 0, TimeSpan.Zero);
// Reset banked consommé le 21 : la fenêtre suivante démarre là, et raccourcit la précédente.
var banked = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);
var next = banked.AddDays(7);
try
{
    var store = new QuotaWeekStore(file);
    store.Record(Snapshot(start, reset, 10, ("gpt-6-astra", 1_000, 0m)));
    var report = store.Record(Snapshot(start.AddMinutes(15), reset, 20, ("gpt-6-astra", 21_000, 2m)));
    var current = report.Current!;
    Check(current.TokensPerHundredPercent is > 199_000 and < 201_000, "Un intervalle mono-modèle donne les tokens d'un quota plein");
    Check(current.DollarsPerHundredPercent is > 19.9m and < 20.1m, "Le même intervalle donne le prix API d'un quota plein");
    Check(current.UsedPercent == 20 && current.ObservedPercent == 10 && current.MeasuredTokens == 20_000, "Le quota consommé et les tokens mesurés sont conservés");
    Check(report.Models.Count == 1 && report.Models[0].Model == "gpt-6-astra" && report.Models[0].TokensPerHundredPercent is > 199_000 and < 201_000, "Le ratio est aussi mesuré par modèle");
    Check(!current.InitialEstimate && current.ChangePercent is null, "Aucune variation sans fenêtre précédente");

    // Intervalle mixte : compte dans la fenêtre, mais ni ratio par modèle ni prix inventé.
    report = store.Record(Snapshot(start.AddMinutes(30), reset, 25, ("gpt-6-astra", 31_000, 3m), ("gpt-unpriced", 21_000, null)));
    current = report.Current!;
    Check(current.MeasuredTokens == 51_000 && current.ObservedPercent == 15, "Un intervalle mixte alimente le total de la fenêtre");
    Check(report.Models.Count == 1, "Un intervalle mixte ne fabrique pas de ratio par modèle");
    Check(current.DollarsPerHundredPercent is > 33.9m and < 34.1m, "Le prix du quota extrapole la part tarifée et signale qu'elle est partielle");
    Check(current.PricedShare is > .58 and < .60, "La part tarifée est mesurée pour signaler un prix partiel");
    Check(current.Samples == 3, "Chaque relevé est compté");
    var isolated = new QuotaWeekStore(Path.Combine(root,"provider-isolation.json"));
    isolated.Record(Snapshot(start,reset,10,("gpt-6-astra",1_000,0m),("deepseek-flash",1_000,1m)));
    var separated=isolated.Record(Snapshot(start.AddMinutes(15),reset,20,("gpt-6-astra",21_000,2m),("deepseek-flash",9_000_000,100m),("unknown",1_000_000,null)));
    Check(separated.Current!.MeasuredTokens==20_000&&separated.Models.All(m=>m.Model=="gpt-6-astra"),"DeepSeek et modèles inconnus ne gonflent pas la valeur du quota Codex");

    // Compteurs locaux qui régressent (session supprimée) : rien n'est inventé.
    report = store.Record(Snapshot(start.AddMinutes(45), reset, 26, ("gpt-6-astra", 100, 0m)));
    Check(report.Current!.MeasuredTokens == 51_000, "Des compteurs qui reculent n'ajoutent rien");

    // Reset banked : nouvelle fenêtre, la précédente s'arrête au reset consommé.
    report = store.Record(Snapshot(banked, next, 2, ("gpt-6-astra", 31_100, 3.1m)));
    Check(report.Windows.Count == 2, "Un reset banked ouvre une nouvelle fenêtre");
    Check(report.Windows[0].Current && report.Windows[0].Start == banked && report.Windows[0].End == next, "La fenêtre en cours démarre au reset banked");
    Check(!report.Windows[1].Current && report.Windows[1].End == banked, "La fenêtre précédente s'arrête au reset banked, pas à sa date théorique");
    Check(report.Windows[1].TokensPerHundredPercent is > 339_000 and < 341_000, "La valeur de la fenêtre précédente reste mesurée");
    Check(report.Windows[0].MeasuredTokens == 0, "Une fenêtre neuve ne mesure encore rien");

    // Écart long : le total de la fenêtre est conservé, pas le ratio par modèle.
    report = store.Record(Snapshot(banked.AddHours(6), next, 12, ("gpt-6-astra", 61_100, 6.1m)));
    current = report.Current!;
    Check(current.MeasuredTokens == 30_000 && current.ObservedPercent == 10, "Un long écart garde le total de la fenêtre");
    Check(report.Models.Count == 1 && report.Models[0].TokensPerHundredPercent is > 199_000 and < 201_000, "Un long écart n'entre pas dans le ratio par modèle");

    // Dérive de quelques minutes : même fenêtre, et la variation se calcule.
    report = store.Record(Snapshot(banked.AddHours(7), next.AddMinutes(3), 20, ("gpt-6-astra", 81_100, 8.1m)));
    current = report.Current!;
    Check(current.MeasuredTokens == 50_000 && current.ObservedPercent == 18, "Une dérive de reset reste dans la même fenêtre");
    Check(current.TokensPerHundredPercent is > 276_000 and < 279_000 && current.DollarsPerHundredPercent is > 27.7m and < 27.9m, "Le prix et les tokens du quota suivent le second intervalle");
    Check(current.ChangePercent is > -19 and < -17, "La variation se compare à la fenêtre précédente");
    Check(report.Models.Count == 1 && report.Models[0].TokensPerHundredPercent is > 221_000 and < 223_000, "Le ratio par modèle cumule les intervalles mono-modèle");
    Check(report.RecordedSince >= start, "La date du premier relevé est conservée");

    var reopened = new QuotaWeekStore(file).Read();
    var reopenedCurrent = reopened.Current;
    Check(reopened.Windows.Count == 2 && reopenedCurrent is not null && reopenedCurrent.MeasuredTokens == 50_000, "L'historique survit au redémarrage");
    Check(reopenedCurrent?.DollarsPerHundredPercent is > 27.7m and < 27.9m, "Le prix du quota survit au redémarrage");

    // Sans intervalle mesuré : estimation initiale depuis les sessions locales, jamais présentée comme mesurée.
    var fresh = new QuotaWeekStore(Path.Combine(root, "fresh.json"));
    var initial = fresh.Record(Snapshot(start, reset, 20, ("gpt-6-astra", 40_000, 4m))).Current!;
    Check(initial.TokensPerHundredPercent is > 199_000 and < 201_000 && initial.InitialEstimate, "Sans intervalle mesuré, l'estimation initiale vient des sessions");
    Check(initial.MeasuredTokens == 0 && initial.DollarsPerHundredPercent is null, "L'estimation initiale ne se déclare ni mesurée ni tarifée");

    var empty = new QuotaWeekStore(Path.Combine(root, "empty.json")).Read();
    Check(empty.Windows.Count == 0 && empty.Current is null && empty.Models.Count == 0, "Un historique absent reste vide");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, true);
}

static UsageSnapshot Snapshot(DateTimeOffset at, DateTimeOffset resetsAt, double used, params (string Model, long Tokens, decimal? Dollars)[] models)
{
    RateLimitWindow Window() => new() { UsedPercent = used, WindowDurationMins = 10080, ResetsAt = resetsAt.ToUnixTimeSeconds() };
    var response = new GetAccountRateLimitsResponse
    {
        RateLimits = new RateLimitSnapshot { LimitId = "codex", Primary = Window() },
        RateLimitsByLimitId = new Dictionary<string, RateLimitSnapshot>
        {
            ["codex"] = new() { LimitId = "codex", Primary = Window() }
        }
    };
    var estimate = new ApiEquivalentEstimate(
        DollarAmount: models.Sum(model => model.Dollars ?? 0m),
        TodayDollarAmount: 0m,
        ParsedTokens: models.Sum(model => model.Tokens),
        TodayTokens: 0,
        ParsedSessions: 1,
        ScaleFactor: 1d,
        UsesProxyPricing: false,
        UnknownModels: [],
        Models: models.Select(model => new ModelUsageBreakdown(
            model.Model, "high", model.Tokens, 0, 0, model.Tokens, 1, model.Dollars, 100d)).ToArray(),
        DailyUsage: models.Select(model => new DailyModelTokens(DateOnly.FromDateTime(at.UtcDateTime), model.Model, model.Tokens)).ToArray());
    return new UsageSnapshot(at, response, null, null, estimate);
}
