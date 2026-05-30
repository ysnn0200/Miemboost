using System.Text.RegularExpressions;
using Miemboost.Core.Services;
using Miemboost.Windows.Power;

namespace Miemboost.Windows.Services;

public sealed partial class ScWindowsServiceManager(IWindowsCommandRunner commandRunner) : IWindowsServiceManager
{
    public async Task<WindowsServiceSnapshot?> GetStatusAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var result = await commandRunner
            .RunAsync("sc.exe", $"query \"{serviceName}\"", cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return null;
        }

        var match = StateRegex().Match(result.StandardOutput);
        return match.Success
            ? new WindowsServiceSnapshot(serviceName, match.Groups["state"].Value)
            : null;
    }

    public async Task<bool> StopAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var result = await commandRunner
            .RunAsync("sc.exe", $"stop \"{serviceName}\"", cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded || IsAlreadyStopped(result);
    }

    public async Task<bool> StartAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var result = await commandRunner
            .RunAsync("sc.exe", $"start \"{serviceName}\"", cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded || IsAlreadyStarted(result);
    }

    private static bool IsAlreadyStopped(WindowsCommandResult result)
    {
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        return text.Contains("has not been started", StringComparison.OrdinalIgnoreCase)
            || text.Contains("1062", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlreadyStarted(WindowsCommandResult result)
    {
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        return text.Contains("already been started", StringComparison.OrdinalIgnoreCase)
            || text.Contains("1056", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"STATE\s+:\s+\d+\s+(?<state>[A-Z_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex StateRegex();
}
