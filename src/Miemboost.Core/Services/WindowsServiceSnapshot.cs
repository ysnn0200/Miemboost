namespace Miemboost.Core.Services;

public sealed record WindowsServiceSnapshot(
    string ServiceName,
    string Status);
