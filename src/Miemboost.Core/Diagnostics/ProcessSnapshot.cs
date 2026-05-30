namespace Miemboost.Core.Diagnostics;

public sealed record ProcessSnapshot(
    int ProcessId,
    string Name,
    string? MainModulePath,
    long WorkingSetBytes,
    TimeSpan TotalProcessorTime,
    bool IsProtectedCandidate,
    int TcpConnectionCount = 0,
    int EstablishedTcpConnectionCount = 0,
    double NetworkReceiveBytesPerSecond = 0,
    double NetworkSendBytesPerSecond = 0);
