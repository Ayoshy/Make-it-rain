using System.Net;
using System.Text;
using System.Text.Json;
using Battlestation.Shopping;

internal static class TavilySearchTests
{
    public static async Task Run(Action<bool,string> check)
    {
        using var handler=new ReplyHandler();
        using var http=new HttpClient(handler);
        var client=new TavilySearchClient(http);
        handler.Body="""
            {"results":[
              {"url":"https://manufacturer.example/robot","title":"Aspirateur robot fabricant","content":"Brosse anti-emmêlement annoncée."},
              {"url":"https://retailer.example/product","title":"Robot marchand","content":"Fiche produit et conditions de vente."},
              {"url":"javascript:alert(1)","title":"Invalide","content":""},
              {"url":"https://user:password@retailer.example/private","title":"Identifiants URL","content":""},
              {"url":"https://manufacturer.example/robot","title":"Doublon","content":""},
              {"url":"https://retailer.example/no-title","title":null,"content":""}
            ],"usage":{"credits":1}}
            """;
        var results=await client.SearchAsync("  aspirateur robot poils d'animaux  ",CancellationToken.None);
        check(handler.Endpoint==TavilySearchClient.Endpoint&&handler.Method==HttpMethod.Post&&handler.Keyless&&!handler.HasAuthorization,
            "Tavily utilise uniquement le POST et le header keyless officiel, sans clé API");
        using(var request=JsonDocument.Parse(handler.RequestBody))
        {
            var root=request.RootElement;
            check(root.GetProperty("query").GetString()=="aspirateur robot poils d'animaux"&&root.GetProperty("country").GetString()=="france"
                &&root.GetProperty("language").GetString()=="fr","La recherche conserve la demande utile et privilégie les résultats français");
            check(root.GetProperty("search_depth").GetString()=="basic"&&!root.GetProperty("auto_parameters").GetBoolean()
                &&root.GetProperty("max_results").GetInt32()==8&&!root.GetProperty("include_answer").GetBoolean()
                &&!root.GetProperty("include_raw_content").GetBoolean()&&!root.GetProperty("include_images").GetBoolean()
                &&root.GetProperty("include_usage").GetBoolean(),"La requête borne les résultats et désactive les options automatiques coûteuses");
            check(!root.TryGetProperty("include_domains",out _)&&!root.TryGetProperty("api_key",out _),"La recherche ne limite pas les boutiques et n'envoie aucune clé dans le JSON");
        }
        check(results.Count==2&&results[0].Snippet=="Brosse anti-emmêlement annoncée."&&results[1].Url=="https://retailer.example/product",
            "Les liens, titres et contenus officiels sont conservés sans doublons ni URL non web");

        handler.Status=HttpStatusCode.TooManyRequests;
        handler.Body="{\"detail\":\"private raw server data\"}";
        try{await client.SearchAsync("frigo",CancellationToken.None);throw new Exception("Expected quota failure");}
        catch(PriceSourceUnavailableException error)
        {
            check(error.Message.Contains("limite",StringComparison.OrdinalIgnoreCase)&&!error.Message.Contains("private",StringComparison.Ordinal),
                "Le quota est expliqué sans afficher le corps brut de la réponse");
        }
        handler.Status=HttpStatusCode.Unauthorized;
        try{await client.SearchAsync("frigo",CancellationToken.None);throw new Exception("Expected access failure");}
        catch(PriceSourceUnavailableException error){check(error.Message.Contains("accès gratuit",StringComparison.Ordinal),"Un refus keyless reste explicite");}
        handler.Status=HttpStatusCode.OK;handler.Body="{\"results\":[]}";
        check((await client.SearchAsync("frigo",CancellationToken.None)).Count==0,"Une recherche vide reste vide");
        handler.Body="{\"results\":";
        try{await client.SearchAsync("frigo",CancellationToken.None);throw new Exception("Expected JSON failure");}
        catch(PriceSourceUnavailableException error){check(error.Message.Contains("illisible",StringComparison.Ordinal),"Une réponse JSON invalide n'est pas une recherche réussie");}
        handler.Body="{\"detail\":\"Limit reached\"}";
        try{await client.SearchAsync("frigo",CancellationToken.None);throw new Exception("Expected shape failure");}
        catch(PriceSourceUnavailableException error){check(error.Message.Contains("inattendue",StringComparison.Ordinal),"Une réponse sans tableau results est signalée");}
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        var before=handler.Calls;
        try{await client.SearchAsync("frigo",cancelled.Token);throw new Exception("Expected cancellation");}
        catch(OperationCanceledException){check(handler.Calls==before,"Une recherche annulée ne part pas sur le réseau");}
        check((await client.SearchAsync(" ",CancellationToken.None)).Count==0&&handler.Calls==before,"Une demande vide n'appelle pas Tavily");
    }

    sealed class ReplyHandler:HttpMessageHandler
    {
        public string Body="",Endpoint="",RequestBody="";
        public HttpStatusCode Status=HttpStatusCode.OK;
        public HttpMethod? Method;
        public bool Keyless,HasAuthorization;
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Calls++;Endpoint=request.RequestUri!.ToString();Method=request.Method;
            Keyless=request.Headers.TryGetValues("X-Tavily-Access-Mode",out var values)&&values.Single()=="keyless";
            HasAuthorization=request.Headers.Contains("Authorization");
            RequestBody=await request.Content!.ReadAsStringAsync(cancellation);
            return new(Status){Content=new StringContent(Body,Encoding.UTF8,"application/json")};
        }
    }
}
