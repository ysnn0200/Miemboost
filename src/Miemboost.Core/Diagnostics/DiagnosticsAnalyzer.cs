namespace Miemboost.Core.Diagnostics;

public sealed class DiagnosticsAnalyzer
{
    private const double HighCpuPercent = 85;
    private const double HighMemoryPercent = 85;
    private const long HighProcessMemoryBytes = 700L * 1024 * 1024;
    private const long NoticeProcessMemoryBytes = 350L * 1024 * 1024;
    private const int HighEstablishedTcpConnections = 12;
    private const int NoticeEstablishedTcpConnections = 6;

    public DiagnosticsSummary Analyze(
        SystemDiagnosticsSnapshot system,
        PingProbeResult? ping = null,
        DnsProbeResult? dns = null)
    {
        var findings = new List<DiagnosticFinding>();

        var cpuSeverity = AnalyzeCpu(system.Cpu, findings);
        var memorySeverity = AnalyzeMemory(system.Memory, findings);
        AnalyzeProcesses(system.Processes, findings);
        var networkSeverity = AnalyzeNetwork(system.NetworkAdapters, ping, dns, findings);

        var overall = new[]
        {
            cpuSeverity,
            memorySeverity,
            networkSeverity,
            findings.Select(finding => finding.Severity).DefaultIfEmpty(DiagnosticSeverity.Good).Max()
        }.Max();

        return new DiagnosticsSummary(
            OverallSeverity: overall,
            CpuSeverity: cpuSeverity,
            MemorySeverity: memorySeverity,
            NetworkSeverity: networkSeverity,
            Findings: findings);
    }

    private static DiagnosticSeverity AnalyzeCpu(CpuSnapshot cpu, List<DiagnosticFinding> findings)
    {
        if (cpu.UsagePercent < HighCpuPercent)
        {
            return DiagnosticSeverity.Good;
        }

        findings.Add(new DiagnosticFinding(
            Id: "cpu.high-usage",
            Title: "High CPU usage",
            Description: "CPU usage is already high before boosting. Close or lower priority for non-game workloads first.",
            Severity: DiagnosticSeverity.Warning));

        return DiagnosticSeverity.Warning;
    }

    private static DiagnosticSeverity AnalyzeMemory(MemorySnapshot memory, List<DiagnosticFinding> findings)
    {
        if (memory.UsedPercent < HighMemoryPercent)
        {
            return DiagnosticSeverity.Good;
        }

        findings.Add(new DiagnosticFinding(
            Id: "memory.high-usage",
            Title: "High memory usage",
            Description: "Memory usage is high. A user-approved background app pause list may help more than repeated memory clearing.",
            Severity: DiagnosticSeverity.Warning));

        return DiagnosticSeverity.Warning;
    }

    private static void AnalyzeProcesses(
        IReadOnlyList<ProcessSnapshot> processes,
        List<DiagnosticFinding> findings)
    {
        foreach (var process in processes.Where(process => !process.IsProtectedCandidate).Take(20))
        {
            if (process.WorkingSetBytes >= HighProcessMemoryBytes)
            {
                findings.Add(new DiagnosticFinding(
                    Id: "process.high-memory",
                    Title: "High memory background process",
                    Description: $"{process.Name} is using a large amount of memory and may be a candidate for the user-approved pause list.",
                    Severity: DiagnosticSeverity.Warning,
                    RelatedProcessName: process.Name,
                    RelatedProcessId: process.ProcessId));

                continue;
            }

            if (process.WorkingSetBytes >= NoticeProcessMemoryBytes)
            {
                findings.Add(new DiagnosticFinding(
                    Id: "process.notice-memory",
                    Title: "Noticeable memory background process",
                    Description: $"{process.Name} is using noticeable memory. Review it before adding it to any automatic pause list.",
                    Severity: DiagnosticSeverity.Notice,
                    RelatedProcessName: process.Name,
                    RelatedProcessId: process.ProcessId));
            }

            if (process.EstablishedTcpConnectionCount >= HighEstablishedTcpConnections)
            {
                findings.Add(new DiagnosticFinding(
                    Id: "process.high-network-activity",
                    Title: "High background network activity",
                    Description: $"{process.Name} has {process.EstablishedTcpConnectionCount} established TCP connections. Check for downloads, sync, updates, or launchers before playing.",
                    Severity: DiagnosticSeverity.Warning,
                    RelatedProcessName: process.Name,
                    RelatedProcessId: process.ProcessId));

                continue;
            }

            if (process.EstablishedTcpConnectionCount >= NoticeEstablishedTcpConnections)
            {
                findings.Add(new DiagnosticFinding(
                    Id: "process.notice-network-activity",
                    Title: "Noticeable background network activity",
                    Description: $"{process.Name} has {process.EstablishedTcpConnectionCount} established TCP connections. It may affect latency if it is syncing or downloading.",
                    Severity: DiagnosticSeverity.Notice,
                    RelatedProcessName: process.Name,
                    RelatedProcessId: process.ProcessId));
            }
        }
    }

    private static DiagnosticSeverity AnalyzeNetwork(
        IReadOnlyList<NetworkAdapterSnapshot> adapters,
        PingProbeResult? ping,
        DnsProbeResult? dns,
        List<DiagnosticFinding> findings)
    {
        var severity = adapters.Any(adapter => adapter.IsUp)
            ? DiagnosticSeverity.Good
            : DiagnosticSeverity.Critical;

        if (severity == DiagnosticSeverity.Critical)
        {
            findings.Add(new DiagnosticFinding(
                Id: "network.no-active-adapter",
                Title: "No active network adapter",
                Description: "No active network adapter was detected.",
                Severity: DiagnosticSeverity.Critical));
        }

        if (ping is not null)
        {
            severity = Max(severity, AnalyzePing(ping, findings));
        }

        if (dns is not null)
        {
            severity = Max(severity, AnalyzeDns(dns, findings));
        }

        return severity;
    }

    private static DiagnosticSeverity AnalyzePing(PingProbeResult ping, List<DiagnosticFinding> findings)
    {
        if (ping.PacketLossPercent >= 5)
        {
            findings.Add(new DiagnosticFinding(
                Id: "network.packet-loss",
                Title: "Packet loss detected",
                Description: "Packet loss is high enough to cause unstable gameplay. Check Wi-Fi quality and background downloads first.",
                Severity: DiagnosticSeverity.Warning));

            return DiagnosticSeverity.Warning;
        }

        if (ping.JitterMs >= 20)
        {
            findings.Add(new DiagnosticFinding(
                Id: "network.high-jitter",
                Title: "High network jitter",
                Description: "Jitter is high. This usually hurts competitive games more than average latency.",
                Severity: DiagnosticSeverity.Warning));

            return DiagnosticSeverity.Warning;
        }

        if (ping.AverageLatencyMs >= 100)
        {
            findings.Add(new DiagnosticFinding(
                Id: "network.high-latency",
                Title: "High latency",
                Description: "Average latency is high for real-time games. Prefer wired networking or a closer server if available.",
                Severity: DiagnosticSeverity.Notice));

            return DiagnosticSeverity.Notice;
        }

        return DiagnosticSeverity.Good;
    }

    private static DiagnosticSeverity AnalyzeDns(DnsProbeResult dns, List<DiagnosticFinding> findings)
    {
        if (!dns.Succeeded)
        {
            findings.Add(new DiagnosticFinding(
                Id: "network.dns-failed",
                Title: "DNS lookup failed",
                Description: "DNS lookup failed. Refreshing DNS cache or changing DNS provider may help.",
                Severity: DiagnosticSeverity.Warning));

            return DiagnosticSeverity.Warning;
        }

        if (dns.ElapsedMilliseconds >= 250)
        {
            findings.Add(new DiagnosticFinding(
                Id: "network.slow-dns",
                Title: "Slow DNS response",
                Description: "DNS response is slow. DNS tuning may improve launchers and server discovery, but it will not reduce in-match latency by itself.",
                Severity: DiagnosticSeverity.Notice));

            return DiagnosticSeverity.Notice;
        }

        return DiagnosticSeverity.Good;
    }

    private static DiagnosticSeverity Max(DiagnosticSeverity left, DiagnosticSeverity right)
    {
        return left > right ? left : right;
    }
}
