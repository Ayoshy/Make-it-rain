using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace Battlestation;

// The Montagne scene material. Every value travels in one constant register;
// the shader itself is compiled by scripts/Build-Battlestation.ps1 from
// Shaders/Montagne.fx and embedded as a WPF resource. When the GPU cannot run
// pixel shader 3 the dock falls back to its still painted scene, so a panel
// never stays empty.
internal sealed class MontagneEffect : ShaderEffect
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
            string name = typeof(MontagneEffect).Assembly.GetName().Name ?? "Battlestation";
            var shader = new PixelShader { UriSource = new Uri($"pack://application:,,,/{name};component/Shaders/Montagne.ps", UriKind.Absolute) };
            shader.Freeze();
            return shader;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(MontagneEffect), 0);
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register("Time", typeof(double), typeof(MontagneEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty ResolutionProperty = DependencyProperty.Register("Resolution", typeof(Point), typeof(MontagneEffect), new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(1)));
    // The cursor in pixels and the decaying stir it leaves in the flakes.
    public static readonly DependencyProperty PointerProperty = DependencyProperty.Register("Pointer", typeof(Point), typeof(MontagneEffect), new UIPropertyMetadata(new Point(-10000, -10000), PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty StirProperty = DependencyProperty.Register("Stir", typeof(Point), typeof(MontagneEffect), new UIPropertyMetadata(new Point(0, 0), PixelShaderConstantCallback(3)));
    public static readonly DependencyProperty AudioProperty = DependencyProperty.Register("Audio", typeof(Point3D), typeof(MontagneEffect), new UIPropertyMetadata(new Point3D(0, 0, 0), PixelShaderConstantCallback(4)));

    public MontagneEffect()
    {
        PixelShader = compiled;
        foreach (var property in new[]
        {
            InputProperty, TimeProperty, ResolutionProperty,
            PointerProperty, StirProperty, AudioProperty
        }) UpdateShaderValue(property);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }
    public double Time { get => (double)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public Point Resolution { get => (Point)GetValue(ResolutionProperty); set => SetValue(ResolutionProperty, value); }
    public Point Pointer { get => (Point)GetValue(PointerProperty); set => SetValue(PointerProperty, value); }
    public Point Stir { get => (Point)GetValue(StirProperty); set => SetValue(StirProperty, value); }
    public Point3D Audio { get => (Point3D)GetValue(AudioProperty); set => SetValue(AudioProperty, value); }
}
