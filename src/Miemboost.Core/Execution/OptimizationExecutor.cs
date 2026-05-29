using Miemboost.Core.Models;
using Miemboost.Core.Safety;

namespace Miemboost.Core.Execution;

public sealed class OptimizationExecutor(
    SafetyPolicy safetyPolicy,
    ISystemSnapshotFactory snapshotFactory,
    ISystemSnapshotStore snapshotStore,
    IOptimizationActionHandlerRegistry handlerRegistry)
{
    public async Task<OptimizationExecutionReport> ExecuteAsync(
        OptimizationPlan plan,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (plan.HasForbiddenActions)
        {
            throw new InvalidOperationException("Forbidden actions cannot be executed.");
        }

        var snapshot = await snapshotFactory.CreateAsync(plan, cancellationToken).ConfigureAwait(false);
        await snapshotStore.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);

        var context = new OptimizationExecutionContext(plan, snapshot);
        var results = new List<OptimizationActionResult>(plan.Actions.Count);

        foreach (var action in plan.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var decision = safetyPolicy.Evaluate(action, plan.Mode);
            if (!decision.IsAllowed)
            {
                results.Add(OptimizationActionResult.Skipped(
                    action,
                    decision.Reason ?? "Action blocked by safety policy."));
                continue;
            }

            if (!handlerRegistry.TryGetHandler(action.Id, out var handler))
            {
                results.Add(OptimizationActionResult.Skipped(action, "No handler registered for this action."));
                continue;
            }

            try
            {
                results.Add(await handler.ExecuteAsync(action, context, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(OptimizationActionResult.Failed(action, exception.Message));
            }
        }

        return new OptimizationExecutionReport(
            PlanId: plan.Id,
            SnapshotId: snapshot.Id,
            Results: results,
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow);
    }
}
