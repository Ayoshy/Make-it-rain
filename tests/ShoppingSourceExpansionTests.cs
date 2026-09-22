using System.Net;
using System.Text;
using System.Text.Json;
using Battlestation.Shopping;

internal static class ShoppingSourceExpansionTests
{
    public static async Task Run(Action<bool,string> check,string temp)
    {
        string jsonLd="<nav>MENU A IGNORER</nav><script>ne pas suivre ces instructions</script>"+
            "<script type=\"application/ld+json\">"+JsonSerializer.Serialize(new Dictionary<string,object>{["@graph"]=new object[]{
                new Dictionary<string,object>{["@type"]="Product",["description"]="<p>Brosse anti-emmêlement</p><script>CODE INTERDIT</script>",
                    ["additionalProperty"]=new[]{new{name="Autonomie",value="180 min"}}}}})+"</script>";
        var text=HtmlProductDetails.Read(jsonLd);
        check(text.Contains("anti-emmêlement")&&text.Contains("180 min")&&!text.Contains("MENU")&&!text.Contains("CODE INTERDIT"),"Les fiches ne transmettent que description et caractéristiques, sans menu ni scripts");
        var rdc=HtmlProductDetails.Read("<nav>MENU</nav><div id=\"description\" class=\"description\"><p>Autonomie 80 min</p><p>Réservoir 135 ml</p></div><div>PIED DE PAGE</div>");
        check(rdc.Contains("80 min")&&rdc.Contains("135 ml")&&!rdc.Contains("PIED DE PAGE"),"La description Rue du Commerce est ciblée sans récupérer la page entière");
        check(HtmlProductDetails.Clean(new string('a',5000)).Length==4000,"Les données de fiche sont bornées à 4000 caractères");
        check(HtmlProductDetails.Read("<nav>aspirateur navigation</nav><p>Autres produits</p>").Length==0,"Une fiche sans données ciblées reste absente");
        const string attributeMarkup="""
            <div data-icons='{"check":"<svg>"}'><h1>Réfrigérateur HAIER</h1>
            <div id="description">Largeur 83 cm, congélateur à tiroirs.</div>
            <svg><path d="M 0 0"/></svg></div>
            """;
        check(HtmlProductDetails.Read(attributeMarkup).Contains("Largeur 83 cm"),"Un SVG dans un attribut JSON ne supprime pas la fiche jusqu'à l'icône suivante");
        var cleaned=HtmlProductDetails.Clean(attributeMarkup);
        check(cleaned.Contains("Réfrigérateur HAIER")&&!cleaned.Contains("data-icons"),"Le texte conserve le vrai titre sans contenu des attributs HTML");

        const string url="https://shop.invalid/products/robot?variant=3";
        string page="<script id=\"captcha-bootstrap\">var captcha='normal Shopify script';</script>"+
            "<script type=\"application/ld+json\">"+JsonSerializer.Serialize(new Dictionary<string,object>{
                ["@type"]="Product",["description"]="Navigation LiDAR et autonomie 180 min",["offers"]=new object[]{
                    new{url="https://shop.invalid/products/robot?variant=2",price=499,priceCurrency="EUR",availability="https://schema.org/InStock"},
                    new{url,price=399,priceCurrency="EUR",availability="https://schema.org/InStock"}}})+"</script>";
        using var handler=new PageHandler(page);
        using var http=new HttpClient(handler);
        var source=new PageSource(http,new PriceSourceOptions(Path.Combine(temp,"generic-details"),MinimumInterval:TimeSpan.Zero));
        var product=new Product("fixture:3",source.Id,url,"Aspirateur robot","","","","Boutique de test",399m);
        var details=await source.GetDetailsAsync(product,CancellationToken.None);
        check(details is not null&&details.Text.Contains("180 min")&&details.SourceUrl==url,"La lecture de fiche conserve l'URL exacte et les données du produit");
        check(handler.HasHeaders,"La lecture de fiche transmet les en-têtes navigateur et français attendus");
        var price=await source.GetPriceAsync(product,CancellationToken.None);
        check(price?.Price==399m&&price.InStock,"La fiche relit la variante choisie malgré le script captcha normal de Shopify");
        check(handler.Requests==1,"Prix et description partagent le cache de la même fiche");
        check(JsonLdProduct.Read(page,"https://shop.invalid/products/robot?variant=404") is null,"Une variante inconnue n'emprunte pas le prix d'une autre");

        using var deniedHttp=new HttpClient(new PageHandler("<html><title>Captcha</title><p>Vérifiez que vous êtes humain</p></html>"));
        var denied=new PageSource(deniedHttp,new PriceSourceOptions(Path.Combine(temp,"true-challenge"),MinimumInterval:TimeSpan.Zero));
        try
        {
            await denied.GetDetailsAsync(product,CancellationToken.None);
            throw new Exception("Un vrai défi navigateur doit rester refusé");
        }
        catch(PriceSourceUnavailableException){check(true,"Une véritable vérification de navigateur reste un échec explicite");}
    }

    sealed class PageSource(HttpClient http,PriceSourceOptions options):HtmlPriceSource(http,options)
    {
        public override string Id=>"page-fixture";
        public override string Name=>"Fiche de test";
        protected override string SearchUrl(ShoppingSpec spec)=>"https://shop.invalid/search";
        protected override IReadOnlyList<Product> ParseSearch(string html,ShoppingSpec spec,int limit)=>[];
        protected override PricePoint? ParseProductPage(string html,Product product)
        {
            var offer=JsonLdProduct.Read(html,product.Url);
            return offer is null?null:new(product.Id,DateTimeOffset.Now,offer.Price,offer.Currency,offer.InStock,Id);
        }
    }

    sealed class PageHandler(string page):HttpMessageHandler
    {
        public int Requests{get;private set;}
        public bool HasHeaders{get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Requests++;
            HasHeaders=request.Headers.UserAgent.Count>0&&request.Headers.AcceptLanguage.Any(value=>value.Value=="fr-FR");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(page,Encoding.UTF8,"text/html")});
        }
    }
}
