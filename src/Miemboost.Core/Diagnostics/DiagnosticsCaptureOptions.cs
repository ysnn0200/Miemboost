namespace Miemboost.Core.Diagnostics;

public sealed record DiagnosticsCaptureOptions(
    string? PingTarget = "1.1.1.1",
    string? DnsHostName = "www.github.com",
    int PingSampleCount = 4,
    TimeSpan? PingTimeout = null)
{
    public bool ShouldProbePing => !string.IsNullOrWhiteSpace(PingTarget) && PingSampleCount > 0;

    public bool ShouldProbeDns => !string.IsNullOrWhiteSpace(DnsHostName);
}
