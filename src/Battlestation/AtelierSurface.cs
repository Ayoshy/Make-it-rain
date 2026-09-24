using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Battlestation;

internal sealed class AtelierSurface : Surface,IDisposable
{
    readonly AtelierStore store;
    readonly DispatcherTimer poll;
    readonly string loadedBuild=AppContext.BaseDirectory;
    readonly string loadedBuildName=Path.GetFileName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
    string nextLaunch="",error="",cleanupStatus="";
    bool displayed,active,reading,working,disposed,known;
    BuildCleanup? cleanup;
    Task pending=Task.CompletedTask;
    AtelierArtwork artwork=null!;
    internal Task PendingSave=>pending.ContinueWith(_=>{},TaskScheduler.Default);
    bool Kept=>known&&store.IsKeptBuild(loadedBuild,nextLaunch);
    internal AtelierSurface(Station station):base(station,20)
    {
        Width=560;Height=280;store=new(station.Root,station.Data);
        poll=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(8)};
        poll.Tick+=(_,_)=>_=ReadAsync();
        DesktopTheme.Changed+=ApplyTheme;ApplyTheme();
    }
    void ApplyTheme(){artwork=new AtelierArtwork(DesktopTheme.Current);Refresh();}
    internal override void SetDisplayed(bool value){base.SetDisplayed(value);displayed=value;UpdatePolling();}
    internal void SetActive(bool value){active=value;UpdatePolling();}
    void UpdatePolling()
    {
        if(!displayed||!active||disposed){poll.Stop();return;}
        if(!poll.IsEnabled){poll.Start();_=ReadAsync();if(cleanup is null&&!working)Run(false,false);}
    }
    async Task ReadAsync()
    {
        if(reading||disposed)return;reading=true;
        try { string next=await Task.Run(store.ReadNextLaunch);if(!disposed){nextLaunch=next;known=true;} }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException)
        { if(!disposed){known=false;error=e.Message;} }
        finally {reading=false;if(!disposed)Refresh();}
    }
    void Run(bool keep,bool clean)
    {
        if(working||disposed)return;
        working=true;error="";cleanupStatus=clean?"Nettoyage…":keep?"Enregistrement…":"Calcul de l’espace…";Refresh();
        _=WorkAsync(keep,clean);
    }
    async Task WorkAsync(bool keep,bool clean)
    {
        try
        {
            if(keep){pending=store.KeepAsync(loadedBuild);await pending;if(disposed)return;await ReadAsync();}
            var task=store.CleanupAsync(loadedBuild,clean);pending=task;
            var result=await task;
            if(!disposed)
            {
                cleanup=result;
                cleanupStatus=clean?$"{result.Removed} build(s) supprimé(s) · {Size(result.FreedBytes)} libérés":"";
                error=string.Join("\n",result.Errors);
            }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        { if(!disposed){error=e.Message;cleanupStatus="";} }
        finally {working=false;if(!disposed)Refresh();}
    }
    static string Size(long bytes)=>bytes>=1024L*1024*1024?$"{bytes/(1024d*1024*1024):0.0} Go":$"{bytes/(1024d*1024):0} Mo";
    protected override void Paint()
    {
        Header("ATELIER");
        Button("AtelierDetails","Détails",Width-100,10,76,28,ShowDetails,8);
        D.PushClip(new RectangleGeometry(new Rect(1,1,Width-2,Height-2),23,23));
        D.PushOpacity(.65);D.DrawEllipse(artwork.Glow,null,new(55,82),114,96);D.Pop();D.Pop();
        D.PushTransform(new TranslateTransform(20,48));D.PushTransform(new ScaleTransform(64d/104,64d/104));D.DrawDrawing(artwork.Emblem);D.Pop();D.Pop();
        Text(!known?"Lecture de la version…":Kept?"Version conservée":"Version à l’essai",102,53,17,Ink,width:Width-126);
        Text(loadedBuildName,102,85,9,Muted,width:Width-126);
        double y=Height-146;
        Button("AtelierKeep",Kept?"Version conservée":"Garder cette version",24,y,Width-48,42,()=>Run(true,false),11,Ink,!working&&known&&!Kept,radius:21);
        Button("AtelierClean","Nettoyer les builds",24,y+54,Width-48,36,()=>Run(false,true),10,Ink,!working,radius:18);
        string message=error.Length>0?"Action incomplète · voir Détails":cleanupStatus.Length>0?cleanupStatus:cleanup is null?"":$"{Size(cleanup.ReclaimableBytes)} libérables · versions utilisées protégées";
        Text(message,Width/2,Height-38,8,error.Length>0?"#FFC98F":Muted,align:"center",width:Width-48);
    }
    void ShowDetails()=>MessageBox.Show(Window.GetWindow(this),$"Version ouverte :\n{loadedBuild}\n\nProchain lancement :\n{(nextLaunch.Length>0?nextLaunch:"Non défini")}\n\nBuilds protégés :\n{string.Join("\n",cleanup?.Protected??[])}"+(error.Length>0?$"\n\nDernière erreur :\n{error}":""),"Versions de Battlestation",MessageBoxButton.OK,MessageBoxImage.None);
    protected override void OnPointer(MouseEventArgs e){ToolTip=error.Length>0?error:null;}
    internal object Inspect()=>new{active,displayed,busy=reading||working,error,loadedBuild,loadedBuildName,nextLaunch,keptBuild=Kept,cleanup,cleanupStatus};
    public void Dispose(){disposed=true;poll.Stop();DesktopTheme.Changed-=ApplyTheme;}
}
