# Background Services, Session Files & DBC Parameters
> Updated: 2026-08-10T00:00:00Z
> Covers: Hangfire background jobs, how a battery test session file is created/stored, how DBC files/parameters are added and pushed to hardware.

---

## 1. Background Services (Hangfire)

There are **no `IHostedService`/`BackgroundService` classes** in this codebase. All background/scheduled work runs through **Hangfire** (`IBackgroundJobClient` / static `BackgroundJob.*`), enqueued from request-handling code.

### SchedulerService — scheduled program execution
`Services/Implementations/SchedulerService.cs` (`SchedulerService : ISchedulerService`, interface at `Services/Interfaces/ISchedulerService.cs`)

| Method | What it does |
|--------|-------------|
| `CreateScheduleAsync(CreateScheduleRequest)` | Validates `ScheduledAt` is in the future, persists a `ProgramSchedule` entity, calls `BackgroundJob.Schedule<ISchedulerService>(svc => svc.ExecuteScheduleAsync(entity.Id), delay)`, stores the returned Hangfire job ID as `ProgramSchedule.HangfireJobId` |
| `DeleteScheduleAsync` | Cancels the pending job via `BackgroundJob.Delete(entity.HangfireJobId)` |
| `ExecuteScheduleAsync(long scheduleId)` | **The actual Hangfire job body.** Reloads the schedule, resolves Program/Battery/DBC data, finds the live channel handler in `ChannelManager._devices`, checks it's `Idle` + `Stop`, then runs `HWReadyToReadWriteAsync → SetBatteryParamAsync → TransferDbcFile → SetProgramAsync → StartProgram` on `ChannelCommandHandler`, writing one `ScheduleExecutionLog` row (`Success` / `Failed` / `Skipped_Offline` / `Skipped_AlreadyRunning`) per target circuit |

**UI trigger:** `Components/Pages/Programs/SchedulerPage.razor` (`SaveSchedule`, `DeleteSchedule`, `LoadLogsAsync`).

### ExportJobService — async export generation
`Services/Implementations/ExportJobService.cs` (plain class, no interface)

| Method | What it does |
|--------|-------------|
| `[AutomaticRetry(Attempts=1)] RunAsync(int exportRecordId, CancellationToken)` | **The Hangfire job body.** Loads the `ExportRecord`, marks it `Processing`, reads the session via `ISqliteBulkDatabaseManager.GetSessionAsync`, writes an `.xlsx` — small sheets (Program/Battery/Chart) via ClosedXML, the potentially huge MeasurementData sheet via SAX-streaming `OpenXmlWriter` fed by `SqliteBulkDatabaseManager.StreamSessionDataAsync` — then marks the record `Ready` (file path + size) or `Failed` (and deletes the partial file) |

**Enqueued from:** `Controllers/ExportController.cs::RequestExport` via `_hangfire.Enqueue<ExportJobService>(svc => svc.RunAsync(created.Id, ...))`, itself triggered from `Reports.razor::RequestExportAsync` and `BmsDashboard.razor::RequestBackgroundExportAsync`.

---

## 2. Session File Creation

A "session" has two halves: a **DB metadata row** (`BatterySession` entity) and a **physical per-session SQLite file** holding the measurement data.

- `Models/Entities/BatterySession.cs` (table `Sessions.BatterySessions`) — its `SessionFilePath` column stores the *relative* path to the physical `.db` file.
- Runtime session state during an active test lives on `ChannelCommandHandler.Session` (type `SessionRecordDto`) — one instance per channel-slot.

**Path/naming** — built in `ChannelCommandHandler.StartProgram()`:
```
Session.SessionFilePath = Path.Combine(
    sessionDateTime.ToLocalTime().ToString("dd-MM-yyyy"),
    $"{Session.SessionID}_{Channel.DeviceID}_{Channel.ChannelNumber}.db")
```
i.e. `<dd-MM-yyyy>/<packedSessionId>_<deviceId>_<channelNumber>.db`. `Session.SessionID` is a packed epoch value from `DecoderService.GetSessionIdBytes(...)`.

**Physical file creation** — `StartProgram()` builds a `SessionRequest` (Program, Battery, DBC database, expanded steps, producer sub-programs, table-file data) and calls `ISqliteBulkDatabaseManager.InsertSessionAsync(...)`. `SqliteBulkDatabaseManager` (`Services/Implementations/SqliteBulkDatabaseManager.cs`) resolves `BasePath = GlobalConfig.AppSettings.Data/sessions` in its constructor; `BuildPath(dbFileName)` combines that with the relative name and creates the directory if missing — **this is where the SQLite `.db` file actually materializes on disk** (via an EF Core `SqliteDbContext` pointed at that path).

**Flow after hardware ACK:**
1. Hardware ACKs the Start command → `StartProgram()` calls `IProgramServices.CreateSession(Session)` → `ProgramRepository.CreateSession` inserts the `BatterySession` DB row mirroring what's now on disk.
2. Live measurement rows are queued via `ChannelCommandHandler.EnqueueForStore` into an unbounded `System.Threading.Channels.Channel`, drained by `StartStoreWorkerAsync()`, which batches them into `SqliteBulkDatabaseManager.InsertRecordAsync`.
3. When a stop record is seen (`Operator == OperatorConstants.STO`), `Session.EndTime` is set and `IProgramServices.EndSession(Session)` is called.

**Export reads it back:** `ExportController.RequestExport` takes a `SessionFilePath` (see `ExportRequestDto`), creates an `ExportRecord`, and enqueues `ExportJobService.RunAsync`, which reads that same `.db` file via `SqliteBulkDatabaseManager.GetSessionAsync` / `StreamSessionDataAsync`.

**Note:** `ConfigStorageService.cs` is a separate, general config-storage service — not confirmed to be part of this session-file mechanics.

---

## 3. Adding DBC Files & Parameters

- Entity: `Models/Entities/DbcFileRecord.cs` (table `Files.DBCFiles`) — `BatteryId`, `Name`, `Version`, `Description`, `OriginalFileName`, `FilePath` (relative, under `Data/DbcFiles`), `FileSizeBytes`, `dbcstrJson` (the parsed `DbcDatabase`, serialized).
- Interface `IDbcService` (`Services/Interfaces/IServices.cs`), implementation `DbcService` (`Services/Implementations/DbcService.cs`), repository `IDbcRepository` / `DbcRepository` (`Repositories/Implementations/DbcRepository.cs`) — built over `AppDbContext.dbcFileRecords` + `IAuditRepository` (writes an `AuditLog` row with `Module = ModuleName.DBC` on create/update/delete).

### Upload flow
`DbcService.UploadAsync(DbcFileRequest request, IBrowserFile file)`:
1. Rejects non-`.dbc` extensions
2. Checks `_repo.ExistsAsync(BatteryId, Name, Version)` for duplicates
3. Writes the raw file to `Data/DbcFiles/<utc-timestamp>_<sanitized-name>`
4. Parses it with `DbcParser().Parse(fullPath)` into a `DbcDatabase`
5. Builds a `DbcFileRecord` (`dbcstrJson = JsonConvert.Serialize(dbc)`) and calls `_repo.AddAsync(entity)`

### Updating parameters
- `UpdateMetaAsync` — rename / re-version / change description (dup-check excludes self)
- `UpdateDbcDatabaseAsync(id, DbcDatabase)` — re-serializes the parsed signal/message tree back into `dbcstrJson`, used after the user toggles which signals are selected in the UI
- `DeleteAsync` — removes the physical file (`Path.Combine(GlobalConfig.AppSettings.Data, entity.FilePath)`) then the DB row

### UI entry point
`Components/Pages/Batteries/BatteriesList.razor` — `SaveDbc`, `HandleDbcSave`, `LoadDbcForBatteryAsync`, `DownloadDbc`, `ExecuteDeleteDbc`. DBC files are attached **per-battery**.

> ⚠️ Not fully confirmed: the exact UI component/markup where individual signals are edited/added as "parameters" beyond `HandleDbcSave` → `UpdateDbcDatabaseAsync`. Needs a follow-up look at `BatteriesList.razor`'s DBC editor markup if precise parameter-add UX is needed.

### Pushing DBC to hardware
`ChannelCommandHandler.TransferDbcFile(port1Dbc, port2Dbc, port3Dbc)`:
1. Assigns unique per-signal IDs (31–255) across up to 3 ports' `DbcDatabase.Messages[].Signals` where `IsSelected`
2. Merges the three databases into the session (`MergeDbcDatabases`)
3. Builds a multi-port binary payload (`DbcDatabase.BuildMultiPortPayload`)
4. Streams it to the device in 1400-byte chunks via `DecoderService.BuildCommand` / `SendAndWaitForResponseAsync`

Called from both manual channel operations **and** `SchedulerService.ExecuteScheduleAsync` (which resolves `Port1DbcFileId` / `Port2DbcFileId` / `Port3DbcFileId` off `ProgramSchedule` via `IDbcService.GetByIdAsync`).

---

## Key Files Reference
| File | Role |
|------|------|
| `Services/Implementations/SchedulerService.cs` | Scheduled program execution via Hangfire |
| `Services/Implementations/ExportJobService.cs` | Async Excel export generation via Hangfire |
| `Services/Implementations/SqliteBulkDatabaseManager.cs` | Owns per-session `.db` file path/creation and bulk insert/stream of measurement rows |
| `Services/Implementations/ChannelCommandHandler.cs` | `StartProgram`, `EnqueueForStore`, `StartStoreWorkerAsync`, `TransferDbcFile` |
| `Services/Implementations/DbcService.cs` | DBC upload/update/delete |
| `Repositories/Implementations/DbcRepository.cs` | DBC persistence + audit logging |
| `Models/Entities/DbcFileRecord.cs` | DBC file DB record |
| `Models/Entities/BatterySession.cs` | Session DB record (`SessionFilePath`) |
| `Components/Pages/Programs/SchedulerPage.razor` | Scheduling UI |
| `Components/Pages/Batteries/BatteriesList.razor` | DBC upload/edit UI (per-battery) |