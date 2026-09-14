using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Battlestation;
internal static class ResponsiveSurfaceTests
{
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static List<(Rect Rect,Action Action,string Name)> Hits(Surface s)=>(List<(Rect,Action,string)>)typeof(Surface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s)!;
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false,DispatcherPriority.ApplicationIdle);Dispatcher.PushFrame(frame);}
    [STAThread] static void Main()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var station=new Station();
        var editHeader=new DockPanel();editHeader.Children.Add(new Button{Content="×"});var editBorder=new Border{Child=editHeader};
        EditOverlay.Mount(editBorder,editHeader);
        Check(editBorder.Child is Grid controls&&controls.Children.Contains(editHeader)&&controls.Children.OfType<EditHandles>().Count()==1,"Existing header mounts alongside handles without a duplicate logical parent");
        editBorder.Measure(new Size(400,240));editBorder.Arrange(new Rect(0,0,400,240));
        var editBitmap=new RenderTargetBitmap(400,240,96,96,PixelFormats.Pbgra32);editBitmap.Render(editBorder);
        string output=Path.Combine(station.Root,"artifacts/validation/responsive");Directory.CreateDirectory(output);
        foreach(var (id,surface) in new (string,Surface)[]{("clock",new DeskSurface(station,DeskWidget.Clock)),("weather",new DeskSurface(station,DeskWidget.Weather)),("music",new DeskSurface(station,DeskWidget.Music)),("projects",new DeskSurface(station,DeskWidget.Projects)),("apps",new DockSurface(station)),("countdown",new CountdownSurface(station)),("hardware",new DashboardSurface(station,true)),("usage",new DashboardSurface(station,false)),("audio",new AudioSurface(station))})
        {
            var min=DesktopLayout.Minimum(id);
            foreach(bool large in new[]{false,true})
            {
                double w=large?Math.Max(900,min.Width*1.5):min.Width,h=large?Math.Max(400,min.Height*1.4):min.Height;
                surface.Width=w;surface.Height=h;
                var root=new Grid{Width=w,Height=h,Background=new LinearGradientBrush(Color.FromRgb(58,39,72),Color.FromRgb(28,19,40),25)};root.Children.Add(surface);
                var window=new Window{Content=root,Width=w,Height=h,Left=-20000,Top=-20000,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false};window.Show();Pump();
                void Render(string suffix)
                {
                    surface.Refresh();root.UpdateLayout();Pump();var image=new RenderTargetBitmap((int)Math.Ceiling(w),(int)Math.Ceiling(h),96,96,PixelFormats.Pbgra32);image.Render(root);
                    foreach(var hit in Hits(surface))Check(new Rect(0,0,w,h).Contains(hit.Rect),$"{id} {suffix}: off-surface hit {hit.Name} {hit.Rect}");
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(output,id+"-"+(large?"large":"minimum")+suffix+".png"));png.Save(file);
                }
                Native.Panels.Clear();Render("");
                int slot=id switch{"clock"=>0,"weather"=>1,"apps"=>2,"music"=>3,"projects"=>4,"hardware"=>6,"usage"=>7,"audio"=>10,_=>-1};
                Check(slot<0 ? Native.Panels.Count==0 : Native.Panels.TryGetValue(slot,out var frame)&&Math.Abs(frame.Width-w)<.01&&Math.Abs(frame.Height-h)<.01,
                    "Every dock frame follows its actual size; the standalone countdown remains frameless");
                var occupiedBefore=Native.Panels.Where(p=>p.Value.Width>0&&p.Value.Height>0).Select(p=>p.Key).ToArray();
                surface.Refresh();surface.SetDisplayed(false);Render("-hidden");
                Check(Hits(surface).Count==0,"Hidden surface has no stale click targets");
                Check(!Native.Panels.Any(p=>occupiedBefore.Contains(p.Key)&&p.Value.Width>0&&p.Value.Height>0),"Queued render must not resurrect hidden glass");
                surface.SetDisplayed(true);Render("-shown");
                if(id=="projects")
                {
                    var names=Hits(surface).Select(x=>x.Name).ToArray();
                    if(large)Check(names.Count(n=>n.StartsWith("Project"))>6,"Large projects dock renders beyond the former six-project cap");
                    if(!large){Check(names.Count(n=>n.StartsWith("Project"))==2,"Minimum projects shows two cards with real scroll");
                        surface.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=Mouse.MouseWheelEvent});Render("-scroll");Check(Hits(surface).Any(x=>x.Name=="Project2"),"Wheel reaches next project");}
                    Hits(surface).First(x=>x.Name.StartsWith("Project")).Action();Render("-popup");
                    Check(!Hits(surface).Any(x=>x.Name.Length==8&&x.Name.StartsWith("Project")),"Modal background must not receive clicks");
                    Hits(surface).Single(x=>x.Name=="CloseProject").Action();Render("-closed");
                }
                if(id=="apps"&&!large)
                {
                    var before=Hits(surface).Where(x=>x.Name.StartsWith("Launch:")).Select(x=>x.Name).ToArray();
                    surface.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=Mouse.MouseWheelEvent});Render("-scroll");
                    Check(!Hits(surface).Where(x=>x.Name.StartsWith("Launch:")).Select(x=>x.Name).SequenceEqual(before),"Apps wheel exposes other apps");
                }
                if(id=="audio"&&!large)
                {
                    Hits(surface).Single(x=>x.Name=="AudioNext").Action();Render("-next");Check(Hits(surface).Any(x=>x.Name=="AudioMute:app3"),"Audio next page fits minimum");
                }
                if(surface is DashboardSurface drawer)
                {
                    DashboardPageTests.Run(drawer, station, id=="hardware", Render, Pump);
                }
                root.Children.Clear();window.Close();
            }
            if(surface is IDisposable disposable)disposable.Dispose();
        }
        DashboardPageTests.Variants(Pump,output);
        app.Shutdown();Console.WriteLine("PASS: actual responsive WPF surfaces, minimum/large bounds, project modal and scrolling, app scrolling, audio pages; fixture-only data and actions.");
    }
}
