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
    const double RowHeight=96,WatchHeight=46;
    const string Buy="#A6ECBD",Wait="#F0D08A";
    readonly List<(Rect Bounds,RadarRow Row)> rowTargets=[];
    readonly TextBlock detailTip=new(){MaxWidth=480,TextWrapping=TextWrapping.Wrap};
    readonly DispatcherTimer loadingTimer;
    readonly TranslateTransform loadingMotion=new();
    DrawingGroup? loadingDrawing;
    double loadingWidth;
    System.Windows.Media.Color loadingColor;
    string? tipText;
    int page;
    bool displayed=true,disposed,wasBusy,showJournal;

    public ShoppingSurface(Station station):base(station,18)
    {
        Width=700;Height=520;
        ToolTipService.SetShowDuration(this,30000);
        // Seule la durée demande un rafraîchissement du texte. Le reflet est
        // animé par le compositeur WPF, indépendamment du rendu du journal.
        loadingTimer=new(DispatcherPriority.Background,Dispatcher){Interval=TimeSpan.FromSeconds(1)};
        loadingTimer.Tick+=LoadingTick;
        station.Shopping.Changed+=RadarChanged;
        IsVisibleChanged+=VisibilityChanged;
        Unloaded+=OnUnloaded;
        showJournal=Radar.Error.Length>0&&Radar.Progress.Count>0;
        SyncLoading();
    }

    ShoppingRadar Radar=>Station.Shopping;

    protected override void Paint()
    {
        rowTargets.Clear();
        var radar=Radar;
        Text("ACHATS",24,12,14,"#D8C8E3","GTAArtDeco");
        if(!radar.Busy&&(radar.Progress.Count>0||radar.SearchStartedAt is not null))
            Button("ShoppingJournal",showJournal?"Résultats":"Journal",Width-108,7,84,25,()=>{showJournal=!showJournal;Refresh();},9.5,Muted);
        int pending=radar.Results.Count(row=>row.Fit==ProductFit.Unknown);

        double fieldWidth=Math.Max(160,Width-48-100);
        Box(24,38,fieldWidth,40,"#5A140F1F","#3AD9C7F0",12);
        bool empty=radar.Query.Length==0;
        Text(empty?"frigo max 800 €, no frost, 300 L":radar.Query,36,49,13,empty?Muted:Ink,width:fieldWidth-24);
        if(!radar.Busy)Hit("ShoppingEdit",24,38,fieldWidth,40,Edit);
        Button("ShoppingSearch","Chercher",Width-24-88,38,88,40,Run,12,enabled:radar.Query.Length>0&&!radar.Busy);

        if(radar.Busy||showJournal)
        {
            PaintJournal();
            UpdateToolTip();
            return;
        }

        double y=88;
        foreach(var line in (radar.Error.Length>0?radar.Error:radar.Status).Split('\n').Take(2))
        {
            Text(line,24,y,11,radar.Error.Length>0?"#F4B7CA":Muted,width:Width-48);
            y+=16;
        }
        y+=6;
        var visibleResults=radar.Results.OrderBy(row=>row.Fit==ProductFit.Unknown?1:0).ToArray();

        double watchHeight=radar.Watchlist.Count==0?0:Math.Min(radar.Watchlist.Count,2)*(WatchHeight+6)+18;
        int groups=pending>0&&pending<visibleResults.Length?2:1;
        double available=Math.Max(RowHeight,Height-y-watchHeight-12-groups*22);
        int visible=Math.Max(1,(int)(available/RowHeight));
        if(visibleResults.Length>visible)visible=Math.Max(1,(int)((available-32)/RowHeight));
        int pages=Math.Max(1,(visibleResults.Length+visible-1)/visible);
        page=Math.Clamp(page,0,pages-1);
        var rows=visibleResults.Skip(page*visible).Take(visible).ToArray();

        if(rows.Length==0&&empty)
            Text("Décris la demande, le budget et les critères",24,y+14,14,Muted,width:Width-48);

        bool? previousPending=null;
        for(int i=0;i<rows.Length;i++)
        {
            bool isPending=rows[i].Fit==ProductFit.Unknown;
            if(previousPending!=isPending)
            {
                Text(isPending?"PISTES · À VÉRIFIER":"CHOIX",24,y,11,Muted,"GTAArtDeco");y+=22;
                previousPending=isPending;
            }
            Draw(rows[i],y,page*visible+i);y+=RowHeight;
        }
        if(pages>1)
        {
            double bar=Height-watchHeight-34;
            Button("ShoppingPrevious","‹",Width-108,bar,26,26,()=>Page(-1),13,Muted,enabled:page>0);
            Text($"{page+1} / {pages}",Width-72,bar+6,10,Muted,align:"center");
            Button("ShoppingNext","›",Width-36,bar,26,26,()=>Page(1),13,Muted,enabled:page+1<pages);
        }
        if(watchHeight>0)PaintWatch(Height-watchHeight+2);
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
        int count=Math.Max(1,(int)((Height-248)/34));
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
        if(busy)showJournal=false;
        else if(wasBusy&&(Radar.Error.Length>0||Radar.Results.Count==0))showJournal=true;
        wasBusy=busy;SyncLoading();Refresh();
    }

    void SyncLoading()
    {
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
    {loadingTimer.Stop();loadingMotion.BeginAnimation(TranslateTransform.XProperty,null);}

    void Draw(RadarRow row,double y,int index)
    {
        bool hasImage=row.ImagePath.Length>0&&File.Exists(row.ImagePath);
        double left=24;
        if(hasImage)
        {
            Box(24,y+2,48,48,"#33140F1F","#26D9C7F0",10);
            Image(row.ImagePath,26,y+4,44,44);
            left=82;
        }
        double right=Width-24;
        double textWidth=Math.Max(120,right-174-left);
        Text(row.Title,left,y+8,13,Ink,width:textWidth);
        string price=row.Price is null?"Prix à confirmer":ShoppingText.Money(row.Price);
        Text($"{price} · {row.Shop} · {row.Hit.Verdict.PriceLabel}",left,y+29,10.5,"#E4D3F0",width:right-(row.CompliancePercent is null?48:174)-left);
        Text(row.Reason,left,y+49,11,Ink,width:right-left-12);
        if(row.Caveat.Length>0)Text("Réserve : "+row.Caveat,left,y+70,10,Muted,width:right-left-12);
        Text(row.FitLabel,right-46,y+10,13,Color(row.Fit),"GTAArtDeco",align:"right");
        if(row.CompliancePercent is {} score)Text($"{score} % vérifiés",right-46,y+31,9.5,Muted,align:"right");
        // L'ouverture de l'offre est déclarée avant les boutons : le clic le plus précis
        // reste celui du bouton dessiné par-dessus.
        Hit("ShoppingOpen:"+index,20,y,Width-64,RowHeight,()=>Radar.Open(row));
        rowTargets.Add((new Rect(20,y,Width-64,RowHeight),row));
        Button("ShoppingWatch:"+index,row.Watched?"★":"☆",right-38,y+8,30,30,()=>_=Radar.Watch(row),15,row.Watched?Buy:Ink,enabled:row.Watched||row.Price is not null);
        if(index<Station.Shopping.Results.Count-1)Line(24,y+RowHeight-2,Width-24,y+RowHeight-2,"#1AD9C7F0");
    }

    void PaintWatch(double y)
    {
        var list=Station.Shopping.Watchlist;
        Text("SURVEILLÉS",24,y,11,"#D8C8E3","GTAArtDeco");
        int shown=Math.Min(list.Count,2);
        for(int i=0;i<shown;i++)
        {
            var row=list[i];
            double line=y+18+i*(WatchHeight+6);
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
            Button("ShoppingUnwatch:"+i,"×",Width-24-30,line+4,30,26,()=>_=Radar.Unwatch(row.Product.Id),13,Muted);
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
        var text=row is null?null:Details(row);
        if(text==tipText)return;
        tipText=text;detailTip.Text=text??"";ToolTip=text is null?null:detailTip;
    }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        tipText=null;ToolTip=null;base.OnMouseLeave(e);
    }

    void Page(int delta){page+=delta;Refresh();}
    void Run()=>Run(Radar.Query);
    void Run(string request){page=0;_=Radar.SearchAsync(request);}

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
        content.Children.Add(OverlayStyle.Text("Produit, budget et critères · aucun achat automatique",11,"#BCAACD"));
        content.Children.Add(actions);
        window.Content=OverlayStyle.Frame(content);OverlayStyle.Apply(window);window.Height=280;OverlayStyle.Place(window);
        window.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
        if(window.ShowDialog()==true)Run(input.Text);
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
        loadingMotion.BeginAnimation(TranslateTransform.XProperty,null);
        Radar.Changed-=RadarChanged;IsVisibleChanged-=VisibilityChanged;Unloaded-=OnUnloaded;
        rowTargets.Clear();tipText=null;ToolTip=null;
    }
}
