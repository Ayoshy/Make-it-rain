using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ViceCity;

// Offscreen renderer proof. Desktop parenting and interactive QA are separate gates.
internal sealed class PreviewWindow : Form
{
    readonly WebView2 view = new() { Dock = DockStyle.Fill };
    readonly Backend backend;
    readonly string output;
    readonly nint skinWindow;
    public PreviewWindow(Backend backend, string output, nint skinWindow = 0)
    {
        this.backend = backend; this.output = output;
        this.skinWindow = skinWindow;
        Text = "ViceCity Rainmeter — validation du rendu";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(-5200, 0, 5120, 1440);
        ShowInTaskbar = false;
        Controls.Add(view);
        Shown += async (_, _) =>
        {
            try { if (skinWindow != 0) AttachToSkin(); await InitializeAsync(); }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "renderer-error.txt"), e.GetType().Name + ": " + e.Message); Close(); }
        };
    }
    [DllImport("user32.dll", SetLastError = true)] static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetStyle(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern nint SetStyle(nint window, int index, nint style);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern nint GetParent(nint window);
    void AttachToSkin()
    {
        var style = (GetStyle(Handle, -16).ToInt64() & ~0x80000000L) | 0x40000000L;
        SetStyle(Handle, -16, (nint)style);
        SetParent(Handle, skinWindow);
        if (GetParent(Handle) != skinWindow) throw new InvalidOperationException("Échec d'attachement au skin Rainmeter : " + Marshal.GetLastWin32Error());
        SetWindowPos(Handle, 0, 0, 0, 5120, 1440, 0x0010 | 0x0020 | 0x0040);
        File.WriteAllText(Path.Combine(output, "desktop-host.json"), JsonSerializer.Serialize(new { processId = Environment.ProcessId, parent = skinWindow.ToInt64(), child = Handle.ToInt64(), attached = true }, Backend.Json));
    }
    async Task InitializeAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(output, "WebView2"));
        await view.EnsureCoreWebView2Async(environment);
        view.ZoomFactor = 1;
        var core = view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        var web = Path.Combine(Path.GetDirectoryName(typeof(PreviewWindow).Assembly.Location)!, "web");
        core.SetVirtualHostNameToFolderMapping("vicecity.invalid", web, CoreWebView2HostResourceAccessKind.DenyCors);
        core.NavigationStarting += (_, e) => { if (e.Uri != "https://vicecity.invalid/index.html") e.Cancel = true; };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.WebMessageReceived += async (_, e) =>
        {
            if (e.Source != "https://vicecity.invalid/index.html") return;
            long id = 0;
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var request = doc.RootElement;
                id = request.GetProperty("id").GetInt64();
                var channel = request.GetProperty("channel").GetString() ?? "";
                var path = request.GetProperty("path").GetString() ?? "";
                var body = request.TryGetProperty("body", out var b) ? b.Clone() : default;
                var result = await backend.RequestAsync(channel, path, body);
                if (!IsDisposed) core.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, result }, Backend.Json));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) core.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, error = ex is InvalidOperationException ? ex.Message : "Donnée ou commande indisponible." }, Backend.Json));
            }
        };
        core.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess) return;
            await Task.Delay(4000);
            if (!IsDisposed) await CaptureAsync();
        };
        core.Navigate("https://vicecity.invalid/index.html");
    }
    public async Task CaptureAsync()
    {
        if (view.CoreWebView2 is null || IsDisposed) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        using (var file = File.Create(Path.Combine(output, "render-" + stamp + ".png")))
            await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        var evidence = await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({viewport:[innerWidth,innerHeight],dpr:devicePixelRatio,cpu:document.getElementById('sensor-cpu').textContent,quota:document.getElementById('meter-primary').textContent,composition:document.getElementById('composition').getBoundingClientRect().toJSON(),conrad:document.getElementById('conrad').getBoundingClientRect().toJSON(),codex:document.getElementById('codex-meter').getBoundingClientRect().toJSON(),settings:window.GTA_VI_SETTINGS,artworkTransform:getComputedStyle(document.getElementById('artwork')).transform})");
        File.WriteAllText(Path.Combine(output, "render-" + stamp + ".json"), JsonSerializer.Deserialize<string>(evidence));
    }
    public async Task VerifyLayoutsAsync()
    {
        if (view.CoreWebView2 is null || IsDisposed) return;
        // DOM regression only: deliberately never invokes Apply, Heatwave, or reset APIs.
        var results = new List<object>();
        foreach (var button in new[] { "sensor-details-button", "sensor-cooling-button", "meter-limits-button", "meter-models-button" })
        {
            var script = "(function(){var b=document.getElementById(" + JsonSerializer.Serialize(button) + ");if(!b)return 'missing button';b.click();return 'opened';})()";
            var action = await view.CoreWebView2.ExecuteScriptAsync(script);
            await Task.Delay(1200);
            var data = await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({composition:document.getElementById('composition').getBoundingClientRect().toJSON(),conrad:document.getElementById('conrad').getBoundingClientRect().toJSON(),codex:document.getElementById('codex-meter').getBoundingClientRect().toJSON(),open:Array.from(document.querySelectorAll('[aria-expanded=true]')).map(x=>x.id),errors:Array.from(document.querySelectorAll('[role=alert]')).filter(x=>!x.hidden).map(x=>x.textContent)})");
            results.Add(new { button, action, state = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Deserialize<string>(data)!) });
            using var file = File.Create(Path.Combine(output, "layout-" + button + ".png"));
            await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        File.WriteAllText(Path.Combine(output, "layout-checks.json"), JsonSerializer.Serialize(new { kind = "DOM regression, not real user clicks", results }, Backend.Json));
    }
    protected override void Dispose(bool disposing) { if (disposing) view.Dispose(); base.Dispose(disposing); }
}
