namespace Battlestation;

// The slice of the Montagne scene, shared with Shaders/Montagne.fx: the
// horizontal extent follows the dock aspect, the vertical range is fixed, so a
// resize widens the view instead of stretching the massif. The fallback drawing
// uses the very same ridge, so both paths show one mountain.
internal static class MontagneCamera
{
    internal const double YTop=1.0,YBottom=-.70,WidthPerAspect=1.05;

    internal static double Aspect(double width,double height)=>Math.Max(.25,width/Math.Max(1,height));
    internal static double WorldX(double aspect,double u)=>(-aspect*WidthPerAspect)+(2*aspect*WidthPerAspect)*u;
    internal static double WorldY(double v)=>YTop-(YTop-YBottom)*v;

    // One asymmetric summit, exactly as in the shader.
    static double Peak(double x,double center,double widthLeft,double widthRight,double height,double powerLeft,double powerRight)
    {
        double d=x-center;
        double w=d<0?widthLeft:widthRight;
        double p=d<0?powerLeft:powerRight;
        return height*Math.Pow(Math.Clamp(1-Math.Abs(d)/w,0,1),p);
    }

    internal static double Ridge(double x)
    {
        double rough=.030*Math.Sin(x*3.1+1.2)
                    +.016*Math.Sin(x*6.7+.5)
                    +.008*Math.Sin(x*13.3+2.2)
                    +.004*Math.Sin(x*27.7+.9);
        return -.52+.06*Math.Sin(x*1.7+.6)
            +Peak(x,.14,.86,1.12,.94,1.30,1.45)
            +Peak(x,1.42,.50,.40,.30,1.40,1.40)
            +Peak(x,.86,.28,.26,.16,1.50,1.30)
            +Peak(x,-1.38,.68,.56,.50,1.45,1.30)
            +rough;
    }
}
