using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public interface ISystemSnapshotFactory
{
    Task<SystemSnapshot> CreateAsync(
        OptimizationPlan plan,
        CancellationToken cancellationToken = default);
}
