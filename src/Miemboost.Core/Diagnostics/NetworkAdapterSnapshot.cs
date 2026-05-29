namespace Miemboost.Core.Diagnostics;

public sealed record NetworkAdapterSnapshot(
    string Id,
    string Name,
    string Description,
    string Type,
    bool IsUp,
    long SpeedBitsPerSecond,
    long BytesReceived,
    long BytesSent);
