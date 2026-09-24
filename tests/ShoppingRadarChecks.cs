using System.IO;
using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using Battlestation;
using Battlestation.Shopping;

// Real modal controls and the radar, with a local engine only: no store, network,
// key, desktop restart or browser launch. This does not prove physical clicks.
internal static class ShoppingRadarChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}

    internal static string[] DrawnTexts(Surface dock)=>((IDictionary)typeof(Surface)
        .GetField("textCache",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!)
        .Keys.Cast<ITuple>().Select(key=>(string)key[0]!).ToArray();

    internal static string HoverDetails(ShoppingSurface dock,Point point)
    {
        typeof(Surface).GetField("Pointer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dock,point);
        typeof(ShoppingSurface).GetMethod("UpdateToolTip",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dock,null);
        return (dock.ToolTip as TextBlock)?.Text??"";
    }

    internal static string HoverRow(ShoppingSurface dock,int index)
    {
        var hits=(IEnumerable)typeof(Surface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
        var hit=hits.Cast<ITuple>().Single(item=>(string)item[2]! =="ShoppingOpen:"+index);
        var rect=(Rect)hit[0]!;return HoverDetails(dock,new Point(rect.Left+10,rect.Top+Math.Min(12,rect.Height/2)));
    }

    internal static void Loading(Station station,Action<Surface,string> render,Func<Surface,byte[]> pixels)
    {
        var engine=station.ShoppingEngine;
        engine.SearchHold=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=station.Shopping.SearchAsync("frigo max 800 €, no frost, 300 L, blanc");
        WaitUntil(()=>engine.Busy&&engine.Progress.Count>0);
        engine.SeedProgress("Étude de la demande","Préparation des requêtes","Recherche web : réfrigérateur 300 L",
            "Résultats web reçus","Lecture : fabricant.example","Lecture : boutique.example","Fiches réunies","Comparaison DeepSeek");
        var app=Application.Current;var shutdownMode=app.ShutdownMode;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        using var dock=new ShoppingSurface(station){Width=700,Height=560};
        // Logical WPF visibility without exposing a window or taking focus on the desktop.
        var window=new Window{Content=dock,Width=720,Height=600,Left=-10000,Top=-10000,Opacity=0,ShowActivated=false,ShowInTaskbar=false};
        var timer=(DispatcherTimer)typeof(ShoppingSurface).GetField("loadingTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
        var motion=(TranslateTransform)typeof(ShoppingSurface).GetField("loadingMotion",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
        try
        {
            window.Show();WaitUntil(()=>timer.IsEnabled);
            render(dock,"shopping-loading.png");
            var text=DrawnTexts(dock);
            Check(text.Contains("RECHERCHE EN COURS")&&text.Contains("Comparaison DeepSeek")&&text.Contains("Lecture : boutique.example"),"Loading shows the active stage and recent real progress messages");
            Check(text.Contains("Étude de la demande")&&!text.Contains("6 / 8"),"A 560 px tabbed dock shows all eight available stages instead of an arbitrary six");
            engine.SeedProgress(Enumerable.Range(1,24).Select(i=>$"Étape {i:00} · Lecture des caractéristiques").ToArray());
            dock.Height=1160;window.Height=1200;dock.Refresh();render(dock,"shopping-loading-tall.png");
            Check(DrawnTexts(dock).Contains("Étape 01 · Lecture des caractéristiques")&&DrawnTexts(dock).Contains("Étape 24 · Lecture des caractéristiques"),"A tall dock displays more than twelve journal stages");
            dock.Height=460;window.Height=500;dock.Refresh();render(dock,"shopping-loading-compact.png");
            Check(DrawnTexts(dock).Contains("5 / 24"),"A compact dock keeps the latest lines with a bottom margin");
            dock.Height=560;window.Height=600;dock.Refresh();
            Check(station.Shopping.Results.Count==3,"An active search preserves the previous candidates in memory");
            var before=pixels(dock);double previous=motion.X;
            WaitUntil(()=>Math.Abs(motion.X-previous)>30);
            Check(!before.AsSpan().SequenceEqual(pixels(dock)),"The indeterminate loading bar visibly moves while the search waits");
            WaitUntil(()=>motion.X>200);render(dock,"shopping-loading-shimmer.png");
            long renders=dock.RenderCount;var elapsed=System.Diagnostics.Stopwatch.StartNew();
            using(var process=System.Diagnostics.Process.GetCurrentProcess())
            {
                var cpu=process.TotalProcessorTime;double start=motion.X;
                WaitUntil(()=>elapsed.ElapsedMilliseconds>=450);
                Check(Math.Abs(motion.X-start)>20&&dock.RenderCount-renders<=2,"The shimmer moves without repainting all text each frame");
                Console.WriteLine($"Shopping shimmer: {elapsed.ElapsedMilliseconds} ms, {dock.RenderCount-renders} full paints, {(process.TotalProcessorTime-cpu).TotalMilliseconds:F0} ms process CPU (WPF fixture).");
            }
            dock.SetDisplayed(false);Check(!timer.IsEnabled&&!motion.HasAnimatedProperties,"A hidden dock stops both its duration timer and animation");
            dock.SetDisplayed(true);WaitUntil(()=>timer.IsEnabled);
            window.Hide();Check(!timer.IsEnabled&&!motion.HasAnimatedProperties,"An unloaded or invisible window stops its animation");
            window.Show();WaitUntil(()=>timer.IsEnabled);
            engine.SearchHold.SetResult();WaitUntil(()=>pending.IsCompleted&&!timer.IsEnabled);pending.GetAwaiter().GetResult();
            render(dock,"shopping-after-loading.png");
            Check(station.ShoppingTabs.Active.CompletedAt is not null,"A finished search marks its tab complete");
            var pulses=(IDictionary)typeof(ShoppingSurface).GetField("tabPulses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
            var pulse=(DrawingGroup)((ITuple)pulses.Values.Cast<object>().Single())[2]!;
            Check(pulse.HasAnimatedProperties,"The visible completed tab starts a short compositor pulse");
            double opacity=pulse.Opacity;WaitUntil(()=>Math.Abs(pulse.Opacity-opacity)>.01);
            dock.SetDisplayed(false);Check(!pulse.HasAnimatedProperties,"A hidden dock releases completion animation clocks");
            dock.SetDisplayed(true);dock.Refresh();render(dock,"shopping-completed-tab.png");
            Check(station.Shopping.Results.Count==3,"Completed loading restores the candidate view");
            Click(dock,"ShoppingJournal");render(dock,"shopping-journal.png");
            Check(DrawnTexts(dock).Contains("RECHERCHE TERMINÉE"),"The completed search journal remains available from its button");
            Click(dock,"ShoppingJournal");

            engine.SearchHold=new(TaskCreationOptions.RunContinuationsAsynchronously);
            engine.SearchFailure="Tavily limite temporairement les recherches gratuites. Réessayez plus tard.";
            pending=station.Shopping.SearchAsync("frigo max 800 €");WaitUntil(()=>engine.Busy);
            engine.SeedProgress("Étude de la demande","Préparation des requêtes","Recherche web : réfrigérateur 300 L","Interrogation de Tavily");
            engine.SearchHold.SetResult();WaitUntil(()=>pending.IsCompleted&&!timer.IsEnabled);pending.GetAwaiter().GetResult();
            app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
            render(dock,"shopping-loading-failed.png");
            Check(DrawnTexts(dock).Contains("RECHERCHE ÉCHOUÉE")&&DrawnTexts(dock).Contains(engine.SearchFailure),"A failed search automatically keeps its identified failure and journal visible");
            var duration=station.Shopping.Elapsed;
            bool nextTurn=false;app.Dispatcher.BeginInvoke(new Action(()=>nextTurn=true));WaitUntil(()=>nextTurn);
            Check(station.Shopping.Elapsed==duration,"A completed search duration stops increasing");

            engine.SearchFailure="";engine.SearchHold=new(TaskCreationOptions.RunContinuationsAsynchronously);
            pending=station.Shopping.SearchAsync("frigo max 800 €");WaitUntil(()=>timer.IsEnabled);
            dock.Dispose();Check(!timer.IsEnabled&&!motion.HasAnimatedProperties,"Disposing the shopping surface releases its timer and animation");
            engine.PublishProgress("Lecture des fiches");Check(!timer.IsEnabled,"Later engine events cannot restart a disposed surface");
            engine.SearchHold.SetResult();WaitUntil(()=>pending.IsCompleted);pending.GetAwaiter().GetResult();
        }
        finally
        {
            engine.SearchHold?.TrySetResult();engine.SearchHold=null;engine.SearchFailure="";
            window.Close();app.ShutdownMode=shutdownMode;
        }
    }

    internal static void Click(Surface dock,string name)
    {
        var hits=(IEnumerable)typeof(Surface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
        var hit=hits.Cast<ITuple>().Single(item=>(string)item[2]! ==name);
        ((Action)hit[1]!)();
    }

    internal static void Submit(ShoppingSurface dock,string request,string effort="max")
    {
        var shutdownMode=Application.Current.ShutdownMode;
        Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        Application.Current.Dispatcher.BeginInvoke(new Action(()=>
        {
            var window=Application.Current.Windows.Cast<Window>().Single(item=>item.Title=="Battlestation · Achats");
            var content=(StackPanel)((Border)window.Content).Child;
            var input=content.Children.OfType<TextBox>().Single();
            input.Text=request;
            var levels=content.Children.OfType<DockPanel>().Single().Children.OfType<ComboBox>().Single();
            levels.SelectedValue=effort;
            if(effort=="high")
            {
                levels.ApplyTemplate();levels.IsDropDownOpen=true;window.UpdateLayout();
                var popup=(System.Windows.Controls.Primitives.Popup)levels.Template.FindName("PART_Popup",levels);
                Check(popup.IsOpen&&levels.Items.Count==4,"The reasoning dropdown opens with all four levels");
                var popupBody=(FrameworkElement)popup.Child;popupBody.UpdateLayout();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(popupBody.ActualWidth),(int)Math.Ceiling(popupBody.ActualHeight),96,96,PixelFormats.Pbgra32);
                bitmap.Render(popupBody);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using(var file=File.Create("artifacts/validation/dock-review/shopping-reasoning-options.png"))png.Save(file);
                levels.IsDropDownOpen=false;
            }
            var actions=content.Children.OfType<StackPanel>().Single();
            var button=actions.Children.OfType<Button>().Single(item=>(string)item.Content=="Chercher");
            Check(button.IsEnabled,"Typing a request enables the modal search button");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        try{typeof(ShoppingSurface).GetMethod("Edit",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dock,null);}
        finally{Application.Current.ShutdownMode=shutdownMode;}
    }

    internal static void ParallelSearches(Station station,Action<Surface,string> render)
    {
        using var tabs=station.ShoppingTabs;
        using var dock=new ShoppingSurface(station){Width=700,Height=640};
        var first=tabs.Active;
        station.ShoppingEngine.SearchHold=new(TaskCreationOptions.RunContinuationsAsynchronously);
        Submit(dock,"Réfrigérateur pour la cuisine","max");
        WaitUntil(()=>first.Radar.Busy&&first.Radar.Progress.Count>0);
        var second=tabs.Add();
        var engine=station.ExtraShoppingEngines.Last();engine.SearchHold=new(TaskCreationOptions.RunContinuationsAsynchronously);
        Submit(dock,"Aspirateur robot pour le salon","high");
        WaitUntil(()=>engine.Busy&&second.Radar.Progress.Count>0);
        Check(first.Radar.Busy&&second.Radar.Busy&&tabs.AnyBusy,"Two tabs execute concurrently without the global Busy guard");
        Check(first.Radar.Query.StartsWith("Réfrigérateur")&&second.Radar.Query.StartsWith("Aspirateur"),"Each tab retains its own request");
        Check(first.Radar.ReasoningEffort=="max"&&second.Radar.ReasoningEffort=="high","The modal reasoning selection is independent for each tab");
        first.Radar.SetReasoningEffort("low");
        Check(first.Radar.ReasoningEffort=="max","A running search keeps the reasoning level it started with");
        station.ShoppingEngine.PublishProgress("Fiche du frigo");engine.PublishProgress("Fiche du robot");
        Check(!first.Radar.Progress.Any(step=>step.Message=="Fiche du robot")&&!second.Radar.Progress.Any(step=>step.Message=="Fiche du frigo"),"Parallel progress does not cross between tabs");
        render(dock,"shopping-tabs-running.png");
        engine.SearchHold.SetResult();WaitUntil(()=>!second.Radar.Busy);
        Check(first.Radar.Busy&&second.Radar.Results.Count==3,"One tab can finish while the other continues");
        WaitUntil(()=>second.CompletedAt is not null);
        dock.Refresh();render(dock,"shopping-tabs-results.png");
        tabs.Select(first.Id);dock.Refresh();render(dock,"shopping-tabs-first.png");Click(dock,"ShoppingStop");
        WaitUntil(()=>!first.Radar.Busy);
        Check(first.Radar.Status=="Recherche arrêtée"&&second.Radar.Results.Count==3,"Stopping a tab cancels only its own search");
        Check(first.CompletedAt is null,"Cancellation does not masquerade as a successful completion");
        tabs.Select(second.Id);
        station.Shopping.Watch(second.Radar.Results[0],second.Radar.Query).GetAwaiter().GetResult();
        Check(station.Shopping.Watchlist.Single().Item.Watch.Request==second.Radar.Query,"Shared watches retain the request of the tab that added them");
        tabs.Close(first.Id);Check(tabs.Active==second&&tabs.Items.Count==1,"Closing another tab preserves the selected results");
        var third=tabs.Add();var thirdEngine=station.ExtraShoppingEngines.Last();
        thirdEngine.SearchHold=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=third.Radar.SearchAsync("Troisième recherche");WaitUntil(()=>thirdEngine.Busy);
        tabs.Close(third.Id);WaitUntil(()=>pending.IsCompleted);pending.GetAwaiter().GetResult();
        Check(tabs.Active==second&&!tabs.AnyBusy,"Closing a running tab cancels it without leaving a background search");
        for(int i=0;i<6;i++)tabs.Add();
        dock.Width=440;dock.Refresh();render(dock,"shopping-tabs-overflow.png");
        Click(dock,"ShoppingTabPrevious");
        Check(tabs.Active!=tabs.Items[^1],"Overflow arrows can select tabs outside the visible strip");
        foreach(var tab in tabs.Items.ToArray())tabs.Close(tab.Id);
        Check(tabs.Items.Count==1&&tabs.Active.Radar.Query.Length==0,"Closing the last tab leaves one empty search");
        Console.WriteLine("PASS: concurrent shopping tabs, modal reasoning, isolated stop/close and shared watches.");
    }

    internal static void SerperSettings(Station station)
    {
        using var tabs=station.ShoppingTabs;using var dock=new ShoppingSurface(station);
        const string fake="fixture-serper-secret-not-real";
        var app=Application.Current;var mode=app.ShutdownMode;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var path=(string)typeof(SerperKeyStore).GetField("path",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(station.Serper)!;
        try
        {
            app.Dispatcher.BeginInvoke(new Action(()=>
            {
                var window=app.Windows.Cast<Window>().Single(item=>item.Title=="Battlestation · Connexion Serper");
                var content=(StackPanel)((Border)window.Content).Child;var field=content.Children.OfType<PasswordBox>().Single();
                Check(field.Password.Length==0,"The API key field starts empty and masked");field.Password=fake;
                var save=content.Children.OfType<StackPanel>().Single().Children.OfType<Button>().Single(button=>(string)button.Content=="Enregistrer");
                Check(save.IsEnabled,"Pasting a key enables Save");
                window.UpdateLayout();
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
                image.Render(window);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using(var file=File.Create("artifacts/validation/dock-review/shopping-serper-settings.png"))png.Save(file);
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            dock.ConfigureSearch();
            Check(station.Serper.HasKey&&new SerperKeyStore(Path.GetDirectoryName(path)!).Read()==fake,"Saving persists the key for the same Windows user");
            Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(fake),"The key file contains protected data, not plaintext");
            Console.WriteLine("PASS: masked Serper key entry and Windows-protected persistence; no network.");
        }
        finally
        {
            app.ShutdownMode=mode;File.Delete(path);Directory.Delete(Path.GetDirectoryName(path)!);
        }
    }

    internal static void WaitUntil(Func<bool> condition)
    {
        if(condition())return;
        var frame=new DispatcherFrame();
        var deadline=DateTime.UtcNow.AddSeconds(8);
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(5)};
        timer.Tick+=(_,_)=>{if(condition()||DateTime.UtcNow>=deadline)frame.Continue=false;};
        timer.Start();
        try{Dispatcher.PushFrame(frame);}
        finally{timer.Stop();}
        Check(condition(),"The radar operation completed without blocking the dispatcher");
    }

    internal static void Run()
    {
        var uiThread=Environment.CurrentManagedThreadId;
        using var engine=new Engine(uiThread);
        using var radar=new ShoppingRadar(engine,Path.GetTempPath(),Dispatcher.CurrentDispatcher,_=>{});
        WaitUntil(()=>radar.Revision>0);

        engine.SearchReleased.Reset();
        var search=radar.SearchAsync("  casque max 300 €  ");
        Check(radar.Busy,"Busy starts before the engine has parsed the request");
        Check(radar.Query=="casque max 300 €","The submitted request is retained and trimmed");
        WaitUntil(()=>engine.SearchEntered.IsSet);
        Check(radar.Status=="Étude de la demande…","The parser stage replaces the generic search status");
        engine.PublishActivity("Lecture des fiches…");
        Check(radar.Status=="Lecture des fiches…","The radar exposes the latest running stage");
        radar.SearchAsync("second request").GetAwaiter().GetResult();
        Check(engine.SearchCount==1&&radar.Query=="casque max 300 €","A second click cannot start or replace a running search");
        bool responsive=false;
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>responsive=true));
        WaitUntil(()=>responsive);
        Check(!search.IsCompleted,"The dispatcher stays usable while synchronous engine work is blocked");
        engine.SearchReleased.Set();
        WaitUntil(()=>search.IsCompleted);
        search.GetAwaiter().GetResult();
        Check(!radar.Busy&&radar.Results.Count==1&&radar.Error.Length==0,"The search publishes its result and clears Busy");
        Check(!radar.Status.Contains("Lecture des fiches"),"A completed search returns to its result summary");
        Check(engine.LastRequest==radar.Query,"The engine receives the submitted request");

        radar.Watch(radar.Results[0]).GetAwaiter().GetResult();
        Check(radar.Watchlist.Count==1&&radar.Results[0].Watched,"Adding a watch updates both the row and watchlist");
        Task.Run(()=>engine.PublishPrice(240m)).GetAwaiter().GetResult();
        WaitUntil(()=>radar.Watchlist[0].Price==240m);
        Check(radar.Watchlist[0].Item.Watch.LastCheck==engine.CheckedAt,"An automatic engine change republishes the latest watch snapshot");

        engine.WatchReadEntered.Reset();engine.WatchReadReleased.Reset();
        radar.Reload();
        WaitUntil(()=>engine.WatchReadEntered.IsSet);
        responsive=false;
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>
        {
            responsive=radar.Results.Count==1&&radar.Watchlist[0].Price==240m;
        }));
        WaitUntil(()=>responsive);
        engine.WatchReadReleased.Set();
        radar.RecheckAsync().GetAwaiter().GetResult();
        Check(engine.Forced,"Manual recheck bypasses the automatic cadence");
        radar.Unwatch(engine.Product.Id).GetAwaiter().GetResult();
        Check(radar.Watchlist.Count==0&&!radar.Results[0].Watched,"Removing a watch updates both cached views");
        Check(!engine.TouchedUiThread,"Search and watch persistence never run on the dispatcher thread");

        engine.SearchFailure=new IOException("fixture search failed");
        radar.SearchAsync("failed request").GetAwaiter().GetResult();
        Check(!radar.Busy&&radar.Error=="fixture search failed","A failed search clears Busy and reports its error");
        engine.SearchFailure=null;
        radar.SearchAsync("retry").GetAwaiter().GetResult();
        Check(!radar.Busy&&radar.Error.Length==0,"A new search succeeds after a failed one");
        Console.WriteLine("PASS: shopping automatic watch refresh, background persistence, Busy guard and retry.");
    }

    sealed class Engine(int uiThread) : IShoppingEngine
    {
        public readonly Product Product=new("test:1","test","https://example.test/headphones","Casque","","","","Fixture",280m);
        readonly Lock gate=new();
        IReadOnlyList<WatchedItem> watches=[];
        public readonly ManualResetEventSlim SearchEntered=new(false),SearchReleased=new(true),WatchReadEntered=new(false),WatchReadReleased=new(true);
        public int SearchCount;
        public string LastRequest="";
        public Exception? SearchFailure;
        public bool TouchedUiThread,Forced;
        public DateTimeOffset CheckedAt;
        public bool Busy=>false; // Parsing can begin before the service reports Busy.
        public string Activity{get;private set;}="";
        public IReadOnlyList<ShoppingProgress> Progress{get;private set;}=[];
        public DateTimeOffset? SearchStartedAt{get;private set;}
        public IReadOnlyList<SourceReport> Sources=>[];
        public ShoppingSettings Settings=>ShoppingSettings.Default;
        public event Action? Changed;
        public event Action<ShoppingAlert>? Alert;
        void Touch(){if(Environment.CurrentManagedThreadId==uiThread)TouchedUiThread=true;}
        public IReadOnlyList<WatchedItem> Watchlist
        {
            get
            {
                Touch();WatchReadEntered.Set();
                if(!WatchReadReleased.Wait(TimeSpan.FromSeconds(6)))throw new TimeoutException("Blocked fixture read was not released");
                lock(gate)return watches;
            }
        }
        public Task<SearchOutcome> SearchAsync(string request,CancellationToken cancellation)
        {
            Touch();Interlocked.Increment(ref SearchCount);LastRequest=request;SearchStartedAt=DateTimeOffset.Now;Progress=[];PublishActivity("Étude de la demande…");SearchEntered.Set();
            if(!SearchReleased.Wait(TimeSpan.FromSeconds(6)))throw new TimeoutException("Blocked fixture search was not released");
            if(SearchFailure is {} failure)throw failure;
            var spec=new ShoppingSpec("casque",[],300m,[],[],request);
            ShoppingHit hit=new(Product,new(Product.Id,DateTimeOffset.Now,280m,"EUR",true,"test"),Verdict.Unknown,[]);
            return Task.FromResult(new SearchOutcome([hit],[],spec,request));
        }
        public void Watch(Product product,decimal? targetPrice,string request)
        {
            Touch();
            lock(gate)watches=[new(new("watch:1",request,product.Id,targetPrice,DateTimeOffset.Now,DateTimeOffset.Now,DateTimeOffset.Now.AddMinutes(30),0,false,product.Price,product.Title,product.Shop),product)];
            Changed?.Invoke();
        }
        public void Unwatch(string watchId){Touch();lock(gate)watches=[];Changed?.Invoke();}
        public bool IsWatched(string productId)=>throw new Exception("The radar must read one snapshot, not the watchlist for each result");
        public void PublishPrice(decimal price)
        {
            lock(gate)
            {
                CheckedAt=DateTimeOffset.Now;
                watches=watches.Select(item=>item with{Watch=item.Watch with{LastPrice=price,LastCheck=CheckedAt}}).ToArray();
            }
            Changed?.Invoke();
        }
        public void PublishActivity(string text){Activity=text;Progress=Progress.Append(new ShoppingProgress(DateTimeOffset.Now,text)).TakeLast(12).ToArray();Changed?.Invoke();}
        public Task CheckWatchesAsync(CancellationToken cancellation,bool force=false){Touch();Forced=force;Alert?.Invoke(new("fixture","fixture",Product.Url,240m));Changed?.Invoke();return Task.CompletedTask;}
        public void Apply(ShoppingSettings settings){Touch();}
        public void Start(){}
        public void Stop(){}
        public void Dispose(){SearchReleased.Set();WatchReadReleased.Set();}
    }
}
