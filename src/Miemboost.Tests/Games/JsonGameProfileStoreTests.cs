using Miemboost.Core.Games;
using Miemboost.Windows.Games;

namespace Miemboost.Tests.Games;

public sealed class JsonGameProfileStoreTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        "Miemboost.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_AndListAsync_RoundTripsProfile()
    {
        var store = new JsonGameProfileStore(Path.Combine(_directoryPath, "games.json"));
        var profile = GameProfileFactory.Create("Valorant", "C:\\Games\\VALORANT.exe");

        await store.SaveAsync(profile);
        var profiles = await store.ListAsync();

        var loaded = Assert.Single(profiles);
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.Name, loaded.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProfile()
    {
        var store = new JsonGameProfileStore(Path.Combine(_directoryPath, "games.json"));
        var profile = GameProfileFactory.Create("Valorant", "C:\\Games\\VALORANT.exe");

        await store.SaveAsync(profile);
        await store.DeleteAsync(profile.Id);

        Assert.Empty(await store.ListAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }
}
