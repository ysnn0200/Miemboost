using Miemboost.Core.Diagnostics;

namespace Miemboost.Tests.Diagnostics;

public sealed class DiagnosticsServiceTests
{
    [Fact]
    public async Task CaptureAsync_ReturnsReportWithSummary()
    {
        var service = new DiagnosticsService(
            new StubSystemDiagnosticsReader(CreateSystemSnapshot()),
            new StubNetworkDiagnosticsReader());

        var report = await service.CaptureAsync();

        Assert.NotNull(report.System);
        Assert.NotNull(report.Ping);
        Assert.NotNull(report.Dns);
        Assert.Equal(DiagnosticSeverity.Good, report.Summary.OverallSeverity);
    }

    [Fact]
    public async Task CaptureAsync_SkipsNetworkProbesWhenDisabled()
    {
        var networkReader = new StubNetworkDiagnosticsReader();
        var service = new DiagnosticsService(
            new StubSystemDiagnosticsReader(CreateSystemSnapshot()),
            networkReader);

        var report = await service.CaptureAsync(new DiagnosticsCaptureOptions(
            PingTarget: null,
            DnsHostName: null));

        Assert.Null(report.Ping);
        Assert.Null(report.Dns);
        Assert.Equal(0, networkReader.PingCalls);
        Assert.Equal(0, networkReader.DnsCalls);
    }

    private static SystemDiagnosticsSnapshot CreateSystemSnapshot()
    {
        return new SystemDiagnosticsSnapshot(
            Cpu: new CpuSnapshot(12, 8, DateTimeOffset.UnixEpoch),
            Memory: new MemorySnapshot(100, 60, DateTimeOffset.UnixEpoch),
            Processes: [],
            NetworkAdapters:
            [
                new NetworkAdapterSnapshot(
                    Id: "adapter",
                    Name: "Ethernet",
                    Description: "Ethernet",
                    Type: "Ethernet",
                    IsUp: true,
                    SpeedBitsPerSecond: 1_000_000_000,
                    BytesReceived: 0,
                    BytesSent: 0)
            ],
            CapturedAt: DateTimeOffset.UnixEpoch);
    }

    private sealed class StubSystemDiagnosticsReader(SystemDiagnosticsSnapshot snapshot) : ISystemDiagnosticsReader
    {
        public Task<SystemDiagnosticsSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StubNetworkDiagnosticsReader : INetworkDiagnosticsReader
    {
        public int PingCalls { get; private set; }

        public int DnsCalls { get; private set; }

        public Task<PingProbeResult> ProbePingAsync(
            string target,
            int sampleCount = 4,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            PingCalls++;

            return Task.FromResult(new PingProbeResult(
                Target: target,
                Sent: sampleCount,
                Received: sampleCount,
                PacketLossPercent: 0,
                AverageLatencyMs: 20,
                JitterMs: 2,
                LatencySamplesMs: [18, 20, 22, 20],
                CapturedAt: DateTimeOffset.UnixEpoch));
        }

        public Task<DnsProbeResult> ProbeDnsAsync(
            string hostName,
            CancellationToken cancellationToken = default)
        {
            DnsCalls++;

            return Task.FromResult(new DnsProbeResult(
                HostName: hostName,
                Succeeded: true,
                ElapsedMilliseconds: 25,
                Addresses: ["127.0.0.1"],
                ErrorMessage: null,
                CapturedAt: DateTimeOffset.UnixEpoch));
        }
    }
}
