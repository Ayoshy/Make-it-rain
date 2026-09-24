using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;

internal static class AtelierTests
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void WaitUntil(Func<bool> condition)
    {
        var frame=new System.Windows.Threading.DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(10);
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(_,_)=>{if(condition()||DateTime.UtcNow>=deadline)frame.Continue=false;};timer.Start();
        try {System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{timer.Stop();}
        Check(condition(),"Lecture du prochain lancement terminée");
    }
    static void Render(Surface surface,string name)
    {
        var grid=new Grid{Width=surface.Width,Height=surface.Height,Background=new SolidColorBrush(Color.FromRgb(27,24,37))};grid.Children.Add(surface);
        grid.Measure(new Size(grid.Width,grid.Height));grid.Arrange(new Rect(0,0,grid.Width,grid.Height));grid.UpdateLayout();
        var image=new RenderTargetBitmap((int)grid.Width,(int)grid.Height,96,96,PixelFormats.Pbgra32);image.Render(grid);
        string output=Path.GetFullPath(Path.Combine("artifacts","validation","atelier",name));Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(output);png.Save(stream);grid.Children.Clear();
    }
    [STAThread] static int Main()
    {
        int result=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async (_,_)=>
        {
            string root=Path.Combine(Path.GetTempPath(),"Battlestation-atelier-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                string candidate=Path.Combine(root,"build","candidate");Directory.CreateDirectory(candidate);File.WriteAllText(Path.Combine(candidate,"Battlestation.exe"),"");
                string pointer=Path.Combine(root,"build","current.txt");File.WriteAllText(pointer,"build/previous");
                var store=new AtelierStore(root,Path.Combine(root,"data"));
                Check(!store.IsKeptBuild(candidate,store.ReadNextLaunch()),"Le candidat est distinct de la version conservée");
                using(var locked=File.Open(pointer,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    try {await store.KeepAsync(candidate);throw new Exception("La sélection verrouillée devait échouer");}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
                    Check(File.ReadAllText(pointer)=="build/previous","Une erreur conserve la sélection précédente intacte");
                }
                await store.KeepAsync(candidate);
                Check(store.ReadNextLaunch()=="build/candidate"&&store.IsKeptBuild(candidate+"\\",store.ReadNextLaunch()),"Un clic conserve exactement la version chargée");
                await store.KeepAsync(candidate);Check(store.ReadNextLaunch()=="build/candidate","Garder une version deux fois reste sans effet secondaire");
                foreach(string invalid in new[]{Path.Combine(root,"backups","candidate"),Path.Combine(root,"build","missing")})
                {
                    Directory.CreateDirectory(invalid);
                    try{await store.KeepAsync(invalid);throw new Exception("Le dossier invalide devait être refusé");}catch(InvalidDataException){}
                }
                Check(store.ReadNextLaunch()=="build/candidate","Un dossier absent ou archivé ne remplace pas la sélection");
                Check(!Directory.EnumerateFiles(Path.Combine(root,"build"),"*.tmp").Any(),"Aucun fichier temporaire après succès ou erreur");
                // Render-only fixture: the cleanup script is a no-op; real cleanup has separate tests.
                string scripts=Path.Combine(root,"scripts");Directory.CreateDirectory(scripts);
                File.WriteAllText(Path.Combine(scripts,"Clean-BattlestationBuilds.ps1"),"param($Root,$Data,$LoadedBuild,[switch]$Apply)\n'{\"reclaimableBytes\":7849589458,\"freedBytes\":0,\"removed\":0,\"protected\":[],\"errors\":[]}'");
                var surface=new AtelierSurface(new Station(root)){Width=440,Height=280};surface.SetDisplayed(true);surface.SetActive(true);
                WaitUntil(()=>!JsonSerializer.Serialize(surface.Inspect()).Contains("\"busy\":true"));
                foreach(var theme in DesktopTheme.Definitions){DesktopTheme.Select(theme.Id,false);Render(surface,"atelier-"+theme.Id+"-minimum.png");}
                var hits=((IEnumerable)typeof(Surface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!).Cast<ITuple>().Select(row=>(string)row[2]!).ToArray();
                Check(hits.Contains("AtelierKeep")&&hits.Contains("AtelierClean")&&hits.Contains("AtelierDetails")&&hits.Length==3,"Seulement garder, nettoyer et détails");
                File.WriteAllText(pointer,AppContext.BaseDirectory);surface.SetActive(false);surface.SetActive(true);
                WaitUntil(()=>JsonSerializer.Serialize(surface.Inspect()).Contains("\"keptBuild\":true"));
                surface.Width=592;Render(surface,"atelier-kept.png");surface.Dispose();Console.WriteLine("ATELIER_CHECKS_PASS");
            }
            catch(Exception e){Console.Error.WriteLine(e);result=1;}
            finally{try{Directory.Delete(root,true);}catch(IOException){}app.Shutdown();}
        };
        app.Run();return result;
    }
}
namespace Battlestation
{
    internal sealed class Station(string root){internal string Root=>root;internal string Data=>Path.Combine(root,"data");}
    internal static class DesktopScreens{internal static Rect ToPixels(Rect rect)=>rect;}
    internal static class Native{internal static void BackgroundPanel(int slot,float x,float y,float w,float h){}}
}
