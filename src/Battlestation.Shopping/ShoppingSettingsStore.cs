using System.Text.Json;

namespace Battlestation.Shopping;

/// <summary>Réglages du radar dans %LOCALAPPDATA%\Battlestation\shopping.json, écrits de façon atomique.</summary>
public static class ShoppingSettingsStore
{
    public const string FileName="shopping.json";
    public const string EventsFileName="promo-events.json";
    static readonly JsonSerializerOptions Format=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};

    public static ShoppingSettings Load(string path)
    {
        if(!File.Exists(path))return ShoppingSettings.Default;
        try
        {
            var settings=JsonSerializer.Deserialize<ShoppingSettings>(File.ReadAllText(path),Format);
            return (settings??ShoppingSettings.Default).Validate();
        }
        catch(Exception e) when(e is JsonException or IOException or UnauthorizedAccessException){return ShoppingSettings.Default;}
    }

    public static void Save(string path,ShoppingSettings settings)
    {
        var temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(settings.Validate(),Format));
        File.Move(temp,path,true);
    }
}
