using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>Liens de fiches observés dans une page catalogue, sans transformer ses cartes en offres.</summary>
public static class ProductPageLinks
{
    public static IReadOnlyList<WebSearchResult> Read(string html,string pageUrl,ShoppingSpec spec)
    {
        if(!PublicUrl(pageUrl,out var page))return [];
        // Templates can contain real rendered product anchors. Scripts, navigation
        // and forms cannot provide candidates, even when they contain HTML strings.
        var body=HtmlProductDetails.ProductContent(html);
        var typeWords=ShoppingText.Fold(spec.Category).Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(word=>word.Length>=4).ToList();
        if(typeWords.Contains("refrigerateur"))typeWords.Add("frigo");
        var found=new Dictionary<string,Link>(StringComparer.Ordinal);
        int position=0;
        foreach(Match anchor in Regex.Matches(body,@"(?<open><a\b(?:""[^""]*""|'[^']*'|[^'"">])*>)(?<body>.*?)</a>",RegexOptions.IgnoreCase|RegexOptions.Singleline))
        {
            var tag=anchor.Groups["open"].Value;
            var href=Attribute(tag,"href");
            if(href.Length==0||href.StartsWith('#')||!Uri.TryCreate(page,href,out var target)||!PublicUrl(target.AbsoluteUri,out target)
                ||!target.Host.Equals(page.Host,StringComparison.OrdinalIgnoreCase)||SamePage(target,page)||Navigation(target,tag))continue;
            var inner=anchor.Groups["body"].Value;
            var heading=Regex.Match(inner,@"<h[1-4]\b[^>]*>(.*?)</h[1-4]>",RegexOptions.IgnoreCase|RegexOptions.Singleline);
            var text=Clean(inner);
            var title=heading.Success?Clean(heading.Groups[1].Value):text;
            if(title.Length==0)title=Attribute(tag,"title");
            if(title.Length==0)
            {
                var image=Regex.Match(inner,@"<img\b(?:""[^""]*""|'[^']*'|[^'"">])*>",RegexOptions.IgnoreCase|RegexOptions.Singleline);
                if(image.Success)title=Attribute(image.Value,"alt");
            }
            var path=Uri.UnescapeDataString(target.AbsolutePath);
            var folded=ShoppingText.Fold(title+" "+path);
            bool typeMatch=typeWords.Count==0||typeWords.Any(word=>folded.Contains(word,StringComparison.Ordinal));
            bool reference=Regex.IsMatch(title,@"\b(?=[\p{L}\d-]{3,}\b)(?=[\p{L}\d-]*\p{L})(?=[\p{L}\d-]*\d)[\p{L}\d-]+\b");
            bool productPath=Regex.IsMatch(path,@"/(?:ref|p|product|products|produit|produits)/|/[^/]*\d{4,}[^/]*(?:\.html)?$",RegexOptions.IgnoreCase);
            bool productTag=Regex.IsMatch(Attribute(tag,"class"),@"product|pdt",RegexOptions.IgnoreCase);
            // Category-only anchors have neither a model/reference nor a product
            // card/path. A card without text is retained until its labelled twin.
            if(!productPath&&!reference&&!productTag)continue;
            if(!typeMatch&&!productTag&&!(productPath&&title.Length==0))continue;
            if(title.Length==0)title=path;
            int score=(typeMatch?8:0)+(reference?6:0)+(heading.Success?5:0)+(productTag?4:0)+(productPath?3:0);
            var url=new UriBuilder(target){Fragment=""}.Uri.AbsoluteUri;
            var snippet=text.Length>0?text:title;
            var candidate=new Link(new(url,Limit(title,240),Limit(snippet,360)),score,position++);
            if(found.TryGetValue(url,out var previous))
            {
                if(candidate.Score>previous.Score)found[url]=candidate with{Position=previous.Position};
            }
            else found.Add(url,candidate);
        }
        return found.Values.OrderByDescending(item=>item.Score).ThenBy(item=>item.Position).Take(24).Select(item=>item.Result).ToArray();
    }

    static bool Navigation(Uri uri,string tag)
    {
        var path=Uri.UnescapeDataString(uri.AbsolutePath);
        if(Regex.IsMatch(path,@"\.(?:jpg|jpeg|png|gif|webp|svg|pdf|js|css|woff2?|mp4|zip)$",RegexOptions.IgnoreCase))return true;
        if(Regex.IsMatch(path,@"(?:^|/)(?:c|cat|category|categories|categorie|marques|brands|search|recherche|catalogsearch|checkout|cart|panier|account|customer|login|connexion|compte|wishlist|compare|comparateur|contact|consent|cookies|privacy|conditions|livraison|paiement|faq|aide|magasins)(?:/|$)",RegexOptions.IgnoreCase))return true;
        if(Regex.IsMatch(uri.Query,@"(?:\?|&)(?:action|controller|add|add-to-cart|remove|filter[^=]*|sort[^=]*|page|p)=",RegexOptions.IgnoreCase))return true;
        return Regex.IsMatch(Attribute(tag,"class")+" "+Attribute(tag,"rel"),@"\b(?:menu|nav|add-to-cart|quick-view|quickview|compare|wishlist|nofollow-action)\b",RegexOptions.IgnoreCase);
    }

    static bool PublicUrl(string text,out Uri uri)
    {
        uri=null!;
        if(!Uri.TryCreate(text,UriKind.Absolute,out var parsed)||parsed.Scheme is not ("https" or "http")||parsed.UserInfo.Length>0||parsed.Port is not (80 or 443))return false;
        var host=parsed.DnsSafeHost.TrimEnd('.');
        if(!host.Contains('.')&&!host.Contains(':')||host.Equals("localhost",StringComparison.OrdinalIgnoreCase)
            ||Regex.IsMatch(host,@"\.(?:localhost|local|internal)$",RegexOptions.IgnoreCase))return false;
        if(IPAddress.TryParse(host,out var address))
        {
            if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
            var bytes=address.GetAddressBytes();
            if(IPAddress.IsLoopback(address))return false;
            if(address.AddressFamily==AddressFamily.InterNetwork)
            {
                if(bytes[0] is 0 or 10 or 127||bytes[0]>=224||bytes[0]==192&&bytes[1]==168
                    ||bytes[0]==172&&bytes[1] is >=16 and <=31||bytes[0]==169&&bytes[1]==254||bytes[0]==100&&bytes[1] is >=64 and <=127)return false;
            }
            else if(address.Equals(IPAddress.IPv6Any)||address.IsIPv6LinkLocal||address.IsIPv6SiteLocal||address.IsIPv6Multicast||(bytes[0]&0xfe)==0xfc)return false;
        }
        uri=parsed;return true;
    }
    static bool SamePage(Uri first,Uri second)=>first.Host==second.Host&&first.AbsolutePath.TrimEnd('/')==second.AbsolutePath.TrimEnd('/')&&first.Query==second.Query;
    static string Attribute(string tag,string name)=>WebUtility.HtmlDecode(Regex.Match(tag,$@"(?<![\w:-]){Regex.Escape(name)}\s*=\s*(?<quote>[""'])(?<value>.*?)\k<quote>",RegexOptions.IgnoreCase|RegexOptions.Singleline).Groups["value"].Value).Trim();
    static string Clean(string html)=>Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html,@"<(?:""[^""]*""|'[^']*'|[^'"">])*>"," ",RegexOptions.Singleline)),@"\s+"," ").Trim();
    static string Limit(string value,int count)=>value[..Math.Min(value.Length,count)];
    sealed record Link(WebSearchResult Result,int Score,int Position);
}
