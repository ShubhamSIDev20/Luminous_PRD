# ARCHITECTURE.md — System Design Overview
> Updated: 2026-08-11T00:00:00Z
> Project: WebAppME / BatteryTestingSystem

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    BatteryTestingSystem (ASP.NET Core 8)        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │  Blazor UI   │  │  REST API    │  │  MCP Server          │  │
│  │  (Server)    │  │  Controllers │  │  (DeviceMcpTools)    │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────┘  │
│         │                 │                      │             │
│         └─────────────────┼──────────────────────┘             │
│                           ▼                                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                   Service Layer                           │  │
│  │  ChannelManager ◄──► DeviceConnection ◄──► ChannelCmdHandler │
│  │  CircuitManager ◄──► IProgramServices, IBatteryServices   │
│  │  ExportJobService ◄──► Hangfire / IBackgroundJobClient    │
│  └──────────────────────────┬────────────────────────────────┘  │
│                             ▼                                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                   Data Layer                              │  │
│  │  AppDbContext (EF Core 8) ◄──► SQLite / SQLCipher        │
│  │  Identity (ApplicationUser, ApplicationRole)             │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Architectural Decisions

| Decision | Status | Reference |
|----------|--------|-----------|
| Single ASP.NET Core project (not clean architecture split) | **Implemented** | `docs/architecture/02-backend-clean-architecture-design.md` (design only) |
| Device-level connection multiplexing (1 TCP conn / device) | **Implemented** | `docs/superpowers/specs/2026-08-07-device-connection-multiplexing-design.md` |
| JWT auth defined but not wired in `Program.cs` | **Known Gap** | `AuthController.cs`, `Program.cs` |
| Hangfire for background export jobs | **Implemented** | `ExportController.cs`, `ExportJobService.cs` |
| Blazor Server (Interactive Server) for UI | **Implemented** | `Program.cs`, `Components/Pages/` |

---

## Component Overview

### Device Communication Stack (Multiplexed)
| Component | Role | Key File |
|-----------|------|----------|
| `ChannelManager` | Orchestrates per-device `DeviceConnection`; demultiplexes incoming packets | `Services/ChannelManager.cs` |
| `DeviceConnection` | Owns `TcpClient`; serializes writes; correlates responses by channel address byte | `Services/DeviceConnection.cs` |
| `ChannelCommandHandler` | Channel-slot; delegates I/O to `DeviceConnection`; retains session state | `Services/Implementations/ChannelCommandHandler.cs` |
| `ChannelManager.HandleCommandClientAsync` | Persistent per-device read loop; demultiplexes responses | `Services/ChannelManager.cs` |

**Multiplexing Invariants:**
- One `TcpClient` per physical device (up to 64 channels)
- `SemaphoreSlim` serializes all writes on the connection
- `ConcurrentDictionary<byte, TaskCompletionSource<byte[]>>` correlates responses by address byte
- Disconnecting the shared connection marks **all** channel slots for that device offline
- Channel state (session, program, etc.) survives reconnects
- Public `ChannelManager._devices` remains flat, keyed by `"deviceId-boardNumber-channelId"`

### REST API Controllers
| Controller | Auth | Purpose |
|------------|------|---------|
| `AuthController` | `[AllowAnonymous]` on `/login` | Login → JWT issuance |
| `ExportController` | `[Authorize]` | Hangfire-backed async export (request/status/download/delete) |
| `DeviceController` | **No `[Authorize]` currently** | Legacy hardware/session ops; reused by `DeviceMcpTools` |

### Background Jobs
| Component | Technology | Purpose |
|-----------|------------|---------|
| `ExportJobService` | Hangfire + `IBackgroundJobClient` | Async Excel export generation |
| `ExportController` | Hangfire | REST endpoints for export request/status/download |

### Data Layer
| Component | Technology | Key Entities |
|-----------|------------|--------------|
| `AppDbContext` | EF Core 8 + SQLite / SQLCipher | `Device`, `Channel`, `SecondaryBoard`, `BatterySession`, `Batteries`, `BtsPrograms`, `ExportRecord`, `ApplicationUser`, `ApplicationRole`, `AuditLog`, `CalibrationDataPoint`, `DbcFileRecord`, `ProgramSchedule`, `ScheduleExecutionLog`, `UserCircuitAccess`, `RegistrationStandard`, `BatteryType`, `ConfigurationEntity`, `CodeMessage` |

### Authentication
- ASP.NET Core Identity (`ApplicationUser`, `ApplicationRole`)
- Cookie/Identity auth currently wired in `Program.cs`
- `AuthController` issues JWTs via `JwtSettings` but `AddJwtAuthentication` **not called** in `Program.cs`
- `ExportController` uses `[Authorize]`; `DeviceController` has no `[Authorize]`

### Background Processing
- Hangfire with SQLite storage
- `IBackgroundJobClient` for fire-and-forget
- `ExportJobService` handles Excel generation via `ClosedXML`/`OpenXML`

### UI Layer
- Blazor Server with Interactive Server rendering
- Razor Components under `Components/Pages/`:
  - `Devices/` → DeviceList, DeviceDetail
  - `Batteries/` → BatteryList, BatteryDetail
  - `Programs/` → ProgramList, ProgramEditor
  - `Reports/` → Reports
  - `Settings/` → Users, Settings
  - `Home/` → Dashboard
  - `Test/` → Test pages

### MCP Integration
- `DeviceMcpTools.cs` exposes device operations to MCP clients
- Reuses `DeviceController.CoreSendProgram`, `CoreStart`, `CoreStop`, etc.

---

## Data Flow: Device Command (Multiplexed)

```
Blazor UI / REST / MCP
        │
        ▼
ChannelManager.GetOrCreateDeviceConnection(deviceId)
        │
        ▼
DeviceConnection.SendAndWaitAsync(channelAddress, commandBytes)
        │
        ├──► SemaphoreSlim.WaitAsync()  (serialize write)
        │
        ├──► TcpClient.WriteAsync(commandBytes)
        │
        ├──► Register TaskCompletionSource keyed by channelAddress
        │
        ▼
[Network] ──► ChannelManager.HandleCommandClientAsync (persistent per-device read loop)
        │
        ├──► stream.ReadAsync(buffer)  [buffer = 1024 bytes — must fit the
        │      largest response type (calibration payload = 163 bytes);
        │      undersizing this truncates a packet and corrupts the address
        │      byte used to route every packet after it — see 2026-08-11 fix]
        │
        ├──► Read packet → extract addressByte (buffer[2])
        │
        ├──► deviceConnection.HandleIncomingPacket(addressByte, payload)
        │      └─► _pendingResponses.TryGetValue(addressByte, out tcs) → tcs.TrySetResult(payload)
        │
        ▼
DeviceConnection.SendAndWaitAsync returns responseBytes
        │
        ▼
ChannelCommandHandler processes response → updates session state
```

---

## Known Gaps / Risks

| Area | Issue | Impact |
|------|-------|--------|
| Auth | `AddJwtAuthentication` defined but not called in `Program.cs` | JWTs issued but not validated on protected endpoints |
| Auth | `DeviceController` has no `[Authorize]` | Device ops accessible without auth |
| DI | `AddControllers` called twice in `Program.cs` (2nd sets `ReferenceHandler.IgnoreCycles`) | Potential duplicate registration |
| Config | `appsettings.json` contains secret-shaped values (DB password, encryption key, JWT settings) | Must not commit real secrets; use user secrets / env vars in prod |
| Architecture | Clean-architecture split documented but **not implemented** | Current code remains single-project; future split must preserve `DeviceController` and Blazor-facing services |

---

## Deployment Topology

- **Runtime**: ASP.NET Core 8 on Windows (development) / Linux (production target)
- **Database**: SQLite file (`D:\MEWebApp\BtsAppdb.db`) with optional SQLCipher encryption
- **Background Jobs**: Hangfire SQLite storage (same DB or separate)
- **Logging**: Serilog → console + file
- **CORS**: Configured for localhost dev origins + production domain
- **Health Checks**: Enabled (default ASP.NET Core)

---

## Future Architecture (Designed, Not Implemented)

Per `docs/architecture/02-backend-clean-architecture-design.md`:

```
Proposed Split (not yet implemented):
├── BatteryTestingSystem.Domain          # Entities, value objects, domain events
├── BatteryTestingSystem.Application     # Use cases, DTOs, interfaces
├── BatteryTestingSystem.Infrastructure  # EF Core, Hangfire, Device comms, Identity
├── BatteryTestingSystem.Api             # Controllers, MCP, minimal APIs
└── BatteryTestingSystem.Web             # Blazor Server (consumes Api)
```

**Coexistence Rule**: Legacy `DeviceController` and Blazor-facing hardware services (`IProgramServices`, `IBatteryServices`, `IDeviceChannelServices`, `IDbcService`) must be preserved during transition.