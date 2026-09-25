using System.Collections.Concurrent;
using System.IO;

namespace Battlestation;

internal sealed record DiskVolume(string Path,string Name,long Total,long Free);
internal sealed record DiskScanInfo(DateTimeOffset? CompletedAt,bool Scanning,bool Pending,bool Partial,long Entries,string Status);
internal sealed record DiskNode(string Path,string Name,long Bytes,bool Directory,bool Partial,DiskNode[] Children)
{
    internal DiskNode? Find(string path)
    {
        if(string.Equals(Path,path,StringComparison.OrdinalIgnoreCase))return this;
        foreach(var child in Children)
            if(child.Directory&&(string.Equals(child.Path,path,StringComparison.OrdinalIgnoreCase)||path.StartsWith(child.Path.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)))return child.Find(path);
        return null;
    }
}

// Navigation selects a retained index, never the lifetime of its watcher.
internal sealed class DiskIndex : IDisposable
{
    // Trace des scans complets, branchée par le bureau sur le journal de cycle de
    // vie ; les tests la laissent vide pour ne pas écrire dans le journal réel.
    internal static Action<string>? ScanTrace;
    readonly Dictionary<string,DiskVolumeIndex> indexes=new(StringComparer.OrdinalIgnoreCase);
    readonly object gate=new();
    readonly SemaphoreSlim scans=new(3,3);
    readonly Func<DiskVolume[]> readVolumes;
    readonly CancellationTokenSource stop=new();
    readonly ManualResetEventSlim visible=new(false);
    volatile DiskVolumeIndex? selected;
    volatile DiskVolume[] volumes=[];
    bool active,disposed;
    public DiskNode? Snapshot=>selected?.Snapshot;
    public DiskVolume[] Volumes=>volumes;
    public string Status=>selected?.Status??"";
    public long Scanned=>selected?.Scanned??0;
    public event Action? Changed;
    internal DiskIndex(Func<DiskVolume[]>? readVolumes=null){this.readVolumes=readVolumes??DiskVolumeIndex.ReadVolumes;_=Task.Run(PollVolumes);}
    internal void SetActive(bool value)
    {
        lock(gate)
        {
            if(disposed)return;active=value;if(value)visible.Set();else visible.Reset();
            foreach(var index in indexes.Values)index.SetActive(value);
        }
    }
    DiskVolumeIndex Ensure(string path)
    {
        if(indexes.TryGetValue(path,out var index))return index;
        index=new DiskVolumeIndex(path,scans);indexes.Add(path,index);
        index.Changed+=()=>Changed?.Invoke();index.SetActive(active);return index;
    }
    internal void Select(string? path)
    {
        lock(gate){if(disposed)return;selected=path is null?null:Ensure(path);}
        Changed?.Invoke();
    }
    internal void Rescan()=>selected?.Rescan();
    internal DiskScanInfo? Info(string path){lock(gate)return indexes.TryGetValue(path,out var index)?index.Info:null;}
    internal object InspectIndexes(){lock(gate)return indexes.Select(pair=>new{path=pair.Key,bytes=pair.Value.Snapshot?.Bytes,status=pair.Value.Status,fullScans=pair.Value.FullScans,metadataReads=pair.Value.MetadataReads,trigger=pair.Value.LastTrigger,scan=pair.Value.Info}).ToArray();}
    async Task PollVolumes()
    {
        try
        {
            while(true)
            {
                visible.Wait(stop.Token);
                try
                {
                    var found=readVolumes();
                    lock(gate){if(disposed)return;volumes=found;foreach(var volume in found)Ensure(volume.Path);}
                    Changed?.Invoke();
                }
                catch(IOException){}catch(UnauthorizedAccessException){}
                await Task.Delay(5000,stop.Token);
            }
        }
        catch(OperationCanceledException){}
        finally{visible.Dispose();stop.Dispose();}
    }
    public void Dispose()
    {
        lock(gate)
        {
            if(disposed)return;disposed=true;
            foreach(var index in indexes.Values)index.Dispose();
            try{stop.Cancel();}catch(ObjectDisposedException){}
        }
    }
}

// Metadata only. Each worker owns its directory table and publishes immutable trees.
internal sealed class DiskVolumeIndex : IDisposable
{
    readonly CancellationTokenSource stop=new();
    readonly ManualResetEventSlim visible=new(false);
    readonly ConcurrentDictionary<string,byte> dirty=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,DiskNode> directories=new(StringComparer.OrdinalIgnoreCase);
    FileSystemWatcher? watcher;
    readonly string rootPath;
    readonly SemaphoreSlim scans;
    // Un débordement du suivi ne relance pas aussitôt un scan complet : sur C:,
    // la moindre compilation le provoque et un scan coûte un cœur pendant minutes.
    // Les deltas (8 s) assurent la fraîcheur réelle ; le scan complet n'est qu'une
    // réconciliation des événements perdus. Décision Ayo 25/09 : rare plutôt que
    // 30 % de CPU — toutes les 6 h, et ↻ reste immédiat.
    const long DeltaMilliseconds=8000,AutomaticRescanMilliseconds=6*60*60*1000;
    volatile bool rescan=true,manualRescan;
    volatile string lastTrigger="";
    long lastFullScan,lastDelta;
    volatile bool scanning;
    volatile DiskNode? snapshot;
    volatile string status="";
    long scanned,metadataReads,completedTicks;
    int fullScans;
    public DiskNode? Snapshot=>snapshot;
    public string Status=>status;
    public long Scanned=>Interlocked.Read(ref scanned);
    public long MetadataReads=>Interlocked.Read(ref metadataReads);
    public int FullScans=>Volatile.Read(ref fullScans);
    public string LastTrigger=>lastTrigger;
    internal DiskScanInfo Info
    {
        get{long ticks=Interlocked.Read(ref completedTicks);return new(ticks==0?null:new DateTimeOffset(ticks,TimeSpan.Zero),scanning,rescan, snapshot?.Partial??false,Scanned,Status);}
    }
    public event Action? Changed;
    internal DiskVolumeIndex(string path,SemaphoreSlim scans){rootPath=path;this.scans=scans;_=Task.Run(Run);}
    internal void SetActive(bool value){if(value)visible.Set();else visible.Reset();}
    internal void Rescan(){manualRescan=true;rescan=true;}
    void Check(){stop.Token.ThrowIfCancellationRequested();visible.Wait(stop.Token);}
    void Notify()=>Changed?.Invoke();
    static bool Expected(Exception e)=>e is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    internal static DiskVolume[] ReadVolumes()=>DriveInfo.GetDrives().Where(d=>d.DriveType is DriveType.Fixed or DriveType.Removable).Select(d=>
    {
        try{return d.IsReady?new DiskVolume(d.RootDirectory.FullName,string.IsNullOrWhiteSpace(d.VolumeLabel)?"Disque local":d.VolumeLabel,d.TotalSize,d.AvailableFreeSpace):null;}
        catch(Exception e) when(Expected(e)){return null;}
    }).OfType<DiskVolume>().ToArray();
    void Mark(string path)
    {
        var parent=System.IO.Path.GetDirectoryName(path.TrimEnd('\\'));
        if(parent is not null)dirty[parent]=0;
        if(dirty.Count>4096){dirty.Clear();rescan=true;}
    }
    void Watch(string path)
    {
        watcher?.Dispose();watcher=null;
        try
        {
            watcher=new(path){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.Size|NotifyFilters.LastWrite,InternalBufferSize=65536};
            watcher.Created+=(_,e)=>Mark(e.FullPath);watcher.Deleted+=(_,e)=>Mark(e.FullPath);watcher.Changed+=(_,e)=>Mark(e.FullPath);
            watcher.Renamed+=(_,e)=>{Mark(e.OldFullPath);Mark(e.FullPath);};watcher.Error+=(_,_)=>rescan=true;watcher.EnableRaisingEvents=true;
        }
        catch(Exception e) when(Expected(e)){status="Suivi indisponible · actualisation manuelle";}
    }
    async Task Run()
    {
        try
        {
            Check();Watch(rootPath);
            while(true)
            {
                Check();
                string target=rootPath;
                long now=Environment.TickCount64;
                if(rescan&&(manualRescan||lastFullScan==0||now-lastFullScan>=AutomaticRescanMilliseconds))
                {
                    await scans.WaitAsync(stop.Token);
                    try
                    {
                        Check();
                        lastTrigger=manualRescan?"manuel":lastFullScan==0?"initial":"reconciliation";
                        DiskIndex.ScanTrace?.Invoke($"disk-scan {rootPath} {lastTrigger}");
                        rescan=manualRescan=false;scanning=true;scanned=0;status="Analyse en cours…";
                        Interlocked.Increment(ref fullScans);Notify();directories.Clear();
                        snapshot=Scan(target,true,true);Interlocked.Exchange(ref completedTicks,DateTimeOffset.UtcNow.Ticks);
                        status=watcher is null?"Suivi indisponible · actualisation manuelle":"";
                    }
                    finally{scanning=false;lastFullScan=Environment.TickCount64;scans.Release();}
                    Notify();
                }
                else if(!dirty.IsEmpty&&now-lastDelta>=DeltaMilliseconds)
                {
                    lastDelta=now;
                    var pending=dirty.Keys.ToArray();foreach(string path in pending)dirty.TryRemove(path,out _);
                    var changedDirectories=new HashSet<string>(pending,StringComparer.OrdinalIgnoreCase);
                    var refresh=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach(string changed in pending)
                    {
                        string? path=changed;
                        while(path is not null&&path.StartsWith(target,StringComparison.OrdinalIgnoreCase))
                        {refresh.Add(path);if(string.Equals(path,target,StringComparison.OrdinalIgnoreCase))break;path=System.IO.Path.GetDirectoryName(path.TrimEnd('\\'));}
                    }
                    // Seuls les dossiers signalés sont relus ; leurs ancêtres se recalculent en mémoire.
                    foreach(string path in refresh.OrderByDescending(p=>p.Length))
                    {
                        Check();if(!directories.ContainsKey(path))continue;
                        if(!changedDirectories.Contains(path))Rebuild(path);
                        else if(System.IO.Directory.Exists(path))Scan(path,false,false);
                    }
                    if(directories.TryGetValue(target,out var root)){snapshot=root;Notify();}
                }
                await Task.Delay(2500,stop.Token);
            }
        }
        catch(OperationCanceledException){}
        catch(Exception e) when(Expected(e)){status="Disque indisponible";Notify();}
        finally{watcher?.Dispose();visible.Dispose();stop.Dispose();}
    }
    DiskNode Scan(string path,bool recursive,bool progress)
    {
        Check();var children=new List<DiskNode>();var files=new PriorityQueue<DiskNode,long>();long other=0;bool partial=false;
        try
        {
            foreach(var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                Check();Interlocked.Increment(ref scanned);Interlocked.Increment(ref metadataReads);
                try
                {
                    if((entry.Attributes&FileAttributes.ReparsePoint)!=0){partial=true;continue;}
                    if(entry is DirectoryInfo)
                    {
                        var child=!recursive&&directories.TryGetValue(entry.FullName,out var known)?known:Scan(entry.FullName,recursive,false);
                        children.Add(child);
                        if(progress){snapshot=Make(path,children,true);Notify();}
                    }
                    else if(entry is FileInfo file)
                    {
                        var node=new DiskNode(file.FullName,file.Name,file.Length,false,false,[]);files.Enqueue(node,node.Bytes);
                        if(files.Count>96)other+=files.Dequeue().Bytes;
                    }
                }
                catch(Exception e) when(Expected(e)){partial=true;}
            }
        }
        catch(Exception e) when(Expected(e)){partial=true;}
        children.AddRange(files.UnorderedItems.Select(x=>x.Element));
        if(other>0)children.Add(new("","Autres fichiers",other,false,false,[]));
        if(!recursive&&directories.TryGetValue(path,out var previous))
        {
            var retained=children.Where(n=>n.Directory).Select(n=>n.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            void Forget(DiskNode node){directories.Remove(node.Path);foreach(var child in node.Children)if(child.Directory)Forget(child);}
            foreach(var gone in previous.Children.Where(n=>n.Directory&&!retained.Contains(n.Path)))Forget(gone);
        }
        return directories[path]=Make(path,children,partial);
    }
    void Rebuild(string path)
    {
        var node=directories[path];
        var children=node.Children.Select(child=>child.Directory&&directories.TryGetValue(child.Path,out var fresh)?fresh:child).ToList();
        directories[path]=Make(path,children,node.Partial&&!node.Children.Any(child=>child.Partial));
    }
    static DiskNode Make(string path,List<DiskNode> children,bool partial)=>new(path,new DirectoryInfo(path).Name,children.Sum(c=>c.Bytes),true,partial||children.Any(c=>c.Partial),children.OrderByDescending(c=>c.Bytes).ThenBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).ToArray());
    public void Dispose(){try{stop.Cancel();}catch(ObjectDisposedException){}}
}
