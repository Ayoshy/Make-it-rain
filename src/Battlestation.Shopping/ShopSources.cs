using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>
/// Rue du Commerce : la liste de résultats porte déjà l'offre complète
/// (référence, lien, titre, image, prix), la fiche produit confirme ensuite le prix.
/// </summary>
public sealed partial class RueDuCommerceSource(HttpClient http,PriceSourceOptions options):HtmlPriceSource(http,options)
{
    public override string Id=>"rdc";
    public override string Name=>"Rue du Commerce";
    const string Host="https://www.rueducommerce.fr";

    protected override string SearchUrl(ShoppingSpec spec)=>$"{Host}/recherche/{Uri.EscapeDataString(spec.Query.Replace(' ','-'))}/";

    protected override IReadOnlyList<Product> ParseSearch(string html,ShoppingSpec spec,int limit)
    {
        var products=new List<Product>();
        foreach(var card in Item().Matches(html).Select(match=>match.Value))
        {
            var reference=Attribute(card,"data-url-id");
            var title=Unescape(Element(card,TitleElement()));
            if(reference.Length==0||title.Length==0||!ShoppingRelevance.Matches(title,spec))continue;
            var brand=GuessBrand(title);
            // Une offre remisée porte « new-price », une offre au prix courant un simple « price ».
            var price=Element(card,NewPriceElement());
            if(price.Length==0)price=Element(card,PlainPriceElement());
            products.Add(new(
                $"rdc:{reference}",Id,$"{Host}/p/{reference}.html",title,brand,GuessModel(title,brand),
                Image(card),Name,Amount(price)));
            if(products.Count>=limit)break;
        }
        return products;
    }

    protected override PricePoint? ParseProductPage(string html,Product product)
    {
        if(JsonLdProduct.Read(html) is not {} offer)return null;
        return new(product.Id,DateTimeOffset.Now,offer.Price,offer.Currency,offer.InStock,Id);
    }

    static string Attribute(string card,string name)=>Regex.Match(card,$@"{name}=""([^""]*)""").Groups[1].Value;
    static string Element(string card,Regex element)=>element.Match(card).Groups[1].Value;

    static string Image(string card)
    {
        var match=ImageElement().Match(card);
        return match.Success?Unescape(match.Groups[1].Value):"";
    }

    [GeneratedRegex(@"<li[^>]*class=""[^""]*pdt-item[^""]*""(.*?)</li>",RegexOptions.Singleline)]
    private static partial Regex Item();

    [GeneratedRegex(@"<h3[^>]*>(.*?)</h3>",RegexOptions.Singleline)]
    private static partial Regex TitleElement();

    [GeneratedRegex(@"class=""new-price""[^>]*>(?:<span[^>]*>[^<]*</span>)?([^<]*)",RegexOptions.Singleline)]
    private static partial Regex NewPriceElement();

    [GeneratedRegex(@"<div class=""price"">([^<]*)</div>",RegexOptions.Singleline)]
    private static partial Regex PlainPriceElement();

    [GeneratedRegex(@"src=""(https://[^""]+)""[^>]*class=""listing-product__img""",RegexOptions.Singleline)]
    private static partial Regex ImageElement();
}

/// <summary>
/// Boulanger : chaque carte de résultat expose ses attributs d'analyse
/// (référence, prix TTC, marque, image) à côté du libellé lisible.
/// </summary>
public sealed partial class BoulangerSource(HttpClient http,PriceSourceOptions options):HtmlPriceSource(http,options)
{
    public override string Id=>"boulanger";
    public override string Name=>"Boulanger";
    const string Host="https://www.boulanger.com";

    protected override string SearchUrl(ShoppingSpec spec)=>$"{Host}/resultats?tr={Uri.EscapeDataString(spec.Query)}";

    protected override IReadOnlyList<Product> ParseSearch(string html,ShoppingSpec spec,int limit)
    {
        var products=new List<Product>();
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var card in Card().Matches(html).Select(match=>match.Value))
        {
            var reference=Attribute(card,"href") is {Length:>0} href?Reference().Match(href).Groups[1].Value:"";
            if(reference.Length==0)reference=Attribute(card,"data-analytics_product_sap");
            var title=Unescape(Attribute(card,"data-product-label"));
            if(title.Length==0)title=Unescape(Attribute(card,"data-analytics_product_name").Replace('_',' '));
            var condition=ShoppingText.Fold(Attribute(card,"data-analytics_product_condition")+" "+Attribute(card,"data-analytics_product_grade"));
            if(condition.Contains("reconditionne",StringComparison.Ordinal)||condition.Contains("occasion",StringComparison.Ordinal))continue;
            if(reference.Length==0||title.Length==0||!seen.Add(reference)||!ShoppingRelevance.Matches(title,spec))continue;
            var brand=Attribute(card,"data-product-brand-name");
            if(brand.Length==0)brand=GuessBrand(title);
            products.Add(new(
                $"boulanger:{reference}",Id,$"{Host}/ref/{reference}",title,brand,GuessModel(title,brand),
                Attribute(card,"data-product-img"),Name,Amount(Attribute(card,"data-analytics_product_unitprice_ati"))));
            if(products.Count>=limit)break;
        }
        return products;
    }

    protected override PricePoint? ParseProductPage(string html,Product product)
    {
        if(JsonLdProduct.Read(html) is not {} offer)return null;
        return new(product.Id,DateTimeOffset.Now,offer.Price,offer.Currency,offer.InStock,Id);
    }

    static string Attribute(string card,string name)=>Regex.Match(card,$@"{name}=""([^""]*)""").Groups[1].Value;

    // La classe de la carte est précédée d'autres classes, et une carte peut contenir
    // des listes imbriquées : seule la carte suivante la termine.
    [GeneratedRegex(@"<div class=""[^""]*product-list__product-area-1[^""]*""(.*?)(?=<div class=""[^""]*product-list__product-area-1|$)",RegexOptions.Singleline)]
    private static partial Regex Card();

    [GeneratedRegex(@"/ref/(\d+)")]
    private static partial Regex Reference();
}
