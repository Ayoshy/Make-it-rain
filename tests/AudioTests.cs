using Battlestation;
using NAudio.Wave;
using System.Diagnostics;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;

internal static class AudioTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static float[] Signal(double hz)
    {
        var analysis=new AudioEnvelope();float[]? last=null;
        for(int i=0;i<8192;i++)last=analysis.Push((float)(.3*Math.Sin(i*2*Math.PI*hz/48000)),48000)??last;
        return last!;
    }
    [STAThread] static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--preview"){Preview(args[1]);return;}
        if(args.Length==4&&args[0]=="--render"){RenderNative(args[1],args[2],args[3]);return;}
        Check(Signal(0).All(x=>x==0),"Silence must return zero energy");
        var low=Signal(85);var high=Signal(9000);
        Check(low.Take(4).Max()>.5f&&low.Skip(8).Max()<.1f,"Bass must excite the low bands only");
        Check(high.Skip(8).Max()>.5f&&high.Take(4).Max()<.1f,"Treble must excite the high bands only");
        Check(low.Concat(high).All(x=>float.IsFinite(x)&&x is >=0 and <=1),"All energy is finite and bounded");
        var analysis=new AudioEnvelope();float[]? invalid=null;
        for(int i=0;i<2048;i++)invalid=analysis.Push(float.NaN,48000)??invalid;
        Check(invalid!.All(x=>x==0),"Invalid samples cannot poison the renderer");
        analysis.Reset();Check(analysis.Push(.5f,48000) is null,"Reset discards the previous sample window");
        string folder=Path.Combine(Path.GetTempPath(),"Battlestation-audio-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"settings.json");var defaults=new DesktopSettings(folder,"Paris",48,2);
            File.WriteAllText(file,JsonSerializer.Serialize(new{defaults.ProjectRoot,defaults.WeatherCity,defaults.Latitude,defaults.Longitude}));
            var migrated=DesktopSettings.Load(file,defaults);Check(migrated.ReactiveAudio&&migrated.AudioIntensity==.55,"Old preferences retain new defaults");
            (migrated with{ReactiveAudio=false,AudioIntensity=.8}).Save(file);var saved=DesktopSettings.Load(file,defaults);
            Check(!saved.ReactiveAudio&&saved.AudioIntensity==.8,"Audio settings survive reload");
            bool rejected=false;try{(defaults with{AudioIntensity=double.NaN}).Validate();}catch(ArgumentException){rejected=true;}
            Check(rejected,"Nonfinite intensity is rejected");
        }
        finally{Directory.Delete(folder,true);}
        Console.WriteLine("PASS: silence, separated frequency bands, invalid samples, reset, preferences migration and persistence.");
        if(args.Contains("--live"))Live();
    }
    static void Preview(string path)
    {
        var app=new Application();using var surface=new AudioSurface(new Station());
        var frame=new System.Windows.Controls.Border{Background=new SolidColorBrush(Color.FromRgb(35,22,49)),Child=surface,Width=720,Height=336};
        frame.Measure(new Size(720,336));frame.Arrange(new Rect(0,0,720,336));frame.UpdateLayout();
        var bitmap=new RenderTargetBitmap(720,336,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
        Console.WriteLine("Preview only; not a live desktop validation: "+path);app.Shutdown();
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] delegate void StartBackground(nint parent,[MarshalAs(UnmanagedType.LPWStr)] string images);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StopBackground();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Appearance(int animate,float opacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Energy(float low,float mid,float high,float intensity);
    static void RenderNative(string dll,string images,string destination)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Battlestation-audio-render-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(folder,"Battlestation"));Directory.CreateDirectory(destination);
        string? original=Environment.GetEnvironmentVariable("LOCALAPPDATA");Environment.SetEnvironmentVariable("LOCALAPPDATA",folder);
        nint library=NativeLibrary.Load(Path.GetFullPath(dll));
        T Export<T>(string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
        var stop=Export<StopBackground>("BackgroundStop");
        using var parent=new HwndSource(new HwndSourceParameters("Isolated musical background fixture"){Width=1,Height=1,PositionX=-10000,PositionY=-10000,WindowStyle=unchecked((int)0x80000000)});
        void Pump(int milliseconds){var dispatch=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();dispatch.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(dispatch);}
        try
        {
            Export<Appearance>("BackgroundAppearance")(0,.46f);Export<StartBackground>("BackgroundStart")(parent.Handle,Path.GetFullPath(images));
            Pump(1800);string frame=Path.Combine(folder,"Battlestation","native-background-frame.png");
            var capture=Export<StopBackground>("BackgroundCapture");
            void Capture(string name)
            {
                if(File.Exists(frame))File.Delete(frame);capture();
                for(int i=0;i<40;i++){Pump(100);if(File.Exists(frame)){try{using var stream=File.Open(frame,FileMode.Open,FileAccess.Read,FileShare.None);if(stream.Length>5000)break;}catch(IOException){}}}
                if(!File.Exists(frame))foreach(string log in Directory.EnumerateFiles(Path.Combine(folder,"Battlestation"),"*error*.txt"))Console.WriteLine(File.ReadAllText(log));
                File.Copy(frame,Path.Combine(destination,name),true);
            }
            Capture("audio-background-silent.png");Export<Energy>("BackgroundAudio")(.8f,.55f,.7f,.85f);Pump(1000);Capture("audio-background-reactive.png");
            Check(!File.ReadAllBytes(Path.Combine(destination,"audio-background-silent.png")).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(destination,"audio-background-reactive.png"))),"Audio energy must change the actual native render");
            Console.WriteLine("PASS: audio energy reaches Direct2D and changes the isolated rendered frame; desktop installation still requires live validation.");
        }
        finally{var stopping=Task.Run(()=>stop());while(!stopping.IsCompleted)Pump(20);stopping.GetAwaiter().GetResult();NativeLibrary.Free(library);Environment.SetEnvironmentVariable("LOCALAPPDATA",original);Directory.Delete(folder,true);}
    }
    static void Live()
    {
        using var mixer=new AudioMixer();mixer.Poll(true);
        Check(mixer.OutputId is not null,"Physical output must be present for the live test");
        string? output=mixer.OutputId;float? volume=mixer.Volume;bool? mute=mixer.Muted,mic=mixer.MicrophoneMuted;
        using var player=new WasapiOut();player.Init(new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000,2)));player.Play();
        Thread.Sleep(400);mixer.Poll(true);
        string key=Process.GetCurrentProcess().ProcessName;
        Check(mixer.Apps.Any(s=>s.Key==key),"The fixture must own a separate Windows audio session");
        var original=mixer.Apps.Single(s=>s.Key==key);
        try
        {
            mixer.SetVolume(key,.37f);mixer.Poll();Check(Math.Abs(mixer.Apps.Single(s=>s.Key==key).Volume-.37)<.005,"Session volume must change in Core Audio");
            mixer.ToggleMute(key);mixer.Poll();Check(mixer.Apps.Single(s=>s.Key==key).Muted!=original.Muted,"Session mute must change in Core Audio");
            mixer.SetVolume(key,float.NaN);mixer.Poll();Check(Math.Abs(mixer.Apps.Single(s=>s.Key==key).Volume-.37)<.005,"Invalid writes do not change the session");
            using var spectrum=new Spectrum();spectrum.Start();Thread.Sleep(400);
            Check(spectrum.Running&&spectrum.Error is null&&spectrum.DeviceId==output,"Loopback captures the real default output");
            spectrum.Stop();Check(!spectrum.Running&&spectrum.Bands.All(x=>x==0),"Stopping clears all aggregate audio data");
        }
        finally
        {
            mixer.SetVolume(key,original.Volume);mixer.Poll();
            if(mixer.Apps.Single(s=>s.Key==key).Muted!=original.Muted)mixer.ToggleMute(key);
            player.Stop();
        }
        mixer.Poll(true);
        Check(mixer.OutputId==output&&mixer.Volume==volume&&mixer.Muted==mute&&mixer.MicrophoneMuted==mic,"User output, master volume and microphone must remain unchanged");
        Console.WriteLine(JsonSerializer.Serialize(new{status="PASS live",output=mixer.OutputName,outputs=mixer.Outputs.Count,apps=mixer.Apps.Select(x=>x.Name),microphone=mixer.MicrophoneName,userAudioPreserved=true}));
    }
}

namespace Battlestation
{
    // The preview exercises the real drawing surface without booting backends or the desktop.
    internal sealed class Station {}
    internal static class Native {internal static void BackgroundPanel(int slot,float x,float y,float w,float h){}}
}
