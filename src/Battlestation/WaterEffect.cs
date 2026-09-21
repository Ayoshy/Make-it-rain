using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Battlestation;

// The aquarium water runs in one pixel shader: caustics, surface refraction and
// the cursor wake are computed per pixel instead of being drawn as strokes. The
// compiled shader is embedded; when it cannot be loaded or the GPU is too old the
// aquarium draws its former water and nothing here is used.
internal sealed class WaterEffect : ShaderEffect
{
    static readonly PixelShader? compiled=Load();
    internal static bool Available=>compiled is not null&&ShaderSupported;
    static bool ShaderSupported
    {
        get
        {
            // The water needs shader model 3; a machine that cannot run it keeps the
            // drawn water instead of an empty panel.
            try{return RenderCapability.Tier>0&&RenderCapability.IsPixelShaderVersionSupported(3,0);}
            catch(NotSupportedException){return RenderCapability.Tier>0;}
        }
    }
    static PixelShader? Load()
    {
        try
        {
            string name=typeof(WaterEffect).Assembly.GetName().Name??"Battlestation";
            var shader=new PixelShader{UriSource=new Uri($"pack://application:,,,/{name};component/Shaders/Water.ps",UriKind.Absolute)};
            shader.Freeze();return shader;
        }
        catch(Exception e) when(e is IOException or InvalidOperationException or ArgumentException or UriFormatException){return null;}
    }
    public static readonly DependencyProperty InputProperty=RegisterPixelShaderSamplerProperty("Input",typeof(WaterEffect),0);
    public static readonly DependencyProperty TimeProperty=DependencyProperty.Register("Time",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(0d,PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty ResolutionProperty=DependencyProperty.Register("Resolution",typeof(Point),typeof(WaterEffect),new UIPropertyMetadata(new Point(1,1),PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty PointerProperty=DependencyProperty.Register("Pointer",typeof(Point),typeof(WaterEffect),new UIPropertyMetadata(new Point(-1,-1),PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty BassProperty=DependencyProperty.Register("Bass",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(0d,PixelShaderConstantCallback(3)));
    public static readonly DependencyProperty MidProperty=DependencyProperty.Register("Mid",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(0d,PixelShaderConstantCallback(4)));
    public static readonly DependencyProperty TrebleProperty=DependencyProperty.Register("Treble",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(0d,PixelShaderConstantCallback(5)));
    public static readonly DependencyProperty EnergyProperty=DependencyProperty.Register("Energy",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(0d,PixelShaderConstantCallback(6)));
    public static readonly DependencyProperty Trail0Property=DependencyProperty.Register("Trail0",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(-1000,-1000,0),PixelShaderConstantCallback(7)));
    public static readonly DependencyProperty Trail1Property=DependencyProperty.Register("Trail1",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(-1000,-1000,0),PixelShaderConstantCallback(8)));
    public static readonly DependencyProperty Trail2Property=DependencyProperty.Register("Trail2",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(-1000,-1000,0),PixelShaderConstantCallback(9)));
    public static readonly DependencyProperty Trail3Property=DependencyProperty.Register("Trail3",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(-1000,-1000,0),PixelShaderConstantCallback(10)));
    public static readonly DependencyProperty Trail4Property=DependencyProperty.Register("Trail4",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(-1000,-1000,0),PixelShaderConstantCallback(11)));
    public static readonly DependencyProperty TintProperty=DependencyProperty.Register("Tint",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(.6,.8,.9),PixelShaderConstantCallback(12)));
    public static readonly DependencyProperty LightProperty=DependencyProperty.Register("Light",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(1,1,1),PixelShaderConstantCallback(13)));
    public static readonly DependencyProperty LevelProperty=DependencyProperty.Register("Level",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(.8d,PixelShaderConstantCallback(14)));
    public static readonly DependencyProperty DeepProperty=DependencyProperty.Register("Deep",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(.04,.08,.12),PixelShaderConstantCallback(15)));
    public static readonly DependencyProperty BodyProperty=DependencyProperty.Register("Body",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(.10,.22,.28),PixelShaderConstantCallback(16)));
    public static readonly DependencyProperty AirProperty=DependencyProperty.Register("Air",typeof(Point3D),typeof(WaterEffect),new UIPropertyMetadata(new Point3D(.05,.03,.09),PixelShaderConstantCallback(17)));
    public static readonly DependencyProperty SceneProperty=DependencyProperty.Register("Scene",typeof(double),typeof(WaterEffect),new UIPropertyMetadata(0d,PixelShaderConstantCallback(18)));
    public WaterEffect()
    {
        PixelShader=compiled;
        UpdateShaderValue(InputProperty);
        foreach(var property in new[]{TimeProperty,ResolutionProperty,PointerProperty,BassProperty,MidProperty,TrebleProperty,EnergyProperty,Trail0Property,Trail1Property,Trail2Property,Trail3Property,Trail4Property,TintProperty,LightProperty,LevelProperty,DeepProperty,BodyProperty,AirProperty,SceneProperty})UpdateShaderValue(property);
    }
    public Brush Input{get=>(Brush)GetValue(InputProperty);set=>SetValue(InputProperty,value);}
    public double Time{get=>(double)GetValue(TimeProperty);set=>SetValue(TimeProperty,value);}
    public Point Resolution{get=>(Point)GetValue(ResolutionProperty);set=>SetValue(ResolutionProperty,value);}
    public Point Pointer{get=>(Point)GetValue(PointerProperty);set=>SetValue(PointerProperty,value);}
    public double Bass{get=>(double)GetValue(BassProperty);set=>SetValue(BassProperty,value);}
    public double Mid{get=>(double)GetValue(MidProperty);set=>SetValue(MidProperty,value);}
    public double Treble{get=>(double)GetValue(TrebleProperty);set=>SetValue(TrebleProperty,value);}
    public double Energy{get=>(double)GetValue(EnergyProperty);set=>SetValue(EnergyProperty,value);}
    public Point3D Trail0{get=>(Point3D)GetValue(Trail0Property);set=>SetValue(Trail0Property,value);}
    public Point3D Trail1{get=>(Point3D)GetValue(Trail1Property);set=>SetValue(Trail1Property,value);}
    public Point3D Trail2{get=>(Point3D)GetValue(Trail2Property);set=>SetValue(Trail2Property,value);}
    public Point3D Trail3{get=>(Point3D)GetValue(Trail3Property);set=>SetValue(Trail3Property,value);}
    public Point3D Trail4{get=>(Point3D)GetValue(Trail4Property);set=>SetValue(Trail4Property,value);}
    public Point3D Tint{get=>(Point3D)GetValue(TintProperty);set=>SetValue(TintProperty,value);}
    public Point3D Light{get=>(Point3D)GetValue(LightProperty);set=>SetValue(LightProperty,value);}
    // Water depth as a fraction of the dock height; 0.80 leaves a fifth for the air.
    public double Level{get=>(double)GetValue(LevelProperty);set=>SetValue(LevelProperty,value);}
    public Point3D Deep{get=>(Point3D)GetValue(DeepProperty);set=>SetValue(DeepProperty,value);}
    public Point3D Body{get=>(Point3D)GetValue(BodyProperty);set=>SetValue(BodyProperty,value);}
    public Point3D Air{get=>(Point3D)GetValue(AirProperty);set=>SetValue(AirProperty,value);}
    // 1 = the layer's fill is a rendered scene to refract, 0 = the painted water.
    public double Scene{get=>(double)GetValue(SceneProperty);set=>SetValue(SceneProperty,value);}
}
