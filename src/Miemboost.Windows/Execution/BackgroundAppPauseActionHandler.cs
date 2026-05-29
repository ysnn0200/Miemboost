using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;

namespace Miemboost.Windows.Execution;

public sealed class BackgroundAppPauseActionHandler(IProcessPriorityManager processPriorityManager) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.PauseApprovedBackgroundApps.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var processIds = ReadProcessIds(action);
        if (processIds.Count == 0)
        {
            return OptimizationActionResult.Skipped(action, "No approved background processes were provided.");
        }

        var changed = 0;
        foreach (var processId in processIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await processPriorityManager
                    .SetPriorityAsync(processId, ManagedProcessPriority.BelowNormal, cancellationToken)
                    .ConfigureAwait(false);
                changed++;
            }
            catch
            {
                // Individual background processes may exit between preview and execution.
            }
        }

        return changed == 0
            ? OptimizationActionResult.Skipped(action, "No approved background process priority was changed.")
            : OptimizationActionResult.Succeeded(action, $"Lowered priority for {changed} approved background process(es).");
    }

    public async Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var processIds = ReadProcessIds(action);
        if (processIds.Count == 0)
        {
            return OptimizationActionResult.Skipped(action, "No approved background processes were provided.");
        }

        var restored = 0;
        foreach (var processId in processIds)
        {
            var snapshot = context.Snapshot.ProcessPriorities
                .FirstOrDefault(item => item.ProcessId == processId);

            if (snapshot is null
                || !Enum.TryParse<ManagedProcessPriority>(
                    snapshot.PreviousPriorityClass,
                    ignoreCase: true,
                    out var previousPriority))
            {
                continue;
            }

            try
            {
                await processPriorityManager
                    .SetPriorityAsync(processId, previousPriority, cancellationToken)
                    .ConfigureAwait(false);
                restored++;
            }
            catch
            {
                // The process may already have exited, which makes restore unnecessary.
            }
        }

        return restored == 0
            ? OptimizationActionResult.Skipped(action, "No approved background process priority was restored.")
            : OptimizationActionResult.Succeeded(action, $"Restored priority for {restored} approved background process(es).");
    }

    private static IReadOnlyList<int> ReadProcessIds(OptimizationActionDescriptor action)
    {
        if (!action.Parameters.TryGetValue(BackgroundAppPauseActionParameters.ProcessIds, out var processIdsText))
        {
            return [];
        }

        return processIdsText
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var processId) ? processId : 0)
            .Where(processId => processId > 0)
            .Distinct()
            .ToArray();
    }
}
