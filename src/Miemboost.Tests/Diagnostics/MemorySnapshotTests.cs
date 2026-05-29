using Miemboost.Core.Diagnostics;

namespace Miemboost.Tests.Diagnostics;

public sealed class MemorySnapshotTests
{
    [Fact]
    public void UsedBytes_ReturnsTotalMinusAvailable()
    {
        var snapshot = new MemorySnapshot(
            TotalBytes: 16,
            AvailableBytes: 6,
            CapturedAt: DateTimeOffset.UnixEpoch);

        Assert.Equal<ulong>(10, snapshot.UsedBytes);
    }

    [Fact]
    public void UsedPercent_ReturnsZeroWhenTotalIsZero()
    {
        var snapshot = new MemorySnapshot(
            TotalBytes: 0,
            AvailableBytes: 0,
            CapturedAt: DateTimeOffset.UnixEpoch);

        Assert.Equal(0, snapshot.UsedPercent);
    }
}
