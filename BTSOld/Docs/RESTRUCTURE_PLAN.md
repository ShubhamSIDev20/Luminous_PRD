# BTS Restructure Plan — Full System Architecture for 100+ Devices
> **Document**: Complete System Restructure Plan (v2 — Code-Verified)
> **Version**: 2.0
> **Date**: 2026-05-18
> **Author**: Maya | Deepak Chougale | Ador Powertron Ltd.
> **Status**: PLANNING — Review before implementation
> **Based on**: Full read of all UI pages, components, backend services, Program.cs, DI config

---

## 1. Executive Summary

The BTS system is a **monolithic Blazor Server application** where every hardware device creates:
- A permanent in-memory `CircuitCommandHandler` object (~5MB RAM each)
- 2 background Tasks (StoreWorker + ConnectionAlive)
- An unbounded Channel<T> queue (grows if processing falls behind)
- Direct event subscriptions into Blazor UI (every 10ms UDP fires StateHasChanged)

**Result**: 20+ devices → system slows. 40+ → UI freezes. 100+ → OOM.

**Goal**: Run 100+ devices on a simple Windows PC. Zero data loss. Same GUI. Same hardware protocol.

---

## 2. What Was Fully Read and Analyzed

### 2.1 Pages
| Page | Key Features |
|---|---|
| `/Dashboard` (DashboardView.razor) | Grid of DeviceCircuit cards, context menu per card (Start/Stop/Pause/Continue/Transfer/Calibration/Details), multi-select with state validation, filter by device/status, DBC panel, real-time status counters |
| `/settings/Discover` (DeviceDiscovery.razor) | SADP UDP broadcast, device list with IP/MAC config, Q1/Q4/Q5 commands via BroadcastUdpService |
| `/program/editor/{id}` (ProgramEditor.razor) | Full spreadsheet-style editor: StepRow, OperatorSelect, NominalValuesEditor, LimitActionPairEditor, RegistrationsEditor, keyboard nav (Alt+N, Ctrl+S, Ctrl+Delete, PgUp/Dn), PRODUCER program viewer dialog, JSON import/export, hex packet download |
| `/program/registration` (Registrations.razor) | Registration standards CRUD, unit selections |
| `/programs` (ProgramList.razor) | List + CRUD for test programs |
| `/batteries` (BatteriesList.razor) | Battery CRUD |
| `/batteries/dbc` (DbcDatabaseEditor.razor) | DBC CAN file parser & editor |
| `/programs/errormsg` (ErrorMsgConfig.razor) | Error/Message code configuration |
| `/programs/tablefiles` (TableFileManager.razor) | Table file upload/manage |
| `/scheduler` (SchedulerPage.razor) | Hangfire-backed scheduler: Schedules tab + Execution Logs tab, CRUD for schedule (Program + Battery + DBC + Circuits + CronExpression) |
| `/reports/sessions` (Reports.razor) | Session list DataTable, click → opens BmsDashboard tab, Excel + PDF export |
| `/settings/live-logs` (LiveLogs.razor) | Real-time Serilog log viewer, pause/resume, log-level filter, clear |
| `/settings/users` (Users.razor) | User management |
| `/settings/audit` (ApplicationErrorLogs.razor) | Audit log viewer |

### 2.2 Complex UI Components
| Component | Complexity |
|---|---|
| **DeviceCircuit.razor** (1755 lines) | 5 render modes (Mini/Compact/Normal/Chart/List), real-time data via `OnDataChanged` event subscription, double-click → opens BmsDashboard tab, single-click dialog (Overview/DigitalSignals/Manufacturing/Factory/Battery tabs), inline mini chart, IO status panel, ResetSystem command |
| **BmsDashboard.razor** | Chart (60%) + ProgramTable + DataTable (40%) layout, fullscreen toggle per panel, live mode via EventBus subscription, step-select filter, Excel export with chart PNG, Battery modal |
| **BmsChart.razor** | Highcharts/HighStock integration, live append, fullscreen |
| **BmsDataTable.razor** | Virtualized scrolling measurement data table |
| **BmsProgramTable.razor** | Step list with live step highlight, producer programs expansion, table file data |
| **TransferDialog.razor** | Program + Battery + DBC selection, sends to N circuits in parallel (device-locked) |
| **Calibration.razor** (1560 lines) | Current/Voltage/Temperature type toggle, Range 1-4, live calibration data panel, gain/offset entry, verify mode, read previous calibration |
| **TabViewer.razor** | Window-manager style tabs with keep-alive, slide animation, taskbar, dock positions |
| **DataTable.razor** | Generic reusable table: search, Excel export, PDF export, pagination, column slots |
| **DbcValuePanel.razor** | Docked/floating panel showing live DBC CAN signal values |
| **GridLayout.razor** | Responsive card grid for dashboard |
| **CircuitFilter.razor** | Multi-filter: by device, connection status, circuit status, program status |

### 2.3 Backend Services
| Service | Role |
|---|---|
| **CircuitManager.cs** | BackgroundService: TCP:9999 listener, UDP:10000 view listener, UDP:10001 store listener, single-threaded UDP store processor, device dictionary |
| **CircuitCommandHandler.cs** (1245 lines) | Per-circuit: TCP command send/receive, StoreWorker loop, ConnectionAlive loop, Start/Stop/Pause/Continue/SetProgram/SetBattery/Calibration commands, session management |
| **SqliteBulkDatabaseManager.cs** | Per-session SQLite .db: InsertRecord, InsertSession (Program/Battery/DBC/ProducerPrograms/TableFiles/ExpandedSteps), GetSession, ReadSessionData |
| **SchedulerService.cs** | Hangfire-backed: CRON schedules, auto-transfer (Program+Battery+DBC) + auto-start, execution log |
| **EventBusService.cs** | In-process pub/sub: BmsDashboard subscribes to session file path topic → receives live MeasurementData rows |
| **BroadcastUdpService.cs** | SADP: UDP:10002 broadcast send, UDP:10003 reply receive, device discovery/config |
| **DecoderService.cs** | Protocol: parse real-time packets, store packets, build command payloads, CRC-16 |
| **ServerSessionStorageService.cs** | Server-side per-user session state (CardConfig, ProgramEditor unsaved state) |
| **InMemoryLogStore.cs** | Ring-buffer for live log viewer |

### 2.4 DI Registration (ServiceCollectionExtensions.cs)
- `CircuitManager` → Singleton BackgroundService
- `EventBusService` → Singleton
- `CircuitCommandHandler` → Factory (new per circuit, not DI)
- All Repos, Services → Scoped
- `TabService` → per Blazor circuit (scoped)
- Hangfire SQLite → separate DB file
- AppDbContext → SQLite (main app DB)
- MCP server → HTTP stateless at `/mcp`

---

## 3. Root Cause Analysis — Why Scaling Fails

### Problem 1: UI directly coupled to UDP rate (10ms = 100/sec/device)
```
UDP packet arrives every 10ms
→ CircuitManager fires OnUdpViewDataReceived event
→ ViewUdpData() called
→ handler.RealTime.NotifyDataChanged(record) called
→ DeviceCircuit.OnRealTimeChanged fires
→ InvokeAsync(StateHasChanged) → Blazor re-renders card

At 100 devices: 100 × 100 pkt/sec = 10,000 StateHasChanged calls/sec
→ Blazor SignalR circuit floods → UI freezes
```
DeviceCircuit does throttle via `_lastUiUpdate` check (RefreshRate param), but event still fires and NotifyDataChanged still executes for every packet.

### Problem 2: Single-threaded UDP store processor
```
All circuits share ONE sequential processor loop
→ At N circuits × 100 pkt/s:
  10 circuits  = 1000 pkt/s → OK
  50 circuits  = 5000 pkt/s → Backlog builds
  100 circuits = 10000 pkt/s → Queue grows unbounded → OOM
```

### Problem 3: Per-circuit RAM × N
```
Each CircuitCommandHandler holds in RAM:
  - Program (with ProgramStepModel — can be large)
  - Battery
  - ExpandedProgramSteps (expanded PRODUCER chains)
  - CalibrationDto
  - Channel<recordStoreRequest> (unbounded queue)
  - TcpClient
  - RealTime (with chartData list)
  - Session state

Estimate: 3–8 MB per circuit depending on program complexity
100 circuits = 300–800 MB RAM for handlers alone
```

### Problem 4: Per-circuit background tasks
```
Each circuit spawns:
  - Task: ConnectionAlive() — polls every 200ms
  - Task: StartStoreWorkerAsync() — awaits Channel<T>

100 circuits = 200 background tasks
→ ThreadPool pressure → context switching overhead
```

### Problem 5: BmsDashboard live subscription
```
BmsDashboard subscribes to EventBusService topic (SQLFilePath)
EventBus.PublishAsync fires on every StoreUdpData batch
→ UI appends rows → InvokeAsync(StateHasChanged)
Multiple BmsDashboard tabs open = multiple subscribers all re-rendering
```

### Problem 6: Session creation blocks on many objects in RAM
```
StartProgram() builds in RAM:
  - ProducerPrograms (resolved from DB)
  - TableFileData (loaded from files)
  - ExpandedProgramSteps (built inline)
  - Serializes all to JSON → InsertSessionAsync
All of this happens synchronously in the command path
```

---

## 4. Proposed Architecture

### 4.1 Separation: Backend Process + Frontend

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  BTS BACKEND (BTS.Core — .NET Worker Service / Windows Service)             │
│                                                                             │
│  Owns all hardware I/O:                                                     │
│   TCP:9999  — device registration + commands                                │
│   UDP:10000 — live view data                                                │
│   UDP:10001 — store data                                                    │
│   UDP:10002 — SADP broadcast send                                           │
│   UDP:10003 — SADP reply receive                                            │
│                                                                             │
│  In-memory: lean DeviceStateStore (ConcurrentDictionary)                   │
│  Disk queue: raw .pkt files for store data                                  │
│  N configurable StoreWorker threads → SQLite per session                    │
│  LiveDataHub → pushes aggregated state every 500ms via SignalR              │
│  REST API for commands                                                      │
│                                                                             │
└─────────────────────┬───────────────────────────────────────────────────────┘
                      │ REST + SignalR (localhost)
┌─────────────────────▼───────────────────────────────────────────────────────┐
│  BTS FRONTEND (BTS.Web — Blazor Server)                                     │
│                                                                             │
│  All existing pages + components — SAME GUI                                 │
│  Dashboard: reads DeviceState DTOs via SignalR (not ICircuitCommandHandler) │
│  Commands: HTTP POST to backend REST API                                    │
│  BmsDashboard: loads SQLite directly OR via backend API                     │
│  Reports, Programs, Batteries, Scheduler, Settings: all REST                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Lean Device State Model (replaces ICircuitCommandHandler in UI)

Current: UI holds direct reference to `ICircuitCommandHandler` (5MB+ per device)

New: UI holds a lightweight DTO only:

```csharp
// This replaces ICircuitCommandHandler everywhere in the UI layer
public class DeviceStateDto
{
    public long DeviceId { get; set; }
    public long CircuitId { get; set; }
    public string DeviceName { get; set; }
    public bool IsConnected { get; set; }

    // Live data (same fields as RealTimeRecord — zero UI change)
    public RealTimeRecord RealTimeRecord { get; set; } = new();
    public List<IOStatusItem> IOStatus { get; set; } = new();

    // Session context (loaded on demand, not kept permanently in RAM)
    public string? SessionFilePath { get; set; }
    public string? SessionName { get; set; }
    public DateTime? SessionStart { get; set; }
    public DateTime? SessionEnd { get; set; }
    public int Storerecordcount { get; set; }
    public int Unstorerecordcount { get; set; }  // backlog

    // Loaded on demand (not in RAM permanently)
    public string? ProgramName { get; set; }
    public int? ProgramSteps { get; set; }
    public string? BatteryName { get; set; }

    // Status flags
    public CalibrationDto? Calibration { get; set; }
    public List<NotificationItem> Notifications { get; set; } = new();
}
```

**Impact on existing UI**:
- `DeviceCircuit.razor`: receives `DeviceStateDto` instead of `ICircuitCommandHandler`
- All `Circuit.Program.ProgramName` → `Circuit.ProgramName` (simple field swap)
- All `Circuit.Battery.Name` → `Circuit.BatteryName`
- All command calls: `Circuit.StartProgram()` → `await ApiClient.StartProgramAsync(deviceId, circuitId)`
- RealTime binding: `Circuit.RealTime.RealTimeRecord` → `Circuit.RealTimeRecord` (same nested object structure)
- This requires updating DeviceCircuit.razor parameters and DashboardView.razor — a one-time refactor, not a rewrite

### 4.3 Disk Queue Pattern for UDP Store Data

Replace unbounded in-memory Channel with disk-backed queue:

```
UDP Packet arrives on port 10001 (every 10ms per device)
          │
          ▼
[UDP Store Listener]  — just receives bytes, nothing else
          │
          ▼
 File.WriteAllBytes(
   $"{QueuePath}\\{epochMs}_{deviceId}_{circuitId}.pkt",
   rawBytes
 )
 → Disk write: ~0.1ms, non-blocking
 → Listener loop continues immediately
          │
          ▼ (background, N workers)
[StoreWorkerPool — N configurable threads]
   Worker picks oldest .pkt file
   → rename to .processing (atomic claim — prevents duplicate processing)
   → DecoderService.RealStoreData(bytes)  ← SAME decode logic, no change
   → SqliteBulkDatabaseManager.InsertRecordAsync(...)  ← SAME write logic
   → File.Delete(.processing file)
   → Loop to next file
```

**Why rename before process?**
- Atomic on NTFS — if two workers race, only one rename succeeds
- If server crashes mid-process → `.processing` files remain → on restart, workers retry them
- Zero data loss guaranteed even on power failure (NTFS is durable)

**Worker count from config:**
```json
"BTS": {
  "StoreWorkers": 4,
  "QueuePath": "D:\\BTSQueue",
  "QueueMaxAgeMinutes": 5
}
```

### 4.4 Live Data Decoupling (Fixes UI freeze)

```
UDP View Packet arrives (port 10000, every 10ms)
          │
          ▼
Update ConcurrentDictionary<Key, DeviceStateDto>.RealTimeRecord
(pure memory write — zero events fired, zero UI calls)
          │
          ▼  (separate timer, configurable)
[LiveDataBroadcastService — 500ms timer]
  Reads ALL DeviceStateDto from dictionary
  Pushes to SignalR Hub: IHubContext<BtsHub>
          │
          ▼
[BtsHub → Frontend clients]
Frontend Blazor subscribes to SignalR events
Updates local DeviceStateDto list
Calls StateHasChanged() on timer — NOT per UDP packet
```

**Result**:
- 100 devices × 500ms = 200 pushes/second total (manageable)
- UI re-renders at 2/sec per device (not 100/sec)
- Dashboard stays fluid at 100+ devices

### 4.5 BmsDashboard Live Mode — Via SignalR Not EventBus

Current: `EventBusService.Subscribe(SQLFilePath, ...)` — in-process, works only when Blazor + backend in same process

New: Backend publishes new measurement rows to SignalR group named after SessionFilePath
BmsDashboard joins group → receives live rows → appends to Data list
This works regardless of whether frontend and backend are same process or split

Keep EventBusService for same-process mode (Phase 1-2), migrate to SignalR in Phase 3-4.

### 4.6 Session Data — On Demand, Not Permanent RAM

Current: `CircuitCommandHandler` holds Program, Battery, ExpandedProgramSteps, CalibrationDto in RAM permanently.

New:
- `DeviceStateDto` holds only lightweight fields (ProgramName, BatteryName, SessionFilePath)
- When user opens Transfer Dialog, Detail Dialog, or Calibration → load full data from SQLite or REST API
- Session SQLite file already stores Program+Battery+DBC+ProducerPrograms+TableFiles → just load from there
- CalibrationDto stays in backend DeviceState (it's needed for live calibration workflow)

### 4.7 ConnectionAlive — Replace Per-Circuit Task with Single Monitor

Current: Each circuit has its own `ConnectionAlive()` task polling every 200ms.
100 circuits = 100 polling tasks.

New:
```
Single ConnectionMonitorService (BackgroundService)
  Every 200ms:
    foreach device in DeviceStateStore:
      if TcpClient.Client.Poll(0, SelectRead) && Available == 0:
        MarkDisconnected(device)
        emit DeviceDisconnected event → SignalR push
```
1 task monitors all devices. RAM and thread savings: 99 tasks eliminated.

---

## 5. Component-by-Component Impact Analysis

### 5.1 Dashboard (DashboardView.razor)
**Current**: `List<ICircuitCommandHandler> circuits` injected directly from `CM._devices.Values`

**Change needed**:
- Replace `List<ICircuitCommandHandler>` with `List<DeviceStateDto>`
- Subscribe to SignalR hub on init, receive pushed updates
- `CM.HardwareManagerChanged` event → replaced by SignalR `DeviceConnected/Disconnected` events
- All grid rendering: same Tailwind classes, same CardConfig, same filter logic
- **No visible UI change**

### 5.2 DeviceCircuit.razor
**Current**: `[Parameter] ICircuitCommandHandler Circuit`

**Change needed**:
- Parameter type: `DeviceStateDto Circuit` (same name, different type)
- `Circuit.RealTime.RealTimeRecord.X` → `Circuit.RealTimeRecord.X` (minor field path)
- `Circuit.Program.ProgramName` → `Circuit.ProgramName`
- `Circuit.Battery.Name` → `Circuit.BatteryName`
- `Circuit.IsConnected` → `Circuit.IsConnected` (same)
- `Circuit.Session.SessionFilePath` → `Circuit.SessionFilePath` (same)
- Remove event subscription `Circuit.RealTime.OnDataChanged` — UI updates come from SignalR push
- Command calls: `Circuit.StartProgram()` → `await Commands.StartAsync(Circuit.DeviceId, Circuit.CircuitId)`
- Detail dialog: load Program/Battery/Manufacturing/Factory on demand via API (already done in `LoadData()`)
- BmsDashboard double-click: pass `Circuit.SessionFilePath` + `Key` — same as now
- **Render modes (Mini/Compact/Normal/Chart/List)**: All render fragments use the same field names → just update the source type. No template rewrite.
- **5 render modes + IOStatus + chart**: All work as-is once parameter type changes

### 5.3 BmsDashboard.razor
**Current**: Injects `CircuitManager CM`, does `CM._devices.TryGetValue(Key, ...)` to get handler for live mode check.

**Change needed**:
- Remove CM injection
- Get live status from DeviceStateDto (passed as param or via API)
- EventBus subscription → SignalR group subscription (same data, different transport)
- All chart/table/program rendering: no change — still driven by `List<MeasurementData>` and `ProgramDTO`

### 5.4 TransferDialog.razor
**Current**: Takes `List<ICircuitCommandHandler> selectedCircuits`, calls `cr.SetBatteryParamAsync()`, `cr.SetProgramAsync()`, `cr.StartProgram()` directly.

**Change needed**:
- Takes `List<DeviceStateDto>` instead
- All operations → POST to REST API
  - `POST /api/circuits/{id}/battery`
  - `POST /api/circuits/{id}/program`
  - `POST /api/circuits/{id}/command/start`
- Progress tracking: API returns task ID → poll for completion or use SignalR events
- **UI layout (Program/Battery/DBC selection grid)**: No change

### 5.5 Calibration.razor
**Current**: Gets handler from `CM._devices`, calls `handler.HWReadyToCalibrationAsync()`, `handler.SetGainOffset()` etc. directly.

**Change needed**:
- Get `DeviceStateDto` (with CalibrationDto embedded)
- All calibration commands → REST API calls
- Live calibration data (calibrationBuffer) → pushed via SignalR group
- CalibrationDto state → maintained in backend, pushed to frontend
- **1560-line component**: UI templates unchanged — only data source and command path changes

### 5.6 SchedulerService (Hangfire)
**Current**: Takes `CircuitManager` as dependency, calls `_circuitManager.Get()` then handler commands.

**Change needed**:
- Inject `ICommandService` (abstraction over REST API or direct handler)
- In Phase 1-2 (same process): ICommandService wraps CircuitManager directly (no change)
- In Phase 3-4 (split process): ICommandService sends HTTP to backend
- **Scheduler logic, CRON, execution logs**: No change

### 5.7 DeviceDiscovery.razor / BroadcastUdpService
**No change** — SADP broadcast is already isolated in BroadcastUdpService. Works same in any architecture.

### 5.8 ProgramEditor, ProgramList, BatteriesList, Reports, Settings pages
**No change needed** — these work via Scoped services (IProgramServices, IBatteryServices etc.)
They have no direct CircuitManager or ICircuitCommandHandler dependency.
In Phase 4 (split), these services call backend REST API instead of direct DB — minimal change.

---

## 6. Migration Phases

### Phase 1 — Disk Queue (2 weeks) ← DO THIS FIRST
**Goal**: Eliminate RAM/CPU bottleneck for UDP store data. Immediate scaling win.
**Scope**: CircuitManager + CircuitCommandHandler (store path only)
**Hardware protocol**: Zero change
**UI**: Zero change

Steps:
1. Create `DiskQueueService` — writes raw UDP bytes to `{QueuePath}\{ts}_{dev}_{cir}.pkt`
2. In `RunUdpStoreListenerAsync` → call `DiskQueueService.Enqueue(rawBytes)` instead of `_udpChannel.Writer.WriteAsync`
3. Remove global `_udpChannel` + `StartUdpProcessorAsync` task
4. Add `StoreWorkerPool` — N workers from config, scan queue folder, process files
5. Remove per-circuit `_StoreQueue` Channel and `StartStoreWorkerAsync` task from `CircuitCommandHandler`
6. Keep `EnqueueForStore` as noop or reroute to disk queue
7. Add queue health endpoint: file count, oldest file age, failed files

**Result**: RAM = flat. CPU = controlled by worker count. Data loss = zero (disk survives crash).

---

### Phase 2 — Lean Device State (2 weeks)
**Goal**: Reduce per-circuit RAM from ~5MB to ~150KB
**Scope**: CircuitCommandHandler, DeviceStateStore (new class)
**UI**: Zero change

Steps:
1. Create `DeviceStateStore` — `ConcurrentDictionary<string, DeviceLiveState>` (backend only)
2. `DeviceLiveState` holds: TcpClient, RealTimeRecord (latest), SessionId, SessionFilePath, CalibrationDto, ConnectionStatus
3. Remove Program, Battery, ExpandedProgramSteps from handler RAM
4. `InitializeAsync()`: load only SessionId and SessionFilePath from DB (not full objects)
5. When command needs Program/Battery: load from SQLite session DB on demand
6. Replace ConnectionAlive per-circuit loop → `ConnectionMonitorService` (single background task)

**Result**: 100 circuits = ~15MB RAM for all handlers. Tasks: from 200 to ~N+4.

---

### Phase 3 — Live Data Decoupling (2 weeks)
**Goal**: UI refresh rate decoupled from UDP rate
**Scope**: CircuitManager (view path), new LiveDataBroadcastService, Blazor SignalR integration

Steps:
1. Add `BtsHub : Hub` (SignalR)
2. Add `LiveDataBroadcastService` — 500ms timer, reads DeviceStateStore, pushes DTOs to hub
3. `ViewUdpData` → only updates `DeviceStateStore[key].RealTimeRecord` (pure memory, no events)
4. Remove `OnUdpViewDataReceived` event and `OnDataChanged` event chain entirely
5. DashboardView.razor: inject HubConnection, subscribe to `DeviceStateChanged` events
6. DeviceCircuit.razor: remove event subscriptions, receive updates via parent re-render (already batched by SignalR push)
7. Add device-specific SignalR groups for BmsDashboard live mode

**Result**: UI: 2 renders/sec/device. Zero event-chain flooding.

---

### Phase 4 — Backend / Frontend Split (4 weeks, Optional)
**Goal**: True separation — backend is a Windows Service, frontend is a separate web app
**When**: After Phases 1-3. Not strictly required for 100 device target.

Steps:
1. Extract hardware I/O, DeviceStateStore, DiskQueueService, StoreWorkerPool into `BTS.Core` project
2. Add REST API layer in `BTS.Core` (already partially there — Controllers/DeviceController.cs exists)
3. Frontend `BTS.Web`: replace direct service injection with typed `HttpClient` wrappers
4. Create `IDeviceCommandClient` abstraction — implementable as direct or HTTP
5. Scheduler: use `IDeviceCommandClient` to decouple from CircuitManager
6. Session data reads: frontend reads SQLite directly (files are on shared path) OR via REST API

---

## 7. Zero Data Loss — Guarantee Table

| Failure Scenario | Protection Mechanism |
|---|---|
| Server crash while processing UDP store packet | .pkt file not yet deleted → worker retries on restart |
| Worker crash mid-decode | .processing rename → on restart, workers pick up .processing files first |
| SQLite write fails (disk full, lock) | Worker logs error, does NOT delete .pkt file, retries after delay |
| Power failure | .pkt files on NTFS survive (journaled FS). SQLite WAL mode = no partial-write corruption |
| Queue overflow (workers too slow) | Files accumulate on disk (visible, finite), no OOM. Alert shown in UI when oldest file age > threshold |
| Session started but program crash before end | SessionFilePath derived from epochSeconds in each .pkt → worker finds correct .db and writes records |
| Multiple workers claiming same file | File rename is atomic on NTFS → only one worker succeeds. Others skip that file |

---

## 8. Hardware Protocol — Unchanged

| Protocol Element | Status |
|---|---|
| TCP:9999 registration handshake (33-byte packet) | ✅ No change |
| Registration response (5 bytes) | ✅ No change |
| UDP:10001 store packet (0xCC start byte) | ✅ No change |
| UDP:10000 live packet (0xCC / 0xA0 start byte) | ✅ No change |
| TCP command frame (Start/Stop/Pause/Continue/SetProgram/SetBattery/Calibration) | ✅ No change |
| CRC-16/Modbus | ✅ No change |
| Port numbers (9999, 10000, 10001, 10002, 10003) | ✅ No change |
| Packet byte layout | ✅ No change |
| DecoderService logic | ✅ No change |

---

## 9. Capacity Projection After Restructure

| Metric | Current | After Phase 1+2 | After Phase 3 | After Phase 4 |
|---|---|---|---|---|
| Max devices before RAM issue | ~20 | **100+** | 100+ | 100+ |
| RAM per device | ~5 MB | ~150 KB | ~100 KB | ~50 KB |
| 100 devices total RAM | ~500 MB | ~15 MB | ~10 MB | ~5 MB |
| UDP store bottleneck | 1 thread | N workers (config) | N workers | N workers |
| Tasks per device | 2 | 0.04 (shared) | 0 | 0 |
| UI refresh rate (per device) | 100/sec | 100/sec | **2/sec** | 2/sec |
| UI freeze at 50+ devices | Yes | Reduced | **No** | No |
| Data loss on crash | ❌ (RAM queue) | ✅ disk | ✅ disk | ✅ disk |
| Scheduler works | Yes | Yes | Yes | Yes |
| Calibration works | Yes | Yes | Yes | Yes |
| BmsDashboard live mode | Yes | Yes | Yes | Yes |
| All existing UI | Yes | Yes | Yes | Yes |

---

## 10. New Configuration Reference

```json
{
  "AppSettings": {
    "Data": "D:\\BTSData"
  },
  "BTS": {
    "Queue": {
      "Path": "D:\\BTSQueue",
      "StoreWorkers": 4,
      "MaxAgeMinutes": 5,
      "RetryDelaySeconds": 30
    },
    "LiveData": {
      "PushIntervalMs": 500
    },
    "SQLite": {
      "WalMode": true
    }
  }
}
```

---

## 11. Queue Health UI Panel (New — Admin Only)

Add a small panel to Settings or Dashboard header:

| Field | Source |
|---|---|
| Queue depth (files) | Count .pkt files in queue folder |
| Oldest file age | DateTime.Now - oldest file CreationTime |
| Active workers | Worker thread count from config |
| Records stored today | Sum from SQLite across all session DBs |
| Failed files | Count .failed files in queue folder |
| Worker status | Running / Idle / Overloaded |

Alert if oldest file age > MaxAgeMinutes: "Queue backlog detected — consider increasing StoreWorkers."

---

## 12. Decision Points for Deepak

Before implementation starts, confirm:

1. **Phase priority** — Phases 1+2 alone give ~85% of the benefit. Start there?
2. **QueuePath location** — D: drive recommended (separate from app). Confirm path.
3. **Worker count** — How many CPU cores on the target PC?
4. **SQLite WAL mode** — Enable now? (Safer concurrent writes, very low risk)
5. **Frontend split (Phase 4)** — Required now or defer?
6. **SignalR** — Already wired in Program.cs? (AddServerSideBlazor is there, AddSignalR may need adding)
7. **Existing REST API** — DeviceController.cs exists. Should command routing go through it?

---

*Generated by Maya — Deepak's JARVIS 🤖 | BTS Full Code Analysis | Ador Powertron Ltd | 2026-05-18*

---

## 13. Redis � Optional Integration Layer

Redis slots into 3 independent points. Each is optional. System works without Redis on a single PC.

### Point 1 � Live Device State Store
Without Redis: ConcurrentDictionary in process RAM (default, zero deps).
With Redis: Write DeviceStateDto to Redis Hash on every UDP update. Enables multi-machine read.
Abstraction: IDeviceStateStore interface, swap impl in DI config only.
When: Only when backend + frontend on separate machines.

### Point 2 � SignalR Backplane
Without Redis: Single SignalR server, all clients on same process (covers 100+ devices fine).
With Redis: services.AddSignalR().AddStackExchangeRedis('localhost:6379') � one line.
NuGet: Microsoft.AspNetCore.SignalR.StackExchangeRedis
When: Only when 2+ frontend servers load-balanced.

### Point 3 � Store Queue (Redis Streams)
Without Redis: Disk .pkt files (Phase 1 default, always crash-safe).
With Redis Streams: XADD on UDP receive. Workers XREADGROUP/XACK after SQLite write. Un-ACKed messages auto-retry via XPENDING.
Risk: Redis MUST have AOF or RDB persistence ON. Without persistence: packets lost on Redis restart.
When: When disk I/O is bottleneck or cleaner queue management desired.

### Redis appsettings.json
{ BTS: { Redis: { Enabled: false, ConnectionString: 'localhost:6379', UseForStateStore: false, UseForSignalR: false, UseForStoreQueue: false } } }

### Decision Table
| Scenario | State Store | SignalR | Queue |
| Single PC 100 devices | No | No | No |
| 2+ frontend servers | Optional | YES | No |
| Backend+Frontend separate PCs | YES | YES | Optional |
| 200+ devices max throughput | Yes | Yes | YES |

Recommendation: Start with NO Redis. Phases 1-3 handle 100+ devices on one PC. Add Redis only if multi-machine needed.
