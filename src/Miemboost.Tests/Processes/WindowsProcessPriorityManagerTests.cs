using System.Diagnostics;
using Miemboost.Core.Processes;
using Miemboost.Windows.Processes;

namespace Miemboost.Tests.Processes;

public sealed class WindowsProcessPriorityManagerTests
{
    [Fact]
    public void PriorityMapping_DoesNotExposeRealtime()
    {
        var mapped = WindowsProcessPriorityManager.FromProcessPriorityClass(ProcessPriorityClass.RealTime);

        Assert.Equal(ManagedProcessPriority.Normal, mapped);
    }

    [Fact]
    public void PriorityMapping_MapsHighPriority()
    {
        var mapped = WindowsProcessPriorityManager.ToProcessPriorityClass(ManagedProcessPriority.High);

        Assert.Equal(ProcessPriorityClass.High, mapped);
    }
}
