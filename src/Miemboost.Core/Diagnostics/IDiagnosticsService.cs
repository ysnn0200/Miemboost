namespace Miemboost.Core.Diagnostics;

public interface IDiagnosticsService
{
    Task<DiagnosticsReport> CaptureAsync(
        DiagnosticsCaptureOptions? options = null,
        CancellationToken cancellationToken = default);
}
