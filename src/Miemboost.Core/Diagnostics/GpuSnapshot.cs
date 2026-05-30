namespace Miemboost.Core.Diagnostics;

public sealed record GpuSnapshot(
    double UsagePercent,
    bool IsAvailable,
    string Source,
    DateTimeOffset CapturedAt,
    double? TemperatureCelsius = null,
    double? DedicatedMemoryUsedBytes = null,
    double? DedicatedMemoryTotalBytes = null,
    double? CoreClockMHz = null,
    double? MemoryClockMHz = null);
