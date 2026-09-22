using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Battlestation;
internal sealed class CommandPaletteWindow : Window
{
    readonly TextBox search=new(){FontSize=21,BorderThickness=new Thickness(0),Background=Brushes.Transparent};
    readonly ListBox results=new(){Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
    readonly TextBlock context=OverlayStyle.Text("Applications et projets",12,"#BCAACD");
    readonly TextBlock count=OverlayStyle.Text("",11,"#BCAACD");
    static readonly Dictionary<string,Task<BitmapSource?>> icons=new(StringComparer.OrdinalIgnoreCase);
    readonly TextBlock empty=OverlayStyle.Text("Aucun résultat",15,"#BCAACD");
    readonly TextBlock placeholder=OverlayStyle.Text("Rechercher…",21,"#AC99BF");
    readonly List<PaletteEntry> catalog;
    readonly string root;
    readonly Func<string>? currentProfile;
    readonly Dictionary<string,Border> profileCards=[];
    readonly Dictionary<string,bool> hovered=[];
    readonly nint previous;
    IReadOnlyList<PaletteEntry>? actions;
    string previousQuery="";
    bool closing;
    internal CommandPaletteWindow(IEnumerable<PaletteEntry> entries,string root,Func<string>? currentProfile=null)
    {
        this.root=root;
        previous=Native.GetForegroundWindow();catalog=entries.ToList();this.currentProfile=currentProfile;Title="Battlestation · Palette";Width=900;Height=600;ShowInTaskbar=false;Topmost=true;
        OverlayStyle.Apply(this);
        UseLayoutRounding=true;
        ScrollViewer.SetHorizontalScrollBarVisibility(results,ScrollBarVisibility.Disabled);
        results.Resources[typeof(ScrollBar)]=RailScrollBar();
        var layout=new DockPanel();
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
        var heading=new DockPanel{Margin=new Thickness(0,0,0,18)};
        var close=OverlayStyle.Button("Échap",()=>Dismiss(true));close.FontSize=10;close.Padding=new Thickness(10,5,10,5);close.Margin=new Thickness(0);DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
        var title=OverlayStyle.Text("Recherche",20);title.FontWeight=FontWeights.SemiBold;heading.Children.Add(title);top.Children.Add(heading);
        var searchRow=new DockPanel();var magnifier=Glyph("\uE721",20);magnifier.Margin=new Thickness(16,0,6,0);DockPanel.SetDock(magnifier,Dock.Left);searchRow.Children.Add(magnifier);
        var searchArea=new Grid();search.FontSize=19;search.Padding=new Thickness(10,14,12,14);search.VerticalContentAlignment=VerticalAlignment.Center;
        placeholder.FontSize=19;placeholder.Margin=new Thickness(10,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;searchArea.Children.Add(placeholder);searchArea.Children.Add(search);searchRow.Children.Add(searchArea);
        top.Children.Add(new Border{CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=Brush("#62CCB5E4"),Background=DockAppearance.ButtonFill,Child=searchRow});
        var resultHeading=new DockPanel{Margin=new Thickness(4,19,4,10)};DockPanel.SetDock(count,Dock.Right);resultHeading.Children.Add(count);resultHeading.Children.Add(context);top.Children.Add(resultHeading);
        var cardStack=new StackPanel();
        var railScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,Padding=new Thickness(0,0,4,0)};
        railScroll.Resources[typeof(ScrollBar)]=RailScrollBar();railScroll.Content=cardStack;
        var railLabel=OverlayStyle.Text("Scènes",13,"#CDB9DF");railLabel.FontWeight=FontWeights.SemiBold;railLabel.Margin=new Thickness(10,6,0,18);
        var rail=new DockPanel{Width=196,Margin=new Thickness(0,0,20,0)};DockPanel.SetDock(railLabel,Dock.Top);rail.Children.Add(railLabel);rail.Children.Add(railScroll);
        foreach(var entry in catalog.Where(e=>e.ProfileName is not null))cardStack.Children.Add(ProfileCard(entry));
        var divider=new Border{Width=1,Background=OverlayStyle.B(DockAppearance.ButtonRim),Margin=new Thickness(0,0,22,0)};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(4,14,0,0)};
        foreach(var (key,label) in new[]{("↑ ↓","Parcourir"),("↵","Ouvrir"),(">","Commandes")}){footer.Children.Add(Keycap(key));var hint=OverlayStyle.Text(label,11,"#BCAACD");hint.VerticalAlignment=VerticalAlignment.Center;hint.Margin=new Thickness(6,0,18,0);footer.Children.Add(hint);}
        DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var center=new Grid();center.Children.Add(results);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;center.Children.Add(empty);layout.Children.Add(center);
        var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(rail,0);Grid.SetColumn(divider,1);Grid.SetColumn(layout,2);columns.Children.Add(rail);columns.Children.Add(divider);columns.Children.Add(layout);Content=OverlayStyle.Frame(columns);
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,9,14,9)));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,2,4,2)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));style.Setters.Add(new Setter(UIElement.FocusableProperty,false));style.Setters.Add(new Setter(FrameworkElement.CursorProperty,Cursors.Hand));
        var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));
        foreach(var pair in new[]{(Border.BackgroundProperty,"Background"),(Border.BorderBrushProperty,"BorderBrush"),(Border.BorderThicknessProperty,"BorderThickness"),(Border.PaddingProperty,"Padding")})frame.SetBinding(pair.Item1,new System.Windows.Data.Binding(pair.Item2){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=frame}));
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,DockAppearance.ButtonHover));style.Triggers.Add(hover);
        var selectedRow=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selectedRow.Setters.Add(new Setter(Control.BackgroundProperty,DesktopTheme.Gradient("#32D4BCEB","#14DED2EE",15)));selectedRow.Setters.Add(new Setter(Control.BorderBrushProperty,DesktopTheme.Gradient("#62CCB5E4",DockAppearance.ButtonRim,25)));style.Triggers.Add(selectedRow);results.ItemContainerStyle=style;
        AutomationProperties.SetName(search,"Rechercher une application, un projet ou une commande");AutomationProperties.SetName(results,"Résultats");
        search.TextChanged+=(_,_)=>{placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Rebuild();};PreviewKeyDown+=HandleKey;
        results.PreviewMouseLeftButtonUp+=(_,e)=>{var item=ItemsControl.ContainerFromElement(results,e.OriginalSource as DependencyObject) as ListBoxItem;if(item is not null){results.SelectedItem=item;Execute();e.Handled=true;}};
        Deactivated+=(_,_)=>Dismiss(false);Loaded+=(_,_)=>{search.Focus();Keyboard.Focus(search);var selected=currentProfile?.Invoke();if(selected is not null&&profileCards.TryGetValue(selected,out var active))active.BringIntoView();};Closed+=(_,_)=>{closing=true;DesktopTheme.Changed-=RefreshTheme;};
        DesktopTheme.Changed+=RefreshTheme;
        Rebuild();UpdateProfileSelection();
    }
    Border ProfileCard(PaletteEntry entry)
    {
        var name=entry.ProfileName!;
        var card=new Border{Width=180,MinHeight=86,Margin=new Thickness(0,0,0,7),Padding=new Thickness(9),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),Cursor=Cursors.Hand};
        var stack=new StackPanel();var title=OverlayStyle.Text(name,13,"#E6D9F0");title.Margin=new Thickness(2,0,0,6);stack.Children.Add(title);
        var plan=new Border{Height=44,CornerRadius=new CornerRadius(7),Child=MiniMap(name),Opacity=.7};stack.Children.Add(plan);card.Child=stack;
        card.MouseLeftButtonUp+=(_,e)=>{entry.Execute();UpdateProfileSelection();e.Handled=true;};
        card.MouseEnter+=(_,_)=>{hovered[name]=true;UpdateProfileSelection();};
        card.MouseLeave+=(_,_)=>{hovered[name]=false;UpdateProfileSelection();};
        profileCards[name]=card;return card;
    }
    UIElement MiniMap(string name)
    {
        var canvas=new Canvas{Width=160,Height=44,ClipToBounds=true};
        var monitors=DesktopScreens.Current;
        double left=monitors.Min(screen=>screen.Dip.Left),top=monitors.Min(screen=>screen.Dip.Top);
        double width=Math.Max(1,monitors.Max(screen=>screen.Dip.Right)-left),height=Math.Max(1,monitors.Max(screen=>screen.Dip.Bottom)-top);
        foreach(var screen in monitors)
        {
            var panel=new Border{Width=screen.Dip.Width/width*156,Height=screen.Dip.Height/height*38,Background=DockAppearance.ButtonFill,BorderBrush=Brush(DockAppearance.ButtonRim),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4)};
            Canvas.SetLeft(panel,(screen.Dip.Left-left)/width*156+2);Canvas.SetTop(panel,(screen.Dip.Top-top)/height*38+3);canvas.Children.Add(panel);
        }
        foreach(var block in catalog.First(e=>e.ProfileName==name).Preview??[])
        {
            var x=(block.X-left)/width*156+3;var y=(block.Y-top)/height*38+4;
            var w=Math.Max(2,Math.Min(158-x,block.Width/width*156-1));var h=Math.Max(2,Math.Min(42-y,block.Height/height*38-1));
            var tile=new Border{Width=w,Height=h,Background=Brush("#32D4BCEB"),BorderBrush=Brush("#62CCB5E4"),BorderThickness=new Thickness(.6),CornerRadius=new CornerRadius(1.5)};Canvas.SetLeft(tile,x);Canvas.SetTop(tile,y);canvas.Children.Add(tile);
        }
        return canvas;
    }
    void UpdateProfileSelection()
    {
        var selected=currentProfile?.Invoke();
        foreach(var pair in profileCards)
        {
            bool current=pair.Key==selected,over=hovered.GetValueOrDefault(pair.Key);
            pair.Value.BorderBrush=Brush(current?"#62CCB5E4":over?DockAppearance.ButtonRim:"#00000000");
            pair.Value.Background=current?DockAppearance.ButtonHover:over?DockAppearance.ButtonFill:Brushes.Transparent;
            if(pair.Value.Child is StackPanel stack&&stack.Children[1] is Border plan)plan.Opacity=current?1:.6;
        }
    }
    static SolidColorBrush Brush(string value)=>DesktopTheme.Brush(value);
    static TextBlock Glyph(string text,double size){var glyph=OverlayStyle.Text(text,size,"#E0D1F0");glyph.FontFamily=new FontFamily("Segoe Fluent Icons");glyph.VerticalAlignment=VerticalAlignment.Center;glyph.HorizontalAlignment=HorizontalAlignment.Center;return glyph;}
    static Border Keycap(string text)=>new(){Padding=new Thickness(6,2,6,3),CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1),BorderBrush=Brush(DockAppearance.ButtonRim),Background=DockAppearance.ButtonFill,Child=OverlayStyle.Text(text,10,"#BCAACD")};
    void RefreshTheme()
    {
        var selected=(results.SelectedItem as ListBoxItem)?.Tag;
        Rebuild();
        foreach(ListBoxItem item in results.Items)if(Equals(item.Tag,selected)){results.SelectedItem=item;break;}
    }
    UIElement EntryIcon(PaletteEntry entry)
    {
        var content=new Grid();var fallback=Glyph(entry.Id.StartsWith("app:")?"\uE71D":entry.Icon,20);content.Children.Add(fallback);
        if(entry.Id.StartsWith("app:"))
        {
            // Use the same nacre artwork and scene variants as DockSurface/Surface.
            string key=Regex.Replace(entry.Title.ToLowerInvariant(),"[^a-z0-9]","");
            string directory=Path.Combine(root,"dock","icons");
            if(DesktopTheme.Current.Id!="vice-city")directory=Path.Combine(directory,"themes",DesktopTheme.Current.Id);
            string path=Path.Combine(directory,"neon",key+".png");
            if(!icons.TryGetValue(path,out var task))icons[path]=task=Task.Run<BitmapSource?>(()=>
            {
                if(!File.Exists(path))return null;
                var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(path);bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();return bitmap;
            });
            var image=new Image{Width=42,Height=42,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);content.Children.Add(image);_=SetIcon(image,fallback,task);
        }
        return new Border{Width=42,Height=42,Margin=new Thickness(0,0,13,0),CornerRadius=new CornerRadius(12),Background=DockAppearance.ButtonFill,BorderBrush=Brush(DockAppearance.ButtonRim),BorderThickness=new Thickness(1),Child=content};
    }
    async Task SetIcon(Image image,TextBlock fallback,Task<BitmapSource?> task)
    {
        try{var source=await task;if(closing||source is null)return;image.Source=source;fallback.Visibility=Visibility.Hidden;}
        catch(Exception e) when(e is System.IO.IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException){}
    }
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
            var row=new DockPanel();var icon=EntryIcon(entry);DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
            var enter=Keycap("↵");enter.VerticalAlignment=VerticalAlignment.Center;enter.Margin=new Thickness(12,0,0,0);enter.SetBinding(UIElement.VisibilityProperty,new System.Windows.Data.Binding("IsSelected"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(ListBoxItem),1),Converter=new BooleanToVisibilityConverter()});DockPanel.SetDock(enter,Dock.Right);row.Children.Add(enter);
            var words=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var title=OverlayStyle.Text(entry.Title,15);title.FontWeight=FontWeights.Medium;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(title);var detail=OverlayStyle.Text(entry.Detail,11,"#BCAACD");detail.Margin=new Thickness(0,3,0,0);detail.TextTrimming=TextTrimming.CharacterEllipsis;detail.TextWrapping=TextWrapping.NoWrap;words.Children.Add(detail);row.Children.Add(words);
            var item=new ListBoxItem{Content=row,Tag=entry};AutomationProperties.SetName(item,entry.Title+", "+entry.Detail);results.Items.Add(item);
        }
        empty.Visibility=found.Count==0?Visibility.Visible:Visibility.Collapsed;
        count.Text=found.Count.ToString("00");
        if(actions is null)context.Text=search.Text.TrimStart().StartsWith('>')?"Commandes":search.Text.Length>0?"Résultats":"Applications et projets";
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
