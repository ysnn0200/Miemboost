using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class ProcessPriorityActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_SetsConfiguredProcessPriority()
    {
        var manager = new RecordingProcessPriorityManager();
        var handler = new ProcessPriorityActionHandler(manager);
        var action = DefaultActionCatalog.CreateGamePriorityHighAction(123);

        var result = await handler.ExecuteAsync(action, CreateContext(action, ManagedProcessPriority.Normal));

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(123, manager.LastProcessId);
        Assert.Equal(ManagedProcessPriority.High, manager.LastPriority);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenProcessIdIsMissing()
    {
        var manager = new RecordingProcessPriorityManager();
        var handler = new ProcessPriorityActionHandler(manager);
        var action = DefaultActionCatalog.GamePriorityHigh;

        var result = await handler.ExecuteAsync(action, CreateContext(action, ManagedProcessPriority.Normal));

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
        Assert.Null(manager.LastProcessId);
    }

    [Fact]
    public async Task RestoreAsync_RestoresPriorityFromSnapshot()
    {
        var manager = new RecordingProcessPriorityManager();
        var handler = new ProcessPriorityActionHandler(manager);
        var action = DefaultActionCatalog.CreateGamePriorityHighAction(123);

        var result = await handler.RestoreAsync(action, CreateContext(action, ManagedProcessPriority.Normal));

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(ManagedProcessPriority.Normal, manager.LastPriority);
    }

    private static OptimizationExecutionContext CreateContext(
        OptimizationActionDescriptor action,
        ManagedProcessPriority previousPriority)
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [action],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = new SystemSnapshot(
            Id: "snapshot",
            PlanId: plan.Id,
            PreviousPowerPlanId: null,
            ProcessPriorities:
            [
                new ProcessPrioritySnapshot(
                    ProcessId: 123,
                    ProcessName: "game",
                    PreviousPriorityClass: previousPriority.ToString())
            ],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);

        return new OptimizationExecutionContext(plan, snapshot);
    }

    private sealed class RecordingProcessPriorityManager : IProcessPriorityManager
    {
        public int? LastProcessId { get; private set; }

        public ManagedProcessPriority? LastPriority { get; private set; }

        public Task<ManagedProcessPriority?> GetPriorityAsync(
            int processId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManagedProcessPriority?>(ManagedProcessPriority.Normal);
        }

        public Task SetPriorityAsync(
            int processId,
            ManagedProcessPriority priority,
            CancellationToken cancellationToken = default)
        {
            LastProcessId = processId;
            LastPriority = priority;
            return Task.CompletedTask;
        }
    }
}
