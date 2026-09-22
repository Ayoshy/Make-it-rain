using System.Text.Json;

namespace Battlestation.Shopping;

/// <summary>
/// Calendrier promotionnel français. Les dates légales (soldes, Black Friday) sont
/// calculées, jamais saisies : deux exécutions la même année donnent le même résultat.
/// Un fichier JSON par enseigne peut compléter ou corriger les repères commerciaux.
/// </summary>
public sealed class PromoCalendar
{
    readonly List<PromoEvent> custom=[];
    readonly List<PromoEvent> overrides=[];
    public IReadOnlyList<PromoEvent> Custom=>custom;

    public static PromoCalendar Load(string? path)
    {
        var calendar=new PromoCalendar();
        if(path is null||!File.Exists(path))return calendar;
        try
        {
            var events=JsonSerializer.Deserialize<List<PromoEvent>>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            if(events is not null)calendar.Merge(events);
        }
        catch(JsonException){/* Un JSON illisible n'efface pas le calendrier calculé. */}
        return calendar;
    }

    /// <summary>Un événement utilisateur remplace celui de même identifiant au lieu de s'y ajouter.</summary>
    public void Merge(IEnumerable<PromoEvent> events)
    {
        foreach(var item in events.Where(item=>item.Start<=item.End))
        {
            var existing=custom.FindIndex(stored=>stored.Id==item.Id);
            if(existing>=0)custom[existing]=item;else custom.Add(item);
            overrides.Add(item);
        }
    }

    /// <summary>Tous les repères connus entre deux dates, triés par début.</summary>
    public IReadOnlyList<PromoEvent> Between(DateOnly from,DateOnly to)
    {
        var events=new List<PromoEvent>();
        for(int year=from.Year-1;year<=to.Year+1;year++)events.AddRange(Computed(year));
        events.AddRange(custom);
        return events
            .Where(item=>item.Start<=to&&item.End>=from)
            .GroupBy(item=>item.Id)
            .Select(group=>overrides.LastOrDefault(item=>item.Id==group.Key)??group.First())
            .OrderBy(item=>item.Start).ThenBy(item=>item.Label,StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Les événements qui commencent dans la fenêtre demandée, sans ceux déjà en cours.</summary>
    public IReadOnlyList<PromoEvent> Upcoming(DateOnly today,int days)=>days<=0?[]
        :Between(today,today.AddDays(days)).Where(item=>item.Start>today).ToArray();

    /// <summary>Le prochain changement de prix annoncé pour une enseigne donnée, "" pour toutes.</summary>
    public PromoEvent? Next(DateOnly today,int days,string shop="")
    {
        var upcoming=Upcoming(today,days);
        if(!string.IsNullOrWhiteSpace(shop))
        {
            var named=upcoming.Where(item=>item.Shop.Length>0&&ShoppingText.Fold(item.Shop)==ShoppingText.Fold(shop)).ToArray();
            if(named.Length>0)return named[0];
        }
        var generic=upcoming.Where(item=>item.Shop.Length==0).ToArray();
        return generic.Length>0?generic[0]:string.IsNullOrWhiteSpace(shop)?upcoming.FirstOrDefault():null;
    }

    /// <summary>Repères calendaires fixes. Les opérations décidées par les enseignes exigent une date fournie.</summary>
    public static IReadOnlyList<PromoEvent> Computed(int year)
    {
        var winter=WinterSalesStart(year);
        var summer=SummerSalesStart(year);
        var black=BlackFriday(year);
        return
        [
            new($"soldes-hiver-{year}","","Soldes d'hiver",winter,winter.AddDays(27)),
            new($"soldes-ete-{year}","","Soldes d'été",summer,summer.AddDays(27)),
            new($"black-friday-{year}","","Black Friday",black,black),
            new($"cyber-monday-{year}","","Cyber Monday",black.AddDays(3),black.AddDays(3))
        ];
    }

    /// <summary>Deuxième mercredi de janvier, à 8 h, pour quatre semaines.</summary>
    public static DateOnly WinterSalesStart(int year)=>NthWeekday(year,1,DayOfWeek.Wednesday,2);

    /// <summary>Dernier mercredi de juin ; s'il tombe après le 28, l'avant-dernier mercredi.</summary>
    public static DateOnly SummerSalesStart(int year)
    {
        var last=LastWeekday(year,6,DayOfWeek.Wednesday);
        return last.Day>28?last.AddDays(-7):last;
    }

    /// <summary>Vendredi qui suit le quatrième jeudi de novembre.</summary>
    public static DateOnly BlackFriday(int year)=>NthWeekday(year,11,DayOfWeek.Thursday,4).AddDays(1);

    static DateOnly NthWeekday(int year,int month,DayOfWeek weekday,int occurrence)
    {
        var first=new DateOnly(year,month,1);
        int offset=((int)weekday-(int)first.DayOfWeek+7)%7;
        return first.AddDays(offset+(occurrence-1)*7);
    }

    static DateOnly LastWeekday(int year,int month,DayOfWeek weekday)
    {
        var last=new DateOnly(year,month,DateTime.DaysInMonth(year,month));
        int offset=((int)last.DayOfWeek-(int)weekday+7)%7;
        return last.AddDays(-offset);
    }
}
