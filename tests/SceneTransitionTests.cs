using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Battlestation;

// Headless scene changes: no window is ever shown, the docks are plain WPF windows.
internal static class SceneTransitionTests
{
    internal static void Run(Action<bool,string> check)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;
        var docks=new List<Window>();
        for(int index=0;index<5;index++)docks.Add(new Window{Top=100+index*120,Left=index*40,Content=new Grid()});
        var stage=docks.Select(dock=>new SceneDock(dock,true)).ToList();
        var glass=new List<double>();
        bool concealed=false,concealedBeforeApply=false;
        int applied=0;
        double visibleDuringApply=double.NaN;
        var scene=new SceneTransition(dispatcher,()=>stage.ToArray(),alpha=>glass.Add(alpha),()=>concealed=true);
        scene.Request(()=>{applied++;concealedBeforeApply=concealed;visibleDuringApply=docks.Max(dock=>dock.Opacity);});
        Pump(dispatcher,()=>!scene.Busy,2000);
        check(applied==1&&concealedBeforeApply,"La scène demandée est appliquée une fois, après la sortie");
        check(visibleDuringApply==0,"La disposition change quand plus rien n'est dessiné");
        check(docks.All(dock=>dock.Opacity==1&&dock.IsHitTestVisible&&(dock.Content as Grid)!.RenderTransform is null),"Les docks reviennent visibles, saisissables et sans décalage");
        check(glass.Count>4&&glass[0]==1&&glass.Min()==0&&glass[^1]==1&&scene.GlassAlpha==1,"Le verre natif se dissout et revient à un");

        bool swapped=false;double[]? entering=null;
        scene.Request(()=>swapped=true);
        Pump(dispatcher,()=>{entering??=swapped&&docks[0].Opacity>0?docks.Select(dock=>dock.Opacity).ToArray():null;return !scene.Busy;},2000);
        check(entering is not null&&entering[0]>entering[^1],"La cascade entre par le haut et finit par le bas");

        int replaced=0,destination=0;
        scene.Request(()=>replaced++);
        Pump(dispatcher,()=>false,40);
        scene.Request(()=>destination++);
        Pump(dispatcher,()=>!scene.Busy,2000);
        check(replaced==0&&destination==1,"Une scène choisie pendant la sortie remplace la précédente");

        stage[3]=new SceneDock(docks[3],false);stage[4]=new SceneDock(docks[4],false);
        swapped=false;double[]? reduced=null;
        scene.Request(()=>swapped=true);
        Pump(dispatcher,()=>{reduced??=swapped&&docks[0].Opacity>0?docks.Select(dock=>dock.Opacity).ToArray():null;return !scene.Busy;},2000);
        check(reduced is not null&&reduced[0]>0&&reduced[3]==0&&reduced[4]==0,"Un bloc absent de la scène n'apparaît pas pendant l'entrée");
        check(docks.All(dock=>dock.Opacity==1&&dock.IsHitTestVisible),"Tous les blocs restent saisissables après la scène suivante");

        bool failed=false;
        void Failure(object? sender,DispatcherUnhandledExceptionEventArgs args){failed=true;args.Handled=true;}
        dispatcher.UnhandledException+=Failure;
        try
        {
            scene.Request(()=>throw new InvalidOperationException("Scène en panne"));
            Pump(dispatcher,()=>!scene.Busy,2000);
        }
        finally{dispatcher.UnhandledException-=Failure;}
        check(failed,"Une erreur du changement de scène remonte au bureau");
        check(docks.All(dock=>dock.Opacity==1),"Une erreur ne laisse pas les docks invisibles");
        check(docks.All(dock=>dock.IsHitTestVisible),"Une erreur ne laisse pas les docks insaisissables");
        check(docks.All(dock=>(dock.Content as Grid)!.RenderTransform is null),"Une erreur ne laisse pas de décalage");
        check(scene.GlassAlpha==1&&!scene.Busy,"Une erreur laisse le verre natif et la scène au repos");
        scene.Dispose();
    }

    static void Pump(Dispatcher dispatcher,Func<bool> done,double milliseconds)
    {
        var frame=new DispatcherFrame();
        var watch=Stopwatch.StartNew();
        var timer=new DispatcherTimer(TimeSpan.FromMilliseconds(8),DispatcherPriority.Background,(_,_)=>{if(done()||watch.ElapsedMilliseconds>milliseconds)frame.Continue=false;},dispatcher);
        timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
    }
}
