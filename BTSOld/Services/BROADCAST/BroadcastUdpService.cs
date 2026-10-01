using Serilog;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BatteryTestingSystem.Services.BROADCAST;

/// <summary>
/// UDP broadcast service for SADP-style device discovery.
///
/// Protocol:
///   Server → clients : broadcast to 255.255.255.255:10002  (commands)
///   Clients → server : broadcast to 255.255.255.255:10003  (replies)
///
/// Register as Singleton in Program.cs:
///   builder.Services.AddSingleton&lt;BroadcastUdpService&gt;();
/// </summary>
public sealed class BroadcastUdpService : IAsyncDisposable
{
    // ── Protocol ports ────────────────────────────────────────────────────
    private const int SendPort = 10002;   // server broadcasts commands to clients
    private const int ListenPort = 10003;   // server listens for client replies

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>Fired for every reply datagram received on :10003.</summary>
    public event Action<byte[], IPEndPoint>? RawPacketReceived;

    // ── Internal state ────────────────────────────────────────────────────
    private readonly Serilog.ILogger _log = Log.ForContext<BroadcastUdpService>();
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public bool IsListening { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Start the background :10003 reply listener.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        await _startLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsListening) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _listener = new UdpClient(new IPEndPoint(IPAddress.Any, ListenPort));
            _listener.EnableBroadcast = true;
            IsListening = true;

            _receiveTask = ReceiveLoopAsync(_cts.Token);

            _log.Information("BroadcastUdpService listening for replies on :{Port}", ListenPort);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "ReadUPDBroadcast error");
        }
        finally
        {
            _startLock.Release();
        }
    }

    // ── Send ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Broadcast a command to all clients on the local network via 255.255.255.255:10002.
    /// A throw-away socket is used so no fixed source port is occupied.
    /// </summary>
    //public async Task SendBroadcastAsync(byte[] payload, CancellationToken ct = default)
    //{
    //    ArgumentNullException.ThrowIfNull(payload);
    //    if (payload.Length == 0)
    //        throw new ArgumentException("Payload must not be empty.", nameof(payload));

    //    try
    //    {
    //        using var udp = new UdpClient();   // OS assigns ephemeral source port
    //        udp.EnableBroadcast = true;

    //        _log.Debug("SendBroadcastAsync Local endpoint: {Local}", udp.Client.LocalEndPoint);

    //        var broadcastEp = new IPEndPoint(IPAddress.Broadcast, SendPort);
    //        await udp.SendAsync(payload, payload.Length, broadcastEp)
    //                  .WaitAsync(ct)
    //                  .ConfigureAwait(false);

    //        _log.Debug("Broadcast {Len} bytes → 255.255.255.255:{Port}", payload.Length, SendPort);
    //    }
    //    catch (SocketException ex)
    //    {
    //        _log.Error(ex, "Failed to broadcast to 255.255.255.255:{Port}", SendPort);
    //        throw;
    //    }
    //}

    public async Task SendBroadcastAsync(byte[] payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length == 0)
            throw new ArgumentException("Payload must not be empty.", nameof(payload));

        // Get all up, non-loopback interfaces that support IPv4
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic =>
                nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                nic.Supports(NetworkInterfaceComponent.IPv4));

        var tasks = new List<Task>();

        foreach (var nic in interfaces)
        {
            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                // Only IPv4
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var localAddress = unicast.Address;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Bind to this specific interface's IP so the broadcast
                        // goes OUT through it, not through the default route
                        using var udp = new UdpClient(new IPEndPoint(localAddress, 0));
                        udp.EnableBroadcast = true;

                        var broadcastEp = new IPEndPoint(IPAddress.Broadcast, SendPort);
                        await udp.SendAsync(payload, payload.Length, broadcastEp)
                                  .WaitAsync(ct)
                                  .ConfigureAwait(false);

                        _log.Debug("Broadcast {Len} bytes via {IP} → 255.255.255.255:{Port}",
                            payload.Length, localAddress, SendPort);
                    }
                    catch (SocketException ex)
                    {
                        // Log per-interface failure but don't abort others
                        _log.Warning(ex,
                            "Broadcast failed on interface {IP} → 255.255.255.255:{Port}",
                            localAddress, SendPort);
                    }
                }, ct));
            }
            
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    // ── Receive loop ──────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        _log.Debug("Receive loop started on :{Port}", ListenPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listener!.ReceiveAsync(ct).ConfigureAwait(false);

                _log.Debug("Received {Len} bytes from {Remote}",
                    result.Buffer.Length, result.RemoteEndPoint);

                DispatchPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.Interrupted
                                                                  or SocketError.OperationAborted)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ScopeUdpService receive error");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }

        IsListening = false;
        _log.Debug("Receive loop stopped");
    }

    /// <summary>
    /// Invoke <see cref="RawPacketReceived"/> without letting a faulty subscriber
    /// kill the receive loop.
    /// </summary>
    private void DispatchPacket(byte[] buffer, IPEndPoint remote)
    {
        try
        {
            RawPacketReceived?.Invoke(buffer, remote);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "RawPacketReceived subscriber threw — continuing receive loop");
        }
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Receive loop faulted during dispose");
            }
        }

        _listener?.Dispose();
        _cts?.Dispose();
        _startLock.Dispose();
    }
}
