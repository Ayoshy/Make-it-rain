using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Battlestation;
internal sealed record ConsoleTitleInfo(int Pid,string? Title,bool Codex,int Error=0,bool Kilo=false,string? Project=null,bool? DeepSeek=null);

// Ligne de commande du CLI Codex d'un onglet : elle porte la variante
// (`model_provider="deepseek"`) et, avec -C, le projet suivi. Lecture seule du
// bloc de parametres du processus ; aucun handle de console, aucune ecriture.
internal static class CodexCommand
{
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(nint process,int infoClass,byte[] info,int length,out int returned);
    [DllImport("kernel32.dll",SetLastError=true)] static extern nint OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(nint process,nint address,byte[] buffer,nint size,out nint read);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    public static string? Text(int pid)
    {
        nint process=OpenProcess(0x0410,false,pid); // PROCESS_QUERY_INFORMATION | PROCESS_VM_READ
        if(process==0)return null;
        try
        {
            var basic=new byte[48];
            if(NtQueryInformationProcess(process,0,basic,basic.Length,out _)<0)return null;
            nint peb=(nint)BitConverter.ToInt64(basic,8);if(peb==0)return null;
            nint parameters=Pointer(process,peb+0x20);if(parameters==0)return null;
            // Deux dispositions de RTL_USER_PROCESS_PARAMETERS circulent selon la
            // version : la ligne lue est gardee si elle porte le CLI attendu.
            foreach(int offset in new[]{0x70,0x78})
            {
                int length=Unsigned(process,parameters+offset);
                nint buffer=Pointer(process,parameters+offset+8);
                if(length<=0||length>32768||buffer==0)continue;
                var bytes=new byte[length];
                if(!Read(process,buffer,bytes))continue;
                string text=Encoding.Unicode.GetString(bytes);
                if(text.Contains("codex",StringComparison.OrdinalIgnoreCase))return text;
            }
            return null;
        }
        finally{CloseHandle(process);}
    }
    static nint Pointer(nint process,nint address){var bytes=new byte[8];return Read(process,address,bytes)?(nint)BitConverter.ToInt64(bytes):0;}
    static ushort Unsigned(nint process,nint address){var bytes=new byte[2];return Read(process,address,bytes)?BitConverter.ToUInt16(bytes):(ushort)0;}
    static bool Read(nint process,nint address,byte[] buffer)
        => ReadProcessMemory(process,address,buffer,buffer.Length,out nint read)&&read==buffer.Length;
    // Variante DeepSeek et projet de la ligne de commande, sans dependre de l'ordre.
    public static (bool DeepSeek,string? Project) Parse(string? command)
    {
        var arguments=Split(command);bool deepSeek=false;string? project=null;
        for(int index=0;index<arguments.Count;index++)
        {
            string argument=arguments[index];
            if(argument.Equals("model_provider=deepseek",StringComparison.OrdinalIgnoreCase)){deepSeek=true;continue;}
            if((argument=="-C"||argument=="--cd")&&index+1<arguments.Count)project=arguments[index+1];
        }
        return(deepSeek,string.IsNullOrWhiteSpace(project)?null:project);
    }
    // Decoupage des arguments en respectant les guillemets.
    internal static List<string> Split(string? command)
    {
        var arguments=new List<string>();var current=new StringBuilder();bool quoted=false;
        foreach(char value in command??"")
        {
            if(value=='"'){quoted=!quoted;continue;}
            if(!quoted&&char.IsWhiteSpace(value)){if(current.Length>0){arguments.Add(current.ToString());current.Clear();}continue;}
            current.Append(value);
        }
        if(current.Length>0)arguments.Add(current.ToString());
        return arguments;
    }
}

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
            var title=new StringBuilder(512);GetConsoleTitle(title,512);bool codex=false,kilo=false;
            int codexPid=0;
            var processes=new uint[128];uint count=GetConsoleProcessList(processes,(uint)processes.Length);
            foreach(uint id in processes.Take((int)Math.Min(count,(uint)processes.Length)))
            {
                try
                {
                    using var process=Process.GetProcessById((int)id);string name=process.ProcessName;
                    if(name.Equals("codex",StringComparison.OrdinalIgnoreCase)){codex=true;codexPid=(int)id;}
                    else if(name.Equals("kilo",StringComparison.OrdinalIgnoreCase))kilo=true;
                }
                catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
            }
            var (deepSeek,project)=codexPid==0?(false,(string?)null):CodexCommand.Parse(CodexCommand.Text(codexPid));
            return new(pid,TerminalTabPreferences.Clean(title.ToString()),codex,0,kilo,project,codexPid==0?null:deepSeek);
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
