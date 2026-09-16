using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Battlestation;
internal sealed record DesktopSettings(string ProjectRoot,string WeatherCity,double Latitude,double Longitude,bool AnimateBackground=true,double GlassOpacity=.46,bool ReactiveAudio=true,double AudioIntensity=.55,bool KeepMediaLinks=true,bool GridEnabled=true,int GridStep=8,bool LinkDocks=false,string ThemeId="vice-city",bool DualSenseTouchTrail=false)
{
    public static DesktopSettings Load(string file,DesktopSettings defaults)
    {
        if(!File.Exists(file))return defaults;
        try{return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(file))?.Validate(false)??defaults;}
        catch(Exception e) when(e is JsonException or ArgumentException){return defaults;}
    }
    public DesktopSettings Validate(bool checkDirectory=true)
    {
        if(GridStep is <4 or >64)throw new ArgumentException("Le pas de grille doit être compris entre 4 et 64.");
        if(string.IsNullOrWhiteSpace(ProjectRoot)||ProjectRoot.IndexOfAny(['\r','\n','\0'])>=0)throw new ArgumentException("Choisis un dossier de projets valide.");
        string root=Path.GetFullPath(Environment.ExpandEnvironmentVariables(ProjectRoot.Trim()));
        if(checkDirectory&&!Directory.Exists(root))throw new ArgumentException("Le dossier des projets est introuvable.");
        if(string.IsNullOrWhiteSpace(WeatherCity)||WeatherCity.Length>120||WeatherCity.IndexOfAny(['\r','\n','\0'])>=0)throw new ArgumentException("Indique le nom de la ville.");
        if(!double.IsFinite(Latitude)||Latitude is < -90 or > 90||!double.IsFinite(Longitude)||Longitude is < -180 or > 180)throw new ArgumentException("Les coordonnées météo sont invalides.");
        if(!double.IsFinite(GlassOpacity)||GlassOpacity is < .05 or > .85)throw new ArgumentException("La transparence du verre est invalide.");
        if(!double.IsFinite(AudioIntensity)||AudioIntensity is < 0 or > 1)throw new ArgumentException("L’intensité musicale est invalide.");
        if(ThemeId is not ("vice-city" or "obsidienne" or "aurore"))throw new ArgumentException("Thème inconnu.");
        return this with{ProjectRoot=root,WeatherCity=WeatherCity.Trim()};
    }
    public void Save(string file)=>Write(file,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
    internal static void Write(string file,string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file+".tmp",text);File.Move(file+".tmp",file,true);
    }
    public string[] DeskIni()=>["[Desk]","ProjectRoot="+ProjectRoot,"WeatherCity="+WeatherCity,"WeatherLatitude="+Latitude.ToString(CultureInfo.InvariantCulture),"WeatherLongitude="+Longitude.ToString(CultureInfo.InvariantCulture)];
}
