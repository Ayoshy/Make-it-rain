using System.Windows.Media;
using Battlestation;

internal static class ThemeTransitionTests
{
    internal static void Run(Action<bool,string> check)
    {
        string original=DesktopTheme.Current.Id;
        var first=DesktopTheme.Definitions[0];
        var second=DesktopTheme.Definitions.First(theme=>theme.Id!=first.Id);
        string key=first.Colors.Keys.First(k=>second.Colors.TryGetValue(k,out var value)&&value!=first.Colors[k]);
        try
        {
            DesktopTheme.Select(first.Id,false);
            var brush=DesktopTheme.Brush(key);
            DesktopTheme.Select(second.Id,true);
            check(brush.HasAnimatedProperties,"L’aperçu du thème conserve son fondu de couleur");
            // A scene can select the same theme before the settings preview ends.
            DesktopTheme.Select(second.Id,false);
            check(!brush.HasAnimatedProperties&&brush.Color==DesktopTheme.Color(key),"Une scène interrompt le fondu même si le thème est inchangé");
            DesktopTheme.Select(first.Id,false);
            check(ReferenceEquals(brush,DesktopTheme.Brush(key))&&!brush.HasAnimatedProperties&&brush.Color==DesktopTheme.Color(key),"La scène applique la couleur finale sans remplacer les brosses liées");
            check(!DesktopTheme.LastChangeAnimated,"Les surfaces savent appliquer le thème sans fondu pendant la scène");
        }
        finally { DesktopTheme.Select(original,false); }
    }
}
