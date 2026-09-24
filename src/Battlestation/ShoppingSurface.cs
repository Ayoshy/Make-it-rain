using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Battlestation.Shopping;

namespace Battlestation;

/// <summary>
/// Dock « Achats » : la demande, les offres relevées avec leur verdict et la veille.
/// Le clic ouvre l'annonce ; le dernier achat reste toujours à l'utilisateur.
/// </summary>
internal sealed class ShoppingSurface : Surface,IDisposable
{
    const double WatchHeight=46;
    const string Buy="#A6ECBD",Wait="#F0D08A";
    readonly List<(Rect Bounds,RadarRow Row)> rowTargets=[];
    readonly TextBlock detailTip=new(){MaxWidth=480,TextWrapping=TextWrapping.Wrap};
    readonly DispatcherTimer loadingTimer;
    readonly TranslateTransform loadingMotion=new();
    readonly Dictionary<Guid,(DateTimeOffset At,double Width,DrawingGroup Drawing)> tabPulses=[];
    DrawingGroup? loadingDrawing;
    double loadingWidth;
    System.Windows.Media.Color loadingColor;
    string? tipText;
    Rect summaryBounds=Rect.Empty;
    int tabStart;
    double maxScroll;
    int page {get=>Tabs.Active.Page;set=>Tabs.Active.Page=value;}
    bool showJournal {get=>Tabs.Active.Journal;set=>Tabs.Active.Journal=value;}
    bool displayed=true,disposed,wasBusy,paintingBody;
    ShoppingRadar? previousRadar;
    const double TabHeight=40;
    static readonly TranslateTransform BodyOffset=new(0,TabHeight);
    static ShoppingSurface()=>BodyOffset.Freeze();
    double BodyHeight=>Height-TabHeight;
    protected override double HitOffsetY=>paintingBody?TabHeight:0;

    public ShoppingSurface(Station station):base(station,18)
    {
        Width=700;Height=520;
        ToolTipService.SetShowDuration(this,30000);
        // Seule la durée demande un rafraîchissement du texte. Le reflet est
        // animé par le compositeur WPF, indépendamment du rendu du journal.
        loadingTimer=new(DispatcherPriority.Background,Dispatcher){Interval=TimeSpan.FromSeconds(1)};
        loadingTimer.Tick+=LoadingTick;
        station.ShoppingTabs.Changed+=RadarChanged;
        IsVisibleChanged+=VisibilityChanged;
        Unloaded+=OnUnloaded;
        showJournal=Radar.Error.Length>0&&Radar.Progress.Count>0;
        SyncLoading();
    }

    ShoppingTabs Tabs=>Station.ShoppingTabs;
    ShoppingRadar Radar=>Tabs.Active.Radar;

    protected override void Paint()
    {
        rowTargets.Clear();
        summaryBounds=Rect.Empty;
        var radar=Radar;
        Header("ACHATS",12);
        Text($"{(radar.Settings.Provider=="deepseek"?"DeepSeek":"Ollama")} · {radar.ReasoningEffort}",108,16,9,Muted,width:Math.Max(1,Width-266));
        Button("ShoppingSettings","⚙",Width-146,7,28,25,ConfigureSearch,12,Muted,enabled:!Tabs.AnyBusy);
        if(!radar.Busy&&(radar.Progress.Count>0||radar.SearchStartedAt is not null))
            Button("ShoppingJournal",showJournal?"Résultats":"Journal",Width-108,7,84,25,()=>{showJournal=!showJournal;Refresh();},9.5,Muted);
        PaintTabs();
        paintingBody=true;D.PushTransform(BodyOffset);
        try{PaintBody();}finally{D.Pop();paintingBody=false;}
    }

    void PaintTabs()
    {
        foreach(var id in tabPulses.Keys.Where(id=>!Tabs.Items.Any(tab=>tab.Id==id&&tab.CompletedAt is not null)).ToArray())
        {tabPulses[id].Drawing.BeginAnimation(DrawingGroup.OpacityProperty,null);tabPulses.Remove(id);}
        int capacity=Math.Max(1,(int)((Width-130)/150));
        int active=Tabs.Items.ToList().IndexOf(Tabs.Active);
        if(active<tabStart)tabStart=active;
        if(active>=tabStart+capacity)tabStart=active-capacity+1;
        tabStart=Math.Clamp(tabStart,0,Math.Max(0,Tabs.Items.Count-capacity));
        double width=Math.Min(190,(Width-130)/Math.Min(capacity,Tabs.Items.Count));
        for(int i=0;i<Math.Min(capacity,Tabs.Items.Count-tabStart);i++)
        {
            var tab=Tabs.Items[tabStart+i];double x=24+i*width;
            var theme=DesktopTheme.Current;
            Box(x,38,width-6,28,Tint(tab==Tabs.Active?theme.Light:theme.Glass,tab==Tabs.Active?0x38:0x60),Tint(theme.Rim,tab==Tabs.Active?0x70:0x28),9);
            HoverGlass(new Rect(x,38,width-6,28),9);
            if(tab.CompletedAt is {} completed)PaintTabCompletion(tab.Id,completed,x,width-6);
            Text(tab.Radar.Busy||tab.CompletedAt is not null?"●":tab.Radar.Error.Length>0?"!":"·",x+9,44,9,tab.Radar.Busy?Wait:tab.CompletedAt is not null?"#73D79A":Muted);
            Text(tab.Title,x+23,44,10,tab==Tabs.Active?Ink:Muted,width:width-58);
            Hit("ShoppingTab:"+tab.Id,x,38,width-6,28,()=>Tabs.Select(tab.Id));
            Button("ShoppingCloseTab:"+tab.Id,"×",x+width-31,41,22,22,()=>Tabs.Close(tab.Id),11,Muted);
        }
        if(Tabs.Items.Count>capacity)
        {
            Button("ShoppingTabPrevious","‹",Width-100,39,22,26,()=>Tabs.Select(Tabs.Items[Math.Max(0,active-1)].Id),12,Muted,enabled:active>0);
            Button("ShoppingTabNext","›",Width-77,39,22,26,()=>Tabs.Select(Tabs.Items[Math.Min(Tabs.Items.Count-1,active+1)].Id),12,Muted,enabled:active<Tabs.Items.Count-1);
        }
        Button("ShoppingNewTab","+",Width-52,38,28,28,()=>Tabs.Add(),16,Ink);
    }

    static string Tint(string color,int alpha)=>$"#{alpha:X2}{color[^6..]}";
    void PaintTabCompletion(Guid id,DateTimeOffset completed,double x,double width)
    {
        if(!tabPulses.TryGetValue(id,out var pulse)||pulse.At!=completed||pulse.Width!=width)
        {
            pulse.Drawing?.BeginAnimation(DrawingGroup.OpacityProperty,null);
            var drawing=new DrawingGroup{Opacity=.08};
            using(var draw=drawing.Open())draw.DrawRoundedRectangle(B("#73D79A"),null,new Rect(0,0,width,28),9,9);
            double age=(DateTimeOffset.UtcNow-completed).TotalSeconds;
            if(age<4.8&&displayed&&IsVisible&&!disposed)
                drawing.BeginAnimation(DrawingGroup.OpacityProperty,new DoubleAnimation(.08,.3,TimeSpan.FromSeconds(1.2))
                {AutoReverse=true,RepeatBehavior=new RepeatBehavior(2),BeginTime=TimeSpan.FromSeconds(-Math.Max(0,age)),FillBehavior=FillBehavior.Stop,
                    EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
            tabPulses[id]=pulse=(completed,width,drawing);
        }
        D.PushTransform(new TranslateTransform(x,38));D.DrawDrawing(pulse.Drawing);D.Pop();
    }
    void StopTabAnimations()
    {
        foreach(var pulse in tabPulses.Values)pulse.Drawing.BeginAnimation(DrawingGroup.OpacityProperty,null);
        tabPulses.Clear();
    }

    void PaintBody()
    {
        var radar=Radar;
        int pending=radar.Results.Count(row=>row.Fit==ProductFit.Unknown);

        double fieldWidth=Math.Max(160,Width-48-100);
        Box(24,38,fieldWidth,40,"#5A140F1F","#3AD9C7F0",12);
        if(!radar.Busy)HoverGlass(new Rect(24,38,fieldWidth,40),12);
        bool empty=radar.Query.Length==0;
        Text(empty?"frigo max 800 €, no frost, 300 L":radar.Query,36,49,13,empty?Muted:Ink,width:fieldWidth-24);
        if(!radar.Busy)Hit("ShoppingEdit",24,38,fieldWidth,40,Edit);
        if(radar.Busy)Button("ShoppingStop","Arrêter",Width-24-88,38,88,40,radar.Cancel,12);
        else Button("ShoppingSearch","Chercher",Width-24-88,38,88,40,Run,12,enabled:radar.Query.Length>0);

        if(radar.Busy||showJournal)
        {
            PaintJournal();
            UpdateToolTip();
            return;
        }

        double y=88;
        bool hasSummary=radar.SummaryDetails.Length>0||radar.Error.Length>0;
        if(hasSummary)
        {
            Button("ShoppingSummary","Détails",Width-108,y-1,84,28,ShowSummary,9.5,Muted);
            summaryBounds=new Rect(24,y+TabHeight,Width-136,34);
        }
        foreach(var line in (radar.Error.Length>0?radar.Error:radar.Status).Split('\n').Take(2))
        {
            Text(line,24,y,11,radar.Error.Length>0?"#F4B7CA":Muted,width:Width-(hasSummary?144:48));
            y+=16;
        }
        y+=6;
        var visibleResults=radar.Results.OrderBy(row=>row.Fit==ProductFit.Unknown?1:0).ToArray();

        int watchRows=Math.Min(Station.Shopping.Watchlist.Count,Math.Clamp((int)((BodyHeight-300)/(WatchHeight+6)),0,2));
        double watchHeight=watchRows==0?0:watchRows*(WatchHeight+6)+18;
        int groups=visibleResults.Length==0?0:pending>0&&pending<visibleResults.Length?2:1;
        var layouts=visibleResults.Select(MeasureResult).ToArray();
        double total=layouts.Sum(layout=>layout.Height)+groups*22;
        var viewport=new Rect(20,y,Width-40,Math.Max(1,BodyHeight-y-watchHeight-12));
        maxScroll=Math.Max(0,total-viewport.Height);page=Math.Clamp(page,0,(int)Math.Ceiling(maxScroll));

        if(visibleResults.Length==0&&empty)
            Text("Décris la demande, le budget et les critères",24,y+14,14,Muted,width:Width-48);

        bool? previousPending=null;y-=page;
        D.PushClip(new RectangleGeometry(viewport));InteractionClip=new Rect(viewport.X,viewport.Y+TabHeight,viewport.Width,viewport.Height);
        try
        {
            for(int i=0;i<visibleResults.Length;i++)
            {
                bool isPending=visibleResults[i].Fit==ProductFit.Unknown;
                if(previousPending!=isPending)
                {
                    Text(isPending?"PISTES · À VÉRIFIER":"CHOIX",24,y,11,Muted,"GTAArtDeco");y+=22;
                    previousPending=isPending;
                }
                if(y<viewport.Bottom&&y+layouts[i].Height>viewport.Top)Draw(visibleResults[i],layouts[i],y,i);
                y+=layouts[i].Height;
            }
        }
        finally{InteractionClip=null;D.Pop();}
        if(maxScroll>0)
        {
            double thumb=Math.Max(24,viewport.Height*viewport.Height/total);
            Box(Width-12,viewport.Y,2,viewport.Height,Tint(DesktopTheme.Current.Rim,0x20),radius:1);
            Box(Width-12,viewport.Y+(viewport.Height-thumb)*page/maxScroll,2,thumb,Tint(DesktopTheme.Current.Rim,0x80),radius:1);
        }
        if(watchHeight>0)PaintWatch(BodyHeight-watchHeight+2,watchRows);
        UpdateToolTip();
    }

    void PaintJournal()
    {
        bool busy=Radar.Busy,failed=Radar.Error.Length>0;
        string accent=failed?"#F4B7CA":busy?Wait:Buy;
        Box(24,96,Width-48,72,"#30140F1F","#30D9C7F0",12);
        Text(busy?"RECHERCHE EN COURS":failed?"RECHERCHE ÉCHOUÉE":"RECHERCHE TERMINÉE",38,108,12,accent,"GTAArtDeco");
        Text(Duration(Radar.Elapsed),Width-38,109,11,Muted,align:"right");
        var state=failed?Radar.Error:Radar.Status.Split('\n')[0];
        Text(state,38,135,11.5,Ink,width:Width-76);
        if(busy)
        {
            EnsureLoadingDrawing();
            Box(24,180,Width-48,1,"#26D9C7F0",radius:.5);
            D.DrawDrawing(loadingDrawing!);
        }
        Text("JOURNAL",24,204,11,"#D8C8E3","GTAArtDeco");
        var progress=Radar.Progress;
        int count=Math.Max(1,(int)((BodyHeight-248)/34));
        var recent=progress.TakeLast(count).ToArray();
        if(progress.Count>recent.Length)Text($"{recent.Length} / {progress.Count}",Width-24,206,9,Muted,align:"right");
        var start=Radar.SearchStartedAt;
        for(int i=0;i<recent.Length;i++)
        {
            var step=recent[i];double y=234+i*34;
            bool current=i==recent.Length-1;
            string color=current?accent:Muted;
            Text(start is {} at?"+"+Duration(step.At>at?step.At-at:TimeSpan.Zero):step.At.ToLocalTime().ToString("HH:mm:ss"),24,y,9.5,Muted);
            Text(step.Message,99,y-1,11,current?Ink:Muted,width:Width-123);
            if(current)Box(87,y+6,4,4,color,radius:2);
        }
    }

    static string Duration(TimeSpan duration)=>$"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";

    void RadarChanged()
    {
        if(disposed)return;
        if(!Dispatcher.CheckAccess())
        {
            if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(RadarChanged));
            return;
        }
        bool busy=Radar.Busy;
        if(previousRadar!=Radar){previousRadar=Radar;wasBusy=busy;tipText=null;ToolTip=null;}
        if(busy)showJournal=false;
        else if(wasBusy&&(Radar.Error.Length>0||Radar.Results.Count==0))showJournal=true;
        wasBusy=busy;SyncLoading();Refresh();
    }

    void SyncLoading()
    {
        if(disposed||!displayed||!IsVisible)StopTabAnimations();
        bool active=!disposed&&displayed&&IsVisible&&Radar.Busy;
        if(active)
        {
            EnsureLoadingDrawing();
            if(!loadingTimer.IsEnabled){loadingTimer.Start();StartLoadingMotion();}
        }
        else
        {
            loadingTimer.Stop();loadingMotion.BeginAnimation(TranslateTransform.XProperty,null);
        }
    }
    void EnsureLoadingDrawing()
    {
        double width=Math.Max(1,Width-48);
        var color=B(Ink).Color;
        if(loadingDrawing is not null&&loadingWidth==width&&loadingColor==color)return;
        loadingWidth=width;loadingColor=color;
        double segment=Math.Min(180,width*.32);
        var glow=new LinearGradientBrush{StartPoint=new(0,.5),EndPoint=new(1,.5)};
        foreach(var (offset,alpha) in new[]{(0d,0),(.25,28),(.5,210),(.75,28),(1d,0)})
            glow.GradientStops.Add(new(System.Windows.Media.Color.FromArgb((byte)alpha,color.R,color.G,color.B),offset));
        glow.Freeze();
        var shimmer=new DrawingGroup{Transform=loadingMotion};
        using(var draw=shimmer.Open())
        {
            draw.PushOpacity(.14);draw.DrawRoundedRectangle(glow,null,new Rect(0,177,segment,7),3.5,3.5);draw.Pop();
            draw.DrawRoundedRectangle(glow,null,new Rect(0,179,segment,3),1.5,1.5);
        }
        loadingDrawing=new DrawingGroup{ClipGeometry=new RectangleGeometry(new Rect(24,176,width,9))};
        loadingDrawing.Children.Add(shimmer);
        if(loadingTimer.IsEnabled)StartLoadingMotion();
    }
    void StartLoadingMotion()
    {
        double segment=Math.Min(180,loadingWidth*.32);
        loadingMotion.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(24-segment,24+loadingWidth,TimeSpan.FromSeconds(3.2))
        {RepeatBehavior=RepeatBehavior.Forever});
    }
    void LoadingTick(object? sender,EventArgs e)
    {
        SyncLoading();
        if(!loadingTimer.IsEnabled)return;
        Refresh();
    }
    void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)=>SyncLoading();
    void OnUnloaded(object sender,RoutedEventArgs e)
    {loadingTimer.Stop();loadingMotion.BeginAnimation(TranslateTransform.XProperty,null);StopTabAnimations();}

    sealed record ResultLayout(FormattedText Title,FormattedText Price,FormattedText Reason,FormattedText? Caveat,double Left,double ReasonTop,double Height);
    ResultLayout MeasureResult(RadarRow row)
    {
        double left=row.ImagePath.Length>0&&File.Exists(row.ImagePath)?90:32;
        double titleWidth=Math.Max(100,Width-32-174-left);
        string price=row.Price is null?"Prix à confirmer":ShoppingText.Money(row.Price);
        var title=Paragraph(row.Title,13,Ink,titleWidth);
        var priceText=Paragraph($"{price} · {row.Shop} · {row.Hit.Verdict.PriceLabel}",10.5,"#E4D3F0",titleWidth);
        var reason=Paragraph(row.Reason,11,Ink,Width-64);
        var caveat=row.Caveat.Length==0?null:Paragraph("Réserve : "+row.Caveat,10,Muted,Width-64);
        double reasonTop=10+Math.Max(42,title.Height+4+priceText.Height)+9;
        double height=reasonTop+reason.Height+(caveat is null?0:6+caveat.Height)+24;
        return new(title,priceText,reason,caveat,left,reasonTop,Math.Ceiling(height));
    }

    void Draw(RadarRow row,ResultLayout layout,double y,int index)
    {
        row=row with{Watched=Station.Shopping.Watchlist.Any(item=>item.Product.Id==row.Product.Id)};
        Box(20,y,Width-40,layout.Height-10,Tint(DesktopTheme.Current.Glass,0xB0),Tint(DesktopTheme.Current.Rim,0x24),12);
        HoverGlass(new Rect(20,y,Width-40,layout.Height-10),12);
        bool hasImage=row.ImagePath.Length>0&&File.Exists(row.ImagePath);
        if(hasImage)
        {
            Box(32,y+10,48,48,"#33140F1F","#26D9C7F0",10);
            Image(row.ImagePath,34,y+12,44,44);
        }
        double right=Width-32;
        D.DrawText(layout.Title,new Point(layout.Left,y+10));
        D.DrawText(layout.Price,new Point(layout.Left,y+14+layout.Title.Height));
        D.DrawText(layout.Reason,new Point(32,y+layout.ReasonTop));
        if(layout.Caveat is {} caveat)D.DrawText(caveat,new Point(32,y+layout.ReasonTop+layout.Reason.Height+6));
        Text(row.FitLabel,right-46,y+10,13,Color(row.Fit),"GTAArtDeco",align:"right");
        if(row.CompliancePercent is {} score)Text($"{score} % vérifiés",right-46,y+31,9.5,Muted,align:"right");
        // L'ouverture de l'offre est déclarée avant les boutons : le clic le plus précis
        // reste celui du bouton dessiné par-dessus.
        Hit("ShoppingOpen:"+index,20,y,Width-40,layout.Height-10,()=>Radar.Open(row));
        var bounds=new Rect(20,y+TabHeight,Width-40,layout.Height-10);if(InteractionClip is {} clip)bounds.Intersect(clip);
        if(!bounds.IsEmpty)rowTargets.Add((bounds,row));
        Button("ShoppingWatch:"+index,row.Watched?"★":"☆",right-38,y+8,30,30,()=>_=Station.Shopping.Watch(row,Radar.Query),15,row.Watched?Buy:Ink,enabled:row.Watched||row.Price is not null);
    }

    void PaintWatch(double y,int shown)
    {
        var list=Station.Shopping.Watchlist;
        Text("SURVEILLÉS",24,y,11,"#D8C8E3","GTAArtDeco");
        for(int i=0;i<shown;i++)
        {
            var row=list[i];
            double line=y+18+i*(WatchHeight+6);
            HoverGlass(new Rect(20,line,Width-60,WatchHeight-6),12);
            if(row.ImagePath.Length>0&&File.Exists(row.ImagePath))
            {
                Box(24,line,34,34,"#33140F1F","#26D9C7F0",8);
                Image(row.ImagePath,26,line+2,30,30);
            }
            double left=row.ImagePath.Length>0?66:24;
            Text(row.Title,left,line+2,11.5,Ink,width:Width-left-140);
            string detail=$"{ShoppingText.Money(row.Price)} · {row.Shop}";
            if(row.Item.Watch.TargetPrice is {} target)detail+=$" · cible {ShoppingText.Money(target)}";
            Text(detail,left,line+20,10,Muted,width:Width-left-140);
            Hit("ShoppingOpenWatch:"+i,20,line,Width-60,WatchHeight-6,()=>Radar.Open(row));
            Button("ShoppingUnwatch:"+i,"×",Width-24-30,line+4,30,26,()=>_=Station.Shopping.Unwatch(row.Product.Id),13,Muted);
        }
        if(list.Count>shown)Text($"+{list.Count-shown} autre{(list.Count-shown>1?"s":"")}",Width-24,y+2,10,Muted,align:"right");
    }

    static string Color(ProductFit fit)=>fit switch
    {
        ProductFit.Recommended=>Buy,
        ProductFit.Possible=>Wait,
        ProductFit.Unsuitable=>"#F4B7CA",
        _=>Muted
    };

    static string Details(RadarRow row)
    {
        var lines=new List<string>{row.Product.Title,$"{row.FitLabel} · {row.Reason}"};
        if(row.Hit.Assessment is {} assessment)
        {
            if(assessment.Criteria is {Count:>0} checks)
            {
                if(row.CompliancePercent is {} score)
                    lines.Add($"\n{checks.Count(item=>item.State==CriterionState.Confirmed)}/{checks.Count} critères confirmés · {score} %. Chaque critère pèse autant, budget compris s'il est demandé. Les informations inconnues restent à vérifier.");
                lines.Add("\nCritères demandés");
                foreach(var check in checks)
                    lines.Add($"• {check.Criterion} : {(check.State==CriterionState.Confirmed?"confirmé":check.State==CriterionState.Contradicted?"non respecté":"à vérifier")}{(check.Evidence.Length>0?" — "+check.Evidence:"")}");
            }
            if(assessment.Evidence.Count>0){lines.Add("\nÉléments relevés dans la fiche");lines.AddRange(assessment.Evidence.Select(item=>"• "+item));}
            if(assessment.Caveats.Count>0){lines.Add("\nRéserves");lines.AddRange(assessment.Caveats.Select(item=>"• "+item));}
            if(assessment.EvidenceSource.Length>0)lines.Add("\nFiche utilisée : "+assessment.EvidenceSource);
        }
        lines.Add("\nPrix · "+row.Hit.Verdict.PriceLabel);
        if(row.Price is null)lines.Add("Surveillance disponible après confirmation du prix.");
        lines.AddRange(row.Hit.Verdict.Reasons.Select(item=>"• "+item));
        if(row.Hit.Alternatives.Count>0){lines.Add("\nAutres offres observées");lines.AddRange(row.Hit.Alternatives.Select(item=>"• "+item));}
        lines.Add("\n"+row.Url);
        return string.Join('\n',lines);
    }

    protected override void OnPointer(MouseEventArgs e)=>UpdateToolTip();
    void UpdateToolTip()
    {
        var row=rowTargets.FirstOrDefault(target=>target.Bounds.Contains(Pointer)).Row;
        var text=row is not null?Details(row):summaryBounds.Contains(Pointer)?SummaryText():null;
        if(text==tipText)return;
        tipText=text;detailTip.Text=text??"";ToolTip=text is null?null:detailTip;
    }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        tipText=null;ToolTip=null;base.OnMouseLeave(e);
    }

    void Page(int delta){page=Math.Clamp(page+delta*96,0,(int)Math.Ceiling(maxScroll));Refresh();}
    void Run()=>Run(Radar.Query);
    void Run(string request){page=0;_=Radar.SearchAsync(request);}

    string SummaryText()=>Radar.SummaryDetails.Length>0?Radar.SummaryDetails:Radar.Error;
    internal void ConfigureSearch()
    {
        var window=new Window{Title="Battlestation · Connexion Serper",Width=520,SizeToContent=SizeToContent.Height,ShowInTaskbar=false,Owner=Window.GetWindow(this)};
        var content=new StackPanel();content.Children.Add(OverlayStyle.Text("Clé API Serper",18));
        var input=new PasswordBox{FontSize=16,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,16,0,10),
            Background=DockAppearance.ButtonFill,Foreground=B(Ink),BorderBrush=B(DockAppearance.ButtonRim)};
        content.Children.Add(input);
        var status=OverlayStyle.Text(Station.Serper.Error.Length>0?Station.Serper.Error:Station.Serper.HasKey?"Une clé est déjà enregistrée. Colle une nouvelle clé pour la remplacer.":"Copie la clé depuis Serper → API keys, puis colle-la ici.",12);
        status.TextWrapping=TextWrapping.Wrap;content.Children.Add(status);
        var note=OverlayStyle.Text("Conservée sur ce PC pour ton compte Windows. Aucun test API à l'enregistrement.",11);
        note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,8,0,14);content.Children.Add(note);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var cancel=OverlayStyle.Button("Annuler",()=>window.Close());cancel.IsCancel=true;actions.Children.Add(cancel);
        var save=OverlayStyle.Button("Enregistrer",()=>
        {
            if(Tabs.AnyBusy){status.Text="Arrête les recherches en cours avant de modifier la connexion.";return;}
            try{Station.Serper.Save(input.Password);input.Clear();window.Close();Refresh();}
            catch(Exception e) when(e is ArgumentException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
            {status.Text=e is ArgumentException?e.Message:"La clé n'a pas pu être enregistrée sur ce PC.";}
        });
        save.IsEnabled=false;save.IsDefault=true;input.PasswordChanged+=(_,_)=>{using var secret=input.SecurePassword;save.IsEnabled=secret.Length>0;};
        actions.Children.Add(save);content.Children.Add(actions);
        window.Content=OverlayStyle.Frame(content);OverlayStyle.Apply(window);OverlayStyle.Place(window);
        window.Loaded+=(_,_)=>input.Focus();window.ShowDialog();input.Clear();
    }
    void ShowSummary()
    {
        var text=OverlayStyle.Text(SummaryText(),13);text.TextWrapping=TextWrapping.Wrap;
        var window=new Window{Title="Battlestation · Détails de la recherche",Width=600,SizeToContent=SizeToContent.Height,
            ShowInTaskbar=false,Owner=Window.GetWindow(this)};
        var content=new StackPanel();content.Children.Add(OverlayStyle.Text("Détails de la recherche",18));
        content.Children.Add(new ScrollViewer{Content=text,MaxHeight=520,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,16,0,14)});
        var close=OverlayStyle.Button("Fermer",()=>window.Close());close.IsCancel=true;close.HorizontalAlignment=HorizontalAlignment.Right;content.Children.Add(close);
        window.Content=OverlayStyle.Frame(content);OverlayStyle.Apply(window);OverlayStyle.Place(window);window.ShowDialog();
    }

    /// <summary>
    /// Le champ de saisie est une fenêtre ancrée : les blocs du bureau ne prennent
    /// pas le focus clavier, et la saisie garde un curseur et un copier-coller normaux.
    /// </summary>
    void Edit()
    {
        var input=new TextBox
        {
            FontSize=17,TextWrapping=TextWrapping.Wrap,
            MaxHeight=240,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Text=Radar.Query,Margin=new Thickness(0,16,0,12)
        };
        var window=new Window
        {
            Title="Battlestation · Achats",Width=560,SizeToContent=SizeToContent.Height,
            ShowInTaskbar=false,Owner=Window.GetWindow(this)
        };
        var search=OverlayStyle.Button("Chercher",()=>{window.DialogResult=true;});
        search.IsDefault=true;search.IsEnabled=input.Text.Trim().Length>0;
        input.TextChanged+=(_,_)=>search.IsEnabled=input.Text.Trim().Length>0;
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var cancel=OverlayStyle.Button("Annuler",()=>window.DialogResult=false);cancel.IsCancel=true;
        actions.Children.Add(cancel);actions.Children.Add(search);
        var content=new StackPanel();
        content.Children.Add(OverlayStyle.Text("Demande",18));
        content.Children.Add(input);
        var levels=new ComboBox
        {
            ItemsSource=new[]{new KeyValuePair<string,string>("none","Sans réflexion"),new("low","Low"),new("high","High"),new("max","Max")},
            DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=Radar.ReasoningEffort,
            MinWidth=160,Margin=new Thickness(12,0,0,0),IsEnabled=Radar.Settings.Provider=="deepseek",
            Background=DockAppearance.ButtonFill,Foreground=B(Ink),BorderBrush=B(DockAppearance.ButtonRim)
        };
        levels.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ComboBox">
              <Grid>
                <Border CornerRadius="9" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1"/>
                <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="Transparent"/></ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter Margin="12,8,30,8" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"/>
                <TextBlock Text="⌄" Margin="0,0,12,0" HorizontalAlignment="Right" VerticalAlignment="Center" IsHitTestVisible="False"/>
                <Popup Name="PART_Popup" Placement="Bottom" AllowsTransparency="True" IsOpen="{TemplateBinding IsDropDownOpen}" PopupAnimation="Fade">
                  <Border MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" Background="#F0251C31" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="9" Padding="4">
                    <ScrollViewer MaxHeight="220" CanContentScroll="True"><ItemsPresenter/></ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
            </ControlTemplate>
            """);
        var optionStyle=new Style(typeof(ComboBoxItem));
        optionStyle.Setters.Add(new Setter(Control.ForegroundProperty,B(Ink)));
        optionStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,7,10,7)));
        levels.ItemContainerStyle=optionStyle;
        var reasoningRow=new DockPanel{Margin=new Thickness(0,0,0,12)};
        var reasoningLabel=OverlayStyle.Text("Réflexion",13);reasoningLabel.VerticalAlignment=VerticalAlignment.Center;
        DockPanel.SetDock(reasoningLabel,Dock.Left);reasoningRow.Children.Add(reasoningLabel);reasoningRow.Children.Add(levels);
        content.Children.Add(reasoningRow);
        content.Children.Add(OverlayStyle.Text("Produit, budget et critères · aucun achat automatique",11,"#BCAACD"));
        content.Children.Add(actions);
        window.Content=OverlayStyle.Frame(content);OverlayStyle.Apply(window);window.Height=280;OverlayStyle.Place(window);
        window.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
        if(window.ShowDialog()==true){Radar.SetReasoningEffort(levels.SelectedValue as string??"max");Run(input.Text);}
        Refresh();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Page(e.Delta>0?-1:1);e.Handled=true;
    }

    internal override void SetDisplayed(bool value)
    {
        displayed=value;
        base.SetDisplayed(value);
        SyncLoading();
        if(!value){rowTargets.Clear();tipText=null;ToolTip=null;}
        if(value)Radar.Reload();
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;loadingTimer.Stop();loadingTimer.Tick-=LoadingTick;
        StopTabAnimations();
        loadingMotion.BeginAnimation(TranslateTransform.XProperty,null);
        Tabs.Changed-=RadarChanged;IsVisibleChanged-=VisibilityChanged;Unloaded-=OnUnloaded;
        rowTargets.Clear();tipText=null;ToolTip=null;
    }
}
