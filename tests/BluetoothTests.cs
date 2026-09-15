using Battlestation;

static class BluetoothTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}

    static void Main(string[] args)
    {
        const string valid="BTHENUM\\DEV_D42F4B110CFB\\7&B44BB26&0&BLUETOOTHDEVICE_D42F4B110CFB";
        Check(BluetoothDevices.IsEligibleRemoteId(valid),"A complete remote BTHENUM identity is accepted");
        foreach(var invalid in new[]{
            "USB\\VID_0BDA&PID_A729\\00E04C239987",
            "BTH\\MS_BTHBRB\\6&1D3B1BBE&0&1",
            "BTHENUM\\DEV_D42F4B110CFB",
            "BTHENUM\\DEV_D42F4B110CFB\\",
            "BTHENUM\\DEV_D42F4B110CF\\7&B44BB26",
            "BTHENUM\\DEV_D42F4B110CFG\\7&B44BB26",
            "BTHENUM\\DEV_D42F4B110CFB\\7&B44BB26\\service",
            "BTHENUM\\{00001101-0000-1000-8000-00805F9B34FB}",
            "BTHENUM\\DEV_D42F4B110CFB\\bad\"id",
            "BTHENUM\\DEV_000000000000\\service"})
            Check(!BluetoothDevices.IsEligibleRemoteId(invalid),$"Malformed target rejected: {invalid}");

        var container=Guid.Parse("9ee7477c-1422-555d-a4cd-19059a869398");
        var device=new BluetoothDevice(valid,"Buds3 Pro de Ayo",false,BluetoothKind.Headphones,container,0xD42F4B110CFB);
        Check(device.ContainerId==container&&device.Address==0xD42F4B110CFB,"A scan row retains exact physical identity");
        Check(BluetoothDevices.DecodeBattery(3,1,0)==0&&BluetoothDevices.DecodeBattery(3,1,100)==100,"Empty and full batteries are valid percentages");
        Check(BluetoothDevices.DecodeBattery(3,1,101) is null&&BluetoothDevices.DecodeBattery(3,1,255) is null,"Unknown battery sentinels never become percentages");
        Check(BluetoothDevices.DecodeBattery(7,1,50) is null&&BluetoothDevices.DecodeBattery(3,0,50) is null,"Unexpected native property types and sizes are rejected");
        Check((device with{Connected=true,BatteryPercent=97}).CurrentBatteryPercent==97,"Connected battery is visible");
        Check((device with{BatteryPercent=97}).CurrentBatteryPercent is null&&(device with{Connected=null,BatteryPercent=97}).CurrentBatteryPercent is null,"Cached battery is hidden when disconnected or unknown");
        Check(BudsBattery.Checksum("123456789"u8)==0x31C3,"Samsung CRC uses the CRC16 XMODEM reference value");
        byte[] Packet(byte id,byte[] payload)
        {
            int size=payload.Length+3;var bytes=new byte[size+4];
            bytes[0]=0xFD;bytes[1]=(byte)size;bytes[2]=(byte)(size>>8);bytes[3]=id;payload.CopyTo(bytes,4);
            ushort crc=BudsBattery.Checksum(bytes.AsSpan(3,size-2));bytes[^3]=(byte)crc;bytes[^2]=(byte)(crc>>8);bytes[^1]=0xDD;return bytes;
        }
        var extended=Packet(0x61,[4,8,74,71,1,1,0x33,25]);
        Check(BudsBattery.Decode(extended)==new BudsBattery(74,71,25),"Extended status separates left, right and case");
        var status=Packet(0x60,[4,0,100,1,1,0x33,101]);
        Check(BudsBattery.Decode(status)==new BudsBattery(0,100,null),"Basic status preserves zero and hides unavailable case sentinel");
        Check(BudsBattery.Decode([1,2,..extended,..status])==new BudsBattery(0,100,null),"Coalesced notifications use the last valid status");
        Check(BudsBattery.Decode(extended.AsSpan(0,extended.Length-1)) is null,"Truncated frame rejected");
        extended[6]^=1;Check(BudsBattery.Decode(extended) is null,"Damaged battery packet rejected by checksum");extended[6]^=1;
        extended[2]|=0x20;Check(BudsBattery.Decode(extended) is null,"Fragmented payload is not mistaken for a battery update");
        Check(BudsBattery.Decode(Packet(0x62,[4,8,74,71,1,1,0x33,25])) is null,"Other notifications cannot update battery");
        int nativeIndex=Array.IndexOf(args,"--buds-read");
        if(nativeIndex>=0)
        {
            var library=System.Runtime.InteropServices.NativeLibrary.Load(Path.GetFullPath(args[nativeIndex+1]));
            System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(BudsBattery).Assembly,(name,_,_)=>name=="Battlestation.Desk.dll"?library:0);
            var buds=new BluetoothDevices().Scan().Single(row=>row.Name=="Buds3 Pro de Ayo"&&row.Connected==true);
            var measured=BudsBattery.Read(buds.Address);
            Check(measured is not null,"Live Buds3 Pro notification passes frame and checksum validation");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(measured));
        }
        if(args.Contains("--read-only"))
            foreach(var row in new BluetoothDevices().Scan())
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{row.Name,row.Connected,row.CurrentBatteryPercent}));
        var invalidTarget=new BluetoothSetConnectedResult(BluetoothMutationStatus.InvalidTarget,true,null,0,0);
        Check(invalidTarget.ConfirmedConnected is null,"Invalid target never claims a state");
        var unsupported=new BluetoothSetConnectedResult(BluetoothMutationStatus.UnsupportedState,true,null,0,0);
        Check(unsupported.ConfirmedConnected is null,"Unsupported controller reconnect never claims a state");
        var unconfirmed=new BluetoothSetConnectedResult(BluetoothMutationStatus.Unconfirmed,false,null,0,2);
        Check(unconfirmed.Status!=BluetoothMutationStatus.Confirmed&&unconfirmed.ConfirmedConnected is null,"Unconfirmed state remains distinct from disconnected");
        Console.WriteLine("PASS: Bluetooth target validation, physical identity, and honest mutation results.");
    }
}
