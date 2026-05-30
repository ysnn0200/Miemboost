using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Power;
using Miemboost.Core.Processes;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class WindowsSystemSnapshotFactoryTests
{
    [Fact]
    public async Task CreateAsync_CapturesPreviousPowerPlanWhenPlanSwitchIsPresent()
    {
        var factory = new WindowsSystemSnapshotFactory(new StubPowerPlanManager());
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.PowerPlanSwitch],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = await factory.CreateAsync(plan);

        Assert.Equal("balanced", snapshot.PreviousPowerPlanId);
    }

    [Fact]
    public async Task CreateAsync_DoesNotReadPowerPlanWhenPlanSwitchIsAbsent()
    {
        var manager = new StubPowerPlanManager();
        var factory = new WindowsSystemSnapshotFactory(manager);
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.NetworkDiagnostics],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = await factory.CreateAsync(plan);

        Assert.Null(snapshot.PreviousPowerPlanId);
        Assert.Equal(0, manager.GetActivePlanCallCount);
    }

    [Fact]
    public async Task CreateAsync_CapturesPreviousProcessPriorityWhenPriorityActionIsPresent()
    {
        var factory = new WindowsSystemSnapshotFactory(
            new StubPowerPlanManager(),
            new StubProcessPriorityManager());
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.CreateGamePriorityHighAction(123)],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = await factory.CreateAsync(plan);

        var priority = Assert.Single(snapshot.ProcessPriorities);
        Assert.Equal(123, priority.ProcessId);
        Assert.Equal(ManagedProcessPriority.Normal.ToString(), priority.PreviousPriorityClass);
    }

    [Fact]
    public async Task CreateAsync_CapturesPreviousPriorityForBackgroundPauseAction()
    {
        var factory = new WindowsSystemSnapshotFactory(
            new StubPowerPlanManager(),
            new StubProcessPriorityManager());
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.CreatePauseApprovedBackgroundAppsAction([123, 124])],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = await factory.CreateAsync(plan);

        Assert.Equal(2, snapshot.ProcessPriorities.Count);
    }

    [Fact]
    public async Task CreateAsync_CapturesServiceStateForServicePauseAction()
    {
        var factory = new WindowsSystemSnapshotFactory(
            new StubPowerPlanManager(),
            new StubProcessPriorityManager(),
            new StubWindowsServiceManager());
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.PauseWindowsUpdateDownloads],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = await factory.CreateAsync(plan);

        Assert.Equal(3, snapshot.ServiceStates.Count);
        Assert.Contains(snapshot.ServiceStates, state => state.ServiceName == "wuauserv" && state.PreviousStatus == "RUNNING");
    }

    private sealed class StubPowerPlanManager : IPowerPlanManager
    {
        public int GetActivePlanCallCount { get; private set; }

        public Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default)
        {
            GetActivePlanCallCount++;
            return Task.FromResult<PowerPlanInfo?>(new PowerPlanInfo("balanced", "Balanced"));
        }

        public Task SetActivePlanAsync(string planId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubProcessPriorityManager : IProcessPriorityManager
    {
        public Task<string?> GetProcessNameAsync(
            int processId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>("Process");
        }

        public Task<ManagedProcessPriority?> GetPriorityAsync(
            int processId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManagedProcessPriority?>(ManagedProcessPriority.Normal);
        }

        public Task SetPriorityAsync(
            int processId,
            ManagedProcessPriority priority,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubWindowsServiceManager : Miemboost.Core.Services.IWindowsServiceManager
    {
        public Task<Miemboost.Core.Services.WindowsServiceSnapshot?> GetStatusAsync(
            string serviceName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Miemboost.Core.Services.WindowsServiceSnapshot?>(
                new Miemboost.Core.Services.WindowsServiceSnapshot(serviceName, "RUNNING"));
        }

        public Task<bool> StopAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> StartAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
