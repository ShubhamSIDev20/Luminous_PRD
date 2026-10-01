# 7. UDP 10001 → Session-Wise Storage

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

This is the high-volume path. Every running channel streams measurement packets
at high rate; the design goal is *never block the socket*.

### 7.1 The pipeline

```
  HARDWARE (running channel)
        |
        |  UDP datagram to <server>:10001
        |  Storage Protocol V2:
        |  [0xCC][DevID][AddrByte][QueryID]
        |  [SessionID 4B BE][Step 2B][Op][CircuitStatus]
        |  [PresenceBitmask 2B][ ...packed rows... ]
        v
  +--------------------------------------------------------------+
  | STAGE 1 -- RunUdpStoreListenerAsync   (socket loop)           |
  |                                                               |
  |   _udpStoreDiscovery = new UdpClient(10001)                   |
  |   while (!cancelled)                                          |
  |       result = await ReceiveAsync()                           |
  |       _udpChannel.Writer.WriteAsync(result)   // NON-BLOCKING |
  |                                                               |
  |   This loop does ZERO parsing and ZERO I/O. Its only job is   |
  |   to drain the OS socket buffer as fast as possible so no     |
  |   datagram is dropped while the CPU is busy decoding.         |
  +--------------------------------+------------------------------+
                                   |
                        System.Threading.Channels
                        Channel<UdpReceiveResult>
                        (in-memory hand-off queue)
                                   |
                                   v
  +--------------------------------------------------------------+
  | STAGE 2 -- StartUdpProcessorAsync   (single consumer)         |
  |                                                               |
  |   await foreach (packet in _udpChannel.Reader.ReadAllAsync()) |
  |       try { await StoreUdpData(packet, token); }              |
  |       catch { log and CONTINUE }   // one bad packet never    |
  |                                    // kills the pipeline      |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STAGE 3 -- StoreUdpData(result, token)                        |
  |                                                               |
  |  3a. decoded = DecoderService.RealStoreDataV2(payload)        |
  |         |                                                     |
  |         +-- FAIL --> TrySendFailureNotification(payload, msg) |
  |         |             -> decode Dev/Board/Ch from payload[0..1]|
  |         |             -> handler.SendNotification(...)         |
  |         |             -> toast/bell in the operator's UI       |
  |         |             -> RETURN (packet dropped)               |
  |         |                                                     |
  |  3b. first = decoded.Data.RealStoreRecord.FirstOrDefault()    |
  |         +-- null --> log, RETURN                              |
  |                                                               |
  |  3c. handler = Get(DeviceID, Board, ChannelNumber)            |
  |         +-- not found --> log, RETURN                         |
  |             (data for a channel nobody registered is discarded)|
  |                                                               |
  |  3d. RESOLVE THE DATE FROM THE SESSION ID                     |
  |      DecoderService.SessionIdToDateTime(first.SessionID,      |
  |                              out sessionDateTime, true)       |
  |        - takes now's high 16 epoch bits                       |
  |        - ORs in the stored low 16 bits                        |
  |        - corrects +/- 65536 s if the result lands > ~18 h off |
  |        - on failure: fall back to DateTime.Now (logged)       |
  |                                                               |
  |  3e. BUILD THE FILE PATH                                      |
  |      filePath = "<dd-MM-yyyy>/<SessionID>_<Dev>_<Board>_<Ch>.db"|
  |                                                               |
  |  3f. handler.EnqueueForStore(decoded.Data, handler.dbcData?.DbcValues)|
  |                                                               |
  |  3g. _eventBus.PublishAsync<List<MeasurementData>>(            |
  |          filePath, records)                                   |
  |      -> any open chart/grid subscribed to this session file   |
  |         updates live without touching the disk                |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STAGE 4 -- EnqueueForStore  (per-channel, in ChannelCommandHandler)|
  |                                                               |
  |  DBC de-duplication:                                          |
  |    if (dbcRecord != null && records > 0                       |
  |        && dbcData.CreatedDate > _lastStoredDbcDate)           |
  |         record[0].dbcValues = JSON(dbcRecord)   // write once  |
  |         record[1..n].dbcValues = null                          |
  |         _lastStoredDbcDate = dbcData.CreatedDate               |
  |    else                                                       |
  |         all records .dbcValues = null                          |
  |                                                               |
  |    -> DBC values arrive on UDP 10000 at their own rate. This  |
  |       attaches the newest DBC snapshot to exactly one row per |
  |       arrival instead of duplicating it onto every row.       |
  |                                                               |
  |  _StoreQueue.Writer.TryWrite(dto)      // per-CHANNEL queue    |
  |  Session.Unstorerecordcount += count   // backlog counter -> UI|
  +--------------------------------+------------------------------+
                                   |
                    one unbounded Channel<T> PER CHANNEL SLOT
                    (a slow disk on one channel cannot stall
                     another channel's writes)
                                   |
                                   v
  +--------------------------------------------------------------+
  | STAGE 5 -- StartStoreWorkerAsync  (one loop per channel slot) |
  |            started from handler.InitializeAsync()             |
  |                                                               |
  |  await foreach (item in _StoreQueue.Reader.ReadAllAsync(cts)) |
  |                                                               |
  |    5a. SYSTEM ERROR CHECK                                     |
  |        first record with SystemErrorID in 1..17               |
  |        AND ProgramRunningTime != 0                            |
  |          -> OnNotify(NotificationItem "<D-B-C> System Error:  |
  |                       <SystemError enum name>")               |
  |                                                               |
  |    5b. await ISqliteBulkDatabaseManager                       |
  |               .InsertRecordAsync(item)                        |
  |          -> writes into the SESSION .db (see 7.2)             |
  |                                                               |
  |    5c. END-OF-TEST DETECTION                                  |
  |        any record with Operator == OperatorConstants.STO ?    |
  |          -> Session.EndTime = DateTime.Now                    |
  |          -> IProgramServices.EndSession(Session)              |
  |          -> log "END Session"                                 |
  |        (the hardware ends the test itself; the server         |
  |         learns about it from the data stream, not a command)  |
  |                                                               |
  |    5d. COUNTERS                                               |
  |        Unstorerecordcount -= count   (floored at 0)           |
  |        Storerecordcount   += count                            |
  |                                                               |
  |    catch -> log "SQLite store error", CONTINUE the loop       |
  +--------------------------------------------------------------+
```

### 7.2 What `InsertRecordAsync` does inside the session file

```
  InsertRecordAsync(recordDto)
        |
        +--> ctx = GetContext(recordDto.filePath)
        |       -> BuildPath: <Data>/sessions/<filePath>
        |       -> Directory.CreateDirectory(...) if needed
        |       -> EnsureCreated()
        |       -> SqliteSchemaSync.SyncModelWithDatabase(ctx)
        |          (adds columns added by newer app versions to
        |           older session files -- forward compatibility)
        |
        +--> DBC VALUE RE-KEYING
        |      dbcdata = FetchDbcDatabaseAsync(ctx)
        |         (read from THIS session's own stored config,
        |          not from the handler's transient memory)
        |      orderedSignalIds = selected signals, SingalId != 0,
        |                         distinct, ordered ascending
        |      for each record with dbcValues:
        |         parse signal-ID-keyed dict
        |         -> re-serialize as an INDEX-ALIGNED string array
        |            matching DbcSignalNames order
        |      (compact storage: no repeated key names per row)
        |
        +--> REG BIFURCATION
               a row whose program step carries a Nominal Value label
                 -> RegLogs table, grouped case-insensitively by label
               everything else
                 -> Measurements table
```

### 7.3 On-disk layout

```
  <GlobalConfig.AppSettings.Data>/          e.g. D:\MEWebApp\
    |
    +-- sessions/
          |
          +-- 17-08-2026/
          |     +-- 16909234_1_0_1.db      <- SessionID_Device_Board_Channel
          |     +-- 16909235_1_0_2.db
          |     +-- 16909241_2_1_7.db
          |
          +-- 18-08-2026/
                +-- 16995612_1_0_1.db
                +-- ...

  (folder = the date reconstructed from the packed SessionID,
   NOT the date the file happened to be written)
```

### 7.4 Tables inside one session `.db`

```
  +---------------------------------------------------------------+
  |  <SessionID>_<Dev>_<Board>_<Ch>.db      (SqliteDbContext)      |
  |                                                                |
  |  Measurements         -- the time-series test data             |
  |                          (one row per completed opcode cycle)  |
  |  RegLogs              -- REG-operator rows split out by label  |
  |  Program              -- BtsPrograms snapshot                  |
  |  Battery              -- Batteries snapshot                    |
  |  configurationEntities-- key/value JSON blobs written at start:|
  |                            "Program"                           |
  |                            "Battery"                           |
  |                            "Dbc"     (merged 3-port map)       |
  |                            producer programs                   |
  |                            table file data                     |
  |                            expanded program steps              |
  +---------------------------------------------------------------+
```

### 7.5 Why this shape

| Choice | Reason |
|---|---|
| Socket loop does no work | A blocking decode on the receive loop drops datagrams; UDP has no retransmit |
| One shared decode queue | Decoding is CPU-bound and ordered; one consumer avoids lock contention |
| One write queue **per channel** | Disk latency on one session cannot stall 63 other channels |
| One `.db` **per session** | Files are independently portable, exportable, and deletable; no single giant DB to lock or vacuum |
| Session config copied into the file | Reports remain reproducible even if the program/battery/DBC rows in the main DB are later edited or deleted |
| Date folder from SessionID | Groups a test under the date it *started*, even if packets arrive after midnight |

**Files involved:** `Services/ChannelManager.cs:462-575`,
`Services/Implementations/ChannelCommandHandler.cs:178-286`,
`Services/Implementations/SqliteBulkDatabaseManager.cs:20-358`,
`Data/SqliteDbContext.cs`, `Services/EventBusService.cs`,
`docs/PROTOCOL.md` §12

---


---

[⬅ Previous](06-start-program-and-session.md) · [⬅ Index](README.md) · [Next ➡](08-udp-10000-live-view.md)
