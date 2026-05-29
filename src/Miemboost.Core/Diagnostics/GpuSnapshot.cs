namespace Miemboost.Core.Diagnostics;

public sealed record GpuSnapshot(
    double UsagePercent,
    bool IsAvailable,
    string Source,
    DateTimeOffset CapturedAt);
