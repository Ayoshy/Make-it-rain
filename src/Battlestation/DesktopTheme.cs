using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Battlestation;
internal sealed record ThemeDefinition(string Id,string Name,string Base,string Light,string Secondary,string Glass,string Rim,string Edge,string[] Pearl,string[] Active,Dictionary<string,string> Colors);
internal static class DesktopTheme
{
    internal static readonly ThemeDefinition[] Definitions=JsonSerializer.Deserialize<ThemeDefinition[]>(new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Battlestation.Themes.json")!).ReadToEnd())!;
    internal static ThemeDefinition Current {get;private set;}=Definitions[0];
    internal static string PreviousId {get;private set;}="vice-city";
    internal static event Action? Changed;
    static readonly Dictionary<string,Swatch> swatches=new(StringComparer.OrdinalIgnoreCase);
    internal static Color Color(string original)=>(Color)ColorConverter.ConvertFromString(Current.Colors.GetValueOrDefault(original.ToUpperInvariant(),original));
    internal static SolidColorBrush Brush(string original)
    {
        if(!swatches.TryGetValue(original,out var swatch))swatches[original]=swatch=new Swatch(Color(original));
        return swatch.Brush;
    }
    internal static LinearGradientBrush Gradient(string first,string last,double angle)
    {
        var brush=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(Math.Sin(angle*Math.PI/180),Math.Cos(angle*Math.PI/180))};
        foreach(var (color,offset) in new[]{(first,0d),(last,1d)})
        {
            var stop=new GradientStop{Offset=offset};BindingOperations.SetBinding(stop,GradientStop.ColorProperty,new Binding("Color"){Source=Brush(color)});brush.GradientStops.Add(stop);
        }
        // Preserve the original WPF angle geometry.
        var geometry=new LinearGradientBrush(Colors.Black,Colors.White,angle);brush.StartPoint=geometry.StartPoint;brush.EndPoint=geometry.EndPoint;
        return brush;
    }
    internal static void Select(string id,bool animate=true)
    {
        var next=Definitions.Single(d=>d.Id==id);if(next==Current)return;
        PreviousId=Current.Id;Current=next;
        foreach(var (original,swatch) in swatches)swatch.Set(Color(original),animate);
        Changed?.Invoke();
    }
    // A binding prevents WPF styles from freezing shared brushes. Completed fades release their clocks.
    sealed class Swatch : INotifyPropertyChanged
    {
        public Color Value {get;private set;}
        public SolidColorBrush Brush {get;}=new();
        public event PropertyChangedEventHandler? PropertyChanged;
        internal Swatch(Color color){Value=color;BindingOperations.SetBinding(Brush,SolidColorBrush.ColorProperty,new Binding(nameof(Value)){Source=this});}
        internal void Set(Color color,bool animate)
        {
            var from=Brush.Color;Brush.BeginAnimation(SolidColorBrush.ColorProperty,null);Value=color;PropertyChanged?.Invoke(this,new(nameof(Value)));
            if(animate&&from!=color)Brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(from,color,TimeSpan.FromMilliseconds(240)){FillBehavior=FillBehavior.Stop});
        }
    }
}
[MarkupExtensionReturnType(typeof(SolidColorBrush))]
public sealed class ThemeBrushExtension(string color) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)=>DesktopTheme.Brush(color);
}
[MarkupExtensionReturnType(typeof(Color))]
public sealed class ThemeColorExtension(string color) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)=>new Binding("Color"){Source=DesktopTheme.Brush(color)}.ProvideValue(serviceProvider);
}
