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
    public class CircuitManager : BackgroundService
    {
        #region custructor

        private readonly Channel<UdpReceiveResult> _udpChannel =
            Channel.CreateUnbounded<UdpReceiveResult>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false
                });

        public event Action<UdpReceiveResult> OnUdpViewDataReceived;
        
        private Action<UdpReceiveResult>? _udpViewHandler;

        public event Action? HardwareManagerChanged;
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
        private readonly Serilog.ILogger _log = Log.ForContext<CircuitManager>();

        public CancellationTokenSource? _lifecycleCts;

        public ConcurrentDictionary<string, ICircuitCommandHandler> _devices = new ConcurrentDictionary<string, ICircuitCommandHandler>();
        
        private readonly Func<ICircuitCommandHandler> _handlerFactory;

        private readonly EventBusService _eventBus;

        public CircuitManager(Func<ICircuitCommandHandler> handlerFactory, EventBusService eventBus)
        {
            _handlerFactory = handlerFactory;
            _eventBus = eventBus;
            _lifecycleCts = new CancellationTokenSource();
        }

        #endregion

        #region HelperMethods
        public ICircuitCommandHandler CreateNewHandler()
        {
            return _handlerFactory();
        }
        private string MakeKey(long deviceId, long circuitId) => $"{deviceId}-{circuitId}";

        #endregion

        #region BackgroundService_StartUp
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
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
                _log.Error(ex, $"CircuitManager fatal error: {ex}");
            }

        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopInternalAsync();

            await base.StopAsync(cancellationToken);

            _log.Warning("CircuitManager stopped.");
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
                using var scoped = ServiceLocator.GetScoped<IDeviceCircuitServices>();
                var service = scoped.Service;

                if (service == null) return;

                var result = await service.GetCircuitsAsync();

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

            _lifecycleCts = new CancellationTokenSource();
            // Restart UDP
            _udpDataProcesser = Task.Run(() => StartUdpProcessorAsync(_lifecycleCts.Token), _lifecycleCts.Token);
            // Restart UDP Listener
            _dataStoreListenerTask = Task.Run(() => RunUdpStoreListenerAsync(_lifecycleCts.Token), _lifecycleCts.Token);
            // Restart Command Listener
            _commandListenerTask = Task.Run(() => RunCommandListenerAsync(_lifecycleCts.Token), _lifecycleCts.Token);

            _dataViewListenerTask = Task.Run(() => RunUdpViewListenerAsync(_lifecycleCts.Token), _lifecycleCts.Token);

            _udpViewHandler = async result => await ViewUdpData(result, _lifecycleCts.Token);

            OnUdpViewDataReceived += _udpViewHandler;

        }

        private async Task StopInternalAsync()
        {

            Console.WriteLine("CircuitManager stopping...");

            if (_udpViewHandler != null)
            {
                OnUdpViewDataReceived -= _udpViewHandler;
            }

            _lifecycleCts?.Cancel();
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
            Console.WriteLine("Restarting CircuitManager...");
            await StopInternalAsync();
            await Task.Delay(5000);
            StartInternal();
        }

        #endregion

        #region Circuit Handler 
        public CommonResponse<ICircuitCommandHandler?> Get(CircuitDto circuit)
        {
            try
            {
                var key = MakeKey(circuit.DeviceID, circuit.CircuitID);

                if (_devices.TryGetValue(key, out var handler))
                    return CommonResponse<ICircuitCommandHandler?>.Ok(handler);

                return CommonResponse<ICircuitCommandHandler?>.Fail("Circuit Handler not found.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Get error: {ex.Message}");
                return CommonResponse<ICircuitCommandHandler?>.Fail($"Error: {ex.Message}");
            }
        }
        
        public async Task<CommonResponse<ICircuitCommandHandler>> Add(CircuitDto circuit, TcpClient tcpClient = null)
        {
            try
            {
                if (!circuit.IsRegistered)
                    return CommonResponse<ICircuitCommandHandler>.Fail($"Please allow Circuit is to registered this Circuit. {circuit.DeviceID}-{circuit.CircuitID}");

                var key = MakeKey(circuit.DeviceID, circuit.CircuitID);

                var IsHandler = Get(circuit);

                if (IsHandler.Success)
                {
                    var oldClient = IsHandler.Data._tcpClient;

                    // If same instance, do nothing
                    if (ReferenceEquals(oldClient, tcpClient))
                    {
                        _log.Debug($"TCP Client already assigned for Device={circuit.DeviceID}, Circuit={circuit.CircuitID}");

                        return CommonResponse<ICircuitCommandHandler>.Ok(
                              IsHandler.Data,
                              $"Circuit Handler already TCP Client for Device={circuit.DeviceID}, Circuit={circuit.CircuitID}."
                          );

                    }

                    // Close old client if it exists
                    if (oldClient != null)
                    {
                        try
                        {
                            _log.Debug($"Closing old TCP Client for Device={circuit.DeviceID}, Circuit={circuit.CircuitID}");
                            oldClient.Close();
                            oldClient.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, "Error while closing old TCP client");
                        }
                    }

                    // Assign new client
                    IsHandler.Data._tcpClient = tcpClient;

                    _log.Debug($"Assigned new TCP Client for Device={circuit.DeviceID}, Circuit={circuit.CircuitID}");

                    return CommonResponse<ICircuitCommandHandler>.Ok(
                        IsHandler.Data,
                        $"Circuit Handler already exists for Device={circuit.DeviceID}, Circuit={circuit.CircuitID}."
                    );
                }

                var handler = CreateNewHandler();
                handler._cts = _lifecycleCts;
                handler.Circuit = circuit;
                await handler.InitializeAsync();
                handler._tcpClient = tcpClient;

                _devices.TryAdd(key, handler);

                return CommonResponse<ICircuitCommandHandler>.Ok(
                    handler,
                    $"Circuit Handler added for Device={handler.Circuit.DeviceID}, Circuit={handler.Circuit.CircuitID}."
                );
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Add error: {ex.Message}");
                return CommonResponse<ICircuitCommandHandler>.Fail($"Error adding Circuit handler: {ex.Message}");
            }
            finally
            {
                HardwareManagerChanged?.Invoke();
            }
        }
        public CommonResponse<bool> Remove(CircuitDto circuit, bool force = false)
        {
            if (circuit == null)
                return CommonResponse<bool>.Fail("Circuit is null.");

            var key = MakeKey(circuit.DeviceID, circuit.CircuitID);

            try
            {
                if (!_devices.TryGetValue(key, out var handler))
                    return CommonResponse<bool>.Fail("Circuit Handler not found.");

                // Disconnect safely
                try
                {
                    handler.UnregisterAsync();

                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Disconnect error: {ex.Message}");
                }

                return CommonResponse<bool>.Ok(true, "Circuit Handler removed.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Remove error: {ex.Message}");
                return CommonResponse<bool>.Fail($"Error removing Circuit handler: {ex.Message}");
            }
            finally
            {
                HardwareManagerChanged?.Invoke();
            }
        }

        #endregion

        #region UDP View 
        private async Task RunUdpViewListenerAsync(CancellationToken token)
        {
            try
            {
                _udpViewDiscovery = new UdpClient(dataViewPort);
                Console.WriteLine($"UDP View Data Listener started on port {dataViewPort}");
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpViewDiscovery.ReceiveAsync();
                    // Trigger event for received data
                    OnUdpViewDataReceived?.Invoke(result);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"UDP Listener error: {ex.Message}");
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
                int circuitId = payload[2];
                byte type = payload[3];

                var handler = Get(new CircuitDto { DeviceID = deviceId, CircuitID = circuitId });
               
                if (!handler.Success || handler.Data == null)
                {
                    _log.Error("No handler found for DeviceID={DeviceID}, CircuitID={CircuitID}", deviceId, circuitId);
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
        private async Task RunUdpStoreListenerAsync(CancellationToken token)
        {
            try
            {
                _udpStoreDiscovery = new UdpClient(dataStorePort);
                _log.Information($"UDP Data Listener started on port {dataStorePort}");
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpStoreDiscovery.ReceiveAsync();
                    // Trigger event for received data
                    _ = _udpChannel.Writer.WriteAsync(result); // NON-BLOCKING
                    // OnUdpDataReceived?.Invoke(result);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"UDP Listener error: {ex.Message}");
            }
        }

        private async Task StartUdpProcessorAsync(CancellationToken token)
        {
            _log.Information($"UDP data store processing....");

            await foreach (var packet in _udpChannel.Reader.ReadAllAsync(token))
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

                var handlerResult = Get(new CircuitDto { DeviceID = first.DeviceId, CircuitID = first.CircuitId });
                if (!handlerResult.Success || handlerResult.Data == null)
                {
                    _log.Debug("No handler found for DeviceID={DeviceID}, CircuitID={CircuitID}", first.DeviceId, first.CircuitId);
                    return;
                }

                // Session ID is a packed value: [DeviceID 8b][CircuitID 8b][epoch-low16 16b]
                // Use SessionIdToDateTime to correctly reconstruct the date from the low-16 epoch bits.
                bool validEpoch = DecoderService.SessionIdToDateTime(first.SessionID, out DateTime sessionDateTime, true);
                if (!validEpoch)
                {
                    _log.Debug("StoreUdpData: could not reconstruct date from SessionID - falling back to DateTime.Now.");
                    sessionDateTime = DateTime.Now;
                }

                decoded.Data.filePath = Path.Combine(sessionDateTime.ToString("dd-MM-yyyy"), $"{first.SessionID}_{first.DeviceId}_{first.CircuitId}.db");
               
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
            int circuitId = payload[1];

            var handler = Get(new CircuitDto { DeviceID = deviceId, CircuitID = circuitId });
            if (!handler.Success || handler.Data == null) return;

            var notification = new NotificationItem
            {
                Id = $"{deviceId}-{circuitId}-UDP-Store-Failed",
                Read = false,
                Message = $"{deviceId}-{circuitId} : {message}",
                Timestamp = DateTime.Now
            };

            await handler.Data.SendNotification(notification);
        }

        #endregion

        #region TPC Listener
        private async Task RunCommandListenerAsync(CancellationToken token)
        {
            _commandListener = new TcpListener(IPAddress.Any, commandPort);
            _commandListener.Server.SetSocketOption(
                                SocketOptionLevel.Socket,
                                SocketOptionName.KeepAlive,
                                true);

            _commandListener.Start();
            _log.Information($"Command Listener started on port {commandPort}");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _commandListener.AcceptTcpClientAsync(token);
                    var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
                    _log.Information($"Command client connected: {clientId}");
                    _ = HandleCommandClientAsync(client, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _log.Error(ex, $"[Command Error] {ex.Message}"); }
                finally
                {
                    HardwareManagerChanged?.Invoke();
                }
            }
        }

        private async Task HandleCommandClientAsync(TcpClient client, CancellationToken token)
        {
            var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
            _log.Information($"client Enter in HandleCommandClientAsync: {clientId}");

            ICircuitCommandHandler? circuitHandler = null;

            byte[] responseBytes = DecoderService.ParseRegistrationResponse(0, 0, CommandStatus.Failed);

            try
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[33];

                int read = await stream.ReadAsync(buffer, token);

                // Early exit
                if (read == 0)
                {
                    _log.Warning("No data received. Closing connection.");
                    return;
                }

                // Check registration header
                if (buffer[0] == 0xDD && buffer[1] == 0x01)
                {
                    var newCircuit = DecoderService.ParseRegistrationPacket(buffer);

                    if (newCircuit == null)
                    {
                        _log.Warning("Invalid registration packet.");
                        return;
                    }

                    using var scoped = ServiceLocator.GetScoped<IDeviceCircuitServices>();
                    var db = scoped.Service;

                    try
                    {
                        // Check if circuit exists
                        var existingCircuit = await db.GetCircuitAsync(newCircuit);

                        if (existingCircuit.Success && existingCircuit.Data != null && existingCircuit.Data.IsRegistered)
                        {
                            // Try adding circuit to handler
                            var addResult = await Add(existingCircuit.Data, client);

                            if (addResult.Success)
                            {
                                // Already registered
                                if (addResult.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                                {
                                    responseBytes = DecoderService.ParseRegistrationResponse(
                                        (int)newCircuit.DeviceID,
                                        (int)newCircuit.CircuitID,
                                        CommandStatus.AlreadyRegistered
                                    );

                                    // Update into Database 
                                    await db.UpdateRegistration(newCircuit);
                                }
                                else
                                {
                                    // New registration success
                                    responseBytes = DecoderService.ParseRegistrationResponse(
                                        (int)newCircuit.DeviceID,
                                        (int)newCircuit.CircuitID,
                                        CommandStatus.Success
                                    );
                                }


                                circuitHandler = addResult.Data;
                            }
                            else
                            {
                                responseBytes = DecoderService.ParseRegistrationResponse(
                                       (int)newCircuit.DeviceID,
                                       (int)newCircuit.CircuitID,
                                       CommandStatus.Failed
                                   );
                            }

                        }

                        else if (existingCircuit.Success && existingCircuit.Data != null && !existingCircuit.Data.IsRegistered)
                        {

                            // check if deleted then enable it. 
                            if (existingCircuit.Data.IsDeleted)
                            {
                                await db.UpdateIsDeleteAsync(newCircuit, false);
                            }

                            responseBytes = DecoderService.ParseRegistrationResponse(
                                (int)newCircuit.DeviceID,
                                (int)newCircuit.CircuitID,
                                CommandStatus.Failed
                            );

                        }

                        else
                        {
                            // Insert new record into DB
                            if (existingCircuit.Data == null)
                                await db.InsertAsync(newCircuit);

                            responseBytes = DecoderService.ParseRegistrationResponse(
                                (int)newCircuit.DeviceID,
                                (int)newCircuit.CircuitID,
                                CommandStatus.Failed
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "DB Error: " + ex.Message);
                    }
                }
                else
                {
                    _log.Warning("Unknown command received.");
                    return;
                }

                
                // ?? Finally Send Response to Hardware
                bool sentToHardware = await SendOnly(client, responseBytes, token);

                // No circuit handler = no further communication
                if (!sentToHardware || circuitHandler == null)
                {
                    _log.Warning(" No circuit handler = no further communication");
                }
                
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[HandleCommandClientAsync Error] " + ex.Message);
            }
            finally
            {

                _log.Information($"[HandleCommandClientAsync exited{ clientId}]");
                HardwareManagerChanged?.Invoke();
            }
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
        }

        #endregion
        
    }
}
