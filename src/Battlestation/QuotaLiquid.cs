using System.Windows;
using System.Windows.Media;

namespace Battlestation;

// Quota vu comme un verre d'eau de profil : une rangée de colonnes couplées
// (onde amortie) posée sur un niveau qui suit le pourcentage restant.
// La page dessine seulement le DrawingGroup ; chaque image ne réécrit que lui,
// et au repos plus rien n'est calculé.
internal sealed class QuotaLiquid
{
    const int Columns = 48;
    const double Padding = 5, WaveSpeed = 380, Restore = 4, Damping = 1.9, Viscosity = 7;
    readonly double[] height = new double[Columns], velocity = new double[Columns];
    readonly Point[] surface = new Point[Columns];
    readonly Random random = new();
    internal DrawingGroup Drawing { get; } = new();
    internal bool Awake { get; private set; }
    internal double Level => level;
    Rect bounds; double radius;
    double level = double.NaN, target = double.NaN, levelVelocity;
    Color accent;
    Brush? fill; Pen? line, glow; Geometry? clip;
    Point pointer = new(double.NaN, double.NaN); long pointerTime;

    double Span => Math.Max(1, bounds.Height - Padding);
    double BaseY => bounds.Top + Padding + (1 - level) * Span;

    internal void Layout(Rect rect, double cornerRadius, double percent, Color color)
    {
        bool reshape = rect != bounds || cornerRadius != radius || color != accent;
        if (reshape) { bounds = rect; radius = cornerRadius; accent = color; Build(); }
        double next = double.IsFinite(percent) ? Math.Clamp(percent / 100, 0, 1) : double.NaN;
        if (!next.Equals(target))
        {
            double previous = target; target = next;
            if (!double.IsFinite(level) || !double.IsFinite(next)) { level = next; Calm(); reshape = true; }
            else Pour(next - previous);
        }
        if (reshape && !Awake) Draw();
    }

    // Le niveau glisse vers la nouvelle valeur ; la surface ballotte, et une
    // recharge verse le liquide d'un côté.
    void Pour(double delta)
    {
        double amplitude = Math.Clamp(Math.Abs(delta) * Span * 1.4, 8, 42), side = random.NextDouble() < .5 ? -1 : 1;
        for (int i = 0; i < Columns; i++)
            velocity[i] += side * amplitude * Math.Cos(Math.PI * i / (Columns - 1)) + (random.NextDouble() - .5) * amplitude * .3;
        if (delta > 0) Splash((side < 0 ? .22 : .78) * bounds.Width + bounds.Left, -amplitude * 3.5, 3);
        Awake = true;
    }

    void Splash(double x, double strength, double width)
    {
        double column = (x - bounds.Left) / Math.Max(1, bounds.Width) * (Columns - 1);
        for (int i = 0; i < Columns; i++)
        {
            double d = (i - column) / width;
            if (Math.Abs(d) < 3) velocity[i] += strength * Math.Exp(-d * d);
        }
        Awake = true;
    }

    // Passage de la souris : le curseur effleure la surface, pousse le liquide
    // dans son sens et le fait onduler en entrant ou en sortant du verre.
    internal void Stir(Point next, long time)
    {
        if (!double.IsFinite(level) || bounds.Width < 4)
        {
            pointer = next; pointerTime = time; return;
        }
        bool inside = bounds.Contains(next), wasInside = double.IsFinite(pointer.X) && bounds.Contains(pointer);
        if (inside || wasInside)
        {
            if (inside != wasInside) Splash(inside ? next.X : pointer.X, -30, 2.2);
            else
            {
                // Impulsions proportionnelles au trajet, pas au nombre d'événements souris.
                bool recent = time - pointerTime < 150;
                double moveX = recent ? Math.Clamp(next.X - pointer.X, -60, 60) : 0, moveY = recent ? Math.Clamp(next.Y - pointer.Y, -60, 60) : 0;
                double depth = next.Y - BaseY, touch = depth > 0 ? 1 : Math.Exp(depth / 26);
                Splash(next.X, -Math.Sqrt(moveX * moveX + moveY * moveY) * 1.6 * touch, 1.8);
                for (int i = 0; i < Columns; i++)
                    velocity[i] -= moveX * .22 * touch * Math.Cos(Math.PI * i / (Columns - 1));
            }
        }
        pointer = next; pointerTime = time;
    }

    internal bool Step(double elapsed)
    {
        if (!Awake) return false;
        double dt = Math.Clamp(elapsed, 0, 1 / 30d), span = Span;
        if (double.IsFinite(target))
        {
            // Ressort un peu sous-amorti : le niveau dépasse à peine puis se pose.
            double omega = 2 * Math.PI * .55, accel = omega * omega * (target - level) - 2 * .62 * omega * levelVelocity;
            levelVelocity += accel * dt; level += levelVelocity * dt;
            for (int i = 0; i < Columns; i++) velocity[i] -= accel * span * .02 * Math.Cos(2 * Math.PI * i / (Columns - 1)) * dt;
        }
        double dx = Math.Max(1, bounds.Width / (Columns - 1)), c2 = WaveSpeed * WaveSpeed / (dx * dx);
        int steps = Math.Max(1, (int)Math.Ceiling(dt * WaveSpeed / (.45 * dx)));
        double h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            for (int i = 0; i < Columns; i++)
            {
                int left = Math.Max(0, i - 1), right = Math.Min(Columns - 1, i + 1);
                velocity[i] += (c2 * (height[left] + height[right] - 2 * height[i]) - Restore * height[i] - Damping * velocity[i]
                    + Viscosity * (velocity[left] + velocity[right] - 2 * velocity[i])) * h;
            }
            for (int i = 0; i < Columns; i++) height[i] += velocity[i] * h;
        }
        double motion = 0;
        for (int i = 0; i < Columns; i++) motion = Math.Max(motion, Math.Abs(height[i]) + Math.Abs(velocity[i]) * .05);
        if (motion < .12 && Math.Abs(target - level) * span < .1 && Math.Abs(levelVelocity) * span < .4) { level = target; Calm(); }
        Draw();
        return Awake;
    }

    internal void Settle()
    {
        level = target; Calm(); Draw();
    }

    void Calm()
    {
        Array.Clear(height); Array.Clear(velocity); levelVelocity = 0; Awake = false;
    }

    void Build()
    {
        clip = new RectangleGeometry(bounds, radius, radius); clip.Freeze();
        Color deep = Mix(accent, Color.FromRgb(0x16, 0x0B, 0x2A), .32), bright = Mix(accent, Colors.White, .45);
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        gradient.GradientStops.Add(new GradientStop(Alpha(accent, 0x6A), 0));
        gradient.GradientStops.Add(new GradientStop(Alpha(accent, 0x3C), .16));
        gradient.GradientStops.Add(new GradientStop(Alpha(deep, 0x5E), 1));
        gradient.Freeze(); fill = gradient;
        line = new Pen(new SolidColorBrush(Alpha(bright, 0xE6)), 1.3) { LineJoin = PenLineJoin.Round }; line.Freeze();
        glow = new Pen(new SolidColorBrush(Alpha(accent, 0x38)), 6) { LineJoin = PenLineJoin.Round }; glow.Freeze();
    }

    void Draw()
    {
        using var dc = Drawing.Open();
        if (!double.IsFinite(level) || fill is null || bounds.Width < 4 || bounds.Height < 4) return;
        double dx = bounds.Width / (Columns - 1), baseY = BaseY, amplitude = Span * .3;
        for (int i = 0; i < Columns; i++)
        {
            // Léger ménisque contre les parois du verre.
            double meniscus = 1.8 * (Math.Exp(-i / 1.4) + Math.Exp(-(Columns - 1 - i) / 1.4));
            double y = baseY - Math.Clamp(height[i], -amplitude, amplitude) - meniscus;
            surface[i] = new Point(bounds.Left + i * dx, Math.Clamp(y, bounds.Top + 1, bounds.Bottom + 1));
        }
        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new Point(bounds.Left, bounds.Bottom + 2), true, true);
            g.PolyLineTo(surface, false, true);
            g.LineTo(new Point(bounds.Right, bounds.Bottom + 2), false, false);
        }
        body.Freeze();
        var edge = new StreamGeometry();
        using (var g = edge.Open())
        {
            g.BeginFigure(surface[0], false, false);
            g.PolyLineTo(surface[1..], true, true);
        }
        edge.Freeze();
        dc.PushClip(clip);
        dc.DrawGeometry(fill, null, body);
        dc.DrawGeometry(null, glow, edge);
        dc.DrawGeometry(null, line, edge);
        dc.Pop();
    }

    static Color Alpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
    static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
}
