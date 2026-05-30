using Miemboost.Core.Models;
using Miemboost.Core.Safety;
using Miemboost.Core.Security;

namespace Miemboost.Core.Preflight;

public sealed class OptimizationPreflightService(
    SafetyPolicy safetyPolicy,
    IPrivilegeChecker privilegeChecker)
{
    public OptimizationPreflightReport Evaluate(OptimizationPlan plan)
    {
        var actions = plan.Actions
            .Select(action => EvaluateAction(plan.Mode, action))
            .ToArray();

        return new OptimizationPreflightReport(actions);
    }

    private OptimizationActionPreflight EvaluateAction(
        BoostMode mode,
        OptimizationActionDescriptor action)
    {
        var safetyDecision = safetyPolicy.Evaluate(action, mode);
        if (!safetyDecision.IsAllowed)
        {
            return new OptimizationActionPreflight(
                action,
                OptimizationPreflightStatus.BlockedBySafety,
                safetyDecision.Reason ?? "Action blocked by safety policy.");
        }

        if (action.RequiresElevation && !privilegeChecker.IsAdministrator())
        {
            return new OptimizationActionPreflight(
                action,
                OptimizationPreflightStatus.RequiresAdministrator,
                "需要管理员权限，否则执行时会跳过。");
        }

        var restoreText = action.CanRestore ? "可恢复" : "不可恢复";
        return new OptimizationActionPreflight(
            action,
            OptimizationPreflightStatus.Ready,
            restoreText);
    }
}
