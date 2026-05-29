using Miemboost.Core.Games;

namespace Miemboost.Tests.Games;

public sealed class GameProfileUpdaterTests
{
    [Fact]
    public void AddAllowedBackgroundProcess_AddsProcessName()
    {
        var profile = GameProfileFactory.Create("Valorant", "C:\\Games\\VALORANT.exe");

        var updated = GameProfileUpdater.AddAllowedBackgroundProcess(profile, "Discord");

        Assert.Contains("Discord", updated.AllowedBackgroundProcessNames);
    }

    [Fact]
    public void AddAllowedBackgroundProcess_DeduplicatesNamesIgnoringCase()
    {
        var profile = GameProfileFactory.Create("Valorant", "C:\\Games\\VALORANT.exe");

        var updated = GameProfileUpdater.AddAllowedBackgroundProcess(profile, "Discord");
        updated = GameProfileUpdater.AddAllowedBackgroundProcess(updated, "discord");

        Assert.Single(updated.AllowedBackgroundProcessNames);
    }
}
