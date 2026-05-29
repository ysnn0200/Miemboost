using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public interface IOptimizationActionHandler
{
    string ActionId { get; }

    Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default);
}
