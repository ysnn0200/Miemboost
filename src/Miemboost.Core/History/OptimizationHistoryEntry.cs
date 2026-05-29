using Miemboost.Core.Execution;
using Miemboost.Core.Models;

namespace Miemboost.Core.History;

public sealed record OptimizationHistoryEntry(
    string Id,
    string PlanId,
    string? SnapshotId,
    BoostMode Mode,
    OptimizationHistoryEventType EventType,
    OptimizationExecutionStatus Status,
    int SucceededCount,
    int SkippedCount,
    int FailedCount,
    DateTimeOffset CreatedAt);
