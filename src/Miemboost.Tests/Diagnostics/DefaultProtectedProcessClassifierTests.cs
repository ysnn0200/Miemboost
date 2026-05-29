using Miemboost.Core.Diagnostics;

namespace Miemboost.Tests.Diagnostics;

public sealed class DefaultProtectedProcessClassifierTests
{
    [Theory]
    [InlineData("vgc")]
    [InlineData("EasyAntiCheat")]
    [InlineData("BEService_x64")]
    public void IsProtectedCandidate_ReturnsTrueForKnownAntiCheatNames(string processName)
    {
        var classifier = new DefaultProtectedProcessClassifier();

        Assert.True(classifier.IsProtectedCandidate(processName, null));
    }

    [Fact]
    public void IsProtectedCandidate_ReturnsTrueForAntiCheatPathHints()
    {
        var classifier = new DefaultProtectedProcessClassifier();

        var result = classifier.IsProtectedCandidate(
            "service",
            "C:\\Games\\Example\\EasyAntiCheat\\service.exe");

        Assert.True(result);
    }

    [Fact]
    public void IsProtectedCandidate_ReturnsFalseForNormalUserProcess()
    {
        var classifier = new DefaultProtectedProcessClassifier();

        Assert.False(classifier.IsProtectedCandidate("Discord", "C:\\Users\\User\\AppData\\Discord\\Discord.exe"));
    }
}
