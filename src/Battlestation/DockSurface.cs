using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Threading;

namespace Battlestation;
internal sealed class DockSurface : Surface
{
    readonly double[] amount=new double[12],from=new double[12],target=new double[12];
    readonly DateTime[] since=new DateTime[12];
    readonly bool[] press=new bool[12];
    readonly DispatcherTimer animation;
    int hover=-1;
    int rowOffset;
    int Columns=>Math.Max(1,Math.Min(Math.Max(1,Station.Apps.Count),(int)((Width-80)/88)));
    int VisibleRows=>Math.Max(1,(int)((Height-24)/88));
    void ClampRows()=>rowOffset=Math.Clamp(rowOffset,0,Math.Max(0,(int)Math.Ceiling(Station.Apps.Count/(double)Columns)-VisibleRows));
    public DockSurface(Station s):base(s,2){Width=720;Height=DesktopLayout.DockHeight(s.Apps.Count);animation=new DispatcherTimer(TimeSpan.FromMilliseconds(16),DispatcherPriority.Render,(_,_)=>Animate(),Dispatcher);animation.Stop();}
    void Retarget(int i,double value,bool clicked=false){if(i<0||i>=12)return;from[i]=amount[i];target[i]=value;since[i]=DateTime.UtcNow;press[i]=clicked;animation.Start();}
    void Animate(){bool active=false;for(int i=0;i<12;i++){double p=Math.Clamp((DateTime.UtcNow-since[i]).TotalMilliseconds/160,0,1);amount[i]=from[i]+(target[i]-from[i])*(1-Math.Pow(1-p,3));active|=p<1;}Refresh();if(!active)animation.Stop();}
    protected override void OnPointer(MouseEventArgs e)
    {
        ClampRows();int cols=Columns;double pitch=(Width-80)/cols;int next=-1;
        for(int i=rowOffset*cols;i<Math.Min(Station.Apps.Count,(rowOffset+VisibleRows)*cols);i++)if(new Rect(16+(i%cols)*pitch+pitch/2-44,8+(i/cols-rowOffset)*88,88,88).Contains(Pointer))next=i;
        if(next!=hover){Retarget(hover,0);hover=next;Retarget(hover,1);ToolTip=hover>=0?Station.Apps[hover].Name:null;}
    }
    protected override void OnMouseLeave(MouseEventArgs e){Retarget(hover,0);hover=-1;ToolTip=null;base.OnMouseLeave(e);}
    protected override void Paint()
    {
        ClampRows();double h=Height;
        int cols=Columns;double pitch=(Width-80)/cols,left=16;
        for(int i=rowOffset*cols;i<Math.Min(Station.Apps.Count,(rowOffset+VisibleRows)*cols);i++)
        {
            var app=Station.Apps[i];double x=left+(i%cols)*pitch+pitch/2,y=56+(i/cols-rowOffset)*88;
            var key=Regex.Replace(app.Name.ToLowerInvariant(),"[^a-z0-9]","");var path=Path.Combine(Station.Root,"dock/icons/neon",key+".png");
            double p=Math.Clamp((DateTime.UtcNow-since[i]).TotalMilliseconds/160,0,1);
            double size=72*(1+amount[i]/6-(press[i]?Math.Sin(p*Math.PI)*.065:0));y-=amount[i]*3;
            if(File.Exists(path))Image(path,x-size/2,y-size/2,size,size);else Text(app.Name[..1],x,y-22,26,align:"center");
            int index=i;Hit("Launch:"+app.Name,x-44,8+(i/cols-rowOffset)*88,88,88,()=>{Retarget(index,hover==index?1:0,true);Station.Launch(app);});
        }
        Line(Width-64,32,Width-64,h-32,"#22D2BDDF");Text("+",Width-35,h/2-15,17,Muted,align:"center");Hit("ManageApps",Width-58,15,50,h-30,()=>OpenEditor());
        int total=(int)Math.Ceiling(Station.Apps.Count/(double)cols);if(total>VisibleRows){double track=Height-32;Box(Width-70,16,3,track,"#305C4868",radius:2);Box(Width-70,16+track*rowOffset/total,3,track*VisibleRows/total,"#A0DAC3E5",radius:2);}
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e){rowOffset+=e.Delta>0?-1:1;ClampRows();Refresh();e.Handled=true;}
    internal void OpenEditor(Window? owner=null)
    {
        var window=new Window{Title="Applications du dock",Width=500,Height=475,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=B("#261A32"),Foreground=B(Ink)};
        OverlayStyle.Apply(window);if(owner is not null){window.Owner=owner;window.WindowStartupLocation=WindowStartupLocation.CenterOwner;}
        var panel=new DockPanel{Margin=new Thickness(18)};var buttons=new WrapPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
        var items=Station.Apps.ToList();var list=new ListBox{DisplayMemberPath="Name",Background=B("#201629"),Foreground=B(Ink),BorderThickness=new Thickness(0)};panel.Children.Add(list);
        void Reload(int index){list.ItemsSource=null;list.ItemsSource=items;list.SelectedIndex=Math.Clamp(index,-1,items.Count-1);}
        void Add(string text,Action action){var b=new Button{Content=text,Margin=new Thickness(3),Padding=new Thickness(10,7,10,7)};b.Click+=(_,_)=>action();buttons.Children.Add(b);}
        Add("Ajouter",()=>{if(items.Count>=12)return;var dialog=new OpenFileDialog{Filter="Applications|*.lnk;*.exe;*.url"};if(dialog.ShowDialog(window)==true){items.Add(new DockApp(Path.GetFileNameWithoutExtension(dialog.FileName),dialog.FileName));Reload(items.Count-1);}});
        Add("Retirer",()=>{int i=list.SelectedIndex;if(i>=0){items.RemoveAt(i);Reload(Math.Min(i,items.Count-1));}});
        Add("↑",()=>{int i=list.SelectedIndex;if(i>0){(items[i-1],items[i])=(items[i],items[i-1]);Reload(i-1);}});
        Add("↓",()=>{int i=list.SelectedIndex;if(i>=0&&i<items.Count-1){(items[i+1],items[i])=(items[i],items[i+1]);Reload(i+1);}});
        Add("Appliquer",()=>{try{Station.SaveApps(items);window.Close();Refresh();}catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException){MessageBox.Show(window,e.Message,"Applications du dock",MessageBoxButton.OK,MessageBoxImage.Information);}});Add("Annuler",window.Close);
        var title=OverlayStyle.Text("Applications du dock",20);title.Margin=new Thickness(0,0,0,15);DockPanel.SetDock(title,Dock.Top);panel.Children.Insert(1,title);
        window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};
        Reload(0);window.Content=OverlayStyle.Frame(panel);window.ShowDialog();
    }
}
