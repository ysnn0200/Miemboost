using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Power;
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
}
