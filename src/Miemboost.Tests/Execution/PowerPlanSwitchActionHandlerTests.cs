using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Power;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class PowerPlanSwitchActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_SwitchesToHighPerformanceAlias()
    {
        var manager = new RecordingPowerPlanManager();
        var handler = new PowerPlanSwitchActionHandler(manager);
        var action = DefaultActionCatalog.PowerPlanSwitch;

        var result = await handler.ExecuteAsync(action, CreateContext(previousPowerPlanId: "balanced"));

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(KnownPowerPlans.HighPerformanceAlias, manager.LastSetPlanId);
    }

    [Fact]
    public async Task RestoreAsync_RestoresPreviousPowerPlanFromSnapshot()
    {
        var manager = new RecordingPowerPlanManager();
        var handler = new PowerPlanSwitchActionHandler(manager);
        var action = DefaultActionCatalog.PowerPlanSwitch;

        var result = await handler.RestoreAsync(action, CreateContext(previousPowerPlanId: "balanced"));

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal("balanced", manager.LastSetPlanId);
    }

    [Fact]
    public async Task RestoreAsync_SkipsWhenSnapshotHasNoPreviousPowerPlan()
    {
        var manager = new RecordingPowerPlanManager();
        var handler = new PowerPlanSwitchActionHandler(manager);
        var action = DefaultActionCatalog.PowerPlanSwitch;

        var result = await handler.RestoreAsync(action, CreateContext(previousPowerPlanId: null));

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
        Assert.Null(manager.LastSetPlanId);
    }

    private static OptimizationExecutionContext CreateContext(string? previousPowerPlanId)
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.PowerPlanSwitch],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = new SystemSnapshot(
            Id: "snapshot",
            PlanId: plan.Id,
            PreviousPowerPlanId: previousPowerPlanId,
            ProcessPriorities: [],
            ServiceStates: [],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);

        return new OptimizationExecutionContext(plan, snapshot);
    }

    private sealed class RecordingPowerPlanManager : IPowerPlanManager
    {
        public string? LastSetPlanId { get; private set; }

        public Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PowerPlanInfo?>(new PowerPlanInfo("balanced", "Balanced"));
        }

        public Task SetActivePlanAsync(string planId, CancellationToken cancellationToken = default)
        {
            LastSetPlanId = planId;
            return Task.CompletedTask;
        }
    }
}
