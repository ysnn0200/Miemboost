using Miemboost.Core.Diagnostics;

namespace Miemboost.Core.Processes;

public sealed class BackgroundProcessAnalyzer
{
    private const long MinimumCandidateMemoryBytes = 150L * 1024 * 1024;
    private const int MinimumCandidateEstablishedTcpConnections = 6;

    public IReadOnlyList<BackgroundProcessCandidate> FindCandidates(
        IReadOnlyList<ProcessSnapshot> processes)
    {
        return processes
            .Where(process => !process.IsProtectedCandidate)
            .Where(process =>
                process.WorkingSetBytes >= MinimumCandidateMemoryBytes
                || process.EstablishedTcpConnectionCount >= MinimumCandidateEstablishedTcpConnections)
            .Where(process => !IsMiemboostProcess(process.Name))
            .OrderByDescending(CalculateCandidateScore)
            .Take(20)
            .Select(process => new BackgroundProcessCandidate(
                ProcessId: process.ProcessId,
                Name: process.Name,
                Path: process.MainModulePath,
                WorkingSetBytes: process.WorkingSetBytes,
                IsProtectedCandidate: process.IsProtectedCandidate,
                TcpConnectionCount: process.TcpConnectionCount,
                EstablishedTcpConnectionCount: process.EstablishedTcpConnectionCount))
            .ToArray();
    }

    private static double CalculateCandidateScore(ProcessSnapshot process)
    {
        var memoryScore = process.WorkingSetBytes / (1024d * 1024d);
        var networkScore = process.EstablishedTcpConnectionCount * 60d;
        return memoryScore + networkScore;
    }

    private static bool IsMiemboostProcess(string processName)
    {
        return string.Equals(processName, "Miemboost", StringComparison.OrdinalIgnoreCase);
    }
}
