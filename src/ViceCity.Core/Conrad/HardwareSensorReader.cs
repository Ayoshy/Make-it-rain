using LibreHardwareMonitor.Hardware;

namespace ConradSensor;

internal sealed class HardwareSensorReader : IDisposable
{
    private const string ExpectedCpu = "Intel Core i5-9600KF";
    private const string ExpectedGpu = "NVIDIA GeForce RTX 2060 SUPER";

    private readonly MsiCenterSensorReader _msiCenter = new();
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = false,
        IsGpuEnabled = true
    };

    private bool _isOpen;

    public TemperatureSnapshot Read()
    {
        EnsureOpen();
        _computer.Accept(UpdateVisitor.Instance);

        var hardware = Flatten(_computer.Hardware).ToArray();
        var gpu = hardware.FirstOrDefault(item =>
                      item.HardwareType == HardwareType.GpuNvidia &&
                      item.Name.Contains("2060", StringComparison.OrdinalIgnoreCase))
                  ?? hardware.FirstOrDefault(item => item.HardwareType == HardwareType.GpuNvidia);

        var msiCpu = _msiCenter.Read();
        var coreTemperatures = msiCpu.Cores;
        var package = msiCpu.PackageCelsius;
        double? hottestCore = coreTemperatures.Count == 0
            ? null
            : coreTemperatures.Max(item => item.Celsius);

        var gpuTemperatures = Sensors(gpu, SensorType.Temperature).ToArray();
        var gpuTemperature = FindValue(gpuTemperatures, "GPU Core")
                             ?? FindValue(gpuTemperatures, "Core")
                             ?? MaxValue(gpuTemperatures);

        return new TemperatureSnapshot(
            CpuPackageCelsius: package,
            CpuHottestCoreCelsius: hottestCore,
            CpuCores: coreTemperatures,
            CpuLoadPercent: msiCpu.LoadPercent,
            GpuCelsius: gpuTemperature,
            GpuLoadPercent: FindValue(Sensors(gpu, SensorType.Load), "GPU Core")
                            ?? MaxValue(Sensors(gpu, SensorType.Load)),
            GpuFanPercent: FindValue(Sensors(gpu, SensorType.Control), "GPU Fan")
                           ?? MaxValue(Sensors(gpu, SensorType.Control)),
            GpuPowerWatts: FindValue(Sensors(gpu, SensorType.Power), "GPU Package")
                           ?? MaxValue(Sensors(gpu, SensorType.Power)),
            FetchedAt: DateTimeOffset.Now);
    }

    public string HardwareSummary
    {
        get
        {
            EnsureOpen();
            var names = Flatten(_computer.Hardware)
                .Where(item => item.HardwareType is HardwareType.Cpu or HardwareType.GpuNvidia)
                .Select(item => item.Name)
                .ToArray();
            return names.Length == 0
                ? $"{ExpectedCpu} · {ExpectedGpu}"
                : string.Join(" · ", names);
        }
    }

    private void EnsureOpen()
    {
        if (_isOpen)
        {
            return;
        }

        _computer.Open();
        _isOpen = true;
    }

    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> roots)
    {
        foreach (var hardware in roots)
        {
            yield return hardware;
            foreach (var child in Flatten(hardware.SubHardware))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<ISensor> Sensors(IHardware? hardware, SensorType type) =>
        hardware?.Sensors.Where(sensor => sensor.SensorType == type)
        ?? Enumerable.Empty<ISensor>();

    private static double? FindValue(IEnumerable<ISensor> sensors, string name) =>
        sensors.FirstOrDefault(sensor =>
            sensor.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
            sensor.Value.HasValue)?.Value;

    private static double? MaxValue(IEnumerable<ISensor> sensors)
    {
        var values = sensors
            .Where(sensor => sensor.Value.HasValue)
            .Select(sensor => (double)sensor.Value!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private static int CoreNumber(string name)
    {
        var digits = new string(name.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : int.MaxValue;
    }

    private static string NormalizeCoreName(string name)
    {
        var number = CoreNumber(name);
        return number == int.MaxValue ? name : $"Cœur {number}";
    }

    public void Dispose()
    {
        if (_isOpen)
        {
            _computer.Close();
            _isOpen = false;
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public static UpdateVisitor Instance { get; } = new();

        public void VisitComputer(IComputer computer) => computer.Traverse(this);

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
