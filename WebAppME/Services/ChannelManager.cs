using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Models.SqliteEntities;
using BatteryTestingSystem.Services.Implementations;
using BatteryTestingSystem.Services.Interfaces;
using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace BatteryTestingSystem.Services
{
    public class ChannelManager : BackgroundService
    {
        #region custructor

        // Recreated on every StartInternal(): StopInternalAsync completes the writer,
        // and channel completion is irreversible - reusing the same instance across a
        // restart would silently drop every subsequent UDP packet.
        private static Channel<UdpReceiveResult> CreateUdpChannel() =>
            Channel.CreateUnbounded<UdpReceiveResult>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false
                });

        private Channel<UdpReceiveResult> _udpChannel = CreateUdpChannel();

        public event Action<UdpReceiveResult> OnUdpViewDataReceived;

        private Action<UdpReceiveResult>? _udpViewHandler;

        public event Action? HardwareManagerChanged;

        // HardwareManagerChanged is raised from hardware threads, but its Blazor subscribers
        // reload through circuit-SCOPED services, and a circuit's scoped services share one
        // AppDbContext. Registering 64 channels used to raise 64 events within a second, which
        // started 64 overlapping queries on that single context and made EF Core throw
        // "A second operation was started on this context instance".
        //
        // So a raise only sets a dirty flag; a periodic flush turns any burst into one event.
        // A periodic flush rather than a restart-on-every-event debounce is deliberate: a device
        // stuck in a reconnect loop would starve a debounce forever, whereas this has a hard
        // HwFlushIntervalMs ceiling and always fires.
        private const int HwFlushIntervalMs = 250;
        private int _hwDirty;
        private Timer? _hwFlushTimer;
        private UdpClient? _udpStoreDiscovery;
        private UdpClient? _udpViewDiscovery;
        public TcpListener? _commandListener;

        private Task? _udpDataProcesser;
        private Task? _commandListenerTask;
        private Task? _dataStoreListenerTask;
        private Task? _dataViewListenerTask;

        private readonly int commandPort = 9999;
        private readonly int dataStorePort = 10001;
        private readonly int dataViewPort = 10000;
        private readonly Serilog.ILogger _log = Log.ForContext<ChannelManager>();

        #region Listener supervision

        // TCP 9999 and UDP 10000/10001 must stay bound for the life of the process.
        // Every listener runs under SuperviseAsync, which retries forever with
        // exponential backoff instead of letting a bind failure or a mid-flight
        // socket error silently kill the port (the listener Tasks are never awaited,
        // so an escaping exception would otherwise vanish without a trace).
        private const int ListenerBackoffFloorMs = 1_000;
        private const int ListenerBackoffCeilingMs = 30_000;
        private const int ListenerEscalateAfter = 5;

        public const string ListenerHealthTopic = "listener-health";

        public sealed record ListenerState(
            string Name,
            int Port,
            bool IsListening,
            int ConsecutiveFailures,
            string? LastError,
            DateTime? LastErrorUtc);

        private readonly ConcurrentDictionary<string, ListenerState> _listenerStates = new();

        public IReadOnlyCollection<ListenerState> ListenerStates => _listenerStates.Values.ToArray();

        public bool AllListenersHealthy =>
            _listenerStates.Count == 3 && _listenerStates.Values.All(s => s.IsListening);

        #endregion

        public CancellationTokenSource? _lifecycleCts;

        public ConcurrentDictionary<string, IChannelCommandHandler> _devices = new ConcurrentDictionary<string, IChannelCommandHandler>();

        // Keyed by (device, secondary board), not by device alone: the hardware team may
        // give each secondary board its own TCP client, so board 2 connecting must not
        // clobber board 1's already-live socket. Boards that DO share one socket simply
        // end up attached to the same DeviceLink (see _deviceLinks), which is what keeps
        // their writes serialized against each other.
        public ConcurrentDictionary<(int DeviceId, int BoardNumber), DeviceConnection> _deviceConnections = new();

        // One DeviceLink per physical socket, identified by the TcpClient instance itself.
        // Whether a socket carries one board or several is decided by the hardware, and
        // discovered here from the socket each registration packet actually arrives on.
        private readonly ConcurrentDictionary<TcpClient, DeviceLink> _deviceLinks = new();

        public DeviceConnection GetOrCreateDeviceConnection(int deviceId, int boardNumber)
        {
            return _deviceConnections.GetOrAdd((deviceId, boardNumber), _ => new DeviceConnection());
        }

        /// <summary>
        /// The link for a socket, creating it on first sight. Returns null for a null socket:
        /// Add() is also called without one (rehydrating known channels at startup, and via
        /// the operator "Allow" gate), and those callers must not fabricate a link - the board
        /// simply has no live socket yet until its hardware actually dials in.
        /// </summary>
        public DeviceLink? GetOrCreateDeviceLink(TcpClient? tcpClient)
        {
            if (tcpClient == null) return null;

            return _deviceLinks.GetOrAdd(tcpClient, c => new DeviceLink(c));
        }

        private readonly Func<IChannelCommandHandler> _handlerFactory;

        private readonly EventBusService _eventBus;

        private readonly IAlarmService _alarms;

        public ChannelManager(Func<IChannelCommandHandler> handlerFactory, EventBusService eventBus, IAlarmService alarms)
        {
            _handlerFactory = handlerFactory;
            _eventBus = eventBus;
            _alarms = alarms;
            _lifecycleCts = new CancellationTokenSource();
        }

        #endregion

        #region HelperMethods
        public IChannelCommandHandler CreateNewHandler()
        {
            return _handlerFactory();
        }
        private string MakeKey(long deviceId, long boardNumber, long channelId) => $"{deviceId}-{boardNumber}-{channelId}";

        #endregion

        #region BackgroundService_StartUp
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Started before LoadDevicesAsync: that call adds handlers, which raise.
                _hwFlushTimer = new Timer(
                    _ => FlushHardwareManagerChanged(), null, HwFlushIntervalMs, HwFlushIntervalMs);

                await LoadDevicesAsync();

                StartInternal();

                // ?? KEEP SERVICE ALIVE FOREVER
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"ChannelManager fatal error: {ex}");
            }

        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopInternalAsync();

            await base.StopAsync(cancellationToken);

            _log.Warning("ChannelManager stopped.");
        }

        #region Dispose

        public override void Dispose()
        {
            try
            {
                if (_udpViewHandler != null)
                {
                    OnUdpViewDataReceived -= _udpViewHandler;
                }

                _hwFlushTimer?.Dispose();
                _hwFlushTimer = null;

                _lifecycleCts?.Cancel();
                _lifecycleCts?.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Dispose error: {ex.Message}");
            }
            finally
            {
                base.Dispose();
            }
        }

        #endregion

        #endregion

        #region Device Load
        private async Task LoadDevicesAsync()
        {
            try
            {
                using var scoped = ServiceLocator.GetScoped<IDeviceChannelServices>();
                var service = scoped.Service;

                if (service == null) return;

                var result = await service.GetChannelsAsync();

                foreach (var dev in result.Data?.Where(x => x.IsRegistered) ?? [])
                {
                    await Add(dev);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadDevices error: {ex}");
            }
        }

        #endregion

        #region Lifecycle Control

        public void StartInternal()
        {
            // StartInternal is also the restart path - dispose the previous source so a
            // restart does not leak a CancellationTokenSource and its registrations.
            var previousCts = _lifecycleCts;
            _lifecycleCts = new CancellationTokenSource();
            previousCts?.Dispose();

            var token = _lifecycleCts.Token;

            // Fresh channel per lifecycle - see CreateUdpChannel().
            var channel = CreateUdpChannel();
            _udpChannel = channel;

            _udpDataProcesser = Task.Run(() => StartUdpProcessorAsync(channel, token), token);

            // Every socket listener runs under SuperviseAsync so a bind failure or a
            // dead socket is retried instead of silently killing the port.
            _dataStoreListenerTask = Task.Run(() => SuperviseAsync(
                "udp-store", dataStorePort, t => RunUdpStoreListenerAsync(channel, t), token), token);

            _commandListenerTask = Task.Run(() => SuperviseAsync(
                "tcp-command", commandPort, RunCommandListenerAsync, token), token);

            _dataViewListenerTask = Task.Run(() => SuperviseAsync(
                "udp-view", dataViewPort, RunUdpViewListenerAsync, token), token);

            _udpViewHandler = async result => await ViewUdpData(result, token);

            OnUdpViewDataReceived += _udpViewHandler;

        }

        /// <summary>
        /// Keeps one listener bound for the life of the process. A listener that throws -
        /// failed bind, dead socket, unexpected error - is restarted after a backoff that
        /// grows from <see cref="ListenerBackoffFloorMs"/> to a
        /// <see cref="ListenerBackoffCeilingMs"/> ceiling.
        /// <para>
        /// Policy: it never gives up and never stops the host. At a customer site the
        /// hardware ports matter more than this process's own liveness, and stopping the
        /// host would take the web UI - and any chance of remote diagnosis - down with
        /// the ports. After <see cref="ListenerEscalateAfter"/> consecutive failures the
        /// log escalates to Fatal and a <see cref="ListenerHealthTopic"/> event is
        /// published so the UI can raise a banner.
        /// </para>
        /// </summary>
        private async Task SuperviseAsync(
            string name,
            int port,
            Func<CancellationToken, Task> run,
            CancellationToken token)
        {
            var attempt = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await run(token);

                    if (token.IsCancellationRequested) break;

                    // A clean return without cancellation means the loop gave up on its
                    // socket - treat it exactly like a throw.
                    attempt = NextAttempt(name, attempt);
                    ReportListenerFailure(name, port, attempt,
                        $"{name} listener returned unexpectedly on port {port}.");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    attempt = NextAttempt(name, attempt);
                    ReportListenerFailure(name, port, attempt, ex.Message, ex);
                }

                try
                {
                    await Task.Delay(BackoffMs(attempt), token);
                }
                catch (OperationCanceledException) { break; }
            }

            MarkListenerStopped(name, port);
        }

        // A listener that was up and then died gets a fresh 1s retry; repeated bind
        // failures keep climbing towards the ceiling and the Fatal escalation.
        private int NextAttempt(string name, int attempt) =>
            _listenerStates.TryGetValue(name, out var state) && state.IsListening ? 1 : attempt + 1;

        private static int BackoffMs(int attempt) => Math.Min(
            ListenerBackoffCeilingMs,
            ListenerBackoffFloorMs * (1 << Math.Min(Math.Max(attempt - 1, 0), 5)));

        private void MarkListenerListening(string name, int port)
        {
            _listenerStates[name] = new ListenerState(name, port, true, 0, null, null);
            _log.Information("[{Listener}] listening on port {Port}.", name, port);
        }

        private void MarkListenerStopped(string name, int port)
        {
            _listenerStates.TryGetValue(name, out var current);
            _listenerStates[name] = new ListenerState(
                name, port, false, current?.ConsecutiveFailures ?? 0, current?.LastError, current?.LastErrorUtc);
        }

        private void ReportListenerFailure(
            string name, int port, int consecutiveFailures, string message, Exception? ex = null)
        {
            _listenerStates[name] = new ListenerState(
                name, port, false, consecutiveFailures, message, DateTime.UtcNow);

            if (consecutiveFailures >= ListenerEscalateAfter)
            {
                _log.Fatal(ex,
                    "[{Listener}] port {Port} is DOWN after {Count} consecutive failures, still retrying: {Message}",
                    name, port, consecutiveFailures, message);

                _ = PublishListenerHealthAsync();
            }
            else
            {
                _log.Error(ex,
                    "[{Listener}] port {Port} failed (attempt {Count}), retrying in {Delay}ms: {Message}",
                    name, port, consecutiveFailures, BackoffMs(consecutiveFailures), message);
            }
        }

        private async Task PublishListenerHealthAsync()
        {
            try
            {
                await _eventBus.PublishAsync(ListenerHealthTopic, ListenerStates.ToList());
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to publish listener health: {Message}", ex.Message);
            }
        }

        // Windows reports the ICMP port-unreachable from a datagram sent to an offline
        // device as a ConnectionReset exception on the NEXT ReceiveAsync call. Disabling
        // SIO_UDP_CONNRESET stops that, so one powered-off device can no longer take the
        // whole data pipe down.
        private void DisableUdpConnReset(UdpClient udp)
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
                udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception ex)
            {
                _log.Warning($"Could not disable SIO_UDP_CONNRESET: {ex.Message}");
            }
        }

        /// <summary>
        /// Marks the hardware view as changed. Cheap and thread-safe: subscribers are not called
        /// here, only on the next <see cref="FlushHardwareManagerChanged"/> tick, so a burst of
        /// registrations costs one notification instead of one per channel.
        /// </summary>
        private void RaiseHardwareManagerChanged() => Interlocked.Exchange(ref _hwDirty, 1);

        // A throwing subscriber must never be able to kill the flush timer.
        private void FlushHardwareManagerChanged()
        {
            if (Interlocked.Exchange(ref _hwDirty, 0) == 0) return;

            try
            {
                HardwareManagerChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"HardwareManagerChanged subscriber error: {ex.Message}");
            }
        }

        private async Task StopInternalAsync()
        {

            Console.WriteLine("ChannelManager stopping...");

            if (_udpViewHandler != null)
            {
                OnUdpViewDataReceived -= _udpViewHandler;
            }

            _lifecycleCts?.Cancel();

            // Completing the writer lets the processor drain and exit. This is
            // irreversible for THIS channel instance - StartInternal creates a new one.
            _udpChannel.Writer.TryComplete();

            _commandListener?.Stop();
            _udpStoreDiscovery?.Dispose();
            _udpViewDiscovery?.Dispose();

            var tasks = new[]
            {
                _udpDataProcesser,
                _dataStoreListenerTask,
                _commandListenerTask,
                _dataViewListenerTask
            }
            .Where(t => t != null)
            .Cast<Task>()
            .ToArray();

            if (tasks.Length > 0)
            {
                await Task.WhenAny(
                Task.WhenAll(tasks),
                    Task.Delay(TimeSpan.FromSeconds(5))
                );
            }
        }

        public async Task RestartService()
        {
            Console.WriteLine("Restarting ChannelManager...");
            await StopInternalAsync();
            await Task.Delay(5000);
            StartInternal();
        }

        #endregion

        #region Channel Handler
        public CommonResponse<IChannelCommandHandler?> Get(ChannelDto channel)
        {
            try
            {
                var key = MakeKey(channel.DeviceID, channel.SecondaryBoardNumber, channel.ChannelNumber);

                if (_devices.TryGetValue(key, out var handler))
                    return CommonResponse<IChannelCommandHandler?>.Ok(handler);

                return CommonResponse<IChannelCommandHandler?>.Fail("Channel Handler not found.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Get error: {ex.Message}");
                return CommonResponse<IChannelCommandHandler?>.Fail($"Error: {ex.Message}");
            }
        }

        public async Task<CommonResponse<IChannelCommandHandler>> Add(ChannelDto channel, TcpClient tcpClient = null)
        {
            try
            {
                if (!channel.IsRegistered)
                    return CommonResponse<IChannelCommandHandler>.Fail($"Please allow Channel is to registered this Channel. {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber}");

                var key = MakeKey(channel.DeviceID, channel.SecondaryBoardNumber, channel.ChannelNumber);

                var IsHandler = Get(channel);

                if (IsHandler.Success)
                {
                    var oldClient = IsHandler.Data._tcpClient;

                    // If same instance, do nothing except re-arm this channel's own
                    // "am I marked offline" flag. _tcpClient is a live pass-through to
                    // the shared per-device DeviceConnection.TcpClient - the first
                    // channel to register after a reconnect is what actually updates
                    // that shared reference, so every OTHER channel on the same device
                    // sees it as "already assigned" here and would otherwise never run
                    // the Connection assignment below that resets _manuallyDisconnected,
                    // leaving its card stuck showing Offline forever (the "only 1 of N
                    // cards shows Connected after restart" bug - reproduced live with a
                    // 64-channel device: only channel 1 came back, 2-64 stayed Offline
                    // despite live UDP data still flowing).
                    if (ReferenceEquals(oldClient, tcpClient))
                    {
                        IsHandler.Data.Connection = IsHandler.Data.Connection;
                        GetOrCreateDeviceConnection(channel.DeviceID, channel.SecondaryBoardNumber).ChannelSlotKeys.Add(key);

                        _log.Debug($"TCP Client already assigned for Device={channel.DeviceID}, Board={channel.SecondaryBoardNumber}, Channel={channel.ChannelNumber}");

                        return CommonResponse<IChannelCommandHandler>.Ok(
                              IsHandler.Data,
                              $"Channel Handler already TCP Client for Device={channel.DeviceID}, Board={channel.SecondaryBoardNumber}, Channel={channel.ChannelNumber}."
                          );

                    }

                    // Reattach to the new socket - the same DeviceConnection instance
                    // is reused so ChannelSlotKeys/pending-response state carries over,
                    // and the shared TcpClient is what actually changes here (the old
                    // socket, if any, is superseded rather than explicitly closed - the
                    // read-loop owning it will exit on its own once it observes the
                    // connection is gone).
                    var existingDeviceConnection = GetOrCreateDeviceConnection(channel.DeviceID, channel.SecondaryBoardNumber);
                    existingDeviceConnection.AttachLink(GetOrCreateDeviceLink(tcpClient));
                    existingDeviceConnection.ChannelSlotKeys.Add(key);
                    IsHandler.Data.Connection = existingDeviceConnection;

                    _log.Debug($"Assigned new TCP Client for Device={channel.DeviceID}, Board={channel.SecondaryBoardNumber}, Channel={channel.ChannelNumber}");

                    return CommonResponse<IChannelCommandHandler>.Ok(
                        IsHandler.Data,
                        $"Channel Handler already exists for Device={channel.DeviceID}, Board={channel.SecondaryBoardNumber}, Channel={channel.ChannelNumber}."
                    );
                }

                var deviceConnection = GetOrCreateDeviceConnection(channel.DeviceID, channel.SecondaryBoardNumber);
                deviceConnection.AttachLink(GetOrCreateDeviceLink(tcpClient));
                deviceConnection.ChannelSlotKeys.Add(key);

                var handler = CreateNewHandler();
                handler._cts = _lifecycleCts;
                handler.Channel = channel;
                handler.Connection = deviceConnection;
                await handler.InitializeAsync();

                _devices.TryAdd(key, handler);

                return CommonResponse<IChannelCommandHandler>.Ok(
                    handler,
                    $"Channel Handler added for Device={handler.Channel.DeviceID}, Board={handler.Channel.SecondaryBoardNumber}, Channel={handler.Channel.ChannelNumber}."
                );
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Add error: {ex.Message}");
                return CommonResponse<IChannelCommandHandler>.Fail($"Error adding Channel handler: {ex.Message}");
            }
            finally
            {
                RaiseHardwareManagerChanged();
            }
        }
        public CommonResponse<bool> Remove(ChannelDto channel, bool force = false)
        {
            if (channel == null)
                return CommonResponse<bool>.Fail("Channel is null.");

            var key = MakeKey(channel.DeviceID, channel.SecondaryBoardNumber, channel.ChannelNumber);

            try
            {
                if (!_devices.TryGetValue(key, out var handler))
                    return CommonResponse<bool>.Fail("Channel Handler not found.");

                // Disconnect safely
                try
                {
                    handler.UnregisterAsync();

                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Disconnect error: {ex.Message}");
                }

                return CommonResponse<bool>.Ok(true, "Channel Handler removed.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Remove error: {ex.Message}");
                return CommonResponse<bool>.Fail($"Error removing Channel handler: {ex.Message}");
            }
            finally
            {
                RaiseHardwareManagerChanged();
            }
        }

        #endregion

        #region UDP View
        private async Task RunUdpViewListenerAsync(CancellationToken token)
        {
            // Bind outside the receive loop but inside the supervised body, and keep the
            // loop OUTSIDE the try so one bad datagram cannot end the listener.
            var udp = new UdpClient(dataViewPort);
            DisableUdpConnReset(udp);
            _udpViewDiscovery = udp;
            MarkListenerListening("udp-view", dataViewPort);

            const int maxReceiveFailures = 5;
            var receiveFailures = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udp.ReceiveAsync(token);
                        receiveFailures = 0;

                        // Trigger event for received data
                        try
                        {
                            OnUdpViewDataReceived?.Invoke(result);
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, $"UDP View subscriber error: {ex.Message}");
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        // ICMP port-unreachable from an offline device - keep receiving.
                        _log.Debug("UDP View: ignoring ICMP connection reset.");
                    }
                    catch (Exception ex)
                    {
                        receiveFailures++;
                        _log.Error(ex, $"UDP View Listener error {receiveFailures}/{maxReceiveFailures}: {ex.Message}");

                        if (receiveFailures >= maxReceiveFailures) throw;

                        await Task.Delay(200, token);
                    }
                }
            }
            finally
            {
                if (ReferenceEquals(_udpViewDiscovery, udp)) _udpViewDiscovery = null;
                udp.Dispose();
            }
        }

        private async Task ViewUdpData(UdpReceiveResult result, CancellationToken token)
        {
            try
            {
                var payload = result.Buffer;
                if (payload == null || payload.Length < 4) return;

                byte identify = payload[0];
                int deviceId = payload[1];
                var (boardNumber, channelNumber) = Utils.ChannelAddressCodec.Decode(payload[2]);
                byte type = payload[3];

                var handler = Get(new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = boardNumber, ChannelNumber = channelNumber });

                if (!handler.Success || handler.Data == null)
                {
                    _log.Error("No handler found for DeviceID={DeviceID}, Board={Board}, ChannelID={ChannelID}", deviceId, boardNumber, channelNumber);
                    return;
                }

                var data = handler.Data;

                switch ((StartByte)identify)
                {
                    case StartByte.LiveData when type == 0x01:
                    {
                        var rec = DecoderService.ParseRealTimeData(payload);
                        if (rec != null)
                        {
                            data.RealTime.IOStatus = rec.IOStatus;
                            data.RealTime.NotifyDataChanged(rec.RealTimeRecord);
                        }
                        break;
                    }
                    case StartByte.LiveData when type == 0x02:
                    {
                        var rec = DecoderService.ParseDBCValues(payload);
                        data.dbcData.CreatedDate = DateTime.Now;
                        data.dbcData.DbcValues = rec.DbcValues;
                        break;
                    }
                    case StartByte.Calibration:
                    {
                        var rec = DecoderService.ParseRealTimeData(payload);
                        if (rec == null) break;

                        var buffer = data.calibration.calibrationBuffer;
                        if (buffer.Count > 3) buffer.RemoveAt(0);
                        buffer.Add(rec.RealTimeRecord);
                        data.RealTime.NotifyDataChanged(rec.RealTimeRecord);
                        break;
                    }
                    default:
                        _log.Debug("Received unknown UDP packet with identify byte: {Identify:X2}", identify);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error processing UDP data: {Message}", ex.Message);
            }
        }

        #endregion

        #region UDP Store Listener
        private async Task RunUdpStoreListenerAsync(Channel<UdpReceiveResult> channel, CancellationToken token)
        {
            // Loop OUTSIDE the try - one bad datagram must not end the listener.
            var udp = new UdpClient(dataStorePort);
            DisableUdpConnReset(udp);
            _udpStoreDiscovery = udp;
            MarkListenerListening("udp-store", dataStorePort);

            const int maxReceiveFailures = 5;
            var receiveFailures = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udp.ReceiveAsync(token);
                        receiveFailures = 0;

                        // NON-BLOCKING. TryWrite instead of a discarded WriteAsync: on a
                        // completed writer that returned a faulted ValueTask nobody ever
                        // observed, so packets vanished silently.
                        if (!channel.Writer.TryWrite(result))
                        {
                            _log.Error("UDP Store: channel refused the packet (writer completed) - packet dropped.");
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        // ICMP port-unreachable from an offline device - keep receiving.
                        _log.Debug("UDP Store: ignoring ICMP connection reset.");
                    }
                    catch (Exception ex)
                    {
                        receiveFailures++;
                        _log.Error(ex, $"UDP Store Listener error {receiveFailures}/{maxReceiveFailures}: {ex.Message}");

                        if (receiveFailures >= maxReceiveFailures) throw;

                        await Task.Delay(200, token);
                    }
                }
            }
            finally
            {
                if (ReferenceEquals(_udpStoreDiscovery, udp)) _udpStoreDiscovery = null;
                udp.Dispose();
            }
        }

        private async Task StartUdpProcessorAsync(Channel<UdpReceiveResult> channel, CancellationToken token)
        {
            _log.Information($"UDP data store processing....");

            try
            {
                await foreach (var packet in channel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        // await TimingHelper.MeasureAsync("StoreUdpData", async () =>
                        // {
                        //     await StoreUdpData(packet, token);
                        // });

                        await StoreUdpData(packet, token);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"UDP data processing error: {ex.Message}");
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    _log.Fatal("UDP data processor exited while still running - stored data will stop until restart.");
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                _log.Fatal(ex, $"UDP data processor stopped: {ex.Message}");
            }
        }

        private async Task StoreUdpData(UdpReceiveResult result, CancellationToken token)
        {
            try
            {
                var payload = result.Buffer;
                var decoded = DecoderService.RealStoreDataV2(payload);

                if (!decoded.Success)
                {
                    _log.Debug("Failed to decode StoreUdpData: {Message}", decoded.Message);
                    await TrySendFailureNotification(payload, decoded.Message);
                    return;
                }

                var first = decoded.Data?.RealStoreRecord.FirstOrDefault();
                if (first == null)
                {
                    _log.Debug("StoreUdpData: no records in decoded payload. {Message}", decoded.Message);
                    return;
                }

                var handlerResult = Get(new ChannelDto { DeviceID = first.DeviceId, SecondaryBoardNumber = first.SecondaryBoardNumber, ChannelNumber = first.ChannelId });
                if (!handlerResult.Success || handlerResult.Data == null)
                {
                    _log.Debug("No handler found for DeviceID={DeviceID}, Board={Board}, ChannelID={ChannelID}", first.DeviceId, first.SecondaryBoardNumber, first.ChannelId);
                    return;
                }

                // Session ID is a packed value: [DeviceID 8b][Board/Channel address 8b][epoch-low16 16b]
                // Use SessionIdToDateTime to correctly reconstruct the date from the low-16 epoch bits.
                bool validEpoch = DecoderService.SessionIdToDateTime(first.SessionID, out DateTime sessionDateTime, true);
                if (!validEpoch)
                {
                    _log.Debug("StoreUdpData: could not reconstruct date from SessionID - falling back to DateTime.Now.");
                    sessionDateTime = DateTime.Now;
                }

                decoded.Data.filePath = Path.Combine(sessionDateTime.ToString("dd-MM-yyyy"), $"{first.SessionID}_{first.DeviceId}_{first.SecondaryBoardNumber}_{first.ChannelId}.db");

                handlerResult.Data.EnqueueForStore(decoded.Data, handlerResult.Data.dbcData?.DbcValues);
                _ = _eventBus.PublishAsync<List<MeasurementData>>(decoded.Data.filePath, decoded.Data?.RealStoreRecord);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error processing StoreUdpData: {Message}", ex.Message);
            }
        }

        private async Task TrySendFailureNotification(byte[]? payload, string message)
        {
            if (payload == null || payload.Length < 2) return;

            int deviceId = payload[0];
            // NOTE: unverified whether payload[1] is genuinely a full board/channel address byte
            // or a bare legacy channel number — confirm what actually produces this packet before
            // trusting this decode. See design doc risk #7.
            var (boardNumber, channelNumber) = Utils.ChannelAddressCodec.Decode(payload[1]);

            var handler = Get(new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = boardNumber, ChannelNumber = channelNumber });
            if (!handler.Success || handler.Data == null) return;

            await _alarms.RaiseAsync(new AlarmRequest
            {
                AlarmKey = $"{deviceId}/{boardNumber}/{channelNumber}/store-failed",
                Severity = SeverityLevel.ERROR,
                Source = AlarmSource.DataStore,
                DeviceId = deviceId.ToString(),
                BoardNumber = boardNumber,
                ChannelNumber = channelNumber,
                Title = $"Data store failed - device {deviceId} ch {channelNumber}",
                Message = message
            });
        }

        #endregion

        #region TPC Listener
        private async Task RunCommandListenerAsync(CancellationToken token)
        {
            // Bind inside the supervised body: a failed Start() (port 9999 already held
            // by a stale instance) must reach SuperviseAsync, not vanish into Task.Run.
            var listener = new TcpListener(IPAddress.Any, commandPort);
            listener.Server.SetSocketOption(
                                SocketOptionLevel.Socket,
                                SocketOptionName.KeepAlive,
                                true);

            listener.Start();
            _commandListener = listener;
            MarkListenerListening("tcp-command", commandPort);

            // A listener socket can reach a state where AcceptTcpClientAsync throws
            // immediately on every call. Without this counter the loop spins at 100% CPU
            // and floods the log; after a few failures we hand the socket back to
            // SuperviseAsync for a clean rebind.
            const int maxAcceptFailures = 5;
            var acceptFailures = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await listener.AcceptTcpClientAsync(token);
                        acceptFailures = 0;
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        acceptFailures++;
                        _log.Error(ex, $"[Command Accept Error {acceptFailures}/{maxAcceptFailures}] {ex.Message}");

                        if (acceptFailures >= maxAcceptFailures) throw;

                        await Task.Delay(500, token);
                        continue;
                    }

                    var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
                    _log.Information($"Command client connected: {clientId}");
                    _ = HandleCommandClientAsync(client, token);

                    // Deliberately not in a finally: this must fire per accepted client,
                    // not once when the accept loop finally exits.
                    RaiseHardwareManagerChanged();
                }
            }
            finally
            {
                try { listener.Stop(); } catch { /* already torn down */ }
                if (ReferenceEquals(_commandListener, listener)) _commandListener = null;
            }
        }

        private async Task HandleCommandClientAsync(TcpClient client, CancellationToken token)
        {
            var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
            _log.Information($"client Enter in HandleCommandClientAsync: {clientId}");

            // Every (device, board) that has registered on THIS socket, keyed by board
            // number. Usually one entry - but if the hardware multiplexes several
            // secondary boards over a single TCP client, each one registers separately
            // and lands here, so responses can be routed to the right board's pending
            // requests and a socket close can take down exactly the boards it carried.
            var boardsOnThisSocket = new Dictionary<int, DeviceConnection>();

            try
            {
                var stream = client.GetStream();
                // Must be large enough for every response type multiplexed through this
                // per-device loop, not just the 33-byte registration packet — the largest
                // (DecoderService.ParseCalibrationPayload's PreviousCalibration response)
                // is 163 bytes. A too-small buffer truncates a packet mid-frame, and the
                // leftover bytes get read as the START of the next iteration, corrupting
                // buffer[2] (the address byte HandleIncomingPacket routes responses on)
                // for every packet after it. 1024 matches the buffer size the old
                // per-channel SendAndWaitForResponseAsync used before this shared-socket
                // read loop replaced it.
                byte[] buffer = new byte[1024];

                while (!token.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, token);

                    if (read == 0)
                    {
                        _log.Information("Connection closed by remote for {ClientId}", clientId);
                        break;
                    }

                    // Registration header
                    if (buffer[0] == 0xDD && buffer[1] == 0x01)
                    {
                        var registrationResult = await ProcessRegistrationPacketAsync(buffer, client);
                        if (registrationResult != null)
                        {
                            var (parsedChannel, response) = registrationResult.Value;
                            boardsOnThisSocket[parsedChannel.SecondaryBoardNumber] =
                                GetOrCreateDeviceConnection(parsedChannel.DeviceID, parsedChannel.SecondaryBoardNumber);
                            await SendOnly(client, response, token);
                        }
                        continue;
                    }

                    // Not a registration packet - route to the device's DeviceConnection
                    // as an in-flight response or an unrecognized unsolicited packet.
                    // Correlate on (addressByte, queryId) - buffer[2] is the board/channel
                    // address byte, buffer[3] is the query type - so two different query
                    // types in flight for the same channel don't collide on one slot.
                    if (boardsOnThisSocket.Count > 0 && read >= 3)
                    {
                        byte queryId = read >= 4 ? buffer[3] : (byte)0;

                        // The address byte carries the board in its high nibble, so a socket
                        // shared by several boards can still hand each response to the board
                        // that actually asked for it. Fall back to the only registered board
                        // when the decoded board is unknown here (a malformed address byte
                        // decodes to a board that never registered - Decode does not validate).
                        var (packetBoard, _) = Utils.ChannelAddressCodec.Decode(buffer[2]);

                        if (!boardsOnThisSocket.TryGetValue(packetBoard, out var target) &&
                            boardsOnThisSocket.Count == 1)
                        {
                            target = boardsOnThisSocket.Values.First();
                        }

                        if (target != null)
                        {
                            target.HandleIncomingPacket(buffer[2], queryId, buffer.Take(read).ToArray());
                        }
                        else
                        {
                            _log.Warning(
                                "Packet for board {Board} on {ClientId} has no registered board connection - dropped.",
                                packetBoard, clientId);
                        }
                    }
                    else
                    {
                        _log.Warning("Received non-registration packet before any channel registered on {ClientId}.", clientId);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[HandleCommandClientAsync Error] " + ex.Message);
            }
            finally
            {
                // Losing the socket loses every board that was riding it, so tear down each
                // one this loop saw register - not just the last.
                //
                // Each board is still guarded individually: only clean up a board whose link
                // is STILL this socket. Without that guard, a simulator restart races - the
                // new socket's registrations can already reattach some/all channels
                // (ChannelManager.Add resets Connection -> _manuallyDisconnected = false)
                // before this stale loop's finally observes the OLD socket's close, which
                // would otherwise re-mark those just-reconnected channels offline (the
                // "only 1 of 4 cards shows Connected after restart" bug).
                foreach (var (boardNumber, boardConnection) in boardsOnThisSocket)
                {
                    if (!boardConnection.IsOn(client))
                        continue;

                    boardConnection.FailAllPending(new IOException("Device connection closed."));

                    foreach (var slotKey in boardConnection.ChannelSlotKeys)
                    {
                        if (_devices.TryGetValue(slotKey, out var slotHandler))
                            slotHandler.MarkDisconnected();
                    }

                    // One alarm per (device, board) link, not per channel slot - a board that
                    // drops is one fault, and raising it per channel is exactly the noise the
                    // storm guard would otherwise have to clean up. Keyed per board because
                    // boards can now be on separate sockets: board 3 dropping while board 1
                    // stays up is a real, separately-clearable fault.
                    var lostDeviceId = boardConnection.ChannelSlotKeys
                        .Select(k => k.Split('-').FirstOrDefault())
                        .FirstOrDefault(d => !string.IsNullOrEmpty(d));

                    if (!string.IsNullOrEmpty(lostDeviceId))
                    {
                        await _alarms.RaiseAsync(new AlarmRequest
                        {
                            AlarmKey = $"{lostDeviceId}/{boardNumber}/comms-loss",
                            Severity = SeverityLevel.CRITICAL,
                            Source = AlarmSource.Comms,
                            DeviceId = lostDeviceId,
                            BoardNumber = boardNumber,
                            Title = $"Device {lostDeviceId} board {boardNumber} disconnected",
                            Message = "TCP command connection closed. No data is being recorded " +
                                      "for this board until it reconnects."
                        });
                    }
                }

                // Drop the physical link once no board is on it any more, so a reconnecting
                // socket never inherits a stale write lock (and the table can't grow forever).
                if (!_deviceConnections.Values.Any(c => c.IsOn(client)))
                    _deviceLinks.TryRemove(client, out _);

                _log.Information($"[HandleCommandClientAsync exited {clientId}]");
                RaiseHardwareManagerChanged();
            }
        }

        /// <summary>
        /// Parses and processes one registration packet. Returns the parsed channel and
        /// the response bytes to send back, or null if the packet was invalid.
        /// </summary>
        private async Task<(ChannelDto Channel, byte[] Response)?> ProcessRegistrationPacketAsync(byte[] buffer, TcpClient client)
        {
            var newChannel = DecoderService.ParseRegistrationPacket(buffer);

            if (newChannel == null)
            {
                _log.Warning("Invalid registration packet.");
                return null;
            }

            // A board that sends any valid registration packet has a working link: retire the
            // outstanding comms-loss alarm rather than leaving a stale fault in the bell. This is
            // independent of whether the DB registration below succeeds - the TCP link is what
            // the alarm is about. Keyed per (device, board) to match how the fault is raised:
            // board 1 coming back must not clear a still-dead board 3's alarm.
            await _alarms.ClearAsync($"{newChannel.DeviceID}/{newChannel.SecondaryBoardNumber}/comms-loss");

            byte[] responseBytes = DecoderService.ParseRegistrationResponse(0, 0, CommandStatus.Failed);

            using var scoped = ServiceLocator.GetScoped<IDeviceChannelServices>();
            var db = scoped.Service;

            try
            {
                // Check if channel exists
                var existingChannel = await db.GetChannelAsync(newChannel);

                if (existingChannel.Success && existingChannel.Data != null && existingChannel.Data.IsRegistered)
                {
                    // Try adding channel to handler
                    var addResult = await Add(existingChannel.Data, client);

                    if (addResult.Success)
                    {
                        // Already registered
                        if (addResult.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                        {
                            responseBytes = DecoderService.ParseRegistrationResponse(
                                (int)newChannel.DeviceID,
                                Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                                CommandStatus.AlreadyRegistered
                            );

                            // Update into Database
                            await db.UpdateRegistration(newChannel);
                        }
                        else
                        {
                            // New registration success
                            responseBytes = DecoderService.ParseRegistrationResponse(
                                (int)newChannel.DeviceID,
                                Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                                CommandStatus.Success
                            );
                        }
                    }
                    else
                    {
                        responseBytes = DecoderService.ParseRegistrationResponse(
                               (int)newChannel.DeviceID,
                               Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                               CommandStatus.Failed
                           );
                    }
                }
                else if (existingChannel.Success && existingChannel.Data != null && !existingChannel.Data.IsRegistered)
                {
                    // check if deleted then enable it.
                    if (existingChannel.Data.IsDeleted)
                    {
                        await db.UpdateIsDeleteAsync(newChannel, false);
                    }

                    responseBytes = DecoderService.ParseRegistrationResponse(
                        (int)newChannel.DeviceID,
                        Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                        CommandStatus.Failed
                    );
                }
                else
                {
                    // Insert new record into DB
                    if (existingChannel.Data == null)
                        await db.InsertAsync(newChannel);

                    responseBytes = DecoderService.ParseRegistrationResponse(
                        (int)newChannel.DeviceID,
                        Utils.ChannelAddressCodec.Encode(newChannel.SecondaryBoardNumber, newChannel.ChannelNumber),
                        CommandStatus.Failed
                    );
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "DB Error: " + ex.Message);
            }

            return (newChannel, responseBytes);
        }


        public async Task<bool> SendOnly(TcpClient client, byte[] Payload, CancellationToken token)
        {

            try
            {
                var stream = client.GetStream();
                await stream.WriteAsync(Payload, token);
                await stream.FlushAsync(token);
                await Task.Delay(20, token);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                RaiseHardwareManagerChanged();
            }
        }

        #endregion

    }
}
