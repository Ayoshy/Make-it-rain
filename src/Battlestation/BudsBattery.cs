using System.Runtime.InteropServices;

namespace Battlestation;

internal sealed record BudsBattery(int? Left,int? Right,int? Case)
{
    internal static BudsBattery? Read(ulong address)
    {
        var bytes=new byte[4096];
        int length=BluetoothBudsReadStatus(address,bytes,bytes.Length);
        return length>0&&length<=bytes.Length?Decode(bytes.AsSpan(0,length)):null;
    }

    // Buds3 Pro Samsung RFCOMM status layout; see docs/BLUETOOTH.md.
    internal static BudsBattery? Decode(ReadOnlySpan<byte> bytes)
    {
        BudsBattery? result=null;
        for(int offset=0;offset+7<=bytes.Length;offset++)
        {
            if(bytes[offset]!=0xFD)continue;
            int header=bytes[offset+1]|bytes[offset+2]<<8,size=header&0x3FF;
            if((header&0x2000)!=0||size<3||offset+size+4>bytes.Length)continue;
            var body=bytes.Slice(offset+3,size);
            if(bytes[offset+size+3]!=0xDD||Checksum(body[..^2])!=(body[^2]|body[^1]<<8))continue;
            int shift=body[0] switch{0x60=>0,0x61=>1,_=>-1};
            if(shift>=0&&size>=10+shift)
                result=new(Percent(body[2+shift]),Percent(body[3+shift]),Percent(body[7+shift]));
            offset+=size+3;
        }
        return result;
    }

    static int? Percent(byte value)=>value<=100?value:null;
    internal static ushort Checksum(ReadOnlySpan<byte> data)
    {
        ushort crc=0;
        foreach(byte value in data)
        {
            crc^=(ushort)(value<<8);
            for(int bit=0;bit<8;bit++)crc=(ushort)((crc<<1)^((crc&0x8000)!=0?0x1021:0));
        }
        return crc;
    }

    [DllImport("Battlestation.Desk.dll",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
    static extern int BluetoothBudsReadStatus(ulong address,[Out] byte[] data,int capacity);
}
