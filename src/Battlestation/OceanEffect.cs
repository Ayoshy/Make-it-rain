using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace Battlestation;

// The water material. Every value the shape and the light need travels in one
// constant register; the shader itself is compiled by scripts/Build-Battlestation.ps1
// from Shaders/Ocean.fx and embedded as a WPF resource. When the GPU cannot run
// pixel shader 3 the dock falls back to its still drawn block, so a panel never
// stays empty.
internal sealed class OceanEffect : ShaderEffect
{
    static readonly PixelShader? compiled = Load();
    internal static bool Available => compiled is not null && ShaderSupported && RenderCapability.Tier > 0;
    static bool ShaderSupported
    {
        get
        {
            try { return RenderCapability.IsPixelShaderVersionSupported(3, 0); }
            catch (NotSupportedException) { return false; }
        }
    }
    static PixelShader? Load()
    {
        try
        {
            string name = typeof(OceanEffect).Assembly.GetName().Name ?? "Battlestation";
            var shader = new PixelShader { UriSource = new Uri($"pack://application:,,,/{name};component/Shaders/Ocean.ps", UriKind.Absolute) };
            shader.Freeze();
            return shader;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(OceanEffect), 0);
    public static readonly DependencyProperty BedProperty = RegisterPixelShaderSamplerProperty("Bed", typeof(OceanEffect), 1);
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register("Time", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty ResolutionProperty = DependencyProperty.Register("Resolution", typeof(Point), typeof(OceanEffect), new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty EyeProperty = DependencyProperty.Register("Eye", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(1.4, .9, 2), PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register("Target", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, -.31, 0), PixelShaderConstantCallback(3)));
    public static readonly DependencyProperty FovProperty = DependencyProperty.Register("Fov", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(OceanCamera.Fov, PixelShaderConstantCallback(4)));
    // Five wave packets: (x, z, amplitude) then the ring radius they reached.
    public static readonly DependencyProperty Ripple0Property = DependencyProperty.Register("Ripple0", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(5)));
    public static readonly DependencyProperty Ring0Property = DependencyProperty.Register("Ring0", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(6)));
    public static readonly DependencyProperty Ripple1Property = DependencyProperty.Register("Ripple1", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(7)));
    public static readonly DependencyProperty Ring1Property = DependencyProperty.Register("Ring1", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(8)));
    public static readonly DependencyProperty Ripple2Property = DependencyProperty.Register("Ripple2", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(9)));
    public static readonly DependencyProperty Ring2Property = DependencyProperty.Register("Ring2", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(10)));
    public static readonly DependencyProperty Ripple3Property = DependencyProperty.Register("Ripple3", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(11)));
    public static readonly DependencyProperty Ring3Property = DependencyProperty.Register("Ring3", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(12)));
    public static readonly DependencyProperty Ripple4Property = DependencyProperty.Register("Ripple4", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(13)));
    public static readonly DependencyProperty Ring4Property = DependencyProperty.Register("Ring4", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(14)));
    public static readonly DependencyProperty AudioProperty = DependencyProperty.Register("Audio", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(15)));
    // Natural water: what the body scatters, what it absorbs, and the sky it
    // reflects. Deliberately independent of the desktop theme, so the water never
    // turns violet.
    public static readonly DependencyProperty ShallowProperty = DependencyProperty.Register("Shallow", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(.10, .46, .44), PixelShaderConstantCallback(16)));
    public static readonly DependencyProperty DeepProperty = DependencyProperty.Register("Deep", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(.34, .08, .06), PixelShaderConstantCallback(17)));
    public static readonly DependencyProperty SkyProperty = DependencyProperty.Register("Sky", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(.72, .84, .95), PixelShaderConstantCallback(18)));
    // x: absorption, y: reference swell scale, z: foam amount.
    public static readonly DependencyProperty ParamsProperty = DependencyProperty.Register("Params", typeof(Point3D), typeof(OceanEffect), new UIPropertyMetadata(new Point3D(.55, 1, .5), PixelShaderConstantCallback(19)));
    public static readonly DependencyProperty ReliefProperty = DependencyProperty.Register("Relief", typeof(double), typeof(OceanEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(20)));
    public static readonly DependencyProperty CropProperty = DependencyProperty.Register("Crop", typeof(Point), typeof(OceanEffect), new UIPropertyMetadata(new Point(0, 0), PixelShaderConstantCallback(21)));

    public OceanEffect()
    {
        PixelShader = compiled;
        foreach (var property in new[]
        {
            InputProperty, BedProperty, TimeProperty, ResolutionProperty, EyeProperty, TargetProperty, FovProperty,
            Ripple0Property, Ring0Property, Ripple1Property, Ring1Property, Ripple2Property, Ring2Property,
            Ripple3Property, Ring3Property, Ripple4Property, Ring4Property,
            AudioProperty, ShallowProperty, DeepProperty, SkyProperty, ParamsProperty, ReliefProperty, CropProperty
        }) UpdateShaderValue(property);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }
    public Brush Bed { get => (Brush)GetValue(BedProperty); set => SetValue(BedProperty, value); }
    public double Time { get => (double)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public Point Resolution { get => (Point)GetValue(ResolutionProperty); set => SetValue(ResolutionProperty, value); }
    public Point3D Eye { get => (Point3D)GetValue(EyeProperty); set => SetValue(EyeProperty, value); }
    public Point3D Target { get => (Point3D)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public double Fov { get => (double)GetValue(FovProperty); set => SetValue(FovProperty, value); }
    public Point3D Ripple0 { get => (Point3D)GetValue(Ripple0Property); set => SetValue(Ripple0Property, value); }
    public double Ring0 { get => (double)GetValue(Ring0Property); set => SetValue(Ring0Property, value); }
    public Point3D Ripple1 { get => (Point3D)GetValue(Ripple1Property); set => SetValue(Ripple1Property, value); }
    public double Ring1 { get => (double)GetValue(Ring1Property); set => SetValue(Ring1Property, value); }
    public Point3D Ripple2 { get => (Point3D)GetValue(Ripple2Property); set => SetValue(Ripple2Property, value); }
    public double Ring2 { get => (double)GetValue(Ring2Property); set => SetValue(Ring2Property, value); }
    public Point3D Ripple3 { get => (Point3D)GetValue(Ripple3Property); set => SetValue(Ripple3Property, value); }
    public double Ring3 { get => (double)GetValue(Ring3Property); set => SetValue(Ring3Property, value); }
    public Point3D Ripple4 { get => (Point3D)GetValue(Ripple4Property); set => SetValue(Ripple4Property, value); }
    public double Ring4 { get => (double)GetValue(Ring4Property); set => SetValue(Ring4Property, value); }
    public Point3D Audio { get => (Point3D)GetValue(AudioProperty); set => SetValue(AudioProperty, value); }
    public Point3D Shallow { get => (Point3D)GetValue(ShallowProperty); set => SetValue(ShallowProperty, value); }
    public Point3D Deep { get => (Point3D)GetValue(DeepProperty); set => SetValue(DeepProperty, value); }
    public Point3D Sky { get => (Point3D)GetValue(SkyProperty); set => SetValue(SkyProperty, value); }
    public Point3D Params { get => (Point3D)GetValue(ParamsProperty); set => SetValue(ParamsProperty, value); }
    public double Relief { get => (double)GetValue(ReliefProperty); set => SetValue(ReliefProperty, value); }
    public Point Crop { get => (Point)GetValue(CropProperty); set => SetValue(CropProperty, value); }
}
