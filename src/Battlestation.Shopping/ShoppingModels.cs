namespace Battlestation.Shopping;

/// <summary>Ce que l'utilisateur demande, une fois la phrase libre transformée en champs.</summary>
public sealed record ShoppingSpec(
    string Category,
    IReadOnlyList<string> Keywords,
    decimal? MaxPrice,
    IReadOnlyList<string> Required,
    IReadOnlyList<string> Optional,
    string Request)
{
    public static readonly ShoppingSpec Empty=new("",[],null,[],[],"");

    /// <summary>Termes envoyés aux boutiques : la catégorie puis les mots-clés conservés.</summary>
    public string Query=>string.Join(' ',new[]{Category}.Concat(Keywords).Where(term=>!string.IsNullOrWhiteSpace(term)));

    public ShoppingSpec WithCategory(string category)=>this with{Category=category};
}

/// <summary>Un produit neuf relevé chez une boutique. Le prix peut venir de la liste ou de la fiche.</summary>
public sealed record Product(
    string Id,
    string Source,
    string Url,
    string Title,
    string Brand,
    string Model,
    string Image,
    string Shop,
    decimal? Price=null)
{
    /// <summary>Clé de regroupement entre boutiques : même appareil, même titre normalisé.</summary>
    public string Identity=>Model.Length>0?$"{ShoppingText.Fold(Brand)}:{ProductIdentity.Key(Model)}:{ProductIdentity.Variant(Title)}":ShoppingText.Identity(Brand+" "+Title);
}

/// <summary>Extrait de la fiche réellement consultée, utilisé uniquement pendant l'analyse.</summary>
public sealed record ProductDetails(string Text,string SourceUrl);
public sealed record ShoppingProgress(DateTimeOffset At,string Message);
public sealed class ShoppingReasoning
{
    string effort="max";
    public string Effort {get=>effort;set=>effort=value is "none" or "low" or "high" or "max"?value:"max";}
}
public sealed record ProductCandidate(Product Product,ProductDetails? Details);
public enum ProductFit { Recommended, Possible, Unsuitable, Unknown }
public enum CriterionState { Confirmed, Contradicted, Unknown }
public sealed record CriterionAssessment(string Criterion,CriterionState State,string Evidence);
public sealed record ProductAssessment(string ProductId,ProductFit Fit,string Reason,IReadOnlyList<string> Evidence,IReadOnlyList<string> Caveats,IReadOnlyList<CriterionAssessment>? Criteria=null,string EvidenceSource="")
{
    public string Label=>Fit switch
    {
        ProductFit.Recommended=>"À privilégier",
        ProductFit.Possible=>"À comparer",
        ProductFit.Unsuitable=>"Non adapté",
        _=>"À vérifier"
    };
}

/// <summary>Un relevé de prix daté. Un produit sans stock reste relevé : l'absence de prix n'est pas zéro.</summary>
public sealed record PricePoint(string ProductId, DateTimeOffset At, decimal? Price, string Currency, bool InStock, string Source)
{
    public bool Usable=>Price is > 0 && InStock;
}

public enum VerdictDecision { Acheter, Attendre, Surveiller }

/// <summary>Décision argumentée : les raisons citent les prix et les dates qui la fondent.</summary>
public sealed record Verdict(
    VerdictDecision Decision,
    double Confidence,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Sources,
    decimal? TargetPrice,bool HasSufficientHistory=false)
{
    public static readonly Verdict Unknown=new(VerdictDecision.Surveiller,0,["Aucun relevé de prix"],[],null);

    public string Label=>Decision switch{VerdictDecision.Acheter=>"achète",VerdictDecision.Attendre=>"attends",_=>"surveille"};
    public string PriceLabel=>!HasSufficientHistory?"Historique insuffisant":Decision switch
    {
        VerdictDecision.Acheter=>"Prix bas observé",
        VerdictDecision.Attendre=>"Baisse à attendre",
        _=>"Prix à surveiller"
    };
}

/// <summary>Une offre retenue pour l'affichage : le produit, son relevé du jour et le verdict.</summary>
public sealed record ShoppingHit(Product Product, PricePoint? Price, Verdict Verdict, IReadOnlyList<string> Alternatives, int Criteria=0, int CriteriaTotal=0,ProductAssessment? Assessment=null)
{
    public decimal? Amount=>Price?.Price;

    /// <summary>Un critère absent du titre reste à vérifier, sans conclure que l'appareil ne le satisfait pas.</summary>
    public string CriteriaText=>CriteriaTotal==0?"":Criteria==0?"critères à vérifier":$"{Criteria}/{CriteriaTotal} critères vérifiés";
}

/// <summary>Une entrée de veille : l'article est revérifié en fond jusqu'à ce que l'utilisateur l'arrête.</summary>
public sealed record WatchItem(
    string Id,
    string Request,
    string ProductId,
    decimal? TargetPrice,
    DateTimeOffset AddedAt,
    DateTimeOffset LastCheck,
    DateTimeOffset NextCheck,
    int Failures,
    bool Alerted,
    decimal? LastPrice,
    string Title,
    string Shop);

/// <summary>Événement promotionnel daté, déterministe ou fourni par l'utilisateur dans son JSON.</summary>
public sealed record PromoEvent(string Id,string Shop,string Label,DateOnly Start,DateOnly End,double DiscountHint=0)
{
    public bool Covers(DateOnly day)=>day>=Start&&day<=End;
    public string DateText=>Start==End?Start.ToString("d MMMM",ShoppingText.French):$"{Start:d MMMM} → {End:d MMMM}";
}

/// <summary>Notification de veille : un prix cible est atteint ou l'article redevient disponible.</summary>
public sealed record ShoppingAlert(string Title,string Message,string Url,decimal? Price);

/// <summary>Une veille avec le produit qu'elle suit : le dock a besoin du titre, de la boutique et de l'image.</summary>
public sealed record WatchedItem(WatchItem Watch,Product Product);

/// <summary>Résultat d'une recherche : les offres classées, les sources interrogées et l'état de chacune.</summary>
public sealed record SearchOutcome(IReadOnlyList<ShoppingHit> Hits,IReadOnlyList<SourceReport> Sources,ShoppingSpec Spec,string Request,string AnalysisNote="")
{
    public static SearchOutcome Failed(string request,ShoppingSpec spec,IReadOnlyList<SourceReport> sources)=>new([],sources,spec,request);

    public string Error=>Sources.All(source=>source.State==SourceState.Failed)?string.Join(" · ",Sources.Select(source=>source.Detail)):"";
}

public enum SourceState { Ok, Empty, Failed, Disabled }

public sealed record SourceReport(string Source,string Name,SourceState State,int Count,string Detail,bool Partial=false);

/// <summary>Erreur d'une boutique : elle est rapportée telle quelle, jamais remplacée par un prix inventé.</summary>
public sealed class PriceSourceUnavailableException(string message):Exception(message);

/// <summary>Réglages persistés dans shopping.json (%LOCALAPPDATA%\Battlestation).</summary>
public sealed record ShoppingSettings(
    string Provider="deepseek",
    string Model="",
    string Endpoint="",
    int CadenceMinutes=30,
    int HistoryDays=90,
    int MinPricePoints=3,
    double BuyMargin=1.02,
    int WaitWindowDays=14,
    int MaxResults=6,
    string NtfyTopic="",
    bool Enabled=true)
{
    public static readonly ShoppingSettings Default=new ShoppingSettings().Validate();

    public ShoppingSettings Validate()=>this with
    {
        Provider=Provider is "ollama"?"ollama":"deepseek",
        Model=string.IsNullOrWhiteSpace(Model)?(Provider is "ollama"?"qwen2.5:7b":DeepSeekClient.DefaultModel):Model.Trim(),
        Endpoint=string.IsNullOrWhiteSpace(Endpoint)?(Provider is "ollama"?"http://127.0.0.1:11434":"https://api.deepseek.com"):Endpoint.Trim().TrimEnd('/'),
        CadenceMinutes=Math.Clamp(CadenceMinutes,5,24*60),
        HistoryDays=Math.Clamp(HistoryDays,14,365),
        MinPricePoints=Math.Clamp(MinPricePoints,1,30),
        BuyMargin=Math.Clamp(BuyMargin,1,1.3),
        WaitWindowDays=Math.Clamp(WaitWindowDays,0,60),
        MaxResults=Math.Clamp(MaxResults,2,12),
        NtfyTopic=NtfyTopic.Trim()
    };
}

/// <summary>Normalisation partagée : accents, casse et ponctuation ne doivent pas séparer deux fois le même mot.</summary>
public static class ShoppingText
{
    public static readonly System.Globalization.CultureInfo French=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    public static string Fold(string text)
    {
        var normalized=text.Normalize(System.Text.NormalizationForm.FormD);
        var builder=new System.Text.StringBuilder(normalized.Length);
        foreach(char c in normalized)
        {
            if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)==System.Globalization.UnicodeCategory.NonSpacingMark)continue;
            builder.Append(char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):' ');
        }
        return string.Join(' ',builder.ToString().Split(' ',StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Empreinte stable d'un titre : les mots de liaison et les volumes ne créent pas de doublon.</summary>
    public static string Identity(string text)
    {
        // La couleur et la finition restent dans l'empreinte : deux finitions du même
        // appareil sont deux références, elles ne doivent pas fusionner.
        var stop=new HashSet<string>{"de","du","des","la","le","les","un","une","et","avec","pour","en","au","aux"};
        var words=Fold(text).Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(word=>word.Length>1&&!stop.Contains(word)).Take(9).ToArray();
        return string.Join(' ',words);
    }

    public static string Money(decimal? amount)=>amount is null?"—":$"{amount.Value:N2} €";

    public static string ShortDate(DateTimeOffset at)=>at.ToLocalTime().ToString("dd/MM HH:mm",French);

    /// <summary>Forme sans espaces ni accents : « NoFrost », « no-frost » et « no frost » se rejoignent.</summary>
    public static string Compact(string text)=>Fold(text).Replace(" ","");
}
