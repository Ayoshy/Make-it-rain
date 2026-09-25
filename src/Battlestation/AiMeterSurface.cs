using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace Battlestation;

// AI Meter uses the same retained pages, glass, buttons and visibility lifecycle as the hardware dock.
internal sealed partial class DashboardSurface
{
    string costFamily = "openai", costPeriod = "total";
    bool claudeQuota, costBreakdown;
    string Cost(string field) => Station.M($"ai:{costFamily}:{costPeriod}:{field}");
    int CostCount => int.TryParse(Cost("count"), out int n) ? Math.Max(0, n) : 0;
    // Le gabarit ample sert quand le dock est assez haut ; les profils compacts gardent la densité d'origine.
    bool AiRoomy => body.Height >= 220;
    const string AiSoft = "#CBBEDA";
    double QuotaPitch => AiRoomy ? 52 : 36;
    double CostPitch => AiRoomy ? 30 : 25;
    int CostCapacity => Math.Max(1, (int)((body.Height - (AiRoomy ? 98 : 86)) / CostPitch));
    internal object InspectAi() => new
    {
        page = pages.Requested, costFamily, costPeriod, localStatus = Station.M("aiStatus"), localError = Station.M("aiError"),
        liquid = new { codex = LiquidPercent(liquids[0]), claude = LiquidPercent(liquids[1]), animating = liquidHooked },
        codex = new { remaining = Station.M("remaining"), status = Station.M("codexStatus"), value = Station.M("weekValue") },
        deepseek = new { balance = Station.M("deepseekTotal"), currency = Station.M("deepseekCurrency"), status = Station.M("deepseekStatus") },
        // Le quota Claude est relu depuis les lignes réellement dessinées, pas d'une copie.
        claude = new
        {
            linked = Station.M("claudeWindowCount") is not ("" or "0"),
            plan = Station.M("claudePlan"), status = Station.M("claudeStatus"), error = Station.M("claudeError"),
            remaining = Station.M("claudeRemaining"), label = Station.M("claudeWindowLabel"),
            weekly = Station.M("claudeWeekly"), reset = Station.M("claudeReset"), cardLine = Station.M("claudeResetLine"),
            spend = Station.M("claudeSpend"),
            windows = ClaudeQuotaRows().Where(row => row.Duration != ClaudeCreditLabel)
                .Select(row => new { label = row.Duration, remaining = row.Remaining, detail = row.Reset }).ToArray(),
            credits = ClaudeQuotaRows().Where(row => row.Duration == ClaudeCreditLabel)
                .Select(row => new { label = row.Duration, remaining = row.Remaining, detail = row.Reset }).ToArray()
        },
        families = new[] { "openai", "deepseek", "claude", "unknown" }.Select(family => new
        {
            family, count = Station.M($"ai:{family}:total:count"), tokens = Station.M($"ai:{family}:total:tokens"),
            cost = Station.M($"ai:{family}:total:cost"), today = Station.M($"ai:{family}:today:tokens")
        }).ToArray()
    };
    readonly QuotaLiquid[] liquids = [new(), new()];
    static double? LiquidPercent(QuotaLiquid liquid) => double.IsFinite(liquid.Level) ? Math.Round(liquid.Level * 100, 1) : null;
    TimeSpan liquidFrame;
    bool liquidHooked;
    bool WakeLiquids()
    {
        if (liquidHooked) return true;
        if (sensors || !IsVisible) return false;
        liquidHooked = true; liquidFrame = default;
        CompositionTarget.Rendering += LiquidFrame;
        return true;
    }
    void SleepLiquids(bool settle)
    {
        if (liquidHooked) { CompositionTarget.Rendering -= LiquidFrame; liquidHooked = false; }
        if (settle) foreach (var liquid in liquids) liquid.Settle();
    }
    void LiquidFrame(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs frame || frame.RenderingTime == liquidFrame) return;
        double elapsed = liquidFrame == default ? 1 / 60d : (frame.RenderingTime - liquidFrame).TotalSeconds;
        liquidFrame = frame.RenderingTime;
        if (!IsVisible || pages.Current != 0 && pages.Requested != 0) { SleepLiquids(true); return; }
        bool awake = false;
        foreach (var liquid in liquids) awake |= liquid.Step(elapsed);
        if (!awake) SleepLiquids(false);
    }
    void StirLiquids(Point pointer)
    {
        if (sensors || pages.Current != 0 || pages.Running) return;
        long now = Environment.TickCount64;
        pointer.Offset(-body.X, -body.Y);
        foreach (var liquid in liquids) liquid.Stir(pointer, now);
        if (liquids.Any(liquid => liquid.Awake) && !WakeLiquids()) SleepLiquids(true);
    }
    void OpenAiPage(int page)
    {
        if (pages.Running) return;
        if (page == 0) { pages.Settle(0); Refresh(); }
        else if (pages.Requested != page) pages.Toggle(page);
    }
    void AiChoice(string name, string label, double x, double y, double width, bool selected, Action action)
    {
        if (selected) Box(x, y, width, 24, "#26DBC5ED", "#72E7D5FA", DockAppearance.ButtonRadius);
        PageButton(name, label, new Rect(x, y, width, 24), action);
    }
    void AiSummary()
    {
        double col = (body.Width - 20) / 3, height = body.Height;
        for (int i = 0; i < 3; i++)
        {
            double x = i * (col + 10);
            string name = i switch { 0 => "Codex", 1 => "DeepSeek", _ => "Claude" };
            string icon = i switch { 0 => "codex", 1 => "deepseek", _ => "claudecode" };
            var rect = new Rect(x, 0, col, height);
            Box(x, 0, col, height, "#1971508A", "#40C6AEDF", 12);
            if (i != 1)
            {
                // Le quota restant remplit la carte comme un verre, sous le texte.
                var liquid = liquids[i / 2];
                bool filled = i == 0 || Station.M("claudeWindowCount") is not ("" or "0");
                liquid.Layout(rect, 12, filled ? Station.N(i == 0 ? "remaining" : "claudeRemaining") : double.NaN,
                    DesktopTheme.Color(i == 0 ? "#C8A6EC" : "#E9B49C"));
                D.DrawDrawing(liquid.Drawing);
                if (liquid.Awake && !WakeLiquids()) liquid.Settle();
            }
            if (interactive) HoverGlass(rect, 12);
            bool linked = Station.M("claudeWindowCount") is not ("" or "0");
            string plan = i == 0 ? Station.M("codexStatus") : i == 1 ? "" : linked ? Station.M("claudePlan") : "Non connecté";
            string currency = Station.M("deepseekCurrency"), balance = Station.M("deepseekTotal");
            if (balance != "—" && currency == "USD") balance += " $";
            string value = i == 0 ? Station.M("remaining") : i == 1 ? balance : linked ? Station.M("claudeRemaining") : "—";
            string weekly = i == 2 && linked ? Station.M("claudeWeekly") : "";
            string label = i == 0 ? "restants · " + Station.M("quotaLabel").ToLowerInvariant()
                : i == 1 ? (currency is "USD" or "—" ? "solde" : "solde · " + currency)
                : linked ? "restants · " + Station.M("claudeWindowLabel").ToLowerInvariant()
                : "À connecter";
            string footer = i == 0 ? Station.M("reset") : i == 1 ? Station.M("deepseekStatus") : linked ? Station.M("claudeResetLine") : "Aucun relevé de quota";
            string valueColor = i == 2 && !linked ? Muted : "#F1E6FF";
            string iconPath = Path.Combine(Station.Root, "dock", "icons", "neon", icon + ".png");
            if (AiRoomy && col >= 190)
            {
                // Grande carte : le chiffre domine, les lignes utiles restent lisibles à distance.
                double inner = col - 28;
                Image(iconPath, x + 14, 14, 36, 36);
                Text(name, x + 60, plan == "" ? 22 : 13, 13, bold: true, width: col - 72);
                if (plan != "") Text(plan, x + 60, 35, 9.5, AiSoft, width: col - 72);
                double size = value.Length <= 6 ? 40 : value.Length <= 8 ? 34 : 28;
                Text(value, x + 14, 78, size, valueColor, DockAppearance.NumberFont, width: inner);
                Text(label, x + 14, 138, 10.5, AiSoft, width: inner);
                if (weekly != "") Text(weekly, x + 14, 158, 10.5, AiSoft, width: inner);
                Text(footer, x + 14, height - 30, 10, Ink, width: inner);
            }
            else
            {
                Image(iconPath, x + 8, 7, 24, 24);
                Text(name, x + 38, plan == "" ? 12 : 6, 10, bold: true, width: col - 44);
                if (plan != "") Text(plan, x + 38, 22, 8, AiSoft, width: col - 44);
                Text(value, x + 10, 40, col < 150 ? 18 : 23, valueColor, DockAppearance.NumberFont, width: col - 20);
                if (height >= 94) Text(label + (weekly == "" ? "" : " · " + weekly), x + 10, 74, 8.5, AiSoft, width: col - 20);
                if (height >= 111) Text(footer, x + 10, height - 19, 8, AiSoft, width: col - 20);
            }
            int provider = i;
            if (interactive) Hit("Provider:" + name, x, 0, col, height, () =>
            {
                if (pages.Running) return;
                if (provider == 0) { claudeQuota = false; OpenAiPage(3); }
                else { costFamily = provider == 1 ? "deepseek" : "claude"; modelOffset = 0; OpenAiPage(4); }
            });
        }
    }
    void AiQuotas()
    {
        double w = body.Width, chip = Math.Min(115, (w - 16) / 3);
        AiChoice("QuotaCodex", "CODEX", 0, 0, chip, !claudeQuota, () => { claudeQuota = false; quotaOffset = 0; Refresh(); });
        AiChoice("QuotaClaude", "CLAUDE", chip + 8, 0, chip, claudeQuota, () => { claudeQuota = true; Refresh(); });
        if (!claudeQuota) PageButton("Week", "100 % ≈", new Rect(w - chip, 0, chip, 24), () => OpenAiPage(6));
        var rows = claudeQuota ? ClaudeQuotaRows() : QuotaRows();
        int capacity = QuotaCapacity;
        quotaOffset = Math.Clamp(quotaOffset, 0, Math.Max(0, rows.Count - capacity));
        bool roomy = AiRoomy;
        if (rows.Count == 0)
        {
            Text(claudeQuota ? "CLAUDE · À CONNECTER" : "Quotas indisponibles", 0, 42, roomy ? 13 : 11, bold: claudeQuota, width: w);
            if (claudeQuota) Text("Aucun quota reçu pour ce compte", 0, roomy ? 72 : 69, roomy ? 10.5 : 9, AiSoft, width: w);
            return;
        }
        double top = roomy ? 34 : 32, pitch = QuotaPitch, box = pitch - 4;
        double title = roomy ? 11 : 9.5, detail = roomy ? 9.5 : 8;
        for (int i = 0; i < capacity && quotaOffset + i < rows.Count; i++)
        {
            var row = rows[quotaOffset + i]; double y = top + i * pitch;
            string color = row.IsReserve ? "#AEB8F4" : row.Name == "Claude" ? "#E9B49C" : "#C8A6EC";
            Box(0, y, w - 8, box, row.IsReserve ? "#2F3569C0" : "#1871508A", radius: 8);
            Text(row.Name + " · " + row.Duration, 10, y + (roomy ? 6 : 2), title, bold: true, width: w - 160);
            var match = Regex.Match(row.Remaining, @"(\d+)%");
            double remaining = match.Success ? double.Parse(match.Groups[1].Value) : double.NaN;
            Text(double.IsFinite(remaining) ? remaining + " % restants" : "Indisponible", w - 18, y + (roomy ? 6 : 2), title, color, bold: true, align: "right");
            Text(row.Reset, 10, y + (roomy ? 25 : 17), detail, AiSoft, width: w - 30);
            Track(10, y + box - (roomy ? 5 : 2), w - 38, remaining, color, roomy ? 3 : 2);
        }
        if (body.Height >= 115)
            Text(claudeQuota ? Station.M("claudeSpend") : Station.M("credits"), 0, body.Height - (roomy ? 18 : 14), roomy ? 9.5 : 8, AiSoft, width: w - 10);
        ScrollMark(rows.Count, capacity, quotaOffset, top, capacity * pitch - 5);
    }
    void AiCosts()
    {
        double w = body.Width, h = body.Height;
        bool roomy = AiRoomy;
        string[] families = ["openai", "deepseek", "claude", "unknown"];
        string[] labels = ["OPENAI", "DEEPSEEK", "CLAUDE", "AUTRES"];
        double periodW = w < 500 ? 66 : 92, chip = (w - periodW - 32) / 4;
        for (int i = 0; i < families.Length; i++)
        {
            string family = families[i];
            AiChoice("Cost:" + family, labels[i], i * (chip + 6), 0, chip, costFamily == family,
                () => { costFamily = family; modelOffset = 0; Refresh(); });
        }
        PageButton("CostPeriod", costPeriod == "total" ? "CUMUL ▾" : "JOUR ▾", new Rect(w - periodW, 0, periodW, 24),
            () => { costPeriod = costPeriod == "total" ? "today" : "total"; modelOffset = 0; Refresh(); });
        if (costFamily == "claude" && CostCount == 0)
        {
            bool linked = Station.M("claudeWindowCount") is not ("" or "0");
            Text(linked ? "CLAUDE · AUCUN COMPTEUR LOCAL" : "CLAUDE · À CONNECTER", 0, 42, roomy ? 13 : 11, bold: true);
            Text(linked ? "Quota lu · aucune session Claude locale comptée" : "Aucun quota ni compteur Claude local", 0, roomy ? 72 : 69, roomy ? 10.5 : 9, AiSoft, width: w);
            return;
        }
        string price = Cost("cost"), priceLabel = costFamily == "openai" ? "ÉQUIVALENT API" : "COÛT API ESTIMÉ";
        if (costBreakdown)
        {
            PageButton("CostBack", "‹ MODÈLES", new Rect(0, 31, 105, 24), () => { costBreakdown = false; Refresh(); });
            string[] fields = ["input", "cached", "output"], titles = ["ENTRÉE HORS CACHE", "ENTRÉE EN CACHE", "SORTIE¹"];
            // L'écriture de cache n'existe que chez les fournisseurs qui la publient.
            if (Cost("write") is not ("" or "—"))
            {
                fields = ["input", "cached", "write", "output"];
                titles = ["ENTRÉE HORS CACHE", "ENTRÉE EN CACHE", "ÉCRITURE DE CACHE", "SORTIE¹"];
            }
            double column = w / fields.Length;
            for (int i = 0; i < fields.Length; i++)
            {
                Text(titles[i], i * column, roomy ? 76 : 65, roomy ? 9 : 7.5, AiSoft, width: column - 8);
                Text(Cost(fields[i]), i * column, roomy ? 96 : 80, roomy ? 22 : 15, font: DockAppearance.NumberFont, width: column - 8);
            }
            if (h >= 130) Text("¹ Raisonnement inclus si fourni · Compteurs locaux", 0, h - (roomy ? 18 : 14), roomy ? 9.5 : 8, AiSoft, width: w);
            return;
        }
        PageButton("CostTokens", Cost("tokens") + " TOKENS ▸", new Rect(0, 31, w * .4, 24), () => { costBreakdown = true; Refresh(); });
        Text((price == "—" ? "—" : "≈ " + price) + " · " + priceLabel, w - 8, roomy ? 34 : 36, roomy ? 11 : 9, "#B5E6CB", align: "right", bold: roomy, width: w * .55);
        bool wide = w >= 520;
        string source = costFamily switch
        {
            "claude" => "SESSIONS CLAUDE LOCALES",
            "deepseek" => "SESSIONS DEEPSEEK LOCALES",
            "unknown" => "SESSIONS LOCALES",
            _ => "SESSIONS CODEX LOCALES"
        };
        double head = roomy ? 8.5 : 7.5, cell = roomy ? 10 : 8.5, top = roomy ? 82 : 72, pitch = CostPitch, headY = roomy ? 62 : 57;
        Text("MODÈLE · " + source, 8, headY, head, AiSoft, width: w * .55);
        if (wide) Text("CACHE", w * .63, headY, head, AiSoft, align: "right");
        Text("TOKENS", w * .79, headY, head, AiSoft, align: "right");
        Text("ESTIMÉ", w - 18, headY, head, AiSoft, align: "right");
        int count = CostCapacity, total = CostCount;
        modelOffset = Math.Clamp(modelOffset, 0, Math.Max(0, total - count));
        if (total == 0) Text("Aucun compteur local", 8, top + 10, cell, AiSoft, width: w - 20);
        for (int i = 0; i < count && modelOffset + i < total; i++)
        {
            double y = top + i * pitch, row = y + (roomy ? 5 : 4); string key = (modelOffset + i) + ":";
            Box(0, y, w - 8, pitch - 2, i % 2 == 0 ? "#18B483D8" : "#08B483D8", radius: 6);
            string effort = Cost(key + "effort");
            Text(Cost(key + "name") + (effort == "" ? "" : " · " + effort), 8, row, cell, bold: true, width: w * (wide ? .48 : .54) - 10);
            if (wide) Text(Cost(key + "cache"), w * .63, row, cell, AiSoft, align: "right");
            Text(Cost(key + "tokens"), w * .79, row, cell + 1, font: DockAppearance.NumberFont, align: "right");
            Text(Cost(key + "cost"), w - 18, row, cell, "#B5E6CB", align: "right");
        }
        string note = Station.M("aiError");
        if (note is "" or "—") note = Cost("coverage") + " · pas une facture · Kilo / Shopping non couverts";
        Text(note, 0, h - (roomy ? 18 : 14), roomy ? 9.5 : 8, AiSoft, width: w - 10);
        ScrollMark(total, count, modelOffset, top, count * pitch - 2);
    }
}
