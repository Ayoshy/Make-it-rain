using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>Cadence et cache d'une boutique : une requête lente, espacée, gardée sur disque.</summary>
public sealed record PriceSourceOptions(
    string CacheDirectory,
    TimeSpan? Timeout=null,
    TimeSpan? MinimumInterval=null,
    TimeSpan? SearchCache=null,
    TimeSpan? ProductCache=null)
{
    public TimeSpan RequestTimeout=>Timeout??TimeSpan.FromSeconds(20);
    public TimeSpan Interval=>MinimumInterval??TimeSpan.FromSeconds(4);
    public TimeSpan SearchFreshness=>SearchCache??TimeSpan.FromMinutes(30);
    public TimeSpan ProductFreshness=>ProductCache??TimeSpan.FromHours(6);
}

public interface IPriceSource
{
    string Id{get;}
    string Name{get;}
    Task<IReadOnlyList<Product>> SearchAsync(ShoppingSpec spec,int limit,CancellationToken cancellation);
    Task<PricePoint?> GetPriceAsync(Product product,CancellationToken cancellation);
}

/// <summary>
/// Base des boutiques sans API publique : recherche HTML, fiche produit JSON-LD, cache disque.
/// Une boutique qui refuse la lecture lève <see cref="PriceSourceUnavailableException"/> ;
/// aucun prix n'est deviné à sa place.
/// </summary>
public abstract partial class HtmlPriceSource(HttpClient http,PriceSourceOptions options):IPriceSource
{
    static readonly SemaphoreSlim Gate=new(1,1);
    static readonly Dictionary<string,DateTimeOffset> LastRequest=new(StringComparer.OrdinalIgnoreCase);
    public abstract string Id{get;}
    public abstract string Name{get;}

    protected abstract string SearchUrl(ShoppingSpec spec);
    protected abstract IReadOnlyList<Product> ParseSearch(string html,ShoppingSpec spec,int limit);
    protected abstract PricePoint? ParseProductPage(string html,Product product);

    public async Task<IReadOnlyList<Product>> SearchAsync(ShoppingSpec spec,int limit,CancellationToken cancellation)
    {
        var url=SearchUrl(spec);
        var html=await FetchAsync(url,options.SearchFreshness,cancellation);
        return ParseSearch(html,spec,limit);
    }

    public async Task<PricePoint?> GetPriceAsync(Product product,CancellationToken cancellation)
    {
        if(product.Url.Length==0)return null;
        var html=await FetchAsync(product.Url,options.ProductFreshness,cancellation);
        return ParseProductPage(html,product);
    }

    /// <summary>Page mise en cache disque, relue sans réseau tant qu'elle est fraîche.</summary>
    protected async Task<string> FetchAsync(string url,TimeSpan freshness,CancellationToken cancellation)
    {
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24];
        var file=Path.Combine(options.CacheDirectory,$"{Id}-{key}.html");
        if(File.Exists(file)&&DateTimeOffset.UtcNow-File.GetLastWriteTimeUtc(file)<freshness)
            return await File.ReadAllTextAsync(file,cancellation);
        var html=await DownloadAsync(url,cancellation);
        Directory.CreateDirectory(options.CacheDirectory);
        await File.WriteAllTextAsync(file,html,cancellation);
        return html;
    }

    async Task<string> DownloadAsync(string url,CancellationToken cancellation)
    {
        var host=new Uri(url).Host;
        await Gate.WaitAsync(cancellation);
        try
        {
            if(LastRequest.TryGetValue(host,out var last))
            {
                var wait=options.Interval-(DateTimeOffset.UtcNow-last);
                if(wait>TimeSpan.Zero)await Task.Delay(wait,cancellation);
            }
            LastRequest[host]=DateTimeOffset.UtcNow;
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.TryAddWithoutValidation("User-Agent",UserAgent);
            request.Headers.TryAddWithoutValidation("Accept","text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language","fr-FR,fr;q=0.9");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(options.RequestTimeout);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests
                or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.Unauthorized)
                throw new PriceSourceUnavailableException($"{Name} refuse la lecture automatique ({(int)response.StatusCode}).");
            if(!response.IsSuccessStatusCode)
                throw new PriceSourceUnavailableException($"{Name} a répondu {(int)response.StatusCode}.");
            var bytes=await response.Content.ReadAsByteArrayAsync(timeout.Token);
            var content=Decode(bytes,response.Content.Headers.ContentType?.CharSet);
            if(Challenge().IsMatch(content))
                throw new PriceSourceUnavailableException($"{Name} demande une vérification de navigateur.");
            return content;
        }
        catch(HttpRequestException e)
        {
            throw new PriceSourceUnavailableException($"{Name} injoignable : {e.Message}");
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested)
        {
            throw new PriceSourceUnavailableException($"{Name} n'a pas répondu en {options.RequestTimeout.TotalSeconds:N0} s.");
        }
        finally{Gate.Release();}
    }

    static string Decode(byte[] bytes,string? charset)
    {
        if(!string.IsNullOrWhiteSpace(charset))
        {
            try{return Encoding.GetEncoding(charset.Trim('"')).GetString(bytes);}catch(ArgumentException){}
        }
        var utf8=new UTF8Encoding(false,false).GetString(bytes);
        var declared=Charset().Match(utf8);
        if(declared.Success)
        {
            try{return Encoding.GetEncoding(declared.Groups[1].Value).GetString(bytes);}catch(ArgumentException){}
        }
        return utf8;
    }

    protected static string Unescape(string text)=>System.Net.WebUtility.HtmlDecode(text).Trim();

    protected static decimal? Amount(string text)
    {
        var digits=new string(text.Where(c=>char.IsDigit(c)||c is ',' or '.' or '\u00A0' or ' ').ToArray())
            .Replace("\u00A0","").Replace(" ","");
        if(digits.Length==0)return null;
        if(digits.Contains(','))digits=digits.Replace(".","").Replace(',','.');
        else if(digits.Count(c=>c=='.')>1)digits=digits.Replace(".","");
        return decimal.TryParse(digits,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)&&value>0?value:null;
    }

    /// <summary>Marque déduite du titre : les mots avant le premier qui porte un chiffre ou une référence.</summary>
    protected static string GuessBrand(string title)
    {
        var words=title.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var brand=new List<string>();
        foreach(var word in words)
        {
            if(word.Any(char.IsDigit)||Regex.IsMatch(word,"^[A-Z0-9-]{3,}$"))break;
            if(brand.Count>=3)break;
            brand.Add(word);
        }
        return brand.Count==0?"":string.Join(' ',brand);
    }

    protected static string GuessModel(string title,string brand)
    {
        var model=title.StartsWith(brand,StringComparison.OrdinalIgnoreCase)?title[brand.Length..].Trim():title;
        var match=Regex.Match(model,@"\b([A-Z0-9][A-Z0-9-]{3,})\b");
        return match.Success?match.Groups[1].Value:"";
    }

    const string UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    [GeneratedRegex(@"charset\s*=\s*[""']?([\w-]+)",RegexOptions.IgnoreCase)]
    private static partial Regex Charset();

    [GeneratedRegex(@"captcha|Just a moment|Something has gone wrong|Access Denied|Vérifiez que vous êtes humain",RegexOptions.IgnoreCase)]
    private static partial Regex Challenge();
}

/// <summary>Lecture du bloc JSON-LD « Product » d'une fiche boutique : prix, devise, disponibilité.</summary>
public static partial class JsonLdProduct
{
    public sealed record Offer(decimal? Price,string Currency,bool InStock,string Name,string Image,string Brand);

    public static Offer? Read(string html)
    {
        foreach(Match block in Script().Matches(html))
        {
            var body=block.Groups[1].Value;
            if(!body.Contains("\"Product\"",StringComparison.Ordinal))continue;
            var json=FirstObject(body);
            if(json is null)continue;
            try
            {
                using var document=JsonDocument.Parse(json);
                var offer=Read(document.RootElement);
                if(offer is not null)return offer;
            }
            catch(JsonException){}
        }
        // Le corps JSON-LD peut contenir un tableau de produits : le premier prix suffit.
        var price=PriceField().Match(html);
        return price.Success?new Offer(HtmlPriceSourceAmount(price.Groups[1].Value),"EUR",!html.Contains("OutOfStock",StringComparison.Ordinal),"","",""):null;
    }

    static Offer? Read(JsonElement root)
    {
        if(root.ValueKind==JsonValueKind.Array)
        {
            foreach(var item in root.EnumerateArray())if(Read(item) is {} nested)return nested;
            return null;
        }
        if(root.ValueKind!=JsonValueKind.Object)return null;
        var type=root.TryGetProperty("@type",out var kind)?kind.ToString():"";
        if(!type.Contains("Product",StringComparison.OrdinalIgnoreCase))return null;
        string name=Text(root,"name"), image=Text(root,"image");
        string brand=root.TryGetProperty("brand",out var brandNode)
            ?brandNode.ValueKind==JsonValueKind.Object?Text(brandNode,"name"):brandNode.ToString()
            :"";
        if(!root.TryGetProperty("offers",out var offers))return new Offer(null,"EUR",true,name,image,brand);
        if(offers.ValueKind==JsonValueKind.Array&&offers.GetArrayLength()>0)offers=offers[0];
        if(offers.ValueKind!=JsonValueKind.Object)return new Offer(null,"EUR",true,name,image,brand);
        string currency=Text(offers,"priceCurrency");
        string availability=Text(offers,"availability");
        var price=Number(Text(offers,"price"))??Number(Text(offers,"lowPrice"));
        bool inStock=!availability.Contains("OutOfStock",StringComparison.OrdinalIgnoreCase)&&!availability.Contains("SoldOut",StringComparison.OrdinalIgnoreCase);
        return new Offer(price,currency.Length==0?"EUR":currency,inStock,name,image,brand);
    }

    static string Text(JsonElement node,string name)
    {
        if(!node.TryGetProperty(name,out var value))return "";
        return value.ValueKind switch
        {
            JsonValueKind.String=>value.GetString()??"",
            JsonValueKind.Array=>value.EnumerateArray().Select(item=>item.ValueKind==JsonValueKind.String?item.GetString():"").FirstOrDefault(text=>!string.IsNullOrWhiteSpace(text))??"",
            JsonValueKind.Object=>Text(value,"url") is {Length:>0} url?url:Text(value,"name"),
            _=>value.ToString()
        };
    }

    static decimal? Number(string text)=>decimal.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)?value:null;

    static decimal? HtmlPriceSourceAmount(string text)
    {
        var digits=text.Replace(".","").Replace(',','.');
        return decimal.TryParse(digits,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)?value:null;
    }

    /// <summary>Premier objet JSON complet d'un bloc : les accolades imbriquées sont comptées.</summary>
    static string? FirstObject(string text)
    {
        int start=text.IndexOf('{');
        if(start<0)return null;
        int depth=0;bool quoted=false;bool escaped=false;
        for(int index=start;index<text.Length;index++)
        {
            char c=text[index];
            if(escaped){escaped=false;continue;}
            if(c=='\\'&&quoted){escaped=true;continue;}
            if(c=='"')quoted=!quoted;
            if(quoted)continue;
            if(c=='{')depth++;
            else if(c=='}'&&--depth==0)return text[start..(index+1)];
        }
        return null;
    }

    [GeneratedRegex(@"<script[^>]*application/ld\+json[^>]*>(.*?)</script>",RegexOptions.IgnoreCase|RegexOptions.Singleline)]
    private static partial Regex Script();

    [GeneratedRegex(@"""price""\s*:\s*""?([\d.,]+)",RegexOptions.IgnoreCase)]
    private static partial Regex PriceField();
}
