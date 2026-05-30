using Miemboost.Core.Models;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class JsonSystemSnapshotStoreTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        "Miemboost.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_AndGetAsync_RoundTripsSnapshot()
    {
        var store = new JsonSystemSnapshotStore(_directoryPath);
        var snapshot = CreateSnapshot("snapshot-1");

        await store.SaveAsync(snapshot);
        var loaded = await store.GetAsync(snapshot.Id);

        Assert.NotNull(loaded);
        Assert.Equal(snapshot.Id, loaded.Id);
        Assert.Equal(snapshot.PlanId, loaded.PlanId);
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsSavedSnapshots()
    {
        var store = new JsonSystemSnapshotStore(_directoryPath);

        await store.SaveAsync(CreateSnapshot("snapshot-1"));
        await store.SaveAsync(CreateSnapshot("snapshot-2"));

        var snapshots = await store.ListRecentAsync();

        Assert.Equal(2, snapshots.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private static SystemSnapshot CreateSnapshot(string id)
    {
        return new SystemSnapshot(
            Id: id,
            PlanId: "plan",
            PreviousPowerPlanId: "power-plan",
            ProcessPriorities:
            [
                new ProcessPrioritySnapshot(
                    ProcessId: 123,
                    ProcessName: "game",
                    PreviousPriorityClass: "Normal")
            ],
            ServiceStates: [],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);
    }
}
