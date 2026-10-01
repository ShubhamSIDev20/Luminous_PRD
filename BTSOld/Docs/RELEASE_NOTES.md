# Release Notes
**Document ID:** BTS-RN-001  **Version:** 0.6  **Date:** May 2026  **Status:** Approved

---

## v0.6 — May 2026

### New Features

- **PRODUCER Operator (byte = 20)** — New program step operator that embeds another saved program's steps inline. On hardware transfer (`SetProgramAsync`), PRODUCER steps are transparently expanded: the referenced program's inner steps (excluding its SET and STO boundary steps) are inlined and all step numbers are renumbered sequentially. The operator appears in the Program Editor dropdown alongside all other operators.

- **PRODUCER — Program Dropdown & Viewer** — When a step is set to PRODUCER, the Nominal Values cell in the Program Editor renders a dropdown listing all saved programs (excluding the current program to prevent self-reference). A 👁 eye button next to the dropdown opens a read-only `ProgramViewer` dialog showing the sub-program's full step list.

- **PRODUCER Validation** — PRODUCER steps are validated at the ProgramEditor level: the selected program name must be non-empty and must exist in the database. Error indicators appear on the step row in the same style as all other validation errors.

- **Session Self-Containment — PRODUCER Programs** — When a test session starts (`StartProgram`), the full `ProgramDTO` (including all steps) of every program referenced by a PRODUCER step is serialised into the session SQLite file under the key `ProducerProgram_{name}`. This makes sessions fully self-contained for offline replay.

- **Session Self-Containment — TABLE Files** — At session start, the raw content of every TABLE file referenced in any program step is read from disk and serialised into the session SQLite file under the key `TableFile_{fileName}`. Sessions no longer depend on the TABLE file being present on the server at playback time.

- **Offline Session Viewer — PRODUCER Eye Button** — In the BmsDashboard program table (offline mode), PRODUCER step rows now show the referenced program name with a 👁 eye button. Clicking it opens a modal showing that sub-program's steps — loaded entirely from the session DB, not from the live program database.

- **Offline Session Viewer — TABLE Eye Button** — TABLE step rows in the BmsDashboard program table now show the referenced filename with a 📄 button. Clicking it opens a modal showing the TABLE file content — loaded from the session DB snapshot.

- **Reports — Session Properties Panel** — An ℹ Info button has been added to each row in the Sessions table. Clicking it opens a Session Properties panel that shows: session metadata (program, battery, device/circuit, times, duration), a list of PRODUCER sub-programs embedded in the session (name + step count), and a list of TABLE files captured (filename + line count). All data is read from the session SQLite file on demand.

### Changed

- `SessionRequest` DTO — two new nullable properties: `ProducerPrograms` (`List<ProgramDTO>?`) and `TableFileData` (`Dictionary<string, string[]>?`). Fully backwards compatible — existing callers that do not set these fields default to `null`.
- `SqliteBulkDatabaseManager.InsertSessionAsync` — persists PRODUCER programs and TABLE file data into `configurationEntities` table rows.
- `SqliteBulkDatabaseManager.GetSessionAsync` — restores PRODUCER programs and TABLE file data from the session DB.
- `DecoderService.ConvertProgramIntoBytesPackets` — new overload that accepts `Dictionary<string, List<StepModel>>? resolvedPrograms`; expands PRODUCER steps before byte conversion.
- `CircuitCommandHandler.SetProgramAsync` — resolves PRODUCER references then uses the new DecoderService overload.
- `CircuitCommandHandler.StartProgram` — collects PRODUCER sub-programs and TABLE file data before calling `InsertSessionAsync`.

---

## v0.5 — May 2026

### New Features
- **Program Scheduler (Hangfire)** — Schedule program uploads to target circuits at a future UTC time; supports multiple circuit targets per schedule, DBC file assignment, enable/disable toggle, and full execution log with per-circuit status (Success / Failed / Skipped_Offline / Skipped_AlreadyRunning)
- **SchedulerPage** — New Blazor page under Programs for creating, viewing, toggling, and deleting scheduled jobs
- **Group-By DataTable** — Collapsible grouped rows in DataTable component with expand/collapse state persisted via `ServerSessionStorageService`
- **DbcDatabaseEditor** — Message/Signal grouped collapsible view with group-level checkbox selection and Remote Frame toggle at message level
- **GitHub Releases** — Automated ZIP release artifact (`v0.5.{build}`) on every push to main
- **PROTOCOL.md** — Full hardware communication protocol specification document
- **Docs/** — Full CMMI Level 3 documentation suite
- **ARCHITECTURE_CAPACITY.md** — System capacity and performance analysis document

### Bug Fixes
- **Toast.razor** — Fixed CS0101/CS8802/CS0426 build errors caused by inline `__builder =>` lambdas in switch expressions
- **Dockerfile** — Fixed port mismatch: ASPNETCORE_URLS correctly set to 5000, EXPOSE corrected
- **CI/CD Workflow** — Fixed: switched from `windows-latest` to `ubuntu-latest`, added `needs: build` dependency, switched from DockerHub to ghcr.io with `GITHUB_TOKEN`, added `permissions: packages: write`
- **Repository warnings** — CS0108/CS0114 warnings (hidden members) documented

### Infrastructure
- Docker image published to `ghcr.io/sysin1/battery-testing-system:latest`
- `docker-compose.yml` added for production deployment
- All ports documented: 5000 (Web), 9999 (TCP), 10000/10001 (UDP data), 10002/10003 (UDP discovery)
- Hangfire background job processing (`install-hangfire.ps1` for setup)
- New EF Core migration: `20260505200711_AddProgramScheduler` (ProgramSchedules + ScheduleExecutionLogs tables)

---

## v0.4 — April 2026

### New Features
- DBC CAN file upload, parsing, and signal selection
- Per-session SQLite database with dynamic DBC signal columns (`SqliteSchemaSync`)
- `SqliteBulkDatabaseManager` with `Channel<T>` for non-blocking UDP → SQLite writes
- Audit logging (`AuditLogs` table)
- User circuit access control (`UserCircuitAccess` table)
- `BatterySession` summary properties (TotalCapacity, TotalEnergy, CycleCount)

---

## v0.3 — March 2026

### New Features
- DBC file records database table
- DbcParser — full DBC file parsing (messages, signals, baudrate, port)
- Program upload via TCP (0xBB protocol — steps + DBC file)

---

## v0.2 — January 2026

### New Features
- `CodeMessages` table — hardware error code definitions
- `ConfigurationEntity` table — server-side UI state persistence
- `ServerSessionStorageService` — per-user, per-component state management

---

## v0.1 — December 2025

### Initial Release
- Base architecture: Blazor Server + EF Core + SQLite
- Device/Circuit/Program/Battery/Session entities
- TCP registration (CircuitManager + CircuitCommandHandler)
- UDP real-time data listener (port 10000)
- UDP session data listener (port 10001)
- SADP device discovery (BroadcastUdpService)
- ASP.NET Identity authentication
- Registration Standards
- Serilog rolling file logging
