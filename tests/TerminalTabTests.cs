using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Battlestation;

static class TerminalTabTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    [STAThread] static void Main(string[] args)
    {
        var id=Guid.NewGuid();var other=Guid.NewGuid();string file=Path.Combine(Path.GetTempPath(),"Battlestation-tab-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var preferences=new TerminalTabPreferences(file);var raw=new TerminalTabInfo(id,"PowerShell",true);
            var title=new ConsoleTitleInfo(7,"Thinking | Project slice | Project",true);var automatic=preferences.Decorate(raw,title);
            Check(automatic.Title=="Project slice | Project"&&automatic.Activity==TerminalActivity.Thinking,"Codex metadata provides name and thinking separately");
            Check(TerminalTitle.Parse("Project slice | Project | Working",true).Activity==TerminalActivity.Working,"run-state can appear after the project fields");
            Check(TerminalTitle.Parse("Needs input | Project slice | Project | Thinking",true).Activity==TerminalActivity.Attention,"Action required overrides busy state");
            Check(TerminalTitle.Parse("[ ! ] Action Required | Project slice | Project",true)==("Project slice | Project",TerminalActivity.Attention),"Actual Codex 0.154 action-required title replaces run-state while blocked");
            Check(TerminalTitle.Parse("Working | Project",false).Activity==TerminalActivity.Unknown,"Other console applications cannot claim Codex state");
            Check(preferences.Decorate(raw,new(7,"Windows PowerShell",false)).Title=="PowerShell","Generic shell titles do not replace useful host tab names");
            Check(TerminalTitle.Parse("Project slice | Project",true).Activity==TerminalActivity.Unknown,"No state is not ready");
            preferences.Set(id,new("Mon nom","#8cbdeb",false,false));var manual=preferences.Decorate(raw,title);
            Check(manual.Title=="Mon nom"&&manual.Accent=="#8CBDEB"&&!manual.Effects,"Manual name, color and effects are independent from runtime state");
            preferences=new TerminalTabPreferences(file);Check(preferences.Get(id).Name=="Mon nom","Preferences survive desktop reload");
            preferences.Set(id,preferences.Get(id) with{AutomaticTitle=true});Check(preferences.Decorate(raw,title).Title==automatic.Title,"Automatic title resumes live metadata");
            preferences.Set(other,new("Other","not a color"));Check(preferences.Get(other).Color is null&&preferences.Get(id).Color=="#8CBDEB","Bad color rejected without affecting other tabs");
            Check(TerminalTabPreferences.Clean("A\u001b\u202eB")=="AB","Control and bidi characters removed from titles");
            var requests=new List<(Guid Id,TerminalTabAction Action,string? Value)>();var tabs=new TerminalTabs();int selected=0,closed=0;
            tabs.Customize+=(tab,action,value)=>requests.Add((tab,action,value));tabs.Selected+=_=>selected++;tabs.Closed+=_=>closed++;
            var window=new Window{Content=tabs,Width=1000,Height=90,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};
            try
            {
                tabs.SetTabs([automatic,new(other,"Second",false)]);window.Show();Pump(80);
                var card=Descendants(tabs).OfType<Grid>().Single(g=>g.Tag is Guid value&&value==id);
                var icon=Descendants(card).OfType<TerminalActivityIndicator>().Single();icon.SetState(TerminalActivity.Working,true);Pump(50);
                var arc=Descendants(icon).OfType<System.Windows.Shapes.Path>().Single();
                Check(arc.Visibility==Visibility.Visible,"Working uses a visible loading arc, not a dot");
                if(SystemParameters.ClientAreaAnimation)Check(arc.RenderTransform.HasAnimatedProperties,"Working arc rotates when effects are enabled");
                icon.SetState(TerminalActivity.Unknown,true);Check(arc.Visibility==Visibility.Collapsed&&!arc.RenderTransform.HasAnimatedProperties,"Unknown does not pretend to work");
                card.ContextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                ((MenuItem)card.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(requests.Single().Id==id&&requests[0].Action==TerminalTabAction.Rename&&selected==0&&closed==0,"Rename targets its tab without selecting or closing it");
                var colors=(MenuItem)card.ContextMenu.Items[1];colors.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Bleu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(requests.Last().Value=="#8CBDEB","Color menu emits selected color");
                tabs.Customize+=(tab,action,value)=>
                {
                    if(action!=TerminalTabAction.Color)return;
                    preferences.Set(tab,preferences.Get(tab) with{Color=value});
                    tabs.SetTabs([preferences.Decorate(raw,title),new(other,"Second",false)]);
                };
                byte[] Pixels()
                {
                    tabs.UpdateLayout();Pump(20);
                    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)card.ActualWidth,(int)card.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(card);
                    var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);return pixels;
                }
                colors.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Bleu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));var blue=Pixels();
                colors.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Corail")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));var coral=Pixels();
                int changedPixels=Enumerable.Range(0,blue.Length/4).Count(i=>Math.Abs(blue[i*4]-coral[i*4])+Math.Abs(blue[i*4+2]-coral[i*4+2])>15);
                Check(changedPixels>blue.Length/4*.35,"Changing a color visibly tints the tab body, not only its one-pixel rim");
                tabs.SetTabs([preferences.Decorate(raw with{Active=false},title),new(other,"Second",true)]);
                var plate=(Border)card.Children[0];Check(plate.Background is LinearGradientBrush tint&&tint.GradientStops.All(s=>s.Color.R==0xEB&&s.Color.G==0x9B&&s.Color.B==0x9B),"Custom tint survives deactivation and activity decoration");
                Check(new TerminalTabPreferences(file).Get(id).Color=="#EB9B9B","Menu color persists for the same session");
                colors.Items.OfType<MenuItem>().Single(i=>Equals(i.Header,"Par défaut")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(ReferenceEquals(plate.Background,DockAppearance.ButtonHover),"Default restores the shared glass background");
                tabs.SetTabs([automatic with{Title="Updated live title",Activity=TerminalActivity.Ready},new(other,"Second",false)]);Pump(50);
                Check(ReferenceEquals(card,Descendants(tabs).OfType<Grid>().Single(g=>g.Tag is Guid value&&value==id)),"Updates preserve card/context menu");
            }
            finally{window.Close();}
            if(args.Length>0)
            {
                string fixture=Path.Combine(Path.GetTempPath(),"Battlestation-title-"+Guid.NewGuid().ToString("N")+".ps1");
                File.WriteAllText(fixture,"[Console]::Title = 'Working | Synthetic | Project'\nStart-Sleep -Seconds 10\n");
                var session=new ConPtySession("powershell.exe -NoLogo -NoProfile -File \""+fixture+"\"",Path.GetDirectoryName(fixture)!);
                try
                {
                    session.Start();Check(session.Ready.Wait(5000)&&session.Ready.Result,"Own fixture shell starts");
                    using var metadata=new TerminalMetadataClient(Path.GetFullPath(args[0]));ConsoleTitleInfo? result=null;
                    for(int i=0;i<20;i++){var read=metadata.Read([session.Pid]);Check(read.Wait(3000),"Metadata query is bounded");result=read.Result.SingleOrDefault();if(result?.Title?.Contains("Synthetic")==true)break;Pump(100);}
                    Check(result?.Title=="Working | Synthetic | Project"&&result.Error==0&&!result.Codex,"Reads own console title without pretending it is Codex");
                    Check(session.InputCharacters==0&&!session.Exited,"No input sent; shell survives metadata query");
                }
                finally{session.Close();File.Delete(fixture);}
            }
            Console.WriteLine("PASS: persistent customization, automatic/manual names, explicit states, unknown state, context routing, stable tab visuals and optional title-only helper.");
        }
        finally{File.Delete(file);if(File.Exists(file+".tmp"))File.Delete(file+".tmp");}
    }
}
