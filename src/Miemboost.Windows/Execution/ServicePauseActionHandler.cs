using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Services;

namespace Miemboost.Windows.Execution;

public sealed class ServicePauseActionHandler(IWindowsServiceManager serviceManager) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.PauseWindowsUpdateDownloads.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var serviceNames = ReadServiceNames(action);
        if (serviceNames.Count == 0)
        {
            return OptimizationActionResult.Skipped(action, "No services were configured for pause.");
        }

        var stopped = 0;
        foreach (var serviceName in serviceNames)
        {
            var status = context.Snapshot.ServiceStates
                .FirstOrDefault(snapshot => string.Equals(snapshot.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                ?.PreviousStatus;

            if (!string.Equals(status, "RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await serviceManager.StopAsync(serviceName, cancellationToken).ConfigureAwait(false))
            {
                stopped++;
            }
        }

        return stopped == 0
            ? OptimizationActionResult.Skipped(action, "No running Windows update download services were stopped.")
            : OptimizationActionResult.Succeeded(action, $"Paused {stopped} Windows update download service(s).");
    }

    public async Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var serviceNames = ReadServiceNames(action);
        var restored = 0;

        foreach (var serviceName in serviceNames)
        {
            var snapshot = context.Snapshot.ServiceStates
                .FirstOrDefault(item => string.Equals(item.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

            if (snapshot is null || !string.Equals(snapshot.PreviousStatus, "RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await serviceManager.StartAsync(serviceName, cancellationToken).ConfigureAwait(false))
            {
                restored++;
            }
        }

        return restored == 0
            ? OptimizationActionResult.Skipped(action, "No Windows update download services needed restore.")
            : OptimizationActionResult.Succeeded(action, $"Restored {restored} Windows update download service(s).");
    }

    private static IReadOnlyList<string> ReadServiceNames(OptimizationActionDescriptor action)
    {
        if (!action.Parameters.TryGetValue(ServicePauseActionParameters.ServiceNames, out var serviceNamesText))
        {
            return [];
        }

        return serviceNamesText
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
