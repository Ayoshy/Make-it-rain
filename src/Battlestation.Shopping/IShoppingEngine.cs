namespace Battlestation.Shopping;

/// <summary>
/// Ce que le dock utilise du moteur : chercher, surveiller, connaître l'état.
/// Le dock ne connaît ni SQLite, ni HTTP, ni modèle de langage.
/// </summary>
public interface IShoppingEngine : IDisposable
{
    string Activity=>"";
    IReadOnlyList<ShoppingProgress> Progress=>[];
    DateTimeOffset? SearchStartedAt=>null;
    bool Busy{get;}
    IReadOnlyList<SourceReport> Sources{get;}
    IReadOnlyList<WatchedItem> Watchlist{get;}
    ShoppingSettings Settings{get;}
    event Action? Changed;
    event Action<ShoppingAlert>? Alert;
    Task<SearchOutcome> SearchAsync(string request,CancellationToken cancellation);
    void Watch(Product product,decimal? targetPrice,string request);
    void Unwatch(string watchId);
    bool IsWatched(string productId);
    Task CheckWatchesAsync(CancellationToken cancellation,bool force=false);
    void Apply(ShoppingSettings settings);
    void Start();
    void Stop();
}
