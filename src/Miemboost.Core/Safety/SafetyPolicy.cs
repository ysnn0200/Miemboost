using Miemboost.Core.Models;

namespace Miemboost.Core.Safety;

public sealed class SafetyPolicy
{
    private static readonly HashSet<OptimizationActionKind> ForbiddenKinds =
    [
        OptimizationActionKind.GameInjection,
        OptimizationActionKind.GameMemoryScan,
        OptimizationActionKind.RenderHook,
        OptimizationActionKind.PacketModification,
        OptimizationActionKind.AntiCheatInterference,
        OptimizationActionKind.InputAutomation
    ];

    public SafetyDecision Evaluate(OptimizationActionDescriptor action, BoostMode mode)
    {
        if (ForbiddenKinds.Contains(action.Kind) || action.RiskLevel == RiskLevel.Forbidden)
        {
            return SafetyDecision.Blocked(action.Id, "This action is forbidden because it can look like cheat or anti-cheat interference behavior.");
        }

        if (action.RiskLevel == RiskLevel.Aggressive && mode != BoostMode.Aggressive)
        {
            return SafetyDecision.Blocked(action.Id, "Aggressive actions require the explicit Aggressive mode.");
        }

        if (action.RiskLevel == RiskLevel.Balanced && mode == BoostMode.Conservative)
        {
            return SafetyDecision.Blocked(action.Id, "Balanced actions require Balanced or Aggressive mode.");
        }

        if (!action.CanRestore && action.RiskLevel == RiskLevel.Aggressive)
        {
            return SafetyDecision.Blocked(action.Id, "Non-restorable aggressive system changes are not allowed.");
        }

        return SafetyDecision.Allowed(action.Id);
    }
}
