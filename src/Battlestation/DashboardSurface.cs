using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace Battlestation;
internal sealed class DashboardSurface : Surface
{
    readonly bool sensors;
    readonly DashboardTransition pages;
    int modelOffset, quotaOffset;
    bool drawingPage, interactive, manual, draftDirty;
    double fan = double.NaN, thermal = double.NaN;
    Rect body;
    readonly Dictionary<string, Rect> sliders = [];
    string? drag;
    public int Drawer => pages.Requested;
    internal DashboardTransition Transition => pages;
    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => index == 0 ? pages.Visual : throw new ArgumentOutOfRangeException(nameof(index));
    protected override double HitOffsetX => drawingPage ? body.X : 0;
    protected override double HitOffsetY => drawingPage ? body.Y : 0;

    public DashboardSurface(Station station, bool hardware) : base(station,hardware?6:7)
    {
        sensors = hardware; Width = 779; Height = hardware ? 218 : 209;
        pages = new DashboardTransition(Refresh); AddVisualChild(pages.Visual);
        Unloaded += (_, _) => { CancelDrag(); pages.Settle(pages.Requested); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { CancelDrag(); pages.Settle(pages.Requested); } else Refresh(); };
    }
    public void Toggle(int next)
    {
        if (sensors ? next is not (1 or 2) : next is not (3 or 4)) return;
        CancelDrag(); sliders.Clear();
        if (next == 2 && pages.Requested != 2 && !draftDirty)
        {
            Station.Command("GpuRead");
        }
        pages.Toggle(next);
    }
    internal void CloseDetails() { CancelDrag(); pages.Settle(0); Refresh(); }
    internal override void SetDisplayed(bool value)
    {
        if (!value) { CancelDrag(); pages.Settle(pages.Requested); sliders.Clear(); }
        pages.Visual.Opacity = value ? 1 : 0;
        base.SetDisplayed(value);
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        CancelDrag(); pages.Settle(pages.Requested); base.OnRenderSizeChanged(sizeInfo); Refresh();
    }
    protected override void Paint()
    {
        double w = ActualWidth, h = ActualHeight, buttonY = h - 44, buttonW = (w - 72) / 3;
        Header(sensors ? "CONRAD SENSOR" : "CODEX METER");
        Text(Station.M(sensors ? "sensorStatus" : "codexStatus"), w - 24, 18, 8, align: "right", width: Math.Max(80, w - 235));
        Nav(sensors ? "Cores" : "Quotas", sensors ? "6 CŒURS" : "QUOTAS", 24, sensors ? 1 : 3);
        Nav(sensors ? "Cooling" : "Models", sensors ? (w < 600 ? "FROID ⚙" : "REFROIDISSEMENT ⚙") : "MODÈLES", 36 + buttonW, sensors ? 2 : 4);
        Button(sensors ? "Heatwave" : "Refresh", sensors ? (Station.M("heatwave") == "1" ? "♨ ACTIVE" : "♨ CANICULE") : "ACTUALISER ↻",
            48 + 2 * buttonW, buttonY, buttonW, 32, () => Station.Command(sensors ? "Heatwave" : "Refresh"), 8, Ink);
        void Nav(string name, string label, double x, int page)
        {
            if (pages.Requested == page) Box(x, buttonY, buttonW, 32, "#26DBC5ED", "#72E7D5FA", DockAppearance.ButtonRadius);
            Button(name, label, x, buttonY, buttonW, 32, () => Toggle(page), 8, Ink);
        }
        if (!draftDirty && Station.M("controlAvailable") == "1")
        {
            fan = Station.N("fanTarget"); thermal = Station.N("thermalTarget");
            manual = Station.M("fanAuto") == "0";
        }
        body = new Rect(24, 46, Math.Max(1, w - 48), Math.Max(1, buttonY - 54));
        sliders.Clear();
        var context = D; var pointer = Pointer;
        drawingPage = true; Pointer = new Point(pointer.X - body.X, pointer.Y - body.Y);
        pages.Render(body, (dc, page, canInteract) =>
        {
            D = dc; interactive = canInteract;
            switch (page)
            {
                case 0: Summary(); break;
                case 1: Cores(); break;
                case 2: Cooling(); break;
                case 3: Quotas(); break;
                case 4: Models(); break;
            }
        });
        D = context; Pointer = pointer; drawingPage = false;
    }
    void Summary()
    {
        double w = body.Width, h = body.Height;
        if (sensors)
        {
            double col = w / 3;
            for (int i = 0; i < 2; i++)
            {
                string key = i == 0 ? "cpu" : "gpu", color = i == 0 ? Pink : Purple; double x = i * col;
                Text(key.ToUpperInvariant(), x, 2, 9, bold: true);
                Text(Station.M(key + "Load"), x + col - 16, 3, 8, align: "right");
                Text(Station.M(key), x, 23, 29, color, DockAppearance.NumberFont);
                Track(x, Math.Min(h - 8, 88), col - 20, Station.N(key), color);
            }
            double right = 2 * col;
            Text("VENTILATEUR", right, 3, 7); Text(Station.M("fan"), right, 20, 16, font: DockAppearance.NumberFont);
            Text("PUISSANCE", right, 59, 7); Text(Station.M("watts"), right, 75, 16, font: DockAppearance.NumberFont);
        }
        else
        {
            double right = w * .55;
            Text(Station.M("quotaLabel"), 0, 0, 9);
            Text(Station.M("remaining"), 0, 19, 30, Purple, DockAppearance.NumberFont);
            Track(0, 76, right - 24, Station.N("remaining"), Purple);
            Text(Station.M("reset"), 0, 86, 7.5, width: right - 20);
            Text("TOKENS DU JOUR", right, 1, 8); Text(Station.M("today"), right, 19, 17, font: DockAppearance.NumberFont);
            Text("TOKENS CUMULÉS", right, 58, 8); Text(Station.M("total"), right, 75, 17, font: DockAppearance.NumberFont);
        }
    }
    void Cores()
    {
        double w = body.Width, h = body.Height;
        Text("i5-9600KF", 0, 0, 8.5, bold: true);
        Text("MAX  " + Station.M("hottest"), w, 0, 10, "#E4ABFA", DockAppearance.NumberFont, "right");
        double cellW = (w - 16) / 3, cellH = Math.Min(76, (h - 29) / 2);
        for (int i = 0; i < 6; i++)
        {
            double x = i % 3 * (cellW + 8), y = 23 + i / 3 * (cellH + 6);
            Box(x, y, cellW, cellH, "#186E4093", "#41B47FDB", 9);
            Text("C" + (i + 1), x + 9, y + (cellH - 13) / 2, 8, "#CCB1DC", bold: true);
            Text(Station.M("core" + i), x + cellW - 9, y + (cellH - 25) / 2, 17, "#F0D5FD", DockAppearance.NumberFont, "right");
        }
    }
    void PageButton(string name, string label, Rect rect, Action action, bool enabled = true)
    {
        Button(name, label, rect.X, rect.Y, rect.Width, rect.Height, () => { if (!pages.Running) action(); },
            8, Ink, enabled, DockAppearance.ButtonRadius, interactive);
    }
    void Cooling()
    {
        double w = body.Width, col = (w - 20) / 2;
        bool available = Station.M("controlAvailable") == "1" && double.IsFinite(fan) && double.IsFinite(thermal);
        Text("RTX 2060 SUPER", 0, 1, 8.5, bold: true);
        PageButton("FanMode", available ? (manual ? "MANUEL" : "AUTO") : "—", new Rect(w - 88, 0, 88, 24), () => { manual = !manual; draftDirty = true; }, available);
        Text("VITESSE", 0, 33, 7.5, bold: true);
        Text(available ? (manual ? fan + " %" : "AUTO") : "—", col, 30, 11, align: "right");
        Text("CIBLE THERMIQUE", col + 20, 33, 7.5, bold: true);
        Text(available ? thermal + "°" : "—", w, 30, 11, align: "right");
        Slider("fan", new Rect(9, 61, col - 18, 6), fan, available && manual);
        Slider("thermal", new Rect(col + 29, 61, col - 18, 6), thermal, available);
        double applyW = w < 500 ? 138 : 174;
        PageButton("Apply", "APPLIQUER AU GPU", new Rect(w - applyW, 84, applyW, 27), () =>
        {
            Station.Command($"Apply:{(manual ? 1 : 0)}:{fan:0}:{thermal:0}");
        }, available);
        string error = Station.Error != "" ? Station.Error : Station.M("sensorError");
        Text(error != "" ? error : available ? "" : "Contrôle GPU indisponible", 0, 89, 7.5, "#FF95C1", width: w - applyW - 12);
    }
    void Slider(string name, Rect track, double value, bool enabled)
    {
        double min = Station.N(name == "fan" ? "fanMin" : "thermalMin"), max = Station.N(name == "fan" ? "fanMax" : "thermalMax");
        bool valid = double.IsFinite(min + max + value) && max > min;
        enabled &= valid;
        Box(track.X, track.Y, track.Width, track.Height, "#885C3570", radius: 3);
        double ratio = valid ? Math.Clamp((value - min) / (max - min), 0, 1) : 0;
        D.DrawEllipse(B(enabled ? "#E8D5FC" : Muted), null, new Point(track.X + ratio * track.Width, track.Y + 3), 7, 7);
        if (enabled && interactive)
        {
            sliders[name] = track;
            Hit("Slider:" + name, track.X - 8, track.Y - 8, track.Width + 16, 22, () => { if (!pages.Running) SetSlider(name, Pointer.X - body.X); });
        }
    }
    void SetSlider(string name, double x)
    {
        if (!sliders.TryGetValue(name, out var track)) return;
        double min = Station.N(name == "fan" ? "fanMin" : "thermalMin"), max = Station.N(name == "fan" ? "fanMax" : "thermalMax");
        if (!double.IsFinite(min + max) || max <= min) return;
        double value = Math.Round(min + Math.Clamp((x - track.X) / track.Width, 0, 1) * (max - min));
        if (name == "fan") fan = value; else thermal = value;
        draftDirty = true; Refresh();
    }
    void CancelDrag() { drag = null; if (IsMouseCaptured) ReleaseMouseCapture(); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!pages.Running && pages.Current == 2)
        {
            var point = e.GetPosition(this); point.Offset(-body.X, -body.Y);
            foreach (var (name, track) in sliders)
            {
                var hit = track; hit.Inflate(8, 8);
                if (!hit.Contains(point)) continue;
                drag = name; CaptureMouse(); SetSlider(name, point.X); e.Handled = true; return;
            }
        }
        base.OnMouseLeftButtonDown(e);
    }
    protected override void OnPointer(MouseEventArgs e)
    {
        if (drag is not null && e.LeftButton == MouseButtonState.Pressed) SetSlider(drag, e.GetPosition(this).X - body.X);
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (drag is not null) { CancelDrag(); e.Handled = true; } else base.OnMouseLeftButtonUp(e);
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { drag = null; base.OnLostMouseCapture(e); }
    List<(string Name, string Remaining, string Duration, string Reset)> QuotaRows()
    {
        var rows = new List<(string, string, string, string)>(); string name = "";
        foreach (var line in Station.M("quotaDetails").Split('\n'))
        {
            if (line.Length == 0) { name = ""; continue; }
            if (name == "") { name = line; continue; }
            foreach (var item in line.Split('·'))
            {
                var parts = item.Split(" / ");
                if (parts.Length == 3) rows.Add((name, parts[0].Trim(), parts[1].Trim(), parts[2].Trim()));
            }
        }
        return rows;
    }
    int QuotaCapacity => Math.Max(1, (int)((body.Height - 18) / 48));
    int ModelCapacity => Math.Max(1, (int)((body.Height - 60) / 29));
    int ModelCount => double.IsFinite(Station.N("modelsCount")) ? Math.Max(0, (int)Station.N("modelsCount")) : 0;
    void Quotas()
    {
        double w = body.Width; var rows = QuotaRows(); int count = QuotaCapacity;
        quotaOffset = Math.Clamp(quotaOffset, 0, Math.Max(0, rows.Count - count));
        if (rows.Count == 0) Text("Quotas indisponibles", 0, 5, 10);
        for (int i = 0; i < count && quotaOffset + i < rows.Count; i++)
        {
            var row = rows[quotaOffset + i]; double y = i * 48;
            Box(0, y, w - 8, 43, "#12804DAE", "#37B77DDF", 9);
            Text(row.Name == "codex" ? "Codex" : row.Name, 10, y + 3, 9, bold: true, width: w * .45);
            Text(row.Duration.ToUpperInvariant(), w - 87, y + 6, 7.5, align: "right");
            var match = Regex.Match(row.Remaining, @"(\d+)%");
            double percent = match.Success ? double.Parse(match.Groups[1].Value) : double.NaN;
            Text(match.Success ? percent + "%" : "N/D", w - 18, y + 1, 17, Purple, DockAppearance.NumberFont, "right");
            Text("Reset  " + row.Reset, 10, y + 23, 7.5, width: w - 30);
            Track(10, y + 40, w - 38, percent, Purple, 2);
        }
        Text(Station.M("credits"), 0, body.Height - 15, 7.5, Muted, width: w - 10);
        ScrollMark(rows.Count, count, quotaOffset, 0, count * 48 - 5);
    }
    void Models()
    {
        double w = body.Width; int count = ModelCapacity, total = ModelCount;
        modelOffset = Math.Clamp(modelOffset, 0, Math.Max(0, total - count));
        Text(Station.M("total") + " tokens", 0, 0, 10.5, font: DockAppearance.NumberFont);
        Text("≈ " + Station.M("totalCost") + " API", w - 8, 0, 10.5, "#96E7B2", DockAppearance.NumberFont, "right");
        bool wide = w >= 550;
        Text("MODÈLE", 8, 25, 7, Muted, bold: true);
        if (wide) Text("EFFORT", w * .54, 25, 7, Muted, align: "center", bold: true);
        Text("TOKENS", w * .77, 25, 7, Muted, align: "right", bold: true);
        Text("ESTIMÉ", w - 18, 25, 7, Muted, align: "right", bold: true);
        if (total == 0) Text("Modèles indisponibles", 8, 44, 9);
        for (int i = 0; i < count && modelOffset + i < total; i++)
        {
            double y = 42 + i * 29;
            Box(0, y, w - 8, 27, i % 2 == 0 ? "#18B483D8" : "#08B483D8", radius: 6);
            var pieces = Station.M($"model:{modelOffset + i}:name").Split("  ");
            string name = pieces[0] == "unknown" ? "Modèle inconnu" : pieces[0];
            string effort = pieces.Length > 1 && pieces[1] != "unspecified" ? pieces[1].ToUpperInvariant() : "";
            Text(wide || effort == "" ? name : name + " · " + effort, 8, y + 5, 8.5, bold: true, width: w * .46 - 12);
            if (wide) Text(effort, w * .54, y + 7, 7, "#C79FE2", align: "center");
            Text(Station.M($"model:{modelOffset + i}:tokens"), w * .77, y + 4, 10, font: DockAppearance.NumberFont, align: "right");
            Text(Station.M($"model:{modelOffset + i}:cost"), w - 18, y + 4, 10, "#96E7B2", DockAppearance.NumberFont, "right");
        }
        Text("Estimation locale partielle · pas une facture", 0, body.Height - 14, 7, Muted, width: w - 12);
        ScrollMark(total, count, modelOffset, 42, count * 29 - 2);
    }
    void ScrollMark(int total, int visible, int offset, double y, double height)
    {
        if (total <= visible) return;
        double thumb = Math.Max(10, height * visible / total);
        Box(body.Width - 3, y, 2, height, "#22DACDEC", radius: 1);
        Box(body.Width - 3, y + (height - thumb) * offset / (total - visible), 2, thumb, "#BBDACDEC", radius: 1);
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (pages.Running || !body.Contains(e.GetPosition(this))) return;
        if (ScrollPage(e.Delta > 0 ? -1 : 1)) e.Handled = true;
    }
    internal bool ScrollPage(int delta)
    {
        if (pages.Running) return false;
        if (pages.Current == 4) modelOffset = Math.Clamp(modelOffset + delta, 0, Math.Max(0, ModelCount - ModelCapacity));
        else if (pages.Current == 3) quotaOffset = Math.Clamp(quotaOffset + delta, 0, Math.Max(0, QuotaRows().Count - QuotaCapacity));
        else return false;
        Refresh(); return true;
    }
}
