using System.Windows;
using System.Windows.Media.Media3D;

namespace Battlestation;

// The diorama camera is fixed: a three-quarter view slightly above the block. Only
// the distance and the crop follow the dock size, so the object keeps its
// proportions and is re-framed instead of being stretched. The very same numbers
// are handed to the pixel shader, which is why one class owns them.
internal static class OceanCamera
{
    internal const double Azimuth = .60, Elevation = .46, Fov = .60;
    internal const double TrayHalf = 1, TrayBottom = -.62, TrayTop = 0;
    internal const double Margin = .93;
    static readonly Point3D[] Corners =
    [
        new(-TrayHalf, TrayBottom, -TrayHalf), new(TrayHalf, TrayBottom, -TrayHalf),
        new(-TrayHalf, TrayBottom, TrayHalf), new(TrayHalf, TrayBottom, TrayHalf),
        new(-TrayHalf, TrayTop, -TrayHalf), new(TrayHalf, TrayTop, -TrayHalf),
        new(-TrayHalf, TrayTop, TrayHalf), new(TrayHalf, TrayTop, TrayHalf)
    ];
    internal static Vector3D Direction { get; } = MakeDirection();

    static Vector3D MakeDirection()
    {
        var direction = new Vector3D(Math.Cos(Elevation) * Math.Sin(Azimuth), Math.Sin(Elevation), Math.Cos(Elevation) * Math.Cos(Azimuth));
        direction.Normalize();
        return direction;
    }

    internal static double Aspect(double width, double height) => Math.Max(.25, width / Math.Max(1, height));

    internal static (Point3D Eye, Point3D Target, Point Crop) Frame(double width, double height)
    {
        double aspect = Aspect(width, height);
        var target = new Point3D(0, (TrayTop + TrayBottom) / 2, 0);
        var eye = target + Direction * Fit(target, aspect);
        Bounds(eye, target, aspect, out double centerX, out double centerY, out _, out _);
        return (eye, target, new Point(centerX, centerY));
    }

    static double Fit(Point3D target, double aspect)
    {
        double low = 1.2, high = 16;
        for (int step = 0; step < 48; step++)
        {
            double middle = (low + high) / 2;
            if (Bounds(target + Direction * middle, target, aspect, out _, out _, out double left, out double right) && left >= -Margin && right <= Margin) high = middle;
            else low = middle;
        }
        return high;
    }

    static bool Bounds(Point3D eye, Point3D target, double aspect, out double centerX, out double centerY, out double minimumX, out double maximumX)
    {
        centerX = centerY = minimumX = maximumX = 0;
        Basis(eye, target, out var forward, out var right, out var up);
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var corner in Corners)
        {
            var view = corner - eye;
            double depth = Vector3D.DotProduct(view, forward);
            if (depth <= .05) return false;
            double x = Vector3D.DotProduct(view, right) / (depth * Fov * aspect);
            double y = Vector3D.DotProduct(view, up) / (depth * Fov);
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        centerX = (minX + maxX) / 2; centerY = (minY + maxY) / 2;
        minimumX = minX; maximumX = maxX;
        return minX >= -Margin && maxX <= Margin && minY >= -Margin && maxY <= Margin;
    }

    static void Basis(Point3D eye, Point3D target, out Vector3D forward, out Vector3D right, out Vector3D up)
    {
        forward = target - eye;
        forward.Normalize();
        right = Vector3D.CrossProduct(forward, new Vector3D(0, 1, 0));
        right.Normalize();
        up = Vector3D.CrossProduct(right, forward);
    }

    // Same ray as the shader builds for one pixel.
    internal static Vector3D Ray(Point3D eye, Point3D target, Point crop, double aspect, double u, double v)
    {
        Basis(eye, target, out var forward, out var right, out var up);
        double x = (u * 2 - 1) + crop.X, y = (1 - v * 2) + crop.Y;
        var direction = forward + right * (x * Fov * aspect) + up * (y * Fov);
        direction.Normalize();
        return direction;
    }

    // Where the pointer meets the still water. The reference relief is a few
    // hundredths of a unit high, so the plane is used for the intersection and the
    // footprint decides whether the water was touched at all.
    internal static bool TryTouch(Point3D eye, Point3D target, Point crop, double aspect, double u, double v, out Point3D hit)
    {
        hit = new Point3D(0, 0, 0);
        var direction = Ray(eye, target, crop, aspect, u, v);
        if (direction.Y >= -.0005) return false;
        double t = -eye.Y / direction.Y;
        if (t <= 0) return false;
        var point = eye + direction * t;
        if (Math.Abs(point.X) > TrayHalf - .04 || Math.Abs(point.Z) > TrayHalf - .04) return false;
        hit = point;
        return true;
    }
}
