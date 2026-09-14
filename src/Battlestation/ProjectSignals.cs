using System.Diagnostics;
using System.IO;

namespace Battlestation;
internal sealed record ProjectSignal(string? Branch,int? Changes);
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
    internal async void Poll(IEnumerable<string> paths)
    {
        if(busy||Environment.TickCount64<nextPoll||stop.IsCancellationRequested)return;
        busy=true;nextPoll=Environment.TickCount64+10000;
        try
        {
            foreach(string path in paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var signal=await Git(path,stop.Token);
                if(!stop.IsCancellationRequested&&cache.GetValueOrDefault(path)!=signal){cache[path]=signal;Revision++;}
            }
        }
        catch(OperationCanceledException){}
        finally{busy=false;nextPoll=Environment.TickCount64+30000;}
    }
    internal static async Task<ProjectSignal> Git(string path,CancellationToken cancellation=default)
    {
        if(!Directory.Exists(Path.Combine(path,".git"))&&!File.Exists(Path.Combine(path,".git")))return new(null,null);
        var info=new ProcessStartInfo("git"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"--no-optional-locks","-c","core.fsmonitor=false","-C",path,"status","--porcelain=v2","--branch","--untracked-files=normal"})info.ArgumentList.Add(arg);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var process=new Process{StartInfo=info};
        try
        {
            process.Start();var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
            string? branch=null;int changes=0;
            while(await process.StandardOutput.ReadLineAsync(timeout.Token) is {} line)
            {
                if(line.StartsWith("# branch.head "))branch=line[14..]=="(detached)"?"HEAD détachée":line[14..];
                else if(line.StartsWith("1 ")||line.StartsWith("2 ")||line.StartsWith("u ")||line.StartsWith("? "))changes++;
            }
            await process.WaitForExitAsync(timeout.Token);await stderr;
            return process.ExitCode==0?new(branch,changes):new(null,null);
        }
        catch(Exception e) when(e is System.ComponentModel.Win32Exception or IOException or OperationCanceledException){return new(null,null);}
        finally{try{if(process.Id>0&&!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}}
    }
    public void Dispose(){watcher?.Dispose();stop.Cancel();}
}
