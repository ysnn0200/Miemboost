namespace Miemboost.Core.Diagnostics;

public sealed record CpuSnapshot(
    double UsagePercent,
    int LogicalProcessorCount,
    DateTimeOffset CapturedAt,
    double? TemperatureCelsius = null,
    double? CurrentClockMHz = null,
    double? MaxClockMHz = null);
