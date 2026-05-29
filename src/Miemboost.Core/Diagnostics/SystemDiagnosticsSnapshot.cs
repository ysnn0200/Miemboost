namespace Miemboost.Core.Diagnostics;

public sealed record SystemDiagnosticsSnapshot(
    CpuSnapshot Cpu,
    GpuSnapshot Gpu,
    MemorySnapshot Memory,
    IReadOnlyList<ProcessSnapshot> Processes,
    IReadOnlyList<NetworkAdapterSnapshot> NetworkAdapters,
    DateTimeOffset CapturedAt);
