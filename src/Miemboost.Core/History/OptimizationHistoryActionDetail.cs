using Miemboost.Core.Execution;
using Miemboost.Core.Models;

namespace Miemboost.Core.History;

public sealed record OptimizationHistoryActionDetail(
    string ActionId,
    OptimizationActionKind Kind,
    OptimizationExecutionStatus Status,
    string Message,
    DateTimeOffset CompletedAt);
