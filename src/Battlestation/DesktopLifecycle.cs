using System.IO;

namespace Battlestation;

// Temporary incident trace: desktop lifecycle and scene stages only, never terminal data.
internal static class DesktopLifecycle
{
    static readonly object gate=new();
    internal static void Write(string message)
    {
        try
        {
            lock(gate)
            {
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Battlestation");
                Directory.CreateDirectory(folder);
                string file=Path.Combine(folder,"desktop-lifecycle.log");
                if(File.Exists(file)&&new FileInfo(file).Length>65536)File.Move(file,file+".previous",true);
                File.AppendAllText(file,$"{DateTimeOffset.Now:O} pid={Environment.ProcessId} {message}{Environment.NewLine}");
            }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
    }
}
