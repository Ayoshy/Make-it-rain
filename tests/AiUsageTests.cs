using CodexUsageTray;
using Battlestation.Core;

static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
var now=DateTimeOffset.Now;
var models=new[]{
    new ModelUsageBreakdown("gpt-6-astra","high",1_000_000,800_000,100_000,1_100_000,1,7.8m,0),
    new ModelUsageBreakdown("deepseek-flash","unspecified",1_000_000,500_000,100_000,1_100_000,1,.1365m,0),
    new ModelUsageBreakdown("unknown","unspecified",1_000,0,500,1_500,1,null,0)};
var day=DateOnly.FromDateTime(now.LocalDateTime);
var estimate=new ApiEquivalentEstimate(7.9365m,null,2_201_500,null,2,1,false,["unknown"],models,
    [new(day,"deepseek-flash",1100,1000,500,100),new(day.AddDays(-1),"gpt-6-astra",2000,1000,0,1000)]);
var report=new AiUsageReport(estimate,now);
Check(report.Metric("ai:openai:total:count")=="1"&&report.Metric("ai:deepseek:total:count")=="1","Families are isolated");
Check(report.Metric("ai:unknown:total:0:cost")=="Non tarifé","Unknown model has no substitute price");
Check(report.Metric("ai:claude:total:tokens")=="—"&&report.Metric("ai:claude:total:cost")=="—","Missing provider is not zero");
Check(report.Metric("ai:openai:today:count")=="0"&&report.Metric("ai:deepseek:today:count")=="1","Today follows the local date and provider");
Check(report.Metric("ai:deepseek:total:0:cache")=="50 %","Cache percentage uses input counters");
Check(report.Metric("ai:deepseek:total:input")!=report.Metric("ai:deepseek:total:tokens"),"Uncached input is distinct from all tokens");
var folder=Path.Combine(Path.GetTempPath(),"Battlestation-AiUsage-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(folder,"sessions"));
try
{
    string rollout=Path.Combine(folder,"sessions","rollout-fixture.jsonl");
    string timestamp=now.ToString("O");
    await File.WriteAllTextAsync(rollout,"{\"type\":\"turn_context\",\"payload\":{\"model\":\"deepseek-flash\"}}\n"+
        "{\"timestamp\":\""+timestamp+"\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"input_tokens\":1000,\"cached_input_tokens\":500,\"output_tokens\":100,\"total_tokens\":1100}}}}\n");
    var estimator=new ApiEquivalentEstimator(folder,Path.Combine(folder,"cache.json"));
    var parsed=await estimator.EstimateAsync(null);
    Check(parsed?.DailyUsage is [{InputTokens:1000,CachedInputTokens:500,OutputTokens:100}],"Parser preserves daily split for model costs");
    var second=await estimator.EstimateAsync(null);
    Check(second?.ParsedTokens==parsed?.ParsedTokens,"Cached refresh does not double count");
}
finally{Directory.Delete(folder,true);}
await ClaudeCounters();
ClaudeAccountReader();
Console.WriteLine("AI_USAGE_CHECKS_PASS");

async Task ClaudeCounters()
{
    var home=Path.Combine(Path.GetTempPath(),"Battlestation-Claude-"+Guid.NewGuid().ToString("N"));
    var project=Path.Combine(home,"projects","C--Users-Ayo-Documents-Project-Battlestation");
    Directory.CreateDirectory(project);
    var stamp=DateTimeOffset.Now.ToString("O");
    string Line(string id,string model,long input,long creation,long cached,long output)
        =>"{\"type\":\"assistant\",\"timestamp\":\""+stamp+"\",\"message\":{\"id\":\""+id+"\",\"model\":\""+model+"\","+
          "\"usage\":{\"input_tokens\":"+input+",\"cache_creation_input_tokens\":"+creation+
          ",\"cache_read_input_tokens\":"+cached+",\"output_tokens\":"+output+"}}}";
    try
    {
        // Un message tronque en plusieurs lignes ne compte qu'une fois : Claude Code
        // reecrit la meme reponse, et une session reprise recopie les memes messages.
        await File.WriteAllTextAsync(Path.Combine(project,"session-a.jsonl"),
            Line("msg_1","claude-opus-5-5",1000,100,9000,100)+"\n"+
            Line("msg_1","claude-opus-5-5",1000,100,9000,100)+"\n"+
            "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"bonjour\"}}\n"+
            Line("msg_2","claude-haiku-4-5-20251001",100,0,0,10)+"\n"+
            Line("msg_3","<synthetic>",0,0,0,0)+"\n");
        var estimator=new ClaudeSessionEstimator(home,Path.Combine(home,"cache.json"));
        var estimate=await estimator.EstimateAsync();
        var opus=estimate?.Models.FirstOrDefault(model=>model.Model=="claude-opus-5-5");
        Check(opus is not null&&opus.InputTokens==10100&&opus.CachedInputTokens==9000&&opus.CacheCreationTokens==100,
            "Claude counters keep the input, cache read and cache write slices");
        Check(opus?.TotalTokens==10200&&estimate?.ParsedTokens==10310,"A repeated message is counted once");
        Check(opus?.DollarAmount==0.037875m,"Cache writes are priced at the API rate, not the plain input rate");
        Check(estimate?.Models.Count==2,"A synthetic service message carries no counters");
        Check(estimate?.DailyUsage?.Any(day=>day.Model=="claude-opus-5-5"&&day.CacheCreationTokens==100)==true,
            "The daily split keeps the cache write counter");
        var again=await estimator.EstimateAsync();
        Check(again?.ParsedTokens==estimate?.ParsedTokens,"A cached refresh does not count Claude sessions twice");
        var report=new AiUsageReport(estimate,now);
        Check(report.Metric("ai:claude:total:count")=="2","Claude models reach the Claude family");
        Check(report.Metric("ai:claude:today:count")=="2","Claude counters land on the local day");
        Check(report.Metric("ai:claude:total:write")!="—","Cache writes are exposed to the dock");
        Check(report.Metric("ai:claude:total:input")!="—"&&report.Metric("ai:claude:total:input")!=report.Metric("ai:claude:total:tokens"),
            "Uncached input stays distinct from the total");
        Check(report.Metric("ai:openai:total:count")=="0","Claude sessions do not pollute the OpenAI family");
        var linked=ApiEquivalentEstimate.Combine(estimate,estimate);
        Check(linked?.Models.Count==4&&linked.DollarAmount==estimate!.DollarAmount*2,"Combining readers adds their counters");
        Check(ApiEquivalentEstimate.Combine(null,null)is null,"No reader means no report");
        Check(ApiEquivalentEstimator.CalculateCost("claude-sonnet-4-6",1_000_000,0,0)==3m,"The Sonnet family price applies by name");
        Check(ApiEquivalentEstimator.CalculateCost("gpt-inconnu",1_000_000,0,0) is null,"An unknown model stays unpriced");
    }
    finally{Directory.Delete(home,true);}
}

void ClaudeAccountReader()
{
    const string payload="""
    {"five_hour":{"utilization":3.0,"resets_at":"2026-09-25T18:19:59.939327+00:00"},
     "seven_day":{"utilization":0.0,"resets_at":"2026-09-25T23:59:59.939347+00:00"},
     "limits":[{"kind":"session","group":"session","percent":3,"severity":"normal","resets_at":"2026-09-25T18:19:59.939327+00:00","is_active":true},
               {"kind":"weekly_all","group":"weekly","percent":12,"severity":"warning","resets_at":"2026-09-28T23:59:59.939347+00:00","is_active":false},
               {"kind":"weekly_opus","group":"weekly","percent":40,"resets_at":null}],
     "spend":{"used":{"amount_minor":1234,"currency":"EUR","exponent":2},"limit":{"amount_minor":200000,"currency":"EUR","exponent":2},"enabled":true}}
    """;
    var account=ClaudeUsageReader.Parse(payload,now,"pro");
    Check(account?.Windows.Count==3,"Every published Claude window is kept");
    Check(account?.Primary?.Kind=="session"&&account.Primary.Label=="5 heures","The session window leads the list");
    Check(account?.Windows[1].Label=="7 jours"&&account.Windows[1].UsedPercent==12,"The weekly window keeps its own percentage");
    Check(account?.Windows[2].Label=="7 jours · Opus"&&account.Windows[2].ResetsAt is null,"A named weekly window stays readable without a reset");
    Check(account?.Spend is {Currency:"EUR",Used:12.34m,Limit:2000m},"The extra usage credit is converted from minor units");
    Check(account?.Plan=="pro","The subscription plan is reported as published");
    Check(account?.Primary?.ResetsAt?.Offset==TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.Now),"Reset times are converted to local time");
    var fallback=ClaudeUsageReader.Parse("""{"five_hour":{"utilization":25,"resets_at":"2026-09-25T18:19:59+00:00"},"seven_day":{"utilization":50}}""",now);
    Check(fallback?.Windows.Count==2&&fallback.Windows[0].UsedPercent==25,"A response without a limits list falls back to the two windows");
    // Les montants en dollars ne sont publies que pour les comptes factures ainsi.
    var dollars=ClaudeUsageReader.Parse("""
    {"limits":[{"kind":"session","percent":30,"limit_dollars":50,"remaining_dollars":35}],
     "spend":{"enabled":true,"used":{"amount_minor":500,"currency":"USD","exponent":2},
              "limit":{"amount_minor":5000,"currency":"USD","exponent":2},
              "balance":{"amount_minor":1250,"currency":"USD","exponent":2}}}
    """,now);
    Check(dollars?.Primary is {RemainingDollars:35m,LimitDollars:50m},"A dollar-billed window keeps its published amounts");
    Check(dollars?.Spend is {Currency:"USD",Balance:12.50m,Used:5m,Limit:50m},"A published credit balance is kept with its own currency");
    var withoutMoney=ClaudeUsageReader.Parse("""{"limits":[{"kind":"session","percent":30,"limit_dollars":null,"remaining_dollars":null}]}""",now);
    Check(withoutMoney?.Primary is {RemainingDollars:null,LimitDollars:null},"An account billed in limits publishes no dollar amount");
    // Le credit de session cloud arrive sous un nom de code instable : seuls les
    // blocs publiant un montant restant sont repris, jamais le nom de code lui-meme.
    var credit=ClaudeUsageReader.Parse("""
    {"limits":[{"kind":"session","percent":11}],"iguana_necktie":{"utilization":0,"resets_at":"2026-11-05T07:59:00+00:00","limit_dollars":100,"used_dollars":0,"remaining_dollars":100},
     "nimbus_quill":{"utilization":0}}
    """,now);
    Check(credit?.PublishedCredits is [{Key:"iguana_necktie",Remaining:100m,Limit:100m,UsedPercent:0}] ,
        "A dollar credit is kept with its published amount");
    Check(credit?.PublishedCredits[0].ExpiresAt==DateTimeOffset.Parse("2026-11-05T07:59:00+00:00").ToLocalTime(),
        "A dollar credit keeps its expiry");
    Check(credit?.Windows.Count==1,"A credit bucket is not a quota window");
    Check(withoutMoney?.PublishedCredits.Count==0,"A block without an amount is not a credit");
    Check(ClaudeUsageReader.Parse("{}",now) is null,"A response without any quota is not invented");
    Check(ClaudeUsageReader.Parse("pas du json",now) is null,"An unreadable response is not a quota");
    Check(ClaudeUsageReader.Parse(payload,now)?.Spend?.Used==12.34m,"The extra usage credit survives without a plan");
}
