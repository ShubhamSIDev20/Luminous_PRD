# BM Control Data Frame Format V3.0

**Source:** `BM Documents/SW & HW Query-Responce/Excel/Control Data/Control Data MQTT Frame format V3.0.xlsx`
**Interface:** Web Application (SW) ↔ BTS Hardware (HW)  
**Direction:** SW → HW query, HW → SW response

> ⚠️ **Amended 2026-08-12 from observed ME traffic, not from the source Excel.**
> **Q1 Start carries a 4-byte Session ID** that the original table did not list.
> The Excel above has not been re-exported, so where the two disagree, **the wire
> is authoritative** — this amendment was made because a real frame proved it.
>
> How it was found: the ME Primary Board rejected a valid Start frame as
> `BAD_CRC`. Believing Q1 to be 6 bytes, it split the 10-byte frame at 6 and
> checksummed only `EE 01 11 01` against `01 11` — the first two bytes of the
> Session ID. The CRC had been correct all along. Captured frame:
>
> ```
> EE 01 11 01 01 11 35 F3 62 E7
> |  |  |  |  \_________/ \___/
> |  |  |  |   SessionID   CRC-16/Modbus over the preceding 8 bytes
> |  |  |  QueryID 0x01 = Start
> |  |  CircuitNumber 0x11 (Secondary 1, Channel 1)
> |  DeviceNumber
> Start 0xEE
> ```
>
> Regression test with these literal bytes: `me-primary/tests/test_control_frame.c`.
> **Confirmed by the developer:** only Q1 carries a Session ID; Q2/Q3/Q4/Q6 carry
> no payload.

## Packet Structure

All packets: `Start | Device Number | Circuit Number | Query ID | [Payload] | CRC (2 Bytes)`

- Start byte: `0xEE`
- Value `0x01` = Success/ACK, `0x00` = Failure/NACK
- Multi-byte fields are **big-endian** (high byte first), including the CRC

## Query Summary

| Q# | Description | Query ID | Payload | Total |
|----|-------------|----------|---------|-------|
| Q1 | Start the Program | `0x01` | **Session ID (4 bytes, big-endian)** ⚠️ *see amendment above* | **10** |
| Q2 | Stop the Program | `0x02` | None | 6 |
| Q3 | Pause the Program | `0x03` | None | 6 |
| Q4 | Continue the Program | `0x04` | None | 6 |
| Q5 | Synchronize Time | `0x05` | Epoch time (4 bytes, big-endian) | 10 |
| Q6 | Reset the System | `0x06` | None | 6 |

Q1 and Q5 both carry four bytes at the same offset meaning different things — a
session identifier and a clock. Nothing on the wire distinguishes them, so the
QueryID is the only thing that says which one arrived.

## Sample Packets

### Q1: Start Program
```
Query:    EE 01 01 01 S3 S2 S1 S0 -- --      (S3..S0 = Session ID, big-endian)
Real captured frame:  EE 01 11 01 01 11 35 F3 62 E7   (session 0x011135F3)

Response (OK):   EE 01 01 01 01 -- --
Response (FAIL): EE 01 01 01 00 -- --
```

**Legacy note:** the original table gave this as `EE 01 01 01 -- --` (6 bytes,
"Payload: None"). Kept here because older BTS firmware may still send that form.

### Q5: Sync Time (example epoch 0x6821D394)
```
Query: EE 01 01 05 68 21 D3 94 -- --
Response (OK):   EE 01 01 05 01 -- --
Response (FAIL): EE 01 01 05 00 -- --
```

## Notes
- Firmware module: BCT/CMU in `bts_app.c`
- CRC is 16-bit, appended as 2 bytes at end of every packet
- **CRC-16/Modbus, high byte first** (poly 0xA001 reflected, init 0xFFFF, no final
  XOR) — hardware-verified in both directions 2026-08-10. `ICD.md` §3.2 claims
  little-endian and is wrong; see ADR-9.
- **ME implementation status (2026-08-12):** all six queries are parsed and
  answered with the documented response frame. Start/Stop/Pause/Continue reach
  Core Logic; Sync Time is parsed but is still a logged no-op; the response means
  *"command accepted and queued"*, not *"command completed"*.
- **This frame group carries no length field**, so a receiver must know each
  query's payload size in advance — which is exactly what made the Q1 amendment
  above a silent, non-local failure rather than one bad frame. ME now falls back
  to locating the frame boundary by CRC when its layout table disagrees
  (`me_frame_resolve_len()`), and logs loudly when it has to.
