# CODEBASE_MAP.md
> Updated: 2026-09-08T10:00:00Z

---

## New files (session #41, 2026-09-04)
- `Utils/BatteryUnitResolver.cs` — resolves battery-relative program units (`ACN`/`ACN1`/`ACN2`/`ACN4`/`ACN5`/`ACN10`/`ACN20`, `VNC`) to plain A/V using a `BatteryDTO`; called from `ProgramBuilder.ProcessNominalValues`/`ProcessStandardLimit`/`TryParseRegistration`. Extended session #42 (2026-09-08) with `InternVariableNames`/`GetBatteryGlobalVariables` for the §12.3 bare battery-parameter tokens (`CNom`/`INom`/`UGas`/etc.) — see `sessions/2026-09-04_1200_vnc-acn5-program-editor.md` and `sessions/2026-09-08_1000_battery-parameters-intern-table.md`.
- `docs/manual-extract/BM_Manual_eng.txt` / `VNC-ACN-battery-parameters.md` — searchable extraction of `docs/BM_Manual_eng.pdf` and a curated formula reference; re-grep these before ever re-parsing the PDF.
- `docs/VNC-ACN5-Implementation-Plan.md` — the approved implementation plan for VNC/ACN5.
- `docs/Battery-Parameters-Intern-Table-Plan.md` (session #42) — the approved implementation plan for the §12.3 `INTERN[]` bare-token feature; also records why Ramp/Resistance are genuinely blocked by `docs/PROTOCOL.md`'s wire format, not just deferred.

---

## Annotated File Tree

```
WebAppME/ (BatteryTestingSystem)
├── Program.cs                     # Entry point — Serilog, Blazor Server, Hangfire, MCP, CORS, Swagger, health checks, DB migration/seed
├── appsettings.json                # DB connection, JwtSettings, CORS, AppSettings (DB encryption/password — secret-shaped, do not expose)
├── Controllers/
│   ├── AuthController.cs          # POST /api/auth/login — Identity sign-in + JWT issuance (AllowAnonymous)
│   ├── DeviceController.cs        # Legacy hardware/session REST API — SendProgram/Start/Stop/Pause/Continue/GetLiveData; Core* helpers reused by MCP
│   └── ExportController.cs        # /api/export — Hangfire-backed async export request/status/list/download/delete ([Authorize])
├── Services/
│   ├── ChannelManager.cs          # Orchestrates per-device DeviceConnection; demultiplexing read loop; device/channel registry (_devices); SuperviseAsync keeps TCP 9999 / UDP 10000+10001 bound (ADR-4)
│   ├── ListenerHealthCheck.cs     # IHealthCheck ("hardware-listeners") reporting ChannelManager.ListenerStates — Unhealthy if any port is unbound
│   ├── DeviceConnection.cs        # Per-device TCP connection; write serialization (SemaphoreSlim); response correlation by address byte
│   ├── DecoderService.cs          # Packet decoding logic (modified — check git diff before assuming stable)
│   └── Implementations/
│       ├── ChannelCommandHandler.cs   # Channel-slot; delegates I/O to DeviceConnection; retains session/program state
│       ├── BatteryServices.cs
│       ├── ProgramServices.cs
│       ├── DeviceChannelServices.cs
│       ├── DbcService.cs
│       ├── SchedulerService.cs
│       ├── ExportJobService.cs        # Hangfire job — generates Excel exports (ClosedXML/OpenXML)
│       ├── AuditService.cs
│       ├── CodeMessageService.cs
│       ├── ConfigStorageService.cs
│       ├── ServiceLocator.cs          # Static service locator used during startup migration/seeding
│       ├── SqliteBulkDatabaseManager.cs
│       └── UserCircuitAccessService.cs
│   └── Interfaces/
│       ├── IChannelCommandHandler.cs
│       ├── ISchedulerService.cs
│       ├── IServices.cs
│       ├── IAlarmService.cs          # AlarmRequest, AlarmChangeKind, AlarmChanged record, contract — ADDED 2026-08-18 (session #18)
│       └── ISqliteBulkDatabaseManager.cs
├── Services/Alarms/                 # ADDED 2026-08-18 (session #18)
│   ├── AlarmService.cs               # Singleton: collapse/re-arm/persist/policy/fan-out. Own event, NOT EventBusService (ADR-5)
│   ├── AlarmPolicy.cs                # Pure cooldown/storm-guard/mute logic — no I/O, fully unit tested
│   ├── AlarmOptions.cs               # Bound from appsettings.json "Alarms" section
│   └── AlarmRetentionService.cs      # BackgroundService — daily prune, never deletes an unresolved alarm
├── Repositories/
│   ├── Interfaces/
│   │   ├── IRepository.cs              # NOTE: namespace typo documented in clean-arch design doc — verify before relying on it
│   │   ├── IExportRepository.cs
│   │   ├── ISchedulerRepository.cs
│   │   ├── IAlarmRepository.cs         # ADDED 2026-08-18 (session #18) — active/collapse/acknowledge/prune queries
│   │   └── ISpecificRepositories.cs
│   └── Implementations/
│       ├── Repository.cs               # Generic base repository
│       ├── AuditRepository.cs
│       ├── AlarmRepository.cs          # ADDED 2026-08-18 (session #18)
│       ├── BatteryRepository.cs
│       ├── CodeMessageRepository.cs
│       ├── ConfigStorageRepository.cs
│       ├── DbcRepository.cs
│       ├── DeviceChannelRepository.cs
│       ├── ExportRepository.cs
│       ├── ProgramRepository.cs
│       ├── SchedulerRepository.cs
│       └── UserCircuitAccessRepository.cs
├── Data/
│   └── AppDbContext.cs             # IdentityDbContext<ApplicationUser, ApplicationRole, string> — all DbSets, OnModelCreating
├── Migrations/                     # EF Core migrations — never edit after applied
├── Models/
│   ├── Entities/                   # Device, Channel, SecondaryBoard, BatterySession, Batteries, BtsPrograms, ExportRecord, ApplicationUser/Role, AuditLog, AlarmLog (ADDED 2026-08-18, session #18 — Audit.AlarmLog table), CalibrationDataPoint, DbcFileRecord, ProgramSchedule, ScheduleExecutionLog, UserCircuitAccess, RegistrationStandard, BatteryType, ConfigurationEntity, CodeMessage
│   ├── Dtos/                       # ~22 DTO files across subfolders — request/response shapes for services/controllers
│   └── InitializeDataSeeder.cs     # Startup seed logic (currently modified in working tree)
├── DbSecurity/                     # Column-level encryption: IColumnEncryptionService(WithSalt), EncryptionAttribute, ModelBuilderEncryptionExtensions
├── MCP/
│   └── DeviceMcpTools.cs           # Exposes device ops as MCP tools; reuses DeviceController.Core* static helpers
├── Middleware/
│   ├── ExceptionHandlerMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # AddBtsServiceDependencies — DI wiring for services/repositories
├── Components/
│   ├── Layout/                     # Blazor layout shells
│   ├── UI/                         # Shared Blazor UI components
│   └── Pages/                      # Route pages: Devices/, Batteries/, Programs/, Reports/, Settings/, Home/, Test/
├── wwwroot/                        # Static assets
├── docs/
│   ├── PROTOCOL.md                  # Byte-level wire protocol spec (framing, CRC-16, every packet layout)
│   ├── flowDocs/00-SYSTEM-FLOW.md   # ASCII block-diagram doc: registration → allow → transfer → start → UDP 10001 session-wise storage → control commands. Every diagram cites its source file/line
│   ├── architecture/02-backend-clean-architecture-design.md   # Future clean-arch split — DESIGNED, NOT IMPLEMENTED
│   ├── deployment/                  # Customer-site installation guide (IIS/Docker, config, DB backup/restore)
│   └── superpowers/specs/2026-08-07-device-connection-multiplexing-design.md  # Multiplexing design — IMPLEMENTED (commits c86426c..2fbaf43)
├── HardwareSimulator/               # Python load-test harness — NOT part of the .NET app; simulates hardware for end-to-end testing
│   ├── README.md                     # ADDED 2026-08-18 (session #16) — how to operate it: CLI flags, per-key config reference, TCP/UDP lifecycle, Python↔C# mirror table, troubleshooting. START HERE.
│   ├── simulator.py                 # One TCP connection per simulated DEVICE, shared across all its channels (mirrors DeviceConnection); board/channel addressing via address_byte
│   ├── program_decoder.py           # ChunkReassembler + decode_program_steps — mirrors ProgramBuilder.cs's 0xAA55/offset/0x55AA byte format
│   ├── dbc_decoder.py                # decode_dbc_signals — mirrors DbcDatabase.BuildMultiPortPayload
│   ├── core_engine.py                # CoreEngine — step-execution state machine driving dummy telemetry from real program steps
│   ├── run_sim.py                    # Thin launcher — prints a banner, shells out to simulator.py with the same argv
│   ├── Send-RealtimeUdp.ps1          # Debug tool — hand-builds and sends ONE 0xCC packet to UDP 10000, no simulator/CoreEngine involved
│   ├── tests/                        # pytest suite (17 tests) — test_program_decoder.py, test_dbc_decoder.py, test_core_engine.py
│   ├── config.json                  # channels_per_device (was circuit_count_per_device), device_count, server ports. Dead keys stripped 2026-08-18 — see gotcha below.
│   └── run_sim.py                   # Launcher — `python run_sim.py [-d N] [-i N] [-H host]`
└── BatteryTestingSystem.Tests/     # xUnit tests: ChannelManagerMultiplexingTests, DeviceConnectionTests, ChannelAddressCodecTests
```

---

## Key Functions / Classes

| Name | File | What It Does |
|------|------|-------------|
| `ChannelManager.GetOrCreateDeviceConnection` | `Services/ChannelManager.cs` | Returns/creates the shared `DeviceConnection` for a device — entry point for multiplexed I/O |
| `ChannelManager.HandleCommandClientAsync` | `Services/ChannelManager.cs` | Persistent per-device read loop; demultiplexes incoming packets to pending requests; read buffer is 1024 bytes (fixed 2026-08-11 — see Gotchas) |
| `DeviceConnection.SendAndWaitAsync` | `Services/DeviceConnection.cs` | Serializes a write via `SemaphoreSlim`, registers a `TaskCompletionSource` keyed by channel address byte, awaits response |
| `DeviceConnection.HandleIncomingPacket` | `Services/DeviceConnection.cs` | Resolves the pending `TaskCompletionSource` for an address byte and completes it |
| `DeviceConnection.FailAllPending` | `Services/DeviceConnection.cs` | On disconnect, fails every pending response for that device — marks all channel slots offline |
| `ChannelCommandHandler` (impl) | `Services/Implementations/ChannelCommandHandler.cs` | Channel-slot abstraction; delegates actual I/O to the device's `DeviceConnection`; keeps `Session` and program state |
| `AuthController.Login` | `Controllers/AuthController.cs` | Validates credentials via `SignInManager`, generates JWT via `GenerateToken` |
| `AuthController.GenerateToken` | `Controllers/AuthController.cs` | Builds JWT claims/token from `JwtSettings` |
| `ExportController.RequestExport` | `Controllers/ExportController.cs` | Enqueues a Hangfire job via `IBackgroundJobClient` to generate an export |
| `DeviceController.CoreSendProgram` / `CoreStart` / `CoreStop` / `CorePause` / `CoreContinue` / `CoreLiveData` | `Controllers/DeviceController.cs` | Static helpers shared between REST endpoints and `DeviceMcpTools` — single source of truth for device operations |
| `AppDbContext` | `Data/AppDbContext.cs` | EF Core + Identity context; owns all `DbSet<T>` and `OnModelCreating` |
| `ServiceCollectionExtensions.AddBtsServiceDependencies` | `Extensions/ServiceCollectionExtensions.cs` | DI registration for all services/repositories, called from `Program.cs` |
| `ServiceLocator` | `Services/Implementations/ServiceLocator.cs` | Static locator used during startup DB migration/seeding in `Program.cs` |

---

## Naming Conventions

| Thing | Convention | Example |
|-------|-----------|---------|
| Files | PascalCase | `ChannelCommandHandler.cs` |
| Classes / Interfaces | PascalCase, `I` prefix for interfaces | `IChannelCommandHandler` |
| Methods / Properties | PascalCase | `SendAndWaitAsync` |
| Private fields | camelCase, `_` prefix | `_pendingResponses`, `_writeLock` |
| Razor components/pages | PascalCase `.razor` | `DeviceList.razor` |
| DB tables (EF default) | PascalCase (pluralized DbSet name) | `Devices`, `Channels` |
| Device/channel keys | `"deviceId-boardNumber-channelId"` string key | used in `ChannelManager._devices` |

---

## Gotchas / Watch Out For

- `ChannelManager._devices` stays a **flat** dictionary keyed by `"deviceId-boardNumber-channelId"` — the multiplexing refactor deliberately preserved this public shape so existing callers (`DeviceController`, `DeviceMcpTools`) don't break.
- One `TcpClient` per **device**, not per channel — up to 64 channels share a single `DeviceConnection`. Never open a second socket per channel.
- Disconnecting a `DeviceConnection` marks **every** channel slot for that device offline — this is intentional, not a bug.
- `AddJwtAuthentication` is defined (JWT settings, `AuthController`) but **not confirmed wired into `Program.cs`** — verify before assuming bearer-token auth is enforced anywhere beyond `[Authorize]` attribute presence.
- `DeviceController` currently has **no `[Authorize]`** — anyone reachable can call hardware endpoints. Known gap, not yet fixed.
- `appsettings.json` contains secret-shaped values (`AppSettings:DbEncryption`, `AppSettings:Password`, `ConnectionStrings:DefaultConnection` local path) — never copy real values into docs; treat as sensitive even in dev.
- `Repositories/Interfaces/IRepository.cs` has a documented namespace typo per the clean-architecture design doc — confirm current state before depending on it in new code.
- `docs/architecture/02-backend-clean-architecture-design.md` is a **future design**, not current state — the codebase is still a single ASP.NET Core project. Don't treat proposed Domain/Application/Infrastructure/Api project names as real.
- **FIXED 2026-08-11** — `ChannelManager.HandleCommandClientAsync`'s shared per-device read `buffer` was `new byte[33]` (sized only for the registration packet) but the multiplexing refactor routes *every* response type through the same loop; factory/manufacturing config and calibration responses run up to 163 bytes (`DecoderService.ParseCalibrationPayload`). A too-small buffer truncates the packet mid-frame, and the leftover bytes get read as the start of the *next* iteration — corrupting `buffer[2]` (the address byte `DeviceConnection.HandleIncomingPacket` routes on) for every packet after it. Bumped to 1024 bytes (matches the old per-channel `SendAndWaitForResponseAsync`'s buffer size, pre-multiplexing). **Any new response type must stay comfortably under 1024 bytes, or this needs revisiting.**
- **FIXED 2026-08-11** — `ChannelCommandHandler.StartProgram` never set `Session.SecondaryBoardNumber` (stayed default `0`, persisted that way via `ProgramRepository.CreateSession`), and `Session.SessionFilePath` was built as `{SessionID}_{DeviceID}_{ChannelNumber}.db` — no board segment — while `ChannelManager.StoreUdpData` already names the live-data file `{SessionID}_{DeviceId}_{SecondaryBoardNumber}_{ChannelId}.db`. Two channels sharing a channel number on different boards of the same device would collide on session identity/file path. Any new "assign session identity" code must always include `SecondaryBoardNumber`, not just `DeviceID`+`ChannelNumber` — this project now has 3-part addressing everywhere, not 2-part.
- `HardwareSimulator/` (Python) mirrors the server's device-level multiplexing on the client side: one TCP connection per simulated device, shared across up to 64 board/channel-addressed circuits. It is a standalone script, not built/tested by `dotnet build`/`dotnet test` — verify manually with `python -m py_compile simulator.py` and `python -m pytest tests -q` after editing. **`HardwareSimulator/README.md` (added session #16) is the operator guide** — read it before changing the simulator.
- 🪤 **The four listener Tasks in `ChannelManager` are never awaited** (`Task.Run` into `_commandListenerTask` / `_dataStoreListenerTask` / `_dataViewListenerTask` / `_udpDataProcesser`), and `ExecuteAsync` parks on `Task.Delay(Timeout.Infinite)` inside a catch-all. An exception escaping a listener body therefore **cannot** crash the process — it silently kills the port while the app looks healthy. That is why every listener now runs under `SuperviseAsync` (ADR-4) and why `ListenerHealthCheck` exists. **Never add a socket loop here without wrapping it in `SuperviseAsync`.**
- 🪤 **UDP receive loops must stay OUTSIDE their `try`.** They used to be inside it, so a single exception on a single datagram permanently ended the listener. Related: on Windows an ICMP port-unreachable (a datagram sent to a powered-off device) surfaces as `SocketException(ConnectionReset)` on the **next** `ReceiveAsync` — `DisableUdpConnReset` sets `SIO_UDP_CONNRESET(false)` to stop that, and the loops also swallow `ConnectionReset` explicitly.
- 🪤 **`_udpChannel` must be recreated per lifecycle.** `StopInternalAsync` calls `Writer.TryComplete()`, which is **irreversible for that instance**. It used to be a `readonly` field, so `RestartService()` left the writer permanently completed and `_ = Writer.WriteAsync(...)` returned a discarded faulted `ValueTask` → **silent total UDP data loss with no log**. `StartInternal` now creates a fresh channel and passes the instance to the writer + processor as a parameter (not a field read). Writer uses `TryWrite` so a refusal is logged.
- 🪤 **Never invoke `HardwareManagerChanged` from a `finally`.** It used to fire on every accept *and* every accept error; a throwing subscriber bypassed both catches and killed the accept loop. Use `RaiseHardwareManagerChanged()`, which swallows subscriber exceptions.
- ⚠️ **The simulator's `server.*_port` config keys only configure the CLIENT side.** The server's listener ports are **hardcoded** in `Services/ChannelManager.cs:42-44` (`commandPort = 9999`, `dataViewPort = 10000`, `dataStorePort = 10001`). Editing `HardwareSimulator/config.json` alone silently breaks the link — change both sides or neither.
- **CLEANED 2026-08-18 (session #16)** — `HardwareSimulator/config.json` had 4 groups of keys no code path ever read: the entire `program` block (a pre-CoreEngine leftover; programs arrive over the wire as `0xBB` and are decoded by `program_decoder.py` — its `"DC"` operator isn't even a valid `OperatorConstants` name), `device_defaults.mac_address` (a per-channel `uuid4` is used instead), `device_defaults.serial_number` (computed as `10000000 + d*100 + c`), and `data_ranges.capacity_min/max` + `power_min/max` (power and capacity are **derived**: `power = current * voltage`, capacity integrated per tick). Only `current_*`, `voltage_*`, `temperature_*` in `data_ranges` are live. Before adding a config key here, confirm something actually reads it.
- ⚠️ **`DeviceCircuit.is_dbc` is write-only** as of session #16 — set at `simulator.py:758`/`:791` when a DBC transfer arrives, read nowhere. The only reader was a dead `data_sender` block that logged `"DBC mode - sending DBC packet"` while sending nothing; removed. If DBC UDP transmission is implemented later, `is_dbc` is the flag to hang it off.
- ⚠️ **Simulator `-d` scales DEVICES, not boards.** `create_devices` derives the board from the **channel index only** (`board = ((c-1)//8) + board_base`), so `-d 10` yields `1-1-1`..`1-1-8` then `2-1-1`.. — it can never produce `1-2-1`. The middle (board) segment only grows once `channels_per_device` passes 8. Added `-n/--channels` in session #16 so this no longer needs a `config.json` edit. Asked about repeatedly; the README now has a worked ID table.
- **ADDED 2026-08-18 (session #16) — `--board-base {0,1}` / `simulation.secondary_board_base`.** The old formula `((c-1)//8) + 1` could **never emit board 0**, yet real field devices register as `1-0-1`..`1-0-8` (session #11) and `Utils/ChannelAddressCodec.cs:7` accepts boards `0-8`. The simulator therefore could not reproduce the production topology — board-`0` server paths were exercised only by real hardware. Default stays `1` (unchanged IDs); `--board-base 0` reproduces the field layout. ⚠️ **Not yet run end-to-end against the server** — unit-verified only.
- 🪤 **`if args.board_base is not None:` — never "simplify" to truthiness.** `--board-base 0` is falsy, and the adjacent `if args.devices:` / `if args.interval:` overrides in `simulator.py::main` use plain truthiness, so copying that pattern silently ignores the only value the flag exists to set. Same trap applies to any future `0`-valued simulator flag.
- ⚠️ **`docs/flowDocs/index.html` cannot be regenerated.** `docs/flowDocs/README.md:25` documents `python docs/flowDocs/_build/build.py`, but **that script does not exist and was never committed** (`git log --all -- 'docs/flowDocs/_build/*'` is empty). The 190 KB HTML is pre-rendered, so **every chapter edit must be hand-mirrored into it** — code fences become `<figure class="panel">…<pre>` (escape `<<` as `&lt;&lt;`), tables become `<div class="table-wrap"><table>`. Chapters 04 + A were mirrored this way 2026-08-18. Either write the generator or delete the instruction.
- **CORRECTED 2026-08-18 (session #16) — `docs/PROTOCOL.md` §2.4 was badly stale.** It described the circuit-address-byte nibble split as *"Planned Change (Not Yet Implemented) … do not treat it as current behavior"* when it has shipped since the multiplexing work (`c86426c`..`2fbaf43`); linked the **deleted** `docs/architecture/01-hardware-hierarchy-protocol-design.md`; named the class `CircuitAddressCodec` (real: `ChannelAddressCodec`); and gave the ranges as board 0-15 / circuit 0-15 (real: board **0-8**, channel **1-8**). It also claimed legacy values map to `board=0` — **wrong**, `EncodeLegacy(ch)` is `Encode(1, ch)`, so legacy is board **1** and board **0** is a distinct real board. Anyone implementing from §2.4 before this fix would have got the address byte wrong.
- ⚠️ **`ChannelAddressCodec.Decode` does not validate.** `Encode` throws outside board `0-8` / channel `1-8`, but `Decode` only masks the two nibbles — a malformed or hostile packet yields board `9-15` or channel `0` silently. Any code resolving a channel from a decoded address must handle "no such channel" rather than trusting the pair. Documented in `docs/flowDocs/A-appendix-reference-tables.md` §A.5.
- **Simulator UDP load formula:** `device_count × channels_per_device × (1000 / packet_interval_ms)` packets/sec. Defaults (10 × 8 @ 1000 ms) = 80 pkt/s; the old `4 ch @ 10 ms` config was 4,000 pkt/s. Raise `packet_interval_ms` first if the server starts dropping packets.
- **`wwwroot/css/app.min.css` has no rebuild pipeline** — no `package.json` anywhere in the repo, nothing invokes `tailwind.config.js`. Any brand-new Tailwind utility class referenced only from `.razor` markup silently fails to render (confirmed: `border-t-8`, `border-x-8`, `border-x-transparent`, `w-0`, `h-0` were missing from the compiled CSS despite being used in `DeviceChannel.razor`). Grep the compiled CSS before relying on a new Tailwind class, or use inline `style="..."` instead. See [ADR-2](DECISIONS/2026-08-11_css-build-has-no-pipeline.md).
- **Transfer order is Battery → DBC → Program**, not Program first. `TransferDialog.razor:372-445` runs `HWReadyToReadWriteAsync` → `SetBatteryParamAsync` → `TransferDbcFile` (skipped if no port selected) → `SetProgramAsync`. The dialog's *result list* is assembled in the order IsReady/Program/Battery/DBC, which routinely misleads people into thinking Program is sent first. Display order ≠ execution order.
- ⚠️ **The UI and REST/MCP transfer paths disagree.** `TransferDialog.razor:372-445` sends **Battery → DBC → Program** and passes all three DBC ports. `DeviceController.CoreSendProgram` (`DeviceController.cs:41-108`, also the path `DeviceMcpTools` uses) sends **Program → Battery → DBC** and calls `TransferDbcFile(dbc.Data)` — **port 1 only**, p2/p3 left null. So a channel loaded over REST receives its program before its DBC signal map and can never be given port-2/3 DBC files. They also differ on failure handling: the UI aborts remaining steps, `CoreSendProgram` records a message and continues. Found 2026-08-18 (session #12), **not reconciled** — see T-23.
- ⚠️ **`TimeSyn()` and `ResetSystem()` build the same wire frame.** `ChannelCommandHandler.TimeSyn` uses `StartByte.Control` (`0xEE`) with `ConfigurationQuery.SyncTime` (`0x06`); `ResetSystem` uses `ProgramControlQuery.SystemReset` (also `0x06`). Both emit `0xEE` + query `0x06`, differing only in TimeSyn's 4-byte epoch payload — while `docs/PROTOCOL.md` §7 lists Control-family SyncTime as `0x05`. Found 2026-08-18 (session #12), **not fixed** — needs firmware confirmation. See T-21. Source: `ChannelCommandHandler.cs:559-592`, `Models/Enums/CircuitEnums.cs:12-40`.
- **Registration is deliberately fail-first.** `ProcessRegistrationPacketAsync` answers an unapproved channel with `CommandStatus.Failed` *but still inserts its row* into `Channels`. There is no server→device "provision" command; approval works by flipping `IsRegistered` to `1` and letting the hardware's own retry loop land in the success branch. Don't "fix" the Failed response.
- **A session can end two ways.** Operator `StopProgram()` (`0xEE/0x02`), *or* `StartStoreWorkerAsync` spotting an `OperatorConstants.STO` record while draining the store queue. Both call `IProgramServices.EndSession`. Natural completion is discovered in the data stream, not signalled by a command — so any change to store-queue draining can silently break end-of-session detection.
- New: `Components/UI/Dashboard/CardPreviewData.cs` — shared static metadata (property dictionaries + preview sample values) used by both `CardSettings.razor` (quick dialog) and `Components/Pages/Settings/CardConfiguration.razor` (full page), so their property lists/previews can't drift apart again.
- **ADDED 2026-08-18 (session #18) — `NotificationItem` and IndexedDB `saveNotifications`/`getNotifications`/`deleteNotification` are GONE.** The navbar bell's old per-browser storage was replaced entirely by server-persisted `Models/Entities/AlarmLog` via `Services/Alarms/AlarmService.cs`. Do not resurrect `NotificationItem` or the IndexedDB functions — see ADR-5 and the session #18 log for why.
- 🪤 **`Services/EventBusService.cs` is unsuitable for anything alarm-like or long-lived.** `PublishAsync` only refreshes a subscriber's `LastActive` when a message is *published*; a 5-minute `Cleanup` timer then silently removes subscribers past that cutoff on a quiet topic. `_handlers` is also an unlocked plain `Dictionary` mutated from multiple threads. `AlarmService` deliberately uses its own plain C# event instead (ADR-5). Don't route a "must never silently stop receiving" channel through `EventBusService` without fixing these two issues first.
- **Connections are two layers, keyed differently (T-45, 2026-08-20).** `DeviceLink` = one per *physical socket*, and it owns the **write lock**; `DeviceConnection` = one per *(device, secondary board)*, owning that board's pending-response correlation and channel slots, pointing at whichever link its registrations arrive on. `ChannelManager._deviceLinks` is keyed by the `TcpClient` **instance**, so boards sharing a socket share the link (writes serialize) and boards on separate sockets stay independent — the hardware picks the shape, the server discovers it. ⚠️ Never move the write lock back onto `DeviceConnection`: two boards on one socket would then hold separate locks over the same stream and interleave bytes mid-frame. ⚠️ `Add(channel, tcpClient = null)` is called **without** a socket at startup and from the Allow gate — `GetOrCreateDeviceLink(null)` returns null and `AttachLink(null)` is a deliberate no-op.
- **One comms-loss alarm per (device, board) since T-45** (key `{device}/{board}/comms-loss`) — boards can drop independently. Previously per device. The paragraph below predates that change:
- **One comms-loss alarm per device, not per channel slot.** `ChannelManager`'s disconnect `finally` block (around `HandleCommandClientAsync`) extracts the device id from the *first* `-`-split segment of a `ChannelSlotKey` (`"{deviceId}-{board}-{channel}"`) rather than raising once per slot — a dead cable is one fault, not up to 64.
- **Comms-loss alarm clears on ANY valid registration packet**, independent of whether the DB registration branch that follows succeeds, fails, or finds an already-registered channel — the alarm is about the TCP link being back, which receiving any parseable packet already proves.
- ⚠️ `ButtonSize` enum member is `Small`, not `Sm` (`Components/UI/Button/Button.razor`'s `enum ButtonSize { Default, Small, Large, Icon, Auto }`) — easy typo when writing new pages.
- **Dev database is SQLite** (`D:\MEWebApp\BtsAppdb.db`), confirmed via `dotnet run` at session #18 — despite `[Table(Schema = "Audit")]`-style attributes throughout the entity model (a SQL Server convention). EF logs one `SchemaConfiguredWarning` per schema-attributed entity at startup ("SQLite does not support schemas... ignored") — this is expected noise, not a bug to fix.
- ⚠️ **Dashboard header status counts and card status badges use different sources.** The header chips in `DashboardView.razor` count `RealTime.RealTimeRecord.CircuitStatus` directly; `DeviceChannel.razor:1708` derives the *card's* badge from `Channel.IsConnected` first (`IsConnected ? CircuitStatus : (ProgramStatus == Running ? Error : Offline)`). A circuit whose TCP link dropped while its last record still reads `Charge` is counted **Online** in the header but renders **Offline** on its card. Known and deliberate as of 2026-08-18 (session #15) — don't 'fix' one side in isolation.
- **`Online` is not a `CircuitStatus` member.** The enum is `Idle/Charge/Discharging/Pause/Countinue/Interrupt/Error/Msg/Offline` (`Models/Enums/CircuitEnums.cs:48`); the dashboard's `Online: N` chip is the *complement* of `Offline` and lives in its own `bool _onlineOnly` flag, mutually exclusive with `_statusChip`. Adding it to the enum would break every `CircuitStatus` wire-decode path — it is a UI-only concept.
- **New status colors must reuse an existing `--status-*` var.** There is no `--status-online`, and per ADR-2 `wwwroot/css/app.min.css` has no rebuild pipeline, so a var added to `app.css` never reaches the browser. The Online chip therefore reuses the green `text-status-continue`.
- ⚠️ **`ColumnDef` defines a column's value in three independent places** — `SearchSelector`, `ExcelValueSelector` and `CellTemplate`. For any *composed* (non-plain-property) column they drift silently: `DeviceList.razor`'s Circuit ID had correct 3-part `device-board-channel` search + Excel selectors while the `CellTemplate` still rendered 2-part `device-channel`, so the grid disagreed with its own search and export (fixed 2026-08-18, session #14). When editing a composed column, change all three. A single computed `ChannelDto.CircuitId` would remove the class of bug — not done yet.
- **`dotnet build` fails while the app is running** — `MSB3021`/`MSB3027` "file is locked by BatteryTestingSystem" on `bin/Debug/net8.0/BatteryTestingSystem.exe`. This is a *copy* failure, not a compile error, and `-t:Compile` does **not** avoid it. To verify code compiles without stopping the app: `dotnet build -p:OutputPath=obj/verify-out/`, then delete `obj/verify-out`.
- ⚠️ **`DashboardView.razor` row width math is padding-sensitive.** `RowGapPx` spreads *all* leftover width across a row's inter-card gaps, so the width it is given must be the width a row may actually occupy. Since 2026-08-18 (session #13) rows carry `EdgePadPx = 12` of horizontal padding (so side cards' borders don't sit flush against the scroll container / docked DBC panel), and both `ColumnsPerRow` and `RowGapPx` read `AvailableRowWidthPx` (`_measuredContainerWidthPx - 2 * EdgePadPx`), **not** the raw measured width. Add any further row padding/margin without mirroring it there and rows overflow — the same horizontal-scroll bug `elementSize.js` was introduced to fix.
- New: `wwwroot/js/elementSize.js` — `ResizeObserver`-based `window.ElementSize.observe(elementId, dotNetRef)` module, used by `DashboardView.razor` to measure the real grid container width instead of a hardcoded assumption (was causing horizontal scroll).
- **Program byte format is not blindly self-describing** — a step with zero Limits gets no count byte at all (not even `0x00`); `HardwareSimulator/program_decoder.py` resolves this with a bounded candidate-search validated against the step's known length (from the outer `0xAA55` offset framing). See [ADR-3](DECISIONS/2026-08-11_program-decode-candidate-search.md).
- **SET/REG operators are not setpoints** — they configure a registration bitmask (which telemetry fields get reported), not current/voltage. Real setpoints come from CC_CHG/CV_CHG/CP_CHG/CCCV_CHG (+`_DCHG`) and TABLE row profiles. Don't assume otherwise when touching `core_engine.py` or `program_decoder.py`.
- **Incoming TCP command frames carry a trailing 2-byte CRC** (`data[4:-2]` is the real payload, not `data[4:]`) — caught mid-implementation when chunk reassembly initially fed the CRC bytes into the byte stream, corrupting decode. Any new command-payload consumer in `simulator.py` must slice off the trailing CRC.
- **FIXED 2026-08-11** — `simulator.py`'s `build_realtime_packet` wrote the Cycle/Table field block as `struct.pack(">HH", ...)` five times (20 bytes) where `DecoderService.ParseRealTimeData` expects 5 distinct 2-byte fields (10 bytes) — every realtime packet was mis-sized, corrupting IOStatus/CRC alignment on decode. Fixed alongside CoreEngine wiring; verified the fix produces the exact expected packet length.
- **FIXED 2026-08-11** — `simulator.py`'s `OperatorCode` enum didn't match the real wire values in `Components/UI/Program/OperatorConstants.cs` (was using unrelated guessed values like `SET=0x01`). Replaced with the verified real values (`PAU=8, GOTO=9, SET=10, STO=11, ...`).
- **FIXED 2026-08-12** — `DeviceChannelRepository.GetChannelAsync` (a read/existence check) called `GetOrCreateBoardAsync`, which inserts a `SecondaryBoards` row unconditionally — on a brand-new device's first registration the `Device` row doesn't exist yet, so the insert violated the `DeviceId` FK. Made `GetChannelAsync` a read-only board lookup; only `InsertAsync` (which creates `Device` first) still creates boards.
- **The C# `ProgramRunningStatus` enum has only 2 members (`Stop=0x00`/`Running=0x01`)** even though the wire protocol and `simulator.py`'s Python enum support more (`IDLE`/`RUNNING`/`PAUSED`/`COMPLETED`). **FIXED 2026-08-12** — simulator sent `COMPLETED` (`0x03`) on natural program-end, an undefined value in C#'s enum, so `DashboardView.CanContextAction`'s `ps == ProgramRunningStatus.Stop` gate (controls Start/Transfer/Details/Calibration button enablement) permanently failed after a program finished on its own — only the Stop button stayed enabled. Simulator now sends `IDLE` (`0x00`) instead. **Any future simulator status value must map to one of the 2 existing C# values, not a new one**, or button-enablement logic silently breaks.
- New: `Components/UI/Dashboard/PreviewChannelCommandHandler.cs` (2026-08-12) — static no-op `IChannelCommandHandler` implementation seeded from `CardPreviewData`'s sample values, used so `CardSettings.razor`/`CardConfiguration.razor` previews render the real `DeviceChannel` component instead of a hand-built approximation (guarantees preview/actual-card parity; fixes the recurring drift issue noted in ADR-2's history).
- **FIXED 2026-08-12** — `ChannelFilter.razor`'s ("My Channels" popover) `_expandedDevices`/`_expandedSecondaries` state started empty with no default-expand logic, so every Device/Board node rendered collapsed on open — looked like a flat list, not a treeview, until manually expanded one at a time. Added `_seenDevices`/`_seenSecondaries` tracking so a node auto-expands the first time it appears, without clobbering a user's later manual collapse.