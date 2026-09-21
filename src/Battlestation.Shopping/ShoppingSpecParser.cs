using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>
/// Transforme « frigo max 800 €, no frost, 300 L, blanc » en champs exploitables.
/// L'analyse locale répond toujours ; le modèle ne fait que l'enrichir.
/// </summary>
public sealed partial class ShoppingSpecParser(ILlmClient? llm=null)
{
    const string SystemPrompt=
        "Tu extrais une demande d'achat française en JSON strict, sans texte autour. "+
        "Clés attendues : category (nom de produit au singulier, en minuscules), keywords (2 à 4 termes de recherche), "+
        "maxPrice (nombre en euros ou null), required (critères obligatoires), optional (critères souhaités). "+
        "N'invente aucun prix ni aucune marque absente de la demande.";

    public ILlmClient? Model=>llm;

    /// <summary>Demande au modèle, puis retombe sur l'analyse locale s'il répond mal ou pas du tout.</summary>
    public async Task<ShoppingSpec> ParseAsync(string request,CancellationToken cancellation)
    {
        var local=Deterministic(request);
        if(llm is null||string.IsNullOrWhiteSpace(request))return local;
        try
        {
            var text=await llm.CompleteAsync(SystemPrompt,request,cancellation);
            var draft=LlmJson.Extract<SpecDraft>(text);
            return draft is null?local:Merge(local,draft,request);
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or UriFormatException)
        {
            return local;
        }
    }

    static ShoppingSpec Merge(ShoppingSpec local,SpecDraft draft,string request)
    {
        var category=string.IsNullOrWhiteSpace(draft.Category)?local.Category:draft.Category.Trim().ToLowerInvariant();
        var required=Clean(draft.Required) is {Length:>0} fromModel?fromModel:local.Required;
        var keywords=Clean(draft.Keywords) is {Length:>0} modelKeywords?modelKeywords:local.Keywords;
        var optional=Clean(draft.Optional) is {Length:>0} modelOptional?modelOptional:local.Optional;
        decimal? budget=draft.MaxPrice is>0?draft.MaxPrice:local.MaxPrice;
        return new(category,keywords,budget,required,optional,request);
    }

    static string[] Clean(IReadOnlyList<string>? values)=>values is null?[]:values
        .Where(value=>!string.IsNullOrWhiteSpace(value))
        .Select(value=>value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(6).ToArray();

    sealed record SpecDraft(string? Category,List<string>? Keywords,decimal? MaxPrice,List<string>? Required,List<string>? Optional);

    /// <summary>Analyse locale déterministe : budget, catégorie, volume, froid ventilé, couleur, classe énergie.</summary>
    public static ShoppingSpec Deterministic(string request)
    {
        var text=(request??"").Trim();
        var folded=ShoppingText.Fold(text);
        var words=folded.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var required=new List<string>();
        var optional=new List<string>();
        if(Volume().Match(text) is {Success:true} volume&&int.TryParse(volume.Groups[1].Value,out int litres)&&litres is >=20 and <=5000)
            required.Add($"{litres} L");
        if(NoFrost().IsMatch(folded))required.Add("no frost");
        if(EnergyClass().Match(folded) is {Success:true} energy)required.Add($"classe {energy.Groups[1].Value.ToUpperInvariant()}");
        foreach(var color in Colors.Where(color=>words.Contains(color)))required.Add(color);
        required.AddRange(References(words));

        var category=Categories.FirstOrDefault(entry=>entry.Tokens.Any(token=>folded.Contains(token,StringComparison.OrdinalIgnoreCase))).Category??"";
        var keywords=Keywords(words,category,required);
        if(required.Count==0)optional.AddRange(words
            .Where(word=>word.Length>3&&!StopWords.Contains(word)&&!keywords.Contains(word))
            .Distinct().Take(3));
        return new(category,keywords,Amount(text),required,optional,text);
    }

    static readonly (string Category,string[] Tokens)[] Categories=
    [
        ("réfrigérateur",["frigo","refrigerateur","refrigirateur","frigidaire"]),
        ("lave-linge",["lave linge","machine a laver"]),
        ("lave-vaisselle",["lave vaisselle"]),
        ("sèche-linge",["seche linge","secheuse"]),
        ("micro-ondes",["micro ondes"]),
        ("four",["four encastrable","four"]),
        ("téléviseur",["televiseur","television","oled","qled"]),
        ("aspirateur",["aspirateur","robot aspirateur"]),
        ("cafetière",["cafetiere","machine a cafe","expresso","nespresso"]),
        ("ordinateur portable",["pc portable","ordinateur portable","laptop","macbook","ultrabook"]),
        ("smartphone",["smartphone","iphone","galaxy","telephone portable"]),
        ("casque",["casque","ecouteurs","earbuds"]),
        ("imprimante",["imprimante"]),
        ("écran",["moniteur","ecran pc","ecran 27","ecran 24"]),
        ("robot cuiseur",["robot cuiseur","cookeo","monsieur cuisine"]),
        ("barre de son",["barre de son","soundbar"]),
        ("console",["playstation","xbox","switch"]),
        ("trottinette",["trottinette"]),
        ("vélo",["velo","vae"]),
        ("matelas",["matelas"]),
        ("perceuse",["perceuse","visseuse"]),
        ("tondeuse",["tondeuse"])
    ];

    static readonly string[] Colors=["blanc","blanche","noir","noire","inox","gris","argent","rouge","bleu","vert","beige","chrome","cuivre"];

    static readonly string[] StopWords=["max","maxi","maximum","budget","euro","euros","eur","prix","pas","trop","cher","avec","pour","sans","dans","livraison","neuf","neuve","piece","pieces","et","le","la","les","un","une","des","de","du","no"];

    /// <summary>Montant en euros : « 800 € », « max 1 200 € », « budget de 800 euros ».</summary>
    static decimal? Amount(string text)
    {
        var match=BudgetAmount().Match(text);
        if(!match.Success)match=CurrencyAmount().Match(text);
        if(!match.Success)return null;
        return ParseNumber(new string(match.Groups[1].Value.Where(c=>char.IsDigit(c)||c is ',' or '.').ToArray()));
    }

    static decimal? ParseNumber(string digits)
    {
        if(string.IsNullOrWhiteSpace(digits))return null;
        if(digits.Contains(','))digits=digits.Replace(".","").Replace(',','.');
        else if(digits.Count(c=>c=='.')>1||(digits.Contains('.')&&digits.Length-digits.LastIndexOf('.')-1 is not 1 and not 2))digits=digits.Replace(".","");
        return decimal.TryParse(digits,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&value>0?value:null;
    }

    /// <summary>Termes envoyés à la boutique : la marque et le nom, jamais le prix ni les attributs.</summary>
    static string[] Keywords(string[] words,string category,List<string> required)
    {
        var attributes=required.Where(term=>!term.EndsWith(" L",StringComparison.Ordinal)).Select(ShoppingText.Fold).ToArray();
        var attributeWords=attributes.SelectMany(attribute=>attribute.Split(' ',StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var categoryTerms=Categories.Where(entry=>entry.Category==category).SelectMany(entry=>entry.Tokens).ToArray();
        // Les boutiques cherchent mal les demandes longues : « réfrigérateur no frost 300 L »
        // ne renvoie presque rien. La recherche reste courte, les critères servent ensuite
        // à vérifier chaque offre au lieu de la masquer.
        return words
            .Where(word=>word.Length>2&&word.All(char.IsLetter)&&!StopWords.Contains(word)&&!categoryTerms.Contains(word)&&!attributeWords.Contains(word)&&!Colors.Contains(word))
            .Distinct().Take(2).ToArray();
    }

    /// <summary>Références de modèle citées par l'utilisateur : « wh1000xm5 », « rcdl180 ».</summary>
    static string[] References(string[] words)=>words
        .Where(word=>word.Length>=4&&word.Any(char.IsDigit)&&word.Any(char.IsLetter)&&!StopWords.Contains(word))
        .Distinct().Take(2).ToArray();

    [GeneratedRegex(@"(?:max(?:imum)?|budget(?:\s*de)?|jusqu'?\s*[aà]|moins\s*de|<)\s*(\d[\d\s\u00A0.,]{0,9})(?:\s*(?:€|eur|euros?))?",RegexOptions.IgnoreCase)]
    private static partial Regex BudgetAmount();

    [GeneratedRegex(@"(\d[\d\s\u00A0.,]{0,9})\s*(?:€|eur|euros?)\b",RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyAmount();

    [GeneratedRegex(@"(\d{2,4})\s*(?:l|litres?)\b",RegexOptions.IgnoreCase)]
    private static partial Regex Volume();

    [GeneratedRegex("no frost|sans givre|froid ventile",RegexOptions.IgnoreCase)]
    private static partial Regex NoFrost();

    [GeneratedRegex(@"classe\s*([a-g])\b",RegexOptions.IgnoreCase)]
    private static partial Regex EnergyClass();
}
