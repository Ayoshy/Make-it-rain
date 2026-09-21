using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;

namespace Battlestation;
// Native messaging carries JSON with a base64 JPEG (imposed by Chrome). Frames
// become bounded binary records for the local pipe so the desktop process never
// parses JSON or base64 per image; every other message is relayed verbatim.
internal static class FramePacker
{
    internal static byte[] Pack(byte[] json)
    {
        try
        {
            var reader=new Utf8JsonReader(json);
            if(!reader.Read()||reader.TokenType!=JsonTokenType.StartObject)return json;
            string? type=null,kind=null,capture=null,visibility=null,version=null;
            int tabId=0,width=0,height=0;long sequence=0,decodedFrames=-1;double encodeMs=0;bool playing=false;byte[]? jpeg=null;
            while(reader.Read())
            {
                if(reader.TokenType==JsonTokenType.EndObject)break;
                if(reader.TokenType!=JsonTokenType.PropertyName){reader.Skip();continue;}
                if(reader.ValueTextEquals("type")){if(!reader.Read())break;type=reader.TokenType==JsonTokenType.String?reader.GetString():null;}
                else if(reader.ValueTextEquals("tabId")){if(!reader.Read())break;if(reader.TokenType==JsonTokenType.Number)tabId=reader.GetInt32();}
                else if(reader.ValueTextEquals("kind")){if(!reader.Read())break;kind=reader.TokenType==JsonTokenType.String?reader.GetString():null;}
                else if(reader.ValueTextEquals("captureId")){if(!reader.Read())break;capture=reader.TokenType==JsonTokenType.String?reader.GetString():null;}
                else if(reader.ValueTextEquals("sequence")){if(!reader.Read())break;if(reader.TokenType==JsonTokenType.Number)sequence=reader.GetInt64();}
                else if(reader.ValueTextEquals("width")){if(!reader.Read())break;if(reader.TokenType==JsonTokenType.Number)width=reader.GetInt32();}
                else if(reader.ValueTextEquals("height")){if(!reader.Read())break;if(reader.TokenType==JsonTokenType.Number)height=reader.GetInt32();}
                else if(reader.ValueTextEquals("playing")){if(!reader.Read())break;playing=reader.TokenType==JsonTokenType.True;}
                else if(reader.ValueTextEquals("decodedFrames")){if(!reader.Read())break;if(reader.TokenType==JsonTokenType.Number)decodedFrames=reader.GetInt64();}
                else if(reader.ValueTextEquals("encodeMs")){if(!reader.Read())break;if(reader.TokenType==JsonTokenType.Number)encodeMs=reader.GetDouble();}
                else if(reader.ValueTextEquals("visibility")){if(!reader.Read())break;visibility=reader.TokenType==JsonTokenType.String?reader.GetString():null;}
                else if(reader.ValueTextEquals("version")){if(!reader.Read())break;version=reader.TokenType==JsonTokenType.String?reader.GetString():null;}
                else if(reader.ValueTextEquals("jpeg")){jpeg=ReadJpeg(ref reader);if(jpeg is null)return json;}
                else reader.Skip();
            }
            if(type!="frame"||jpeg is null)return json;
            if(kind is not string kindName)return json;
            if(VideoFrameRecord.KindCode(kindName) is not byte kindCode)return json;
            if(capture is null||!Guid.TryParseExact(capture,"N",out var captureId))return json;
            if(!VideoFrameRecord.TryBuild(tabId,captureId,kindCode,sequence,width,height,playing,jpeg,Diagnostics(decodedFrames,encodeMs,visibility,version),out var record))return json;
            return record;
        }
        catch(Exception e) when(e is JsonException or FormatException or InvalidOperationException or ArgumentException or OverflowException){return json;}
    }
    static byte[]? ReadJpeg(ref Utf8JsonReader reader)
    {
        if(!reader.Read()||reader.TokenType!=JsonTokenType.String)return null;
        if(reader.HasValueSequence||reader.ValueIsEscaped)return Convert.FromBase64String(reader.GetString()!);
        var span=reader.ValueSpan;var buffer=new byte[Base64.GetMaxDecodedFromUtf8Length(span.Length)];
        if(Base64.DecodeFromUtf8(span,buffer,out int written,out int consumed)!=OperationStatus.Done||consumed!=span.Length)return null;
        return written==buffer.Length?buffer:buffer[..written];
    }
    static byte[] Diagnostics(long decodedFrames,double encodeMs,string? visibility,string? version)
    {
        var buffer=new ArrayBufferWriter<byte>(96);
        using(var json=new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            if(decodedFrames>=0)json.WriteNumber("decodedFrames",decodedFrames);
            if(encodeMs>0)json.WriteNumber("encodeMs",encodeMs);
            if(visibility is {Length:>0 and <=16})json.WriteString("visibility",visibility);
            if(version is {Length:>0 and <=16})json.WriteString("version",version);
            json.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
}
