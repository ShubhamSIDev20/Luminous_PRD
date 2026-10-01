# Appendix A — Reference Tables

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

### A.1 Port assignments

| Port | Protocol | Direction | Purpose | Handler |
|---|---|---|---|---|
| 9999 | TCP | App ↔ Device | All commands + registration. Request/response, 15 s timeout | `RunCommandListenerAsync` / `DeviceConnection` |
| 10000 | UDP | Device → App | Live telemetry (`0xCC` t1), DBC values (`0xCC` t2), calibration live (`0xA0`) | `RunUdpViewListenerAsync` |
| 10001 | UDP | Device → App | High-volume store packets (Storage Protocol V2) | `RunUdpStoreListenerAsync` |
| 10002 † | UDP | App → broadcast | Device discovery (Q3), network config (Q4), server config (Q5). Sent from **every** up/non-loopback/IPv4 interface, not once to `255.255.255.255` | `BroadcastUdpService.SendBroadcastAsync` (`SendPort`) |
| 10003 † | UDP | Device → App | Replies to broadcast queries | `BroadcastUdpService.ReceiveLoopAsync` (`ListenPort`) |

† **The broadcast ports above are what the code does.** `docs/PROTOCOL.md` §1.2 documents
these as `10003` (send) / `10004` (receive) instead. See Chapter 15 — the divergence is
unresolved; the table here reflects `BroadcastUdpService.cs:21-22`.

### A.2 Start bytes

| Value | Name | Meaning |
|---|---|---|
| `0xAA` | Configuration | Hardware / configuration commands |
| `0xBB` | Program | Program and DBC data download |
| `0xCC` | LiveData | Telemetry and store packets (device → app) |
| `0xDD` | Registration | Registration, delete, broadcast |
| `0xEE` | Control | Start / Stop / Pause / Continue / Reset |
| `0xA0` | Calibration | Calibration command family |

### A.3 Query IDs by family

| Family | Query | ID |
|---|---|---|
| Configuration `0xAA` | HWReady | `0x01` |
| | ReadFactoryConfig | `0x02` |
| | ReadManufacturingConfig | `0x03` |
| | ReadBatteryParams | `0x04` |
| | WriteBatteryParams | `0x05` |
| | SyncTime | `0x06` |
| Program `0xBB` | HWReadyForProgram | `0x01` |
| | SendProgramStepsCount | `0x03` |
| | SendProgram | `0x04` |
| | SendDbcStepsCount | `0x07` |
| | SendDbcFile | `0x08` |
| Control `0xEE` | Start | `0x01` |
| | Stop | `0x02` |
| | Pause | `0x03` |
| | Continue | `0x04` |
| | SyncTime | `0x05` |
| | SystemReset | `0x06` |
| Registration `0xDD` | Registration | `0x01` |
| | Delete | `0x02` |

### A.4 Registration status codes

| Value | Name | Meaning |
|---|---|---|
| `0x00` | Failed | Not approved, or the add failed. Hardware retries. |
| `0x01` | Success | New handler created this run. Channel is live. |
| `0x02` | AlreadyRegistered | Handler existed; socket re-attached after reconnect. |

### A.5 Packed Session ID bit layout

```
  Bit:  31        24 23        16 15                        0
       +-----------+------------+---------------------------+
       | Device ID | Addr byte  |  Epoch seconds (low 16)   |
       |  (8 bit)  |  (8 bit)   |         (16 bit)          |
       +-----------+------------+---------------------------+

  Encoding:
    packed = ((deviceId & 0xFF) << 24)
           | ((addressByte & 0xFF) << 16)
           |  (epochSeconds & 0xFFFF)
    written big-endian as 4 bytes

  Decoding (SessionIdToDateTime):
    1. take now's high 16 epoch bits  (nowEpoch & 0xFFFF0000)
    2. OR in the stored low 16 bits
    3. correct +/- 65,536 s if the candidate is more than
       32,768 s in the future or the past

  Accuracy guarantee: correct if the session started within
  roughly 9 hours of the moment of decoding.
```

The address byte itself packs board and channel into one nibble each
(`Utils.ChannelAddressCodec.Encode` / `.Decode`):

```
  Bit:  7  6  5  4   3  2  1  0
       +--------+--------+
       | Board  |Channel |     addressByte = (board << 4) | channel
       | (0-8)  | (1-8)  |
       +--------+--------+

  1-0-1  ->  0x01        board 0, channel 1   (current field hardware)
  1-1-1  ->  0x11        board 1, channel 1   (legacy / EncodeLegacy)
  1-8-8  ->  0x88        board 8, channel 8   (64th channel of a device)
```

| | Board nibble | Channel nibble |
|---|---|---|
| Nibble can hold | 0-15 | 0-15 |
| `Encode` accepts | **0-8** (throws otherwise) | **1-8** (throws otherwise) |
| `Decode` validates | ✗ no — masks and returns 0-15 | ✗ no — masks and returns 0-15 |

⚠️ **`Decode` does not validate**, so a malformed or hostile packet can yield board 9-15 or
channel 0. Callers that look a channel up by decoded address must handle "no such channel"
rather than assuming the pair is in range.

⚠️ **Board `0` and board `1` are both real and both in use.** `EncodeLegacy(channel)` is
`Encode(1, channel)` — board `1` is the pre-multi-board implicit board (and the `IsImplicit`
sentinel in `DeviceChannelRepository.GetOrCreateBoardAsync`), while current field hardware
registers on board `0`. Never treat board `0` as "unset". Simulate the board-`0` topology with
`python run_sim.py --board-base 0` (see `HardwareSimulator/README.md`).

### A.6 Storage Protocol V2 header (UDP 10001)

| Offset | Size | Field |
|---|---|---|
| 0 | 1 B | Start byte `0xCC` |
| 1 | 1 B | Device ID |
| 2 | 1 B | Board/Channel address byte |
| 3 | 1 B | Query ID |
| 4 | 4 B | Session ID (packed, BE) |
| 8 | 2 B | Step number (BE) |
| 10 | 1 B | Operator code |
| 11 | 1 B | Circuit status |
| 12 | 2 B | Presence bitmask (BE) |

Bit *N* of the bitmask set ⇒ opcode *N+1* is present in each row. Fields are
read in ascending opcode order with no per-field tag bytes; one full cycle of
present opcodes = one measurement row.

### A.7 Session file naming

```
  <Data>/sessions/<dd-MM-yyyy>/<SessionID>_<DeviceID>_<Board>_<Channel>.db

  example:  D:\MEWebApp\sessions\18-08-2026\16995612_1_0_1.db
```

Both `ChannelCommandHandler.StartProgram` and `ChannelManager.StoreUdpData`
build this path independently — they must stay in agreement, and the board
number must appear in both or two channels on different boards collide.

### A.8 Calibration query IDs (`0xA0`)

| ID | Name | Group |
|---|---|---|
| `0x01` | IsReady | Session control |
| `0x02` | SendLive | Session control — starts the UDP 10000 calibration stream |
| `0x03` / `0x04` | CurrentChargeLowPoint / HighPoint | Current, charge |
| `0x05` | CurrentChargeGainOffset | Current, charge — **per range** |
| `0x06` / `0x07` | CurrentDisChargeLowPoint / HighPoint | Current, discharge |
| `0x08` | CurrentDisChargeGainOffset | Current, discharge — **per range** |
| `0x09` / `0x0A` | VoltageChargeLowPoint / HighPoint | Voltage, charge |
| `0x0B` | VoltageChargeGainOffset | Voltage, charge — single point |
| `0x0C` / `0x0D` | VoltageDisChargeLowPoint / HighPoint | Voltage, discharge |
| `0x0E` | VoltageDisChargeGainOffset | Voltage, discharge — single point |
| `0x0F` / `0x10` | TemperatureLowPoint / HighPoint | Temperature |
| `0x11` | TemperatureGainOffset | Temperature — single point |
| `0x12` | CancelCalibration | Abort |
| `0x13` | StopCalibration | Stop the session |
| `0x14` / `0x15` | StartChargeVerifyCurrent / StartDisChargeVerifyCurrent | Verification |
| `0x16` | StopVerifyCurrent | Verification |
| `0x17` | PreviousCalibration | Read back the full 163-byte record |

Ranges (`CalibrationRange`) apply to **current only**: `Full_Range`, `Range1`..`Range4`.
Voltage and temperature hold a single gain/offset pair each.

### A.9 Divergence register

Places where two parts of the system, or the code and the spec, do not agree. Each is
documented in its chapter; none has been changed.

| # | Divergence | Where | Chapter |
|---|---|---|---|
| 1 | **Transfer order and DBC port count.** UI sends Battery → DBC → Program with 3 DBC ports; `DeviceController.CoreSendProgram` (and therefore MCP) sends Program → Battery → DBC with port 1 only. The Scheduler agrees with the UI. | `TransferDialog.razor:372-445` vs `DeviceController.cs:41-108` vs `SchedulerService.cs:324-367` | [S4](13-code-call-sequences.md#s4-transfer-from-rest--mcp-devicecontrollercoresendprogram) |
| 2 | **`TimeSyn` and `ResetSystem` emit the same frame** (`0xEE` + query `0x06`), differing only in TimeSyn's epoch payload. Spec says Control-family SyncTime is `0x05`. | `ChannelCommandHandler.cs:559-592`, `CircuitEnums.cs:12-40` vs `PROTOCOL.md` §7 | [9](09-control-commands.md) |
| 3 | **Broadcast ports.** Code uses send `10002` / listen `10003`; spec documents `10003` / `10004`. | `BroadcastUdpService.cs:21-22` vs `PROTOCOL.md` §1.2 | [15](15-broadcast-discovery.md) |
| 4 | **Scheduler ignores the board number** when resolving a target channel — matches `DeviceID` + `ChannelNumber` only, while addressing is 3-part everywhere else. | `SchedulerService.cs:291-293` vs `ChannelManager.cs:76` | [16](16-scheduled-execution.md) |

**None of these should be "fixed" from this document alone.** Items 1–3 touch the wire
protocol and need firmware confirmation; item 4 needs a decision on whether
`ScheduleCircuitTarget` gains a board field and how existing schedule rows migrate.

### A.10 Related documents

| Document | Contents |
|---|---|
| `docs/PROTOCOL.md` | Byte-level protocol specification (framing, CRC-16, every packet layout) |
| `.claude/ARCHITECTURE.md` | System design overview, known gaps, deployment topology |
| `.claude/CODEBASE_MAP.md` | File-by-file map of the repository |
| `.claude/DECISIONS.md` | Architecture decision records |
| `docs/deployment/` | Customer-site installation guide |
| `HardwareSimulator/README.md` | How to drive the simulator: CLI flags, config keys, `--board-base 0` for the field board-`0` topology |

---

*End of document.*

---

[⬅ Previous](17-report-and-export.md) · [⬅ Index](README.md)
