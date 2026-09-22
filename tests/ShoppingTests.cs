using System.Text.Json;
using Battlestation.Shopping;

// Contrôles du radar d'achat : analyse de la demande, verdicts, calendrier,
// historique SQLite, veille et alertes. Le réseau n'est sollicité qu'avec --live.
static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
static void Equal<T>(T expected,T actual,string text){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"{text} ({expected} ≠ {actual})");Console.WriteLine("PASS "+text);}

string temp=Path.Combine(Path.GetTempPath(),"Battlestation-shopping-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    await ShoppingLlmTests.Run(Check);
    await ShoppingAdvisorTests.Run(Check);
    await ShoppingSourceExpansionTests.Run(Check,temp);
    await ShoppingPipelineTests.Run(Check,temp);
    await ShoppingWebTests.Run(Check,temp);
    await TavilySearchTests.Run(Check);
    ProductPageLinksTests.Run(Check);
    await ShoppingUpgradeTests.Run(Check,temp);
    await ShoppingRelevanceTests.Run(Check,temp);
    // ── Analyse locale de la demande ────────────────────────────────────────────
    var fridge=ShoppingSpecParser.Deterministic("frigo max 800 €, no frost, 300 L, blanc");
    Equal("réfrigérateur",fridge.Category,"La demande de frigo donne la catégorie");
    Equal<decimal?>(800,fridge.MaxPrice,"Le budget est lu sur « max 800 € »");
    Check(fridge.Required.Contains("no frost"),"Le froid ventilé est un critère obligatoire");
    Check(fridge.Required.Contains("300 L"),"Le volume est un critère obligatoire");
    Check(fridge.Required.Contains("blanc"),"La couleur est un critère obligatoire");
    Check(fridge.Query.StartsWith("réfrigérateur",StringComparison.Ordinal),"La recherche part du produit");
    Check(!fridge.Query.Contains("800",StringComparison.Ordinal),"Le budget n'est pas envoyé à la boutique");
    Equal("réfrigérateur",fridge.Query,"La recherche reste courte : les boutiques ne comprennent pas les demandes longues");
    Check(fridge.Required.Count>=3,"Les trois critères de la demande sont conservés");

    var headphones=ShoppingSpecParser.Deterministic("casque Sony WH-1000XM5 max 300 €");
    Equal("casque",headphones.Category,"Un casque est reconnu");
    Equal<decimal?>(300,headphones.MaxPrice,"Le budget du casque est lu");
    Check(headphones.Query.Contains("sony",StringComparison.OrdinalIgnoreCase),"La marque reste dans la recherche");
    Check(headphones.Required.Contains("1000xm5"),"La référence du modèle devient un critère vérifiable");

    var television=ShoppingSpecParser.Deterministic("TV OLED 55 pouces, budget de 1 200 €");
    Equal("téléviseur",television.Category,"Un téléviseur est reconnu");
    Equal<decimal?>(1200,television.MaxPrice,"Le budget avec espace est lu");

    Equal("",ShoppingSpecParser.Deterministic("").Query,"Une demande vide n'invente aucun produit");

    // ── Extraction JSON du modèle ───────────────────────────────────────────────
    Equal("réfrigérateur",LlmJson.Extract<Draft>("```json\n{\"category\":\"réfrigérateur\",\"maxPrice\":800}\n```")?.Category,"Le JSON encadré est extrait");
    Equal<decimal?>(800,LlmJson.Extract<Draft>("{\"maxPrice\":\"800\"}")?.MaxPrice,"Un nombre converti en chaîne reste un prix");
    Check(LlmJson.Extract<Draft>("pas de json ici") is null,"Une réponse sans objet JSON est refusée");

    var withModel=await new ShoppingSpecParser(new FixedModel("{\"category\":\"réfrigérateur combiné\",\"keywords\":[\"no frost\",\"blanc\"],\"required\":[\"300 L\"],\"maxPrice\":850}"))
        .ParseAsync("frigo max 800 €",CancellationToken.None);
    Equal("réfrigérateur combiné",withModel.Category,"Le modèle affine la catégorie");
    Equal<decimal?>(800,withModel.MaxPrice,"Le modèle ne relève pas le budget explicite de l'utilisateur");
    var broken=await new ShoppingSpecParser(new FixedModel("désolé, je ne peux pas répondre"))
        .ParseAsync("frigo max 800 €",CancellationToken.None);
    Equal("réfrigérateur",broken.Category,"Une réponse illisible retombe sur l'analyse locale");
    Equal<decimal?>(800,broken.MaxPrice,"Le budget local survit à un modèle inutile");

    // ── Calendrier promotionnel déterministe ───────────────────────────────────
    Equal(new DateOnly(2026,1,14),PromoCalendar.WinterSalesStart(2026),"Les soldes d'hiver 2026 commencent le deuxième mercredi de janvier");
    Equal(new DateOnly(2026,6,24),PromoCalendar.SummerSalesStart(2026),"Les soldes d'été 2026 suivent le dernier mercredi de juin");
    Equal(new DateOnly(2022,6,22),PromoCalendar.SummerSalesStart(2022),"Après le 28 juin, les soldes d'été reculent à l'avant-dernier mercredi");
    Equal(new DateOnly(2026,11,27),PromoCalendar.BlackFriday(2026),"Le Black Friday suit le quatrième jeudi de novembre");
    var events=PromoCalendar.Computed(2026);
    Check(events.Any(item=>item.Label=="Black Friday"&&item.Start==new DateOnly(2026,11,27)),"Le calendrier expose le Black Friday");
    Check(!events.Any(item=>item.Label.Contains("Prime Day")||item.Label.Contains("French Days")),"Les dates commerciales variables ne sont pas inventées");
    Check(events.All(item=>item.DiscountHint==0),"Le calendrier ne promet aucun pourcentage de remise");
    var calendar=PromoCalendar.Load(Path.Combine(temp,"absent.json"));
    Equal(0,calendar.Upcoming(new DateOnly(2026,9,17),0).Count,"Une fenêtre nulle n'annonce aucun événement");
    var autumn=calendar.Between(new DateOnly(2026,9,1),new DateOnly(2026,12,31));
    Check(autumn.Any(item=>item.Label=="Black Friday"&&item.Start==new DateOnly(2026,11,27)),"Le Black Friday est daté dans l'année");
    Check(autumn.Any(item=>item.Label=="Cyber Monday"&&item.Start==new DateOnly(2026,11,30)),"Le Cyber Monday suit le Black Friday");
    Check(calendar.Next(new DateOnly(2026,9,17),30) is null,"Pas d'annonce promotionnelle sans date connue dans la période");
    Equal("Black Friday",calendar.Next(new DateOnly(2026,11,17),30,"Rue du Commerce")?.Label??"","Un repère national connu reste consultable");

    var custom=Path.Combine(temp,ShoppingSettingsStore.EventsFileName);
    await File.WriteAllTextAsync(custom,JsonSerializer.Serialize(new PromoEvent[]{new("boulanger-anniv","Boulanger","Anniversaire Boulanger",new DateOnly(2026,10,10),new DateOnly(2026,10,12),.12)}));
    var loaded=PromoCalendar.Load(custom);
    Equal("Anniversaire Boulanger",loaded.Next(new DateOnly(2026,9,17),30,"Boulanger")?.Label??"","Le JSON par enseigne ajoute un repère propre à la boutique");
    Check(loaded.Next(new DateOnly(2026,9,17),30,"Rue du Commerce") is null,"Un repère d'une autre enseigne ne s'impose pas");
    Equal("Boulanger",loaded.Next(new DateOnly(2026,9,17),30,"Boulanger")?.Shop??"","Le repère personnalisé garde son enseigne");

    // ── Verdicts ───────────────────────────────────────────────────────────────
    var engine=new VerdictEngine();
    var settings=ShoppingSettings.Default;
    var spec=ShoppingSpecParser.Deterministic("frigo max 800 €");
    var product=new Product("test:1","test","https://exemple.fr/1","Réfrigérateur combiné LISTO 300 L","LISTO","",  "","Test",349m);
    var today=DateOnly.FromDateTime(DateTime.Today);

    var flat=History("test:1",[399m,379m,359m,349m]);
    var buy=engine.Evaluate(product,flat,[],spec,settings);
    Equal(VerdictDecision.Acheter,buy.Decision,"Un prix au plancher sans promo déclenche « achète »");
    Check(buy.Reasons.Any(reason=>reason.Contains("plancher",StringComparison.OrdinalIgnoreCase)),"La raison cite le plancher observé");
    Check(buy.Sources.Contains("https://exemple.fr/1"),"La source du prix accompagne le verdict");

    var expensive=product with{Price=429m};
    PromoEvent[] promo=[new($"soldes-ete-{today.Year}","","Soldes d'été",today.AddDays(8),today.AddDays(35),.2)];
    var wait=engine.Evaluate(expensive,flat,promo,spec,settings);
    Equal(VerdictDecision.Attendre,wait.Decision,"Une promo proche sur un prix haut donne « attends »");
    Check(wait.Reasons.Any(reason=>reason.Contains("Soldes d'été",StringComparison.Ordinal)),"La raison nomme l'événement attendu");
    Check(wait.Reasons.Any(reason=>reason.Contains("jours",StringComparison.Ordinal)||reason.Contains("dans 8 j",StringComparison.Ordinal)),"La raison donne le délai");

    var watch=engine.Evaluate(expensive,flat,[],spec,settings);
    Equal(VerdictDecision.Surveiller,watch.Decision,"Un prix haut sans rendez-vous commercial reste « surveille »");
    Equal<decimal?>(Math.Round(349m*(decimal)settings.BuyMargin,2),watch.TargetPrice,"La cible suit le plancher et la marge");

    var over=engine.Evaluate(product with{Price=949m},flat,[],spec,settings);
    Equal(VerdictDecision.Surveiller,over.Decision,"Un prix au-dessus du budget n'est jamais « achète »");
    Check(over.Reasons.Any(reason=>reason.Contains("budget",StringComparison.OrdinalIgnoreCase)),"La raison nomme le budget");

    var blind=engine.Evaluate(product with{Price=null},[],[],spec,settings);
    Equal(VerdictDecision.Surveiller,blind.Decision,"Sans prix relevé, la réponse reste prudente");
    Check(blind.Confidence<.3,"Sans relevé, la confiance est basse");

    var thin=engine.Evaluate(product with{Price=339m},History("test:1",[349m]),[],spec,settings);
    Check(thin.Reasons.Any(reason=>reason.Contains("historique court",StringComparison.OrdinalIgnoreCase)),"Un historique court est annoncé");
    var thinPromo=engine.Evaluate(product,[],promo,spec,settings);
    Check(thinPromo.Decision==VerdictDecision.Surveiller&&!thinPromo.HasSufficientHistory&&thinPromo.PriceLabel=="Historique insuffisant","Une promo ne crée pas un conseil attends sans historique");
    Check(!thinPromo.Reasons.Any(reason=>reason.Contains("plancher")||reason.Contains("remise annoncée")),"Un budget seul ne devient ni plancher mesuré ni remise annoncée");

    // ── Historique SQLite ──────────────────────────────────────────────────────
    var database=Path.Combine(temp,"shopping.db");
    using(var store=new ShoppingStore(database))
    {
        store.SaveProduct(product);
        Equal("Réfrigérateur combiné LISTO 300 L",store.FindProduct("test:1")?.Title??"","Le produit est relu depuis SQLite");
        Check(store.RecordPrice(new("test:1",DateTimeOffset.Now.AddDays(-40),399m,"EUR",true,"test")),"Un relevé s'enregistre");
        Check(store.RecordPrice(new("test:1",DateTimeOffset.Now.AddDays(-30),379m,"EUR",true,"test")),"Un second relevé s'enregistre");
        Check(store.RecordPrice(new("test:1",DateTimeOffset.Now,349m,"EUR",true,"test")),"Le relevé du jour s'enregistre");
        var history=store.History("test:1",90);
        Equal(3,history.Count,"Les trois relevés forment l'historique");
        Equal<decimal?>(349,store.LastPrice("test:1")?.Price,"Le dernier prix est le plus récent");
        Check(!store.RecordPrice(new("test:1",DateTimeOffset.Now.AddMinutes(5),349m,"EUR",true,"test")),"Un prix identique dans la journée ne crée pas de doublon");
        Equal(3,store.History("test:1",90).Count,"L'historique ne gonfle pas d'un relevé identique");
        Check(store.RecordPrice(new("test:1",DateTimeOffset.Now.AddDays(1),349m,"EUR",true,"test")),"Un prix stable reçoit son relevé du lendemain");
        Equal(4,store.History("test:1",90).Count,"La tendance se construit même sans changement de prix");
        store.SaveWatch(new WatchItem("watch:test:1","frigo max 800 €","test:1",400m,DateTimeOffset.Now,DateTimeOffset.Now,DateTimeOffset.Now,0,false,null,product.Title,product.Shop));
        var watches=store.Watches();
        Equal(1,watches.Count,"La veille est enregistrée");
        Equal("test:1",watches[0].Watch.ProductId,"La veille cite son produit");
        Equal("Test",watches[0].Product.Shop,"La veille retrouve la boutique");
        Equal<decimal?>(349,watches[0].Watch.LastPrice,"La veille porte le dernier prix connu");
    }
    using(var reopened=new ShoppingStore(database))
    {
        Equal(4,reopened.History("test:1",90).Count,"L'historique survit à la réouverture");
        reopened.RemoveWatch("watch:test:1");
        Equal(0,reopened.Watches().Count,"Une veille peut être retirée");
    }

    // Un fichier illisible est mis de côté : le bureau démarre quand même.
    var corrupt=Path.Combine(temp,"corrupt.db");
    await File.WriteAllTextAsync(corrupt,"ceci n'est pas une base SQLite");
    using(var salvaged=new ShoppingStore(corrupt))
    {
        Equal(0,salvaged.Watches().Count,"Un historique illisible repart d'une base neuve");
        salvaged.SaveProduct(product);
        Equal("Réfrigérateur combiné LISTO 300 L",salvaged.FindProduct("test:1")?.Title??"","La base reconstruite accepte les écritures");
    }
    Check(Directory.GetFiles(temp,"corrupt.db.invalid-*").Length==1,"Le fichier illisible est conservé de côté");

    // Un fichier inaccessible n'est pas une preuve de corruption.
    var lockedDatabase=Path.Combine(temp,"locked.db");
    using(var ready=new ShoppingStore(lockedDatabase))ready.SaveProduct(product);
    var originalDatabase=File.ReadAllBytes(lockedDatabase);
    using(var held=new FileStream(lockedDatabase,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
    {
        try
        {
            using var inaccessible=new ShoppingStore(lockedDatabase);
            throw new Exception("La base verrouillée ne doit pas être ouverte");
        }
        catch(Microsoft.Data.Sqlite.SqliteException)
        {
            Check(true,"Une erreur d'accès SQLite reste une erreur d'accès");
        }
    }
    Check(originalDatabase.SequenceEqual(File.ReadAllBytes(lockedDatabase)),"Une base inaccessible reste intacte");
    Equal(0,Directory.GetFiles(temp,"locked.db.invalid-*").Length,"Une base inaccessible n'est pas mise en quarantaine");

    using(var stockStore=new ShoppingStore(Path.Combine(temp,"stock.db")))
    {
        var at=DateTimeOffset.Now.AddHours(-2);
        Check(stockStore.RecordPrice(new("stock:1",at,100m,"EUR",true,"stock")),"Le premier état de stock est conservé");
        Check(stockStore.RecordPrice(new("stock:1",at.AddMinutes(30),100m,"EUR",false,"stock")),"Une rupture au même prix est conservée");
        Check(!stockStore.LastPrice("stock:1")!.InStock,"Le dernier relevé signale la rupture");
        Check(stockStore.RecordPrice(new("stock:1",at.AddMinutes(60),null,"EUR",false,"stock")),"Une rupture sans prix reste enregistrée");
        Equal<decimal?>(null,stockStore.LastPrice("stock:1")!.Price,"Le prix absent ne devient pas zéro");
        Check(stockStore.RecordPrice(new("stock:1",at.AddMinutes(90),100m,"EUR",true,"stock")),"Le retour au même prix est conservé");
        Check(!stockStore.RecordPrice(new("stock:1",at.AddMinutes(95),100m,"EUR",true,"stock")),"Le prix et le stock inchangés sont dédupliqués");
    }

    // ── Recherche multi-boutiques et veille ────────────────────────────────────
    using(var store=new ShoppingStore(Path.Combine(temp,"engine.db")))
    {
        var cheap=new FakeSource("alpha","Alpha",[
            new Product("alpha:1","alpha","https://alpha.fr/1","Réfrigérateur combiné LISTO RCDL180 300 L","LISTO","RCDL180","","Alpha",409m),
            new Product("alpha:2","alpha","https://alpha.fr/2","Réfrigérateur top ESSENTIELB ERT85 250 L","ESSENTIELB","ERT85","","Alpha",299m)]);
        var dearer=new FakeSource("beta","Bêta",[
            new Product("beta:1","beta","https://beta.fr/1","Réfrigérateur combiné LISTO RCDL180 300 L","LISTO","RCDL180","","Bêta",389m)]);
        var rejected=new FakeSource("gamma","Gamma",[]){Failure="Gamma refuse la lecture"};
        using var service=new ShoppingService(store,[cheap,dearer,rejected],settings,temp,new HttpClient(),engine,calendar,new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null));
        var outcome=await service.SearchAsync("frigo max 500 €, 300 L",CancellationToken.None);
        Equal(2,outcome.Hits.Count,"Les deux appareils distincts sont présentés");
        Equal(SourceState.Failed,outcome.Sources.Single(report=>report.Source=="gamma").State,"Une boutique en échec est signalée");
        Equal(SourceState.Ok,outcome.Sources.Single(report=>report.Source=="alpha").State,"Une boutique lue est signalée");
        var combined=outcome.Hits.Single(hit=>hit.Product.Title.Contains("RCDL180",StringComparison.Ordinal));
        Equal("Bêta",combined.Product.Shop,"Entre deux boutiques, l'offre la moins chère est gardée");
        Check(combined.Alternatives.Any(alternative=>alternative.Contains("Alpha")),"L'autre boutique reste citée comme alternative");
        // Veille : prix cible atteint, notification, puis retour au calme.
        var watched=outcome.Hits[0];
        service.Watch(watched.Product,watched.Product.Price??300m,"frigo max 500 €, 300 L");
        Check(service.IsWatched(watched.Product.Id),"L'article surveillé est connu du moteur");
        ShoppingAlert? received=null;
        service.Alert+=alert=>received=alert;
        var watchedSource=watched.Product.Source==cheap.Id?cheap:dearer;
        watchedSource.NextPrice=(watched.Product.Price??300m)-40m;
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(received is not null,"Un prix cible atteint produit une alerte");
        Check(received!.Message.Contains(watched.Product.Title,StringComparison.Ordinal),"L'alerte nomme l'article");
        Check(received.Url==watched.Product.Url,"L'alerte renvoie vers l'annonce");
        var stored=store.Watches()[0].Watch;
        Check(stored.Alerted,"La veille retient que l'alerte a été envoyée");
        received=null;
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(received is null,"La même alerte n'est pas répétée au passage suivant");
        for(int index=0;index<3;index++)
        {
            store.SaveWatch(store.Watches()[0].Watch with{NextCheck=DateTimeOffset.Now.AddSeconds(-1)});
            await service.CheckWatchesAsync(CancellationToken.None);
            Check(received is null,$"La cible inchangée reste silencieuse à l'échéance {index+2}");
        }
        watchedSource.NextPrice=(watched.Product.Price??300m)+20m;
        store.SaveWatch(store.Watches()[0].Watch with{NextCheck=DateTimeOffset.Now.AddSeconds(-1)});
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(!store.Watches()[0].Watch.Alerted,"Le prix revenu au-dessus de la cible réarme l'alerte");
        watchedSource.NextPrice=(watched.Product.Price??300m)-40m;
        store.SaveWatch(store.Watches()[0].Watch with{NextCheck=DateTimeOffset.Now.AddSeconds(-1)});
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(received is not null,"Une nouvelle baisse sous la cible produit une nouvelle alerte");
        received=null;
        watchedSource.InStock=false;
        store.SaveWatch(store.Watches()[0].Watch with{NextCheck=DateTimeOffset.Now.AddSeconds(-1)});
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(received is null,"Une offre indisponible sous la cible ne produit pas d'alerte de prix");
        watchedSource.InStock=true;
        store.SaveWatch(store.Watches()[0].Watch with{NextCheck=DateTimeOffset.Now.AddSeconds(-1)});
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(received is not null,"Le retour en stock au même prix produit une alerte");
        received=null;
        watchedSource.NextPrice=(watched.Product.Price??300m)-100m;
        await service.CheckWatchesAsync(CancellationToken.None);
        Check(received is null,"La veille automatique attend la prochaine échéance");
        await service.CheckWatchesAsync(CancellationToken.None,force:true);
        Check(received is not null,"Une revérification manuelle relit le prix avant l'échéance");

        // Échec de lecture : la cadence recule au lieu de marteler la boutique.
        var failing=new FakeSource("epsilon","Epsilon",[new Product("epsilon:1","epsilon","https://epsilon.fr/1","Réfrigérateur combiné LISTO RCDL180 300 L","LISTO","RCDL180","","Epsilon",359m)]);
        using var second=new ShoppingService(store,[cheap,failing],settings,temp,new HttpClient(),engine,calendar,new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null));
        second.Watch(failing.Products[0],300m,"frigo max 500 €");
        var pending=store.Watches().Single(item=>item.Watch.ProductId=="epsilon:1").Watch;
        store.SaveWatch(pending with{NextCheck=DateTimeOffset.Now.AddSeconds(-1)});
        failing.Failure="Epsilon injoignable";
        await second.CheckWatchesAsync(CancellationToken.None);
        var after=store.Watches().Single(item=>item.Watch.ProductId=="epsilon:1").Watch;
        Equal(1,after.Failures,"Un échec est compté");
        Check(after.NextCheck>DateTimeOffset.Now.AddMinutes(settings.CadenceMinutes),"Après un échec, la revérification est repoussée");
    }

    // Une boutique sans résultat n'est pas une panne : la recherche se termine proprement.
    using(var quietStore=new ShoppingStore(Path.Combine(temp,"quiet.db")))
    using(var quiet=new ShoppingService(quietStore,[new FakeSource("delta","Delta",[])],settings,temp,new HttpClient(),engine,calendar,new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null)))
    {
        var outcome=await quiet.SearchAsync("frigo max 500 €",CancellationToken.None);
        Equal(SourceState.Empty,outcome.Sources.Single().State,"Une boutique sans résultat est signalée vide");
        Equal(0,outcome.Hits.Count,"Sans offre, aucun résultat n'est inventé");
        Equal("",outcome.Error,"Un résultat vide n'est pas une erreur");
    }

    // Toutes les boutiques muettes : la raison remonte au lieu d'un écran vide sans explication.
    using(var deadStore=new ShoppingStore(Path.Combine(temp,"dead.db")))
    using(var dead=new ShoppingService(deadStore,[new FakeSource("delta","Delta",[]){Failure="Delta refuse la lecture"}],settings,temp,new HttpClient(),engine,calendar,new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null)))
    {
        var outcome=await dead.SearchAsync("frigo max 500 €",CancellationToken.None);
        Equal(SourceState.Failed,outcome.Sources.Single().State,"Une boutique muette est signalée en panne");
        Check(outcome.Error.Contains("Delta",StringComparison.Ordinal),"La raison de l'échec est disponible");
    }

    using(var busyStore=new ShoppingStore(Path.Combine(temp,"busy.db")))
    {
        var delayed=new DelayedModel();
        using var pending=new ShoppingService(busyStore,[],settings,temp,parser:new ShoppingSpecParser(delayed),advisor:new ShoppingAdvisor(null));
        var searching=pending.SearchAsync("frigo max 500 €",CancellationToken.None);
        Check(pending.Busy,"La recherche est occupée dès l'analyse de la demande");
        try
        {
            await pending.SearchAsync("casque",CancellationToken.None);
            throw new Exception("Une seconde recherche ne doit pas démarrer en parallèle");
        }
        catch(InvalidOperationException){Check(true,"Une recherche en cours ne peut pas être doublée");}
        Check(pending.Busy,"Le refus du doublon ne termine pas la recherche active");
        delayed.Response.SetResult("{}");
        await searching;
        Check(!pending.Busy,"L'état occupé disparaît après la recherche");
        await pending.SearchAsync("",CancellationToken.None);
        Check(!pending.Busy,"Une demande vide libère aussi l'état occupé");
    }

    if(args.Contains("--live"))
    {
        var cache=Path.Combine(temp,"cache");
        var http=new HttpClient{Timeout=Timeout.InfiniteTimeSpan};
        var options=new PriceSourceOptions(cache,Timeout:TimeSpan.FromSeconds(25),MinimumInterval:TimeSpan.FromSeconds(5));
        var live=new IPriceSource[]{new RueDuCommerceSource(http,options),new BoulangerSource(http,options)};
        foreach(var source in live)
        {
            try
            {
                var products=await source.SearchAsync(ShoppingSpecParser.Deterministic("frigo no frost 300 L"),6,CancellationToken.None);
                Check(products.Count>0,$"{source.Name} renvoie des offres");
                Check(products.Count<=6,$"{source.Name} respecte la limite demandée");
                Check(products.All(product=>product.Url.StartsWith("https://",StringComparison.Ordinal)),$"{source.Name} donne un lien par offre");
                Console.WriteLine($"     {source.Name} :");
                foreach(var offer in products.Take(3))
                    Console.WriteLine($"       {ShoppingText.Money(offer.Price)} · {offer.Title} · {offer.Url}");
                var first=products.First(product=>product.Price is>0);
                Check(products.Count(product=>product.Price is>0)>=products.Count-1,$"{source.Name} lit le prix de chaque offre");
                var price=await source.GetPriceAsync(first,CancellationToken.None);
                Check(price?.Price is>0,$"{source.Name} relit le prix sur la fiche produit");
                Console.WriteLine($"       fiche {first.Url} → {ShoppingText.Money(price!.Price)} ({(price.InStock?"en stock":"indisponible")})");
            }
            catch(PriceSourceUnavailableException e)
            {
                Console.WriteLine($"     {source.Name} indisponible : {e.Message}");
            }
        }
        // Le cas réel du plan, de bout en bout : demande en français, deux boutiques,
        // dédoublonnage, historique et verdict.
        using var store=new ShoppingStore(Path.Combine(temp,"live.db"));
        using var radar=new ShoppingService(store,live,settings,temp,http,engine,calendar,new ShoppingSpecParser(),advisor:new ShoppingAdvisor(null));
        var request="frigo max 800 €, no frost, 300 L, blanc";
        var outcome=await radar.SearchAsync(request,CancellationToken.None);
        Console.WriteLine($"     Demande « {request} » → catégorie {outcome.Spec.Category}, budget {ShoppingText.Money(outcome.Spec.MaxPrice)}");
        Console.WriteLine($"     Critères : {string.Join(", ",outcome.Spec.Required)}");
        foreach(var report in outcome.Sources)Console.WriteLine($"     {report.Name} : {report.State} · {report.Detail}");
        Check(outcome.Hits.Count>0,"Le cas « frigo 800 € » ramène des offres réelles");
        Check(outcome.Hits.Any(hit=>hit.Product.Price is>0),"Les offres réelles portent leur prix");
        Check(outcome.Hits.All(hit=>hit.Verdict.Reasons.Count>0),"Chaque verdict réel s'explique");
        foreach(var hit in outcome.Hits)
            Console.WriteLine($"       {hit.Verdict.Label,-9} {ShoppingText.Money(hit.Amount),-11} {hit.Product.Shop,-16} {hit.CriteriaText,-12} {hit.Product.Title}");
    }
    return 0;
}
finally
{
    Directory.Delete(temp,true);
}

static IReadOnlyList<PricePoint> History(string productId,decimal[] prices)
{
    var start=DateTimeOffset.Now.AddDays(-90);
    return prices.Select((price,index)=>new PricePoint(productId,start.AddDays(index*20),price,"EUR",true,"test")).ToArray();
}

sealed record Draft(string? Category,List<string>? Keywords,decimal? MaxPrice,List<string>? Required,List<string>? Optional);

sealed class FixedModel(string answer):ILlmClient
{
    public string Name=>"modèle de test";
    public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)=>Task.FromResult(answer);
}

sealed class DelayedModel:ILlmClient
{
    public string Name=>"modèle retardé de test";
    public TaskCompletionSource<string> Response{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)=>Response.Task;
}

sealed class FakeSource(string id,string name,IReadOnlyList<Product> products):IPriceSource
{
    public string Id=>id;
    public string Name=>name;
    public string Failure{get;set;}="";
    public decimal? NextPrice{get;set;}
    public bool InStock{get;set;}=true;
    public IReadOnlyList<Product> Products=>products.Select(product=>NextPrice is{} price?product with{Price=price}:product).ToArray();
    public Task<IReadOnlyList<Product>> SearchAsync(ShoppingSpec spec,int limit,CancellationToken cancellation)
        =>Failure.Length>0?Task.FromException<IReadOnlyList<Product>>(new PriceSourceUnavailableException(Failure)):Task.FromResult(Products);
    public Task<PricePoint?> GetPriceAsync(Product product,CancellationToken cancellation)
        =>Failure.Length>0?Task.FromException<PricePoint?>(new PriceSourceUnavailableException(Failure)):Task.FromResult<PricePoint?>(new(product.Id,DateTimeOffset.Now,NextPrice??product.Price,"EUR",InStock,id));
}
