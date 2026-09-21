namespace Battlestation.Shopping;

public interface IVerdictEngine
{
    Verdict Evaluate(Product product,IReadOnlyList<PricePoint> history,IReadOnlyList<PromoEvent> upcoming,ShoppingSpec spec,ShoppingSettings settings);
}

/// <summary>
/// Confronte le prix courant au plancher observé et au prochain rendez-vous commercial.
/// Toutes les raisons citent une valeur mesurée : sans relevé, la réponse est « surveille ».
/// </summary>
public sealed class VerdictEngine : IVerdictEngine
{
    public Verdict Evaluate(Product product,IReadOnlyList<PricePoint> history,IReadOnlyList<PromoEvent> upcoming,ShoppingSpec spec,ShoppingSettings settings)
    {
        var reasons=new List<string>();
        var sources=new List<string>();
        if(product.Url.Length>0)sources.Add(product.Url);
        var points=history.Where(point=>point.Usable).OrderBy(point=>point.At).ToArray();
        decimal? current=product.Price??points.LastOrDefault()?.Price;
        if(current is not {} price)
        {
            reasons.Add($"Aucun prix relevé chez {product.Shop}");
            return new(VerdictDecision.Surveiller,.2,reasons,sources,null);
        }

        decimal floor=points.Length>0?points.Min(point=>point.Price!.Value):price;
        decimal ceiling=points.Length>0?points.Max(point=>point.Price!.Value):price;
        int days=points.Length>1?(int)Math.Round((points[^1].At-points[0].At).TotalDays):0;
        bool enough=points.Length>=settings.MinPricePoints;
        // Sans historique, la cible ne peut pas être un plancher : seul le budget parle.
        decimal? target=enough?Math.Round(floor*decimal.CreateChecked(1+(decimal)(settings.BuyMargin-1)),2):spec.MaxPrice;
        if(target is {} wanted&&spec.MaxPrice is {} budget&&wanted>budget)target=budget;

        bool overBudget=spec.MaxPrice is {} max&&price>max;
        var promo=upcoming.FirstOrDefault();
        if(promo is not null)sources.Add($"calendrier promo · {promo.Label} ({promo.DateText})");

        reasons.Add(enough
            ?$"{ShoppingText.Money(price)} chez {product.Shop} · plancher {settings.HistoryDays} j {ShoppingText.Money(floor)} · haut {ShoppingText.Money(ceiling)}"
            :$"{ShoppingText.Money(price)} chez {product.Shop} · historique court ({points.Length} relevé{(points.Length>1?"s":"")}{(days>0?$" sur {days} j":"")})");
        if(spec.MaxPrice is {} limit)reasons.Add(overBudget
            ?$"Au-dessus du budget : {ShoppingText.Money(price)} > {ShoppingText.Money(limit)}"
            :$"Dans le budget : {ShoppingText.Money(price)} ≤ {ShoppingText.Money(limit)}");
        if(target is {} goal&&price<=goal)
        {
            string position=price<=floor
                ?$"Au plancher observé ({ShoppingText.Money(floor)})"
                :$"{(floor-price)/floor*100:N1} % sous le plancher";
            reasons.Add($"{position} · cible {ShoppingText.Money(goal)}");
        }
        if(promo is not null)
        {
            int inDays=promo.Start.DayNumber-DateOnly.FromDateTime(DateTime.Today).DayNumber;
            reasons.Add($"{promo.Label} dans {inDays} j ({promo.DateText}){(promo.DiscountHint>0?$" · remise annoncée ~{promo.DiscountHint*100:N0} %":"")}");
        }

        double confidence=Math.Min(.95,.35+.06*points.Length+(days>=30?.2:days>=14?.1:0));
        if(!enough)confidence=Math.Min(confidence,.55);

        // Acheter exige un historique suffisant : un prix seul n'est jamais une bonne affaire démontrée.
        var decision=overBudget?VerdictDecision.Surveiller
            :!enough?promo is not null?VerdictDecision.Attendre:VerdictDecision.Surveiller
            :price<=target&&(promo is null||price<=floor)?VerdictDecision.Acheter
            :promo is not null&&price>floor?VerdictDecision.Attendre
            :VerdictDecision.Surveiller;
        if(decision==VerdictDecision.Attendre&&!enough)reasons.Add("Historique trop court pour dire si le prix du jour est bon");
        if(decision==VerdictDecision.Surveiller&&promo is null&&enough&&price>target)reasons.Add($"Attendu sous {ShoppingText.Money(target)} pour acheter maintenant");
        return new(decision,confidence,reasons,sources,target);
    }
}
