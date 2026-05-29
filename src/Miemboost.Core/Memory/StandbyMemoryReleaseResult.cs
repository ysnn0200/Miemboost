namespace Miemboost.Core.Memory;

public sealed record StandbyMemoryReleaseResult(
    bool Succeeded,
    string Message);
