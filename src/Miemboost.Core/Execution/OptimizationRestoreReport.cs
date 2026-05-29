namespace Miemboost.Core.Execution;

public sealed record OptimizationRestoreReport(
    string PlanId,
    string SnapshotId,
    IReadOnlyList<OptimizationActionResult> Results,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public bool HasFailures => Results.Any(result => result.Status == OptimizationExecutionStatus.Failed);
}
