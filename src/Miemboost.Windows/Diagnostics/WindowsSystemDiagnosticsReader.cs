using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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
        var gpu = await CaptureGpuAsync(cancellationToken).ConfigureAwait(false);
        var memory = CaptureMemory(capturedAt);
        var networkStats = CaptureProcessNetworkStats();
        var processes = CaptureProcesses(networkStats);
        var adapters = CaptureNetworkAdapters();

        return new SystemDiagnosticsSnapshot(cpu, gpu, memory, processes, adapters, capturedAt);
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

    private static async Task<GpuSnapshot> CaptureGpuAsync(CancellationToken cancellationToken)
    {
        const string categoryName = "GPU Engine";
        const string counterName = "% Utilization";

        PerformanceCounter[] counters = [];
        try
        {
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                return new GpuSnapshot(0, false, "GPU Engine counter unavailable", DateTimeOffset.UtcNow);
            }

            var category = new PerformanceCounterCategory(categoryName);
            var instances = category.GetInstanceNames()
                .Where(instance => instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .OrderBy(instance => instance, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (instances.Length == 0)
            {
                return new GpuSnapshot(0, false, "No 3D GPU engine counters", DateTimeOffset.UtcNow);
            }

            counters = instances
                .Select(instance => new PerformanceCounter(categoryName, counterName, instance, readOnly: true))
                .ToArray();

            foreach (var counter in counters)
            {
                _ = counter.NextValue();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);

            var usage = counters.Sum(counter => counter.NextValue());
            return new GpuSnapshot(Math.Clamp(usage, 0, 100), true, "Windows GPU Engine", DateTimeOffset.UtcNow);
        }
        catch
        {
            return new GpuSnapshot(0, false, "GPU counter read failed", DateTimeOffset.UtcNow);
        }
        finally
        {
            foreach (var counter in counters)
            {
                counter.Dispose();
            }
        }
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

    private IReadOnlyList<ProcessSnapshot> CaptureProcesses(
        IReadOnlyDictionary<int, ProcessNetworkStats> networkStats)
    {
        return Process.GetProcesses()
            .Select(process => ReadProcess(process, networkStats))
            .Where(process => process is not null)
            .Select(process => process!)
            .OrderByDescending(process => process.WorkingSetBytes)
            .ToArray();
    }

    private ProcessSnapshot? ReadProcess(
        Process process,
        IReadOnlyDictionary<int, ProcessNetworkStats> networkStats)
    {
        try
        {
            var name = process.ProcessName;
            var path = TryReadMainModulePath(process);
            networkStats.TryGetValue(process.Id, out var stats);

            return new ProcessSnapshot(
                ProcessId: process.Id,
                Name: name,
                MainModulePath: path,
                WorkingSetBytes: process.WorkingSet64,
                TotalProcessorTime: process.TotalProcessorTime,
                IsProtectedCandidate: _protectedProcessClassifier.IsProtectedCandidate(name, path),
                TcpConnectionCount: stats.TotalConnections,
                EstablishedTcpConnectionCount: stats.EstablishedConnections);
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

    private static IReadOnlyDictionary<int, ProcessNetworkStats> CaptureProcessNetworkStats()
    {
        try
        {
            var rows = TcpConnectionTableReader.ReadIpv4();
            return rows
                .GroupBy(row => row.ProcessId)
                .ToDictionary(
                    group => group.Key,
                    group => new ProcessNetworkStats(
                        TotalConnections: group.Count(),
                        EstablishedConnections: group.Count(row => row.State == TcpState.Established)));
        }
        catch
        {
            return new Dictionary<int, ProcessNetworkStats>();
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

    private readonly record struct ProcessNetworkStats(
        int TotalConnections,
        int EstablishedConnections);

    private readonly record struct TcpConnectionRow(
        int ProcessId,
        TcpState State);

    private static class TcpConnectionTableReader
    {
        private const int AfInet = 2;
        private const int TcpTableOwnerPidAll = 5;
        private const int ErrorInsufficientBuffer = 122;

        public static IReadOnlyList<TcpConnectionRow> ReadIpv4()
        {
            var bufferSize = 0;
            var result = GetExtendedTcpTable(
                IntPtr.Zero,
                ref bufferSize,
                sort: true,
                ipVersion: AfInet,
                tableClass: TcpTableOwnerPidAll,
                reserved: 0);

            if (result != ErrorInsufficientBuffer || bufferSize <= 0)
            {
                return [];
            }

            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                result = GetExtendedTcpTable(
                    buffer,
                    ref bufferSize,
                    sort: true,
                    ipVersion: AfInet,
                    tableClass: TcpTableOwnerPidAll,
                    reserved: 0);

                if (result != 0)
                {
                    return [];
                }

                var rowCount = Marshal.ReadInt32(buffer);
                var rowPointer = IntPtr.Add(buffer, sizeof(int));
                var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
                var rows = new List<TcpConnectionRow>(rowCount);

                for (var index = 0; index < rowCount; index++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(IntPtr.Add(rowPointer, index * rowSize));
                    rows.Add(new TcpConnectionRow(
                        ProcessId: (int)row.OwningPid,
                        State: row.State));
                }

                return rows;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(
            IntPtr tcpTable,
            ref int tcpTableLength,
            bool sort,
            int ipVersion,
            int tableClass,
            int reserved);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct MibTcpRowOwnerPid
        {
            public readonly TcpState State;
            public readonly uint LocalAddr;
            public readonly uint LocalPort;
            public readonly uint RemoteAddr;
            public readonly uint RemotePort;
            public readonly uint OwningPid;
        }
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
