using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;
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
        var networkStats = await CaptureProcessNetworkStatsAsync(cancellationToken).ConfigureAwait(false);
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

        var maxClock = TryReadCpuMaxClockMHz();
        return new CpuSnapshot(
            UsagePercent: usage,
            LogicalProcessorCount: processorCount,
            CapturedAt: DateTimeOffset.UtcNow,
            TemperatureCelsius: TryReadCpuTemperatureCelsius(),
            CurrentClockMHz: TryReadCpuCurrentClockMHz(maxClock),
            MaxClockMHz: maxClock);
    }

    private static double? TryReadCpuMaxClockMHz()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "~MHz",
                null);

            return value is int mhz && mhz > 0 ? mhz : null;
        }
        catch
        {
            return null;
        }
    }

    private static double? TryReadCpuCurrentClockMHz(double? maxClockMHz)
    {
        try
        {
            using var counter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", readOnly: true);
            _ = counter.NextValue();
            Thread.Sleep(60);
            var performancePercent = counter.NextValue();
            return maxClockMHz is > 0
                ? Math.Max(0, maxClockMHz.Value * performancePercent / 100d)
                : null;
        }
        catch
        {
            return maxClockMHz;
        }
    }

    private static double? TryReadCpuTemperatureCelsius()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                if (item["CurrentTemperature"] is uint rawTemperature)
                {
                    var celsius = rawTemperature / 10d - 273.15d;
                    if (celsius is > 0 and < 130)
                    {
                        return celsius;
                    }
                }
            }
        }
        catch
        {
            // Many desktop systems do not expose CPU temperature through ACPI/WMI.
        }

        return null;
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
            var adapterMemory = CaptureGpuAdapterMemory();
            var vendorMetrics = TryCaptureNvidiaGpuMetrics();
            return new GpuSnapshot(
                UsagePercent: Math.Clamp(usage, 0, 100),
                IsAvailable: true,
                Source: vendorMetrics is null ? "Windows GPU Engine" : "Windows GPU Engine + nvidia-smi",
                CapturedAt: DateTimeOffset.UtcNow,
                TemperatureCelsius: vendorMetrics?.TemperatureCelsius,
                DedicatedMemoryUsedBytes: vendorMetrics?.MemoryUsedBytes ?? adapterMemory.DedicatedUsageBytes,
                DedicatedMemoryTotalBytes: vendorMetrics?.MemoryTotalBytes,
                CoreClockMHz: vendorMetrics?.CoreClockMHz,
                MemoryClockMHz: vendorMetrics?.MemoryClockMHz);
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

    private static GpuAdapterMemorySnapshot CaptureGpuAdapterMemory()
    {
        const string categoryName = "GPU Adapter Memory";

        try
        {
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                return new GpuAdapterMemorySnapshot(null, null);
            }

            var category = new PerformanceCounterCategory(categoryName);
            var dedicatedUsage = ReadGpuAdapterMemoryCounter(category, categoryName, "Dedicated Usage");
            var sharedUsage = ReadGpuAdapterMemoryCounter(category, categoryName, "Shared Usage");
            return new GpuAdapterMemorySnapshot(dedicatedUsage, sharedUsage);
        }
        catch
        {
            return new GpuAdapterMemorySnapshot(null, null);
        }
    }

    private static double? ReadGpuAdapterMemoryCounter(
        PerformanceCounterCategory category,
        string categoryName,
        string counterName)
    {
        try
        {
            return category.GetInstanceNames()
                .Select(instance => new PerformanceCounter(categoryName, counterName, instance, readOnly: true))
                .Select(counter =>
                {
                    using (counter)
                    {
                        return (double)counter.NextValue();
                    }
                })
                .Sum();
        }
        catch
        {
            return null;
        }
    }

    private static NvidiaGpuMetrics? TryCaptureNvidiaGpuMetrics()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi.exe",
                Arguments = "--query-gpu=temperature.gpu,clocks.gr,clocks.mem,memory.used,memory.total --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null || !process.WaitForExit(1500) || process.ExitCode != 0)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            var firstLine = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                return null;
            }

            var values = firstLine.Split(',', StringSplitOptions.TrimEntries);
            if (values.Length < 5)
            {
                return null;
            }

            return new NvidiaGpuMetrics(
                TemperatureCelsius: ParseNullableDouble(values[0]),
                CoreClockMHz: ParseNullableDouble(values[1]),
                MemoryClockMHz: ParseNullableDouble(values[2]),
                MemoryUsedBytes: ParseNullableDouble(values[3]) * 1024d * 1024d,
                MemoryTotalBytes: ParseNullableDouble(values[4]) * 1024d * 1024d);
        }
        catch
        {
            return null;
        }
    }

    private static double? ParseNullableDouble(string text)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private sealed record GpuAdapterMemorySnapshot(
        double? DedicatedUsageBytes,
        double? SharedUsageBytes);

    private sealed record NvidiaGpuMetrics(
        double? TemperatureCelsius,
        double? MemoryUsedBytes,
        double? MemoryTotalBytes,
        double? CoreClockMHz,
        double? MemoryClockMHz);

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
                EstablishedTcpConnectionCount: stats.EstablishedConnections,
                NetworkReceiveBytesPerSecond: stats.ReceiveBytesPerSecond,
                NetworkSendBytesPerSecond: stats.SendBytesPerSecond);
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

    private static async Task<IReadOnlyDictionary<int, ProcessNetworkStats>> CaptureProcessNetworkStatsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var first = TcpConnectionTableReader.ReadIpv4();
            var firstAt = Stopwatch.GetTimestamp();

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);

            var second = TcpConnectionTableReader.ReadIpv4();
            var secondAt = Stopwatch.GetTimestamp();
            var elapsedSeconds = (secondAt - firstAt) / (double)Stopwatch.Frequency;
            var firstByKey = first.ToDictionary(row => row.Key, row => row);

            return second
                .GroupBy(row => row.ProcessId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var receiveBytesPerSecond = 0d;
                        var sendBytesPerSecond = 0d;

                        if (elapsedSeconds > 0)
                        {
                            foreach (var row in group)
                            {
                                if (!firstByKey.TryGetValue(row.Key, out var previous))
                                {
                                    continue;
                                }

                                receiveBytesPerSecond += CalculateRate(previous.BytesReceived, row.BytesReceived, elapsedSeconds);
                                sendBytesPerSecond += CalculateRate(previous.BytesSent, row.BytesSent, elapsedSeconds);
                            }
                        }

                        return new ProcessNetworkStats(
                            TotalConnections: group.Count(),
                            EstablishedConnections: group.Count(row => row.State == TcpState.Established),
                            ReceiveBytesPerSecond: receiveBytesPerSecond,
                            SendBytesPerSecond: sendBytesPerSecond);
                    });
        }
        catch
        {
            return new Dictionary<int, ProcessNetworkStats>();
        }
    }

    private static double CalculateRate(
        ulong previous,
        ulong current,
        double elapsedSeconds)
    {
        return current >= previous
            ? (current - previous) / elapsedSeconds
            : 0;
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
        int EstablishedConnections,
        double ReceiveBytesPerSecond,
        double SendBytesPerSecond);

    private readonly record struct TcpConnectionRow(
        int ProcessId,
        TcpState State,
        TcpConnectionKey Key,
        ulong BytesReceived,
        ulong BytesSent);

    private readonly record struct TcpConnectionKey(
        uint LocalAddress,
        uint LocalPort,
        uint RemoteAddress,
        uint RemotePort);

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
                    var key = new TcpConnectionKey(row.LocalAddr, row.LocalPort, row.RemoteAddr, row.RemotePort);
                    var counters = ReadConnectionCounters(row);
                    rows.Add(new TcpConnectionRow(
                        ProcessId: (int)row.OwningPid,
                        State: row.State,
                        Key: key,
                        BytesReceived: counters.BytesReceived,
                        BytesSent: counters.BytesSent));
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

        private static TcpConnectionCounters ReadConnectionCounters(MibTcpRowOwnerPid ownerRow)
        {
            var tcpRow = new MibTcpRow(
                ownerRow.State,
                ownerRow.LocalAddr,
                ownerRow.LocalPort,
                ownerRow.RemoteAddr,
                ownerRow.RemotePort);
            var data = new TcpEstatsDataRod();
            var size = Marshal.SizeOf<TcpEstatsDataRod>();
            var status = GetPerTcpConnectionEStats(
                ref tcpRow,
                TcpConnectionEstatsData,
                IntPtr.Zero,
                0,
                0,
                IntPtr.Zero,
                0,
                0,
                ref data,
                0,
                (uint)size);

            return status == 0
                ? new TcpConnectionCounters(data.DataBytesIn, data.DataBytesOut)
                : new TcpConnectionCounters(0, 0);
        }

        private const int TcpConnectionEstatsData = 1;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetPerTcpConnectionEStats(
            ref MibTcpRow row,
            int estatsType,
            IntPtr rw,
            uint rwVersion,
            uint rwSize,
            IntPtr ros,
            uint rosVersion,
            uint rosSize,
            ref TcpEstatsDataRod rod,
            uint rodVersion,
            uint rodSize);

        private readonly record struct TcpConnectionCounters(
            ulong BytesReceived,
            ulong BytesSent);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct MibTcpRow(
            TcpState state,
            uint localAddr,
            uint localPort,
            uint remoteAddr,
            uint remotePort)
        {
            public readonly TcpState State = state;
            public readonly uint LocalAddr = localAddr;
            public readonly uint LocalPort = localPort;
            public readonly uint RemoteAddr = remoteAddr;
            public readonly uint RemotePort = remotePort;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TcpEstatsDataRod
        {
            public ulong DataBytesOut;
            public ulong DataSegsOut;
            public ulong DataBytesIn;
            public ulong DataSegsIn;
            public ulong SegsOut;
            public ulong SegsIn;
            public uint SoftErrors;
            public uint SoftErrorReason;
            public uint SndUna;
            public uint SndNxt;
            public uint SndMax;
            public ulong ThruBytesAcked;
            public uint RcvNxt;
            public ulong ThruBytesReceived;
        }

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
