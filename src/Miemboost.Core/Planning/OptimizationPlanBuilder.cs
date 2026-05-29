using Miemboost.Core.Models;
using Miemboost.Core.Safety;

namespace Miemboost.Core.Planning;

public sealed class OptimizationPlanBuilder(SafetyPolicy safetyPolicy)
{
    private readonly List<OptimizationActionDescriptor> _actions = [];

    public OptimizationPlanBuilder Add(OptimizationActionDescriptor action)
    {
        _actions.Add(action);
        return this;
    }

    public OptimizationPlan Build(BoostMode mode, string? gameProfileId = null)
    {
        var allowedActions = _actions
            .Where(action => safetyPolicy.Evaluate(action, mode).IsAllowed)
            .ToArray();

        return new OptimizationPlan(
            Id: Guid.NewGuid().ToString("N"),
            Mode: mode,
            GameProfileId: gameProfileId,
            Actions: allowedActions,
            CreatedAt: DateTimeOffset.UtcNow);
    }
}
