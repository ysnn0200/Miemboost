using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public sealed record OptimizationExecutionReport(
    string PlanId,
    string SnapshotId,
    IReadOnlyList<OptimizationActionResult> Results,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public bool HasFailures => Results.Any(result => result.Status == OptimizationExecutionStatus.Failed);

    public bool HasExecutedActions => Results.Any(result => result.Status == OptimizationExecutionStatus.Succeeded);
}
