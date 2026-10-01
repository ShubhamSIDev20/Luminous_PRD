# Battery Testing System (BTS) — Full Communication Protocol Specification

> **Source-level reference.** Derived from `DecoderService.cs` (2371 lines), `ProgramBuilder.cs`, `ChannelCommandHandler.cs`, `ChannelManager.cs`, `DbcParser.cs`, `Models/Enums/CircuitEnums.cs`, and `OperatorConstants.cs`. Every byte layout and algorithm can be traced to a specific method in those files.

---

## Table of Contents

1. [Architecture & Transport](#1-architecture--transport)
2. [Packet Framing & CRC-16](#2-packet-framing--crc-16)
   - [2.4 Circuit Address Byte Split](#24-circuit-address-byte-split-implemented)
3. [Session ID Encoding](#3-session-id-encoding)
4. [Command Dispatch (TryDecode)](#4-command-dispatch-trydecode)
5. [Configuration Commands (0xAA)](#5-configuration-commands-0xaa)
6. [Program Commands (0xBB)](#6-program-commands-0xbb)
7. [Control Commands (0xEE)](#7-control-commands-0xee)
8. [Registration & Broadcast (0xDD)](#8-registration--broadcast-0xdd)
9. [Calibration Commands (0xA0)](#9-calibration-commands-0xa0)
10. [Real-Time Live Telemetry (0xCC)](#10-real-time-live-telemetry-0xcc)
11. [Storage Protocol V1](#11-storage-protocol-v1)
12. [Storage Protocol V2](#12-storage-protocol-v2)
13. [DBC Signal Decoding](#13-dbc-signal-decoding)
14. [Program Step Download & Framing](#14-program-step-download--framing)
15. [Program Step Binary Encoding](#15-program-step-binary-encoding)
16. [DBC Multi-Port Payload](#16-dbc-multi-port-payload)
17. [Digital I/O Bit Mapping](#17-digital-io-bit-mapping)
18. [Reference Tables & Enums](#18-reference-tables--enums)

---

## 1. Architecture & Transport

### 1.1 Protocol Stack

```
+-----------------------------------------------------------------+
|                      Blazor Server App                           |
|  ChannelManager (UDP)  |  ChannelCommandHandler (TCP)            |
|  +------------------+  |  +----------------------------+        |
|  | UDP Live :10000  |--+--| TCP Command :9999            |      |
|  | UDP Store:10001  |--+--|   SendAndWaitForResponse     |      |
|  +------------------+  |  +----------------------------+        |
|                        |                                        |
|  SADP Broadcast        |                                       |
|  +------------------+  |                                       |
|  | UDP Tx  :10003   |  |                                       |
|  | UDP Rx  :10004   |  |                                       |
|  +------------------+  |                                       |
+-----------------------------------------------------------------+
                           |
                     +-----+------+
                     |  BTS HW    |
                     |  Device    |
                     +------------+
```

### 1.2 Port Assignments

| Port | Protocol | Direction | Purpose | Handler |
|------|----------|-----------|---------|---------|
| **9999** | TCP | App <-> Device | Commands (config, program, control, calibration, registration). One-at-a-time request/response with 15 s timeout. | `ChannelCommandHandler._tcpClient` |
| **10000** | UDP | Device -> App | Real-time live telemetry (LiveData `0xCC` type `0x01`), DBC values (`0xCC` type `0x02`), Calibration live (`0xA0`). | `ChannelManager.RunUdpViewListenerAsync` |
| **10001** | UDP | Device -> App | High-volume store packets (RealStoreDataV2 `0xCC`). Decoded by `DecoderService.RealStoreDataV2` -> enqueued via `ChannelCommandHandler.EnqueueForStore` -> persisted to per-session SQLite DB. | `ChannelManager.RunUdpStoreListenerAsync` |
| **10003** | UDP | App -> Broadcast | Device discovery (`Q3`), network config (`Q4`), server config (`Q5`). Sent to `255.255.255.255`. | `DecoderService.DeviceDiscovery()` etc. |
| **10004** | UDP | Device -> App | Responses to broadcast queries (`Q3`/`Q4`/`Q5`). | `DecoderService.DecodeDeviceDiscovery()` etc. |

### 1.3 Endianness

**All multi-byte numeric fields are Big-Endian** (network byte order): `int16`, `int32`, `uint16`, `uint32`, `float32`.

**Exception:** The CRC-16 is appended in **Little-Endian byte order** (low byte first, high byte second) — see Section 2.2.

### 1.4 Float Encoding

All floats are **IEEE-754 single-precision (32-bit)** unless stated otherwise. `GetFloatBytes` / `ReadSingleBigEndian` reverse bytes on little-endian hosts.

---

## 2. Packet Framing & CRC-16

### 2.1 Standard Command Frame

Every command sent from the app to the device follows this envelope, built by `DecoderService.BuildCommand(CommandRequest)`:

```
+-----------+-----------+-----------+-----------+--------------+-----------------+--------------+
| StartByte | DeviceID  | CircuitID |  QueryID  | Range (opt.) | Data Payload    | CRC-16 (2B)  |
|  (1 B)    |  (1 B)    |  (1 B)    |  (1 B)    |   (1 B)      |  (0-N bytes)    | (LE: Lo,Hi)  |
+-----------+-----------+-----------+-----------+--------------+-----------------+--------------+
```

- **4-byte header** (normal): `[StartByte][DeviceID][CircuitID][QueryID]`
- **5-byte header** (when `CommandRequest.Range` is set): adds `[Range]` after QueryID
- **Data payload** priority:
  1. If `req.Data` is non-empty -> `header + req.Data`
  2. Else if `req.SingleByte.HasValue` -> `header + [SingleByte]`
  3. Else -> header only
- **CRC-16** appended to the assembled `header + payload` bytes.

### 2.2 CRC-16/MODBUS Algorithm

**Polynomial:** `0xA001` (bit-reversed form of CRC-16-CCITT `0x1021`)
**Initial value:** `0xFFFF`

```csharp
public static ushort CalculateCRC16(byte[] data, int offset, int length)
{
    ushort crc = 0xFFFF;
    for (int i = offset; i < offset + length; i++)
    {
        crc ^= data[i];
        for (int j = 0; j < 8; j++)
        {
            if ((crc & 0x0001) != 0)
                crc = (ushort)((crc >> 1) ^ 0xA001);
            else
                crc >>= 1;
        }
    }
    return crc;
}
```

**Wire encoding** (`BindCRC16`):
```
result[data.Length]     = (byte)(crc & 0xFF);   // Low byte FIRST
result[data.Length + 1] = (byte)(crc >> 8);      // High byte SECOND
```

> **Note:** CRC is **Little-Endian** on the wire while every other numeric field is Big-Endian. This matches the MODBUS CRC convention.

### 2.3 Response Frame Validation

All incoming responses are validated through `DecoderService.TryDecode<T>(byte[] payload)`:
- Minimum length check (>= 5 bytes)
- StartByte identity extracted from `payload[0]`
- QueryID selector position varies by identity:
  - `0xAA`, `0xBB`, `0xEE`, `0xA0` -> `payload[3]`
  - `0xDD` -> `payload[1]`
- Status byte position varies: `payload[4]` for most; `payload[2]` for `0xDD` queries 0x04/0x05; `payload[5]` for calibration gain/offset queries.

---

## 2.4 Circuit Address Byte Split (Implemented)

> **Status:** ✅ **Implemented** — `Utils/ChannelAddressCodec.cs`, shipped with the
> device-connection multiplexing work (commits `c86426c`..`2fbaf43`). The byte at header offset 2
> is **not** an opaque 0-255 value; treat the nibble split below as current wire behavior
> everywhere this document says "Circuit ID".

The 1-byte address field at header offset 2 packs an addressable secondary board and a channel
into one nibble each, staying wire-compatible with deployed hardware:

```
Bit:    7  6  5  4   3  2  1  0
       +--------+--------+
       | Board  |Channel |
       | (0-8)  | (1-8)  |
       +--------+--------+
```

- Upper nibble = `SecondaryBoard` number, lower nibble = channel number within that board.
- `ChannelAddressCodec.Encode(boardNumber, channelNumber) -> byte` /
  `.Decode(byte) -> (BoardNumber, ChannelNumber)`.
- **Enforced ranges are narrower than the nibbles allow.** `Encode` throws
  `ArgumentOutOfRangeException` for `boardNumber` outside **0-8** or `channelNumber` outside
  **1-8** — so a device tops out at 64 channels, not the 256 the byte could theoretically hold.
  `Decode` does **not** validate; it masks both nibbles and will happily return board 9-15 from a
  malformed packet.
- **Board `0` and board `1` are both live in the field, and mean different things:**
  - `ChannelAddressCodec.EncodeLegacy(channel)` maps to **`Encode(1, channel)`** — board `1` is
    the legacy/implicit board from before multi-board addressing, and is also the `IsImplicit`
    sentinel in `DeviceChannelRepository.GetOrCreateBoardAsync`.
  - Current field hardware registers on **board `0`** (`1-0-1`..`1-0-8`). The original lower bound
    of `boardNumber < 1` rejected these outright; it was widened to `< 0` (see
    `Models/Entities/SecondaryBoard`'s `[Range(0,8)]`).
  - ⚠️ So "old values map to `board=0`" is **not** what the implementation does — legacy maps to
    board `1`, and board `0` is a distinct real board.
- Every "Circuit ID" field elsewhere in this document (packet headers in §2.1, session ID packing
  in §3, registration in §8.1, live telemetry in §10.1/10.2, storage headers in §11.1/§12.1)
  carries this same packed byte. Its position on the wire never moved — only its interpretation.
- Channel identity is **3-part** (`device-board-channel`) throughout the app; see
  `docs/flowDocs/A-appendix-reference-tables.md` §A.5. To exercise board-`0` addressing without
  real hardware, run the simulator with `--board-base 0` (see `HardwareSimulator/README.md`).

**Naming collision to watch for:** §17 (Digital I/O Bit Mapping) already uses "Primary Board" and
"Secondary Board" to describe a *fixed*, always-present 2-board digital I/O bit grouping within a
single circuit's LiveData packet — a completely different concept from the addressable
`SecondaryBoard` *entity* (boards 0-8 per device) described here. The two uses of "secondary
board" are unrelated; don't conflate them when implementing or documenting.

---

## 3. Session ID Encoding

### 3.1 Packed 4-Byte Session ID (`GetSessionIdBytes`)

Prevents primary-key collisions when multiple circuits start simultaneously.

```
Bit:   31        24  23        16  15                    0
      +-----------+-----------+---------------------------+
      | DeviceID  | CircuitID |  Epoch Seconds (low 16b)  |
      |  (8 bit)  |  (8 bit)  |      (16 bit)             |
      +-----------+-----------+---------------------------+
```

**Encoding:**
```csharp
uint packed = ((uint)(deviceId  & 0xFF) << 24)
            | ((uint)(circuitId & 0xFF) << 16)
            | (epochSeconds & 0xFFFF);
```

Written as 4-byte Big-Endian.

### 3.2 Inverse Decoding (`SessionIdToDateTime`)

Recovers the full 32-bit Unix epoch by:
1. Taking the current epoch's high 16 bits (`nowEpoch & 0xFFFF0000`)
2. OR-ing in the stored low 16 bits
3. Correcting +/- 65,536 seconds (~18.2 hours) if the candidate is > 32,768 seconds in the future or past

**Accuracy guarantee:** Correct if session started within ~9 hours of decoding time.

---

## 4. Command Dispatch (TryDecode)

`TryDecode<T>(byte[] payload)` is the universal response parser. It routes on `StartByte`:

| StartByte | Hex | QueryID Source | QueryIDs -> Handler | Return Type |
|-----------|-----|----------------|--------------------|-------------|
| **Configuration** | `0xAA` | `payload[3]` | `0x01` HWReady -> `payload[4]==0x01` | `bool` |
| | | | `0x02` ReadFactoryConfig -> `FactoryParameters(payload)` | `FactoryConfigDetailDTO` |
| | | | `0x03` ReadManufacturingConfig -> `ManufacturingParameters(payload)` | `ManufacturingDetailDTO` |
| | | | `0x04` ReadBatteryParams -> `BatteryParameters(payload)` | `BatteryDTO` |
| | | | `0x05` WriteBatteryParams -> `payload[4]==0x01` | `bool` |
| | | | `0x06` SyncTime -> `payload[4]==0x01` | `bool` |
| **Program** | `0xBB` | `payload[3]` | `0x01,0x02,0x03,0x04,0x07,0x08` -> `payload[4]==0x01` | `bool` |
| | | | `0x09` LiveStepUpdate -> `payload[4]==0x01`, else `Fail(reason)` from `payload[5]` | `bool` (STATUS+REASON, index 4 and 5!) |
| | | | `0x0A` JumpToStep -> `payload[4]==0x01`, else `Fail(reason)` from `payload[5]` | `bool` (STATUS+REASON, index 4 and 5!) |
| **Control** | `0xEE` | `payload[3]` | `0x01-0x06` -> `payload[4]==0x01` | `bool` |
| **Registration** | `0xDD` | `payload[1]` | `0x01,0x02` -> `payload[4]==0x01` | `bool` |
| | | | `0x04,0x05` -> `payload[2]==0x01` | `bool` (status at index 2!) |
| **Calibration** | `0xA0` | `payload[3]` | `0x17` PreviousCalibration -> `ParseCalibrationPayload` | `CalibrationData` |
| | | | `0x03-0x08` GainOffset points -> `payload[5]==0x01` | `bool` (status at index 5!) |
| | | | default -> `payload[4]==0x01` | `bool` |

> **Warning:** The status byte position varies by identity and query — hard-coded per branch (index 2, 4, or 5).

---

## 5. Configuration Commands (0xAA)

### 5.1 Read Factory Config (`QueryID = 0x02`)

**Response payload** (after `0xAA DevID CirID 0x02` header):

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 4 | 6B | bytes | MAC Address (colon-separated hex) |
| 10 | 4B | IP | Device IP Address |
| 14 | 4B | IP | Client Remote IP Address |
| 18 | 2B | uint16 | TCP Client Remote Port (BE) |
| 20 | 2B | uint16 | UDP Client Remote Port (BE) |
| 22 | 2B | uint16 | UDP Store Remote Port (BE) |
| 24 | 1B | byte | DHCP Enabled (0x01=Yes, 0x00=No) |
| 25 | 1B | byte | Circuit Type (0=Single, 1=Dual Transistor Bank) |
| 26 | 4B | float32 | ZNT Max Voltage |
| 30 | 4B | float32 | LNT Max Voltage |
| 34 | 4B | float32 | Circuit Max Voltage |
| 38 | 4B | float32 | Circuit Min Voltage |
| 42 | 4B | float32 | Circuit Max Discharge Current |
| 46 | 4B | float32 | Circuit Max Charge Current |
| 50 | 1B | byte | Circuit Number |

### 5.2 Read Manufacturing Config (`QueryID = 0x03`)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 4 | 11B | string | Master SW Version (null-trimmed ASCII) |
| 15 | 11B | string | Communication SW Version |
| 26 | 11B | string | Secondary SW Version |
| 37 | 4B | uint32 | Primary Serial Number (BE) |
| 41 | 4B | uint32 | Secondary Serial Number (BE) |
| 45 | 4B | uint32 | Manufacture Date (Unix Epoch, UTC) |
| 49 | 4B | uint32 | Commissioning Date (Unix Epoch) |
| 53 | 4B | uint32 | Primary PCB Assembly Date (Unix Epoch) |
| 57 | 4B | uint32 | Secondary PCB Assembly Date (Unix Epoch) |

### 5.3 Read Battery Parameters (`QueryID = 0x04`)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 4 | 4B | float32 | Nominal Capacity (Ah) |
| 8 | 1B | byte | Number of Cells |
| 9 | 4B | float32 | Gassing Voltage (V) |
| 13 | 4B | float32 | Maximum Voltage (V) |
| 17 | 4B | float32 | Nominal Current (A) |
| 21 | 4B | float32 | Cold Cranking Current (A) |
| 25 | 1B | byte | Charge Factor |
| 26 | 4B | float32 | Impedance (mOhm) |
| 30 | 4B | float32 | Break Voltage (V) |
| 34 | 4B | float32 | Nominal Voltage (V) |
| 38 | 4B | float32 | Energy Density (Wh/kg) |
| 42 | 2B | int16 | Battery ID (BE) |

### 5.4 Write Battery Parameters (`QueryID = 0x05`)

**Request data** = `BuildBatteryBytes(BatteryDTO)` — same layout as above (bytes 0-43).

### 5.5 Sync Time (`QueryID = 0x06`)

**Request data** = `GetEpochTimeBytes()` = 4-byte Big-Endian Unix timestamp of `DateTime.UtcNow`.

---

## 6. Program Commands (0xBB)

### 6.1 Program Download Full Flow

```
App                                              Device
|                                                 |
|-- 1. TCP: StartProgram (0xEE 0x01 + SessionID) ->|
|<-- ACK (bool) ---------------------------------|
|                                                 |
|-- 2. TCP: HWReady (0xBB 0x01) ----------------->|
|<-- ACK (bool) ---------------------------------|
|                                                 |
|-- 3. TCP: SendStepCount (0xBB 0x03, count=N) -->|
|<-- ACK (bool) ---------------------------------|
|                                                 |
|  +-- For each chunk i = 0..N-1 ---------------+|
|  |  TCP: SendStepData (0xBB 0x04, [2B len][chunk_i]) |
|  |  <-- ACK (bool) ----------------------------+|
|  +----------------------------------------------|
|                                                 |
|-- 4. TCP: SendDbcStepCount (0xBB 0x07, count=M) >|
|<-- ACK (bool) ---------------------------------|
|                                                 |
|  +-- For each chunk j = 0..M-1 ---------------+|
|  |  TCP: SendDbcData (0xBB 0x08, [2B len][chunk_j]) |
|  |  <-- ACK (bool) ----------------------------+|
|  +----------------------------------------------|
|                                                 |
|-- 5. Live telemetry flows on UDP :10000 --------->|
|-- 6. Store packets flow on UDP :10001 ----------->|
```

### 6.2 Program Step Count (`QueryID = 0x03`)

**Request data** = number of chunked payloads (2-byte Big-Endian short).

### 6.3 Program Step Data (`QueryID = 0x04`)

**Request data** = `[2B length BE][payload chunk]` where each chunk is <= 1400 bytes.

### 6.4 DBC Step Count (`QueryID = 0x07`)

**Request data** = number of DBC binary payload chunks (2-byte Big-Endian short).

### 6.5 DBC File Data (`QueryID = 0x08`)

**Request data** = `[2B length BE][DBC chunk]` where each chunk is <= 1400 bytes.

### 6.6 Live Step Update (`QueryID = 0x09`)

Amends the **parameters** of the step currently executing on the hardware - the operator itself
cannot change, and nothing is written back to the stored program (no Q1/Q2/Q3 handshake; gated on
a program already being Running or Paused, the opposite of the download flow above).

```
Request:  [0xBB, DevID, CirID, 0x09, 2B length BE, one AA55...55AA step packet]
Response: [0xBB, DevID, CirID, 0x09, STATUS, REASON]
```

`STATUS` is `0x01` on success. `REASON` (`payload[5]`) is `0x00` on success, otherwise one of:

| REASON | Meaning |
|--------|---------|
| `0x01` | Program is idle, or awaiting operator action (interrupt / error wait / message wait) |
| `0x02` | Step number is not the currently executing step |
| `0x03` | Operator change not permitted - parameters only |
| `0x04` | Operator not eligible for live update (TABLE / BEG / CYC / GOTO and other non-regulating operators) |
| `0x05` | Secondary NACK (any cause), or Primary-Secondary link failure |
| `0x06` | Malformed step packet |
| `0x07` | A previous live update is still in flight - retry once the pending response arrives |

Eligible operators (allow-list, everything else -> `0x04`): `CC_Chg CV_Chg CP_Chg CCCV_Chg CC_DChg
CP_DChg CCCV_DChg CV_DChg PAU`. `ChannelCommandHandler.LiveStepUpdateAsync`, wired to the
Dashboard's per-circuit context menu ("Edit Current Step").

### 6.7 Jump to Program Step (`QueryID = 0x0A`) — Implemented

> Source of truth: `bm_program_v3.2.md` (Q10). Ends the step a running channel is currently
> executing and resumes from an arbitrary step number, **without modifying the resident
> program** — the same effect a `GOTO` step authored into the program would have, triggered live
> from the dashboard instead. `ChannelCommandHandler.JumpToStepAsync`, wired to the Dashboard's
> per-circuit context menu ("Jump to Step...").

```
Query:    BB DEV CKT 0A STEP_HI STEP_LO CRC_LO CRC_HI
Response: BB DEV CKT 0A STATUS  REASON  CRC_LO CRC_HI
```

**Request data** = target step number, 2-byte Big-Endian, 1-based (same numbering the `0xCC`
measured-parameters frame reports at payload offset 0-1).

**Response**: `STATUS` (`0x01` = jump performed, `0x00` = rejected) + `REASON`:

| Value | Meaning | Raised by |
|-------|---------|-----------|
| `0x00` | Jump performed (`STATUS = 0x01`) | — |
| `0x01` | No program is running or paused on this circuit | Primary |
| `0x02` | Target step does not exist in the resident program | Primary |
| `0x03` | Malformed frame (wrong length) | Primary |
| `0x04` | Secondary NACK (any cause), or Primary<->Secondary link failure | Primary |
| `0x05` | A previous jump is still in flight — not evaluated, safe to retry once it resolves | Primary |

`0x01`-`0x03` and `0x05` can only ever originate on the Primary; the `ps_program` Q9 ACK/NACK frame
carries no reason field, so **every** Secondary-side rejection (paused, an operator-wait state, a
step request already in flight) reaches the Web App as `0x04` — treat it as "rejected by the
hardware, try again in a moment", not as a link failure.

**Eligibility — no allow-list.** Any step number that exists in the loaded program is a valid jump
target (`BEG`/`CYC`/`GOTO`/`TABLE`/`SET`/`REG` included) — the Primary validates only that the step
exists. A **paused** program rejects the jump (`REASON 0x04`) — the operator must Continue first.
Jumping onto a loop's own `BEG` step resets that loop's iteration counter to zero; jumping within a
loop it's already inside leaves the counter unchanged; jumping out leaves the abandoned loop's
partial count in place.

**Effect on the truncated step**: elapsed time, step capacity/energy, and cutoff debounce counters
reset; an end-of-step registration record is written for it. Program-level elapsed time and
Ah/Wh, and any registration type/parameters carried from an earlier `SET`/`REG`/`TABLE` step,
**continue** unaffected — this is the opposite of a live step-parameter amendment, which would
preserve the step's own accumulators instead.

Full detail: `docs/manual-extract/bm_program_v3.2.md` (Q9 + Q10, both implemented).

---

## 7. Control Commands (0xEE)

| QueryID | Name | Action | Response |
|---------|------|--------|----------|
| `0x01` | Start | Begin test execution (sends packed SessionID as data) | `bool` via `payload[4]` |
| `0x02` | Stop | Stop running test | `bool` via `payload[4]` |
| `0x03` | Pause | Pause current step | `bool` via `payload[4]` |
| `0x04` | Continue | Resume paused test | `bool` via `payload[4]` |
| `0x05` | SyncTime | Clock synchronization (4-byte epoch) | `bool` via `payload[4]` |
| `0x06` | SystemReset | Soft reboot/reset circuit | `bool` via `payload[4]` |

---

## 8. Registration & Broadcast (0xDD)

### 8.1 Device Registration Packet (QueryID = 0x01/0x02)

**Incoming registration packet** from device (`ParseRegistrationPacket`, >= 32 bytes):

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Start Byte (0xDD) |
| 1 | 1B | byte | Query ID |
| 2 | 1B | byte | Length / Reserved |
| 3 | 1B | byte | Device ID |
| 4 | 1B | byte | Circuit ID |
| 5 | 16B | string | Device Name (ASCII, null-padded) |
| 21 | 4B | bytes | IP Address (4 bytes, dotted-decimal) |
| 25 | 6B | bytes | MAC Address (6 bytes, colon-hex) |

**Registration handshake response** (`ParseRegistrationResponse`):

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | 0xDD (Start Byte) |
| 1 | 1B | byte | 0x01 (Query ID) |
| 2 | 1B | byte | Device ID |
| 3 | 1B | byte | Circuit ID |
| 4 | 1B | byte | Command Status (0x01=Success, 0x00=Fail, 0x02=AlreadyRegistered) |
| + 2B | CRC16 | | |

### 8.2 Broadcast: Device Discovery (Q3)

**Request:** `[0xDD, 0x03]` + CRC16 (4 bytes). Sent to `255.255.255.255:10003`.

**Response** (44 bytes):

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | 0xDD (Start Byte) |
| 1 | 1B | byte | 0x03 (Query ID) |
| 2 | 4B | bytes | Unique ID |
| 6 | 4B | IP | Remote IP (Client) |
| 10 | 4B | int32 | TCP Port (BE) |
| 14 | 4B | IP | Device IP Address |
| 18 | 4B | IP | Subnet Mask |
| 22 | 4B | IP | Gateway IP |
| 26 | 4B | IP | DNS 1 IP |
| 30 | 4B | IP | DNS 2 IP |
| 34 | 4B | int32 | UDP Live Port (BE) |
| 38 | 4B | int32 | UDP Registration Port (BE) |
| 42 | 2B | CRC16 | Checksum |

### 8.3 Broadcast: Change Network Config (Q4)

**Request:** `[0xDD, 0x04, UniqueID(4B), DeviceIP(4B), SubnetMask(4B), Gateway(4B), DNS1(4B), DNS2(4B)]` + CRC16

**Response:** `[0xDD, 0x04, StatusByte(0x01=ok)]` + CRC16

### 8.4 Broadcast: Change Server Config (Q5)

**Request:** `[0xDD, 0x05, UniqueID(4B), RemoteIP(4B), TcpPort(4B), UdpLivePort(4B), UdpRegPort(4B)]` + CRC16

**Response:** `[0xDD, 0x05, StatusByte(0x01=ok)]` + CRC16

---

## 9. Calibration Commands (0xA0)

### 9.1 Calibration Flow

```
App                                               Device
|                                                  |
|-- HWReadyToCalibration (0xA0 0x01) ------------->|
|<-- ACK (bool) ---------------------------------|
|                                                  |
|-- SendLiveCurrentVoltage (0xA0 0x02) ---------->|
|<-- ACK (bool) ---------------------------------|
|                                                  |
|-- CalibrationPointPreset (0xA0 + QID + 5B hdr + float) >|
|<-- ACK (bool, at payload[4]) ------------------|
|                                                  |
|-- SetGainOffset (0xA0 + QID + Gain(4B) + Offset(4B) + Epoch(4B)) >|
|<-- ACK (bool, at payload[5]) ------------------|
|                                                  |
|-- PreviousCalibration (0xA0 0x17) ------------->|
|<-- Full 163-byte CalibrationData --------------|
```

### 9.2 Calibration Query IDs

| QueryID | Hex | Name |
|---------|-----|------|
| 0x01 | IsReady | Query readiness |
| 0x02 | SendLive | Start streaming calibration data |
| 0x03 | CurrentChargeLowPoint | Set low point (Current, Charge) |
| 0x04 | CurrentChargeHighPoint | Set high point |
| 0x05 | CurrentChargeGainOffset | Set gain + offset |
| 0x06 | CurrentDisChargeLowPoint | Set low point (Current, Discharge) |
| 0x07 | CurrentDisChargeHighPoint | Set high point |
| 0x08 | CurrentDisChargeGainOffset | Set gain + offset |
| 0x09 | VoltageChargeLowPoint | Set low point (Voltage, Charge) |
| 0x0A | VoltageChargeHighPoint | Set high point |
| 0x0B | VoltageChargeGainOffset | Set gain + offset |
| 0x0C | VoltageDisChargeLowPoint | Set low point (Voltage, Discharge) |
| 0x0D | VoltageDisChargeHighPoint | Set high point |
| 0x0E | VoltageDisChargeGainOffset | Set gain + offset |
| 0x0F | TemperatureLowPoint | Set low point (Temperature) |
| 0x10 | TemperatureHighPoint | Set high point |
| 0x11 | TemperatureGainOffset | Set gain + offset |
| 0x12 | CancelCalibration | Abort calibration |
| 0x13 | StopCalibration | Stop calibration session |
| 0x14 | StartChargeVerifyCurrent | Start charge current verification |
| 0x15 | StartDisChargeVerifyCurrent | Start discharge current verification |
| 0x16 | StopVerifyCurrent | Stop verification |
| 0x17 | PreviousCalibration | Fetch full calibration data (163B) |

### 9.3 Previous Calibration Record (163 Bytes)

| Offset | Size | Field |
|--------|------|-------|
| 0 | 1B | Start Byte (0xA0) |
| 1 | 1B | Device ID |
| 2 | 1B | Circuit ID |
| 3 | 1B | Query ID (0x17) |
| 4 | 1B | Error Status Code |
| **RangeFull (bytes 5-52):** | | |
| 5-8 | 4B | Current Charge Gain (float32 BE) |
| 9-12 | 4B | Current Charge Offset (float32 BE) |
| 13-16 | 4B | Current Charge DateTime (Unix Epoch) |
| 17-20 | 4B | Current Discharge Gain (float32 BE) |
| 21-24 | 4B | Current Discharge Offset (float32 BE) |
| 25-28 | 4B | Current Discharge DateTime (Unix Epoch) |
| 29-32 | 4B | Voltage Charge Gain (float32 BE) |
| 33-36 | 4B | Voltage Charge Offset (float32 BE) |
| 37-40 | 4B | Voltage Charge DateTime (Unix Epoch) |
| 41-44 | 4B | Voltage Discharge Gain (float32 BE) |
| 45-48 | 4B | Voltage Discharge Offset (float32 BE) |
| 49-52 | 4B | Voltage Discharge DateTime (Unix Epoch) |
| **Range1 (bytes 53-76):** | 24B | Current Charge Gain/Offset/DT + Discharge Gain/Offset/DT |
| **Range2 (bytes 77-100):** | 24B | Same structure (Current only) |
| **Range3 (bytes 101-124):** | 24B | Same structure (Current only) |
| **Range4 (bytes 125-148):** | 24B | Same structure (Current only) |
| **Temperature (bytes 149-160):** | | |
| 149-152 | 4B | Temperature Gain (float32 BE) |
| 153-156 | 4B | Temperature Offset (float32 BE) |
| 157-160 | 4B | Temperature DateTime (Unix Epoch) |
| **Footer:** | | |
| 161-162 | 2B | CRC-16 Checksum |

---

## 10. Real-Time Live Telemetry (0xCC)

Received on **UDP port 10000**. Dispatched by `ChannelManager.ViewUdpData()`.

### 10.1 Packet Dispatch by Type Byte

```
payload[0] = 0xCC (StartByte.LiveData)
payload[1] = DeviceID
payload[2] = CircuitID
payload[3] = Type byte
```

| Type Byte | Content | Decoder |
|-----------|---------|---------|
| `0x01` | Live telemetry | `DecoderService.ParseRealTimeData(payload)` -> `RealTimeRecordDto` |
| `0x02` | DBC signal values | `DecoderService.ParseDBCValues(payload)` -> `DbcRecord` |
| `0xA0` | Calibration live | `DecoderService.ParseRealTimeData(payload)` -> calibration buffer |

### 10.2 LiveData (Type 0x01) — Full Payload Layout

Minimum 37 bytes. CRC validation is logged but non-fatal.

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Start Byte (0xCC) |
| 1 | 1B | byte | Device ID |
| 2 | 1B | byte | Circuit ID |
| 3 | 1B | byte | Query ID (skip) |
| 4 | 2B | int16 | Step Number (BE) |
| 6 | 1B | byte | Program Running Status (0x00=Stop, 0x01=Running) |
| 7 | 1B | byte | Circuit Status (CircuitStatus enum: 0x00-0x08) |
| 8 | 1B | byte | Error ID |
| 9 | 4B | int32 | System Error Bitmask (BE, SystemError flags) |
| 13 | 4B | int32 | Step Running Time in ms (BE) |
| 17 | 4B | int32 | Total Running Time in ms (BE) |
| 21 | 4B | float32 | Current (A) |
| 25 | 4B | float32 | Voltage (V) |
| 29 | 4B | float32 | Temperature (C) |
| 33 | 4B | float32 | Power (W) |
| 37 | 4B | float32 | Accumulated Capacity (Ah) |
| 41 | 4B | float32 | Charge Capacity (Ah) |
| 45 | 4B | float32 | Discharge Capacity (Ah) |
| 49 | 4B | float32 | Step Capacity (Ah) |
| 53 | 4B | float32 | Accumulated Energy (Wh) |
| 57 | 4B | float32 | Charge Energy (Wh) |
| 61 | 4B | float32 | Discharge Energy (Wh) |
| 65 | 4B | float32 | Step Energy (Wh) |
| 69 | 1B | byte | Operator Code (OperatorConstants) |
| 70 | 1B | byte | Cycle Status |
| 71 | 2B | int16 | Cycle Number (BE) |
| 73 | 2B | int16 | Cycle RUN Iteration (BE) |
| 75 | 2B | int16 | Table Step Number (BE) |
| 77 | 2B | int16 | Table Total Row Number (BE) |
| 79 | 2B | — | Reserved (skipped) |
| 81 | 3B | bytes | IO Status (see Section 17) |
| 84 | 2B | CRC16 | Checksum (Big-Endian UInt16) |

**Total: 86 bytes** (84 data + 2 CRC)

### 10.3 DBC Values (Type 0x02) — `ParseDBCValues`

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Start Byte (0xCC) |
| 1 | 1B | byte | Device ID |
| 2 | 1B | byte | Circuit ID |
| 3 | 1B | byte | Query ID |
| 4+ | N x entries | | `[SignalID 1B][Header 1B][Data 0-16B]` (see Section 13) |
| last-2 | 2B | — | CRC (stripped by caller) |

---

## 11. Storage Protocol V1

Received on **UDP port 10001**. Decoded by `DecoderService.RealStoreData()`.

### 11.1 Header (12 bytes)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Start Byte (0xCC) |
| 1 | 1B | byte | Device ID |
| 2 | 1B | byte | Circuit ID |
| 3 | 1B | byte | Query ID |
| 4 | 4B | int32 | Session ID (packed, see Section 3) |
| 8 | 2B | int16 | Step Number (BE) |
| 10 | 1B | byte | Operator Code |
| 11 | 1B | byte | Circuit Status |

### 11.2 Opcode-Tagged Body

After the header, data fields stream as `[Opcode Byte] + [Value]`:

| Opcode | Size | Type | Field | Decoding |
|--------|------|------|-------|----------|
| `0x01` | 4B | int32 BE | Program Running Time (ms) | Creates new `MeasurementData` row; previous row flushed |
| `0x02` | 4B | float32 BE | Current (A) | `current.Current = floatValue` |
| `0x03` | 4B | float32 BE | Voltage (V) | `current.Voltage = floatValue` |
| `0x04` | 4B | float32 BE | Temperature (C) | `current.Temperature = floatValue` |
| `0x05` | 4B | float32 BE | Power (W) | `current.Power = floatValue` |
| `0x06` | 4B | float32 BE | Accumulated Capacity (Ah) | `current.AccumulatedCapacity` |
| `0x07` | 4B | float32 BE | Charge Capacity (Ah) | `current.ChargeCapacity` |
| `0x08` | 4B | float32 BE | Discharge Capacity (Ah) | `current.DischargeCapacity` |
| `0x09` | 4B | float32 BE | Step Capacity (Ah) | `current.StepCapacity` |
| `0x0A` | 4B | float32 BE | Accumulated Energy (Wh) | `current.AccumulatedEnergy` |
| `0x0B` | 4B | float32 BE | Charge Energy (Wh) | `current.ChargeEnergy` |
| `0x0C` | 4B | float32 BE | Discharge Energy (Wh) | `current.DischargeEnergy` |
| `0x0D` | 4B | float32 BE | Step Energy (Wh) | `current.StepEnergy` |
| `0x0E` | 4B | int32 BE | System Error Bitmask | Bitwise-AND against `SystemError` flags -> `Remark` = comma-joined names |
| `0x0F` | 1B | uint8 | Message ID | `current.Remark` = `ErrorMessages.Messages[id].Message` |
| `0x10` | 1B | uint8 | Error ID | `current.Remark` = `ErrorMessages.Errors[id].Message` |
| `0x11` | 2B | int16 BE | Custom Command Code | `current.Remark` = `CommandTracker.GetCommand(id, true)` |
| `0x00` | — | — | DBC terminator | Marks end of DBC entries for current row |
| `0x1F-0xFA` | Var | Sub-byte header | DBC Signal Entry | See Section 13 |

**Row lifecycle:** Opcode `0x01` flushes the previous row into the list and creates a new one. DBC entries accumulate into a `Dictionary<string, object?>`. At end-of-packet, the last row is flushed.

**DBC back-fill:** After all rows, the first row with > 1 DBC entries has its `dbcValues` JSON copied to every row lacking DBC data.

---

## 12. Storage Protocol V2

Received on **UDP port 10001**. Decoded by `DecoderService.RealStoreDataV2()` — **active store-path decoder**.

### 12.1 Header (14 bytes)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Start Byte (0xCC) |
| 1 | 1B | byte | Device ID |
| 2 | 1B | byte | Circuit ID |
| 3 | 1B | byte | Query ID |
| 4 | 4B | int32 | Session ID (packed) |
| 8 | 2B | int16 | Step Number (BE) |
| 10 | 1B | byte | Operator Code |
| 11 | 1B | byte | Circuit Status |
| 12 | 2B | uint16 | Presence Bitmask (BE) |

### 12.2 Presence Bitmask

Bit N (0..16) -> opcode (N+1) is present:

```
Bit:  16 15 14 13 12 11 10 9  8  7  6  5  4  3  2  1  0
Op:   17 16 15 14 13 12 11 10 9  8  7  6  5  4  3  2  1
```

Example: `0x0006` -> bits 1,2 set -> opcodes 1,2 -> each row = `[RunningTime 4B][Current 4B]`.

### 12.3 Parse Loop

1. Collect present opcode indices in ascending order
2. Read fields in that order — **no per-field opcode bytes** in stream
3. Wrap around; **one full cycle = one row**
4. Opcode 1 creates new `MeasurementData` (flushes previous)
5. **No DBC support** (opcodes 31-250 are V1-only)

### 12.4 Field Sizes

| Opcode | Size | Type |
|--------|------|------|
| 1, 14 | 4B | int32 BE |
| 2-13 | 4B | float32 BE |
| 15, 16 | 1B | uint8 |
| 17 | 2B | int16 BE |

---

## 13. DBC Signal Decoding

### 13.1 DBC Entry Format

Each entry: `[SignalID 1B][Header 1B][Data 0-16B]`

**Header byte:** `DataType = (header >> 4) & 0x0F`, `DataLength = header & 0x0F`

| DataType | Meaning | Read Size | Value Decoding |
|----------|---------|-----------|----------------|
| 0 | Boolean | 4B | `ReadInt32BigEndian != 0` |
| 1 | Int32 | 4B | `ReadInt32BigEndian` |
| 2 | Int32 | 4B | `ReadInt32BigEndian` |
| 3 | Float32 | 4B | `ReadSingleBigEndian` |
| 4-15 | Raw bytes | DataLength B | `temp = (temp << 8) | payload[i+j]` |

Index advances by `DataLength` regardless of DataType.

---

## 14. Program Step Download & Framing

### 14.1 Conversion Pipeline

`DecoderService.ConvertProgramIntoBytesPackets(steps, resolvedPrograms)`:
1. **PRODUCER expansion:** `ExpandProducerSteps` inlines sub-programs (skipping outer SET + STO)
2. **Global variables:** `ExtractGlobalVariables` collects SET-step named constants
3. **Per-step encoding:** Each step produces bytes (see Section 15)
4. **Framing:** `BuildPackets` wraps each step

### 14.2 Packet Frame

```
+------+------+------------------+--------------+------+------+
| 0xAA | 0x55 | StepOffset (4B)  | Step Bytes   | 0x55 | 0xAA |
| 1 B  | 1 B  |    Big-Endian    |  (Variable)  | 1 B  | 1 B  |
+------+------+------------------+--------------+------+------+
```

- **StepOffset:** Cumulative byte offset to next step. Each offset = `previousStep.Length + 8`.
- **Last step sentinel:** `0xFFFFFFFF`.

### 14.3 Chunked Transmission

After framing, concatenated packets split into <= 1400-byte chunks:
1. Send count: `[0xBB, DevID, CirID, 0x03] + [count 2B BE]`
2. Each chunk: `[0xBB, DevID, CirID, 0x04] + [chunkLen 2B BE] + [data]` -> await ACK

### 14.4 Battery-Relative Units (ACNx, VNC, §12.3 Tokens)

The hardware has no native concept of any of these — both paths resolve entirely host-side, in `Utils/BatteryUnitResolver.cs`, before Section 15's byte encoding runs. Neither adds a new wire byte.

- **ACNx / VNC** (`BatteryUnitResolver.Resolve`) — a formula applied at encode time: `ACN`/`ACN1`/`ACN2`/`ACN4`/`ACN5`/`ACN10`/`ACN20` (bare `ACN` defaults to 5h) resolve to `value × (battery.NominalCapacity / X hours)` → plain unit `A`; `VNC` resolves to `value × battery.NumberOfCells` → plain unit `V`. Requires a battery with a non-zero `NominalCapacity` / `NumberOfCells` selected at transfer time, or throws `BatteryUnitResolutionException` (caught centrally in `ChannelCommandHandler.SetProgramAsync`, returned as `CommonResponse.Fail`). A resolved step encodes byte-identically to the equivalent plain `"A"`/`"V"` step (Section 15.7-15.11, existing `CutoffCondition`/`RegistrationType` bytes — no protocol change).
- **§12.3 bare tokens** (`BatteryUnitResolver.GetBatteryGlobalVariables`) — manual §12.3 "Using Battery Parameters", p.159-160: `CNom`, `NoCell`, `UGas`, `UMax`, `UNom`, `CutOff`, `INom`, `ICrank`, `ChargeF`, `EDensity` carry no multiplier; the bare name IS the value. Injected as ordinary `GlobalVariable` entries alongside the program's own SET-step constants (Section 14.1 step 2, `DecoderService.ConvertProgramIntoBytesPackets`), so `ProcessNominalValues`/`ProcessStandardLimit`/`AddRegistrations` need no changes — a step's own SET-defined variable of the same name always wins on a collision. `Rin` (internal resistance) is deliberately excluded: no `CutoffCondition` (15.8) or `RegistrationType` (15.11) byte exists for Ohms. `ProgramEditor.razor`/`ProgramViewer.razor` seed the same 10 names as placeholder variables (`GetPlaceholderGlobalVariables`) so bare tokens validate before any battery is selected.
- **Confirmed out of scope, protocol level:** Ramp and Resistance parameters — the wire format has no per-value type tag (the operator code alone tells the firmware which quantity to regulate), no `CutoffCondition`/`RegistrationType` byte exists for Resistance, and no ramp convention (start/end/duration) exists anywhere in this spec or `HardwareSimulator`. Both would require a firmware + wire-format change, not a Program Editor change.

See `docs/manual-extract/VNC-ACN-battery-parameters.md` for the manual's worked examples and full derivation.

---

## 15. Program Step Binary Encoding

Every step begins with:
```
[StepNumber 2B BE] [OperatorCode 1B]
```

### 15.1 SET (Opcode 10)

```
[Global Parameter Count 1B]     - No. of Ah/Wh global-parameter blocks (0 by default)
Per block (5B):
  [Code 1B]                     - 0x26 Accumulated Capacity (Ah) or 0x2A Accumulated Energy (Wh)
  [Value 4B BE]                 - float32
[RType 2B BE]       - Bitmask of active registration types
```

A SET step's nominal value written as `Ah = <n>` or `Wh = <n>` produces one global-parameter
block (Web App: `ProgramBuilder.ProcessSetOperator`), resetting `batAccuCapacityAh` /
`batAccuEnergyWh` (and the matching `presDetectAccu*` registration tracker) on the device. Any
other SET nominal-value name (`myVar = 5 A`) is a purely in-app named variable and produces no
block — a SET step with none of these is byte-identical to the pre-LOCKAh/PERCAh wire format
(count byte `0x00`). Whenever a global-parameter block is present and no registration was chosen
explicitly, the Web App inherits the nearest *preceding* SET step's Registrations (the same rule
§15.2's REG already uses), falling back to `STANDARD` only when there is no earlier SET to inherit
from.

**RType bitmask:** Sum of `unitValueMap[unit]` for matched standard's unit list + `ERR_E(8192)` + `MSG_E(16384)`.

### 15.2 REG (Opcode 15)

```
[RType 2B BE]       - Inherited from nearest preceding SET step
```

### 15.3 TABLE (Opcode 18)

```
[Table Binary Data]         - From BuildTableOPTxtToBinary
[Registration Count 1B]
[Registration Entries...]   - Each: [RGtype 1B][Value 4B BE]
```

### 15.4 PAU (Opcode 8)

```
[No limit count byte!]     - PAU omits the limit count
[Limit 1: value 4B BE]     - Float or int32 (time->ms)
[Action 1: opcode 1B + extra?]
...
[Registration Count 1B]
[Registration Entries...]
```

### 15.5 GOTO (Opcode 9)

```
[Target StepNumber 2B BE]
[Registration Count 1B]
[Registration Entries...]
```

### 15.6 STO (Opcode 11)

No additional data. Step = `[StepNumber 2B BE][0x0B]`.

### 15.7 Default (Opcodes 1-7, 12-14, 16, 17, 19, 21)

```
[Nominal Values...]                - Each: [Value 4B BE]
[Limit Count 1B]
[Limit: Unit 1B][Operator 1B][Value 4B BE]
[Action: Opcode 1B + extra?]
...
[Registration Count 1B]
[Registration Entries...]
```

### 15.8 Unit Byte Values (`CutoffCondition`)

| Byte | Unit | Byte | Unit |
|------|------|------|------|
| 0x31 | Current (A) | 0x3A | Accumulated Capacity (Ah) |
| 0x32 | Voltage (V) | 0x3B | Step Capacity (Ah) |
| 0x33 | Power (W) | 0x3C | Accumulated Energy (Wh) |
| 0x34 | Charge Capacity (Ah) | 0x3D | Step Energy (Wh) |
| 0x35 | Discharge Capacity (Ah) | 0x38 | Temperature (C) |
| 0x36 | Charge Energy (Wh) | 0x39 | Time (s) |
| 0x37 | Discharge Energy (Wh) | 0x3E | PercAh (%) — regulating operators only, never `PAU`/`LOCKAh` |

### 15.9 Operator Byte Values (`LogicOperator`)

| Byte | Operator |
|------|----------|
| 0x51 | `>` |
| 0x52 | `<` |
| 0x53 | `>=` |
| 0x54 | `<=` |
| 0x55 | `!=` |
| 0x56 | `==` |

### 15.10 Action Byte Values (`ActionType`)

| Byte | Action | Extra |
|------|--------|-------|
| 0x00 | Blank | None |
| 0x09 | GOTO | 2B target step (BE) |
| 0x0B | STO | None |
| 0x0E | INT | None |
| 0x10 | ERR | 1B error code |
| 0x11 | MSG | 1B message code |

### 15.11 Registration Byte Values (`RegistrationType`)

| Byte | Type | Byte | Type |
|------|------|------|------|
| 0x21 | Time | 0x28 | Discharge Capacity (Ah) |
| 0x22 | Current (A) | 0x29 | Step Capacity (Ah) |
| 0x23 | Voltage (V) | 0x2A | Accumulated Energy (Wh) |
| 0x24 | Temperature (C) | 0x2B | Charge Energy (Wh) |
| 0x25 | Power (W) | 0x2C | Discharge Energy (Wh) |
| 0x26 | Accumulated Capacity (Ah) | 0x2D | Step Energy (Wh) |
| 0x27 | Charge Capacity (Ah) | | |

### 15.12 Number Conversion (`ExtractFloatAsByteArraySafe`)

1. Strip letters for unit multiplier: `s/sec` -> x1000, `m/min` -> x60000, `h/hr` -> x3600000, else x1
2. Strip non-numeric chars; comma -> dot
3. If multiplier > 1: cast to int -> 4B BE integer (time in ms)
4. If multiplier = 1: keep as float -> 4B BE IEEE-754

### 15.13 Table Binary (`BuildTableOPTxtToBinary`)

Input: `time;A;W;V` (semicolon-delimited text)

```
[Row Count 2B BE]
Per row:
  [Field Count 1B]          - 1 (time) + count of present A/W/V
  [Opcode Flags 1B]         - Bit flags: Time=8, A=4, W=2, V=1
  [Time 4B BE]              - Milliseconds (int32)
  [A 4B BE]                 - Current (float32, if flag set)
  [W 4B BE]                 - Power (float32, if flag set)
  [V 4B BE]                 - Voltage (float32, abs, if flag set)
```

### 15.14 LOCKAh (Opcode 22)

```
[Multiplier 4B BE]  - float32, must be >= 1.0 (Web App validation). 1.0 = 100%.
```

Instantaneous — same minimal shape as GOTO (Opcode, one fixed payload, end). **No cutoff-condition
count byte and no registration count byte at all**, unlike every other regulating/default operator.
On execution the device stores `fabsf(batAccuCapacityAh) * Multiplier` as the reference a later
`PERCAh` cutoff (§15.8, byte `0x3E`) compares against, then ends the step immediately.
`LOCKAh` never carries a Limit/Cutoff section, so `PERCAh` can never appear on it; `PERCAh` is
likewise never valid on `PAU`. See
[`../superpowers/specs/2026-09-21-lockah-percah-webapp-design.md`](../superpowers/specs/2026-09-21-lockah-percah-webapp-design.md)
for the full design and the worked bytes.

---

## 16. DBC Multi-Port Payload

Built by `DbcDatabase.BuildMultiPortPayload(port1, port2, port3)`.

### 16.1 Overall Layout

```
+-----------------------------------------------------+
|                 Port Headers (36 bytes)               |
|  Port 1 Header (12B) | Port 2 Header (12B) | Port 3  |
+-----------------------------------------------------+
|  Port 1 Data (if active):                            |
|  |-- Rx Message Blocks (17B each)                    |
|  |-- Tx Message Blocks (15B each)                    |
|  +-- Rx Signal Blocks  (13B each)                    |
+-----------------------------------------------------+
|  Port 2 Data (if active) ...                         |
+-----------------------------------------------------+
|  Port 3 Data (if active) ...                         |
+-----------------------------------------------------+
```

### 16.2 Port Header (12 bytes)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Port Enable (0x01=active, 0x00=inactive) |
| 1 | 1B | byte | CAN Baudrate (0=250K, 1=500K, 2=750K, 3=1M) |
| 2 | 1B | byte | Rx Message Count |
| 3 | 1B | byte | Tx Message Count |
| 4 | 4B | uint32 | Rx Message Config Offset (BE, from payload start) |
| 8 | 4B | uint32 | Tx Message Config Offset (BE, from payload start) |

### 16.3 Rx Message Block (17 bytes)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | IdType (0=Standard, 1=Extended CAN) |
| 1 | 4B | uint32 | MsgId (BE; Extended: Id - 0x80000000) |
| 5 | 1B | byte | DLC (Data Length Code) |
| 6 | 2B | uint16 | Periodicity / IntervalMs (BE) |
| 8 | 1B | byte | MTO Enable (0x00) |
| 9 | 1B | byte | MTO Action (0=Normal, 1=Remote Frame) |
| 10 | 2B | uint16 | MsgTmOt / Timeout (BE, 0x0000) |
| 12 | 1B | byte | Rx Signal Count |
| 13 | 4B | uint32 | Rx Signal Offset (BE, patched at runtime) |

### 16.4 Tx Message Block (15 bytes)

Same as Rx but omits the 2-byte MsgTmOt field (bytes 10-11).

### 16.5 Rx Signal Block (13 bytes)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| 0 | 1B | byte | Start Bit (LSB position) |
| 1 | 1B | byte | Bit Length |
| 2 | 1B | byte | Value Type (0=Unsigned, 1=Signed) |
| 3 | 1B | byte | Byte Order (0=Motorola/BE, 1=Intel/LE) |
| 4 | 1B | byte | Signal ID (31-255, globally unique across all 3 ports) |
| 5 | 4B | float32 | Factor (BE) |
| 9 | 4B | float32 | Offset (BE) |

### 16.6 Signal ID Assignment

Before calling `BuildMultiPortPayload`, `TransferDbcFile` assigns IDs sequentially:
- Counter starts at **31** (0-30 reserved)
- Increments per selected signal: Port1 -> Port2 -> Port3
- Maximum **255**; overflow -> `SingalId = 0` (unselected)

---

## 17. Digital I/O Bit Mapping

3 bytes in LiveData packet at offset 81:

```
Byte 0: Digital Inputs
  Bits 7-4 -> Primary Board: DI3, DI2, DI1, DI0
  Bits 3-0 -> Secondary Board: DI3, DI2, DI1, DI0

Byte 1: Digital Outputs - Secondary Board
  Bits 7-0 -> DO7, DO6, DO5, DO4, DO3, DO2, DO1, DO0

Byte 2: Digital Outputs - Primary Board
  Bits 2-0 -> DO2, DO1, DO0 (Bits 7-3 unused)
```

Each bit produces an `IOStatus { Id, Value (0|1), Board, Type }`.

---

## 18. Reference Tables & Enums

### 18.1 StartByte

| Byte | Name | Used For |
|------|------|----------|
| `0xAA` | Configuration | HW config, manufacturing, battery, time sync |
| `0xBB` | Program | Program download, DBC upload |
| `0xEE` | Control | Start, Stop, Pause, Continue, Reset |
| `0xDD` | Registration | Device registration, SADP broadcast |
| `0xA0` | Calibration | Calibration commands and data |
| `0xCC` | LiveData | Real-time telemetry and store packets |

### 18.2 CircuitStatus

| Value | Name | Description |
|-------|------|-------------|
| 0x00 | Idle | No test running |
| 0x01 | Charge | Active charge |
| 0x02 | Discharging | Active discharge |
| 0x03 | Pause | Step paused |
| 0x04 | Continue | Resuming |
| 0x05 | Interrupt | Interrupted |
| 0x06 | Error | Alarm state |
| 0x07 | Msg | System message |
| 0x08 | Offline | Disconnected |

### 18.3 SystemError Bitmask

| Bit | Hex | Flag | Description |
|-----|-----|------|-------------|
| 0 | 0x0001 | en_ERR_LNT | Low Negative Temperature |
| 1 | 0x0002 | en_ERR_ZNT | Zero Negative Temperature |
| 2 | 0x0004 | en_ERR_OVER_TEMPERATURE | Over Temperature |
| 3 | 0x0008 | en_ERR_CURRENT_SETPOINT_UNREACHABLE | Current Setpoint Unreachable |
| 4 | 0x0010 | en_ERR_VOLTAGE_SETPOINT_UNREACHABLE | Voltage Setpoint Unreachable |
| 5 | 0x0020 | en_ERR_OVER_CURRENT | Over Current |
| 6 | 0x0040 | en_ERR_OVER_VOLTAGE | Over Voltage |
| 7 | 0x0080 | en_ERR_REVERSE_POLARITY_VOLTAGE_SENSE | Reverse Polarity |
| 8 | 0x0100 | en_ERR_PRIM_SEC_COM | Primary-Secondary Comms Error |
| 9 | 0x0200 | en_ERR_INVALID_CMD_PRIM_TO_SEC | Invalid Command P->S |
| 10 | 0x0400 | en_ERR_POWER_FAIL | Power Fail |
| 11 | 0x0800 | en_ERR_EEPROM_R_WR | EEPROM Fault |
| 12 | 0x1000 | en_ERR_TEMP_FB | Temperature Feedback Fault |
| 13 | 0x2000 | en_ERR_NETWORK_CONN_FAIL | Network Connection Lost |
| 14 | 0x4000 | en_ERR_POWER_SETPOINT_UNREACHABLE | Power Setpoint Unreachable |
| 15 | 0x8000 | en_ERR_OVER_POWER | Over Power |
| 16 | 0x10000 | en_ERR_INVALID_PROG_STEP | Invalid Program Step |

### 18.4 Step Operator Opcodes

| Code | Hex | Name | Category |
|------|-----|------|----------|
| 1 | 0x01 | CC_CHG | Charge |
| 2 | 0x02 | CV_CHG | Charge |
| 3 | 0x03 | CP_CHG | Charge |
| 4 | 0x04 | CCCV_CHG | Charge |
| 5 | 0x05 | CC_DCHG | Discharge |
| 6 | 0x06 | CP_DCHG | Discharge |
| 7 | 0x07 | CCCV_DCHG | Discharge |
| 8 | 0x08 | PAU | Pause |
| 9 | 0x09 | GOTO | Navigation |
| 10 | 0x0A | SET | Config |
| 11 | 0x0B | STO | Execution |
| 12 | 0x0C | CYC | Loop |
| 13 | 0x0D | BEG | Loop |
| 14 | 0x0E | INT | Control |
| 15 | 0x0F | REG | Config |
| 16 | 0x10 | ERR | Control |
| 17 | 0x11 | MSG | Control |
| 18 | 0x12 | TABLE | Control |
| 19 | 0x13 | CV_DCHG | Discharge |
| 20 | 0x14 | PRODUCER | Control (expanded, never on wire) |
| 21 | 0x15 | CC_RECHG | Charge |
| 22 | 0x16 | LOCKAh | Lock — instantaneous, no cutoffs/registration (see §15.10) |

### 18.5 CommandStatus

| Value | Name |
|-------|------|
| 0x00 | Failed |
| 0x01 | Success |
| 0x02 | Already Registered |

### 18.6 CAN Baudrate

| Value | Speed |
|-------|-------|
| 0 | 250 Kbps |
| 1 | 500 Kbps |
| 2 | 750 Kbps |
| 3 | 1 Mbps |

---

*Generated from source code inspection of `DecoderService.cs`, `ProgramBuilder.cs`, `ChannelCommandHandler.cs`, `ChannelManager.cs`, `DbcParser.cs`, `Models/Enums/CircuitEnums.cs`, `OperatorConstants.cs`, `CommandTracker.cs`, and `RequestDTOs.cs`.*
