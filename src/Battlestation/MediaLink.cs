using System.Text.RegularExpressions;

namespace Battlestation;
internal sealed record MediaLink(string Url,string Provider)
{
    internal static MediaLink? Parse(string? text)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>2048)return null;
        if(!Uri.TryCreate(text.Trim(),UriKind.Absolute,out var url)||url.Scheme is not ("https" or "http")||url.UserInfo!=""||!url.IsDefaultPort)return null;
        string[] parts=url.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);
        if(url.Host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtu.be")
        {
            var query=new Dictionary<string,string>();
            foreach(string pair in url.Query.TrimStart('?').Split('&')){var p=pair.Split('=',2);if(p.Length==2)query[p[0]]=Uri.UnescapeDataString(p[1]);}
            string? id=url.Host=="youtu.be"&&parts.Length==1?parts[0]:parts.Length==2&&parts[0] is "shorts" or "live" or "embed"?parts[1]:parts.Length==1&&parts[0]=="watch"?query.GetValueOrDefault("v"):null;
            if(id is not null&&Regex.IsMatch(id,"^[a-zA-Z0-9_-]{11}$"))return new("https://www.youtube.com/watch?v="+id,"YouTube");
            if(parts.Length==1&&parts[0]=="playlist"&&query.GetValueOrDefault("list") is {} list&&Regex.IsMatch(list,"^[a-zA-Z0-9_-]{10,128}$"))return new("https://www.youtube.com/playlist?list="+list,"YouTube");
        }
        if(url.Host=="open.spotify.com")
        {
            if(parts.Length==3&&Regex.IsMatch(parts[0],"^intl-[a-z]{2}$"))parts=parts[1..];
            if(parts.Length==2&&parts[0] is "track" or "album" or "playlist" or "episode" or "show" or "artist"&&Regex.IsMatch(parts[1],"^[a-zA-Z0-9]{22}$"))return new("https://open.spotify.com/"+string.Join('/',parts),"Spotify");
        }
        if(url.Host=="spotify.link"&&parts.Length==1&&Regex.IsMatch(parts[0],"^[a-zA-Z0-9_-]{4,128}$"))return new("https://spotify.link/"+parts[0],"Spotify");
        return null;
    }
}
