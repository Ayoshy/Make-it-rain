using System.IO;
using System.Text.Json;
using System.Windows;

namespace Battlestation;
internal sealed record DesktopBlock(string Id,string Title,double Width,double Height,double X,double Y,bool Visible=true)
{
    [System.Text.Json.Serialization.JsonIgnore] public Rect Bounds=>new(X,Y,Width,Height);
}
internal sealed class DesktopLayout
{
    public const int Grid=12,Gap=12;
    public static Size Minimum(string id)=>id switch
    {
        "clock"=>new(360,144),"weather"=>new(360,112),"music"=>new(400,168),
        "projects"=>new(360,240),"apps"=>new(144,112),"terminal"=>new(480,300),
        "countdown"=>new(400,360),"hardware"=>new(440,218),"usage"=>new(440,209),
        "network"=>new(440,280),"dualsense"=>new(480,380),"video"=>new(480,270),"audio"=>new(440,220),"bluetooth"=>new(128,128),"reminders"=>new(440,112),
        "lol"=>new(440,180),"shopping"=>new(560,420),"notes"=>new(320,180),"atelier"=>new(440,280),"disks"=>new(440,320),"gmail"=>new(440,320),_=>new(120,100)
    };
    static DesktopBlock Dimensions(DesktopBlock original,DesktopBlock saved)=>original with
    {
        X=saved.X,Y=saved.Y,Visible=saved.Visible,
        Width=saved.Width==0?original.Width:saved.Width,
        Height=saved.Height==0?original.Height:saved.Height
    };
    readonly string path;
    public List<DesktopBlock> Blocks {get;private set;}
    // Real monitors in DIPs. The authored plan below stays the reference every
    // saved arrangement is written against, so a different monitor set only
    // translates positions instead of invalidating them.
    public IReadOnlyList<Rect> Screens {get;private set;}=DesktopScreens.Reference;
    internal bool SingleScreen {get;set;}
    internal IEnumerable<Rect> AvailableScreens=>SingleScreen?Screens.Take(1):Screens;
    bool Fits(DesktopBlock block,IEnumerable<DesktopBlock> others)=>Valid(block,others,Screens)&&(!block.Visible||!SingleScreen||Screens[0].Contains(block.Bounds));
    public DesktopBlock this[string id]=>Blocks.Single(b=>b.Id==id);
    public static List<DesktopBlock> Defaults(int apps)=>[
        new("clock","Horloge",720,164,2688,0),
        new("weather","Météo",720,112,2688,192),
        new("apps","Applications",720,DockHeight(apps),3456,0),
        new("music","Lecteur",720,168,3456,144+(DockHeight(apps)-116)),
        new("projects","Projets",720,293,3456,336+(DockHeight(apps)-116)),
        new("terminal","Terminal",1488,1428-TerminalY(apps),2688,TerminalY(apps)),
        new("countdown","Vice City",779,706,4212,132),
        new("hardware","Matériel",779,218,4212,852),
        new("usage","AI Meter",779,209,4212,1092),
        new("reminders","Nudge",779,112,4212,0),
        new("video","Vidéo",576,372,3396,312,false),
        new("audio","Audio",720,264,2616,360,false),
        new("bluetooth","Bluetooth",560,280,2616,720,false),
        new("dualsense","DualSense",720,440,24,384,false),
        new("network","Réseau",720,336,24,912,false),
        // LoL arrives hidden until asked for.
        new("lol","LoL",700,220,24,960,false),
        // Le radar d'achat arrive masqué lui aussi : il s'ajoute depuis « Ajouter un bloc ».
        new("shopping","Achats",700,520,1080,384,false),
        new("notes","Bloc-notes",480,320,24,384,false),
        new("atelier","Atelier",560,280,24,384,false),
        new("disks","Disques",560,600,1344,552,false),
        new("gmail","Gmail",560,460,1344,552,false)
    ];
    static double TerminalY(int apps)=>Math.Max(720,Math.Ceiling((336+DockHeight(apps)-116+293+Gap)/Grid)*Grid);
    public static double DockHeight(int apps)=>Math.Max(1,Math.Ceiling(apps/6d))*88+28;
    public DesktopLayout(string file,int apps,IReadOnlyList<Rect>? screens=null)
    {
        path=file;if(screens is{Count:>0})Screens=[..screens];Blocks=Adapt(Defaults(apps));
        if(!File.Exists(path))return;
        try
        {
            var saved=JsonSerializer.Deserialize<List<DesktopBlock>>(File.ReadAllText(path));
            if(saved is null)return;
            var restored=new List<DesktopBlock>();
            foreach(var original in Blocks)
            {
                var old=saved.FirstOrDefault(b=>b.Id==original.Id);
                var item=old is null?original:Dimensions(original,old);
                if(!Screens.Any(screen=>screen.Contains(item.Bounds)))item=MoveInto(item);
                if(!Valid(item,restored,Screens))item=Place(item,restored)??item with{Visible=false};
                restored.Add(item);
            }
            Blocks=restored;
        }
        catch(JsonException){File.Copy(path,path+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true);}
    }
    internal static bool Valid(DesktopBlock block,IEnumerable<DesktopBlock> others,IReadOnlyList<Rect>? screens=null)
    {
        if(!double.IsFinite(block.X+block.Y+block.Width+block.Height)||block.Width<=0||block.Height<=0)return false;
        if(!(screens??DesktopScreens.Reference).Any(screen=>screen.Contains(block.Bounds)))return false;
        if(!block.Visible)return true;
        var occupied=block.Bounds;occupied.Inflate(Gap/2d,Gap/2d);
        return !others.Any(b=>{
            if(!b.Visible||b.Id==block.Id)return false;
            var other=Inflated(b.Bounds);
            // Exact 12 px gaps are valid: touching inflated edges do not overlap.
            return occupied.Left<other.Right&&occupied.Right>other.Left&&occupied.Top<other.Bottom&&occupied.Bottom>other.Top;
        });
    }
    internal bool Valid(DesktopBlock block,IEnumerable<DesktopBlock> others)=>Valid(block,others,Screens);
    internal bool Valid(DesktopBlock block)=>Valid(block,Blocks,Screens);
    static Rect Inflated(Rect r){r.Inflate(Gap/2d,Gap/2d);return r;}
    DesktopBlock? FindFree(DesktopBlock block,double x,double y,IEnumerable<DesktopBlock> others)
    {
        if(!double.IsFinite(x+y))return null;
        var occupied=others.Where(b=>b.Visible&&b.Id!=block.Id).ToArray();
        var wanted=block with{X=Math.Round(x/Grid)*Grid,Y=Math.Round(y/Grid)*Grid};
        if(Fits(wanted,occupied))return wanted;
        DesktopBlock? best=null;double distance=double.PositiveInfinity;
        foreach(var screen in AvailableScreens)
            for(double yy=screen.Top;yy+block.Height<=screen.Bottom;yy+=Grid)
                for(double xx=Math.Ceiling(screen.Left/Grid)*Grid;xx+block.Width<=screen.Right;xx+=Grid)
                {
                    double d=(xx-x)*(xx-x)+(yy-y)*(yy-y);if(d>=distance)continue;
                    var candidate=block with{X=xx,Y=yy};
                    if(Fits(candidate,occupied)){best=candidate;distance=d;}
                }
        return best;
    }
    // A block written on another monitor set keeps its intent: it moves into the
    // monitor owning its reference screen, then shrinks to its minimum before it
    // is dropped for lack of space.
    internal List<DesktopBlock> Adapt(IEnumerable<DesktopBlock> authored)
    {
        var result=new List<DesktopBlock>();
        foreach(var block in authored)
        {
            var item=Screens.Any(screen=>screen.Contains(block.Bounds))?block:MoveInto(block);
            if(!Valid(item,result,Screens))item=Place(item,result)??item with{Visible=false};
            result.Add(item);
        }
        return result;
    }
    DesktopBlock? Place(DesktopBlock block,IEnumerable<DesktopBlock> others)
    {
        if(FindFree(block,block.X,block.Y,others) is{} placed)return placed;
        var minimum=Minimum(block.Id);var screens=AvailableScreens.ToArray();
        if(screens.Length==0)return null;
        var smaller=block with{
            Width=Math.Max(minimum.Width,Math.Min(block.Width,screens.Max(screen=>screen.Width))),
            Height=Math.Max(minimum.Height,Math.Min(block.Height,screens.Max(screen=>screen.Height)))};
        return smaller==block?null:FindFree(smaller,block.X,block.Y,others);
    }
    DesktopBlock MoveInto(DesktopBlock block)
    {
        var screens=AvailableScreens.ToArray();
        if(screens.Length==0)return block with{Visible=false};
        int index=ReferenceIndex(block);var reference=DesktopScreens.Reference[index];
        var target=screens[Math.Min(index,screens.Length-1)];
        double width=Math.Min(block.Width,target.Width),height=Math.Min(block.Height,target.Height);
        double x=target.Left+Math.Clamp(block.X-reference.Left,0,Math.Max(0,target.Width-width));
        double y=target.Top+Math.Clamp(block.Y-reference.Top,0,Math.Max(0,target.Height-height));
        return block with{X=x,Y=y,Width=width,Height=height};
    }
    static int ReferenceIndex(DesktopBlock block)
    {
        int best=0;double score=double.NegativeInfinity;
        for(int i=0;i<DesktopScreens.Reference.Length;i++)
        {
            var screen=DesktopScreens.Reference[i];var hit=Rect.Intersect(screen,block.Bounds);
            double distance=Math.Abs(screen.Left+screen.Width/2-(block.X+block.Width/2))+Math.Abs(screen.Top+screen.Height/2-(block.Y+block.Height/2));
            double value=hit.IsEmpty||hit.Width<=0||hit.Height<=0?-distance:1e12+hit.Width*hit.Height;
            if(value>score){score=value;best=i;}
        }
        return best;
    }
    internal void SetScreens(IReadOnlyList<Rect> screens,bool refit)
    {
        if(screens.Count==0||screens.SequenceEqual(Screens))return;
        Screens=[..screens];
        if(refit)Blocks=Adapt(Blocks);
    }
    public bool Move(string id,double x,double y)
    {
        var next=FindFree(this[id],x,y,Blocks);if(next is null)return false;
        Blocks[Blocks.FindIndex(b=>b.Id==id)]=next;return true;
    }
    public bool Resize(string id,double height)
    {
        var block=this[id];var next=FindFree(block with{Height=height},block.X,block.Y,Blocks);
        if(next is null)return false;Blocks[Blocks.FindIndex(b=>b.Id==id)]=next;return true;
    }
    public bool SetVisible(string id,bool visible)
    {
        var b=this[id];var shown=b with{Visible=true};
        var next=visible?(Fits(shown,Blocks)?shown:FindFree(shown,b.X,b.Y,Blocks)):b with{Visible=false};
        if(next is null)return false;Blocks[Blocks.FindIndex(b=>b.Id==id)]=next;return true;
    }
    public void Reset(int apps)=>Blocks=Adapt(Defaults(apps));
    public bool Restore(IEnumerable<DesktopBlock> saved)
    {
        var incoming=saved.ToArray();if(incoming.Any(b=>b is null))return false;var next=new List<DesktopBlock>();
        foreach(var block in Blocks)
        {
            var old=incoming.FirstOrDefault(b=>b.Id==block.Id);
            var candidate=old is null?block with{Visible=false}:Dimensions(block,old);
            if(!Fits(candidate,next))return false;next.Add(candidate);
        }
        Blocks=next;return true;
    }
    internal event Action? Saved;
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Blocks,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
        Saved?.Invoke();
    }
}
