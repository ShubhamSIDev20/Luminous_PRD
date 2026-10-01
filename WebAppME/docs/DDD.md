# Detailed Design Document (DDD)
**Document ID:** BTS-DDD-001  **Version:** 0.6  **Date:** May 2026  **Status:** Approved

---

## 1. Component: ChannelManager

**File:** `Services/ChannelManager.cs`  
**Type:** `BackgroundService` (singleton, IHostedService)

### Responsibilities
- TCP registration listener on port 9999
- UDP real-time data listener on port 10000
- UDP session data listener on port 10001
- Maintaining `ConcurrentDictionary<string, IChannelCommandHandler>` of active handlers

### State Machine — Hardware Connection
```
[Offline] ──TCP Connect──▶ [Registering] ──Success──▶ [Online]
                                │                          │
                                └──Failed──▶ [Rejected]   │
                                                           │
                                        [Disconnect detected]
                                                           │
                                                     [Offline]
```

### Key Methods

| Method | Description |
|---|---|
| `StartAsync()` | Starts TCP listener + 2 UDP listeners as parallel tasks |
| `HandleRegistrationAsync()` | Parses 33-byte packet, validates DB, creates/updates handler |
| `HandleViewDataAsync()` | Receives UDP:10000, decodes via DecoderService, routes to handler |
| `HandleStoreDataAsync()` | Receives UDP:10001, enqueues to handler's Channel<T> |
| `GetHandler(deviceId, circuitId)` | Returns handler by composite key |

### Handler Key
`"{DeviceID}_{CircuitID}"` — string composite key in the dictionary.

---

## 2. Component: ChannelCommandHandler

**File:** `Services/Implementations/ChannelCommandHandler.cs`  
**Interface:** `IChannelCommandHandler`  
**Lifetime:** One instance per registered Device-Circuit pair

### State Fields

| Field | Type | Purpose |
|---|---|---|
| `_client` | TcpClient | Live TCP connection to hardware |
| `_stream` | NetworkStream | Write stream for commands |
| `_storeChannel` | Channel<recordStoreRequest> | Non-blocking session write queue |
| `RealTimeData` | RealTimeRecord | Latest decoded measurement snapshot |
| `_calibrationBuffer` | CalibrationData[3] | Last 3 calibration records |
| `_sessionId` | long | Unix epoch — current active session |
| `IsConnected` | bool | TCP health status |

### Command Flow
```
UI/Service calls handler.StartAsync(sessionId)
    │
    ├──▶ PacketAnalyzer.BuildStartPacket(0xEE, 0x01)
    ├──▶ _stream.WriteAsync(packet)
    └──▶ await ReadResponseAsync()
             │
             └──▶ validate CRC-16 + Status byte
```

### Channel<T> Write Pipeline
```
UDP:10001 received
    │
ChannelManager.HandleStoreDataAsync()
    │
handler._storeChannel.Writer.TryWrite(record)
    │
Background consumer task (per handler)
    │
SqliteBulkDatabaseManager.InsertBatchAsync()
    │
Session SQLite file
```

### IChannelCommandHandler Interface Methods

| Method | Protocol | Description |
|---|---|---|
| `IsReadyAsync()` | 0xAA Q1 | Check hardware ready |
| `ReadFactoryConfigAsync()` | 0xAA Q2 | Read network/hardware config |
| `ReadManufacturingConfigAsync()` | 0xAA Q3 | Read SW versions, serial numbers |
| `WriteBatteryParamsAsync()` | 0xAA Q5 | Write battery configuration |
| `StartAsync()` | 0xEE Q1 | Start program execution |
| `StopAsync()` | 0xEE Q2 | Stop program execution |
| `PauseAsync()` | 0xEE Q3 | Pause program |
| `ContinueAsync()` | 0xEE Q4 | Resume program |
| `SendProgramStepsAsync()` | 0xBB Q3/Q4 | Upload program steps |
| `SendDbcFileAsync()` | 0xBB Q7/Q8 | Upload DBC CAN file |
| `StartCalibrationAsync()` | 0xA0 Qn | Various calibration operations |
| `PreviousCalibration()` | 0xA0 Q14 | Read last calibration data |

---

## 3. Component: DecoderService

**File:** `Services/DecoderService.cs`  
**Type:** Stateless static decoder

### Decode Real-Time Packet (0xCC, min 77 bytes)
```
byte[0]  = 0xCC (start)
byte[1]  = DeviceID
byte[2]  = CircuitID
byte[3]  = QueryID
byte[4-5] = StepNumber (int16)
byte[6]  = ProgramStatus
byte[7]  = CircuitStatus
byte[8]  = ErrorId
byte[9-12] = SystemErrorId (int32)
byte[13-16] = StepRunningTime (int32 ms)
byte[17-20] = RunningTime (int32 ms)
byte[21-24] = Current (float A)
byte[25-28] = Voltage (float V)
byte[29-32] = Temperature (float °C)
byte[33-36] = Power (float W)
byte[37-40] = AccumulatedCapacity (float Ah)
byte[41-44] = ChargeCapacity (float Ah)
byte[45-48] = DischargeCapacity (float Ah)
byte[49-52] = StepCapacity (float Ah)
byte[53-56] = AccumulatedEnergy (float Wh)
byte[57-60] = ChargeEnergy (float Wh)
byte[61-64] = DischargeEnergy (float Wh)
byte[65-68] = StepEnergy (float Wh)
byte[69]   = Operator
byte[70-71] = CycleNumber (int16)
byte[72-73] = TableStepNumber (int16)
byte[74-76] = IO Status (3 bytes)
byte[77-78] = CRC-16
```

### Decode Session Store Packet (0xCC)
Opcode-tagged format — each field prefixed with 1-byte opcode:
- `0x01` = new row (ProgramRunningTime) — starts a new MeasurementData record
- `0x02–0x17` = measurement fields (see PROTOCOL.md)
- `0x1F–0xFA` = DBC CAN values (key + type-tagged value)

---

## 4. Component: BroadcastUdpService

**File:** `Services/BROADCAST/BroadcastUdpService.cs`  
**Type:** Service class, called on-demand from UI

### Discovery Flow
```
UI clicks "Scan" 
    │
BroadcastUdpService.DiscoverDevicesAsync()
    │
For each NetworkInterface (excluding loopback):
    │── Build Q1 broadcast packet
    │── UdpClient.Send(255.255.255.255:10002)
    │
UdpClient.ReceiveAsync(timeout) on port 10003
    │
Parse BroadcastDeviceInfo[] replies
    │
Return to UI for display
```

### Configuration Push Flow
```
Admin selects device → enters new IP/server config
    │
BroadcastUdpService.SetDeviceIpAsync(uniqueId, config)     // Q4
BroadcastUdpService.SetServerConfigAsync(uniqueId, config)  // Q5
    │
Hardware applies config + reconnects via TCP:9999
```

---

## 5. Component: SqliteBulkDatabaseManager

**File:** `Services/Implementations/SqliteBulkDatabaseManager.cs`

### Design
- One instance per active session
- `Channel<List<MeasurementData>> _writeChannel` — bounded channel (capacity: 500 batches)
- Background `Task _writerTask` — dequeues and bulk-inserts using raw SQL
- `SqliteSchemaSync` — called on session open to ensure schema matches current DBC signal set

### Batch Insert Strategy
```
UDP packets → decode → List<MeasurementData>
    │
_writeChannel.Writer.WriteAsync(batch)
    │
[Background writer]
    │
Using SqliteConnection
    │── BEGIN TRANSACTION
    │── INSERT INTO MeasurementData (...) VALUES (...)  × batch.Count
    └── COMMIT
```

---

## 6. Component: ServerSessionStorageService

**File:** `Services/ServerSessionStorageService.cs`

### Design
- `ConcurrentDictionary<userId, ConcurrentDictionary<storeId, SessionItem>>`
- Items can be `Permanent=true` → serialized to `ConfigurationEntity` DB table
- Background cleanup loop removes expired non-permanent items (TTL: 1 hour)
- Used by DataTable for: font size, page size, column order, column visibility, group expand state

### Storage Key Convention
`"{UserName}_{ComponentTitle}_DataTable"` — per user, per table instance

---

## 7. Component: DataTable (Generic UI Component)

**File:** `Components/UI/Datatable/DataTable.razor`  
**Type:** Generic Blazor component `DataTable<TItem>`

### Features
- Client-side search, pagination (25/50/100/200 rows)
- Column visibility toggle with drag-reorder
- Font size slider
- Excel export (OpenXml)
- `GroupBy` parameter — optional collapsible grouped rows
- All view state persisted via `ServerSessionStorageService`

### GroupBy Architecture
```
GroupDefinition<TItem, TKey>
    implements IGroupRenderer<TItem>
        GetGroupKey(row) → string hash
        RenderGroupHeader(ctx) → RenderFragment
        DefaultExpanded → bool

DataTable<TItem>.BuildGroupedBody()
    Groups _rows by GetGroupKey()
    For each group:
        ├── RenderGroupHeader(ctx) → custom header <tr>
        └── Standard column pipeline <tr> for each child row
```

---

## 8. Middleware & Cross-Cutting Concerns

### Logging (Serilog)
- Console sink (development)
- Rolling file sink: `logs/btsservice-{date}.log` — 7-day retention
- `InMemorySink` — accessible in UI via `InMemoryLogStore`
- Log level: controlled by `LevelSwitch` (runtime adjustable)

### Exception Handling
- `ExceptionHandlerMiddleware` — catches unhandled exceptions, returns structured error response
- Blazor circuit disconnect handled by `MyCircuitHandler`

### Audit Logging
- `AuditService` — called from service layer for create/update/delete operations
- Stores: Action, EntityType, EntityId, OldValue, NewValue, UserId, Timestamp

### Database Column Encryption
- `EncryptionAttribute` marks entity properties for encryption
- `IColumnEncryptionService` — AES-based encrypt/decrypt
- `ModelBuilderEncryptionExtensions` — applies value converters via EF Core model builder

---

## 9. Component: ProgramBuilder (PRODUCER Expansion)

**File:** `Services/ProgramBuilder.cs`  
**Type:** Static helper class

### PRODUCER Step Expansion

The `ExpandProducerSteps()` static method is called before hardware transfer to inline referenced sub-programs:

```
Input: outer program steps (may contain PRODUCER steps)
       + Dictionary<name, List<StepModel>> resolvedPrograms

For each step in outer program:
    if step.OperatorCode == PRODUCER:
        look up name = step.NominalValues[0] in resolvedPrograms
        if found and innerSteps.Count > 2:
            take innerSteps.Skip(1).Take(Count - 2)   // drop SET + STO
            deep-clone each inner step (new GUID, same fields)
            append [programName] prefix to Comment
            add cloned steps to result list
        // else: silently skip (validation catches missing programs)
    else:
        pass step through unchanged

Re-number all result steps from 1
Return expanded list
```

### Key Design Decisions
| Decision | Rationale |
|---|---|
| Skip first (SET) + last (STO) | Outer program already has SET/STO boundary steps |
| New GUIDs for cloned steps | Avoids ID collision with original step records |
| Comment annotation `[programName]` | Traceability — operator can see which steps came from which sub-program |
| Single-level expansion only | Nested PRODUCER (sub-program using another PRODUCER) is not expanded; if needed, apply recursively |
| `innerSteps.Count > 2` guard | Programs with only SET + STO contribute zero inner steps |

---

## 10. Component: PRODUCER — Session Self-Containment

**Files:**  
- `Services/Implementations/ChannelCommandHandler.cs` — data collection  
- `Services/Implementations/SqliteBulkDatabaseManager.cs` — serialisation / retrieval  
- `Models/DTOs/RequestDTOs.cs` — DTO extension

### Data Collection at Session Start (`StartProgram`)

```
ChannelCommandHandler.StartProgram()
    │
    ├── ResolveProducerProgramsAsync(steps)
    │       for each PRODUCER step:
    │           call IProgramServices.GetProgramByNameAsync(name)
    │           collect ProgramDTO → List<ProgramDTO> producerPrograms
    │
    ├── CollectTableFileData(steps)
    │       for each TABLE step:
    │           call FileManagerService.ReadFileLines(fileName)
    │           collect lines → Dictionary<string, string[]> tableFileData
    │
    └── InsertSessionAsync(new SessionRequest {
            ...existing fields...
            ProducerPrograms = producerPrograms,
            TableFileData    = tableFileData
        })
```

### Serialisation into Session SQLite (`configurationEntities` table)

| Key Pattern | Value | Source |
|---|---|---|
| `ProducerProgram_{name}` | `JsonConvert.SerializeObject(ProgramDTO)` | PRODUCER step reference |
| `TableFile_{fileName}` | `JsonConvert.SerializeObject(string[])` | TABLE step reference |

All keys are written as `ConfigurationEntity` rows in the session-specific SQLite file alongside the existing `Program_`, `Battery_`, and `DBC_` keys.

### Retrieval at Offline Playback (`GetSessionAsync`)

```
GetSessionAsync(filePath)
    │
    ├── FetchProducerProgramsAsync(ctx)
    │       WHERE Key LIKE 'ProducerProgram_%'
    │       Deserialize each Value → ProgramDTO
    │       return List<ProgramDTO>
    │
    ├── FetchTableFileDataAsync(ctx)
    │       WHERE Key LIKE 'TableFile_%'
    │       extract fileName = key["TableFile_".Length..]
    │       Deserialize Value → string[]
    │       return Dictionary<string, string[]>
    │
    └── return SessionRequest {
            ProducerPrograms = ...,
            TableFileData    = ...
        }
```

---

## 11. Component: BmsDashboard / BmsProgramTable (Offline Viewer)

**Files:**  
- `Components/UI/DataViewer/BmsDashboard.razor`  
- `Components/UI/DataViewer/BmsProgramTable.razor`

### Data Flow (Offline Mode)

```
BmsDashboard.LoadAsync()
    │
    GetSessionAsync(SQLFilePath)
    │
    SessionRequest {
        Program              → Pg (steps for program table)
        Battery              → Battery (battery modal)
        ProducerPrograms     → _sessionProducerPrograms   ← NEW
        TableFileData        → _sessionTableFileData       ← NEW
    }
    │
    BmsProgramTable(
        Steps="@Steps"
        ProducerPrograms="@_sessionProducerPrograms"      ← NEW
        TableFileData="@_sessionTableFileData"             ← NEW
    )
```

### PRODUCER / TABLE Cell Rendering (BmsProgramTable)

In the Nominal Values column, step operator code is checked:

| Operator | Rendering |
|---|---|
| `PRODUCER` | Program name tag + 👁 eye button (if program found in `ProducerPrograms`) → opens sub-program steps modal |
| `TABLE` | Filename tag + 📄 button (if file found in `TableFileData`) → opens file content modal |
| All others | Standard nominal value tags |

Both modals are rendered inline within `BmsProgramTable.razor` using fixed-overlay `div` elements styled consistently with the rest of the BMS card theme.

---

## 12. Component: Reports — Session Properties Panel

**File:** `Components/Pages/Reports/Reports.razor`

### Properties Panel Flow

```
User clicks ℹ button on session row
    │
OpenSessionPanel(session)
    │
    ├── _showSessionPanel = true  (renders modal overlay)
    ├── _sessionPropsLoading = true
    │
    ├── dbManager.GetSessionAsync(session.SessionFilePath)
    │       → SessionRequest { ProducerPrograms, TableFileData }
    │
    ├── _sessionProducerPrograms = result.ProducerPrograms ?? []
    ├── _sessionTableFileData    = result.TableFileData ?? {}
    └── _sessionPropsLoading = false → panel renders data

Panel shows:
    ┌─────────────────────────────────────┐
    │ Session Properties — {SessionName}  │
    ├─────────────────────────────────────│
    │ Program    │ Battery                │
    │ Dev/Cir    │ Start / End / Duration │
    ├── PRODUCER Sub-programs ────────────│
    │  • SubProgramA — 12 steps           │
    │  • SubProgramB — 8 steps            │
    ├── TABLE Files ──────────────────────│
    │  • table1.csv — 200 lines           │
    └─────────────────────────────────────┘
    [Open Full Session ▶]
```

"Open Full Session" closes the panel and opens BmsDashboard in a new tab via `TabService`.
