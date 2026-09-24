using System.Net;
using System.Text;
using System.Text.Json;
using Battlestation.Shopping;

internal static class ShoppingLlmTests
{
    public static async Task Run(Action<bool,string> check)
    {
        var defaults=new ShoppingSettings().Validate();
        check(defaults.Provider=="deepseek"&&defaults.Model=="deepseek-flash","Les nouveaux réglages choisissent DeepSeek Flash");
        var local=new ShoppingSettings(Provider:"ollama").Validate();
        check(local.Model=="qwen2.5:7b"&&local.Endpoint=="http://127.0.0.1:11434","Un choix Ollama explicite conserve ses valeurs locales");

        using var handler=new ReplyHandler();
        using var http=new HttpClient(handler);
        var client=new DeepSeekClient(http,"fake-key-for-tests",defaults.Model);
        var spec=await new ShoppingSpecParser(client).ParseAsync("frigo max 800 €, no frost, 300 L, blanc",CancellationToken.None);
        check(handler.Endpoint==DeepSeekClient.Endpoint&&handler.Authorized,"DeepSeek reçoit la requête authentifiée sur son endpoint officiel");
        using var body=JsonDocument.Parse(handler.Body);
        var root=body.RootElement;
        check(root.GetProperty("model").GetString()=="deepseek-flash"&&root.GetProperty("thinking").GetProperty("type").GetString()=="enabled"&&root.GetProperty("reasoning_effort").GetString()=="max","DeepSeek Flash reçoit la réflexion activée au niveau max");
        check(!root.TryGetProperty("max_tokens",out _)&&root.GetProperty("response_format").GetProperty("type").GetString()=="json_object","Le JSON conserve le budget de génération par défaut du mode max, sans plafond court");
        check(spec.Category=="réfrigérateur"&&spec.MaxPrice==800&&spec.Required.Contains("300 L"),"Le JSON DeepSeek devient une demande de recherche exploitable");
        check(spec.Query=="réfrigérateur","La demande DeepSeek garde une recherche boutique courte");
        check(await client.CompleteAsync("JSON", "fixture", CancellationToken.None)==handler.Content,"Seule la réponse finale est utilisée, jamais le contenu de réflexion");
        handler.FinishReason="length";
        bool truncated=false;
        try{await client.CompleteAsync("JSON","fixture",CancellationToken.None);}
        catch(InvalidOperationException e){truncated=e.Message.Contains("limite de génération");}
        check(truncated,"Une génération tronquée est refusée même si son JSON semble complet");
        handler.FinishReason="stop";
        var reasoning=new ShoppingReasoning();
        var configurable=new DeepSeekClient(http,"fake-key-for-tests",defaults.Model,reasoning);
        foreach(string level in new[]{"none","low","high","max"})
        {
            reasoning.Effort=level;await configurable.CompleteAsync("JSON","fixture",CancellationToken.None);
            using var sent=JsonDocument.Parse(handler.Body);
            check(sent.RootElement.GetProperty("reasoning_effort").GetString()==level&&sent.RootElement.GetProperty("thinking").GetProperty("type").GetString()==(level=="none"?"disabled":"enabled"),$"Le choix {level} atteint exactement les paramètres DeepSeek");
        }

        const string robotRequest="Aspirateur robot pour mon appart flat 100m² avec un shiba qui perds ses poils seulement les jours en \"di\" et une femme avec des cheveux longs.";
        var robotLocal=ShoppingSpecParser.Deterministic(robotRequest);
        check(robotLocal.Category=="aspirateur robot"&&robotLocal.Query=="aspirateur robot","La phrase complète du robot conserve le type sans envoyer mon appart aux boutiques");
        check(robotLocal.Required.Contains("100 m²")&&!robotLocal.Required.Contains("100m"),"100m² est une surface à vérifier, pas une référence d'appareil");
        check(ShoppingSpecParser.Deterministic("aspi robot pour mon appartement").Query=="aspirateur robot","Aspi robot est reconnu sans marque imposée");
        handler.Content="{\"category\":\"aspirateur robot\",\"keywords\":[],\"required\":[\"100m²\",\"poils d'animaux\",\"cheveux longs\"]}";
        spec=await new ShoppingSpecParser(client).ParseAsync(robotRequest,CancellationToken.None);
        check(spec.Query=="aspirateur robot"&&spec.Required.Count==3,"Un tableau keywords vide du modèle ne réintroduit ni robot ni mon");
        handler.Content="{\"category\":\"aspirateur\",\"keywords\":[]}";
        spec=await new ShoppingSpecParser(client).ParseAsync(robotRequest,CancellationToken.None);
        check(spec.Category=="aspirateur robot","Le modèle ne transforme pas un aspirateur robot demandé en aspirateur général");

        handler.Status=HttpStatusCode.Unauthorized;
        spec=await new ShoppingSpecParser(client).ParseAsync("frigo max 800 €",CancellationToken.None);
        check(spec.Category=="réfrigérateur"&&spec.MaxPrice==800,"Une clé refusée laisse fonctionner la recherche locale");
        handler.Status=HttpStatusCode.OK;
        handler.Content="";
        spec=await new ShoppingSpecParser(client).ParseAsync("frigo max 800 €",CancellationToken.None);
        check(spec.Category=="réfrigérateur"&&spec.MaxPrice==800,"Une réponse DeepSeek vide laisse fonctionner la recherche locale");
    }

    sealed class ReplyHandler:HttpMessageHandler
    {
        public string Endpoint="",Body="";
        public bool Authorized;
        public string FinishReason="stop";
        public HttpStatusCode Status=HttpStatusCode.OK;
        public string Content="{\"category\":\"réfrigérateur\",\"keywords\":[],\"maxPrice\":800,\"required\":[\"no frost\",\"300 L\",\"blanc\"],\"optional\":[]}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Endpoint=request.RequestUri!.ToString();
            Authorized=request.Headers.Authorization?.Scheme=="Bearer"&&request.Headers.Authorization.Parameter=="fake-key-for-tests";
            Body=await request.Content!.ReadAsStringAsync(cancellation);
            return new HttpResponseMessage(Status)
            {
                Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason=FinishReason,message=new{content=Content,reasoning_content="Réflexion simulée : {\"category\":\"incorrect\"}"}}}}),Encoding.UTF8,"application/json")
            };
        }
    }
}
