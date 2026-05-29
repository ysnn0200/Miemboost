using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using Miemboost.Core.Diagnostics;

namespace Miemboost.Windows.Diagnostics;

public sealed class WindowsNetworkDiagnosticsReader : INetworkDiagnosticsReader
{
    public async Task<PingProbeResult> ProbePingAsync(
        string target,
        int sampleCount = 4,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (sampleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount), "Sample count must be greater than zero.");
        }

        var timeoutMs = (int)(timeout ?? TimeSpan.FromSeconds(1)).TotalMilliseconds;
        var samples = new List<long>(sampleCount);

        using var ping = new Ping();

        for (var index = 0; index < sampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var reply = await ping.SendPingAsync(target, timeoutMs).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                {
                    samples.Add(reply.RoundtripTime);
                }
            }
            catch
            {
                // A failed sample contributes to packet loss. The result stays diagnostic-only.
            }
        }

        var average = samples.Count == 0 ? 0 : samples.Average();
        var jitter = CalculateJitter(samples);
        var loss = (sampleCount - samples.Count) * 100d / sampleCount;

        return new PingProbeResult(
            Target: target,
            Sent: sampleCount,
            Received: samples.Count,
            PacketLossPercent: loss,
            AverageLatencyMs: average,
            JitterMs: jitter,
            LatencySamplesMs: samples,
            CapturedAt: DateTimeOffset.UtcNow);
    }

    public async Task<DnsProbeResult> ProbeDnsAsync(
        string hostName,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostName, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new DnsProbeResult(
                HostName: hostName,
                Succeeded: true,
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                Addresses: addresses.Select(address => address.ToString()).ToArray(),
                ErrorMessage: null,
                CapturedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();

            return new DnsProbeResult(
                HostName: hostName,
                Succeeded: false,
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                Addresses: Array.Empty<string>(),
                ErrorMessage: exception.Message,
                CapturedAt: DateTimeOffset.UtcNow);
        }
    }

    private static double CalculateJitter(IReadOnlyList<long> samples)
    {
        if (samples.Count < 2)
        {
            return 0;
        }

        var deltas = new List<long>(samples.Count - 1);

        for (var index = 1; index < samples.Count; index++)
        {
            deltas.Add(Math.Abs(samples[index] - samples[index - 1]));
        }

        return deltas.Average();
    }
}
