# Verification and Validation Plan (VVP)
**Document ID:** BTS-VVP-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Verification — Static Analysis

| Activity | Tool | Trigger |
|---|---|---|
| Build errors and warnings | dotnet build | Every commit |
| Nullable reference analysis | C# nullable | Every commit |
| CI build gate | GitHub Actions | Every push to main |
| Peer code review | GitHub PR | Before merge |

## 2. Unit Test Scope

| Module | Test Focus | Priority |
|---|---|---|
| DecoderService | All field offsets in 0xCC packet | P1 Critical |
| PacketAnalyzer | Packet building + CRC-16 vectors | P1 Critical |
| CRC-16 | Known test vectors | P1 Critical |
| DbcParser | Message and signal parsing | P1 Critical |
| BroadcastUdpService | Q1/Q4/Q5 encoding | P2 High |
| SqliteBulkDatabaseManager | Channel enqueue under burst | P2 High |
| Repository CRUD | EF Core operations | P2 High |

## 3. Hardware-in-Loop (HIL) Validation

| Test ID | Scenario | Pass Criteria |
|---|---|---|
| VAL-001 | TCP registration | Handler created, log shows Success |
| VAL-002 | Real-time dashboard | Values update within 500ms |
| VAL-003 | Session data recording | MeasurementData rows in session SQLite |
| VAL-004 | Device discovery | All devices appear in UI |
| VAL-005 | IP config push | Hardware re-registers with new IP |
| VAL-006 | Program upload and execute | Steps run, data recorded |
| VAL-007 | Calibration | Gain/offset stored in DB |
| VAL-008 | Pause and Continue | No data loss on resume |
| VAL-009 | Disconnect recovery | Streaming resumes after reconnect |
| VAL-010 | DBC CAN signal recording | DBC columns present in session SQLite |

## 4. UI Validation

| Test ID | Scenario | Pass Criteria |
|---|---|---|
| VAL-UI-001 | Login valid credentials | Dashboard accessible |
| VAL-UI-002 | Role access control | Admin pages blocked for Operator |
| VAL-UI-003 | Circuit ACL | User sees only assigned circuits |
| VAL-UI-004 | Audit log | All write operations logged |
| VAL-UI-005 | DataTable state persists | Page, columns, group expand restored after nav |
| VAL-UI-006 | Excel export | File opens correctly with all columns |
| VAL-UI-007 | Group expand/collapse | Message groups expand and collapse correctly |
| VAL-UI-008 | DBC signal checkbox | Select all under message works correctly |

## 5. Acceptance Criteria

System accepted for production release when:
1. All P1 unit tests pass
2. VAL-001 through VAL-009 pass on real hardware
3. All VAL-UI tests pass in Chrome and Edge
4. GitHub Actions CI/CD pipeline green end-to-end
5. Docker container starts cleanly from a fresh volume
6. Zero critical (P1) open bugs in GitHub Issues

---

> **Amendment (v0.5 — May 2026):** Section 4 UI Validation updated — VAL-UI-008 added for Scheduler feature.

| Test ID | Scenario | Pass Criteria |
|---|---|---|
| VAL-UI-008 | Scheduler — create, list, toggle active, delete | Schedule appears/disappears in list; IsActive toggle persists |
| VAL-UI-009 | Scheduler — execution log | Log rows appear per circuit after job fires; Status values correct |
