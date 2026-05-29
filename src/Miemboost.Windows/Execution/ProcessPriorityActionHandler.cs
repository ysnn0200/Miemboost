using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;

namespace Miemboost.Windows.Execution;

public sealed class ProcessPriorityActionHandler(IProcessPriorityManager processPriorityManager) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.GamePriorityHigh.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadParameters(action, out var processId, out var targetPriority, out var error))
        {
            return OptimizationActionResult.Skipped(action, error);
        }

        await processPriorityManager
            .SetPriorityAsync(processId, targetPriority, cancellationToken)
            .ConfigureAwait(false);

        return OptimizationActionResult.Succeeded(
            action,
            $"Set process {processId} priority to {targetPriority}.");
    }

    public async Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadProcessId(action, out var processId, out var error))
        {
            return OptimizationActionResult.Skipped(action, error);
        }

        var previous = context.Snapshot.ProcessPriorities
            .FirstOrDefault(snapshot => snapshot.ProcessId == processId);

        if (previous is null)
        {
            return OptimizationActionResult.Skipped(
                action,
                $"No previous priority was captured for process {processId}.");
        }

        if (!Enum.TryParse<ManagedProcessPriority>(
                previous.PreviousPriorityClass,
                ignoreCase: true,
                out var previousPriority))
        {
            return OptimizationActionResult.Skipped(
                action,
                $"Previous priority '{previous.PreviousPriorityClass}' is not recognized.");
        }

        await processPriorityManager
            .SetPriorityAsync(processId, previousPriority, cancellationToken)
            .ConfigureAwait(false);

        return OptimizationActionResult.Succeeded(
            action,
            $"Restored process {processId} priority to {previousPriority}.");
    }

    private static bool TryReadParameters(
        OptimizationActionDescriptor action,
        out int processId,
        out ManagedProcessPriority targetPriority,
        out string error)
    {
        targetPriority = ManagedProcessPriority.Normal;

        if (!TryReadProcessId(action, out processId, out error))
        {
            return false;
        }

        if (!action.Parameters.TryGetValue(ProcessPriorityActionParameters.TargetPriority, out var priorityText)
            || !Enum.TryParse(priorityText, ignoreCase: true, out targetPriority))
        {
            error = "Process priority action is missing a valid target priority.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadProcessId(
        OptimizationActionDescriptor action,
        out int processId,
        out string error)
    {
        processId = 0;

        if (!action.Parameters.TryGetValue(ProcessPriorityActionParameters.ProcessId, out var processIdText)
            || !int.TryParse(processIdText, out processId)
            || processId <= 0)
        {
            error = "Process priority action is missing a valid process id.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
