using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;
internal static class TerminalTabsPreview
{
    // Render the actual header control without starting any console or backend.
    public static void Run(string output)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        // Un onglet par etat, plus les deux badges de cache : compte a rebours
        // OpenAI teinte et couleur choisie, puis taux de hit DeepSeek.
        var tabs=new TerminalTabs();tabs.SetTabs([new(Guid.NewGuid(),"Battlestation",true,"#BBA0EA",TerminalActivity.Thinking),new TerminalTabInfo(Guid.NewGuid(),"Implémenter l'indicateur de cache | Battlestation",false,Activity:TerminalActivity.Ready,Badge:"\u23F3 ≈ 12 min",CacheHint:TerminalCacheHint.Aging),new TerminalTabInfo(Guid.NewGuid(),"Codex CLI (DS)",false,Activity:TerminalActivity.Ready,Badge:"cache 78 %")]);
        var layout=new DockPanel();DockPanel.SetDock(tabs,Dock.Top);layout.Children.Add(tabs);
        var scroll=new ScrollBar{Orientation=Orientation.Vertical,Minimum=0,Maximum=180,Value=110,ViewportSize=29,Style=TerminalScrollBar.CreateStyle(),HorizontalAlignment=HorizontalAlignment.Right};
        var console=new Border{Margin=new Thickness(0,10,0,0),Background=new SolidColorBrush(Color.FromRgb(33,24,43)),Child=scroll};layout.Children.Add(console);
        var panel=new Border{Padding=new Thickness(16,12,16,12),CornerRadius=new CornerRadius(24),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(95,231,211,255)),Background=new LinearGradientBrush(Color.FromRgb(66,46,79),Color.FromRgb(30,20,44),15),Child=layout};
        var root=new Grid{Width=1000,Height=300,Background=new SolidColorBrush(Color.FromRgb(30,20,44))};panel.Margin=new Thickness(6);root.Children.Add(panel);
        var window=new Window{Content=root,Width=1000,Height=300,Left=-20000,Top=-20000,WindowStyle=WindowStyle.None,ShowActivated=false,ShowInTaskbar=false};
        app.Startup+=(_,_)=>
        {
            window.Show();app.Dispatcher.BeginInvoke(()=>
            {
                root.UpdateLayout();var image=new RenderTargetBitmap(1000,300,96,96,PixelFormats.Pbgra32);image.Render(root);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);using(var file=File.Create(output))encoder.Save(file);
                app.Shutdown();
            },DispatcherPriority.ApplicationIdle);
        };
        app.Run();
    }
}
