using Miemboost.Core.Models;
using Miemboost.Core.Safety;

namespace Miemboost.Core.Planning;

public sealed class DefaultPlanFactory
{
    public OptimizationPlan Create(BoostMode mode, string? gameProfileId = null)
    {
        var builder = new OptimizationPlanBuilder(new SafetyPolicy())
            .Add(DefaultActionCatalog.PowerPlanSwitch)
            .Add(DefaultActionCatalog.GamePriorityHigh)
            .Add(DefaultActionCatalog.PauseApprovedBackgroundApps)
            .Add(DefaultActionCatalog.ReleaseStandbyMemory)
            .Add(DefaultActionCatalog.NetworkDiagnostics);

        return builder.Build(mode, gameProfileId);
    }
}
