# BTS Configuration Data Frame Format — v6.0

**Start byte `0xAA` — configuration data exchange (Web Application ⇄ Primary).**

> ## Provenance
>
> Transcribed from **`Config Data Frame Format V6.0.xlsx`**
> (`Desktop\Old BTS Documents\`), supplied by the developer on **2026-08-12**.
> Sheets in the source workbook: `Configuration`, `Factory Data`,
> `Manufacturing Data`, `Battery Data`, `Other Data`.
>
> This is a **transcription of the source spreadsheet**, not a rewrite. Where the
> spreadsheet is internally inconsistent, or where it disagrees with traffic
> actually observed on hardware, both readings are recorded side by side in
> [§9 Discrepancies](#9-discrepancies--open-questions). Nothing has been silently
> corrected.
>
> Until this document existed, `me-primary/src/proto/battery_frame.h` carried the
> warning *"There is NO battery/configuration specification in Ref Docs"* and its
> field offsets were reverse-engineered from `storeBatteryData()` in the legacy
> `BTS_Primary_SOM/src/networkDataHandler.c`. **§5.3 below confirms those offsets
> against both this specification and a captured frame**, and supplies the 18
> payload bytes the legacy layout was missing.

---

## 1. Frame header

Every **circuit-scoped** `0xAA` frame (Q1–Q6) opens with the same 4-byte header
used by `0xBB`, `0xCC`, `0xDD` and `0xEE`:

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0 | 1 | **Start** | always `0xAA` for configuration |
| 1 | 1 | **Device Number** | `0x01` in every sample |
| 2 | 1 | **Circuit Number** | `0x01` in the samples; on the ME wire this is the packed CircuitID (`0x11` = Secondary 1 / Channel 1) |
| 3 | 1 | **Query ID** | `0x01` … `0x0A`, see §2 |
| 4… | n | payload | query-dependent, see §3–§4 |
| last 2 | 2 | **CRC (2 bytes)** | see §1.2 |

**Broadcast** frames (Q7–Q10) use a **different, 2-byte header** — there is no
Device Number and no Circuit Number:

| Offset | Size | Field |
|---|---|---|
| 0 | 1 | **Start** = `0xAA` |
| 1 | 1 | **Query ID** |
| 2… | n | payload |
| last 2 | 2 | **CRC (2 bytes)** |

> ⚠ **This header difference is structural, not cosmetic.** Code that assumes the
> 4-byte header for all `0xAA` frames will read a Q7 broadcast's *Query ID* as its
> Device Number and its first payload byte as the Circuit Number. See §9.1.

### 1.1 No length field

**No `0xAA` frame carries a length field**, for any query. The receiver must know
each query's payload size from its Query ID, or determine the frame boundary some
other way. In `me-primary` this is why `me_frame_expected_len()` returns `0` for
`0xAA` and the CRC acts as the delimiter of last resort (ADR-19).

### 1.2 CRC

The spreadsheet marks every CRC cell `-` and only states the width, **2 bytes**.
It specifies neither the polynomial nor the byte order.

The algorithm and byte order are established elsewhere and confirmed on hardware
for this project:

- **CRC-16/Modbus** — reflected polynomial `0xA001`, init `0xFFFF`, no final XOR
- computed over **every byte from Start up to but excluding the CRC itself**
- transmitted **big-endian, high byte first** (`ME_CRC_ORDER_DEFAULT`)

Verified against a real `0xAA` Q5 frame from the ME Web Application — see §5.4.

---

## 2. Query summary

| Query ID | Direction | Meaning | Scope | Request payload | Response payload |
|---|---|---|---|---|---|
| `0x01` | Web App → Primary | Is HW ready to read/write configuration data? | circuit | none | 1-byte Value |
| `0x02` | Web App → Primary | Read **all Factory Configuration** data | circuit | none | all factory params, appended |
| `0x03` | Web App → Primary | Read **all Manufacturing Information** data | circuit | none | all manufacturing params, appended |
| `0x04` | Web App → Primary | Read **all Battery Information** data | circuit | none | all battery params, appended |
| `0x05` | Web App → Primary | **Write all Battery Configuration** data | circuit | 40 bytes (§5.3) | 1-byte Value |
| `0x06` | Web App → Primary | **Sync Time** | circuit | 4-byte epoch | 1-byte Value |
| `0x07` | Web App → Primary | Write all Factory Configuration data | **broadcast** | 4-byte Unique Number + params | 1-byte Value |
| `0x08` | Web App → Primary | Write all Manufacturing Information data | **broadcast** | 4-byte Unique Number + params | 1-byte Value |
| `0x09` | Web App → Primary | Read all Factory Configuration data | **broadcast** | none | 4-byte Unique Number + params |
| `0x0A` | Web App → Primary | Read all Manufacturing Information data | **broadcast** | none | 4-byte Unique Number + params |

**Value byte convention** (all acknowledgement responses):

| Value | Meaning |
|---|---|
| `0x01` | Yes / Successful |
| `0x00` | No / Failed |

---

## 3. Circuit-scoped queries (Q1–Q6)

### 3.1 Q1 — Is HW ready to read/write configuration data?

**Request — 6 bytes**

```
 Start   Device   Circuit   QueryID    CRC (2 bytes)
 0xAA     0x01      0x01      0x01      --   --
```

**Response — 7 bytes**

```
 Start   Device   Circuit   QueryID   Value    CRC (2 bytes)
 0xAA     0x01      0x01      0x01     0x01     --   --     <- "Yes"
 0xAA     0x00      0x01      0x01     0x00     --   --     <- "No"  (see §9.2)
```

### 3.2 Q2 — Read all Factory Configuration data

**Request — 6 bytes**

```
 Start   Device   Circuit   QueryID    CRC (2 bytes)
 0xAA     0x01      0x01      0x02      --   --
```

**Response** — header, then every Factory Configuration parameter value appended
one after another sequentially, then CRC.

```
 Start   Device   Circuit   QueryID   Value  Value  ...  CRC (2 bytes)
 0xAA     0x01      0x01      0x02      --     --   ...   --   --
```

Parameter list and widths: [§6 Factory Data](#6-factory-data-parameters).

### 3.3 Q3 — Read all Manufacturing Information data

**Request — 6 bytes**

```
 Start   Device   Circuit   QueryID    CRC (2 bytes)
 0xAA     0x01      0x01      0x03      --   --
```

**Response** — header, then every Manufacturing Information parameter value
appended sequentially, then CRC.

Parameter list and widths: [§7 Manufacturing Data](#7-manufacturing-data-parameters).

### 3.4 Q4 — Read all Battery Information data

**Request — 6 bytes**

```
 Start   Device   Circuit   QueryID    CRC (2 bytes)
 0xAA     0x01      0x01      0x04      --   --
```

**Response** — header, then every Battery parameter value appended sequentially,
then CRC. The payload is expected to be the **same 40-byte layout as the Q5
write** (§5.3), read back rather than written.

### 3.5 Q5 — Write all Battery Configuration data ★

**This is the query the ME Primary Board receives today.** Full field-by-field
breakdown in [§5](#5-q5-write-battery-configuration--full-layout).

**Request — 46 bytes**

```
 Start   Device   Circuit   QueryID   Value  Value  ...  CRC (2 bytes)
 0xAA     0x01      0x01      0x05      --     --   ...   --   --
                                       |<---- 40 bytes ---->|
```

**Response — 7 bytes**

```
 Start   Device   Circuit   QueryID   Value    CRC (2 bytes)
 0xAA     0x01      0x01      0x05     0x01     --   --     <- Write successful
 0xAA     0x01      0x01      0x05     0x00     --   --     <- Write Failed
```

### 3.6 Q6 — Sync Time

**Request — 10 bytes**

```
 Start   Device   Circuit   QueryID   Time Value (4 bytes)     CRC (2 bytes)
 0xAA     0x01      0x01      0x06    0x67 0xE3 0x91 0xC3       --   --
```

The 4-byte time value is a **big-endian Unix epoch seconds** count.
`0x67E391C3` = 1 742 967 235 = 2025-03-26 11:03:55 IST (Wednesday).

**Response — 7 bytes**

```
 Start   Device   Circuit   QueryID   Value    CRC (2 bytes)
 0xAA     0x01      0x01      0x06     0x01     --   --     <- Successful
 0xAA     0x01      0x01      0x06     0x00     --   --     <- Unsuccessful
```

> The spreadsheet prints Query ID `0x01` in both Response6 rows. That is a
> copy-paste artefact from the Q1 response block — see §9.2.

---

## 4. Broadcast queries (Q7–Q10)

These carry **no Device Number and no Circuit Number**. Q7 and Q8 instead carry a
**4-byte Unique Number** identifying the target board.

### 4.1 Q7 — Write all Factory Configuration data (BROADCAST)

**Request**

```
 Start   QueryID   Unique Number (4 bytes)      Value  Value  ...  CRC (2 bytes)
 0xAA     0x07     0x01 0x02 0x03 0x04            --     --   ...   --   --
```

**Response — 5 bytes**

```
 Start   QueryID   Value    CRC (2 bytes)
 0xAA     0x07      0x01     --   --      <- Write successful
 0xAA     0x07      0x00     --   --      <- Write Failed
 0xAA     0x00      0x00     --   --      <- Wrong CRC   (QueryID is ZEROED)
```

> **The Wrong-CRC response zeroes the Query ID**, unlike the Failed response
> which echoes it. A CRC failure is therefore distinguishable from a write
> failure. This convention appears only in the broadcast queries; no equivalent
> is documented for Q1–Q6.

### 4.2 Q8 — Write all Manufacturing Information data (BROADCAST)

**Request**

```
 Start   QueryID   Unique Number (4 bytes)      Value  Value  ...  CRC (2 bytes)
 0xAA     0x08     0x01 0x02 0x03 0x04            --     --   ...   --   --
```

**Response — 5 bytes**

```
 Start   QueryID   Value    CRC (2 bytes)
 0xAA     0x08      0x01     --   --      <- Write successful
 0xAA     0x08      0x00     --   --      <- Write Failed
 0xAA     0x00      0x00     --   --      <- Wrong CRC
```

> ⚠ **Spreadsheet note, verbatim:** *"For this Manufacturing Information data, do
> not write 'Master SW Version', 'COM SW Version' and 'Secondary SW Version'."*
> Those three fields (IDs `0x97`–`0x99`) are read-only — reported by the board,
> never written to it.

### 4.3 Q9 — Read all Factory Configuration data (BROADCAST)

**Request — 4 bytes**

```
 Start   QueryID   CRC (2 bytes)
 0xAA     0x09      --   --
```

**Response**

```
 Start   QueryID   Unique Number (4 bytes)      Value  Value  ...  CRC (2 bytes)
 0xAA     0x09     0x01 0x02 0x03 0x04            --     --   ...   --   --
```

### 4.4 Q10 — Read all Manufacturing Information data (BROADCAST)

**Request — 4 bytes**

```
 Start   QueryID   CRC (2 bytes)
 0xAA     0x0A      --   --
```

**Response**

```
 Start   QueryID   Unique Number (4 bytes)      Value  Value  ...  CRC (2 bytes)
 0xAA     0x0A     0x01 0x02 0x03 0x04            --     --   ...   --   --
```

---

## 5. Q5 Write Battery Configuration — full layout

### 5.1 Parameter table

From sheet **`Battery Data`** — *"BTS Battery Configurable Parameters Encoding
Packet Details"*. The **ID** column is the parameter identifier; see §5.2 for
whether it appears on the wire.

| ID (hex) | ID (dec) | Parameter | Size | Type | Range / unit | Sample bytes | Sample value |
|---|---|---|---|---|---|---|---|
| `0x65` | 101 | Battery Nominal Capacity | 4 | float BE | max 2000.000 Ah, up to 3 decimals, unit `Ah` | `44 A7 D4 EE` | 1342.654 Ah |
| `0x66` | 102 | Battery Number of Cells | 1 | uint8 | 1 … 100 | `06` | 6 |
| `0x67` | 103 | Battery Gassing Voltage | 4 | float BE | 50 … 100 V, unit `V` | `42 BF 33 33` | 95.6 V |
| `0x68` | 104 | Battery Maximum Voltage | 4 | float BE | 20 … 100 V, unit `V` | `42 C8 00 00` | 100.0 V |
| `0x69` | 105 | Battery Nominal Current | 4 | float BE | 10 … 100 A, unit `A` | `42 BF 33 33` | 95.6 A |
| `0x6A` | 106 | Battery Cold Cranking Current | 4 | float BE | 10 … 100 A, unit `A` | `42 BF 33 33` | 95.6 A |
| `0x6B` | 107 | Battery Charge Factor | 1 | uint8 | 1 … 200, unit `%` | `0F` | 15 % |
| `0x6C` | 108 | Battery Impedance | 4 | float BE (§9.3) | 10 … 200 Ohm, unit `Ohm` | `00 00 00 64` | 100 Ohm — sample is wrong, see §9.3 |
| `0x6D` | 109 | Battery Break Voltage | 4 | float BE | 10 … 100 V, unit `V` | `42 BF 33 33` | 95.6 V |
| `0x6E` | 110 | Battery Nominal Voltage | 4 | float BE | 10 … 100 V, unit `V` | `42 BF 33 33` | 95.6 V |
| `0x6F` | 111 | Battery Energy Density | 4 | float BE (§9.3) | 10 … 1000, unit `Wh/Kg` (or `Wh/L`) | `00 00 00 64` | 100 Wh/Kg — sample is wrong, see §9.3 |
| `0x6B` ⚠ | 107 ⚠ | Battery ID | 2 | uint16 BE | — | `00 01` | 1 |

⚠ **Battery ID's stated ID collides with Battery Charge Factor.** Both are given
as `0x6B` / 107. See §9.4 — the value is almost certainly meant to be `0x70` / 112.

**Total: 4+1+4+4+4+4+1+4+4+4+4+2 = 40 bytes.**

### 5.2 The ID byte is NOT transmitted in Q5

The spreadsheet lists a 1-byte ID per parameter, but the captured Q5 frame
(§5.4) contains **no ID bytes at all** — its payload is exactly 40 bytes, which is
the sum of the value widths alone. With IDs interleaved the payload would be
40 + 12 = 52 bytes.

**Q5 is therefore a fixed-order concatenation of values**, in the table order of
§5.1. The IDs identify parameters for documentation and for any future
single-parameter access; they are not part of this frame.

### 5.3 Payload byte map

Offsets are from the **first byte after the 4-byte header**. Frame index =
payload offset + 4.

```
 payload  frame                                        our C identifier
  offset  index  size  field                           (me_battery_t)
  ------  -----  ----  ------------------------------  ---------------------
       0      4     4  Battery Nominal Capacity   f32  nom_capacity
       4      8     1  Battery Number of Cells     u8  no_of_cells
       5      9     4  Battery Gassing Voltage    f32  gassing_voltage
       9     13     4  Battery Maximum Voltage    f32  max_voltage
      13     17     4  Battery Nominal Current    f32  nom_current
      17     21     4  Battery Cold Cranking Cur  f32  cold_cranking_current
      21     25     1  Battery Charge Factor       u8  charge_factor
  ---------------------------------------------------  legacy layout ended here
      22     26     4  Battery Impedance          f32  impedance          [NEW]
      26     30     4  Battery Break Voltage      f32  break_voltage      [NEW]
      30     34     4  Battery Nominal Voltage    f32  nom_voltage        [NEW]
      34     38     4  Battery Energy Density     f32  energy_density     [NEW]
      38     42     2  Battery ID                 u16  battery_id         [NEW]
  ------  -----  ----
                  40  total payload
```

**Full frame: 4 header + 40 payload + 2 CRC = 46 bytes.**

The first seven fields match `battery_frame.h`'s reverse-engineered layout
exactly — the legacy `storeBatteryData()` was correct as far as it went. The five
fields marked `[NEW]` are the 18 bytes that layout stopped short of.

### 5.4 Verification against captured hardware traffic

Frame received by the ME Primary Board from the ME Web Application,
**2026-08-12**, for Secondary 1 / Channel 1:

```
AA 01 11 05 3F 80 00 00 01 3F 80 00 00 3F 80 00 00 3F 80 00 00
3F 80 00 00 01 3F 80 00 00 3F 80 00 00 3F 80 00 00 40 00 00 00
00 01 98 E8
```

Decoded against §5.3:

| Field | Bytes | Value |
|---|---|---|
| Start / Device / Circuit / QueryID | `AA 01 11 05` | `0xAA`, device 1, circuit `0x11`, Q5 |
| Battery Nominal Capacity | `3F 80 00 00` | 1.0 Ah |
| Battery Number of Cells | `01` | 1 |
| Battery Gassing Voltage | `3F 80 00 00` | 1.0 V |
| Battery Maximum Voltage | `3F 80 00 00` | 1.0 V |
| Battery Nominal Current | `3F 80 00 00` | 1.0 A |
| Battery Cold Cranking Current | `3F 80 00 00` | 1.0 A |
| Battery Charge Factor | `01` | 1 % |
| Battery Impedance | `3F 80 00 00` | 1.0 Ohm |
| Battery Break Voltage | `3F 80 00 00` | 1.0 V |
| Battery Nominal Voltage | `3F 80 00 00` | 1.0 V |
| Battery Energy Density | `40 00 00 00` | 2.0 Wh/Kg |
| Battery ID | `00 01` | 1 |
| CRC | `98 E8` | CRC-16/Modbus over the first 44 bytes = **0x98E8 ✅ match, big-endian** |

**Every field boundary lands exactly where §5.3 predicts, and the total is exactly
46 bytes.** The specification and the wire agree.

> The operator had entered `1` in every field and `2` for energy density, so the
> values themselves are test data, not realistic battery parameters — several are
> outside the ranges in §5.1. The Primary Board does **not** currently range-check
> them. The byte *layout* is what this capture confirms.

---

## 6. Factory Data parameters

From sheet **`Factory Data`** — *"BTS Factory Configurable Parameters Encoding
Packet Details"*. Used by Q2 (read, circuit-scoped), Q7 (write, broadcast) and
Q9 (read, broadcast).

| ID (hex) | ID (dec) | Parameter | Size | Type | Access | Sample | Notes |
|---|---|---|---|---|---|---|---|
| `0x01` | 1 | MAC ID | 6 | bytes | R | `C0 1A 2B 3C 64 A8` | `C0:1A:2B:3C:64:A8` |
| `0x02` | 2 | Device IP Address | 4 | bytes | R/W | `C0 A8 64 0E` | 192.168.0.14 |
| `0x03` | 3 | Client Remote IP Address | 4 | bytes | R/W | `C0 A8 64 0E` | 192.168.0.10 (see §9.5) |
| `0x04` | 4 | TCP Client Remote Port | 2 | uint16 BE | R/W | `27 0F` | 9999 |
| `0x05` | 5 | UDP Client (Live data) Remote Port | 2 | uint16 BE | R/W | `27 10` | 10000 |
| `0x05` ⚠ | 5 ⚠ | UDP Client (Registration data) Remote Port | 2 | uint16 BE | R/W | `27 10` | 10001 (see §9.6) |
| `0x06` | 6 | DHCP Enable | 1 | uint8 | R/W | `00` | `0` = Static IP, `1` = Dynamic IP |
| `0x07` | 7 | Circuit type | 1 | uint8 | R/W | `01` | `0` = Single Transistor Bank, `1` = Dual Transistor Bank |
| `0x08` | 8 | ZNT Max Voltage | 4 | float BE | R/W | `42 BF 33 33` | 95.6 V |
| `0x09` | 9 | LNT Max Voltage | 4 | float BE | R/W | `42 BF 33 33` | 95.6 V |
| `0x0A` | 10 | Circuit Max Voltage | 4 | float BE | R/W | `42 BF 33 33` | 95.6 V — max enterable 100.0 V |
| `0x0B` | 11 | Circuit Min Voltage | 4 | float BE | R/W | `41 F0 CC CD` | 30.1 V — min enterable 20.0 V |
| `0x0C` | 12 | Circuit Max discharging current | 4 | float BE | R/W | `42 BF 33 33` | 95.6 A — max enterable 100.0 A |
| `0x0D` | 13 | Circuit Max charging current | 4 | float BE | R/W | `42 BF 33 33` | 95.6 A — max enterable 100.0 A |
| `0x0E` | 14 | Circuit Number | 1 | uint8 | R/W | `01` | *"only used as a device number for the communication between Web App and Primary"* |
| `0x0F` | 15 | Circuit Max Temperature | 4 | float BE | R/W | `42 BF 33 33` | 95.6 °C |
| `0x10` | 16 | Circuit Minimum Temperature | 4 | float BE | R/W | `42 BF 33 33` | 95.6 °C |
| `0x11` | 17 | System Rectifier Max Voltage | 4 | float BE | R/W | `42 BF 33 33` | 95.6 V |
| `0x12` | 18 | Kp during CC Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x13` | 19 | Ki during CC Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x14` | 20 | Kd during CC Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x15` | 21 | Kp during CC Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x16` | 22 | Ki during CC Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x17` | 23 | Kd during CC Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x18` | 24 | Kp during CV Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x19` | 25 | Ki during CV Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x1A` | 26 | Kd during CV Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x1B` | 27 | Kp during CV Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x1C` | 28 | Ki during CV Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x1D` | 29 | Kd during CV Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x1E` | 30 | Kp during CP Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x1F` | 31 | Ki during CP Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x20` | 32 | Kd during CP Charge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x21` | 33 | Kp during CP Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x22` | 34 | Ki during CP Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |
| `0x23` | 35 | Kd during CP Discharge | 4 | float BE | R/W | `42 BF 33 33` | 95.6 |

**Total if concatenated in listed order: 131 bytes.**

```
  18 non-PID parameters                                   59 bytes
    6 + 4 + 4 + 2 + 2 + 2 + 1 + 1                       = 22   (0x01-0x07)
    4 + 4 + 4 + 4 + 4 + 4                               = 24   (0x08-0x0D)
    1                                                   =  1   (0x0E)
    4 + 4 + 4                                           = 12   (0x0F-0x11)
  18 PID gains x 4 bytes                                  72 bytes
                                                         ---------
                                                         131 bytes
```

⚠ The two duplicate `0x05` IDs (§9.6) make the intended *ordering* ambiguous even
though the total is not. Both UDP port fields are counted above; if the duplicate
is an editing error that dropped a field rather than added one, the total changes.

**PID gains** — the 18 constants `0x12`–`0x23` form a regular 3×6 block:
`{Kp, Ki, Kd}` × `{CC Charge, CC Discharge, CV Charge, CV Discharge, CP Charge,
CP Discharge}`. The operating modes match the `0xCC` realtime frame's Operator
Code field (`CCChg`, `CCDchg`, `CVChg`, …) in `bm_measured_param_v5.2.md`.

---

## 7. Manufacturing Data parameters

From sheet **`Manufacturing Data`** — *"BTS Manufacturing Information Parameters
Encoding Packet Details"*. Used by Q3 (read, circuit-scoped), Q8 (write,
broadcast) and Q10 (read, broadcast).

| ID (hex) | ID (dec) | Parameter | Size | Type | Sample | Sample value |
|---|---|---|---|---|---|---|
| `0x97` | 151 | Master SW Version | 11 | ASCII | `39 39 39 2E 39 39 39 2E …` | `"999.999.999"` — **read-only** |
| `0x98` | 152 | COM SW Version | 11 | ASCII | `39 39 39 2E 39 39 39 2E …` | `"999.999.999"` — **read-only** |
| `0x99` | 153 | Secondary SW Version | 11 | ASCII | `39 39 39 2E 39 39 39 2E …` | `"999.999.999"` — **read-only** |
| `0x9A` | 154 | BTS Primary Serial number | 4 | uint32 BE | `00 00 05 3E` | 1342 |
| `0x9B` | 155 | BTS Secondary Serial number | 4 | uint32 BE | `00 00 05 3E` | 1342 |
| `0x9C` | 156 | Manufacture date & time | 4 | epoch BE | `67 E3 91 C3` | 1742967235 = 2025-03-26 11:03:55 |
| `0x9D` | 157 | Commissioning date & time | 4 | epoch BE | `67 E3 91 C3` | 1742967235 |
| `0x9E` | 158 | BTS Primary PCB assembly date & time | 4 | epoch BE | `67 E3 91 C3` | 1742967235 |
| `0x9F` | 159 | BTS Secondary PCB assembly date & time | 4 | epoch BE | `67 E3 91 C3` | 1742967235 |

**Total: 3×11 + 6×4 = 57 bytes.**

The three SW-version strings are `"999.999.999"` — exactly 11 ASCII characters,
so the field is a fixed-width string with **no NUL terminator**. `0x39` is `'9'`
and `0x2E` is `'.'`.

Per the Q8 note (§4.2), the three version fields must **not** be written by the
Web Application — the board reports them.

---

## 8. Other Data parameters

From sheet **`Other Data`** — *"BTS Other Configurable Parameters Encoding Packet
Details"*.

| ID (hex) | ID (dec) | Parameter | Size | Type | Sample | Sample value |
|---|---|---|---|---|---|---|
| `0xC9` | 201 | Real time — Epoch time | 4 | epoch BE | `67 E3 91 C3` | 1742967235 = 2025-03-26 11:03:55 Wednesday |

Spreadsheet note: *"No need to display this value on GUI."*

This is the same value the Q6 Sync Time frame (§3.6) carries, and the same
encoding as the Manufacturing date fields (§7).

---

## 9. Discrepancies & open questions

Recorded rather than corrected. Each needs the Web Application team to confirm,
**except §9.3, which the developer resolved on 2026-08-12** (Impedance and Energy
Density are `float`).

### 9.1 Broadcast frames use a 2-byte header

Q7–Q10 have **no Device Number and no Circuit Number** (§1). Any receiver written
against the Q1–Q6 header will mis-parse them: a Q7 request `AA 07 01 02 03 04 …`
reads as Start `0xAA`, Device `0x07`, Circuit `0x01`, QueryID `0x02` — i.e. it
would look like a *Q2 Read Factory Configuration* for a circuit that does not
exist.

*Status in `me-primary`:* Q7–Q10 are not implemented. `frame_router.c` parses all
`0xAA` frames with the 4-byte header, so a broadcast frame would be classified as
`ME_MSG_STORE_CONFIG` for CircuitID `0x01` — which is malformed (secondary nibble
`0`, and both nibbles are 1-based), so `me_circuit_slot()` rejects it and
admission control drops the frame. **The current behaviour is safe, but by
accident.** It must be handled deliberately before Q7–Q10 are used.

### 9.2 Copy-paste artefacts in the response rows

| Location | Printed | Should almost certainly be |
|---|---|---|
| Response1 "No" (row 12) | Device Number `0x00` | `0x01` — Device Number is not a status field |
| Response6 (rows 79–80) | Query ID `0x01` | `0x06` — the whole block is duplicated from Q1 |
| Response6 "Unsuccessful" | Device Number `0x00` | `0x01` |

The Value byte (`0x01`/`0x00`) is what carries success or failure. Treating
Device Number as a status indicator would be a misreading of the spreadsheet.

### 9.3 Battery Impedance and Energy Density — float or integer? ✅ RESOLVED: float

**Resolved by the developer on 2026-08-12: both are `float` BE.** The Web
Application team will be told to send them as float only. The spreadsheet's
integer-looking samples are therefore an **error in the spreadsheet**, not an
alternative encoding, and this section is kept only to record why the question
was ever open.

**The spreadsheet and the observed traffic disagreed.**

| Source | Impedance (`0x6C`) | Energy Density (`0x6F`) |
|---|---|---|
| Spreadsheet sample | `00 00 00 64` labelled "100 Ohm" → looks like **uint32** | `00 00 00 64` labelled "100 Wh/Kg" → looks like **uint32** |
| Captured frame (§5.4) | `3F 80 00 00` = **float** 1.0 | `40 00 00 00` = **float** 2.0 |
| **Developer, 2026-08-12** | **float** | **float** |

Every other 4-byte battery field is annotated *"It will be in float"* in the
spreadsheet; these two are the only ones that are **not**, and their samples
decode sensibly only as integers (`0x00000064` = 100 as uint32, but
1.4 × 10⁻⁴³ as float).

The captured frame was already decisive about what the Web Application actually
sends: the operator entered `1` and `2`, and the bytes are IEEE-754 floats. Had
the encoding been uint32, they would have been `00 00 00 01` and `00 00 00 02`.
The developer's confirmation agrees with the wire, so **the parser needed no
change** — `me_battery_parse()` has read both as `float` since 2026-08-12, pinned
by `test_impedance_and_energy_density_are_floats()`.

Worth keeping in mind: both readings are 4 bytes wide, so had this gone the other
way it would have produced a nonsense value rather than a parse error. That is
why it was raised rather than assumed.

### 9.4 Battery ID's parameter ID collides with Battery Charge Factor ⚠

Both are documented as ID `0x6B` / 107. The battery IDs otherwise run
consecutively `0x65`–`0x6F` (101–111), so **Battery ID should be `0x70` / 112**.

This does not affect the Q5 frame — §5.2 establishes that IDs are not transmitted
there — but it would matter for any single-parameter access keyed by ID.

### 9.5 Client Remote IP Address sample bytes contradict its decoded value

Row 19–20: bytes `C0 A8 64 0E` are printed with the decoded value
**192.168.0.10**, but `0x0E` is 14, so those bytes are 192.168.0.14 — identical to
the Device IP Address above them. Either the bytes should end `0x0A`, or the
decoded text should read `.14`. The *width* (4 bytes) is unambiguous.

### 9.6 Two Factory parameters share ID `0x05`

"UDP Client (Live data) Remote Port" and "UDP Client (Registration data) Remote
Port" are both given ID `0x05` / 5, and both show sample bytes `27 10` (= 10000)
while the decoded values read 10000 and **10001** respectively. The registration
port's ID is presumably `0x06`, which would then push DHCP Enable to `0x07` and
cascade through the whole table — or the duplicate is simply an editing error and
the remaining IDs are correct.

**Do not renumber the Factory table on the strength of this document.** The
ambiguity affects the byte ordering of the Q2/Q7/Q9 concatenated payloads, which
is why §6's total is marked as needing a recount.

Note also that this project calls UDP 10001 the **session** port
(`ME_PORT_UDP_SESSION`), whereas the spreadsheet calls it the **registration
data** port. Registration itself runs over TCP 9999 in the ME design.

### 9.7 The referenced sheet "BTS_ConfigPacket" does not exist

The Configuration sheet repeats the note *"For this Packet please refer sheet
'BTS_ConfigPacket'"* for Q2, Q3, Q4 and Q5. **No such sheet exists in this
workbook.** The per-parameter detail is in the `Factory Data`, `Manufacturing
Data`, `Battery Data` and `Other Data` sheets, transcribed above as §5–§8.

### 9.8 Q6 Sync Time appears on both `0xAA` and `0xEE`

This document puts Sync Time on `0xAA` Q6 with a 4-byte epoch.
`bm_control_v3.0.md` puts a Sync Time on `0xEE` Q5, also with a 4-byte epoch, and
that is the one `me-primary` implements (`ME_CTRL_SYNC_TIME`). Whether these are
two paths to the same function, or one supersedes the other, is unresolved.

---

## 10. Implementation status in `me-primary`

| Query | Router classification | Handled | Notes |
|---|---|---|---|
| Q1 ready | `ME_MSG_STORE_CONFIG` | ❌ | Owes a real `Value` response; not answered |
| Q2 read factory | `ME_MSG_STORE_CONFIG` | ❌ | Read query — owes data, not an ack |
| Q3 read manufacturing | `ME_MSG_STORE_CONFIG` | ❌ | Read query — owes data |
| Q4 read battery | `ME_MSG_STORE_CONFIG` | ❌ | Read query — owes data; would serve from the store |
| **Q5 write battery** | `ME_MSG_STORE_BATTERY` | ✅ | Parsed, stored per circuit, acked `0x01`/`0x00` |
| Q6 sync time | `ME_MSG_STORE_CONFIG` | ❌ | `0xEE` Q5 is the implemented path (§9.8) |
| Q7–Q10 broadcast | mis-parsed, then dropped | ❌ | See §9.1 |

`STORE_CONFIG` frames are deliberately **not** acknowledged: Q1–Q4 and Q6 owe the
Web Application either real data or a meaningful status, and a bare 7-byte
"success" would claim a read that never happened (ADR-18).

The Q5 acknowledgement is built by `me_ack_pack()` in `src/proto/ack_frame.c`,
which echoes Start, Device Number, Circuit Number and Query ID from the inbound
frame and appends the Value plus CRC — **exactly the shape §3.5 documents**. That
function's header previously described the `0xAA` reply as inferred; this document
is its source.

---

## Appendix — related documents

| Document | Start byte | Covers |
|---|---|---|
| `bm_device_registration_v5.0.md` | `0xDD` | Device registration over TCP 9999 |
| **`bm_config_v6.0.md`** (this file) | `0xAA` | Factory / manufacturing / battery configuration |
| `bm_program_v3.0.md` | `0xBB` | Battery-testing program transfer |
| `bm_measured_param_v5.2.md` | `0xCC` | Live measured parameters over UDP 10000 |
| `bm_control_v3.0.md` | `0xEE` | Start / Stop / Pause / Continue / Sync Time |
| `bm_calibration_v4.2.md` | `0xA0` | Calibration |
