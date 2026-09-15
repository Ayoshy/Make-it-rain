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
    BudsBattery? budsBattery;
    bool readingBuds;
    Task<BudsBattery?>? budsReadTask;
    long nextBudsRead,budsGeneration;

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
            stateCurrent=true;scanError="";RefreshState();PollBuds();
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

    async void PollBuds()
    {
        if(disposed||readingBuds||changing||rows[1]?.Connected!=true||Environment.TickCount64<nextBudsRead)return;
        var target=rows[1]!;long generation=budsGeneration;
        readingBuds=true;nextBudsRead=Environment.TickCount64+30000;
        try
        {
            budsReadTask=Task.Run(()=>BudsBattery.Read(target.Address));
            var battery=await budsReadTask;
            if(!disposed&&generation==budsGeneration&&rows[1]?.Address==target.Address&&rows[1]?.Connected==true)
            {budsBattery=battery;RefreshState();}
        }
        catch(Exception)
        {
            if(!disposed&&generation==budsGeneration){budsBattery=null;RefreshState();}
        }
        finally{readingBuds=false;budsReadTask=null;}
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
            bool detailed=i==1&&row?.Connected==true;
            DrawDevice(i,x,y-(hover?2:0)-(detailed?rect.Height*.12:0),size*(detailed?.82:1),MaterialAmount(i));D.Pop();
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
            else if(detailed)DrawBudsBattery(rect);
            else if(row.CurrentBatteryPercent is int battery)
                Text($"{battery} %",x,y+rect.Height*.31,Math.Clamp(rect.Width*.085,6,9),
                    battery<=20?"#F4BD8C":"#DBE9F5",align:"center",bold:true);
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

    void DrawBudsBattery(Rect rect)
    {
        double x=rect.X+rect.Width/2,y=rect.Y+rect.Height/2;
        double font=Math.Clamp(rect.Width*.075,5.5,8.5);
        void Level(int? value,double cx,double top)=>Text(value is int percent?$"{percent} %":"—",cx,top,font,
            value is <=20?"#F4BD8C":value is null?"#A89BB5":"#DBE9F5",align:"center",bold:true);
        Level(budsBattery?.Left,x-rect.Width*.23,y+rect.Height*.16);
        Level(budsBattery?.Right,x+rect.Width*.23,y+rect.Height*.16);
        double icon=Math.Clamp(rect.Width*.3,14,32),caseY=y+rect.Height*.42;
        string path=Path.Combine(Station.Root,"dock/icons/bluetooth",budsBattery?.Case is null?"pearl":"connected","buds-case.png");
        Image(path,x-rect.Width*.16-icon/2,caseY-icon/2+font*.8,icon,icon);
        Level(budsBattery?.Case,x+rect.Width*.13,caseY);
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
    void RefreshState()
    {
        if(!stateCurrent||rows[1]?.Connected!=true||changing&&changingTarget==1)
        {budsBattery=null;nextBudsRead=0;budsGeneration++;}
        SyncMaterials();UpdateToolTip();Refresh();
    }
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
        string battery=row?.CurrentBatteryPercent is int percent?$" · Batterie {percent} %":row?.Connected==true?" · Batterie non communiquée":"";
        if(index==1&&row?.Connected==true)
        {
            string Level(int? value)=>value is int level?$"{level} %":"non communiqué";
            battery=$"\nGauche : {Level(budsBattery?.Left)} · Droite : {Level(budsBattery?.Right)}\nBoîtier : {Level(budsBattery?.Case)}";
            if(budsBattery is null&&row.CurrentBatteryPercent is int windows)battery+=$"\nNiveau global Windows : {windows} %";
        }
        ToolTip=$"{Favorites[index]} · {link}{battery}\n{action}";
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
            // Let the bounded passive read release its RFCOMM socket before a
            // requested disconnect, so an in-flight connect cannot undo the click.
            if(index==1&&budsReadTask is {} pending){try{await pending;}catch{}}
            if(disposed)return;
            var result=await Task.Run(()=>devices.SetConnected(row,changingDesired,actionCancellation.Token));
            if(disposed)return;
            error=ResultError(result,row.Kind==BluetoothKind.Controller);
            if(result.ConfirmedConnected is bool actual&&rows[index] is BluetoothDevice current&&string.Equals(current.Id,row.Id,StringComparison.OrdinalIgnoreCase))
                rows=rows.Select((item,itemIndex)=>itemIndex==index?current with{Connected=actual,BatteryPercent=null}:item).ToArray();
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
    internal object Inspect()=>new{devices=rows.Select(row=>new{row?.Name,row?.Connected,row?.CurrentBatteryPercent}),budsBattery,readingBuds};
    public void Dispose(){disposed=true;actionCancellation?.Cancel();busyTimer.Stop();}
}
