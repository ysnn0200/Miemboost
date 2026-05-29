using Miemboost.Core.Models;
using Miemboost.Core.Planning;
using Miemboost.Core.Processes;

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
    public void Create_IncludesParameterizedProcessPriorityActionWithGameProcess()
    {
        var plan = new DefaultPlanFactory().Create(BoostMode.Balanced, gameProcessId: 123);

        var action = Assert.Single(plan.Actions, action => action.Kind == OptimizationActionKind.ProcessPriorityChange);
        Assert.Equal("123", action.Parameters[ProcessPriorityActionParameters.ProcessId]);
        Assert.Equal(ManagedProcessPriority.High.ToString(), action.Parameters[ProcessPriorityActionParameters.TargetPriority]);
    }
}
