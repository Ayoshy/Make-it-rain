using System.Windows;
using System.Windows.Controls;
namespace Battlestation;
internal sealed class NetworkSettingsWindow : Window
{
    internal NetworkSettingsWindow(NetworkSampler sampler)
    {
        Title="Battlestation · Réseau";Width=570;Height=580;ShowInTaskbar=false;OverlayStyle.Apply(this);
        var root=new DockPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var close=OverlayStyle.Button("×",Close);DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(OverlayStyle.Text("Réseau",24));
        var footer=new DockPanel{Margin=new Thickness(0,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var feedback=OverlayStyle.Text("",11,"#F4B7CA");
        string selected=sampler.Preferences.InterfaceId;
        var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        body.Children.Add(OverlayStyle.Text("Interface",16));var choices=new List<Button>();
        void Choice(string id,string label)
        {
            Button? button=null;button=OverlayStyle.Button(label,()=>{selected=id;foreach(var choice in choices)choice.Opacity=(string)choice.Tag==selected?1:.5;});
            button.Tag=id;button.Opacity=selected==id?1:.5;button.HorizontalContentAlignment=HorizontalAlignment.Left;choices.Add(button);body.Children.Add(button);
        }
        Choice("","Automatique · route par défaut");
        foreach(var adapter in sampler.Snapshot.Adapters)Choice(adapter.Id,adapter.Name+(adapter.Up?"":" · déconnectée"));
        var title=OverlayStyle.Text("Cible de latence",16);title.Margin=new Thickness(0,20,0,10);body.Children.Add(title);
        var target=new TextBox{Text=sampler.Preferences.Target};body.Children.Add(target);
        body.Children.Add(OverlayStyle.Text("Adresse IP ou nom d’hôte · une mesure par seconde",11,"#BCAACD"));
        var save=OverlayStyle.Button("Enregistrer",()=>{try{sampler.Configure(new(selected,target.Text));Close();}catch(Exception e) when(e is ArgumentException or System.IO.IOException or UnauthorizedAccessException){feedback.Text=e.Message;}});
        DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);footer.Children.Add(feedback);
        Content=OverlayStyle.Frame(root);
    }
}
