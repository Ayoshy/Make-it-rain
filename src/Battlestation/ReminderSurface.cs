using System.Windows;
using System.Windows.Controls;

namespace Battlestation;

internal sealed class ReminderSurface : Surface
{
    public ReminderSurface(Station station) : base(station,11) { Width=779;Height=112; }

    protected override void Paint()
    {
        Text("NUDGE",24,12,14,"#D8C8E3","GTAArtDeco");
        bool hasReminder=Station.Reminders.Count>0;
        double textWidth=Width-(hasReminder?174:104);
        Text(hasReminder?Station.Reminders[0].Text:"Rien à retenir pour l’instant",
            24,39,21,"#F4EAF5","GTAArtDeco",width:textWidth);
        if(hasReminder)
        {
            // Edit the reminder directly; no separate edit button.
            Hit("ReminderEdit",24,36,textWidth,43,()=>Edit(false));
            Button("ReminderDone","✓",Width-130,32,44,44,Complete,16,"#D4F0DD");
        }
        if(Station.Reminders.Count>1)
        {
            Text($"1 / {Station.Reminders.Count}",24,83,9,Muted);
            Text("›",83,77,14,Muted);
            Hit("ReminderNext",20,77,90,30,Next);
        }
        Button("ReminderAdd","+",Width-72,32,44,44,()=>Edit(true),19,"#F2E4F4",enabled:Station.Reminders.Count<32);
    }

    void Complete()
    {
        if(Station.Reminders.Count==0)return;
        Station.SaveReminders(Station.Reminders.Skip(1));Refresh();
    }

    void Next()
    {
        if(Station.Reminders.Count<2)return;
        Station.SaveReminders(Station.Reminders.Skip(1).Append(Station.Reminders[0]));Refresh();
    }

    void Edit(bool adding)
    {
        if(!adding&&Station.Reminders.Count==0)return;
        var input=new TextBox
        {
            MaxLength=140,FontSize=17,TextWrapping=TextWrapping.Wrap,
            Text=adding?"":Station.Reminders[0].Text,Margin=new Thickness(0,16,0,12)
        };
        var window=new Window
        {
            Title="Nudge",Width=500,SizeToContent=SizeToContent.Height,
            ShowInTaskbar=false,Owner=Window.GetWindow(this)
        };
        var save=OverlayStyle.Button(adding?"Ajouter":"Enregistrer",()=>
        {
            if(string.IsNullOrWhiteSpace(input.Text))return;
            var reminder=new ReminderItem(input.Text);
            Station.SaveReminders(adding?Station.Reminders.Append(reminder):Station.Reminders.Skip(1).Prepend(reminder));
            Refresh();window.DialogResult=true;
        });
        save.IsDefault=true;save.IsEnabled=!string.IsNullOrWhiteSpace(input.Text);
        input.TextChanged+=(_,_)=>save.IsEnabled=!string.IsNullOrWhiteSpace(input.Text);
        var cancel=OverlayStyle.Button("Annuler",()=>window.DialogResult=false);cancel.IsCancel=true;
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        actions.Children.Add(cancel);actions.Children.Add(save);
        var content=new StackPanel();
        content.Children.Add(OverlayStyle.Text(adding?"Un petit rappel":"Modifier le rappel",18));
        content.Children.Add(input);content.Children.Add(actions);
        window.Content=OverlayStyle.Frame(content);OverlayStyle.Apply(window);
        // Place uses explicit dimensions before WPF's first measurement.
        window.Height=240;OverlayStyle.Place(window);
        window.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
        window.ShowDialog();
    }
}
