namespace Miemboost.Core.Diagnostics;

public sealed record MemorySnapshot(
    ulong TotalBytes,
    ulong AvailableBytes,
    DateTimeOffset CapturedAt)
{
    public ulong UsedBytes => TotalBytes > AvailableBytes ? TotalBytes - AvailableBytes : 0;

    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100d / TotalBytes;
}
