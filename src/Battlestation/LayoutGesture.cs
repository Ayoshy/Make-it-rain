using System.Windows;

namespace Battlestation;

[Flags]
internal enum LayoutEdge { Move=0,Left=1,Right=2,Top=4,Bottom=8 }
internal sealed record LayoutPreview(IReadOnlyList<DesktopBlock> Blocks,IReadOnlySet<string> Affected,bool Blocked);

// A gesture always solves against its immutable starting arrangement. No window,
// persistence or backend side effects belong in this geometry engine.
internal sealed class LayoutGesture
{
    readonly DesktopBlock[] start;
    readonly string id;
    readonly LayoutEdge edge;
    readonly Point pointerStart;
    readonly int step;
    readonly bool grid;
    readonly bool linkNeighbors;
    readonly Rect[] screens;
    bool? pushHorizontal;
    int pushSign;
    LayoutPreview last;
    public IReadOnlyList<DesktopBlock> Start=>start;
    public LayoutGesture(IEnumerable<DesktopBlock> blocks,string selected,LayoutEdge handle,Point pointer,bool snap,int gridStep,bool linkNeighbors=true,IEnumerable<Rect>? screens=null)
    {
        start=blocks.ToArray();id=selected;edge=handle;pointerStart=pointer;grid=snap;step=Math.Clamp(gridStep,4,64);
        this.linkNeighbors=linkNeighbors;
        this.screens=(screens??DesktopScreens.Reference).ToArray();
        last=new(start,new HashSet<string>{id},false);
    }
    static bool Overlap(double a,double b,double c,double d)=>a<d-.001&&b>c+.001;
    static double Low(DesktopBlock b,bool horizontal)=>horizontal?b.X:b.Y;
    static double High(DesktopBlock b,bool horizontal)=>Low(b,horizontal)+(horizontal?b.Width:b.Height);
    static bool Across(DesktopBlock a,DesktopBlock b,bool horizontal)=>Overlap(Low(a,!horizontal),High(a,!horizontal),Low(b,!horizontal),High(b,!horizontal));
    Rect Screen(DesktopBlock b)=>screens.First(s=>s.Contains(b.Bounds));
    static DesktopBlock Translate(DesktopBlock b,bool horizontal,double amount)=>horizontal?b with{X=b.X+amount}:b with{Y=b.Y+amount};
    static DesktopBlock SetEdge(DesktopBlock b,bool horizontal,bool low,double amount)=>horizontal
        ?low?b with{X=b.X+amount,Width=b.Width-amount}:b with{Width=b.Width+amount}
        :low?b with{Y=b.Y+amount,Height=b.Height-amount}:b with{Height=b.Height+amount};
    double Snap(double value,double origin)=>grid?origin+Math.Round((value-origin)/step)*step:value;
    bool Valid(DesktopBlock[] blocks)=>blocks.All(b=>DesktopLayout.Valid(b,blocks,screens));
    public LayoutPreview Preview(Point pointer)
    {
        if(!double.IsFinite(pointer.X+pointer.Y))return last with{Blocked=true};
        var selected=start.Single(b=>b.Id==id);var screen=Screen(selected);
        double dx=pointer.X-pointerStart.X,dy=pointer.Y-pointerStart.Y;
        if(Math.Abs(dx)+Math.Abs(dy)<.001){last=new(start,new HashSet<string>{id},false);return last;}
        var next=start.ToArray();bool blocked=false;
        if(edge==LayoutEdge.Move)
        {
            if(pushHorizontal is null){pushHorizontal=Math.Abs(dx)>=Math.Abs(dy);pushSign=Math.Sign(pushHorizontal.Value?dx:dy);}
            var target=screens.FirstOrDefault(s=>s.Contains(pointer));
            if(target.IsEmpty||target.Width==0)target=screen;
            double x=Math.Clamp(Snap(selected.X+dx,target.Left),target.Left,target.Right-selected.Width);
            double y=Math.Clamp(Snap(selected.Y+dy,target.Top),target.Top,target.Bottom-selected.Height);
            int index=Array.FindIndex(next,b=>b.Id==id);next[index]=selected with{X=x,Y=y};
            var queue=new Queue<int>();queue.Enqueue(index);int work=0;
            while(linkNeighbors&&queue.Count>0&&work++<start.Length*start.Length*4)
            {
                var active=next[queue.Dequeue()];
                for(int i=0;i<next.Length;i++)
                {
                    var other=next[i];if(!other.Visible||other.Id==active.Id)continue;
                    if(!Collision(active,other))continue;
                    if(other.Id==id){blocked=true;break;}
                    bool horizontal=pushHorizontal.Value;
                    double amount=pushSign>0?High(active,horizontal)+DesktopLayout.Gap-Low(other,horizontal):Low(active,horizontal)-DesktopLayout.Gap-High(other,horizontal);
                    next[i]=Translate(other,horizontal,amount);
                    if(!Screen(start[i]).Contains(next[i].Bounds)){blocked=true;break;}
                    queue.Enqueue(i);
                }
                if(blocked)break;
            }
            if(linkNeighbors&&queue.Count>0||!Valid(next))blocked=true;
            if(blocked)return last with{Blocked=true};
        }
        else
        {
            if((edge&(LayoutEdge.Left|LayoutEdge.Right))!=0)
            {
                bool low=edge.HasFlag(LayoutEdge.Left);double pos=low?selected.X:selected.X+selected.Width;
                double amount=Snap(pos+dx,screen.Left)-pos;
                (next,bool stop)=ResizeAxis(next,true,low,amount);blocked|=stop;
            }
            if((edge&(LayoutEdge.Top|LayoutEdge.Bottom))!=0)
            {
                bool low=edge.HasFlag(LayoutEdge.Top);double pos=low?selected.Y:selected.Y+selected.Height;
                double amount=Snap(pos+dy,screen.Top)-pos;
                (next,bool stop)=ResizeAxis(next,false,low,amount);blocked|=stop;
            }
        }
        var affected=next.Where((b,i)=>b!=start[i]).Select(b=>b.Id).Append(id).ToHashSet();
        last=new(next,affected,blocked);return last;
    }
    static bool Collision(DesktopBlock a,DesktopBlock b)=>
        a.X<b.X+b.Width+DesktopLayout.Gap-.001&&a.X+a.Width+DesktopLayout.Gap>b.X+.001&&
        a.Y<b.Y+b.Height+DesktopLayout.Gap-.001&&a.Y+a.Height+DesktopLayout.Gap>b.Y+.001;
    (DesktopBlock[],bool) ResizeAxis(DesktopBlock[] baseline,bool horizontal,bool low,double requested)
    {
        if(Math.Abs(requested)<.001)return(baseline,false);
        int selected=Array.FindIndex(baseline,b=>b.Id==id);var screen=Screen(baseline[selected]);
        // Per member: which edge moves, and how far the pointer must travel
        // before a newly encountered neighbour joins the separator.
        var members=new Dictionary<int,(bool Low,double Delay)>{{selected,(low,0)}};
        double edgePosition=low?Low(baseline[selected],horizontal):High(baseline[selected],horizontal);
        bool outward=low?requested<0:requested>0;
        bool changed;
        do
        {
            changed=false;
            if(!linkNeighbors)break;
            foreach(var entry in members.ToArray())
            {
                var a=baseline[entry.Key];double aEdge=entry.Value.Low?Low(a,horizontal):High(a,horizontal);
                for(int i=0;i<baseline.Length;i++)
                {
                    var b=baseline[i];if(!b.Visible||members.ContainsKey(i)||Screen(b)!=screen)continue;
                    double sameEdge=entry.Value.Low?Low(b,horizontal):High(b,horizontal);
                    double lateralGap=Math.Max(Low(a,!horizontal),Low(b,!horizontal))-Math.Min(High(a,!horizontal),High(b,!horizontal));
                    if(Math.Abs(aEdge-sameEdge)<=2&&lateralGap>=0&&lateralGap<=64)
                    {members[i]=entry.Value;changed=true;continue;}
                    if(!Across(a,b,horizontal))continue;
                    double gap=entry.Value.Low?Low(a,horizontal)-High(b,horizontal):Low(b,horizontal)-High(a,horizontal);
                    if(gap<DesktopLayout.Gap-.001)continue;
                    // Opposing partners must be on the selected separator,
                    // never on a partner's fixed outer edge.
                    double candidateEdge=entry.Value.Low?High(b,horizontal):Low(b,horizontal);
                    if(baseline.Where((_,j)=>j!=i&&j!=entry.Key).Any(c=>c.Visible&&Across(a,c,horizontal)&&Across(b,c,horizontal)&&Low(c,horizontal)>Math.Min(aEdge,candidateEdge)&&High(c,horizontal)<Math.Max(aEdge,candidateEdge)))continue;
                    if(gap<=64)
                    {members[i]=(!entry.Value.Low,entry.Value.Delay);changed=true;}
                    else if(entry.Key==selected&&outward&&gap-DesktopLayout.Gap<Math.Abs(requested))
                    {members[i]=(!low,Math.Sign(requested)*(gap-DesktopLayout.Gap));changed=true;}
                }
            }
        }while(changed);
        DesktopBlock[] At(double amount)
        {
            var result=baseline.ToArray();
            foreach(var (index,rule) in members)
            {
                double delta=rule.Delay==0?amount:Math.Sign(amount)*Math.Max(0,Math.Abs(amount)-Math.Abs(rule.Delay));
                result[index]=SetEdge(baseline[index],horizontal,rule.Low,delta);
            }
            return result;
        }
        bool Fits(DesktopBlock[] result)
        {
            foreach(int i in members.Keys)
            {
                var b=result[i];var min=DesktopLayout.Minimum(b.Id);
                // Existing smaller legacy dimensions may remain unchanged.
                if(b.Width<Math.Min(min.Width,baseline[i].Width)||b.Height<Math.Min(min.Height,baseline[i].Height)||!screen.Contains(b.Bounds))return false;
            }
            return Valid(result);
        }
        var full=At(requested);if(Fits(full))return(full,false);
        double valid=0,invalid=1;
        for(int i=0;i<32;i++){double t=(valid+invalid)/2;if(Fits(At(requested*t)))valid=t;else invalid=t;}
        double limited=requested*valid;
        // Numerical bisection approaches the valid side of an exact boundary.
        return(At(limited),true);
    }
}
