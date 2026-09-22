using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>Texte de la fiche produit seulement : description et caractéristiques JSON-LD.</summary>
public static partial class HtmlProductDetails
{
    public static string Read(string html)
    {
        foreach(Match block in JsonScripts().Matches(html))
        {
            try
            {
                using var json=JsonDocument.Parse(block.Groups[1].Value);
                foreach(var product in Products(json.RootElement))
                {
                    var parts=new List<string>();
                    if(product.TryGetProperty("description",out var description)&&description.ValueKind==JsonValueKind.String)
                    {
                        var value=description.GetString()??"";
                        if(!Uri.IsWellFormedUriString(value,UriKind.Absolute))parts.Add(value);
                    }
                    if(product.TryGetProperty("additionalProperty",out var properties)&&properties.ValueKind==JsonValueKind.Array)
                        foreach(var property in properties.EnumerateArray())
                            if(property.ValueKind==JsonValueKind.Object&&property.TryGetProperty("name",out var name)&&property.TryGetProperty("value",out var value))
                                parts.Add($"{name}: {value}");
                    var text=Clean(string.Join("\n",parts));
                    if(text.Length>0)return text;
                }
            }
            catch(JsonException){}
        }
        // Rue du Commerce ne fournit pas sa description dans le JSON-LD.
        var detail=Description().Match(WithoutScripts(html));
        return detail.Success?Clean(detail.Groups[1].Value):"";
    }

    static IEnumerable<JsonElement> Products(JsonElement node)
    {
        if(node.ValueKind==JsonValueKind.Array)
        {
            foreach(var item in node.EnumerateArray())foreach(var product in Products(item))yield return product;
        }
        else if(node.ValueKind==JsonValueKind.Object)
        {
            if(node.TryGetProperty("@type",out var type)&&type.ToString().Contains("Product",StringComparison.Ordinal))yield return node;
            else if(node.TryGetProperty("@graph",out var graph))foreach(var product in Products(graph))yield return product;
        }
    }

    public static string Clean(string html)
    {
        var text=System.Net.WebUtility.HtmlDecode(Tags().Replace(WithoutScripts(html)," "));
        text=Whitespace().Replace(text," ").Trim();
        return text.Length<=4000?text:text[..4000];
    }

    internal static string WithoutScripts(string html)=>StripElements(html,false);
    internal static string ProductContent(string html)=>StripElements(html,true);

    static string StripElements(string html,bool navigation)
    {
        // Read complete tags first: markup inside quoted JSON attributes is not
        // a real SVG/script opening tag and must not swallow the product body.
        var output=new System.Text.StringBuilder();
        var tags=new Regex(@"<!--.*?-->|<(?:""[^""]*""|'[^']*'|[^'"">])*>",RegexOptions.Singleline);
        int previous=0;
        for(var token=tags.Match(html);token.Success;token=tags.Match(html,previous))
        {
            output.Append(html,previous,token.Index-previous);
            previous=token.Index+token.Length;
            if(token.Value.StartsWith("<!--",StringComparison.Ordinal))continue;
            var tag=Regex.Match(token.Value,@"^<\s*(/)?([a-z][\w-]*)",RegexOptions.IgnoreCase);
            var name=tag.Groups[2].Value.ToLowerInvariant();
            bool skip=name is "script" or "style" or "svg" or "noscript"
                ||navigation&&name is "header" or "footer" or "nav" or "aside" or "form";
            if(!tag.Groups[1].Success&&skip)
            {
                if(!token.Value.TrimEnd().EndsWith("/>",StringComparison.Ordinal))
                {
                    var end=new Regex($@"</{name}\s*>",RegexOptions.IgnoreCase).Match(html,previous);
                    previous=end.Success?end.Index+end.Length:html.Length;
                }
                continue;
            }
            output.Append(token.Value);
        }
        output.Append(html,previous,html.Length-previous);
        return output.ToString();
    }

    [GeneratedRegex(@"<script[^>]*application/ld\+json[^>]*>(.*?)</script>",RegexOptions.IgnoreCase|RegexOptions.Singleline)]
    private static partial Regex JsonScripts();

    [GeneratedRegex(@"<(?:""[^""]*""|'[^']*'|[^'"">])*>",RegexOptions.Singleline)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<div\b[^>]*\bid=[""']description[""'][^>]*>(.*?)</div>",RegexOptions.IgnoreCase|RegexOptions.Singleline)]
    private static partial Regex Description();
}
