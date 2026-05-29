namespace Miemboost.Core.Diagnostics;

public interface ISystemDiagnosticsReader
{
    Task<SystemDiagnosticsSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
