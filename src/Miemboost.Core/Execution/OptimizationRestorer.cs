using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public sealed class OptimizationRestorer(IOptimizationActionHandlerRegistry handlerRegistry)
{
    public async Task<OptimizationRestoreReport> RestoreAsync(
        OptimizationPlan plan,
        SystemSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var context = new OptimizationExecutionContext(plan, snapshot);
        var results = new List<OptimizationActionResult>(plan.Actions.Count);

        foreach (var action in plan.Actions.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!action.CanRestore)
            {
                results.Add(OptimizationActionResult.Skipped(action, "This action does not have a restore operation."));
                continue;
            }

            if (!handlerRegistry.TryGetHandler(action.Id, out var handler))
            {
                results.Add(OptimizationActionResult.Skipped(action, "No handler registered for this action."));
                continue;
            }

            try
            {
                results.Add(await handler.RestoreAsync(action, context, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(OptimizationActionResult.Failed(action, exception.Message));
            }
        }

        return new OptimizationRestoreReport(
            PlanId: plan.Id,
            SnapshotId: snapshot.Id,
            Results: results,
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow);
    }
}
