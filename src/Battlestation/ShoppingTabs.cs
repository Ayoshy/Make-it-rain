namespace Battlestation;

internal sealed class ShoppingTab(int number,ShoppingRadar radar)
{
    public Guid Id {get;}=Guid.NewGuid();
    public ShoppingRadar Radar {get;}=radar;
    public string Title=>Radar.Query.Length==0?$"Recherche {number}":Radar.Query;
    public int Page {get;set;}
    public bool Journal {get;set;}
    public bool WasBusy {get;set;}
    public DateTimeOffset? CompletedAt {get;set;}
}

/// <summary>Recherches indépendantes ; le premier moteur reste le seul responsable de la veille.</summary>
internal sealed class ShoppingTabs : IDisposable
{
    readonly ShoppingRadar shared;
    readonly Func<ShoppingRadar> create;
    readonly List<ShoppingTab> items=[];
    int sequence;
    public IReadOnlyList<ShoppingTab> Items=>items;
    public ShoppingTab Active {get;private set;}
    public bool AnyBusy=>shared.Busy||items.Any(tab=>tab.Radar.Busy);
    public event Action? Changed;

    public ShoppingTabs(ShoppingRadar shared,Func<ShoppingRadar> create)
    {
        this.shared=shared;this.create=create;
        Active=new(++sequence,shared);items.Add(Active);shared.Changed+=OnChanged;
    }
    void OnChanged()
    {
        foreach(var tab in items)
        {
            bool busy=tab.Radar.Busy;
            if(busy)tab.CompletedAt=null;
            else if(tab.WasBusy&&!tab.Radar.Cancelled&&tab.Radar.Error.Length==0)tab.CompletedAt=DateTimeOffset.UtcNow;
            tab.WasBusy=busy;
        }
        Changed?.Invoke();
    }
    public ShoppingTab Add()
    {
        var tab=new ShoppingTab(++sequence,create());
        tab.Radar.Changed+=OnChanged;items.Add(tab);Active=tab;OnChanged();return tab;
    }
    public void Select(Guid id)
    {
        if(items.FirstOrDefault(tab=>tab.Id==id) is not {} tab||tab==Active)return;
        Active=tab;tab.Radar.Reload();OnChanged();
    }
    public void Close(Guid id)
    {
        if(items.FirstOrDefault(tab=>tab.Id==id) is not {} tab)return;
        int index=items.IndexOf(tab);items.Remove(tab);tab.Radar.Cancel();
        // La veille du moteur partagé continue même si son onglet est fermé.
        if(tab.Radar!=shared){tab.Radar.Changed-=OnChanged;tab.Radar.Dispose();}
        if(items.Count==0){Add();return;}
        if(Active==tab)Active=items[Math.Min(index,items.Count-1)];
        OnChanged();
    }
    public void Dispose()
    {
        shared.Changed-=OnChanged;
        foreach(var tab in items.Where(tab=>tab.Radar!=shared))
        {tab.Radar.Changed-=OnChanged;tab.Radar.Dispose();}
        shared.Dispose();items.Clear();
    }
}
