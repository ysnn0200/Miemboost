namespace Miemboost.Core.Diagnostics;

public sealed record DnsProbeResult(
    string HostName,
    bool Succeeded,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Addresses,
    string? ErrorMessage,
    DateTimeOffset CapturedAt);
