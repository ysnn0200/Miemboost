using Miemboost.Core.Models;

namespace Miemboost.Core.Games;

public interface IGameProfileStore
{
    Task<IReadOnlyList<GameProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(GameProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(string profileId, CancellationToken cancellationToken = default);
}
