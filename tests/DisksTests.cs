using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Battlestation;

internal static class DisksTests
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static async Task Wait(Func<bool> condition,string name,int seconds=30)
    {
        var until=DateTime.UtcNow.AddSeconds(seconds);
        while(!condition()&&DateTime.UtcNow<until)await Task.Delay(100);
        Check(condition(),name);
    }
    static void Write(string path,int length){using var file=File.Create(path);file.SetLength(length);}
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(target)!;
    static void Set(object target,string name,object? value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(target,value);
    static void Invoke(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(target,args);
    static void Render(DisksSurface surface,string name)
    {
        var grid=new Grid{Width=surface.Width,Height=surface.Height,Background=new SolidColorBrush(Color.FromRgb(29,29,40))};grid.Children.Add(surface);
        grid.Measure(new(grid.Width,grid.Height));grid.Arrange(new(0,0,grid.Width,grid.Height));grid.UpdateLayout();
        var image=new RenderTargetBitmap((int)grid.Width,(int)grid.Height,96,96,PixelFormats.Pbgra32);image.Render(grid);
        string output=Path.GetFullPath(Path.Combine("artifacts","validation","disks",name+".png"));Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(output);png.Save(stream);grid.Children.Clear();
    }
    [STAThread] static int Main(string[] args)
    {
        int result=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async (_,_)=>
        {
            if(args.Contains("--cards-only"))
            {
                try
                {
                    using var cards=new DisksSurface(new Station(Directory.GetCurrentDirectory()));
                    Set(Field<DiskIndex>(cards,"index"),"volumes",new DiskVolume[]{new("C:\\","Disque local",499301478400,116608362086),new("D:\\","Data_Win",249952202752,97962393600),new("E:\\","Data2",250058108928,86835007488)});
                    var cardIndex=Field<DiskIndex>(cards,"index");
                    foreach(var volume in cardIndex.Volumes)cardIndex.Select(volume.Path);
                    cardIndex.Select(null);
                    var cardIndexes=Field<Dictionary<string,DiskVolumeIndex>>(cardIndex,"indexes");
                    foreach(string volume in new[]{"C:\\","E:\\"})
                    {
                        Set(cardIndexes[volume],"completedTicks",DateTimeOffset.UtcNow.AddMinutes(-12).Ticks);Set(cardIndexes[volume],"rescan",false);
                        Set(cardIndexes[volume],"snapshot",new DiskNode(volume,volume,100,true,volume=="E:\\",[]));
                    }
                    Set(cardIndexes["D:\\"],"scanning",true);
                    foreach(var theme in DesktopTheme.Definitions)
                    {
                        DesktopTheme.Select(theme.Id,false);
                        foreach(var dimensions in new[]{new Size(665,367),new Size(1242,726),new Size(440,320),new Size(560,600),new Size(780,492),new Size(900,340)})
                        {cards.Width=dimensions.Width;cards.Height=dimensions.Height;cards.Refresh();Render(cards,$"cards-{theme.Id}-{dimensions.Width}x{dimensions.Height}");}
                    }
                    foreach(var dimensions in new[]{new Size(613,310),new Size(440,320),new Size(1242,726)})
                    foreach(string folder in new[]{"C:\\","C:\\Users\\Ayo\\Documents\\Project\\Battlestation\\assets\\Illustrations"})
                    {
                        cards.Width=dimensions.Width;cards.Height=dimensions.Height;
                        Set(cards,"path",folder);Set(cards,"shown",new DiskNode(folder,folder,900,true,false,[new(folder+"\\Program Files","Program Files",450,true,false,[]),new(folder+"\\Users","Users",300,true,false,[]),new(folder+"\\Windows","Windows",150,true,false,[])]));Set(cards,"drawing",null);
                        cards.Refresh();Render(cards,$"header-{(folder.Length>3?"long":"root")}-{dimensions.Width}x{dimensions.Height}");
                    }
                    Console.WriteLine("DISKS_CARDS_RENDERED 24");
                }
                catch(Exception e){Console.Error.WriteLine(e);result=1;}
                finally{app.Shutdown();}
                return;
            }
            string root=Path.Combine(Path.GetTempPath(),"Battlestation-disks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string otherRoot=root+"-other";
            try
            {
                string folder=Path.Combine(root,"Games");Directory.CreateDirectory(folder);Write(Path.Combine(folder,"large.bin"),8000);Write(Path.Combine(root,"root.bin"),2000);
                for(int i=0;i<160;i++)Write(Path.Combine(folder,$"small-{i}.bin"),i+1);
                DiskNode tree;
                using(var first=new DiskIndex(()=>[])){first.Select(root);first.SetActive(true);await Wait(()=>first.Snapshot is not null&&first.Status=="","Fixture analysée");tree=first.Snapshot!;}
                Check(tree.Bytes==22880,"Les tailles incluent tous les fichiers, même regroupés");
                Check(tree.Find(folder)!.Children.Length==97,"Les petits fichiers sont regroupés sans tronquer le total");
                var rect=new Rect(10,20,740,310);var tiles=DiskMap.Layout(tree.Children,rect);
                Check(Math.Abs(tiles.Sum(t=>t.Bounds.Width*t.Bounds.Height)-rect.Width*rect.Height)<.001,"La mosaïque couvre exactement sa surface");
                foreach(var tile in tiles)Check(Math.Abs(tile.Bounds.Width*tile.Bounds.Height/(rect.Width*rect.Height)-tile.Node.Bytes/(double)tree.Bytes)<.00001,"Les surfaces sont proportionnelles aux tailles");
                Check(!tiles[0].Bounds.IntersectsWith(new Rect(tiles[1].Bounds.X+.01,tiles[1].Bounds.Y+.01,tiles[1].Bounds.Width-.02,tiles[1].Bounds.Height-.02)),"Les zones ne se recouvrent pas");
                using(var index=new DiskIndex(()=>[]))
                {
                    index.Select(root);index.SetActive(true);await Wait(()=>index.Snapshot?.Bytes==22880&&index.Status=="","Analyse initiale en arrière-plan");
                    Write(Path.Combine(folder,"large.bin"),18000);await Wait(()=>index.Snapshot?.Bytes==32880,"Modification de taille suivie sans analyse complète");
                    string nested=Path.Combine(folder,"New");Directory.CreateDirectory(nested);Write(Path.Combine(nested,"new.bin"),1234);
                    await Wait(()=>index.Snapshot?.Bytes==34114,"Création d'un sous-dossier prise en compte");
                    Directory.Move(nested,Path.Combine(root,"Moved"));await Wait(()=>index.Snapshot?.Find(Path.Combine(root,"Moved"))?.Bytes==1234&&index.Snapshot.Bytes==34114,"Déplacement sans double comptage");
                    index.SetActive(false);await Task.Delay(150);var before=index.Snapshot;Write(Path.Combine(root,"root.bin"),7000);await Task.Delay(2800);
                    Check(ReferenceEquals(before,index.Snapshot),"Aucune réanalyse pendant le masquage");
                    index.SetActive(true);await Wait(()=>index.Snapshot?.Bytes==39114,"Reprise avec les changements survenus pendant le masquage");
                    File.Delete(Path.Combine(root,"Moved","new.bin"));await Wait(()=>index.Snapshot?.Bytes==37880,"Suppression répercutée sur les ancêtres");
                    var retained=Field<Dictionary<string,DiskVolumeIndex>>(index,"indexes");var firstDisk=retained[root];
                    var cached=index.Snapshot;long reads=firstDisk.MetadataReads;
                    index.Select(null);index.Select(root);
                    Check(ReferenceEquals(cached,index.Snapshot)&&firstDisk.MetadataReads==reads,"Retour au disque immédiat, même index sans lecture disque");
                    Directory.CreateDirectory(otherRoot);Write(Path.Combine(otherRoot,"other.bin"),111);
                    index.Select(otherRoot);await Wait(()=>index.Snapshot?.Bytes==111&&index.Status=="","Deuxième disque analysé une seule fois");
                    var secondDisk=retained[otherRoot];var secondTree=index.Snapshot;
                    Write(Path.Combine(folder,"large.bin"),19000);
                    await Wait(()=>firstDisk.Snapshot?.Bytes==38880,"Delta appliqué au disque A pendant la consultation de B");
                    Check(ReferenceEquals(secondTree,index.Snapshot),"Le delta de A ne remplace pas la vue B");
                    index.Select(root);Check(ReferenceEquals(index.Snapshot,firstDisk.Snapshot)&&index.Snapshot?.Bytes==38880,"Réouverture immédiate sur le delta déjà appliqué");
                    index.Select(null);Write(Path.Combine(otherRoot,"other.bin"),333);
                    await Wait(()=>secondDisk.Snapshot?.Bytes==333,"Les deltas continuent depuis l'accueil");
                    index.SetActive(false);await Task.Delay(150);reads=firstDisk.MetadataReads+secondDisk.MetadataReads;
                    Write(Path.Combine(folder,"large.bin"),20000);Write(Path.Combine(otherRoot,"other.bin"),444);await Task.Delay(2800);
                    Check(firstDisk.MetadataReads+secondDisk.MetadataReads==reads,"Tous les index suspendent leurs lectures quand le dock est masqué");
                    index.SetActive(true);await Wait(()=>firstDisk.Snapshot?.Bytes==39880&&secondDisk.Snapshot?.Bytes==444,"Les deux index reprennent leurs deltas");
                    Check(firstDisk.FullScans==1&&secondDisk.FullScans==1,"Un seul scan complet par disque malgré navigation, deltas et masquage");
                    reads=firstDisk.MetadataReads+secondDisk.MetadataReads;
                    for(int i=0;i<20;i++){index.Select(root);index.Select(otherRoot);index.Select(null);}
                    await Task.Delay(3000);
                    Check(firstDisk.MetadataReads+secondDisk.MetadataReads==reads,"Vingt allers-retours sans aucune réénumération des fichiers");
                    // Incident réel : sur C:, chaque débordement du suivi relançait un scan complet (un cœur pendant des minutes).
                    var mark=typeof(DiskVolumeIndex).GetMethod("Mark",BindingFlags.NonPublic|BindingFlags.Instance)!;
                    for(int i=0;i<4100;i++)mark.Invoke(firstDisk,[Path.Combine(root,"burst"+i,"file.tmp")]);
                    await Task.Delay(3000);
                    Check(firstDisk.FullScans==1&&firstDisk.Info.Pending,"Un débordement du suivi reporte le scan complet au lieu de le relancer aussitôt");
                    index.Select(root);index.Rescan();await Wait(()=>firstDisk.FullScans==2&&index.Status=="","Le bouton manuel peut toujours relancer un scan");
                    Check(secondDisk.FullScans==1,"La relance manuelle reste limitée au disque sélectionné");
                }
                using(var surface=new DisksSurface(new Station(root),new DiskIndex(()=>[new(root,"Fixture",100000,50000)])))
                {
                    surface.SetDisplayed(true);surface.SetActive(true);var source=Field<DiskIndex>(surface,"index");await Wait(()=>source.Volumes.Length>0,"Volumes détectés sans sélectionner un disque");
                    foreach(var theme in DesktopTheme.Definitions)
                    {
                        DesktopTheme.Select(theme.Id,false);surface.Width=780;surface.Height=492;Render(surface,"drives-"+theme.Id);
                        surface.Width=440;surface.Height=320;Render(surface,"drives-small-"+theme.Id);
                    }
                    surface.Width=560;surface.Height=600;Render(surface,"drives-desktop");
                    surface.Width=780;surface.Height=492;Set(surface,"path",root);Set(surface,"root",root);Set(surface,"shown",tree);Set(surface,"drawing",null);Render(surface,"map");
                    source.Select(root);await Wait(()=>source.Snapshot?.Path==root&&source.Status=="","Données de navigation disponibles");
                    Render(surface,"map-live");var first=Field<DiskTile[]>(surface,"tiles").First(t=>t.Node.Directory);
                    Field<Stack<string?>>(surface,"history").Push(root);Invoke(surface,"Navigate",first.Node.Path,first.Bounds,true);
                    await Task.Delay(45);var incoming=Field<DrawingGroup>(surface,"drawing");Check(incoming.Transform.Value.M11<1||incoming.Transform.Value.M22<1,"Le zoom entrant part du rectangle ciblé");
                    await Wait(()=>!Field<bool>(surface,"animating"),"Fin du zoom entrant");Render(surface,"inside");Check(Field<string>(surface,"path")==folder,"Navigation au dossier choisi");
                    Invoke(surface,"Back");await Wait(()=>!Field<bool>(surface,"animating"),"Fin du zoom sortant");Render(surface,"returned");
                    Check(Field<string>(surface,"path")==root,"Retour au niveau précédent");
                    Check(Math.Abs((Field<DrawingGroup>(surface,"drawing").Transform?.Value.M11??1)-1)<.001,"Le retour restaure une carte à pleine taille");
                    Invoke(surface,"Navigate",folder,first.Bounds,true);surface.SetActive(false);Check(!Field<bool>(surface,"animating"),"Masquage arrête les transitions");
                }
                Check(DiskActivity.Busy((0,0),(30,100)) is {} busy&&Math.Abs(busy-.7)<1e-9,"Un volume inactif 30 % du temps est actif à 70 %");
                Check(DiskActivity.Busy((50,100),(50,100)) is null,"Un intervalle nul ne donne aucune charge");
                var counters=new System.Collections.Concurrent.ConcurrentDictionary<string,(long,long)>();
                using(var io=new DiskActivity(()=>[new(root,"Fixture",100000,50000)],path=>counters.TryGetValue(path,out var counter)?counter:null))
                using(var surface=new DisksSurface(new Station(root),new DiskIndex(()=>[new(root,"Fixture",100000,50000)]),io))
                {
                    surface.SetDisplayed(true);surface.SetActive(true);var source=Field<DiskIndex>(surface,"index");await Wait(()=>source.Volumes.Length>0,"Volumes détectés pour la charge d'E/S");
                    surface.Width=780;surface.Height=492;Render(surface,"io-idle");
                    // Incident mesuré : C: oscille entre 0 et 6 % au repos (sessions Claude Code)
                    // et tenait l'onde éveillée la moitié du temps.
                    counters[root]=(0,0);io.Sample();counters[root]=(94,100);io.Sample();surface.PourActivity();
                    Check(!Field<bool>(surface,"liquidHooked"),"Un disque à 6 % d'activité laisse le liquide au repos");
                    counters[root]=(194,200);io.Sample();surface.PourActivity();
                    Check(!Field<bool>(surface,"liquidHooked"),"Le retour à 0 % d'un disque calme ne relance pas de vague");
                    counters[root]=(224,300);io.Sample();surface.PourActivity();
                    Check(Field<bool>(surface,"liquidHooked"),"Une vraie charge d'E/S fait monter le liquide");
                    surface.SetActive(false);
                    var liquid=Field<Dictionary<string,QuotaLiquid>>(surface,"liquids")[root];
                    Check(!Field<bool>(surface,"liquidHooked")&&!liquid.Awake&&liquid.Level>.3,"Dock masqué : le liquide se pose à son niveau, sans boucle d'images");
                    surface.SetActive(true);Render(surface,"io-loaded");
                    surface.Width=560;surface.Height=180;Render(surface,"io-loaded-row");
                }
                var automatic=Enumerable.Range(0,4).Select(i=>new DiskVolume(Path.Combine(root,"Auto"+i),"Volume "+i,10000,9000)).ToArray();
                foreach(var volume in automatic){Directory.CreateDirectory(volume.Path);Write(Path.Combine(volume.Path,"fixture.bin"),100);}
                using(var auto=new DiskIndex(()=>automatic))
                {
                    auto.SetActive(true);
                    await Wait(()=>automatic.All(v=>auto.Info(v.Path)?.CompletedAt is not null),"Tous les volumes indexés depuis l'accueil sans sélection");
                    Check(auto.Snapshot is null,"L'indexation automatique ne change pas la vue sélectionnée");
                    var info=auto.Info(automatic[0].Path)!;
                    Check(!info.Pending&&!info.Scanning&&DisksSurface.ScanLabel(info).StartsWith("Scan · "),"La carte affiche l'heure du scan terminé");
                    // Tout volume NTFS a des zones refusées (System Volume Information) : le
                    // suffixe « partiel » permanent ne portait aucun signal sur la carte.
                    Check(DisksSurface.ScanLabel(info with{Partial=true})==DisksSurface.ScanLabel(info),"Les zones inaccessibles restent au survol, pas sur la carte");
                    // Incident réel : le watcher C: déborde en continu (sessions Claude), la
                    // réconciliation différée affichait « En attente » en permanence.
                    Check(DisksSurface.ScanLabel(info with{Pending=true}).StartsWith("Scan · "),"Une réconciliation différée n'affiche pas En attente");
                    Write(Path.Combine(automatic[0].Path,"fixture.bin"),200);
                    auto.Select(automatic[0].Path);await Wait(()=>auto.Snapshot?.Bytes==200,"Delta après indexation automatique");
                    Check(auto.Info(automatic[0].Path)!.CompletedAt==info.CompletedAt,"Un delta ne modifie pas l'heure du scan complet");
                    auto.Select(null);auto.SetActive(false);auto.SetActive(true);await Task.Delay(2800);
                    Check(Field<Dictionary<string,DiskVolumeIndex>>(auto,"indexes").Values.All(i=>i.FullScans==1),"Réafficher l'accueil conserve les quatre index");
                }
                using(var slots=new SemaphoreSlim(3,3))
                using(var started=new CountdownEvent(3))
                using(var release=new ManualResetEventSlim(false))
                {
                    var workers=automatic.Select(v=>new DiskVolumeIndex(v.Path,slots)).ToArray();int arrivals=0;
                    foreach(var worker in workers)worker.Changed+=()=>{if(worker.Info.Scanning&&worker.Scanned==0){if(Interlocked.Increment(ref arrivals)<=3)started.Signal();release.Wait();}};
                    try
                    {
                        foreach(var worker in workers)worker.SetActive(true);
                        await Wait(()=>started.IsSet,"Trois scans peuvent avancer en parallèle");
                        Check(workers.Count(w=>w.Info.Scanning)==3&&workers.Sum(w=>w.FullScans)==3,"Le quatrième scan attend une place");
                        release.Set();await Wait(()=>workers.All(w=>w.Info is {CompletedAt:not null,Scanning:false}),"La file reprend dès qu'un scan se termine");
                    }
                    finally{release.Set();foreach(var worker in workers)worker.Dispose();}
                }
                if(args.Contains("--scan-real"))
                {
                    using var real=new DiskIndex();using var process=System.Diagnostics.Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var watch=System.Diagnostics.Stopwatch.StartNew();real.SetActive(true);real.Select("E:\\");
                    await Wait(()=>real.Snapshot?.Path=="E:\\"&&real.Status=="","Analyse du volume E réel",180);process.Refresh();
                    Console.WriteLine($"REAL_SCAN elapsedMs={watch.ElapsedMilliseconds} cpuSeconds={(process.TotalProcessorTime-cpu).TotalSeconds:0.00} privateMB={process.PrivateMemorySize64/1048576d:0.0} bytes={real.Snapshot!.Bytes} entries={real.Scanned} partial={real.Snapshot.Partial}");
                    using var preview=new DisksSurface(new Station(root)){Width=960,Height=600};Set(preview,"path","E:\\");Set(preview,"root","E:\\");Set(preview,"shown",real.Snapshot);Render(preview,"real-e");
                    real.Select("D:\\");await Wait(()=>real.Snapshot?.Path=="D:\\"&&real.Status=="","Deuxième volume réel conservé",180);
                    var retained=Field<Dictionary<string,DiskVolumeIndex>>(real,"indexes");
                    real.Select(null);await Task.Delay(3000);process.Refresh();cpu=process.TotalProcessorTime;
                    long reads=retained.Values.Sum(i=>i.MetadataReads);var latency=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<20;i++){real.Select("E:\\");real.Select("D:\\");real.Select(null);}
                    latency.Stop();await Task.Delay(10000);process.Refresh();
                    Console.WriteLine($"REAL_RETAINED cpuSeconds10s={(process.TotalProcessorTime-cpu).TotalSeconds:0.000} privateMB={process.PrivateMemorySize64/1048576d:0.0} navigationMs={latency.Elapsed.TotalMilliseconds:0.000} additionalMetadataReads={retained.Values.Sum(i=>i.MetadataReads)-reads} fullScans={string.Join(',',retained.Values.Select(i=>i.FullScans))}");
                    Check(retained.Values.All(i=>i.FullScans==1),"Les volumes réels ne sont pas rescannés par la navigation");
                    real.SetActive(false);process.Refresh();cpu=process.TotalProcessorTime;await Task.Delay(10000);process.Refresh();Console.WriteLine($"REAL_PAUSED cpuSeconds10s={(process.TotalProcessorTime-cpu).TotalSeconds:0.000}");
                }
                Console.WriteLine($"DISKS_CHECKS_PASS {checks}");
            }
            catch(Exception e){Console.Error.WriteLine(e);result=1;}
            finally{try{Directory.Delete(root,true);if(Directory.Exists(otherRoot))Directory.Delete(otherRoot,true);}catch(IOException){}app.Shutdown();}
        };app.Run();return result;
    }
}
namespace Battlestation
{
    internal sealed class Station(string root){internal string Root=>root;}
    internal static class DesktopScreens{internal static Rect ToPixels(Rect rect)=>rect;}
    internal static class Native{internal static void BackgroundPanel(int slot,float x,float y,float w,float h){}}
}
