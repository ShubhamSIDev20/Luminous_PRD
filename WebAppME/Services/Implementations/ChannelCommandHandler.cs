using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Config;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Channels;

namespace BatteryTestingSystem.Services.Implementations
{
    public class ChannelCommandHandler : IChannelCommandHandler, IDisposable
    {
        #region Custructor
        public ChannelDto Channel { get; set; } = new();
        public ProgramDTO Program { get; set; } = new();
        public List<StepModel> ExpandedProgramSteps { get; private set; } = new();

        // Q9 "Live Step Update" is session-only by design (never written back to Program /
        // ExpandedProgramSteps) - this is the one place the last accepted amendment survives, so
        // reopening the edit dialog for the same still-executing step shows what was actually
        // last applied to the hardware, not the original resident values. Cleared once the
        // circuit is no longer running so a later session never sees a stale amendment.
        public StepModel? LastLiveStepUpdate { get; private set; }

        public BatteryDTO Battery { get; set; } = new();
        private DeviceConnection _connection = null!;

        // Setting Connection always clears the manual-disconnect flag - this is the ONLY
        // place a channel-slot gets reattached to a (possibly new) socket, whether on
        // first registration or on reconnect after a drop (see ChannelManager.Add,
        // both branches), so it's the correct single point to reset "am I marked offline".
        public DeviceConnection Connection
        {
            get => _connection;
            set
            {
                _connection = value;
                _manuallyDisconnected = false;
            }
        }

        // Kept for the one remaining external read site (MainLayout.razor's active-device
        // count badge) - proxies to the shared DeviceConnection's socket. Multiple
        // channel-slots on the same device correctly return the SAME TcpClient here.
        public TcpClient _tcpClient => Connection?.TcpClient;

        private volatile bool _manuallyDisconnected = false;

        public bool IsConnected
        {
            get
            {
                if (_manuallyDisconnected)
                    return false;

                try
                {
                    var client = Connection?.TcpClient;
                    return client != null && client.Connected;
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                finally
                {
                    TryInvokeCircuitChanged();
                }
            }
        }

        public void MarkDisconnected()
        {
            _manuallyDisconnected = true;
            TryInvokeCircuitChanged();
        }

        public recordRequest RealTime { get; set; } = new();
        public SessionRecordDto Session { get; set; } = new();
        public bool commandinterrupt { get; set; } = new();
        public CancellationTokenSource _cts { get; set; } = new CancellationTokenSource();
        public System.Threading.Channels.Channel<recordStoreRequest> _StoreQueue { get; set; } = System.Threading.Channels.Channel.CreateUnbounded<recordStoreRequest>();
        public CalibrationDto calibration { get;  set; } = new();
        public DbcRecord dbcData { get; set; } = new ();
        
        private DateTime _lastStoredDbcDate;

        /// <summary>Set by the factory in ServiceCollectionExtensions; null in preview instances.</summary>
        public IAlarmService? Alarms { get; set; }

        public event Action? OnChannelChanged;

        private readonly Serilog.ILogger _log = Log.ForContext<ChannelCommandHandler>();

        public async Task InitializeAsync()
        {
            using var scope = ServiceLocator.CreateScope();

            var programService = scope.ServiceProvider.GetRequiredService<IProgramServices>();

            try
            {
                var GetSession  = await programService.GetLastSessionAsync(Channel);
               
                if (GetSession.Success && GetSession.Data != null)
                {
                    Session = GetSession.Data;
                    Program = Session.programs;
                    Battery = Session.battery;

                    var dbcService = scope.ServiceProvider.GetRequiredService<IDbcService>();

                    var p1Result = await dbcService.GetByIdAsync(Session.DbcFileRecordID ?? 0);
                    var p2Result = await dbcService.GetByIdAsync(Session.Port2DbcFileRecordID ?? 0);
                    var p3Result = await dbcService.GetByIdAsync(Session.Port3DbcFileRecordID ?? 0);

                    var p1Db = p1Result.Success ? p1Result.Data?.dbcDatabase : null;
                    var p2Db = p2Result.Success ? p2Result.Data?.dbcDatabase : null;
                    var p3Db = p3Result.Success ? p3Result.Data?.dbcDatabase : null;

                    if (p1Db != null || p2Db != null || p3Db != null)
                        Session.dbcDatabse = MergeDbcDatabases(p1Db, p2Db, p3Db);

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ChannelCommandHandler InitializeAsync");
            }
            finally
            {
                _ = StartStoreWorkerAsync();
            }
        }

        #endregion

        #region No Interface Members
        private DateTime _lastCircuitChanged = DateTime.MinValue;
        private readonly object _circuitLock = new object();
        private void TryInvokeCircuitChanged()
        {
            try
            {
                lock (_circuitLock)
                {
                    var now = DateTime.UtcNow;

                    if ((now - _lastCircuitChanged).TotalSeconds < 2)
                        return;

                    _lastCircuitChanged = now;
                }

                // Invoke outside lock to avoid blocking subscribers
                OnChannelChanged?.Invoke();
            }
            catch (Exception ex)
            {
                // Handle or log safely so it never breaks IsConnected flow
                // Example:
                Debug.WriteLine($"OnChannelChanged error: {ex}");
            }
        }


        // ConnectionAlive() (the old per-handler continuous-read/disconnect-detection loop)
        // has been removed. With a shared TcpClient per device, that job now belongs
        // entirely to ChannelManager's per-device read-loop (HandleCommandClientAsync) -
        // having every one of up to 64 channel-slots on a device separately poll the
        // SAME socket would race for bytes and multiply CPU usage for no benefit.

        #endregion

        #region DataStore
        public void EnqueueForStore(recordStoreRequest dto, Dictionary<string, object>? dbcRecord)
        {
            if (dbcRecord != null && dto.RealStoreRecord.Count > 0 && dbcData.CreatedDate > _lastStoredDbcDate)
            {
                // DBC packet arrived since last store — write to first record only, null the rest
                dto.RealStoreRecord[0].dbcValues = JsonConvert.SerializeObject(dbcRecord);
                for (int i = 1; i < dto.RealStoreRecord.Count; i++)
                    dto.RealStoreRecord[i].dbcValues = null;

                _lastStoredDbcDate = dbcData.CreatedDate;
            }
            else
            {
                // No change (or null) — null all records
                foreach (var r in dto.RealStoreRecord)
                    r.dbcValues = null;
            }

            // REG-with-label bifurcation (Measurements vs RegLogs) happens inside
            // SqliteBulkDatabaseManager.InsertRecordAsync — same place/process that already
            // resolves DBC signal names from this session's own stored config, rather than
            // depending on this handler's transient in-memory ExpandedProgramSteps.
            _StoreQueue.Writer.TryWrite(dto);
            Session.Unstorerecordcount += dto.RealStoreRecord.Count;
        }

        public async Task StartStoreWorkerAsync()
        {
            _log.Debug($"{Channel?.DeviceID}-{Channel?.SecondaryBoardNumber}-{Channel?.ChannelNumber} SQLite Store Worker Started...");

            using var StoreService = ServiceLocator.GetScoped<ISqliteBulkDatabaseManager>();
          
            await foreach (var item in _StoreQueue.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {

                    if (StoreService == null || StoreService.Service == null)
                        continue;

                    #region notification on ui for system errors
                    var noti = string.Empty;

                    var record = item?.RealStoreRecord?
                    .FirstOrDefault(r => r.SystemErrorID > 0);

                    if (record != null)
                    {
                        if (record != null &&
                            record.SystemErrorID > 0 && record.SystemErrorID <= 17 &&
                            record.ProgramRunningTime != 0)
                        {
                            var error = (SystemError)record.SystemErrorID;

                            noti = $"{Channel?.DeviceID}-{Channel?.SecondaryBoardNumber}-{Channel?.ChannelNumber} System Error: {error.ToString()}";
                        }
                        //else if (record.ErrorId > 0)
                        //{
                        //    var error = ErrorMessages.Errors.FirstOrDefault(e => e.Index == record.ErrorId);
                        //    noti = $"{Channel?.DeviceID}-{Channel?.ChannelNumber} User Error: {error?.Message}";
                        //}
                        //else if (record.MessageId > 0)
                        //{
                        //    var message = ErrorMessages.Messages.FirstOrDefault(e => e.Index == record.MessageId);
                        //    noti = $"{Channel?.DeviceID}-{Channel?.ChannelNumber} Message: {message?.Message}";
                        //}
                    }

                    if (!string.IsNullOrEmpty(noti) && Alarms != null)
                    {
                        await Alarms.RaiseAsync(new AlarmRequest
                        {
                            AlarmKey = $"{Channel?.DeviceID}/{Channel?.SecondaryBoardNumber}/{Channel?.ChannelNumber}/channel-error",
                            Severity = SeverityLevel.ERROR,
                            Source = AlarmSource.ChannelError,
                            DeviceId = Channel?.DeviceID.ToString(),
                            BoardNumber = (int?)Channel?.SecondaryBoardNumber,
                            ChannelNumber = (int?)Channel?.ChannelNumber,
                            Title = $"Channel error - device {Channel?.DeviceID} ch {Channel?.ChannelNumber}",
                            Message = noti
                        });
                    }

                    #endregion

                    await StoreService.Service.InsertRecordAsync(item);

                    var IsEnd = item.RealStoreRecord.FirstOrDefault(e => (byte)e.Operator == OperatorConstants.STO);

                    if (IsEnd != null)
                    {
                        using var PService = ServiceLocator.GetScoped<IProgramServices>();

                        if (PService != null || PService.Service != null)
                        {
                            Session.EndTime = DateTime.Now;
                            await PService.Service.EndSession(Session);
                            _log.Warning($"{Channel?.DeviceID}-{Channel?.SecondaryBoardNumber}-{Channel?.ChannelNumber} END Session");

                        }
                    }

                    Session.Unstorerecordcount = Math.Max(0, Session.Unstorerecordcount - item.RealStoreRecord.Count);
                    Session.Storerecordcount += item.RealStoreRecord.Count;

                    //_log.Debug($"StartStoreWorkerAsync - {Channel?.DeviceID}-{Channel?.ChannelNumber} RecordCount:{item.RealStoreRecord.Count}");

                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"SQLite store error: {ex.Message}");
                }


            }

            _log.Debug($"{Channel?.DeviceID}-{Channel?.ChannelNumber} SQLite Store Worker Stoped...");

        }

        #endregion

        #region Control Commands

        public async Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command)
        {
            if (Connection?.TcpClient == null || !Connection.TcpClient.Connected)
                return CommonResponse<T>.Fail("TCP Command client is not connected.");

            if (commandinterrupt)
                return CommonResponse<T>.Fail("Another command is in progress.");

            commandinterrupt = true;

            try
            {
                byte addressByte = Utils.ChannelAddressCodec.Encode(Channel.SecondaryBoardNumber, Channel.ChannelNumber);
                // DecoderService.BuildCommand always places QueryId at header index 3,
                // regardless of whether a Range byte or Data payload follows.
                byte queryId = command.Length > 3 ? command[3] : (byte)0;

                var response = await Connection.SendAndWaitAsync(addressByte, queryId, command, TimeSpan.FromSeconds(15));

                return DecoderService.TryDecode<T>(response);
            }
            catch (TimeoutException)
            {
                return CommonResponse<T>.Fail("No response received from device.");
            }
            catch (OperationCanceledException)
            {
                return CommonResponse<T>.Fail("Read timed out after 15 seconds.");
            }
            catch (Exception ex)
            {
                return CommonResponse<T>.Fail($"Exception: {ex.Message}");
            }
            finally
            {
                commandinterrupt = false;
                OnChannelChanged?.Invoke();
            }
        }

        public async Task<CommonResponse<bool>> StartProgram()
        {
            if (Program == null || Program.ProgramSteps == null || Program.ProgramSteps == 0)
            {
                return CommonResponse<bool>.Fail("No program loaded to start.");
            }

            if (Program.ProgramHash != Session.ProgramHash)
            {
                return CommonResponse<bool>.Fail(
                    "The loaded program does not match the current program. The program has been modified."
                );
            }

            if (RealTime?.RealTimeRecord?.ProgramStatus == Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("Program is already running.");
            }

            if (RealTime?.RealTimeRecord?.CircuitStatus != CircuitStatus.Idle)
            {
                return CommonResponse<bool>.Fail($"The circuit is in an {RealTime?.RealTimeRecord?.CircuitStatus.ToString()} state and cannot be started.");
            }

            // A fresh run must never inherit a Q9 amendment from a previous session.
            LastLiveStepUpdate = null;

            // Use channel-aware packed session ID to avoid UNIQUE constraint collisions
            // when multiple channels are started in parallel from the dashboard.
            // Layout: [DeviceID 8-bit][Board/Channel address 8-bit][epoch low 16-bit] → always 4 bytes.
            byte[] sessionID = DecoderService.GetSessionIdBytes(
                Channel.DeviceID, Utils.ChannelAddressCodec.Encode(Channel.SecondaryBoardNumber, Channel.ChannelNumber),
                out var epochSeconds, out var sessionDateTime);

            ushort id = CommandTracker.AddCommand($"{ProgramControlQuery.Start.ToString()} command executed by {CurrentUser.UserName}!");

            byte[] payload = DecoderService.BuildCommand(
         
                new CommandRequest
            {
                Start = Models.Enums.StartByte.Control,
                DeviceId = Channel.DeviceID,
                SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                ChannelId = Channel.ChannelNumber,
                QueryId = (byte)Models.Enums.ProgramControlQuery.Start,
                Data = sessionID
            });

            #region AssingSession


            Session.SessionID = epochSeconds;
            Session.ChannelNumber = Channel.ChannelNumber;
            Session.DeviceID = Channel.DeviceID;
            Session.SecondaryBoardNumber = Channel.SecondaryBoardNumber;
            // Session.programs = Program;
            Session.StartTime = DateTime.Now;
            Session.EndTime = null; 
            Session.SessionName = $"{Program.ProgramName}_{sessionDateTime?.ToLocalTime():yyyyMMddHHmmssfff}";
            // Board number must be part of the file name — ChannelManager.StoreUdpData already
            // names the live-data file "{SessionID}_{DeviceId}_{SecondaryBoardNumber}_{ChannelId}.db";
            // without it here, two channels with the same ChannelNumber on different boards of the
            // same device collide onto the same session file / same SecondaryBoardNumber=0 row.
            Session.SessionFilePath = Path.Combine(sessionDateTime?.ToLocalTime().ToString("dd-MM-yyyy"), $"{Session.SessionID}_{Channel.DeviceID}_{Channel.SecondaryBoardNumber}_{Channel.ChannelNumber}.db");
            // Session.ProgramHash = Program.ProgramHash ?? string.Empty;
            Session.Unstorerecordcount = 0;
            Session.Storerecordcount = 0;
            #endregion

            #region Build SessionDB

            using var StoreService = ServiceLocator.GetScoped<ISqliteBulkDatabaseManager>();
            if (StoreService == null || StoreService.Service == null)
                return CommonResponse<bool>.Fail("unable to get SqliteDatabase, Contact to System Admin!.");
            
            // Collect PRODUCER sub-programs and TABLE file data for self-contained session dump
            var producerPrograms = await ResolveProducerProgramsAsync(Program?.ProgramStepModel ?? new());
            var tableFileData = CollectTableFileData(Program?.ProgramStepModel ?? new());

            await StoreService.Service.InsertSessionAsync(new SessionRequest
            {
                filePath = Session.SessionFilePath,
                Program = Program,
                Battery = Battery,
                dbcDatabase = Session.dbcDatabse,
                ProducerPrograms = producerPrograms.Count > 0 ? producerPrograms : null,
                TableFileData = tableFileData.Count > 0 ? tableFileData : null,
                ExpandedProgramSteps = ExpandedProgramSteps.Count > 0 ? ExpandedProgramSteps : null
            });

            #endregion

            var start = await SendAndWaitForResponseAsync<bool>(payload);

            #region InsertNewSessionRecord 
          
            if (start.Success)
            {
                using var PService = ServiceLocator.GetScoped<IProgramServices>();
                if (PService == null || PService.Service == null)
                    return CommonResponse<bool>.Fail("unable to get BTSDatabase, Contact to System Admin!.");

                await PService.Service.CreateSession(Session);

            }
         
            #endregion

            _log.Debug($"Started Session : {JsonConvert.SerializeObject(Session)} ");
            _log.Debug("Stoped : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));

            await ErrorMessages.GetErrors(true);
            await ErrorMessages.GetMessages(true);

            return start;
        }

        public async Task<CommonResponse<bool>> StopProgram()
        {
            ushort id = CommandTracker.AddCommand($"{ProgramControlQuery.Stop.ToString()} command executed by {CurrentUser.UserName}!");

            byte[] payload = DecoderService.BuildCommand(
              new CommandRequest
              {
                  Start = Models.Enums.StartByte.Control,
                  DeviceId = Channel.DeviceID,
                  SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                  ChannelId = Channel.ChannelNumber,
                  QueryId = (byte)Models.Enums.ProgramControlQuery.Stop,
              });


            #region Update Session EndTime

            //using var StoreService = ServiceLocator.GetScoped<ISqliteBulkDatabaseManager>();

            //if (StoreService != null || StoreService.Service != null)
            //    await StoreService.Service.InsertLogs(Session.SessionFilePath, new ProgramAuditExecution
            //    {
            //        UserName = CurrentUser.UserName,
            //        Severity = Models.Enums.SeverityLevel.WARNING,
            //        LogMessage =
            //            $"Program Stop requested. " +
            //            $"Program='{Program.ProgramName}', " +
            //            $"DeviceID={Channel.DeviceID}, " +
            //            $"ChannelNumber={Channel.ChannelNumber}, ",
            //        LogTime = DateTime.Now,
            //        CircuitStatus = RealTime.RealTimeRecord.CircuitStatus,
            //    });


            var stop = await SendAndWaitForResponseAsync<bool>(payload);
          
            if (stop.Success)
            {
                using var PService = ServiceLocator.GetScoped<IProgramServices>();

                if (PService != null || PService.Service != null)
                {
                    Session.EndTime = DateTime.Now;
                    await PService.Service.EndSession(Session);
                }
                  
            }
           
            #endregion

            _log.Debug($"Stoped Session : {JsonConvert.SerializeObject(Session)} ");
            _log.Debug("Stoped : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));

            return stop;
        }

        public async Task<CommonResponse<bool>> PauseProgram()
        {
            ushort id = CommandTracker.AddCommand($"{ProgramControlQuery.Pause.ToString()} command executed by {CurrentUser.UserName}!");

            if (RealTime?.RealTimeRecord?.ProgramStatus != Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("Program is not running, cannot Interrupt.");
            }

            byte[] payload = DecoderService.BuildCommand(
            new CommandRequest
            {
                Start = Models.Enums.StartByte.Control,
                DeviceId = Channel.DeviceID,
                SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                ChannelId = Channel.ChannelNumber,
                QueryId = (byte)Models.Enums.ProgramControlQuery.Pause,
            });


            _log.Debug("PauseProgram : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> ContinueProgram()
        {
            ushort id = CommandTracker.AddCommand($"{ProgramControlQuery.Continue.ToString()} command executed by {CurrentUser.UserName}!");

            if (RealTime?.RealTimeRecord?.ProgramStatus != Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("Program is not running, cannot Continue.");
            }

            byte[] payload = DecoderService.BuildCommand(
            new CommandRequest
            {
                Start = Models.Enums.StartByte.Control,
                DeviceId = Channel.DeviceID,
                SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                ChannelId = Channel.ChannelNumber,
                QueryId = (byte)Models.Enums.ProgramControlQuery.Continue,
            });

            _log.Debug("ContinueProgram : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> TimeSyn()
        {
            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Control,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ConfigurationQuery.SyncTime,
                    Data = DecoderService.GetEpochTimeBytes(out var epochSeconds, out _)
                });

            _log.Debug("ContinueProgram : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));
            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        // only if has system error 
        public async Task<CommonResponse<bool>> ResetSystem()
        {
            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Control,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ProgramControlQuery.SystemReset,
                });
            _log.Debug("ResetSystem : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));
            return await SendAndWaitForResponseAsync<bool>(payload);

        }

        // Q10 (bm_program_v3.2): abandon the currently executing step and resume from
        // stepNumber, without modifying the resident program. Only valid while a program is
        // actually running - the hardware rejects it otherwise (see JumpToStepReasonMessage).
        public async Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber)
        {
            if (RealTime?.RealTimeRecord?.ProgramStatus != Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("No program is running on this circuit.");
            }

            if (!Utils.JumpStepValidator.IsValid(stepNumber, Program?.ProgramSteps))
            {
                return CommonResponse<bool>.Fail(Utils.JumpStepValidator.InvalidMessage);
            }

            byte[] stepBytes = BitConverter.GetBytes((ushort)stepNumber);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(stepBytes);

            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Program,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ProgramDataQuery.JumpToStep,
                    Data = stepBytes,
                });

            _log.Debug("JumpToStepAsync({StepNumber}) : {Command}", stepNumber, string.Join(" ", payload.Select(b => b.ToString("X2"))));

            var response = await SendAndWaitForResponseAsync<bool>(payload);

            return response.Success
                ? CommonResponse<bool>.Ok(true, $"Jumped to step {stepNumber}.")
                : response;
        }

        public async Task<CommonResponse<bool>> HWReadyToReadWriteAsync()
        {
            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest {
                    Start = Models.Enums.StartByte.Program,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ProgramDataQuery.HWReadyForProgram,
                });

            _log.Debug(
                "HWReadyToReadWriteAsync : {Command}",
                string.Join(" ", payload.Select(b => b.ToString("X2")))
            );
            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams)
        {
            if (RealTime?.RealTimeRecord?.ProgramStatus == Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("Program is running, cannot SetBattery.");
            }

            byte[] payload = DecoderService.BuildCommand(
               new CommandRequest
               {
                   Start = Models.Enums.StartByte.Configuration,
                   DeviceId = Channel.DeviceID,
                   SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                   ChannelId = Channel.ChannelNumber,
                   QueryId = (byte)Models.Enums.ConfigurationQuery.WriteBatteryParams,
                   Data = DecoderService.BuildBatteryBytes(BatteryParams)
               }
            );

            Session.BatteryID = BatteryParams.Id;
            Session.BatteryName = BatteryParams.Name;
            Session.battery = BatteryParams;
            Battery = BatteryParams;

            _log.Debug(
              "SetBatteryParamAsync : {Command}",
              string.Join(" ", payload.Select(b => b.ToString("X2")))
            );

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramDto, BatteryDTO? Battery = null)
        {
            if (RealTime?.RealTimeRecord?.ProgramStatus == Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("Program is running, cannot SetProgram.");
            }

            var resolvedProducers = await ResolveProducerProgramsAsync(ProgramDto.ProgramStepModel ?? new());
            var resolvedDict = resolvedProducers
                .Where(p => p.ProgramName != null && p.ProgramStepModel != null)
                .ToDictionary(p => p.ProgramName!, p => p.ProgramStepModel!);

            // Store the expanded step list so UI can map hardware step numbers correctly
            ExpandedProgramSteps = resolvedDict.Count > 0
                ? ProgramBuilder.ExpandProducerSteps(ProgramDto.ProgramStepModel ?? new(), resolvedDict)
                : new();

            List<byte[]> steps;
            try
            {
                steps = DecoderService.ConvertProgramIntoBytesPackets(ProgramDto.ProgramStepModel, resolvedDict, Battery);
            }
            catch (BatteryUnitResolutionException ex)
            {
                // A step uses a battery-relative unit (ACNx, VN) but no usable battery was
                // supplied — fail cleanly instead of sending an unscaled/garbage value.
                return CommonResponse<bool>.Fail(ex.Message);
            }

            if (steps == null || steps.Count == 0)
            {
                return CommonResponse<bool>.Fail("No data to send. The steps list is empty.");
            }

            List<byte[]> payloads = new();

            const int MaxPacketSize = 1400;

            List<byte> packet = new(MaxPacketSize);

            foreach (var step in steps)
            {
                if (step == null || step.Length == 0)
                    continue;

                int offset = 0;
                int stepLength = step.Length;

                while (offset < stepLength)
                {
                    int available = MaxPacketSize - packet.Count;

                    if (available == 0)
                    {
                        payloads.Add(packet.ToArray());
                        packet.Clear();
                        continue;
                    }

                    int toCopy = Math.Min(available, stepLength - offset);

                    packet.AddRange(step.AsSpan(offset, toCopy));

                    offset += toCopy;
                }
            }

            if (packet.Count > 0)
            {
                payloads.Add(packet.ToArray());
            }

            byte[] payloadsCount = BitConverter.GetBytes((short)payloads.Count); // Use short to keep 2 bytes

            if (BitConverter.IsLittleEndian)
                Array.Reverse(payloadsCount);


            byte[] QueryPayload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Program,
                    DeviceId = (int)Channel.DeviceID,
                    SecondaryBoardNumber = (int)Channel.SecondaryBoardNumber,
                    ChannelId = (int)Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ProgramDataQuery.SendProgramStepsCount,
                    Data = payloadsCount
                }
            );

            var sendProgram = await SendAndWaitForResponseAsync<bool>(QueryPayload);

            if (!sendProgram.Success)
                return sendProgram;

            for (int i = 0; i < payloads.Count; i++)
            {
                ushort length = (ushort)payloads[i].Length;

                byte[] lengthBytes = BitConverter.GetBytes(length);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                }

                byte[] fullData = lengthBytes.Concat(payloads[i]).ToArray();

                var FinalPacket = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Program,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ProgramDataQuery.SendProgram,
                    Data = fullData
                });

                payloads[i] = FinalPacket;

            }

            foreach (var payload in payloads)
            {

                var sendToDevice = await SendAndWaitForResponseAsync<bool>(payload);

                if (!sendToDevice.Success)
                {
                    return sendToDevice;
                }
            }

            Session.ProgramHash = ProgramDto.ProgramHash;
            Session.ProgramID = ProgramDto.ProgramId;
            Session.ProgramName = ProgramDto.ProgramName;
            Session.programs = ProgramDto;
            Program = ProgramDto;

            _log.Debug(
                $"SetProgramAsync Command Payload Count {payloads.Count()} | " +
                string.Join(" | ", payloads.Select(b =>
                    string.Join(" ", b.Select(p => p.ToString("X2")))
                ))
            );

            return CommonResponse<bool>.Ok(true);
        }

        // Q9 "Live Step Update": amend the currently-executing step's PARAMETERS ONLY (operator
        // locked) without touching programDataBuffer/EEPROM - this is session-only and never goes
        // through IProgramServices/ProgramDTO persistence, unlike SetProgramAsync above. Unlike
        // Start/Stop/Pause/Continue, Q9 is gated on the program ALREADY running - "No Q1/Q2/Q3
        // handshake... gated on the opposite condition" (bm_program_v3.2.md).
        public async Task<CommonResponse<bool>> LiveStepUpdateAsync(StepModel editedStep)
        {
            if (RealTime?.RealTimeRecord?.ProgramStatus != Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<bool>.Fail("Program is idle, or awaiting operator action (interrupt / error wait / message wait).");
            }

            int currentStepNumber = RealTime.RealTimeRecord.StepNumber;
            var fullProgramSteps = LiveStepUpdateValidator.ResolveFullProgramSteps(ExpandedProgramSteps, Program?.ProgramStepModel);
            var residentStep = fullProgramSteps.FirstOrDefault(s => s.StepNumber == currentStepNumber);

            var validationError = LiveStepUpdateValidator.Validate(residentStep, editedStep, currentStepNumber);
            if (validationError != null)
            {
                return CommonResponse<bool>.Fail(validationError);
            }

            byte[] stepPacket = DecoderService.BuildLiveStepUpdatePacket(editedStep, fullProgramSteps, Battery);

            byte[] lengthBytes = BitConverter.GetBytes((ushort)stepPacket.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            byte[] fullData = lengthBytes.Concat(stepPacket).ToArray();

            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Program,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ProgramDataQuery.LiveStepUpdate,
                    Data = fullData
                });

            _log.Debug("LiveStepUpdateAsync : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));

            var response = await SendAndWaitForResponseAsync<bool>(payload);

            // Session-only cache of the last accepted amendment for THIS step number - Q9 never
            // persists to Program/ExpandedProgramSteps by design, so without this, reopening the
            // dialog for the same still-executing step would keep showing the original resident
            // values instead of what was actually just applied to the hardware.
            if (response.Success)
            {
                LastLiveStepUpdate = editedStep;
            }

            return response;
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingDetails()
        {
            if (RealTime?.RealTimeRecord?.ProgramStatus == Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<ManufacturingDetailDTO>.Fail("Program is running, Manufacturing SetProgram.");
            }

            byte[] payload = DecoderService.BuildCommand(
               new CommandRequest
               {
                   Start = Models.Enums.StartByte.Configuration,
                   DeviceId = Channel.DeviceID,
                   SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                   ChannelId = Channel.ChannelNumber,
                   QueryId = (byte)Models.Enums.ConfigurationQuery.ReadManufacturingConfig,
               }
            );

            var Md = await SendAndWaitForResponseAsync<ManufacturingDetailDTO>(payload);

            using var Dservice = ServiceLocator.GetScoped<IDeviceChannelServices>();

            if (Dservice != null && Dservice.Service != null && Md.Success)
            {
                var saveResult = await Dservice.Service.UpdateManufacturingAsync(Channel, Md.Data);
                if (!saveResult.Success)
                    _log.Warning(
                        "UpdateManufacturingAsync failed for Device={DeviceId} SecondaryBoard={SecondaryBoardNumber} Channel={ChannelId}: {Message}",
                        Channel.DeviceID, Channel?.SecondaryBoardNumber, Channel?.ChannelNumber, saveResult.Message);
            }

            OnChannelChanged?.Invoke();
            _log.Debug(
                          "GetManufacturingDetails : {Command}",
                          string.Join(" ", payload.Select(b => b.ToString("X2")))
                      );
            return Md;
        }

        public async Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryConfigDetails()
        {
            if (RealTime?.RealTimeRecord?.ProgramStatus == Models.Enums.ProgramRunningStatus.Running)
            {
                return CommonResponse<FactoryConfigDetailDTO>.Fail("Program is running, FactoryConfig SetProgram.");
            }

            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Configuration,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.ConfigurationQuery.ReadFactoryConfig,
                }
            );

            var Fc = await SendAndWaitForResponseAsync<FactoryConfigDetailDTO>(payload);

            using var Dservice = ServiceLocator.GetScoped<IDeviceChannelServices>();

            if (Dservice != null && Dservice.Service != null && Fc.Success)
            {
                var saveResult = await Dservice.Service.UpdateFactoryAsync(Channel, Fc.Data);
                if (!saveResult.Success)
                    _log.Warning(
                        "UpdateFactoryAsync failed for Device={DeviceId} SecondaryBoard={SecondaryBoardNumber} Channel={ChannelId}: {Message}",
                        Channel.DeviceID, Channel.SecondaryBoardNumber, Channel.ChannelNumber, saveResult.Message);
            }

            OnChannelChanged?.Invoke();
            _log.Debug("GetFactoryConfigDetails : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))) );
            return Fc;
        }

        public async Task<CommonResponse<bool>> UnregisterAsync()
        {
            byte[] payload = DecoderService.BuildRegistration(Channel.DeviceID, Utils.ChannelAddressCodec.Encode(Channel.SecondaryBoardNumber, Channel.ChannelNumber), (byte)Models.Enums.RegistrationQuery.Delete);

            var Unregister = await SendAndWaitForResponseAsync<bool>(payload);

            _log.Debug("UnregisterAsync : {Command}", string.Join(" ", payload.Select(b => b.ToString("X2"))));
            return Unregister;
        }
        
        #endregion

        #region Calibration 
        public async Task<CommonResponse<bool>> HWReadyToCalibrationAsync()
        {
            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Calibration,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)Models.Enums.CalibrationQueryId.IsReady,
                }
            );

            _log.Debug(
           "HWReadyToCalibrationAsync : {Command}",
                string.Join(" ", payload.Select(b => b.ToString("X2")))
            );

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> SendLiveCurrentandVoltage()
        {
            byte[] payload = DecoderService.BuildCommand(
               new CommandRequest
               {
                   Start = Models.Enums.StartByte.Calibration,
                   DeviceId = Channel.DeviceID,
                   SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                   ChannelId = Channel.ChannelNumber,
                   QueryId = (byte)Models.Enums.CalibrationQueryId.SendLive,
               }
            );

            _log.Debug(
                "SendLiveCurrentandVoltage : {Command}",
               string.Join(" ", payload.Select(b => b.ToString("X2")))
           );


            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> CalibrationPointPreset(CalibrationQueryId queryId, float value, CalibrationRange? range = null)
        {
            byte[] payload = DecoderService.BuildCommand(
               new CommandRequest
               {
                   Start = Models.Enums.StartByte.Calibration,
                   DeviceId = Channel.DeviceID,
                   SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                   ChannelId = Channel.ChannelNumber,
                   QueryId = (byte)queryId,
                   Range = range != null ? (byte)range : null,
                   Data = DecoderService.GetFloatBytes(value),
               }
            );

            _log.Debug(
                  "CalibrationPointPreset : {Command}",
                 string.Join(" ", payload.Select(b => b.ToString("X2")))
             );

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> SetGainOffset(CalibrationQueryId queryId, float gain, float offset, CalibrationRange? range = null)
        {
            var data = DecoderService.GetFloatBytes(gain)
                .Concat(DecoderService.GetFloatBytes(offset))
                .Concat(DecoderService.GetEpochTimeBytes(out int tempEpoch, out DateTime? dateTime))
                .ToArray();

            _log.Debug("SetGainOffset Payload : {Command}", string.Join(" ", data.Select(b => b.ToString("X2"))));

            // ── Update in-memory calibration snapshot ─────────────────────────
            // Current Charge/Discharge are per-range lists; Voltage/Temperature are single points.
            if (dateTime.HasValue)
            {
                var dt = dateTime.Value.ToLocalTime();

                switch (queryId)
                {
                    case CalibrationQueryId.CurrentChargeGainOffset:
                        CalibrationDto.UpsertRangePoint(calibration.CurrentCharge, new CalibrationDataPointDto
                        {
                            Range    = range ?? CalibrationRange.Full_Range,
                            Gain     = gain,
                            Offset   = offset,
                            DateTime = dt,
                            Mode     = CalibrationMode.Charge,
                            Type     = CalibrationType.Current
                        });
                        break;

                    case CalibrationQueryId.CurrentDisChargeGainOffset:
                        CalibrationDto.UpsertRangePoint(calibration.CurrentDischarge, new CalibrationDataPointDto
                        {
                            Range    = range ?? CalibrationRange.Full_Range,
                            Gain     = gain,
                            Offset   = offset,
                            DateTime = dt,
                            Mode     = CalibrationMode.Discharge,
                            Type     = CalibrationType.Current
                        });
                        break;

                    case CalibrationQueryId.VoltageChargeGainOffset:
                        calibration.VoltageCharge ??= new();
                        calibration.VoltageCharge.Gain     = gain;
                        calibration.VoltageCharge.Offset   = offset;
                        calibration.VoltageCharge.DateTime = dt;
                        break;

                    case CalibrationQueryId.VoltageDisChargeGainOffset:
                        calibration.VoltageDischarge ??= new();
                        calibration.VoltageDischarge.Gain     = gain;
                        calibration.VoltageDischarge.Offset   = offset;
                        calibration.VoltageDischarge.DateTime = dt;
                        break;

                    case CalibrationQueryId.TemperatureGainOffset:
                        calibration.TemperaturePoint ??= new();
                        calibration.TemperaturePoint.Gain     = gain;
                        calibration.TemperaturePoint.Offset   = offset;
                        calibration.TemperaturePoint.DateTime = dt;
                        break;
                }
            }

            byte[] payload = DecoderService.BuildCommand(
               new CommandRequest
               {
                   Start = Models.Enums.StartByte.Calibration,
                   DeviceId = Channel.DeviceID,
                   SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                   ChannelId = Channel.ChannelNumber,
                   QueryId = (byte)queryId,
                   Range = range != null ? (byte)range : null,
                   Data = data,
               }
            );

            _log.Debug(
                    "SetGainOffset : {Command}",
                   string.Join(" ", payload.Select(b => b.ToString("X2")))
               );

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> CancelCalibration()
        {
            byte[] payload = DecoderService.BuildCommand(
              new CommandRequest
              {
                  Start = Models.Enums.StartByte.Calibration,
                  DeviceId = Channel.DeviceID,
                  SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                  ChannelId = Channel.ChannelNumber,
                  QueryId = (byte)CalibrationQueryId.CancelCalibration,
              }
           );

            _log.Debug(
               "CancelCalibration : {Command}",
              string.Join(" ", payload.Select(b => b.ToString("X2")))
          );
            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> StopCalibration(bool force = false)
        {
            if (calibration?.IsCalibrationInPrcoess == false && calibration?.IsVerifying == false)
            {
                return CommonResponse<bool>.Fail("No calibration or verification in process to stop.");
            }

            byte[] payload = DecoderService.BuildCommand(
              new CommandRequest
              {
                  Start = Models.Enums.StartByte.Calibration,
                  DeviceId = Channel.DeviceID,
                  SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                  ChannelId = Channel.ChannelNumber,
                  QueryId = (byte)CalibrationQueryId.StopCalibration,
              }
           );

            _log.Debug(
            "StopCalibration : {Command}",
                   string.Join(" ", payload.Select(b => b.ToString("X2")))
               );

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<bool>> StopverifyCalibration()
        {
            byte[] payload = DecoderService.BuildCommand(
              new CommandRequest
              {
                  Start = Models.Enums.StartByte.Calibration,
                  DeviceId = Channel.DeviceID,
                  SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                  ChannelId = Channel.ChannelNumber,
                  QueryId = (byte)CalibrationQueryId.StopVerifyCurrentcalibration,
              }
           );

            _log.Debug(
            "StopVerifyCurrentcalibration : {Command}",
                   string.Join(" ", payload.Select(b => b.ToString("X2")))
               );

            return await SendAndWaitForResponseAsync<bool>(payload);
        }

        public async Task<CommonResponse<CalibrationData?>> PreviousCalibration()
        {
            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Calibration,
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
                    QueryId = (byte)CalibrationQueryId.PreviousCalibration,
                }
            );

            _log.Debug(
            "PreviousCalibration : {Command}",
                   string.Join(" ", payload.Select(b => b.ToString("X2")))
               );

            return await SendAndWaitForResponseAsync<CalibrationData?>(payload);
        }
        #endregion

        public async Task<CommonResponse<bool>> TransferDbcFile(
            DbcFileRecordDto? port1Dbc = null,
            DbcFileRecordDto? port2Dbc = null,
            DbcFileRecordDto? port3Dbc = null)
        {
            try
            {
                if (port1Dbc == null && port2Dbc == null && port3Dbc == null)
                    return CommonResponse<bool>.Fail("No DBC files provided for transfer.");

                // Assign signal IDs unique across ALL 3 ports, range 31–255 (0 = unselected)
                int sigId = 31;
                var skippedSignals = new List<string>();
                foreach (var dbc in new[] { port1Dbc, port2Dbc, port3Dbc })
                {
                    if (dbc?.dbcDatabase == null) continue;
                    foreach (var msg in dbc.dbcDatabase.Messages)
                        foreach (var sig in msg.Value.Signals)
                        {
                            if (sig.IsSelected)
                            {
                                if (sigId > 255)
                                {
                                    sig.SingalId = 0;
                                    skippedSignals.Add(sig.Name ?? "unknown");
                                }
                                else
                                {
                                    sig.SingalId = sigId++;
                                }
                            }
                            else
                            {
                                sig.SingalId = 0;
                            }
                        }

                    dbc.DbcstrJson = JsonConvert.SerializeObject(dbc.dbcDatabase);
                }

                // Store all 3 port DBC IDs/names in session
                Session.DbcFileRecordID = port1Dbc?.Id ?? 0;
                Session.DbcName = port1Dbc?.Name ?? string.Empty;
                Session.Port2DbcFileRecordID = port2Dbc?.Id ?? 0;
                Session.Port2DbcName = port2Dbc?.Name ?? string.Empty;
                Session.Port3DbcFileRecordID = port3Dbc?.Id ?? 0;
                Session.Port3DbcName = port3Dbc?.Name ?? string.Empty;
                // Merge all 3 DBC databases so the session file has the full signal ID→name map
                Session.dbcDatabse = MergeDbcDatabases(
                    port1Dbc?.dbcDatabase,
                    port2Dbc?.dbcDatabase,
                    port3Dbc?.dbcDatabase);

                const int MaxPacketSize = 1400;

                byte[] packets = DbcDatabase.BuildMultiPortPayload(
                    port1Dbc?.dbcDatabase,
                    port2Dbc?.dbcDatabase,
                    port3Dbc?.dbcDatabase);

                // Send total packet count first
                byte[] totalPacketsBytes = BitConverter.GetBytes(
                    (short)((packets.Length + MaxPacketSize - 1) / MaxPacketSize));
                if (BitConverter.IsLittleEndian) Array.Reverse(totalPacketsBytes);

                byte[] totalPacketsPayload = DecoderService.BuildCommand(
                    new CommandRequest
                    {
                        Start = Models.Enums.StartByte.Program,
                        DeviceId = Channel.DeviceID,
                        SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                        ChannelId = Channel.ChannelNumber,
                        QueryId = (byte)Models.Enums.ProgramDataQuery.SendDbcStepsCount,
                        Data = totalPacketsBytes
                    });

                var countResponse = await SendAndWaitForResponseAsync<bool>(totalPacketsPayload);
                if (!countResponse.Success)
                    return CommonResponse<bool>.Fail("Failed to send DBC file packet count. Error: " + countResponse.Message);

                for (int offset = 0; offset < packets.Length; offset += MaxPacketSize)
                {
                    int chunkSize = Math.Min(MaxPacketSize, packets.Length - offset);
                    byte[] chunkWithLength = new byte[chunkSize + 2];

                    ushort length = (ushort)chunkSize;
                    byte[] lengthBytes = BitConverter.GetBytes(length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

                    Array.Copy(lengthBytes, 0, chunkWithLength, 0, 2);
                    Array.Copy(packets, offset, chunkWithLength, 2, chunkSize);

                    byte[] payload = DecoderService.BuildCommand(
                        new CommandRequest
                        {
                            Start = Models.Enums.StartByte.Program,
                            DeviceId = Channel.DeviceID,
                            SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                            ChannelId = Channel.ChannelNumber,
                            QueryId = (byte)Models.Enums.ProgramDataQuery.SendDbcFile,
                            Data = chunkWithLength
                        });

                    var response = await SendAndWaitForResponseAsync<bool>(payload);
                    if (!response.Success)
                        return CommonResponse<bool>.Fail(
                            $"Failed to transfer DBC file chunk at offset {offset}. Error: {response.Message}");
                }

                if (skippedSignals.Count > 0)
                    return CommonResponse<bool>.Ok(true,
                        $"Transfer completed with warnings: {skippedSignals.Count} signal(s) exceeded the 255 ID limit and were skipped: {string.Join(", ", skippedSignals)}");

                return CommonResponse<bool>.Ok(true, "Success");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Exception during DBC transfer: {ex.Message}");
            }
            finally
            {
                _log.Debug("TransferDbcFile");
            }
        }

        #region PRODUCER / TABLE session helpers

        /// <summary>
        /// Loads the ProgramDTO (with steps) for every PRODUCER step found in the given step list.
        /// Returns an empty list if no PRODUCER steps exist.
        /// </summary>
        private async Task<List<ProgramDTO>> ResolveProducerProgramsAsync(List<StepModel> steps)
        {
            var result = new List<ProgramDTO>();

            var producerNames = steps
                .Where(s => s.OperatorCode == OperatorConstants.PRODUCER)
                .Select(s => s.NominalValues.FirstOrDefault() ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (producerNames.Count == 0)
                return result;

            using var pService = ServiceLocator.GetScoped<IProgramServices>();
            if (pService?.Service == null)
                return result;

            foreach (var name in producerNames)
            {
                var response = await pService.Service.GetProgramByNameAsync(name);
                if (response.Success && response.Data != null)
                    result.Add(response.Data);
            }

            return result;
        }

        /// <summary>
        /// Reads the actual file content for every TABLE step in the step list.
        /// Returns a dictionary of fileName → lines[].
        /// </summary>
        private static Dictionary<string, string[]> CollectTableFileData(List<StepModel> steps)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            var tableNames = steps
                .Where(s => s.OperatorCode == OperatorConstants.TABLE)
                .Select(s => s.NominalValues.FirstOrDefault() ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var fileName in tableNames)
            {
                try
                {
                    var loaded = FileManagerService.ReadFileLines(fileName);
                    if (loaded.Success && loaded.Data != null)
                        result[fileName] = loaded.Data;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "CollectTableFileData: could not load table file {FileName}", fileName);
                }
            }

            return result;
        }

        #endregion

        public void Dispose()
        {

            try
            {
                _cts.Cancel();

                _tcpClient?.Close();
                _tcpClient?.Dispose();

                _lastStoredDbcDate = default;
            }
            catch { }
            finally
            {

                _log.Debug($"Dispose - {Channel?.DeviceID}-{Channel?.ChannelNumber}");
            }

        }

        private static DbcDatabase MergeDbcDatabases(DbcDatabase? p1, DbcDatabase? p2, DbcDatabase? p3)
        {
            var merged = new DbcDatabase();
            foreach (var db in new[] { p1, p2, p3 })
            {
                if (db?.Messages == null) continue;
                foreach (var kv in db.Messages)
                    merged.Messages[kv.Key] = kv.Value;
            }
            return merged;
        }

    }
}
