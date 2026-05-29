namespace Miemboost.Core.Diagnostics;

public sealed record DiagnosticFinding(
    string Id,
    string Title,
    string Description,
    DiagnosticSeverity Severity,
    string? RelatedProcessName = null,
    int? RelatedProcessId = null);
