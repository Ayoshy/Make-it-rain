using System.Buffers.Binary;

namespace Battlestation;
// Binary frame record shared by the video bridge, the desktop and the tests.
// Layout: marker, tab, capture identity, kind, sequence, size, playing, then the
// raw JPEG bytes and an optional bounded diagnostics JSON blob. The base64 JSON
// envelope stays on the native-messaging hop (extension to bridge); the desktop
// never parses or allocates it per frame.
internal readonly struct VideoFrameHeader
{
    internal readonly int TabId;
    internal readonly Guid CaptureId;
    internal readonly byte Kind;
    internal readonly long Sequence;
    internal readonly int Width;
    internal readonly int Height;
    internal readonly bool Playing;
    internal readonly int JpegOffset;
    internal readonly int JpegLength;
    internal readonly int ExtraOffset;
    internal readonly int ExtraLength;
    internal VideoFrameHeader(int tabId,Guid captureId,byte kind,long sequence,int width,int height,bool playing,int jpegOffset,int jpegLength,int extraOffset,int extraLength)
    {
        TabId=tabId;CaptureId=captureId;Kind=kind;Sequence=sequence;Width=width;Height=height;Playing=playing;
        JpegOffset=jpegOffset;JpegLength=jpegLength;ExtraOffset=extraOffset;ExtraLength=extraLength;
    }
}
internal static class VideoFrameRecord
{
    internal const byte Marker=0x01;
    internal const int HeaderSize=44;
    internal const int MaxJpegBytes=2097152;
    internal const int MaxExtraBytes=4096;
    internal const byte KindYoutube=1;
    internal const byte KindTwitch=2;
    internal static byte? KindCode(string kind)=>kind switch{"youtube"=>KindYoutube,"twitch"=>KindTwitch,_=>null};
    internal static string? KindName(byte code)=>code switch{KindYoutube=>"youtube",KindTwitch=>"twitch",_=>null};
    internal static bool TryBuild(int tabId,Guid captureId,byte kind,long sequence,int width,int height,bool playing,ReadOnlySpan<byte> jpeg,ReadOnlySpan<byte> extra,out byte[] record)
    {
        record=[];
        if(!Valid(tabId,kind,sequence,width,height,jpeg.Length,extra.Length))return false;
        record=new byte[HeaderSize+jpeg.Length+extra.Length];var span=record.AsSpan();
        span[0]=Marker;
        BinaryPrimitives.WriteInt32LittleEndian(span[1..],tabId);
        captureId.TryWriteBytes(span.Slice(5,16));
        span[21]=kind;
        BinaryPrimitives.WriteInt64LittleEndian(span[22..],sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(span[30..],(ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..],(ushort)height);
        span[34]=playing?(byte)1:(byte)0;
        span[35]=0;
        BinaryPrimitives.WriteInt32LittleEndian(span[36..],jpeg.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..],extra.Length);
        jpeg.CopyTo(span[HeaderSize..]);extra.CopyTo(span[(HeaderSize+jpeg.Length)..]);
        return true;
    }
    internal static bool TryRead(ReadOnlySpan<byte> message,out VideoFrameHeader header)
    {
        header=default;
        if(message.Length<HeaderSize||message[0]!=Marker)return false;
        int tabId=BinaryPrimitives.ReadInt32LittleEndian(message[1..]);
        var captureId=new Guid(message.Slice(5,16));
        byte kind=message[21];
        long sequence=BinaryPrimitives.ReadInt64LittleEndian(message[22..]);
        int width=BinaryPrimitives.ReadUInt16LittleEndian(message[30..]);
        int height=BinaryPrimitives.ReadUInt16LittleEndian(message[32..]);
        int jpegLength=BinaryPrimitives.ReadInt32LittleEndian(message[36..]);
        int extraLength=BinaryPrimitives.ReadInt32LittleEndian(message[40..]);
        if(!Valid(tabId,kind,sequence,width,height,jpegLength,extraLength))return false;
        if((long)HeaderSize+jpegLength+extraLength!=message.Length)return false;
        header=new(tabId,captureId,kind,sequence,width,height,message[34]!=0,HeaderSize,jpegLength,HeaderSize+jpegLength,extraLength);
        return true;
    }
    static bool Valid(int tabId,byte kind,long sequence,int width,int height,int jpegLength,int extraLength)=>
        tabId>0&&KindName(kind) is not null&&sequence>=0&&width is >=1 and <=1920&&height is >=1 and <=1080&&jpegLength is >=12 and <=MaxJpegBytes&&extraLength is >=0 and <=MaxExtraBytes;
}
