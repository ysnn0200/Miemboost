using Miemboost.Core.Diagnostics;
using Miemboost.Core.Processes;

namespace Miemboost.Tests.Processes;

public sealed class BackgroundProcessAnalyzerTests
{
    [Fact]
    public void FindCandidates_ExcludesProtectedProcesses()
    {
        var analyzer = new BackgroundProcessAnalyzer();

        var candidates = analyzer.FindCandidates(
        [
            CreateProcess("vgc", 900, isProtected: true),
            CreateProcess("Discord", 400, isProtected: false)
        ]);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Discord", candidate.Name);
    }

    [Fact]
    public void FindCandidates_ExcludesSmallProcesses()
    {
        var analyzer = new BackgroundProcessAnalyzer();

        var candidates = analyzer.FindCandidates([CreateProcess("Tiny", 20, isProtected: false)]);

        Assert.Empty(candidates);
    }

    private static ProcessSnapshot CreateProcess(
        string name,
        long memoryMb,
        bool isProtected)
    {
        return new ProcessSnapshot(
            ProcessId: Random.Shared.Next(100, 10000),
            Name: name,
            MainModulePath: null,
            WorkingSetBytes: memoryMb * 1024 * 1024,
            TotalProcessorTime: TimeSpan.Zero,
            IsProtectedCandidate: isProtected);
    }
}
