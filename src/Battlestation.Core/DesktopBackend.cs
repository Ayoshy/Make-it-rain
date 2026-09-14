namespace Battlestation.Core;

/// <summary>Direct access to desktop data and commands.</summary>
public sealed class DesktopBackend : IDisposable
{
    readonly Backend backend;
    public DesktopBackend(string dataDirectory) => backend = new Backend(dataDirectory);
    public string Read(string metric) => backend.Metric(metric);
    public int Revision(bool hardware)=>backend.Revision(hardware);
    public void Command(string command) => backend.NativeCommand(command);
    public void Dispose() => backend.Dispose();
}
