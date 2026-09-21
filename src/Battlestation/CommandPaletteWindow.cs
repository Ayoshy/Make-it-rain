using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
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
    readonly Dictionary<string,bool> hovered=[];
    readonly nint previous;
    IReadOnlyList<PaletteEntry>? actions;
    string previousQuery="";
    bool closing;
    internal CommandPaletteWindow(IEnumerable<PaletteEntry> entries,Func<string>? currentProfile=null)
    {
        previous=Native.GetForegroundWindow();catalog=entries.ToList();this.currentProfile=currentProfile;Title="Battlestation · Palette";Width=886;Height=520;ShowInTaskbar=false;Topmost=true;
        OverlayStyle.Apply(this);
        var layout=new DockPanel();
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
        var heading=new DockPanel();var close=OverlayStyle.Button("Échap",()=>Dismiss(true));close.FontSize=11;DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);heading.Children.Add(OverlayStyle.Text("BATTLESTATION",12,"#CDB9DF"));top.Children.Add(heading);
        var searchArea=new Grid{Margin=new Thickness(0,12,0,8)};placeholder.Margin=new Thickness(10,8,0,0);placeholder.IsHitTestVisible=false;searchArea.Children.Add(placeholder);searchArea.Children.Add(search);top.Children.Add(searchArea);top.Children.Add(context);
        var cardStack=new StackPanel();
        var railScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,Padding=new Thickness(0,0,4,0)};
        railScroll.Resources[typeof(ScrollBar)]=RailScrollBar();railScroll.Content=cardStack;
        var railLabel=OverlayStyle.Text("Dispositions",12,"#CDB9DF");railLabel.Margin=new Thickness(2,0,0,10);
        var rail=new DockPanel{Width=196,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(railLabel,Dock.Top);rail.Children.Add(railLabel);rail.Children.Add(railScroll);
        foreach(var entry in catalog.Where(e=>e.ProfileName is not null&&e.ProfileName!="Personnel"))cardStack.Children.Add(ProfileCard(entry));
        var divider=new Border{Width=1,Background=OverlayStyle.B("#3CDBCAE8"),Margin=new Thickness(0,0,14,0)};
        top.Children.Add(new Border{Height=1,Background=OverlayStyle.B("#3CDBCAE8"),Margin=new Thickness(0,15,0,12)});
        var footer=OverlayStyle.Text("↑ ↓  Parcourir       Entrée  Ouvrir       >  Commandes",11,"#AB9ABB");footer.Margin=new Thickness(0,12,0,0);DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var center=new Grid();center.Children.Add(results);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;center.Children.Add(empty);layout.Children.Add(center);
        var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(rail,0);Grid.SetColumn(divider,1);Grid.SetColumn(layout,2);columns.Children.Add(rail);columns.Children.Add(divider);columns.Children.Add(layout);Content=OverlayStyle.Frame(columns);
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,10,12,10)));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,2,0,2)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));style.Setters.Add(new Setter(UIElement.FocusableProperty,false));
        var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));
        foreach(var pair in new[]{(Border.BackgroundProperty,"Background"),(Border.BorderBrushProperty,"BorderBrush"),(Border.BorderThicknessProperty,"BorderThickness"),(Border.PaddingProperty,"Padding")})frame.SetBinding(pair.Item1,new System.Windows.Data.Binding(pair.Item2){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=frame}));
        foreach(var property in new[]{ListBoxItem.IsSelectedProperty,UIElement.IsMouseOverProperty}){var trigger=new Trigger{Property=property,Value=true};trigger.Setters.Add(new Setter(Control.BackgroundProperty,OverlayStyle.B("#32D4BCEB")));trigger.Setters.Add(new Setter(Control.BorderBrushProperty,OverlayStyle.B("#62CCB5E4")));style.Triggers.Add(trigger);}results.ItemContainerStyle=style;
        AutomationProperties.SetName(search,"Rechercher une application, un projet ou une commande");AutomationProperties.SetName(results,"Résultats");
        search.TextChanged+=(_,_)=>{placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Rebuild();};PreviewKeyDown+=HandleKey;
        results.PreviewMouseLeftButtonUp+=(_,e)=>{var item=ItemsControl.ContainerFromElement(results,e.OriginalSource as DependencyObject) as ListBoxItem;if(item is not null){results.SelectedItem=item;Execute();e.Handled=true;}};
        Deactivated+=(_,_)=>Dismiss(false);Loaded+=(_,_)=>{search.Focus();Keyboard.Focus(search);var selected=currentProfile?.Invoke();if(selected is not null&&profileCards.TryGetValue(selected,out var active))active.BringIntoView();};Closed+=(_,_)=>closing=true;
        Rebuild();UpdateProfileSelection();
    }
    Border ProfileCard(PaletteEntry entry)
    {
        var name=entry.ProfileName!;
        var card=new Border{Width=180,MinHeight=98,Margin=new Thickness(0,0,0,8),Padding=new Thickness(8),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),Background=Brush("#241A30D8"),BorderBrush=Brush("#3C334A80"),Cursor=Cursors.Hand};
        var stack=new StackPanel();var title=OverlayStyle.Text(name,13,"#E6D9F0");title.Margin=new Thickness(2,0,0,6);stack.Children.Add(title);
        var plan=new Border{Height=56,Background=Brush("#2A1E38CC"),BorderBrush=Brush("#3C8C7BB0"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Child=MiniMap(name)};stack.Children.Add(plan);card.Child=stack;
        card.MouseLeftButtonUp+=(_,e)=>{entry.Execute();UpdateProfileSelection();e.Handled=true;};
        card.MouseEnter+=(_,_)=>{hovered[name]=true;UpdateProfileSelection();};
        card.MouseLeave+=(_,_)=>{hovered[name]=false;UpdateProfileSelection();};
        profileCards[name]=card;return card;
    }
    UIElement MiniMap(string name)
    {
        var canvas=new Canvas{Width=160,Height=54};
        var monitors=DesktopScreens.Current;
        double left=monitors.Min(screen=>screen.Dip.Left),top=monitors.Min(screen=>screen.Dip.Top);
        double width=Math.Max(1,monitors.Max(screen=>screen.Dip.Right)-left),height=Math.Max(1,monitors.Max(screen=>screen.Dip.Bottom)-top);
        foreach(var block in catalog.First(e=>e.ProfileName==name).Preview??[])
        {
            var x=(block.X-left)/width*156+2;var y=(block.Y-top)/height*46+4;
            var w=Math.Max(3,block.Width/width*156);var h=Math.Max(3,block.Height/height*46);
            canvas.Children.Add(new Border{Width=Math.Min(154-x,w),Height=Math.Min(46-y,h),Background=Brush("#A8D5B58C"),BorderBrush=Brush("#F3E7D7E8"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),Margin=new Thickness(x,y,0,0)});
        }
        return canvas;
    }
    void UpdateProfileSelection()
    {
        var selected=currentProfile?.Invoke();
        foreach(var pair in profileCards)
        {
            bool current=pair.Key==selected,over=hovered.GetValueOrDefault(pair.Key);
            pair.Value.BorderBrush=Brush(current?"#CDA9F0E8":over?"#62CCB5E4":"#3C334A80");
            pair.Value.Background=Brush(current?"#2E2140E8":over?"#2A1F3AD8":"#241A30D8");
        }
    }
    static SolidColorBrush Brush(string value)=>DesktopTheme.Brush(value);
    static Style RailScrollBar()=>(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'><Setter Property='Width' Value='12'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'><Track.Thumb><Thumb MinHeight='28'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Width='4' CornerRadius='2' HorizontalAlignment='Center' Background='#A9CDA9F0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0'/></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>");
    internal void AddProjects(IEnumerable<PaletteEntry> entries)
    {
        if(closing)return;foreach(var entry in entries)if(!catalog.Any(e=>e.Id==entry.Id))catalog.Add(entry);if(actions is null)Rebuild();
    }
    internal void ProjectActions(string name,Action explorer,Action codex,Action codexDs,Action kilo)
    {
        previousQuery=search.Text;actions=[new("explorer","Explorateur",name,"\uE8B7",explorer),new("codex","Codex CLI (ChatGPT)",name,"\uE756",codex),new("codex-ds","Codex CLI (DS)",name,"\uE756",codexDs),new("kilo","Kilo CLI (DS)",name,"\uE756",kilo)];context.Text=name+"  ·  Échap pour revenir";placeholder.Text="Choisir une action";search.Text="";Rebuild();search.Focus();
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
