# 📡 BTS Communication Protocol Reference

> Battery Testing System (BTS) v0.5  
> All multi-byte integers are **Big Endian** unless noted otherwise.  
> All packets end with a **CRC-16/Modbus** checksum (2 bytes, Little Endian).

---

## 📋 Table of Contents

1. [Frame Structure](#frame-structure)
2. [Start Bytes (Identity Bytes)](#start-bytes)
3. [Command Status Codes](#command-status-codes)
4. [Device Discovery — UDP Broadcast](#device-discovery--udp-broadcast)
5. [Registration — TCP Port 9999](#registration--tcp-port-9999)
6. [Configuration Commands — 0xAA](#configuration-commands--0xaa)
7. [Program Commands — 0xBB](#program-commands--0xbb)
8. [Control Commands — 0xEE](#control-commands--0xee)
9. [Calibration Commands — 0xA0](#calibration-commands--0xa0)
10. [Real-Time Data Stream — UDP Port 10000](#real-time-data-stream--udp-port-10000)
11. [Session Store Stream — UDP Port 10001](#session-store-stream--udp-port-10001)
12. [CRC-16 Calculation](#crc-16-calculation)
13. [Circuit & System Status Codes](#circuit--system-status-codes)

---

## Frame Structure

### Command Frame (Server → Hardware)
```
┌──────────┬──────────┬──────────┬──────────┬────────────┬───────────┐
│ Start    │ DeviceID │ CircuitID│ QueryID  │ Payload    │ CRC-16    │
│ 1 byte   │ 1 byte   │ 1 byte   │ 1 byte   │ N bytes    │ 2 bytes   │
└──────────┴──────────┴──────────┴──────────┴────────────┴───────────┘
```

### Response Frame (Hardware → Server)
```
┌──────────┬──────────┬──────────┬──────────┬────────────┬───────────┐
│ Start    │ DeviceID │ CircuitID│ QueryID  │ Status/Data│ CRC-16    │
│ 1 byte   │ 1 byte   │ 1 byte   │ 1 byte   │ N bytes    │ 2 bytes   │
└──────────┴──────────┴──────────┴──────────┴────────────┴───────────┘
```

---

## Start Bytes

| Byte | Enum | Purpose |
|------|------|---------|
| `0xDD` | `Registration` | Hardware registration and deletion |
| `0xAA` | `Configuration` | Hardware configuration read/write |
| `0xBB` | `Program` | Test program upload |
| `0xEE` | `Control` | Program control (Start/Stop/Pause/Continue) |
| `0xA0` | `Calibration` | Calibration operations |
| `0xCC` | *(RealTime)* | Real-time data packets (UDP only) |

---

## Command Status Codes

| Value | Meaning |
|-------|---------|
| `0x00` | Failed |
| `0x01` | Success |
| `0x02` | Already Registered |

---

## Device Discovery — UDP Broadcast

BTS uses a **SADP-style UDP broadcast** mechanism for discovering and configuring hardware devices on the local network before they register via TCP.

```
Server                              Hardware Devices
  │                                        │
  │── Broadcast CMD → 255.255.255.255:10002 ──▶ All devices
  │                                        │
  │◀─────── Reply ← device IP : 10003 ─────│
  │                                        │
```

### Ports

| Port | Direction | Purpose |
|------|-----------|---------|
| `10002` | Server → Broadcast | Server sends discovery/config commands |
| `10003` | Hardware → Server | Hardware replies to server |

The server broadcasts simultaneously on **all active network interfaces** (not just the default route) to ensure discovery works in multi-NIC environments.

### Discovery Query Types (Broadcast)

#### Q1 — Discover Devices
Server broadcasts a discovery request. All online devices reply with their full network and port configuration.

**Reply payload from device:**
```
┌──────────┬──────────────┬─────────────┬─────────────┬──────────┬──────────┬─────────────┬──────────┬──────────┬──────────┬
│ UniqueID │  Device IP   │ Subnet Mask │   Gateway   │   DNS1   │   DNS2   │  RemoteIP   │ TCP Port │UDP Live  │ UDP Reg  │
│  4 bytes │   4 bytes    │   4 bytes   │   4 bytes   │  4 bytes │  4 bytes │   4 bytes   │  2 bytes │  2 bytes │  2 bytes │
└──────────┴──────────────┴─────────────┴─────────────┴──────────┴──────────┴─────────────┴──────────┴──────────┴──────────┘
```

**Parsed into `BroadcastDeviceInfo`:**

| Field | Description |
|-------|-------------|
| `UniqueId` | 4-byte unique hardware identifier |
| `DeviceIp` | Current IP of the hardware |
| `SubnetMask` | Subnet mask |
| `Gateway` | Default gateway |
| `Dns1 / Dns2` | DNS servers |
| `RemoteIp` | BTS server IP the device is pointed at |
| `TcpPort` | Command port (should be 9999) |
| `UdpLivePort` | Real-time data port (should be 10000) |
| `UdpRegPort` | Session store port (should be 10001) |

#### Q4 — Set Device IP Configuration
Server sends a targeted broadcast to set a specific device's network config using its `UniqueId`.

**Payload (`BroadcastIpConfig`):**
```
┌──────────┬──────────────┬─────────────┬─────────────┬──────────┬──────────┐
│ UniqueID │  Device IP   │ Subnet Mask │   Gateway   │   DNS1   │   DNS2   │
│  4 bytes │   4 bytes    │   4 bytes   │   4 bytes   │  4 bytes │  4 bytes │
└──────────┴──────────────┴─────────────┴─────────────┴──────────┴──────────┘
```

#### Q5 — Set Server Configuration
Server pushes the BTS server address and port configuration to a specific device.

**Payload (`BroadcastServerConfig`):**
```
┌──────────┬─────────────┬──────────┬──────────┬──────────┐
│ UniqueID │  RemoteIP   │ TCP Port │UDP Live  │ UDP Reg  │
│  4 bytes │   4 bytes   │  2 bytes │  2 bytes │  2 bytes │
└──────────┴─────────────┴──────────┴──────────┴──────────┘
```

> After Q5, the hardware knows the BTS server IP and all port numbers and will initiate TCP registration on port `9999`.

---

## Registration — TCP Port 9999

Hardware initiates TCP connection to the server on port `9999` after network configuration.

### Registration Handshake Flow
```
Hardware                              BTS Server
   │                                      │
   │──── TCP Connect → :9999 ────────────▶│
   │                                      │
   │──── Registration Packet (33 bytes) ─▶│  Header: 0xDD 0x01
   │                                      │  Parse device/circuit info
   │                                      │  Check DB → approve/reject
   │                                      │
   │◀─── Registration Response (5 bytes) ─│
   │                                      │
   │   [if Success] TCP stays open        │
   │   [if Failed]  Device not approved   │
```

### Registration Packet (Hardware → Server) — 33 bytes

```
┌───────┬────────┬──────────┬──────────┬──────────────────┬─────────────┬──────────────┐
│ 0xDD  │  0x01  │ reserved │ DeviceID │   DeviceName     │  IP Address │  MAC Address │
│ 1 byte│ 1 byte │  1 byte  │  1 byte  │    16 bytes      │   4 bytes   │   6 bytes    │
└───────┴────────┴──────────┴──────────┴──────────────────┴─────────────┴──────────────┘
  also includes CircuitID (1 byte) after DeviceID
```

**Parsed into `CircuitDto`:**

| Field | Size | Description |
|-------|------|-------------|
| `DeviceID` | 1B | Hardware device ID |
| `CircuitID` | 1B | Circuit number on device |
| `DeviceName` | 16B | ASCII name, null-padded |
| `IPAddress` | 4B | Hardware IP (dot-notation) |
| `MACID` | 6B | MAC address (colon-notation) |

### Registration Response (Server → Hardware) — 5 bytes

```
┌───────┬──────────┬──────────┬────────┬───────────┐
│ 0xDD  │ DeviceID │ CircuitID│ Status │  Reserved │
│ 1 byte│  1 byte  │  1 byte  │ 1 byte │   1 byte  │
└───────┴──────────┴──────────┴────────┴───────────┘
```

| Status | Value | Meaning |
|--------|-------|---------|
| `Success` | `0x01` | Registered — TCP stays open, streaming begins |
| `AlreadyRegistered` | `0x02` | Reconnect accepted — handler updated |
| `Failed` | `0x00` | Not approved in DB — admin must whitelist in web UI |

---

## Configuration Commands — 0xAA

| QueryID | Name | Direction | Description |
|---------|------|-----------|-------------|
| `0x01` | HW Ready | Server→HW | Check if hardware is ready |
| `0x02` | Read Factory Config | Server→HW | Read factory/network config |
| `0x03` | Read Manufacturing Config | Server→HW | Read SW versions, serial numbers, dates |
| `0x04` | Read Battery Params | Server→HW | Read battery configuration |
| `0x05` | Write Battery Params | Server→HW | Write battery configuration |
| `0x06` | Sync Time | Server→HW | Synchronize RTC time |

### Q2 — Factory Config Response (Hardware → Server)

```
┌────────────────────┬────────────────────────────────────────────────────────────┐
│ Field              │ Size  │ Description                                         │
├────────────────────┼───────┼─────────────────────────────────────────────────────┤
│ MAC Address        │ 6B    │ Hardware MAC                                        │
│ Device IP          │ 4B    │ Current device IP                                   │
│ Client Remote IP   │ 4B    │ BTS server IP                                       │
│ TCP Port           │ 2B    │ Command port                                        │
│ UDP Port           │ 2B    │ Real-time data port                                 │
│ UDP Store Port     │ 2B    │ Session store port                                  │
│ DHCP               │ 1B    │ 0x01=enabled                                        │
│ Circuit Type       │ 1B    │ 0x00=Single Bank, 0x01=Dual Bank                   │
│ ZNT Max Voltage    │ 4B    │ float                                               │
│ LNT Max Voltage    │ 4B    │ float                                               │
│ Circuit Max Volt   │ 4B    │ float                                               │
│ Circuit Min Volt   │ 4B    │ float                                               │
│ Max Discharge Cur  │ 4B    │ float (A)                                           │
│ Max Charge Cur     │ 4B    │ float (A)                                           │
│ Circuit Number     │ 1B    │ Circuit index                                       │
└────────────────────┴───────┴─────────────────────────────────────────────────────┘
```

### Q3 — Manufacturing Config Response

| Field | Size | Description |
|-------|------|-------------|
| Master SW Version | 11B | ASCII string |
| Com SW Version | 11B | ASCII string |
| Secondary SW Version | 11B | ASCII string |
| Primary Serial No | 4B | uint32 |
| Secondary Serial No | 4B | uint32 |
| Manufacture Date | 4B | Unix epoch UTC |
| Commissioning Date | 4B | Unix epoch UTC |
| Primary PCB Date | 4B | Unix epoch UTC |
| Secondary PCB Date | 4B | Unix epoch UTC |

---

## Program Commands — 0xBB

| QueryID | Name | Description |
|---------|------|-------------|
| `0x01` | HW Ready for Program | Confirm hardware ready to receive program |
| `0x03` | Send Steps Count | Send total step count before upload |
| `0x04` | Send Program | Upload program steps data |
| `0x07` | Send DBC Steps Count | Send DBC channel count |
| `0x08` | Send DBC File | Upload DBC CAN file |

All program responses: `payload[4] == 0x01` = Success

---

## Control Commands — 0xEE

| QueryID | Name | Description |
|---------|------|-------------|
| `0x01` | Start | Start program execution |
| `0x02` | Stop | Stop program execution |
| `0x03` | Pause | Pause program execution |
| `0x04` | Continue | Resume paused execution |
| `0x05` | Sync Time | Sync RTC time |
| `0x06` | System Reset | Reset the hardware |

All control responses: `payload[4] == 0x01` = Success

---

## Calibration Commands — 0xA0

| QueryID | Name |
|---------|------|
| `0x01` | Is Ready |
| `0x02` | Send Live Calibration Data |
| `0x03` | Current Charge Low Point |
| `0x04` | Current Charge High Point |
| `0x05` | Current Charge Gain+Offset |
| `0x06` | Current Discharge Low Point |
| `0x07` | Current Discharge High Point |
| `0x08` | Current Discharge Gain+Offset |
| `0x09` | Voltage Charge Low Point |
| `0x0A` | Voltage Charge High Point |
| `0x0B` | Voltage Charge Gain+Offset |
| `0x0C` | Voltage Discharge Low Point |
| `0x0D` | Voltage Discharge High Point |
| `0x0E` | Voltage Discharge Gain+Offset |
| `0x0F` | Cancel Calibration |
| `0x10` | Stop Calibration |
| `0x11` | Start Charge Verify |
| `0x12` | Start Discharge Verify |
| `0x13` | Stop Verify |
| `0x14` | Read Previous Calibration |

### Q14 — Previous Calibration Response

Four blocks, each block:
```
┌──────────┬──────────┬──────────────┐
│  Gain    │  Offset  │  DateTime    │
│  4B float│  4B float│  4B unix UTC │
└──────────┴──────────┴──────────────┘
```
Blocks in order: Current Charge, Current Discharge, Voltage Charge, Voltage Discharge.

---

## Real-Time Data Stream — UDP Port 10000

Hardware streams data continuously to the BTS server. Two packet types share this port.

### Packet Type 0xCC — Live Measurement Data (min 37 bytes)

```
┌──────┬─────────┬──────────┬─────────┬─────────┬──────────┬────────────────────────┬──────┐
│ 0xCC │DeviceID │ CircuitID│ QueryID │StepNum  │ProgStatus│ ... measurement fields │CRC16 │
│  1B  │   1B    │    1B    │   1B    │   2B    │    1B    │                        │  2B  │
└──────┴─────────┴──────────┴─────────┴─────────┴──────────┴────────────────────────┴──────┘
```

**Full field layout:**

| Offset | Size | Field | Type | Description |
|--------|------|-------|------|-------------|
| 0 | 1B | Start | byte | `0xCC` |
| 1 | 1B | DeviceID | byte | Hardware device ID |
| 2 | 1B | CircuitID | byte | Circuit number |
| 3 | 1B | QueryID | byte | Query type |
| 4 | 2B | StepNumber | int16 | Current program step |
| 6 | 1B | ProgramStatus | enum | `0x00`=Stop, `0x01`=Running |
| 7 | 1B | CircuitStatus | enum | See status codes below |
| 8 | 1B | ErrorId | byte | Error code |
| 9 | 4B | SystemErrorId | int32 | Bitmask of system errors |
| 13 | 4B | StepRunningTime | int32 | Step elapsed time (ms) |
| 17 | 4B | RunningTime | int32 | Total elapsed time (ms) |
| 21 | 4B | Current | float | Amperes (A) |
| 25 | 4B | Voltage | float | Volts (V) |
| 29 | 4B | Temperature | float | Celsius (°C) |
| 33 | 4B | Power | float | Watts (W) |
| 37 | 4B | AccumulatedCapacity | float | Ah |
| 41 | 4B | ChargeCapacity | float | Ah (charge) |
| 45 | 4B | DischargeCapacity | float | Ah (discharge) |
| 49 | 4B | StepCapacity | float | Ah (this step) |
| 53 | 4B | AccumulatedEnergy | float | Wh |
| 57 | 4B | ChargeEnergy | float | Wh (charge) |
| 61 | 4B | DischargeEnergy | float | Wh (discharge) |
| 65 | 4B | StepEnergy | float | Wh (this step) |
| 69 | 1B | Operator | byte | Operator code |
| 70 | 2B | CycleNumber | int16 | Cycle count |
| 72 | 2B | TableStepNumber | int16 | Table step index |
| 74 | 3B | IO Status | bytes | Digital I/O state (see below) |
| 77 | 2B | CRC-16 | uint16 | Checksum |

### IO Status Byte Layout (3 bytes)

**Byte 0 — Digital Inputs:**
- Bits 7–4 → Primary DI3–DI0
- Bits 3–0 → Secondary DI3–DI0

**Byte 1 — Secondary Digital Outputs DO7–DO0**

**Byte 2 — Primary Digital Outputs DO2–DO0** (bits 2–0 only)

### Packet Type 0xA0 — Calibration Live Data (min 21 bytes)

| Field | Size | Description |
|-------|------|-------------|
| Start | 1B | `0xA0` |
| DeviceID | 1B | |
| CircuitID | 1B | |
| QueryID | 1B | |
| Current (float) | 4B | Measured current A |
| Current (ADC) | 4B | Raw ADC count int32 |
| Voltage (float) | 4B | Measured voltage V |
| Voltage (ADC) | 4B | Raw ADC count int32 |
| ErrorId | 1B | Error byte |
| CRC-16 | 2B | |

---

## Session Store Stream — UDP Port 10001

Hardware sends batched measurement records for persistent storage. The server decodes, enqueues via `Channel<T>`, and writes to the per-session SQLite file asynchronously.

### Packet Structure (Start byte: `0xCC`)

```
┌──────┬────────┬─────────┬────────┬───────────┬─────────┬─────────┬──────────────────┬──────┐
│ 0xCC │DeviceID│CircuitID│QueryID │ SessionID │StepNum  │Operator │ Data Records ... │CRC16 │
│  1B  │   1B   │   1B    │   1B   │    4B     │   2B    │   1B    │   N × records    │  2B  │
└──────┴────────┴─────────┴────────┴───────────┴─────────┴─────────┴──────────────────┴──────┘
```

**SessionID** is a Unix epoch timestamp — used to derive the session file path:
```
{AppData}/{dd-MM-yyyy}/{SessionID}_{DeviceId}_{CircuitId}.db
```

### Data Record Opcodes

Records are encoded as opcode-tagged fields. Opcode `0x01` starts a new measurement row:

| Opcode | Size | Field | Type |
|--------|------|-------|------|
| `0x01` | 4B | ProgramRunningTime | int32 (ms) — **starts new row** |
| `0x02` | 4B | Current | float (A) |
| `0x03` | 4B | Voltage | float (V) |
| `0x04` | 4B | Temperature | float (°C) |
| `0x05` | 4B | Power | float (W) |
| `0x06` | 4B | AccumulatedCapacity | float (Ah) |
| `0x07` | 4B | ChargeCapacity | float (Ah) |
| `0x08` | 4B | DischargeCapacity | float (Ah) |
| `0x09` | 4B | StepCapacity | float (Ah) |
| `0x10` | 4B | AccumulatedEnergy | float (Wh) |
| `0x11` | 4B | ChargeEnergy | float (Wh) |
| `0x12` | 4B | DischargeEnergy | float (Wh) |
| `0x13` | 4B | StepEnergy | float (Wh) |
| `0x14` | 4B | SystemErrorId | int32 bitmask |
| `0x15` | 1B | MessageId | byte |
| `0x16` | 1B | ErrorId | byte |
| `0x17` | 2B | CustomRemark | int16 |
| `0x1F`–`0xFA` | variable | DBC CAN Values | key + type-tagged value |

### DBC CAN Value Encoding (opcodes 31–250)

```
┌──────────┬──────────┬──────────────────────┐
│ DBC Key  │ TypeByte │ Value                │
│  1 byte  │  1 byte  │  1–8 bytes           │
└──────────┴──────────┴──────────────────────┘
```

**TypeByte:** high nibble = data length, low nibble = type

| Type nibble | C# Type | Size |
|-------------|---------|------|
| `1` | float | 4B |
| `2` | int32 | 4B |
| `3` | int16 | 2B |
| `4` | byte | 1B |
| `5` | bool | 1B |
| `6` | double | 8B |

---

## CRC-16 Calculation

All packets use **CRC-16/Modbus** (polynomial `0xA001`, init `0xFFFF`).

CRC covers all bytes **except** the last 2 CRC bytes themselves.
CRC is appended **Little Endian** (low byte first, high byte second).

```csharp
// CRC-16/Modbus
ushort crc = 0xFFFF;
for (int i = 0; i < data.Length; i++) {
    crc ^= data[i];
    for (int j = 0; j < 8; j++) {
        if ((crc & 0x0001) != 0)
            crc = (ushort)((crc >> 1) ^ 0xA001);
        else
            crc >>= 1;
    }
}
// Append: low byte first, then high byte
```

---

## Circuit & System Status Codes

### Circuit Status (`0xCC` byte 7)

| Value | Name | Description |
|-------|------|-------------|
| `0x00` | Idle | No program running |
| `0x01` | Charge | Charging battery |
| `0x02` | Discharging | Discharging battery |
| `0x03` | Pause | Program paused |
| `0x04` | Continue | Resuming from pause |
| `0x05` | Interrupt | Interrupted by command |
| `0x06` | Error | Hardware error |
| `0x07` | Msg | Message/notification |
| `0x08` | Offline | Device disconnected |

### System Error Bitmask (int32)

| Bit | Flag | Description |
|-----|------|-------------|
| 0 | `ERR_LNT` | LNT error |
| 1 | `ERR_ZNT` | ZNT error |
| 2 | `ERR_OVER_TEMPERATURE` | Over temperature |
| 3 | `ERR_CURRENT_SETPOINT_UNREACHABLE` | Current setpoint not reachable |
| 4 | `ERR_VOLTAGE_SETPOINT_UNREACHABLE` | Voltage setpoint not reachable |
| 5 | `ERR_OVER_CURRENT` | Over current |
| 6 | `ERR_OVER_VOLTAGE` | Over voltage |
| 7 | `ERR_REVERSE_POLARITY_VOLTAGE_SENSE` | Reverse polarity on voltage sense |
| 8 | `ERR_PRIM_SEC_COM` | Primary-secondary board communication error |
| 9 | `ERR_INVALID_CMD_PRIM_TO_SEC` | Invalid command between boards |
| 10 | `ERR_POWER_FAIL` | Power failure |
| 11 | `ERR_EEPROM_R_WR` | EEPROM read/write error |
| 12 | `ERR_TEMP_FB` | Temperature feedback error |
| 13 | `ERR_NETWORK_CONN_FAIL` | Network connection failure |
| 14 | `ERR_POWER_SETPOINT_UNREACHABLE` | Power setpoint not reachable |
| 15 | `ERR_OVER_POWER` | Over power |
| 16 | `ERR_INVALID_PROG_STEP` | Invalid program step |

---

*Document generated from BTS v0.5 source — `DecoderService.cs`, `CircuitEnums.cs`, `BroadcastUdpService.cs`, `CircuitCommandHandler.cs`*
