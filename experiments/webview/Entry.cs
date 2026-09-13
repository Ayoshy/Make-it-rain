using System.Runtime.InteropServices;

namespace ViceCity;

public static class Entry
{
    static readonly object Gate = new();
    internal static Backend? Backend;
    static Thread? uiThread;
    static PreviewWindow? preview;
    static string root = "";

    [UnmanagedCallersOnly]
    public static int Start(nint path)
    {
        try { lock (Gate) { root = Marshal.PtrToStringUni(path) ?? throw new ArgumentException(); Backend ??= new Backend(root); } return 0; }
        catch { return -1; }
    }
    [UnmanagedCallersOnly]
    public static int Read(nint buffer, int length)
    {
        try
        {
            if (length < 1) return -1;
            var value = Backend?.Display ?? "Indisponible";
            var chars = (value.Length < length ? value : value[..(length - 1)]).ToCharArray();
            Marshal.Copy(chars, 0, buffer, chars.Length);
            Marshal.WriteInt16(buffer, chars.Length * 2, 0);
            return chars.Length;
        }
        catch { return -1; }
    }
    [UnmanagedCallersOnly]
    public static void Refresh() { try { Backend?.RequestRefresh(); } catch { } }
    [UnmanagedCallersOnly]
    public static void Preview() => OpenRenderer(0);
    [UnmanagedCallersOnly]
    public static void Desktop(nint window) => OpenRenderer(window);
    static void OpenRenderer(nint window)
    {
        try
        {
            lock (Gate)
            {
                if (Backend is null || uiThread is { IsAlive: true }) return;
                uiThread = new Thread(() =>
                {
                    try { preview = new PreviewWindow(Backend, root, window); Application.Run(preview); }
                    catch (Exception e) { File.WriteAllText(Path.Combine(root, "renderer-error.txt"), e.GetType().Name + ": " + e.Message); }
                    finally { preview = null; }
                }) { IsBackground = true, Name = "ViceCity renderer" };
                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();
            }
        }
        catch { }
    }
    [UnmanagedCallersOnly]
    public static void Capture() { try { preview?.BeginInvoke(() => _ = preview.CaptureAsync()); } catch { } }
    [UnmanagedCallersOnly]
    public static void Verify() { try { preview?.BeginInvoke(() => _ = preview.VerifyLayoutsAsync()); } catch { } }
    [UnmanagedCallersOnly]
    public static void ClosePreview() { try { preview?.BeginInvoke(() => preview.Close()); } catch { } }
    [UnmanagedCallersOnly]
    public static void Stop()
    {
        try { preview?.BeginInvoke(() => preview.Close()); uiThread?.Join(TimeSpan.FromSeconds(5)); Backend?.Dispose(); Backend = null; }
        catch { }
    }
}
