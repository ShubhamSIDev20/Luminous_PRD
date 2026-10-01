# 13. Code Call Sequences

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Chapters 1–12 show *what* happens. This chapter shows *which method calls which*,
in order, with the exact signatures. Read a lane diagram top-to-bottom: time flows
downward, each numbered arrow is one call, and `-->` returns are shown where the
return value matters.

---

### S1. Registration — hardware connects and announces a channel

```
 HARDWARE   ChannelManager      ChannelManager        DecoderService   IDeviceChannel   ChannelManager
            RunCommand          HandleCommand                          Services         Add
            ListenerAsync       ClientAsync
    |            |                    |                     |               |               |
 1  |  TCP SYN ->|                    |                     |               |               |
    |            |                    |                     |               |               |
 2  |            | AcceptTcpClientAsync(token)              |               |               |
    |            |-------------------->|                    |               |               |
    |            |  _ = HandleCommandClientAsync(client, token)             |               |
    |            |    (fire-and-forget: one task per socket)                |               |
    |            |                    |                     |               |               |
 3  | 0xDD 0x01 ------------------->  | stream.ReadAsync(buffer[1024], token)               |
    |            |                    |                     |               |               |
 4  |            |                    | buffer[0]==0xDD && buffer[1]==0x01  |               |
    |            |                    | -> ProcessRegistrationPacketAsync(buffer, client)   |
    |            |                    |                     |               |               |
 5  |            |                    | ParseRegistrationPacket(buffer)     |               |
    |            |                    |-------------------->|               |               |
    |            |                    |<-- ChannelDto? newChannel           |               |
    |            |                    |   (null -> log warning, return null)|               |
    |            |                    |                     |               |               |
 6  |            |                    | ServiceLocator.GetScoped<IDeviceChannelServices>()  |
    |            |                    |                     |               |               |
 7  |            |                    | GetChannelAsync(newChannel)         |               |
    |            |                    |------------------------------------>|               |
    |            |                    |<-- CommonResponse<ChannelDto> existingChannel       |
    |            |                    |                     |               |               |
 8  |            |                    |  BRANCH on (Data != null, IsRegistered, IsDeleted)  |
    |            |                    |                     |               |               |
    |            |   8a  EXISTS && IsRegistered == true      |               |               |
    |            |                    | Add(existingChannel.Data, client)   |               |
    |            |                    |------------------------------------------------->  |
    |            |                    |<-- CommonResponse<IChannelCommandHandler> addResult |
    |            |                    |   msg contains "already exists"                     |
    |            |                    |     -> status = AlreadyRegistered (0x02)            |
    |            |                    |     -> UpdateRegistration(newChannel)               |
    |            |                    |        (refresh Name / IP / MAC)                    |
    |            |                    |   else -> status = Success (0x01)                   |
    |            |                    |   addResult failed -> status = Failed (0x00)        |
    |            |                    |                     |               |               |
    |            |   8b  EXISTS && IsRegistered == false     |               |               |
    |            |                    | if (IsDeleted) UpdateIsDeleteAsync(newChannel,false)|
    |            |                    | status = Failed (0x00)              |               |
    |            |                    |                     |               |               |
    |            |   8c  DOES NOT EXIST                      |               |               |
    |            |                    | InsertAsync(newChannel)             |               |
    |            |                    |------------------------------------>|               |
    |            |                    | status = Failed (0x00)              |               |
    |            |                    |                     |               |               |
 9  |            |                    | ParseRegistrationResponse(          |               |
    |            |                    |     deviceId,                       |               |
    |            |                    |     ChannelAddressCodec.Encode(board, ch),          |
    |            |                    |     status)         |               |               |
    |            |                    |-------------------->|               |               |
    |            |                    |<-- byte[] responseBytes             |               |
    |            |                    |                     |               |               |
10  |            |                    | return (newChannel, responseBytes)  |               |
    |            |                    |                     |               |               |
11  |            |                    | boardsOnThisSocket[board] = GetOrCreateDeviceConnection(DeviceID, board)
    |            |                    |                     |               |               |
12  |            |                    | SendOnly(client, response, token)   |               |
    |<-------------------------------- |   WriteAsync -> FlushAsync -> Delay(20ms)          |
    |            |                    |                     |               |               |
13  |            |                    | continue  (loop back to 3 for the NEXT channel      |
    |            |                    |            on this same socket)     |               |
```

**Entry:** `ChannelManager.cs:579` → **Exit:** back to the read loop at `:629`.

---

### S2. Operator approval — the Allow click

```
 OPERATOR   DeviceList     IDeviceChannel   IDeviceChannel     MAIN DB      HARDWARE
            .razor         Services         Repository
    |           |               |                 |               |            |
 1  | click badge               |                 |               |            |
    |---------->|               |                 |               |            |
    |           | ShowRegisteredConfirm(circuit)  |               |            |
    |           |   confirmAction = () => RegisterCircuitAsync(circuit)         |
    |           |   isConfirmDialogOpen = true    |               |            |
    |           |               |                 |               |            |
 2  | confirm   |               |                 |               |            |
    |---------->| ExecuteConfirmAction()          |               |            |
    |           |   -> confirmAction.Invoke()     |               |            |
    |           |               |                 |               |            |
 3  |           | AllowCicuitAsync(circuit, true) |               |            |
    |           |-------------->|                 |               |            |
    |           |               | AllowCicuitAsync(circuit, true) |            |
    |           |               |---------------->|               |            |
    |           |               |                 | UPDATE Channels            |
    |           |               |                 | SET IsRegistered = 1       |
    |           |               |                 |-------------->|            |
    |           |               |<-- CommonResponse<ChannelDto>   |            |
    |           |<--------------|                 |               |            |
    |           |               |                 |               |            |
 4  |           | Toast.Success(...)              |               |            |
    |           | await GetLoadStartup()   (reload the grid)      |            |
    |           |               |                 |               |            |
    |           |     NOTHING IS SENT TO THE HARDWARE HERE                     |
    |           |               |                 |               |            |
 5  |           |               |                 |               | retry      |
    |           |               |                 |               | 0xDD 0x01  |
    |           |               |                 |               |<-----------|
    |           |               |                 |    -> re-enters S1 at step 3,
    |           |               |                 |       now lands in branch 8a,
    |           |               |                 |       responds Success (0x01)
```

**Deregister is the mirror image**, plus teardown:

```
 3' AllowCicuitAsync(circuit, false)      -> IsRegistered = 0
 4' CM.Remove(circuit, force: true)
        -> handler.UnregisterAsync()      -> BuildRegistration(dev, addr, RegistrationQuery.Delete)
                                          -> SendAndWaitForResponseAsync<bool>   [0xDD / 0x02]
        -> _devices.TryRemove(key)
```

**Handler creation, expanded (`ChannelManager.Add`, called at S1 step 8a):**

```
 Add(channel, tcpClient)
  |
  1  if (!channel.IsRegistered) -> Fail("Please allow Channel ...")        <-- the gate
  2  key = MakeKey(DeviceID, SecondaryBoardNumber, ChannelNumber)
  3  Get(channel)                                    -> existing handler?
  |
  |   YES + ReferenceEquals(oldClient, tcpClient):
  |       Connection = Connection                    (re-arm this slot's offline flag)
  |       GetOrCreateDeviceConnection(DeviceID, board).ChannelSlotKeys.Add(key)
  |       return Ok("already TCP Client")
  |
  |   YES + different socket:
  |       existingDeviceConnection = GetOrCreateDeviceConnection(DeviceID, board)
  |       existingDeviceConnection.AttachLink(GetOrCreateDeviceLink(tcpClient))
  |       existingDeviceConnection.ChannelSlotKeys.Add(key)
  |       IsHandler.Data.Connection = existingDeviceConnection
  |       return Ok("already exists")                <-- triggers AlreadyRegistered
  |
  |   NO:
  4      deviceConnection = GetOrCreateDeviceConnection(DeviceID, board)
  5      deviceConnection.AttachLink(GetOrCreateDeviceLink(tcpClient))
  6      deviceConnection.ChannelSlotKeys.Add(key)
  7      handler = CreateNewHandler()                (_handlerFactory())
  8      handler._cts        = _lifecycleCts
  9      handler.Channel     = channel
 10      handler.Connection  = deviceConnection
 11      await handler.InitializeAsync()             --> S2b
 12      _devices.TryAdd(key, handler)
         return Ok("Channel Handler added")          <-- triggers Success
  |
  finally: HardwareManagerChanged?.Invoke()          (UI refresh)
```

> ⚠️ `tcpClient` is **optional** here. Startup rehydration (`LoadDevicesAsync`) and the
> operator "Allow" gate call `Add` with `null`, meaning "this board has no live socket yet".
> `GetOrCreateDeviceLink(null)` returns `null` and `AttachLink(null)` is a deliberate no-op
> that keeps any link a real registration already attached — it must never clear it, and it
> must never be passed to `ConcurrentDictionary.GetOrAdd` (a null key throws).

**S2b — `InitializeAsync` rehydration:**

```
 InitializeAsync()
  1  scope = ServiceLocator.CreateScope()
  2  programService.GetLastSessionAsync(Channel)   --> SessionRecordDto
  3      Session = GetSession.Data
  4      Program = Session.programs
  5      Battery = Session.battery
  6  dbcService.GetByIdAsync(Session.DbcFileRecordID      ?? 0)   --> p1
  7  dbcService.GetByIdAsync(Session.Port2DbcFileRecordID ?? 0)   --> p2
  8  dbcService.GetByIdAsync(Session.Port3DbcFileRecordID ?? 0)   --> p3
  9  Session.dbcDatabse = MergeDbcDatabases(p1Db, p2Db, p3Db)
  finally:
 10  _ = StartStoreWorkerAsync()      (fire-and-forget; runs for the handler's lifetime)
```

---

### S3. Transfer from the UI (`TransferDialog.razor`)

```
 OPERATOR  TransferDialog  ChannelCommandHandler   DecoderService/   DeviceConnection  HARDWARE
           .razor                                  DbcDatabase
    |          |                  |                      |                |             |
 1  | Transfer |                  |                      |                |             |
    |--------->|                  |                      |                |             |
    |          | HWReadyToReadWriteAsync()               |                |             |
    |          |----------------->|                      |                |             |
    |          |                  | BuildCommand(Program, 0x01)           |             |
    |          |                  |--------------------->|                |             |
    |          |                  | SendAndWaitForResponseAsync<bool>(payload)          |
    |          |                  |-------------------------------------->|             |
    |          |                  |                      |   SendAndWaitAsync(          |
    |          |                  |                      |     addr, queryId, cmd, 15s) |
    |          |                  |                      |                |------------>|
    |          |                  |                      |                |<------------|
    |          |<-- CommonResponse<bool> isReadyResponse |                |             |
    |          |                  |                      |                |             |
    |          |  FAIL -> both steps marked "Skipped: Device not ready", STOP           |
    |          |                  |                      |                |             |
 2  |          | SetBatteryParamAsync(battery)           |                |             |
    |          |----------------->|                      |                |             |
    |          |                  | guard: ProgramStatus != Running       |             |
    |          |                  | BuildBatteryBytes(battery)            |             |
    |          |                  |--------------------->|                |             |
    |          |                  | BuildCommand(Configuration, 0x05, data)             |
    |          |                  | Session.BatteryID / BatteryName / battery = ...     |
    |          |                  | Battery = BatteryParams               |             |
    |          |                  | SendAndWaitForResponseAsync<bool>     |------------>|
    |          |<-- batteryResponse                      |                |             |
    |          |                  |                      |                |             |
 3  |          | resolve p1Dbc / p2Dbc / p3Dbc from DbcFiles by id        |             |
    |          |   all three null -> SKIP step 4 entirely                 |             |
    |          |                  |                      |                |             |
 4  |          | TransferDbcFile(p1Dbc, p2Dbc, p3Dbc)    |                |             |
    |          |----------------->|                      |                |             |
    |          |                  | 4a sigId = 31; for each selected signal across all  |
    |          |                  |    3 ports: SingalId = sigId++  (0 if unselected    |
    |          |                  |    or if sigId > 255 -> name added to skippedSignals)|
    |          |                  | 4b DbcstrJson = JsonConvert.SerializeObject(...)    |
    |          |                  | 4c Session.DbcFileRecordID / Port2.. / Port3.. = .. |
    |          |                  | 4d MergeDbcDatabases(p1,p2,p3) -> Session.dbcDatabse|
    |          |                  | 4e BuildMultiPortPayload(p1,p2,p3)    |             |
    |          |                  |--------------------->|                |             |
    |          |                  |<-- byte[] packets    |                |             |
    |          |                  | 4f BuildCommand(Program, 0x07,        |             |
    |          |                  |      ceil(len/1400) as int16 BE)      |             |
    |          |                  |    SendAndWaitForResponseAsync<bool>  |------------>|
    |          |                  |    FAIL -> abort the whole transfer   |             |
    |          |                  | 4g for offset in 0, 1400, 2800, ...:  |             |
    |          |                  |      chunkWithLength = [len 2B BE][chunk]           |
    |          |                  |      BuildCommand(Program, 0x08, chunkWithLength)   |
    |          |                  |      SendAndWaitForResponseAsync<bool>|------------>|
    |          |                  |      FAIL -> abort, report the offset |             |
    |          |<-- Ok(true) or Ok(true, "Transfer completed with warnings: ...")       |
    |          |                  |                      |                |             |
 5  |          | dev.dbcData.DbcValues = null            |                |             |
    |          |                  |                      |                |             |
 6  |          | SetProgramAsync(program)                |                |             |
    |          |----------------->|                      |                |             |
    |          |                  | guard: ProgramStatus != Running       |             |
    |          |                  | 6a ResolveProducerProgramsAsync(steps)|             |
    |          |                  |      -> IProgramServices.GetProgramAsync per        |
    |          |                  |         PRODUCER step                 |             |
    |          |                  | 6b ExpandedProgramSteps =             |             |
    |          |                  |      ProgramBuilder.ExpandProducerSteps(steps, dict)|
    |          |                  | 6c ConvertProgramIntoBytesPackets(steps, dict)      |
    |          |                  |--------------------->|                |             |
    |          |                  |<-- List<byte[]> steps|                |             |
    |          |                  | 6d re-pack into <=1400-byte payloads  |             |
    |          |                  |    (a step MAY be split across two)   |             |
    |          |                  | 6e BuildCommand(Program, 0x03,        |             |
    |          |                  |      payloads.Count as int16 BE)      |             |
    |          |                  |    SendAndWaitForResponseAsync<bool>  |------------>|
    |          |                  |    FAIL -> return immediately         |             |
    |          |                  | 6f for each payload:                  |             |
    |          |                  |      fullData = [len 2B BE][payload]  |             |
    |          |                  |      BuildCommand(Program, 0x04, fullData)          |
    |          |                  |      SendAndWaitForResponseAsync<bool>|------------>|
    |          |                  |      FAIL -> return that failure      |             |
    |          |                  | 6g Session.ProgramHash / ProgramID /  |             |
    |          |                  |    ProgramName / programs = ...       |             |
    |          |                  |    Program = ProgramDto               |             |
    |          |<-- Ok(true)      |                      |                |             |
    |          |                  |                      |                |             |
 7  |          | build List<StepResult> and render:                       |             |
    |<---------|   IsReady | Program Transfer | Battery Transfer | DBC Transfer          |
    |          |   (DISPLAY order -- NOT the execution order above)       |             |
```

> **⚠ Note the last line.** The dialog assembles its result list as
> `{ isReadyStep, programStep, batteryStep }` and appends `dbcStep` only if it ran.
> Execution order is Battery → DBC → Program; display order is Program → Battery → DBC.
> This is the single most common source of confusion about this flow.
> Source: `TransferDialog.razor:448-458`.

---

### S4. Transfer from REST / MCP (`DeviceController.CoreSendProgram`)

**This path uses a different order and only one DBC port.** Both paths are live.

```
 CLIENT   DeviceController   DeviceController      IProgramServices /   ChannelCommandHandler
          .SendProgram       .CoreSendProgram      IBatteryServices /
                                                   IDbcService
    |          |                    |                      |                  |
 1  | POST /api/Device/SendProgram  |                      |                  |
    |   body: List<CommonRequest>   |                      |                  |
    |--------->|                    |                      |                  |
    |          | CoreSendProgram(_cm, _programServices,    |                  |
    |          |     _batteryServices, _dbcService, request)                  |
    |          |------------------->|                      |                  |
    |          |                    |                      |                  |
 2  |          |   foreach (req in request):               |                  |
    |          |     key = "{DeviceID}-{SecondaryBoardNumber}-{ChannelNumber}"|
    |          |     cm._devices.TryGetValue(key, out handler)                |
    |          |       not found -> messages.Add("{key} -> Handler not found")|
    |          |                    |                      |     CONTINUE     |
    |          |                    |                      |                  |
 3  |          |     HWReadyToReadWriteAsync()             |                  |
    |          |                    |------------------------------------->   |
    |          |       fail -> messages.Add("Hardware not ready"), CONTINUE   |
    |          |                    |                      |                  |
 4  |          |     GetProgramAsync(req.ProgramId)        |                  |
    |          |                    |--------------------->|                  |
    |          |     SetProgramAsync(program.Data)         |     <-- FIRST    |
    |          |                    |------------------------------------->   |
    |          |                    |                      |                  |
 5  |          |     GetBattery(req.BatteryId)             |                  |
    |          |                    |--------------------->|                  |
    |          |     SetBatteryParamAsync(battery.Data)    |     <-- SECOND   |
    |          |                    |------------------------------------->   |
    |          |                    |                      |                  |
 6  |          |     GetByIdAsync(req.dbcId)               |                  |
    |          |                    |--------------------->|                  |
    |          |     TransferDbcFile(dbc.Data)             |     <-- THIRD,   |
    |          |                    |------------------------------------->   |
    |          |                    |   ONLY port 1 -- p2 and p3 default null |
    |          |                    |                      |                  |
 7  |          |     handler.dbcData.DbcValues = null      |                  |
    |          |                    |                      |                  |
 8  |          |<-- List<string> messages ("Program : 1-0-1 -> Success", ...) |
    |<---------| Ok(messages)       |                      |                  |
```

| | UI path (`TransferDialog`) | REST / MCP path (`CoreSendProgram`) |
|---|---|---|
| Order | Battery → DBC → Program | **Program → Battery → DBC** |
| DBC ports | 3 (`p1, p2, p3`) | **1 only** (`dbc.Data` → port 1) |
| Not-ready | skips everything, reports it | `continue` to the next request item |
| Sub-step failure | aborts the remaining steps | records the message, keeps going |

> **⚠ These two entry points do not agree.** A channel loaded over REST receives its
> program *before* its DBC map, and can never be given port-2 or port-3 DBC files.
> Whether the hardware tolerates program-before-DBC has not been verified here —
> confirm before using the REST path for any DBC-dependent program.
> Source: `DeviceController.cs:41-108` vs `TransferDialog.razor:372-445`.

---

### S5. Start program

```
 CALLER  ChannelCommandHandler   DecoderService   ISqliteBulk        IProgram    DeviceConn  HW
         .StartProgram                            DatabaseManager    Services
   |            |                       |               |               |           |       |
 1 | StartProgram()                     |               |               |           |       |
   |----------->|                       |               |               |           |       |
   |            | GUARD 1: Program == null || ProgramSteps == 0                     |       |
   |            | GUARD 2: Program.ProgramHash != Session.ProgramHash               |       |
   |            | GUARD 3: RealTime.ProgramStatus == Running                        |       |
   |            | GUARD 4: RealTime.CircuitStatus != Idle                           |       |
   |            |   any guard trips -> CommonResponse<bool>.Fail(msg), RETURN       |       |
   |            |                       |               |               |           |       |
 2 |            | GetSessionIdBytes(DeviceID,           |               |           |       |
   |            |     ChannelAddressCodec.Encode(board, ch),            |           |       |
   |            |     out epochSeconds, out sessionDateTime)            |           |       |
   |            |---------------------->|               |               |           |       |
   |            |<-- byte[4] sessionID  |               |               |           |       |
   |            |                       |               |               |           |       |
 3 |            | CommandTracker.AddCommand("Start command executed by <user>!")    |       |
   |            |                       |               |               |           |       |
 4 |            | BuildCommand(Control, 0x01, Data = sessionID)         |           |       |
   |            |---------------------->|               |               |           |       |
   |            |<-- byte[] payload     |               |               |           |       |
   |            |                       |               |               |           |       |
 5 |            | STAMP Session: SessionID, DeviceID, SecondaryBoardNumber,         |       |
   |            |   ChannelNumber, StartTime = Now, EndTime = null,                 |       |
   |            |   SessionName = "<ProgramName>_yyyyMMddHHmmssfff",                |       |
   |            |   SessionFilePath = "<dd-MM-yyyy>/<SID>_<Dev>_<Board>_<Ch>.db",   |       |
   |            |   Unstorerecordcount = 0, Storerecordcount = 0                    |       |
   |            |                       |               |               |           |       |
 6 |            | ResolveProducerProgramsAsync(ProgramStepModel)        |           |       |
   |            |   -> IProgramServices.GetProgramAsync per PRODUCER step           |       |
   |            | CollectTableFileData(ProgramStepModel)                |           |       |
   |            |                       |               |               |           |       |
 7 |            | InsertSessionAsync(new SessionRequest {               |           |       |
   |            |     filePath, Program, Battery, dbcDatabase,          |           |       |
   |            |     ProducerPrograms, TableFileData, ExpandedProgramSteps })      |       |
   |            |-------------------------------------->|               |           |       |
   |            |            GetContext(filePath) -> BuildPath -> CreateDirectory   |       |
   |            |            EnsureCreated() -> SqliteSchemaSync.SyncModelWithDatabase       |
   |            |            BeginTransactionAsync()    |               |           |       |
   |            |            configurationEntities.Add("Program" / "Battery" /      |       |
   |            |                 "Dbc" / producers / tables / expanded steps)      |       |
   |            |            SaveChanges + Commit       |               |           |       |
   |            |                       |               |               |           |       |
   |            |   THE SESSION FILE NOW EXISTS ON DISK, BEFORE THE COMMAND IS SENT |       |
   |            |                       |               |               |           |       |
 8 |            | SendAndWaitForResponseAsync<bool>(payload)           |            |       |
   |            |------------------------------------------------------------------>|      |
   |            |            SendAndWaitAsync(addr, 0x01, payload, 15s)  |          |------>|
   |            |                                                       |          |<------|
   |            |<-- CommonResponse<bool> start                         |          |       |
   |            |                       |               |               |           |       |
 9 |            | start.Success ?       |               |               |           |       |
   |            |   YES -> CreateSession(Session)       |               |           |       |
   |            |          --------------------------------------------->|          |       |
   |            |          (row in main DB BatterySession)               |          |       |
   |            |   NO  -> return start (the .db file stays empty; harmless)        |       |
   |            |                       |               |               |           |       |
10 |            | ErrorMessages.GetErrors(true) / GetMessages(true)     |           |       |
   |<-----------| return start          |               |               |           |       |
```

---

### S6. UDP 10001 — packet to disk

```
 HW   RunUdpStore   _udpChannel   StartUdp      StoreUdpData   Decoder   ChannelCmd   _Store   StartStore   ISqliteBulk
      ListenerAsync  (Channel<T>) ProcessorAsync               Service   Handler      Queue    WorkerAsync  DatabaseMgr
  |        |             |            |              |            |          |          |          |            |
1 | UDP -> |             |            |              |            |          |          |          |            |
  |        | ReceiveAsync()           |              |            |          |          |          |            |
2 |        | Writer.WriteAsync(result)|              |            |          |          |          |            |
  |        |------------>|            |              |            |          |          |          |            |
  |        | loop back to 1 IMMEDIATELY (no parsing, no I/O on this thread)  |          |          |            |
  |        |             |            |              |            |          |          |          |            |
3 |        |             | Reader.ReadAllAsync(token)|            |          |          |          |            |
  |        |             |----------->|              |            |          |          |          |            |
4 |        |             |            | StoreUdpData(packet, token)          |          |          |            |
  |        |             |            |------------->|            |          |          |          |            |
  |        |             |            |  try/catch: one bad packet never kills the loop |          |            |
  |        |             |            |              |            |          |          |          |            |
5 |        |             |            |              | RealStoreDataV2(payload)         |          |            |
  |        |             |            |              |----------->|          |          |          |            |
  |        |             |            |              |<-- CommonResponse<recordStoreRequest>       |            |
  |        |             |            |              |  !Success -> TrySendFailureNotification(payload, msg)    |
  |        |             |            |              |             -> ChannelAddressCodec.Decode(payload[1])    |
  |        |             |            |              |             -> Get(...) -> SendNotification(...)         |
  |        |             |            |              |             -> RETURN (packet dropped)     |            |
  |        |             |            |              |            |          |          |          |            |
6 |        |             |            |              | first = RealStoreRecord.FirstOrDefault()   |            |
  |        |             |            |              |   null -> log, RETURN            |          |            |
  |        |             |            |              |            |          |          |          |            |
7 |        |             |            |              | Get(new ChannelDto{ DeviceId,    |          |            |
  |        |             |            |              |     SecondaryBoardNumber, ChannelId })     |            |
  |        |             |            |              |   not found -> log, RETURN       |          |            |
  |        |             |            |              |            |          |          |          |            |
8 |        |             |            |              | SessionIdToDateTime(first.SessionID,       |            |
  |        |             |            |              |     out sessionDateTime, true)   |          |            |
  |        |             |            |              |----------->|          |          |          |            |
  |        |             |            |              |   false -> log, sessionDateTime = DateTime.Now          |
  |        |             |            |              |            |          |          |          |            |
9 |        |             |            |              | filePath = Path.Combine(         |          |            |
  |        |             |            |              |     dd-MM-yyyy,                  |          |            |
  |        |             |            |              |     "{SID}_{Dev}_{Board}_{Ch}.db")         |            |
  |        |             |            |              |            |          |          |          |            |
10|        |             |            |              | EnqueueForStore(decoded.Data,    |          |            |
  |        |             |            |              |     handler.dbcData?.DbcValues)  |          |            |
  |        |             |            |              |---------------------->|          |          |            |
  |        |             |            |              |     dbcData.CreatedDate > _lastStoredDbcDate ?          |
  |        |             |            |              |       YES: record[0].dbcValues = JSON(dbcRecord)        |
  |        |             |            |              |            record[1..n].dbcValues = null                |
  |        |             |            |              |            _lastStoredDbcDate = dbcData.CreatedDate     |
  |        |             |            |              |       NO : all .dbcValues = null |          |            |
  |        |             |            |              |     _StoreQueue.Writer.TryWrite(dto)       |            |
  |        |             |            |              |                       |--------->|          |            |
  |        |             |            |              |     Session.Unstorerecordcount += count    |            |
  |        |             |            |              |            |          |          |          |            |
11|        |             |            |              | _eventBus.PublishAsync<List<MeasurementData>>(          |
  |        |             |            |              |     filePath, records)  -> live charts/grids            |
  |        |             |            |              |            |          |          |          |            |
12|        |             |            |              |            |          |          | Reader.ReadAllAsync(_cts.Token)
  |        |             |            |              |            |          |          |--------->|            |
  |        |             |            |              |            |          |          |          |            |
13|        |             |            |              |  scan for SystemErrorID in 1..17 && ProgramRunningTime != 0
  |        |             |            |              |      -> OnNotify(NotificationItem "<D-B-C> System Error: <enum>")
  |        |             |            |              |            |          |          |          |            |
14|        |             |            |              |            |          |          |          | InsertRecordAsync(item)
  |        |             |            |              |            |          |          |          |----------->|
  |        |             |            |              |     GetContext(filePath) -> EnsureCreated -> SchemaSync  |
  |        |             |            |              |     FetchDbcDatabaseAsync(ctx)              |            |
  |        |             |            |              |     orderedSignalIds = selected && SingalId != 0,        |
  |        |             |            |              |                        distinct, ascending  |            |
  |        |             |            |              |     rec.dbcValues: signal-ID dict -> index-aligned array |
  |        |             |            |              |     REG-with-label -> RegLogs ; else -> Measurements     |
  |        |             |            |              |     SaveChangesAsync  |          |          |            |
  |        |             |            |              |            |          |          |          |            |
15|        |             |            |              |  any record Operator == OperatorConstants.STO ?          |
  |        |             |            |              |      -> Session.EndTime = Now    |          |            |
  |        |             |            |              |      -> IProgramServices.EndSession(Session)             |
  |        |             |            |              |      -> log "END Session"        |          |            |
  |        |             |            |              |            |          |          |          |            |
16|        |             |            |              |  Unstorerecordcount -= count (floor 0)     |            |
  |        |             |            |              |  Storerecordcount   += count     |          |            |
  |        |             |            |              |  catch -> log "SQLite store error", CONTINUE the loop    |
```

**Three independent loops, three failure boundaries.** A crash in stage 14 does not
stop stage 3, and neither stops stage 1. That is why each has its own try/catch that
logs and continues rather than propagating.

---

### S7. Command send / response correlation

Every command in the system funnels through this pair.

```
 CALLER   ChannelCommandHandler        DeviceConnection          ChannelManager        HW
          SendAndWaitForResponseAsync                            HandleCommandClientAsync
    |             |                          |                          |               |
 1  | <cmd>()     |                          |                          |               |
    |------------>|                          |                          |               |
    |             | Connection?.TcpClient == null || !Connected         |               |
    |             |     -> Fail("TCP Command client is not connected.") |               |
    |             | commandinterrupt == true                            |               |
    |             |     -> Fail("Another command is in progress.")      |               |
    |             | commandinterrupt = true                             |               |
    |             |                          |                          |               |
 2  |             | addressByte = ChannelAddressCodec.Encode(board, ch) |               |
    |             | queryId     = command[3]  (BuildCommand always puts it there)       |
    |             |                          |                          |               |
 3  |             | SendAndWaitAsync(addressByte, queryId, command, 15s)|               |
    |             |------------------------->|                          |               |
    |             |          a. await _writeLock.WaitAsync()            |               |
    |             |          b. _pendingResponses[(addr, queryId)] = new TCS<byte[]>()  |
    |             |          c. stream.WriteAsync(command)  ------------------------->  |
    |             |          d. _writeLock.Release()         |          |               |
    |             |          e. await tcs.Task with 15 s timeout        |               |
    |             |                          |                          |               |
 4  |             |                          |     stream.ReadAsync(buffer[1024])       |
    |             |                          |          <--------------------------------
    |             |                          |     buffer[0]==0xDD && buffer[1]==0x01 ? |
    |             |                          |       yes -> registration path (S1)      |
    |             |                          |       no  -> continue                    |
    |             |                          |                          |               |
 5  |             |                          | HandleIncomingPacket(    |               |
    |             |                          |     buffer[2],   // address byte         |
    |             |                          |     buffer[3],   // query id             |
    |             |                          |     buffer.Take(read).ToArray())         |
    |             |                          |<-------------------------|               |
    |             |          _pendingResponses.TryGetValue((addr, queryId), out tcs)    |
    |             |          tcs.TrySetResult(payload)                  |               |
    |             |                          |                          |               |
 6  |             |<-- byte[] response       |                          |               |
    |             | DecoderService.TryDecode<T>(response)               |               |
    |             |                          |                          |               |
    |             | catch TimeoutException          -> "No response received from device."
    |             | catch OperationCanceledException-> "Read timed out after 15 seconds."
    |             | catch Exception                 -> "Exception: {msg}"
    |             | finally: commandinterrupt = false                   |               |
    |             |          OnChannelChanged?.Invoke()                 |               |
    |<------------| CommonResponse<T>        |                          |               |
```

**`commandinterrupt` is per-handler, not per-device.** Two *different* channels on the
same device can have commands in flight simultaneously — the `SemaphoreSlim` serializes
the writes and the `(addr, queryId)` key keeps their responses apart. What it prevents is
one channel issuing a second command before its first returns.

---

### S8. Disconnect and reconnect

```
 HW        ChannelManager                 DeviceConnection      ChannelCommandHandler(s)
           HandleCommandClientAsync
  |             |                               |                        |
1 | socket dies |                               |                        |
  |------------>| stream.ReadAsync returns 0    |                        |
  |             |   (or throws IOException)     |                        |
  |             | break out of the while loop   |                        |
  |             |                               |                        |
2 |             | finally:                      |                        |
  |             |   deviceConnection != null &&  |                        |
  |             |   ReferenceEquals(deviceConnection.TcpClient, client) ? |
  |             |                               |                        |
  |             |   NO  -> a NEWER socket already replaced this one.      |
  |             |          SKIP all cleanup. (Without this guard a stale  |
  |             |          loop re-marks just-reconnected channels        |
  |             |          offline -- the "only 1 of N cards Connected"   |
  |             |          bug.)                |                        |
  |             |                               |                        |
3 |             |   YES -> FailAllPending(new IOException(               |
  |             |              "Device connection closed."))             |
  |             |          ------------------->|                        |
  |             |          every pending TCS.TrySetException(...)        |
  |             |          -> awaiting callers fail NOW, not in 15 s     |
  |             |                               |                        |
4 |             |   foreach (slotKey in deviceConnection.ChannelSlotKeys)|
  |             |       _devices[slotKey].MarkDisconnected()             |
  |             |       ------------------------------------------------>|
  |             |       ALL channels on this device go offline together  |
  |             |                               |                        |
5 |             |   HardwareManagerChanged?.Invoke()   -> UI refresh     |
  |             |                               |                        |
  |             |   NOTE: the store worker keeps running. Session,       |
  |             |   Program, Battery and DBC state live on the handler,  |
  |             |   not on the socket.          |                        |
  |             |                               |                        |
6 | reconnect ->| AcceptTcpClientAsync -> NEW HandleCommandClientAsync task
  |             |                               |                        |
7 | 0xDD 0x01 ->| ProcessRegistrationPacketAsync -> Add(channel, newClient)
  |   (x N)     |   first channel to register swaps DeviceConnection.TcpClient
  |             |   every other channel hits ReferenceEquals(old, new) == true
  |             |     -> Connection = Connection  (re-arms its own offline flag)
  |             |   ChannelSlotKeys.Add(key) for each
  |             |                               |                        |
8 |             | all N channels show Connected again                    |
```

---

### S9. Cross-reference — call sequence to source

| Sequence | Entry method | File:line |
|---|---|---|
| S1 Registration | `RunCommandListenerAsync` → `HandleCommandClientAsync` → `ProcessRegistrationPacketAsync` | `ChannelManager.cs:579, 608, 701` |
| S2 Allow | `ShowRegisteredConfirm` → `AllowCicuitAsync` → `ChannelManager.Add` | `DeviceList.razor`, `DeviceChannelServices.cs:19`, `ChannelManager.cs:252` |
| S2b Rehydration | `InitializeAsync` | `ChannelCommandHandler.cs:97` |
| S3 Transfer (UI) | `TransferDialog` handler → `HWReadyToReadWriteAsync` / `SetBatteryParamAsync` / `TransferDbcFile` / `SetProgramAsync` | `TransferDialog.razor:370`, `ChannelCommandHandler.cs:593, 611, 1111, 643` |
| S4 Transfer (REST) | `SendProgram` → `CoreSendProgram` | `DeviceController.cs:41, 192` |
| S5 Start | `StartProgram` → `InsertSessionAsync` → `CreateSession` | `ChannelCommandHandler.cs:340`, `SqliteBulkDatabaseManager.cs:211` |
| S6 UDP store | `RunUdpStoreListenerAsync` → `StartUdpProcessorAsync` → `StoreUdpData` → `EnqueueForStore` → `StartStoreWorkerAsync` → `InsertRecordAsync` | `ChannelManager.cs:462, 482, 504`, `ChannelCommandHandler.cs:178, 204`, `SqliteBulkDatabaseManager.cs:81` |
| S7 Command | `SendAndWaitForResponseAsync` → `SendAndWaitAsync` ↔ `HandleIncomingPacket` | `ChannelCommandHandler.cs:300`, `DeviceConnection.cs`, `ChannelManager.cs:657` |
| S8 Disconnect | `HandleCommandClientAsync` finally block | `ChannelManager.cs:672-694` |

---


---

[⬅ Previous](12-entry-points.md) · [⬅ Index](README.md) · [Next ➡](14-calibration.md)
