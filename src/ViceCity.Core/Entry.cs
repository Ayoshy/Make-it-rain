using System.Runtime.InteropServices;

namespace ViceCity;

public static class Entry
{
    static Backend? backend;
    [UnmanagedCallersOnly]
    public static int Start(nint path)
    {
        try { backend ??= new Backend(Marshal.PtrToStringUni(path) ?? throw new ArgumentException()); return 0; }
        catch { return -1; }
    }
    [UnmanagedCallersOnly]
    public static int ReadMetric(nint metric, nint buffer, int length)
    {
        try
        {
            if (length < 1) return -1;
            var value = backend?.Metric(Marshal.PtrToStringUni(metric) ?? "summary") ?? "—";
            var chars = value[..Math.Min(value.Length, length - 1)].ToCharArray();
            Marshal.Copy(chars, 0, buffer, chars.Length);
            Marshal.WriteInt16(buffer, chars.Length * 2, 0);
            return chars.Length;
        }
        catch { Marshal.WriteInt16(buffer, 0); return -1; }
    }
    [UnmanagedCallersOnly]
    public static void Command(nint command) { try { backend?.NativeCommand(Marshal.PtrToStringUni(command) ?? ""); } catch { } }
    [UnmanagedCallersOnly]
    public static void Stop() { try { backend?.Dispose(); backend = null; } catch { } }
}
