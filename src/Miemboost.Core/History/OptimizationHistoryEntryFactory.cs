using Miemboost.Core.Execution;
using Miemboost.Core.Models;

namespace Miemboost.Core.History;

public static class OptimizationHistoryEntryFactory
{
    public static OptimizationHistoryEntry FromExecution(
        OptimizationExecutionReport report,
        BoostMode mode)
    {
        return Create(
            planId: report.PlanId,
            snapshotId: report.SnapshotId,
            mode: mode,
            eventType: OptimizationHistoryEventType.Boost,
            results: report.Results);
    }

    public static OptimizationHistoryEntry FromRestore(
        OptimizationRestoreReport report,
        BoostMode mode)
    {
        return Create(
            planId: report.PlanId,
            snapshotId: report.SnapshotId,
            mode: mode,
            eventType: OptimizationHistoryEventType.Restore,
            results: report.Results);
    }

    private static OptimizationHistoryEntry Create(
        string planId,
        string? snapshotId,
        BoostMode mode,
        OptimizationHistoryEventType eventType,
        IReadOnlyList<OptimizationActionResult> results)
    {
        var failedCount = results.Count(result => result.Status == OptimizationExecutionStatus.Failed);
        var succeededCount = results.Count(result => result.Status == OptimizationExecutionStatus.Succeeded);
        var skippedCount = results.Count(result => result.Status == OptimizationExecutionStatus.Skipped);

        return new OptimizationHistoryEntry(
            Id: Guid.NewGuid().ToString("N"),
            PlanId: planId,
            SnapshotId: snapshotId,
            Mode: mode,
            EventType: eventType,
            Status: failedCount > 0 ? OptimizationExecutionStatus.Failed : OptimizationExecutionStatus.Succeeded,
            SucceededCount: succeededCount,
            SkippedCount: skippedCount,
            FailedCount: failedCount,
            CreatedAt: DateTimeOffset.UtcNow,
            Details: results
                .Select(result => new OptimizationHistoryActionDetail(
                    ActionId: result.ActionId,
                    Kind: result.Kind,
                    Status: result.Status,
                    Message: result.Message,
                    CompletedAt: result.CompletedAt))
                .ToArray());
    }
}
