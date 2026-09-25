using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;

namespace Battlestation;

internal sealed record GmailMessage(string Id,string ThreadId,string Sender,string Subject,string Preview,DateTimeOffset Date,bool Unread);
internal sealed record GmailInbox(string Email,int? Unread,GmailMessage[] Messages,DateTimeOffset Updated);

// Only OAuth state is persisted; message contents stay in memory.
internal sealed class GmailStore(string directory) : IDataStore
{
    static readonly byte[] Entropy=Encoding.UTF8.GetBytes("Battlestation.Gmail.v1");
    string PathFor<T>(string key)=>Path.Combine(directory,typeof(T)==typeof(ClientSecrets)?"gmail-client.bin":"gmail-token.bin");
    public Task StoreAsync<T>(string key,T value)=>Task.Run(()=>
    {
        byte[] plain=JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            var encrypted=ProtectedData.Protect(plain,Entropy,DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(directory);string path=PathFor<T>(key);
            File.WriteAllBytes(path+".tmp",encrypted);File.Move(path+".tmp",path,true);
        }
        finally{CryptographicOperations.ZeroMemory(plain);}
    });
    public Task<T> GetAsync<T>(string key)=>Task.Run(()=>
    {
        string path=PathFor<T>(key);if(!File.Exists(path))return default!;
        byte[] plain=ProtectedData.Unprotect(File.ReadAllBytes(path),Entropy,DataProtectionScope.CurrentUser);
        try{return JsonSerializer.Deserialize<T>(plain)!;}
        finally{CryptographicOperations.ZeroMemory(plain);}
    });
    public Task DeleteAsync<T>(string key)=>Task.Run(()=>File.Delete(PathFor<T>(key)));
    public Task ClearAsync()=>DeleteAsync<TokenResponse>("account");
}

internal sealed class GmailClient : IDisposable
{
    internal const string Scope="https://www.googleapis.com/auth/gmail.readonly";
    readonly GmailStore store;
    readonly HttpClient http;
    ClientSecrets? secrets;
    UserCredential? credential;
    public bool Configured=>secrets is not null;
    public bool Connected=>credential is not null;
    internal GmailClient(string directory,HttpMessageHandler? handler=null)
    {store=new(directory);http=handler is null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(25);}

    public async Task InitializeAsync()
    {
        secrets=await store.GetAsync<ClientSecrets>("client");
        var token=await store.GetAsync<TokenResponse>("account");
        if(secrets is not null&&token?.RefreshToken is not null)
            credential=new UserCredential(new GoogleAuthorizationCodeFlow(new(){ClientSecrets=secrets,Scopes=[Scope],DataStore=store}),"account",token);
    }
    internal static ClientSecrets ParseClient(string json)
    {
        using var doc=JsonDocument.Parse(json);
        if(!doc.RootElement.TryGetProperty("installed",out var app))throw new InvalidDataException("Choisis un client OAuth de type Application de bureau.");
        string id=GetString(app,"client_id"),secret=GetString(app,"client_secret");
        if(!id.EndsWith(".apps.googleusercontent.com",StringComparison.Ordinal)||secret.Length==0)
            throw new InvalidDataException("Le fichier OAuth Google est incomplet.");
        return new(){ClientId=id,ClientSecret=secret};
    }
    public async Task ImportAsync(string path)
    {
        var next=ParseClient(await File.ReadAllTextAsync(path));
        await DisconnectAsync();await store.StoreAsync("client",next);secrets=next;
    }
    public async Task ConnectAsync(CancellationToken cancellation)
    {
        if(secrets is null)throw new InvalidDataException("Importe le fichier OAuth Google.");
        await store.ClearAsync();credential=null;
        credential=await GoogleWebAuthorizationBroker.AuthorizeAsync(secrets,[Scope],"account",cancellation,store);
    }
    public async Task DisconnectAsync(){await store.ClearAsync();credential=null;}
    public async Task<GmailInbox> ReadAsync(CancellationToken cancellation)
    {
        if(credential is null)throw new InvalidOperationException("Connecte ton compte Google.");
        string token;
        try{token=await credential.GetAccessTokenForRequestAsync(cancellationToken:cancellation);}
        catch(TokenResponseException e) when(e.Error?.Error=="invalid_grant")
        {credential=null;throw new InvalidOperationException("Connexion expirée · Reconnecte Google.");}
        return await FetchAsync(token,cancellation);
    }
    internal async Task<GmailInbox> FetchAsync(string token,CancellationToken cancellation)
    {
        using var profile=await GetAsync("profile?fields=emailAddress",token,cancellation);
        using var label=await GetAsync("labels/INBOX?fields=messagesUnread",token,cancellation);
        using var list=await GetAsync("messages?labelIds=INBOX&maxResults=12&fields=messages(id)",token,cancellation);
        var messages=new List<GmailMessage>();
        if(list.RootElement.TryGetProperty("messages",out var entries))
            foreach(var entry in entries.EnumerateArray())
            {
                try
                {
                    using var message=await GetAsync("messages/"+Uri.EscapeDataString(GetString(entry,"id"))+"?format=metadata&metadataHeaders=From&metadataHeaders=Subject&fields=id,threadId,labelIds,snippet,internalDate,payload/headers",token,cancellation);
                    messages.Add(ParseMessage(message.RootElement));
                }
                catch(HttpRequestException e) when(e.StatusCode==HttpStatusCode.NotFound){} // Moved/deleted between list and get.
            }
        int? unread=label.RootElement.TryGetProperty("messagesUnread",out var count)&&count.TryGetInt32(out int number)?number:null;
        return new(GetString(profile.RootElement,"emailAddress"),unread,messages.OrderByDescending(m=>m.Date).ToArray(),DateTimeOffset.Now);
    }
    async Task<JsonDocument> GetAsync(string path,string token,CancellationToken cancellation)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://gmail.googleapis.com/gmail/v1/users/me/"+path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(request,cancellation);
        if(!response.IsSuccessStatusCode)
        {
            if(response.StatusCode==HttpStatusCode.Unauthorized)credential=null;
            string message=response.StatusCode switch
            {
                HttpStatusCode.Unauthorized=>"Connexion expirée · Reconnecte Google.",
                HttpStatusCode.Forbidden=>"Accès refusé · Vérifie l’API Gmail et les autorisations Google.",
                HttpStatusCode.TooManyRequests=>"Gmail limite les requêtes · Réessaie plus tard.",
                _=>"Gmail indisponible · Réessaie plus tard."
            };
            throw new HttpRequestException(message,null,response.StatusCode);
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
    }
    internal static GmailMessage ParseMessage(JsonElement value)
    {
        string Header(string name)=>value.TryGetProperty("payload",out var payload)&&payload.TryGetProperty("headers",out var headers)
            ?headers.EnumerateArray().Where(h=>GetString(h,"name").Equals(name,StringComparison.OrdinalIgnoreCase)).Select(h=>GetString(h,"value")).FirstOrDefault()??"":"";
        string sender=Header("From");
        if(MailAddress.TryCreate(sender,out var address))sender=string.IsNullOrWhiteSpace(address.DisplayName)?address.Address:address.DisplayName;
        string subject=Header("Subject");
        var date=long.TryParse(GetString(value,"internalDate"),out long milliseconds)&&milliseconds>=0&&milliseconds<=253402300799999
            ?DateTimeOffset.FromUnixTimeMilliseconds(milliseconds):DateTimeOffset.MinValue;
        return new(GetString(value,"id"),GetString(value,"threadId"),sender,subject.Length==0?"(Sans objet)":subject,
            WebUtility.HtmlDecode(GetString(value,"snippet")).Replace('\r',' ').Replace('\n',' '),date,
            value.TryGetProperty("labelIds",out var labels)&&labels.EnumerateArray().Any(l=>l.GetString()=="UNREAD"));
    }
    static string GetString(JsonElement value,string key)=>value.TryGetProperty(key,out var item)&&item.ValueKind==JsonValueKind.String?item.GetString()??"":"";
    internal static string InboxUrl(string email)=>"https://mail.google.com/mail/u/?authuser="+Uri.EscapeDataString(email);
    internal static string MessageUrl(string email,string thread)=>InboxUrl(email)+"#all/"+Uri.EscapeDataString(thread);
    public void Dispose(){http.Dispose();credential?.Flow.Dispose();}
}
