using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms=System.Windows.Forms;

namespace Battlestation;
internal static class OverlayStyle
{
    internal static SolidColorBrush B(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    internal static TextBlock Text(string text,double size=14,string color="#E9E0F2")=>new(){Text=text,FontSize=size,Foreground=B(color),TextWrapping=TextWrapping.Wrap};
    internal static Button Button(string text,Action action)
    {
        var button=new Button{Content=text,Padding=new Thickness(14,9,14,9),Margin=new Thickness(3),Cursor=Cursors.Hand};button.Click+=(_,_)=>action();return button;
    }
    internal static void Apply(Window window)
    {
        window.FontFamily=new FontFamily(DockAppearance.TextFont);window.FontSize=14;window.Foreground=B("#E9E0F2");window.Background=Brushes.Transparent;
        window.WindowStyle=WindowStyle.None;window.AllowsTransparency=true;window.ResizeMode=ResizeMode.NoResize;
        var button=new Style(typeof(Button));
        button.Setters.Add(new Setter(Control.ForegroundProperty,B("#F3EAF8")));
        button.Setters.Add(new Setter(Control.BackgroundProperty,DockAppearance.ButtonFill));
        button.Setters.Add(new Setter(Control.BorderBrushProperty,B(DockAppearance.ButtonRim)));button.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(DockAppearance.ButtonRadius));
        border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        border.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        border.SetBinding(Border.BorderThicknessProperty,new System.Windows.Data.Binding("BorderThickness"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        content.SetBinding(FrameworkElement.MarginProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.AppendChild(content);
        button.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)){VisualTree=border}));
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,DockAppearance.ButtonHover));button.Triggers.Add(hover);
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Control.BorderBrushProperty,B("#E2C3EBF5")));button.Triggers.Add(focus);
        var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.4));button.Triggers.Add(disabled);
        window.Resources[typeof(Button)]=button;
        var input=new Style(typeof(TextBox));input.Setters.Add(new Setter(Control.BackgroundProperty,B("#6620142D")));input.Setters.Add(new Setter(Control.ForegroundProperty,B("#F0E7F7")));input.Setters.Add(new Setter(Control.BorderBrushProperty,B("#50C4A9D9")));input.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,8,10,8)));input.Setters.Add(new Setter(TextBox.CaretBrushProperty,B("#E9DBF7")));window.Resources[typeof(TextBox)]=input;
        var check=new Style(typeof(CheckBox));check.Setters.Add(new Setter(Control.ForegroundProperty,B("#E9E0F2")));check.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(7,4,0,4)));check.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,5,0,5)));
        var checkRoot=new FrameworkElementFactory(typeof(Border));checkRoot.Name="CheckRoot";checkRoot.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        checkRoot.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
        var checkRow=new FrameworkElementFactory(typeof(DockPanel));
        var indicator=new FrameworkElementFactory(typeof(Border));indicator.Name="Indicator";indicator.SetValue(DockPanel.DockProperty,Dock.Left);
        indicator.SetValue(FrameworkElement.WidthProperty,20d);indicator.SetValue(FrameworkElement.HeightProperty,20d);indicator.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        indicator.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));indicator.SetValue(Border.BackgroundProperty,DockAppearance.ButtonFill);
        indicator.SetValue(Border.BorderBrushProperty,B(DockAppearance.ButtonRim));indicator.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        var mark=new FrameworkElementFactory(typeof(TextBlock));mark.Name="CheckMark";mark.SetValue(TextBlock.TextProperty,"✓");mark.SetValue(TextBlock.ForegroundProperty,B("#BDEDE5"));
        mark.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);mark.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);mark.SetValue(UIElement.VisibilityProperty,Visibility.Hidden);indicator.AppendChild(mark);checkRow.AppendChild(indicator);
        var caption=new FrameworkElementFactory(typeof(ContentPresenter));caption.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);caption.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);caption.SetValue(FrameworkElement.MarginProperty,new Thickness(8,0,0,0));checkRow.AppendChild(caption);checkRoot.AppendChild(checkRow);
        var checkTemplate=new ControlTemplate(typeof(CheckBox)){VisualTree=checkRoot};
        var selected=new Trigger{Property=System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,Value=true};selected.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"CheckMark"));checkTemplate.Triggers.Add(selected);
        var checkHover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};checkHover.Setters.Add(new Setter(Border.BackgroundProperty,DockAppearance.ButtonHover,"Indicator"));checkTemplate.Triggers.Add(checkHover);
        var checkFocus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};checkFocus.Setters.Add(new Setter(Border.BorderBrushProperty,B("#E2C3EBF5"),"Indicator"));checkTemplate.Triggers.Add(checkFocus);
        var checkDisabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};checkDisabled.Setters.Add(new Setter(UIElement.OpacityProperty,.4,"CheckRoot"));checkTemplate.Triggers.Add(checkDisabled);
        check.Setters.Add(new Setter(Control.TemplateProperty,checkTemplate));check.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));window.Resources[typeof(CheckBox)]=check;
    }
    internal static Border Frame(UIElement child)=>new(){CornerRadius=new CornerRadius(24),Padding=new Thickness(22),BorderThickness=new Thickness(1),BorderBrush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#A4E7D4F5"),(Color)ColorConverter.ConvertFromString("#4579C3D5"),55),Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#F038293F"),(Color)ColorConverter.ConvertFromString("#F21D162A"),60),Child=child};
    internal static void Place(Window window)
    {
        Native.GetCursorPos(out var cursor);var area=Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X,cursor.Y)).WorkingArea;
        // Convert the selected monitor's physical working area to WPF coordinates.
        var handle=new WindowInteropHelper(window).EnsureHandle();Native.SetWindowPos(handle,0,area.Left+40,area.Top+40,0,0,0x15);
        double scale=Native.GetDpiForWindow(handle)/96d;if(scale<=0)scale=1;
        window.Left=area.Left/scale+Math.Max(0,(area.Width/scale-window.Width)/2);
        window.Top=area.Top/scale+Math.Max(0,(area.Height/scale-window.Height)*.28);
    }
    internal static void Reveal(Window window,bool animate)
    {
        Place(window);window.Show();window.Activate();Native.SetForegroundWindow(new WindowInteropHelper(window).Handle);
        if(animate)window.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(130)));
    }
}
