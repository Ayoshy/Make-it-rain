using Microsoft.Data.Sqlite;

namespace Battlestation.Shopping;

/// <summary>
/// Historique local en SQLite : produits, relevés datés, veilles et calendrier.
/// Rien n'est envoyé ailleurs ; un relevé manquant reste absent plutôt que remplacé par zéro.
/// </summary>
public sealed class ShoppingStore : IDisposable
{
    readonly SqliteConnection database;
    readonly Lock gate=new();

    public string Path{get;}

    public ShoppingStore(string path)
    {
        Path=path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        database=Open(path);
    }

    const string Schema=
        """
        CREATE TABLE IF NOT EXISTS product(id TEXT PRIMARY KEY,source TEXT,url TEXT,title TEXT,brand TEXT,model TEXT,image TEXT,shop TEXT,updated INTEGER);
        CREATE TABLE IF NOT EXISTS price_point(product_id TEXT NOT NULL,at INTEGER NOT NULL,price REAL,currency TEXT,in_stock INTEGER,source TEXT,PRIMARY KEY(product_id,at));
        CREATE TABLE IF NOT EXISTS watch_item(id TEXT PRIMARY KEY,request TEXT,product_id TEXT,target REAL,added INTEGER,last_check INTEGER,next_check INTEGER,failures INTEGER,alerted INTEGER);
        CREATE TABLE IF NOT EXISTS promo_event(id TEXT PRIMARY KEY,shop TEXT,label TEXT,starts_on TEXT,ends_on TEXT,discount REAL);
        """;

    /// <summary>
    /// Un fichier illisible est mis de côté plutôt que de faire échouer le bureau,
    /// comme `layout.json` le fait pour une disposition corrompue.
    /// </summary>
    static SqliteConnection Open(string path)
    {
        try{return Connect(path);}
        catch(SqliteException e) when(e.SqliteErrorCode is 11 or 26) // SQLITE_CORRUPT / SQLITE_NOTADB
        {
            Quarantine(path);
            return Connect(path);
        }
    }

    static SqliteConnection Connect(string path)
    {
        // Sans regroupement de connexions : la fermeture libère vraiment le fichier.
        var connection=new SqliteConnection($"Data Source={path};Pooling=False");
        try
        {
            connection.Open();
            using var command=connection.CreateCommand();
            command.CommandText="PRAGMA journal_mode=WAL;PRAGMA synchronous=NORMAL;"+Schema;
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            // Une tentative ratée ne doit pas garder le fichier ouvert avant sa mise de côté.
            connection.Dispose();
            throw;
        }
    }

    static void Quarantine(string path)
    {
        SqliteConnection.ClearAllPools();
        var stamp=DateTime.Now.ToString("yyyyMMddHHmmss");
        foreach(var suffix in new[]{"","-wal","-shm"})
            if(File.Exists(path+suffix))File.Move(path+suffix,$"{path}.invalid-{stamp}{suffix}",true);
    }

    public void SaveProduct(Product product)=>Write(
        "INSERT INTO product(id,source,url,title,brand,model,image,shop,updated) VALUES($id,$source,$url,$title,$brand,$model,$image,$shop,$updated) "+
        "ON CONFLICT(id) DO UPDATE SET url=$url,title=$title,brand=$brand,model=$model,image=$image,shop=$shop,updated=$updated",
        ("$id",product.Id),("$source",product.Source),("$url",product.Url),("$title",product.Title),
        ("$brand",product.Brand),("$model",product.Model),("$image",product.Image),("$shop",product.Shop),
        ("$updated",Now()));

    public Product? FindProduct(string id)=>Read(
        "SELECT id,source,url,title,brand,model,image,shop FROM product WHERE id=$id",
        reader=>new Product(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7)),
        ("$id",id));

    /// <summary>Enregistre un relevé. Une même valeur répétée dans la journée ne crée pas un second point.</summary>
    public bool RecordPrice(PricePoint point)
    {
        lock(gate)
        {
            var last=LastPriceLocked(point.ProductId);
            // Un relevé par jour suffit à décrire la tendance : le prix identique
            // n'est réécrit qu'après une journée complète.
            if(last is not null&&last.Price==point.Price&&last.InStock==point.InStock&&last.Currency==point.Currency
                &&point.At-last.At<TimeSpan.FromHours(20))return false;
            Run(
                "INSERT INTO price_point(product_id,at,price,currency,in_stock,source) VALUES($product,$at,$price,$currency,$stock,$source) "+
                "ON CONFLICT(product_id,at) DO UPDATE SET price=$price,in_stock=$stock",
                ("$product",point.ProductId),("$at",point.At.ToUnixTimeSeconds()),("$price",point.Price is {} price?(double)price:DBNull.Value),
                ("$currency",point.Currency),("$stock",point.InStock?1:0),("$source",point.Source));
            return true;
        }
    }

    public PricePoint? LastPrice(string productId)
    {
        lock(gate)return LastPriceLocked(productId);
    }

    PricePoint? LastPriceLocked(string productId)
    {
        using var command=database.CreateCommand();
        command.CommandText="SELECT at,price,currency,in_stock,source FROM price_point WHERE product_id=$product ORDER BY at DESC LIMIT 1";
        command.Parameters.AddWithValue("$product",productId);
        using var reader=command.ExecuteReader();
        return reader.Read()?Point(reader,productId):null;
    }

    public IReadOnlyList<PricePoint> History(string productId,int days)=>ReadMany(
        "SELECT at,price,currency,in_stock,source FROM price_point WHERE product_id=$product AND at>=$since ORDER BY at",
        reader=>Point(reader,productId),
        ("$product",productId),("$since",DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds()));

    public void SaveWatch(WatchItem watch)=>Write(
        "INSERT INTO watch_item(id,request,product_id,target,added,last_check,next_check,failures,alerted) "+
        "VALUES($id,$request,$product,$target,$added,$last,$next,$failures,$alerted) "+
        "ON CONFLICT(id) DO UPDATE SET target=$target,last_check=$last,next_check=$next,failures=$failures,alerted=$alerted",
        ("$id",watch.Id),("$request",watch.Request),("$product",watch.ProductId),
        ("$target",watch.TargetPrice is {} target?(double)target:DBNull.Value),
        ("$added",watch.AddedAt.ToUnixTimeSeconds()),("$last",watch.LastCheck.ToUnixTimeSeconds()),("$next",watch.NextCheck.ToUnixTimeSeconds()),
        ("$failures",watch.Failures),("$alerted",watch.Alerted?1:0));

    public void RemoveWatch(string id)=>Write("DELETE FROM watch_item WHERE id=$id",("$id",id));

    /// <summary>Les veilles avec leur dernier prix connu, du plus récent au plus ancien.</summary>
    public IReadOnlyList<(WatchItem Watch,Product Product)> Watches()=>ReadMany(
        "SELECT w.id,w.request,w.product_id,w.target,w.added,w.last_check,w.next_check,w.failures,w.alerted,"+
        "p.id,p.source,p.url,p.title,p.brand,p.model,p.image,p.shop,"+
        "(SELECT price FROM price_point WHERE product_id=w.product_id ORDER BY at DESC LIMIT 1) "+
        "FROM watch_item w LEFT JOIN product p ON p.id=w.product_id ORDER BY w.added DESC",
        reader=>
        {
            var product=new Product(
                Text(reader,9),Text(reader,10),Text(reader,11),Text(reader,12),Text(reader,13),Text(reader,14),Text(reader,15),Text(reader,16),
                reader.IsDBNull(17)?null:(decimal)reader.GetDouble(17));
            var watch=new WatchItem(
                reader.GetString(0),reader.GetString(1),reader.GetString(2),
                reader.IsDBNull(3)?null:(decimal)reader.GetDouble(3),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)),DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),(int)reader.GetInt64(7),reader.GetInt64(8)!=0,
                product.Price,product.Title,product.Shop);
            return (watch,product);
        });

    public void SavePromoEvents(IEnumerable<PromoEvent> events)
    {
        foreach(var item in events)Write(
            "INSERT INTO promo_event(id,shop,label,starts_on,ends_on,discount) VALUES($id,$shop,$label,$start,$end,$discount) "+
            "ON CONFLICT(id) DO UPDATE SET shop=$shop,label=$label,starts_on=$start,ends_on=$end,discount=$discount",
            ("$id",item.Id),("$shop",item.Shop),("$label",item.Label),("$start",item.Start.ToString("yyyy-MM-dd")),
            ("$end",item.End.ToString("yyyy-MM-dd")),("$discount",item.DiscountHint));
    }

    static string Text(SqliteDataReader reader,int index)=>reader.IsDBNull(index)?"":reader.GetString(index);

    static PricePoint Point(SqliteDataReader reader,string productId)=>new(
        productId,
        DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
        reader.IsDBNull(1)?null:(decimal)reader.GetDouble(1),
        reader.IsDBNull(2)?"EUR":reader.GetString(2),
        reader.GetInt64(3)!=0,
        reader.IsDBNull(4)?"":reader.GetString(4));

    static long Now()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    void Write(string sql,params (string Name,object? Value)[] arguments)
    {
        lock(gate)Run(sql,arguments);
    }

    T? Read<T>(string sql,Func<SqliteDataReader,T> map,params (string Name,object? Value)[] arguments)
    {
        lock(gate)return ReadMany(sql,map,arguments).FirstOrDefault();
    }

    IReadOnlyList<T> ReadMany<T>(string sql,Func<SqliteDataReader,T> map,params (string Name,object? Value)[] arguments)
    {
        lock(gate)
        {
            using var command=Command(sql,arguments);
            using var reader=command.ExecuteReader();
            var results=new List<T>();
            while(reader.Read())results.Add(map(reader));
            return results;
        }
    }

    void Run(string sql,params (string Name,object? Value)[] arguments)
    {
        using var command=Command(sql,arguments);
        command.ExecuteNonQuery();
    }

    SqliteCommand Command(string sql,(string Name,object? Value)[] arguments)
    {
        var command=database.CreateCommand();
        command.CommandText=sql;
        foreach(var (name,value) in arguments)command.Parameters.AddWithValue(name,value??DBNull.Value);
        return command;
    }

    void Execute(string sql)
    {
        using var command=database.CreateCommand();
        command.CommandText=sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        lock(gate)database.Dispose();
    }
}
