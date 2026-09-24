using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Battlestation;

internal sealed class ReminderSurface : Surface
{
    const double RowHeight=44;
    int page;
    public ReminderSurface(Station station) : base(station,11) { Width=779;Height=112; }

    protected override void Paint()
    {
        Header("NUDGE",12);
        // The number of visible reminders follows the dock height, like the audio rows.
        int count=Math.Max(1,(int)((Height-96)/RowHeight));
        int pages=Math.Max(1,(Station.Reminders.Count+count-1)/count);
        page=Math.Clamp(page,0,pages-1);
        var rows=Station.Reminders.Skip(page*count).Take(count).ToArray();
        if(Station.Reminders.Count==0)Text("Rien à retenir pour l’instant",24,42,21,"#F4EAF5","GTAArtDeco",width:Width-104);
        for(int i=0;i<rows.Length;i++)
        {
            double y=48+i*RowHeight;int index=page*count+i;
            HoverGlass(new Rect(20,y,Width-84,38));
            Text(rows[i].Text,24,y+9,15,"#F4EAF5","GTAArtDeco",width:Width-104);
            // Edit the reminder directly; no separate edit button.
            Hit("ReminderEdit:"+index,20,y,Width-84,38,()=>Edit(index));
            Button("ReminderDone:"+index,"✓",Width-56,y+3,34,32,()=>Complete(index),15,"#D4F0DD");
        }
        if(pages>1)
        {
            Button("ReminderPrevious","‹",Width-176,14,26,26,()=>Page(-1),13,Muted,enabled:page>0);
            Text($"{page+1} / {pages}",Width-140,20,9,Muted,align:"center");
            Button("ReminderNext","›",Width-104,14,26,26,()=>Page(1),13,Muted,enabled:page+1<pages);
        }
        Button("ReminderAdd","+",Width-64,10,40,34,()=>Edit(-1),18,"#F2E4F4",enabled:Station.Reminders.Count<32);
    }

    void Complete(int index)
    {
        if(index<0||index>=Station.Reminders.Count)return;
        Station.SaveReminders(Station.Reminders.Where((_,position)=>position!=index));Refresh();
    }

    void Edit(int index)
    {
        bool adding=index<0;
        if(!adding&&(index<0||index>=Station.Reminders.Count))return;
        var input=new TextBox
        {
            MaxLength=140,FontSize=17,TextWrapping=TextWrapping.Wrap,
            Text=adding?"":Station.Reminders[index].Text,Margin=new Thickness(0,16,0,12)
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
            Station.SaveReminders(adding?Station.Reminders.Append(reminder):Station.Reminders.Select((item,position)=>position==index?reminder:item));
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
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Page(e.Delta>0?-1:1);e.Handled=true;
    }
    void Page(int delta){page+=delta;Refresh();}
}
