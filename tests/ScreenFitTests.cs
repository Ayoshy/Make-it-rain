using System.IO;
using System.Text.Json;
using System.Windows;
using Battlestation;

// The docks are authored on a 2 x 2560 x 1440 plan. These checks state what
// happens when the real monitor set is a laptop panel, a 1920 x 1080 secondary
// or an ultrawide: positions follow, sizes never exceed a screen, and the
// authored plan itself stays byte-for-byte identical.
internal static class ScreenFitTests
{
    internal static void Run(string directory,Action<bool,string> check)
    {
        string folder=Path.Combine(directory,"screen-fit");Directory.CreateDirectory(folder);
        Rect[] authored=DesktopScreens.Reference;
        Rect[] secondary1080=[authored[0],new(2560,0,1920,1080)];
        Rect[] laptop=[new(0,0,1920,1080)];
        Rect[] ultrawide=[new(0,0,3440,1440)];

        var reference=new DesktopLayout(Path.Combine(folder,"reference.json"),11,authored);
        check(reference.Blocks.SequenceEqual(DesktopLayout.Defaults(11)),"Plan 2 x 2560 x 1440 : aucune derive des positions ni des tailles");

        var mono=new DesktopLayout(Path.Combine(folder,"mono.json"),11,laptop);
        check(mono.Blocks.Where(b=>b.Visible).All(b=>laptop[0].Contains(b.Bounds)),"Portable 1920 x 1080 : chaque dock visible tient sur la dalle");

        var wide=new DesktopLayout(Path.Combine(folder,"wide.json"),11,ultrawide);
        check(wide.Blocks.Where(b=>b.Visible).All(b=>ultrawide[0].Contains(b.Bounds)),"Ultra large 3440 x 1440 : chaque dock visible tient sur la dalle");
        check(wide.Blocks.Where(b=>b.Visible).Count()>=mono.Blocks.Where(b=>b.Visible).Count(),"Ultra large : au moins autant de docks places qu'en 1920 x 1080");

        // The authored plan keeps the left column on the first screen; only the
        // blocks written for the secondary move into the smaller panel.
        var dual=new DesktopLayout(Path.Combine(folder,"dual.json"),11,secondary1080);
        check(dual.Blocks.All(b=>secondary1080.Any(screen=>screen.Contains(b.Bounds))),"Secondaire 1920 x 1080 : tous les blocs restent sur une dalle connue");
        foreach(string id in new[]{"clock","weather","apps","dualsense","network","montagne"})
            check(dual[id].X==DesktopLayout.Defaults(11).Single(b=>b.Id==id).X,id+" garde la position du plan sur le premier ecran");

        // A saved arrangement from the 1440p pair must fit the new pair at load.
        string saved=Path.Combine(folder,"saved.json");
        File.WriteAllText(saved,JsonSerializer.Serialize(new List<DesktopBlock>{
            new("clock","Horloge",560,168,24,24),
            new("terminal","Terminal",1656,1200,880,216),
            new("countdown","Vice City",760,364,3712,1052),
            new("video","Video",1384,720,3712,24)}));
        var loaded=new DesktopLayout(saved,11,secondary1080);
        check(loaded.Blocks.All(b=>secondary1080.Any(screen=>screen.Contains(b.Bounds))),"Agencement 1440p relu sur un secondaire 1080p : tout reste sur une dalle");
        check(loaded["clock"]==new DesktopBlock("clock","Horloge",560,168,24,24),"Le premier ecran n'est pas retouche");
        check(loaded["countdown"].X+loaded["countdown"].Width<=authored[1].Right-2560+authored[1].Left+1920&&loaded["countdown"].Y+loaded["countdown"].Height<=1080,"Le bloc du secondaire est ramene dans la dalle 1080p");

        // Scene presets are authored too: they must land inside the real set.
        foreach(string name in DesktopProfiles.Names)
        {
            var preset=DesktopProfiles.Preset(name,DesktopLayout.Defaults(11));
            check(mono.Adapt(preset).Where(b=>b.Visible).All(b=>laptop[0].Contains(b.Bounds)),"Modele "+name+" : les docks visibles tiennent sur un portable");
            check(dual.Adapt(preset).All(b=>secondary1080.Any(screen=>screen.Contains(b.Bounds))),"Modele "+name+" : les blocs restent sur une dalle en 1080p");
        }
    }
}
