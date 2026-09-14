using System.IO;
using Battlestation;

internal static class DesktopLayoutTests
{
    static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
    static void Validate(DesktopLayout layout)
    {
        foreach(var block in layout.Blocks.Where(b=>b.Visible))
        {
            Check(DesktopLayout.Screens.Any(s=>s.Contains(block.Bounds)),"Block left its screen: "+block.Id);
            foreach(var other in layout.Blocks.Where(b=>b.Visible&&b.Id!=block.Id))Check(!block.Bounds.IntersectsWith(other.Bounds),"Blocks overlap: "+block.Id+" / "+other.Id);
        }
    }
    static void Main()
    {
        LayoutGestureTests.Run();
        var directory=Path.Combine(Path.GetTempPath(),"Battlestation-layout-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var path=Path.Combine(directory,"layout.json");
            foreach(int apps in new[]{0,5,6,7,12})Validate(new DesktopLayout(path,apps));
            var layout=new DesktopLayout(path,5);
            var original=layout.Blocks.Where(b=>b.Id!="video").ToArray();
            Check(!layout["video"].Visible,"New video block must not relocate existing desktop widgets");
            Check(layout.SetVisible("video",true),"Video can be added to a free grid position");Validate(layout);
            Check(original.SequenceEqual(layout.Blocks.Where(b=>b.Id!="video")),"Adding video must preserve every existing block");
            Check(layout.Move("video",900,600),"Video is movable");layout.Save();
            var savedVideo=new DesktopLayout(path,5);Check(savedVideo["video"]==layout["video"],"Video visibility and position survive reload");
            layout.SetVisible("video",false);
            var touching=new DesktopLayout(Path.Combine(directory,"touching.json"),5);
            foreach(var block in touching.Blocks.ToArray())touching.SetVisible(block.Id,false);
            touching.SetVisible("video",true);touching.Move("video",0,0);touching.SetVisible("clock",true);touching.Move("clock",0,384);
            Check(touching["clock"].X==0&&touching["clock"].Y==384,"Exactly one grid gap must fit without relocation");Validate(touching);
            Check(touching.Move("video",3396,312)&&touching["video"].X==3396,"Video moves from primary to secondary monitor");
            foreach(var summary in new[]{new System.Windows.Rect(4272,720,779,218),new System.Windows.Rect(4272,1210,779,218),new System.Windows.Rect(36,0,779,209),new System.Windows.Rect(4272,960,779,209)})
            {
                foreach(bool above in new[]{true,false})
                {
                    var expanded=DashboardBounds.Expand(summary,443,above);
                    Check(expanded.X==summary.X&&expanded.Contains(summary),"Drawer must preserve the complete summary anchor");
                    Check(DesktopLayout.Screens.Any(s=>s.Contains(expanded)&&s.Contains(summary)),"Drawer must stay on its original screen, including bottom edge");
                    Check(DashboardBounds.Expand(summary,summary.Height,above)==summary,"Closing restores exact bounds");
                }
            }
            Check(layout.Move("clock",layout["apps"].X,layout["apps"].Y),"Occupied target must find a free slot");Validate(layout);
            Check(layout.Move("clock",60,84),"Can move to first screen");Check(layout["clock"].X<2560,"First screen move applied");
            Check(!layout.Move("clock",double.NaN,12),"Invalid coordinates rejected");
            Check(layout.SetVisible("countdown",false),"Countdown removable");
            Check(layout["hardware"].Visible&&layout["usage"].Visible,"Removing countdown must keep monitoring");
            Check(layout.Resize("hardware",443),"Hardware expansion finds space");Validate(layout);
            Check(layout.Resize("usage",443),"Usage expansion finds space independently");Validate(layout);
            Check(layout.SetVisible("countdown",true),"Removed block can be restored into free space");Validate(layout);
            layout.Save();var restored=new DesktopLayout(path,5);Validate(restored);
            Check(restored["clock"].X==layout["clock"].X&&restored["clock"].Y==layout["clock"].Y,"Positions survive reload");
            Check(restored.Resize("apps",DesktopLayout.DockHeight(12)),"Adding dock apps reserves another row");Validate(restored);
            foreach(var b in restored.Blocks.ToArray())restored.SetVisible(b.Id,false);
            restored.Save();var empty=new DesktopLayout(path,12);Check(empty.Blocks.All(b=>!b.Visible),"All hidden state survives reload");
            Check(empty.SetVisible("terminal",true),"Tray can restore terminal from empty desktop");Validate(empty);
            Console.WriteLine("PASS: default layouts, collisions, two screens, invalid coordinates, independent removal/expansion, persistence, dock growth and empty-desktop restoration.");
        }
        finally{Directory.Delete(directory,true);}
    }
}
