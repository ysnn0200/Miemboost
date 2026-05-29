namespace Miemboost.Core.Diagnostics;

public sealed record DiagnosticsSummary(
    DiagnosticSeverity OverallSeverity,
    DiagnosticSeverity CpuSeverity,
    DiagnosticSeverity MemorySeverity,
    DiagnosticSeverity NetworkSeverity,
    IReadOnlyList<DiagnosticFinding> Findings);
