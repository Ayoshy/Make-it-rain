using System.Text.Json;
using Battlestation.Shopping;

internal static class ShoppingPipelineTests
{
    const string Request="Aspirateur robot pour 100 m², un shiba et des cheveux longs";

    public static async Task Run(Action<bool,string> check,string temp)
    {
        Product[] products=[Offer("compact",30m),Offer("unknown",19m),Offer("premium",699m),Offer("adapted",399m)];
        var descriptions=new Dictionary<string,string>
        {
            ["compact"]="Surface maximale : 30 m². Recharge USB.",
            ["premium"]="Surface maximale : 150 m². Brosse anti-enchevêtrement pour poils et cheveux longs.",
            ["adapted"]="Surface maximale : 150 m². Brosse anti-enchevêtrement pour poils et cheveux longs."
        };
        var source=new Source("web",products,descriptions);
        var legacy=new Source("legacy",[],new Dictionary<string,string>());
        var reply=new ReplyModel(JsonSerializer.Serialize(new{assessments=new object[]{
            new{productId="compact",fit="Unsuitable",reason="Surface annoncée inférieure aux 100 m² demandés.",evidence=new[]{"Surface maximale : 30 m²."},criteria=new[]{new{criterion="100 m²",state="Contradicted",evidence=descriptions["compact"]}}},
            new{productId="adapted",fit="Recommended",reason="Surface et brosse annoncées adaptées.",evidence=new[]{descriptions["adapted"]},price=1,url="https://invented.invalid/replacement",criteria=new[]{new{criterion="100 m²",state="Confirmed",evidence=descriptions["adapted"]}}},
            new{productId="invented",fit="Recommended",reason="Produit absent des sources.",evidence=new[]{"Invention"}},
            new{productId="premium",fit="Recommended",reason="Autre appareil avec surface et brosse adaptées annoncées.",evidence=new[]{descriptions["premium"]},criteria=new[]{new{criterion="100 m²",state="Confirmed",evidence=descriptions["premium"]}}},
            new{productId="unknown",fit="Unknown",reason="Fiche absente.",evidence=Array.Empty<string>()}
        }}));
        using var http=new HttpClient(new NoNetwork());
        using var store=new ShoppingStore(Path.Combine(temp,"pipeline.db"));
        using var service=new ShoppingService(store,[source,legacy],ShoppingSettings.Default with{MaxResults=2},temp,http,
            parser:new ShoppingSpecParser(),advisor:new ShoppingAdvisor(reply));
        var phases=new List<(string Activity,bool Busy)>();
        service.Changed+=()=>phases.Add((service.Activity,service.Busy));
        var outcome=await service.SearchAsync(Request,CancellationToken.None);
        check(outcome.Hits.Select(hit=>hit.Product.Id).SequenceEqual(new[]{"adapted","premium"}),"Le parcours complet privilégie le robot adapté à 399 € et exclut le 30 € incompatible avant la limite de résultats");
        check(outcome.Hits.Count==2&&outcome.Hits.All(hit=>hit.Assessment?.Fit==ProductFit.Recommended),"L'exclusion avant MaxResults conserve deux appareils documentés");
        check(source.DetailsRead.SetEquals(products.Select(product=>product.Id)),"Les fiches de tous les candidats sont lues avant la comparaison");
        check(source.Searches==1&&legacy.Searches==0,"La recherche Web remplace la découverte par une liste fermée de boutiques");
        check(reply.Calls==1,"Le parcours compare tous les candidats en un seul appel modèle simulé");
        check(outcome.Hits.All(hit=>products.Any(product=>product.Id==hit.Product.Id&&product.Url==hit.Product.Url&&product.Price==hit.Amount)),"Le modèle ne crée ni offre ni URL et ne remplace aucun prix relevé");
        check(store.FindProduct("invented") is null&&store.FindProduct("compact") is null,"Les produits inventés et les appareils exclus n'entrent pas dans l'historique de la recherche");
        using(var payload=JsonDocument.Parse(reply.User))
        {
            var candidates=payload.RootElement.GetProperty("candidates");
            check(payload.RootElement.GetProperty("request").GetString()==Request&&candidates.GetArrayLength()==4,"La comparaison reçoit la demande originale et tous les candidats avant MaxResults");
            check(candidates.EnumerateArray().Single(item=>item.GetProperty("productId").GetString()=="adapted").GetProperty("details").GetProperty("sourceUrl").GetString()==products[3].Url,"Les preuves transmises restent liées à la fiche exacte de l'appareil");
        }
        int analyse=phases.FindIndex(phase=>phase.Activity.StartsWith("Analyse de la demande",StringComparison.Ordinal));
        int recherche=phases.FindIndex(phase=>phase.Activity.StartsWith("Recherche",StringComparison.Ordinal));
        int lecture=phases.FindIndex(phase=>phase.Activity.StartsWith("Lecture",StringComparison.Ordinal));
        int comparaison=phases.FindIndex(phase=>phase.Activity.StartsWith("Comparaison",StringComparison.Ordinal));
        check(analyse>=0&&recherche>analyse&&lecture>recherche&&comparaison>lecture,"L'activité publie successivement analyse, recherche, lecture et comparaison");
        check(phases.Where(phase=>phase.Activity.Length>0).All(phase=>phase.Busy)&&!service.Busy&&service.Activity.Length==0,"Le parcours reste occupé durant ses phases puis retrouve l'état inactif");

        reply.Response=JsonSerializer.Serialize(new{assessments=new[]{new{productId="adapted",fit="Recommended",reason="Surface et brosse annoncées adaptées.",evidence=new[]{descriptions["adapted"]},criteria=new[]{new{criterion="100 m²",state="Confirmed",evidence=descriptions["adapted"]}}}}});
        var single=await service.SearchAsync(Request,CancellationToken.None);
        check(single.Hits.Count(hit=>hit.Assessment?.Fit!=ProductFit.Unknown)==1&&single.Hits[0].Product.Id=="adapted","Une seule recommandation reste distincte des pistes d'aptitude inconnue");

        reply.Response="réponse illisible";
        var degraded=await service.SearchAsync(Request,CancellationToken.None);
        check(degraded.Hits.Count>0&&degraded.Hits.All(hit=>hit.Assessment?.Fit==ProductFit.Unknown)&&degraded.AnalysisNote.Contains("0 choix confirmé",StringComparison.Ordinal),"Une analyse inutilisable conserve les offres réelles avec une aptitude inconnue explicitement signalée");
        check(degraded.Hits.All(hit=>products.Any(product=>product.Id==hit.Product.Id&&product.Price==hit.Amount)),"Le repli sans analyse ne fabrique aucun prix ou produit");
        await CheckProgress(check,temp,http);
    }

    static async Task CheckProgress(Action<bool,string> check,string temp,HttpClient http)
    {
        using var store=new ShoppingStore(Path.Combine(temp,"pipeline-progress.db"));
        var source=new ProgressSource();
        using var service=new ShoppingService(store,[source],ShoppingSettings.Default,temp,http,
            parser:new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null));
        check(service.SearchStartedAt is null&&service.Progress.Count==0,"Avant la première recherche, le journal et son horodatage sont absents");
        var before=DateTimeOffset.UtcNow;
        var first=service.SearchAsync(Request,CancellationToken.None);
        await source.Entered.Task;
        var parallelSource=new ProgressSource();
        using var parallel=new ShoppingService(store,[parallelSource],ShoppingSettings.Default,temp,http,
            parser:new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null));
        parallel.SetReasoningEffort("high");
        var concurrent=parallel.SearchAsync("Casque pour le bureau",CancellationToken.None);
        await parallelSource.Entered.Task;
        parallelSource.Report("Lecture du casque");
        check(service.Busy&&parallel.Busy&&parallel.ReasoningEffort=="high"&&service.ReasoningEffort=="max"
            &&!service.Progress.Any(step=>step.Message=="Lecture du casque"),"Deux moteurs sur la même base travaillent simultanément avec journaux et réflexions indépendants");
        bool protectedLevel=false;
        try{parallel.SetReasoningEffort("low");}catch(InvalidOperationException){protectedLevel=true;}
        check(protectedLevel&&parallel.ReasoningEffort=="high","Le moteur refuse de changer le niveau pendant son appel");
        parallelSource.Completion.SetResult([]);await concurrent;
        check(service.Busy&&!parallel.Busy,"La fin d'une recherche ne libère pas le verrou d'une autre");
        check(service.Busy&&service.SearchStartedAt is {} started&&started>=before&&started<=DateTimeOffset.UtcNow,"Le début de recherche publie son horodatage pendant le travail en cours");
        source.Report("Connexion au moteur de recherche");
        check(service.Activity=="Connexion au moteur de recherche"&&service.Progress[^1].Message==service.Activity,"La progression fine de la source devient immédiatement l'activité visible");
        for(int index=1;index<=20;index++)source.Report($"Page {index} : lecture des caractéristiques");
        var bounded=service.Progress;
        check(bounded.Count>20&&bounded.Any(entry=>entry.Message.StartsWith("Page 1 :",StringComparison.Ordinal))&&bounded[^1].Message.StartsWith("Page 20 :",StringComparison.Ordinal),"Le journal conserve toutes les étapes de la recherche pour les grands docks");
        check(bounded.All(entry=>entry.At>=service.SearchStartedAt&&entry.At<=DateTimeOffset.UtcNow)&&bounded.Select(entry=>entry.At).SequenceEqual(bounded.Select(entry=>entry.At).Order()),"Les étapes sont horodatées dans l'ordre de la recherche en cours");
        source.Report(bounded[^1].Message);
        check(service.Progress.Count==bounded.Count&&service.Progress[^1].At==bounded[^1].At,"Une étape répétée ne remplit pas le journal de doublons");
        source.Completion.SetResult([]);
        await first;
        check(!service.Busy&&service.Activity.Length==0&&service.Progress[^1].Message.StartsWith("Recherche terminée",StringComparison.Ordinal),"Une recherche réussie conserve son étape finale après la fin du chargement");
        var finished=service.Progress;
        source.Report("Message tardif de la source");
        check(service.Progress.SequenceEqual(finished),"Une source au repos ne pollue pas le journal de la recherche terminée");

        var previousStart=service.SearchStartedAt;
        source.Reset();
        var second=service.SearchAsync(Request,CancellationToken.None);
        await source.Entered.Task;
        check(service.SearchStartedAt>=previousStart&&service.Progress.Count<12&&!service.Progress.Any(entry=>entry.Message.StartsWith("Page ",StringComparison.Ordinal)||entry.Message.StartsWith("Recherche terminée",StringComparison.Ordinal)),"Une nouvelle recherche réinitialise le journal et son temps de départ");
        source.Report("Attente de la réponse du moteur");
        source.Completion.SetException(new PriceSourceUnavailableException("Tavily indisponible (429)"));
        var failed=await second;
        check(failed.Error.Contains("429",StringComparison.Ordinal)&&failed.Sources.Single().State==SourceState.Failed,"Le refus du moteur reste un échec explicite dans le résultat de recherche");
        check(!service.Busy&&service.Activity.Length==0&&service.Progress.Any(entry=>entry.Message.Contains("429",StringComparison.Ordinal))&&service.Progress[^1].Message.StartsWith("Recherche arrêtée",StringComparison.Ordinal),"Le journal conserve la cause de l'échec et termine le chargement");
    }

    static Product Offer(string id,decimal price)=>new(id,"web","https://merchant.invalid/"+id,"Aspirateur robot modèle "+id,"","","","Boutique",price);

    sealed class Source(string id,IReadOnlyList<Product> products,IReadOnlyDictionary<string,string> descriptions):IPriceSource
    {
        public string Id=>id;
        public string Name=>"Recherche de test "+id;
        public int Searches{get;private set;}
        public HashSet<string> DetailsRead{get;}=[];
        public Task<IReadOnlyList<Product>> SearchAsync(ShoppingSpec spec,int limit,CancellationToken cancellation)
        {Searches++;return Task.FromResult(products);}
        public Task<PricePoint?> GetPriceAsync(Product product,CancellationToken cancellation)=>Task.FromResult<PricePoint?>(null);
        public Task<ProductDetails?> GetDetailsAsync(Product product,CancellationToken cancellation)
        {
            DetailsRead.Add(product.Id);
            return Task.FromResult(descriptions.TryGetValue(product.Id,out var text)?new ProductDetails(text,product.Url):null);
        }
    }

    sealed class ReplyModel(string response):ILlmClient
    {
        public string Name=>"Comparaison simulée";
        public string Response{get;set;}=response;
        public string User{get;private set;}="";
        public int Calls{get;private set;}
        public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)
        {Calls++;User=user;return Task.FromResult(Response);}
    }

    sealed class ProgressSource:IPriceSource,IShoppingProgressSource
    {
        public string Id=>"web";
        public string Name=>"Recherche avec journal";
        public event Action<string>? Progress;
        public TaskCompletionSource Entered{get;private set;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<Product>> Completion{get;private set;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<Product>> SearchAsync(ShoppingSpec spec,int limit,CancellationToken cancellation)
        {
            Entered.TrySetResult();
            return Completion.Task.WaitAsync(cancellation);
        }
        public Task<PricePoint?> GetPriceAsync(Product product,CancellationToken cancellation)=>Task.FromResult<PricePoint?>(null);
        public void Report(string message)=>Progress?.Invoke(message);
        public void Reset()
        {
            Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    sealed class NoNetwork:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
            =>throw new Exception("Un test du parcours shopping ne doit pas appeler le réseau.");
    }
}
