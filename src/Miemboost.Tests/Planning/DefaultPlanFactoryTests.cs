using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;
using Miemboost.Core.Diagnostics;

namespace Miemboost.Tests.Planning;

public sealed class DefaultPlanFactoryTests
{
    [Fact]
    public void Create_DoesNotIncludeProcessPriorityActionWithoutGameProcess()
    {
        var plan = new DefaultPlanFactory().Create(BoostMode.Balanced);

        Assert.DoesNotContain(plan.Actions, action => action.Kind == OptimizationActionKind.ProcessPriorityChange);
    }

    [Fact]
    public void Create_ConservativeModeIncludesOnlySafeDefaultActions()
    {
        var plan = new DefaultPlanFactory().Create(BoostMode.Conservative, gameProcessId: 123);

        Assert.Contains(plan.Actions, action => action.Kind == OptimizationActionKind.PowerPlanSwitch);
        Assert.Contains(plan.Actions, action => action.Kind == OptimizationActionKind.NetworkDiagnostics);
        Assert.DoesNotContain(plan.Actions, action => action.Kind == OptimizationActionKind.StandbyMemoryRelease);
        Assert.DoesNotContain(plan.Actions, action => action.Kind == OptimizationActionKind.DnsCacheFlush);
        Assert.DoesNotContain(plan.Actions, action => action.Kind == OptimizationActionKind.ProcessPriorityChange);
    }

    [Fact]
    public void Create_BalancedModeIncludesDnsCacheFlush()
    {
        var plan = new DefaultPlanFactory().Create(BoostMode.Balanced);

        Assert.Contains(plan.Actions, action => action.Kind == OptimizationActionKind.DnsCacheFlush);
    }

    [Fact]
    public void Create_IncludesParameterizedProcessPriorityActionWithGameProcess()
    {
        var plan = new DefaultPlanFactory().Create(BoostMode.Balanced, gameProcessId: 123);

        var action = Assert.Single(plan.Actions, action => action.Kind == OptimizationActionKind.ProcessPriorityChange);
        Assert.Equal("123", action.Parameters[ProcessPriorityActionParameters.ProcessId]);
        Assert.Equal(ManagedProcessPriority.High.ToString(), action.Parameters[ProcessPriorityActionParameters.TargetPriority]);
    }

    [Fact]
    public void Create_IncludesApprovedBackgroundPauseActionFromProfile()
    {
        var profile = new GameProfile(
            Id: "game",
            Name: "Game",
            ExecutablePath: "game.exe",
            RecommendedMode: BoostMode.Balanced,
            AutoRestoreOnExit: true,
            AllowedBackgroundProcessNames: ["Discord"],
            NetworkTargets: []);

        var processes = new[]
        {
            new ProcessSnapshot(
                ProcessId: 321,
                Name: "Discord",
                MainModulePath: null,
                WorkingSetBytes: 500,
                TotalProcessorTime: TimeSpan.Zero,
                IsProtectedCandidate: false)
        };

        var plan = new DefaultPlanFactory().Create(
            BoostMode.Balanced,
            gameProfileId: profile.Id,
            gameProfile: profile,
            processes: processes);

        var action = Assert.Single(plan.Actions, action => action.Kind == OptimizationActionKind.BackgroundAppPause);
        Assert.Equal("321", action.Parameters[BackgroundAppPauseActionParameters.ProcessIds]);
    }

    [Fact]
    public void Create_DoesNotIncludeProtectedBackgroundProcessEvenWhenAllowedByName()
    {
        var profile = new GameProfile(
            Id: "game",
            Name: "Game",
            ExecutablePath: "game.exe",
            RecommendedMode: BoostMode.Balanced,
            AutoRestoreOnExit: true,
            AllowedBackgroundProcessNames: ["vgc"],
            NetworkTargets: []);

        var processes = new[]
        {
            new ProcessSnapshot(
                ProcessId: 321,
                Name: "vgc",
                MainModulePath: null,
                WorkingSetBytes: 500,
                TotalProcessorTime: TimeSpan.Zero,
                IsProtectedCandidate: true)
        };

        var plan = new DefaultPlanFactory().Create(
            BoostMode.Balanced,
            gameProfileId: profile.Id,
            gameProfile: profile,
            processes: processes);

        Assert.DoesNotContain(plan.Actions, action => action.Kind == OptimizationActionKind.BackgroundAppPause);
    }
}
