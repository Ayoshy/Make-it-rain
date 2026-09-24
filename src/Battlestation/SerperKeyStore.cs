using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Battlestation;

internal sealed class SerperKeyStore
{
    readonly string path;
    volatile string? key;
    public bool HasKey=>!string.IsNullOrWhiteSpace(key);
    public string Error{get;private set;}="";
    static readonly byte[] Entropy=Encoding.UTF8.GetBytes("Battlestation.Serper.v1");
    public SerperKeyStore(string directory)
    {
        path=Path.Combine(directory,"serper-key.bin");
        try{if(File.Exists(path))key=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),Entropy,DataProtectionScope.CurrentUser));}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException)
        {Error="Clé enregistrée illisible. Enregistre-la de nouveau.";}
    }
    internal string? Read()=>key;
    public void Save(string value)
    {
        value=value.Trim();
        if(value.Length is 0 or >1024||value.Any(char.IsWhiteSpace))throw new ArgumentException("Colle uniquement la clé API Serper.");
        byte[] plain=Encoding.UTF8.GetBytes(value);
        try
        {
            var encrypted=ProtectedData.Protect(plain,Entropy,DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary=path+".tmp";
            File.WriteAllBytes(temporary,encrypted);File.Move(temporary,path,true);key=value;Error="";
        }
        finally{CryptographicOperations.ZeroMemory(plain);}
    }
}
