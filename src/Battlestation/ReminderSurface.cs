using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Battlestation;

internal sealed class ReminderSurface : Surface
{
    public ReminderSurface(Station station) : base(station,11)
    {
        Width=779;Height=112;
    }

    protected override void Paint()
    {
        Text("NUDGE",24,16,11,"#D8C8E3",bold:true);
        // Pearl-like reminder mark: a soft volume replaces the old flat trio
        // of circles while keeping the same compact footprint.
        D.DrawEllipse(B("#26101625"),null,new Point(79,53),19,6);
        var pearl=new RadialGradientBrush(Color.FromArgb(255,255,247,252),Color.FromArgb(255,207,177,216)){Center=new Point(.32,.26),GradientOrigin=new Point(.25,.18),RadiusX=.9,RadiusY=.9};
        pearl.Freeze();D.DrawEllipse(pearl,null,new Point(78,48),18,18);
        D.DrawEllipse(B("#BEE8D9"),null,new Point(72,44),7,8);
        D.DrawEllipse(B("#F7D9C5"),null,new Point(84,53),7,8);
        D.DrawEllipse(B("#B8F1E2"),null,new Point(74,39),3,3);

        if(Station.Reminders.Count==0)
        {
            Text("Rien à retenir pour l’instant",112,31,15,"#EEE5F1");
            Text("Ajoute une petite chose, pas une liste de vie",112,58,10,Muted);
        }
        else
        {
            var reminder=Station.Reminders[0];
            Text("Tu peux y aller tranquillement",112,17,10,Muted);
            Text(reminder.Text,112,38,17,"#F4EAF5",bold:true,width:Width-330);
            Text(Station.Reminders.Count==1?"1 rappel":$"{Station.Reminders.Count} rappels",112,70,9,Muted);
            Button("ReminderDone","✓",Width-198,28,52,48,Complete,17,"#D4F0DD");
            Button("ReminderLater","◷",Width-136,28,52,48,Later,17,"#E5D6F0");
        }
        Button("ReminderAdd","+",Width-68,28,48,48,Add,20,"#F2E4F4");
    }

    void Complete()
    {
        if(Station.Reminders.Count==0)return;
        Station.SaveReminders(Station.Reminders.Skip(1));Refresh();
    }

    void Later()
    {
        if(Station.Reminders.Count<2)return;
        Station.SaveReminders(Station.Reminders.Skip(1).Append(Station.Reminders[0]));Refresh();
    }

    void Add()
    {
        var input=new TextBox{MaxLength=140,MinWidth=360,FontSize=17,TextWrapping=TextWrapping.Wrap};
        var add=OverlayStyle.Button("Ajouter",()=>{if(!string.IsNullOrWhiteSpace(input.Text)){Station.SaveReminders(Station.Reminders.Append(new ReminderItem(input.Text)));Refresh();Window.GetWindow(input)!.DialogResult=true;}});
        var cancel=OverlayStyle.Button("Annuler",()=>Window.GetWindow(input)!.DialogResult=false);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(cancel);actions.Children.Add(add);
        var content=new StackPanel{Width=430};content.Children.Add(OverlayStyle.Text("Un petit rappel",18,"#F3E8F5"));content.Children.Add(OverlayStyle.Text("Écris-le comme tu le dirais à toi-même.",11,"#BCAACD"));content.Children.Add(input);content.Children.Add(actions);
        var window=new Window{Title="Nudge",Content=OverlayStyle.Frame(content),Width=500,Height=220,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Window.GetWindow(this),ShowInTaskbar=false};
        OverlayStyle.Apply(window);window.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};window.ShowDialog();
    }
}
