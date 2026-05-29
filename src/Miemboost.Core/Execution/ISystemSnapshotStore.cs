using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public interface ISystemSnapshotStore
{
    Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<SystemSnapshot?> GetAsync(string snapshotId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemSnapshot>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);
}
