using System.Globalization;

namespace ConradSensor;

internal sealed record CoreTemperature(string Name, double Celsius);

internal sealed record TemperatureSnapshot(
    double? CpuPackageCelsius,
    double? CpuHottestCoreCelsius,
    IReadOnlyList<CoreTemperature> CpuCores,
    double? CpuLoadPercent,
    double? GpuCelsius,
    double? GpuLoadPercent,
    double? GpuFanPercent,
    double? GpuPowerWatts,
    DateTimeOffset FetchedAt)
{
    public double? HottestCelsius
    {
        get
        {
            var values = new[] { CpuPackageCelsius, CpuHottestCoreCelsius, GpuCelsius }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
            return values.Length == 0 ? null : values.Max();
        }
    }
}

internal enum ThermalLevel
{
    Cool,
    Warm,
    Hot,
    Unavailable
}

internal sealed record ThermalAssessment(
    ThermalLevel Level,
    string Label,
    string Detail,
    string AccentHex,
    string SurfaceHex,
    string BorderHex)
{
    public static ThermalAssessment From(TemperatureSnapshot snapshot)
    {
        if (snapshot.HottestCelsius is null)
        {
            return new(
                ThermalLevel.Unavailable,
                "CAPTEURS INDISPONIBLES",
                "Aucune température exploitable",
                "#8C97A8",
                "#1D222B",
                "#3A424F");
        }

        var cpu = snapshot.CpuPackageCelsius ?? snapshot.CpuHottestCoreCelsius;
        var gpu = snapshot.GpuCelsius;

        if (cpu >= 90 || gpu >= 84)
        {
            return new(
                ThermalLevel.Hot,
                "CHAUD",
                "Charge thermique élevée",
                "#FF7B72",
                "#2A181A",
                "#613139");
        }

        if (cpu >= 76 || gpu >= 76)
        {
            return new(
                ThermalLevel.Warm,
                "EN CHARGE",
                "Températures soutenues",
                "#FFBE55",
                "#2A2418",
                "#66522C");
        }

        return new(
            ThermalLevel.Cool,
            "TEMPÉRATURES STABLES",
            "La machine respire correctement",
            "#55E6A5",
            "#16271F",
            "#2B5A43");
    }
}

internal static class SensorFormatting
{
    public static string Temperature(double? value) =>
        value is null ? "—" : $"{Math.Round(value.Value):0}°";

    public static string Percent(double? value) =>
        value is null ? "—" : $"{Math.Round(value.Value):0}%";

    public static string Watts(double? value) =>
        value is null ? "—" : $"{value.Value.ToString("0", CultureInfo.InvariantCulture)} W";
}
