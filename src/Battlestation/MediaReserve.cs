using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace Battlestation;
internal sealed record SavedMedia(string Url,string Provider,string Title,DateTimeOffset Added);
internal sealed class MediaReserve : IDisposable
{
    readonly string file;
    readonly HttpClient http;
    readonly SemaphoreSlim fetchSlots=new(2);
    readonly CancellationTokenSource stop=new();
    readonly Dictionary<string,BitmapSource> covers=[];
    internal List<SavedMedia> Items {get;private set;}=[];
    internal string Error {get;private set;}="";
    internal event Action? Changed;
    internal MediaReserve(string path,HttpMessageHandler? transport=null)
    {
        file=path;
        http=new HttpClient(transport??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(5)};
        try
        {
            if(File.Exists(file))Items=(JsonSerializer.Deserialize<List<SavedMedia>>(File.ReadAllText(file))??[])
                .Where(x=>x is not null&&MediaLink.Parse(x.Url) is {} parsed&&parsed.Url==x.Url).Select(x=>x with{Provider=MediaLink.Parse(x.Url)!.Provider,Title=new string((x.Title??"").Where(c=>!char.IsControl(c)).Take(200).ToArray())}).DistinctBy(x=>x.Url).Take(100).ToList();
        }
        catch(Exception e) when(e is IOException or JsonException){Error="Réserve indisponible";}
    }
    internal BitmapSource? Cover(string url)=>covers.GetValueOrDefault(url);
    internal void Add(MediaLink link)
    {
        var prior=Items.FirstOrDefault(x=>x.Url==link.Url);
        var item=prior is null?new SavedMedia(link.Url,link.Provider,link.Provider,DateTimeOffset.Now):prior with{Added=DateTimeOffset.Now};
        var next=Items.Where(x=>x.Url!=link.Url).Prepend(item).Take(100).ToList();
        if(!Save(next))return;
        if(prior is null||!covers.ContainsKey(link.Url))_ = Enrich(link);
    }
    internal void Warm(){foreach(var item in Items.Take(8))if(!covers.ContainsKey(item.Url))_ = Enrich(new(item.Url,item.Provider));}
    internal void Remove(string url){if(Save(Items.Where(x=>x.Url!=url).ToList()))covers.Remove(url);}
    bool Save(List<SavedMedia> next)
    {
        try{DesktopSettings.Write(file,JsonSerializer.Serialize(next));Items=next;Error="";}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){Error="Impossible de sauvegarder la réserve";}
        Changed?.Invoke();return Error=="";
    }
    async Task Enrich(MediaLink link)
    {
        bool acquired=false;
        try
        {
            await fetchSlots.WaitAsync(stop.Token);acquired=true;
            string api=link.Provider=="Spotify"?"https://open.spotify.com/oembed?url=":"https://www.youtube.com/oembed?format=json&url=";
            using var response=await http.GetAsync(api+Uri.EscapeDataString(link.Url),HttpCompletionOption.ResponseHeadersRead,stop.Token);response.EnsureSuccessStatusCode();
            var bytes=await Bounded(response,64*1024,stop.Token);using var document=JsonDocument.Parse(bytes);var root=document.RootElement;
            string title=root.TryGetProperty("title",out var value)?value.GetString()??link.Provider:link.Provider;
            title=new string(title.Where(c=>!char.IsControl(c)).Take(200).ToArray());
            if(string.IsNullOrWhiteSpace(title))title=link.Provider;
            if(stop.IsCancellationRequested||!Items.Any(x=>x.Url==link.Url))return;
            Save(Items.Select(x=>x.Url==link.Url?x with{Title=title}:x).ToList());
            if(root.TryGetProperty("thumbnail_url",out var thumb)&&Uri.TryCreate(thumb.GetString(),UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host is "i.ytimg.com" or "i.scdn.co" or "mosaic.scdn.co" or "image-cdn-ak.spotifycdn.com")
            {
                using var picture=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,stop.Token);picture.EnsureSuccessStatusCode();
                var data=await Bounded(picture,2*1024*1024,stop.Token);
                using var stream=new MemoryStream(data);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=128;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
                if(stop.IsCancellationRequested||!Items.Any(x=>x.Url==link.Url))return;
                if(covers.Count>=32)covers.Remove(covers.Keys.First());covers[link.Url]=bitmap;Changed?.Invoke();
            }
        }
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or JsonException or IOException or NotSupportedException or ArgumentException){}
        finally{if(acquired)fetchSlots.Release();}
    }
    static async Task<byte[]> Bounded(HttpResponseMessage response,int max,CancellationToken token)
    {
        if(response.Content.Headers.ContentLength>max)throw new IOException();
        using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[8192];
        int count;while((count=await input.ReadAsync(buffer,token))>0){if(output.Length+count>max)throw new IOException();output.Write(buffer,0,count);}return output.ToArray();
    }
    public void Dispose(){stop.Cancel();http.Dispose();}
}
