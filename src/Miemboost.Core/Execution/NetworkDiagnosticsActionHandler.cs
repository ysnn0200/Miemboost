using Miemboost.Core.Diagnostics;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;

namespace Miemboost.Core.Execution;

public sealed class NetworkDiagnosticsActionHandler(IDiagnosticsService diagnosticsService) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.NetworkDiagnostics.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var report = await diagnosticsService.CaptureAsync(new DiagnosticsCaptureOptions(
            PingTarget: "1.1.1.1",
            DnsHostName: "www.github.com",
            PingSampleCount: 4,
            PingTimeout: TimeSpan.FromMilliseconds(900)), cancellationToken).ConfigureAwait(false);

        var pingText = report.Ping is null
            ? "Ping unavailable"
            : $"Ping {report.Ping.AverageLatencyMs:0} ms, jitter {report.Ping.JitterMs:0} ms, loss {report.Ping.PacketLossPercent:0.#}%";
        var dnsText = report.Dns is null
            ? "DNS unavailable"
            : $"DNS {report.Dns.ElapsedMilliseconds} ms";

        return OptimizationActionResult.Succeeded(
            action,
            $"{pingText}; {dnsText}; {report.Summary.Findings.Count} finding(s).");
    }

    public Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OptimizationActionResult.Skipped(
            action,
            "Network diagnostics are read-only and do not require restore."));
    }
}
