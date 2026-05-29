namespace Miemboost.Core.Diagnostics;

public sealed record DiagnosticsReport(
    SystemDiagnosticsSnapshot System,
    PingProbeResult? Ping,
    DnsProbeResult? Dns,
    DiagnosticsSummary Summary,
    TimeSpan Elapsed,
    DateTimeOffset CapturedAt);
