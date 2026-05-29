using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public sealed record OptimizationActionResult(
    string ActionId,
    OptimizationActionKind Kind,
    OptimizationExecutionStatus Status,
    string Message,
    DateTimeOffset CompletedAt)
{
    public static OptimizationActionResult Succeeded(OptimizationActionDescriptor action, string message)
    {
        return new OptimizationActionResult(
            action.Id,
            action.Kind,
            OptimizationExecutionStatus.Succeeded,
            message,
            DateTimeOffset.UtcNow);
    }

    public static OptimizationActionResult Skipped(OptimizationActionDescriptor action, string message)
    {
        return new OptimizationActionResult(
            action.Id,
            action.Kind,
            OptimizationExecutionStatus.Skipped,
            message,
            DateTimeOffset.UtcNow);
    }

    public static OptimizationActionResult Failed(OptimizationActionDescriptor action, string message)
    {
        return new OptimizationActionResult(
            action.Id,
            action.Kind,
            OptimizationExecutionStatus.Failed,
            message,
            DateTimeOffset.UtcNow);
    }
}
