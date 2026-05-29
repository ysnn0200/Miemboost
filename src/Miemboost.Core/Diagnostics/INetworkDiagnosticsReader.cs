namespace Miemboost.Core.Diagnostics;

public interface INetworkDiagnosticsReader
{
    Task<PingProbeResult> ProbePingAsync(
        string target,
        int sampleCount = 4,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<DnsProbeResult> ProbeDnsAsync(
        string hostName,
        CancellationToken cancellationToken = default);
}
