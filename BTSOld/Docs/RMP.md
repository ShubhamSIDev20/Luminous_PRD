# Risk Management Plan (RMP)
**Document ID:** BTS-RMP-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Introduction

### 1.1 Purpose
This document identifies, analyzes, and defines mitigation strategies for risks associated with the development, deployment, and operation of the Battery Testing System (BTS) v0.5. It satisfies the CMMI Level 3 Risk Management (RSKM) process area requirements.

### 1.2 Scope
Risks are assessed across four domains:
- **Technical** — hardware communication, data integrity, software reliability
- **Operational** — deployment, network infrastructure, production environment
- **Project** — schedule, resource, and quality risks
- **Security** — authentication, data protection, network exposure

### 1.3 Risk Rating Methodology

**Probability:**

| Rating | Label | Definition |
|---|---|---|
| 1 | Rare | < 10% chance in project lifetime |
| 2 | Unlikely | 10–30% |
| 3 | Possible | 30–60% |
| 4 | Likely | 60–80% |
| 5 | Almost Certain | > 80% |

**Impact:**

| Rating | Label | Definition |
|---|---|---|
| 1 | Negligible | No effect on operations |
| 2 | Minor | Slight degradation, workaround available |
| 3 | Moderate | Noticeable impact, recovery < 1 day |
| 4 | Major | Significant data/operation loss, recovery 1–3 days |
| 5 | Critical | Total system failure or safety incident |

**Risk Score = Probability × Impact**

| Score | Level | Action Required |
|---|---|---|
| 1–4 | Low | Monitor |
| 5–9 | Medium | Mitigate |
| 10–16 | High | Mitigate urgently |
| 17–25 | Critical | Immediate action + escalation |

---

## 2. Risk Register

### 2.1 Technical Risks

| ID | Risk | Prob | Impact | Score | Level |
|---|---|---|---|---|---|
| T-01 | TCP connection drop from hardware mid-session | 4 | 4 | 16 | High |
| T-02 | UDP packet loss on high-frequency data stream (port 10000) | 3 | 3 | 9 | Medium |
| T-03 | SQLite session DB corruption under burst write load | 2 | 5 | 10 | High |
| T-04 | DBC CAN file parse failure for non-standard DBC dialects | 3 | 2 | 6 | Medium |
| T-05 | CRC-16 checksum mismatch causing silent data corruption | 2 | 4 | 8 | Medium |
| T-06 | Hardware firmware version mismatch with server protocol | 2 | 4 | 8 | Medium |
| T-07 | Blazor SignalR circuit disconnection under high load | 3 | 3 | 9 | Medium |
| T-08 | Server memory exhaustion from uncontrolled Channel<T> queue growth | 2 | 4 | 8 | Medium |
| T-09 | SQLite main DB (AppDbContext) lock contention under concurrent access | 2 | 3 | 6 | Medium |
| T-10 | Multi-NIC broadcast failure on device discovery | 2 | 3 | 6 | Medium |

### 2.2 Operational Risks

| ID | Risk | Prob | Impact | Score | Level |
|---|---|---|---|---|---|
| O-01 | Docker container restart losing in-memory session state | 3 | 3 | 9 | Medium |
| O-02 | Disk space exhaustion from growing per-session SQLite files | 3 | 4 | 12 | High |
| O-03 | Server NTP time drift causing incorrect session timestamps | 2 | 3 | 6 | Medium |
| O-04 | Network switch/VLAN misconfiguration blocking UDP broadcast | 2 | 4 | 8 | Medium |
| O-05 | Inadequate server hardware (CPU/RAM) for large device counts | 2 | 4 | 8 | Medium |
| O-06 | Docker volume mount misconfiguration losing session DBs | 2 | 5 | 10 | High |

### 2.3 Project Risks

| ID | Risk | Prob | Impact | Score | Level |
|---|---|---|---|---|---|
| P-01 | Hardware delivery delay blocking integration testing | 3 | 3 | 9 | Medium |
| P-02 | Key developer unavailability (single point of knowledge) | 2 | 4 | 8 | Medium |
| P-03 | Scope creep from additional hardware circuit types | 3 | 3 | 9 | Medium |
| P-04 | EF Core migration failure on production DB upgrade | 2 | 4 | 8 | Medium |
| P-05 | Third-party NuGet package breaking change on update | 2 | 3 | 6 | Medium |

### 2.4 Security Risks

| ID | Risk | Prob | Impact | Score | Level |
|---|---|---|---|---|---|
| S-01 | Unauthenticated REST API access from internal network | 2 | 5 | 10 | High |
| S-02 | JWT token interception on HTTP (non-HTTPS) deployment | 3 | 4 | 12 | High |
| S-03 | Brute-force attack on `/api/Auth/login` endpoint | 2 | 4 | 8 | Medium |
| S-04 | Rogue hardware device spoofing registration packet | 1 | 5 | 5 | Medium |
| S-05 | Sensitive DB encryption key exposed in environment variables | 2 | 5 | 10 | High |
| S-06 | Audit log tampering by privileged user | 1 | 4 | 4 | Low |

---

## 3. Mitigation Strategies

### T-01 — TCP Connection Drop (High)
**Mitigation:**
- `CircuitCommandHandler.ConnectionAlive()` loop actively monitors TCP health and detects disconnects within the keepalive interval.
- On disconnect, `CircuitStatus` is set to `Offline` and the UI is notified immediately.
- Hardware auto-reconnects and sends re-registration packet (`0xDD 0x02` → `AlreadyRegistered` response) without loss of session context.
- Running session is preserved in SQLite and resumes from last stored record on reconnect.

### T-02 — UDP Packet Loss (Medium)
**Mitigation:**
- Port 10000 (view) is best-effort — packet loss is acceptable as the UI refreshes on the next packet.
- Port 10001 (store) uses `Channel<T>` with bounded capacity; hardware retransmits unacknowledged store records.
- Monitor `Unstorerecordcount` in session state to detect persistent loss.

### T-03 — SQLite Session DB Corruption (High)
**Mitigation:**
- `SqliteBulkDatabaseManager` writes in transactions with WAL (Write-Ahead Logging) mode enabled.
- `Channel<T>` queue decouples UDP receive from disk write — burst traffic is buffered, not dropped.
- Daily backup of session DB directory via OS-level cron or Docker volume snapshot.
- `Storerecordcount` vs `Unstorerecordcount` in session state provides real-time integrity monitoring.

### T-04 — DBC Parse Failure (Medium)
**Mitigation:**
- `DbcDatabaseEditor` exposes `ParseErrors` panel listing all parse warnings.
- Invalid DBC files are rejected before upload; the existing DB record is preserved.
- DBC files are stored as raw binary in `DbcFileRecords` — reparse is possible after server updates.

### T-05 — CRC Mismatch (Medium)
**Mitigation:**
- All packets validated by CRC-16/Modbus before processing in `DecoderService`.
- Packets failing CRC are silently discarded and logged at Debug level.
- Hardware firmware validates server command CRC before acting.

### T-06 — Firmware Version Mismatch (Medium)
**Mitigation:**
- `QueryID 0x03` (Read Manufacturing Config) retrieves firmware version strings on registration.
- Version compatibility matrix maintained in `CodeMessages` table.
- Incompatible firmware triggers `CircuitStatus.Error` and operator notification.

### T-07 — SignalR Circuit Disconnection (Medium)
**Mitigation:**
- ASP.NET Core SignalR reconnect is enabled with exponential backoff.
- Server-side state (session, real-time data) is preserved between reconnections.
- `CircuitManager` background service operates independently of UI connections.

### T-08 — Memory Exhaustion from Queue (Medium)
**Mitigation:**
- `Channel<T>` created with `BoundedChannelOptions` — capacity limit enforced, oldest unprocessed records dropped under extreme load.
- Serilog memory sink has a fixed-size circular buffer (`InMemoryLogStore`).
- Docker container memory limit configured in `docker-compose.yml`.

### O-02 — Disk Space Exhaustion (High)
**Mitigation:**
- Session DB files are stored in dated subdirectories (`{AppData}/{dd-MM-yyyy}/`).
- Automated cleanup policy: sessions older than 90 days archived/deleted via scheduled task.
- Docker volume monitoring via `df -h` alert in CI/CD health check.
- Admin dashboard shows total session storage consumed.

### O-06 — Docker Volume Misconfiguration (High)
**Mitigation:**
- `docker-compose.yml` documents exact volume mount path: `./bts-data:/app/config`.
- Startup validation: application checks for writable `/app/config` directory and fails fast with a clear error message if absent.
- Volume path and encryption key verified in deployment checklist (see `PMP.md`).

### S-01 — Unauthenticated API Access (High)
**Mitigation:**
- All REST endpoints protected by `[Authorize]` attribute with JWT Bearer scheme.
- `/mcp` endpoint additionally mapped with `.RequireAuthorization()`.
- Only `/api/Auth/login` is `[AllowAnonymous]`.

### S-02 — JWT on HTTP (High)
**Mitigation:**
- Production deployment must use HTTPS with valid TLS certificate (Nginx reverse proxy recommended).
- `Strict-Transport-Security` header enforced.
- Internal-only deployments behind VPN or firewall are acceptable on HTTP.

### S-03 — Brute Force Login (Medium)
**Mitigation:**
- ASP.NET Identity `LockoutOptions` configured: 5 failed attempts → 15-minute lockout.
- Login failures logged via Serilog with IP address for monitoring.

### S-04 — Rogue Device Spoofing (Medium)
**Mitigation:**
- Registration response returns `Failed (0x00)` for any device not pre-approved in the `Devices` table.
- Admin must explicitly whitelist Device ID + Circuit ID pairs via web UI before hardware can register.
- MAC address logged on registration for audit.

### S-05 — Encryption Key Exposure (High)
**Mitigation:**
- `AppSettings__DbEncryption` must be set as a Docker secret or environment variable — never hardcoded.
- Key rotation procedure documented in `CMP.md`.
- `.env` files excluded from version control via `.gitignore`.

---

## 4. Risk Monitoring and Review

### 4.1 Review Cadence

| Activity | Frequency | Owner |
|---|---|---|
| Risk register review | Monthly | Project Manager |
| High/Critical risk review | Weekly | Lead Developer |
| New risk identification | Per sprint | Whole team |
| Post-incident risk update | After any incident | Lead Developer |

### 4.2 Risk Triggers and Escalation

| Trigger | Action |
|---|---|
| T-01: > 3 disconnects/hour on same device | Investigate hardware/network, escalate to HW team |
| O-02: Disk > 80% full | Immediate cleanup + capacity review |
| S-01/S-02: Unauthorized access attempt detected in logs | Security incident response, rotate JWT keys |
| T-03: `Unstorerecordcount` growing consistently | Investigate `SqliteBulkDatabaseManager`, check disk I/O |

### 4.3 Risk Closure Criteria
A risk is closed when:
- The mitigation has been implemented and verified in production for ≥ 30 days, OR
- The risk condition can no longer occur due to architectural changes, OR
- The risk has been formally accepted by the project manager with documented rationale.

---

## 5. Residual Risks

The following risks are accepted as residual after mitigation — probability and impact are reduced to acceptable levels:

| ID | Residual Risk | Accepted By |
|---|---|---|
| T-02 | < 0.1% UDP view packet loss is acceptable for live dashboard | Development Team |
| T-10 | Single-NIC environments have no discovery broadcast issue | Operations Team |
| S-06 | Audit log tampering requires admin DB access — acceptable with access controls | Security Lead |
| P-05 | NuGet breaking changes mitigated by pinned versions in `.csproj` | Development Team |

---

*Document ID: BTS-RMP-001 | Prepared by: BTS Development Team | CMMI Process Area: RSKM*
