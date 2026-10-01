# Architecture Design Document (ADD)
**Document ID:** BTS-ADD-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. System Architecture Overview

BTS follows a **Layered + Event-Driven** architecture running as a single ASP.NET 8 Blazor Server application. Communication with hardware is handled by a dedicated Background Service layer. The UI layer is Blazor Server — all rendering and state management run on the server side via SignalR.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        BTS Server Process                              │
│                                                                        │
│  ┌──────────────┐  ┌─────────────────────────────────────────────┐    │
│  │  Presentation │  │           Application Services              │    │
│  │  Layer        │  │                                             │    │
│  │  ─────────── │  │  CircuitManager (BackgroundService)          │    │
│  │  Blazor Pages │  │  ├── TCP Listener :9999                     │    │
│  │  Razor Comp.  │  │  ├── UDP View Listener :10000               │    │
│  │  SignalR Hub  │  │  └── UDP Store Listener :10001              │    │
│  │              │  │                                             │    │
│  │  REST API     │  │  CircuitCommandHandler (per Device-Circuit) │    │
│  │  Controllers  │  │  ├── TCP Client (hardware)                  │    │
│  │              │  │  ├── Channel<T> store queue                  │    │
│  │  MCP Server  │  │  └── RealTime state holder                  │    │
│  └──────┬───────┘  │                                             │    │
│         │          │  BroadcastUdpService (Device Discovery)      │    │
│  ┌──────▼───────┐  │  ├── UDP Broadcast :10002                   │    │
│  │  Business     │  │  └── UDP Reply :10003                      │    │
│  │  Logic Layer  │  └─────────────────────────────────────────────┘    │
│  │  Services     │                                                     │
│  │  Repositories │  ┌─────────────────────────────────────────────┐    │
│  └──────┬───────┘  │           Data Layer                         │    │
│         │          │  AppDbContext (SQLite/EF Core)               │    │
│  ┌──────▼───────┐  │  SqliteDbContext (per-session SQLite)        │    │
│  │  Data Layer   │  │  SqliteBulkDatabaseManager                  │    │
│  │  EF Core ORM  │  └─────────────────────────────────────────────┘    │
│  └──────────────┘                                                      │
└────────────────────────────────────────────────────────────────────────┘
         ▲ HTTP:5000          ▲ TCP:9999 / UDP:10000/10001/10002/10003
         │                   │
  [Browser / Client]    [BTS Hardware Devices]
```

---

## 2. Layer Descriptions

### 2.1 Presentation Layer
**Technology:** Blazor Server (.NET 8), Tailwind CSS, Highcharts  
**Components:**
- `Components/Pages/` — Full pages (Devices, Programs, Batteries, Settings, Reports)
- `Components/UI/` — 40+ reusable UI components (DataTable, Modal, Toast, WindowManager, Calibration, Dashboard, etc.)
- `Components/Layout/` — App shell (Navbar, MainLayout, WindowLayout)
- `Controllers/` — REST API endpoints (AuthController, DeviceController)
- `MCP/DeviceMcpTools.cs` — Model Context Protocol server for AI-driven device control

**Key design decisions:**
- All state lives on the server — Blazor SignalR pushes diffs to browser
- `ServerSessionStorageService` persists per-user UI state (pagination, column order, group expand state) across page navigations
- `WindowManager` component provides floating window / MDI-style UI

### 2.2 Application Services Layer
**Technology:** .NET 8 BackgroundService, System.Threading.Channels, System.Net.Sockets

#### CircuitManager
The central orchestration service. Runs as `IHostedService`. Manages:
- `TcpListener` on port 9999 — accepts hardware registration
- `UdpClient` on port 10000 — reads real-time data, routes to correct `CircuitCommandHandler`
- `UdpClient` on port 10001 — reads session data, routes to correct `CircuitCommandHandler` store queue
- All active `CircuitCommandHandler` instances in a `ConcurrentDictionary<string, ICircuitCommandHandler>`

#### CircuitCommandHandler
One instance per registered Device-Circuit pair. Owns:
- `TcpClient` connection to the hardware
- `Channel<recordStoreRequest>` — non-blocking session data write queue
- `RealTimeRecord` — latest measurement snapshot for Blazor UI polling
- Calibration data buffer (last 3 records)
- `ConnectionAlive()` background loop — monitors TCP heartbeat

#### BroadcastUdpService
Handles SADP-style network discovery:
- Broadcasts to all network interfaces simultaneously
- Parses `BroadcastDeviceInfo` replies
- Sends `BroadcastIpConfig` (Q4) and `BroadcastServerConfig` (Q5)

#### DecoderService
Stateless packet decoder:
- Decodes real-time `0xCC` packets (37–79+ bytes) into `RealTimeRecord`
- Decodes calibration `0xA0` packets into `CalibrationData`
- Decodes session store packets from `0xCC` format with opcode-tagged records

#### PacketAnalyzer / PacketAnalyzerNoReverse
Builds raw byte arrays for outgoing command packets with CRC-16/Modbus appended.

### 2.3 Business Logic Layer
**Technology:** Repository pattern, Service layer, DI

- `Services/Implementations/` — One service class per domain entity
- `Repositories/Implementations/` — Data access, extends generic `Repository<T>`
- `ServiceLocator` — Resolves scoped services from background threads safely

### 2.4 Data Layer
**Technology:** Entity Framework Core 8, SQLite (main + per-session), Microsoft.Data.Sqlite

#### AppDbContext (Main Database)
Single SQLite file at `{AppSettings.Data}/BtsAppdb.db`  
Schema managed via EF Core migrations (11 migrations to date)

#### SqliteDbContext + SqliteBulkDatabaseManager (Session Databases)
- One SQLite file per test session
- Path: `{Data}/{dd-MM-yyyy}/{SessionID}_{DeviceID}_{CircuitID}.db`
- Schema synced via `SqliteSchemaSync` — creates tables if missing, adds columns dynamically for DBC CAN signals
- `SqliteBulkDatabaseManager` uses `Channel<T>` + background writer for non-blocking batch inserts

---

## 3. Key Design Patterns

| Pattern | Where Used | Purpose |
|---|---|---|
| BackgroundService | CircuitManager | Long-running hardware listener lifecycle |
| Channel<T> | CircuitCommandHandler, BulkManager | Decouple receive from write; no data loss under burst |
| Repository | All data access | Abstraction over EF Core |
| Service Locator | Background threads | Resolve scoped DI services from singleton context |
| ConcurrentDictionary | CircuitManager handler map | Thread-safe handler registry |
| Observer / Event Bus | EventBusService | Decouple real-time UI updates from hardware layer |
| Cascade Parameters | Blazor UI components | Theme, layout, session state propagation |
| Generic DataTable | UI layer | Reusable sortable/searchable/exportable table |

---

## 4. Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET | 8.0 |
| UI Framework | Blazor Server | .NET 8 |
| CSS | Tailwind CSS | 3.x |
| Charts | Highcharts | via CDN |
| ORM | Entity Framework Core | 8.x |
| Main DB | SQLite | via EF Core |
| Session DB | SQLite | Microsoft.Data.Sqlite |
| Auth | ASP.NET Identity | .NET 8 |
| Logging | Serilog | latest |
| Serialization | Newtonsoft.Json + System.Text.Json | mixed |
| Excel Export | DocumentFormat.OpenXml | latest |
| API Protocol | MCP (Model Context Protocol) | via McpServer |
| Container | Docker | ghcr.io |
| CI/CD | GitHub Actions | ubuntu-latest |

---

## 5. Deployment Architecture

```
[GitHub Repository]
       │ push to main
       ▼
[GitHub Actions CI/CD]
  ├── dotnet build + publish
  ├── GitHub Release (ZIP)
  └── Docker push → ghcr.io/sysin1/battery-testing-system:latest

[Production Server]
  docker-compose up -d
       │
  ┌────▼────────────────────────┐
  │  bts-server container       │
  │  Port 5000 → Web UI         │
  │  Port 9999 → TCP Commands   │
  │  Port 10000 → UDP Realtime  │
  │  Port 10001 → UDP Store     │
  │  Port 10002 → UDP Broadcast │
  │  Port 10003 → UDP Discovery │
  │  Volume: ./bts-data:/app/config │
  └─────────────────────────────┘
```

---

## 6. Security Architecture

| Concern | Mechanism |
|---|---|
| Authentication | ASP.NET Identity, cookie-based session |
| Authorization | Role-based (Admin/Operator/Viewer) + circuit-level ACL |
| Password storage | PBKDF2 via Identity (bcrypt-equivalent) |
| Column encryption | Custom `EncryptionAttribute` + `IColumnEncryptionService` |
| Anti-forgery | Blazor antiforgery tokens |
| Audit | All write operations logged to `AuditLogs` table |
| CORS | Configured policy in `ServiceCollectionExtensions` |
