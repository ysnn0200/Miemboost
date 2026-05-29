namespace Miemboost.Core.Diagnostics;

public sealed record CpuSnapshot(
    double UsagePercent,
    int LogicalProcessorCount,
    DateTimeOffset CapturedAt);
