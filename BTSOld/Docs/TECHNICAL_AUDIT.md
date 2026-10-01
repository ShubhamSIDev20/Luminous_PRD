# Battery Testing System — Technical Audit Report
**Document ID:** BTS-AUDIT-001  **Version:** 1.0  **Date:** 2026-05-14
**Auditors:** Claude Code  **Scope:** Backend · Frontend · Scalability

---

## 1. Executive Summary

BTS is a production-grade Blazor Server application (.NET 8) for controlling battery testing hardware. It communicates with physical BTS hardware devices over TCP (command control) and UDP (real-time data + session storage), exposes a REST API and MCP interface, and stores data in per-session SQLite files. The codebase is well-structured for its current scope, but faces meaningful scalability constraints under high device/channel concurrency and has several code quality and security gaps that should be addressed before multi-tenant or large-scale deployment.

**Overall Assessment:** Capable single-instance system with clear architectural patterns. Needs hardening before scale.

| Area | Rating | Key Concern |
|---|---|---|
| Backend Architecture | 7 / 10 | Service Locator anti-pattern; Channel<T> sizing; no test suite |
| Frontend Architecture | 7 / 10 | Blazor Server stateful model; 40+ UI components; heavy SignalR |
| Data Layer | 6 / 10 | Per-session SQLite; no DB connection pooling; SchemaSync overhead |
| Security | 6 / 10 | RSA key in-memory; hardcoded encryption key; no rate limiting |
| Scalability | 5 / 10 | Single-instance; sticky sessions; UDP port exhaustion risk |
| Observability | 8 / 10 | Serilog dual-sink; in-memory log UI; CommandTracker |
| Hardware Communication | 7 / 10 | Robust Channel<T> decouple; TCP heartbeat; reconnect logic |
| API & Integration | 8 / 10 | REST + MCP clean separation; JWT auth; SSE live stream |
| DevOps / Deployment | 7 / 10 | Docker + compose; healthcheck; 7-day log retention |

---

## 2. Backend Architecture Audit

### 2.1 Application Layer

**Technology:** .NET 8 ASP.NET Core, Blazor Server, Tailwind CSS, Highcharts (CDN), Hangfire

#### Strengths
- Clean layered separation: Presentation → Services → Repositories → Data
- Dependency injection properly configured in `ServiceCollectionExtensions.cs`
- Background service pattern (`CircuitManager`) correctly uses `IHostedService` / `BackgroundService`
- `Channel<T>` correctly decouples UDP receive from SQLite write (non-blocking, burst-safe)
- `ConcurrentDictionary<string, ICircuitCommandHandler>` for thread-safe device registry
- REST API (`DeviceController`) and MCP tools (`DeviceMcpTools`) share business logic — no duplication
- Swagger documentation auto-generated
- Health checks registered (`AddDbContextCheck<AppDbContext>`)
- Hangfire dashboard with custom auth filter

#### Issues

**CRITICAL — Service Locator Anti-Pattern (Program.cs + CircuitManager.cs)**
```csharp
// Program.cs:120
ServiceLocator.SetProvider(app.Services);

// CircuitCommandHandler.cs:41
using var scope = ServiceLocator.CreateScope();
```
`CircuitManager` is registered as a **singleton** (`AddSingleton<CircuitManager>()`) but spawns **scoped** services via `ServiceLocator`. This is a well-known DI anti-pattern. It makes testing difficult, hides dependencies, and can produce subtle scoping bugs. Every `ServiceLocator.GetScoped<T>()` call in background threads should be replaced with proper injected `IServiceProvider` or use ` IServiceScopeFactory`.

**CRITICAL — RSA Key Not Persisted (ServiceCollectionExtensions.cs:30–39)**
```csharp
private static RSA? _sharedRsaKey;
private static RSA GetOrCreateRsaKey() {
    if (_sharedRsaKey == null) { _sharedRsaKey = RSA.Create(2048); }
    return _sharedRsaKey;
}
```
On app restart, a new RSA key is generated. This invalidates any previously issued JWT tokens. Key should be loaded from a file or key vault.

**CRITICAL — Hardcoded Encryption Key (appsettings.json:35)**
```json
"DbEncryption": "abcdefghijklmnopqrstuvwxyz"
```
The column encryption key is stored in plain text in `appsettings.json`. This file may be committed to version control. Must move to environment variable or secrets manager.

**HIGH — `AddSingleton` for CircuitCommandHandler Factory (ServiceCollectionExtensions.cs:76–79)**
```csharp
services.AddSingleton<Func<ICircuitCommandHandler>>(sp => () => new CircuitCommandHandler());
```
`CircuitCommandHandler` holds mutable state (TCP client, session data, Channels). Registering it as a singleton-produced factory while the handler itself manages disposable resources is risky. If the factory ever changes to return a cached instance, it will break. Document and guard this clearly.

**HIGH — No Unit/Integration Tests**
Zero `.test.cs` files found. Critical given:
- Binary protocol decoding (`DecoderService`) has complex parsing logic
- PRODUCER step expansion (`ProgramBuilder.ExpandProducerSteps`) has non-trivial branching
- Session self-containment serialization (SQLite) has multiple edge cases

**MEDIUM — UDP Port Static Allocation**
`CircuitManager` uses hardcoded port numbers (9999, 10000, 10001, 10002, 10003). For scaling to multiple instances, these would need configuration. Currently only one instance can run per host.

**MEDIUM — Unbounded `Channel<UdpReceiveResult>` (CircuitManager.cs:19–26)**
The `Channel.CreateUnbounded` for UDP packets has no back-pressure limit. Under extreme hardware burst, memory could grow unbounded. Consider `BoundedChannelOptions` with a configured max capacity.

**MEDIUM — `Channel<recordStoreRequest>` Per-Handler (CircuitCommandHandler.cs:30)**
Each `CircuitCommandHandler` creates its own unbounded `Channel`. With N circuits, there are N channels. Each reads from its own background task. This works for moderate scale but could cause thread-pool exhaustion at high channel counts.

**LOW — `Console.WriteLine` in Production Code (CircuitManager.cs:152, 182, 218)**
Multiple `Console.WriteLine` calls in `CircuitManager` bypass the Serilog pipeline. These should be replaced with `_log.Info/Debug`.

**LOW — Mixed JSON Serializers (Program.cs:16, 109)**
Both `Newtonsoft.Json` and `System.Text.Json` are used. `Newtonsoft.Json` is used in service layer (e.g., `CircuitCommandHandler.cs:9`). This adds a runtime dependency and potential serialization inconsistencies. Consider standardizing on one.

---

### 2.2 Hardware Communication Layer

#### CircuitManager
- **TCP Listener** (port 9999): Accepts hardware registration, parses 33-byte packets, responds with 5-byte status. Correctly handles AlreadyRegistered reconnect case.
- **UDP Listener (View)** (port 10000): Real-time data → routes to handler's `RealTime.NotifyDataChanged()` → Blazor SignalR push.
- **UDP Listener (Store)** (port 10001): Session data → `Channel.Writer.WriteAsync()` → background processor → SQLite bulk insert.
- **TCP Keep-Alive**: `SetSocketOption(KeepAlive, true)` on the listener socket. Good.
- **Device Reconnect**: Correctly handles reconnect by replacing old TCP client, closing stale connection.

#### CircuitCommandHandler
- **ConnectionAlive() loop**: 200ms polling interval for `_tcpClient.Available`. Works but is CPU-reactive. Consider `ReceiveAsync` with cancellation token.
- **TCP Response Wait**: 15-second timeout with `Task.WhenAny`. Reasonable.
- **Store Worker**: Background `ReadAllAsync` on channel. Clean pattern.
- **Session Self-Containment**: `ResolveProducerProgramsAsync` and `CollectTableFileData` correctly embed sub-program steps and TABLE file content into session SQLite at start time (FR-011). This is a well-designed feature.
- **DBC Transfer**: Chunked packet transfer with per-chunk hardware ACK. Correct protocol.

#### BroadcastUdpService
- Discovers devices via SADP broadcast on all network interfaces.
- Correctly handles Q4 (IP config) and Q5 (server config) push.

#### DecoderService
- Stateless packet decoder for binary protocols (0xDD, 0xAA, 0xBB, 0xEE, 0xA0, 0xCC).
- No test coverage on parsing edge cases.

---

### 2.3 REST API & MCP

**REST API** (`DeviceController`):
- All endpoints accept `List<CommonRequest>` for batch operations (good for multi-circuit control)
- JWT Bearer auth on `/api/*`; cookie auth on UI pages
- SSE endpoint (`GetLiveSSE`) for live data streaming
- Clean `CommonResponse<T>` envelope pattern

**MCP Server** (`/mcp`):
- Reuses `DeviceController.Core*` static helpers — zero duplication
- Stateless HTTP transport
- JWT auth wired

**Issues:**
- CORS allowed origins hardcoded in `appsettings.json` — should be environment-variable driven
- JWT token lifetime is 24 hours (SRS says 24h, appsettings says 60min for access token) — check consistency

---

## 3. Frontend Architecture Audit

### 3.1 Technology
- **Framework:** Blazor Server (.NET 8) — server-side rendering with SignalR
- **CSS:** Tailwind CSS v3 with `tailwindcss-animate`
- **Charts:** Highcharts via CDN
- **Components:** 40+ custom UI components in `Components/UI/`

### 3.2 Pages (Routes)
| Page | Purpose | Complexity |
|---|---|---|
| `DashboardView` | Real-time circuit monitoring, live charts | High |
| `TabView` | Per-circuit tabbed view | High |
| `DeviceList` | Device registration, status management | Medium |
| `ProgramList` / `ProgramEditor` | Program CRUD, step editing | High |
| `BatteriesList` / `DbcDatabaseEditor` | Battery + DBC file management | Medium |
| `Reports` | Excel export, session history | Medium |
| `DeviceDiscovery` | SADP network discovery UI | Medium |
| `SchedulerPage` | Program scheduling via Hangfire | Medium |
| `Users` | User management, role assignment | Low |
| `CANPortConfig` | DBC CAN signal configuration | Medium |
| `Designer` | Unknown — likely circuit/layout designer | ? |
| `ApplicationErrorLogs` / `LiveLogs` | Observability UI | Low |
| `Registrations` | Registration standards management | Low |
| `TableFileManager` | TABLE file upload/management | Low |
| `ErrorMsgConfig` | Error code configuration | Low |

### 3.3 UI Component Library

The `Components/UI/` folder contains 40+ Shadcn-inspired components:
`Accordion`, `Alert`, `Avatar`, `Badge`, `Card`, `Checkbox`, `ContextMenu`, `DataTable`, `Dialog`, `DropdownMenu`, `Form/*`, `Input`, `Modal`, `Pagination`, `Popover`, `Progress`, `RadioGroup`, `Search`, `Select`, `Separator`, `Sheet`, `Skeleton`, `Switch`, `Table`, `Tabs`, `TextArea`, `Toast/Toaster`, `Tooltip`, `TreeView`, `WindowManager`, `DynamicTable`, `Calibration`, `Dashboard/GridLayout`, `CircuitModels`

**Strengths:**
- Well-organized folder structure
- Most components have parameter-driven theming
- `WindowManager` enables MDI-style floating windows (good for multi-circuit monitoring)
- `DataTable` provides sortable/searchable/exportable table base
- Reusable `Form*` components with validation integration

**Issues:**

**HIGH — Blazor Server Stateful Model**
Blazor Server renders server-side and streams DOM diffs over SignalR. This means:
- Every connected browser tab holds a circuit connection in ASP.NET Core's circuit registry
- `DisconnectedCircuitRetentionPeriod = 3 minutes` (Program.cs:71) — disconnected circuits persist for 3 min
- No horizontal scaling without sticky sessions (Azure SignalR or Redis backplane required)
- Memory grows linearly with concurrent users

**MEDIUM — CDN Dependency for Highcharts**
Highcharts is loaded via CDN (no local fallback). If CDN is unreachable in production, charts fail silently. Consider bundling or a fallback.

**MEDIUM — Heavy Component Count**
40+ components in a mid-size project adds build overhead and potential for inconsistent prop naming. Some components appear to have overlapping functionality (Modal + Dialog + Sheet + WindowManager all serve overlay needs).

**MEDIUM — State Management**
Per-user UI state (pagination, column order, group expand) is persisted via `ServerSessionStorageService` into SQLite. This works but creates DB contention under high user load. Consider Redis or in-memory distributed cache.

**LOW — No Client-Side Validation**
Form validation appears server-side only. Client-side validation (using `DataAnnotations` with `EditForm`) would improve UX.

**LOW — Theme System**
`ThemeEnums` and cascading theme parameters exist but theme switching UI is not apparent in the page list. If dark/light mode is planned, ensure consistency across all 40+ components.

---

## 4. Data Layer Audit

### 4.1 Databases

**Main DB:** `BtsAppdb.db` (SQLite via EF Core)
- 11 migrations
- Entity Framework Core 8
- Schema: Devices, Circuits, Programs, Batteries, Sessions, DBC Files, Users, Roles, AuditLogs, Calibration, Configuration, Scheduler

**Session DBs:** Per-session SQLite files
- Path: `{Data}/sessions/{dd-MM-yyyy}/{SessionID}_{DeviceID}_{CircuitID}.db`
- `SqliteBulkDatabaseManager` + `SqliteSchemaSync` for dynamic schema
- `Channel<T>` decouples UDP receive from disk write
- Session self-containment: PRODUCER programs + TABLE files serialized into session DB (FR-011)

### 4.2 Strengths
- Per-session DB isolates data — no single massive session table
- Dynamic DBC column creation via `SqliteSchemaSync` handles CAN signal evolution
- Bulk insert pattern for high-frequency measurement data
- PRODUCER/TABLE self-containment is architecturally sound

### 4.3 Issues

**HIGH — SQLite Per-Connection Lock**
SQLite uses file-level write locking. Multiple concurrent writes (from multiple sessions or the bulk manager) cause lock contention. Under high-frequency data (say 100 Hz across 20 channels), SQLite will serialize writes, creating a bottleneck. Consider:
- Write coalescing (batch commits instead of per-record)
- SQLite WAL mode (`PRAGMA journal_mode=WAL`) — currently not confirmed
- PostgreSQL or SQL Server for production (documented limitation in SRS)

**HIGH — No Database Connection Pooling**
SQLite connections are created per-operation. Under load, connection overhead is significant. EF Core's built-in pooling is used but `SqliteDbContext` instances are created manually in `SqliteBulkDatabaseManager.GetContext()`. This bypasses some EF Core pooling benefits.

**MEDIUM — `EnsureCreated()` in GetContext (SqliteBulkDatabaseManager.cs:67)**
`context.Database.EnsureCreated()` is called on every session DB access. This is expensive. Should be called once per DB file lifetime, not on every read. `SqliteSchemaSync.SyncModelWithDatabase` is also called each time.

**MEDIUM — Session DB Cleanup**
No automatic cleanup of old session SQLite files. Over time, the `sessions/` directory grows unbounded. Need a retention policy (e.g., delete files older than N days).

**MEDIUM — EF Core Migrations in Docker**
Migrations run at startup (`Database.Migrate()` in Program.cs:129). In a zero-downtime deployment, this is risky — migrations may lock tables. Consider running migrations as a separate init container.

---

## 5. Security Audit

### 5.1 Authentication & Authorization
- **ASP.NET Identity** for UI (cookie-based)
- **JWT Bearer** for REST/MCP (24h TTL)
- **Roles:** Admin, Operator, Viewer
- **Circuit-level ACL:** `UserCircuitAccess` table per-user per-circuit control
- **Password policy:** PBKDF2, 8+ chars, upper+lower+digit required

### 5.2 Strengths
- Audit logging (`AuditLogs` table) records all write operations
- Column encryption attribute + service for sensitive data
- Anti-forgery tokens enabled
- CORS policy configured
- Hangfire dashboard has custom auth filter

### 5.3 Issues

**CRITICAL — Encryption Key in Config File**
`appsettings.json` contains `DbEncryption` key in plain text. Must use environment variable or Azure Key Vault / HashiCorp Vault.

**CRITICAL — RSA Key In-Memory**
New RSA key generated on every restart (see §2.1). All issued JWTs invalidated.

**HIGH — No Rate Limiting**
No rate limiting on API endpoints or auth endpoints. Susceptible to brute-force attacks on `/api/Auth/login`. Add `AspNetCoreRateLimit` or similar.

**HIGH — CORS Origins in Config**
`appsettings.json` lists allowed origins including `https://yourdomain.com` (placeholder). Review before production deployment.

**MEDIUM — No Password Complexity Beyond Length**
Password requires upper, lower, digit but does not check for common passwords, dictionary words, or entropy. Consider integrating `zxcvbn` or similar.

**MEDIUM — JWT Secret Key in Config**
`JwtSettings.SecretKey` is in `appsettings.json`. Should be in environment variable or secrets manager.

**MEDIUM — No Audit Log Integrity Protection**
`AuditLogs` records are writable by the application. A compromised admin account could modify logs. Consider write-once storage or cryptographic chaining.

**LOW — No HTTPS Enforcement**
`appsettings.json` does not enforce HTTPS. Docker compose does not redirect HTTP to HTTPS. Production deployment should enforce TLS.

**LOW — JWT `ValidateLifetime = true` but no refresh token rotation**
Refresh tokens exist in config but rotation is not evident. Stolen refresh tokens could be reused indefinitely.

---

## 6. Scalability Analysis

### 6.1 Horizontal Scaling Constraints

| Bottleneck | Severity | Impact |
|---|---|---|
| Blazor Server SignalR (sticky sessions) | CRITICAL | Cannot scale beyond 1 server without Redis backplane or Azure SignalR |
| SQLite file-level locking | HIGH | Session DB writes serialize under high-frequency multi-channel load |
| `CircuitManager` singleton | HIGH | All hardware handlers managed in a single process — no sharding |
| Static UDP port allocation (5 ports) | HIGH | Only one BTS instance per host; port conflicts on multi-instance deploy |
| `ServiceLocator` anti-pattern | MEDIUM | Makes it harder to extract hardware communication into a separate microservice |
| Per-session SQLite files | MEDIUM | Thousands of small DB files create filesystem overhead |
| Serilog rolling logs | MEDIUM | High-volume logging (real-time data logging) writes to same disk as session DBs |
| Hangfire SQLite storage | MEDIUM | Shared SQLite for job scheduling — potential lock contention |

### 6.2 Load Estimates (Current Architecture)

| Metric | Current Estimate | Break Point |
|---|---|---|
| Concurrent circuits | 10–50 | ~100 (SQLite write contention) |
| Data ingestion rate | ~10 records/sec/circuit at 10Hz | ~1000 records/sec total |
| Concurrent browser users | ~10–20 | ~50 (SignalR circuit registry memory) |
| Session DB files | Growing daily | Filesystem inode exhaustion over years |

### 6.3 Scalability Recommendations

**Near-term (Single-instance optimization):**
1. Enable SQLite WAL mode: `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;`
2. Implement session DB retention policy (auto-delete files > 90 days)
3. Configure `BoundedChannelOptions` with max capacity for UDP channels
4. Replace `Console.WriteLine` with Serilog throughout
5. Move secrets to environment variables
6. Add Redis backplane for SignalR (enables horizontal scaling)

**Medium-term (Multi-instance support):**
1. Extract hardware communication (`CircuitManager`) into a separate microservice or sidecar
2. Replace SQLite with PostgreSQL (per-session DB or consolidated schema)
3. Add API Gateway for load balancing across BTS instances
4. Implement WebSocket-aware load balancer for SignalR sticky sessions
5. Use a proper job queue (RabbitMQ / Azure Service Bus) instead of Hangfire SQLite

**Long-term (Multi-tenant / Cloud-native):**
1. Container orchestration (Kubernetes) for auto-scaling BTS instances
2. Time-series database (InfluxDB / TimescaleDB) for measurement data instead of per-session SQLite
3. Separate hot/warm/cold storage tiers for session data
4. MQTT or gRPC for hardware communication instead of raw TCP/UDP

---

## 7. Observability Audit

### 7.1 Logging
- **Serilog** with dual sink: Console + Rolling file (7-day retention)
- **InMemorySink** for UI-accessible logs (`InMemoryLogStore`)
- Structured logging with `{Properties:j}` in output template
- `CommandTracker` service for tracking issued commands (audit trail)
- Request logging middleware (commented out but available)

### 7.2 Metrics & Monitoring
- No Prometheus/ Grafana integration
- No health check detail beyond `AddDbContextCheck`
- No distributed tracing (no OpenTelemetry)

### 7.3 Recommendations
- Add OpenTelemetry for traces, metrics, and logs unification
- Expose Prometheus metrics endpoint (`/metrics`)
- Add structured health check per component (CircuitManager alive, UDP ports listening)

---

## 8. Deployment & DevOps Audit

### 8.1 Containerization
- **Dockerfile:** Multi-stage build (build → publish → final). Uses `mcr.microsoft.com/dotnet/aspnet:8.0-noble`. Correctly exposes all 5 ports.
- **docker-compose.yml:** Named volume `./bts-data:/app/config`. Health check via `curl`. `restart: unless-stopped`. Good production posture.

### 8.2 Issues

**HIGH — Migrations Run at Startup**
`Database.Migrate()` in `Program.cs:129` runs on every container start. In blue-green or rolling deployments, this can cause downtime if a migration locks the DB. Use a separate init container or migration job.

**MEDIUM — No `.dockerignore` or Build Cache Optimization**
Docker build copies everything. Large `node_modules/` and `HardwareSimulator/` directories are copied and then the build only uses the .NET project. These should be excluded in a `.dockerignore`.

**MEDIUM — Health Check Only Hits HTTP**
The health check (`curl http://localhost:5000/health`) only verifies the HTTP endpoint. It does not verify:
- UDP ports are listening
- CircuitManager has loaded devices
- Database migrations are applied

**LOW — No CI/CD Pipeline in Repository**
GitHub Actions workflow exists (`ADD.md` mentions GitHub Actions CI/CD) but no `.github/workflows/*.yml` found in the repository. Workflow should be committed to the repo for auditability.

---

## 9. Code Quality Audit

### 9.1 Positive Patterns
- Consistent `CommonResponse<T>` pattern across all services
- `recordRequest` / `recordStoreRequest` DTO pattern for hardware data
- Clean enum usage (`StartByte`, `ProgramControlQuery`, `CircuitStatus`, etc.)
- `ICommonResponse` interface pattern on repositories

### 9.2 Code Smells

**HIGH — Magic Numbers & Strings**
```csharp
// CircuitManager.cs:100
await Task.Delay(200, _cts.Token);  // Magic poll interval
// CircuitManager.cs:214
Task.WhenAll(tasks), Task.Delay(TimeSpan.FromSeconds(5))  // 5-second shutdown timeout
// CircuitCommandHandler.cs:297
var timeout = Task.Delay(TimeSpan.FromSeconds(15));  // 15-second TCP timeout
```
All timeouts and magic numbers should be extracted to `AppSettings` or constants.

**MEDIUM — Null Checks After ServiceLocator**
```csharp
using var scoped = ServiceLocator.GetScoped<IDeviceCircuitServices>();
var service = scoped.Service;
if (service == null) return;  // defensive but masks real issue
```
Null service from scoped resolution is a symptom of DI misconfiguration. Should not happen; if it does, throw rather than silently continue.

**MEDIUM — Commented-Out Code**
Multiple blocks of commented-out code (e.g., CircuitCommandHandler.cs:189–213, Program.cs:171–182). Should be removed via git history, not left in source.

**MEDIUM — Inconsistent Naming**
- `SqliteDbContext` vs `AppDbContext`
- `CircuitCommandHandler._StoreQueue` vs `CircuitManager._udpChannel`
- `BatterySession` vs `SessionRecordDto` — two session concepts, careful distinction needed
- `ServiceLocator.CreateScope()` vs standard `IServiceScopeFactory`

**LOW — Async/Void Fire-and-Forget**
```csharp
// CircuitManager.cs:452
_ = _udpChannel.Writer.WriteAsync(result);  // Fire-and-forget UDP enqueue
```
While safe for UDP, this pattern hides exceptions. Add error logging for the discarded task.

**LOW — Missing `volatile` / `volatile` used for signaling**
`CancellationTokenSource` fields (`CircuitCommandHandler._cts`) used for signaling across threads. `.Cancel()` is thread-safe but reading `_cts.IsCancellationRequested` without memory barriers may be stale on some architectures. Use `volatile` or check the token explicitly.

---

## 10. Summary of Findings

### Critical (Fix before production)
1. Move `DbEncryption` key out of `appsettings.json`
2. Persist RSA key across restarts
3. Replace `ServiceLocator` with proper injected `IServiceProvider` / `IServiceScopeFactory`
4. Add rate limiting to auth endpoints
5. Run EF Core migrations as a separate init step (not at startup)

### High (Fix before scaling)
6. Enable SQLite WAL mode + configure bounded channels
7. Add unit tests for `DecoderService`, `ProgramBuilder`, session self-containment
8. Add Redis SignalR backplane for horizontal scaling
9. Implement session DB retention/cleanup policy
10. Move all secrets to environment variables
11. Add `.dockerignore` to exclude node_modules, HardwareSimulator, test files

### Medium (Address in next sprint)
12. Standardize on one JSON serializer
13. Replace `Console.WriteLine` with Serilog throughout
14. Extract magic numbers/timeouts to configuration
15. Add OpenTelemetry tracing and metrics
16. Improve health checks to verify UDP listeners and CircuitManager state
17. Remove commented-out code blocks
18. Add Prometheus metrics endpoint

### Low (Technical debt)
19. Document component library design guidelines
20. Add client-side form validation
21. Add CDN fallback for Highcharts
22. Standardize naming conventions across backend and frontend
23. Consider migrating from Hangfire SQLite to a durable job queue
24. Investigate PostgreSQL as SQLite alternative for high-concurrency scenario

---

*Document ID: BTS-AUDIT-001 | Generated by: Claude Code | Based on: SRS.md v0.6, ICD.md v0.5, ADD.md v0.5, source code analysis*