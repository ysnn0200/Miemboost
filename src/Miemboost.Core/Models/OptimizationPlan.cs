namespace Miemboost.Core.Models;

public sealed record OptimizationPlan(
    string Id,
    BoostMode Mode,
    string? GameProfileId,
    IReadOnlyList<OptimizationActionDescriptor> Actions,
    DateTimeOffset CreatedAt)
{
    public bool RequiresElevation => Actions.Any(action => action.RequiresElevation);

    public bool HasForbiddenActions => Actions.Any(action => action.RiskLevel == RiskLevel.Forbidden);

    public bool CanFullyRestore => Actions.All(action => action.CanRestore);
}
