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
    readonly string path;
    public List<DesktopBlock> Blocks {get;private set;}
    public static readonly Rect[] Screens=[new(0,0,2560,1440),new(2560,0,2560,1440)];
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
        new("usage","Codex",779,209,4212,1092)
    ];
    static double TerminalY(int apps)=>Math.Max(720,Math.Ceiling((336+DockHeight(apps)-116+293+Gap)/Grid)*Grid);
    public static double DockHeight(int apps)=>Math.Max(1,Math.Ceiling(apps/6d))*88+28;
    public DesktopLayout(string file,int apps)
    {
        path=file;Blocks=Defaults(apps);
        if(!File.Exists(path))return;
        try
        {
            var saved=JsonSerializer.Deserialize<List<DesktopBlock>>(File.ReadAllText(path));
            if(saved is null)return;
            var restored=new List<DesktopBlock>();
            foreach(var original in Blocks)
            {
                var old=saved.FirstOrDefault(b=>b.Id==original.Id);
                var item=old is null?original:original with{X=old.X,Y=old.Y,Visible=old.Visible};
                if(!Valid(item,restored))item=FindFree(original with{Visible=item.Visible},original.X,original.Y,restored)??original with{Visible=false};
                restored.Add(item);
            }
            Blocks=restored;
        }
        catch(JsonException){File.Copy(path,path+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true);}
    }
    static bool Valid(DesktopBlock block,IEnumerable<DesktopBlock> others)
    {
        if(!double.IsFinite(block.X+block.Y+block.Width+block.Height)||block.Width<=0||block.Height<=0)return false;
        if(!Screens.Any(s=>s.Contains(block.Bounds)))return false;
        if(!block.Visible)return true;
        var occupied=block.Bounds;occupied.Inflate(Gap/2d,Gap/2d);
        return !others.Any(b=>b.Visible&&b.Id!=block.Id&&occupied.IntersectsWith(Inflated(b.Bounds)));
    }
    static Rect Inflated(Rect r){r.Inflate(Gap/2d,Gap/2d);return r;}
    static DesktopBlock? FindFree(DesktopBlock block,double x,double y,IEnumerable<DesktopBlock> others)
    {
        if(!double.IsFinite(x+y))return null;
        var occupied=others.Where(b=>b.Visible&&b.Id!=block.Id).ToArray();
        var wanted=block with{X=Math.Round(x/Grid)*Grid,Y=Math.Round(y/Grid)*Grid};
        if(Valid(wanted,occupied))return wanted;
        DesktopBlock? best=null;double distance=double.PositiveInfinity;
        foreach(var screen in Screens)
            for(double yy=screen.Top;yy+block.Height<=screen.Bottom;yy+=Grid)
                for(double xx=Math.Ceiling(screen.Left/Grid)*Grid;xx+block.Width<=screen.Right;xx+=Grid)
                {
                    double d=(xx-x)*(xx-x)+(yy-y)*(yy-y);if(d>=distance)continue;
                    var candidate=block with{X=xx,Y=yy};
                    if(Valid(candidate,occupied)){best=candidate;distance=d;}
                }
        return best;
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
        var b=this[id];var next=visible?FindFree(b with{Visible=true},b.X,b.Y,Blocks):b with{Visible=false};
        if(next is null)return false;Blocks[Blocks.FindIndex(b=>b.Id==id)]=next;return true;
    }
    public void Reset(int apps)=>Blocks=Defaults(apps);
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Blocks,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
}
