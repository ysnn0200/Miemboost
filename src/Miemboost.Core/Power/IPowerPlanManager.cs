namespace Miemboost.Core.Power;

public interface IPowerPlanManager
{
    Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default);

    Task SetActivePlanAsync(string planId, CancellationToken cancellationToken = default);
}
