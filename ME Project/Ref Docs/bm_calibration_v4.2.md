# BM Calibration Data Frame Format V4.2

**Source:** `BM Documents/SW & HW Query-Responce/Excel/Calibration/Calibration Data Frame Format V4.2.xlsx`
**Interface:** Web Application (SW) ↔ BTS Hardware (HW)
**Direction:** Bidirectional — SW sends preset values, HW returns ADC counts and computed gain/offset
**Start byte:** `0xA0`

---

## Summary of Changes from V4.1

| Change | Affects | Detail |
|--------|---------|--------|
| **Live Temperature added** | Q2 response | +8 bytes: Float[4] + Integer[4]. Packet 23 → 31 bytes |
| **Range byte added** | Q3–Q8 (all current calibration queries) | +1 byte after Query ID, in BOTH the request AND the ACK/NACK response |
| Voltage queries Q9–Q11 | — | **No change**; voltage calibration does not use Range |

> Range byte values: `0xFF` Full Range, `0x01` R1 (100%), `0x02` R2 (50%), `0x03` R3 (25%), `0x04` R4 (12.5%)

---

## Q1 : Is HW Ready for Calibration?

**Request (5 bytes):** `0xA0 | DevNum | CircuitNum | 0x01 | CRC[2]`

**Response ACK (6 bytes):** `0xA0 | DevNum | CircuitNum | 0x01 | 0x01 | CRC[2]`
**Response NACK (6 bytes):** `0xA0 | DevNum | CircuitNum | 0x01 | 0x00 | CRC[2]`

---

## Q2 : Send Live Current, Voltage and Temperature (Periodic from HW during calibration)

**Packet (31 bytes):**

| Byte(s) | Field | Notes |
|---------|-------|-------|
| 0 | Start | `0xA0` |
| 1 | Device Number | |
| 2 | Circuit Number | |
| 3 | Query ID | `0x02` |
| 4–7 | Live Current (Float, IEEE 754) | e.g. `C2 DC 00 00` = −110 A |
| 8–11 | Live Current (Integer, uint32) | raw ADC count |
| 12–15 | Live Voltage (Float) | e.g. `42 A0 00 00` = 80 V |
| 16–19 | Live Voltage (Integer) | raw ADC count |
| **20–23** | **Live Temperature (Float)** ⭐ NEW | e.g. `42 A0 00 00` = 80 °C |
| **24–27** | **Live Temperature (Integer)** ⭐ NEW | raw ADC count |
| 28 | ERR Code / Range | see Range Table |
| 29–30 | CRC | CRC-16 |

> **HW transmits this packet periodically** during calibration mode.

---

## Q3 : Send Charging Current Calibration Low Point Preset

**Request (10 bytes):** ⭐ Range byte added

| Byte | Field |
|------|-------|
| 0 | Start `0xA0` |
| 1 | Device Number |
| 2 | Circuit Number |
| 3 | Query ID `0x03` |
| **4** | **Range** ⭐ NEW (`0xFF` / `0x01`–`0x04`) |
| 5–8 | Low point Current Preset (Float, 4B) |
| 9–10 | CRC[2] |

Example: `A0 01 01 03 FF 41 C0 00 00 -- --` → Range=Full, Preset=24 A

**Response ACK (8 bytes):** `A0 | Dev | Cct | 03 | Range | 0x01 | CRC[2]`
**Response NACK (8 bytes):** `A0 | Dev | Cct | 03 | Range | 0x00 | CRC[2]`

---

## Q4 : Send Charging Current Calibration High Point Preset

Same structure as Q3 with Query ID `0x04`. Example: `A0 01 01 04 FF 42 C8 00 00 -- --` → Range=Full, Preset=100 A.

**Response:** `A0 | Dev | Cct | 04 | Range | Err | CRC[2]`

---

## Q5 : Send Current Calibration Gain and Offset during Charge Mode

**Request (18 bytes):** ⭐ Range byte added

| Byte | Field |
|------|-------|
| 0 | Start `0xA0` |
| 1 | Device Number |
| 2 | Circuit Number |
| 3 | Query ID `0x05` |
| **4** | **Range** ⭐ NEW |
| 5–8 | Current Gain in Charge Mode (Float, 4B) |
| 9–12 | Current Offset (Float, 4B) |
| 13–16 | Calibration Date and Time (epoch, 4B) |
| 17–18 | CRC[2] |

Example: `A0 01 01 05 FF 3A 9D 49 52 39 D1 B7 17 67 E3 91 C3 -- --`

**Response:** `A0 | Dev | Cct | 05 | Range | Err | CRC[2]` (8 bytes)

---

## Q6 : Send Discharging Current Calibration Low Point Preset

Same structure as Q3 with Query ID `0x06`. Example: `A0 01 01 06 FF 41 C0 00 00 -- --` → 24 A.

**Response:** `A0 | Dev | Cct | 06 | Range | Err | CRC[2]`

---

## Q7 : Send Discharging Current Calibration High Point Preset

Same structure as Q3 with Query ID `0x07`. Example: `A0 01 01 07 FF 42 C8 00 00 -- --` → 100 A.

**Response:** `A0 | Dev | Cct | 07 | Range | Err | CRC[2]`

---

## Q8 : Send Current Calibration Gain and Offset during Discharge Mode

Same structure as Q5 with Query ID `0x08`. 18-byte request, 8-byte ACK/NACK response with Range echoed.

---

## Q9 : Send Charging Voltage Calibration Low Point

**Request (9 bytes — no Range byte):**

| Byte | Field |
|------|-------|
| 0 | Start `0xA0` |
| 1 | Device Number |
| 2 | Circuit Number |
| 3 | Query ID `0x09` |
| 4–7 | Low point Voltage (Float, 4B) |
| 8–9 | CRC[2] |

Example: `A0 01 01 09 41 C0 00 00 -- --` → 24.0 V

**Response ACK (7 bytes):** `A0 | Dev | Cct | 09 | 0x01 | CRC[2]`
**Response NACK (7 bytes):** `A0 | Dev | Cct | 09 | 0x00 | CRC[2]`

---

## Q10 : Send Charging Voltage Calibration High Point Preset

Same structure as Q9 with Query ID `0x0A`. Example: `A0 01 01 0A 42 A0 00 00 -- --` → 80 V.

---

## Q11 : Send Voltage Calibration Gain and Offset during Charge Mode

**Request (17 bytes — no Range byte):**

| Byte | Field |
|------|-------|
| 0 | Start `0xA0` |
| 1 | Device Number |
| 2 | Circuit Number |
| 3 | Query ID `0x0B` |
| 4–7 | Voltage Gain in Charge Mode (Float, 4B) |
| 8–11 | Voltage Offset (Float, 4B) |
| 12–15 | Calibration Date and Time (epoch, 4B) |
| 16–17 | CRC[2] |

Example: `A0 01 01 0B 3A 9D 49 52 39 D1 B7 17 67 E3 91 C3 -- --`

**Response:** `A0 | Dev | Cct | 0B | Err | CRC[2]` (7 bytes)

---

## Q12+ : Discharge Voltage Low / High / Gain+Offset

Same structure as Q9 / Q10 / Q11 respectively. **No Range byte** (voltage calibration does not use Range).

Query IDs: Q12 = `0x0C` (DCha Vol Low), Q13 = `0x0D` (DCha Vol High), Q14 = `0x0E` (DCha Vol Gain+Offset).

---

## Q15+ : Cancel / Stop / Verify / Stop-Verify / Previous Calibration Data

> ⚠️ **Pending verification** — layouts of Q15–Q23 (cancel, stop, verify-charge, verify-discharge, stop-verify, previous-calibration-parameters) were not fully re-read from V4.2 Excel due to Excel MCP failure during this session. The structures are expected to be the same as V4.1 unless the Range byte was also added to verify-current queries.
>
> **TODO:** Re-read pages A170:FG283 of V4.2 sheet "Calibration Data Packet Details" when MCP is restored.

---

## Range Table

| Code | Meaning | % of Max | Low Point | High Point |
|------|---------|---------|-----------|------------|
| `0xFF` | Full Range | 100% / 50% | 0.5 | 50 |
| `0x01` | Range 1 | 100% / 50% | 25 | 50 |
| `0x02` | Range 2 | 50% / 25% | 12.5 | 25 |
| `0x03` | Range 3 | 25% / 12.5% | 6.25 | 12.5 |
| `0x04` | Range 4 | 12.5% / 6.25% | 0.5 | 6.25 |

---

## Calibration Sequence (unchanged from V4.1)

1. SW sends Low preset current to HW (e.g., 0.5 A) for selected Range
2. HW sets output, measures ADC count X1; SW records DMM reading Y1
3. SW sends High preset current (e.g., 5.0 A) for same Range
4. HW measures ADC count X2; SW records DMM reading Y2
5. Primary computes Gain = (Y2−Y1)/(X2−X1), Offset = Y1 − Gain×X1
6. Gain and Offset returned to SW (with Range byte)

Same sequence repeated for Voltage calibration (no Range) and for Discharge direction.

---

## Implementation Notes

- Firmware module: CAL in `bts_app.c`, parsing in `networkDataHandler.c`
- Float values are IEEE 754 single-precision, transmitted **big-endian**
- Buffer size constant: `CALIB_DATA_MAX_LENGTH = 31` (in `bts_app.h`) — sized for Q2 live-data packet
- Range byte is **pass-through**: Primary firmware does NOT expand calibration storage per-range in v1.0; it forwards the Range byte to Secondary and echoes it back in ACK/NACK
- Storage model: single `bts_calibData.ccChgGain` / `ccChgOffset` etc. per direction; Range is informational only
