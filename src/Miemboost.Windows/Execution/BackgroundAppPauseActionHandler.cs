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
        var targets = ReadTargets(action);
        if (targets.Count == 0)
        {
            return OptimizationActionResult.Skipped(action, "No approved background processes were provided.");
        }

        var changed = 0;
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!await ProcessNameStillMatchesAsync(target, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                await processPriorityManager
                    .SetPriorityAsync(target.ProcessId, ManagedProcessPriority.BelowNormal, cancellationToken)
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
        var targets = ReadTargets(action);
        if (targets.Count == 0)
        {
            return OptimizationActionResult.Skipped(action, "No approved background processes were provided.");
        }

        var restored = 0;
        foreach (var target in targets)
        {
            var snapshot = context.Snapshot.ProcessPriorities
                .FirstOrDefault(item => item.ProcessId == target.ProcessId);

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
                if (!await ProcessNameStillMatchesAsync(target, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                await processPriorityManager
                    .SetPriorityAsync(target.ProcessId, previousPriority, cancellationToken)
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

    private async Task<bool> ProcessNameStillMatchesAsync(
        BackgroundProcessTarget target,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.ExpectedName))
        {
            return true;
        }

        var currentName = await processPriorityManager
            .GetProcessNameAsync(target.ProcessId, cancellationToken)
            .ConfigureAwait(false);

        return string.Equals(currentName, target.ExpectedName, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<BackgroundProcessTarget> ReadTargets(OptimizationActionDescriptor action)
    {
        if (!action.Parameters.TryGetValue(BackgroundAppPauseActionParameters.ProcessIds, out var processIdsText))
        {
            return [];
        }

        var processIds = processIdsText
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var processId) ? processId : 0)
            .Where(processId => processId > 0)
            .Distinct()
            .ToArray();

        action.Parameters.TryGetValue(BackgroundAppPauseActionParameters.ProcessNames, out var processNamesText);
        var processNames = string.IsNullOrWhiteSpace(processNamesText)
            ? []
            : processNamesText.Split([';', ','], StringSplitOptions.TrimEntries);

        return processIds
            .Select((processId, index) => new BackgroundProcessTarget(
                processId,
                index < processNames.Length ? processNames[index] : null))
            .ToArray();
    }

    private sealed record BackgroundProcessTarget(
        int ProcessId,
        string? ExpectedName);
}
