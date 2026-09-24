using System.Net;
using System.Text;
using System.Text.Json;

namespace Battlestation.Shopping;

public sealed record WebSearchResult(string Url,string Title,string Snippet);
public interface IWebSearchClient
{
    string Id{get;}
    string Name{get;}
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query,CancellationToken cancellation);
}

/// <summary>Recherche générale via l'accès gratuit sans clé officiellement proposé par Tavily.</summary>
public sealed class TavilySearchClient(HttpClient http):IWebSearchClient
{
    public string Id=>"tavily";
    public string Name=>"Tavily";
    public const string Endpoint="https://api.tavily.com/search";
    const int MaxResults=8,MaxResponseBytes=2*1024*1024;

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(string.IsNullOrWhiteSpace(query))return [];
        using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint)
        {
            Content=new StringContent(JsonSerializer.Serialize(new
            {
                query=query.Trim(),topic="general",country="france",language="fr",
                search_depth="basic",auto_parameters=false,max_results=MaxResults,
                include_answer=false,include_raw_content=false,include_images=false,include_usage=true
            }),Encoding.UTF8,"application/json")
        };
        request.Headers.Add("X-Tavily-Access-Mode","keyless");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode)throw new PriceSourceUnavailableException(Error(response.StatusCode));
            if(response.Content.Headers.ContentLength>MaxResponseBytes)
                throw new PriceSourceUnavailableException("Tavily : réponse de recherche trop volumineuse.");
            using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer=new MemoryStream();
            var chunk=new byte[16384];
            int count;
            while((count=await stream.ReadAsync(chunk,deadline.Token))>0)
            {
                if(buffer.Length+count>MaxResponseBytes)throw new PriceSourceUnavailableException("Tavily : réponse de recherche trop volumineuse.");
                buffer.Write(chunk,0,count);
            }
            using var json=JsonDocument.Parse(buffer.ToArray());
            if(json.RootElement.ValueKind!=JsonValueKind.Object||!json.RootElement.TryGetProperty("results",out var results)||results.ValueKind!=JsonValueKind.Array)
                throw new PriceSourceUnavailableException("Tavily : réponse de recherche inattendue.");
            var links=new List<WebSearchResult>();
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in results.EnumerateArray())
            {
                if(item.ValueKind!=JsonValueKind.Object)continue;
                var url=Text(item,"url");var title=Text(item,"title");
                if(title.Length==0||url.Length>4096||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("https" or "http")
                    ||uri.UserInfo.Length>0||uri.Host.Length==0||!seen.Add(uri.AbsoluteUri))continue;
                var snippet=Text(item,"content");
                links.Add(new(uri.AbsoluteUri,title[..Math.Min(title.Length,400)],snippet[..Math.Min(snippet.Length,3000)]));
                if(links.Count==MaxResults)break;
            }
            return links;
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested)
        {throw new PriceSourceUnavailableException("Tavily n'a pas répondu dans le délai de 20 s.");}
        catch(HttpRequestException)
        {throw new PriceSourceUnavailableException("Tavily est momentanément injoignable.");}
        catch(IOException)
        {throw new PriceSourceUnavailableException("La réponse Tavily a été interrompue. Réessayez plus tard.");}
        catch(JsonException)
        {throw new PriceSourceUnavailableException("Tavily a renvoyé une réponse de recherche illisible.");}
    }

    static string Text(JsonElement node,string property)=>node.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()?.Trim()??"":"";
    static string Error(HttpStatusCode status)=>(int)status switch
    {
        401 or 403=>"Tavily n'autorise pas l'accès gratuit pour cette requête.",
        429=>"Tavily limite temporairement les recherches gratuites. Réessayez plus tard.",
        432 or 433=>"La limite de recherche Tavily est atteinte. Réessayez plus tard.",
        400 or 422=>"Tavily n'a pas accepté les paramètres de recherche.",
        >=500=>"Tavily est momentanément indisponible.",
        _=>$"La recherche Tavily a échoué (HTTP {(int)status})."
    };
}
