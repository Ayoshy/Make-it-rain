using System.IO;
using Battlestation;
namespace Battlestation;
// Settings UI tests never start hardware, audio, a desktop renderer or terminal sessions.
internal sealed class Station
{
    internal DesktopSettings Settings {get;private set;}
    internal DesktopLayout Layout {get;}
    internal string ProjectRoot=>Settings.ProjectRoot;
    internal string TargetDate {get;private set;}="2027-01-01T00:00:00+01:00";
    internal bool? PreviewReactiveAudio {get;set;}
    internal double? PreviewAudioIntensity {get;set;}
    internal bool ClipboardRegistered=>false;
    internal TerminalSession? Terminal=>null;
    internal Action? Saved;
    internal Station(DesktopSettings settings,DesktopLayout layout){Settings=settings;Layout=layout;}
    internal void PreviewAppearance(string id,bool animate,double opacity)=>DesktopTheme.Select(id,false);
    internal void ApplySettings(DesktopSettings settings,string target){Settings=settings.Validate();TargetDate=target;PreviewAppearance(settings.ThemeId,settings.AnimateBackground,settings.GlassOpacity);Saved?.Invoke();}
    internal void ShowReserve(){}
}
internal sealed class TerminalSession {internal int Pid=>0;internal bool ThemeSupported=>false;}
internal sealed class PaletteHotkey {internal bool Registered=>true;internal void Retry(){}}
internal static class Native
{
    internal struct CursorPoint{public int X,Y;}
    internal static bool GetCursorPos(out CursorPoint point){point=new CursorPoint{X=0,Y=0};return false;}
    internal static uint GetDpiForWindow(nint window)=>96;
    internal static bool SetWindowPos(nint a,nint b,int x,int y,int w,int h,uint flags)=>false;
    internal static bool SetForegroundWindow(nint window)=>false;
}
