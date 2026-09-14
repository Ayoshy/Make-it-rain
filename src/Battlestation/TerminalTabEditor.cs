using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Battlestation;
internal sealed class TerminalTabEditor : Window
{
    readonly TextBox input;
    readonly TextBlock error;
    public TerminalTabEditor(string heading,string initial,bool color,Action<string> save)
    {
        Title=heading;Width=420;Height=230;SizeToContent=SizeToContent.Height;ShowInTaskbar=false;
        OverlayStyle.Apply(this);
        var stack=new StackPanel();stack.Children.Add(OverlayStyle.Text(heading,20));
        input=new TextBox{Text=initial,MaxLength=color?7:160,Margin=new Thickness(0,18,0,8)};stack.Children.Add(input);
        error=OverlayStyle.Text("",12,"#FFB9BC");stack.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        void Accept()
        {
            string value=input.Text.Trim();
            if(color&&TerminalTabPreferences.Color(value) is null){error.Text="Couleur au format #RRGGBB";return;}
            if(!color&&string.IsNullOrWhiteSpace(TerminalTabPreferences.Clean(value))){error.Text="Choisis un nom.";return;}
            try{save(value);Close();}catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException){error.Text=e.Message;}
        }
        buttons.Children.Add(OverlayStyle.Button("Annuler",Close));buttons.Children.Add(OverlayStyle.Button("Enregistrer",Accept));stack.Children.Add(buttons);Content=OverlayStyle.Frame(stack);
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.Enter){Accept();e.Handled=true;}};
        Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
    }
}
