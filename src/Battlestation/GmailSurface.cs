using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Battlestation;

internal sealed class GmailSurface : Surface,IDisposable
{
    readonly GmailClient client;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMinutes(2)};
    readonly CancellationTokenSource lifetime=new();
    readonly Pen envelopePen=new(B(Ink),1.8);
    CancellationTokenSource? operation;
    GmailInbox? inbox;
    bool ready,busy,active,disposed,connecting;
    string error="";
    int offset;
    DateTimeOffset attempted=DateTimeOffset.MinValue;
    internal GmailSurface(Station station):base(station,22)
    {
        Width=560;Height=460;client=new(station.Data);
        timer.Tick+=(_,_)=>Poll();
        _=InitializeAsync();
    }
    async Task InitializeAsync()
    {
        await RunAsync(async _=>{await client.InitializeAsync();ready=true;});
        if(!ready)ready=true;
        Poll();
    }
    internal void SetActive(bool value)
    {
        if(active==value)return;active=value;
        if(value){timer.Start();Poll();}
        else{timer.Stop();if(!connecting)operation?.Cancel();}
    }
    void Poll(bool force=false)
    {
        if(disposed||!ready||busy||!active||!client.Connected||!force&&DateTimeOffset.UtcNow-attempted<TimeSpan.FromMinutes(2))return;
        attempted=DateTimeOffset.UtcNow;
        _=RunAsync(async token=>{var next=await client.ReadAsync(token);if(!token.IsCancellationRequested)inbox=next;});
    }
    async Task RunAsync(Func<CancellationToken,Task> work,bool authorization=false)
    {
        if(busy||disposed)return;busy=true;connecting=authorization;error="";Refresh();
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        cancellation.CancelAfter(authorization?TimeSpan.FromMinutes(3):TimeSpan.FromSeconds(60));operation=cancellation;
        try{await Task.Run(()=>work(cancellation.Token));}
        catch(OperationCanceledException){if(active&&!disposed)error=authorization?"Connexion annulée · Réessaie quand tu es prêt.":"Actualisation interrompue.";}
        catch(InvalidDataException e){error=e.Message;}
        catch(InvalidOperationException e){error=e.Message;}
        catch(HttpRequestException e){error=e.StatusCode is null?"Hors connexion · Dernières données conservées.":e.Message;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {error="Données de connexion illisibles · Importe à nouveau le fichier OAuth.";}
        catch(Google.Apis.Auth.OAuth2.Responses.TokenResponseException){error="Connexion Google refusée · Vérifie le compte de test et les autorisations.";}
        catch(Exception){error="Connexion indisponible · Réessaie depuis les réglages Gmail.";}
        finally
        {
            operation=null;busy=false;connecting=false;
            if(!disposed)Refresh();else client.Dispose();
        }
    }
    async void Connect()
    {
        if(!client.Configured){Configure();return;}
        await RunAsync(client.ConnectAsync,true);attempted=DateTimeOffset.MinValue;Poll();
    }
    void Open(string url)
    {
        try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true})?.Dispose();}
        catch{error="Impossible d’ouvrir le navigateur.";Refresh();}
    }
    internal void Configure()
    {
        if(busy||!ready)return;
        var window=new Window{Title="Battlestation · Gmail",Width=560,SizeToContent=SizeToContent.Height,ShowInTaskbar=false,Owner=Window.GetWindow(this)};
        var content=new StackPanel();content.Children.Add(OverlayStyle.Text("Connexion Gmail",18));
        var note=OverlayStyle.Text("1. Active Gmail API dans ton projet Google Cloud.\n2. Crée un client OAuth « Application de bureau ».\n3. Télécharge son fichier JSON, puis importe-le ici.\n4. Connecte ton compte Google en lecture seule.",12);
        note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,16,0,14);content.Children.Add(note);
        var links=new StackPanel{Orientation=Orientation.Horizontal};
        links.Children.Add(OverlayStyle.Button("Google Cloud",()=>Open("https://console.cloud.google.com/auth/clients")));
        links.Children.Add(OverlayStyle.Button("Guide",()=>Open(Path.Combine(Station.Root,"docs","GMAIL.md"))));content.Children.Add(links);
        var status=OverlayStyle.Text(client.Configured?"Fichier OAuth enregistré sur ce PC.":"Aucun fichier OAuth importé.",12);status.Margin=new Thickness(0,14,0,10);content.Children.Add(status);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};
        actions.Children.Add(OverlayStyle.Button("Importer le fichier OAuth…",()=>
        {
            var picker=new Microsoft.Win32.OpenFileDialog{Title="Client OAuth Google · Application de bureau",Filter="Fichier JSON (*.json)|*.json"};
            if(picker.ShowDialog(window)!=true)return;
            window.Close();inbox=null;offset=0;
            _=RunAsync(_=>client.ImportAsync(picker.FileName));
        }));
        var disconnect=OverlayStyle.Button("Déconnecter ce PC",()=>{window.Close();inbox=null;offset=0;_=RunAsync(_=>client.DisconnectAsync());});
        disconnect.IsEnabled=client.Connected;actions.Children.Add(disconnect);content.Children.Add(actions);
        var close=OverlayStyle.Button("Fermer",()=>window.Close());close.IsCancel=true;close.HorizontalAlignment=HorizontalAlignment.Right;close.Margin=new Thickness(0,14,0,0);content.Children.Add(close);
        window.Content=OverlayStyle.Frame(content);OverlayStyle.Apply(window);OverlayStyle.Place(window);window.ShowDialog();
    }
    int Capacity=>Math.Max(1,(int)((Height-164)/76));
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if(inbox is null)return;
        offset=Math.Clamp(offset+(e.Delta<0?1:-1),0,Math.Max(0,inbox.Messages.Length-Capacity));Refresh();e.Handled=true;
    }
    protected override void Paint()
    {
        Header("GMAIL");
        Button("GmailSettings","⚙",Width-54,12,32,30,Configure,13,Muted,enabled:ready&&!busy);
        if(!ready||!client.Connected)
        {
            Envelope(Width/2-25,76);
            Text(connecting?"Autorise l’accès dans ton navigateur":!ready?"Chargement…":"Connecter ton compte Gmail",Width/2,144,13,Ink,align:"center");
            if(error.Length>0)Text(error,22,182,10,"#FFC98F",width:Width-44);
            if(connecting)Button("GmailCancel","Annuler",Width/2-70,Height-65,140,34,()=>operation?.Cancel());
            else Button("GmailConnect",client.Configured?"Connecter Gmail":"Configurer Gmail",Width/2-90,Height-65,180,34,Connect,enabled:ready&&!busy);
            return;
        }
        Text(inbox?.Email??"Gmail",24,58,10,Muted,width:Width-170);
        Button("GmailRefresh",busy?"…":"↻",Width-96,12,32,30,()=>Poll(true),16,Muted,enabled:!busy);
        Button("GmailOpen","Ouvrir Gmail",Width-142,53,120,28,()=>Open(GmailClient.InboxUrl(inbox?.Email??"")),9);
        string count=inbox?.Unread?.ToString()??"—";
        double labelX=24+Math.Max(50,count.Length*21)+16;
        Text(count,24,86,23,Ink,bold:true);
        Text("non lus · boîte de réception",labelX,98,10,Muted,width:Width-labelX-22);
        int capacity=Capacity;offset=Math.Clamp(offset,0,Math.Max(0,(inbox?.Messages.Length??0)-capacity));
        if(inbox is null)Text(busy?"Actualisation…":"Aucune donnée reçue",24,145,12,Muted);
        else if(inbox.Messages.Length==0)Text("Boîte de réception vide",24,145,12,Muted);
        else foreach(var (message,index) in inbox.Messages.Skip(offset).Take(capacity).Select((m,i)=>(m,i)))
        {
            double y=132+index*76;var rect=new Rect(16,y,Width-32,70);HoverGlass(rect);
            if(message.Unread)D.DrawEllipse(B(Purple),null,new Point(26,y+16),3,3);
            string date=message.Date==DateTimeOffset.MinValue?"":message.Date.LocalDateTime.Date==DateTime.Today?message.Date.ToLocalTime().ToString("HH:mm"):message.Date.ToLocalTime().ToString("dd MMM",French);
            Text(message.Sender,38,y+6,10,message.Unread?Ink:Muted,bold:message.Unread,width:Width-130);
            Text(date,Width-27,y+7,9,Muted,align:"right");
            Text(message.Subject,38,y+26,11,Ink,bold:message.Unread,width:Width-62);
            Text(message.Preview,38,y+47,9,Muted,width:Width-62);
            Hit("GmailMessage"+index,rect.X,rect.Y,rect.Width,rect.Height,()=>Open(GmailClient.MessageUrl(inbox.Email,message.ThreadId)));
        }
        string footer=error.Length>0?error:busy?"Actualisation…":inbox is null?"":$"Actualisé à {inbox.Updated:HH:mm}";
        Text(footer,24,Height-25,9,error.Length>0?"#FFC98F":Muted,width:Width-110);
        if(inbox is not null&&inbox.Messages.Length>capacity)Text($"{offset+1}–{Math.Min(offset+capacity,inbox.Messages.Length)}/{inbox.Messages.Length}",Width-24,Height-25,9,Muted,align:"right");
    }
    void Envelope(double x,double y)
    {
        var pen=envelopePen;
        D.DrawRoundedRectangle(B("#16FFFFFF"),pen,new Rect(x,y,50,36),8,8);
        D.DrawLine(pen,new Point(x+3,y+5),new Point(x+25,y+21));D.DrawLine(pen,new Point(x+25,y+21),new Point(x+47,y+5));
    }
    internal object Inspect()=>new{ready,busy,active,configured=client.Configured,connected=client.Connected,messages=inbox?.Messages.Length,unread=inbox?.Unread,updated=inbox?.Updated,error};
    public void Dispose()
    {
        disposed=true;timer.Stop();lifetime.Cancel();
        // In-flight I/O observes cancellation before the client is disposed.
        if(!busy)client.Dispose();
    }
}
