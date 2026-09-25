using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

namespace Battlestation;

// AI Meter uses the same retained pages, glass, buttons and visibility lifecycle as the hardware dock.
internal sealed partial class DashboardSurface
{
    string costFamily = "openai", costPeriod = "total";
    bool claudeQuota, costBreakdown;
    string Cost(string field) => Station.M($"ai:{costFamily}:{costPeriod}:{field}");
    int CostCount => int.TryParse(Cost("count"), out int n) ? Math.Max(0, n) : 0;
    int CostCapacity => Math.Max(1, (int)((body.Height - 86) / 25));
    internal object InspectAi() => new
    {
        page = pages.Requested, costFamily, costPeriod, localStatus = Station.M("aiStatus"), localError = Station.M("aiError"),
        codex = new { remaining = Station.M("remaining"), status = Station.M("codexStatus"), value = Station.M("weekValue") },
        deepseek = new { balance = Station.M("deepseekTotal"), currency = Station.M("deepseekCurrency"), status = Station.M("deepseekStatus") },
        families = new[] { "openai", "deepseek", "claude", "unknown" }.Select(family => new
        {
            family, count = Station.M($"ai:{family}:total:count"), tokens = Station.M($"ai:{family}:total:tokens"),
            cost = Station.M($"ai:{family}:total:cost"), today = Station.M($"ai:{family}:today:tokens")
        }).ToArray()
    };
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
            if (interactive) HoverGlass(rect, 12);
            Image(Path.Combine(Station.Root, "dock", "icons", "neon", icon + ".png"), x + 8, 7, 24, 24);
            Text(name, x + 38, 7, 9, bold: true, width: col - 44);
            bool linked = Station.M("claudeWindowCount") is not ("" or "0");
            Text(i == 0 ? Station.M("codexStatus") : i == 1 ? "Compte API" : linked ? Station.M("claudePlan") : "Non connecté",
                x + 38, 23, 7, Muted, width: col - 44);
            string value = i == 0 ? Station.M("remaining") : i == 1 ? Station.M("deepseekTotal") : linked ? Station.M("claudeRemaining") : "—";
            Text(value, x + 10, 41, col < 150 ? 18 : 23, i == 2 && !linked ? Muted : "#E7D5FA", DockAppearance.NumberFont, width: col - 20);
            string label = i == 0 ? "restants · " + Station.M("quotaLabel").ToLowerInvariant()
                : i == 1 ? "solde · " + Station.M("deepseekCurrency")
                : linked ? "restants · " + Station.M("claudeWindowLabel").ToLowerInvariant() : "À connecter";
            if (height >= 94) Text(label, x + 10, 75, 7.5, Muted, width: col - 20);
            if (height >= 111)
            {
                if (i == 0) Track(x + 10, 96, col - 20, Station.N("remaining"), "#C8A6EC", 3);
                else if (i == 2 && linked) Track(x + 10, 96, col - 20, Station.N("claudeRemaining"), "#E9B49C", 3);
                Text(i == 0 ? Station.M("reset") : i == 1 ? Station.M("deepseekStatus") : linked ? Station.M("claudeReset") : "Aucun relevé de quota",
                    x + 10, height - 18, 7, Muted, width: col - 20);
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
        if (rows.Count == 0)
        {
            Text(claudeQuota ? "CLAUDE · À CONNECTER" : "Quotas indisponibles", 0, 42, claudeQuota ? 11 : 10, bold: claudeQuota, width: w);
            if (claudeQuota) Text("Aucun quota reçu pour ce compte", 0, 69, 9, Muted, width: w);
            return;
        }
        for (int i = 0; i < capacity && quotaOffset + i < rows.Count; i++)
        {
            var row = rows[quotaOffset + i]; double y = 32 + i * 36;
            string color = row.IsReserve ? "#AEB8F4" : row.Name == "Claude" ? "#E9B49C" : "#C8A6EC";
            Box(0, y, w - 8, 32, row.IsReserve ? "#2F3569C0" : "#1871508A", radius: 8);
            Text(row.Name + " · " + row.Duration, 10, y + 3, 8.5, bold: true, width: w - 125);
            var match = Regex.Match(row.Remaining, @"(\d+)%");
            double remaining = match.Success ? double.Parse(match.Groups[1].Value) : double.NaN;
            Text(double.IsFinite(remaining) ? remaining + " % restants" : "Indisponible", w - 18, y + 3, 8.5, color, align: "right");
            Text("Reset " + row.Reset, 10, y + 17, 7, Muted, width: w - 30);
            Track(10, y + 31, w - 38, remaining, color, 2);
        }
        if (body.Height >= 115) Text(claudeQuota ? Station.M("claudeSpend") : Station.M("credits"), 0, body.Height - 13, 7, Muted, width: w - 10);
        ScrollMark(rows.Count, capacity, quotaOffset, 32, capacity * 36 - 5);
    }
    void AiCosts()
    {
        double w = body.Width, h = body.Height;
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
            Text(linked ? "CLAUDE · AUCUN COMPTEUR LOCAL" : "CLAUDE · À CONNECTER", 0, 42, 11, bold: true);
            Text(linked ? "Quota lu · aucune session Claude locale comptée" : "Aucun quota ni compteur Claude local", 0, 69, 8, Muted, width: w);
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
                Text(titles[i], i * column, 65, 7, Muted, width: column - 8);
                Text(Cost(fields[i]), i * column, 80, 15, font: DockAppearance.NumberFont, width: column - 8);
            }
            if (h >= 130) Text("¹ Raisonnement inclus si fourni · Compteurs locaux", 0, h - 13, 7, Muted, width: w);
            return;
        }
        PageButton("CostTokens", Cost("tokens") + " TOKENS ▸", new Rect(0, 31, w * .4, 24), () => { costBreakdown = true; Refresh(); });
        Text((price == "—" ? "—" : "≈ " + price) + " · " + priceLabel, w - 8, 36, 9, "#B5E6CB", align: "right", width: w * .55);
        bool wide = w >= 520;
        string source = costFamily switch
        {
            "claude" => "SESSIONS CLAUDE LOCALES",
            "deepseek" => "SESSIONS DEEPSEEK LOCALES",
            "unknown" => "SESSIONS LOCALES",
            _ => "SESSIONS CODEX LOCALES"
        };
        Text("MODÈLE · " + source, 8, 57, 7, Muted, width: w * .55);
        if (wide) Text("CACHE", w * .63, 57, 7, Muted, align: "right");
        Text("TOKENS", w * .79, 57, 7, Muted, align: "right");
        Text("ESTIMÉ", w - 18, 57, 7, Muted, align: "right");
        int count = CostCapacity, total = CostCount;
        modelOffset = Math.Clamp(modelOffset, 0, Math.Max(0, total - count));
        if (total == 0) Text("Aucun compteur local · Kilo et Shopping non couverts", 8, 82, 8, Muted, width: w - 20);
        for (int i = 0; i < count && modelOffset + i < total; i++)
        {
            double y = 72 + i * 25; string key = (modelOffset + i) + ":";
            Box(0, y, w - 8, 23, i % 2 == 0 ? "#18B483D8" : "#08B483D8", radius: 6);
            string effort = Cost(key + "effort");
            Text(Cost(key + "name") + (effort == "" ? "" : " · " + effort), 8, y + 4, 8, bold: true, width: w * (wide ? .48 : .54) - 10);
            if (wide) Text(Cost(key + "cache"), w * .63, y + 4, 8, Muted, align: "right");
            Text(Cost(key + "tokens"), w * .79, y + 3, 9, font: DockAppearance.NumberFont, align: "right");
            Text(Cost(key + "cost"), w - 18, y + 4, 8, "#B5E6CB", align: "right");
        }
        string note = Station.M("aiError");
        if (note is "" or "—") note = Cost("coverage") + " · pas une facture · Kilo / Shopping non couverts";
        Text(note, 0, h - 13, 7, Muted, width: w - 10);
        ScrollMark(total, count, modelOffset, 72, count * 25 - 2);
    }
}
