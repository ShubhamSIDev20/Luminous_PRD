# BM Measured Parameter Frame Format V5.2

**Source:** `BM Documents/SW & HW Query-Responce/Excel/Real Time Data/Measured Parameter Frame format V5.2.xlsx` (updated 2026-06-25)
**Interface:** BTS Hardware (Primary) → Web Application (SW)
**Start byte:** `0xCC`
**Supersedes:** `bm_measured_param_v5.1.md` (V5.1 = 78-byte payload). V5.2 inserts one new 2-byte field ("Registration type") ahead of the digital I/O bytes — payload 78 → 80 bytes. **Already implemented in firmware** (`bts_app.h` macros are 77/80/86) — this doc was written after the fact to catch documentation up to code.

## Two Ports

| Port | Q# | Description |
|------|----|-------------|
| 10000 (UDP) | Q1 | Real-time measurement data |
| 10000 (UDP) | Q2 | CAN data |
| 10001 (UDP) | Q1 | Registration data (session-based) |

## Port 10000 — Real-Time Data Packet (Q1)

```
CC 01 01 01 [payload bytes...] CRC_HI CRC_LO
```
Header = `Start(0xCC) | Device Number | Circuit Number | Query ID` (4 bytes), then payload, then CRC (2 bytes).

## Port 10001 — Registration Data Packet (Q1)

```
CC 01 01 01 SID3 SID2 SID1 SID0 [payload bytes...] CRC_HI CRC_LO
```
Includes 4-byte Session ID prefix before payload. **Unchanged in V5.2** — verified against the "Real-Time & Registration" sheet; the registration payload still ends at field 16 (Error ID), same as V5.1.

## Real-Time Data Payload — Packet Encoding

Two index conventions:
- **Payload Offset** = 0-based from payload start (what `bts_appData.realTimeDataPacketBuffer` payload region holds).
- **Frame Index** = byte position in the full frame = Payload Offset + 4 (after `[0]=Start [1]=Device [2]=Circuit [3]=QueryID`).

| Payload Offset | Frame Index | Field | Size | Format | Notes |
|--------|--------|-------|------|--------|-------|
| 0  | 4  | Step Number | 2 | uint16 | `00 01` = step 1 |
| 2  | 6  | Program Running Status | 1 | uint8 | `0x00`=Idle, `0x01`=Running |
| 3  | 7  | Circuit Status | 1 | uint8 | `0x00`=Idle, `0x01`=Charge, `0x02`=Discharge, `0x03`=Pause, `0x04`=Continue, `0x05`=Int, `0x06`=Error, `0x07`=Msg |
| 4  | 8  | User ERR/MSG Number | 1 | uint8 | 0–9 |
| 5  | 9  | Exception/Error ID | 4 | uint32 | Primary OR-overlays its own error ID here |
| 9  | 13 | Step Running Time | 4 | uint32 (ms) | `00 00 00 64` = 100 ms |
| 13 | 17 | Program Running Time | 4 | uint32 (ms) | `00 00 01 23` = 291 ms |
| 17 | 21 | Current | 4 | float | `42 BF 33 33` = 95.6 A |
| 21 | 25 | Voltage | 4 | float | `41 F0 CC CD` = 30.1 V |
| 25 | 29 | Temperature | 4 | float | `42 22 00 00` = 40.5 °C |
| 29 | 33 | Power | 4 | float | W |
| 33 | 37 | Accumulated Capacity | 4 | float | Ah |
| 37 | 41 | Charge Capacity | 4 | float | Ah |
| 41 | 45 | Discharge Capacity | 4 | float | Ah |
| 45 | 49 | Step Capacity | 4 | float | Ah |
| 49 | 53 | Accumulated Energy | 4 | float | Wh |
| 53 | 57 | Charge Energy | 4 | float | Wh |
| 57 | 61 | Discharge Energy | 4 | float | Wh |
| 61 | 65 | Step Energy | 4 | float | Wh |
| 65 | 69 | Operator | 1 | uint8 | `0x01` = CCChg |
| 66 | 70 | Cycle Status | 1 | uint8 | `0x00`=Not running, `0x01`=Running |
| 67 | 71 | Cycle Start Step Number | 2 | uint16 | |
| 69 | 73 | Cycle Run Iteration | 2 | uint16 | |
| 71 | 75 | Table Step Number | 2 | uint16 | |
| 73 | 77 | Table Total Row Number | 2 | uint16 | |
| 75 | 79 | **Registration type** | 2 | uint16 | **NEW in V5.2.** Power-fail resume context: Secondary reports this to Primary every real-time cycle; Primary persists it in EEPROM as part of the real-time block; on power resume Primary echoes the whole block (incl. this field) back to Secondary via `en_POWER_RESUME_CMD`, so Secondary can recover the registration type it was in before the outage. Primary passes it through to the WebApp unchanged, opaque — **Secondary owns the value semantics**, not Primary/BM. |
| 77 | 81 | Digital Inputs (Primary + Secondary) | 1 | bitfield | bits[7:4]=Primary DI[3:0], bits[3:0]=Secondary DI[3:0] (1=High) |
| 78 | 82 | Digital Outputs (Secondary) | 1 | bitfield | DO[7:0] (1=High) |
| 79 | 83 | Digital Outputs (Primary) | 1 | bitfield | DO[2:0] in LSBs |

**Total real-time payload: 80 bytes** (Payload Offsets 0–79 / Frame Indices 4–83).
**Total frame: 86 bytes** = 4 (header) + 80 (payload) + 2 (CRC).

> ✅ **Spreadsheet verified (V5.2.xlsx, "Real-Time Data Packet" sheet, read via excel-reader on 2026-07-10):**
> Index column confirms: Table Total Row Number ends at frame index 78, Registration Type occupies frame indices 79–80, Digital Inputs at 81, DO-Secondary at 82, DO-Primary at 83. Last byte = 83 → frame = `REAL_TIME_DATA_PACKET_LENGTH = MEASURED_DATA_PAYLOAD_LENGTH + 9 = 86` (4 header + 80 payload + 2 CRC).
>
> Also checked the "Real-Time & Registration" sheet: Port 10000 Q1/Q2 headers and the Port 10001 registration payload (Step Number … Error ID, 16 fields) are **unchanged** from V5.1 — the new field only appears in the real-time (Q1, port 10000) packet.

### Firmware length mapping (Primary) — **already implemented**

| Macro | File | Value | Means |
|-------|------|-----------|-------|
| `MEASURED_DATA_PAYLOAD_LENGTH` | `bts_app.h:55` | **77** | Measured fields incl. Registration Type (Step Number … Registration Type), Payload Offsets 0–76 |
| `REAL_TIME_DATA_LENGTH` | `bts_app.h:57` (derived `= MEASURED+3`) | **80** | Measured 77 + 3 digital bytes (DI combined + DO-Sec + DO-Prim) |
| `REAL_TIME_DATA_PACKET_LENGTH` | `bts_app.h:91` (`= MEASURED+9`) | **86** | Full `0xCC` frame: 4 header + 80 payload + 2 CRC |

✅ Verified directly in code: `bts_app.h:55/57/91` already carry these V5.2 values, and `bts_app.c:557` (`sendMeasuredDataOnUdp`) memcpy's the full 77-byte `measuredDataPayload` (Registration Type included) straight into the outgoing WebApp packet. **This spec is already fully implemented on the Primary side** — corrected from an earlier draft of this doc, which wrongly assumed the firmware was still on the V5.1 (78 B) layout without checking `bts_app.h` or `ps_measured_data_v5.md` first.

## Relationship to the Primary↔Secondary frame (`ps_measured_data_v5.md`)

`ps_measured_data_v5.md` already documents this same field (added 2026-06-25, the same day as this BM Excel revision) — it is **not** a BM-only addition. Secondary sends it to Primary on every real-time cycle; Primary passes it through unchanged to the WebApp.

| | Measured block (incl. Registration Type) | Trailing digital bytes | Payload total |
|---|---|---|---|
| **P↔S `0x33`** (Sec→Prim, per `ps_measured_data_v5.md`) | 77 B | DI-Secondary (1) + DO-Secondary (1) | 79 B |
| **WebApp `0xCC`** (Prim→WebApp, this doc) | 77 B | DI Prim+Sec combined (1) + DO-Secondary (1) + DO-Primary (1) | **80 B** |

## Field purpose — Registration Type (confirmed with dev team, 2026-07-10)

This field carries **power-fail resume context**, not a WebApp-facing configuration value:
1. Secondary computes/tracks its current registration type and reports it to Primary in every real-time payload (`0x33` Q1).
2. Primary persists the entire real-time block — including this field — to EEPROM on power fail (`power_fail.c:141`, `writeDataToMemory(REAL_TIME_DATA, ...)`).
3. On power resume, Primary reads the persisted block back (`getPowerFailRealTimeData()`) and sends it to Secondary via the Control-Data link's `en_POWER_RESUME_CMD` (Q5) — `prim_uart.c:1524-1532` — so Secondary can recover the registration type it was in immediately before the outage, even if Secondary's own RAM state was lost across the power cycle.
4. Primary also forwards the value unchanged into the WebApp-facing real-time packet documented in this file (informational passthrough — the WebApp itself does not act on power-fail resume).

## Value semantics — owned by Secondary

The specific value/enum meanings of Registration Type are deliberately not documented on the Primary/BM side — **Secondary owns this data** (computes and interprets it); Primary and the WebApp only store/relay it opaquely. No action needed here.

## V5.1 → V5.2 delta
- Inserted **Registration Type (2B, uint16)** between "Table Total Row Number" and "Digital Inputs Status" — power-fail resume context, see Field Purpose above.
- Digital Inputs / DO-Secondary / DO-Primary each shift by +2 bytes (Frame Index 79→81, 80→82, 81→83).
- Payload 78 → 80 bytes; full frame 84 → 86 bytes.
- Port 10001 registration packet payload: **no change**.
- **Already implemented on Primary** — this doc update is documentation catching up to firmware, not firmware catching up to spec.

## Notes
- Firmware module: BCT/PRU in `bts_app.c`, `prim_uart.c`, `power_fail.c`
- UDP on port 10000 (real-time), 10001 (registration)
- Cross-board alignment tracked as umbrella **U-06**. Registration Type specifically: Secondary→Primary (`ps_measured_data_v5.md`) and Primary→WebApp (this doc) both confirmed implemented 2026-06-25/2026-07-10; value semantics intentionally undocumented here — owned by Secondary.
