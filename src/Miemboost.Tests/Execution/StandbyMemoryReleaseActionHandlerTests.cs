using Miemboost.Core.Execution;
using Miemboost.Core.Memory;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Windows.Execution;

namespace Miemboost.Tests.Execution;

public sealed class StandbyMemoryReleaseActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_SucceedsWhenManagerReleasesStandbyMemory()
    {
        var handler = new StandbyMemoryReleaseActionHandler(new StubStandbyMemoryManager(true, "Released."));

        var result = await handler.ExecuteAsync(DefaultActionCatalog.ReleaseStandbyMemory, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal("Released.", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenManagerCannotReleaseStandbyMemory()
    {
        var handler = new StandbyMemoryReleaseActionHandler(new StubStandbyMemoryManager(false, "Administrator required."));

        var result = await handler.ExecuteAsync(DefaultActionCatalog.ReleaseStandbyMemory, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
        Assert.Equal("Administrator required.", result.Message);
    }

    [Fact]
    public async Task RestoreAsync_SkipsBecauseStandbyReleaseIsOneTime()
    {
        var handler = new StandbyMemoryReleaseActionHandler(new StubStandbyMemoryManager(true, "Released."));

        var result = await handler.RestoreAsync(DefaultActionCatalog.ReleaseStandbyMemory, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
    }

    private static OptimizationExecutionContext CreateContext()
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.ReleaseStandbyMemory],
            CreatedAt: DateTimeOffset.UnixEpoch);

        var snapshot = new SystemSnapshot(
            Id: "snapshot",
            PlanId: plan.Id,
            PreviousPowerPlanId: null,
            ProcessPriorities: [],
            PausedProcesses: [],
            CreatedAt: DateTimeOffset.UnixEpoch);

        return new OptimizationExecutionContext(plan, snapshot);
    }

    private sealed class StubStandbyMemoryManager(bool succeeds, string message) : IStandbyMemoryManager
    {
        public Task<StandbyMemoryReleaseResult> ReleaseAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StandbyMemoryReleaseResult(succeeds, message));
        }
    }
}
