using System.Diagnostics;
using System.Net.NetworkInformation;
using Miemboost.Core.Diagnostics;

namespace Miemboost.Windows.Diagnostics;

public sealed class WindowsSystemDiagnosticsReader(
    IProtectedProcessClassifier? protectedProcessClassifier = null) : ISystemDiagnosticsReader
{
    private readonly IProtectedProcessClassifier _protectedProcessClassifier =
        protectedProcessClassifier ?? new DefaultProtectedProcessClassifier();

    public async Task<SystemDiagnosticsSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var cpu = await CaptureCpuAsync(cancellationToken).ConfigureAwait(false);
        var memory = CaptureMemory(capturedAt);
        var processes = CaptureProcesses();
        var adapters = CaptureNetworkAdapters();

        return new SystemDiagnosticsSnapshot(cpu, memory, processes, adapters, capturedAt);
    }

    private static async Task<CpuSnapshot> CaptureCpuAsync(CancellationToken cancellationToken)
    {
        var first = Process.GetProcesses().Sum(ReadTotalProcessorMilliseconds);
        var firstAt = Stopwatch.GetTimestamp();

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);

        var second = Process.GetProcesses().Sum(ReadTotalProcessorMilliseconds);
        var secondAt = Stopwatch.GetTimestamp();

        var elapsedMs = (secondAt - firstAt) * 1000d / Stopwatch.Frequency;
        var processorCount = Environment.ProcessorCount;
        var usage = elapsedMs <= 0 || processorCount <= 0
            ? 0
            : Math.Clamp((second - first) / (elapsedMs * processorCount) * 100d, 0, 100);

        return new CpuSnapshot(usage, processorCount, DateTimeOffset.UtcNow);
    }

    private static double ReadTotalProcessorMilliseconds(Process process)
    {
        try
        {
            return process.TotalProcessorTime.TotalMilliseconds;
        }
        catch
        {
            return 0;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static MemorySnapshot CaptureMemory(DateTimeOffset capturedAt)
    {
        var (totalBytes, availableBytes) = WindowsMemoryStatus.Capture();
        return new MemorySnapshot(totalBytes, availableBytes, capturedAt);
    }

    private IReadOnlyList<ProcessSnapshot> CaptureProcesses()
    {
        return Process.GetProcesses()
            .Select(ReadProcess)
            .Where(process => process is not null)
            .Select(process => process!)
            .OrderByDescending(process => process.WorkingSetBytes)
            .ToArray();
    }

    private ProcessSnapshot? ReadProcess(Process process)
    {
        try
        {
            var name = process.ProcessName;
            var path = TryReadMainModulePath(process);

            return new ProcessSnapshot(
                ProcessId: process.Id,
                Name: name,
                MainModulePath: path,
                WorkingSetBytes: process.WorkingSet64,
                TotalProcessorTime: process.TotalProcessorTime,
                IsProtectedCandidate: _protectedProcessClassifier.IsProtectedCandidate(name, path));
        }
        catch
        {
            return null;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static string? TryReadMainModulePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<NetworkAdapterSnapshot> CaptureNetworkAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Select(ReadAdapter)
            .ToArray();
    }

    private static NetworkAdapterSnapshot ReadAdapter(NetworkInterface adapter)
    {
        var statistics = adapter.GetIPv4Statistics();

        return new NetworkAdapterSnapshot(
            Id: adapter.Id,
            Name: adapter.Name,
            Description: adapter.Description,
            Type: adapter.NetworkInterfaceType.ToString(),
            IsUp: adapter.OperationalStatus == OperationalStatus.Up,
            SpeedBitsPerSecond: adapter.Speed,
            BytesReceived: statistics.BytesReceived,
            BytesSent: statistics.BytesSent);
    }
}
