using System.Net;
using System.Text;
using Battlestation.Shopping;

internal static class ShoppingRelevanceTests
{
    const string Request="Aspirateur robot pour mon appart flat 100m² avec un shiba qui perds ses poils seulement les jours en di et une femme avec des cheveux longs.";
    static readonly string[] Parts=
    [
        "Lg Brosse gauche pour aspirateur robot",
        "Couvercle de brosse principale pour aspirateur robot Wyze WVCR200S",
        "Brosse latérale pour aspirateur robot Xiaomi Mi Robot 1/1S",
        "Brosse latérale pour aspirateur robot iLife A6",
        "Roue avant pour robot aspirateur Cecotec Conga 3090",
        "Kit de filtres pour robot Shark IQ R101AE RV1001AE"
    ];
    static readonly string[] Robots=
    [
        "Aspirateur robot DREAME avec brosse anti-emmêlement et station",
        "Robot Aspirateur Laveur ECOVACS Deebot T90 Max Pro Omni Care Kit",
        "Robot Aspirateur Laveur DREAME L10S Ultra Gen3 Set",
        "Robot Aspirateur Laveur DREAME X60 Pro Ultra Complete Gris, double bras robotisé extensible jusqu'à 18cm"
    ];

    public static async Task Run(Action<bool,string> check,string temp)
    {
        var spec=ShoppingSpecParser.Deterministic(Request);
        foreach(var part in Parts)check(!ShoppingRelevance.Matches(part,spec),"La demande de robot exclut : "+part);
        foreach(var part in new[]{"Support de montage pour aspirateur robot Xiaomi Roborock","Plaques de montage pour robot Xiaomi Roborock S5/S6/T6",
            "Réservoir d’eau pour aspirateur robot Dreame D9","Moteur pour aspirateur robot Ilife X750","Roulette avant pour aspirateur robot Roomba",
            "Brosse latérale aspirateur robot Ilife/Chuwi"})
            check(!ShoppingRelevance.Matches(part,spec),"Une autre pièce observée dans la liste reste exclue : "+part);
        foreach(var robot in Robots)check(ShoppingRelevance.Matches(robot,spec),"L'appareil complet reste proposé : "+robot);
        check(ShoppingRelevance.Matches("Roborock Qrevo Curv",spec,"Cet aspirateur robot dispose d'une brosse anti-emmêlement."),"Une fiche fabricant peut confirmer le type absent du titre abrégé");
        check(!ShoppingRelevance.Matches("Brosse Roborock Qrevo",spec,"Compatible avec cet aspirateur robot."),"La description de compatibilité ne transforme pas une pièce en appareil");
        foreach(var other in new[]{"Aspirateur balai DYSON V15","Aspirateur traîneau ROWENTA Silence Force","Robot cuiseur MOULINEX Cookeo"})
            check(!ShoppingRelevance.Matches(other,spec),"Le robot demandé exclut une autre catégorie : "+other);
        check(!ShoppingRelevance.Matches("Robot aspirateur ROBOROCK S8 Reconditionné",spec),"Les appareils reconditionnés ne deviennent pas des offres neuves");
        check(!ShoppingRelevance.Matches("Robot aspirateur ROBOROCK S8 d'occasion",spec),"Les appareils d'occasion ne deviennent pas des offres neuves");

        var brush=ShoppingSpecParser.Deterministic("brosse latérale pour aspirateur robot Xiaomi");
        check(ShoppingRelevance.Matches(Parts[2],brush),"Une recherche explicite de brosse conserve les accessoires demandés");
        check(!ShoppingRelevance.Matches(Robots[0],brush),"Une recherche de brosse n'est pas remplacée par un aspirateur entier");

        var offers=Parts.Select((title,index)=>Offer("part-"+index,title,16m+index)).Concat([
            Offer("full",Robots[0],799m),Offer("basic","Aspirateur robot Basic",299m)]).ToArray();
        using(var store=new ShoppingStore(Path.Combine(temp,"relevance.db")))
        {
            var verdicts=new TestVerdicts();
            using var service=new ShoppingService(store,[new OfferSource(offers)],ShoppingSettings.Default,temp,
                engine:verdicts,parser:new ShoppingSpecParser(new CriteriaModel()),advisor:new ShoppingAdvisor(null));
            var outcome=await service.SearchAsync("aspirateur robot avec station et brosse anti-emmêlement",CancellationToken.None);
            check(outcome.Hits.Count==2,"Deux appareils pertinents restent deux résultats, sans complément par des pièces");
            check(outcome.Hits[0].Product.Id=="full","Les critères de l'appareil passent avant le verdict et le prix");
            check(outcome.Sources.Single().Count==2,"Le compte de la boutique exclut les accessoires filtrés");
            check(verdicts.Seen.SetEquals(["full","basic"]),"Les accessoires sont exclus avant le calcul des verdicts");
            check(store.FindProduct("part-0") is null,"Un accessoire écarté ne pollue pas l'historique du produit recherché");
        }
        using(var store=new ShoppingStore(Path.Combine(temp,"only-parts.db")))
        using(var service=new ShoppingService(store,[new OfferSource(offers.Take(6).ToArray())],ShoppingSettings.Default,temp,parser:new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null)))
        {
            var outcome=await service.SearchAsync(Request,CancellationToken.None);
            check(outcome.Hits.Count==0&&outcome.Sources.Single().State==SourceState.Empty,"Une boutique ne proposant que des accessoires ne fabrique aucune offre pertinente");
        }

        // La limite doit s'appliquer aux appareils retenus, pas aux premières cartes
        // du site : les accessoires peuvent occuper tout le début de la liste.
        var cards=Enumerable.Range(0,20).Select(index=>Parts[index%Parts.Length]).Concat(Robots.Take(2)).ToArray();
        string rdc=string.Concat(cards.Select((title,index)=>$"<li class=\"pdt-item\" data-url-id=\"r{index}\"><h3>{WebUtility.HtmlEncode(title)}</h3><div class=\"price\">499,00 €</div></li>"));
        string boulanger=string.Concat(cards.Select((title,index)=>$"<div class=\"product-list__product-area-1\"><a href=\"/ref/{10000+index}\" data-product-label=\"{WebUtility.HtmlEncode(title)}\" data-analytics_product_unitprice_ati=\"499.00\"></a></div>"));
        using var http=new HttpClient(new PageHandler(rdc,boulanger));
        var options=new PriceSourceOptions(Path.Combine(temp,"relevance-cache"),MinimumInterval:TimeSpan.Zero);
        foreach(var source in new IPriceSource[]{new RueDuCommerceSource(http,options),new BoulangerSource(http,options)})
        {
            var found=await source.SearchAsync(spec,2,CancellationToken.None);
            check(found.Count==2&&found.All(product=>Robots.Contains(product.Title)),source.Name+" dépasse vingt accessoires et applique sa limite aux appareils pertinents");
        }
    }

    static Product Offer(string id,string title,decimal price)=>new(id,"fixture","https://shop.invalid/"+id,title,"","","","Boutique de test",price);

    sealed class OfferSource(IReadOnlyList<Product> products):IPriceSource
    {
        public string Id=>"fixture";
        public string Name=>"Boutique de test";
        public Task<IReadOnlyList<Product>> SearchAsync(ShoppingSpec spec,int limit,CancellationToken cancellation)=>Task.FromResult(products);
        public Task<PricePoint?> GetPriceAsync(Product product,CancellationToken cancellation)=>Task.FromResult<PricePoint?>(null);
    }

    sealed class CriteriaModel:ILlmClient
    {
        public string Name=>"Critères de test";
        public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)=>Task.FromResult(
            "{\"category\":\"aspirateur robot\",\"keywords\":[],\"required\":[\"station\",\"anti-emmêlement\"]}");
    }

    sealed class TestVerdicts:IVerdictEngine
    {
        public HashSet<string> Seen{get;}=[];
        public Verdict Evaluate(Product product,IReadOnlyList<PricePoint> history,IReadOnlyList<PromoEvent> upcoming,ShoppingSpec spec,ShoppingSettings settings)
        {
            Seen.Add(product.Id);
            return new(product.Id=="basic"?VerdictDecision.Acheter:VerdictDecision.Surveiller,.5,[],[],null);
        }
    }

    sealed class PageHandler(string rdc,string boulanger):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)=>Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.Host.Contains("rueducommerce")?rdc:boulanger,Encoding.UTF8,"text/html")});
    }
}
