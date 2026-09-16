using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Terminal.Wpf;

namespace Battlestation;
internal sealed class TerminalView : Grid
{
    public TerminalControl Terminal {get;}=new(){AutoResize=true,Focusable=true};
    public ConPtySession Session {get;}
    bool connected;
    TerminalTheme? currentTheme;
    internal void ApplyTheme(TerminalTheme theme){currentTheme=theme;if(IsLoaded)Terminal.SetTheme(theme,"Cascadia Mono",14);}
    public TerminalView(string command,string directory,TerminalTheme? theme=null)
    {
        currentTheme=theme;
        Terminal.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=TerminalScrollBar.CreateStyle();
        Session=new ConPtySession(command,directory);Children.Add(Terminal);
        KeyboardNavigation.SetTabNavigation(this,KeyboardNavigationMode.Contained);
        KeyboardNavigation.SetDirectionalNavigation(this,KeyboardNavigationMode.Contained);
        Loaded+=(_,_)=>
        {
            if(currentTheme is {} colors)Terminal.SetTheme(colors,"Cascadia Mono",14);
            Session.Resize((uint)Math.Max(1,Terminal.Rows),(uint)Math.Max(1,Terminal.Columns));
            if(!connected){connected=true;Terminal.Connection=Session;}
        };
    }
}
