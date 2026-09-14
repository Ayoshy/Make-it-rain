using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using Battlestation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class ExperiencesTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [STAThread] static void Main()
    {
        var folder=Path.Combine(Path.GetTempPath(),"Battlestation-experiences-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try{Links(folder);Profiles(folder);Projects(folder).GetAwaiter().GetResult();}
        finally
        {
            if(!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Fixture cleanup escaped the temporary directory");
            foreach(string file in Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories))File.SetAttributes(file,FileAttributes.Normal);
            Directory.Delete(folder,true);
        }
        Console.WriteLine("PASS: media URL boundaries/deduplication/persistence/capacity, independent layout profiles/rollback and real temporary Git states. No user clipboard or account read.");
    }
    static void Links(string folder)
    {
        string canonical="https://www.youtube.com/watch?v=abcdefghijk";
        foreach(string url in new[]{"https://youtu.be/abcdefghijk?si=tracking","https://www.youtube.com/watch?v=abcdefghijk&t=30","https://youtube.com/shorts/abcdefghijk","http://m.youtube.com/live/abcdefghijk"})Check(MediaLink.Parse(url)?.Url==canonical,"YouTube aliases must share one canonical URL");
        string spotify="https://open.spotify.com/track/1234567890123456789012";
        Check(MediaLink.Parse(spotify+"?si=tracking")?.Url==spotify,"Spotify tracking stripped");
        Check(MediaLink.Parse("https://open.spotify.com/intl-fr/track/1234567890123456789012")?.Url==spotify,"Spotify locale stripped");
        Check(MediaLink.Parse("https://spotify.link/Abcdefg123?si=tracking")?.Url=="https://spotify.link/Abcdefg123","Spotify share links supported");
        foreach(string invalid in new[]{"ordinary clipboard text","secret-value", "https://youtube.com.evil.test/watch?v=abcdefghijk","https://youtube.com@evil.test/watch?v=abcdefghijk","https://user@youtube.com/watch?v=abcdefghijk","file:///C:/private.txt","https://www.youtube.com/watch?v=short","https://www.youtube.com:123/watch?v=abcdefghijk","https://open.spotify.com/track/wrong",new string('a',3000)})Check(MediaLink.Parse(invalid) is null,"Unrecognized or unsafe text must be discarded");
        string file=Path.Combine(folder,"reserve.json");
        using(var store=new MediaReserve(file,new Offline()))
        {
            store.Add(MediaLink.Parse(canonical)!);store.Add(MediaLink.Parse("https://youtu.be/abcdefghijk?t=3")!);
            Check(store.Items.Count==1,"Repeated aliases never duplicate");
            for(int i=0;i<105;i++)store.Add(MediaLink.Parse("https://youtu.be/"+i.ToString("D11"))!);
            Check(store.Items.Count==100,"Reserve has a hard capacity");
            string newest=store.Items[0].Url;store.Remove(newest);Check(store.Items.Count==99&&store.Items.All(x=>x.Url!=newest),"Removing an entry persists without affecting others");
        }
        using var loaded=new MediaReserve(file,new Offline());Check(loaded.Items.Count==99,"Reserve survives reload");
        Check(!File.ReadAllText(file).Contains("tracking"),"Only canonical media is persisted");
        using var metadata=new MediaReserve(Path.Combine(folder,"metadata.json"),new Metadata());metadata.Add(MediaLink.Parse(canonical)!);
        Check(metadata.Items.Single().Title=="Fixture title","Metadata enriches the saved link without a browser or account");
        using var artwork=new MediaReserve(Path.Combine(folder,"artwork.json"),new Artwork());artwork.Add(MediaLink.Parse(spotify)!);
        Check(artwork.Cover(spotify) is not null,"The current Spotify thumbnail CDN must decode into an in-memory image");
    }
    static void Profiles(string folder)
    {
        var settings=new DesktopSettings(folder,"Paris",48,2);var layout=new DesktopLayout(Path.Combine(folder,"layout.json"),5);
        var original=layout.Blocks.ToArray();var profiles=new DesktopProfiles(Path.Combine(folder,"profiles.json"));
        profiles.Switch("Cinéma",layout,settings);Check(!layout["terminal"].Visible&&layout["music"].Visible,"Cinema hides the terminal frame and keeps playback");
        Check(layout.Move("music",3000,1000),"Cinema can have its own arrangement");Check(layout.Restore(layout.Blocks.Select(b=>b.Id=="music"?b with{Width=640,Height=200}:b)),"Cinema can resize a dock");var cinema=layout.Blocks.ToArray();
        profiles.Switch("Personnel",layout,settings);Check(layout.Blocks.SequenceEqual(original),"Personal restores every original block exactly");
        profiles.Switch("Cinéma",layout,settings);Check(layout.Blocks.SequenceEqual(cinema),"Cinema remembers its edited arrangement");
        var reloaded=new DesktopProfiles(Path.Combine(folder,"profiles.json"));Check(reloaded.Current=="Cinéma","Selected profile persists");
        reloaded.Switch("Personnel",layout,settings);Check(layout.Blocks.SequenceEqual(original),"Stored profiles survive reload");
        var bad=original.Select(x=>x.Id=="music"?x with{X=layout["clock"].X,Y=layout["clock"].Y}:x).ToList();
        Check(!layout.Restore(bad)&&layout.Blocks.SequenceEqual(original),"Collision rejection must be transactional");
        var nan=original.Select(x=>x.Id=="music"?x with{X=double.NaN}:x).ToList();Check(!layout.Restore(nan),"Invalid profile coordinates rejected");
        foreach(string preset in DesktopProfiles.Presets)
        {
            reloaded.Switch(preset,layout,settings);var ready=layout.Blocks.ToArray();
            foreach(var block in ready.Where(b=>b.Visible)){
                var minimum=DesktopLayout.Minimum(block.Id);
                Check(DesktopLayout.Valid(block,ready)&&block.Width>=minimum.Width&&block.Height>=minimum.Height,"Preset respects gaps, screens and content minima: "+preset+" / "+block.Id);
            }
            Check(ready.Any(b=>b.Visible&&b!=original.Single(o=>o.Id==b.Id)),"Preset supplies actual positions and dimensions");
            Check(preset=="Double écran"?ready.Any(b=>b.Visible&&b.X<2560):ready.All(b=>!b.Visible||b.X>=2560),"Only the dual-screen preset occupies the primary monitor");
            reloaded.Switch("Personnel",layout,settings);Check(layout.Blocks.SequenceEqual(original),"Preset preserves personal layout exactly");
            reloaded.Switch(preset,layout,settings);Check(layout.Blocks.SequenceEqual(ready),"Preset survives switching away and back");
            reloaded.Switch("Personnel",layout,settings);
        }
    }
    static async Task Projects(string folder)
    {
        Check((await ProjectSignals.Git(folder)).Changes is null,"Non-Git project has unknown status, not zero");
        string repo=Path.Combine(folder,"sample project (one)");Directory.CreateDirectory(repo);
        RunGit(repo,"init","--initial-branch=main");File.WriteAllText(Path.Combine(repo,"sample.txt"),"one");RunGit(repo,"add","sample.txt");
        RunGit(repo,"-c","user.name=Fixture","-c","user.email=fixture@example.invalid","-c","core.hooksPath="+Path.Combine(folder,"no-hooks"),"commit","--no-gpg-sign","-m","fixture");
        var clean=await ProjectSignals.Git(repo);Check(clean.Branch=="main"&&clean.Changes==0,"Actual clean Git repository detected");
        File.WriteAllText(Path.Combine(repo,"sample.txt"),"two");File.WriteAllText(Path.Combine(repo,"new file.txt"),"untracked");
        var changed=await ProjectSignals.Git(repo);Check(changed.Branch=="main"&&changed.Changes==2,"Tracked and untracked changes counted");
        RunGit(repo,"checkout","--detach");Check((await ProjectSignals.Git(repo)).Branch=="HEAD détachée","Detached HEAD remains explicit");
    }
    static void RunGit(string path,params string[] args)
    {
        var info=new ProcessStartInfo("git"){WorkingDirectory=path,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();process.WaitForExit();Task.WaitAll(stdout,stderr);Check(process.ExitCode==0,"Git fixture command failed");
    }
    sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
    sealed class Metadata : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Check(request.RequestUri!.Host=="www.youtube.com","Only the approved metadata endpoint is contacted; arbitrary thumbnail hosts are ignored");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"title\":\"Fixture title\",\"thumbnail_url\":\"https://invalid.example/private.png\"}")});
        }
    }
    sealed class Artwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(request.RequestUri!.Host=="open.spotify.com")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"title\":\"Music fixture\",\"thumbnail_url\":\"https://image-cdn-ak.spotifycdn.com/image/fixture\"}")});
            Check(request.RequestUri.Host=="image-cdn-ak.spotifycdn.com","Artwork request restricted to approved CDN");
            var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{240,210,230,255},4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(stream.ToArray())});
        }
    }
}
