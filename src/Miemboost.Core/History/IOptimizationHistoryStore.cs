namespace Miemboost.Core.History;

public interface IOptimizationHistoryStore
{
    Task AddAsync(OptimizationHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OptimizationHistoryEntry>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);
}
