using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Battlestation;
using Microsoft.Terminal.Wpf;

internal static class NativeTerminalTests
{
    [STAThread] static void Main(string[] args)
    {
        var fixture=Path.GetFullPath(args[0]);var app=new Application();
        var terminal=new TerminalView("powershell.exe -NoLogo -NoProfile -File \""+fixture+"\"",Path.GetDirectoryName(fixture)!);
        var log=new System.Text.StringBuilder();var gate=new object();
        terminal.Session.TerminalOutput+=(_,e)=>{lock(gate)log.Append(e.Data);};
        string Output(){lock(gate)return log.ToString();}
        var window=new Window{Title="Battlestation terminal test",Left=-20000,Top=-20000,Width=900,Height=500,ShowActivated=false,ShowInTaskbar=false,Content=terminal};
        int code=1;
        window.Loaded+=async(_,_)=>
        {
            try
            {
                async Task WaitFor(string marker)
                {
                    for(int i=0;i<150;i++)
                    {
                        if(Output().Contains(marker))return;
                        if(terminal.Session.Error is {} error)throw new Exception(error);
                        await Task.Delay(100);
                    }
                    throw new Exception("ConPTY timeout: "+marker);
                }
                await WaitFor("BATTLESTATION_NATIVE_READY");
                if(terminal.Terminal.Rows<10||terminal.Terminal.Columns<40)throw new Exception("Invalid native terminal dimensions");
                await Task.Delay(200);
                var scrollbar=(ScrollBar)terminal.Terminal.FindName("scrollbar");
                scrollbar.ApplyTemplate();
                if(scrollbar.Template.FindName("PART_Track",scrollbar) is not Track track||track.Thumb is null||scrollbar.Maximum<50)
                    throw new Exception("Native scrollback or glass scrollbar unavailable");
                track.Thumb.ApplyTemplate();
                if(track.Thumb.Template.FindName("Glass",track.Thumb) is not System.Windows.Controls.Border)
                    throw new Exception("Native terminal still uses the system scrollbar skin");
                double bottom=scrollbar.Value;
                ScrollBar.PageUpCommand.Execute(null,scrollbar);await Task.Delay(100);
                if(scrollbar.Value>=bottom||track.Value!=scrollbar.Value)throw new Exception("Glass scrollbar page navigation did not move the native track");
                ScrollBar.ScrollToBottomCommand.Execute(null,scrollbar);await Task.Delay(100);
                if(scrollbar.Value!=scrollbar.Maximum)throw new Exception("Scrollback did not return to the prompt");
                for(int i=0;i<3;i++){terminal.Visibility=Visibility.Collapsed;await Task.Delay(80);terminal.Visibility=Visibility.Visible;window.Width+=24;await Task.Delay(100);}
                terminal.Session.WriteInput("azerty-42\r");
                await WaitFor("BATTLESTATION_NATIVE_OK");
                var output=Output();
                if(!output.Contains("été 42")||!output.Contains("ALTERNATE_SCREEN"))throw new Exception("UTF-8 or alternate screen output missing");
                using(var host=new NativeTerminalWindow(Path.GetDirectoryName(Path.GetDirectoryName(fixture))!))
                {
                    System.Text.Json.JsonElement Inspect()=>System.Text.Json.JsonDocument.Parse(host.Command("inspect")).RootElement.Clone();
                    host.Command("place:-20000:-20000:900:500");host.Command("start");host.Command("command:NewShell");
                    for(int i=0;i<100&&!Inspect().GetProperty("sessions").EnumerateArray().All(s=>s.GetProperty("ready").GetBoolean());i++)await Task.Delay(100);
                    var sessions=Inspect().GetProperty("sessions").EnumerateArray().ToArray();
                    if(sessions.Length!=2||sessions.Any(s=>!s.GetProperty("ready").GetBoolean()))throw new Exception("Test sessions failed to start");
                    var first=sessions[0].GetProperty("id").GetGuid();var second=sessions[1].GetProperty("id").GetGuid();
                    host.Command("chrome:external");host.Command("select:"+first);await Task.Delay(150);
                    var external=Inspect();if(external.GetProperty("headerVisible").GetBoolean())throw new Exception("Native header must disappear in external chrome mode");
                    if(!external.GetProperty("sessions")[0].GetProperty("active").GetBoolean())throw new Exception("Tab selection did not reach host");
                    host.Command("chrome:internal");await Task.Delay(150);
                    if(!Inspect().GetProperty("headerVisible").GetBoolean())throw new Exception("Detached header must remain available");
                    foreach(var session in sessions)
                        if(!Inspect().GetProperty("sessions").EnumerateArray().Any(s=>s.GetProperty("id").GetGuid()==session.GetProperty("id").GetGuid()&&s.GetProperty("pid").GetInt32()==session.GetProperty("pid").GetInt32()))throw new Exception("Chrome change restarted a session");
                    bool rejected=false;try{host.Command("close-tab:"+Guid.NewGuid());}catch(InvalidOperationException){rejected=true;}
                    if(!rejected||Inspect().GetProperty("sessions").GetArrayLength()!=2)throw new Exception("Stale tab identity must not close another tab");
                    host.Command("close-tab:"+first);
                    if(Inspect().GetProperty("sessions").GetArrayLength()!=1||Inspect().GetProperty("sessions")[0].GetProperty("id").GetGuid()!=second)throw new Exception("Closing a test tab affected the other tab");
                    bool emptyNotified=false;host.LastTabClosed+=()=>emptyNotified=true;
                    host.Command("hide");host.Command("show");
                    if(emptyNotified||!host.HasTabs)throw new Exception("Hiding the host must not retire it");
                    host.Command("close-tab:"+second);
                    if(!emptyNotified||host.HasTabs)throw new Exception("Explicit last-tab closure must allow the host to retire");
                }
                Console.WriteLine("PASS: external/internal header, selection by session ID, session survival and safe rejection of stale IDs (isolated test shells only).");
                Console.WriteLine("PASS: glass scrollbar with native scrollback, page navigation and return to prompt (WPF commands, not physical clicks).");
                Console.WriteLine("PASS: native Windows Terminal control, ConPTY PowerShell input/output, UTF-8, truecolor/alternate-screen sequences, resize and repeated hide/show. No Codex or clipboard input.");code=0;
            }
            catch(Exception e){Console.Error.WriteLine(e);}
            finally
            {
                terminal.Session.Close();await Task.Delay(200);
                app.Shutdown(code);
            }
        };
        app.Run(window);Environment.ExitCode=code;
    }
}
