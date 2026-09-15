using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Battlestation;

static class DockReviewTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static JsonElement Hits(Surface surface)=>JsonSerializer.SerializeToElement(surface.InspectHits());
    static bool Has(Surface surface,string name)=>Hits(surface).EnumerateArray().Any(h=>h.GetProperty("name").GetString()==name);
    static void Render(Surface surface,string file)
    {
        surface.Measure(new Size(surface.Width,surface.Height));
        surface.Arrange(new Rect(0,0,surface.Width,surface.Height));surface.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(surface.Width),(int)Math.Ceiling(surface.Height),96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var dc=background.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(29,22,40)),null,new Rect(0,0,surface.Width,surface.Height));
        image.Render(background);image.Render(surface);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
        using var stream=File.Create(file);png.Save(stream);
        foreach(var hit in Hits(surface).EnumerateArray())
        {
            double x=hit.GetProperty("x").GetDouble(),y=hit.GetProperty("y").GetDouble();
            Check(x>=0&&y>=0&&x+hit.GetProperty("width").GetDouble()<=surface.Width&&y+hit.GetProperty("height").GetDouble()<=surface.Height,"Hit must fit inside dock");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        var app=new Application();
        string root=Path.GetFullPath(args[0]),output=Path.Combine(root,"artifacts/validation/dock-review");
        Directory.CreateDirectory(output);
        var station=new Station{Root=root};
        var face=new Typeface(new FontFamily(new Uri("pack://application:,,,/"),"./Assets/Fonts/#GTAArtDeco Condensed"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
        Check(face.TryGetGlyphTypeface(out var font)&&font.FontUri.ToString().Contains("art-deco"),"Embedded Art Deco resolves without font fallback");
        foreach(int count in new[]{0,1,3})
        {
            station.Reminders=Enumerable.Range(0,count).Select(i=>new ReminderItem("Penser à faire une pause")).ToList();
            var nudge=new ReminderSurface(station){Width=440};
            Render(nudge,Path.Combine(output,$"nudge-{count}.png"));
            Check(Has(nudge,"ReminderDone")==(count>0),"Done only exists for a reminder");
            Check(Has(nudge,"ReminderNext")==(count>1),"No useless Next for one reminder");
            if(count>0)
            {
                typeof(ReminderSurface).GetMethod("Complete",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(nudge,null);
                Check(station.Reminders.Count==count-1,"Done removes exactly one reminder");
            }
        }
        station.Reminders=[new("Penser à faire une pause")];
        Render(new ReminderSurface(station){Width=920},Path.Combine(output,"nudge-wide.png"));
        station.Apps=new[]{"Steam","Brave","Discord","Stremio","League of Legends","Codex","Claude Code","Edge","Chrome","qBittorrent","battle.net"}.Select(name=>new DockApp(name,Path.Combine(root,"fixtures",name+".exe"))).ToList();
        var appsSurface=new DockSurface(station){Width=1120,Height=116};
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,[]));
        Render(appsSurface,Path.Combine(output,"apps-closed.png"));
        var closedHits=Hits(appsSurface).GetRawText();
        var running=station.Apps.Where((_,index)=>index%2==0).Select(appItem=>new AppProcessSnapshot(Path.GetFileNameWithoutExtension(appItem.Path),appItem.Path)).ToArray();
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,running));
        Thread.Sleep(90);
        var midpoint=JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps")[0].GetProperty("material").GetDouble();
        Check(midpoint>0&&midpoint<1,"A process change produces an intermediate material");
        Render(appsSurface,Path.Combine(output,"apps-transition.png"));
        Thread.Sleep(280);appsSurface.Refresh();
        Render(appsSurface,Path.Combine(output,"apps-mixed.png"));
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,station.Apps.Select(appItem=>new AppProcessSnapshot(Path.GetFileNameWithoutExtension(appItem.Path),appItem.Path)).ToArray()));
        Thread.Sleep(280);appsSurface.Refresh();
        Render(appsSurface,Path.Combine(output,"apps-running.png"));
        var open=JsonSerializer.SerializeToElement(appsSurface.InspectActivity());
        Check(open.GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("state").GetString()=="Running"&&row.GetProperty("material").GetDouble()==1),"All icons reach their running material");
        Check(Hits(appsSurface).GetRawText()==closedHits,"Activity leaves every launch and editor hit unchanged");
        appsSurface.ApplyActivitySnapshot(AppActivity.Unknown(station.Apps,"fixture"));
        Check(JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("state").GetString()=="Unknown"&&row.GetProperty("material").GetDouble()==1),"Unknown retains the last confirmed material");
        station.Apps.Reverse();
        Check(JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("material").GetDouble()==1),"Reordering retains state by application identity");
        appsSurface.ApplyActivitySnapshot(AppActivity.Snapshot(station.Apps,[]));
        Thread.Sleep(280);appsSurface.Refresh();
        Render(appsSurface,Path.Combine(output,"apps-stopped-again.png"));
        Check(JsonSerializer.SerializeToElement(appsSurface.InspectActivity()).GetProperty("apps").EnumerateArray().All(row=>row.GetProperty("material").GetDouble()==0),"Process closure restores pearl");
        appsSurface.Dispose();
        station.Apps.Add(new DockApp("Spotify",Path.Combine(root,"fixtures","Spotify.exe")));
        using(var audioSurface=new AudioSurface(station))
        {
            // Use deterministic rows after the read-only worker has stopped.
            audioSurface.Mixer.Dispose();
            var worker=(Thread)typeof(AudioMixerWorker).GetField("thread",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(audioSurface.Mixer)!;
            Check(worker.Join(10000),"Audio worker stops");
            typeof(AudioMixerWorker).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(audioSurface.Mixer,
                new AudioMixerSnapshot([],Enumerable.Range(0,6).Select(i=>new AudioApp("app"+i,"Source "+i,.65f,false,.4f)).ToArray(),"Casque (Buds3 Pro de Ayo)","fixture","Micro",.65f,false,false,""));
            foreach(var size in new[]{new Size(620,264),new Size(440,220),new Size(720,336)})
            {
                audioSurface.Width=size.Width;audioSurface.Height=size.Height;audioSurface.Refresh();
                Render(audioSurface,Path.Combine(output,$"audio-{size.Width}-{size.Height}.png"));
                Check(Has(audioSurface,"AudioNext"),"Overflow audio sources remain reachable");
                Check(Has(audioSurface,"AudioVolume:master"),"Compact audio retains master slider");
            }
        }
        foreach(var size in new[]{new Size(1120,270),new Size(920,230),new Size(720,204),new Size(240,112),new Size(1120,116)})
        {
            var packed=new DockSurface(station){Width=size.Width,Height=size.Height};
            Render(packed,Path.Combine(output,$"apps-{size.Width}-{size.Height}.png"));
            var targets=Hits(packed).EnumerateArray().ToArray();
            var add=targets.Single(hit=>hit.GetProperty("name").GetString()=="ManageApps");
            Check(add.GetProperty("width").GetDouble()==28&&add.GetProperty("height").GetDouble()==28,"Add app uses a compact square button");
            if(size.Height>=230)Check(targets.Count(hit=>hit.GetProperty("name").GetString()!.StartsWith("Launch:"))==12,"Wide packs expose all 12 apps");
            Rect Bounds(JsonElement hit)=>new(hit.GetProperty("x").GetDouble(),hit.GetProperty("y").GetDouble(),hit.GetProperty("width").GetDouble(),hit.GetProperty("height").GetDouble());
            for(int i=0;i<targets.Length;i++)for(int j=i+1;j<targets.Length;j++)
            {
                var overlap=Rect.Intersect(Bounds(targets[i]),Bounds(targets[j]));
                Check(overlap.IsEmpty||overlap.Width==0||overlap.Height==0,"App controls do not overlap");
            }
            packed.Dispose();
        }
        float sixty=0,oneFortyFour=0;
        for(int i=0;i<60;i++)sixty=AudioMeterMotion.Step(sixty,.8f,1/60d);
        for(int i=0;i<144;i++)oneFortyFour=AudioMeterMotion.Step(oneFortyFour,.8f,1/144d);
        Check(Math.Abs(sixty-oneFortyFour)<.0002f,"Meter attack is independent of display refresh rate");
        Check(AudioMeterMotion.Step(.8f,0,1/60d) is >0 and <.8f,"Silence decays smoothly");
        Check(float.IsFinite(AudioMeterMotion.Step(float.NaN,float.NaN,.1)),"Invalid meter samples cannot poison animation");
        for(int i=0;i<180;i++)sixty=AudioMeterMotion.Step(sixty,0,1/60d);
        Check(sixty==0,"Silence settles completely so repainting can stop");
        using var bluetooth=new BluetoothSurface(station){Width=488,Height=280};
        var rows=new BluetoothDevice?[]{
            new("BTHENUM\\DEV_000000000001\\fixture-party","PARTYBTMS3",true,BluetoothKind.Speaker,null,1,100),
            new("BTHENUM\\DEV_000000000002\\fixture-buds","Buds3 Pro de Ayo",true,BluetoothKind.Headphones,null,2,17),
            new("BTHENUM\\DEV_000000000003\\fixture-controller","DualSense Wireless Controller",false,BluetoothKind.Controller,null,3),
            new("BTHENUM\\DEV_000000000004\\fixture-soundbar","[Samsung] Soundbar J-Series",null,BluetoothKind.Speaker,null,4)};
        typeof(BluetoothSurface).GetField("rows",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bluetooth,rows);
        typeof(BluetoothSurface).GetField("budsBattery",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bluetooth,new BudsBattery(74,71,25));
        typeof(BluetoothSurface).GetMethod("RefreshState",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(bluetooth,null);
        Thread.Sleep(280);
        Render(bluetooth,Path.Combine(output,"bluetooth.png"));
        Check(Hits(bluetooth).GetArrayLength()==4,"Only the four device drawings are clickable");
        Check(Native.Panels[12]==new Rect(0,0,488,280),"Bluetooth registers its liquid glass bounds");
        bluetooth.SetDisplayed(false);
        Check(Native.Panels[12].IsEmpty||Native.Panels[12].Width==0,"Removing Bluetooth clears the glass");
        bluetooth.DesktopX=4192;bluetooth.DesktopY=936;bluetooth.SetDisplayed(true);bluetooth.UpdateGlassBounds();
        Check(Native.Panels[12]==new Rect(4192,936,488,280),"Readding or moving Bluetooth restores the glass bounds");
        foreach(var size in new[]{new Size(512,128),new Size(128,512),new Size(440,288),new Size(128,128)})
        {
            bluetooth.Width=size.Width;bluetooth.Height=size.Height;
            Render(bluetooth,Path.Combine(output,$"bluetooth-{size.Width}x{size.Height}.png"));
            var slots=BluetoothIconLayout.Arrange(size.Width,size.Height);
            Check(Hits(bluetooth).GetArrayLength()==4,"Every Bluetooth device remains visible in every dock shape");
            Check(slots.All(rect=>new Rect(new Point(),size).Contains(rect)),"Responsive Bluetooth hits stay inside the dock");
            Check(!slots.SelectMany((rect,index)=>slots.Skip(index+1).Select(other=>rect.IntersectsWith(other))).Any(overlaps=>overlaps),"Responsive Bluetooth hits never overlap");
            if(size.Width==512)Check(slots.Select(rect=>rect.Y).Distinct().Count()==1,"Wide Bluetooth dock uses one row");
            if(size.Height==512)Check(slots.Select(rect=>rect.X).Distinct().Count()==1,"Tall Bluetooth dock uses one column");
        }
        bluetooth.Width=488;bluetooth.Height=280;
        Check(new AppIcons().Get(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe")) is not null,"Native shell icon fallback");
        BluetoothSurfaceChecks.Run(bluetooth,args.Contains("--bluetooth-read-only"));
        if(args.Contains("--bluetooth-read-only"))
            foreach(var device in new BluetoothDevices().Scan())
                Console.WriteLine(JsonSerializer.Serialize(new{device.Name,device.Connected,device.ContainerId,device.Address}));
        Console.WriteLine("PASS: font, reminders, app packs and compact add button, audio bounds and meter smoothing, Bluetooth targets, native icon fallback. Render fixtures are not live click validation.");
    }
}
