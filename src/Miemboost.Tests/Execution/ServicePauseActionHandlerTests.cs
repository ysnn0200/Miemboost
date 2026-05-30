using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Services;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class ServicePauseActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_StopsOnlyPreviouslyRunningServices()
    {
        var manager = new RecordingServiceManager();
        var handler = new ServicePauseActionHandler(manager);

        var result = await handler.ExecuteAsync(
            DefaultActionCatalog.PauseWindowsUpdateDownloads,
            CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(["wuauserv", "DoSvc"], manager.StoppedServices);
    }

    [Fact]
    public async Task RestoreAsync_StartsOnlyPreviouslyRunningServices()
    {
        var manager = new RecordingServiceManager();
        var handler = new ServicePauseActionHandler(manager);

        var result = await handler.RestoreAsync(
            DefaultActionCatalog.PauseWindowsUpdateDownloads,
            CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(["wuauserv", "DoSvc"], manager.StartedServices);
    }

    private static OptimizationExecutionContext CreateContext()
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.PauseWindowsUpdateDownloads],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = new SystemSnapshot(
            Id: "snapshot",
            PlanId: plan.Id,
            PreviousPowerPlanId: null,
            ProcessPriorities: [],
            ServiceStates:
            [
                new ServiceStateSnapshot("wuauserv", "RUNNING"),
                new ServiceStateSnapshot("BITS", "STOPPED"),
                new ServiceStateSnapshot("DoSvc", "RUNNING")
            ],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);

        return new OptimizationExecutionContext(plan, snapshot);
    }

    private sealed class RecordingServiceManager : IWindowsServiceManager
    {
        public List<string> StoppedServices { get; } = [];

        public List<string> StartedServices { get; } = [];

        public Task<WindowsServiceSnapshot?> GetStatusAsync(
            string serviceName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WindowsServiceSnapshot?>(new WindowsServiceSnapshot(serviceName, "RUNNING"));
        }

        public Task<bool> StopAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            StoppedServices.Add(serviceName);
            return Task.FromResult(true);
        }

        public Task<bool> StartAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            StartedServices.Add(serviceName);
            return Task.FromResult(true);
        }
    }
}
