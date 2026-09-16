using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Battlestation;
internal sealed class CommandPaletteWindow : Window
{
    readonly TextBox search=new(){FontSize=21,BorderThickness=new Thickness(0),Background=Brushes.Transparent};
    readonly ListBox results=new(){Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
    readonly TextBlock context=OverlayStyle.Text("Applications, projets, commandes",12,"#BCAACD");
    readonly TextBlock empty=OverlayStyle.Text("Aucun résultat",15,"#BCAACD");
    readonly TextBlock placeholder=OverlayStyle.Text("Rechercher…",21,"#AC99BF");
    readonly List<PaletteEntry> catalog;
    readonly Func<string>? currentProfile;
    readonly Dictionary<string,Border> profileCards=[];
    readonly nint previous;
    IReadOnlyList<PaletteEntry>? actions;
    string previousQuery="";
    bool closing;
    internal CommandPaletteWindow(IEnumerable<PaletteEntry> entries,Func<string>? currentProfile=null)
    {
        previous=Native.GetForegroundWindow();catalog=entries.ToList();this.currentProfile=currentProfile;Title="Battlestation · Palette";Width=660;Height=520;ShowInTaskbar=false;Topmost=true;
        OverlayStyle.Apply(this);
        var layout=new DockPanel();
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
        var heading=new DockPanel();var close=OverlayStyle.Button("Échap",()=>Dismiss(true));close.FontSize=11;DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);heading.Children.Add(OverlayStyle.Text("BATTLESTATION",12,"#CDB9DF"));top.Children.Add(heading);
        var searchArea=new Grid{Margin=new Thickness(0,12,0,8)};placeholder.Margin=new Thickness(10,8,0,0);placeholder.IsHitTestVisible=false;searchArea.Children.Add(placeholder);searchArea.Children.Add(search);top.Children.Add(searchArea);top.Children.Add(context);
        var previews=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Height=118,Margin=new Thickness(0,10,0,0)};
        var previewRow=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var entry in catalog.Where(e=>e.ProfileName is not null&&e.ProfileName!="Personnel"))previewRow.Children.Add(ProfileCard(entry));
        previews.Content=previewRow;top.Children.Add(previews);
        top.Children.Add(new Border{Height=1,Background=OverlayStyle.B("#3CDBCAE8"),Margin=new Thickness(0,15,0,12)});
        var footer=OverlayStyle.Text("↑ ↓  Parcourir       Entrée  Ouvrir       >  Commandes",11,"#AB9ABB");footer.Margin=new Thickness(0,12,0,0);DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var center=new Grid();center.Children.Add(results);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;center.Children.Add(empty);layout.Children.Add(center);
        Content=OverlayStyle.Frame(layout);
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,10,12,10)));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,2,0,2)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));style.Setters.Add(new Setter(UIElement.FocusableProperty,false));
        var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));
        foreach(var pair in new[]{(Border.BackgroundProperty,"Background"),(Border.BorderBrushProperty,"BorderBrush"),(Border.BorderThicknessProperty,"BorderThickness"),(Border.PaddingProperty,"Padding")})frame.SetBinding(pair.Item1,new System.Windows.Data.Binding(pair.Item2){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=frame}));
        foreach(var property in new[]{ListBoxItem.IsSelectedProperty,UIElement.IsMouseOverProperty}){var trigger=new Trigger{Property=property,Value=true};trigger.Setters.Add(new Setter(Control.BackgroundProperty,OverlayStyle.B("#32D4BCEB")));trigger.Setters.Add(new Setter(Control.BorderBrushProperty,OverlayStyle.B("#62CCB5E4")));style.Triggers.Add(trigger);}results.ItemContainerStyle=style;
        AutomationProperties.SetName(search,"Rechercher une application, un projet ou une commande");AutomationProperties.SetName(results,"Résultats");
        search.TextChanged+=(_,_)=>{placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Rebuild();};PreviewKeyDown+=HandleKey;
        results.PreviewMouseLeftButtonUp+=(_,e)=>{var item=ItemsControl.ContainerFromElement(results,e.OriginalSource as DependencyObject) as ListBoxItem;if(item is not null){results.SelectedItem=item;Execute();e.Handled=true;}};
        Deactivated+=(_,_)=>Dismiss(false);Loaded+=(_,_)=>{search.Focus();Keyboard.Focus(search);};Closed+=(_,_)=>closing=true;
        Rebuild();UpdateProfileSelection();
    }
    Border ProfileCard(PaletteEntry entry)
    {
        var card=new Border{Width=188,Height=104,Margin=new Thickness(0,0,8,0),Padding=new Thickness(8),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1),Background=Brush("#241A30D8"),BorderBrush=Brush("#3C334A80")};
        var stack=new StackPanel();var title=OverlayStyle.Text(entry.ProfileName!,13,"#E6D9F0");title.Margin=new Thickness(2,0,0,4);stack.Children.Add(title);
        var plan=new Border{Height=62,Background=Brush("#33253FCC"),CornerRadius=new CornerRadius(7),Child=MiniMap(entry.ProfileName!)};stack.Children.Add(plan);card.Child=stack;
        card.MouseLeftButtonUp+=(_,e)=>{entry.Execute();UpdateProfileSelection();e.Handled=true;};
        profileCards[entry.ProfileName!]=card;return card;
    }
    UIElement MiniMap(string name)
    {
        var canvas=new Canvas{Width=166,Height=60};
        foreach(var block in catalog.First(e=>e.ProfileName==name).Preview??[])
        {
            var x=block.X/5120*162+2;var y=block.Y/1440*50+4;var w=Math.Max(3,block.Width/5120*162);var h=Math.Max(3,block.Height/1440*50);
            canvas.Children.Add(new Border{Width=Math.Min(160-x,w),Height=Math.Min(50-y,h),Background=Brush("#A8D5B58C"),BorderBrush=Brush("#F3E7D7E8"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),Margin=new Thickness(x,y,0,0)});
        }
        return canvas;
    }
    void UpdateProfileSelection()
    {
        var selected=currentProfile?.Invoke();
        foreach(var pair in profileCards)pair.Value.BorderBrush=Brush(pair.Key==selected?"#CDA9F0E8":"#3C334A80");
    }
    static SolidColorBrush Brush(string value)=>DesktopTheme.Brush(value);
    internal void AddProjects(IEnumerable<PaletteEntry> entries)
    {
        if(closing)return;foreach(var entry in entries)if(!catalog.Any(e=>e.Id==entry.Id))catalog.Add(entry);if(actions is null)Rebuild();
    }
    internal void ProjectActions(string name,Action explorer,Action codex,Action kilo)
    {
        previousQuery=search.Text;actions=[new("explorer","Explorateur",name,"\uE8B7",explorer),new("codex","Codex CLI",name,"\uE756",codex),new("kilo","Kilo CLI",name,"\uE756",kilo)];context.Text=name+"  ·  Échap pour revenir";placeholder.Text="Choisir une action";search.Text="";Rebuild();search.Focus();
    }
    void Rebuild()
    {
        var found=PaletteSearch.Find(actions??catalog,search.Text);results.Items.Clear();
        foreach(var entry in found)
        {
            var row=new DockPanel();var icon=OverlayStyle.Text(entry.Icon,22,"#E0D1F0");icon.FontFamily=new FontFamily("Segoe Fluent Icons");icon.Width=42;icon.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
            var words=new StackPanel();words.Children.Add(OverlayStyle.Text(entry.Title,16));var detail=OverlayStyle.Text(entry.Detail,11,"#BCAACD");detail.TextTrimming=TextTrimming.CharacterEllipsis;detail.TextWrapping=TextWrapping.NoWrap;words.Children.Add(detail);row.Children.Add(words);
            var item=new ListBoxItem{Content=row,Tag=entry};AutomationProperties.SetName(item,entry.Title+", "+entry.Detail);results.Items.Add(item);
        }
        empty.Visibility=found.Count==0?Visibility.Visible:Visibility.Collapsed;
        Height=Math.Clamp(210+found.Count*62,300,582);
        if(found.Count>0)results.SelectedIndex=0;
    }
    void HandleKey(object sender,KeyEventArgs e)
    {
        if(e.Key is Key.Down or Key.Up or Key.Tab)
        {
            int direction=e.Key==Key.Up||(e.Key==Key.Tab&&Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))?-1:1;
            if(results.Items.Count>0){results.SelectedIndex=Math.Clamp(results.SelectedIndex+direction,0,results.Items.Count-1);results.ScrollIntoView(results.SelectedItem);}e.Handled=true;
        }
        else if(e.Key==Key.Enter){Execute();e.Handled=true;}
        else if(e.Key==Key.Escape)
        {
            if(actions is not null){actions=null;context.Text="Applications, projets, commandes";placeholder.Text="Rechercher…";search.Text=previousQuery;Rebuild();search.Focus();}else Dismiss(true);e.Handled=true;
        }
    }
    void Execute()
    {
        if(results.SelectedItem is not ListBoxItem{Tag:PaletteEntry entry})return;
        // Project selection stays in the palette; an actual launch closes it first.
        try{if(entry.Id.StartsWith("project:"))entry.Execute();else{Dismiss(false);entry.Execute();}}
        catch(Exception e){MessageBox.Show(e.Message,"Battlestation",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    internal void Dismiss(bool restore)
    {
        if(closing)return;closing=true;bool ownedFocus=Native.GetForegroundWindow()==new WindowInteropHelper(this).Handle;Close();
        if(restore&&ownedFocus&&previous!=0&&Native.IsWindow(previous))Native.SetForegroundWindow(previous);
    }
}
