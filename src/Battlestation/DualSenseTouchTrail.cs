using System.Windows;

namespace Battlestation;
internal readonly record struct DualSenseTouchPoint(Point Position,double At,int Stroke);
internal sealed class DualSenseTouchTrail
{
    internal const double Lifetime=.85;
    readonly List<DualSenseTouchPoint>[] points=[[],[]];
    readonly bool[] down=new bool[2];
    readonly int[] stroke=new int[2];
    internal IReadOnlyList<DualSenseTouchPoint> Points(int finger)=>points[finger];
    internal bool Down(int finger)=>down[finger];
    internal void Clear(){foreach(var list in points)list.Clear();Array.Clear(down);}
    internal void Update(DualSenseState state,double seconds,bool enabled)
    {
        if(!enabled||state.Connected==0||state.TouchAvailable==0){Clear();return;}
        for(int finger=0;finger<2;finger++)
        {
            var list=points[finger];list.RemoveAll(p=>seconds-p.At>Lifetime);
            bool contact=(finger==0?state.Touch1:state.Touch2)==1;
            float x=finger==0?state.Touch1X:state.Touch2X,y=finger==0?state.Touch1Y:state.Touch2Y;
            if(contact&&float.IsFinite(x+y)){
                if(!down[finger])stroke[finger]++;
                var position=new Point(Math.Clamp(x,0,1),Math.Clamp(y,0,1));
                if(list.Count==0||list[^1].Position!=position||seconds-list[^1].At>.04)
                    list.Add(new(position,seconds,stroke[finger]));
                if(list.Count>64)list.RemoveAt(0);
            }
            down[finger]=contact;
        }
    }
}
