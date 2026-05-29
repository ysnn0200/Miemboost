using Miemboost.Core.Models;

namespace Miemboost.Core.Games;

public sealed record GameProfileMatch(
    GameProfile Profile,
    int ProcessId);
