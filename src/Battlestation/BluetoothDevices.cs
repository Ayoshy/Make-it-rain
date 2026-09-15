using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Battlestation;

internal enum BluetoothKind { Headphones,Speaker,Controller,Other }

internal sealed record BluetoothDevice(
    string Id,
    string Name,
    bool? Connected,
    BluetoothKind Kind,
    Guid? ContainerId,
    ulong Address,
    int? BatteryPercent=null)
{
    internal int? CurrentBatteryPercent=>Connected==true&&BatteryPercent is >=0 and <=100?BatteryPercent:null;
}

internal enum BluetoothMutationStatus { Confirmed,NativeError,Cancelled,TargetMissing,InvalidTarget,UnsupportedState,Unconfirmed }

internal sealed record BluetoothSetConnectedResult(
    BluetoothMutationStatus Status,
    bool RequestedConnected,
    bool? ConfirmedConnected,
    int NativeError,
    int EndpointCount);

internal sealed class BluetoothScanException : Exception
{
    internal int NativeError {get;}
    internal BluetoothScanException(string message,int nativeError):base(message)=>NativeError=nativeError;
}

internal sealed class BluetoothDevices
{
    internal const int ErrorCancelled=1223;
    internal const int ErrorNotFound=1168;
    const int ErrorNoMoreItems=259,ErrorFileNotFound=2;
    const uint Present=2,AllClasses=4,FriendlyName=12,Description=0,DevPropTypeGuid=0x0000000D;
    static readonly Guid BluetoothClass=new("e0cbf06c-cd8b-4647-bb8a-263b43f0f974");
    static readonly DevPropKey ContainerKey=new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),2);
    // Windows Bluetooth battery property, also exposed on Hands-Free service nodes.
    static readonly DevPropKey BatteryKey=new(new Guid("104ea319-6ee2-4701-bd47-8ddbf425bbe5"),2);
    static readonly TimeSpan ConfirmationTimeout=TimeSpan.FromSeconds(15);

    sealed class Candidate
    {
        internal string Id;
        internal string Name;
        internal Guid? ContainerId;
        internal Candidate(string id,string name,Guid? containerId){Id=id;Name=name;ContainerId=containerId;}
    }

    public IReadOnlyList<BluetoothDevice> Scan()
    {
        var connections=ReadConnections();
        var grouped=new Dictionary<ulong,Candidate>();
        var batteries=new Dictionary<Guid,int>();
        var bluetoothClass=BluetoothClass;
        var set=SetupDiGetClassDevs(ref bluetoothClass,"BTHENUM",0,Present|AllClasses);
        if(set==Invalid)throw new BluetoothScanException("Bluetooth enumeration unavailable",Marshal.GetLastWin32Error());
        try
        {
            for(uint index=0;;index++)
            {
                var info=new DeviceInfo{cbSize=Marshal.SizeOf<DeviceInfo>()};
                if(!SetupDiEnumDeviceInfo(set,index,ref info))
                {
                    int error=Marshal.GetLastWin32Error();
                    if(error==ErrorNoMoreItems)break;
                    throw new BluetoothScanException("Bluetooth enumeration failed",error);
                }
                string? id=InstanceId(info.DevInst,out int identityError);
                if(id is null)throw new BluetoothScanException("Bluetooth device identity unavailable",identityError);
                if(id.StartsWith("BTHENUM\\DEV_",StringComparison.OrdinalIgnoreCase)&&!TryGetRemoteAddress(id,out _))
                    throw new BluetoothScanException("Malformed Bluetooth device identity",13);
                var container=ContainerId(set,ref info);
                if(container is Guid batteryContainer&&BatteryPercent(set,ref info) is int percent)
                    batteries[batteryContainer]=batteries.TryGetValue(batteryContainer,out int previous)?Math.Min(previous,percent):percent;
                if(!TryGetRemoteAddress(id,out ulong address)||!connections.ContainsKey(address))continue;
                string name=Property(set,ref info,FriendlyName)??Property(set,ref info,Description)??"Bluetooth";
                if(!grouped.TryGetValue(address,out var candidate))
                    grouped[address]=new Candidate(id!,name,container);
                else
                {
                    if(candidate.ContainerId is null&&container is not null)candidate.ContainerId=container;
                    if(candidate.Name=="Bluetooth"&&!string.Equals(name,"Bluetooth",StringComparison.OrdinalIgnoreCase))
                    { candidate.Name=name;candidate.Id=id!; }
                }
            }
        }
        finally{SetupDiDestroyDeviceInfoList(set);}
        return grouped.OrderBy(x=>x.Value.Name,StringComparer.CurrentCultureIgnoreCase)
            .Select(x=>new BluetoothDevice(x.Value.Id,x.Value.Name,connections[x.Key],GuessKind(x.Value.Name),x.Value.ContainerId,x.Key,
                connections[x.Key]&&x.Value.ContainerId is Guid container&&batteries.TryGetValue(container,out int battery)?battery:null))
            .ToArray();
    }

    internal BluetoothSetConnectedResult SetConnected(BluetoothDevice device,bool connected,CancellationToken cancellation=default)
    {
        if(device is null||!TryGetRemoteAddress(device.Id,out ulong address)||device.Address!=0&&device.Address!=address)
            return new(BluetoothMutationStatus.InvalidTarget,connected,null,0,0);
        BluetoothDevice? fresh;
        try{fresh=Scan().FirstOrDefault(row=>string.Equals(row.Id,device.Id,StringComparison.OrdinalIgnoreCase));}
        catch(BluetoothScanException e){return new(BluetoothMutationStatus.NativeError,connected,null,e.NativeError,0);}
        catch{ return new(BluetoothMutationStatus.NativeError,connected,null,1,0); }
        if(fresh is null)return new(BluetoothMutationStatus.TargetMissing,connected,null,0,0);
        if(fresh.Address!=address||fresh.ContainerId!=device.ContainerId)
            return new(BluetoothMutationStatus.InvalidTarget,connected,null,0,0);
        if(cancellation.IsCancellationRequested)return new(BluetoothMutationStatus.Cancelled,connected,fresh.Connected,ErrorCancelled,0);

        int native=0;
        try
        {
            if(fresh.Kind==BluetoothKind.Controller)
            {
                if(connected)return new(BluetoothMutationStatus.UnsupportedState,connected,fresh.Connected,0,0);
                if(fresh.Connected!=connected)native=BluetoothDisconnectPaired(address);
            }
            else
            {
                if(fresh.ContainerId is null)return new(BluetoothMutationStatus.UnsupportedState,connected,fresh.Connected,0,0);
                native=BluetoothAudioSetConnected(fresh.ContainerId.Value.ToString("B"),connected?1:0);
            }
        }
        catch(DllNotFoundException){return new(BluetoothMutationStatus.NativeError,connected,null,1,0);}
        catch(EntryPointNotFoundException){return new(BluetoothMutationStatus.NativeError,connected,null,1,0);}
        if(native!=0)return new(BluetoothMutationStatus.NativeError,connected,null,native,0);
        return ConfirmConnected(fresh,connected,cancellation);
    }

    internal static bool IsEligibleRemoteId(string id)=>TryGetRemoteAddress(id,out _);

    BluetoothSetConnectedResult ConfirmConnected(BluetoothDevice target,bool desired,CancellationToken cancellation)
    {
        bool? lastRadio=target.Connected;int lastError=0,lastEndpoints=0;
        long deadline=Environment.TickCount64+(long)ConfirmationTimeout.TotalMilliseconds;
        do
        {
            if(cancellation.IsCancellationRequested)return new(BluetoothMutationStatus.Cancelled,desired,lastRadio,ErrorCancelled,lastEndpoints);
            try
            {
                var current=Scan().FirstOrDefault(row=>string.Equals(row.Id,target.Id,StringComparison.OrdinalIgnoreCase));
                lastRadio=current?.Connected;
                if(current is null){lastError=ErrorNotFound;break;}
                if(target.Kind==BluetoothKind.Controller)
                {
                    if(lastRadio==desired)return new(BluetoothMutationStatus.Confirmed,desired,lastRadio,0,0);
                }
                else if(current.ContainerId is Guid container)
                {
                    int state=BluetoothAudioState(container.ToString("B"),out int audio,out int endpoints);
                    lastEndpoints=endpoints;
                    if(state!=0)lastError=state;
                    else if(audio==(desired?1:0)&&lastRadio==desired)
                        return new(BluetoothMutationStatus.Confirmed,desired,lastRadio,0,endpoints);
                }
                else lastError=0x1002;
            }
            catch(BluetoothScanException e){lastError=e.NativeError;}
            catch(Exception){lastError=1;}
            if(Environment.TickCount64>=deadline)break;
            Thread.Sleep(250);
        }while(Environment.TickCount64<deadline);
        if(lastError!=0)return new(BluetoothMutationStatus.NativeError,desired,lastRadio,lastError,lastEndpoints);
        return new(BluetoothMutationStatus.Unconfirmed,desired,lastRadio,0,lastEndpoints);
    }

    static Dictionary<ulong,bool> ReadConnections()
    {
        var result=new Dictionary<ulong,bool>();
        var search=new SearchInfo{Size=Marshal.SizeOf<SearchInfo>(),Authenticated=true,Remembered=true,Connected=true};
        var device=new RadioDevice{Size=Marshal.SizeOf<RadioDevice>(),Name=""};
        nint find=BluetoothFindFirstDevice(ref search,ref device);
        if(find==0)
        {
            int error=Marshal.GetLastWin32Error();
            if(error!=ErrorFileNotFound&&error!=ErrorNoMoreItems)throw new BluetoothScanException("Bluetooth link scan failed",error);
            return result;
        }
        try
        {
            do
            {
                if((device.Address&0xFFFF000000000000UL)==0&&device.Address!=0&&device.Authenticated)result[device.Address]=device.Connected;
                device.Size=Marshal.SizeOf<RadioDevice>();
            }while(BluetoothFindNextDevice(find,ref device));
            int error=Marshal.GetLastWin32Error();
            if(error!=ErrorNoMoreItems)throw new BluetoothScanException("Bluetooth link scan failed",error);
        }
        finally{BluetoothFindDeviceClose(find);}
        return result;
    }

    static bool TryGetRemoteAddress(string? id,out ulong address)
    {
        address=0;
        if(string.IsNullOrEmpty(id))return false;
        var parts=id.Split('\\');
        if(parts.Length!=3||!string.Equals(parts[0],"BTHENUM",StringComparison.OrdinalIgnoreCase))return false;
        if(parts[1].Length!=16||!parts[1].StartsWith("DEV_",StringComparison.OrdinalIgnoreCase)||parts[2].Length==0)return false;
        if(parts[2].IndexOfAny(['"','\r','\n'])>=0)return false;
        return ulong.TryParse(parts[1].AsSpan(4),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out address)&&address!=0&&(address&0xFFFF000000000000UL)==0;
    }

    static string? Property(nint set,ref DeviceInfo info,uint key)
    {
        var buffer=Marshal.AllocHGlobal(2048);
        try
        {
            if(!SetupDiGetDeviceRegistryProperty(set,ref info,key,out _,buffer,2048,out uint size))return null;
            return Marshal.PtrToStringUni(buffer,Math.Max(0,(int)size/2-1))?.TrimEnd('\0').Trim();
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }

    static Guid? ContainerId(nint set,ref DeviceInfo info)
    {
        var key=ContainerKey;var buffer=Marshal.AllocHGlobal(16);
        try
        {
            if(!SetupDiGetDeviceProperty(set,ref info,ref key,out uint type,buffer,16,out uint required,0)||type!=DevPropTypeGuid||required<16)return null;
            var value=Marshal.PtrToStructure<Guid>(buffer);return value==Guid.Empty?null:value;
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }

    internal static int? DecodeBattery(uint type,uint size,byte value)=>type==3&&size==1&&value<=100?value:null;

    static int? BatteryPercent(nint set,ref DeviceInfo info)
    {
        var key=BatteryKey;var buffer=Marshal.AllocHGlobal(1);
        try
        {
            return SetupDiGetDeviceProperty(set,ref info,ref key,out uint type,buffer,1,out uint size,0)
                ?DecodeBattery(type,size,Marshal.ReadByte(buffer)):null;
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }

    static BluetoothKind GuessKind(string name){string n=name.ToLowerInvariant();return n.Contains("buds")||n.Contains("head")||n.Contains("ear")||n.Contains("écoute")?BluetoothKind.Headphones:n.Contains("speaker")||n.Contains("soundbar")||n.Contains("party")||n.Contains("enceinte")?BluetoothKind.Speaker:n.Contains("controller")||n.Contains("dual")||n.Contains("manette")||n.Contains("gamepad")?BluetoothKind.Controller:BluetoothKind.Other;}
    static string? InstanceId(uint node,out int error){var buffer=new StringBuilder(512);error=CM_Get_Device_ID(node,buffer,buffer.Capacity,0);return error==0?buffer.ToString():null;}
    static readonly nint Invalid=new(-1);

    [DllImport("Battlestation.Desk.dll",CharSet=CharSet.Unicode,CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)] static extern int BluetoothAudioState(string containerId,out int connected,out int endpointCount);
    [DllImport("Battlestation.Desk.dll",CharSet=CharSet.Unicode,CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)] static extern int BluetoothAudioSetConnected(string containerId,int desired);
    [DllImport("Battlestation.Desk.dll",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)] static extern int BluetoothDisconnectPaired(ulong address);

    [StructLayout(LayoutKind.Sequential)] struct DevPropKey{public Guid FormatId;public uint PropertyId;internal DevPropKey(Guid formatId,uint propertyId){FormatId=formatId;PropertyId=propertyId;}}
    [StructLayout(LayoutKind.Sequential)] struct SearchInfo{public int Size;[MarshalAs(UnmanagedType.Bool)]public bool Authenticated;[MarshalAs(UnmanagedType.Bool)]public bool Remembered;[MarshalAs(UnmanagedType.Bool)]public bool Unknown;[MarshalAs(UnmanagedType.Bool)]public bool Connected;[MarshalAs(UnmanagedType.Bool)]public bool Inquiry;public byte Timeout;public nint Radio;}
    [StructLayout(LayoutKind.Sequential)] struct SystemTime{public ushort Year,Month,DayOfWeek,Day,Hour,Minute,Second,Milliseconds;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct RadioDevice{public int Size;public ulong Address;public uint Class;[MarshalAs(UnmanagedType.Bool)]public bool Connected;[MarshalAs(UnmanagedType.Bool)]public bool Remembered;[MarshalAs(UnmanagedType.Bool)]public bool Authenticated;public SystemTime LastSeen,LastUsed;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)]public string Name;}
    [StructLayout(LayoutKind.Sequential)] struct DeviceInfo{public int cbSize;public Guid ClassGuid;public uint DevInst;public nint Reserved;}
    [DllImport("bthprops.cpl",SetLastError=true)] static extern nint BluetoothFindFirstDevice(ref SearchInfo search,ref RadioDevice device);
    [DllImport("bthprops.cpl",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothFindNextDevice(nint find,ref RadioDevice device);
    [DllImport("bthprops.cpl")][return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothFindDeviceClose(nint find);
    [DllImport("setupapi.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern nint SetupDiGetClassDevs(ref Guid classGuid,string? enumerator,nint hwnd,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiEnumDeviceInfo(nint set,uint index,ref DeviceInfo info);
    [DllImport("setupapi.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceRegistryProperty(nint set,ref DeviceInfo info,uint property,out uint regType,nint buffer,uint size,out uint required);
    [DllImport("setupapi.dll",SetLastError=true,CharSet=CharSet.Unicode,EntryPoint="SetupDiGetDevicePropertyW")] static extern bool SetupDiGetDeviceProperty(nint set,ref DeviceInfo info,ref DevPropKey key,out uint type,nint buffer,uint size,out uint required,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiDestroyDeviceInfoList(nint set);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_Device_ID(uint devInst,StringBuilder buffer,int length,uint flags);
}
