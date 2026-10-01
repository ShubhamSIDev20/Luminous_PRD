# Program Packet V0.12 - BTS Program-Step Protocol Reference

| Field | Value |
|---|---|
| Document title | Program Packet - BTS Program-Step Protocol |
| Source file | `C:\Users\NNEVREKAR\OneDrive - Ador Powertron Ltd\Documents\Projects\BTS\Task\BM Documents\Program Packet\Program Packet V0.12.xlsx` |
| Version | V0.12 |
| Transcribed | 2026-08-14 |

> **This document is a transcription of the source `.xlsx` workbook.** It is a
> readable, implementer-friendly rendering of the spreadsheet. **The `.xlsx`
> file remains the authoritative source.** Where this document and the
> workbook disagree, the workbook wins.
>
> This transcription additionally cross-checks V0.12 against two other
> sources:
> 1. **`stepData.c` / `stepData.h`** (old STM32 secondary firmware) - the
>    **authoritative parser implementation**. Where the spreadsheet and the
>    firmware disagree, the firmware is ground truth for what actually gets
>    decoded on hardware.
> 2. **`Program Packet V0.7.xlsx`** - a known-**stale** earlier revision of
>    this same spreadsheet, previously proven wrong in six specific ways
>    relative to the firmware (see
>    [Cross-check vs V0.7 errata](#cross-check-vs-v07-errata)).
>
> Everything ambiguous, interpreted, or internally inconsistent in the V0.12
> workbook itself is called out with `⚠ unclear:` inline and summarized in
> [Transcription Notes](#transcription-notes).

Sheets transcribed, in the order given: `Reference Program Steps`,
`Sample Packets`, `Op-Codes`, `TABLE Sample Packet`.

---

## Reference Program Steps

Sheet used range: `A4:L20`.

This sheet is a **human-readable** program made of 11 steps. It uses plain
values ("10.0 A", "14.4 V") rather than raw bytes - the byte-level encoding of
this exact program is what the `Sample Packets` sheet (next section) provides.

Header row (row 4): `NEW` | `Step` | `Label` | `Operator` | `Param Name` |
`Nominal Value` | `Cutoff Conditiuon` *(sic, verbatim typo in the workbook)* |
`Logic` | `Limit Value` | `Action` | `Registration`.

| Step | Label | Operator | Param Name | Nominal Value | Cutoff Condition | Logic | Limit Value | Action | Registration |
|---|---|---|---|---|---|---|---|---|---|
| 1 | | CCCV Chg | Current | 10.0 A | Current | `<` | 0.5A | | 10.0 min |
| | | | Voltage | 14.4 V | Time | `>=` | 10.0 h | ERR 7 | 0.2 A |
| | | | | | Charge Capacity | `>` | 100 Ah | INT | |
| | | | | | Temperature | `>` | 40.0 C | GOTO COOL | |
| 2 | Charge | CC Chg | Current | 1A | Voltage | `>` | 14.1V | | |
| 3 | | CV Chg | Voltage | 14.1V | Current | `<` | 1A | | 0.1 A |
| 4 | COOL | PAU | | | | | 1h | GOTO Charge | 0.1 A |
| 5 | | SET | | | | | | | STANDARD |
| 6 | | STO | | | | | | | |
| 7 | | CC DChg | Current | 1.0 A | Voltage | `<` | 10.5 V | | 0.1 V |
| 8 | | GOTO | | | COOL *(value, in `G8`)* | | | | |
| 9 | | REG | | | | | | | SPECIAL |
| 10 | | REG | | | FILE_2 *(value, in `G17`)* | | | | SPECIAL |
| 11 | | REG | | | FILE_3 *(value, in `G18`)* | | | | |

Notes on this sheet as transcribed:

- Every row is prefixed `NEW` in column B for all 11 populated data rows
  (`B5:B18`); this looks like a workbook change-tracking marker, not part of
  the protocol.
- Step 1 occupies 4 rows (its 4 cutoff conditions, each with its own
  condition/logic/limit/action, and two of the four rows also carry a
  `Registration` entry - `10.0 min` on the first row, `0.2 A` on the second).
  This "one row per condition" layout is a spreadsheet presentation choice;
  the wire format packs it differently (see below).
- Step 8's `GOTO` operator carries its destination step as a **label**
  (`COOL`) here, not a byte value - resolved to step number `4` (COOL = step
  4) in the `Sample Packets` sheet.
- Step 10's `Param Name`-column cell (`G17`) holds `FILE_2` and step 11's
  (`G18`) holds `FILE_3` - these read as **registration-group / file
  identifiers** for the `REG` operator, distinct from the `Registration`
  column's `SPECIAL` marker. ⚠ unclear: the sheet does not define what
  `FILE_2`/`FILE_3` select.
- Rows `19`-`20` are empty (end of used range).

---

## Sample Packets

Sheet used range: `A2:BK72`, read in two pages (`A2:BK64`, `A65:BK72`).

This sheet gives the **byte-level wire encoding** of the same 11-step program
as `Reference Program Steps`, one packet per step (step 9's packet is
captioned `9/10`, i.e. it is presented as the shared template for both step 9
and step 10, since both are generic `REG` frames). Every packet is delimited
`AA 55 ... 55 AA` (2-byte START sentinel, 2-byte END sentinel).

### Structural finding: the "Length / Offset" field is a running cumulative offset, not a per-step length

The 4-byte field labelled `Length / Offset (4 Bytes)` does **not** hold the
byte length of its own step. It holds the **cumulative byte offset of the end
of this step within the whole concatenated program stream** (equivalently,
the start offset of the next step). Evidence, using the byte-offset rows
printed under each sample:

| Step | Declared `Length/Offset` | Step's own byte count (from offset row) | Running total check |
|---|---|---|---|
| 1 | `62` | 62 (offsets `0`-`61`) | `0 + 62 = 62` ✓ |
| 2 | `86` | 24 (offsets `62`-`85`) | `62 + 24 = 86` ✓ |
| 3 | `115` | 29 (offsets `86`-`114`) | `86 + 29 = 115` ✓ |
| 4 | `139` | 24 (offsets `115`-`138`) | `115 + 24 = 139` ✓ |
| 5 (SET) | `153` | 14 (offsets `139`-`152`) | `139 + 14 = 153` ✓ |
| 6 (STO) | `164` | 11 (offsets `153`-`163`) | `153 + 11 = 164` ✓ |
| 7 | `193` | 29 (offsets `164`-`192`) | `164 + 29 = 193` ✓ |
| 8 (GOTO) | `206` | 13 (offsets `193`-`205`) | `193 + 13 = 206` ✓ |
| 9/10 (REG) | `219` | 13 (offsets `206`-`218`) | `206 + 13 = 219` ✓ |
| 11 (REG) | `0xFFFFFFFF` (`NA`) | 12 (offsets `219`-`230`) | not cumulative - placeholder |

Every value is exactly consistent with "cumulative end-offset" and
inconsistent with "this step's length" (e.g. step 2 is 24 bytes long but
declares `86`). ⚠ unclear: whether firmware (`stepData.c`) actually reads
this field as an absolute cumulative offset, a next-step pointer, or ignores
it and relies on frame boundaries (`AA 55` / `55 AA`) alone - the workbook
does not say. Sample 11 sets the field to all `0xFF` with annotation `NA`,
suggesting this field is sometimes a "not computed" placeholder rather than a
value the receiver depends on.

### Per-channel byte layout, sample by sample

Each entry below gives the raw byte sequence then an offset-annotated field
breakdown. Offsets are local to the step (start = `0` at the first `AA`).

#### Step 1 - `CCCV Chg`, 4 cutoff conditions (offsets 0-61, 62 bytes)

```
AA 55 00 00 00 3E 00 01 04 41 20 00 00 41 66 66 66 04 31 52 3F 00 00 00
00 39 53 02 25 51 00 10 07 34 51 42 C8 00 00 0E 38 31 42 22 00 00 09 00
04 02 29 00 09 27 C0 21 3E 4C CC CD 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 0-1 | `AA 55` | START | Start sequence |
| 2-5 | `00 00 00 3E` | Length/Offset | `62` (cumulative) |
| 6-7 | `00 01` | Step No | `1` |
| 8 | `04` | Operator | `CCCV Chg` (`0x04`) |
| 9-12 | `41 20 00 00` | 1st Param Value (Current) | float32 BE = `10.0` A |
| 13-16 | `41 66 66 66` | 2nd Param Value (Voltage) | float32 BE ≈ `14.4` V |
| 17 | `04` | No. of Cutoff Conditions | `4` |
| 18 | `31` | Cutoff Condition #1 | `Current` (`0x31`) |
| 19 | `52` | Logic #1 | `<` (`0x52`) |
| 20-23 | `3F 00 00 00` | Limit Value (Current) | float32 BE = `0.5` A |
| 24 | `00` | Action Type #1 | `Blank` (`0x00`) |
| 25 | `39` | Cutoff Condition #2 | `Time` (`0x39`) |
| 26 | `53` | Logic #2 | `>=` (`0x53`) |
| 27-30 | `02 25 51 00` | Limit Value (Time, ms) | int32 = `36,000,000` ms = `10.0` h |
| 31 | `10` | Action Type #2 | `ERR` (`0x10`) |
| 32 | `07` | Action Value (ERR NO) | `7` |
| 33 | `34` | Cutoff Condition #3 | `Charge Capacity` (`0x34`) |
| 34 | `51` | Logic #3 | `>` (`0x51`) |
| 35-38 | `42 C8 00 00` | Limit Value (Charge Capacity) | float32 BE = `100.0` Ah |
| 39 | `0E` | Action Type #3 | `INT` (`0x0E`) |
| 40 | `38` | Cutoff Condition #4 | `Temperature` (`0x38`) |
| 41 | `31` | Logic #4 | `0x31` - see ⚠ below |
| 42-45 | `42 22 00 00` | Limit Value (Temperature) | float32 BE = `40.5` C |
| 46 | `09` | Action Type #4 | `GOTO` (`0x09`) |
| 47-48 | `00 04` | Action Value (2 bytes) | destination step `4` (COOL) |
| 49 | `02` | No. of Registration parameters | `2` |
| 50 | `29` | Registration Type #1 | `0x29` - see ⚠ below |
| 51-54 | `00 09 27 C0` | Registration Value #1 (ms) | int32 = `600,000` ms = `10.0` min |
| 55 | `21` | Registration Type #2 | `0x21` - see ⚠ below |
| 56-59 | `3E 4C CC CD` | Registration Value #2 | float32 BE ≈ `0.2` A |
| 60-61 | `55 AA` | END | End sequence |

> ⚠ **Internal inconsistency (offset 41, Logic #4):** the annotation row and
> `Reference Program Steps` both say this condition is `Temperature > 40.0/40.5
> C`, i.e. Logic should be `>` = `0x51`. The raw byte is `0x31`, which is not a
> valid Logic code at all - `0x31` is the *Cutoff Condition* code for
> `Current`. This looks like a copy/paste slip in the sample, not a firmware
> ambiguity. Flagged, not corrected.
>
> ⚠ **Internal inconsistency (offsets 50 and 55, Registration Type bytes):**
> the sheet's own annotation labels registration #1 as `Time` (value
> `600,000 ms` = 10 min, matching `Reference Program Steps` step 1's `10.0
> min`) and registration #2 as `Current` (value `0.2` A, matching `0.2 A`).
> Per the Op-Codes registration table (below), `Time = 0x21` and `Current =
> 0x22`. The raw bytes are `0x29` (`Step Capacity`) for #1 and `0x21`
> (`Time`) for #2 - neither matches its own annotation or the intended
> physical quantity. This is a defect in the sample packet's construction,
> not evidence about firmware correctness one way or the other.

#### Step 2 - `CC Chg` (offsets 62-85, 24 bytes)

```
AA 55 00 00 00 56 00 02 01 3F 80 00 00 01 32 51 41 61 99 9A 00 00 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 0-1 | `AA 55` | START | |
| 2-5 | `00 00 00 56` | Length/Offset | `86` (cumulative) |
| 6-7 | `00 02` | Step No | `2` |
| 8 | `01` | Operator | `CC Chg` (`0x01`) |
| 9-12 | `3F 80 00 00` | Param Value (Current) | float32 BE = `1.0` A |
| 13 | `01` | No. of Cutoff Conditions | `1` |
| 14 | `32` | Cutoff Condition | `Voltage` (`0x32`) |
| 15 | `51` | Logic | `>` (`0x51`) |
| 16-19 | `41 61 99 9A` | Limit Value (Voltage) | float32 BE ≈ `14.1` V |
| 20 | `00` | Action Type | `Blank` |
| 21 | `00` | No. of Registration parameters | `0` |
| 22-23 | `55 AA` | END | |

#### Step 3 - `CV Chg` (offsets 86-114, 29 bytes)

```
AA 55 00 00 00 73 00 03 02 41 61 99 9A 01 31 52 3F 80 00 00 00 01 21 3D
CC CC CD 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 73` | Length/Offset | `115` (cumulative) |
| 6-7 | `00 03` | Step No | `3` |
| 8 | `02` | Operator | `CV Chg` (`0x02`) |
| 9-12 | `41 61 99 9A` | Param Value (Voltage) | float32 BE ≈ `14.1` V |
| 13 | `01` | No. of Cutoff Conditions | `1` |
| 14 | `31` | Cutoff Condition | `Current` (`0x31`) |
| 15 | `52` | Logic | `<` (`0x52`) |
| 16-19 | `3F 80 00 00` | Limit Value (Current) | float32 BE = `1.0` A |
| 20 | `00` | Action Type | `Blank` |
| 21 | `01` | No. of Registration parameters | `1` |
| 22 | `21` | Registration Type | `0x21` = `Time` per Op-Codes table - see ⚠ below |
| 23-26 | `3D CC CC CD` | Registration Value | float32 BE ≈ `0.1` |
| 27-28 | `55 AA` | END | |

> ⚠ **Internal inconsistency:** annotation labels this registration entry
> `(Current)` with unit `0.1 A ~ 100mA`, matching `Reference Program Steps`
> step 3's `Registration = 0.1 A`. But the raw Registration Type byte is
> `0x21`, which the Op-Codes table defines as `Time`, not `Current`
> (`Current = 0x22`). Same class of error as Step 1's registration bytes.

#### Step 4 - `PAU` / `COOL` with `GOTO` action (offsets 115-138, 24 bytes)

```
AA 55 00 00 00 8B 00 04 08 00 36 EE 80 09 00 02 01 21 3D CC CC CD 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 8B` | Length/Offset | `139` (cumulative) |
| 6-7 | `00 04` | Step No | `4` (`COOL`) |
| 8 | `08` | Operator | `PAU` (`0x08`) |
| 9-12 | `00 36 EE 80` | Limit Value (Time, ms) | int32 = `3,600,000` ms = `1` h |
| 13 | `09` | Action Type | `GOTO` (`0x09`) |
| 14-15 | `00 02` | Action Value (2 bytes) | destination step `2` |
| 16 | `01` | No. of Registration parameters | `1` |
| 17 | `21` | Registration Type | `0x21` = `Time` - see ⚠ below |
| 18-21 | `3D CC CC CD` | Registration Value | float32 BE ≈ `0.1` |
| 22-23 | `55 AA` | END | |

> ⚠ **Internal inconsistency:** annotation labels the registration entry
> `(Current)`, matching `Reference Program Steps` step 4's `Registration = 0.1
> A`, but the raw byte is `0x21` = `Time`, not `0x22` = `Current`. Same
> pattern as steps 1 and 3.
>
> This sample is the clean confirmation that the **GOTO Action Value is 2
> bytes** (`00 02`), not 1 byte.

#### Step 5 - `SET` (offsets 139-152, 14 bytes)

```
AA 55 00 00 00 99 00 05 0A 00 01 FF 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 99` | Length/Offset | `153` (cumulative) |
| 6-7 | `00 05` | Step No | `5` |
| 8 | `0A` | Operator | `SET` (`0x0A`) |
| 9 | `00` | No. of Global Limit Parameters | `0` |
| 10-11 | `01 FF` | Registration OpCode (2 bytes) | `0x01FF` ("User Defined" example) |
| 12-13 | `55 AA` | END | |

This is the clean confirmation that **SET's registration/channel-select field
is 2 bytes** (`01 FF`), not 1 byte - directly resolving V0.7 errata item 2.

#### Step 6 - `STO` (offsets 153-163, 11 bytes)

```
AA 55 00 00 00 A4 00 06 0B 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 A4` | Length/Offset | `164` (cumulative) |
| 6-7 | `00 06` | Step No | `6` |
| 8 | `0B` | Operator | `STO` (`0x0B`) |
| 9-10 | `55 AA` | END | |

`STO` carries no parameters at all - operator byte followed immediately by
END.

#### Step 7 - `CC DChg` (offsets 164-192, 29 bytes)

```
AA 55 00 00 00 C1 00 07 05 3F 80 00 00 01 32 52 41 28 00 00 00 01 22 3D
CC CC CD 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 C1` | Length/Offset | `193` (cumulative) |
| 6-7 | `00 07` | Step No | `7` |
| 8 | `05` | Operator | `CC DChg` (`0x05`) |
| 9-12 | `3F 80 00 00` | Param Value (Current) | float32 BE = `1.0` A |
| 13 | `01` | No. of Cutoff Conditions | `1` |
| 14 | `32` | Cutoff Condition | `Voltage` (`0x32`) |
| 15 | `52` | Logic | `<` (`0x52`) |
| 16-19 | `41 28 00 00` | Limit Value (Voltage) | float32 BE = `10.5` V |
| 20 | `00` | Action Type | `Blank` |
| 21 | `01` | No. of Registration parameters | `1` |
| 22 | `22` | Registration Type | `0x22` = `Current` per Op-Codes table - see ⚠ below |
| 23-26 | `3D CC CC CD` | Registration Value | float32 BE ≈ `0.1` |
| 27-28 | `55 AA` | END | |

> ⚠ **Internal inconsistency:** annotation labels the registration entry
> `(Voltage)` with unit `0.1 V ~ 100mV`, matching `Reference Program Steps`
> step 7's `Registration = 0.1 V`. The raw byte is `0x22` = `Current` per the
> Op-Codes table, not `0x23` = `Voltage`. Same off-by-one-code pattern as
> the other registration mismatches in this sheet.

#### Step 8 - `GOTO` as a full step (offsets 193-205, 13 bytes)

```
AA 55 00 00 00 CE 00 08 09 00 04 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 CE` | Length/Offset | `206` (cumulative) |
| 6-7 | `00 08` | Step No | `8` |
| 8 | `09` | Operator | `GOTO` (`0x09`) |
| 9-10 | `00 04` | Param Value (Step Number, 2 bytes) | destination step `4` |
| 11-12 | `55 AA` | END | |

Confirms 2-byte destination-step width for `GOTO` a second way: as the
step's own operator parameter, not only as a cutoff `Action Value`.

#### Step 9/10 - `REG` (offsets 206-218, 13 bytes; shared template for both steps)

```
AA 55 00 00 00 DB 00 09 0F 02 FF 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `00 00 00 DB` | Length/Offset | `219` (cumulative) |
| 6-7 | `00 09` | Step No | `9` |
| 8 | `0F` | Operator | `REG` (`0x0F`) |
| 9-10 | `02 FF` | Registration OpCode (2 bytes) | `0x02FF` ("User Defined" example) |
| 11-12 | `55 AA` | END | |

Confirms the 2-byte `REG` OpCode width matches `SET`'s (both use the same
2-byte field name and width).

#### Step 11 - `REG` (offsets 219-230, 12 bytes; second paging range)

```
AA 55 FF FF FF FF 00 0B 0F 00 00 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `FF FF FF FF` | Length/Offset | `NA` (placeholder, all-`0xFF`) |
| 6-7 | `00 0B` | Step No | `11` |
| 8 | `0F` | Operator | `REG` (`0x0F`) |
| 9-10 | `00 00` | Registration OpCode (2 bytes) | `0x0000` |
| 11-12 | `55 AA` | END | |

---

## Op-Codes

Sheet used range: `B2:M31`. **This is the highest-value sheet in the
workbook** - it is the master opcode reference.

### Operator codes

Header (`B2:C2`): `Opearator` *(sic)* | `Op-Code (Hex)`.

| Operator | Op-Code |
|---|---|
| CC Chg | `0x01` |
| CV Chg | `0x02` |
| CP Chg | `0x03` |
| CCCV Chg | `0x04` |
| CC DChg | `0x05` |
| CP DChg | `0x06` |
| CCCV DChg | `0x07` |
| PAU | `0x08` |
| GOTO | `0x09` |
| SET | `0x0A` |
| STO | `0x0B` |
| CYC | `0x0C` |
| BEG | `0x0D` |
| INT | `0x0E` |
| REG | `0x0F` |
| ERR | `0x10` |
| MSG | `0x11` |
| TABLE | `0x12` |
| CV DChg | `0x13` |

19 operator codes, `0x01`-`0x13`, fully sequential with no gaps. `BLANK =
0x00` (the firmware's sentinel/no-op value) is **not listed** in this table -
consistent with `0x00` never appearing as a real step operator in any
sample.

### Cutoff Condition codes

Header (`E2:F2`): `Cutoff Conditions` | `Value (Hex)`.

| Cutoff Condition | Value |
|---|---|
| Current | `0x31` |
| Voltage | `0x32` |
| Power | `0x33` |
| Charge Capacity | `0x34` |
| Discharge Capacity | `0x35` |
| Charge Energy | `0x36` |
| Discharge Energy | `0x37` |
| Temperature | `0x38` |
| Time | `0x39` |

**9 codes, `0x31`-`0x39` only.** No entries for `0x3A`-`0x3D`
(Accumulated Capacity, Step Capacity, Accumulated Energy, Step Energy) appear
anywhere in this sheet.

### Logic codes

Header (`H2:I2`): `Logic` | `Value (Hex)`.

| Logic | Value |
|---|---|
| `>` | `0x51` |
| `<` | `0x52` |
| `≥` | `0x53` |
| `≤` | `0x54` |
| `≠` | `0x55` |
| `=` | `0x56` |

6 codes, `0x51`-`0x56`, sequential, no gaps.

### Action Type codes

Header (`K2:M2`): `Action Type` | `Value (Hex)` | *(unlabeled remarks
column)*.

| Action Type | Value | Remark |
|---|---|---|
| Blank | `0x00` | |
| INT | `0x0E` | |
| STO | `0x0B` | |
| ERR | `0x10` | `ERR NO (1 Byte)` |
| MSG | `0x11` | `MSG NO (1 Byte)` |
| GOTO | `0x09` | `Dest. Step number (2 Bytes)` |

Transcribed in the exact row order given in the sheet (not sorted by value).
The remark column explicitly states the two variable-payload Action Types'
widths: **ERR NO is 1 byte**, **GOTO's destination-step payload is 2
bytes**.

### Registration Type codes (with bit position)

Header spans two rows, `E17:H17` and `E18:H18` (transcribed as reader
returned it - the same four header labels repeat on both rows, most likely a
vertically-merged header cell reported twice): `Registration` | `Value
(Hex)` | `Unit` | `(OpCode) Bit Position`. Data starts at row 19.

| Registration Type | Value (Hex) | Unit | Bit Position |
|---|---|---|---|
| Time | `0x21` | h,min,sec | `0` |
| Current | `0x22` | A | `1` |
| Voltage | `0x23` | V | `2` |
| Temperature | `0x24` | C | `3` |
| Power | `0x25` | W | `4` |
| Accumulated Capacity | `0x26` | Ah | `5` |
| Charge Capacity | `0x27` | AhCha | `6` |
| Discharge Capacity | `0x28` | AhDch | `7` |
| Step Capacity | `0x29` | AhStep | `8` |
| Accumulated Energy | `0x2A` | Wh | `9` |
| Charge Energy | `0x2B` | WhCha | `10` |
| Discharge Energy | `0x2C` | WhDch | `11` |
| Step Energy | `0x2D` | WhStep | `12` |

**13 codes, `0x21`-`0x2D`, fully sequential, no gaps.** The `(OpCode) Bit
Position` column (`0`-`12`, 13 positions) is new in V0.12 versus what the
V0.7 errata described, and is strong supporting evidence for a **13-bit
selection bitmask carried in a 2-byte field** - exactly the width the
`Sample Packets` sheet's `SET` and `REG` frames use for their `Registration
OpCode` field.

---

## TABLE Sample Packet

Sheet used range: `B3:BY23`, read in two column bands (`B3:AA23`,
`AB3:BY23`). This sheet has two distinct pieces of content: (1) an actual
2-step sample packet (a `TABLE` step followed by an `STO` step), and (2) a
separate explanatory mini-table (rows 16-23) documenting the `Op-code for
Values` bitmask used inside a `TABLE` row. They are transcribed separately
below because - as the cross-check reveals - their numbers do not actually
line up with each other.

### Step 1 - `TABLE` operator, 5 rows (offsets 0-75, 76 bytes)

```
AA 55 00 00 00 4C 00 01 12 00 02 02 09 00 00 01 F4 41 40 00 00 04 0F 00
36 EE 80 41 40 00 00 00 00 01 F4 41 40 00 00 03 0B 00 09 27 C0 40 A0 00
00 41 40 00 00 01 08 00 01 D4 C0 03 0D 00 00 27 10 41 20 00 00 41 60 00
00 00 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 0-1 | `AA 55` | START | |
| 2-5 | `00 00 00 4C` | Length/Offset | `76` (cumulative) |
| 6-7 | `00 01` | Step No | `1` |
| 8 | `12` | Operator | `TABLE` (`0x12`) |
| 9-10 | `00 02` | No of Rows in a TABLE (2 bytes) | `2` |
| **Row 1 of the TABLE** ||||
| 11 | `02` | No of Values in a Row | `2` |
| 12 | `09` | Op-code for Values | `0x09` = binary `1001` = Time + Voltage present |
| 13-16 | `00 00 01 F4` | Value 1 (Time, int32 ms) | `500` ms - see ⚠ below |
| 17-20 | `41 40 00 00` | Value 2 (Voltage, float32 BE) | `12.0` V |
| **Row 2 of the TABLE** ||||
| 21 | `04` | No of Values in a Row | `4` |
| 22 | `0F` | Op-code for Values | `0x0F` = binary `1111` = Time + A + W + V all present |
| 23-26 | `00 36 EE 80` | Value 1 (Time, int32 ms) | `3,600,000` ms = `1` h |
| 27-30 | `41 40 00 00` | Value 2 (Current) | float32 BE = `12.0` A |
| 31-34 | `00 00 01 F4` | Value 3 (Power) | raw = `500` (int) / not a valid float for `100` - see ⚠ below |
| 35-38 | `41 40 00 00` | Value 4 (Voltage) | float32 BE = `12.0` V - see ⚠ below (annotation says `15` V) |
| **Row 3 of the TABLE** ||||
| 39 | `03` | No of Values in a Row | `3` |
| 40 | `0B` | Op-code for Values | `0x0B` = binary `1011` = Time + Power + Voltage (no Current) |
| 41-44 | `00 09 27 C0` | Value 1 (Time, int32 ms) | `600,000` ms = `10.0` min |
| 45-48 | `40 A0 00 00` | Value 2 (Power) | float32 BE = `5.0` W |
| 49-52 | `41 40 00 00` | Value 3 (Voltage) | float32 BE = `12.0` V |
| **Row 4 of the TABLE** ||||
| 53 | `01` | No of Values in a Row | `1` |
| 54 | `08` | Op-code for Values | `0x08` = binary `1000` = Time only |
| 55-58 | `00 01 D4 C0` | Value 1 (Time, int32 ms) | `120,000` ms = `2.0` min |
| **Row 5 of the TABLE** ||||
| 59 | `03` | No of Values in a Row | `3` |
| 60 | `0D` | Op-code for Values | `0x0D` = binary `1101` = Time + Current + Voltage (no Power) |
| 61-64 | `00 00 27 10` | Value 1 (Time, int32 ms) | `10,000` ms = `10.0` sec |
| 65-68 | `41 20 00 00` | Value 2 (Current) | float32 BE = `10.0` A |
| 69-72 | `41 60 00 00` | Value 3 (Voltage) | float32 BE = `14.0` V |
| **Trailer** ||||
| 73 | `00` | No. of Registration parameters | `0` |
| 74-75 | `55 AA` | END | |

> ⚠ **Time field encoding is confirmed as plain integer milliseconds, but
> Row 1's own value does not match its own annotation.** Rows 2, 3, 4 and 5
> all decode cleanly: `3,600,000 ms = 1 h`, `600,000 ms = 10 min`, `120,000 ms
> = 2 min`, `10,000 ms = 10 sec` - all round numbers, all matching their
> row's annotation exactly. Row 1's raw Time bytes (`00 00 01 F4` = `500`)
> would be `0.5` sec as plain ms, but the sheet's own annotation for this row
> says `10.5 sec`. `500` does not correspond to `10.5` sec under any of the
> ms/float encodings used elsewhere in this same table.
>
> ⚠ **Row 2's Power and Voltage raw bytes do not match their own
> annotations, and the annotation text exactly matches the illustrative
> legend example below (opcode `0x0F`: "1h / 12A / 100W / 15V") rather than
> a value that decodes from the actual bytes.** Power's raw bytes (`00 00 01
> F4`) are not a valid float32 for `100.0` (`100.0f = 0x42C80000`) and
> happen to be the exact same bytes as Row 1's Time value (`500`) - almost
> certainly a copy/paste leftover. Voltage's raw bytes (`41 40 00 00` =
> `12.0`) duplicate Row 2's own Current value instead of encoding `15.0`
> (`15.0f = 0x41700000`). **Conclusion: the sample's annotation text for Row
> 1 and Row 2 appears to have been copied from the legend/example table
> below rather than generated from the actual encoded bytes.** This is a
> defect in how the sample was assembled, not evidence about the firmware.

### Step 2 - `STO` (offsets 76-86, 11 bytes)

```
AA 55 FF FF FF FF 00 02 0B 55 AA
```

| Offset | Bytes | Field | Value |
|---|---|---|---|
| 2-5 | `FF FF FF FF` | Length/Offset | `NA` (placeholder) |
| 6-7 | `00 02` | Step No | `2` |
| 8 | `0B` | Operator | `STO` (`0x0B`) |
| 9-10 | `55 AA` | END | |

### `Op-code for Values` bitmask legend (rows 16-23)

This is a **separate explanatory block**, not a continuation of the packet
trace above. It documents the meaning of the `Op-code for Values` byte as a
4-bit presence mask:

Bit-weight header (row 16): `8` | `4` | `2` | `1` | | `Op-code`.
Column header (row 17): `time (int)` | `A (float)` | `W (float)` | `V
(float)` | | `Op-code`.

| Bit weight | 8 | 4 | 2 | 1 | Op-code (Hex) |
|---|---|---|---|---|---|
| Field | time (int) | A (float) | W (float) | V (float) | |

Illustrative example rows:

| time | A | W | V | Op-code | Bits set |
|---|---|---|---|---|---|
| 10.5 sec | 0 | 0 | 12 | `0x09` | `1001` = time + V |
| 1 h | 12 | 100 | 15 | `0x0F` | `1111` = time + A + W + V |
| 10 min | 0 | 5 | 12 | `0x0B` | `1011` = time + W + V |
| 2 sec | 0 | 0 | 0 | `0x08` | `1000` = time only |
| 10 sec | 10 | 0 | 14 | `0x0D` | `1101` = time + A + V |

The bit decode is internally self-consistent (each op-code's bit pattern
matches the fields marked non-blank in its row) and the bitmask meaning
(`8=time, 4=A(Current), 2=W(Power), 1=V(Voltage)`) matches the Op-code column
used by the actual packet trace above (same five op-codes `0x09, 0x0F, 0x0B,
0x08, 0x0D` appear in both). The example *values* (10.5 sec / 1h,12,100,15 /
10min,5,12 / 2sec / 10sec,10,14) are illustrative only - as shown above, some
of them were reused verbatim as annotation text in the real packet trace
without the underlying bytes being updated to match.

---

## Cross-check vs V0.7 errata

Per the task brief, `Program Packet V0.7.xlsx` was previously proven stale
against `stepData.c` in six specific ways. Verdict for each, against V0.12:

### 1. Nominal/limit values: scaled integer vs IEEE-754 float32 BE

**FIXED.** Every physical-quantity Nominal/Limit field observed in
`Sample Packets` decodes as big-endian IEEE-754 float32, matching the
firmware:

- `10.0` A → `41 20 00 00`
- `14.4` V → `41 66 66 66`
- `0.5` A → `3F 00 00 00`
- `100.0` Ah → `42 C8 00 00`
- `40.5` C → `42 22 00 00`
- `1.0` A → `3F 80 00 00`
- `14.1` V → `41 61 99 9A`
- `10.5` V → `41 28 00 00`
- `0.1` A/V → `3D CC CC CD`
- `0.2` A → `3E 4C CC CD`

No sample anywhere in the workbook encodes a physical quantity as a scaled
integer. (Note: **Time** fields are the one quantity that is genuinely
plain **int32 milliseconds**, not a float - e.g. `10.0 h → 02 25 51 00`
(36,000,000), `1 h → 00 36 EE 80` (3,600,000). This is a distinct, correctly
self-consistent design choice, not a recurrence of the V0.7 scaled-integer
bug, since it is explicitly labelled `(4 Bytes - miliseconds)` throughout and
never claims to be a float.)

### 2. SET registration type: 1 byte vs 2 bytes (13-bit channel bitmask)

**FIXED.** `Sample Packets` step 5 (`SET`) encodes its `Registration OpCode`
as an explicit 2-byte field (`01 FF`), and step 9/10/11 (`REG`) uses the
identical 2-byte field. The `Op-Codes` sheet's Registration Type table now
also carries a `(OpCode) Bit Position` column with 13 positions (`0`-`12`),
directly supporting a 13-bit bitmask carried in a 2-byte field - consistent
with the firmware's 13-bit channel-mask design.

### 3. GOTO action value: 1 byte vs 2 bytes

**FIXED.** Confirmed two independent ways:
- As a cutoff **Action Value** (step 1's 4th condition and step 4's PAU
  timeout both use `GOTO` with an explicit 2-byte `Action Value` field, e.g.
  `00 04`).
- As the **operator's own parameter** when `GOTO` is a full step (step 8:
  `09 00 04`, i.e. operator `0x09` followed by a 2-byte destination step
  `00 04`).

The `Op-Codes` sheet's Action Type table also now states outright: `GOTO |
0x09 | Dest. Step number (2 Bytes)`.

### 4. Registration type codes: only 0x21-0x29 vs full 0x21-0x2D (13 codes)

**FIXED, and exact.** The `Op-Codes` sheet's Registration Type table lists
all 13 codes `0x21`-`0x2D` with no gaps: `Time=0x21, Current=0x22,
Voltage=0x23, Temperature=0x24, Power=0x25, Accumulated Capacity=0x26,
Charge Capacity=0x27, Discharge Capacity=0x28, Step Capacity=0x29,
Accumulated Energy=0x2A, Charge Energy=0x2B, Discharge Energy=0x2C, Step
Energy=0x2D`. This is a byte-for-byte exact match to the firmware ground
truth given in the task brief.

### 5. Cutoff condition codes: only 0x31-0x39 vs full 0x31-0x3D (13 codes)

**STILL PRESENT - NOT FIXED.** The `Op-Codes` sheet's Cutoff Conditions
table still lists only 9 codes, `0x31`-`0x39` (Current, Voltage, Power,
Charge Capacity, Discharge Capacity, Charge Energy, Discharge Energy,
Temperature, Time). There is **no entry anywhere in the workbook** for
`0x3A` (Accumulated Capacity / `ACCU_CAP`), `0x3B` (Step Capacity /
`STEP_CAP`), `0x3C` (Accumulated Energy / `ACCU_ENERGY`), or `0x3D` (Step
Energy / `STEP_ENERGY`) as **cutoff condition** codes, even though the
*Registration Type* table (a different opcode namespace) now fully covers the
equivalent concepts at `0x26`/`0x29`/`0x2A`/`0x2D`. This asymmetry - fixed
for Registration, not fixed for Cutoff Condition - is the single most
actionable remaining gap in V0.12.

### 6. Missing operator CV_DChg = 0x13

**FIXED.** The `Op-Codes` sheet's Operator table now lists `CV DChg = 0x13`
as the 19th and final operator, immediately after `TABLE = 0x12`, exactly
matching the firmware.

**Summary: 4 of 6 known V0.7 defects are fixed in V0.12 (items 1, 2, 3, 4, 6).
One (item 5, cutoff condition codes 0x3A-0x3D) is still missing.**

---

## Cross-check vs firmware (stepData.c)

Comparing every opcode transcribed above against the firmware ground truth
given in the task brief.

### Operators (firmware: `BLANK=0x00` ... `CV_DChg=0x13`, 20 values including BLANK)

| Code | Firmware name | V0.12 sheet | Match |
|---|---|---|---|
| `0x00` | BLANK | *(not listed)* | Not present in sheet - consistent with never appearing as a real step |
| `0x01` | CC_Chg | CC Chg | Match |
| `0x02` | CV_Chg | CV Chg | Match |
| `0x03` | CP_Chg | CP Chg | Match |
| `0x04` | CCCV_Chg | CCCV Chg | Match |
| `0x05` | CC_DChg | CC DChg | Match |
| `0x06` | CP_DChg | CP DChg | Match |
| `0x07` | CCCV_DChg | CCCV DChg | Match |
| `0x08` | PAU | PAU | Match |
| `0x09` | GOTO | GOTO | Match |
| `0x0A` | SET_Operator | SET | Match (name shortened, value matches) |
| `0x0B` | STO | STO | Match |
| `0x0C` | CYC | CYC | Match |
| `0x0D` | BEG | BEG | Match |
| `0x0E` | INT | INT | Match |
| `0x0F` | REG | REG | Match |
| `0x10` | ERR | ERR | Match |
| `0x11` | MSG | MSG | Match |
| `0x12` | TABLE | TABLE | Match |
| `0x13` | CV_DChg | CV DChg | Match |

**19/19 non-BLANK operator codes match exactly.**

### Cutoff condition codes (firmware: 13 codes, `0x31`-`0x3D`)

| Code | Firmware name | V0.12 sheet | Match |
|---|---|---|---|
| `0x31` | CURRENT | Current | Match |
| `0x32` | VOLTAGE | Voltage | Match |
| `0x33` | POWER | Power | Match |
| `0x34` | CHARGE_CAP | Charge Capacity | Match |
| `0x35` | DISCHARGE_CAP | Discharge Capacity | Match |
| `0x36` | CHARGE_ENERGY | Charge Energy | Match |
| `0x37` | DISCHARGE_ENERGY | Discharge Energy | Match |
| `0x38` | TEMPERATURE | Temperature | Match |
| `0x39` | TIME | Time | Match |
| `0x3A` | ACCU_CAP | **absent from sheet** | Mismatch - firmware value not present |
| `0x3B` | STEP_CAP | **absent from sheet** | Mismatch - firmware value not present |
| `0x3C` | ACCU_ENERGY | **absent from sheet** | Mismatch - firmware value not present |
| `0x3D` | STEP_ENERGY | **absent from sheet** | Mismatch - firmware value not present |

**9/13 match; 4 firmware-defined codes have no corresponding sheet entry.**

### Comparator/logic codes (firmware: 6 codes, `0x51`-`0x56`)

| Code | Firmware name | V0.12 sheet | Match |
|---|---|---|---|
| `0x51` | GREATER_THAN | `>` | Match |
| `0x52` | LESS_THAN | `<` | Match |
| `0x53` | GREATER_THAN_EQUAL | `≥` | Match |
| `0x54` | LESS_THAN_EQUAL | `≤` | Match |
| `0x55` | NOT_EQUAL | `≠` | Match |
| `0x56` | EQUAL | `=` | Match |

**6/6 match exactly.**

### Registration type codes (firmware: 13 codes, `0x21`-`0x2D`)

| Code | Firmware name | V0.12 sheet | Match |
|---|---|---|---|
| `0x21` | TIME | Time | Match |
| `0x22` | CURRENT | Current | Match |
| `0x23` | VOLTAGE | Voltage | Match |
| `0x24` | TEMPERATURE | Temperature | Match |
| `0x25` | POWER | Power | Match |
| `0x26` | ACCU_CAP | Accumulated Capacity | Match |
| `0x27` | CHARGE_CAP | Charge Capacity | Match |
| `0x28` | DISCHARGE_CAP | Discharge Capacity | Match |
| `0x29` | STEP_CAP | Step Capacity | Match |
| `0x2A` | ACCU_ENERGY | Accumulated Energy | Match |
| `0x2B` | CHARGE_ENERGY | Charge Energy | Match |
| `0x2C` | DISCHARGE_ENERGY | Discharge Energy | Match |
| `0x2D` | STEP_ENERGY | Step Energy | Match |

**13/13 match exactly.**

### Overall firmware-alignment verdict

Of the four opcode families the firmware defines, **three are now byte-for-
byte exact matches** (Operators, Logic, Registration Types). **One family
(Cutoff Conditions) is still incomplete** - missing the same four codes
(`0x3A`-`0x3D`) that V0.7 was already missing. The four field-width/encoding
defects (float32 encoding, SET/REG registration width, GOTO action width,
and the missing CV_DChg operator) that V0.7 got wrong are all now correct
and verified against real sample bytes, not just against the opcode table.

**Separately from opcode-table completeness**, the `Sample Packets` and
`TABLE Sample Packet` sheets contain **several internal Registration-Type
byte errors within the samples themselves** (steps 1, 3, 4, 7 - see the ⚠
notes under each sample above) where the raw hex byte does not match either
the sample's own plain-language annotation or the value that Op-Codes says
that annotation should carry. These are defects in how the illustrative
samples were hand-built, not evidence that the opcode *definitions* are
wrong - the opcode tables themselves are internally consistent and (for 3 of
4 families) match the firmware exactly.

---

## Transcription Notes

### Ambiguous or interpreted content

1. **"Length / Offset" is a cumulative end-offset, not a per-step length**
   (see the dedicated subsection under [Sample Packets](#sample-packets)).
   This is an interpretation derived from arithmetic across all 10 sample
   steps, not a statement the workbook makes explicitly anywhere. ⚠ unclear
   whether firmware actually consumes this field for anything, or only
   relies on the `AA 55` / `55 AA` frame sentinels.
2. **Step 9's sample is explicitly captioned `9/10`** in `Sample Packets!A61`,
   i.e. one byte-identical template is presented as covering both step 9 and
   step 10 of the reference program (both are generic `REG` steps with
   "User Defined" payload). This is the sheet's own choice, not an inference
   made here.
3. **`Op-Codes!E17:H17` and `E18:H18` both contain the identical header text**
   (`Registration | Value (Hex) | Unit | (OpCode) Bit Position`). This reads
   as a vertically merged cell that the reader tool reported twice rather
   than two distinct header rows; transcribed as a single two-row header
   block.
4. **Reference Program Steps `G17`/`G18` (`FILE_2`/`FILE_3`)** for steps 10
   and 11's `REG` operator: ⚠ unclear what these select - no legend for
   "FILE_2"/"FILE_3" appears anywhere in the four sheets read.

### Internal inconsistencies in the source (not corrected, flagged only)

5. **Step 1's Logic byte for its 4th cutoff condition is invalid** (`0x31`
   where `0x51` for `>` was clearly intended - see the Step 1 breakdown).
6. **Multiple samples' Registration Type bytes do not match either their own
   annotation or the intended physical quantity**: Step 1 (`0x29` where
   `Time=0x21` was intended; `0x21` where `Current=0x22` was intended), Step
   3 (`0x21` where `Current=0x22` was intended), Step 4 (`0x21` where
   `Current=0x22` was intended), Step 7 (`0x22` where `Voltage=0x23` was
   intended). See each sample's ⚠ note above for the exact bytes.
7. **`TABLE Sample Packet` Row 1 and Row 2's annotation text appears to have
   been copy-pasted from the bitmask legend's illustrative example rows
   (rows 16-23) rather than generated from the row's own raw bytes.** Row
   1's Time value (`500` ms) doesn't match its `10.5 sec` annotation under
   any encoding used elsewhere in the sheet; Row 2's Power and Voltage raw
   bytes don't decode to the annotated `100 W` / `15 V` at all (Voltage's
   bytes are an exact duplicate of that same row's Current value). Rows 3,
   4 and 5 of the same table, by contrast, decode perfectly cleanly and
   consistently - so this looks like a two-row authoring slip, not a
   systemic sheet defect.
8. **`Reference Program Steps!H4` has a typo**: `Cutoff Conditiuon` (verbatim,
   preserved in the header transcription above).
9. **`Op-Codes!C2` has a typo**: `Opearator` (verbatim, preserved above).

### Content not present / not readable

10. No cell-embedded images, drawings, or state diagrams were encountered in
    any of the four ranges read (`Reference Program Steps A4:L20`,
    `Sample Packets A2:BK72`, `Op-Codes B2:M31`, `TABLE Sample Packet
    B3:BY23`). Unlike the `master_slave_can_v1.0.md` precedent (which hit an
    unreadable embedded state diagram), no such object was found here.
11. No scaling/resolution/min/max/unit columns exist for the byte-level
    `Sample Packets` fields beyond what is transcribed (float32 for physical
    quantities, int32 ms for time). Units shown in this document for those
    fields are read from the sheet's own annotation rows, not independently
    derived.
