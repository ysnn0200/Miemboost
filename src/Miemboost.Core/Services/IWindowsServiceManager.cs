namespace Miemboost.Core.Services;

public interface IWindowsServiceManager
{
    Task<WindowsServiceSnapshot?> GetStatusAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    Task<bool> StopAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    Task<bool> StartAsync(
        string serviceName,
        CancellationToken cancellationToken = default);
}
