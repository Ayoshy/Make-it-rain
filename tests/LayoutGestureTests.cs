using Battlestation;
using System.Windows;
using System.IO;
using System.Text.Json;

internal static class LayoutGestureTests
{
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Near(double a,double b,string reason)=>Check(Math.Abs(a-b)<.01,$"{reason}: {a} != {b}");
    static DesktopBlock Block(string id,double x,double y,double w=200,double h=200)=>new(id,id,w,h,x,y);
    static DesktopBlock Find(LayoutPreview p,string id)=>p.Blocks.Single(b=>b.Id==id);
    static void Valid(LayoutPreview p)
    {
        foreach(var b in p.Blocks)Check(DesktopLayout.Valid(b,p.Blocks),"Invalid geometry: "+b.Id);
    }
    public static void Run()
    {
        DesktopBlock[] example=[Block("clock",40,0,720,164),Block("music",40,180,720,168),Block("projects",40,364,720,293),Block("video",808,0,576,380)];
        var resize=new LayoutGesture(example,"video",LayoutEdge.Left,new(808,190),false,8);
        var p=resize.Preview(new(608,190));Valid(p);
        foreach(string id in new[]{"clock","music","projects"}){Near(Find(p,id).Width,520,"Three neighbours shrink");Near(Find(p,id).X,40,"Outer edge fixed");}
        Near(Find(p,"video").Width,776,"Video grows");Check(p.Affected.Count==4,"Partial project contact joins separator");
        p=resize.Preview(new(108,190));Valid(p);Check(p.Blocked,"Minimum blocks entire separator");
        Near(Find(p,"music").Width,400,"Reader minimum");Near(Find(p,"clock").Width,400,"Column alignment at minimum");
        p=resize.Preview(new(808,190));Check(p.Blocks.SequenceEqual(example),"Return restores exact start");
        p=resize.Preview(new(872,190));Valid(p);Near(Find(p,"clock").Width,784,"Neighbours recover space");Near(Find(p,"video").Width,512,"Video shrinks");
        p=resize.Preview(new(700.25,190));Near(Find(p,"video").X,700.25,"Continuous grid-off gesture");
        var snapped=new LayoutGesture(example,"video",LayoutEdge.Left,new(808,190),true,16).Preview(new(701,190));Valid(snapped);Near(Find(snapped,"video").X,704,"Selected edge snaps");Near(Find(snapped,"video").X-(Find(snapped,"clock").X+Find(snapped,"clock").Width),48,"Existing gutter preserved");
        var diagonal=new LayoutGesture([Block("a",100,100),Block("b",312,312)],"a",LayoutEdge.Right,new(300,200),false,8).Preview(new(310,200));
        Check(!diagonal.Affected.Contains("b"),"Diagonal contact is not a separator");
        var late=new LayoutGesture([Block("a",100,100),Block("b",500,100)],"a",LayoutEdge.Right,new(300,180),false,8);
        p=late.Preview(new(450,180));Near(Find(p,"b").Width,200,"Distant neighbour not recruited early");
        p=late.Preview(new(520,180));Valid(p);Near(Find(p,"b").X,532,"New obstacle joins at gap");Near(Find(p,"b").Width,168,"Encountered obstacle shrinks");
        DesktopBlock[] chain=[Block("a",100,100),Block("b",312,100),Block("c",524,100)];
        var independent=new LayoutGesture(example,"video",LayoutEdge.Left,new(808,190),false,8,false);
        p=independent.Preview(new(872,190));Valid(p);Near(Find(p,"video").Width,512,"Unlinked resize changes selected dock");
        foreach(var b in example.Where(b=>b.Id!="video"))Check(Find(p,b.Id)==b,"Unlinked neighbours retain exact geometry");
        p=independent.Preview(new(608,190));Valid(p);Check(p.Blocked,"Unlinked enlargement stops at a neighbour");Near(Find(p,"video").X,772,"Unlinked stop retains minimum gutter");
        foreach(var b in example.Where(b=>b.Id!="video"))Check(Find(p,b.Id)==b,"Unlinked collision never resizes neighbours");
        var soloMove=new LayoutGesture(chain,"a",LayoutEdge.Move,new(150,150),true,8,false);
        p=soloMove.Preview(new(250,150));Check(p.Blocked&&p.Blocks.SequenceEqual(chain),"Unlinked occupied drop never pushes a chain");
        p=soloMove.Preview(new(250,450));Valid(p);Near(Find(p,"a").X,200,"Unlinked move still snaps to grid");Near(Find(p,"a").Y,400,"Unlinked move reaches free space");
        Check(Find(p,"b")==chain[1]&&Find(p,"c")==chain[2],"Unlinked movement leaves all neighbours unchanged");
        var move=new LayoutGesture(chain,"a",LayoutEdge.Move,new(150,150),false,8);
        p=move.Preview(new(250,150));Valid(p);Near(Find(p,"b").X,412,"First push");Near(Find(p,"c").X,624,"Cascade");Check(p.Blocks.All(b=>b.Width==200),"Push never resizes");
        p=move.Preview(new(200,150));Near(Find(p,"b").X,362,"Push calculated from start");
        p=move.Preview(new(150,150));Check(p.Blocks.SequenceEqual(chain),"Push reversibility");
        var edge=new LayoutGesture([Block("a",1900,100),Block("b",2112,100),Block("c",2324,100)],"a",LayoutEdge.Move,new(1950,150),false,8);
        p=edge.Preview(new(1970,150));Valid(p);var previous=p.Blocks;
        p=edge.Preview(new(2200,150));Check(p.Blocked&&p.Blocks.SequenceEqual(previous),"Blocked chain keeps last valid preview");Check(p.Blocks.All(b=>b.X<2560),"Neighbours stay on screen");
        var crossing=new LayoutGesture(chain,"a",LayoutEdge.Move,new(150,150),true,8).Preview(new(2700,150));Valid(crossing);Check(Find(crossing,"a").X>=2560,"Selected dock crosses screens");Near(Find(crossing,"b").X,312,"Origin neighbours untouched after crossing");
        var corner=new LayoutGesture([Block("a",100,100)],"a",LayoutEdge.Left|LayoutEdge.Bottom,new(100,300),false,8).Preview(new(290,360));Valid(corner);Check(corner.Blocked,"One corner axis hits minimum");Near(Find(corner,"a").Width,120,"Width minimum");Near(Find(corner,"a").Height,260,"Other corner axis progresses");
        // Sampling unrelated pointer paths must not accumulate error or overlap.
        var random=new Random(741);
        foreach(var summary in new[]{new Rect(4680,0,440,209),new Rect(0,0,440,1440),new Rect(100,100,440,1300)})
        {
            var expanded=DashboardBounds.Expand(summary,summary.Height+234,true,779);
            Check(expanded.Contains(summary)&&DesktopLayout.Screens.Any(s=>s.Contains(expanded)),"Responsive drawer stays on screen and retains its summary anchor");
        }
        for(int i=0;i<150;i++){p=resize.Preview(new(808+random.Next(-800,800),190));Valid(p);foreach(var b in p.Blocks){var min=DesktopLayout.Minimum(b.Id);Check(b.Width>=min.Width-.01&&b.Height>=min.Height-.01,"Minimum invariant");}}
        var file=Path.Combine(Path.GetTempPath(),"Battlestation-resize-"+Guid.NewGuid()+".json");
        try
        {
            var layout=new DesktopLayout(file,5);foreach(var b in layout.Blocks.ToArray())layout.SetVisible(b.Id,false);
            var blocks=layout.Blocks.Select(b=>b.Id=="video"?b with{X=100,Y=100,Width=640,Height=400,Visible=true}:b).ToArray();
            Check(layout.Restore(blocks),"Restore resized arrangement");layout.Save();var loaded=new DesktopLayout(file,5);
            Check(loaded["video"]==layout["video"],"Width and height persist");
            Check(!loaded.Restore(blocks.Select(b=>b.Id=="video"?b with{Width=double.NaN}:b)),"Reject invalid dimensions atomically");
            Check(loaded["video"]==layout["video"],"Invalid restore does not alter layout");
        }
        finally{if(File.Exists(file))File.Delete(file);}
        Console.WriteLine("PASS: linked resizing, partial contact, minima, reverse, grids, cascade, screen bounds, corners, fuzz and dimension persistence.");
    }
}
