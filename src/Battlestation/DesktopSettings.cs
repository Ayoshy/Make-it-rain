using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Battlestation;
internal sealed record DesktopSettings(string ProjectRoot,string WeatherCity,double Latitude,double Longitude,bool AnimateBackground=true,double GlassOpacity=.46)
{
    public static DesktopSettings Load(string file,DesktopSettings defaults)
    {
        if(!File.Exists(file))return defaults;
        try{return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(file))?.Validate(false)??defaults;}
        catch(Exception e) when(e is JsonException or ArgumentException){return defaults;}
    }
    public DesktopSettings Validate(bool checkDirectory=true)
    {
        if(string.IsNullOrWhiteSpace(ProjectRoot)||ProjectRoot.IndexOfAny(['\r','\n','\0'])>=0)throw new ArgumentException("Choisis un dossier de projets valide.");
        string root=Path.GetFullPath(Environment.ExpandEnvironmentVariables(ProjectRoot.Trim()));
        if(checkDirectory&&!Directory.Exists(root))throw new ArgumentException("Le dossier des projets est introuvable.");
        if(string.IsNullOrWhiteSpace(WeatherCity)||WeatherCity.Length>120||WeatherCity.IndexOfAny(['\r','\n','\0'])>=0)throw new ArgumentException("Indique le nom de la ville.");
        if(!double.IsFinite(Latitude)||Latitude is < -90 or > 90||!double.IsFinite(Longitude)||Longitude is < -180 or > 180)throw new ArgumentException("Les coordonnées météo sont invalides.");
        if(!double.IsFinite(GlassOpacity)||GlassOpacity is < .05 or > .85)throw new ArgumentException("La transparence du verre est invalide.");
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
