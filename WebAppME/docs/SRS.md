# Software Requirements Specification (SRS)
**Document ID:** BTS-SRS-001  **Version:** 0.6  **Date:** May 2026  **Status:** Approved

---

## 1. Introduction

### 1.1 Purpose
This document specifies the functional and non-functional requirements for the Battery Testing System (BTS) — a production-grade Blazor Server application used to control, monitor, and record battery testing operations using physical hardware circuits over TCP/UDP communication.

### 1.2 Scope
BTS provides a centralized web-based platform for:
- Discovering, configuring and registering BTS hardware devices over the local network
- Uploading and executing test programs on hardware circuits
- Real-time monitoring of measurement data during test execution
- Storing high-frequency session measurement data in per-session SQLite databases
- Managing batteries, test standards, calibration, DBC CAN files, users and audit logs

### 1.3 Definitions

| Term | Definition |
|---|---|
| Circuit | A single testing channel on a BTS hardware device |
| Device | Physical BTS hardware unit (may contain 1 or 2 circuits) |
| Session | A single test run on a circuit for a battery |
| Program | A sequence of test steps (charge/discharge/rest/cycle etc.) |
| DBC File | CAN Database file defining CAN message/signal mappings |
| Registration | TCP handshake by which hardware announces itself to BTS server |
| SADP | Simple Automatic Device Protocol — UDP broadcast used for discovery |

### 1.4 References
- PROTOCOL.md — BTS Hardware Communication Protocol
- README.md — System Overview

---

## 2. Overall Description

### 2.1 System Context
BTS operates as a server on the local network. Hardware devices communicate with it via TCP (commands, registration) and UDP (data streaming). Operators access BTS via any browser on the network.

```
[Browser / Operator] ──HTTP:5000──▶ [BTS Server]
                                         │
                         ┌───────────────┼───────────────┐
                    TCP:9999        UDP:10000         UDP:10001
                    Commands        RealTime         DataStore
                         │               │               │
                    [BTS Hardware Device 1..N]
```

### 2.2 User Classes

| Role | Description |
|---|---|
| Administrator | Full access: manage devices, users, programs, calibration, settings |
| Operator | Start/stop sessions, view real-time data, view reports |
| Viewer | Read-only access to data and reports |

---

## 3. Functional Requirements

### 3.1 Device Discovery (FR-001)
- FR-001.1: System SHALL broadcast UDP packets on port 10002 to all network interfaces to discover BTS hardware
- FR-001.2: System SHALL receive and parse device replies on port 10003 (UniqueID, IP, MAC, ports)
- FR-001.3: System SHALL allow administrators to push IP configuration (Q4) to a discovered device
- FR-001.4: System SHALL allow administrators to push BTS server address and ports (Q5) to a discovered device
- FR-001.5: System SHALL display discovered devices in a live-updating web UI table

### 3.2 Device Registration (FR-002)
- FR-002.1: System SHALL accept TCP connections from hardware on port 9999
- FR-002.2: System SHALL parse the 33-byte registration packet (DeviceID, CircuitID, DeviceName, IP, MAC)
- FR-002.3: System SHALL respond Success (0x01) if device is whitelisted in the database
- FR-002.4: System SHALL respond AlreadyRegistered (0x02) on reconnect — reassigning the TCP client
- FR-002.5: System SHALL respond Failed (0x00) if device is not in the database
- FR-002.6: System SHALL maintain one ChannelCommandHandler per registered Device-Circuit pair

### 3.3 Real-Time Monitoring (FR-003)
- FR-003.1: System SHALL receive UDP packets on port 10000 and push measurements live to the Blazor dashboard
- FR-003.2: Dashboard SHALL display: Voltage, Current, Temperature, Power, Capacity, Energy, Status, Errors, IO states
- FR-003.3: System SHALL detect calibration live data packets (0xA0) and route them separately from normal data
- FR-003.4: Dashboard SHALL update without full page refresh (Blazor SignalR push)

### 3.4 Session Data Recording (FR-004)
- FR-004.1: System SHALL receive UDP packets on port 10001 and decode measurement records
- FR-004.2: System SHALL write measurement records asynchronously to a per-session SQLite database
- FR-004.3: Session database file path SHALL follow format: `{Data}/{dd-MM-yyyy}/{SessionID}_{DeviceID}_{CircuitID}.db`
- FR-004.4: System SHALL use a `Channel<T>` queue to decouple UDP receive from disk write
- FR-004.5: System SHALL support DBC CAN signal decoding within session data packets

### 3.5 Program Management (FR-005)
- FR-005.1: System SHALL allow creation and editing of test programs with multiple steps
- FR-005.2: Program steps SHALL support types: Charge CC/CV, Discharge CC/CV, Rest, Cycle, CCCV, etc.
- FR-005.3: System SHALL upload programs to hardware via TCP (0xBB protocol)
- FR-005.4: System SHALL support DBC CAN file upload (0xBB, Q7/Q8)
- FR-005.5: System SHALL send Start, Stop, Pause, Continue commands (0xEE protocol)
- FR-005.6: System SHALL support a **PRODUCER** operator (code 20) that embeds another saved program's steps inline at hardware transfer time
- FR-005.7: PRODUCER step SHALL present a dropdown of all available programs (excluding the program being edited) in the Nominal Values cell
- FR-005.8: PRODUCER step SHALL provide an eye button to open a read-only preview of the referenced sub-program's steps
- FR-005.9: System SHALL validate that a PRODUCER step references a non-empty, existing program name before allowing program transfer
- FR-005.10: At hardware transfer (`SetProgramAsync`), PRODUCER steps SHALL be expanded: the referenced program's inner steps (skipping its first SET and last STO) are inlined and all step numbers renumbered sequentially
- FR-005.11: GOTO actions using step-number references within a PRODUCER sub-program MAY produce incorrect jump targets after expansion; label-based GOTO is preferred inside sub-programs

### 3.6 Calibration (FR-006)
- FR-006.1: System SHALL support 20 calibration query types (0xA0 protocol)
- FR-006.2: System SHALL store calibration data points in the database
- FR-006.3: System SHALL retrieve last 3 calibration records from hardware (Q14)

### 3.7 Battery & Standard Management (FR-007)
- FR-007.1: System SHALL allow CRUD for Battery records (type, capacity, chemistry, serial no.)
- FR-007.2: System SHALL allow CRUD for Registration Standards
- FR-007.3: System SHALL link batteries to test sessions

### 3.8 User & Access Management (FR-008)
- FR-008.1: System SHALL use ASP.NET Identity for authentication (email/password)
- FR-008.2: System SHALL support three roles: Administrator, Operator, Viewer
- FR-008.3: System SHALL enforce per-user circuit access control (UserCircuitAccess)
- FR-008.4: System SHALL record all user actions in AuditLogs

### 3.9 DBC CAN File Management (FR-009)
- FR-009.1: System SHALL allow upload of DBC CAN files
- FR-009.2: System SHALL parse DBC files (messages, signals, baudrate, port)
- FR-009.3: System SHALL allow selection of signals for recording and display
- FR-009.4: System SHALL allow grouping and collapsible view of messages/signals

### 3.10 Reporting (FR-010)
- FR-010.1: System SHALL provide session data export to Excel
- FR-010.2: System SHALL provide audit log viewing with filters
- FR-010.3: Session list SHALL provide a Session Properties panel showing: program name, battery name, device/circuit, start/end times, duration, embedded PRODUCER sub-programs, and captured TABLE files

### 3.11 Session Self-Containment (FR-011)
- FR-011.1: At session start, the system SHALL serialise the full step definition of every PRODUCER-referenced sub-program into the session SQLite file (`ProducerProgram_{name}` configuration key)
- FR-011.2: At session start, the system SHALL serialise the raw content of every TABLE file referenced in the program into the session SQLite file (`TableFile_{fileName}` configuration key)
- FR-011.3: The offline session viewer (BmsDashboard) SHALL load PRODUCER sub-program steps from the session DB, not the live program database
- FR-011.4: The offline session viewer SHALL load TABLE file content from the session DB, not the filesystem
- FR-011.5: PRODUCER step rows in the offline viewer SHALL show an eye button that opens the sub-program's steps in a read-only modal
- FR-011.6: TABLE step rows in the offline viewer SHALL show a button that opens the TABLE file content in a read-only modal

---

## 4. Non-Functional Requirements

| ID | Category | Requirement |
|---|---|---|
| NFR-001 | Performance | UDP data ingestion must handle burst packets without data loss (Channel<T> buffer) |
| NFR-002 | Performance | Blazor dashboard refresh latency < 500ms from packet receipt |
| NFR-003 | Availability | System shall auto-restart on crash (Docker restart: unless-stopped) |
| NFR-004 | Security | All passwords hashed via ASP.NET Identity (PBKDF2) |
| NFR-005 | Security | Sensitive DB columns encrypted at rest (EncryptionAttribute) |
| NFR-006 | Scalability | Architecture shall support N hardware devices concurrently |
| NFR-007 | Portability | Application shall run on Docker (Linux container) |
| NFR-008 | Maintainability | All database changes managed via EF Core migrations |
| NFR-009 | Observability | Serilog rolling-file logs retained 7 days, in-memory logs accessible in UI |
| NFR-010 | Browser Support | Chrome, Edge, Firefox — latest 2 versions |
