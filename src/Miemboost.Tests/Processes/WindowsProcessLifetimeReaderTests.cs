using Miemboost.Windows.Processes;

namespace Miemboost.Tests.Processes;

public sealed class WindowsProcessLifetimeReaderTests
{
    [Fact]
    public void IsRunning_ReturnsFalseForInvalidProcessId()
    {
        var reader = new WindowsProcessLifetimeReader();

        Assert.False(reader.IsRunning(-1));
    }
}
