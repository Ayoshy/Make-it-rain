using System.Text.RegularExpressions;

namespace Battlestation.Shopping;

/// <summary>Une référence d'appareil observée, pas le titre générique d'une catégorie.</summary>
public static class ProductIdentity
{
    public static string Reference(string title)
        =>Regex.Matches(title,@"\b[\p{L}\d]+(?:[-/][\p{L}\d]+)*\b")
            .Select(match=>match.Value).Where(IsReference).OrderByDescending(value=>value.Length).FirstOrDefault()??"";

    static bool IsReference(string value)
    {
        var compact=Key(value);
        return compact.Length>=2&&compact.Any(char.IsLetter)&&compact.Any(char.IsDigit)
            &&!Regex.IsMatch(compact,@"^\d+(?:l|litres?|cm|mm|m2?|kg|w|kw|pa|mah|h|min|gb|go|tb|to|hz|portes?|tiroirs?|en\d+)$",RegexOptions.IgnoreCase);
    }

    public static string Key(string reference)=>ShoppingText.Compact(reference);
    public static string Variant(string title)=>string.Join(' ',Regex.Matches(ShoppingText.Fold(title),
            @"\b(?:blanc|blanche|white|noir|noire|black|silver|argent|inox|gris|\d+\s*(?:go|gb|to|tb))\b")
        .Select(match=>match.Value switch{"white" or "blanche"=>"blanc","black" or "noire"=>"noir","silver"=>"argent",_=>match.Value})
        .Distinct().OrderBy(value=>value,StringComparer.Ordinal));
    public static bool SameModel(string first,string second)=>first.Length>0&&second.Length>0&&Key(first)==Key(second);

    public static bool IsListing(string url,IReadOnlyList<string> headings,string text)
    {
        var path=new Uri(url).AbsolutePath;
        if(Regex.IsMatch(path,@"/(?:c|category|categories|categorie|collections|catalogsearch|recherche|search)(?:/|$)",RegexOptions.IgnoreCase)
            &&!Regex.IsMatch(path,@"/(?:products?|produits?|ref|p)/",RegexOptions.IgnoreCase))return true;
        var main=headings.FirstOrDefault()??"";
        var title=ShoppingText.Fold(main);
        if(Regex.IsMatch(title,@"\b(?:guide|comparatif|comparaison|comment|meilleurs|notre selection)\b"))return true;
        bool grid=Regex.IsMatch(ShoppingText.Fold(text),@"\b\d+\s+(?:articles|produits|resultats)\b|trier par|tri par prix");
        return grid&&!headings.Any(heading=>Reference(heading).Length>0);
    }

    public static string BrandBeforeModel(string title,string model)
    {
        int index=title.IndexOf(model,StringComparison.OrdinalIgnoreCase);
        if(index<=0)return "";
        var word=Regex.Matches(title[..index],@"\p{L}+").LastOrDefault()?.Value??"";
        return ShoppingText.Fold(word) is "robot" or "aspirateur" or "refrigerateur" or "modele" or "portes"?"":word;
    }
}
