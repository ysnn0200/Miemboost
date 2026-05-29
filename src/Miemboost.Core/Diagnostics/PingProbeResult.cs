namespace Miemboost.Core.Diagnostics;

public sealed record PingProbeResult(
    string Target,
    int Sent,
    int Received,
    double PacketLossPercent,
    double AverageLatencyMs,
    double JitterMs,
    IReadOnlyList<long> LatencySamplesMs,
    DateTimeOffset CapturedAt);
