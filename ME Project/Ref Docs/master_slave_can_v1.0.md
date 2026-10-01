# Master-Slave CAN-FD Protocol - Cell Tester / Pack Tester

| Field | Value |
|---|---|
| Document title | Master-Slave Protocol for Cell Tester/Pack Tester |
| Source file | `D:\Projects\Downloads\Vijay Sir\Master-Slave_CAN_V1.0.xlsx` |
| Version | 1.0 |
| Date | 7-Aug-26 |
| Author | VBS/RLS |
| Remark | - Initial |

> **This document is a transcription of the source `.xlsx` workbook.** It is
> provided as a readable, implementer-friendly rendering of the spreadsheet.
> **The `.xlsx` file remains the authoritative source.** Where this document and
> the workbook disagree, the workbook wins. Anything ambiguous, interpreted, or
> internally inconsistent is called out in [Transcription Notes](#transcription-notes)
> at the end.

Sheets transcribed, in workbook order: `Version`, `CAN Message`, `Set_Cmd`,
`Broad_Cmd`, `Error_Code`, `EEP_Para_List`, `INT_Para_List`.

---

## Version

Sheet used range: `A1:D28`.

Sheet header cell (`A1`, merged across `A1:D1`):

> Master-Slave Protocol for Cell Tester/Pack Tester
>
> The document describes Master-Slave Protocol for Cell Tester/Pack Tester

Revision history:

| Version | Date | Author | Remark |
|---|---|---|---|
| 1.0 | 7-Aug-26 | VBS/RLS | - Initial |

Rows `A5:D28` are empty.

---

## CAN Message

Sheet used range: `A1:N42`.

Title: **CAN-FD Communication Command Format Details**

Note: Master sends commands to Slave for data, then Slave sends required data.

### Bus configuration

| Item | Value |
|---|---|
| Baud Rate | 500 Kbps (arbitration phase) + 2 Mbps (data phase) |
| Byte Order | Intel (Little-Endian) |
| Identifier | 11-bit Standard Identifier |
| DLC | `0x0F` (64 bytes) / `0x09` (12 bytes) |

### Identifier layout

```
yyyyyyzzzzz   then data
```

| Symbol | Meaning | Width |
|---|---|---|
| `y` | Circuit number | 6 bits |
| `z` | Function number | 5 bits |
| `x` | Data | - |

The circuit field is 6 bits, giving **64 circuits**. A 4-channel cell/pack
tester therefore supports **256 channels**; an 8-channel tester supports
**512 channels**.

### Functions

| # | Function | Value | Description (verbatim from sheet) |
|---|---|---|---|
| 1 | SET_VALUES | `0x01` | Setting Presets and command message |
| 2 | READ_VALUES | `0x02` | Response to SET_VALUES |
| 3 | BROADCAST | `0x03` | Broadcast Command to All Cells/Packs |

> **Clarified by the developer (Nikhil_N, 2026-08-14) — read this before implementing.**
>
> `READ_VALUES` (`0x02`) is **issued by the Master as a read command**, not only emitted by the
> Slave as an unsolicited answer. The Master performs a **two-phase exchange**:
>
> 1. Master transmits `SET_VALUES` (function `0x01`) — the Secondary **applies** the setpoints
>    and the `Command` byte.
> 2. Master transmits `READ_VALUES` (function `0x02`) — the Secondary **reports** feedback and
>    `STATE`, changing nothing.
>
> **The 64-byte data packet is identical in both functions.** The Secondary decides what to do
> purely from the **function code in the identifier**. This is why byte `+10` of each channel slot
> is labelled `EEP Para #` in one direction and `INT Para #` in the other — it is one byte whose
> meaning is selected by the function code, not two different fields.
>
> **Supporting evidence inside this same workbook:**
> - The `Set_Cmd` sheet is titled **"Definition of function SET/**GET**_VALUES"** (cell `A2`) —
>   `GET` is a Master-issued verb.
> - The Normal Mode rule (`A18`) states the **Query** carries the `INT Parameter number` that
>   selects which internal parameter comes back in the Response. Only a Master-transmitted read
>   frame can carry that selection.
>
> The `Master` / `Slave` row labels at `A7` / `A8` therefore describe **what the payload means in
> each direction**, not an exhaustive list of who may transmit which function code.
>
> ⚠ **Consequence:** both the read request and its response use function code `0x02`, so two
> different nodes transmit on the same 11-bit arbitration ID. This is safe under strict
> master-slave polling (the bus is serialised by the polling discipline), but a bus analyser
> cannot tell request from response by identifier alone — only by direction and timing.

### Polling principle

Master acts as Primary and polls all Slaves. Each Master message must be
answered by the Slave with the corresponding message. If a Slave does not answer
within a defined time, the message is resent **3 times**; if there is still no
answer the Slave is considered **offline** and is not polled again.

### Bus timing - 1 Module : 4 Channel

| Data-phase rate | Channels | Time |
|---|---|---|
| 2 Mbps | 4 ch | 1 ms |
| 2 Mbps | 20 ch | 5 ms |
| 2 Mbps | 40 ch | 10 ms |
| 2 Mbps | 200 ch | 50 ms |

### Bus timing - 1 Module : 8 Channel

| Data-phase rate | Channels | Time |
|---|---|---|
| 2 Mbps | 8 ch | 2 ms |
| 2 Mbps | 40 ch | 10 ms |
| 2 Mbps | 80 ch | 20 ms |
| 2 Mbps | 400 ch | 100 ms |

---

## Set_Cmd

Sheet used range: `A2:AY65`.

Section title (`A2`, merged `A2:J2`): **Definition of function SET/GET_VALUES**

Label in `A4`: `Protocol:` (the cell to its right is blank; the byte map below is
what it introduces).

### Frame structure overview

The sheet lays out **two 64-byte CAN-FD frames**, one per block:

| Block | Sheet rows | Channels carried | Byte span per channel |
|---|---|---|---|
| Block 1 | 5-8 | Channel 1, Channel 2, (Channel 3), Channel 4 | 0-15, 16-31, 32-47, 48-63 |
| Block 2 | 10-13 | Channel 5, Channel 6, (Channel 7), Channel 8 | 0-15, 16-31, 32-47, 48-63 |

Each block has two message rows:

| Direction | Row (Block 1) | Row (Block 2) | CANID cell |
|---|---|---|---|
| Master (request) | 7 | 12 | `Dev#,1` |
| Slave (response) | 8 | 13 | `Dev#,2` |

> These two rows show **what the 64 bytes mean in each direction**. They are NOT a statement that
> only the Slave may transmit function `0x02` — the Master issues `READ_VALUES` (`0x02`) as a read
> command. See the clarification under [Functions](#functions) above.
>
> Identifier construction, both functions:
> `CAN_ID = (circuit6 << 5) | function5`, 11-bit standard identifier.

**Two behaviours confirmed by the developer (Nikhil_N, 2026-08-14), not stated in the workbook:**

1. **The Secondary LATCHES the setpoint.** Once a `SET_VALUES` frame is applied, the setpoint
   persists until the next `SET_VALUES`. **No keep-alive re-send is required**, and the Master
   must not re-issue a setpoint merely to obtain feedback.
2. **The Secondary answers EVERY Master frame**, including `SET_VALUES` — it replies to a
   `SET_VALUES` exactly as it replies to a `READ_VALUES`, with a feedback frame. So the request →
   response rule on the `CAN Message` sheet ("each Master message must be answered by the Slave
   with the corresponding message", 3 retries then offline) applies **uniformly to both function
   codes**.

Consequences for the Master implementation:
- Every transmitted frame has exactly one expected response — one timeout/retry rule, not two.
- A `SET_VALUES` at step entry therefore yields a feedback sample **immediately**, rather than the
  Master waiting for the first poll interval to elapse.
- Recurring bus traffic per channel is one `READ_VALUES` per poll interval. `SET_VALUES` is sent
  only on step entry and on stop.

⚠ **Inferred, cheap to confirm:** the Slave is assumed to respond on function `0x02` regardless of
whether the request was `0x01` or `0x02` (per the `Slave` row's `Dev#,2` CANID in both blocks).

**The three frames the Master transmits:**

| Purpose | Function | CAN ID | DLC | Secondary's action |
|---|---|---|---|---|
| Apply setpoint / command | `SET_VALUES` `0x01` | `(circuit << 5) \| 1` | `0x0F` (64 B) | Applies `Set Voltage`, `Set Current`, `Command` per channel slot |
| Poll for feedback | `READ_VALUES` `0x02` | `(circuit << 5) \| 2` | `0x0F` (64 B) | Replies with `Feedback Voltage`, `Feedback Current`, `STATE`; changes nothing |
| Broadcast | `BROADCAST` `0x03` | `(circuit << 5) \| 3` | `0x09` (12 B) | Applies to every channel of every tester; no reply |

`Dev#,1` / `Dev#,2` correspond to the identifier construction from the
`CAN Message` sheet: circuit/device number in the upper 6 bits, function number
in the lower 5 bits (`1` = SET_VALUES, `2` = READ_VALUES).

Every frame is **64 bytes**, i.e. `DLC = 0x0F`, and carries **four channels of
16 bytes each**. The 16-byte per-channel record has an identical layout in every
slot; only the semantics of three fields differ between the Master and Slave
directions.

### Per-channel 16-byte record (slot-relative offsets)

| Offset in slot | Length | Master field (SET_VALUES) | Slave field (READ_VALUES) |
|---|---|---|---|
| +0 .. +3 | 4 | `Voltage : Set Voltage (float)` | `Voltage : Feedback Voltage (float)` |
| +4 .. +7 | 4 | `Current : Set Current (float)` | `Current : Feedback Current (float)` |
| +8 | 1 | `Command` | `STATE` |
| +9 | 1 | `Channel #` | `Channel #` |
| +10 | 1 | `EEP Para #` | `INT Para #` |
| +11 | 1 | `Reserved` | `Reserved` |
| +12 .. +15 | 4 | `Data (float)` | `Data (float)` |

### Block 1 - Master frame (CANID `Dev#,1`), full byte map

Merged header spans in the sheet: row 5 `C5:R5` = "Channel 1", `S5:AH5` =
"Channel 2", `AJ5:AY5` = "Channel 4". Column `AI` is a single-column gap holding
the literal text `---`, standing in for the omitted Channel 3 / bytes 32-47.

| Byte(s) | Channel slot | Field name (verbatim) | Type / length |
|---|---|---|---|
| 0-3 | Channel 1 | `Voltage : Set Voltage (float)` | float, 4 bytes |
| 4-7 | Channel 1 | `Current : Set Current (float)` | float, 4 bytes |
| 8 | Channel 1 | `Command` | u8, 1 byte |
| 9 | Channel 1 | `Channel #` | u8, 1 byte |
| 10 | Channel 1 | `EEP Para #` | u8, 1 byte |
| 11 | Channel 1 | `Reserved` | u8, 1 byte |
| 12-15 | Channel 1 | `Data (float)` | float, 4 bytes |
| 16-19 | Channel 2 | `Voltage : Set Voltage (float)` | float, 4 bytes |
| 20-23 | Channel 2 | `Current : Set Current (float)` | float, 4 bytes |
| 24 | Channel 2 | `Command` | u8, 1 byte |
| 25 | Channel 2 | `Channel #` | u8, 1 byte |
| 26 | Channel 2 | `EEP Para #` | u8, 1 byte |
| 27 | Channel 2 | `Reserved` | u8, 1 byte |
| 28-31 | Channel 2 | `Data (float)` | float, 4 bytes |
| 32-35 | Channel 3 | `---` (elided in sheet; by pattern: `Voltage : Set Voltage (float)`) | float, 4 bytes |
| 36-39 | Channel 3 | `---` (elided in sheet; by pattern: `Current : Set Current (float)`) | float, 4 bytes |
| 40 | Channel 3 | `---` (elided in sheet; by pattern: `Command`) | u8, 1 byte |
| 41 | Channel 3 | `---` (elided in sheet; by pattern: `Channel #`) | u8, 1 byte |
| 42 | Channel 3 | `---` (elided in sheet; by pattern: `EEP Para #`) | u8, 1 byte |
| 43 | Channel 3 | `---` (elided in sheet; by pattern: `Reserved`) | u8, 1 byte |
| 44-47 | Channel 3 | `---` (elided in sheet; by pattern: `Data (float)`) | float, 4 bytes |
| 48-51 | Channel 4 | `Voltage : Set Voltage (float)` | float, 4 bytes |
| 52-55 | Channel 4 | `Current : Set Current (float)` | float, 4 bytes |
| 56 | Channel 4 | `Command` | u8, 1 byte |
| 57 | Channel 4 | `Channel #` | u8, 1 byte |
| 58 | Channel 4 | `EEP Para #` | u8, 1 byte |
| 59 | Channel 4 | `Reserved` | u8, 1 byte |
| 60-63 | Channel 4 | `Data (float)` | float, 4 bytes |

> ⚠ unclear: bytes 32-47 (Channel 3) are **not written out** in the sheet.
> Column `AI` contains only the literal string `---` in the byte-number and both
> message rows. The rows above are reconstructed from the Channel 1 / 2 / 4
> pattern and are marked as such. Confirm against the workbook before coding.

### Block 1 - Slave frame (CANID `Dev#,2`), full byte map

| Byte(s) | Channel slot | Field name (verbatim) | Type / length |
|---|---|---|---|
| 0-3 | Channel 1 | `Voltage : Feedback Voltage (float)` | float, 4 bytes |
| 4-7 | Channel 1 | `Current : Feedback Current (float)` | float, 4 bytes |
| 8 | Channel 1 | `STATE` | u8, 1 byte |
| 9 | Channel 1 | `Channel #` | u8, 1 byte |
| 10 | Channel 1 | `INT Para #` | u8, 1 byte |
| 11 | Channel 1 | `Reserved` | u8, 1 byte |
| 12-15 | Channel 1 | `Data (float)` | float, 4 bytes |
| 16-19 | Channel 2 | `Voltage : Feedback Voltage (float)` | float, 4 bytes |
| 20-23 | Channel 2 | `Current : Feedback Current (float)` | float, 4 bytes |
| 24 | Channel 2 | `STATE` | u8, 1 byte |
| 25 | Channel 2 | `Channel #` | u8, 1 byte |
| 26 | Channel 2 | `INT Para #` | u8, 1 byte |
| 27 | Channel 2 | `Reserved` | u8, 1 byte |
| 28-31 | Channel 2 | `Data (float)` | float, 4 bytes |
| 32-35 | Channel 3 | `---` (elided; by pattern: `Voltage : Feedback Voltage (float)`) | float, 4 bytes |
| 36-39 | Channel 3 | `---` (elided; by pattern: `Current : Feedback Current (float)`) | float, 4 bytes |
| 40 | Channel 3 | `---` (elided; by pattern: `STATE`) | u8, 1 byte |
| 41 | Channel 3 | `---` (elided; by pattern: `Channel #`) | u8, 1 byte |
| 42 | Channel 3 | `---` (elided; by pattern: `INT Para #`) | u8, 1 byte |
| 43 | Channel 3 | `---` (elided; by pattern: `Reserved`) | u8, 1 byte |
| 44-47 | Channel 3 | `---` (elided; by pattern: `Data (float)`) | float, 4 bytes |
| 48-51 | Channel 4 | `Voltage : Feedback Voltage (float)` | float, 4 bytes |
| 52-55 | Channel 4 | `Current : Feedback Current (float)` | float, 4 bytes |
| 56 | Channel 4 | `STATE` | u8, 1 byte |
| 57 | Channel 4 | `Channel #` | u8, 1 byte |
| 58 | Channel 4 | `INT Para #` | u8, 1 byte |
| 59 | Channel 4 | `Reserved` | u8, 1 byte |
| 60-63 | Channel 4 | `Data (float)` | float, 4 bytes |

### Block 2 - Channels 5 to 8

Sheet rows 10-13 repeat the identical structure for the second group of four
channels. Merged header spans: `C10:R10` = "Channel 5", `S10:AH10` = "Channel 6",
`AJ10:AY10` = "Channel 8". Column `AI` again carries only `---` (Channel 7,
bytes 32-47, elided).

**The byte offsets restart at 0** in Block 2 - it is a *second frame*, not a
continuation of Block 1.

| Byte(s) | Channel slot | Master field (row 12) | Slave field (row 13) |
|---|---|---|---|
| 0-3 | Channel 5 | `Voltage : Set Voltage (float)` | `Voltage : Feedback Voltage (float)` |
| 4-7 | Channel 5 | `Current : Set Current (float)` | `Current : Feedback Current (float)` |
| 8 | Channel 5 | `Command` | `STATE` |
| 9 | Channel 5 | `Channel #` | `Channel #` |
| 10 | Channel 5 | `EEP Para #` | `INT Para #` |
| 11 | Channel 5 | `Reserved` | `Reserved` |
| 12-15 | Channel 5 | `Data (float)` | `Data (float)` |
| 16-19 | Channel 6 | `Voltage : Set Voltage (float)` | `Voltage : Feedback Voltage (float)` |
| 20-23 | Channel 6 | `Current : Set Current (float)` | `Current : Feedback Current (float)` |
| 24 | Channel 6 | `Command` | `STATE` |
| 25 | Channel 6 | `Channel #` | `Channel #` |
| 26 | Channel 6 | `EEP Para #` | `INT Para #` |
| 27 | Channel 6 | `Reserved` | `Reserved` |
| 28-31 | Channel 6 | `Data (float)` | `Data (float)` |
| 32-47 | Channel 7 | `---` (elided in sheet) | `---` (elided in sheet) |
| 48-51 | Channel 8 | `Voltage : Set Voltage (float)` | `Voltage : Feedback Voltage (float)` |
| 52-55 | Channel 8 | `Current : Set Current (float)` | `Current : Feedback Current (float)` |
| 56 | Channel 8 | `Command` | `STATE` |
| 57 | Channel 8 | `Channel #` | `Channel #` |
| 58 | Channel 8 | `EEP Para #` | `INT Para #` |
| 59 | Channel 8 | `Reserved` | `Reserved` |
| 60-63 | Channel 8 | `Data (float)` | `Data (float)` |

### Definition of EEP Para #, INT Para # and related Data (float)

Section title (`A16`, merged `A16:J16`): **Definition of EEP Para #, INT Para #
and related Data (float)**

**Normal Mode** (`A18`, merged `A18:J20`), verbatim:

> Normal Mode : If the EEP Parameter number equals 0, the float data in the Query
> is 0 (not used). Instead, the INT Parameter number defines the internal
> parameter number, and its corresponding data is returned in the Response float
> data.

**Calibration/Configuration Mode** (`A22`, merged `A22:J24`), verbatim:

> Calibration/Configuration Mode : If the EEP Parameter number is greater than 0,
> the float data in both the Query and Response relates to the Read/Write EEPROM
> Parameter. In this case, the INT Parameter number is set to 0 (not used).

Summary of the two mutually exclusive modes:

| Mode | `EEP Para #` (Master, byte +10) | Query `Data (float)` | `INT Para #` (Slave, byte +10) | Response `Data (float)` |
|---|---|---|---|---|
| Normal | `0` | `0` (not used) | selects internal parameter | value of that internal parameter |
| Calibration / Configuration | `> 0` | EEPROM parameter value being written | `0` (not used) | EEPROM parameter value read back |

### Definition of Channel No. : 1 Channel to 8 Channel

Section title (`A26`, merged `A26:J26`): **Definition of Channel No. : 1 Channel
to 8 Channel**

Table header (row 28): `Sr.No.` | `MSG_TYPE` (merged `B28:C28`) | `Op-Code` |
`Description` (merged `E28:J28`).

| Sr.No. | MSG_TYPE | Op-Code | Description |
|---|---|---|---|
| 1 | Channel Number | `0x01 - 0x08` | Channel Number from 1 to 8. |

### Definition of Command : CHARGE, DISCHARGE etc.

Section title (`A31`, merged `A31:J31`): **Definition of Command : CHARGE,
DISCHARGE etc.**

Table header (row 33): `Sr.No.` | `Command` (merged `B33:C33`) | `Op-Code` |
`Description` (merged `E33:J33`).

This is the value placed in the Master byte at slot offset `+8`.

| Sr.No. | Command | Op-Code | Description |
|---|---|---|---|
| 1 | `CMD_STO` | `0x00` | The device  has to be in IDLE/STOP mode. (including EMERGENCY STOP). |
| 2 | `CMD_CHA` | `0x01` | The device has to be in CHARGE mode. |
| 3 | `CMD_DCH` | `0x02` | The device has to be in DISCHARGE mode. |
| 4 | `CMD_PAU` | `0x03` | The device  has to be in PAUSE/ACTIVE IDLE mode. |
| 5 | `CMD_RST_ERR` | `0x04` | Reset the last error from the device. |
| 6 | `CMD_CAL_CHA` | `0x05` | Calibration Mode : CHARGE. |
| 7 | `CMD_CAL_DCH` | `0x06` | Calibration Mode : DISCHARGE. |
| 8 | `RST_ALL_PARA` | `0x07` | Reset all EEPROM parameters to default. All Channels must is in IDLE Mode. |
| 9 | `RST_DEF_PARA` | `0x08` | Reset the selected EEPROM parameter to default. |

### Definition of State : CHARGE, DISCHARGE etc.

Section title (`A44`, merged `A44:J44`): **Definition of State : CHARGE,
DISCHARGE etc.**

Table header (row 46): `Sr.No.` | `State` (merged `B46:C46`) | `Op-Code` |
`Description` (merged `E46:J46`).

This is the value returned by the Slave in the byte at slot offset `+8`.

| Sr.No. | State | Op-Code | Description |
|---|---|---|---|
| 1 | `CMD_STO` | `0x00` | The device  has to be in IDLE/STOP mode. |
| 2 | `CMD_CHA` | `0x01` | The device has to be in CHARGE mode. |
| 3 | `CMD_DCH` | `0x02` | The device has to be in DISCHARGE mode. |
| 4 | `CMD_PAU` | `0x03` | The device  has to be in PAUSE/ACTIVE IDLE mode. |
| 5 | `CMD_ERR` | `0x04` | The slave node is in an error state. |
| 6 | `CMD_CAL_CHA` | `0x05` | Calibration Mode : CHARGE. |
| 7 | `CMD_CAL_DCH` | `0x06` | Calibration Mode : DISCHARGE. |
| 8 | `RST_ALL_PARA` | `0x07` | Reset all EEPROM parameters to default successfully. All Channels must is in IDLE Mode. |
| 9 | `RST_DEF_PARA` | `0x08` | Reset the selected EEPROM parameter to default successful. |

### Definition of Read/Write EEP Para # : Parameter Number

Section title (`A57`, merged `A57:J57`): **Definition of Read/Write EEP Para # :
Parameter Number**

Text (`A59`, merged `A59:E59`):

> The EEP Para # is a 8 bit and has the following binary format:

Bit layout (`B60`):

```
riiiiiii
```

| Bit | Name | Meaning (verbatim) |
|---|---|---|
| 7 | `r` | `r = 1 PARAMETER_RD, if bit 7 is set means reading the requested parameter.` |
| 7 | `r` | `r = 0 PARAMETER_WR, if bit 7 is not set means writing the parameter.` |
| 6..0 | `i` | `i = index of the parameter` |

So the byte at slot offset `+10` in a Master frame decomposes as:

| Field | Bits | Mask |
|---|---|---|
| `PARAMETER_RD` / `PARAMETER_WR` flag | bit 7 | `0x80` |
| Parameter index `i` | bits 6..0 | `0x7F` |

### Device State Diagram

Section title (`A65`, merged `A65:J65`): **Device State Diagram**

⚠ unclear: this is the last row of the sheet's used range. No cell content
follows it. Any state diagram present is an embedded drawing/image object and
**cannot be read through cell values**. See Transcription Notes.

---

## Broad_Cmd

Sheet used range: `A2:AI9`.

Section title (`A2`, merged `A2:J2`): **Definition of function Broadcast**

Label in `A4`: `Protocol:`

Description (`A9`, merged `A9:N9`), verbatim:

> Description : This is a broadcast command sent to all cell/pack testers
> simultaneously. The command definition is identical to the one used in Set_Cmd.

There is a **single message row** (row 6, Master, CANID `Dev#,3`). There is **no
Slave response row** for the broadcast. `Dev#,3` corresponds to function number
`3` = BROADCAST.

The frame is **12 bytes**, i.e. `DLC = 0x09`.

### BROADCAST frame (Master, CANID `Dev#,3`) - byte map

| Byte(s) | Field name (verbatim) | Type / length |
|---|---|---|
| 0-3 | `Voltage : Set Voltage (float)` | float, 4 bytes |
| 4-7 | `Current : Set Current (float)` | float, 4 bytes |
| 8 | `Command` | u8, 1 byte |
| 9 | `Reserved` | u8, 1 byte |
| 10 | `Reserved` | u8, 1 byte |
| 11 | `Reserved` | u8, 1 byte |

Notes on the layout:

- There is **no `Channel #` field** and **no `EEP Para #` field** - the broadcast
  addresses every channel of every tester at once.
- The `Command` byte takes values from the
  [Definition of Command](#definition-of-command--charge-discharge-etc) table in
  `Set_Cmd` (`0x00` .. `0x08`) - the sheet states the command definition is
  identical.
- Bytes 9, 10 and 11 are three **separate** `Reserved` cells (columns `L`, `M`,
  `N`), not one merged 3-byte field.
- Columns `O` through `AI` of the used range are entirely empty.

---

## Error_Code

Sheet used range: `A2:H25`.

Section title (`A2`, merged `A2:C2`): **Definition of Error Codes**

Table header (row 4): `Error code No#` | `Error code Name` | `Error code Definition`.

| Error code No# | Error code Name | Error code Definition |
|---|---|---|
| 0 | `ERR_NONE` | There are not any fault |
| 1 | `ERR_DC_LINK_OVP` | DC link voltage above threshold |
| 2 | `ERR_DC_LINK_UVP` | DC link voltage below threshold |
| 3 | `ERR_BT_OVP` | Battery voltage above threshold |
| 4 | `ERR_BT_UVP` | Battery voltage below threshold |
| 5 | `ERR_CAP_OVP` | Capacitor voltage above threshold |
| 6 | `ERR_BT_OCP` | Battery current above threshold |
| 7 | `ERR_SETPOINT_TIMEOUT` | Neither CV nor CC loop reaches setpoint |
| 8 | `ERR_CAP_BT_MISMATCH` | Capacitor and battery voltage mismatch |
| 9 | `ERR_PRIM_COMM_LOSS` | Communication loss between primary board and DC-DC converter MCU |
| 10 | `ERR_PRECHARGE_TIMEOUT` | Startup pre-charge timed out |
| 11 | `ERR_CUR_DIR_MISMATCH` | Current direction is reverse as compare to CC |
| 12 | `ERR_BT_REVERSE` | Battery is connected reverse |
| 13 | `ERR_BT_OTP` | Battery temperature above threshold |
| 14 | `ERR_BT_UTP` | Battery temperature below threshold |
| 15 | `ERR_SHORT_CIRCUIT` | Inductor current above threshold |
| 16 | `ERR_FAST_DC_LINK_OVP` | DC link voltage above threshold at fast rate |
| 17 | `ERR_FAST_CAP_OVP` | Capacitor voltage above threshold at fast rate |
| 18 | `ERR_HEAT_SINK_OTP` | Heat sink temperature above threshold |
| 19 | `ERR_HEAT_SINK_UTP` | Heat sink temperature below threshold |

Total: **20 error codes** (`0` .. `19`). Row 25 and columns `D`..`H` are empty.

These are surfaced through the `INT Para #` mechanism - see internal parameters
`1` (`Error Flags`) and `2` (`Error Number`) in
[INT_Para_List](#int_para_list).

---

## EEP_Para_List

Sheet used range: `A2:D48`.

Section title (`A2`, merged `A2:D2`): **Definition of Parameter : EEPROM Parameters**

Table header (row 4): `Para #` | `Cal/ Conf` | `Description` | `Remarks `
(note: the `Remarks` header cell has a trailing space in the workbook).

These are the parameters selected by the `EEP Para #` byte (slot offset `+10` in
a Master frame), with the read/write direction encoded in bit 7 of that byte.

| Para # | Cal/ Conf | Description | Remarks |
|---|---|---|---|
| 1 | `---` | Reserved | Reserved |
| 2 | Calibration | V Gain CHA | Voltage Gain for Charge for Channel 1 - 8 |
| 3 | Calibration | V Offset CHA | Voltage Offset for Charge for Channel 1 - 8 |
| 4 | Calibration | I Gain CHA | Current Gain for Charge for Channel 1 - 8 |
| 5 | Calibration | I Offset CHA | Current Offset for Charge for Channel 1 - 8 |
| 6 | Calibration | I Gain DCH | Current Gain for Discharge for Channel 1 - 8 |
| 7 | Calibration | I Offset DCH | Current Offset for Discharge for Channel 1 - 8 |
| 8 | Calibration | Vdc link Gain | Vdc link Gain for Channel 1 - 8 |
| 9 | Calibration | Vdc link Offset | Vdc link Offset for Channel 1 - 8 |
| 10 | Calibration | Vdc Cap Gain | Vdc Capacitor Gain for Channel 1 - 8 |
| 11 | Calibration | Vdc Cap Offset | Vdc Capacitor Offset for Channel 1 - 8 |
| 12 | Calibration | CV Kp CHA | Voltage Kp for Charge for Channel 1 - 8 |
| 13 | Calibration | CV Ki CHA | Voltage Ki for Charge for Channel 1 - 8 |
| 14 | Calibration | CV Kp DCH | Voltage Kp for Discharge for Channel 1 - 8 |
| 15 | Calibration | CV Ki DCH | Voltage Ki for Discharge for Channel 1 - 8 |
| 16 | Calibration | CC Kp CHA | Current Kp for Charge for Channel 1 - 8 |
| 17 | Calibration | CC Ki CHA | Current Ki for Charge for Channel 1 - 8 |
| 18 | Calibration | CC Kp DCH | Current Kp for Discharge for Channel 1 - 8 |
| 19 | Calibration | CC Ki DCH | Current Ki for Discharge for Channel 1 - 8 |
| 20 | Calibration | BATT TEMP | Battery Temperature for Channel 1 - 8 |
| 21 | Calibration | HEAT SINK TEMP | Heat Sink Temperature for Channel 1 - 8 |
| 22 | Calibration | IL TEMP | Inductor Current Temperature for Channel 1 - 8 |
| 23 | Configuration | System Type | System Type : 6V/10A, 100V/50A, 6V/50A etc. |
| 24 | Configuration | Software version | Software version is 1.00, 1.01 etc. |
| 25 | Configuration | Hardware version | Hardware version is 1.00, 1.01 etc. |
| 26 | Configuration | Software DIP switch | Storing virtual settings in memory allows you to change device configurations digitally. |
| 27 | Configuration | Battery Voltage MAX | Maximum Voltage of the system. - For trip (Protection of circuit) |
| 28 | Configuration | Battery Voltage MIN | Minimum Voltage of the system. - For trip (Protection of circuit) |
| 29 | Configuration | Battery Current MAX CHA | Maximum Current of the circuit/system. For e.g. 10amp- For trip (Protection of circuit) |
| 30 | Configuration | Battery Current MAX DCH | Maximum Current of the circuit/system. For e.g. -10amp- For trip (Protection of circuit) |
| 31 | Configuration | DC link voltage MAX | Higher limit for DC link voltage |
| 32 | Configuration | DC link voltage MIN | Lower limit for DC link voltage |
| 33 | Configuration | BATT TEMP MAX | Battery Temperature Maximum |
| 34 | Configuration | BATT TEMP MIN | Battery Temperature Minimum |
| 35 | Configuration | HEAT SINK TEMP MAX | Heat Sink Temperature Maximum |
| 36 | Configuration | HEAT SINK TEMP MIN | Heat Sink Temperature Minimum |
| 37 | Configuration | IL TEMP MAX | Inductor Current Temperature Maximum |
| 38 | Configuration | IL TEMP MIN | Inductor Current Temperature Minimum |
| 39 | Configuration | Diff between Vcap and Vsense | 1. To make sure, No extra V drop in between, Vcap and V cell<br>2. Also to make sure, connection err is not there |
| 40 | Configuration | SETPOINT TIMEOUT | CV or CC loop Setpoint Timeout |
| 41 | Configuration | PRECHARGE TIMEOUT | Startup Precharge Timeout |

Total: **41 EEPROM parameters** (`1` .. `41`); parameter `1` is `Reserved`.
Split: `2`..`22` are **Calibration** (21 parameters), `23`..`41` are
**Configuration** (19 parameters). Rows 46-48 are empty.

---

## INT_Para_List

Sheet used range: `A2:C15`.

Section title (`A2`, merged `A2:C2`): **Definition of Parameter : Internal Parameters**

Table header (row 4): `Para #` | `Description` | `Remarks `
(note: the `Remarks` header cell has a trailing space in the workbook).

These are the parameters selected by the `INT Para #` byte (slot offset `+10` in
a Slave frame) and returned in the Slave `Data (float)` at slot offsets
`+12 .. +15`.

| Para # | Description | Remarks |
|---|---|---|
| 1 | Error Flags | Error Flags for Channel 1 - 8 |
| 2 | Error Number | Error Numbers for Channel 1 - 8 |
| 3 | Vdc link | Vdc link for Channel 1 - 8 |
| 4 | BATT TEMP | Battery Temperature for Channel 1 -8 |
| 5 | HEAT SINK TEMP | Heat Sink Temperature for Channel 1 - 8 |
| 6 | IL TEMP | Inductor Current Temperature for Channel 1 - 8 |
| 7 | IL Current | Inductor Current for Channel 1 - 8 |
| 8 | Vdc Cap | Vdc Capacitor for Channel 1 - 8 |

Total: **8 internal parameters** (`1` .. `8`). Rows 13-15 are empty.

The value returned for `INT Para # = 2` (`Error Number`) is an error code from
the [Error_Code](#error_code) table.

---

## Transcription Notes

### Columns the workbook does not provide

1. **There are no scaling / resolution / unit / min / max columns anywhere in
   this workbook.** The task brief asked for these to be preserved per field;
   they simply do not exist in `Master-Slave_CAN_V1.0.xlsx` v1.0. All numeric
   payload fields are declared only as `(float)` with no explicit scaling factor,
   no engineering unit, and no range. The `Type / length` columns in this
   document are **derived** from the `(float)` annotation in the field name plus
   the number of consecutive byte columns the field spans - they are not
   separately stated in the sheet. Implementers should treat float fields as raw
   IEEE-754 binary32 in engineering units with a scaling factor of 1 unless the
   hardware team says otherwise, and should get units confirmed before writing
   any decode code.
2. Likewise, no field carries a "Remark" column in the byte-map region. The only
   remarks in the workbook are on the two parameter-list sheets, and those are
   transcribed in full.

### Elided / unreadable content

3. **Channel 3 (bytes 32-47) and Channel 7 (bytes 32-47) are not written out.**
   In `Set_Cmd`, column `AI` holds only the literal three-character string `---`
   in the byte-number row and in both the Master and Slave rows of both blocks.
   Excel column `AI` sits between the Channel 2 block (`S`..`AH`) and the
   Channel 4 block (`AJ`..`AY`), so the sheet is using `---` as a visual
   ellipsis. The 16 bytes for Channel 3 / Channel 7 in this document are
   **reconstructed by pattern** and are explicitly flagged as such in the byte
   tables. This is an interpretation, not a transcription.
4. **`Set_Cmd` row 65, "Device State Diagram", has no readable content.** The
   heading is the final row of the used range. Any actual diagram is an embedded
   image or drawing object, which the cell reader cannot access. The state
   machine behind `CMD_STO` / `CMD_CHA` / `CMD_DCH` / `CMD_PAU` / `CMD_ERR` and
   the calibration states is therefore **not documented here** and must be read
   from the `.xlsx` directly.
5. `Set_Cmd` cell `A4` and `Broad_Cmd` cell `A4` both contain the label
   `Protocol:` with an empty cell to the right. Nothing was lost; the label
   simply introduces the byte map below it.

### Internal inconsistencies in the source

6. **The STATE table reuses `CMD_` prefixes for states.** In `Set_Cmd` rows
   46-55, the "State" column is populated with names like `CMD_STO`, `CMD_CHA`,
   `CMD_PAU` - i.e. command identifiers used as state identifiers. Rows 34-42 use
   the same names for the actual commands. If these are emitted as C enums under
   one namespace they will collide. The two tables are semantically different
   (one is Master -> Slave, one is Slave -> Master) and want distinct names.
7. **Op-code `0x04` means two different things depending on direction.** In the
   Command table `0x04` is `CMD_RST_ERR` ("Reset the last error from the
   device"). In the State table `0x04` is `CMD_ERR` ("The slave node is in an
   error state"). Same numeric value, opposite meaning, and the *names differ*
   while every other row of the two tables shares a name. This is the single most
   likely place to introduce a decode bug.
8. **`RST_ALL_PARA` (`0x07`) and `RST_DEF_PARA` (`0x08`) appear in the STATE
   table as well as the Command table.** As states their descriptions read
   "Reset all EEPROM parameters to default **successfully**" and "...to default
   **successful**", so they are being used as *acknowledgement* states rather
   than steady operating states. That overloading is not explained anywhere in
   the sheet. ⚠ unclear: whether the Slave latches these states or reports them
   for a single poll cycle.
9. **Grammar/typos preserved verbatim** rather than corrected, per the fidelity
   requirement: "All Channels must is in IDLE Mode." (rows 41 and 54), "The
   device  has to be in..." (double space, rows 34, 37, 47, 50), "Reset the
   selected EEPROM parameter to default successful." (row 55).
10. **Trailing whitespace in cell values.** Several command/state name cells in
    `Set_Cmd` carry trailing spaces in the workbook (`"CMD_STO     "`,
    `"CMD_CHA      "`, `"CMD_DCH "`, `"CMD_PAU "`), as do the `Remarks ` headers
    on both parameter sheets, `"SETPOINT TIMEOUT "` (EEP para 40), `"Vdc Cap "`
    (INT para 8), and `"BATT TEMP MAX "` (EEP para 33). Names are rendered
    trimmed in this document because trailing spaces are invisible in Markdown;
    do **not** treat the trailing space as significant.
11. `EEP_Para_List` para 39 (`Diff between Vcap and Vsense`) has a two-line
    remark; the line break is preserved as `<br>` in the table cell.

### Corrections applied after first transcription

20. **`READ_VALUES` is Master-issued, not Slave-only** — corrected 2026-08-14 by the developer.
    The first pass of this document read the `Master`/`Slave` row labels at `Set_Cmd!A7`/`A8` as
    an exhaustive statement of who transmits which function code, and concluded there was no
    separate read command. That was wrong. Three things in the workbook contradict it: the sheet
    title says `SET/GET_VALUES`; the Normal Mode rule requires the **Query** to carry the
    `INT Parameter number`; and a read-only poll is otherwise impossible without re-commanding the
    power stage. See the clarification block under [Functions](#functions).

### Interpretation choices made

12. **Block 2 byte offsets restart at 0.** The sheet repeats `Byte0`..`Byte63`
    for the Channel 5-8 block. This document reads that as a *second, separate
    64-byte CAN-FD frame* rather than a 128-byte continuation, because CAN-FD
    caps at 64 bytes and the workbook states `DLC = 0x0F` (64 bytes). This is an
    interpretation, though a well-supported one.
13. **CANID cell notation `Dev#,1` / `Dev#,2` / `Dev#,3`** is read as
    "device (circuit) number, function number", matching the
    `yyyyyyzzzzz` identifier layout and the SET_VALUES=1 / READ_VALUES=2 /
    BROADCAST=3 function table on the `CAN Message` sheet. The workbook never
    spells the mapping out on the `Set_Cmd` sheet itself.
14. **`Channel #` at slot offset `+9` is redundant with slot position** - the
    slot already determines the channel. The sheet does not say whether the
    receiver must validate this field, whether it is 1-based (`0x01`..`0x08` per
    the Channel Number table) and therefore mismatched against the 0-based slot
    index, or whether it may be used to remap channels into arbitrary slots.
    ⚠ unclear - confirm before implementing.
15. **How a Master addresses one channel versus all four in a frame** is not
    stated. Every slot has its own `Command` byte, so presumably an unaddressed
    slot needs a no-op value - but there is no defined "no change" command
    (`0x00` is `CMD_STO`, an active stop). ⚠ unclear - this matters.
16. Header merges are noted inline where they span columns (`A2:J2`, `B28:C28`,
    `C5:R5`, `S5:AH5`, `AJ5:AY5`, `E33:J33`, `A18:J20`, `A22:J24`, `A59:E59`,
    `B61:G61`, `B62:G62`, `B63:G63`, `A9:N9` on `Broad_Cmd`, etc.). Merged cells
    were reported by the reader as the same value repeated across the span; the
    spans above are inferred from where that repetition starts and stops.
17. `Broad_Cmd` declares no Slave response row. Combined with the `CAN Message`
    polling rule that "each Master message must be answered by the Slave", it is
    ⚠ unclear whether BROADCAST is exempt from the answer requirement (most
    likely, since a broadcast would collide with N simultaneous replies) or
    whether the response is simply undocumented in v1.0.
18. `Broad_Cmd` used range extends to column `AI` but columns `O`..`AI` are
    entirely empty across all rows - residual formatting, no lost content.
19. The `CAN Message` sheet content in this document was supplied pre-captured
    with the transcription task and was not re-read from the workbook. It is
    reproduced as given.
