using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Power;
using Miemboost.Core.Processes;

namespace Miemboost.Windows.Execution;

public sealed class WindowsSystemSnapshotFactory(
    IPowerPlanManager powerPlanManager,
    IProcessPriorityManager? processPriorityManager = null) : ISystemSnapshotFactory
{
    public async Task<SystemSnapshot> CreateAsync(
        OptimizationPlan plan,
        CancellationToken cancellationToken = default)
    {
        var shouldCapturePowerPlan = plan.Actions.Any(action =>
            action.Kind == OptimizationActionKind.PowerPlanSwitch);

        var activePowerPlan = shouldCapturePowerPlan
            ? await powerPlanManager.GetActivePlanAsync(cancellationToken).ConfigureAwait(false)
            : null;

        IReadOnlyList<ProcessPrioritySnapshot> processPriorities = processPriorityManager is null
            ? Array.Empty<ProcessPrioritySnapshot>()
            : await CaptureProcessPrioritiesAsync(plan, processPriorityManager, cancellationToken)
                .ConfigureAwait(false);

        return new SystemSnapshot(
            Id: Guid.NewGuid().ToString("N"),
            PlanId: plan.Id,
            PreviousPowerPlanId: activePowerPlan?.Id,
            ProcessPriorities: processPriorities,
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UtcNow);
    }

    private static async Task<IReadOnlyList<ProcessPrioritySnapshot>> CaptureProcessPrioritiesAsync(
        OptimizationPlan plan,
        IProcessPriorityManager processPriorityManager,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<ProcessPrioritySnapshot>();

        foreach (var action in plan.Actions.Where(action => action.Kind == OptimizationActionKind.ProcessPriorityChange))
        {
            if (!action.Parameters.TryGetValue(ProcessPriorityActionParameters.ProcessId, out var processIdText)
                || !int.TryParse(processIdText, out var processId)
                || processId <= 0)
            {
                continue;
            }

            var priority = await processPriorityManager
                .GetPriorityAsync(processId, cancellationToken)
                .ConfigureAwait(false);

            if (priority is null)
            {
                continue;
            }

            snapshots.Add(new ProcessPrioritySnapshot(
                ProcessId: processId,
                ProcessName: action.Title,
                PreviousPriorityClass: priority.Value.ToString()));
        }

        return snapshots;
    }
}
