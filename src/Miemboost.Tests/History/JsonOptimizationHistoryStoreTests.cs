using Miemboost.Core.Execution;
using Miemboost.Core.History;
using Miemboost.Core.Models;
using Miemboost.Windows.History;

namespace Miemboost.Tests.History;

public sealed class JsonOptimizationHistoryStoreTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(
        Path.GetTempPath(),
        "Miemboost.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AddAsync_AndListRecentAsync_RoundTripsEntries()
    {
        var store = new JsonOptimizationHistoryStore(Path.Combine(_directoryPath, "history.json"));
        var entry = CreateEntry("entry-1");

        await store.AddAsync(entry);
        var entries = await store.ListRecentAsync();

        var loaded = Assert.Single(entries);
        Assert.Equal(entry.Id, loaded.Id);
        Assert.NotNull(loaded.Details);
        Assert.Single(loaded.Details);
    }

    [Fact]
    public async Task ListRecentAsync_RespectsLimit()
    {
        var store = new JsonOptimizationHistoryStore(Path.Combine(_directoryPath, "history.json"));

        await store.AddAsync(CreateEntry("entry-1"));
        await store.AddAsync(CreateEntry("entry-2"));

        var entries = await store.ListRecentAsync(limit: 1);

        Assert.Single(entries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private static OptimizationHistoryEntry CreateEntry(string id)
    {
        return new OptimizationHistoryEntry(
            Id: id,
            PlanId: "plan",
            SnapshotId: "snapshot",
            Mode: BoostMode.Balanced,
            EventType: OptimizationHistoryEventType.Boost,
            Status: OptimizationExecutionStatus.Succeeded,
            SucceededCount: 1,
            SkippedCount: 0,
            FailedCount: 0,
            CreatedAt: DateTimeOffset.UnixEpoch,
            Details:
            [
                new OptimizationHistoryActionDetail(
                    ActionId: "action",
                    Kind: OptimizationActionKind.Diagnostics,
                    Status: OptimizationExecutionStatus.Succeeded,
                    Message: "ok",
                    CompletedAt: DateTimeOffset.UnixEpoch)
            ]);
    }
}
