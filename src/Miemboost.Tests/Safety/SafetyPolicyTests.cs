using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Safety;

namespace Miemboost.Tests.Safety;

public sealed class SafetyPolicyTests
{
    [Fact]
    public void Evaluate_BlocksForbiddenActionKinds()
    {
        var action = new OptimizationActionDescriptor(
            Id: "game.inject",
            Title: "Inject into game",
            Description: "Forbidden test action.",
            Kind: OptimizationActionKind.GameInjection,
            RiskLevel: RiskLevel.Forbidden,
            RequiresElevation: true,
            CanRestore: false);

        var decision = new SafetyPolicy().Evaluate(action, BoostMode.Aggressive);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Evaluate_AllowsStandbyMemoryReleaseInBalancedMode()
    {
        var decision = new SafetyPolicy().Evaluate(
            DefaultActionCatalog.ReleaseStandbyMemory,
            BoostMode.Balanced);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Evaluate_BlocksAggressiveActionOutsideAggressiveMode()
    {
        var action = new OptimizationActionDescriptor(
            Id: "service.pause",
            Title: "Pause service",
            Description: "Aggressive test action.",
            Kind: OptimizationActionKind.ServicePause,
            RiskLevel: RiskLevel.Aggressive,
            RequiresElevation: true,
            CanRestore: true);

        var decision = new SafetyPolicy().Evaluate(action, BoostMode.Balanced);

        Assert.False(decision.IsAllowed);
    }
}
