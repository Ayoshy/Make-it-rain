using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace Battlestation;

internal sealed class LolArtwork
{
    readonly ConcurrentDictionary<string,BitmapSource> portraits=new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> requested=new(StringComparer.OrdinalIgnoreCase);
    readonly string directory;
    internal LolArtwork(string assets)=>directory=Path.Combine(assets,"LoL","champions");
    internal BitmapSource? Get(string id)=>portraits.TryGetValue(id,out var image)?image:null;
    internal Task Load(IEnumerable<string> ids)
    {
        var pending=ids.Where(id=>id.Length>0&&id.All(char.IsAsciiLetterOrDigit)&&requested.Add(id)).ToArray();
        if(pending.Length==0)return Task.CompletedTask;
        return Task.Run(()=>
        {
            foreach(string id in pending)
            {
                try
                {
                    using var stream=File.OpenRead(Path.Combine(directory,id+".png"));
                    var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
                    image.StreamSource=stream;image.EndInit();image.Freeze();portraits[id]=image;
                }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException or NotSupportedException) { }
            }
        });
    }
}
