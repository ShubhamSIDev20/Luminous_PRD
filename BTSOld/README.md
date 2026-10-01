# 🔋 Battery Testing System (BTS) — v0.5

A production-grade **Blazor Server** application for real-time battery testing, monitoring, and session data management. BTS communicates with physical hardware over TCP/UDP, stores session measurements in per-session SQLite databases, and provides a live web dashboard.

---

## 📐 System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  BTS Server (Blazor)                    │
│                                                         │
│  ┌─────────────┐     ┌──────────────────────────────┐  │
│  │ CircuitMgr  │────▶│  CircuitCommandHandler (x N) │  │
│  │ (Background)│     │  per Device-Circuit pair     │  │
│  └──────┬──────┘     └──────────────┬───────────────┘  │
│         │                           │                   │
│  TCP :9999   UDP :10000   UDP :10001│                   │
│  Commands    RealTime     DataStore │                   │
│                                     ▼                   │
│  ┌──────────────┐    ┌─────────────────────────────┐   │
│  │  AppDbContext│    │ SqliteDbContext (per session)│   │
│  │  (Main DB)   │    │ SqliteBulkDatabaseManager   │   │
│  └──────────────┘    └─────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
          ▲ TCP/UDP            ▲ UDP
          │                   │
   ┌──────┴──────┐     ┌──────┴──────┐
   │ BTS Hardware│     │ BTS Hardware│
   │  Device 1   │     │  Device 2   │
   └─────────────┘     └─────────────┘
```

---

## 🌐 Network Ports

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| `5000` | TCP | Inbound | Blazor Web UI (HTTP) |
| `9999` | TCP | Inbound | Hardware Command & Control channel |
| `10000` | UDP | Inbound | Real-time data stream → live dashboard |
| `10001` | UDP | Inbound | Session measurement data → SQLite store |
| `10002` | UDP | Outbound | Device discovery broadcast (server → all devices) |
| `10003` | UDP | Inbound | Device discovery replies (hardware → server) |

> 📄 Full protocol specification: **[PROTOCOL.md](./PROTOCOL.md)**

---

## ⚙️ Core Components

### CircuitManager (`Services/CircuitManager.cs`)
The central `BackgroundService` that orchestrates all hardware communication. On startup it:
- Loads all registered Device-Circuit pairs from the main database
- Starts the **TCP Command Listener** on port `9999` — accepts hardware registration handshakes (`0xDD 0x01` header) and maps each physical device to a `CircuitCommandHandler`
- Starts the **UDP View Listener** on port `10000` — receives real-time data packets and pushes them live to the Blazor dashboard via `NotifyDataChanged`
- Starts the **UDP Store Listener** on port `10001` — receives session measurement packets and enqueues them via a `Channel<T>` for non-blocking async SQLite writes

### CircuitCommandHandler (`Services/Implementations/CircuitCommandHandler.cs`)
One handler instance exists per registered Device-Circuit pair. Responsibilities:
- Holds the active `TcpClient` connection to the physical hardware
- Maintains a `ConnectionAlive()` loop — monitors TCP health and detects disconnects
- Owns a `Channel<recordStoreRequest>` store queue — measurements are written to the session SQLite DB via `SqliteBulkDatabaseManager`
- Exposes `RealTime` record state for the live Blazor UI
- Manages calibration data buffer (last 3 calibration records)
- Sends notifications to the UI for hardware events and errors

---

## 🗄️ Databases

### AppDbContext (Main Application DB — SQLite/SQL Server)
Stores all persistent application configuration and identity data:

| Table | Purpose |
|-------|---------|
| `Users / Roles` | ASP.NET Identity authentication |
| `Devices / Circuits` | Registered hardware inventory |
| `BtsPrograms` | Test program definitions |
| `BatterySessions` | Session metadata (start/end/status) |
| `Batteries / BatteryTypes` | Battery master data |
| `RegistrationStandards` | Testing standard configurations |
| `CalibrationDataPoints` | Calibration history |
| `DbcFileRecords` | Uploaded DBC CAN files |
| `CodeMessages` | Hardware error code definitions |
| `AuditLogs` | User action audit trail |
| `UserCircuitAccess` | Per-user circuit access control |
| `ConfigurationEntity` | Runtime configuration store |

### SqliteDbContext + SqliteBulkDatabaseManager (Session DB)
Each test session gets its **own isolated SQLite file** stored at:
```
{AppSettings.Data}/{dd-MM-yyyy}/{SessionID}_{DeviceId}_{CircuitId}.db
```
Stores high-frequency `MeasurementData` records received over UDP port `10001`. Writes are batched and non-blocking via an internal `Channel<T>` queue to handle burst UDP traffic without data loss.

---

## 🚀 Deployment

### Option 1 — Docker Compose (Recommended)

```bash
# 1. Pull latest image
docker compose pull

# 2. Start the server
docker compose up -d

# 3. Access the web UI
open http://localhost:5000
```

**First run** will auto-create `./bts-data/` folder and apply database migrations automatically.

> ⚠️ Edit `docker-compose.yml` and set `AppSettings__DbEncryption` to your own secure key before first run.

---

### Option 2 — Manual / Direct Run

1. Download `BatteryTestingSystem-release.zip` from [GitHub Releases](https://github.com/sysin1/BatteryTestingSystem/releases)
2. Extract the ZIP
3. Edit `appsettings.json` — set your data path and DB encryption key:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\BTSData\\BtsAppdb.db"
  },
  "AppSettings": {
    "Data": "C:\\BTSData",
    "DbEncryption": "your-secure-key"
  }
}
```
4. Run the application:
```bash
dotnet BatteryTestingSystem.dll
```
5. Open browser at `http://localhost:5000`

---

### Option 3 — Docker Manual Run

```bash
docker run -d \
  --name battery-testing-system \
  -v /your/data/path:/app/config \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/config/BtsAppdb.db" \
  -e AppSettings__Data="/app/config" \
  -e AppSettings__DbEncryption="your-secure-key" \
  -p 5000:5000/tcp \
  -p 9999:9999/tcp \
  -p 10000:10000/udp \
  -p 10001:10001/udp \
  -p 10002:10002/udp \
  -p 10003:10003/udp \
  ghcr.io/sysin1/battery-testing-system:latest
```

---

## 🔧 Configuration Reference

| Environment Variable | Description | Example |
|---------------------|-------------|---------|
| `ASPNETCORE_URLS` | Web server listen URL | `http://+:5000` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ConnectionStrings__DefaultConnection` | Main app SQLite DB path | `Data Source=/app/config/BtsAppdb.db` |
| `AppSettings__Data` | Root folder for session DBs and logs | `/app/config` |
| `AppSettings__DbEncryption` | Encryption key for sensitive DB columns | `your-secure-key` |

---

## 📡 Device Discovery (Broadcast)

Before hardware can register via TCP, it must be discovered and configured on the network using **UDP broadcast** (SADP-style protocol).

```
Server                              Hardware Devices
  │                                        │
  │── Broadcast → 255.255.255.255:10002 ──▶│  Discover all devices
  │◀───────── Reply from device :10003 ────│  Device reports IP, ports, UniqueID
  │                                        │
  │── Broadcast → 255.255.255.255:10002 ──▶│  Q4: Set device IP config
  │── Broadcast → 255.255.255.255:10002 ──▶│  Q5: Set server IP + ports
  │                                        │
  │  [hardware now knows BTS server addr]  │
  │◀──── TCP Connect → :9999 ──────────────│  Registration begins
```

The **web UI** provides a Device Discovery page where you can:
- Scan the network and see all online BTS hardware devices
- View each device's current IP, MAC, firmware versions and port config
- Push new IP/network settings to a device (Q4)
- Push the BTS server address and ports to a device (Q5)

The server broadcasts on **all active network interfaces** simultaneously to work correctly in multi-NIC server environments.

> See [PROTOCOL.md](./PROTOCOL.md) for full broadcast packet formats.

---

## 🔌 Hardware Registration Flow

When a BTS hardware device powers on, it connects to the server on **TCP port 9999** and sends a registration packet (`0xDD 0x01` header). The server responds with one of:

| Status | Meaning |
|--------|---------|
| `Success` | First-time registration accepted, handler created |
| `AlreadyRegistered` | Reconnect of a known device, TCP client reassigned |
| `Failed` | Device not whitelisted in the application DB — admin must approve via the web UI |

After a successful handshake, the device starts streaming data on UDP ports `10000` and `10001`.

---

## 📦 CI/CD Pipeline

Every push to `main` automatically:

1. ✅ **Build** — Restores, builds and publishes the .NET 8 app
2. 📦 **Release** — Creates a versioned GitHub Release (`v0.5.x`) with a downloadable ZIP
3. 🐳 **Docker** — Builds and pushes image to `ghcr.io/sysin1/battery-testing-system:latest`

Download releases: [github.com/sysin1/BatteryTestingSystem/releases](https://github.com/sysin1/BatteryTestingSystem/releases)

Docker image: `ghcr.io/sysin1/battery-testing-system:latest`

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | Blazor Server (.NET 8) |
| Styling | Tailwind CSS |
| Main Database | SQLite (EF Core + Migrations) |
| Session Database | SQLite (per-session, bulk writer) |
| Authentication | ASP.NET Identity |
| Logging | Serilog (file + console + in-memory) |
| Hardware Comms | Raw TCP + UDP sockets |
| API | REST Controllers + MCP Server |
| Container | Docker / ghcr.io |

---

## 📁 Project Structure

```
BatteryTestingSystem/
├── Components/          # Blazor pages, layouts, UI components
├── Controllers/         # REST API controllers
├── Data/                # AppDbContext, SqliteDbContext, BulkManager
├── Models/              # Entities, DTOs, ViewModels, Enums
├── Repositories/        # Data access layer
├── Services/
│   ├── CircuitManager.cs          # Main background service (TCP/UDP)
│   └── Implementations/
│       └── CircuitCommandHandler.cs  # Per-device handler
├── Migrations/          # EF Core migrations (AppDbContext)
├── wwwroot/             # Static assets (CSS, JS)
├── Dockerfile
├── docker-compose.yml
└── appsettings.json
```
