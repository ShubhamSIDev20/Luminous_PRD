# Requirements Traceability Matrix (RTM)
**Document ID:** BTS-RTM-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Introduction

This document traces every functional and non-functional requirement from the SRS to its implementing component(s) in the codebase, and to its validation test. Full coverage confirms CMMI Level 3 REQM compliance.

**Traceability structure:**
```
SRS Requirement → Repository Interface → Service Interface → Service Implementation → Validation Test
```

---

## 2. Layer Map

| Layer | Location | Role |
|---|---|---|
| Repository Interface | `Repositories/Interfaces/` | Data access contract |
| Repository Implementation | `Repositories/Implementations/` | EF Core / SQLite data access |
| Service Interface | `Services/Interfaces/` | Business logic contract |
| Service Implementation | `Services/Implementations/` | Business logic |
| Hardware Service | `Services/ChannelManager.cs` | Background TCP/UDP orchestration |
| Circuit Handler | `Services/Implementations/ChannelCommandHandler.cs` | Per-device command handler |
| Broadcast Service | `Services/BROADCAST/BroadcastUdpService.cs` | Device discovery UDP |
| Decoder | `Services/DecoderService.cs` | Binary packet parser |
| REST API | `Controllers/` | External REST interface |
| MCP | `MCP/DeviceMcpTools.cs` | AI/LLM tool interface |
| UI | `Components/` | Blazor Server pages |
| Auth | `Services/Auth/` | ASP.NET Identity providers |

---

## 3. Functional Requirements Traceability

### FR-001 — Device Discovery

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-001.1 | Server broadcasts discovery on UDP :10002 to all network interfaces | — | — | `BroadcastUdpService.StartAsync()` | VAL-004 | ✅ |
| FR-001.2 | Parse device replies on UDP :10003 into `BroadcastDeviceInfo` | — | — | `BroadcastUdpService` + `BroadCastModels` | VAL-004 | ✅ |
| FR-001.3 | Push IP/network config to device (Q4 — `BroadcastIpConfig`) | — | — | `BroadcastUdpService.SendIpConfig()` | VAL-005 | ✅ |
| FR-001.4 | Push BTS server address and ports to device (Q5 — `BroadcastServerConfig`) | — | — | `BroadcastUdpService.SendServerConfig()` | VAL-005 | ✅ |
| FR-001.5 | Display discovered devices in web UI | — | — | Device Discovery Blazor page | VAL-004 | ✅ |

### FR-002 — Hardware Registration (TCP :9999)

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-002.1 | TCP listener on port 9999 accepting hardware connections | — | — | `ChannelManager` (BackgroundService) | VAL-001 | ✅ |
| FR-002.2 | Parse 33-byte registration packet (`0xDD 0x01`) | — | — | `ChannelManager.HandleRegistration()` | VAL-001 | ✅ |
| FR-002.3 | Return `Success (0x01)` for approved device | `IDeviceCircuitRepository` | `IDeviceCircuitServices` | `DeviceCircuitServices + ChannelManager` | VAL-001 | ✅ |
| FR-002.4 | Return `AlreadyRegistered (0x02)` on reconnect | — | — | `ChannelManager` | VAL-009 | ✅ |
| FR-002.5 | Return `Failed (0x00)` for non-whitelisted device | `IDeviceCircuitRepository` | `IDeviceCircuitServices` | `ChannelManager` | VAL-001 | ✅ |
| FR-002.6 | Create one `ChannelCommandHandler` per Device-Circuit pair | — | `IChannelCommandHandler` | `ChannelCommandHandler` | VAL-001 | ✅ |
| FR-002.7 | Maintain persistent TCP connection for command/response | — | `IChannelCommandHandler` | `ChannelCommandHandler.InitializeAsync()` | VAL-001 | ✅ |
| FR-002.8 | Monitor TCP health via `ConnectionAlive()` loop | — | — | `ChannelCommandHandler` | VAL-009 | ✅ |
| FR-002.9 | Notify UI on circuit connect/disconnect | — | `IChannelCommandHandler.OnCircuitChanged` | `ChannelCommandHandler` | VAL-001 | ✅ |
| FR-002.10 | Admin whitelist Device+Circuit via web UI | `IDeviceCircuitRepository` | `IDeviceCircuitServices.AllowCicuitAsync()` | `DeviceCircuitServices` | VAL-UI-002 | ✅ |

### FR-003 — Real-Time Data Stream (UDP :10000)

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-003.1 | UDP listener on port 10000 receiving measurement packets | — | — | `ChannelManager` UDP View Listener | VAL-002 | ✅ |
| FR-003.2 | Decode `0xCC` 79-byte measurement packet | — | — | `DecoderService.DecodeRealTimePacket()` | VAL-002 | ✅ |
| FR-003.3 | Route decoded data to correct `ChannelCommandHandler` by DeviceID+CircuitID | — | — | `ChannelManager` | VAL-002 | ✅ |
| FR-003.4 | Push live update to Blazor UI via `OnDataChanged` event | — | `IChannelCommandHandler.RealTime` | `recordRequest.NotifyDataChanged()` | VAL-002 | ✅ |
| FR-003.5 | Display Current, Voltage, Temperature, Power, Capacity, Energy live | — | — | Dashboard Blazor component | VAL-002 | ✅ |
| FR-003.6 | Decode calibration live packet (`0xA0`) | — | — | `DecoderService` | VAL-007 | ✅ |
| FR-003.7 | Expose live data via REST SSE (`GET /api/Device/GetLiveSSE`) | — | — | `DeviceController.SubscribeLive()` | VAL-002 | ✅ |

### FR-004 — Session Data Storage (UDP :10001)

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-004.1 | UDP listener on port 10001 receiving session store packets | — | — | `ChannelManager` UDP Store Listener | VAL-003 | ✅ |
| FR-004.2 | Decode opcode-tagged session records | — | — | `DecoderService.DecodeStorePacket()` | VAL-003 | ✅ |
| FR-004.3 | Enqueue decoded records via `Channel<recordStoreRequest>` | — | `IChannelCommandHandler.EnqueueForStore()` | `ChannelCommandHandler` | VAL-003 | ✅ |
| FR-004.4 | Write records asynchronously to per-session SQLite file | — | `ISqliteBulkDatabaseManager` | `SqliteBulkDatabaseManager` | VAL-003 | ✅ |
| FR-004.5 | Session DB file path: `{AppData}/sessions/{SessionID}_{DeviceId}_{CircuitId}.db` | — | — | `SqliteBulkDatabaseManager.BuildPath()` | VAL-003 | ✅ |
| FR-004.6 | Decode DBC CAN signal opcodes (`0x1F`–`0xFA`) into named columns | — | — | `DecoderService` DBC section | VAL-010 | ✅ |
| FR-004.7 | Track stored vs un-stored record count in session state | — | `IChannelCommandHandler.Session` | `ChannelCommandHandler` | VAL-003 | ✅ |

### FR-005 — Program Management

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-005.1 | Create test program with steps | `IProgramRepository` | `IProgramServices.CreateProgramAsync()` | `ProgramServices` | VAL-006 | ✅ |
| FR-005.2 | Read / list programs | `IProgramRepository` | `IProgramServices.GetProgramsAsync()` | `ProgramServices` | VAL-006 | ✅ |
| FR-005.3 | Update program | `IProgramRepository` | `IProgramServices.UpdateProgramAsync()` | `ProgramServices` | VAL-006 | ✅ |
| FR-005.4 | Soft-delete / recover program | `IProgramRepository` | `IProgramServices.DeleteProgramAsync()` | `ProgramServices` | VAL-006 | ✅ |
| FR-005.5 | Upload program steps to hardware via TCP (`0xBB`) | — | `IChannelCommandHandler.SetProgramAsync()` | `ChannelCommandHandler` | VAL-006 | ✅ |
| FR-005.6 | Upload DBC file to hardware via TCP (`0xBB 0x08`) | — | `IChannelCommandHandler.TransferDbcFile()` | `ChannelCommandHandler` | VAL-010 | ✅ |
| FR-005.7 | Start program (`0xEE 0x01`) | — | `IChannelCommandHandler.StartProgram()` | `ChannelCommandHandler` | VAL-006 | ✅ |
| FR-005.8 | Stop program (`0xEE 0x02`) | — | `IChannelCommandHandler.StopProgram()` | `ChannelCommandHandler` | VAL-006 | ✅ |
| FR-005.9 | Pause program (`0xEE 0x03`) | — | `IChannelCommandHandler.PauseProgram()` | `ChannelCommandHandler` | VAL-008 | ✅ |
| FR-005.10 | Continue program (`0xEE 0x04`) | — | `IChannelCommandHandler.ContinueProgram()` | `ChannelCommandHandler` | VAL-008 | ✅ |
| FR-005.11 | Session create / end / update | `IProgramRepository` | `IProgramServices.CreateSession()` | `ProgramServices` | VAL-003 | ✅ |
| FR-005.12 | Registration Standards CRUD | `IProgramRepository` | `IProgramServices` Registration region | `ProgramServices` | VAL-UI-001 | ✅ |

### FR-006 — Calibration

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-006.1 | Send live calibration data (`0xA0 0x02`) | — | `IChannelCommandHandler.SendLiveCurrentandVoltage()` | `ChannelCommandHandler` | VAL-007 | ✅ |
| FR-006.2 | Set low/high calibration points (`0xA0 0x03`–`0x0E`) | — | `IChannelCommandHandler.CalibrationPointPreset()` | `ChannelCommandHandler` | VAL-007 | ✅ |
| FR-006.3 | Set gain + offset (`0xA0 0x05/0x08/0x0B/0x0E`) | — | `IChannelCommandHandler.SetGainOffset()` | `ChannelCommandHandler` | VAL-007 | ✅ |
| FR-006.4 | Cancel / stop calibration | — | `IChannelCommandHandler.CancelCalibration()` | `ChannelCommandHandler` | VAL-007 | ✅ |
| FR-006.5 | Read previous calibration data (`0xA0 0x14`) | — | `IChannelCommandHandler.PreviousCalibration()` | `ChannelCommandHandler` | VAL-007 | ✅ |
| FR-006.6 | Store calibration data in AppDbContext | `IDeviceCircuitRepository` | `IDeviceCircuitServices.CreateOrUpdateCalibrationDataAsync()` | `DeviceCircuitServices` | VAL-007 | ✅ |

### FR-007 — Battery Management

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-007.1 | Create / update battery record | `IBatteryRepository` | `IBatteryServices.CreateOrUpdateBattery()` | `BatteryServices` | VAL-UI-001 | ✅ |
| FR-007.2 | List batteries | `IBatteryRepository` | `IBatteryServices.GetBatteries()` | `BatteryServices` | VAL-UI-001 | ✅ |
| FR-007.3 | Delete battery | `IBatteryRepository` | `IBatteryServices.BatteryDelete()` | `BatteryServices` | VAL-UI-001 | ✅ |
| FR-007.4 | Push battery params to hardware (`0xAA 0x05`) | — | `IChannelCommandHandler.SetBatteryParamAsync()` | `ChannelCommandHandler` | VAL-006 | ✅ |

### FR-008 — Authentication & Access Control

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-008.1 | Username/password login — ASP.NET Identity | — | — | `AuthController`, `IdentityUserAccessor` | VAL-UI-001 | ✅ |
| FR-008.2 | JWT token generation (24h expiry, roles as claims) | — | — | `AuthController.GenerateToken()` | VAL-UI-001 | ✅ |
| FR-008.3 | Role-based page access (Admin / Operator / Viewer) | — | — | `[Authorize(Roles=...)]` on Blazor pages | VAL-UI-002 | ✅ |
| FR-008.4 | Circuit-level access control per user | `IUserCircuitAccessRepository` | `IUserCircuitAccessService` | `UserCircuitAccessService` | VAL-UI-003 | ✅ |
| FR-008.5 | Audit logging on all write operations | `IAuditRepository` | `IAuditService.LogEventAsync()` | `AuditService` | VAL-UI-004 | ✅ |
| FR-008.6 | Cookie-based auth for Blazor UI (revalidating) | — | — | `IdentityRevalidatingAuthenticationStateProvider` | VAL-UI-001 | ✅ |
| FR-008.7 | Global user state (`CurrentUser`) across Blazor circuits | — | — | `GlobalState.cs` | VAL-UI-001 | ✅ |

### FR-009 — DBC File Management

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-009.1 | Upload DBC file (.dbc) for a battery | `IDbcRepository` | `IDbcService.UploadAsync()` | `DbcService` + `DbcParser` | VAL-010 | ✅ |
| FR-009.2 | Parse DBC file into messages + signals | — | — | `DbcParser.Parse()` | VAL-010 | ✅ |
| FR-009.3 | Display parse errors in UI | — | — | `DbcDatabaseEditor` — `ParseErrors` panel | VAL-010 | ✅ |
| FR-009.4 | Edit signal selection, byte order, factor, offset in UI | — | — | `DbcDatabaseEditor` component | VAL-010 | ✅ |
| FR-009.5 | Save updated DBC database | `IDbcRepository` | `IDbcService.UpdateDbcDatabaseAsync()` | `DbcService` | VAL-010 | ✅ |
| FR-009.6 | Delete DBC file record | `IDbcRepository` | `IDbcService.DeleteAsync()` | `DbcService` | VAL-010 | ✅ |

### FR-010 — System Services

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-010.1 | Hardware error/message code management | `ICodeMessageRepository` | `ICodeMessageService` | `CodeMessageService` | VAL-UI-001 | ✅ |
| FR-010.2 | Persistent key-value configuration storage | `IConfigStorageRepository` | `IConfigStorageService` | `ConfigStorageService` | VAL-UI-001 | ✅ |
| FR-010.3 | Server-side session state per user | — | — | `ServerSessionStorageService` | VAL-UI-005 | ✅ |
| FR-010.4 | In-process event bus (pub/sub) | — | — | `EventBusService` | VAL-002 | ✅ |
| FR-010.5 | In-memory log store for UI log viewer | — | — | `InMemoryLogStore` | VAL-UI-001 | ✅ |
| FR-010.6 | DataTable view state persistence (page, cols, groups) | — | — | `DataTable` + `ServerSessionStorageService` | VAL-UI-005/006 | ✅ |
| FR-010.7 | UI toast notification system | — | — | `ToastService` | VAL-UI-001 | ✅ |
| FR-010.8 | UI popup/modal system | — | — | `PopupService` | VAL-UI-001 | ✅ |
| FR-010.9 | Time sync to hardware (`0xEE 0x05`) | — | `IChannelCommandHandler.TimeSyn()` | `ChannelCommandHandler` | VAL-006 | ✅ |
| FR-010.10 | Hardware system reset (`0xEE 0x06`) | — | `IChannelCommandHandler.ResetSystem()` | `ChannelCommandHandler` | VAL-006 | ✅ |
| FR-010.11 | CRC-16/Modbus on all TCP/UDP packets | — | — | `PacketAnalyzer` / `PacketAnalyzerNoReverse` | VAL-001 | ✅ |
| FR-010.12 | REST API for external integration | — | — | `DeviceController` + `AuthController` | VAL-001 | ✅ |
| FR-010.13 | MCP tool interface for AI/LLM clients | — | — | `DeviceMcpTools` | VAL-001 | ✅ |
| FR-010.14 | Program builder — binary step encoding | — | — | `ProgramBuilder` | VAL-006 | ✅ |
| FR-010.15 | File manager service (DBC binary storage) | — | — | `FileManagerService` | VAL-010 | ✅ |

---

## 4. Non-Functional Requirements Traceability

| Req ID | Requirement | Implementation | Test ID | Status |
|---|---|---|---|---|
| NFR-001 | No UDP burst data loss — `Channel<T>` bounded queue | `ChannelCommandHandler._StoreQueue` | VAL-003 | ✅ |
| NFR-002 | Real-time UI update < 500ms from hardware transmit | `recordRequest.OnDataChanged` → `StateHasChanged` | VAL-002 | ✅ |
| NFR-003 | Auto-restart on crash — Docker `restart: unless-stopped` | `docker-compose.yml` | — | ✅ |
| NFR-004 | Password hashing — ASP.NET Identity BCrypt | `UserManager<ApplicationUser>` | — | ✅ |
| NFR-005 | JWT token expiry 24 hours | `AuthController.GenerateToken()` | VAL-UI-001 | ✅ |
| NFR-006 | Session storage expiry 1 hour (non-permanent) | `ServerSessionStorageService._expiry` | — | ✅ |
| NFR-007 | Docker containerised deployment | `Dockerfile` + `docker-compose.yml` | — | ✅ |
| NFR-008 | EF Core code-first migrations | `/Migrations/` (11 migrations) | — | ✅ |
| NFR-009 | Structured logging — Serilog rolling file + console | `Program.cs` Serilog config | — | ✅ |
| NFR-010 | CI/CD build + release on every `main` push | `.github/workflows/dotnet-ci-cd.yml` | — | ✅ |
| NFR-011 | GitHub Container Registry Docker image | `ghcr.io/sysin1/battery-testing-system:latest` | — | ✅ |
| NFR-012 | GitHub Release ZIP artifact per version | `softprops/action-gh-release@v2` | — | ✅ |
| NFR-013 | SQLite WAL mode for session writes | `SqliteBulkDatabaseManager` | VAL-003 | ✅ |
| NFR-014 | Per-user circuit access enforcement | `UserCircuitAccessService` | VAL-UI-003 | ✅ |
| NFR-015 | Audit trail for all write operations | `AuditService` + `IAuditRepository` | VAL-UI-004 | ✅ |
| NFR-016 | Generic repository pattern — `IRepository<T>` | `Repository.cs` + all specific repos | — | ✅ |

---

## 5. Repository → Service → Controller Dependency Map

| Repository | Service | Controller / Consumer |
|---|---|---|
| `DeviceCircuitRepository` : `IDeviceCircuitRepository` | `DeviceCircuitServices` : `IDeviceCircuitServices` | Blazor Pages, `ChannelManager` |
| `ProgramRepository` : `IProgramRepository` | `ProgramServices` : `IProgramServices` | Blazor Pages, `DeviceController`, `DeviceMcpTools` |
| `BatteryRepository` : `IBatteryRepository` | `BatteryServices` : `IBatteryServices` | Blazor Pages, `DeviceController`, `DeviceMcpTools` |
| `DbcRepository` : `IDbcRepository` | `DbcService` : `IDbcService` | Blazor Pages, `DeviceController`, `DeviceMcpTools` |
| `AuditRepository` : `IAuditRepository` | `AuditService` : `IAuditService` | All service implementations |
| `CodeMessageRepository` : `ICodeMessageRepository` | `CodeMessageService` : `ICodeMessageService` | Blazor Pages |
| `ConfigStorageRepository` : `IConfigStorageRepository` | `ConfigStorageService` : `IConfigStorageService` | `ServerSessionStorageService.LoadDbStorage()` |
| `UserCircuitAccessRepository` : `IUserCircuitAccessRepository` | `UserCircuitAccessService` : `IUserCircuitAccessService` | Blazor Pages, Auth middleware |
| *(no repository — in-memory)* | `ServerSessionStorageService` | `DataTable`, all maintenance pages |
| *(no repository — in-memory)* | `EventBusService` | `ChannelManager`, Blazor components |
| *(no repository — SQLite direct)* | `SqliteBulkDatabaseManager` : `ISqliteBulkDatabaseManager` | `ChannelCommandHandler` |

---

## 6. Validation Test Reference

| Test ID | Description | Mechanism |
|---|---|---|
| VAL-001 | Hardware TCP registration flow | Hardware-in-loop |
| VAL-002 | Real-time dashboard data display | Hardware-in-loop |
| VAL-003 | Session SQLite data recording | Hardware-in-loop + DB inspection |
| VAL-004 | UDP device discovery | Hardware-in-loop |
| VAL-005 | IP and server config push to device | Hardware-in-loop |
| VAL-006 | Program upload and execution | Hardware-in-loop |
| VAL-007 | Calibration flow end-to-end | Hardware-in-loop |
| VAL-008 | Pause / Continue without data gap | Hardware-in-loop |
| VAL-009 | TCP disconnect and reconnect recovery | Hardware-in-loop |
| VAL-010 | DBC CAN signal recording in session DB | Hardware-in-loop + DB inspection |
| VAL-UI-001 | Login and basic navigation | Manual UI |
| VAL-UI-002 | Role-based page access | Manual UI |
| VAL-UI-003 | Circuit access control per user | Manual UI |
| VAL-UI-004 | Audit log entries | Manual UI + DB inspection |
| VAL-UI-005 | DataTable state persistence | Manual UI |
| VAL-UI-006 | Group expand/collapse state persistence | Manual UI |
| VAL-UI-007 | Excel export correctness | Manual UI + file inspection |

---

*Document ID: BTS-RTM-001 | Generated from: `Services/Interfaces/`, `Services/Implementations/`, `Repositories/Interfaces/`, `Repositories/Implementations/`, `Controllers/`, `MCP/`, `Components/` | CMMI Process Area: REQM*


### FR-011 — Program Scheduler

| Req ID | Requirement | Repository | Service Interface | Implementation | Test ID | Status |
|---|---|---|---|---|---|---|
| FR-011.1 | Create a named schedule: assign program, battery, optional DBC, target circuits, and UTC execution time | `ISchedulerRepository` | `ISchedulerService.CreateScheduleAsync()` | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.2 | List all schedules with program/battery/DBC names resolved | `ISchedulerRepository` | `ISchedulerService.GetAllSchedulesAsync()` | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.3 | Delete a schedule and cancel its Hangfire job | `ISchedulerRepository` | `ISchedulerService.DeleteScheduleAsync()` | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.4 | Enable / disable a schedule without deleting it | `ISchedulerRepository` | `ISchedulerService.ToggleActiveAsync()` | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.5 | At scheduled UTC time, upload program + battery (+ optional DBC) to all target circuits | — | `ISchedulerService.ExecuteScheduleAsync()` | `SchedulerService` (Hangfire job) | VAL-UI-008 | ✅ |
| FR-011.6 | Skip circuit if offline; log `Skipped_Offline` | — | — | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.7 | Skip circuit if already running; log `Skipped_AlreadyRunning` | — | — | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.8 | Record per-circuit execution result in `ScheduleExecutionLogs` | `ISchedulerRepository` | — | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.9 | View execution log filtered by schedule | `ISchedulerRepository` | `ISchedulerService.GetLogsAsync()` | `SchedulerService` | VAL-UI-008 | ✅ |
| FR-011.10 | Hangfire job ID stored on schedule for rescheduling / cancellation | `ISchedulerRepository` | — | `SchedulerService` | — | ✅ |


---

> **Amendment (v0.5 — May 2026):** Section 6 Validation Test Reference updated — VAL-UI-008 added for Scheduler feature.

| Test ID | Addition |
|---|---|
| VAL-UI-008 | Scheduler CRUD, toggle, and execution log — Manual UI |

