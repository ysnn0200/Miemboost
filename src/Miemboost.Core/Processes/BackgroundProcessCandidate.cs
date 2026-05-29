namespace Miemboost.Core.Processes;

public sealed record BackgroundProcessCandidate(
    int ProcessId,
    string Name,
    string? Path,
    long WorkingSetBytes,
    bool IsProtectedCandidate);
