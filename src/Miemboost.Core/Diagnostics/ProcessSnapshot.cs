namespace Miemboost.Core.Diagnostics;

public sealed record ProcessSnapshot(
    int ProcessId,
    string Name,
    string? MainModulePath,
    long WorkingSetBytes,
    TimeSpan TotalProcessorTime,
    bool IsProtectedCandidate);
