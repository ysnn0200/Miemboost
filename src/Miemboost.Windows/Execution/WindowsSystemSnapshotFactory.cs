using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Power;

namespace Miemboost.Windows.Execution;

public sealed class WindowsSystemSnapshotFactory(IPowerPlanManager powerPlanManager) : ISystemSnapshotFactory
{
    public async Task<SystemSnapshot> CreateAsync(
        OptimizationPlan plan,
        CancellationToken cancellationToken = default)
    {
        var shouldCapturePowerPlan = plan.Actions.Any(action =>
            action.Kind == OptimizationActionKind.PowerPlanSwitch);

        var activePowerPlan = shouldCapturePowerPlan
            ? await powerPlanManager.GetActivePlanAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return new SystemSnapshot(
            Id: Guid.NewGuid().ToString("N"),
            PlanId: plan.Id,
            PreviousPowerPlanId: activePowerPlan?.Id,
            ProcessPriorities: [],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UtcNow);
    }
}
