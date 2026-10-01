# Interface Control Document (ICD)
**Document ID:** BTS-ICD-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Introduction

### 1.1 Purpose
This document defines all interfaces between the Battery Testing System (BTS) server and external systems — including BTS hardware devices, REST API consumers, MCP tool clients, and the Blazor web UI. It is the authoritative reference for any system integrating with BTS.

### 1.2 Scope
Interfaces covered:

| Interface | Direction | Protocol |
|---|---|---|
| Hardware Command & Control | Hardware ↔ Server | TCP :9999 |
| Device Discovery & Configuration | Server → All Devices (broadcast) | UDP :10002/:10003 |
| Real-Time Data Stream | Hardware → Server | UDP :10000 |
| Session Data Store | Hardware → Server | UDP :10001 |
| REST API | Client → Server | HTTP/HTTPS |
| MCP Tool Interface | LLM/AI Client → Server | HTTP SSE (MCP protocol) |
| Blazor Web UI | Browser ↔ Server | SignalR (WebSocket) |

### 1.3 References
- `PROTOCOL.md` — Full binary packet specification
- `SRS.md` — System requirements
- `ADD.md` — Architecture design

---

## 2. Interface Summary Diagram

```
                    ┌─────────────────────────────────────────┐
                    │           BTS Server                    │
                    │                                         │
  Browser ──SignalR─┤  Blazor UI (port 5000)                  │
                    │                                         │
  REST Client ──────┤  POST /api/Device/*   (JWT Auth)        │
  MCP/AI Client ────┤  GET/SSE /mcp         (JWT Auth)        │
                    │                                         │
  BTS Hardware ─TCP─┤  CircuitManager :9999 (Registration)    │
  BTS Hardware ─UDP─┤  UDP Listener :10000  (RealTime View)   │
  BTS Hardware ─UDP─┤  UDP Listener :10001  (Session Store)   │
                    │                                         │
  Server ─Broadcast─┤  UDP :10002 → Devices (Discovery)      │
  Devices ──────────┤  UDP :10003 ← Replies (Discovery)      │
                    └─────────────────────────────────────────┘
```

---

## 3. Hardware TCP Interface — Port 9999

### 3.1 Overview
BTS hardware devices initiate a TCP connection to the server on port 9999 after network configuration via device discovery. This persistent connection carries all command/response traffic for the lifetime of the device session.

### 3.2 General Frame Format

All packets share a common 4-byte header followed by a payload and a 2-byte CRC-16/Modbus checksum.

**Command Frame (Server → Hardware):**
```
┌──────────┬──────────┬──────────┬──────────┬─────────────┬──────────┐
│ StartByte│ DeviceID │ CircuitID│ QueryID  │   Payload   │  CRC-16  │
│  1 byte  │  1 byte  │  1 byte  │  1 byte  │   N bytes   │  2 bytes │
└──────────┴──────────┴──────────┴──────────┴─────────────┴──────────┘
```

**Response Frame (Hardware → Server):**
```
┌──────────┬──────────┬──────────┬──────────┬─────────────┬──────────┐
│ StartByte│ DeviceID │ CircuitID│ QueryID  │ Status/Data │  CRC-16  │
│  1 byte  │  1 byte  │  1 byte  │  1 byte  │   N bytes   │  2 bytes │
└──────────┴──────────┴──────────┴──────────┴─────────────┴──────────┘
```

**CRC-16/Modbus:** polynomial `0xA001`, init `0xFFFF`, covers all bytes except the 2 CRC bytes, appended Little Endian.

### 3.3 Start Byte (Command Group Identifier)

| Start Byte | Group | Purpose |
|---|---|---|
| `0xDD` | Registration | Device registration handshake |
| `0xAA` | Configuration | Hardware configuration read/write |
| `0xBB` | Program | Test program upload |
| `0xEE` | Control | Program execution control |
| `0xA0` | Calibration | Calibration operations |

### 3.4 Registration Interface (`0xDD`)

**Registration packet (Hardware → Server) — 33 bytes:**

| Offset | Size | Field | Description |
|---|---|---|---|
| 0 | 1B | Start | `0xDD` |
| 1 | 1B | SubCmd | `0x01` = Register |
| 2 | 1B | Reserved | `0x00` |
| 3 | 1B | DeviceID | Hardware device index |
| 4 | 1B | CircuitID | Circuit number on device |
| 5 | 16B | DeviceName | ASCII null-padded |
| 21 | 4B | IPAddress | Device IP (4 octets) |
| 25 | 6B | MACAddress | Hardware MAC address |
| 31 | 2B | CRC-16 | Checksum |

**Registration response (Server → Hardware) — 5 bytes:**

| Offset | Size | Field | Values |
|---|---|---|---|
| 0 | 1B | Start | `0xDD` |
| 1 | 1B | DeviceID | Echo |
| 2 | 1B | CircuitID | Echo |
| 3 | 1B | Status | `0x01`=Success, `0x02`=AlreadyRegistered, `0x00`=Failed |
| 4 | 1B | Reserved | `0x00` |

### 3.5 Configuration Interface (`0xAA`)

| QueryID | Name | Direction | Description |
|---|---|---|---|
| `0x01` | HW Ready | S→HW | Poll readiness before any operation |
| `0x02` | Read Factory Config | S→HW | Read network + limits config |
| `0x03` | Read Manufacturing | S→HW | Read FW versions, serial numbers, dates |
| `0x04` | Read Battery Params | S→HW | Read current battery configuration |
| `0x05` | Write Battery Params | S→HW | Push battery configuration |
| `0x06` | Sync Time | S→HW | Synchronise RTC to server time |

### 3.6 Program Interface (`0xBB`)

| QueryID | Name | Description |
|---|---|---|
| `0x01` | HW Ready for Program | Confirm hardware ready to receive |
| `0x03` | Send Steps Count | Send total step count |
| `0x04` | Send Program | Upload program step binary data |
| `0x07` | Send DBC Steps Count | Send DBC channel count |
| `0x08` | Send DBC File | Upload DBC CAN file binary |

### 3.7 Control Interface (`0xEE`)

| QueryID | Name |
|---|---|
| `0x01` | Start program |
| `0x02` | Stop program |
| `0x03` | Pause program |
| `0x04` | Continue (resume) program |
| `0x05` | Sync time |
| `0x06` | System reset |

### 3.8 Calibration Interface (`0xA0`)

| QueryID | Name |
|---|---|
| `0x01` | Is Ready |
| `0x02` | Send Live Calibration Data |
| `0x03`–`0x0E` | Low Point / High Point / Gain+Offset per channel |
| `0x0F` | Cancel Calibration |
| `0x10` | Stop Calibration |
| `0x11`–`0x13` | Verify operations |
| `0x14` | Read Previous Calibration |

---

## 4. Device Discovery Interface — UDP :10002 / :10003

### 4.1 Overview
Before TCP registration, devices are discovered and configured over UDP broadcast. The server broadcasts on all active network interfaces simultaneously.

| Port | Role | Direction |
|---|---|---|
| `10002` | Discovery commands | Server → 255.255.255.255 (broadcast) |
| `10003` | Device replies | Device → Server (unicast) |

### 4.2 Discovery Query Types

**Q1 — Discover All Devices**
Server broadcasts discovery request; all online devices reply with their full network configuration (`BroadcastDeviceInfo`):

| Field | Size | Description |
|---|---|---|
| UniqueID | 4B | Hardware unique identifier |
| DeviceIP | 4B | Current device IP |
| SubnetMask | 4B | Subnet mask |
| Gateway | 4B | Default gateway |
| DNS1 / DNS2 | 4B each | DNS servers |
| RemoteIP | 4B | BTS server IP currently configured |
| TcpPort | 2B | Command port (should be 9999) |
| UdpLivePort | 2B | Real-time data port (should be 10000) |
| UdpRegPort | 2B | Session store port (should be 10001) |

**Q4 — Set Device IP Configuration (`BroadcastIpConfig`)**

| Field | Size |
|---|---|
| UniqueID | 4B |
| DeviceIP | 4B |
| SubnetMask | 4B |
| Gateway | 4B |
| DNS1 / DNS2 | 4B each |

**Q5 — Set Server Configuration (`BroadcastServerConfig`)**

| Field | Size |
|---|---|
| UniqueID | 4B |
| RemoteIP | 4B |
| TcpPort | 2B |
| UdpLivePort | 2B |
| UdpRegPort | 2B |

---

## 5. Real-Time Data Stream — UDP :10000

### 5.1 Overview
Hardware streams live measurement data continuously once registered. The server's `CircuitManager` receives packets on port `10000` and pushes updates to the Blazor UI via SignalR.

### 5.2 Measurement Packet (`0xCC`) — 79 bytes

| Offset | Size | Field | Type | Description |
|---|---|---|---|---|
| 0 | 1B | Start | byte | `0xCC` |
| 1 | 1B | DeviceID | byte | |
| 2 | 1B | CircuitID | byte | |
| 3 | 1B | QueryID | byte | |
| 4 | 2B | StepNumber | int16 | Current program step |
| 6 | 1B | ProgramStatus | enum | `0x00`=Stop, `0x01`=Running |
| 7 | 1B | CircuitStatus | enum | See §9.1 |
| 8 | 1B | ErrorId | byte | Hardware error code |
| 9 | 4B | SystemErrorId | int32 | Bitmask (see §9.2) |
| 13 | 4B | StepRunningTime | int32 | Step elapsed ms |
| 17 | 4B | RunningTime | int32 | Total elapsed ms |
| 21 | 4B | Current | float | Amperes (A) |
| 25 | 4B | Voltage | float | Volts (V) |
| 29 | 4B | Temperature | float | Celsius (°C) |
| 33 | 4B | Power | float | Watts (W) |
| 37 | 4B | AccumulatedCapacity | float | Ah |
| 41 | 4B | ChargeCapacity | float | Ah |
| 45 | 4B | DischargeCapacity | float | Ah |
| 49 | 4B | StepCapacity | float | Ah |
| 53 | 4B | AccumulatedEnergy | float | Wh |
| 57 | 4B | ChargeEnergy | float | Wh |
| 61 | 4B | DischargeEnergy | float | Wh |
| 65 | 4B | StepEnergy | float | Wh |
| 69 | 1B | Operator | byte | |
| 70 | 2B | CycleNumber | int16 | |
| 72 | 2B | TableStepNumber | int16 | |
| 74 | 3B | IO Status | bytes | Digital I/O state |
| 77 | 2B | CRC-16 | uint16 | |

### 5.3 Calibration Live Packet (`0xA0`) — 21 bytes

| Field | Size | Description |
|---|---|---|
| Start | 1B | `0xA0` |
| DeviceID / CircuitID / QueryID | 1B each | |
| Current (float) | 4B | Measured A |
| Current ADC (int32) | 4B | Raw ADC count |
| Voltage (float) | 4B | Measured V |
| Voltage ADC (int32) | 4B | Raw ADC count |
| ErrorId | 1B | |
| CRC-16 | 2B | |

---

## 6. Session Store Interface — UDP :10001

### 6.1 Overview
Hardware sends batched measurement records for persistent storage. The server enqueues records via `Channel<T>` and writes asynchronously to a per-session SQLite file at:
```
{AppData}/{dd-MM-yyyy}/{SessionID}_{DeviceId}_{CircuitId}.db
```

### 6.2 Packet Structure

```
┌───────┬────────┬─────────┬────────┬───────────┬─────────┬─────────┬──────────────┬──────┐
│ 0xCC  │DeviceID│CircuitID│QueryID │ SessionID │StepNum  │Operator │ Data Records │CRC16 │
│  1B   │   1B   │   1B    │   1B   │    4B     │   2B    │   1B    │  N x records │  2B  │
└───────┴────────┴─────────┴────────┴───────────┴─────────┴─────────┴──────────────┴──────┘
```

### 6.3 Data Record Opcodes

| Opcode | Field | Type |
|---|---|---|
| `0x01` | ProgramRunningTime (starts new row) | int32 ms |
| `0x02`–`0x04` | Current / Voltage / Temperature | float |
| `0x05` | Power | float W |
| `0x06`–`0x09` | Capacities (Accumulated/Charge/Discharge/Step) | float Ah |
| `0x10`–`0x13` | Energies (Accumulated/Charge/Discharge/Step) | float Wh |
| `0x14` | SystemErrorId | int32 bitmask |
| `0x15` | MessageId | byte |
| `0x16` | ErrorId | byte |
| `0x17` | CustomRemark | int16 |
| `0x1F`–`0xFA` | DBC CAN values (opcode = channel key, type-tagged) | variable |

---

## 7. REST API Interface

### 7.1 Authentication
All endpoints (except `/api/Auth/login`) require a JWT Bearer token:
```
Authorization: Bearer <token>
```

### 7.2 Authentication Endpoint

**POST** `/api/Auth/login`

Request:
```json
{ "email": "user@apl.com", "password": "secret" }
```
Response `200 OK`:
```json
{
  "success": true,
  "data": {
    "token": "<JWT>",
    "expiresAt": "2026-05-06T12:00:00Z"
  }
}
```
Token lifetime: 24 hours. Claims include `sub` (user ID), `email`, `username`, roles.

### 7.3 Device Endpoints — `/api/Device`

All multi-device endpoints accept a JSON array of `CommonRequest`:
```json
[{ "deviceID": 1, "circuitID": 1, "programId": 5, "batteryId": 2, "dbcId": 3 }]
```

| Method | Endpoint | Description | Response |
|---|---|---|---|
| GET | `/api/Device` | Health check | `"DeviceController is working!"` |
| GET | `/api/Device/logs` | In-memory log tail | Array of log strings |
| POST | `/api/Device/SendProgram` | Upload program + battery + DBC to circuits | `CommonResponse<List<string>>` |
| POST | `/api/Device/Start` | Start program on circuits | `CommonResponse<List<string>>` |
| POST | `/api/Device/Stop` | Stop program on circuits | `CommonResponse<List<string>>` |
| POST | `/api/Device/Pause` | Pause program on circuits | `CommonResponse<List<string>>` |
| POST | `/api/Device/Continue` | Resume paused program | `CommonResponse<List<string>>` |
| GET | `/api/Device/GetSessions` | Get active session info per circuit | Array of session objects |
| POST | `/api/Device/GetDevices` | Get real-time record per circuit | `List<RealTimeRecordDto>` |
| POST | `/api/Device/GetPrograms` | List all programs | `CommonResponse<object>` |
| POST | `/api/Device/GetBatteries` | List all batteries | `CommonResponse` |
| POST | `/api/Device/GetLiveData` | Snapshot live data for circuits | `CommonResponse<List<RealTimeRecordDto>>` |
| GET | `/api/Device/GetLiveSSE` | Server-Sent Events live data stream | `text/event-stream` |

### 7.4 CommonResponse Envelope

All endpoints return a consistent envelope:
```json
{
  "success": true,
  "message": "Processed",
  "data": <payload>
}
```

### 7.5 Server-Sent Events (SSE) — `GET /api/Device/GetLiveSSE`

Query parameters: `?deviceID=1&circuitID=1`

The server holds the HTTP connection open and writes a new JSON line every time live data changes:
```
{"success":true,"data":{<RealTimeRecordDto>}}\n\n
```
Disconnect by closing the HTTP request. The server automatically unsubscribes from the data change event.

---

## 8. MCP Tool Interface

### 8.1 Overview
BTS exposes an MCP (Model Context Protocol) server at `/mcp` for integration with AI assistants (Claude Desktop, etc.). All tools reuse the same business logic as the REST API — zero code duplication.

**Endpoint:** `GET /mcp` (HTTP SSE, stateless mode)
**Authentication:** JWT Bearer token (same as REST API)

### 8.2 Available MCP Tools

| Tool | Parameters | Description |
|---|---|---|
| `SendProgram` | deviceId, circuitId, programId, batteryId?, dbcId? | Upload program + battery + DBC to a circuit |
| `StartProgram` | deviceId, circuitId | Start test execution |
| `StopProgram` | deviceId, circuitId | Stop test execution |
| `PauseProgram` | deviceId, circuitId | Pause test execution |
| `ContinueProgram` | deviceId, circuitId | Resume paused test |
| `GetLiveData` | deviceId, circuitId | Get current measurement snapshot |

All tools return a human-readable string result suitable for AI display.

### 8.3 Example MCP Tool Call
```json
{
  "tool": "StartProgram",
  "arguments": { "deviceId": 1, "circuitId": 1 }
}
```
Response: `"1-1 -> Success"`

---

## 9. Status Code Reference

### 9.1 Circuit Status Codes

| Value | Name | Description |
|---|---|---|
| `0x00` | Idle | No program running |
| `0x01` | Charge | Charging battery |
| `0x02` | Discharging | Discharging battery |
| `0x03` | Pause | Program paused |
| `0x04` | Continue | Resuming from pause |
| `0x05` | Interrupt | Interrupted by command |
| `0x06` | Error | Hardware error |
| `0x07` | Msg | Message/notification event |
| `0x08` | Offline | Device disconnected |

### 9.2 System Error Bitmask (int32)

| Bit | Flag | Description |
|---|---|---|
| 0 | ERR_LNT | LNT error |
| 1 | ERR_ZNT | ZNT error |
| 2 | ERR_OVER_TEMPERATURE | Over temperature |
| 3 | ERR_CURRENT_SETPOINT_UNREACHABLE | Current setpoint not reachable |
| 4 | ERR_VOLTAGE_SETPOINT_UNREACHABLE | Voltage setpoint not reachable |
| 5 | ERR_OVER_CURRENT | Over current |
| 6 | ERR_OVER_VOLTAGE | Over voltage |
| 7 | ERR_REVERSE_POLARITY_VOLTAGE_SENSE | Reverse polarity |
| 8 | ERR_PRIM_SEC_COM | Board communication error |
| 9 | ERR_INVALID_CMD_PRIM_TO_SEC | Invalid inter-board command |
| 10 | ERR_POWER_FAIL | Power failure |
| 11 | ERR_EEPROM_R_WR | EEPROM read/write error |
| 12 | ERR_TEMP_FB | Temperature feedback error |
| 13 | ERR_NETWORK_CONN_FAIL | Network connection failure |
| 14 | ERR_POWER_SETPOINT_UNREACHABLE | Power setpoint not reachable |
| 15 | ERR_OVER_POWER | Over power |
| 16 | ERR_INVALID_PROG_STEP | Invalid program step |

---

## 10. Blazor Web UI Interface (SignalR)

### 10.1 Overview
The Blazor Server UI communicates with the server via ASP.NET Core SignalR over WebSocket. All UI state management and rendering occurs server-side. Clients receive DOM diffs only.

### 10.2 Real-Time UI Updates
The `CircuitCommandHandler` holds a `RealTimeState` object with an `OnDataChanged` event. When a new UDP packet arrives on port `10000`, `CircuitManager` fires this event. Blazor components subscribed to it call `InvokeAsync(StateHasChanged)` to update the live dashboard without polling.

### 10.3 Browser Requirements

| Requirement | Value |
|---|---|
| WebSocket support | Required (SignalR fallback: Long Polling) |
| Modern browser | Chrome 90+, Edge 90+, Firefox 88+ |
| Authentication | Cookie-based (ASP.NET Identity) for UI |

---

*Document ID: BTS-ICD-001 | Generated from source: `DeviceController.cs`, `DeviceMcpTools.cs`, `CircuitManager.cs`, `DecoderService.cs`, `BroadcastUdpService.cs`, `CircuitEnums.cs`*
