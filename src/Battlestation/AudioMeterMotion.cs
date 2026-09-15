namespace Battlestation;

internal static class AudioMeterMotion
{
    // Time-based attack/release: same motion at 60, 120 or 144 Hz.
    internal static float Step(float current,float target,double seconds)
    {
        if(!float.IsFinite(current))current=0;
        target=float.IsFinite(target)?Math.Clamp(target,0,1):0;
        if(!double.IsFinite(seconds)||seconds<=0)return current;
        float next=(float)(current+(target-current)*(1-Math.Exp(-seconds/(target>current?.045:.18))));
        return Math.Abs(next-target)<.0001f?target:next;
    }
}
