using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace ConradSensor;

internal sealed record GpuControlState(
    bool Available,
    bool FanControlSupported,
    bool ThermalControlSupported,
    bool FanAuto,
    int FanPercent,
    int FanMinPercent,
    int FanMaxPercent,
    int ThermalLimitCelsius,
    int ThermalMinCelsius,
    int ThermalMaxCelsius,
    string Status)
{
    public static GpuControlState Unavailable(string status) =>
        new(
            Available: false,
            FanControlSupported: false,
            ThermalControlSupported: false,
            FanAuto: true,
            FanPercent: 0,
            FanMinPercent: 0,
            FanMaxPercent: 100,
            ThermalLimitCelsius: 0,
            ThermalMinCelsius: 0,
            ThermalMaxCelsius: 100,
            Status: status);
}

internal sealed record GpuControlRequest(
    bool FanManual,
    int FanPercent,
    int ThermalLimitCelsius);

internal sealed class NativeGpuControlService : IDisposable
{
    internal const string ExpectedGpuName = "RTX 2060 SUPER";

    private readonly object _sync = new();
    private Computer? _computer;
    private IHardware? _gpu;
    private IControl[] _fanControls = [];
    private NativeThermalControl? _thermalControl;
    private ElevatedGpuControlSession? _elevatedSession;
    private GpuControlState? _heatwaveRestoreState;
    private bool _disposed;

    public GpuControlState ReadState()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            try
            {
                EnsureInitialized();
                return ReadStateCore();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                DllNotFoundException or
                EntryPointNotFoundException or
                BadImageFormatException)
            {
                return GpuControlState.Unavailable(exception.Message);
            }
        }
    }

    public GpuControlState Apply(GpuControlRequest request)
    {
        lock (_sync)
        {
            _heatwaveRestoreState = null;
            return ApplyCore(request);
        }
    }

    public GpuControlState SetHeatwaveMode(bool enabled)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            EnsureInitialized();

            if (enabled)
            {
                if (_heatwaveRestoreState is not null)
                {
                    return ReadStateCore();
                }

                var before = ReadStateCore();
                if (!before.FanControlSupported ||
                    !before.ThermalControlSupported)
                {
                    throw new InvalidOperationException(
                        "Le mode canicule nécessite le ventilateur " +
                        "et la cible thermique NVIDIA.");
                }

                var after = ApplyCore(CreateHeatwaveRequest(before));
                _heatwaveRestoreState = before;
                return after;
            }

            if (_heatwaveRestoreState is not { } restoreState)
            {
                return ReadStateCore();
            }

            var restored = ApplyCore(new GpuControlRequest(
                FanManual: !restoreState.FanAuto,
                FanPercent: restoreState.FanPercent,
                ThermalLimitCelsius:
                    restoreState.ThermalLimitCelsius));
            _heatwaveRestoreState = null;
            return restored;
        }
    }

    internal static GpuControlRequest CreateHeatwaveRequest(
        GpuControlState state) =>
        new(
            FanManual: true,
            FanPercent: state.FanMaxPercent,
            ThermalLimitCelsius: state.ThermalMinCelsius);

    private GpuControlState ApplyCore(GpuControlRequest request)
    {
            ThrowIfDisposed();
            EnsureInitialized();

            var before = ReadStateCore();
            if (!before.Available)
            {
                throw new InvalidOperationException(before.Status);
            }

            var normalized = NormalizeRequest(request, before);
            var thermalChangeRequested =
                before.ThermalControlSupported &&
                normalized.ThermalLimitCelsius !=
                before.ThermalLimitCelsius;
            var fanChangeRequested =
                before.FanControlSupported &&
                (before.FanAuto == normalized.FanManual ||
                 (normalized.FanManual &&
                  normalized.FanPercent != before.FanPercent));

            if (thermalChangeRequested || fanChangeRequested)
            {
                _elevatedSession ??= new ElevatedGpuControlSession();
                _elevatedSession.Apply(normalized);
            }

            UpdateGpu();
            return ReadStateCore();
    }

    internal static GpuControlRequest NormalizeRequest(
        GpuControlRequest request,
        GpuControlState state) =>
        request with
        {
            FanPercent = state.FanControlSupported
                ? Math.Clamp(
                    request.FanPercent,
                    state.FanMinPercent,
                    state.FanMaxPercent)
                : request.FanPercent,
            ThermalLimitCelsius = state.ThermalControlSupported
                ? Math.Clamp(
                    request.ThermalLimitCelsius,
                    state.ThermalMinCelsius,
                    state.ThermalMaxCelsius)
                : request.ThermalLimitCelsius
        };

    private void EnsureInitialized()
    {
        if (_computer is not null)
        {
            return;
        }

        var computer = new Computer { IsGpuEnabled = true };
        try
        {
            computer.Open();
            computer.Accept(UpdateVisitor.Instance);

            var gpu = computer.Hardware.FirstOrDefault(
                          hardware =>
                              hardware.HardwareType == HardwareType.GpuNvidia &&
                              hardware.Name.Contains(
                                  ExpectedGpuName,
                                  StringComparison.OrdinalIgnoreCase))
                      ?? computer.Hardware.FirstOrDefault(
                          hardware =>
                              hardware.HardwareType == HardwareType.GpuNvidia)
                      ?? throw new InvalidOperationException(
                          "Aucun GPU NVIDIA compatible n’a été détecté.");

            var fanControls = gpu.Sensors
                .Where(sensor =>
                    sensor.SensorType == SensorType.Control &&
                    sensor.Name.Contains(
                        "GPU Fan",
                        StringComparison.OrdinalIgnoreCase) &&
                    sensor.Control is not null)
                .Select(sensor => sensor.Control!)
                .ToArray();

            NativeThermalControl? thermalControl = null;
            try
            {
                thermalControl = NativeThermalControl.Open(ExpectedGpuName);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                // Fan control remains useful when the thermal policy endpoint
                // is unavailable on another driver or GPU.
            }

            _computer = computer;
            _gpu = gpu;
            _fanControls = fanControls;
            _thermalControl = thermalControl;
        }
        catch
        {
            computer.Close();
            throw;
        }
    }

    private GpuControlState ReadStateCore()
    {
        UpdateGpu();

        FanState? fan = null;
        try
        {
            fan = _thermalControl?.ReadFan();
        }
        catch (InvalidOperationException)
        {
            // Keep thermal control available if fan control disappears.
        }

        var fanSupported = fan is not null;
        var fanMinimum = fan?.MinimumPercent ?? 0;
        var fanMaximum = fan?.MaximumPercent ?? 100;
        var fanAuto = fan?.Auto ?? true;
        var fanPercent = fan?.CurrentPercent ?? 0;

        ThermalState? thermal = null;
        try
        {
            thermal = _thermalControl?.Read();
        }
        catch (InvalidOperationException)
        {
            // A driver update may remove the private thermal policy endpoint.
        }

        var thermalSupported = thermal is not null;
        var available = fanSupported || thermalSupported;
        var capabilities = (fanSupported, thermalSupported) switch
        {
            (true, true) => "ventilateur + cible thermique",
            (true, false) => "ventilateur",
            (false, true) => "cible thermique",
            _ => "aucun contrôle"
        };

        return new GpuControlState(
            Available: available,
            FanControlSupported: fanSupported,
            ThermalControlSupported: thermalSupported,
            FanAuto: fanAuto,
            FanPercent: Math.Clamp(fanPercent, fanMinimum, fanMaximum),
            FanMinPercent: fanMinimum,
            FanMaxPercent: fanMaximum,
            ThermalLimitCelsius: thermal?.CurrentCelsius ?? 0,
            ThermalMinCelsius: thermal?.MinimumCelsius ?? 0,
            ThermalMaxCelsius: thermal?.MaximumCelsius ?? 100,
            Status: available
                ? $"Pilote NVIDIA natif · {capabilities}"
                : "Le pilote NVIDIA n’expose aucun contrôle compatible.");
    }

    private void UpdateGpu()
    {
        _gpu?.Update();
        foreach (var child in _gpu?.SubHardware ??
                              Enumerable.Empty<IHardware>())
        {
            child.Update();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _elevatedSession?.Dispose();
            _elevatedSession = null;
            _computer?.Close();
            _computer = null;
            _gpu = null;
            _fanControls = [];
            _thermalControl = null;
            _heatwaveRestoreState = null;
            _disposed = true;
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public static UpdateVisitor Instance { get; } = new();

        public void VisitComputer(IComputer computer) =>
            computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var child in hardware.SubHardware)
            {
                child.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }
}

internal sealed record ThermalState(
    int CurrentCelsius,
    int MinimumCelsius,
    int DefaultCelsius,
    int MaximumCelsius);

internal sealed record FanState(
    bool Auto,
    int CurrentPercent,
    int MinimumPercent,
    int MaximumPercent);

internal sealed class ElevatedGpuControlSession : IDisposable
{
    private const string CommandName = "--gpu-control-helper";
    private NamedPipeServerStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Process? _process;
    private bool _disposed;

    public void Apply(GpuControlRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        _writer!.WriteLine(
            string.Join(
                '|',
                "APPLY",
                request.FanManual ? "1" : "0",
                request.FanPercent.ToString(
                    CultureInfo.InvariantCulture),
                request.ThermalLimitCelsius.ToString(
                    CultureInfo.InvariantCulture)));

        var response = _reader!.ReadLine();
        if (string.Equals(response, "OK", StringComparison.Ordinal))
        {
            return;
        }

        var message = response?.StartsWith(
                "ERR|",
                StringComparison.Ordinal) == true
            ? response[4..]
            : "Le helper GPU élevé ne répond plus.";
        throw new InvalidOperationException(message);
    }

    private void EnsureConnected()
    {
        if (_pipe?.IsConnected == true)
        {
            return;
        }

        // Assembly.Location is empty in the published single-file executable.
        var appHostPath = Path.Combine(Path.GetDirectoryName(typeof(NativeGpuControlService).Assembly.Location)!, "ViceCity.GpuHelper.exe");
        if (!File.Exists(appHostPath))
        {
            throw new InvalidOperationException(
                "Impossible de localiser l’exécutable Conrad Sensor.");
        }

        var pipeName = $"ViceCityRainmeter.Gpu.{Guid.NewGuid():N}";
        _pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        var startInfo = new ProcessStartInfo
        {
            FileName = appHostPath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add(CommandName);
        startInfo.ArgumentList.Add(pipeName);

        try
        {
            _process = Process.Start(startInfo) ??
                throw new InvalidOperationException(
                    "Impossible de lancer le helper GPU élevé.");

            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(20));
            _pipe.WaitForConnectionAsync(timeout.Token)
                .GetAwaiter()
                .GetResult();
            _reader = new StreamReader(
                _pipe,
                Encoding.UTF8,
                leaveOpen: true);
            _writer = new StreamWriter(
                _pipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                leaveOpen: true)
            {
                AutoFlush = true
            };
        }
        catch (Win32Exception exception) when (
            exception.NativeErrorCode == 1223)
        {
            ResetConnection();
            throw new InvalidOperationException(
                "La confirmation Windows est requise pour appliquer " +
                "les réglages GPU.",
                exception);
        }
        catch (OperationCanceledException exception)
        {
            ResetConnection();
            throw new InvalidOperationException(
                "Le helper GPU élevé n’a pas répondu à temps.",
                exception);
        }
    }

    private void ResetConnection()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _pipe?.Dispose();
        _process?.Dispose();
        _writer = null;
        _reader = null;
        _pipe = null;
        _process = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _writer?.WriteLine("EXIT");
            _process?.WaitForExit(5000);
        }
        catch
        {
            // Closing the pipe also makes the helper restore safe defaults.
        }
        finally
        {
            ResetConnection();
            _disposed = true;
        }
    }

    internal static bool IsHelperCommand(string value) =>
        string.Equals(
            value,
            CommandName,
            StringComparison.OrdinalIgnoreCase);
}

internal static class ElevatedGpuControlHost
{
    public static bool TryRunHelper(
        string[] args,
        out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 ||
            !ElevatedGpuControlSession.IsHelperCommand(args[0]))
        {
            return false;
        }

        if (args.Length != 2 ||
            string.IsNullOrWhiteSpace(args[1]))
        {
            exitCode = 2;
            return true;
        }

        try
        {
            Run(args[1]);
        }
        catch
        {
            exitCode = 1;
        }

        return true;
    }

    private static void Run(string pipeName)
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.None);
        pipe.Connect(15000);
        using var reader = new StreamReader(
            pipe,
            Encoding.UTF8,
            leaveOpen: true);
        using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: true)
        {
            AutoFlush = true
        };

        var control = NativeThermalControl.Open(
            NativeGpuControlService.ExpectedGpuName);
        var originalThermal = control.Read().CurrentCelsius;
        var originalFan = control.ReadFan();

        try
        {
            while (reader.ReadLine() is { } command)
            {
                if (string.Equals(
                        command,
                        "EXIT",
                        StringComparison.Ordinal))
                {
                    break;
                }

                try
                {
                    ApplyCommand(control, command);
                    writer.WriteLine("OK");
                }
                catch (Exception exception)
                {
                    writer.WriteLine(
                        $"ERR|{Sanitize(exception.Message)}");
                }
            }
        }
        finally
        {
            TryRestore(() => control.SetFan(
                manual: !originalFan.Auto,
                percent: originalFan.CurrentPercent));
            TryRestore(() => control.SetLimit(originalThermal));
        }
    }

    private static void ApplyCommand(
        NativeThermalControl control,
        string command)
    {
        var parts = command.Split('|');
        if (parts.Length != 4 ||
            !string.Equals(
                parts[0],
                "APPLY",
                StringComparison.Ordinal) ||
            !int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var manualValue) ||
            !int.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var fanPercent) ||
            !int.TryParse(
                parts[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var thermalCelsius))
        {
            throw new InvalidOperationException(
                "Commande GPU invalide.");
        }

        var beforeFan = control.ReadFan();
        var beforeThermal = control.Read();
        try
        {
            if (beforeThermal.CurrentCelsius != thermalCelsius)
            {
                control.SetLimit(thermalCelsius);
            }

            var manual = manualValue != 0;
            if (beforeFan.Auto == manual ||
                (manual &&
                 beforeFan.CurrentPercent != fanPercent))
            {
                control.SetFan(manual, fanPercent);
            }
        }
        catch
        {
            TryRestore(() => control.SetFan(
                !beforeFan.Auto,
                beforeFan.CurrentPercent));
            TryRestore(() => control.SetLimit(
                beforeThermal.CurrentCelsius));
            throw;
        }
    }

    private static void TryRestore(Action restore)
    {
        try
        {
            restore();
        }
        catch
        {
            // Preserve the primary command error or finish shutdown.
        }
    }

    private static string Sanitize(string message) =>
        message
            .Replace('|', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}

internal sealed class NativeThermalControl
{
    private const uint InitializeId = 0x0150E828;
    private const uint EnumPhysicalGpusId = 0xE5AC921F;
    private const uint GetFullNameId = 0xCEEE8E9F;
    private const uint GetThermalInfoId = 0x0D258BB5;
    private const uint GetThermalStatusId = 0xE9C425A1;
    private const uint SetThermalStatusId = 0x34C0B13D;
    private const uint GetFanControlId = 0x814B209F;
    private const uint SetFanControlId = 0xA58971A5;
    private const uint GetFanStatusId = 0x35AED5E8;
    private const int MaxPhysicalGpus = 64;
    private const int MaxThermalEntries = 4;
    private const int MaxFanItems = 32;

    private readonly IntPtr _gpuHandle;
    private readonly GetThermalInfo _getInfo;
    private readonly GetThermalStatus _getStatus;
    private readonly SetThermalStatus _setStatus;
    private readonly GetFanControl _getFanControl;
    private readonly SetFanControl _setFanControl;
    private readonly GetFanStatus _getFanStatus;

    private NativeThermalControl(
        IntPtr gpuHandle,
        GetThermalInfo getInfo,
        GetThermalStatus getStatus,
        SetThermalStatus setStatus,
        GetFanControl getFanControl,
        SetFanControl setFanControl,
        GetFanStatus getFanStatus)
    {
        _gpuHandle = gpuHandle;
        _getInfo = getInfo;
        _getStatus = getStatus;
        _setStatus = setStatus;
        _getFanControl = getFanControl;
        _setFanControl = setFanControl;
        _getFanStatus = getFanStatus;
    }

    public static NativeThermalControl Open(string expectedGpuName)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
        {
            throw new InvalidOperationException(
                "Le contrôle thermique natif nécessite Windows 64 bits.");
        }

        var library = NativeLibrary.Load("nvapi64.dll");
        var queryPointer = NativeLibrary.GetExport(
            library,
            "nvapi_QueryInterface");
        var query = Marshal.GetDelegateForFunctionPointer<QueryInterface>(
            queryPointer);

        var initialize = GetDelegate<Initialize>(query, InitializeId);
        CheckStatus("Initialisation NVAPI", initialize());

        var enumPhysicalGpus = GetDelegate<EnumPhysicalGpus>(
            query,
            EnumPhysicalGpusId);
        var getFullName = GetDelegate<GetFullName>(query, GetFullNameId);
        var getInfo = GetDelegate<GetThermalInfo>(query, GetThermalInfoId);
        var getStatus = GetDelegate<GetThermalStatus>(
            query,
            GetThermalStatusId);
        var setStatus = GetDelegate<SetThermalStatus>(
            query,
            SetThermalStatusId);
        var getFanControl = GetDelegate<GetFanControl>(
            query,
            GetFanControlId);
        var setFanControl = GetDelegate<SetFanControl>(
            query,
            SetFanControlId);
        var getFanStatus = GetDelegate<GetFanStatus>(
            query,
            GetFanStatusId);

        var handles = new IntPtr[MaxPhysicalGpus];
        CheckStatus(
            "Détection des GPU NVIDIA",
            enumPhysicalGpus(handles, out var count));

        for (var index = 0; index < count; index++)
        {
            var name = new StringBuilder(64);
            if (getFullName(handles[index], name) == 0 &&
                name.ToString().Contains(
                    expectedGpuName,
                    StringComparison.OrdinalIgnoreCase))
            {
                var control = new NativeThermalControl(
                    handles[index],
                    getInfo,
                    getStatus,
                    setStatus,
                    getFanControl,
                    setFanControl,
                    getFanStatus);
                _ = control.Read();
                _ = control.ReadFan();
                return control;
            }
        }

        throw new InvalidOperationException(
            $"Le GPU NVIDIA {expectedGpuName} n’a pas été détecté.");
    }

    public ThermalState Read()
    {
        var info = NewThermalInfo();
        var status = NewThermalStatus();

        CheckStatus(
            "Lecture des limites thermiques NVIDIA",
            _getInfo(_gpuHandle, ref info));
        CheckStatus(
            "Lecture de la cible thermique NVIDIA",
            _getStatus(_gpuHandle, ref status));

        var infoCount = Math.Min(info.Count, (byte)MaxThermalEntries);
        var statusCount = Math.Min(
            status.Count,
            (uint)MaxThermalEntries);
        if (infoCount == 0 || statusCount == 0)
        {
            throw new InvalidOperationException(
                "Le pilote NVIDIA n’expose pas de cible thermique réglable.");
        }

        var infoEntry = info.Entries[0];
        var statusEntry = status.Entries
            .Take((int)statusCount)
            .FirstOrDefault(entry =>
                entry.Controller == infoEntry.Controller);
        if (statusEntry.Controller != infoEntry.Controller)
        {
            statusEntry = status.Entries[0];
        }

        return new ThermalState(
            CurrentCelsius: FromShiftedCelsius(statusEntry.Value),
            MinimumCelsius: FromShiftedCelsius(infoEntry.MinTemperature),
            DefaultCelsius: FromShiftedCelsius(
                infoEntry.DefaultTemperature),
            MaximumCelsius: FromShiftedCelsius(infoEntry.MaxTemperature));
    }

    public void SetLimit(int celsius)
    {
        var state = Read();
        var normalized = Math.Clamp(
            celsius,
            state.MinimumCelsius,
            state.MaximumCelsius);
        var current = NewThermalStatus();
        CheckStatus(
            "Lecture de la cible thermique NVIDIA",
            _getStatus(_gpuHandle, ref current));

        var entry = current.Entries[0];
        entry.Value = ToShiftedCelsius(normalized);

        var request = NewThermalStatus();
        request.Count = 1;
        request.Entries[0] = entry;
        CheckStatus(
            "Application de la cible thermique NVIDIA",
            _setStatus(_gpuHandle, ref request));
    }

    public FanState ReadFan()
    {
        var control = NewFanControl();
        var status = NewFanStatus();
        CheckStatus(
            "Lecture du contrôle ventilateur NVIDIA",
            _getFanControl(_gpuHandle, ref control));
        CheckStatus(
            "Lecture du ventilateur NVIDIA",
            _getFanStatus(_gpuHandle, ref status));

        var controlCount = Math.Min(
            control.Count,
            (uint)MaxFanItems);
        var statusCount = Math.Min(
            status.Count,
            (uint)MaxFanItems);
        if (controlCount == 0 || statusCount == 0)
        {
            throw new InvalidOperationException(
                "Le pilote NVIDIA n’expose pas de ventilateur réglable.");
        }

        var controlItem = control.Items[0];
        var statusItem = status.Items
            .Take((int)statusCount)
            .FirstOrDefault(item =>
                item.CoolerId == controlItem.CoolerId);
        if (statusItem.CoolerId != controlItem.CoolerId)
        {
            statusItem = status.Items[0];
        }

        return new FanState(
            Auto: controlItem.ControlMode != FanControlMode.Manual,
            CurrentPercent: (int)statusItem.CurrentLevel,
            MinimumPercent: (int)statusItem.CurrentMinLevel,
            MaximumPercent: (int)statusItem.CurrentMaxLevel);
    }

    public void SetFan(bool manual, int percent)
    {
        var control = NewFanControl();
        CheckStatus(
            "Lecture du contrôle ventilateur NVIDIA",
            _getFanControl(_gpuHandle, ref control));

        var count = Math.Min(control.Count, (uint)MaxFanItems);
        for (var index = 0; index < count; index++)
        {
            var item = control.Items[index];
            item.ControlMode = manual
                ? FanControlMode.Manual
                : FanControlMode.Auto;
            item.Level = manual ? (uint)percent : 0;
            control.Items[index] = item;
        }

        CheckStatus(
            "Application du ventilateur NVIDIA",
            _setFanControl(_gpuHandle, ref control));
    }

    private static T GetDelegate<T>(
        QueryInterface query,
        uint id) where T : Delegate
    {
        var pointer = query(id);
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Fonction NVAPI indisponible (0x{id:X8}).");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private static ThermalInfo NewThermalInfo() =>
        new()
        {
            Version = MakeVersion<ThermalInfo>(2),
            Entries = new ThermalInfoEntry[MaxThermalEntries]
        };

    private static ThermalStatus NewThermalStatus() =>
        new()
        {
            Version = MakeVersion<ThermalStatus>(2),
            Entries = new ThermalStatusEntry[MaxThermalEntries]
        };

    private static FanControl NewFanControl() =>
        new()
        {
            Version = MakeVersion<FanControl>(1),
            Reserved = new uint[8],
            Items = new FanControlItem[MaxFanItems]
        };

    private static FanStatus NewFanStatus() =>
        new()
        {
            Version = MakeVersion<FanStatus>(1),
            Items = new FanStatusItem[MaxFanItems]
        };

    private static uint MakeVersion<T>(uint version) =>
        (uint)Marshal.SizeOf<T>() | (version << 16);

    private static int FromShiftedCelsius(int value) => value >> 8;

    private static int FromShiftedCelsius(uint value) =>
        unchecked((int)value) >> 8;

    private static uint ToShiftedCelsius(int value) =>
        unchecked((uint)(value << 8));

    private static void CheckStatus(string operation, int status)
    {
        if (status != 0)
        {
            throw new NvApiException(operation, status);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Initialize();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumPhysicalGpus(
        [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = MaxPhysicalGpus)]
        IntPtr[] handles,
        out int count);

    [UnmanagedFunctionPointer(
        CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
    private delegate int GetFullName(
        IntPtr handle,
        StringBuilder name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetThermalInfo(
        IntPtr handle,
        ref ThermalInfo info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetThermalStatus(
        IntPtr handle,
        ref ThermalStatus status);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetThermalStatus(
        IntPtr handle,
        ref ThermalStatus status);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFanControl(
        IntPtr handle,
        ref FanControl control);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetFanControl(
        IntPtr handle,
        ref FanControl control);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFanStatus(
        IntPtr handle,
        ref FanStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct ThermalInfo
    {
        public uint Version;
        public byte Count;
        public byte Flags;
        public ushort Padding;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxThermalEntries)]
        public ThermalInfoEntry[] Entries;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThermalInfoEntry
    {
        public uint Controller;
        public uint Unknown;
        public int MinTemperature;
        public int DefaultTemperature;
        public int MaxTemperature;
        public uint DefaultFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThermalStatus
    {
        public uint Version;
        public uint Count;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxThermalEntries)]
        public ThermalStatusEntry[] Entries;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThermalStatusEntry
    {
        public uint Controller;
        public uint Value;
        public uint Flags;
    }

    private enum FanControlMode : uint
    {
        Auto = 0,
        Manual = 1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct FanControl
    {
        public uint Version;
        public uint Reserved1;
        public uint Count;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxFanItems)]
        public FanControlItem[] Items;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct FanControlItem
    {
        public uint CoolerId;
        public uint Level;
        public FanControlMode ControlMode;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct FanStatus
    {
        public uint Version;
        public uint Count;
        public ulong Reserved1;
        public ulong Reserved2;
        public ulong Reserved3;
        public ulong Reserved4;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxFanItems)]
        public FanStatusItem[] Items;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct FanStatusItem
    {
        public uint CoolerId;
        public uint CurrentRpm;
        public uint CurrentMinLevel;
        public uint CurrentMaxLevel;
        public uint CurrentLevel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] Reserved;
    }
}

internal sealed class NvApiException : InvalidOperationException
{
    public const int InvalidUserPrivilege = -137;

    public NvApiException(string operation, int status)
        : base($"{operation} impossible (NVAPI {status}).")
    {
        Status = status;
    }

    public int Status { get; }
}
