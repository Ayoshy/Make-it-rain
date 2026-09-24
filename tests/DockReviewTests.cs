using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;

static class DockReviewTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static JsonElement Hits(Surface surface)=>JsonSerializer.SerializeToElement(surface.InspectHits());
    static bool Has(Surface surface,string name)=>Hits(surface).EnumerateArray().Any(h=>h.GetProperty("name").GetString()==name);
    static void Render(Surface surface,string file,FrameworkElement? under=null)
    {
        // A dock may own a layer under its drawings: the Montagne snow is one.
        var root=Layered(surface,under);
        root.Measure(new Size(surface.Width,surface.Height));
        root.Arrange(new Rect(0,0,surface.Width,surface.Height));root.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(surface.Width),(int)Math.Ceiling(surface.Height),96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var dc=background.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(29,22,40)),null,new Rect(0,0,surface.Width,surface.Height));
        image.Render(background);image.Render(root);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
        using var stream=File.Create(file);png.Save(stream);
        foreach(var hit in Hits(surface).EnumerateArray())
        {
            double x=hit.GetProperty("x").GetDouble(),y=hit.GetProperty("y").GetDouble();
            Check(x>=0&&y>=0&&x+hit.GetProperty("width").GetDouble()<=surface.Width&&y+hit.GetProperty("height").GetDouble()<=surface.Height,"Hit must fit inside dock");
        }
    }
    static FrameworkElement Layered(Surface surface,FrameworkElement? under)
    {
        if(under is null)return surface;
        // A WPF element keeps its parent: detach before reusing the same dock again.
        if(surface.Parent is System.Windows.Controls.Panel previous)previous.Children.Remove(surface);
        if(under.Parent is System.Windows.Controls.Panel previousUnder)previousUnder.Children.Remove(under);
        var stack=new System.Windows.Controls.Grid{Width=surface.Width,Height=surface.Height};
        stack.Children.Add(under);stack.Children.Add(surface);
        return stack;
    }
    static byte[] Pixels(Surface surface,FrameworkElement? under=null)
    {
        // A dock only repaints when it is invalidated; the fixture changes the data
        // behind the same dock, so every capture starts from a fresh render.
        surface.Refresh();
        var root=Layered(surface,under);
        root.Measure(new Size(surface.Width,surface.Height));
        root.Arrange(new Rect(0,0,surface.Width,surface.Height));root.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(surface.Width),(int)Math.Ceiling(surface.Height),96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var dc=background.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(29,22,40)),null,new Rect(0,0,surface.Width,surface.Height));
        image.Render(background);image.Render(root);
        int stride=image.PixelWidth*4;var data=new byte[stride*image.PixelHeight];image.CopyPixels(data,stride,0);return data;
    }
    static bool Different(byte[] first,byte[] second)=>first.Length!=second.Length||!first.AsSpan().SequenceEqual(second);
    // Moyenne d'une bande verticale : sert à prouver qu'un dessin atteint bien un bord.
    static double Strip(byte[] pixels,Size size,int x,int width,double fromHeight)
    {
        double sum=0;int count=0;
        for(int y=(int)(size.Height*fromHeight);y<(int)size.Height;y++)
        for(int column=x;column<Math.Min((int)size.Width,x+width);column++)
        {
            int index=(y*(int)size.Width+column)*4;
            sum+=(pixels[index]+pixels[index+1]+pixels[index+2])/3d;count++;
        }
        return count==0?0:sum/count;
    }
    static float[] MusicPeaks(Surface surface)=>(float[])typeof(DeskSurface).GetField("peaks",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
    // Feeds the same smoothing the audio tick publishes, without any capture device.
    static void FeedMusic(DeskSurface surface,float[] values)
    {
        var bands=(float[])typeof(DeskSurface).GetField("bands",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
        Array.Copy(values,bands,Math.Min(values.Length,bands.Length));
        var level=typeof(DeskSurface).GetMethod("MusicLevel",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var peaks=MusicPeaks(surface);
        for(int i=0;i<peaks.Length;i++)peaks[i]=(float)level.Invoke(surface,[i/(double)(peaks.Length-1)])!;
    }
    static BitmapSource CoverFixture()
    {
        var visual=new DrawingVisual();
        using(var art=visual.RenderOpen())
        {
            art.DrawRectangle(new LinearGradientBrush(Color.FromRgb(38,96,58),Color.FromRgb(212,224,160),35),null,new Rect(0,0,300,300));
            art.DrawEllipse(new SolidColorBrush(Color.FromRgb(232,236,214)),null,new Point(190,120),72,72);
            art.DrawRectangle(new SolidColorBrush(Color.FromRgb(24,52,34)),null,new Rect(0,214,300,86));
        }
        var cover=new RenderTargetBitmap(300,300,96,96,PixelFormats.Pbgra32);
        cover.Render(visual);cover.Freeze();return cover;
    }
    static object? Private(object target,string name,params object?[] arguments)
        =>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,arguments);

    /// <summary>
    /// Le dock d'achat : demande vide, offres avec verdict, veille, ouverture de l'annonce.
    /// Les pixels prouvent le changement d'état, pas la lisibilité sur le bureau.
    /// </summary>
    static void ShoppingChecks(Station station,string output)
    {
        ShoppingRadarChecks.Run();
        using var dock=new ShoppingSurface(station){Width=700,Height=560};
        Render(dock,Path.Combine(output,"shopping-empty.png"));
        Check(Has(dock,"ShoppingEdit"),"Le champ de demande est cliquable");
        Check(Has(dock,"ShoppingSearch")==false,"Sans demande, rien à chercher");
        var empty=Pixels(dock);
        ShoppingRadarChecks.Submit(dock,"frigo max 800 €, no frost, 300 L, blanc");
        Check(station.Shopping.Query=="frigo max 800 €, no frost, 300 L, blanc","La demande saisie quitte la fenêtre et atteint le radar");
        ShoppingRadarChecks.WaitUntil(()=>!station.Shopping.Busy);
        dock.Refresh();
        Render(dock,Path.Combine(output,"shopping-results.png"));
        var shoppingTexts=ShoppingRadarChecks.DrawnTexts(dock);
        Check(shoppingTexts.Contains("À privilégier")&&shoppingTexts.Contains("À comparer")&&shoppingTexts.Contains("À vérifier")&&shoppingTexts.Contains("CHOIX")&&shoppingTexts.Contains("PISTES · À VÉRIFIER"),"Choix puis pistes partagent une liste avec deux titres");
        Check(shoppingTexts.Any(text=>text.Contains("Historique insuffisant"))&&shoppingTexts.Contains("100 % vérifiés")&&shoppingTexts.Contains("33 % vérifiés")&&!shoppingTexts.Contains("35 %"),"Le pourcentage reflète les critères confirmés, indépendamment de la confiance du verdict prix");
        Check(!Has(dock,"ShoppingPending")&&Has(dock,"ShoppingOpen:2"),"La piste est directement visible sous les deux choix, sans bouton de bascule");
        Check(shoppingTexts.Any(text=>text.StartsWith("Réserve : ")),"La réserve principale reste visible dans le dock");
        var details=ShoppingRadarChecks.HoverRow(dock,0);
        Check(details.Contains("Éléments relevés dans la fiche")&&details.Contains("La fiche indique 300 L et NoFrost.")&&details.Contains("Dimensions à confirmer")&&details.Contains("Prix · Prix bas observé"),"Le survol donne les preuves, réserves et motifs prix complets");
        Check(details.Contains("Critères demandés")&&details.Contains("300 L : confirmé")&&details.Contains("Autres offres observées"),"Le détail expose les contrôles de chaque critère et les offres alternatives");
        Check(ShoppingRadarChecks.HoverDetails(dock,new Point(5,5)).Length==0,"Quitter une offre efface son détail");
        Check(ShoppingRadarChecks.HoverRow(dock,2).Contains("1/3 critères confirmés")&&ShoppingRadarChecks.HoverRow(dock,2).Contains("Volume et type de froid"),"La piste explique son pourcentage et ses critères inconnus au survol");
        Check(Has(dock,"ShoppingSummary")&&ShoppingRadarChecks.HoverDetails(dock,new Point(80,132)).Contains("CRITÈRES DEMANDÉS"),"Le récapitulatif complet est disponible sans tronquer les critères");
        Check(Different(empty,Pixels(dock)),"Les offres remplacent l'état vide");
        Check(Has(dock,"ShoppingSearch"),"Une demande envoyée peut être relancée");
        Check(Has(dock,"ShoppingOpen:0")&&Has(dock,"ShoppingOpen:1"),"Chaque offre s'ouvre au clic");
        Check(Has(dock,"ShoppingWatch:0")&&Has(dock,"ShoppingWatch:1"),"Chaque offre peut être surveillée");
        var order=Hits(dock).EnumerateArray().Select(hit=>hit.GetProperty("name").GetString()!).ToArray();
        Check(Array.IndexOf(order,"ShoppingWatch:0")>Array.IndexOf(order,"ShoppingOpen:0"),"L'étoile garde la priorité sur l'ouverture de l'offre");
        var before=Pixels(dock);
        station.Shopping.Watch(station.Shopping.Results[0]).GetAwaiter().GetResult();
        dock.Refresh();
        Render(dock,Path.Combine(output,"shopping-watched.png"));
        Check(station.Shopping.Watchlist.Count==1,"Surveiller ajoute l'offre à la veille");
        Check(Has(dock,"ShoppingUnwatch:0"),"La veille affiche son retrait");
        var watchOrder=Hits(dock).EnumerateArray().Select(hit=>hit.GetProperty("name").GetString()!).ToArray();
        Check(Array.IndexOf(watchOrder,"ShoppingUnwatch:0")>Array.IndexOf(watchOrder,"ShoppingOpenWatch:0"),"Le retrait de veille garde la priorité sur l'ouverture");
        Check(Different(before,Pixels(dock)),"La veille change visiblement le dock");
        station.Shopping.Open(station.Shopping.Results[0]);
        Check(Station.Opened.Count==1&&Station.Opened[0]=="https://exemple.fr/frigo-1","Le clic ouvre l'annonce dans le navigateur");
        station.Shopping.Unwatch("fixture:1").GetAwaiter().GetResult();
        dock.Refresh();
        Check(station.Shopping.Watchlist.Count==0,"Retirer la veille la supprime");
        station.ShoppingEngine.AssessmentAvailable=false;
        station.Shopping.SearchAsync(station.Shopping.Query).GetAwaiter().GetResult();
        Render(dock,Path.Combine(output,"shopping-pending-automatic.png"));
        Check(Has(dock,"ShoppingOpen:0")&&ShoppingRadarChecks.DrawnTexts(dock).Contains("PISTES · À VÉRIFIER"),"Une recherche sans choix confirmé affiche directement ses pistes dans le dock existant");
        using var unavailable=new ShoppingSurface(station){Width=700,Height=560};
        Render(unavailable,Path.Combine(output,"shopping-analysis-unavailable.png"));
        Check(Has(unavailable,"ShoppingOpen:0")&&!Has(unavailable,"ShoppingPending"),"Les pistes sont visibles à la réouverture, sans bouton vers des choix vides");
        var unavailableTexts=ShoppingRadarChecks.DrawnTexts(unavailable);
        Check(unavailableTexts.Contains("À vérifier")&&unavailableTexts.Contains("Analyse indisponible")&&!unavailableTexts.Contains("À privilégier")&&!unavailableTexts.Any(text=>text.EndsWith("% vérifiés")),"Une analyse indisponible reste explicite, sans pourcentage inventé");
        station.ShoppingEngine.AssessmentAvailable=true;
        ShoppingRadarChecks.Loading(station,(surface,name)=>Render(surface,Path.Combine(output,name)),surface=>Pixels(surface));
    }
    static void ShoppingCardChecks(string root,string output)
    {
        var station=new Station{Root=root};using var tabs=station.ShoppingTabs;
        station.ShoppingEngine.LongReason=string.Join(" ",Enumerable.Repeat("Les critères sont documentés sur la fiche et les détails de chaque fonction doivent rester entièrement lisibles.",6))+" Fin du résumé complet.";
        station.ShoppingEngine.SourceNotice="www.example.fr refuse la lecture (403).";
        station.Shopping.SearchAsync("Réfrigérateur avec de nombreux critères détaillés").GetAwaiter().GetResult();
        using var dock=new ShoppingSurface(station){Width=700,Height=560};
        string previous=DesktopTheme.Current.Id;
        try
        {
            foreach(string theme in new[]{"vice-city","aurore","obsidienne"})
            {
                DesktopTheme.Select(theme,false);dock.Refresh();Render(dock,Path.Combine(output,$"shopping-cards-{theme}.png"));
            }
            Check(station.Shopping.Status.Contains("sources partielles")&&!station.Shopping.Status.Contains("403")&&station.Shopping.SummaryDetails.Contains("403"),"Les erreurs de source sont signalées brièvement et restent disponibles en détail");
            var measure=typeof(ShoppingSurface).GetMethod("MeasureResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dock,[station.Shopping.Results[0]])!;
            var reason=(FormattedText)measure.GetType().GetProperty("Reason")!.GetValue(measure)!;
            Check(reason.Height>80&&reason.Trimming==TextTrimming.None&&reason.Text.EndsWith("Fin du résumé complet."),"Les résumés longs gardent tout leur texte et agrandissent la fiche");
            Private(dock,"Page",1000);dock.Refresh();Render(dock,Path.Combine(output,"shopping-cards-scrolled.png"));
            Check(Has(dock,"ShoppingOpen:2")&&ShoppingRadarChecks.HoverRow(dock,2).Contains("1/3 critères confirmés"),"La liste défile jusqu'à la dernière piste sans perdre ses interactions");
            Check(Hits(dock).EnumerateArray().Where(hit=>hit.GetProperty("name").GetString()!.StartsWith("ShoppingOpen:")).All(hit=>hit.GetProperty("y").GetDouble()>=166&&hit.GetProperty("y").GetDouble()+hit.GetProperty("height").GetDouble()<=dock.Height-12),"Les zones cliquables restent limitées au cadre visible après défilement");
        }
        finally{DesktopTheme.Select(previous,false);}
    }

    sealed class GlassProbe(Station station):Surface(station)
    {
        internal bool Enabled=true,Frame;
        internal double Offset;
        protected override double HitOffsetY=>Offset;
        internal void PointAt(double x,double y){Pointer=new(x,y);Refresh();}
        protected override void Paint()
        {
            Header("CODEX METER");
            D.PushTransform(new TranslateTransform(0,Offset));
            Button("GlassAction","Actualiser",24,50,180,38,()=>{},enabled:Enabled);D.Pop();
            if(Frame)GlassReflection(new Rect(1,1,Width-2,Height-2),23);
        }
    }
    static void GlassChecks(Station station,string output)
    {
        var probe=new GlassProbe(station){Width=360,Height=240};
        var idle=Pixels(probe);string hits=Hits(probe).GetRawText();
        probe.PointAt(34,58);var hover=Pixels(probe);
        Check(Different(idle,hover),"Enabled glass visibly reacts to the pointer");
        Check(hits==Hits(probe).GetRawText(),"Hover leaves click targets unchanged");
        probe.PointAt(194,78);Check(Different(hover,Pixels(probe)),"The reflection follows the pointer inside one control");
        probe.Enabled=false;probe.PointAt(-1,-1);var disabled=Pixels(probe);
        probe.PointAt(34,58);Check(!Different(disabled,Pixels(probe)),"A disabled control has no hover highlight");
        probe.Enabled=true;probe.Offset=90;probe.PointAt(-1,-1);var translated=Pixels(probe);
        probe.PointAt(34,58);Check(!Different(translated,Pixels(probe)),"A translated page does not react at its old location");
        probe.PointAt(34,148);Check(Different(translated,Pixels(probe)),"A translated page reacts at its actual hit target");
        probe.SetDisplayed(false);probe.SetDisplayed(true);Check(!Different(translated,Pixels(probe)),"Hiding a dock clears its hover without a timer");
        probe.Offset=0;probe.Frame=true;
        foreach(var theme in DesktopTheme.Definitions)
        {
            DesktopTheme.Select(theme.Id,false);probe.PointAt(34,58);Render(probe,Path.Combine(output,$"glass-hover-{theme.Id}.png"));
        }
        station.Reminders=[new("Penser à faire une pause")];
        var nudge=new ReminderSurface(station){Width=440,Height=232};
        typeof(Surface).GetField("Pointer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(nudge,new Point(30,60));
        Render(nudge,Path.Combine(output,"glass-nudge-hover.png"));
        Console.WriteLine("PASS glass: visible moving reflection, disabled controls, unchanged targets, translated pages, cleared hidden hover and scene renders.");
    }
    static void AppLayoutChecks(Station station,string output)
    {
        station.Apps=new[]{"Steam","Brave","Discord","Stremio","League of Legends","Codex","Claude Code","Edge","Chrome","qBittorrent","battle.net","Spotify"}.Select(name=>new DockApp(name,Path.Combine(station.Root,"fixtures",name+".exe"))).ToList();
        foreach(var size in new[]{new Size(144,1188),new Size(144,112),new Size(176,440),new Size(240,1188),new Size(1120,270),new Size(920,230),new Size(720,204),new Size(240,112),new Size(1120,116)})
        {
            var dock=new DockSurface(station){Width=size.Width,Height=size.Height};
            Render(dock,Path.Combine(output,$"apps-layout-{size.Width}-{size.Height}.png"));
            var targets=Hits(dock).EnumerateArray().ToArray();
            var add=targets.Single(hit=>hit.GetProperty("name").GetString()=="ManageApps");
            Check(add.GetProperty("width").GetDouble()==28&&add.GetProperty("height").GetDouble()==28,"The app editor keeps its full target");
            var launches=targets.Where(hit=>hit.GetProperty("name").GetString()!.StartsWith("Launch:")).ToArray();
            Check(launches.All(hit=>hit.GetProperty("y").GetDouble()>=44),"Icons leave the Applications heading clear");
            if(size.Height>=1188)Check(launches.Length==12&&launches.All(hit=>Math.Abs(hit.GetProperty("x").GetDouble()+hit.GetProperty("width").GetDouble()/2-size.Width/2)<.01),"The tall column exposes and centres all 12 apps");
            Rect Bounds(JsonElement hit)=>new(hit.GetProperty("x").GetDouble(),hit.GetProperty("y").GetDouble(),hit.GetProperty("width").GetDouble(),hit.GetProperty("height").GetDouble());
            for(int i=0;i<targets.Length;i++)for(int j=i+1;j<targets.Length;j++)
            {
                var overlap=Rect.Intersect(Bounds(targets[i]),Bounds(targets[j]));
                Check(overlap.IsEmpty||overlap.Width==0||overlap.Height==0,"App and editor targets never overlap");
            }
            if(size==new Size(144,112))
            {
                typeof(DockSurface).GetField("rowOffset",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dock,11);
                dock.Refresh();Render(dock,Path.Combine(output,"apps-layout-last-row.png"));
                Check(Has(dock,"Launch:Spotify"),"The last app remains reachable in a short narrow dock");
            }
            dock.Dispose();
        }
        Console.WriteLine("PASS app layouts: narrow/tall/short/wide, heading space, centred column, no overlapping targets and last row reachable.");
    }
    [STAThread] static void Main(string[] args)
    {
        var app=new Application();
        string root=Path.GetFullPath(args[0]),output=Path.Combine(root,"artifacts/validation/dock-review");
        Directory.CreateDirectory(output);
        var station=new Station{Root=root};
        if(args.Contains("--apps-only")){AppLayoutChecks(station,output);return;}
        if(args.Contains("--glass-only")){GlassChecks(station,output);return;}
        if(args.Contains("--projects-only"))
        {
            Native.Values["projectCount"]="1";Native.Values["project:0:name"]="Battlestation";Native.Values["project:0:path"]=root;
            Native.Values["selectedPath"]=root;Native.Values["selectedProject"]="Battlestation";
            station.Projects.Remember(root,new ProjectSignal("main",3,0,0,DateTimeOffset.Now.AddMinutes(-13)));
            foreach(var size in new[]{new Size(1270,344),new Size(720,293),new Size(360,240)})
            {
                var projects=new DeskSurface(station,DeskWidget.Projects){Width=size.Width,Height=size.Height};
                typeof(DeskSurface).GetField("popup",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(projects,true);
                Render(projects,Path.Combine(output,$"projects-menu-{size.Width}.png"));
                Check(new[]{"Explorer","OpenCodex","OpenCodexDs","OpenKilo","CloseProject","FoldProject"}.All(name=>Has(projects,name)),"Every project launch and fold action remains reachable");
                var buttons=Hits(projects).EnumerateArray().Where(h=>new[]{"Explorer","OpenCodex","OpenCodexDs","OpenKilo"}.Contains(h.GetProperty("name").GetString())).Select(h=>new Rect(h.GetProperty("x").GetDouble(),h.GetProperty("y").GetDouble(),h.GetProperty("width").GetDouble(),h.GetProperty("height").GetDouble())).ToArray();
                for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)Check(!buttons[i].IntersectsWith(buttons[j]),"Project action targets never overlap");
                var actions=(System.Collections.IEnumerable)typeof(Surface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(projects)!;
                foreach(var value in actions){var hit=((Rect Rect,Action Action,string Name))value;if(hit.Name=="FoldProject"){hit.Action();break;}}
                projects.Refresh();Render(projects,Path.Combine(output,$"projects-folded-{size.Width}.png"));
                Check(Has(projects,"Project0")&&!Has(projects,"OpenKilo"),"Folding restores the project cards without launching a CLI");
            }
            Console.WriteLine("PASS project menus: wide/default/minimum render, action bounds, no overlapping launches, fold restores cards. No live clicks or CLI launches.");
            return;
        }
        // Scene preview: a real window, so the pixel shader actually runs (a
        // RenderTargetBitmap ignores pixel shaders). Captured from outside.
        if(args.Contains("--water-preview"))
        {
            station.MusicBands=[.85f,.75f,.65f,.55f,.40f,.30f,.45f,.20f,.15f,.30f,.20f,.10f];
            var mountain=new MontagneSurface(station){Width=960,Height=600};
            mountain.SetActive(true);
            var stack=new System.Windows.Controls.Grid{Width=960,Height=600};
            stack.Children.Add(mountain.SceneLayer);stack.Children.Add(mountain);
            var window=new Window{Width=960,Height=600,Left=180,Top=180,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
                Background=new SolidColorBrush(Color.FromRgb(18,14,28)),Content=stack,ShowInTaskbar=false,Topmost=true};
            var close=new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(9),System.Windows.Threading.DispatcherPriority.Background,(_,_)=>{window.Close();},app.Dispatcher);
            window.Loaded+=(_,_)=>close.Start();
            app.Run(window);
            return;
        }
        var face=new Typeface(new FontFamily(new Uri("pack://application:,,,/"),"./Assets/Fonts/#GTAArtDeco Condensed"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
        Check(face.TryGetGlyphTypeface(out var font)&&font.FontUri.ToString().Contains("art-deco"),"Embedded Art Deco resolves without font fallback");
        ShoppingChecks(station,output);
        ShoppingRadarChecks.ParallelSearches(new Station{Root=root},(surface,name)=>Render(surface,Path.Combine(output,name)));
        ShoppingCardChecks(root,output);
        ShoppingRadarChecks.SerperSettings(new Station{Root=root});
        foreach(int count in new[]{0,1,3})
        {
            station.Reminders=Enumerable.Range(0,count).Select(i=>new ReminderItem("Penser à faire une pause")).ToList();
            var nudge=new ReminderSurface(station){Width=440,Height=232};
            Render(nudge,Path.Combine(output,$"nudge-{count}.png"));
            Check(Has(nudge,"ReminderDone:0")==(count>0),"Done only exists for a reminder");
            Check(Has(nudge,"ReminderPrevious")==false&&Has(nudge,"ReminderNext")==false,"No pagination while every reminder fits");
            if(count>0)
            {
                Private(nudge,"Complete",0);
                Check(station.Reminders.Count==count-1,"Done removes exactly one reminder");
            }
        }
        station.Reminders=Enumerable.Range(0,7).Select(i=>new ReminderItem("Rappel "+(i+1))).ToList();
        var paged=new ReminderSurface(station){Width=779,Height=232};
        Render(paged,Path.Combine(output,"nudge-paged-1.png"));
        Check(Has(paged,"ReminderEdit:0")&&Has(paged,"ReminderEdit:2")&&!Has(paged,"ReminderEdit:3"),"Three reminders fill a 232 px dock");
        Check(Has(paged,"ReminderNext")&&!Has(paged,"ReminderPrevious"),"The pagination starts on the first page");
        Private(paged,"Page",1);
        Render(paged,Path.Combine(output,"nudge-paged-2.png"));
        Check(Has(paged,"ReminderEdit:3")&&Has(paged,"ReminderEdit:5")&&!Has(paged,"ReminderEdit:0")&&Has(paged,"ReminderPrevious"),"The second page shows the following reminders");
        var compactNudge=new ReminderSurface(station){Width=779,Height=112};
        Render(compactNudge,Path.Combine(output,"nudge-compact.png"));
        Check(Has(compactNudge,"ReminderEdit:0")&&!Has(compactNudge,"ReminderEdit:1"),"The minimum height shows a single reminder and its pagination");
        station.Reminders=[new("Penser à faire une pause")];
        Render(new ReminderSurface(station){Width=920,Height=232},Path.Combine(output,"nudge-wide.png"));
        station.Apps=new[]{"Steam","Brave","Discord","Stremio","League of Legends","Codex","Claude Code","Edge","Chrome","qBittorrent","battle.net"}.Select(name=>new DockApp(name,Path.Combine(root,"fixtures",name+".exe"))).ToList();
        var appsSurface=new DockSurface(station){Width=1120,Height=116};
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,[]));
        Render(appsSurface,Path.Combine(output,"apps-closed.png"));
        var closedHits=Hits(appsSurface).GetRawText();
        var running=station.Apps.Where((_,index)=>index%2==0).Select(appItem=>new AppProcessSnapshot(Path.GetFileNameWithoutExtension(appItem.Path),appItem.Path)).ToArray();
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,running));
        Thread.Sleep(90);
        var midpoint=JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps")[0].GetProperty("material").GetDouble();
        Check(midpoint>0&&midpoint<1,"A process change produces an intermediate material");
        Render(appsSurface,Path.Combine(output,"apps-transition.png"));
        Thread.Sleep(280);appsSurface.Refresh();
        Render(appsSurface,Path.Combine(output,"apps-mixed.png"));
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,station.Apps.Select(appItem=>new AppProcessSnapshot(Path.GetFileNameWithoutExtension(appItem.Path),appItem.Path)).ToArray()));
        Thread.Sleep(280);appsSurface.Refresh();
        Render(appsSurface,Path.Combine(output,"apps-running.png"));
        var open=JsonSerializer.SerializeToElement(appsSurface.InspectActivity());
        Check(open.GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("state").GetString()=="Running"&&row.GetProperty("material").GetDouble()==1),"All icons reach their running material");
        Check(Hits(appsSurface).GetRawText()==closedHits,"Activity leaves every launch and editor hit unchanged");
        appsSurface.ApplyActivitySnapshot(AppActivity.Unknown(station.Apps,"fixture"));
        Check(JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("state").GetString()=="Unknown"&&row.GetProperty("material").GetDouble()==1),"Unknown retains the last confirmed material");
        station.Apps.Reverse();
        Check(JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("material").GetDouble()==1),"Reordering retains state by application identity");
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,[]));
        Thread.Sleep(280);appsSurface.Refresh();
        Render(appsSurface,Path.Combine(output,"apps-stopped-again.png"));
        Check(JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("material").GetDouble()==0),"Process closure restores pearl");
        appsSurface.Dispose();
        station.Apps.Add(new DockApp("Spotify",Path.Combine(root,"fixtures","Spotify.exe")));
        using(var audioSurface=new AudioSurface(station))
        {
            // Use deterministic rows after the read-only worker has stopped.
            audioSurface.Mixer.Dispose();
            var worker=(Thread)typeof(AudioMixerWorker).GetField("thread",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(audioSurface.Mixer)!;
            Check(worker.Join(10000),"Audio worker stops");
            typeof(AudioMixerWorker).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(audioSurface.Mixer,
                new AudioMixerSnapshot([],Enumerable.Range(0,6).Select(i=>new AudioApp("app"+i,"Source "+i,.65f,false,.4f)).ToArray(),"Casque (Buds3 Pro de Ayo)","fixture","Micro",.65f,false,false,""));
            foreach(var size in new[]{new Size(620,264),new Size(440,220),new Size(720,336)})
            {
                audioSurface.Width=size.Width;audioSurface.Height=size.Height;audioSurface.Refresh();
                Render(audioSurface,Path.Combine(output,$"audio-{size.Width}-{size.Height}.png"));
                Check(Has(audioSurface,"AudioNext"),"Overflow audio sources remain reachable");
                Check(Has(audioSurface,"AudioVolume:master"),"Compact audio retains master slider");
            }
        }
        foreach(var size in new[]{new Size(1120,270),new Size(920,230),new Size(720,204),new Size(240,112),new Size(1120,116)})
        {
            var packed=new DockSurface(station){Width=size.Width,Height=size.Height};
            Render(packed,Path.Combine(output,$"apps-{size.Width}-{size.Height}.png"));
            var targets=Hits(packed).EnumerateArray().ToArray();
            var add=targets.Single(hit=>hit.GetProperty("name").GetString()=="ManageApps");
            Check(add.GetProperty("width").GetDouble()==28&&add.GetProperty("height").GetDouble()==28,"Add app uses a compact square button");
            if(size.Height>=230)Check(targets.Count(hit=>hit.GetProperty("name").GetString()!.StartsWith("Launch:"))==12,"Wide packs expose all 12 apps");
            Rect Bounds(JsonElement hit)=>new(hit.GetProperty("x").GetDouble(),hit.GetProperty("y").GetDouble(),hit.GetProperty("width").GetDouble(),hit.GetProperty("height").GetDouble());
            for(int i=0;i<targets.Length;i++)for(int j=i+1;j<targets.Length;j++)
            {
                var overlap=Rect.Intersect(Bounds(targets[i]),Bounds(targets[j]));
                Check(overlap.IsEmpty||overlap.Width==0||overlap.Height==0,"App controls do not overlap");
            }
            packed.Dispose();
        }
        float sixty=0,oneFortyFour=0;
        for(int i=0;i<60;i++)sixty=AudioMeterMotion.Step(sixty,.8f,1/60d);
        for(int i=0;i<144;i++)oneFortyFour=AudioMeterMotion.Step(oneFortyFour,.8f,1/144d);
        Check(Math.Abs(sixty-oneFortyFour)<.0002f,"Meter attack is independent of display refresh rate");
        Check(AudioMeterMotion.Step(.8f,0,1/60d) is >0 and <.8f,"Silence decays smoothly");
        Check(float.IsFinite(AudioMeterMotion.Step(float.NaN,float.NaN,.1)),"Invalid meter samples cannot poison animation");
        for(int i=0;i<180;i++)sixty=AudioMeterMotion.Step(sixty,0,1/60d);
        Check(sixty==0,"Silence settles completely so repainting can stop");
        using var bluetooth=new BluetoothSurface(station){Width=488,Height=280};
        var rows=new BluetoothDevice?[]{
            new("BTHENUM\\DEV_000000000001\\fixture-party","PARTYBTMS3",true,BluetoothKind.Speaker,null,1,100),
            new("BTHENUM\\DEV_000000000002\\fixture-buds","Buds3 Pro de Ayo",true,BluetoothKind.Headphones,null,2,17),
            new("BTHENUM\\DEV_000000000003\\fixture-controller","DualSense Wireless Controller",false,BluetoothKind.Controller,null,3),
            new("BTHENUM\\DEV_000000000004\\fixture-soundbar","[Samsung] Soundbar J-Series",null,BluetoothKind.Speaker,null,4)};
        typeof(BluetoothSurface).GetField("rows",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bluetooth,rows);
        typeof(BluetoothSurface).GetField("budsBattery",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bluetooth,new BudsBattery(74,71,25));
        typeof(BluetoothSurface).GetMethod("RefreshState",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(bluetooth,null);
        Thread.Sleep(280);
        Render(bluetooth,Path.Combine(output,"bluetooth.png"));
        Check(Hits(bluetooth).GetArrayLength()==4,"Only the four device drawings are clickable");
        Check(Native.Panels[12]==new Rect(0,0,488,280),"Bluetooth registers its liquid glass bounds");
        bluetooth.SetDisplayed(false);
        Check(Native.Panels[12].IsEmpty||Native.Panels[12].Width==0,"Removing Bluetooth clears the glass");
        bluetooth.DesktopX=4192;bluetooth.DesktopY=936;bluetooth.SetDisplayed(true);bluetooth.UpdateGlassBounds();
        Check(Native.Panels[12]==new Rect(4192,936,488,280),"Readding or moving Bluetooth restores the glass bounds");
        foreach(var size in new[]{new Size(512,128),new Size(128,512),new Size(440,288),new Size(128,128)})
        {
            bluetooth.Width=size.Width;bluetooth.Height=size.Height;
            Render(bluetooth,Path.Combine(output,$"bluetooth-{size.Width}x{size.Height}.png"));
            var slots=BluetoothIconLayout.Arrange(size.Width,size.Height);
            Check(Hits(bluetooth).GetArrayLength()==4,"Every Bluetooth device remains visible in every dock shape");
            Check(slots.All(rect=>new Rect(new Point(),size).Contains(rect)),"Responsive Bluetooth hits stay inside the dock");
            Check(!slots.SelectMany((rect,index)=>slots.Skip(index+1).Select(other=>rect.IntersectsWith(other))).Any(overlaps=>overlaps),"Responsive Bluetooth hits never overlap");
            if(size.Width==512)Check(slots.Select(rect=>rect.Y).Distinct().Count()==1,"Wide Bluetooth dock uses one row");
            if(size.Height==512)Check(slots.Select(rect=>rect.X).Distinct().Count()==1,"Tall Bluetooth dock uses one column");
        }
        bluetooth.Width=488;bluetooth.Height=280;
        Check(new AppIcons().Get(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe")) is not null,"Native shell icon fallback");
        BluetoothSurfaceChecks.Run(bluetooth,args.Contains("--bluetooth-read-only"));
        if(args.Contains("--bluetooth-read-only"))
            foreach(var device in new BluetoothDevices().Scan())
                Console.WriteLine(JsonSerializer.Serialize(new{device.Name,device.Connected,device.ContainerId,device.Address}));
        foreach(var theme in DesktopTheme.Definitions)
        {
            DesktopTheme.Select(theme.Id,false);
            var themedApps=new DockSurface(station){Width=1120,Height=116};
            themedApps.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,[]));themedApps.SetDisplayed(false);themedApps.SetDisplayed(true);
            Render(themedApps,Path.Combine(output,"theme-"+theme.Id+"-apps.png"));themedApps.Dispose();
            bluetooth.SetDisplayed(false);bluetooth.SetDisplayed(true);Render(bluetooth,Path.Combine(output,"theme-"+theme.Id+"-bluetooth.png"));
        }
        DesktopTheme.Select("vice-city",false);
        foreach(var size in new[]{new Size(480,380),new Size(654,382),new Size(720,440),new Size(936,480),new Size(1692,994)})
        {
            using var controller=new DualSenseSurface(station){Width=size.Width,Height=size.Height};
            var state=new DualSenseState(1,2,-1,0,640,-1024,0,128,16000,32000,(1u<<0)|(1u<<9),14,0);
            typeof(DualSenseReader).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(controller.Reader,new DualSenseSnapshot(state,"Bluetooth"));
            Render(controller,Path.Combine(output,$"dualsense-{size.Width}.png"));
            var art=(DualSenseArtwork)typeof(DualSenseSurface).GetField("artwork",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!;
            var frame=new System.Windows.Threading.DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(8);
            var wait=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
            wait.Tick+=(_,_)=>{if(art.CacheReady||DateTime.UtcNow>=deadline)frame.Continue=false;};wait.Start();
            try{System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{wait.Stop();}
            Check(art.CacheReady,"High resolution artwork completes off the UI thread");
            controller.Refresh();Render(controller,Path.Combine(output,$"dualsense-{size.Width}.png"));
            Check(Has(controller,"DualSenseAxes")&&!Has(controller,"DualSenseRumble"),"Bluetooth exposes raw axes and cannot vibrate");
            typeof(DualSenseSurface).GetField("details",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(controller,true);controller.Refresh();
            Render(controller,Path.Combine(output,$"dualsense-{size.Width}-axes.png"));
            Check(state.BatteryPercent is null,"Unknown battery stays unknown");
            Check(DualSenseState.Offset(0,0)==0&&Math.Abs(DualSenseState.Offset(32768,0)-100)<.001,"Drift readout retains the unfiltered centre offset");
        }
        var trails=new DualSenseTouchTrail();
        var touch=new DualSenseState(1,2,75,0,0,0,0,0,0,0,0,1,1,TouchAvailable:1,Touch1:1,Touch1X:.2f,Touch1Y:.3f);
        trails.Update(touch,0,true);trails.Update(touch with{Touch1X=.6f,Touch1Y=.8f},.1,true);
        Check(trails.Points(0).Count==2&&trails.Down(0),"Real contacts create a bounded trail");
        trails.Update(touch with{Touch1=0},.2,true);trails.Update(touch with{Touch1X=.8f},.3,true);
        Check(trails.Points(0)[^1].Stroke!=trails.Points(0)[^2].Stroke,"Lifting a finger prevents a connecting line to the next contact");
        trails.Update(touch with{Touch1=0},2,true);Check(trails.Points(0).Count==0,"Released trails expire completely");
        trails.Update(touch,3,false);Check(trails.Points(0).Count==0,"Compatible mode never invents touch positions");
        station.ApplySettings(station.Settings with{DualSenseTouchTrail=true},station.TargetDate);
        using(var controller=new DualSenseSurface(station){Width=1000,Height=740})
        {
            var history=(DualSenseTouchTrail)typeof(DualSenseSurface).GetField("touchTrail",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!;
            for(int i=0;i<=30;i++)history.Update(touch with{Touch1X=.1f+i*.025f,Touch1Y=.55f+(float)Math.Sin(i*.18)*.18f,Touch2=1,Touch2X=.85f-i*.02f,Touch2Y=.25f+(float)Math.Cos(i*.16)*.12f},i/60d,true);
            typeof(DualSenseReader).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(controller.Reader,new DualSenseSnapshot(touch,"Bluetooth"));
            typeof(DualSenseSurface).GetField("seconds",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(controller,.5d);
            Render(controller,Path.Combine(output,"dualsense-touch-trails.png"));
            Check(Has(controller,"DualSenseTrail")&&!Has(controller,"DualSenseRumble"),"Touch mode stays separate from removed vibration controls");
        }
        foreach(var size in new[]{new Size(440,280),new Size(872,328),new Size(832,480)})
        {
            using var network=new NetworkSurface(station){Width=size.Width,Height=size.Height};
            var samples=Enumerable.Range(0,60).Select(i=>new NetworkSample(Environment.TickCount64-(59-i)*1000,12000+Math.Sin(i*.22)*10000+i*800,4000+Math.Cos(i*.3)*3000)).ToArray();
            typeof(NetworkSampler).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(network.Sampler,new NetworkSnapshot("fixture","Ethernet",samples,null,"1.1.1.1","",[new("fixture","Ethernet",true)]));
            Render(network,Path.Combine(output,$"network-{size.Width}.png"));
            Check(Has(network,"NetworkApplications")&&Has(network,"NetworkSettings"),"Network controls remain inside resized docks");
            Check(NetworkSurface.Rate(null)=="—","Unavailable rates are never zero");
            typeof(NetworkDetailClient).GetField("frame",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(network.Details,new NetworkDetailFrame(true,"TCP + UDP",Enumerable.Range(0,5).Select(i=>new NetworkAppRate("Application "+i,50000/(i+1),6000/(i+1),i+1)).ToArray()));
            typeof(NetworkSurface).GetField("detailView",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(network,true);network.Refresh();
            Render(network,Path.Combine(output,$"network-{size.Width}-apps.png"));
        }
        // Applications: one window state per icon, established by a single window list.
        var ranked=station.Apps.Take(4).ToArray();
        var rankedProcesses=ranked.Select((app,index)=>new AppProcessSnapshot(Path.GetFileNameWithoutExtension(app.Path),app.Path,true,700+index)).ToArray();
        var rankedWindows=new[]{new AppWindowSnapshot(700,true,false,true),new AppWindowSnapshot(701,true,false,false),new AppWindowSnapshot(702,false,true,false)};
        var states=new DockSurface(station){Width=1120,Height=116};
        {
            states.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,rankedProcesses,rankedWindows));
            Thread.Sleep(300);states.Refresh();
            var windowRows=JsonSerializer.SerializeToElement(states.InspectActivity()).GetProperty("apps").EnumerateArray().ToDictionary(row=>row.GetProperty("name").GetString()!);
            Check(windowRows[ranked[0].Name].GetProperty("window").GetString()=="Foreground","The active window is at the front");
            Check(windowRows[ranked[1].Name].GetProperty("window").GetString()=="Background","A visible window stays in the background");
            Check(windowRows[ranked[2].Name].GetProperty("window").GetString()=="Minimized","A reduced window is reported as reduced");
            Check(windowRows[ranked[3].Name].GetProperty("window").GetString()=="Tray","A running application without a window is in the tray");
            Check(windowRows[station.Apps[4].Name].GetProperty("state").GetString()=="Stopped","An application outside the process list keeps its stopped state");
            var front=Pixels(states);
            Render(states,Path.Combine(output,"apps-window-states.png"));
            states.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,rankedProcesses,rankedWindows.Select(window=>window with{Foreground=false}).ToArray()));
            Thread.Sleep(300);states.Refresh();
            Check(Different(front,Pixels(states)),"Leaving the foreground visibly changes the icon material");
        }
        states.Dispose();
        // Montagne: the snow falls while it is exposed, a hidden dock stops.
        using(var mountain=new MontagneSurface(station){Width=960,Height=600})
        {
            // The first render registers the dock's glass slot, exactly like the
            // desktop does.
            Pixels(mountain);
            Check(Native.Panels[15]==new Rect(0,0,960,600),"The mountain owns the glass slot 15");
            mountain.SetActive(true);
            Check(mountain.Animating,"The mountain animates while it is exposed");
            var before=mountain.Elapsed;
            for(int frame=0;frame<30;frame++)mountain.Advance(1d/60d);
            Check(mountain.Elapsed>before,"The mountain advances its time while it is exposed");
            Render(mountain,Path.Combine(output,"montagne.png"),mountain.SceneLayer);
            Check(JsonSerializer.SerializeToElement(mountain.Inspect()).GetProperty("scene").GetString() is "shader" or "dessinée","The mountain states which scene it runs");
            mountain.SetActive(false);
            var frozen=mountain.Elapsed;mountain.Advance(.5);
            Check(!mountain.Animating&&mountain.Elapsed==frozen,"A hidden mountain stops advancing");
        }
        // The stir the pointer leaves and the shader reads: it builds with the
        // gesture, decays back to rest, and never leaves the bounded range.
        using(var mountain=new MontagneSurface(station){Width=960,Height=600})
        {
            mountain.SetActive(true);
            mountain.Stir(40);
            var stirred=JsonSerializer.SerializeToElement(mountain.Inspect()).GetProperty("stir").GetProperty("X").GetDouble();
            Check(Math.Abs(stirred)>1&&Math.Abs(stirred)<=26,"The stir builds within its bounds");
            for(int frame=0;frame<180;frame++)mountain.Advance(1d/60d);
            var rested=JsonSerializer.SerializeToElement(mountain.Inspect()).GetProperty("stir").GetProperty("X").GetDouble();
            Check(Math.Abs(rested)<1,"The stir decays back to rest");
        }
        // LoL: rest state, synthetic reading, timers and the certificate predicate.
        // Montagne and music: the bass raises the wind and shakes the flakes with
        // one bounded gust per beat.
        using(var mountain=new MontagneSurface(station){Width=960,Height=600})
        {
            station.MusicBands=new float[12];
            mountain.SetActive(true);
            for(int frame=0;frame<60;frame++)mountain.Advance(1d/60d);
            station.MusicBands=[.92f,.80f,.70f,.60f,.30f,.20f,.15f,.10f,.08f,.06f,.05f,.04f];
            for(int frame=0;frame<40;frame++)mountain.Advance(1d/60d);
            Check(mountain.Animating,"La montagne reste animée");
            var excited=JsonSerializer.SerializeToElement(mountain.Inspect());
            Check(excited.GetProperty("stir").GetProperty("X").GetDouble()!=0&&excited.GetProperty("energy").GetDouble()>0,"L'énergie musicale est mesurée");
            Render(mountain,Path.Combine(output,"montagne-music.png"),mountain.SceneLayer);
            station.MusicBands=new float[12];
            for(int frame=0;frame<600;frame++)mountain.Advance(1d/60d);
            Check(Math.Abs(JsonSerializer.SerializeToElement(mountain.Inspect()).GetProperty("stir").GetProperty("X").GetDouble())<1,"Le silence ramène le calme");
            mountain.SetActive(false);
            var frozen=mountain.Elapsed;mountain.Advance(.5);
            Check(mountain.Elapsed==frozen,"Une Montagne masquée n'avance plus");
        }
        Check(LolTelemetry.IsLiveClientEndpoint("127.0.0.1",2999)&&!LolTelemetry.IsLiveClientEndpoint("localhost",2999)
            &&!LolTelemetry.IsLiveClientEndpoint("127.0.0.1",3000)&&!LolTelemetry.IsLiveClientEndpoint("127.0.0.1.evil.test",2999),
            "Only 127.0.0.1:2999 justifies accepting the live client certificate");
        Check(LolTelemetry.Probe(true,true)&&!LolTelemetry.Probe(true,false)&&!LolTelemetry.Probe(false,true),
            "The live client API is only read while a game process runs");
        var reading=LolTelemetry.Parse(LiveGame,DateTimeOffset.UtcNow);
        Check(reading.State==LolState.Live&&reading.Champion=="Ahri"&&reading.Level==7&&reading.Kills==4&&reading.Deaths==2&&reading.Assists==9&&reading.CreepScore==182&&reading.Gold==12480,
            "The synthetic reading gives champion, level, KDA, CS and gold");
        Check(reading.Blue.Dragons==1&&reading.Blue.Barons==1&&reading.Blue.Turrets==2&&reading.Blue.Inhibitors==1&&reading.Red.Dragons==1&&reading.Red.Turrets==3&&reading.Red.Inhibitors==0,
            "Objectives are counted per team");
        Check(reading.DragonIn is{} dragon&&Math.Abs(dragon-236)<.001&&reading.BaronIn is{} baron&&Math.Abs(baron-396)<.001,
            "Dragon counts 5:00 and Baron 6:00 after the corresponding kill");
        Check(reading.Respawn is{} respawn&&Math.Abs(respawn-7.5)<.001,"The respawn timer is read while the player is dead");
        bool refused=false;try{LolTelemetry.Parse("{}",DateTimeOffset.UtcNow);}catch(FormatException){refused=true;}
        Check(refused,"A response without game data is refused");
        var aram=LolTelemetry.Parse(AramGame,DateTimeOffset.UtcNow);
        Check(aram.Aram&&aram.MapNumber==12&&aram.Mode=="ARAM","Howling Abyss is recognised from the map and the mode");
        Check(aram.DragonIn is null&&aram.BaronIn is null,"ARAM exposes no dragon or baron timer to invent");
        Check(aram.Events!.Contains("TurretKilled")&&aram.Events.Contains("GameStart"),"The received event names are published for verification");
        using(var league=new LolSurface(station){Width=700,Height=220})
        {
            var telemetry=typeof(LolSurface).GetField("telemetry",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(league)!;
            typeof(LolTelemetry).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(telemetry,LolSnapshot.Idle(LolState.NoGame));
            league.Poll();Render(league,Path.Combine(output,"lol-none.png"));
            Check(Native.Panels[16]==new Rect(0,0,700,220),"LoL owns the glass slot 16");
            Check(JsonSerializer.SerializeToElement(league.Inspect()).GetProperty("state").GetString()=="NoGame","The resting dock states that no game runs");
            var none=Pixels(league);
            typeof(LolTelemetry).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(telemetry,reading);
            league.Poll();Render(league,Path.Combine(output,"lol-live.png"));
            Check(Different(none,Pixels(league)),"A live reading changes the dock");
            var live=JsonSerializer.SerializeToElement(league.Inspect());
            Check(live.GetProperty("state").GetString()=="Live"&&live.GetProperty("creepScore").GetInt32()==182&&live.GetProperty("blue").GetProperty("Dragons").GetInt32()==1,"inspect publishes the live reading and its objectives");
            typeof(LolTelemetry).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(telemetry,LolSnapshot.Idle(LolState.Unavailable,"HttpRequestException"));
            league.Poll();Render(league,Path.Combine(output,"lol-unavailable.png"));
            Check(JsonSerializer.SerializeToElement(league.Inspect()).GetProperty("state").GetString()=="Unavailable","A failed reading is reported as unavailable");
            typeof(LolTelemetry).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(telemetry,aram);
            league.Poll();
            var aramImage=Pixels(league);Render(league,Path.Combine(output,"lol-aram.png"));
            var aramInspect=JsonSerializer.SerializeToElement(league.Inspect());
            Check(aramInspect.GetProperty("aram").GetBoolean()&&aramInspect.GetProperty("mode").GetString()=="ARAM","inspect publishes the ARAM mode");
            typeof(LolTelemetry).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(telemetry,reading);
            league.Poll();
            Check(Different(aramImage,Pixels(league)),"An ARAM reading does not draw the dragon and baron counters");
        }
        // Météo: the hourly strip only exists from 190 px, the rain line everywhere.
        Native.Values["weatherTemp"]="21°";Native.Values["weatherCity"]="Aix-en-Provence";Native.Values["weatherCondition"]="Éclaircies";
        Native.Values["weatherIcon"]="weather-sun";Native.Values["weatherRange"]="↑ 24°   ↓ 13°";Native.Values["weatherWind"]="Vent · 12 km/h";
        Native.Values["weatherRain"]="";
        const string hours="14:14:3:10;15:15:61:70;16:16:3:40;17:17:2:20;18:18:1:10;19:19:0:5";
        Native.Values["weatherHours"]=hours;
        {
            var tallWeather=new DeskSurface(station,DeskWidget.Weather){Width=480,Height=192};
            var compactWeather=new DeskSurface(station,DeskWidget.Weather){Width=720,Height=112};
            var tall=Pixels(tallWeather);var compact=Pixels(compactWeather);
            Render(tallWeather,Path.Combine(output,"weather-tall.png"));
            Render(compactWeather,Path.Combine(output,"weather-compact.png"));
            Native.Values["weatherHours"]="";
            Check(!Different(compact,Pixels(compactWeather)),"The compact weather dock ignores the hourly strip");
            Check(Different(tall,Pixels(tallWeather)),"The tall weather dock draws the hourly strip");
            Native.Values["weatherHours"]=hours;
            Native.Values["weatherRain"]="Pluie dans 15 min";
            Check(Different(compact,Pixels(compactWeather)),"The imminent rain line reaches the compact dock");
            Check(Different(tall,Pixels(tallWeather)),"The imminent rain line reaches the tall dock");
        }
        // Musique : la pochette habille le verre, l'égaliseur suit les bandes.
        {
            Native.Values["source"]="Spotify";Native.Values["title"]="Footballeur";Native.Values["artist"]="Vald";
            Native.Values["mediaTime"]="1:12 / 3:40";Native.Values["mediaProgress"]="0.33";Native.Values["playing"]="1";
            Native.Values["canPlay"]="1";Native.Values["canNext"]="1";Native.Values["canPrevious"]="1";Native.Values["canSeek"]="1";
            float[] loud=[.92f,.84f,.72f,.58f,.44f,.30f,.24f,.18f,.14f,.10f,.08f,.05f];
            float[] quiet=[.12f,.10f,.08f,.06f,.05f,.04f,.03f,.02f,.02f,.01f,.01f,.01f];
            var music=new DeskSurface(station,DeskWidget.Music){Width=720,Height=168};
            FeedMusic(music,loud);
            var loudImage=Pixels(music);
            Render(music,Path.Combine(output,"music-playing.png"));
            FeedMusic(music,quiet);
            Check(Different(loudImage,Pixels(music)),"Le ruban spectral suit les bandes");
            // L'égaliseur est étalé bord à bord, pas groupé sous le texte.
            var quietImage=Pixels(music);
            var size=new Size(720,168);
            Check(Strip(loudImage,size,2,16,.55)>Strip(quietImage,size,2,16,.55)+1,"L'égaliseur atteint le bord gauche");
            Check(Strip(loudImage,size,702,16,.55)>Strip(quietImage,size,702,16,.55)+1,"L'égaliseur atteint le bord droit");
            Render(music,Path.Combine(output,"music-quiet.png"));
            var cover=CoverFixture();
            station.CoverArt=cover;FeedMusic(music,loud);
            Check(Different(Pixels(music),loudImage),"La pochette de la piste atteint le verre");
            Render(music,Path.Combine(output,"music-cover.png"));
            // Crêtes : elles survivent à l'extinction, puis retombent jusqu'à zéro.
            Native.Values["playing"]="";
            music.TickAudio();
            Check(MusicPeaks(music).Any(peak=>peak>0),"Les crêtes retombent après l'arrêt au lieu de disparaître");
            for(int i=0;i<160;i++)music.TickAudio();
            Check(MusicPeaks(music).All(peak=>peak==0),"Les crêtes finissent par se poser");
            Native.Values["playing"]="1";
            var tall=new DeskSurface(station,DeskWidget.Music){Width=720,Height=220};
            FeedMusic(tall,loud);
            Render(tall,Path.Combine(output,"music-tall.png"));
            // Taille du bloc Lecteur sur le bureau mono écran.
            var live=new DeskSurface(station,DeskWidget.Music){Width=966,Height=480};
            FeedMusic(live,loud);
            Render(live,Path.Combine(output,"music-live.png"));
            station.CoverArt=null;
            var compact=new DeskSurface(station,DeskWidget.Music){Width=520,Height=168};
            FeedMusic(compact,loud);
            Render(compact,Path.Combine(output,"music-compact.png"));
            Check(Different(Pixels(compact),Pixels(music)),"Sans pochette, le bloc garde son verre et son égaliseur");
        }
        // Projets: divergence, commit age and the terminal badge.
        var projectRoot=Path.Combine(root,"fixtures","Battlestation");
        Native.Values["projectCount"]="1";Native.Values["project:0:name"]="Battlestation";Native.Values["project:0:path"]=projectRoot;Native.Values["project:0:age"]="3 h";
        station.Projects.Remember(projectRoot,new ProjectSignal("main",3,2,1,DateTimeOffset.Now.AddHours(-4)));
        static string Status(ProjectSignal signal)=>(string)typeof(DeskSurface).GetMethod("ProjectStatus",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{signal})!;
        Check(Status(new ProjectSignal("main",3,2,1,DateTimeOffset.Now.AddHours(-4)))=="main · 3 modif. · ↑2 ↓1 · 4 h","The project card shows branch, changes, divergence and commit age");
        Check(Status(new ProjectSignal("main",0,null,null,null))=="main · propre","A project without divergence keeps its former text");
        station.Terminal=new TerminalSession{Titles=["Battlestation · Codex CLI"]};
        var badge=Pixels(new DeskSurface(station,DeskWidget.Projects){Width=720,Height=293});
        station.Terminal=null;
        var plain=Pixels(new DeskSurface(station,DeskWidget.Projects){Width=720,Height=293});
        Check(Different(badge,plain),"An open terminal tab adds the neon badge to the project card");
        Console.WriteLine("PASS: font, reminders, app packs and compact add button, audio bounds and meter smoothing, Bluetooth targets, native icon fallback, Montagne snow, LoL telemetry, weather strip, project card. Render fixtures are not live click validation.");
    }

    // One synthetic live client response: no game, no account and no engine is involved.
    const string LiveGame="""
        {"activePlayer":{"riotId":"Ayo#EUW","summonerName":"Ayo","level":7,"currentGold":12480,"isDead":true,"respawnTimer":7.5},
         "gameData":{"gameMode":"CLASSIC","gameTime":164.0},
         "allPlayers":[
           {"riotId":"Ayo#EUW","summonerName":"Ayo","championName":"Ahri","level":7,"team":"ORDER","isDead":true,"respawnTimer":7.5,"scores":{"kills":4,"deaths":2,"assists":9,"creepScore":182}},
           {"riotId":"EnemyMid#EUW","summonerName":"EnemyMid","championName":"Zed","level":9,"team":"CHAOS","isDead":false,"respawnTimer":0,"scores":{"kills":3,"deaths":4,"assists":5,"creepScore":150}}],
         "events":{"Events":[
           {"EventName":"DragonKill","EventTime":60.0,"KillerName":"EnemyMid"},
           {"EventName":"DragonKill","EventTime":100.0,"KillerName":"Ayo","DragonType":"Fire"},
           {"EventName":"BaronKill","EventTime":200.0,"KillerName":"Ayo"},
           {"EventName":"TurretKilled","EventTime":120.0,"KillerName":"Ayo"},
           {"EventName":"TurretKilled","EventTime":130.0,"KillerName":"Ayo"},
           {"EventName":"TurretKilled","EventTime":140.0,"KillerName":"EnemyMid"},
           {"EventName":"TurretKilled","EventTime":150.0,"KillerName":"EnemyMid"},
           {"EventName":"TurretKilled","EventTime":160.0,"KillerName":"EnemyMid"},
           {"EventName":"InhibKilled","EventTime":161.0,"KillerName":"Ayo"}]}}
        """;

    // Howling Abyss, same player: no dragon, no baron, only structures and the
    // event names the local API really sends on this map.
    const string AramGame="""
        {"activePlayer":{"riotId":"Ayo#EUW","summonerName":"Ayo","level":3,"currentGold":1420,"isDead":false,"respawnTimer":0},
         "gameData":{"gameMode":"ARAM","mapName":"Howling Abyss","mapNumber":12,"gameTime":72.0},
         "allPlayers":[
           {"riotId":"Ayo#EUW","summonerName":"Ayo","championName":"Miss Fortune","level":3,"team":"CHAOS","isDead":false,"respawnTimer":0,"scores":{"kills":0,"deaths":0,"assists":0,"creepScore":0}}],
         "events":{"Events":[
           {"EventName":"GameStart","EventTime":0.0},
           {"EventName":"MinionsSpawning","EventTime":20.0},
           {"EventName":"TurretKilled","EventTime":64.0,"KillerName":"Ayo"}]}}
        """;
}
