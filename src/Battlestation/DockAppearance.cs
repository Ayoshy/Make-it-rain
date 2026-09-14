using System.Windows.Media;

namespace Battlestation;
// Shared WPF chrome. The desktop material itself is drawn by NativeBackground.
internal static class DockAppearance
{
    public const string TextFont="Segoe UI Variable Text",NumberFont="Bahnschrift";
    public static FontFamily UiFont {get;}=new(TextFont);
    internal const string Ink="#DAD2E7",Muted="#AE9FBD",ButtonRim="#22DACDEC";
    internal const double HeaderPoints=10,PanelRadius=24,ButtonRadius=15;
    public static readonly LinearGradientBrush ButtonFill=Fill(false),ButtonHover=Fill(true);
    static LinearGradientBrush Fill(bool hover)
    {
        var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(hover?"#26EEE1F8":"#14DED2EE"),(Color)ColorConverter.ConvertFromString(hover?"#18B8A8D4":"#0CB29CCC"),90);
        brush.Freeze();return brush;
    }
}
