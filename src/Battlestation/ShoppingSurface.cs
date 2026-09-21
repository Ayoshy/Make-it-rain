using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Battlestation.Shopping;

namespace Battlestation;

/// <summary>
/// Dock « Achats » : la demande, les offres relevées avec leur verdict et la veille.
/// Le clic ouvre l'annonce ; le dernier achat reste toujours à l'utilisateur.
/// </summary>
internal sealed class ShoppingSurface : Surface
{
    const double RowHeight=74,WatchHeight=46;
    const string Buy="#A6ECBD",Wait="#F0D08A";
    int page;

    public ShoppingSurface(Station station):base(station,18)
    {
        Width=700;Height=520;
        ToolTip="Radar d'achat · aucun achat automatique";
        // Le moteur travaille en fond : chaque changement d'état redessine le dock.
        station.Shopping.Changed+=()=>
        {
            if(Dispatcher.CheckAccess())Refresh();
            else Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(Refresh));
        };
    }

    ShoppingRadar Radar=>Station.Shopping;

    protected override void Paint()
    {
        var radar=Radar;
        Text("ACHATS",24,12,14,"#D8C8E3","GTAArtDeco");
        if(radar.Busy)Text("recherche…",Width-24,16,11,Wait,align:"right");

        double fieldWidth=Math.Max(160,Width-48-100);
        Box(24,38,fieldWidth,40,"#5A140F1F","#3AD9C7F0",12);
        bool empty=radar.Query.Length==0;
        Text(empty?"frigo max 800 €, no frost, 300 L":radar.Query,36,49,13,empty?Muted:Ink,width:fieldWidth-24);
        Hit("ShoppingEdit",24,38,fieldWidth,40,Edit);
        Button("ShoppingSearch","Chercher",Width-24-88,38,88,40,Run,12,enabled:radar.Query.Length>0&&!radar.Busy);

        double y=88;
        foreach(var line in radar.Status.Split('\n').Take(2))
        {
            Text(radar.Error.Length>0?radar.Error:line,24,y,11,radar.Error.Length>0?"#F4B7CA":Muted,width:Width-48);
            y+=16;
        }
        y+=6;

        double watchHeight=radar.Watchlist.Count==0?0:Math.Min(radar.Watchlist.Count,2)*(WatchHeight+6)+18;
        double available=Math.Max(RowHeight,Height-y-watchHeight-12);
        int visible=Math.Max(1,(int)(available/RowHeight));
        int pages=Math.Max(1,(radar.Results.Count+visible-1)/visible);
        page=Math.Clamp(page,0,pages-1);
        var rows=radar.Results.Skip(page*visible).Take(visible).ToArray();

        if(rows.Length==0&&radar.Results.Count==0)
            Text(empty?"Décris la demande, le budget et les critères":radar.Status.Split('\n')[0],24,y+14,14,Muted,width:Width-48);

        for(int i=0;i<rows.Length;i++)Draw(rows[i],y+i*RowHeight,page*visible+i);
        if(pages>1)
        {
            double bar=Height-watchHeight-34;
            Button("ShoppingPrevious","‹",Width-108,bar,26,26,()=>Page(-1),13,Muted,enabled:page>0);
            Text($"{page+1} / {pages}",Width-72,bar+6,10,Muted,align:"center");
            Button("ShoppingNext","›",Width-36,bar,26,26,()=>Page(1),13,Muted,enabled:page+1<pages);
        }
        if(watchHeight>0)PaintWatch(Height-watchHeight+2);
    }

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
        double textWidth=Math.Max(120,right-152-left);
        Text(row.Title,left,y+8,13,Ink,width:textWidth);
        string price=ShoppingText.Money(row.Price);
        string criteria=row.Hit.CriteriaText;
        Text($"{price} · {row.Shop}{(criteria.Length>0?$" · {criteria}":"")}",left,y+28,11.5,"#E4D3F0",width:textWidth);
        if(row.Hit.Verdict.Reasons.Count>0)Text(row.Hit.Verdict.Reasons[0],left,y+47,10,Muted,width:textWidth);
        Text(row.Hit.Verdict.Label,right-46,y+10,13,Color(row.Hit.Verdict.Decision),"GTAArtDeco",align:"right");
        Text($"{(int)Math.Round(row.Hit.Verdict.Confidence*100)} %",right-46,y+30,10,Muted,align:"right");
        // L'ouverture de l'offre est déclarée avant les boutons : le clic le plus précis
        // reste celui du bouton dessiné par-dessus.
        Hit("ShoppingOpen:"+index,20,y,Width-64,RowHeight,()=>Radar.Open(row));
        Button("ShoppingWatch:"+index,row.Watched?"★":"☆",right-38,y+8,30,30,()=>Radar.Watch(row),15,row.Watched?Buy:Ink);
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
            Button("ShoppingUnwatch:"+i,"×",Width-24-30,line+4,30,26,()=>Radar.Unwatch(row.Product.Id),13,Muted);
        }
        if(list.Count>shown)Text($"+{list.Count-shown} autre{(list.Count-shown>1?"s":"")}",Width-24,y+2,10,Muted,align:"right");
    }

    static string Color(VerdictDecision decision)=>decision switch
    {
        VerdictDecision.Acheter=>Buy,
        VerdictDecision.Attendre=>Wait,
        _=>Muted
    };

    void Page(int delta){page+=delta;Refresh();}
    void Run(){page=0;_=Radar.SearchAsync(Radar.Query);}

    /// <summary>
    /// Le champ de saisie est une fenêtre ancrée : les blocs du bureau ne prennent
    /// pas le focus clavier, et la saisie garde un curseur et un copier-coller normaux.
    /// </summary>
    void Edit()
    {
        var input=new TextBox
        {
            MaxLength=180,FontSize=17,TextWrapping=TextWrapping.Wrap,
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
        if(window.ShowDialog()==true)Run();
        Refresh();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Page(e.Delta>0?-1:1);e.Handled=true;
    }

    internal override void SetDisplayed(bool value)
    {
        base.SetDisplayed(value);
        if(value)Radar.Reload();
    }
}
