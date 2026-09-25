using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;

static class GmailTests
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    static void Set(object target,string name,object? value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    sealed class FakeGmail:HttpMessageHandler
    {
        internal int Requests;
        internal bool Empty,MissingCount,MissingMessage,Offline;
        internal HttpStatusCode Status=HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Requests++;cancellation.ThrowIfCancellationRequested();
            Check(request.Method==HttpMethod.Get,"Mailbox requests are read-only");
            Check(request.RequestUri!.Host=="gmail.googleapis.com","Only Gmail endpoint");
            Check(request.Headers.Authorization?.Parameter=="test-access","Token is sent as a header");
            if(Offline)throw new HttpRequestException("Simulated offline");
            string path=request.RequestUri.AbsolutePath;
            string json=path.EndsWith("profile")?"{\"emailAddress\":\"demo@example.com\"}":path.EndsWith("INBOX")?(MissingCount?"{}":"{\"messagesUnread\":42}"):
                path.EndsWith("messages")?(Empty?"{}":"{\"messages\":[{\"id\":\"abc\"}]}"):
                """{"id":"abc","threadId":"def","labelIds":["INBOX","UNREAD"],"snippet":"Bonjour &amp; bienvenue","internalDate":"1790337600000","payload":{"headers":[{"name":"from","value":"Camille <demo@example.com>"},{"name":"Subject","value":"Déjeuner demain ?"}]}}""";
            var status=MissingMessage&&path.EndsWith("abc")?HttpStatusCode.NotFound:Status;
            return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(json)});
        }
    }
    static void Render(GmailSurface surface,string name)
    {
        var grid=new Grid{Width=surface.Width,Height=surface.Height,Background=new SolidColorBrush(Color.FromRgb(25,29,39))};grid.Children.Add(surface);
        grid.Measure(new(grid.Width,grid.Height));grid.Arrange(new(0,0,grid.Width,grid.Height));grid.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)grid.Width,(int)grid.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(grid);
        string directory=Path.GetFullPath("artifacts/validation/gmail");Directory.CreateDirectory(directory);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,name+".png"));encoder.Save(file);grid.Children.Clear();
    }
    [STAThread] static int Main()
    {
        int exit=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async (_,_)=>
        {
            string root=Path.Combine(Path.GetTempPath(),"Battlestation-gmail-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var store=new GmailStore(root);var token=new TokenResponse{AccessToken="test-access",RefreshToken="test-refresh",IssuedUtc=DateTime.UtcNow,ExpiresInSeconds=3600};
                await store.StoreAsync("account",token);
                Check(!Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(root,"gmail-token.bin"))).Contains("test-refresh"),"Token encrypted on disk");
                Check((await store.GetAsync<TokenResponse>("account")).RefreshToken=="test-refresh","DPAPI token round trip");
                var config=GmailClient.ParseClient("""{"installed":{"client_id":"test.apps.googleusercontent.com","client_secret":"test-client"}}""");
                await store.StoreAsync("client",config);await store.ClearAsync();
                Check(await store.GetAsync<TokenResponse>("account") is null,"Disconnect removes token");
                Check((await store.GetAsync<ClientSecrets>("client")).ClientId==config.ClientId,"Disconnect preserves client configuration");
                try{GmailClient.ParseClient("{\"web\":{}}");throw new Exception("Web credentials accepted");}catch(InvalidDataException){checks++;}
                using var handler=new FakeGmail();using var client=new GmailClient(root,handler);
                await client.InitializeAsync();Check(client.Configured&&!client.Connected,"Startup does not launch OAuth");
                var inbox=await client.FetchAsync("test-access",default);
                Check(inbox.Unread==42&&inbox.Messages.Length==1,"Unread total independent of visible messages");
                Check(inbox.Messages[0].Sender=="Camille"&&inbox.Messages[0].Preview=="Bonjour & bienvenue"&&inbox.Messages[0].Unread,"Headers, snippet and unread parsed");
                handler.MissingCount=true;Check((await client.FetchAsync("test-access",default)).Unread is null,"Missing is not zero");handler.MissingCount=false;
                handler.Empty=true;Check((await client.FetchAsync("test-access",default)).Messages.Length==0,"Empty inbox supported");handler.Empty=false;
                handler.MissingMessage=true;Check((await client.FetchAsync("test-access",default)).Messages.Length==0,"Deletion during refresh is tolerated");handler.MissingMessage=false;
                handler.Status=HttpStatusCode.Forbidden;
                try{await client.FetchAsync("test-access",default);throw new Exception("403 ignored");}catch(HttpRequestException e){Check(e.StatusCode==HttpStatusCode.Forbidden&&!e.Message.Contains("test-access"),"Errors sanitized");}handler.Status=HttpStatusCode.OK;
                handler.Offline=true;try{await client.FetchAsync("test-access",default);throw new Exception("Offline ignored");}catch(HttpRequestException){checks++;}handler.Offline=false;
                using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await client.FetchAsync("test-access",cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){checks++;}}
                Check(GmailClient.MessageUrl("demo+tag@example.com","def").Contains("authuser=demo%2Btag%40example.com#all/def"),"Message link selects connected account");
                using var surface=new GmailSurface(new Station(root));
                for(int i=0;i<100&&!Field<bool>(surface,"ready");i++)await Task.Delay(10);
                Check(Field<bool>(surface,"ready"),"Dock initialized");
                Render(surface,"disconnected");
                var liveClient=Field<GmailClient>(surface,"client");
                Set(liveClient,"credential",new UserCredential(new GoogleAuthorizationCodeFlow(new(){ClientSecrets=config,Scopes=[GmailClient.Scope]}),"account",token));
                Set(surface,"inbox",inbox with{Messages=Enumerable.Range(0,12).Select(i=>inbox.Messages[0] with{Id=i.ToString(),Unread=i%2==0,Subject=i==1?"Un objet très long qui doit rester dans la largeur du dock sans chevaucher la ligne suivante":inbox.Messages[0].Subject}).ToArray()});
                foreach(var dimensions in new[]{new Size(440,320),new Size(560,460),new Size(720,600)})
                {surface.Width=dimensions.Width;surface.Height=dimensions.Height;surface.Refresh();Render(surface,$"inbox-{dimensions.Width}x{dimensions.Height}");}
                Set(surface,"error","Hors connexion · Dernières données conservées.");surface.Refresh();Render(surface,"offline");
                var timer=Field<System.Windows.Threading.DispatcherTimer>(surface,"timer");
                Set(surface,"attempted",DateTimeOffset.UtcNow);surface.SetActive(true);Check(timer.IsEnabled,"Visible dock schedules refresh");surface.SetActive(false);Check(!timer.IsEnabled,"Hidden dock stops polling");
                using var pending=new CancellationTokenSource();Set(surface,"operation",pending);Set(surface,"active",true);surface.SetActive(false);Check(pending.IsCancellationRequested,"Hidden dock cancels in-flight refresh");Set(surface,"operation",null);
                Console.WriteLine($"GMAIL_CHECKS_PASS {checks}");
            }
            catch(Exception e){Console.Error.WriteLine(e);exit=1;}
            finally{Directory.Delete(root,true);app.Shutdown();}
        };app.Run();return exit;
    }
}
namespace Battlestation
{
    internal sealed class Station(string root){internal string Root=>Directory.GetCurrentDirectory();internal string Data=>root;}
    internal static class DesktopScreens{internal static Rect ToPixels(Rect rect)=>rect;}
    internal static class Native{internal static void BackgroundPanel(int slot,float x,float y,float w,float h){}}
    internal static class OverlayStyle
    {
        internal static TextBlock Text(string text,double size)=>new(){Text=text,FontSize=size};
        internal static Button Button(string text,Action action)=>new(){Content=text};
        internal static Border Frame(UIElement child)=>new(){Child=child};
        internal static void Apply(Window window){}
        internal static void Place(Window window){}
    }
}
