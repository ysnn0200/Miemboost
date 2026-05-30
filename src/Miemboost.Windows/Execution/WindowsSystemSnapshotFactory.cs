using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Power;
using Miemboost.Core.Processes;
using Miemboost.Core.Services;

namespace Miemboost.Windows.Execution;

public sealed class WindowsSystemSnapshotFactory(
    IPowerPlanManager powerPlanManager,
    IProcessPriorityManager? processPriorityManager = null,
    IWindowsServiceManager? serviceManager = null) : ISystemSnapshotFactory
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
        IReadOnlyList<ServiceStateSnapshot> serviceStates = serviceManager is null
            ? Array.Empty<ServiceStateSnapshot>()
            : await CaptureServiceStatesAsync(plan, serviceManager, cancellationToken)
                .ConfigureAwait(false);

        return new SystemSnapshot(
            Id: Guid.NewGuid().ToString("N"),
            PlanId: plan.Id,
            PreviousPowerPlanId: activePowerPlan?.Id,
            ProcessPriorities: processPriorities,
            ServiceStates: serviceStates,
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UtcNow);
    }

    private static async Task<IReadOnlyList<ServiceStateSnapshot>> CaptureServiceStatesAsync(
        OptimizationPlan plan,
        IWindowsServiceManager serviceManager,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<ServiceStateSnapshot>();
        foreach (var serviceName in ReadServiceNames(plan))
        {
            var status = await serviceManager.GetStatusAsync(serviceName, cancellationToken).ConfigureAwait(false);
            if (status is null)
            {
                continue;
            }

            snapshots.Add(new ServiceStateSnapshot(status.ServiceName, status.Status));
        }

        return snapshots;
    }

    private static IReadOnlyList<string> ReadServiceNames(OptimizationPlan plan)
    {
        return plan.Actions
            .Where(action => action.Kind == OptimizationActionKind.ServicePause)
            .SelectMany(action =>
                action.Parameters.TryGetValue(ServicePauseActionParameters.ServiceNames, out var serviceNamesText)
                    ? serviceNamesText.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ProcessPrioritySnapshot>> CaptureProcessPrioritiesAsync(
        OptimizationPlan plan,
        IProcessPriorityManager processPriorityManager,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<ProcessPrioritySnapshot>();

        foreach (var action in plan.Actions.Where(action =>
            action.Kind is OptimizationActionKind.ProcessPriorityChange or OptimizationActionKind.BackgroundAppPause))
        {
            foreach (var processId in ReadProcessIds(action))
            {
                var priority = await processPriorityManager
                    .GetPriorityAsync(processId, cancellationToken)
                    .ConfigureAwait(false);

                if (priority is null || snapshots.Any(snapshot => snapshot.ProcessId == processId))
                {
                    continue;
                }

                snapshots.Add(new ProcessPrioritySnapshot(
                    ProcessId: processId,
                    ProcessName: action.Title,
                    PreviousPriorityClass: priority.Value.ToString()));
            }
        }

        return snapshots;
    }

    private static IReadOnlyList<int> ReadProcessIds(OptimizationActionDescriptor action)
    {
        if (action.Parameters.TryGetValue(ProcessPriorityActionParameters.ProcessId, out var processIdText)
            && int.TryParse(processIdText, out var processId)
            && processId > 0)
        {
            return [processId];
        }

        if (!action.Parameters.TryGetValue(BackgroundAppPauseActionParameters.ProcessIds, out var processIdsText))
        {
            return [];
        }

        return processIdsText
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var parsedProcessId) ? parsedProcessId : 0)
            .Where(parsedProcessId => parsedProcessId > 0)
            .Distinct()
            .ToArray();
    }
}
