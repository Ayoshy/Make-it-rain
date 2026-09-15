using Battlestation;

static class BluetoothTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}

    static void Main()
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
        var invalidTarget=new BluetoothSetConnectedResult(BluetoothMutationStatus.InvalidTarget,true,null,0,0);
        Check(invalidTarget.ConfirmedConnected is null,"Invalid target never claims a state");
        var unsupported=new BluetoothSetConnectedResult(BluetoothMutationStatus.UnsupportedState,true,null,0,0);
        Check(unsupported.ConfirmedConnected is null,"Unsupported controller reconnect never claims a state");
        var unconfirmed=new BluetoothSetConnectedResult(BluetoothMutationStatus.Unconfirmed,false,null,0,2);
        Check(unconfirmed.Status!=BluetoothMutationStatus.Confirmed&&unconfirmed.ConfirmedConnected is null,"Unconfirmed state remains distinct from disconnected");
        Console.WriteLine("PASS: Bluetooth target validation, physical identity, and honest mutation results.");
    }
}
