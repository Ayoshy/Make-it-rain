using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;
internal static class GlassMenuTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false,DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame);}
    internal static void Run()
    {
        var host=new Window{Width=500,Height=260,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};
        var menu=new ContextMenu{PlacementTarget=host};int executed=0;
        var selected=new MenuItem{Header="Casque (PARTYBTMS3)",IsCheckable=true,IsChecked=true};
        var disabled=new MenuItem{Header="Sortie indisponible",IsEnabled=false};
        var group=new MenuItem{Header="Ajouter un bloc"};var leaf=new MenuItem{Header="Applications"};leaf.Click+=(_,_)=>executed++;group.Items.Add(leaf);
        menu.Items.Add(selected);menu.Items.Add(disabled);menu.Items.Add(new Separator());menu.Items.Add(group);
        try
        {
            host.Show();menu.IsOpen=true;Pump();menu.ApplyTemplate();selected.ApplyTemplate();disabled.ApplyTemplate();group.ApplyTemplate();
            Check(menu.FontFamily.Source==DockAppearance.TextFont,"Menu font must follow dock typography");
            Check(((TextBlock)selected.Template.FindName("Check",selected)).Visibility==Visibility.Visible,"Native checked state must draw the pearl check");
            Check(((Border)disabled.Template.FindName("Row",disabled)).Opacity==.4,"Disabled menu entries must remain visibly disabled");
            Save((FrameworkElement)VisualTreeHelper.GetChild(menu,0),"menu-preview.png");
            group.IsSubmenuOpen=true;Pump();
            var popup=(Popup)group.Template.FindName("PART_Popup",group);Check(popup.IsOpen,"WPF submenu opens through IsSubmenuOpen");
            Check(((Border)popup.Child).CornerRadius==new CornerRadius(15),"Submenu uses the same rounded plate");
            leaf.ApplyTemplate();Check(leaf.Template.FindName("Row",leaf) is Border,"Nested entries inherit the shared template");
            Save((FrameworkElement)popup.Child,"submenu-preview.png");
            typeof(MenuItem).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(leaf,null);Pump();
            Check(executed==1,"Templating must preserve the native menu action exactly once");
            Check(!menu.IsOpen,"Leaf activation closes the WPF menu");
            menu.IsOpen=true;Pump();
            typeof(MenuItem).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(selected,null);Pump();
            Check(!selected.IsChecked,"Checkable items retain native toggling");
        }
        finally{menu.IsOpen=false;host.Close();}
        Console.WriteLine("PASS: shared glass menu and submenu templates, checks, disabled rows, native activation and dismissal. Isolated WPF controls, no device change.");
    }
    static void Save(FrameworkElement visual,string name)
    {
        visual.UpdateLayout();var dpi=VisualTreeHelper.GetDpi(visual);
        var image=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(visual.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);image.Render(visual);
        var output=Path.Combine(Environment.CurrentDirectory,"artifacts/validation/harmonization");Directory.CreateDirectory(output);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(output,name));encoder.Save(file);
    }
}
