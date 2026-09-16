using System.Runtime.InteropServices;
namespace Battlestation;
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DualSenseState(int Connected,int Transport,int Battery,int Power,int LX,int LY,int RX,int RY,int LT,int RT,uint Buttons,uint Device,int EnhancedReports,int RawLT=-32768,int RawRT=-32768,int TouchAvailable=0,int Touch1=0,int Touch2=0,float Touch1X=0,float Touch1Y=0,float Touch2X=0,float Touch2Y=0)
{
    internal bool Down(int button)=>(Buttons&(1u<<button))!=0;
    internal int? BatteryPercent=>Connected==1&&Battery is >=0 and <=100?Battery:null;
    internal static double Axis(int value)=>Math.Clamp(value/32768d,-1,1);
    internal static double Offset(int x,int y)=>Math.Sqrt((double)x*x+(double)y*y)/32768*100;
}
internal sealed record DualSenseSnapshot(DualSenseState State,string Status);
internal sealed class DualSenseReader : IDisposable
{
    const string Library="Battlestation.Controller.dll";
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int PadInitialize(int touch);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void PadRead(out DualSenseState state);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void PadShutdown();
    readonly AutoResetEvent wake=new(false);
    Thread? worker;
    volatile bool active,stopping,touchEnabled;
    DualSenseSnapshot snapshot=new(default,"En veille");
    internal DualSenseSnapshot Snapshot=>Volatile.Read(ref snapshot);
    internal bool TouchEnabled=>touchEnabled;
    internal void SetTouchEnabled(bool value){if(touchEnabled==value)return;touchEnabled=value;wake.Set();}
    internal bool Active=>active;
    internal bool Running=>worker?.IsAlive==true;
    void Publish(DualSenseState state,string status){if(snapshot.State!=state||snapshot.Status!=status)Volatile.Write(ref snapshot,new(state,status));}
    internal void SetActive(bool value)
    {
        if(stopping||active==value)return;active=value;
        if(value&&worker is null){worker=new Thread(Work){IsBackground=true,Name="Battlestation DualSense"};worker.Start();}
        wake.Set();
    }
    void Work()
    {
        bool opened=false,openedTouch=false;
        try
        {
            while(!stopping)
            {
                if(!active)
                {
                    if(opened){PadShutdown();opened=false;}
                    Publish(default,"En veille");wake.WaitOne();continue;
                }
                if(opened&&openedTouch!=touchEnabled){PadShutdown();opened=false;}
                if(!opened)
                {
                    Publish(default,"Connexion…");
                    openedTouch=touchEnabled;
                    if(PadInitialize(openedTouch?1:0)==0){Publish(default,"Lecture indisponible");wake.WaitOne(1000);continue;}
                    opened=true;
                }
                PadRead(out var state);
                Publish(state,state.Connected==0?"Déconnectée":state.Transport==1?"USB":state.Transport==2?"Bluetooth":"Connexion inconnue");
                wake.WaitOne(8);
            }
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){Publish(default,"SDL3 indisponible");}
        finally{if(opened)PadShutdown();}
    }
    public void Dispose(){stopping=true;active=false;wake.Set();if(worker?.IsAlive==true)worker.Join();wake.Dispose();}
}
