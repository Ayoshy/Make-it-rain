using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ConradSensor;

internal sealed record MsiCpuSnapshot(
    double? PackageCelsius,
    IReadOnlyList<CoreTemperature> Cores,
    double? LoadPercent);

internal sealed class MsiCenterSensorReader
{
    private const string CommonApiPath =
        @"C:\Program Files (x86)\MSI\MSI Center\CS_CommonAPI.dll";

    private static readonly byte[] CurrentDataCommand =
        [0x05, 0x03, 0x01, 0x08, 0x01, 0x00, 0x00, 0x01];

    private readonly MethodInfo? _sendData = FindSendData();

    public MsiCpuSnapshot Read()
    {
        if (_sendData is null)
        {
            return new(null, [], null);
        }

        var response = (byte[]?)_sendData.Invoke(
            null,
            [9999, CurrentDataCommand]);
        if (response is not { Length: > 1 })
        {
            return new(null, [], null);
        }

        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        var coreCount = root.TryGetProperty("CoreNo", out var coreCountElement)
            ? Math.Clamp(coreCountElement.GetInt32(), 0, 6)
            : 0;

        var cores = new List<CoreTemperature>(coreCount);
        if (root.TryGetProperty("CurT", out var coreTemperatures) &&
            coreTemperatures.ValueKind == JsonValueKind.Array)
        {
            for (var index = 0;
                 index < coreCount && index < coreTemperatures.GetArrayLength();
                 index++)
            {
                var value = coreTemperatures[index].GetDouble();
                if (value > 0)
                {
                    cores.Add(new CoreTemperature($"Cœur {index + 1}", value));
                }
            }
        }

        var package = FirstArrayValue(root, "TemperatureValue");
        var load = FirstArrayValue(root, "CPUusage", allowZero: true);
        return new(package, cores, load);
    }

    private static double? FirstArrayValue(JsonElement root, string propertyName, bool allowZero = false)
    {
        if (!root.TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array ||
            values.GetArrayLength() == 0)
        {
            return null;
        }

        var value = values[0].GetDouble();
        return double.IsFinite(value) && (value > 0 || allowZero && value == 0) ? value : null;
    }

    private static MethodInfo? FindSendData()
    {
        if (!File.Exists(CommonApiPath))
        {
            return null;
        }

        try
        {
            var assembly = Assembly.LoadFrom(CommonApiPath);
            var clientType = assembly.GetType("CS_CommonAPI.C_Client");
            return clientType?
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(method =>
                {
                    if (method.Name != "SendData")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(int) &&
                           parameters[1].ParameterType == typeof(byte[]);
                });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"MSI Center telemetry unavailable: {exception.Message}");
            return null;
        }
    }
}
