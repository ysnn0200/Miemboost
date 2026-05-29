using Miemboost.Core.Diagnostics;
using Miemboost.Core.Games;
using Miemboost.Core.Models;

namespace Miemboost.Tests.Games;

public sealed class GameProfileMatcherTests
{
    [Fact]
    public void FindRunningMatch_MatchesByExecutablePath()
    {
        var profile = GameProfileFactory.Create("Valorant", "C:\\Games\\VALORANT.exe");
        var process = CreateProcess("VALORANT", "C:\\Games\\VALORANT.exe", isProtected: false);

        var match = new GameProfileMatcher().FindRunningMatch([profile], [process]);

        Assert.NotNull(match);
        Assert.Equal(process.ProcessId, match.ProcessId);
    }

    [Fact]
    public void FindRunningMatch_DoesNotMatchProtectedProcess()
    {
        var profile = GameProfileFactory.Create("vgc", "C:\\Riot\\vgc.exe");
        var process = CreateProcess("vgc", "C:\\Riot\\vgc.exe", isProtected: true);

        var match = new GameProfileMatcher().FindRunningMatch([profile], [process]);

        Assert.Null(match);
    }

    private static ProcessSnapshot CreateProcess(
        string name,
        string path,
        bool isProtected)
    {
        return new ProcessSnapshot(
            ProcessId: 123,
            Name: name,
            MainModulePath: path,
            WorkingSetBytes: 100,
            TotalProcessorTime: TimeSpan.Zero,
            IsProtectedCandidate: isProtected);
    }
}
