using Miemboost.Core.Execution;
using Miemboost.Core.Memory;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;

namespace Miemboost.Windows.Execution;

public sealed class StandbyMemoryReleaseActionHandler(IStandbyMemoryManager standbyMemoryManager) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.ReleaseStandbyMemory.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await standbyMemoryManager.ReleaseAsync(cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? OptimizationActionResult.Succeeded(action, result.Message)
            : OptimizationActionResult.Skipped(action, result.Message);
    }

    public Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OptimizationActionResult.Skipped(
            action,
            "Standby Memory release is a one-time Windows cache trim and has no restore operation."));
    }
}
