using System.Text.Json;
using Battlestation.Shopping;

internal static class ShoppingAdvisorTests
{
    public static async Task Run(Action<bool,string> check)
    {
        const string request="Aspirateur robot pour mon appart flat 100m² avec un shiba qui perds ses poils seulement les jours en di et une femme avec des cheveux longs.";
        var spec=ShoppingSpecParser.Deterministic(request);
        ProductCandidate[] candidates=
        [
            Candidate("shop:cheap","Robot aspirateur compact",30m,"Surface maximale : 30 m². Recharge USB."),
            Candidate("shop:fit","Aspirateur robot avec station",399m,"Surface maximale : 150 m². Brosse anti-enchevêtrement pour poils d'animaux et cheveux longs."),
            Candidate("shop:unknown","Robot aspirateur sans fiche",199m,null)
        ];
        var model=new ReplyModel(JsonSerializer.Serialize(new{assessments=new[]{
            new{productId="shop:fit",fit="Recommended",reason="Surface annoncée suffisante et brosse adaptée annoncée.",evidence=new[]{"Surface maximale : 150 m².","Brosse anti-enchevêtrement pour poils d'animaux et cheveux longs."},caveats=new[]{"Performances annoncées, sans essai indépendant."},criteria=new[]{new{criterion="100 m²",state="Confirmed",evidence="Surface maximale : 150 m²."}}},
            new{productId="shop:cheap",fit="Unsuitable",reason="Surface annoncée inférieure aux 100 m² demandés.",evidence=new[]{"Surface maximale : 30 m²."},caveats=Array.Empty<string>(),criteria=new[]{new{criterion="100 m²",state="Contradicted",evidence="Surface maximale : 30 m²."}}},
            new{productId="shop:unknown",fit="Unknown",reason="Aucune fiche ne documente la surface ou les poils.",evidence=Array.Empty<string>(),caveats=new[]{"Surface et entretien des cheveux inconnus."},criteria=new[]{new{criterion="100 m²",state="Unknown",evidence=""}}}
        }}));
        var advisor=new ShoppingAdvisor(model);
        var results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(model.Calls==1,"Une seule comparaison analyse tous les candidats");
        check(results.Select(item=>item.ProductId).SequenceEqual(new[]{"shop:fit","shop:cheap","shop:unknown"}),"L'ordre comparatif du modèle est conservé plutôt que le prix");
        check(results[0].Fit==ProductFit.Recommended&&results[1].Fit==ProductFit.Unsuitable&&results[2].Fit==ProductFit.Unknown,"Les jugements documentés et l'absence de fiche restent distincts");
        using(var payload=JsonDocument.Parse(model.User))
        {
            check(payload.RootElement.GetProperty("request").GetString()==request,"La demande originale et ses usages parviennent à l'analyse");
            var sent=payload.RootElement.GetProperty("candidates");
            check(sent.GetArrayLength()==3&&sent[0].GetProperty("price").GetDecimal()==30m,"Les prix connus sont transmis sans seuil d'exclusion");
            check(sent[1].GetProperty("details").GetProperty("text").GetString()==candidates[1].Details!.Text,"Les faits de la fiche accompagnent les IDs réels");
        }

        model.Response="""
            {"assessments":[
              {"productId":"invented:1","fit":"Recommended","reason":"Faux produit","evidence":["150 m²"]},
              {"productId":"shop:fit","fit":"Recommended","reason":"Citation inventée","evidence":["Autonomie de 400 minutes"]},
              {"productId":"shop:cheap","fit":"Possible","reason":"Premier avis","evidence":["Recharge USB."]},
              {"productId":"shop:cheap","fit":"Unsuitable","reason":"Deuxième avis","evidence":["Surface maximale : 30 m²."]}
            ]}
            """;
        results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.Count==3&&results.All(item=>candidates.Any(candidate=>candidate.Product.Id==item.ProductId)),"Aucun produit inventé ni ID dupliqué ne devient un résultat");
        check(results.All(item=>item.Fit==ProductFit.Unknown),"Citation inventée, avis contradictoires et produit omis deviennent inconnus");
        check(results.Single(item=>item.ProductId=="shop:cheap").Reason.Contains("plusieurs",StringComparison.Ordinal),"Un doublon est rejeté explicitement");

        model.Response="""
            {"assessments":[
              {"productId":"shop:cheap","fit":"Recommended","reason":"Pas cher donc bon","evidence":[]},
              {"productId":"shop:fit","fit":"Unsuitable","reason":"Incompatible sans preuve","evidence":[]},
              {"productId":"shop:unknown","fit":"Possible","reason":"Modèle connu","evidence":["Brosse anti-enchevêtrement pour poils d'animaux et cheveux longs."]}
            ]}
            """;
        results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.All(item=>item.Fit==ProductFit.Unknown),"Le prix seul et une preuve d'un autre appareil ne valident aucune aptitude ou exclusion");

        model.Response="{\"assessments\":[{\"productId\":\"shop:unknown\",\"fit\":\"Recommended\",\"reason\":\"Convient aux 100 m² et aux poils\",\"evidence\":[\"Robot aspirateur sans fiche\"]}]}";
        results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.Single(item=>item.ProductId=="shop:unknown").Fit==ProductFit.Unknown,"Le nom d'un robot seul ne prouve pas son aptitude aux 100 m² et aux poils");

        model.Response="{\"assessments\":[{\"productId\":\"shop:fit\",\"fit\":\"Possible\",\"reason\":\"Brosse annoncée adaptée, usage à confirmer.\",\"evidence\":[\"Brosse   anti-enchevêtrement pour poils d'animaux et cheveux longs.\"],\"caveats\":[\"Pas de test indépendant.\"]}]}";
        results=await advisor.ReviewAsync(spec with{Required=[]},candidates,CancellationToken.None);
        check(results[0].Fit==ProductFit.Possible&&results[0].Caveats.Count==1,"Les différences d'espacement des citations ne perdent pas une preuve valide");

        model.Response="{\"assessments\":";
        results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.All(item=>item.Fit==ProductFit.Unknown&&item.Reason.Contains("invalide",StringComparison.Ordinal)),"Un JSON tronqué signale une analyse indisponible");
        model.Failure=new HttpRequestException("offline");
        results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.All(item=>item.Fit==ProductFit.Unknown&&item.Reason.Contains("indisponible",StringComparison.Ordinal)),"Une panne modèle laisse les offres visibles sans faux avis");
        model.Failure=new TaskCanceledException();
        results=await advisor.ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.All(item=>item.Fit==ProductFit.Unknown&&item.Reason.Contains("temps",StringComparison.Ordinal)),"Un délai modèle dépassé est signalé");
        results=await new ShoppingAdvisor(null).ReviewAsync(spec,candidates,CancellationToken.None);
        check(results.All(item=>item.Fit==ProductFit.Unknown&&item.Reason.Contains("clé API",StringComparison.Ordinal)),"Une clé absente ne ressemble pas à une analyse réussie");

        using var cancelled=new CancellationTokenSource();
        cancelled.Cancel();
        try{await advisor.ReviewAsync(spec,candidates,cancelled.Token);throw new Exception("Expected cancellation");}
        catch(OperationCanceledException){check(true,"L'annulation utilisateur reste une annulation");}
        model.Failure=null;model.Response="{}";
        var twelve=Enumerable.Range(0,13).Select(index=>Candidate($"shop:{index}","Robot aspirateur",100m,"Fiche")).ToArray();
        results=await advisor.ReviewAsync(spec,twelve,CancellationToken.None);
        using var bounded=JsonDocument.Parse(model.User);
        check(results.Count==12&&bounded.RootElement.GetProperty("candidates").GetArrayLength()==12,"Une analyse est bornée à douze candidats");
        var calls=model.Calls;
        results=await advisor.ReviewAsync(spec,[],CancellationToken.None);
        check(results.Count==0&&model.Calls==calls,"Sans candidat aucun appel modèle ne part");
    }

    static ProductCandidate Candidate(string id,string title,decimal price,string? details)
    {
        var product=new Product(id,"shop","https://example.test/"+id,title,"","","","Fixture",price);
        return new(product,details is null?null:new ProductDetails(details,product.Url));
    }

    sealed class ReplyModel(string response):ILlmClient
    {
        public string Name=>"Fake advisor";
        public string Response=response,User="";
        public Exception? Failure;
        public int Calls;
        public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)
        {
            Calls++;User=user;
            return Failure is {} failure?Task.FromException<string>(failure):Task.FromResult(Response);
        }
    }
}
