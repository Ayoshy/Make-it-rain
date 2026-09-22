using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>Vérifie l'objet vendu avant de comparer les prix des aspirateurs.</summary>
public static partial class ShoppingRelevance
{
    /// <summary>Partagé par les listes de boutiques et le moteur, avant toute limite de résultats.</summary>
    public static bool Matches(string title,ShoppingSpec spec,string details="")
    {
        var offered=ShoppingText.Fold(title);
        if(Used().IsMatch(offered))return false;
        var category=ShoppingText.Fold(spec.Category);
        if(!Vacuum().IsMatch(category))return true;

        var request=ShoppingText.Fold(spec.Request);
        bool wantsRobot=Robot().IsMatch(category)||Robot().IsMatch(request);
        var wantedPart=LeadingPart(request);
        var offeredPart=LeadingPart(offered);
        if(wantedPart is not null)
        {
            // Une demande de brosse reste une demande de brosse, même si le modèle
            // a conservé « aspirateur » comme catégorie de compatibilité.
            return offeredPart is not null&&PartName(wantedPart)==PartName(offeredPart);
        }
        if(offeredPart is not null)return false;
        // Une fiche fabricant peut titrer seulement « Roborock Qrevo… ».
        // Son texte lu confirme alors le type, sans réhabiliter une pièce dont
        // le titre annonce déjà brosse/filtre/batterie.
        var described=offered+" "+ShoppingText.Fold(details);
        if(!Vacuum().IsMatch(described))return false;
        return !wantsRobot||Robot().IsMatch(described);
    }

    static string? LeadingPart(string text)
    {
        var device=Device().Match(text);
        var part=Part().Match(text);
        // « aspirateur avec brosse » vend l'appareil ; « brosse pour aspirateur »
        // vend la pièce. La marque peut précéder l'un ou l'autre.
        return part.Success&&(!device.Success||part.Index<device.Index)?part.Value:null;
    }

    static string PartName(string part)=>part.TrimEnd('s');

    [GeneratedRegex(@"\baspirateurs?\b")]
    private static partial Regex Vacuum();

    [GeneratedRegex(@"\brobots?\b")]
    private static partial Regex Robot();

    [GeneratedRegex(@"\b(?:aspirateurs?|robots?)\b")]
    private static partial Regex Device();

    [GeneratedRegex(@"\b(?:accessoires?|pieces?|kits?|brosses?|filtres?|roues?|roulettes?|couvercles?|sacs?|batteries?|stations?|chargeurs?|serpillieres?|lingettes?|supports?|plaques?|reservoirs?|moteurs?)\b")]
    private static partial Regex Part();

    [GeneratedRegex(@"\b(?:reconditionne(?:e|s|es)?|occasion)\b")]
    private static partial Regex Used();
}
