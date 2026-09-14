using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Battlestation;

internal enum DashboardEffect { Slide, Fade, Blur, Facets }

// Two retained drawings, one bounded navigation request; no per-frame UI timer.
internal sealed class DashboardTransition
{
    internal ContainerVisual Visual { get; } = new();
    DrawingVisual current = new(), incoming = new();
    readonly TranslateTransform currentPosition = new(), incomingPosition = new();
    readonly Action refresh;
    readonly Random random;
    readonly SolidColorBrush outgoingOpacity = new(Colors.White), incomingOpacity = new(Colors.White);
    readonly BlurEffect outgoingBlur = new() { RenderingBias = RenderingBias.Performance }, incomingBlur = new() { RenderingBias = RenderingBias.Performance };
    readonly List<(SolidColorBrush Out, SolidColorBrush In, double Delay)> facets = [];
    readonly DashboardEffect[] effectOrder = Enum.GetValues<DashboardEffect>();
    int nextEffect = 4;
    DashboardEffect? previousEffect;
    Rect bounds;
    int destination, generation;
    bool startPending, expedited;
    internal int Current { get; private set; }
    internal int Requested { get; private set; }
    internal bool Running { get; private set; }
    internal DashboardEffect Effect { get; private set; }
    internal bool HasAnimatedProperties => currentPosition.HasAnimatedProperties || incomingPosition.HasAnimatedProperties
        || outgoingOpacity.HasAnimatedProperties || incomingOpacity.HasAnimatedProperties || outgoingBlur.HasAnimatedProperties || incomingBlur.HasAnimatedProperties
        || facets.Any(f => f.Out.HasAnimatedProperties || f.In.HasAnimatedProperties);

    internal DashboardTransition(Action refresh, Random? random = null)
    {
        this.refresh = refresh;
        this.random = random ?? Random.Shared;
        Visual.Children.Add(current); Visual.Children.Add(incoming);
        current.Transform = currentPosition; incoming.Transform = incomingPosition;
        incoming.Opacity = 0;
    }

    internal void Toggle(int page)
    {
        Requested = Requested == page ? 0 : page;
        if (!Running) Start();
        else if (!startPending && !expedited) Animate(90);
        refresh();
    }

    void Start()
    {
        if (Current == Requested) return;
        destination = Requested; Running = startPending = true; expedited = false;
        // A shuffled cycle guarantees all four effects, including across short
        // sessions. Avoid a duplicate at the boundary between two cycles.
        if (nextEffect == effectOrder.Length)
        {
            for (int i = effectOrder.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (effectOrder[i], effectOrder[j]) = (effectOrder[j], effectOrder[i]);
            }
            if (effectOrder[0] == previousEffect) (effectOrder[0], effectOrder[1]) = (effectOrder[1], effectOrder[0]);
            nextEffect = 0;
        }
        Effect = effectOrder[nextEffect++]; previousEffect = Effect;
    }

    internal void Render(Rect area, Action<DrawingContext, int, bool> draw)
    {
        if (bounds != area)
        {
            Settle(Requested);
            bounds = area;
            Visual.Offset = new Vector(area.X, area.Y);
            Visual.Clip = new RectangleGeometry(new Rect(0, 0, area.Width, area.Height), 10, 10);
        }
        using (var dc = current.RenderOpen()) draw(dc, Current, !Running);
        if (Running)
        {
            using (var dc = incoming.RenderOpen()) draw(dc, destination, false);
            if (startPending)
            {
                startPending = false; incoming.Opacity = 1;
                currentPosition.X = 0; incomingPosition.X = -Travel;
                PrepareEffect();
                Animate(Requested == destination ? Duration : 90);
            }
        }
    }

    double Duration => Effect is DashboardEffect.Blur or DashboardEffect.Facets ? 380 : 300;
    double Travel => Effect == DashboardEffect.Slide ? bounds.Width : Effect == DashboardEffect.Fade ? 18 : 0;

    void PrepareEffect()
    {
        if (Effect == DashboardEffect.Slide) return;
        if (Effect == DashboardEffect.Facets)
        {
            // Finer, scattered pixels, with just 12 shared phase brushes per mask.
            // Up to 224 cells still need only 24 opacity clocks, not 448.
            int columns = Math.Clamp((int)(bounds.Width / 28), 10, 28), rows = Math.Clamp((int)(bounds.Height / 24), 4, 8);
            for (int phase = 0; phase < 12; phase++)
                facets.Add((new SolidColorBrush(Colors.White), new SolidColorBrush(Colors.White) { Opacity = 0 }, .62 * phase / 11));
            int[] order = Enumerable.Range(0, columns * rows).ToArray();
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1); (order[i], order[j]) = (order[j], order[i]);
            }
            var outgoing = new DrawingGroup(); var entering = new DrawingGroup();
            using (var a = outgoing.Open())
            using (var b = entering.Open())
            {
                for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    var phase = facets[order[row * columns + column] % facets.Count];
                    double x = Math.Round(column * bounds.Width / columns), y = Math.Round(row * bounds.Height / rows);
                    var cell = new Rect(x, y, Math.Round((column + 1) * bounds.Width / columns) - x, Math.Round((row + 1) * bounds.Height / rows) - y);
                    a.DrawRectangle(phase.Out, null, cell); b.DrawRectangle(phase.In, null, cell);
                }
            }
            current.OpacityMask = Mask(outgoing); incoming.OpacityMask = Mask(entering);
        }
        else
        {
            outgoingOpacity.Opacity = 1; incomingOpacity.Opacity = 0;
            current.OpacityMask = outgoingOpacity; incoming.OpacityMask = incomingOpacity;
            if (Effect == DashboardEffect.Blur)
            {
                outgoingBlur.Radius = 0; incomingBlur.Radius = 10;
                current.Effect = outgoingBlur; incoming.Effect = incomingBlur;
            }
        }
    }

    DrawingBrush Mask(DrawingGroup drawing) => new(drawing)
    {
        ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, bounds.Width, bounds.Height),
        ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, bounds.Width, bounds.Height), Stretch = Stretch.Fill
    };

    static void To(Animatable target, DependencyProperty property, double from, double to, double milliseconds, double delay = 0)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    static void FocusOpacity(SolidColorBrush brush, bool entering, double milliseconds)
    {
        var frames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(milliseconds) };
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(entering ? 0 : 1, KeyTime.FromPercent(0)));
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(entering ? 0 : 1, KeyTime.FromPercent(entering ? .35 : .25)));
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(entering ? 1 : 0, KeyTime.FromPercent(entering ? .8 : .65)));
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(entering ? 1 : 0, KeyTime.FromPercent(1)));
        brush.BeginAnimation(Brush.OpacityProperty, frames, HandoffBehavior.SnapshotAndReplace);
    }

    void Animate(double milliseconds)
    {
        expedited = milliseconds < Duration;
        int version = ++generation;
        double x = currentPosition.X, nextX = incomingPosition.X;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var outgoing = new DoubleAnimation(x, Travel, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing };
        var entering = new DoubleAnimation(nextX, 0, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing };
        if (Effect == DashboardEffect.Fade || Effect == DashboardEffect.Blur && expedited)
        {
            To(outgoingOpacity, Brush.OpacityProperty, outgoingOpacity.Opacity, 0, milliseconds);
            To(incomingOpacity, Brush.OpacityProperty, incomingOpacity.Opacity, 1, milliseconds);
        }
        if (Effect == DashboardEffect.Blur)
        {
            if (!expedited) { FocusOpacity(outgoingOpacity, false, milliseconds); FocusOpacity(incomingOpacity, true, milliseconds); }
            To(outgoingBlur, BlurEffect.RadiusProperty, outgoingBlur.Radius, 10, expedited ? milliseconds : milliseconds * .5);
            To(incomingBlur, BlurEffect.RadiusProperty, incomingBlur.Radius, 0, expedited ? milliseconds : milliseconds * .65, expedited ? 0 : milliseconds * .35);
        }
        foreach (var facet in facets)
        {
            double delay = expedited ? 0 : milliseconds * facet.Delay;
            double duration = expedited ? milliseconds : milliseconds * .12;
            To(facet.Out, Brush.OpacityProperty, facet.Out.Opacity, 0, duration, delay);
            To(facet.In, Brush.OpacityProperty, facet.In.Opacity, 1, duration, delay + (expedited ? 0 : milliseconds * .16));
        }
        outgoing.Completed += (_, _) =>
        {
            if (version != generation || !Running) return;
            StopClocks();
            (current, incoming) = (incoming, current);
            current.Transform = currentPosition; incoming.Transform = incomingPosition;
            incoming.Opacity = 0;
            using (incoming.RenderOpen()) { }
            Current = destination; Running = false;
            Start(); refresh();
        };
        currentPosition.BeginAnimation(TranslateTransform.XProperty, outgoing, HandoffBehavior.SnapshotAndReplace);
        incomingPosition.BeginAnimation(TranslateTransform.XProperty, entering, HandoffBehavior.SnapshotAndReplace);
    }

    void StopClocks()
    {
        generation++;
        currentPosition.BeginAnimation(TranslateTransform.XProperty, null);
        incomingPosition.BeginAnimation(TranslateTransform.XProperty, null);
        currentPosition.X = incomingPosition.X = 0;
        outgoingOpacity.BeginAnimation(Brush.OpacityProperty, null); incomingOpacity.BeginAnimation(Brush.OpacityProperty, null);
        outgoingBlur.BeginAnimation(BlurEffect.RadiusProperty, null); incomingBlur.BeginAnimation(BlurEffect.RadiusProperty, null);
        foreach (var facet in facets) { facet.Out.BeginAnimation(Brush.OpacityProperty, null); facet.In.BeginAnimation(Brush.OpacityProperty, null); }
        facets.Clear();
        current.OpacityMask = incoming.OpacityMask = null;
        current.Effect = incoming.Effect = null;
    }

    internal void Settle(int page)
    {
        StopClocks(); Current = Requested = destination = page;
        Running = startPending = expedited = false;
        incoming.Opacity = 0;
        using (incoming.RenderOpen()) { }
    }
}
