using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Battlestation;

internal sealed class BluetoothSurface : Surface,IDisposable
{
    readonly BluetoothDevices devices=new();
    IReadOnlyList<BluetoothDevice?> rows=[null,null,null,null];
    static readonly string[] Favorites=["PARTYBTMS3","Buds3 Pro de Ayo","DualSense Wireless Controller","[Samsung] Soundbar J-Series"];
    static readonly string[] Artwork=["boombox","buds","dualsense","soundbar"];
    readonly double[] materialFrom=new double[4],materialTarget=new double[4];
    readonly long[] materialSince=new long[4];
    int errorTarget=-1;
    long nextScan,scanGeneration;
    bool scanning,changing,changingDesired,waitingForController,disposed,stateCurrent=true;
    int changingTarget=-1;
    CancellationTokenSource? actionCancellation;
    readonly DispatcherTimer busyTimer;
    string error="",scanError="";

    public BluetoothSurface(Station station):base(station,12)
    {
        Width=560;Height=280;
        busyTimer=new DispatcherTimer(TimeSpan.FromMilliseconds(16),DispatcherPriority.Render,(_,_)=>Animate(),Dispatcher);
        busyTimer.Stop();SizeChanged+=(_,_)=>RefreshState();
    }

    internal async void Poll(bool force=false)
    {
        if(disposed||scanning||changing||!force&&Environment.TickCount64<nextScan)return;
        nextScan=Environment.TickCount64+5000;scanning=true;long generation=++scanGeneration;
        if(!stateCurrent)RefreshState();else Refresh();
        try
        {
            var found=await Task.Run(devices.Scan);
            if(disposed||changing||generation!=scanGeneration)return;
            rows=Favorites.Select(want=>found.FirstOrDefault(row=>string.Equals(row.Name,want,StringComparison.OrdinalIgnoreCase))).ToArray();
            if(waitingForController&&errorTarget>=0&&rows[errorTarget]?.Connected==true){waitingForController=false;error="";}
            stateCurrent=true;scanError="";RefreshState();
        }
        catch(BluetoothScanException e)
        {
            if(!disposed&&generation==scanGeneration){rows=[null,null,null,null];stateCurrent=false;scanError=ScanError(e.NativeError);RefreshState();}
        }
        catch(Exception e)
        {
            if(!disposed&&generation==scanGeneration){rows=[null,null,null,null];stateCurrent=false;scanError=$"Bluetooth indisponible ({e.GetType().Name})";RefreshState();}
        }
        finally
        {
            if(generation==scanGeneration){scanning=false;if(!disposed)UpdateToolTip();}
        }
    }

    protected override void Paint()
    {
        SyncMaterials();
        var slots=Slots();
        for(int i=0;i<slots.Length;i++)
        {
            var rect=slots[i];var row=rows[i];bool hover=rect.Contains(Pointer),busy=changing&&changingTarget==i;
            double x=rect.X+rect.Width/2,y=rect.Y+rect.Height/2;
            if(hover)D.DrawEllipse(B("#16DACDEC"),null,new Point(x,y+rect.Height*.3),rect.Width*.37,rect.Height*.065);
            double size=rect.Width*1.38*(hover?1.07:1);
            D.PushOpacity(busy?.72:row?.Connected is null?.6:1);
            DrawDevice(i,x,y-(hover?2:0),size,MaterialAmount(i));D.Pop();
            if(busy)
            {
                for(int dot=0;dot<3;dot++)
                {
                    D.PushOpacity(.35+.65*(.5+.5*Math.Sin(Environment.TickCount64/150d-dot*.8)));
                    D.DrawEllipse(B("#A9F8FF"),null,new Point(x+(dot-1)*7,y+rect.Height*.43),2,2);D.Pop();
                }
            }
            else if(row?.Connected is null)
                D.DrawEllipse(null,new Pen(B("#D7BB88"),1),new Point(x,y+rect.Height*.43),3,3);
            int index=i;
            if(!changing&&!scanning&&stateCurrent)Hit("BluetoothToggle:"+i,rect.X,rect.Y,rect.Width,rect.Height,()=>Toggle(index));
        }
        string message=scanError!=""?scanError:error;
        if(message!="")
        {
            string label=Width>=240?message:scanError!=""?"Indisponible":message.Contains("PS")?"Appuyez sur PS":"Échec";
            Text(label,Width/2,Height-18,8,"#E8B5C8",align:"center",width:Width-24);
        }
    }

    Rect[] Slots()=>BluetoothIconLayout.Arrange(Width,Height);
    double MaterialAmount(int index)
    {
        double progress=Math.Clamp((Environment.TickCount64-materialSince[index])/260d,0,1);
        double smooth=progress*progress*(3-2*progress);
        return materialFrom[index]+(materialTarget[index]-materialFrom[index])*smooth;
    }
    void SyncMaterials()
    {
        if(disposed)return;
        for(int i=0;i<rows.Count;i++)
        {
            if(rows[i]?.Connected is not bool connected)continue;
            double target=connected?1:0;
            if(materialTarget[i]==target)continue;
            materialFrom[i]=MaterialAmount(i);materialTarget[i]=target;materialSince[i]=Environment.TickCount64;busyTimer.Start();
        }
    }
    void Animate()
    {
        Refresh();
        if(!changing&&materialSince.All(since=>Environment.TickCount64-since>=260))busyTimer.Stop();
    }
    void RefreshState(){SyncMaterials();UpdateToolTip();Refresh();}
    protected override void OnPointer(MouseEventArgs e)=>UpdateToolTip();
    void UpdateToolTip()
    {
        int index=Array.FindIndex(Slots(),r=>r.Contains(Pointer));
        if(index<0){ToolTip=null;return;}
        if(changing&&changingTarget==index)
        {
            string pending=changingDesired?"Connexion en cours":"Déconnexion en cours";
            ToolTip=$"{Favorites[index]} · {pending}";return;
        }
        if(!stateCurrent){ToolTip=$"{Favorites[index]} · État Bluetooth indisponible";return;}

        var row=rows[index];
        string link=row?.Connected switch {true=>"Connecté",false=>"Déconnecté",_=>"Connexion inconnue"};
        string action=row?.Connected switch {true=>"Cliquer pour déconnecter",false=>"Cliquer pour connecter",_=>"Ouvrir les réglages Bluetooth"};
        if(row?.Kind==BluetoothKind.Controller&&row.Connected==false)action="Appuyez sur PS pour connecter";
        ToolTip=$"{Favorites[index]} · {link}\n{action}";
        if(index==errorTarget&&error!="")ToolTip+="\n"+error;
    }

    async void Toggle(int index)
    {
        if(changing||scanning)return;
        if(!stateCurrent){error="État Bluetooth à actualiser";RefreshState();Poll(true);return;}
        var row=rows[index];errorTarget=index;waitingForController=false;
        if(row?.Connected is null){try{Manage();}catch{error="Réglages Bluetooth indisponibles";RefreshState();}return;}
        if(row.Kind==BluetoothKind.Controller&&row.Connected==false){waitingForController=true;error="Appuyez sur PS pour connecter la manette";RefreshState();return;}
        changing=true;changingTarget=index;changingDesired=!row.Connected.Value;scanGeneration++;error="";actionCancellation=new CancellationTokenSource();busyTimer.Start();RefreshState();
        try
        {
            var result=await Task.Run(()=>devices.SetConnected(row,changingDesired,actionCancellation.Token));
            if(disposed)return;
            error=ResultError(result,row.Kind==BluetoothKind.Controller);
            if(result.ConfirmedConnected is bool actual&&rows[index] is BluetoothDevice current&&string.Equals(current.Id,row.Id,StringComparison.OrdinalIgnoreCase))
                rows=rows.Select((item,itemIndex)=>itemIndex==index?current with{Connected=actual}:item).ToArray();
        }
        catch(Exception e){if(!disposed)error=$"Commande Bluetooth indisponible ({e.GetType().Name})";}
        finally
        {
            changing=false;changingTarget=-1;busyTimer.Stop();actionCancellation?.Dispose();actionCancellation=null;nextScan=0;
            if(!disposed){Poll(true);RefreshState();}
        }
    }

    static string ScanError(int nativeError)=>nativeError==0?"Bluetooth indisponible":$"Bluetooth indisponible (0x{nativeError:X})";
    static string ResultError(BluetoothSetConnectedResult result,bool controller)=>result.Status switch
    {
        BluetoothMutationStatus.Confirmed=>"",
        BluetoothMutationStatus.Cancelled=>"Commande Bluetooth annulée",
        BluetoothMutationStatus.TargetMissing=>"Périphérique Bluetooth absent",
        BluetoothMutationStatus.InvalidTarget=>"Périphérique Bluetooth non valide",
        BluetoothMutationStatus.UnsupportedState when controller&&result.RequestedConnected=>"Appuyez sur PS pour connecter la manette",
        BluetoothMutationStatus.UnsupportedState=>"État Bluetooth non pris en charge",
        BluetoothMutationStatus.Unconfirmed=>"État Bluetooth non confirmé",
        _=>$"Commande Bluetooth refusée (0x{result.NativeError:X})"
    };

    void DrawDevice(int index,double x,double y,double size,double connected)
    {
        string root=Path.Combine(Station.Root,"dock/icons/bluetooth");
        Image(Path.Combine(root,"pearl",Artwork[index]+".png"),x-size/2,y-size/2,size,size);
        if(connected>0)Image(Path.Combine(root,"connected",Artwork[index]+".png"),x-size/2,y-size/2,size,size,connected);
    }
    static void Manage()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:bluetooth"){UseShellExecute=true});
    public void Dispose(){disposed=true;actionCancellation?.Cancel();busyTimer.Stop();}
}
