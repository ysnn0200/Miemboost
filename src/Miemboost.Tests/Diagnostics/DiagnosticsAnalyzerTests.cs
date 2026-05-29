using Miemboost.Core.Diagnostics;

namespace Miemboost.Tests.Diagnostics;

public sealed class DiagnosticsAnalyzerTests
{
    [Fact]
    public void Analyze_FlagsHighMemoryUsage()
    {
        var snapshot = CreateSnapshot(memoryUsedPercent: 90);

        var summary = new DiagnosticsAnalyzer().Analyze(snapshot);

        Assert.Equal(DiagnosticSeverity.Warning, summary.MemorySeverity);
        Assert.Contains(summary.Findings, finding => finding.Id == "memory.high-usage");
    }

    [Fact]
    public void Analyze_DoesNotRecommendProtectedProcesses()
    {
        var process = new ProcessSnapshot(
            ProcessId: 10,
            Name: "vgc",
            MainModulePath: null,
            WorkingSetBytes: 2L * 1024 * 1024 * 1024,
            TotalProcessorTime: TimeSpan.Zero,
            IsProtectedCandidate: true);

        var snapshot = CreateSnapshot(processes: [process]);

        var summary = new DiagnosticsAnalyzer().Analyze(snapshot);

        Assert.DoesNotContain(summary.Findings, finding => finding.RelatedProcessName == "vgc");
    }

    [Fact]
    public void Analyze_FlagsPacketLoss()
    {
        var snapshot = CreateSnapshot();
        var ping = new PingProbeResult(
            Target: "example.com",
            Sent: 4,
            Received: 3,
            PacketLossPercent: 25,
            AverageLatencyMs: 35,
            JitterMs: 5,
            LatencySamplesMs: [30, 34, 41],
            CapturedAt: DateTimeOffset.UnixEpoch);

        var summary = new DiagnosticsAnalyzer().Analyze(snapshot, ping);

        Assert.Equal(DiagnosticSeverity.Warning, summary.NetworkSeverity);
        Assert.Contains(summary.Findings, finding => finding.Id == "network.packet-loss");
    }

    [Fact]
    public void Analyze_FlagsHighBackgroundNetworkActivity()
    {
        var process = new ProcessSnapshot(
            ProcessId: 20,
            Name: "GameLauncher",
            MainModulePath: null,
            WorkingSetBytes: 100,
            TotalProcessorTime: TimeSpan.Zero,
            IsProtectedCandidate: false,
            TcpConnectionCount: 15,
            EstablishedTcpConnectionCount: 13);

        var summary = new DiagnosticsAnalyzer().Analyze(CreateSnapshot(processes: [process]));

        Assert.Contains(summary.Findings, finding => finding.Id == "process.high-network-activity");
    }

    private static SystemDiagnosticsSnapshot CreateSnapshot(
        double cpuUsagePercent = 10,
        double memoryUsedPercent = 40,
        IReadOnlyList<ProcessSnapshot>? processes = null)
    {
        const ulong totalMemory = 100;
        var availableMemory = (ulong)Math.Round(totalMemory * (100 - memoryUsedPercent) / 100);

        return new SystemDiagnosticsSnapshot(
            Cpu: new CpuSnapshot(cpuUsagePercent, 8, DateTimeOffset.UnixEpoch),
            Gpu: new GpuSnapshot(0, false, "test", DateTimeOffset.UnixEpoch),
            Memory: new MemorySnapshot(totalMemory, availableMemory, DateTimeOffset.UnixEpoch),
            Processes: processes ?? [],
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
}
