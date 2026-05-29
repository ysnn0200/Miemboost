using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Safety;

namespace Miemboost.Tests.Execution;

public sealed class OptimizationExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SavesSnapshotBeforeExecutingActions()
    {
        var action = DefaultActionCatalog.PowerPlanSwitch;
        var plan = CreatePlan([action]);
        var handler = new RecordingHandler(action.Id);
        var snapshotStore = new RecordingSnapshotStore();
        var executor = CreateExecutor(handler, snapshotStore);

        var report = await executor.ExecuteAsync(plan);

        Assert.True(snapshotStore.WasSaved);
        Assert.True(handler.WasExecuted);
        Assert.Equal(snapshotStore.SavedSnapshotId, report.SnapshotId);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsActionWithoutRegisteredHandler()
    {
        var action = DefaultActionCatalog.PowerPlanSwitch;
        var plan = CreatePlan([action]);
        var executor = CreateExecutor();

        var report = await executor.ExecuteAsync(plan);

        var result = Assert.Single(report.Results);
        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsHandlerFailureAndContinues()
    {
        var first = DefaultActionCatalog.PowerPlanSwitch;
        var second = DefaultActionCatalog.NetworkDiagnostics;
        var plan = CreatePlan([first, second]);
        var failingHandler = new FailingHandler(first.Id);
        var recordingHandler = new RecordingHandler(second.Id);
        var executor = CreateExecutor(failingHandler, recordingHandler);

        var report = await executor.ExecuteAsync(plan);

        Assert.True(report.HasFailures);
        Assert.True(recordingHandler.WasExecuted);
    }

    private static OptimizationExecutor CreateExecutor(params IOptimizationActionHandler[] handlers)
    {
        return CreateExecutor(new RecordingSnapshotStore(), handlers);
    }

    private static OptimizationExecutor CreateExecutor(
        RecordingSnapshotStore snapshotStore,
        params IOptimizationActionHandler[] handlers)
    {
        return new OptimizationExecutor(
            new SafetyPolicy(),
            new StubSnapshotFactory(),
            snapshotStore,
            new OptimizationActionHandlerRegistry(handlers));
    }

    private static OptimizationExecutor CreateExecutor(
        IOptimizationActionHandler handler,
        RecordingSnapshotStore snapshotStore)
    {
        return CreateExecutor(snapshotStore, handler);
    }

    private static OptimizationPlan CreatePlan(IReadOnlyList<OptimizationActionDescriptor> actions)
    {
        return new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: actions,
            CreatedAt: DateTimeOffset.UnixEpoch);
    }

    private sealed class StubSnapshotFactory : ISystemSnapshotFactory
    {
        public Task<SystemSnapshot> CreateAsync(
            OptimizationPlan plan,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SystemSnapshot(
                Id: "snapshot",
                PlanId: plan.Id,
                PreviousPowerPlanId: null,
                ProcessPriorities: [],
                PausedProcesses: [],
                CreatedAt: DateTimeOffset.UnixEpoch));
        }
    }

    private sealed class RecordingSnapshotStore : ISystemSnapshotStore
    {
        public bool WasSaved { get; private set; }

        public string? SavedSnapshotId { get; private set; }

        public Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            WasSaved = true;
            SavedSnapshotId = snapshot.Id;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler(string actionId) : IOptimizationActionHandler
    {
        public string ActionId { get; } = actionId;

        public bool WasExecuted { get; private set; }

        public Task<OptimizationActionResult> ExecuteAsync(
            OptimizationActionDescriptor action,
            OptimizationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.FromResult(OptimizationActionResult.Succeeded(action, "Executed."));
        }

        public Task<OptimizationActionResult> RestoreAsync(
            OptimizationActionDescriptor action,
            OptimizationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OptimizationActionResult.Succeeded(action, "Restored."));
        }
    }

    private sealed class FailingHandler(string actionId) : IOptimizationActionHandler
    {
        public string ActionId { get; } = actionId;

        public Task<OptimizationActionResult> ExecuteAsync(
            OptimizationActionDescriptor action,
            OptimizationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Expected failure.");
        }

        public Task<OptimizationActionResult> RestoreAsync(
            OptimizationActionDescriptor action,
            OptimizationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OptimizationActionResult.Succeeded(action, "Restored."));
        }
    }
}
