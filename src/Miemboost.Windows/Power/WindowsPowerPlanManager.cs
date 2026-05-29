using System.Text.RegularExpressions;
using Miemboost.Core.Power;

namespace Miemboost.Windows.Power;

public sealed partial class WindowsPowerPlanManager(
    IWindowsCommandRunner? commandRunner = null) : IPowerPlanManager
{
    private readonly IWindowsCommandRunner _commandRunner =
        commandRunner ?? new ProcessWindowsCommandRunner();

    public async Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner
            .RunAsync("powercfg", "/getactivescheme", cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return null;
        }

        return ParseActivePlan(result.StandardOutput);
    }

    public async Task SetActivePlanAsync(
        string planId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planId))
        {
            throw new ArgumentException("Power plan id cannot be empty.", nameof(planId));
        }

        var result = await _commandRunner
            .RunAsync("powercfg", $"/setactive {planId}", cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            throw new InvalidOperationException($"Unable to set active power plan: {message}");
        }
    }

    internal static PowerPlanInfo? ParseActivePlan(string output)
    {
        var idMatch = PowerPlanIdRegex().Match(output);

        if (!idMatch.Success)
        {
            return null;
        }

        var nameMatch = PowerPlanNameRegex().Match(output);
        var name = nameMatch.Success ? nameMatch.Groups["name"].Value.Trim() : null;

        return new PowerPlanInfo(idMatch.Value, string.IsNullOrWhiteSpace(name) ? null : name);
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex PowerPlanIdRegex();

    [GeneratedRegex("\\((?<name>[^\\)]+)\\)")]
    private static partial Regex PowerPlanNameRegex();
}
