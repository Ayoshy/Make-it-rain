using System.Net;
using System.Text;
using System.Text.Json;
using Battlestation.Shopping;

internal static class SerperSearchTests
{
    public static async Task Run(Action<bool,string> check)
    {
        using var handler=new Handler();using var http=new HttpClient(handler);
        var client=new SerperSearchClient(http,"fixture-serper-key");
        var found=await client.SearchAsync("aspirateur robot",CancellationToken.None);
        using var payload=JsonDocument.Parse(handler.Body);
        check(handler.Url==SerperSearchClient.Endpoint&&handler.Key=="fixture-serper-key"&&!handler.Body.Contains("fixture-serper-key"),"Serper reçoit la clé uniquement dans son en-tête API");
        check(payload.RootElement.GetProperty("gl").GetString()=="fr"&&payload.RootElement.GetProperty("hl").GetString()=="fr"&&payload.RootElement.GetProperty("num").GetInt32()==10,"Serper utilise les résultats français sans demander cent résultats");
        check(found.Count==1&&found[0].Url=="https://shop.example/product"&&found[0].Snippet=="Robot neuf", "Les résultats Serper deviennent des liens uniques, sans URL non web");
        string? key=null;var configured=new ConfiguredWebSearchClient(http,()=>key);
        check(configured.Id=="tavily","Sans clé enregistrée, le moteur existant reste sélectionné");
        key="fixture-serper-key";
        check(configured.Id=="serper"&&(await configured.SearchAsync("frigo",CancellationToken.None)).Count==1,"Une clé enregistrée sélectionne Serper sans redémarrage");
        handler.Status=HttpStatusCode.Forbidden;bool refused=false;
        try{await configured.SearchAsync("frigo",CancellationToken.None);}catch(PriceSourceUnavailableException e){refused=e.Message.Contains("Serper")&&!e.Message.Contains(key);}
        check(refused&&handler.Url==SerperSearchClient.Endpoint,"Une erreur Serper ne révèle pas la clé et ne bascule pas vers un autre fournisseur");
    }
    sealed class Handler:HttpMessageHandler
    {
        public string Body="",Key="",Url="";public HttpStatusCode Status=HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Url=request.RequestUri!.AbsoluteUri;Key=request.Headers.GetValues("X-API-KEY").Single();Body=await request.Content!.ReadAsStringAsync(cancellation);
            return new(Status){Content=new StringContent("""{"organic":[{"title":"Robot R1","link":"https://shop.example/product","snippet":"Robot neuf"},{"title":"Doublon","link":"https://shop.example/product"},{"title":"Fichier","link":"file:///private"}]}""",Encoding.UTF8,"application/json")};
        }
    }
}
