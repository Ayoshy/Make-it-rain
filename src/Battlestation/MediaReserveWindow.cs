using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Battlestation;
internal sealed class MediaReserveWindow : Window
{
    readonly Station station;
    readonly StackPanel items=new();
    internal MediaReserveWindow(Station state)
    {
        station=state;Title="Battlestation · À regarder, à écouter";Width=650;Height=640;OverlayStyle.Apply(this);
        var root=new DockPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var close=OverlayStyle.Button("×",Close);DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(OverlayStyle.Text("À regarder, à écouter",22));
        root.Children.Add(new ScrollViewer{Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=OverlayStyle.Frame(root);
        station.Reserve.Changed+=Render;Closed+=(_,_)=>station.Reserve.Changed-=Render;Render();station.Reserve.Warm();
    }
    void Render()
    {
        items.Children.Clear();
        if(station.Reserve.Items.Count==0)items.Children.Add(OverlayStyle.Text("Les liens YouTube et Spotify copiés apparaîtront ici.",14,"#BCAACD"));
        foreach(var item in station.Reserve.Items)
        {
            var row=new DockPanel{Margin=new Thickness(0,0,0,10)};
            var remove=OverlayStyle.Button("×",()=>station.Reserve.Remove(item.Url));remove.ToolTip="Retirer";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var open=OverlayStyle.Button("↗",()=>station.Launch(new DockApp(item.Title,item.Url)));open.ToolTip="Ouvrir";DockPanel.SetDock(open,Dock.Right);row.Children.Add(open);
            if(station.Reserve.Cover(item.Url) is {} cover){var image=new Image{Source=cover,Width=72,Height=56,Stretch=Stretch.UniformToFill,Margin=new Thickness(0,0,12,0)};DockPanel.SetDock(image,Dock.Left);row.Children.Add(image);}
            var label=new StackPanel{VerticalAlignment=VerticalAlignment.Center};label.Children.Add(OverlayStyle.Text(item.Title,14));label.Children.Add(OverlayStyle.Text(item.Provider,11,"#BCAACD"));row.Children.Add(label);items.Children.Add(row);
        }
        if(station.Reserve.Error!="")items.Children.Add(OverlayStyle.Text(station.Reserve.Error,12,"#F4B7CA"));
    }
}
