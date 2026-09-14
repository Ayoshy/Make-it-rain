using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Battlestation;
internal sealed record TerminalTabInfo(Guid Id,string Title,bool Active);

// This is desktop chrome, independent of the console renderer and its lifetime.
internal sealed class TerminalTabs : Grid
{
    readonly StackPanel strip=new(){Orientation=Orientation.Horizontal};
    readonly ScrollViewer scroll;
    readonly Button previous,next;
    readonly Style buttonStyle;
    TerminalTabInfo[] current=[];
    public event Action<Guid>? Selected,Closed;
    public event Action? Added,PasteRequested;
    static SolidColorBrush B(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    static LinearGradientBrush Glass(bool active=false)=>new((Color)ColorConverter.ConvertFromString(active?"#38EBDCF7":"#16DED2EE"),(Color)ColorConverter.ConvertFromString(active?"#1CB597D8":"#09B29CCC"),90);
    public TerminalTabs()
    {
        Height=40;ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        buttonStyle=CreateButtonStyle();
        scroll=new ScrollViewer{Content=strip,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false};Children.Add(scroll);
        scroll.PreviewMouseWheel+=(_,e)=>{scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-e.Delta);e.Handled=true;};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(12,0,0,0)};SetColumn(actions,1);Children.Add(actions);
        previous=ActionButton("‹","Onglets précédents",()=>scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-220));
        next=ActionButton("›","Onglets suivants",()=>scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset+220));
        previous.Visibility=next.Visibility=Visibility.Collapsed;actions.Children.Add(previous);actions.Children.Add(next);
        actions.Children.Add(ActionButton("▣","Coller",()=>PasteRequested?.Invoke()));
        actions.Children.Add(ActionButton("+","Nouvel onglet",()=>Added?.Invoke()));
        scroll.ScrollChanged+=(_,_)=>{previous.Visibility=next.Visibility=scroll.ScrollableWidth>1?Visibility.Visible:Visibility.Collapsed;};
    }
    Style CreateButtonStyle()
    {
        var border=new FrameworkElementFactory(typeof(Border));border.Name="Glass";
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(14));
        border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
        var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)){VisualTree=border}));
        style.Setters.Add(new Setter(Control.BackgroundProperty,Glass()));style.Setters.Add(new Setter(Control.BorderBrushProperty,B("#38DACDEC")));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
        style.Setters.Add(new Setter(Control.ForegroundProperty,B("#E2D6EC")));style.Setters.Add(new Setter(Control.FontFamilyProperty,new FontFamily("Segoe UI Variable Text")));style.Setters.Add(new Setter(Control.FontSizeProperty,18d));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty,Cursors.Hand));style.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));
        var hover=new Trigger{Property=IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,Glass(true)));hover.Setters.Add(new Setter(Control.BorderBrushProperty,B("#789DDEE8")));style.Triggers.Add(hover);
        var press=new Trigger{Property=Button.IsPressedProperty,Value=true};press.Setters.Add(new Setter(UIElement.OpacityProperty,.72));style.Triggers.Add(press);return style;
    }
    Button ActionButton(string text,string hint,Action action)
    {
        var button=new Button{Content=text,ToolTip=hint,Width=38,Height=38,Style=buttonStyle,Margin=new Thickness(4,0,0,0)};button.Click+=(_,_)=>action();return button;
    }
    public void SetTabs(IEnumerable<TerminalTabInfo> tabs)
    {
        var values=tabs.ToArray();if(values.SequenceEqual(current))return;current=values;strip.Children.Clear();
        foreach(var tab in current)
        {
            var card=new Border{Height=38,CornerRadius=new CornerRadius(15),Background=Glass(tab.Active),BorderBrush=B(tab.Active?"#7ABCA2DB":"#30DACDEC"),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,0)};
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});card.Child=row;
            var label=new TextBlock{Text=tab.Title,FontSize=14,FontWeight=tab.Active?FontWeights.SemiBold:FontWeights.Normal,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(16,0,12,0),VerticalAlignment=VerticalAlignment.Center};
            var select=new Button{Content=label,Style=buttonStyle,Background=Brushes.Transparent,BorderThickness=new Thickness(0),MinWidth=126,MaxWidth=220,ToolTip=tab.Title};select.Click+=(_,_)=>Selected?.Invoke(tab.Id);row.Children.Add(select);
            var close=new Button{Content="×",Style=buttonStyle,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Width=24,Height=26,FontSize=15,ToolTip="Fermer cet onglet",Margin=new Thickness(0,0,6,0)};SetColumn(close,1);close.Click+=(_,_)=>Closed?.Invoke(tab.Id);row.Children.Add(close);
            strip.Children.Add(card);if(tab.Active)Dispatcher.BeginInvoke(()=>card.BringIntoView(),System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}
