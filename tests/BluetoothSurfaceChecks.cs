using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Battlestation;

static class BluetoothSurfaceChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static void Run(BluetoothSurface surface,bool readOnlyScan)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(BluetoothSurface);
        var refresh=type.GetMethod("RefreshState",flags)!;
        var current=type.GetField("stateCurrent",flags)!;
        var rowsField=type.GetField("rows",flags)!;
        var rows=((IReadOnlyList<BluetoothDevice?>)rowsField.GetValue(surface)!).ToArray();
        var slots=(Rect[])type.GetMethod("Slots",flags)!.Invoke(surface,null)!;
        typeof(Surface).GetField("Pointer",flags)!.SetValue(surface,new Point(slots[1].X+slots[1].Width/2,slots[1].Y+slots[1].Height/2));
        refresh.Invoke(surface,null);
        Check(surface.ToolTip?.ToString()?.Contains("Gauche : 74 % · Droite : 71 %")==true&&surface.ToolTip?.ToString()?.Contains("Boîtier : 25 %")==true,"Separate Buds batteries appear in tooltip");
        rows[1]=rows[1]! with{Connected=false};rowsField.SetValue(surface,rows);refresh.Invoke(surface,null);
        Check(type.GetField("budsBattery",flags)!.GetValue(surface) is null,"Disconnect clears detailed Buds battery");
        rows[1]=rows[1]! with{Connected=true};rowsField.SetValue(surface,rows);refresh.Invoke(surface,null);
        Check(surface.ToolTip?.ToString()?.Contains("Gauche : non communiqué")==true,"Reconnect never reuses old per-ear battery");
        typeof(Surface).GetField("Pointer",flags)!.SetValue(surface,new Point(slots[2].X+slots[2].Width/2,slots[2].Y+slots[2].Height/2));
        refresh.Invoke(surface,null);
        Check(surface.ToolTip?.ToString()?.Contains("connecter")==true,"Disconnected device announces connect");
        rows[2]=rows[2]! with{Connected=true,BatteryPercent=97};rowsField.SetValue(surface,rows);
        refresh.Invoke(surface,null);
        Check(surface.ToolTip?.ToString()?.Contains("déconnecter")==true,"Tooltip updates after state changes without moving the pointer");
        Check(surface.ToolTip?.ToString()?.Contains("Batterie 97 %")==true,"Battery updates in the stationary tooltip");
        var material=type.GetMethod("MaterialAmount",flags)!;
        Thread.Sleep(90);
        double fadingIn=(double)material.Invoke(surface,[2])!;
        Check(fadingIn>0&&fadingIn<1,"Confirmed connection fades toward Vice City material");
        Thread.Sleep(200);
        Check((double)material.Invoke(surface,[2])! == 1,"Connected material reaches full neon");
        rows[2]=rows[2]! with{Connected=false};rowsField.SetValue(surface,rows);refresh.Invoke(surface,null);
        Check(surface.ToolTip?.ToString()?.Contains("97 %")==false,"Disconnection hides cached battery in tooltip");
        Thread.Sleep(90);
        double fadingOut=(double)material.Invoke(surface,[2])!;
        Check(fadingOut>0&&fadingOut<1,"Confirmed disconnection fades back to pearl");
        Thread.Sleep(200);
        Check((double)material.Invoke(surface,[2])! == 0,"Disconnected material returns fully to pearl");
        current.SetValue(surface,false);refresh.Invoke(surface,null);
        Check(surface.ToolTip?.ToString()?.Contains("indisponible")==true,"Failed idle scan is unavailable, not still running");
        current.SetValue(surface,true);
        if(!readOnlyScan)return;

        const string cancellation="Demande Windows annulée";
        var error=type.GetField("error",flags)!;error.SetValue(surface,cancellation);
        var scanning=type.GetField("scanning",flags)!;
        var previousContext=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(surface.Dispatcher));
        surface.Poll(true);
        var frame=new DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(10);
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(_,_)=>{if(!(bool)scanning.GetValue(surface)!||DateTime.UtcNow>=deadline)frame.Continue=false;};
        timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();SynchronizationContext.SetSynchronizationContext(previousContext);}
        Check(!(bool)scanning.GetValue(surface)!&&(bool)current.GetValue(surface)!,"Real read-only Bluetooth scan completes");
        Check((string)error.GetValue(surface)! == cancellation,"Successful readback must not erase a cancelled action");
        var actual=((IReadOnlyList<BluetoothDevice?>)rowsField.GetValue(surface)!)[2];
        Check(actual?.Connected==true&&surface.ToolTip?.ToString()?.Contains("déconnecter")==true,"Live Windows state updates the stationary tooltip");
        Console.WriteLine("PASS: Bluetooth stationary tooltip, unavailable state, and action-error persistence after real read-only scan. No physical clicks.");
    }
}
