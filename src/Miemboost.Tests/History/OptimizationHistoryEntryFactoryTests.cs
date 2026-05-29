using Miemboost.Core.Execution;
using Miemboost.Core.History;
using Miemboost.Core.Models;

namespace Miemboost.Tests.History;

public sealed class OptimizationHistoryEntryFactoryTests
{
    [Fact]
    public void FromExecution_CountsResultStatuses()
    {
        var action = new OptimizationActionDescriptor(
            "action",
            "Action",
            "Description",
            OptimizationActionKind.Diagnostics,
            RiskLevel.Safe,
            RequiresElevation: false,
            CanRestore: true);
        var report = new OptimizationExecutionReport(
            PlanId: "plan",
            SnapshotId: "snapshot",
            Results:
            [
                OptimizationActionResult.Succeeded(action, "ok"),
                OptimizationActionResult.Skipped(action, "skip")
            ],
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch);

        var entry = OptimizationHistoryEntryFactory.FromExecution(report, BoostMode.Balanced);

        Assert.Equal(OptimizationHistoryEventType.Boost, entry.EventType);
        Assert.Equal(1, entry.SucceededCount);
        Assert.Equal(1, entry.SkippedCount);
        Assert.Equal(0, entry.FailedCount);
        Assert.Equal(OptimizationExecutionStatus.Succeeded, entry.Status);
        Assert.NotNull(entry.Details);
        Assert.Equal(2, entry.Details.Count);
        Assert.Contains(entry.Details, detail => detail.ActionId == "action" && detail.Message == "skip");
    }
}
