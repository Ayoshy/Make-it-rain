using System.Windows;

namespace Battlestation;

internal static class BluetoothIconLayout
{
    internal static Rect[] Arrange(double width,double height)
    {
        double availableWidth=Math.Max(1,width-32),availableHeight=Math.Max(1,height-32);
        int columns=new[]{1,2,4}.MaxBy(count=>Math.Min(availableWidth/count,availableHeight/(4/count)));
        int rows=4/columns;
        double pitchX=availableWidth/columns,pitchY=availableHeight/rows;
        double size=Math.Max(1,Math.Min(112,Math.Min(pitchX,pitchY)-8));
        return Enumerable.Range(0,4).Select(index=>new Rect(
            16+(index%columns+.5)*pitchX-size/2,
            16+(index/columns+.5)*pitchY-size/2,size,size)).ToArray();
    }
}
