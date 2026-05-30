using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;

namespace Miemboost.Tests.Execution;

public sealed class OptimizationRestorerTests
{
    [Fact]
    public async Task RestoreAsync_RestoresActionsInReverseOrder()
    {
        var first = DefaultActionCatalog.PowerPlanSwitch;
        var second = DefaultActionCatalog.NetworkDiagnostics;
        var calls = new List<string>();
        var restorer = new OptimizationRestorer(new OptimizationActionHandlerRegistry(
        [
            new RecordingRestoreHandler(first.Id, calls),
            new RecordingRestoreHandler(second.Id, calls)
        ]));

        var report = await restorer.RestoreAsync(CreatePlan([first, second]), CreateSnapshot());

        Assert.False(report.HasFailures);
        Assert.Equal([second.Id, first.Id], calls);
    }

    [Fact]
    public async Task RestoreAsync_SkipsActionWithoutHandler()
    {
        var action = DefaultActionCatalog.PowerPlanSwitch;
        var restorer = new OptimizationRestorer(new OptimizationActionHandlerRegistry([]));

        var report = await restorer.RestoreAsync(CreatePlan([action]), CreateSnapshot());

        var result = Assert.Single(report.Results);
        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
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

    private static SystemSnapshot CreateSnapshot()
    {
        return new SystemSnapshot(
            Id: "snapshot",
            PlanId: "plan",
            PreviousPowerPlanId: "balanced",
            ProcessPriorities: [],
            ServiceStates: [],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);
    }

    private sealed class RecordingRestoreHandler(string actionId, List<string> calls) : IOptimizationActionHandler
    {
        public string ActionId { get; } = actionId;

        public Task<OptimizationActionResult> ExecuteAsync(
            OptimizationActionDescriptor action,
            OptimizationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OptimizationActionResult.Succeeded(action, "Executed."));
        }

        public Task<OptimizationActionResult> RestoreAsync(
            OptimizationActionDescriptor action,
            OptimizationExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            calls.Add(action.Id);
            return Task.FromResult(OptimizationActionResult.Succeeded(action, "Restored."));
        }
    }
}
