using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Windows.Power;

namespace Miemboost.Windows.Execution;

public sealed class DnsCacheFlushActionHandler(IWindowsCommandRunner commandRunner) : IOptimizationActionHandler
{
    public string ActionId => DefaultActionCatalog.FlushDnsCache.Id;

    public async Task<OptimizationActionResult> ExecuteAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await commandRunner
            .RunAsync("ipconfig.exe", "/flushdns", cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            return OptimizationActionResult.Failed(
                action,
                string.IsNullOrWhiteSpace(message) ? "DNS cache flush failed." : message.Trim());
        }

        return OptimizationActionResult.Succeeded(action, "Flushed the local Windows DNS resolver cache.");
    }

    public Task<OptimizationActionResult> RestoreAsync(
        OptimizationActionDescriptor action,
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OptimizationActionResult.Skipped(
            action,
            "DNS cache flush is a one-time resolver cache clear and has no restore operation."));
    }
}
