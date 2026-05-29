using Miemboost.Core.AppInfo;

namespace Miemboost.Tests.AppInfo;

public sealed class AppVersionReaderTests
{
    [Fact]
    public void Read_ReturnsProductNameAndVersion()
    {
        var info = AppVersionReader.Read(typeof(AppVersionReader).Assembly);

        Assert.Equal("Miemboost", info.ProductName);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.False(string.IsNullOrWhiteSpace(info.InformationalVersion));
    }
}
