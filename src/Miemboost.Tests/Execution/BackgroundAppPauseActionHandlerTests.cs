using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class BackgroundAppPauseActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LowersPriorityForApprovedProcesses()
    {
        var manager = new RecordingProcessPriorityManager();
        var handler = new BackgroundAppPauseActionHandler(manager);
        var action = DefaultActionCatalog.CreatePauseApprovedBackgroundAppsAction([10, 11]);

        var result = await handler.ExecuteAsync(action, CreateContext(action));

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(2, manager.SetCalls.Count);
        Assert.Equal(10, manager.SetCalls[0].ProcessId);
        Assert.Equal(11, manager.SetCalls[1].ProcessId);
        Assert.All(manager.SetCalls, call => Assert.Equal(ManagedProcessPriority.BelowNormal, call.Priority));
    }

    [Fact]
    public async Task RestoreAsync_RestoresPrioritiesFromSnapshot()
    {
        var manager = new RecordingProcessPriorityManager();
        var handler = new BackgroundAppPauseActionHandler(manager);
        var action = DefaultActionCatalog.CreatePauseApprovedBackgroundAppsAction([10]);

        var result = await handler.RestoreAsync(action, CreateContext(action));

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        var call = Assert.Single(manager.SetCalls);
        Assert.Equal(ManagedProcessPriority.Normal, call.Priority);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenProcessNameNoLongerMatches()
    {
        var manager = new RecordingProcessPriorityManager();
        manager.ProcessNames[10] = "Browser";
        var handler = new BackgroundAppPauseActionHandler(manager);
        var action = DefaultActionCatalog.CreatePauseApprovedBackgroundAppsAction(
        [
            new Miemboost.Core.Diagnostics.ProcessSnapshot(
                ProcessId: 10,
                Name: "Discord",
                MainModulePath: null,
                WorkingSetBytes: 100,
                TotalProcessorTime: TimeSpan.Zero,
                IsProtectedCandidate: false)
        ]);

        var result = await handler.ExecuteAsync(action, CreateContext(action));

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
        Assert.Empty(manager.SetCalls);
    }


    private static OptimizationExecutionContext CreateContext(OptimizationActionDescriptor action)
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: "game",
            Actions: [action],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = new SystemSnapshot(
            Id: "snapshot",
            PlanId: plan.Id,
            PreviousPowerPlanId: null,
            ProcessPriorities:
            [
                new ProcessPrioritySnapshot(10, "Discord", ManagedProcessPriority.Normal.ToString()),
                new ProcessPrioritySnapshot(11, "Steam", ManagedProcessPriority.AboveNormal.ToString())
            ],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);

        return new OptimizationExecutionContext(plan, snapshot);
    }

    private sealed class RecordingProcessPriorityManager : IProcessPriorityManager
    {
        public List<(int ProcessId, ManagedProcessPriority Priority)> SetCalls { get; } = [];

        public Dictionary<int, string> ProcessNames { get; } = new()
        {
            [10] = "Discord",
            [11] = "Steam"
        };

        public Task<string?> GetProcessNameAsync(
            int processId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProcessNames.GetValueOrDefault(processId));
        }

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
            SetCalls.Add((processId, priority));
            return Task.CompletedTask;
        }
    }
}
