using Miemboost.Core.Execution;
using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Windows.Execution;
using Miemboost.Windows.Power;

namespace Miemboost.Tests.Execution;

public sealed class DnsCacheFlushActionHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_FlushesDnsCache()
    {
        var runner = new StubWindowsCommandRunner(new WindowsCommandResult(0, "ok", string.Empty));
        var handler = new DnsCacheFlushActionHandler(runner);

        var result = await handler.ExecuteAsync(DefaultActionCatalog.FlushDnsCache, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Succeeded, result.Status);
        Assert.Equal("ipconfig.exe", runner.FileName);
        Assert.Equal("/flushdns", runner.Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWhenCommandFails()
    {
        var runner = new StubWindowsCommandRunner(new WindowsCommandResult(1, string.Empty, "failed"));
        var handler = new DnsCacheFlushActionHandler(runner);

        var result = await handler.ExecuteAsync(DefaultActionCatalog.FlushDnsCache, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Failed, result.Status);
        Assert.Equal("failed", result.Message);
    }

    [Fact]
    public async Task RestoreAsync_SkipsBecauseFlushIsOneTime()
    {
        var runner = new StubWindowsCommandRunner(new WindowsCommandResult(0, "ok", string.Empty));
        var handler = new DnsCacheFlushActionHandler(runner);

        var result = await handler.RestoreAsync(DefaultActionCatalog.FlushDnsCache, CreateContext());

        Assert.Equal(OptimizationExecutionStatus.Skipped, result.Status);
    }

    private static OptimizationExecutionContext CreateContext()
    {
        var plan = new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: [DefaultActionCatalog.FlushDnsCache],
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

    private sealed class StubWindowsCommandRunner(WindowsCommandResult result) : IWindowsCommandRunner
    {
        public string? FileName { get; private set; }

        public string? Arguments { get; private set; }

        public Task<WindowsCommandResult> RunAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            return Task.FromResult(result);
        }
    }
}
