namespace Miemboost.Core.Models;

public sealed record GameProfile(
    string Id,
    string Name,
    string ExecutablePath,
    BoostMode RecommendedMode,
    bool AutoRestoreOnExit,
    IReadOnlyList<string> AllowedBackgroundProcessNames,
    IReadOnlyList<string> NetworkTargets);
