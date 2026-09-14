using System.Windows;
using System.Windows.Controls;

namespace Battlestation;
internal static class ProfileNameDialog
{
    internal static string? Show(Window? owner,string title,string initial="")
    {
        var input=new TextBox{Text=initial,MinWidth=300};
        var ok=OverlayStyle.Button("Enregistrer",()=>{});var cancel=OverlayStyle.Button("Annuler",()=>{});
        var dialog=new Window{Title=title,Width=430,Height=190,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,Content=null};
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(cancel);buttons.Children.Add(ok);
        var panel=new StackPanel{Margin=new Thickness(22)};panel.Children.Add(OverlayStyle.Text("Nom de la disposition",13,"#BEADCF"));panel.Children.Add(input);panel.Children.Add(buttons);
        dialog.Content=OverlayStyle.Frame(panel);if(owner is not null)dialog.Owner=owner;
        string? result=null;ok.Click+=(_,_)=>{result=input.Text;dialog.DialogResult=true;};cancel.Click+=(_,_)=>dialog.DialogResult=false;
        dialog.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};dialog.ShowDialog();return result;
    }
}
