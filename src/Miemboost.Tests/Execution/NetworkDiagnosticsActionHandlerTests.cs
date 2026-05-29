using Miemboost.Core.Diagnostics;
using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;

namespace Miemboost.Tests.Execution;

public sealed class NetworkDiagnosticsActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_RunsReadOnlyDiagnostics()
    {
        var diagnosticsService = new StubDiagnosticsService();
        var handler = new NetworkDiagnosticsActionHandler(diagnosticsService);

        var result = await handler.ExecuteAsync(DefaultActionCatalog.NetworkDiagnostics, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.True(diagnosticsService.WasCaptured);
        Assert.Contains("Ping 24 ms", result.Message);
        Assert.Contains("DNS 18 ms", result.Message);
    }

    [Fact]
    public async Task RestoreAsync_SkipsBecauseDiagnosticsAreReadOnly()
    {
        var handler = new NetworkDiagnosticsActionHandler(new StubDiagnosticsService());

        var result = await handler.RestoreAsync(DefaultActionCatalog.NetworkDiagnostics, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
    }

    private static OptimizationExecutionContext CreateContext()
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.NetworkDiagnostics],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = new SystemSnapshot(
            Id: "snapshot",
            PlanId: plan.Id,
            PreviousPowerPlanId: null,
            ProcessPriorities: [],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);

        return new OptimizationExecutionContext(plan, snapshot);
    }

    private sealed class StubDiagnosticsService : IDiagnosticsService
    {
        public bool WasCaptured { get; private set; }

        public Task<DiagnosticsReport> CaptureAsync(
            DiagnosticsCaptureOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            WasCaptured = true;

            var system = new SystemDiagnosticsSnapshot(
                Cpu: new CpuSnapshot(12, 8, DateTimeOffset.UnixEpoch),
                Gpu: new GpuSnapshot(0, false, "test", DateTimeOffset.UnixEpoch),
                Memory: new MemorySnapshot(100, 60, DateTimeOffset.UnixEpoch),
                Processes: [],
                NetworkAdapters: [],
                CapturedAt: DateTimeOffset.UnixEpoch);
            var ping = new PingProbeResult("1.1.1.1", 4, 4, 0, 24, 3, [23, 24, 25, 24], DateTimeOffset.UnixEpoch);
            var dns = new DnsProbeResult("www.github.com", true, 18, ["127.0.0.1"], null, DateTimeOffset.UnixEpoch);
            var summary = new DiagnosticsSummary(
                OverallSeverity: DiagnosticSeverity.Good,
                CpuSeverity: DiagnosticSeverity.Good,
                MemorySeverity: DiagnosticSeverity.Good,
                NetworkSeverity: DiagnosticSeverity.Good,
                Findings: []);

            return Task.FromResult(new DiagnosticsReport(
                System: system,
                Ping: ping,
                Dns: dns,
                Summary: summary,
                Elapsed: TimeSpan.FromMilliseconds(42),
                CapturedAt: DateTimeOffset.UnixEpoch));
        }
    }
}
