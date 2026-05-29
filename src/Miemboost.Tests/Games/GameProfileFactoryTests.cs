using Miemboost.Core.Games;
using Miemboost.Core.Models;

namespace Miemboost.Tests.Games;

public sealed class GameProfileFactoryTests
{
    [Fact]
    public void Create_DefaultsToAutoRestore()
    {
        var profile = GameProfileFactory.Create("Valorant", "C:\\Games\\VALORANT.exe");

        Assert.Equal("Valorant", profile.Name);
        Assert.Equal(BoostMode.Balanced, profile.RecommendedMode);
        Assert.True(profile.AutoRestoreOnExit);
    }
}
