using Miemboost.Core.Models;

namespace Miemboost.Core.Execution;

public interface ISystemSnapshotStore
{
    Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default);
}
