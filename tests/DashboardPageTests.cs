using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.IO;

namespace Battlestation;
internal static class DashboardPageTests
{
    sealed class VariantHost : FrameworkElement
    {
        internal readonly DashboardTransition Pages;
        internal int Paints;
        internal VariantHost()
        {
            Width = 720; Height = 180;
            Pages = new DashboardTransition(InvalidateVisual, new Random(1729)); AddVisualChild(Pages.Visual);
        }
        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => Pages.Visual;
        protected override void OnRender(DrawingContext context)
        {
            Paints++;
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(31, 22, 43)), null, new Rect(0, 0, 720, 180));
            Pages.Render(new Rect(0, 0, 720, 180), (dc, page, interactive) =>
            {
                var color = page == 0 ? Brushes.HotPink : Brushes.MediumPurple;
                for (int i = 0; i < 6; i++)
                {
                    double x = 15 + i % 3 * 235, y = 12 + i / 3 * 84;
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(30, 200, 150, 255)), new Pen(color, .5), new Rect(x, y, 210, 64), 12, 12);
                    var label = new FormattedText(page == 0 ? "CŒUR " + (i + 1) : "MODÈLE " + (i + 1), System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.White, 1);
                    var value = new FormattedText(page == 0 ? "48°" : "1,23 M", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), FlowDirection.LeftToRight, new Typeface("Bahnschrift"), 24, color, 1);
                    dc.DrawText(label, new Point(x + 10, y + 9)); dc.DrawText(value, new Point(x + 110, y + 27));
                }
            });
        }
    }
    internal static void Variants(Action pump, string output)
    {
        var host = new VariantHost();
        var window = new Window { Content = host, Width = 720, Height = 180, Left = -20000, Top = -20000,
            WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false };
        window.Show(); pump();
        void Wait(int ms) { long until = Environment.TickCount64 + ms; while (Environment.TickCount64 < until) { pump(); Thread.Sleep(5); } }
        byte[] Pixels(string? name = null)
        {
            var bitmap = new RenderTargetBitmap(720, 180, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
            if (name is not null)
            {
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, "variant-" + name + ".png")); png.Save(file);
            }
            byte[] pixels = new byte[720 * 180 * 4]; bitmap.CopyPixels(pixels, 720 * 4, 0); return pixels;
        }
        DashboardEffect? previous = null; var seen = new HashSet<DashboardEffect>(); var cycle = new HashSet<DashboardEffect>();
        for (int i = 0; i < 8; i++)
        {
            byte[] before = Pixels(); host.Pages.Toggle(1); pump();
            var effect = host.Pages.Effect; seen.Add(effect); cycle.Add(effect);
            if (i % 4 == 3) { Check(cycle.Count == 4, "Each shuffled cycle contains all four effects"); cycle.Clear(); }
            Check(effect != previous, "Animation variants never immediately repeat"); previous = effect;
            int paints = host.Paints; Wait(110);
            byte[] during = Pixels(effect + "-middle");
            Check(host.Paints == paints, "Every effect runs without a per-frame surface redraw");
            Check(!during.SequenceEqual(before), effect + " changes the rendered pixels");
            var drawings = host.Pages.Visual.Children.Cast<DrawingVisual>().ToArray();
            if (effect == DashboardEffect.Blur)
                Check(drawings.Any(d => d.Effect is BlurEffect { Radius: > 4 and <= 10 }), "The focus transition has a visible, bounded blur phase");
            if (effect == DashboardEffect.Facets)
            {
                Check(drawings.All(d => d.OpacityMask is DrawingBrush), "Facets use drawing masks on both retained pages");
                var mask = (DrawingGroup)((DrawingBrush)drawings[0].OpacityMask).Drawing;
                var cells = mask.Children.OfType<GeometryDrawing>().ToArray();
                Check(cells.Length > 72 && cells.Length <= 224, "Finer pixel cells remain bounded");
                Check(cells.Select(c => c.Brush).Distinct().Count() == 12, "Pixel groups share just twelve opacity clocks per mask");
            }
            Wait(300); byte[] after = Pixels(effect + "-end");
            Check(!after.SequenceEqual(before) && !after.SequenceEqual(during), effect + " settles on distinct, complete incoming content");
            Check(!host.Pages.Running && !host.Pages.HasAnimatedProperties, effect + " releases all effect clocks");
            Check(drawings.All(d => d.Effect is null && d.OpacityMask is null), effect + " removes temporary blur and masks");
        }
        Check(seen.Count == 4, "All four effects were exercised");
        for (int i = 0; i < 4; i++)
        {
            host.Pages.Toggle(1); pump(); Wait(45);
            var effect = host.Pages.Effect;
            host.Pages.Toggle(1); host.Pages.Toggle(2); pump(); Wait(520);
            Check(host.Pages.Current == 2 && !host.Pages.HasAnimatedProperties, effect + " coalesces rapid clicks without residual effects");
            host.Pages.Toggle(1); pump(); Wait(30); host.Pages.Settle(0); host.InvalidateVisual(); pump(); Wait(330);
            Check(host.Pages.Current == 0 && !host.Pages.Running && !host.Pages.HasAnimatedProperties, "Cancellation invalidates old effect completion callbacks");
        }
        window.Close();
        Console.WriteLine("PASS: slide, fade, blur and facets; actual animated pixels, no repeats, no per-frame redraw, bounded blur, rapid clicks and cleanup.");
    }
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static List<(Rect Rect, Action Action, string Name)> Hits(Surface s) =>
        (List<(Rect, Action, string)>)typeof(Surface).GetField("hits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(s)!;
    static T Field<T>(object s, string name) => (T)s.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(s)!;
    internal static void Run(DashboardSurface surface, Station station, bool hardware, Action<string> render, Action pump)
    {
        void Wait(int ms = 420) { var until = Environment.TickCount64 + ms; while (Environment.TickCount64 < until) { pump(); Thread.Sleep(5); } }
        int first = hardware ? 1 : 3, second = hardware ? 2 : 4;
        string firstName = hardware ? "Cores" : "Quotas", secondName = hardware ? "Cooling" : "Models";
        var size = surface.RenderSize;
        station.Metrics["quotaDetails"] = "codex\n19% / 7 jours / 19 sept · 50% / 5 heures / 16:00\n\nAutre\n75% / 7 jours / 20 sept\n";
        station.Metrics["credits"] = "Crédits indisponibles";
        station.Metrics["modelsCount"] = "14"; station.Metrics["totalCost"] = "125,40 $";
        for (int i = 0; i < 14; i++)
        {
            station.Metrics[$"model:{i}:name"] = i == 0 ? "unknown" : $"modèle-{i}  high";
            station.Metrics[$"model:{i}:tokens"] = "1,23 M"; station.Metrics[$"model:{i}:cost"] = i == 0 ? "—" : "12,40 $";
        }
        station.Metrics["controlAvailable"] = "1"; station.Metrics["fanTarget"] = "50";
        station.Metrics["thermalTarget"] = "65"; station.Metrics["fanAuto"] = "1";
        station.Metrics["fanMin"] = "20"; station.Metrics["fanMax"] = "100";
        station.Metrics["thermalMin"] = "60"; station.Metrics["thermalMax"] = "83";
        surface.CloseDetails(); render("-summary");
        Hits(surface).Single(h => h.Name == firstName).Action(); render("-slide-start");
        Check(surface.Transition.Running, "Navigation starts a real WPF animation");
        long before = surface.RenderCount;
        Wait(70);
        var children = surface.Transition.Visual.Children;
        if (surface.Transition.Effect is DashboardEffect.Slide or DashboardEffect.Fade)
        {
            Check(children.Cast<Visual>().OfType<DrawingVisual>().Any(v => ((TranslateTransform)v.Transform).X > 0), "Outgoing page actually moves right");
            Check(children.Cast<Visual>().OfType<DrawingVisual>().Any(v => ((TranslateTransform)v.Transform).X < 0), "Incoming page actually arrives from left");
        }
        Check(surface.RenderCount == before, "Animation does not redraw the dashboard on each frame");
        render("-slide-middle"); Wait(); render("-page-first");
        Check(surface.Transition.Current == first && !surface.Transition.Running && !surface.Transition.HasAnimatedProperties, "Animation finishes and releases its clocks");
        Check(surface.RenderSize == size, "Navigation preserves dock geometry");
        Hits(surface).Single(h => h.Name == secondName).Action(); render("-page-second-start"); Wait(); render("-page-second");
        Check(surface.Transition.Current == second, "Direct navigation reaches second page");
        if (!hardware)
        {
            Check(surface.ScrollPage(1), "Models accept scrolling"); render("-models-scroll");
            Check(Field<int>(surface, "modelOffset") == 1, "Scrolling exposes the next model");
            surface.Toggle(first); render("-quotas-start"); Wait(); render("-quotas");
            surface.ScrollPage(1); render("-quotas-scroll");
            Check(Field<int>(surface, "quotaOffset") == (size.Height < 300 ? 1 : 0), "Quota capacity follows dock height");
            surface.Toggle(second); render("-models-return-start"); Wait(); render("-models-return");
            Check(Field<int>(surface, "modelOffset") == 1, "Model scroll position survives navigation");
            station.Metrics["modelsCount"] = "0"; render("-models-unavailable");
            Check(Field<int>(surface, "modelOffset") == 0, "Removed data clamps the scroll position");
            station.Metrics["modelsCount"] = "14";
        }
        if (hardware)
        {
            bool wasManual = Field<bool>(surface, "manual");
            Hits(surface).Single(h => h.Name == "FanMode").Action(); render("-manual");
            Check(Field<bool>(surface, "draftDirty") && Field<bool>(surface, "manual") != wasManual, "GPU draft is modified locally");
            if (wasManual) { Hits(surface).Single(h => h.Name == "FanMode").Action(); render("-manual"); }
            Check(Hits(surface).Any(h => h.Name == "Slider:fan"), "Manual fan slider becomes interactive");
            var oldApply = Hits(surface).Single(h => h.Name == "Apply").Action;
            surface.Toggle(first); render("-leaving-controls");
            Check(!Hits(surface).Any(h => h.Name is "Apply" or "FanMode" || h.Name.StartsWith("Slider:")), "Moving controls cannot receive input");
            oldApply(); // The fixture throws on any GPU write, so stale callbacks must be inert.
            station.Metrics["fanTarget"] = "72"; station.Metrics["thermalTarget"] = "80";
            Wait(); surface.Toggle(second); render("-return-controls-start"); Wait(); render("-return-controls");
            Check(Field<double>(surface, "fan") == 50 && Field<double>(surface, "thermal") == 65 && Field<bool>(surface, "manual"), "Navigation and late metrics preserve the GPU draft");
        }
        // Last request wins, including a reclick before the first render.
        surface.CloseDetails(); render("-reset");
        surface.Toggle(first); surface.Toggle(first); render("-double-start"); Wait(650);
        Check(surface.Transition.Current == 0 && !surface.Transition.Running, "Double click returns to summary");
        surface.Toggle(first); render("-rapid-start"); Wait(35);
        surface.Toggle(second); surface.Toggle(first); surface.Toggle(second); render("-rapid-last"); Wait(650);
        Check(surface.Transition.Current == second && !surface.Transition.Running, "Rapid clicks settle on the last destination");
        surface.Toggle(first); render("-hide-start"); surface.SetDisplayed(false);
        Check(!surface.Transition.Running && !surface.Transition.HasAnimatedProperties && surface.Transition.Visual.Opacity == 0, "Hidden retained visuals and clocks stop immediately");
        surface.SetDisplayed(true); render("-reshown"); Wait();
        Check(surface.Transition.Current == first, "A cancelled completion cannot overwrite the settled page");
        surface.Toggle(second); render("-resize-start"); surface.Width += 16; pump(); surface.UpdateLayout(); pump();
        Check(!surface.Transition.Running && !surface.Transition.HasAnimatedProperties, "Resize settles navigation and releases clocks");
        surface.Width = size.Width; surface.UpdateLayout(); pump();
        surface.CloseDetails(); render("-closed");
        Check(surface.Drawer == 0 && surface.RenderSize == size, "Close restores summary without changing the custom size");
        station.Metrics.Clear();
    }
}
