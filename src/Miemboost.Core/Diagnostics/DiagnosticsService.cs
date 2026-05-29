using System.Diagnostics;

namespace Miemboost.Core.Diagnostics;

public sealed class DiagnosticsService(
    ISystemDiagnosticsReader systemDiagnosticsReader,
    INetworkDiagnosticsReader networkDiagnosticsReader,
    DiagnosticsAnalyzer? analyzer = null) : IDiagnosticsService
{
    private readonly DiagnosticsAnalyzer _analyzer = analyzer ?? new DiagnosticsAnalyzer();

    public async Task<DiagnosticsReport> CaptureAsync(
        DiagnosticsCaptureOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var captureOptions = options ?? new DiagnosticsCaptureOptions();
        var stopwatch = Stopwatch.StartNew();

        var systemTask = systemDiagnosticsReader.CaptureAsync(cancellationToken);
        var pingTask = captureOptions.ShouldProbePing
            ? ProbePingAsync(captureOptions, cancellationToken)
            : Task.FromResult<PingProbeResult?>(null);
        var dnsTask = captureOptions.ShouldProbeDns
            ? ProbeDnsAsync(captureOptions, cancellationToken)
            : Task.FromResult<DnsProbeResult?>(null);

        await Task.WhenAll(systemTask, pingTask, dnsTask).ConfigureAwait(false);

        var system = await systemTask.ConfigureAwait(false);
        var ping = await pingTask.ConfigureAwait(false);
        var dns = await dnsTask.ConfigureAwait(false);
        var summary = _analyzer.Analyze(system, ping, dns);

        stopwatch.Stop();

        return new DiagnosticsReport(
            System: system,
            Ping: ping,
            Dns: dns,
            Summary: summary,
            Elapsed: stopwatch.Elapsed,
            CapturedAt: DateTimeOffset.UtcNow);
    }

    private async Task<PingProbeResult?> ProbePingAsync(
        DiagnosticsCaptureOptions captureOptions,
        CancellationToken cancellationToken)
    {
        return await networkDiagnosticsReader.ProbePingAsync(
            captureOptions.PingTarget!,
            captureOptions.PingSampleCount,
            captureOptions.PingTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DnsProbeResult?> ProbeDnsAsync(
        DiagnosticsCaptureOptions captureOptions,
        CancellationToken cancellationToken)
    {
        return await networkDiagnosticsReader.ProbeDnsAsync(
            captureOptions.DnsHostName!,
            cancellationToken).ConfigureAwait(false);
    }
}
