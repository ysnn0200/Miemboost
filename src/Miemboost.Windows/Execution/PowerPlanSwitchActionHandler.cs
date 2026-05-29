using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Power;

namespace Miemboost.Windows.Execution;

public sealed class PowerPlanSwitchActionHandler(IPowerPlanManager powerPlanManager) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.PowerPlanSwitch.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await powerPlanManager
            .SetActivePlanAsync(KnownPowerPlans.HighPerformanceAlias, cancellationToken)
            .ConfigureAwait(false);

        return OptimizationActionResult.Succeeded(
            action,
            "Switched to the Windows high performance power plan.");
    }

    public async Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Snapshot.PreviousPowerPlanId))
        {
            return OptimizationActionResult.Skipped(
                action,
                "No previous power plan was captured in the snapshot.");
        }

        await powerPlanManager
            .SetActivePlanAsync(context.Snapshot.PreviousPowerPlanId, cancellationToken)
            .ConfigureAwait(false);

        return OptimizationActionResult.Succeeded(
            action,
            "Restored the previous Windows power plan.");
    }
}
