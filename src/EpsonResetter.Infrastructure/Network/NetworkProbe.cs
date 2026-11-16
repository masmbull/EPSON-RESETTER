using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using EpsonResetter.Core.Services;
using Serilog;

namespace EpsonResetter.Infrastructure.Network;

/// <summary>ICMP and TCP reachability checks used by diagnostics.</summary>
public sealed class NetworkProbe : INetworkProbe
{
    private static readonly ILogger Log = Serilog.Log.ForContext<NetworkProbe>();

    public async Task<NetworkProbeResult> PingAsync(string host, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return NetworkProbeResult.Unreachable("No host address was supplied.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs).WaitAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (reply.Status == IPStatus.Success)
            {
                return NetworkProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
            }

            return NetworkProbeResult.Unreachable($"ICMP status: {reply.Status}.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is PingException or InvalidOperationException or ArgumentException)
        {
            stopwatch.Stop();
            Log.Debug(exception, "Ping to {Host} failed.", host);
            return NetworkProbeResult.Unreachable(exception.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<NetworkProbeResult> TcpConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return NetworkProbeResult.Unreachable("No host address was supplied.");
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return NetworkProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NetworkProbeResult.Unreachable($"Connection to {host}:{port} timed out.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException or InvalidOperationException)
        {
            stopwatch.Stop();
            Log.Debug(exception, "TCP connect to {Host}:{Port} failed.", host, port);
            return NetworkProbeResult.Unreachable(exception.Message, stopwatch.ElapsedMilliseconds);
        }
    }
}