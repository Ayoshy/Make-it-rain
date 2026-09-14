namespace Battlestation.Core;

// Diagnostic snapshots must never stop input, rendering or sensor collection.
public static class DiagnosticFile
{
    static readonly object gate=new();
    static readonly Dictionary<string,string> pending=new();
    static bool running;
    public static void Write(string path,string text)
    {
        lock(gate)
        {
            if(pending.Count>=8&&!pending.ContainsKey(path))return;
            pending[path]=text;
            if(running)return;
            running=true;_=Task.Run(Drain);
        }
    }
    static void Drain()
    {
        while(true)
        {
            KeyValuePair<string,string> next;
            lock(gate){if(pending.Count==0){running=false;return;}next=pending.First();pending.Remove(next.Key);}
            TryWrite(next.Key,next.Value);
        }
    }
    public static bool TryWrite(string path,string text)
    {
        try{File.WriteAllText(path+".tmp",text);File.Move(path+".tmp",path,true);return true;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){return false;}
    }
}
