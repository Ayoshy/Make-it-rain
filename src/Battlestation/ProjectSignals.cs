using System.Diagnostics;
using System.IO;

namespace Battlestation;
internal sealed record ProjectSignal(string? Branch,int? Changes,int? Ahead=null,int? Behind=null,DateTimeOffset? LastCommit=null);
internal sealed class ProjectSignals : IDisposable
{
    readonly CancellationTokenSource stop=new();
    readonly Dictionary<string,ProjectSignal> cache=new(StringComparer.OrdinalIgnoreCase);
    bool busy;
    long nextPoll;
    internal long Revision {get;private set;}
    FileSystemWatcher? watcher;
    static readonly HashSet<string> ignored=new(StringComparer.OrdinalIgnoreCase){".git",".codex",".godot",".vs",".venv","venv","node_modules","bin","obj","build","dist","artifacts","backups","vendor","library","temp","logs","__pycache__",".next",".cache",".dart_tool",".gradle","target","out","cache",".yarn",".pnpm-store",".turbo"};
    internal void Watch(string root,bool active,Action<string> changed)
    {
        if(!active){if(watcher is not null)watcher.EnableRaisingEvents=false;return;}
        try
        {
            if(watcher is not null&&watcher.Path!=root){watcher.Dispose();watcher=null;}
            if(watcher is null&&Directory.Exists(root))
            {
                watcher=new FileSystemWatcher(root){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite};
                void Update(string path){var parts=Path.GetRelativePath(root,path).Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);if(parts.Length>0&&!parts.Any(ignored.Contains)&&!path.EndsWith(".log",StringComparison.OrdinalIgnoreCase))changed(parts.Length>1?parts[0]:"");}
                watcher.Changed+=(_,e)=>Update(e.FullPath);watcher.Created+=(_,e)=>Update(e.FullPath);watcher.Deleted+=(_,e)=>Update(e.FullPath);
                watcher.Renamed+=(_,e)=>{Update(e.OldFullPath);Update(e.FullPath);};watcher.Error+=(_,_)=>changed("");
            }
            if(watcher is not null)watcher.EnableRaisingEvents=true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException){watcher?.Dispose();watcher=null;}
    }
    internal ProjectSignal Read(string path)=>cache.GetValueOrDefault(path)??new(null,null);
    // Single write point: polling and the render tests describe a project the same way.
    internal void Remember(string path,ProjectSignal signal)
    {
        if(cache.GetValueOrDefault(path)==signal)return;
        cache[path]=signal;Revision++;
    }
    internal async void Poll(IEnumerable<string> paths)
    {
        if(busy||Environment.TickCount64<nextPoll||stop.IsCancellationRequested)return;
        busy=true;nextPoll=Environment.TickCount64+10000;
        try
        {
            foreach(string path in paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var signal=await Git(path,stop.Token);
                if(!stop.IsCancellationRequested)Remember(path,signal);
            }
        }
        catch(OperationCanceledException){}
        finally{busy=false;nextPoll=Environment.TickCount64+30000;}
    }
    internal static async Task<ProjectSignal> Git(string path,CancellationToken cancellation=default)
    {
        var empty=new ProjectSignal(null,null);
        if(!Directory.Exists(Path.Combine(path,".git"))&&!File.Exists(Path.Combine(path,".git")))return empty;
        var info=new ProcessStartInfo("git"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"--no-optional-locks","-c","core.fsmonitor=false","-C",path,"status","--porcelain=v2","--branch","--untracked-files=normal"})info.ArgumentList.Add(arg);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var process=new Process{StartInfo=info};
        try
        {
            process.Start();var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
            string? branch=null;int changes=0,ahead=0,behind=0;
            while(await process.StandardOutput.ReadLineAsync(timeout.Token) is {} line)
            {
                if(line.StartsWith("# branch.head "))branch=line[14..]=="(detached)"?"HEAD détachée":line[14..];
                else if(line.StartsWith("# branch.ab ")){var (left,right)=BranchAheadBehind(line[12..]);ahead=left;behind=right;}
                else if(line.StartsWith("1 ")||line.StartsWith("2 ")||line.StartsWith("u ")||line.StartsWith("? "))changes++;
            }
            await process.WaitForExitAsync(timeout.Token);await stderr;
            if(process.ExitCode!=0)return empty;
            return new(branch,changes,ahead,behind,await LastCommit(path,cancellation));
        }
        catch(Exception e) when(e is System.ComponentModel.Win32Exception or IOException or OperationCanceledException){return empty;}
        finally{try{if(process.Id>0&&!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}}
    }
    // "# branch.ab +2 -1": ahead and behind are always printed, even when they are zero.
    static (int Ahead,int Behind) BranchAheadBehind(string text)
    {
        var parts=text.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length!=2||!int.TryParse(parts[0],out int ahead)||!int.TryParse(parts[1],out int behind))return (0,0);
        return (Math.Max(0,ahead),Math.Max(0,behind));
    }
    // Bounded second call: the commit timestamp of the branch head, no history walk.
    static async Task<DateTimeOffset?> LastCommit(string path,CancellationToken cancellation)
    {
        var info=new ProcessStartInfo("git"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"--no-optional-locks","-C",path,"log","-1","--format=%ct"})info.ArgumentList.Add(arg);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var process=new Process{StartInfo=info};
        try
        {
            process.Start();var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
            string? line=await process.StandardOutput.ReadLineAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);await stderr;
            if(process.ExitCode!=0||line is null)return null;
            return long.TryParse(line.Trim(),System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out long seconds)&&seconds>0
                ?DateTimeOffset.FromUnixTimeSeconds(seconds):null;
        }
        catch(Exception e) when(e is System.ComponentModel.Win32Exception or IOException or OperationCanceledException){return null;}
        finally{try{if(process.Id>0&&!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}}
    }
    public void Dispose(){watcher?.Dispose();stop.Cancel();}
}
