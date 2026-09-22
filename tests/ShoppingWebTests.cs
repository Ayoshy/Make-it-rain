using System.Net;
using System.Text;
using System.Text.Json;
using Battlestation.Shopping;

internal static class ShoppingWebTests
{
    const string First="https://shop-one.example/product",Second="https://shop-two.example/product",Followup="https://shop-three.example/product";
    const string Request="Aspirateur robot pour mon appart flat 100m² avec un shiba qui perd ses poils et des cheveux longs.";

    public static async Task Run(Action<bool,string> check,string temp)
    {
        var spec=ShoppingSpecParser.Deterministic(Request);
        PriceSourceOptions Options(string name)=>new(Path.Combine(temp,"web-"+name),MinimumInterval:TimeSpan.Zero);

        using(var handler=new WebHandler(Results((First,"Robot R1"),(Second,"Robot R2"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]=ProductPage(First,"Robot R1",499m);
            handler.Pages[Second]=ProductPage(Second,"Robot R2",649m);
            var source=new WebShoppingSource(http,Options("discovery"));
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            var search=handler.SearchRequests.Single();
            var query=search.Query;
            check(source.Id=="web"&&products.Count==2,"La recherche web découvre deux boutiques depuis les résultats directs de l'API");
            check(search.Uri.AbsoluteUri=="https://api.tavily.com/search"&&search.Method==HttpMethod.Post&&search.Country=="france"&&search.Language=="fr"&&query.Contains("aspirateur robot",StringComparison.OrdinalIgnoreCase)
                &&!query.Contains("mon appart",StringComparison.OrdinalIgnoreCase)&&!query.Contains("shiba",StringComparison.OrdinalIgnoreCase),
                "Sans planificateur, la recherche web transmet le type normalisé et la région française dans le JSON de l'API Tavily");
            check(products.Select(product=>product.Url).ToHashSet().SetEquals([First,Second])&&products.All(product=>product.Price is>0&&product.Source=="web"),
                "Chaque offre web conserve une URL réellement trouvée et son prix lu sur la fiche");
            check(products.Single(product=>product.Url==First).Price==499m&&products.Single(product=>product.Url==Second).Price==649m,
                "Les prix web proviennent du JSON-LD EUR des deux boutiques");
            var product=products.Single(item=>item.Url==First);
            var details=await source.GetDetailsAsync(product,CancellationToken.None);
            check(details is not null&&details.SourceUrl==First&&details.Text.Contains("anti-emmêlement")&&details.Text.Contains("180 min"),
                "Le détail web contient les caractéristiques de la fiche consultée");
            var price=await source.GetPriceAsync(product,CancellationToken.None);
            check(price is not null&&price.Price==499m&&price.Currency=="EUR"&&price.InStock,
                "La veille web relit un prix EUR disponible depuis la même fiche");
        }

        using(var handler=new WebHandler(Results((First,"Robot sans fiche"),(Second,"Robot sans prix"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]="<html><h1>Guide des aspirateurs robots</h1><p>Robot R1 à partir de 499 €</p></html>";
            handler.Pages[Second]="""
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Product","name":"Aspirateur Robot R2","description":"Brosse anti-emmêlement. Autonomie 180 min.","offers":{"@type":"Offer","priceCurrency":"EUR","availability":"https://schema.org/InStock","itemCondition":"https://schema.org/NewCondition"}}</script>
                """;
            var source=new WebShoppingSource(http,Options("missing-offers"));
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            check(products.Count==1&&products[0].Url==Second&&products[0].Price is null,
                "Une vraie fiche sans prix reste un candidat sans montant ; un guide sans Product reste exclu");
            check(await source.GetPriceAsync(products[0],CancellationToken.None) is null,
                "La veille ne fabrique aucun prix pour un produit sans offre chiffrée");
        }

        using(var handler=new WebHandler(Results((First,"Guide des robots"),(Second,"Boutique refusée"))))
        using(var http=new HttpClient(handler))
        using(var store=new ShoppingStore(Path.Combine(temp,"web-partial-empty.db")))
        {
            handler.Pages[First]="<article><h1>Guide des aspirateurs robots</h1><p>Choisir la navigation et les brosses.</p></article>";
            handler.PageStatuses[Second]=HttpStatusCode.Forbidden;
            var source=new WebShoppingSource(http,Options("partial-empty"));
            var progress=new List<string>();
            source.Progress+=progress.Add;
            using var service=new ShoppingService(store,[source],ShoppingSettings.Default,temp,http,
                parser:new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null));
            var result=await service.SearchAsync(Request,CancellationToken.None);
            check(result.Hits.Count==0&&result.Sources.Single().State==SourceState.Empty&&result.Error.Length==0,
                "Une page lue sans produit et une boutique refusée donnent un résultat vide, pas une panne globale");
            check(source.Notice.Contains("1 page",StringComparison.Ordinal)&&source.Notice.Contains("aucun produit",StringComparison.OrdinalIgnoreCase)&&source.Notice.Contains("403",StringComparison.Ordinal),
                "Le résultat partiel explique le nombre de pages lues, l'absence de produit et le refus rencontré");
            check(progress.Contains(source.Notice),"Le diagnostic partiel apparaît aussi dans le journal de progression");
        }

        using(var handler=new WebHandler(Results((First,"Boutique refusée"),(Second,"Autre boutique refusée"))))
        using(var http=new HttpClient(handler))
        {
            handler.PageStatuses[First]=HttpStatusCode.Forbidden;
            handler.PageStatuses[Second]=HttpStatusCode.Forbidden;
            bool failed=false;
            try{await new WebShoppingSource(http,Options("all-refused")).SearchAsync(spec,4,CancellationToken.None);}
            catch(PriceSourceUnavailableException error){failed=error.Message.Contains("403",StringComparison.Ordinal);}
            check(failed,"Si aucune page ne peut être lue, les refus restent une vraie indisponibilité");
        }

        using(var handler=new WebHandler(Results((First,"Robot R1"),(Second,"Boutique refusée"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]=ProductPage(First,"Robot R1",499m);
            handler.PageStatuses[Second]=HttpStatusCode.Forbidden;
            var source=new WebShoppingSource(http,Options("partial-products"));
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            check(products.Count==1&&products[0].Url==First&&products[0].Price==499m&&source.Notice.Contains("403",StringComparison.Ordinal),
                "Une boutique refusée ne retire pas le produit et le prix vérifiés sur une autre page");
        }

        using(var handler=new WebHandler(Results(
            ("http://localhost:1234/product","Local"),("http://127.0.0.1/private","Loopback"),("http://192.168.1.4/private","LAN"),
            ("https://duckduckgo.com/y.js?ad_provider=test","Publicité"),(First,"Robot R1"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]=ProductPage(First,"Robot R1",499m);
            var products=await new WebShoppingSource(http,Options("unsafe-links")).SearchAsync(spec,8,CancellationToken.None);
            check(products.Count==1&&products[0].Url==First,"Les liens locaux et publicitaires sont exclus des offres web");
            check(handler.Requests.All(uri=>uri.Host=="api.tavily.com"||uri.AbsoluteUri==First),
                "Aucune requête ne part vers localhost, les IP privées ou le relais publicitaire");
        }

        using(var handler=new WebHandler("{\"detail\":\"Rate limit exceeded\"}"){SearchStatus=HttpStatusCode.TooManyRequests})
        using(var http=new HttpClient(handler))
        {
            bool unavailable=false;
            try{await new WebShoppingSource(http,Options("rate-limit")).SearchAsync(spec,4,CancellationToken.None);}
            catch(PriceSourceUnavailableException error){unavailable=error.Message.Contains("Tavily",StringComparison.Ordinal)&&error.Message.Contains("limit",StringComparison.OrdinalIgnoreCase);}
            check(unavailable,"Un refus 429 de Tavily est visible comme indisponibilité, pas comme zéro produit");
        }

        using(var handler=new WebHandler(Results((First,"Robot R1"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]="<html><title>Just a moment</title><p>Vérifiez que vous êtes humain</p></html>";
            bool unavailable=false;
            try{await new WebShoppingSource(http,Options("merchant-challenge")).SearchAsync(spec,4,CancellationToken.None);}
            catch(PriceSourceUnavailableException error){unavailable=error.Message.Contains("vérification",StringComparison.OrdinalIgnoreCase);}
            check(unavailable,"Une vérification sur la page marchande reste refusée explicitement");
        }

        using(var handler=new WebHandler(Results((First,"Robot R1"),(Second,"Robot R2"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]=ProductPage(First,"Robot R1",499m);
            handler.Pages[Second]=ProductPage(Second,"Robot R2",649m);
            var planner=new Planner(
                JsonSerializer.Serialize(new{queries=new[]{"aspirateur robot animaux achat France"}}),
                JsonSerializer.Serialize(new{urls=new[]{Second,"https://invented.example/product","http://127.0.0.1/private"},ids=new[]{"invented"}}),
                JsonSerializer.Serialize(new{urls=Array.Empty<string>(),query=""}));
            var products=await new WebShoppingSource(http,Options("selection"),planner).SearchAsync(spec,4,CancellationToken.None);
            check(planner.Calls is 2 or 3&&products.Count==1&&products[0].Url==Second,
                "Le modèle planifie puis sélectionne une fiche parmi les vrais résultats web");
            check(handler.Requests.Where(uri=>uri.Host!="api.tavily.com").All(uri=>uri.AbsoluteUri==Second),
                "Les URL et identifiants inventés par le planificateur ne sont jamais suivis");
        }

        using(var handler=new WebHandler(Results((First,"Robot R1"))))
        using(var http=new HttpClient(handler))
        {
            const string extraQuery="aspirateur robot anti-emmêlement autre modèle";
            handler.SearchPages[extraQuery]=Results((Followup,"Robot R3"));
            handler.Pages[First]=ProductPage(First,"Robot R1",499m);
            handler.Pages[Followup]=ProductPage(Followup,"Robot R3",599m);
            var planner=new Planner(
                JsonSerializer.Serialize(new{queries=new[]{"aspirateur robot achat"}}),
                JsonSerializer.Serialize(new{urls=new[]{First},query=extraQuery}),
                JsonSerializer.Serialize(new{urls=Array.Empty<string>(),query=extraQuery}));
            var products=await new WebShoppingSource(http,Options("followup"),planner).SearchAsync(spec,4,CancellationToken.None);
            check(planner.Calls==3&&handler.SearchRequests.Count==2&&handler.SearchRequests.Any(request=>request.Query==extraQuery),
                "Le planificateur peut demander une seule recherche complémentaire bornée");
            using(var refinement=JsonDocument.Parse(planner.Users[2]))
                check(refinement.RootElement.GetProperty("pages").EnumerateArray().Any(page=>page.GetProperty("Url").GetString()==First),
                    "Une demande de recherche complémentaire préparée d'avance est réévaluée après lecture des premières pages");
            check(products.Any(product=>product.Url==Followup)&&handler.Requests.Any(uri=>uri.AbsoluteUri==Followup),
                "Une fiche devient admissible après sa découverte réelle dans la recherche complémentaire");
        }

        const string category="https://catalog.example/c/robots",chosen="https://catalog.example/products/robot-c400";
        using(var handler=new WebHandler(Results((category,"Aspirateurs robots"))))
        using(var http=new HttpClient(handler))
        {
            const string extraQuery="aspirateur robot autres offres";
            string[] productPaths=["/products/robot-a100","/products/robot-b200","/products/robot-c300","/products/robot-c400"];
            handler.Pages[category]="<main><h1>Aspirateurs robots</h1>"+string.Concat(productPaths.Select((path,index)=>
                $"<a class=\"product-card\" href=\"{path}\"><h2>Aspirateur robot C{(index+1)*100}</h2></a>"))+"</main>";
            handler.Pages[chosen]=ProductPage(chosen,"Robot C400",349m);
            handler.SearchPages[extraQuery]=Results((Followup,"Robot R3"));
            handler.Pages[Followup]=ProductPage(Followup,"Robot R3",599m);
            var planner=new Planner(
                JsonSerializer.Serialize(new{queries=new[]{"aspirateur robot France"}}),
                JsonSerializer.Serialize(new{urls=new[]{category}}),
                JsonSerializer.Serialize(new{urls=new[]{chosen,"https://catalog.example/products/invente"},query=extraQuery}),
                JsonSerializer.Serialize(new{products=Array.Empty<object>()}));
            var source=new WebShoppingSource(http,Options("category-links"),planner);
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            using(var initial=JsonDocument.Parse(planner.Users[1]))
                check(initial.RootElement.GetProperty("results").GetArrayLength()==1&&initial.RootElement.GetProperty("results")[0].GetProperty("Url").GetString()==category,
                    "La première recherche ne connaît que la page catégorie, pas ses quatre fiches produit");
            using(var refinement=JsonDocument.Parse(planner.Users[2]))
            {
                var payload=refinement.RootElement;
                var links=payload.GetProperty("results").EnumerateArray().Select(item=>item.GetProperty("Url").GetString()).ToHashSet();
                check(productPaths.All(path=>links.Contains("https://catalog.example"+path))&&payload.GetProperty("pages").EnumerateArray().Any(page=>page.GetProperty("Url").GetString()==category),
                    "Après lecture de la catégorie, le modèle voit les quatre URL réellement observées et le contenu de cette page");
            }
            int chosenRead=handler.Requests.FindIndex(uri=>uri.AbsoluteUri==chosen);
            int nextSearch=handler.Requests.FindLastIndex(uri=>uri.Host=="api.tavily.com");
            check(chosenRead>=0&&handler.SearchRequests.Count>=2&&chosenRead<nextSearch,
                "La fiche choisie dans la catégorie est lue avant la recherche complémentaire générale");
            check(products.Any(product=>product.Url==chosen&&product.Price==349m)&&products.All(product=>product.Url!=category),
                "La fiche découverte fournit son offre JSON-LD ; la catégorie ne devient pas un produit");
            check(!handler.Requests.Any(uri=>uri.AbsoluteUri=="https://catalog.example/products/invente")&&productPaths.Where(path=>"https://catalog.example"+path!=chosen).All(path=>!handler.Requests.Any(uri=>uri.AbsoluteUri=="https://catalog.example"+path)),
                "Seule la fiche réellement observée et choisie est suivie, sans URL inventée ni lecture automatique des autres cartes");
            var details=await source.GetDetailsAsync(products.Single(product=>product.Url==chosen),CancellationToken.None);
            check(details?.SourceUrl==chosen&&details.Text.Contains("anti-emmêlement",StringComparison.Ordinal),
                "Les preuves de la fiche découverte restent liées à son URL exacte");
        }

        var variant=First+"?variant=3";
        using(var handler=new WebHandler(Results((variant,"Robot variante 3"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[variant]=$$$"""
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Product","url":"{{{First}}}","name":"Aspirateur robot R3","description":"Brosse anti-emmêlement.","offers":[
                {"@type":"Offer","url":"{{{First}}}?variant=2","price":499,"priceCurrency":"EUR","availability":"https://schema.org/InStock","itemCondition":"https://schema.org/NewCondition"},
                {"@type":"Offer","url":"{{{variant}}}","price":399,"priceCurrency":"EUR","availability":"https://schema.org/InStock","itemCondition":"https://schema.org/NewCondition"}]}</script>
                """;
            var source=new WebShoppingSource(http,Options("variant"));
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            check(products.Count==1&&products[0].Url==variant&&products[0].Price==399m,
                "La recherche lit le prix de la variante demandée, pas celui de la première variante JSON-LD");
            var observed=await source.GetPriceAsync(products[0],CancellationToken.None);
            check(observed?.Price==399m,"La veille conserve le prix associé à la même variante dans l'URL");
        }

        using(var handler=new WebHandler(Results((First,"Robot R1"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]=$$$"""
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Product","url":"https://shop-one.example/other","name":"Aspirateur robot autre modèle","description":"Nettoie 150 m² sans recharge."}</script>
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Product","url":"{{{First}}}","name":"Aspirateur robot R1","offers":{"@type":"Offer","url":"{{{First}}}","price":499,"priceCurrency":"EUR","availability":"https://schema.org/InStock","itemCondition":"https://schema.org/NewCondition"}}</script>
                """;
            var source=new WebShoppingSource(http,Options("other-description"));
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            check(products.Count==1&&products[0].Title=="Aspirateur robot R1","Le produit de l'URL consultée reste distinct d'un autre Product JSON-LD");
            var details=await source.GetDetailsAsync(products[0],CancellationToken.None);
            check(details is null||!details.Text.Contains("150"),"Une description appartenant à un autre produit n'est jamais empruntée à sa fiche");
        }

        using(var handler=new WebHandler(Results((First,"Robot R1"))))
        using(var http=new HttpClient(handler))
        {
            handler.Redirects[First]="http://127.0.0.1/private";
            bool refused=false;
            try{await new WebShoppingSource(http,Options("private-redirect")).SearchAsync(spec,4,CancellationToken.None);}
            catch(PriceSourceUnavailableException){refused=true;}
            check(refused&&handler.Requests.Count==2&&handler.Requests.All(uri=>uri.Host!="127.0.0.1"),
                "Une redirection 302 vers une IP privée est refusée avant toute requête à cette IP");
        }

        using(var handler=new WebHandler(Results((variant,"Robot variantes sans URL"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[variant]="""
                <script type="application/ld+json">{"@context":"https://schema.org","@type":"Product","name":"Aspirateur robot R3","description":"Brosse anti-emmêlement.","offers":[
                {"@type":"Offer","price":499,"priceCurrency":"EUR","availability":"https://schema.org/InStock","itemCondition":"https://schema.org/NewCondition"},
                {"@type":"Offer","price":399,"priceCurrency":"EUR","availability":"https://schema.org/InStock","itemCondition":"https://schema.org/NewCondition"}]}</script>
                """;
            var source=new WebShoppingSource(http,Options("unlinked-variants"));
            var products=await source.SearchAsync(spec,4,CancellationToken.None);
            check(products.Count==1&&products[0].Price is null,"Plusieurs variantes sans URL gardent le candidat sans lui attribuer arbitrairement un prix");
            check(await source.GetPriceAsync(products[0],CancellationToken.None) is null,"La veille ne choisit pas le prix d'une variante non identifiée");
        }

        const string guide="https://guide.example/test",unsupported="https://shop-four.example/product",invented="https://invented.example/product";
        using(var handler=new WebHandler(Results((First,"Robot R1"),(Second,"Robot R2"),(guide,"Test robot"),(unsupported,"Robot R4"))))
        using(var http=new HttpClient(handler))
        {
            handler.Pages[First]="<main><h1>Aspirateur robot R1</h1><p>Brosse anti-emmêlement.</p><p>149 €</p><p>Neuf, en stock.</p></main>";
            handler.Pages[Second]="<main><h1>Aspirateur robot R2</h1><p>Autonomie 180 min.</p><p>Paiement en 4 fois 99 €</p><p>Neuf, en stock.</p></main>";
            handler.Pages[guide]="<article><h1>Test et guide Aspirateur robot R3</h1><p>Brosse anti-emmêlement.</p></article>";
            handler.Pages[unsupported]="<main><h1>Aspirateur robot R4</h1><p>Autonomie 90 min.</p></main>";
            var planner=new Planner(
                JsonSerializer.Serialize(new{queries=new[]{"aspirateur robot animaux achat"}}),
                JsonSerializer.Serialize(new{urls=new[]{First,Second,guide,unsupported}}),
                JsonSerializer.Serialize(new{urls=Array.Empty<string>(),query=""}),
                JsonSerializer.Serialize(new{products=new[]
                {
                    new{url=First,product=true,title="Aspirateur robot R1",evidence=new[]{"Brosse anti-emmêlement."},price=49,priceQuote="49 €"},
                    new{url=Second,product=true,title="Aspirateur robot R2",evidence=new[]{"Autonomie 180 min."},price=99,priceQuote="99 €"},
                    new{url=guide,product=true,title="Aspirateur robot R3",evidence=new[]{"Brosse anti-emmêlement."},price=49,priceQuote="49 €"},
                    new{url=unsupported,product=true,title="Aspirateur robot R4",evidence=new[]{"Autonomie 250 min."},price=49,priceQuote="49 €"},
                    new{url=invented,product=true,title="Aspirateur robot imaginaire",evidence=new[]{"Brosse anti-emmêlement."},price=49,priceQuote="49 €"}
                }}));
            var source=new WebShoppingSource(http,Options("unstructured"),planner);
            var products=await source.SearchAsync(spec,8,CancellationToken.None);
            check(planner.Calls==4&&products.Count==2&&products.Select(product=>product.Url).ToHashSet().SetEquals([First,Second]),
                "Le repli sans JSON-LD conserve les vraies fiches et rejette guide, URL inventée et preuve absente");
            check(products.All(product=>product.Price is null),"Le repli ignore les prix du modèle, y compris une mensualité ou 49 extrait de 149 €");
            check(await source.GetPriceAsync(products[0],CancellationToken.None) is null,"Une fiche non structurée ne crée aucun prix de veille");
            var facts=await source.GetDetailsAsync(products.Single(product=>product.Url==First),CancellationToken.None);
            check(facts is not null&&facts.SourceUrl==First&&facts.Text.Contains("Brosse anti-emmêlement."),
                "Les faits du repli restent des citations de la fiche réellement lue");
        }

        using(var handler=new WebHandler(Results((First,"Réfrigérateur R833"))))
        using(var http=new HttpClient(handler))
        {
            const string title="Réfrigérateur Brand R833";
            handler.Pages[First]="<h1>"+title+"</h1><main><p>"+string.Concat(Enumerable.Repeat("Livraison et financement au moment du paiement. ",220))+"</p></main>"+
                "<table id=\"product-attribute-specs-table\"><tbody><tr><th>Technologie</th><td>Froid ventilé No Frost</td></tr><tr><th>Largeur</th><td>83,3 cm</td></tr></tbody></table>";
            var planner=new Planner(
                JsonSerializer.Serialize(new{queries=new[]{"réfrigérateur No Frost largeur 83 cm"}}),
                JsonSerializer.Serialize(new{urls=new[]{First}}),
                JsonSerializer.Serialize(new{urls=Array.Empty<string>(),query=""}),
                JsonSerializer.Serialize(new{products=new[]{new{url=First,product=true,title,evidence=new[]{"Froid ventilé No Frost","Largeur 83,3 cm"}}}}));
            var source=new WebShoppingSource(http,Options("specs-outside-main"),planner);
            var products=await source.SearchAsync(ShoppingSpecParser.Deterministic("frigo No Frost largeur 83 cm"),4,CancellationToken.None);
            check(planner.Calls==4&&products.Count==1&&products[0].Title==title,
                "Une fiche sans JSON-LD conserve son candidat grâce aux caractéristiques placées après un long bloc de paiement");
            var facts=await source.GetDetailsAsync(products[0],CancellationToken.None);
            check(facts?.SourceUrl==First&&facts.Text.Contains("Froid ventilé No Frost",StringComparison.Ordinal)&&facts.Text.Contains("Largeur 83,3 cm",StringComparison.Ordinal),
                "La table technique hors main préserve No Frost et la largeur dans les preuves de la fiche");
        }
    }

    static string Results(params (string Url,string Title)[] items)=>JsonSerializer.Serialize(new{
        results=items.Select(item=>new{url=item.Url,title=item.Title,content="Aspirateur robot neuf, détails et prix sur la fiche."})});

    static string ProductPage(string url,string title,decimal price)
    {
        title="Aspirateur "+title;
        var json=JsonSerializer.Serialize(new Dictionary<string,object>
        {
            ["@context"]="https://schema.org",["@type"]="Product",["@id"]=url,["url"]=url,["name"]=title,
            ["brand"]=new{name="Fixture"},["description"]="Brosse anti-emmêlement. Autonomie 180 min. Adapté aux poils d'animaux.",
            ["offers"]=new Dictionary<string,object>{["@type"]="Offer",["url"]=url,["price"]=price,["priceCurrency"]="EUR",["availability"]="https://schema.org/InStock",["itemCondition"]="https://schema.org/NewCondition"}
        });
        return $"<html><h1>{WebUtility.HtmlEncode(title)}</h1><script type=\"application/ld+json\">{json}</script></html>";
    }

    sealed class Planner(params string[] replies):ILlmClient
    {
        public string Name=>"Fixture web planner";
        public int Calls{get;private set;}
        public List<string> Users{get;}=[];
        public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)
        {
            Users.Add(user);
            var index=Calls++;
            if(index>=replies.Length)throw new InvalidOperationException("Unexpected extra planner call");
            return Task.FromResult(replies[index]);
        }
    }

    sealed record SearchRequest(Uri Uri,HttpMethod Method,string Query,string Country,string Language);

    sealed class WebHandler(string searchJson):HttpMessageHandler
    {
        public readonly Dictionary<string,string> Pages=[],SearchPages=[],Redirects=[];
        public readonly Dictionary<string,HttpStatusCode> PageStatuses=[];
        public readonly List<Uri> Requests=[];
        public readonly List<SearchRequest> SearchRequests=[];
        public HttpStatusCode SearchStatus{get;set;}=HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            var uri=request.RequestUri!;
            Requests.Add(uri);
            if(Redirects.TryGetValue(uri.AbsoluteUri,out var target))
            {
                var redirected=new HttpResponseMessage(HttpStatusCode.Found);redirected.Headers.Location=new Uri(target);return redirected;
            }
            string html;
            HttpStatusCode status=HttpStatusCode.OK;
            if(uri.Host=="api.tavily.com")
            {
                if(request.Method!=HttpMethod.Post||request.Content is null)throw new Exception("La recherche Tavily exige POST et un corps JSON.");
                using var payload=JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellation));
                string query=payload.RootElement.GetProperty("query").GetString()??"";
                SearchRequests.Add(new(uri,request.Method,query,payload.RootElement.GetProperty("country").GetString()??"",payload.RootElement.GetProperty("language").GetString()??""));
                html=SearchPages.GetValueOrDefault(query,searchJson);
                status=SearchStatus;
            }
            else if(PageStatuses.TryGetValue(uri.AbsoluteUri,out var pageStatus)){status=pageStatus;html="Page refused by fixture";}
            else if(!Pages.TryGetValue(uri.AbsoluteUri,out html!)){status=HttpStatusCode.NotFound;html="Unknown fixture URL";}
            return new HttpResponseMessage(status){Content=new StringContent(html,Encoding.UTF8,uri.Host=="api.tavily.com"?"application/json":"text/html")};
        }
    }
}
