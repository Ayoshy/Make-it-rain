using System.Windows;

namespace Battlestation;

internal readonly record struct DiskTile(DiskNode Node,Rect Bounds,int Color);
internal static class DiskMap
{
    // Balanced area subdivision keeps tiles compact and deterministic for a given tree.
    internal static DiskTile[] Layout(DiskNode[] nodes,Rect bounds)
    {
        var items=nodes.Where(n=>n.Bytes>0).OrderByDescending(n=>n.Bytes).ThenBy(n=>n.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        var result=new List<DiskTile>();
        void Split(int start,int count,Rect area)
        {
            if(count==0||area.Width<=0||area.Height<=0)return;
            if(count==1){uint hash=2166136261;foreach(char c in items[start].Name)hash=(hash^c)*16777619;result.Add(new(items[start],area,(int)(hash%6)));return;}
            double total=0;for(int i=start;i<start+count;i++)total+=items[i].Bytes;
            double sum=items[start].Bytes;int cut=1;
            while(cut<count-1&&Math.Abs(sum+items[start+cut].Bytes-total/2)<Math.Abs(sum-total/2)){sum+=items[start+cut].Bytes;cut++;}
            double fraction=sum/total;
            if(area.Width>=area.Height){double width=area.Width*fraction;Split(start,cut,new(area.X,area.Y,width,area.Height));Split(start+cut,count-cut,new(area.X+width,area.Y,area.Width-width,area.Height));}
            else{double height=area.Height*fraction;Split(start,cut,new(area.X,area.Y,area.Width,height));Split(start+cut,count-cut,new(area.X,area.Y+height,area.Width,area.Height-height));}
        }
        Split(0,items.Length,bounds);return result.ToArray();
    }
}
