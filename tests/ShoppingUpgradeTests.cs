using System.Net;
using System.Text;
using System.Text.Json;
using Battlestation.Shopping;

internal static class ShoppingUpgradeTests
{
    const string Pulsat="https://www.pulsat.fr/fr/c/gros-electromenager/refrigerateur/refrigerateur-congelateur-en-bas";
    const string But="https://www.but.fr/produits/6901018083198/Refrigerateur-Multi-portes-Haier-Hcr3818enmm-467l-Silver.html";
    const string Other="https://merchant.example/produits/haier-hcr3818enmm";
    const string Title="HAIER Réfrigérateur Multi-portes Haier Hcr3818enmm 467l Silver";
    const string Request="frigo 1000€ max double porte verticales en haut, tiroir glaciere en bas";

    public static async Task Run(Action<bool,string> check,string temp)
    {
        foreach(var request in new[]{Request,"> Réfrigérateur avec deux portes côte à côte en haut et tiroirs de congélation en bas, maximum 1 000 €."})
            check(ShoppingSpecParser.Deterministic(request).MaxPrice==1000m,"Le budget reste 1000 € dans la formulation courte comme dans la reformulation");
        check(ProductIdentity.Reference(Title).Equals("Hcr3818enmm",StringComparison.OrdinalIgnoreCase),"La référence Haier confirmée est distinguée du volume 467 L");
        check(ProductIdentity.Reference("Réfrigérateur - congélateur en bas")=="","Un titre de catégorie ne devient pas une référence de modèle");
        check(ProductIdentity.Reference("Réfrigérateur 467l 4portes 180cm")=="","Les dimensions et capacités ne sont pas des références de produit");
        var black=new Product("black","test","https://example.test/black","Casque Sony WH1000XM5 noir","Sony","WH1000XM5","","Test",200m);
        check(black.Identity!=(black with{Title="Casque Sony WH1000XM5 blanc"}).Identity,"Deux finitions du même modèle ne sont pas fusionnées pendant la comparaison des offres");

        using(var handler=new Pages())
        using(var http=new HttpClient(handler))
        {
            var model=new Reply(
                JsonSerializer.Serialize(new{queries=new[]{"réfrigérateur multi-portes tiroirs France"}}),
                JsonSerializer.Serialize(new{urls=new[]{Pulsat,But}}),
                JsonSerializer.Serialize(new{urls=Array.Empty<string>(),query=""}),
                JsonSerializer.Serialize(new{products=new[]{
                    new{url=Pulsat,product=true,title="Réfrigérateur - congélateur en bas",evidence=new[]{"Réfrigérateur combiné inversé FRIGELUX RC168BE"}},
                    new{url=But,product=true,title=Title,evidence=new[]{"Volume total 467 litres."}}
                }}));
            var source=new WebShoppingSource(http,new PriceSourceOptions(Path.Combine(temp,"upgrade-web"),MinimumInterval:TimeSpan.Zero),model);
            var products=await source.SearchAsync(ShoppingSpecParser.Deterministic(Request),6,CancellationToken.None);
            check(products.Count==2&&products.All(product=>product.Url!=Pulsat),"La catégorie Pulsat à 97 articles est rejetée même si le modèle la déclare produit");
            check(products.Any(product=>product.Url==But&&product.Model.Equals("HCR3818ENMM",StringComparison.OrdinalIgnoreCase)),"Le vrai Haier reste un modèle identifié depuis sa fiche BUT");
            check(handler.Queries.Count==2&&handler.Queries[1].Contains("Hcr3818enmm",StringComparison.OrdinalIgnoreCase),"La seconde phase recherche la référence effectivement identifiée");
            check(products.Single(product=>product.Url==Other).Price==849m,"L'offre du même modèle chez un autre vendeur garde son prix structuré vérifié");
            check(products.Select(product=>product.Identity).Distinct().Count()==1,"Les deux vendeurs sont regroupables par la même référence Haier");
            check(model.Systems.All(prompt=>prompt.StartsWith(ShoppingPrompts.Rules,StringComparison.Ordinal)),"Le contrat d'achat commun accompagne toutes les étapes DeepSeek");
        }

        var spec=new ShoppingSpec("réfrigérateur multi-portes",[],1000m,["deux portes en haut","tiroirs congélateur en bas"],[],Request);
        var product=new Product("haier","web",But,Title,"Haier","HCR3818ENMM","","BUT",849m);
        const string top="Deux portes réfrigérateur côte à côte en haut.";
        const string bottom="Tiroirs de congélation dans la partie basse.";
        var candidate=new ProductCandidate(product,new ProductDetails(top+" "+bottom,But));
        string Assessment(string secondState,string secondEvidence)=>JsonSerializer.Serialize(new{assessments=new[]{new{
            productId="haier",fit="Recommended",reason="Disposition documentée et prix dans le budget.",evidence=new[]{top},caveats=Array.Empty<string>(),
            criteria=new[]{new{criterion=spec.Required[0],state="Confirmed",evidence=top},new{criterion=spec.Required[1],state=secondState,evidence=secondEvidence}}
        }}});
        var reply=new Reply(Assessment("Confirmed",bottom));
        var result=(await new ShoppingAdvisor(reply).ReviewAsync(spec,[candidate],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Recommended&&result.Criteria!.Count==3&&result.Criteria.All(item=>item.State==CriterionState.Confirmed),"Une fiche précise avec preuves pour chaque exigence et budget confirmé reste recommandable");
        result=(await new ShoppingAdvisor(new Reply(Assessment("Confirmed","Multi-portes"))).ReviewAsync(spec,[candidate],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Unknown,"Le simple mot multi-portes ne valide pas à lui seul les tiroirs demandés");
        var absent=new ShoppingAdvisor(new Reply("{\"assessments\":[{\"productId\":\"haier\",\"fit\":\"Recommended\",\"reason\":\"Tout convient\",\"evidence\":[\"Deux portes réfrigérateur côte à côte en haut.\"]}]}"));
        result=(await absent.ReviewAsync(spec,[candidate],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Unknown&&result.Criteria!.Count(item=>item.State==CriterionState.Unknown)==2,"Un avis global favorable ne remplace pas les vérifications critère par critère");
        result=(await new ShoppingAdvisor(new Reply(Assessment("Confirmed",bottom))).ReviewAsync(spec,[candidate with{Product=product with{Price=1100m}}],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Unsuitable,"Un prix supérieur au budget est rejeté par le code même si le modèle recommande");
        result=(await new ShoppingAdvisor(null).ReviewAsync(spec,[candidate with{Product=product with{Price=1100m}}],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Unsuitable,"La limite de budget reste appliquée quand le modèle est indisponible");
        result=(await new ShoppingAdvisor(new Reply(Assessment("Confirmed",bottom))).ReviewAsync(spec,[candidate with{Product=product with{Price=null}}],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Unknown&&result.Criteria!.Last().State==CriterionState.Unknown,"Le modèle connu sans prix reste une piste, sans garantir le budget");
        var note="[Vérification Battlestation : prix, état et disponibilité à confirmer ; ce constat ne provient pas de la fiche.]";
        var noteReply=JsonSerializer.Serialize(new{assessments=new[]{new{productId="haier",fit="Recommended",reason="Fausse preuve",evidence=new[]{note}}}});
        result=(await new ShoppingAdvisor(new Reply(noteReply)).ReviewAsync(spec,[candidate with{Details=new(top+"\n"+note,But)}],CancellationToken.None)).Single();
        check(result.Fit==ProductFit.Unknown&&result.Evidence.Count==0,"La note ajoutée par l'application n'est jamais acceptée comme preuve du marchand");
    }

    sealed class Reply(params string[] replies):ILlmClient
    {
        public string Name=>"Upgrade fixture";
        public List<string> Systems{get;}=[];
        public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)
        {Systems.Add(system);return Task.FromResult(replies[Systems.Count-1]);}
    }
    sealed class Pages:HttpMessageHandler
    {
        public readonly List<string> Queries=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            string content;string type="text/html";
            if(request.RequestUri!.Host=="api.tavily.com")
            {
                using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                var query=json.RootElement.GetProperty("query").GetString()!;Queries.Add(query);type="application/json";
                content=JsonSerializer.Serialize(new{results=query.Contains("HCR3818ENMM",StringComparison.OrdinalIgnoreCase)
                    ?new[]{new{url=Other,title=Title,content="Offre HCR3818ENMM"}}
                    :new[]{new{url=Pulsat,title="Réfrigérateur - congélateur en bas",content="97 articles"},new{url=But,title=Title,content="Haier HCR3818ENMM"}}});
            }
            else if(request.RequestUri.AbsoluteUri==Pulsat)
                content="<h1>Réfrigérateur - congélateur en bas</h1><p>97 articles</p><button>Tri par prix croissant</button><h2>Réfrigérateur combiné inversé FRIGELUX RC168BE</h2><h2>Réfrigérateur combiné inversé CANDY CCH1518EW</h2>";
            else if(request.RequestUri.AbsoluteUri==But)content="<h1 class=\"product-title block-layout\">"+Title+"</h1><main><p>Volume total 467 litres.</p></main>";
            else if(request.RequestUri.AbsoluteUri==Other)
                content="<h1>"+Title+"</h1><script type=\"application/ld+json\">"+JsonSerializer.Serialize(new Dictionary<string,object>{
                    ["@type"]="Product",["name"]=Title,["url"]=Other,["brand"]=new{name="HAIER"},["description"]="Volume total 467 litres.",
                    ["offers"]=new{url=Other,price=849,priceCurrency="EUR",availability="https://schema.org/InStock",itemCondition="https://schema.org/NewCondition"}})+"</script>";
            else throw new Exception("Unexpected fixture URL: "+request.RequestUri);
            return new(HttpStatusCode.OK){Content=new StringContent(content,Encoding.UTF8,type)};
        }
    }
}
