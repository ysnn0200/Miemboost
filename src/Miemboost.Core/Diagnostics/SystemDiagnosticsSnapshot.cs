namespace Miemboost.Core.Diagnostics;

public sealed record SystemDiagnosticsSnapshot(
    CpuSnapshot Cpu,
    MemorySnapshot Memory,
    IReadOnlyList<ProcessSnapshot> Processes,
    IReadOnlyList<NetworkAdapterSnapshot> NetworkAdapters,
    DateTimeOffset CapturedAt);
