using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Battlestation;

internal sealed record BuildCleanup(long ReclaimableBytes,long FreedBytes,int Removed,string[] Protected,string[] Errors);

internal sealed class AtelierStore(string root,string data)
{
    internal string ReadNextLaunch()
    {
        string path=Path.Combine(root,"build","current.txt");
        return File.Exists(path)?File.ReadAllText(path).Trim():"";
    }
    internal bool IsKeptBuild(string loaded,string next)=>next.Length>0&&string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(loaded)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root,next))),StringComparison.OrdinalIgnoreCase);
    internal Task KeepAsync(string loaded)=>Task.Run(()=>
    {
        string directory=Path.TrimEndingDirectorySeparator(Path.GetFullPath(loaded));
        string builds=Path.GetFullPath(Path.Combine(root,"build"));
        if(!string.Equals(Path.GetDirectoryName(directory),builds,StringComparison.OrdinalIgnoreCase)
            ||!File.Exists(Path.Combine(directory,"Battlestation.exe")))
            throw new InvalidDataException("La version ouverte n’est pas un build de ce projet.");
        string path=Path.Combine(builds,"current.txt"),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,"build/"+Path.GetFileName(directory)+Environment.NewLine,new UTF8Encoding(false));
            File.Move(temporary,path,true);
        }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    });
    internal async Task<BuildCleanup> CleanupAsync(string loaded,bool apply)
    {
        var start=new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
        foreach(string argument in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts","Clean-BattlestationBuilds.ps1"),"-Root",root,"-Data",data,"-LoadedBuild",loaded})start.ArgumentList.Add(argument);
        if(apply)start.ArgumentList.Add("-Apply");
        using var process=Process.Start(start)??throw new IOException("Nettoyage indisponible.");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        string json=await output.ConfigureAwait(false),details=await error.ConfigureAwait(false);
        if(process.ExitCode!=0)throw new IOException(details.Trim());
        return JsonSerializer.Deserialize<BuildCleanup>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Bilan du nettoyage indisponible.");
    }
}
