using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Battlestation;
internal sealed record ConsoleTitleInfo(int Pid,string? Title,bool Codex,int Error=0);
internal static class TerminalMetadataWorker
{
    [DllImport("kernel32.dll")] static extern nint GetStdHandle(int id);
    [DllImport("kernel32.dll")] static extern uint GetFileType(nint handle);
    [DllImport("kernel32.dll")] static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool DuplicateHandle(nint source,nint handle,nint target,out SafeFileHandle copy,uint access,bool inherit,uint options);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] static extern bool FreeConsole();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetConsoleTitle(StringBuilder title,uint size);
    [DllImport("kernel32.dll")] static extern uint GetConsoleProcessList([Out] uint[] processes,uint count);
    // This process never opens CONIN$/CONOUT$, reads a screen buffer, changes a
    // title, emits input or generates a console control event. Its only output
    // goes through duplicated private pipe handles captured BEFORE attachment.
    public static void Run()
    {
        var input=GetStdHandle(-10);var output=GetStdHandle(-11);if(GetFileType(input)!=3||GetFileType(output)!=3)return;
        if(!DuplicateHandle(GetCurrentProcess(),input,GetCurrentProcess(),out var readHandle,0,false,2))return;
        using(readHandle)
        {
            if(!DuplicateHandle(GetCurrentProcess(),output,GetCurrentProcess(),out var writeHandle,0,false,2))return;
            using var inputStream=new FileStream(readHandle,FileAccess.Read);using var outputStream=new FileStream(writeHandle,FileAccess.Write);
            using var reader=new StreamReader(inputStream,new UTF8Encoding(false));using var writer=new StreamWriter(outputStream,new UTF8Encoding(false)){AutoFlush=true};
            FreeConsole();
            try
            {
                string? line;
                while((line=reader.ReadLine()) is not null)
                {
                    if(line.Length>1024)break;
                    var ids=line.Split(',').Take(32).Select(s=>int.TryParse(s,out var id)?id:0).Where(id=>id>0).Distinct().ToArray();
                    writer.WriteLine(JsonSerializer.Serialize(ids.Select(Read).ToArray()));
                }
            }
            catch(IOException){}
            finally{FreeConsole();}
        }
    }
    static ConsoleTitleInfo Read(int pid)
    {
        try
        {
            using(var process=Process.GetProcessById(pid))
                if(process.ProcessName is not ("powershell" or "pwsh" or "codex"))return new(pid,null,false,87);
            if(!AttachConsole((uint)pid))return new(pid,null,false,Marshal.GetLastWin32Error());
            try
            {
                var title=new StringBuilder(512);GetConsoleTitle(title,512);bool codex=false;
                var processes=new uint[128];uint count=GetConsoleProcessList(processes,(uint)processes.Length);
                foreach(uint id in processes.Take((int)Math.Min(count,(uint)processes.Length)))
                {
                    try{using var process=Process.GetProcessById((int)id);if(process.ProcessName.Equals("codex",StringComparison.OrdinalIgnoreCase)){codex=true;break;}}
                    catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
                }
                return new(pid,TerminalTabPreferences.Clean(title.ToString()),codex);
            }
            finally{FreeConsole();}
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){return new(pid,null,false,6);}
    }
}
internal sealed class TerminalMetadataClient : IDisposable
{
    readonly string executable;
    Process? helper;
    bool disposed;
    public int? Pid=>helper is {HasExited:false}?helper.Id:null;
    public TerminalMetadataClient(string program)=>executable=program;
    public async Task<ConsoleTitleInfo[]> Read(IEnumerable<int> ids)
    {
        var values=ids.Where(id=>id>0).Distinct().Take(32).ToArray();if(disposed||values.Length==0)return [];
        try
        {
            if(helper is null||helper.HasExited)
            {
                helper?.Dispose();var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                info.ArgumentList.Add("--terminal-metadata");helper=Process.Start(info)??throw new IOException();
            }
            await helper.StandardInput.WriteLineAsync(string.Join(',',values));await helper.StandardInput.FlushAsync();
            var line=await helper.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            if(line is null||line.Length>32768)throw new IOException();
            return JsonSerializer.Deserialize<ConsoleTitleInfo[]>(line)??[];
        }
        catch(Exception e) when(e is IOException or TimeoutException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception){Stop();return [];}
    }
    void Stop()
    {
        var owned=helper;helper=null;if(owned is null)return;
        try{owned.StandardInput.Close();}catch(InvalidOperationException){}
        try{if(!owned.WaitForExit(200))owned.Kill();}catch(InvalidOperationException){}
        owned.Dispose();
    }
    public void Dispose(){disposed=true;Stop();}
}
