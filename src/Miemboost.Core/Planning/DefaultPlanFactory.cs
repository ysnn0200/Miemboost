using Miemboost.Core.Models;
using Miemboost.Core.Safety;
using Miemboost.Core.Diagnostics;

namespace Miemboost.Core.Planning;

public sealed class DefaultPlanFactory
{
    public OptimizationPlan Create(
        BoostMode mode,
        string? gameProfileId = null,
        int? gameProcessId = null,
        GameProfile? gameProfile = null,
        IReadOnlyList<ProcessSnapshot>? processes = null)
    {
        var builder = new OptimizationPlanBuilder(new SafetyPolicy())
            .Add(DefaultActionCatalog.PowerPlanSwitch)
            .Add(DefaultActionCatalog.ReleaseStandbyMemory)
            .Add(DefaultActionCatalog.NetworkDiagnostics)
            .Add(DefaultActionCatalog.FlushDnsCache);

        if (gameProcessId is > 0)
        {
            builder.Add(DefaultActionCatalog.CreateGamePriorityHighAction(gameProcessId.Value));
        }

        var allowedBackgroundProcessIds = FindAllowedBackgroundProcessIds(gameProfile, processes);
        if (allowedBackgroundProcessIds.Count > 0)
        {
            builder.Add(DefaultActionCatalog.CreatePauseApprovedBackgroundAppsAction(allowedBackgroundProcessIds));
        }

        return builder.Build(mode, gameProfileId);
    }

    private static IReadOnlyList<int> FindAllowedBackgroundProcessIds(
        GameProfile? gameProfile,
        IReadOnlyList<ProcessSnapshot>? processes)
    {
        if (gameProfile is null || processes is null || gameProfile.AllowedBackgroundProcessNames.Count == 0)
        {
            return [];
        }

        var allowedNames = new HashSet<string>(
            gameProfile.AllowedBackgroundProcessNames,
            StringComparer.OrdinalIgnoreCase);

        return processes
            .Where(process => !process.IsProtectedCandidate)
            .Where(process => allowedNames.Contains(process.Name))
            .Select(process => process.ProcessId)
            .Distinct()
            .ToArray();
    }
}
