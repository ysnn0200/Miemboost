namespace Miemboost.Core.Memory;

public interface IStandbyMemoryManager
{
    Task<StandbyMemoryReleaseResult> ReleaseAsync(CancellationToken cancellationToken = default);
}
