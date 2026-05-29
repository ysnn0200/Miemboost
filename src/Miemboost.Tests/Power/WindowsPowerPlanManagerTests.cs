using Miemboost.Windows.Power;

namespace Miemboost.Tests.Power;

public sealed class WindowsPowerPlanManagerTests
{
    [Fact]
    public void ParseActivePlan_ReadsEnglishPowerCfgOutput()
    {
        const string output = "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)";

        var plan = WindowsPowerPlanManager.ParseActivePlan(output);

        Assert.NotNull(plan);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", plan.Id);
        Assert.Equal("Balanced", plan.Name);
    }

    [Fact]
    public void ParseActivePlan_ReadsLocalizedPowerCfgOutputWithGuid()
    {
        const string output = "电源方案 GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (高性能)";

        var plan = WindowsPowerPlanManager.ParseActivePlan(output);

        Assert.NotNull(plan);
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", plan.Id);
        Assert.Equal("高性能", plan.Name);
    }
}
