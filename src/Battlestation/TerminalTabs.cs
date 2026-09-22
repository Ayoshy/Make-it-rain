using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Battlestation;
internal sealed record TerminalTabInfo(Guid Id,string Title,bool Active,string? Accent=null,TerminalActivity Activity=TerminalActivity.Unknown,bool Effects=true,bool AutomaticTitle=true,string? SourceTitle=null,string? Badge=null,TerminalCacheHint CacheHint=TerminalCacheHint.None,string? CacheDetail=null);
internal enum TerminalTabAction { Rename,Color,CustomColor,AutomaticTitle,Effects,Reset,StatusHelp }
internal sealed class TerminalTabs : Grid
{
    readonly StackPanel strip=new(){Orientation=Orientation.Horizontal};
    readonly ScrollViewer scroll;
    readonly Button previous,next;
    readonly Style buttonStyle;
    readonly DispatcherTimer cacheTimer;
    readonly Dictionary<Guid,Card> cards=[];
    TerminalTabInfo[] current=[];
    public event Action<Guid>? Selected,Closed;
    public event Action? Added,PasteRequested;
    // Un seul minuteur pour la barre : chaque onglet relit son etat de cache sur
    // une tache de fond, jamais sur le fil d'interface.
    public event Action? CacheRefreshRequested;
    public event Action<Guid,TerminalTabAction,string?>? Customize;
    internal IReadOnlyList<TerminalTabInfo> Displayed=>current;
    static SolidColorBrush B(string color)=>DesktopTheme.Brush(color);
    // La teinte automatique suit le restant du cache ; une couleur choisie reste prioritaire.
    internal static string? CacheTint(TerminalCacheHint hint)=>hint switch{TerminalCacheHint.Fresh=>"#7FD6A6",TerminalCacheHint.Aging=>"#E9BE81",TerminalCacheHint.Expiring=>"#EB9B9B",TerminalCacheHint.Uncertain=>"#948CA0",_=>null};
    internal static string? EffectiveAccent(string? accent,TerminalCacheHint hint)=>TerminalTabPreferences.Color(accent)??CacheTint(hint);
    internal static string Status(TerminalActivity state)=>state switch{TerminalActivity.Thinking=>"Réflexion",TerminalActivity.Working=>"En cours",TerminalActivity.Ready=>"Prêt",TerminalActivity.Attention=>"Action requise",TerminalActivity.Error=>"Erreur",_=>"État non exposé"};
    public TerminalTabs()
    {
        Height=40;ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});buttonStyle=CreateButtonStyle();
        scroll=new ScrollViewer{Content=strip,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false};Children.Add(scroll);
        scroll.PreviewMouseWheel+=(_,e)=>{scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-e.Delta);e.Handled=true;};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(12,0,0,0)};SetColumn(actions,1);Children.Add(actions);
        previous=ActionButton("‹","Onglets précédents",()=>scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-220));next=ActionButton("›","Onglets suivants",()=>scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset+220));
        previous.Visibility=next.Visibility=Visibility.Collapsed;actions.Children.Add(previous);actions.Children.Add(next);
        actions.Children.Add(ActionButton("▣","Coller",()=>PasteRequested?.Invoke()));actions.Children.Add(ActionButton("+","Nouvel onglet",()=>Added?.Invoke()));
        scroll.ScrollChanged+=(_,_)=>{previous.Visibility=next.Visibility=scroll.ScrollableWidth>1?Visibility.Visible:Visibility.Collapsed;};
        cacheTimer=new DispatcherTimer(TimeSpan.FromSeconds(10),DispatcherPriority.Background,(_,_)=>{if(current.Length>0)CacheRefreshRequested?.Invoke();},Dispatcher);
        IsVisibleChanged+=(_,_)=>{if(IsVisible){cacheTimer.Start();CacheRefreshRequested?.Invoke();}else cacheTimer.Stop();};
        Unloaded+=(_,_)=>cacheTimer.Stop();
    }
    Style CreateButtonStyle()
    {
        var border=new FrameworkElementFactory(typeof(Border));border.Name="Glass";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(DockAppearance.ButtonRadius));
        border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
        var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)){VisualTree=border}));
        style.Setters.Add(new Setter(Control.BackgroundProperty,DockAppearance.ButtonFill));style.Setters.Add(new Setter(Control.BorderBrushProperty,B(DockAppearance.ButtonRim)));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
        style.Setters.Add(new Setter(Control.ForegroundProperty,B(DockAppearance.Ink)));style.Setters.Add(new Setter(Control.FontFamilyProperty,new FontFamily(DockAppearance.TextFont)));style.Setters.Add(new Setter(Control.FontSizeProperty,18d));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty,Cursors.Hand));style.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));
        var hover=new Trigger{Property=IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,DockAppearance.ButtonHover));style.Triggers.Add(hover);
        var press=new Trigger{Property=Button.IsPressedProperty,Value=true};press.Setters.Add(new Setter(UIElement.OpacityProperty,.72));style.Triggers.Add(press);return style;
    }
    Button ActionButton(string text,string hint,Action action){var button=new Button{Content=text,ToolTip=hint,Width=38,Height=38,Style=buttonStyle,Margin=new Thickness(4,0,0,0)};button.Click+=(_,_)=>action();return button;}
    public void SetTabs(IEnumerable<TerminalTabInfo> tabs)
    {
        var values=tabs.ToArray();if(values.SequenceEqual(current))return;
        var previousActive=current.FirstOrDefault(t=>t.Active)?.Id;
        bool reordered=!values.Select(t=>t.Id).SequenceEqual(current.Select(t=>t.Id));
        foreach(var id in cards.Keys.Except(values.Select(t=>t.Id)).ToArray()){cards[id].Stop();cards.Remove(id);}
        if(reordered)strip.Children.Clear();
        foreach(var value in values)
        {
            if(!cards.TryGetValue(value.Id,out var card)){card=new Card(this,value);cards.Add(value.Id,card);}
            card.Update(value);if(reordered)strip.Children.Add(card.Root);
        }
        current=values;
        if(current.FirstOrDefault(t=>t.Active) is {} active&&active.Id!=previousActive)Dispatcher.BeginInvoke(()=>{if(cards.TryGetValue(active.Id,out var card))card.Root.BringIntoView();},System.Windows.Threading.DispatcherPriority.Loaded);
    }
    sealed class Card
    {
        readonly TerminalTabs owner;
        readonly Border plate,glow;
        readonly TextBlock label;
        readonly TextBlock badge;
        readonly Button select;
        readonly TerminalActivityIndicator indicator;
        TerminalTabInfo data;
        public Grid Root {get;}=new(){Height=38,Margin=new Thickness(0,0,8,0)};
        public Card(TerminalTabs owner,TerminalTabInfo value)
        {
            this.owner=owner;data=value;Root.Tag=value.Id;
            plate=new Border{CornerRadius=new CornerRadius(DockAppearance.ButtonRadius),BorderThickness=new Thickness(1)};Root.Children.Add(plate);
            glow=new Border{CornerRadius=new CornerRadius(DockAppearance.ButtonRadius),BorderThickness=new Thickness(2),Opacity=0,IsHitTestVisible=false};Root.Children.Add(glow);
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});Root.Children.Add(row);
            var caption=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(12,0,10,0)};
            indicator=new TerminalActivityIndicator();caption.Children.Add(indicator);
            label=new TextBlock{FontFamily=new FontFamily(DockAppearance.TextFont),FontSize=14,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=236,VerticalAlignment=VerticalAlignment.Center};caption.Children.Add(label);
            badge=new TextBlock{FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=12,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center,MaxWidth=120,TextTrimming=TextTrimming.CharacterEllipsis,Visibility=Visibility.Collapsed};caption.Children.Add(badge);
            select=new Button{Content=caption,Style=owner.buttonStyle,Background=Brushes.Transparent,BorderThickness=new Thickness(0),MinWidth=126,MaxWidth=280};select.Click+=(_,_)=>owner.Selected?.Invoke(data.Id);row.Children.Add(select);
            var close=new Button{Content="×",Style=owner.buttonStyle,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Width=24,Height=26,FontSize=15,ToolTip="Fermer cet onglet",Margin=new Thickness(0,0,6,0)};SetColumn(close,1);close.Click+=(_,_)=>owner.Closed?.Invoke(data.Id);row.Children.Add(close);
            Root.ContextMenu=new ContextMenu();Root.ContextMenu.Opened+=(_,_)=>Menu();
            Root.ContextMenuOpening+=(_,e)=>{if(owner.Customize is null)e.Handled=true;};
            Root.IsVisibleChanged+=(_,_)=>Animate();Root.Unloaded+=(_,_)=>Stop();
        }
        public void Update(TerminalTabInfo value)
        {
            bool changed=data.Activity!=value.Activity||data.Effects!=value.Effects;data=value;
            string? chosen=EffectiveAccent(data.Accent,data.CacheHint);
            var accent=(Color)ColorConverter.ConvertFromString(chosen??"#BEA2DB");
            plate.Background=chosen is null?data.Active?DockAppearance.ButtonHover:DockAppearance.ButtonFill:
                new LinearGradientBrush(Color.FromArgb(data.Active?(byte)130:(byte)90,accent.R,accent.G,accent.B),Color.FromArgb(data.Active?(byte)65:(byte)40,accent.R,accent.G,accent.B),90);
            plate.BorderBrush=new SolidColorBrush(Color.FromArgb(data.Active?(byte)200:(byte)88,accent.R,accent.G,accent.B));
            label.Text=data.Title;label.FontWeight=data.Active?FontWeights.SemiBold:FontWeights.Normal;label.Foreground=B(DockAppearance.Ink);
            badge.Text=data.Badge??"";badge.Visibility=data.Badge is null?Visibility.Collapsed:Visibility.Visible;badge.Foreground=B(CacheTint(data.CacheHint)??DockAppearance.Muted);
            // Le compte a rebours reste lisible : le titre lui reserve sa place.
            label.MaxWidth=data.Badge is null?236:150;
            select.MaxWidth=data.Badge is null?280:320;
            string color=data.Activity switch{TerminalActivity.Working=>"#9FD5F1",TerminalActivity.Ready=>"#A8E5CD",TerminalActivity.Attention=>"#FFCE8C",TerminalActivity.Error=>"#FFA3B4",_=>"#D2BDF1"};
            indicator.SetState(data.Activity,data.Effects);glow.BorderBrush=B(color);
            Root.ToolTip=data.Title+"\n"+Status(data.Activity)+(data.Badge is null?"":"\n"+data.Badge)+(data.CacheDetail is null?"":"\n"+data.CacheDetail);
            if(changed)Animate();
        }
        public void Stop(){glow.BeginAnimation(OpacityProperty,null);glow.Opacity=0;}
        void Animate()
        {
            Stop();if(!Root.IsVisible||!data.Effects||!SystemParameters.ClientAreaAnimation)return;
            if(data.Activity is TerminalActivity.Thinking or TerminalActivity.Working or TerminalActivity.Attention)
            {
                var pulse=new DoubleAnimation(.08,data.Activity==TerminalActivity.Attention?.95:.55,TimeSpan.FromSeconds(data.Activity==TerminalActivity.Attention?.55:1.1)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever};
                Timeline.SetDesiredFrameRate(pulse,30);glow.BeginAnimation(OpacityProperty,pulse);
            }
            else if(data.Activity==TerminalActivity.Ready)glow.BeginAnimation(OpacityProperty,new DoubleAnimation(.85,0,TimeSpan.FromSeconds(1)));
        }
        void Menu()
        {
            var menu=Root.ContextMenu;menu.Items.Clear();
            void Add(string text,TerminalTabAction action,string? value=null,bool? check=null)
            {
                var item=new MenuItem{Header=text,Foreground=menu.Foreground};if(check is bool selected){item.IsCheckable=true;item.IsChecked=selected;}
                item.Click+=(_,_)=>owner.Customize?.Invoke(data.Id,action,check is null?value:item.IsChecked.ToString());menu.Items.Add(item);
            }
            Add("Renommer…",TerminalTabAction.Rename);
            var colors=new MenuItem{Header="Couleur",Foreground=menu.Foreground};
            foreach(var (name,color) in new[]{("Par défaut",(string?)null),("Lavande","#BBA0EA"),("Rose","#ED9EC8"),("Bleu","#8CBDEB"),("Menthe","#91D6BE"),("Ambre","#E9BE81"),("Corail","#EB9B9B"),("Perle","#D9D4E2")})
            {
                var item=new MenuItem{Header=name,IsCheckable=true,IsChecked=data.Accent==color,Foreground=menu.Foreground,Icon=new Border{Width=12,Height=12,CornerRadius=new CornerRadius(6),Background=B(color??"#BDA5D6")}};
                item.Click+=(_,_)=>owner.Customize?.Invoke(data.Id,TerminalTabAction.Color,color);colors.Items.Add(item);
            }
            var custom=new MenuItem{Header="Personnalisée…",Foreground=menu.Foreground};custom.Click+=(_,_)=>owner.Customize?.Invoke(data.Id,TerminalTabAction.CustomColor,null);colors.Items.Add(custom);menu.Items.Add(colors);
            menu.Items.Add(new Separator());Add("Titre automatique",TerminalTabAction.AutomaticTitle,check:data.AutomaticTitle);Add("Effets d’activité",TerminalTabAction.Effects,check:data.Effects);
            Add("Réinitialiser l’onglet",TerminalTabAction.Reset);menu.Items.Add(new Separator());Add("Configurer l’état Codex…",TerminalTabAction.StatusHelp);
        }
    }
}
