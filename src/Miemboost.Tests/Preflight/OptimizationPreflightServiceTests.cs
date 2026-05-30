using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Preflight;
using Miemboost.Core.Safety;
using Miemboost.Core.Security;

namespace Miemboost.Tests.Preflight;

public sealed class OptimizationPreflightServiceTests
{
    [Fact]
    public void Evaluate_FlagsElevatedActionWhenNotAdministrator()
    {
        var plan = CreatePlan([DefaultActionCatalog.ReleaseStandbyMemory]);
        var service = new OptimizationPreflightService(new SafetyPolicy(), new StubPrivilegeChecker(false));

        var report = service.Evaluate(plan);

        var action = Assert.Single(report.Actions);
        Assert.Equal(OptimizationPreflightStatus.RequiresAdministrator, action.Status);
        Assert.True(report.HasBlockingIssues);
    }

    [Fact]
    public void Evaluate_MarksSafeActionReady()
    {
        var plan = CreatePlan([DefaultActionCatalog.NetworkDiagnostics]);
        var service = new OptimizationPreflightService(new SafetyPolicy(), new StubPrivilegeChecker(false));

        var report = service.Evaluate(plan);

        var action = Assert.Single(report.Actions);
        Assert.Equal(OptimizationPreflightStatus.Ready, action.Status);
        Assert.False(report.HasBlockingIssues);
    }

    private static OptimizationPlan CreatePlan(IReadOnlyList<OptimizationActionDescriptor> actions)
    {
        return new OptimizationPlan(
            Id: "plan",
            Mode: BoostMode.Balanced,
            GameProfileId: null,
            Actions: actions,
            CreatedAt: DateTimeOffset.UnixEpoch);
    }

    private sealed class StubPrivilegeChecker(bool isAdministrator) : IPrivilegeChecker
    {
        public bool IsAdministrator()
        {
            return isAdministrator;
        }
    }
}
