using System.Net;
using System.Text;
using System.Text.Json;

namespace Battlestation.Shopping;

public sealed class SerperSearchClient(HttpClient http,string apiKey):IWebSearchClient
{
    public const string Endpoint="https://google.serper.dev/search";
    public string Id=>"serper";
    public string Name=>"Serper";
    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query,CancellationToken cancellation)
    {
        if(string.IsNullOrWhiteSpace(query))return [];
        if(string.IsNullOrWhiteSpace(apiKey))throw new PriceSourceUnavailableException("Clé Serper absente : ouvre les réglages du dock Achats.");
        using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint)
        {Content=new StringContent(JsonSerializer.Serialize(new{q=query.Trim(),gl="fr",hl="fr",num=10}),Encoding.UTF8,"application/json")};
        request.Headers.Add("X-API-KEY",apiKey);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode)throw new PriceSourceUnavailableException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden=>"Serper refuse la clé ou l'accès au service. Vérifie la clé et les crédits dans ton compte.",
                HttpStatusCode.TooManyRequests=>"Serper limite temporairement les recherches. Réessaie plus tard.",
                _=>$"Serper n'a pas pu rechercher (HTTP {(int)response.StatusCode})."
            });
            const int maximum=2*1024*1024;
            if(response.Content.Headers.ContentLength>maximum)throw new PriceSourceUnavailableException("Serper : réponse trop volumineuse.");
            using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var bytes=new MemoryStream();var block=new byte[16384];int read;
            while((read=await stream.ReadAsync(block,deadline.Token))>0)
            {if(bytes.Length+read>maximum)throw new PriceSourceUnavailableException("Serper : réponse trop volumineuse.");bytes.Write(block,0,read);}
            using var json=JsonDocument.Parse(bytes.ToArray());
            if(json.RootElement.ValueKind!=JsonValueKind.Object||!json.RootElement.TryGetProperty("organic",out var results)||results.ValueKind!=JsonValueKind.Array)
                throw new PriceSourceUnavailableException("Serper a renvoyé une réponse de recherche inattendue.");
            var found=new List<WebSearchResult>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in results.EnumerateArray())
            {
                string title=Text(item,"title"),link=Text(item,"link"),snippet=Text(item,"snippet");
                if(title.Length==0||link.Length>4096||!Uri.TryCreate(link,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")
                    ||uri.UserInfo.Length>0||!seen.Add(uri.AbsoluteUri))continue;
                found.Add(new(uri.AbsoluteUri,title[..Math.Min(title.Length,400)],snippet[..Math.Min(snippet.Length,3000)]));
                if(found.Count==8)break;
            }
            return found;
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new PriceSourceUnavailableException("Serper n'a pas répondu dans le délai de 20 s.");}
        catch(HttpRequestException){throw new PriceSourceUnavailableException("Serper est momentanément injoignable.");}
        catch(IOException){throw new PriceSourceUnavailableException("La réponse Serper a été interrompue.");}
        catch(JsonException){throw new PriceSourceUnavailableException("Serper a renvoyé une réponse illisible.");}
    }
    static string Text(JsonElement item,string key)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
}

/// <summary>La clé enregistrée active Serper ; sans clé, le fonctionnement Tavily existant reste disponible.</summary>
public sealed class ConfiguredWebSearchClient(HttpClient http,Func<string?> key):IWebSearchClient
{
    public string Id=>string.IsNullOrWhiteSpace(key())?"tavily":"serper";
    public string Name=>Id=="serper"?"Serper":"Tavily";
    public Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query,CancellationToken cancellation)
        =>key() is {Length:>0} value?new SerperSearchClient(http,value).SearchAsync(query,cancellation):new TavilySearchClient(http).SearchAsync(query,cancellation);
}
