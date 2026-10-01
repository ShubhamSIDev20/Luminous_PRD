# Program Step Data Encoding — notes from Program Packet V0.15

> **V0.15 (2026-09-23) — one more worked sample step, no encoding change.** Our two sample sheets
> were renamed `AhDef-PercAh Reference Steps` → **`Reference Program Steps 2`** and
> `AhDef-PercAh Sample Packets` → **`Sample Packets 2`**, and gained a battery context
> (**CNom = 100 Ah, 6 cells**) plus a new step 7 — **`CV Chg` 10.2 V ending at `0.8 CNom`, logging every
> `0.1 CNom`**. `STO` moved to step 8. **No new op-code, cutoff code, registration code or layout**:
> `CNom` never reaches the wire — the encoder resolves `0.8 × 100 Ah` to an absolute 80 Ah Step
> Capacity limit before sending (§14.10). **No firmware change.** Change record: §16.
>
> Supersedes [`program_packet_v0.14.md`](program_packet_v0.14.md).

> **V0.14 (2026-09-21) — operator op-codes renumbered at the top of the range.** `LOCKAh` moved
> **`0x14` → `0x16`**; two new operators, **`PRODUCER` `0x14`** and **`CC_RECHG` `0x15`**, took the
> slots below it; the firmware's declaration-only `IDLE` is gone. **`0x01`–`0x13` are unchanged.**
>
> `PRODUCER` and `CC_RECHG` are **allocated numbers only** — no behaviour is defined or implemented
> on any board, and the Secondary's parser **rejects** a step carrying either. Do not infer
> semantics for them from this document; there are none yet.
>
> **This is a breaking wire change for `LOCKAh`.** Anything that encodes a program step — the Web
> App above all — must move to `0x16` in the same release. Design record:
> `docs/superpowers/specs/2026-09-21-operator-opcode-renumber-design.md`. Supersedes
> [`program_packet_v0.13.md`](program_packet_v0.13.md).

> ## ⚠ Status: reference notes, NOT a normative spec
>
> The source workbook is **an explainer** — it teaches the step encoding by walking through a few
> worked sample packets. It is **not** a wire specification, and it is **not** part of the
> Primary↔Secondary query/response spec set. Nothing here is a contract.
>
> **Authority order, when anything disagrees:**
> 1. **The firmware parser** — `BTS_SEC_FW_V201/Core/Src/stepData.c` + `Core/Inc/stepData.h`. This
>    is what actually runs.
> 2. **The query/response specs** — [`ps_program_v1.8.md`](ps_program_v1.8.md),
>    [`bm_program_v3.2.md`](bm_program_v3.2.md) — for anything about framing.
> 3. This file / the workbook — for *understanding* the encoding.
>
> Use this to learn the layout. Do not adjudicate a disagreement with it, and do not lift sample
> bytes from it verbatim (see [§11](#11-inconsistencies-within-the-sample-packets)).
>
> **§14 is the one exception, and it is ours, not the workbook's.** It carries our own
> `LOCKAh` / `PERCAh` encoding, and a `CNom`-relative example, transcribed from the sheets
> `Reference Program Steps 2` and `Sample Packets 2` — written in the same sample-packet style but
> not vendor material. §1–§13 remain a transcription of the four sheets inherited unchanged from
> V0.12. Read the header of §14 before using anything in it.

**Source:** `OneDrive - Ador Powertron Ltd\Documents\Projects\BTS\Task\BM Documents\Program Packet\Program Packet V0.15.xlsx`
*(earlier versions of this file: `program_packet_v0.12.md` → `v0.13` (2026-09-18) → `v0.14` (2026-09-21) → `v0.15` (2026-09-23) — see §13, §15, §16.)*
(sheets: `Reference Program Steps`, `Sample Packets`, `Reference Program Steps 2`, `Sample Packets 2`, `Op-Codes`, `TABLE Sample Packet`)

**Transcribed:** 2026-09-16. Every op-code below was cross-checked against the firmware and matches —
so the **op-code tables (§5, §7, §8, §9, §10.2) are trustworthy**, having been confirmed against the
thing that actually runs. The *sample packets* are a different matter; several are internally
inconsistent, as illustrative examples often are.

---

## 1. What this document is, and how it relates to the others

This describes the **step-data payload** — the `AA 55 … 55 AA` blob that describes one program step.
It is the *content*; `bm_program` and `ps_program` are the *envelopes* that carry it.

| Document | Covers | Normative? |
|---|---|---|
| [`bm_program_v3.2.md`](bm_program_v3.2.md) | Web App ↔ Primary framing (`0xBB`), query IDs Q1–Q10 | yes |
| [`ps_program_v1.8.md`](ps_program_v1.8.md) | Primary ↔ Secondary framing (`0x22`), query IDs Q1–Q9 | yes |
| **this file** | the step blob carried inside both — operators, cutoffs, actions, registration, TABLE | **no — explanatory** |

A whole program is a **concatenation of these step packets** in one contiguous buffer. Each step's
`Length / Offset` field chains to the next (see §3).

---

## 2. Step packet envelope

Every step packet, regardless of operator, has this frame:

| Offset | Field | Size | Notes |
|--------|-------|------|-------|
| 0–1 | START sequence | 2 B | always `AA 55` |
| 2–5 | Length / Offset | 4 B | **absolute offset of the NEXT step packet** — see §3 |
| 6–7 | Step Number | 2 B | big-endian, 1-based |
| 8 | Operator | 1 B | see §5 |
| 9… | Operator-specific payload | var | see §6 |
| last 2 | END sequence | 2 B | always `55 AA` |

Maximum step data is **255 bytes** (enforced at protocol level by `ps_program`).

> `STEP_DATA_OPERATOR_INDEX` = 8 in firmware. The operator byte's fixed position is what lets the
> Primary and Secondary both classify a step without a full parse.

---

## 3. The `Length / Offset` field — an absolute offset, not a length ⚠

Bytes `[2..5]` hold the **byte offset of the first byte of the next step packet**, measured from the
start of the whole program buffer — *not* this step's own length.

This rule was already established from the firmware and the live-step/jump work; the workbook's
11-step sample is **corroboration, not the source of the rule**. It is consistent end-to-end, which
is worth something — the offsets chain correctly across all 11 steps:

| Step | Operator | Occupies bytes | `[2..5]` value |
|---|---|---|---|
| 1 | CCCV Chg | 0–61 | 62 (`00 00 00 3E`) |
| 2 | CC Chg | 62–85 | 86 (`00 00 00 56`) |
| 3 | CV Chg | 86–114 | 115 (`00 00 00 73`) |
| 4 | PAU | 115–138 | 139 (`00 00 00 8B`) |
| 5 | SET | 139–152 | 153 (`00 00 00 99`) |
| 6 | STO | 153–163 | 164 (`00 00 00 A4`) |
| 7 | CC DChg | 164–192 | 193 (`00 00 00 C1`) |
| 8 | GOTO | 193–205 | 206 (`00 00 00 CE`) |
| 9 | REG | 206–218 | 219 (`00 00 00 DB`) |
| 11 | REG (last) | 219–231 | **`FF FF FF FF`** |

**The last step in a program carries `FF FF FF FF`** (`TERMINATOR_INDEX`) instead of an offset.

> **Consequence for single-step payloads.** In a standalone one-step payload (`bm_program` Q9 live
> step update, and any single-step Q2), an absolute next-packet offset is meaningless. The rule
> adopted there is: `[2..5]` must equal either the payload length or `FFFFFFFF`, else `REASON 0x06`.
> That rule doubles as the "someone sent a whole multi-step buffer" detector. See `bm_program_v3.2.md`.

---

## 4. Value encodings

Two encodings, and the distinction is load-bearing:

| Kind | Encoding | Used for |
|------|----------|----------|
| **Time** | `uint32` big-endian, **milliseconds** | time cutoff limits, PAU duration, time registration interval, TABLE time column |
| **Everything else** | **IEEE-754 float32, big-endian**, in base SI units | current (A), voltage (V), power (W), capacity (Ah), energy (Wh), temperature (°C) |

Firmware enforces exactly this split — `stepData.c:108` special-cases `TIME_REGISTRATION` to parse
an integer and every other type through `convert4ByteToFloat()`.

Worked decodings from the workbook (all verified exact):

| Bytes | Decodes to | Annotated as |
|---|---|---|
| `41 20 00 00` | 10.0 | 10.0 A |
| `41 66 66 66` | 14.4 | 14.4 V |
| `3F 00 00 00` | 0.5 | 0.5 A |
| `3F 80 00 00` | 1.0 | 1.0 A |
| `41 61 99 9A` | 14.1 | 14.1 V |
| `41 28 00 00` | 10.5 | 10.5 V |
| `42 C8 00 00` | 100.0 | 100 Ah |
| `42 22 00 00` | 40.5 | 40.5 °C |
| `3D CC CC CD` | 0.1 | 0.1 A |
| `3E 4C CC CD` | 0.2 | 0.2 A |
| `02 25 51 00` | 36,000,000 | 10 h (`10:00:00:000`) |
| `00 09 27 C0` | 600,000 | 10 min (`00:10:00:000`) |
| `00 36 EE 80` | 3,600,000 | 1 h (`01:00:00:000`) |

> ⚠ The workbook annotates currents as e.g. *"(10.0 A ~ 10000mA)"*. The mA figure is **explanatory
> text only** — the wire value is a float in amps (`41 20 00 00` = 10.0), not a milliamp integer.
> Do not implement against the annotation.

---

## 5. Operator op-codes

| Operator | Op-code | Kind |
|----------|---------|------|
| `CC Chg` | `0x01` | regulating |
| `CV Chg` | `0x02` | regulating |
| `CP Chg` | `0x03` | regulating |
| `CCCV Chg` | `0x04` | regulating (2 nominal params) |
| `CC DChg` | `0x05` | regulating |
| `CP DChg` | `0x06` | regulating |
| `CCCV DChg` | `0x07` | regulating (2 nominal params) |
| `PAU` | `0x08` | pause |
| `GOTO` | `0x09` | control flow |
| `SET` | `0x0A` | registration format |
| `STO` | `0x0B` | terminate program |
| `CYC` | `0x0C` | cycle end (nominal = repetition count) |
| `BEG` | `0x0D` | cycle begin (nominal = optional cycle name) |
| `INT` | `0x0E` | interrupt / operator wait |
| `REG` | `0x0F` | registration format override |
| `ERR` | `0x10` | raise error |
| `MSG` | `0x11` | operator message |
| `TABLE` | `0x12` | table-driven step (Q4/Q5 handshake) |
| `CV DChg` | `0x13` | regulating |
| `PRODUCER` | `0x14` | **allocated only — no behaviour, rejected by the parser** |
| `CC_RECHG` | `0x15` | **allocated only — no behaviour, rejected by the parser** |
| `LOCKAh` | `0x16` | capacity reference (see §14) — **was `0x14` before 2026-09-21** |

> **The last three rows changed on 2026-09-21.** `0x01`–`0x13` did not.
>
> `PRODUCER` and `CC_RECHG` hold numbers and nothing else. The Secondary's bounds guard in
> `parseStepData()` is deliberately **not** a contiguous `<= LOCKAh` test — it admits `0x01`–`0x13`
> and `0x16`, and refuses `0x14`/`0x15`, so a program using an unimplemented operator is rejected
> cleanly instead of reaching a `switch` with no case for it. Widen that guard when either operator
> is actually implemented.
>
> `CC_RECHG` is expected to correspond to BM4's `RCH` (recharge), but that is an expectation, not a
> specification — and the BTS-600 gap analysis records that no distinct recharge relay exists on this
> hardware. See the design record before implementing it.

> `GOTO`'s **operator** op-code `0x09` and `ps_program`'s **query ID** Q9 `0x09` are unrelated values
> in different namespaces — one is a byte at step-data offset 8, the other a query-ID byte at frame
> offset 2. Coincidental, though fitting: Q9 is the external equivalent of a `GOTO`.

---

## 6. Per-operator payload layouts

All offsets below are **relative to the start of the step packet** (byte 0 = `0xAA`).

### 6.1 Regulating operators with one nominal value (`CC Chg`, `CV Chg`, `CP Chg`, `CC DChg`, `CP DChg`, `CV DChg`)

```
[0..1]  AA 55
[2..5]  next-packet offset
[6..7]  step number
[8]     operator
[9..12] nominal value            (float32)
[13]    No. of cutoff conditions (1 B)
        ... cutoff condition blocks, see §7 ...
[n]     No. of registration params (1 B)
        ... registration param blocks, see §9.1 ...
[..]    55 AA
```

### 6.2 Regulating operators with two nominal values (`CCCV Chg`, `CCCV DChg`)

Identical, except **two** 4-byte nominal values — current first, then voltage:

```
[9..12]  1st nominal value (Current, float32)
[13..16] 2nd nominal value (Voltage, float32)
[17]     No. of cutoff conditions
```

### 6.3 `PAU` (`0x08`)

`PAU` carries a **duration**, then an action, and has **no cutoff-condition count**:

```
[9..12]  Limit Value (Time)  (uint32 ms)
[13]     Action Type         (1 B)
[14..15] Action Value        (2 B — present only for GOTO/ERR/MSG, see §8)
[n]      No. of registration params
         ... registration param blocks ...
[..]     55 AA
```

### 6.4 `GOTO` (`0x09`)

```
[9..10]  Destination step number (2 B, big-endian)
[11..12] 55 AA
```

### 6.5 `SET` (`0x0A`)

```
[9]      No. of Global Limit Parameters (1 B)
[10..11] Registration OpCode bitmask (2 B) — see §9.2
[12..13] 55 AA
```

### 6.6 `REG` (`0x0F`)

⚠ **`REG` has NO leading count byte** — the bitmask follows the operator directly. Copying the `SET`
layout as a template introduces an off-by-one:

```
[9..10]  Registration OpCode bitmask (2 B) — see §9.2
[11..12] 55 AA
```

### 6.7 Bare operators (`STO` `0x0B`, `INT` `0x0E`)

No payload at all:

```
[9..10]  55 AA
```

### 6.8 `TABLE` (`0x12`)

See §10.

### 6.9 `BEG` (`0x0D`) / `CYC` (`0x0C`)

Ordinary steps identified by their operator byte. `CYC`'s nominal value is the repetition count;
`BEG`'s is an optional cycle name. The Primary derives the Q6 cycle table from these at load time;
at run time the Secondary uses that table rather than `BEG`/`CYC` traversal — which is why a `GOTO`
or a Q9 jump landing inside a cycle without passing its `BEG` still behaves correctly.

---

## 7. Cutoff condition block

After the `No. of cutoff conditions` byte, that many blocks follow. Each block is **7 or 8 bytes**:

| Offset in block | Field | Size |
|---|---|---|
| +0 | Cutoff Condition | 1 B |
| +1 | Logic | 1 B |
| +2..+5 | Limit Value | 4 B (float32, or uint32 ms if the condition is Time) |
| +6 | Action Type | 1 B |
| +7… | Action Value | 0, 1 or 2 B — **depends on Action Type**, see §8 |

### Cutoff Condition codes

| Condition | Value | | Condition | Value |
|---|---|---|---|---|
| Current | `0x31` | | Temperature | `0x38` |
| Voltage | `0x32` | | Time | `0x39` |
| Power | `0x33` | | Accumulated Capacity | `0x3A` |
| Charge Capacity | `0x34` | | Step Capacity | `0x3B` |
| Discharge Capacity | `0x35` | | Accumulated Energy | `0x3C` |
| Charge Energy | `0x36` | | Step Energy | `0x3D` |
| Discharge Energy | `0x37` | | | |

> From V0.15 the workbook's `Op-Codes` cutoff table also lists **`PERCAh (Percentage Ah)` =
> `0x3E`** (cell `E16`/`F16`). That code is ours, not vendor material — see §14.1. It was already
> implemented in firmware; the workbook row only caught up.

### Logic codes

| Logic | Value |
|---|---|
| `>` | `0x51` |
| `<` | `0x52` |
| `≥` | `0x53` |
| `≤` | `0x54` |
| `≠` | `0x55` |
| `=` | `0x56` |

---

## 8. Action types — and the variable-length trap

| Action | Value | Trailing Action Value |
|--------|-------|----------------------|
| Blank (just end the step) | `0x00` | none |
| `INT` | `0x0E` | none |
| `STO` | `0x0B` | none |
| `ERR` | `0x10` | **1 byte** — error number |
| `MSG` | `0x11` | **1 byte** — message number |
| `GOTO` | `0x09` | **2 bytes** — destination step number |

⚠ **This makes the cutoff block variable-length.** A parser must read the Action Type before it
knows how many bytes to advance. Firmware stores the trailing value in a single `uint16_t
actionTypeValue` field (`stepData.h:106`, commented *"if actionType is 0x10, 0x11, or 0x09"*).

---

## 9. Registration — two different encodings

This is the part most likely to be implemented wrongly, because the same word "registration" names
two unrelated wire formats.

### 9.1 Per-step inline registration (inside a regulating / `PAU` step)

A count, then that many `type + value` pairs. **The value is the logging interval/threshold** for
that parameter:

```
[n]      No. of registration parameters (1 B)
  per parameter:
  +0     Registration Type (1 B)   — a VALUE CODE from the table below
  +1..+4 Value             (4 B)   — uint32 ms if type is Time (0x21), else float32
```

| Registration | Value code | Unit | | Registration | Value code | Unit |
|---|---|---|---|---|---|---|
| Time | `0x21` | h,min,sec | | Charge Capacity | `0x27` | AhCha |
| Current | `0x22` | A | | Discharge Capacity | `0x28` | AhDch |
| Voltage | `0x23` | V | | Step Capacity | `0x29` | AhStep |
| Temperature | `0x24` | °C | | Accumulated Energy | `0x2A` | Wh |
| Power | `0x25` | W | | Charge Energy | `0x2B` | WhCha |
| Accumulated Capacity | `0x26` | Ah | | Discharge Energy | `0x2C` | WhDch |
| | | | | Step Energy | `0x2D` | WhStep |

### 9.2 `SET` / `REG` registration bitmask

`SET` and `REG` carry a **2-byte bitmask** instead — one bit per parameter, selecting which
parameters get logged for the rest of the program:

| Bit | Parameter | | Bit | Parameter |
|---|---|---|---|---|
| 0 | Time | | 7 | Discharge Capacity |
| 1 | Current | | 8 | Step Capacity |
| 2 | Voltage | | 9 | Accumulated Energy |
| 3 | Temperature | | 10 | Charge Energy |
| 4 | Power | | 11 | Discharge Energy |
| 5 | Accumulated Capacity | | 12 | Step Energy |
| 6 | Charge Capacity | | | |

Firmware masks with `registrationType &= 0x1FFF` (13 valid bits, 0–12) and derives
`noOfParameters` by popcount (`stepData.c:170-175`).

**The value code and the bit position are different numbering schemes for the same parameter list:**
`value code = 0x21 + bit position`. Time is code `0x21` / bit 0; Step Energy is code `0x2D` / bit 12.
Mixing them up is a silent defect — it selects the wrong parameter, and nothing rejects it.

---

## 10. `TABLE` step (`0x12`)

### 10.1 TABLE step packet

```
[0..1]   AA 55
[2..5]   next-packet offset
[6..7]   step number
[8]      0x12 (TABLE)
[9..10]  No. of rows in the TABLE (2 B)
  then per row:
  +0     No. of values in this row (1 B)
  +1     Op-code for values        (1 B)  — see §10.2
  +2…    that many 4-byte values          — order fixed by bit order, see §10.2
[n]      No. of registration parameters (1 B)
[..]     55 AA
```

Rows can differ in width from one another — each row carries its own count and op-code.

### 10.2 The TABLE row op-code is a 4-bit mask ⚠

This is the single least obvious thing in the whole document. The row op-code is not an enum of
named combinations — it is a **bitmask over four columns**:

| Bit | Weight | Column | Encoding |
|-----|--------|--------|----------|
| 3 | 8 | time | `uint32` ms |
| 2 | 4 | A (current) | float32 |
| 1 | 2 | W (power) | float32 |
| 0 | 1 | V (voltage) | float32 |

**Values appear in bit order, MSB → LSB: time, current, power, voltage** — only those whose bit is
set, and `No. of values` equals the popcount.

All eight legal op-codes (bit 3 is always set — every row has a time):

| Op-code | Bits | Columns present | Values | Firmware enum |
|---------|------|-----------------|--------|---------------|
| `0x08` | 1000 | time | 1 | `T_TIME` |
| `0x09` | 1001 | time, V | 2 | `T_TIME_VOLT` |
| `0x0A` | 1010 | time, W | 2 | `T_TIME_POWER` |
| `0x0B` | 1011 | time, W, V | 3 | `T_TIME_POWER_VOLT` |
| `0x0C` | 1100 | time, A | 2 | `T_TIME_CURRENT` |
| `0x0D` | 1101 | time, A, V | 3 | `T_TIME_CURRENT_VOLT` |
| `0x0E` | 1110 | time, A, W | 3 | `T_TIME_CURRENT_POWER` |
| `0x0F` | 1111 | time, A, W, V | 4 | `T_ALL` |

The firmware enum (`stepData.h:72`) is a plain sequential list from `0x08`, so the bitmask property
is *implicit* — it holds, but nothing in the code names it. Worth knowing when adding a column.

### 10.3 TABLE rows are fetched separately

The TABLE step packet as sent over `ps_program` Q2 carries the row **structure**; individual rows
are pulled by the Secondary with Q4 (request row) / Q5 (send row). See `ps_program_v1.8.md`.

---

## 11. Inconsistencies within the sample packets

These are places where the workbook's sample **bytes** disagree with the workbook's own
**annotations** next to them. Since this is an explainer rather than a spec, these are best read as
the ordinary looseness of hand-built examples — not as defects anyone needs to chase down, and
certainly not as something the firmware is failing to match.

**The one practical consequence: don't copy sample bytes out of this workbook into a test vector,
a doc, or a unit test without decoding them first.** That has already happened once (see the note
after the table).

| # | Location | Sample byte says | Annotation implies | Note |
|---|---|---|---|---|
| W-1 | `Sample Packets`, step 1, byte 41 | `0x31` | `0x51` (Logic `>`) | `0x31` is a *cutoff-condition* code (Current), not a logic operator. |
| W-2 | `Sample Packets`, step 1, byte 50 | `0x29` (Step Capacity) | `0x21` (Time) | |
| W-3 | `Sample Packets`, step 1, byte 55 | `0x21` (Time) | `0x22` (Current) | Same shift as W-2. |
| W-4 | `TABLE Sample Packet`, bytes 9–10 | `00 02` (2 rows) | 5 rows | The sheet then lists 5 rows. |
| W-5 | `TABLE Sample Packet`, bytes 13–16 | `00 00 01 F4` (500) | 10.5 s = 10,500 ms | |
| W-6 | `TABLE Sample Packet`, bytes 33–36 | `00 00 01 F4` (int) | 100 W (float32) | An int sitting in a float column. |
| W-7 | `TABLE Sample Packet`, bytes 35–38 | `41 40 00 00` (12.0) | 15 V | |
| W-8 | `Reference Program Steps` vs `Sample Packets` | packet encodes 40.5 °C | sheet says 40.0 °C | The two sheets differ. |
| W-9 | `Sample Packets 2`, step 7, bytes 152–155 (header `Y39:AB39`) | `41 20 00 00` — float32 10.0 | header reads *"Limit Value (Time) (4 Bytes - milliseconds)"* | The **bytes and the annotation below them are right** (10.0 Ah); only the column header is wrong. Registration type `0x29` is Step Capacity, not Time, so per §9.1 the value is a float. Read as uint32 ms it would be 1,092,616,192. |
| W-10 | `Reference Program Steps 2` vs `Sample Packets 2`, step 7 | packet encodes Logic `0x56` (`=`) | reference sheet's Logic column is blank | Not a conflict in meaning — BM writes `0.8 CNom` with no operator — but an encoder has to choose a logic byte. See §14.10 for why `=` is safe. |

> **W-5 already leaked into our own docs — this is the reason the rule above matters.**
> `ps_program_v1.8.md`'s TABLE example had reproduced `00 00 01 F4` and additionally labelled it
> *"(float)"*, so a reader got a value that is neither 10.5 s nor a float. Corrected there
> 2026-09-16.

---

## 12. Firmware cross-reference

| Concept | Secondary (authoritative parser) | Primary |
|---|---|---|
| All op-code enums | `Core/Inc/stepData.h` | — |
| Step parse | `Core/Src/stepData.c` → `parseStepData()` | — |
| Operator dispatch / execution | `btsSecApp.c` → `programStepExecution()` | — |
| Step buffer + step fetch | `btsSecUart.c` | `bts_app.c` → `fetchProgramStep()` |
| Offset / `[2..5]` rule | — | `liveStep.c`, `jumpStep.c` (validation) |
| TABLE row walk | `btsSecApp.c`, `btsSecUart.c` | Q4/Q5 in `prim_uart.c` |

**Known firmware behaviour not documented in the workbook:** a nominal value of **0** is accepted for
every constant-mode operator, and the "setpoint unreachable" error branch is deliberately skipped in
that case. The workbook does not state whether 0 is valid; the user confirmed 2026-08-04 that it is,
and the behaviour is accepted as-is.

---

## 13. Version note

**This file was `program_packet_v0.12.md` until 2026-09-18.** It was renamed when
`Program Packet V0.13.xlsx` became the current workbook, so that the document and the workbook it
describes carry the same version. Older session notes and commit messages still cite the V0.12
name; they refer to this same file.

**V0.13 is a strict superset of V0.12** — verified cell by cell on 2026-09-18:

| Sheet | V0.12 | V0.13 | Verdict |
|---|---|---|---|
| `Reference Program Steps` | 82 cells | 82 cells | identical |
| `Sample Packets` | 710 cells | 710 cells | identical |
| `TABLE Sample Packet` | 273 cells | 273 cells | identical |
| `Op-Codes` | 155 cells | 177 cells | **22 cells added**, none changed, none removed |
| `AhDef-PercAh Reference Steps` | — | new sheet | our design (§14) |
| `AhDef-PercAh Sample Packets` | — | new sheet | our design (§14) |

*(This section compares V0.12 to V0.13 and describes the workbook as it stood then — `B22`/`C22`
held `LOCKAh`/`0x14` at that point. V0.14 later moved `LOCKAh` to `C24`/`0x16` and put `PRODUCER`
in `B22`. See §15.)*

The 22 added `Op-Codes` cells are `LOCKAh` / `0x14` in the operator table (`B22`/`C22`) and the
`PERCAh` + `SET` global-parameter block in columns `O`–`R`. **Nothing V0.12 documented was altered**,
so §1–§13 describe V0.13 exactly as accurately as they described V0.12. Everything genuinely new in
V0.13 lives in §14, behind its own banner.

**Where the earlier versions went.** V0.1 → V0.12 are all still present in the source folder.
`ps_program_v1.8.md` had been pointing at **V0.6**, which was stale — but since this material is
explanatory rather than normative, a stale pointer here never put the firmware at risk; it just sent
readers to an older explainer. That pointer now resolves to this file.

**No version-tracking obligation.** Unlike the query/response Excel specs, there is no need to sweep
this folder at session start or to chase new revisions. If a V0.14 appears it is worth a read, but
the firmware and the `bm_`/`ps_` specs remain the things that must stay in sync.

---

## 14. Our samples — `LOCKAh`, `PERCAh` and `CNom` (OUR design, not vendor material)

> ## ⚠ This section is our design, not vendor material
>
> Sections 1–13 above describe the four sheets inherited unchanged from **V0.12**, which is
> vendor material. This section does not.
> It transcribes the sheets **`Reference Program Steps 2`** and **`Sample Packets 2`** of
> **Program Packet V0.15.xlsx** (named `AhDef-PercAh Reference Steps` / `AhDef-PercAh Sample
> Packets` in V0.13–V0.14) and our additions to `Op-Codes`. They hold **our own encoding**, written
> in the workbook's sample-packet style so it can be read alongside the rest.
>
> Steps 1–6 cover `LOCKAh`/`PERCAh`. Step 7, added in V0.15, is a `CNom`-relative limit and
> registration — **no new encoding**, only an example of how an existing one is used (§14.10).
>
> - **Implemented 2026-09-18, except persistence.** The Secondary parses and executes these bytes
>   (`stepData.c`, `btsSecApp.c`, `capacityLock.c`); the Primary needed no change. **Not yet
>   implemented: surviving a power-fail** — see `ps_measured_data_v6.md`, which is still design
>   only. An interrupted test currently resumes and then halts with `en_ERR_INVALID_PROG_STEP`
>   rather than charging to a wrong target. **Nothing here is HIL-tested.**
> - **Not vendor material.** Do not fold it back into §1–§13, and do not cite it as vendor content.
> - **Normative source:** `docs/superpowers/specs/2026-09-17-percah-ahdef-design.md`.
>   If this section and that spec disagree, the spec wins.
>
> Added 2026-09-17. Rewritten 2026-09-18 against the hand-authored V0.13 sheets.

### 14.1 What the feature does

A program needs to charge a battery back by a **percentage of the capacity it actually delivered**,
not by a fixed number of amp-hours. The threshold is not knowable when the program is written — it
depends on the cell under test. Two pieces implement it, and they are **not interchangeable**:

| Name | Kind | Value | Role |
|---|---|---|---|
| `LOCKAh` | **operator** `0x16` | Nominal Value — multiplier, float32. `1.0` = 100 % | Stores the present Ah counter as a 100 % reference, then ends its step immediately |
| `PERCAh` | **cutoff condition** `0x3E` | Limit Value — percent, float32. `130.0` = 130 % | Ends a step when the Ah counter reaches that percentage of the stored reference |

`LOCKAh` writes the number; `PERCAh` reads it. A program needs both. BM called the first one
`AhDef` — **that name is not used in our design.**

Where `PERCAh` may appear:

| Operator | `PERCAh` as a cutoff | Notes |
|---|---|---|
| `LOCKAh` (`0x16`) | **never** | Carries no cutoff conditions at all |
| `PAU` (`0x08`) | **never** | **Encoding completely unchanged** — see §14.12 |
| Regulating operators (`CC Chg`, `CCCV Chg`, `CC DChg`, …) | yes | Alongside the existing cutoff conditions of §7 |

Three changes to the encoding, and no others:

- **A new operator, `LOCKAh` = `0x16`** (it was `0x14` when this section was first written on
  2026-09-17; renumbered 2026-09-21 when `PRODUCER` and `CC_RECHG` took `0x14`/`0x15`). The
  firmware's declaration-only `IDLE` was deleted in the same change. The parser's range guard in
  `parseStepData()` became `(>= CC_Chg && <= CV_DChg) || == LOCKAh` — deliberately **not**
  contiguous, so the two unimplemented operators are refused rather than admitted.
- **A new cutoff condition, `PERCAh` = `0x3E`,** extending the §7 table past
  `Step Energy 0x3D`. **No packet layout change** — it is one byte value where a `0x3A` would sit.
- **`SET` starts honouring the "No. of Global Limit Parameters" byte at offset 9** — the byte §6.5
  already specifies and the firmware currently skips — so `SET Ah = 0` can be expressed. Parameter
  codes reuse the §9.1 value codes (`0x26` = Accumulated Capacity, `0x2A` = Accumulated Energy).

### 14.2 Reference Program Steps

From the `Reference Program Steps 2` sheet of V0.15.

**Battery context** (new in V0.15, sheet cells `B2:E3`): **Battery Nominal Capacity (CNom) = 100 Ah**,
**Battery No. of Cells = 6**. CNom is what step 7's limit and registration are multiplied by. The
cell count is not used by any byte in these samples.

| Step | Operator | Param Name | Nominal Value | Cutoff Condition | Logic | Limit Value | Registration |
|---|---|---|---|---|---|---|---|
| 1 | `SET` | Accumulated Capacity | Ah = 0 | | | | STANDARD |
| 2 | `CC DChg` | Current | 2.0 A | Voltage | `<` | 3.2 V | |
| 3 | `LOCKAh` | Multiplier | 1.0 | | | | |
| 4 | `SET` | Accumulated Capacity | Ah = 0 | | | | STANDARD |
| 5 | `CCCV Chg` | Current | 1.6 A | Current | `<` | 1.0 A | |
| | | Voltage | 4.1 V | | | | |
| 6 | `CC Chg` | Current | 1.0 A | `PERCAh` | `>` | 130 | |
| 7 | `CV Chg` | Voltage | 10.2 V | `CNom` | *(blank)* | 0.8 CNom | 0.1 CNom |
| 8 | `STO` | | | | | | |

*The reference sheet leaves step 4's Registration cell empty, while the packet encodes STANDARD
(`01 FF`) — unchanged since V0.13. Step 7's Logic cell is blank too; the packet uses `=` (§11 W-10).*
*Step 7 is new in V0.15; until V0.14, step 7 was the `STO`.*

Read the shape, not just the new bytes. Step 2 is what *establishes* a capacity — the reference
cannot be locked until the cell has actually been emptied — and step 3 locks whatever step 2
delivered. Step 4 then clears the counter so the charge is measured from zero.

Note also that `LOCKAh`'s multiplier sits in the **Nominal Value** column, not the Limit column.
That is the visible consequence of making the capacity lock an operator rather than a limit.

> **The Ah counter is program-global, not per-step.** It runs from one `SET Ah = 0` to the next,
> across every step in between. Step 6's `PERCAh` therefore measures the charge accumulated over
> steps 5 **and** 6 together — not step 6 alone. Anyone encoding these programs has to know this;
> assuming a per-step counter produces a program that never terminates.
>
> With a cell that gives up 8 Ah in step 2, steps 5+6 together end at 10.4 Ah.

### 14.3 Packet map

All eight packets are contiguous from offset 0, so the `Length / Offset` chain resolves end to end.

| Step | Operator | Bytes | Offsets | Length / Offset | New? |
|---|---|---|---|---|---|
| 1 | `SET` | 19 | 0–18 | `00 00 00 13` | count byte now read |
| 2 | `CC DChg` | 24 | 19–42 | `00 00 00 2B` | unchanged |
| 3 | `LOCKAh` | 15 | 43–57 | `00 00 00 3A` | **new operator** |
| 4 | `SET` | 19 | 58–76 | `00 00 00 4D` | count byte now read |
| 5 | `CCCV Chg` | 28 | 77–104 | `00 00 00 69` | unchanged |
| 6 | `CC Chg` | 24 | 105–128 | `00 00 00 81` | **new cutoff code** |
| 7 | `CV Chg` | 29 | 129–157 | `00 00 00 9E` | **added in V0.15** — existing codes only |
| 8 | `STO` | 11 | 158–168 | `FF FF FF FF` | unchanged — was step 7 at 129–139 until V0.14 |

Steps 1–6 are byte-identical to V0.14. Step 6's `Length / Offset` still reads 129; it now points
at the `CV Chg` rather than the `STO`.

### 14.4 Sample Packet — step 1, `SET Ah = 0`

19 bytes, offsets 0–18. Compare with the V0.12 `SET` sample (§6.5, 14 bytes), where the byte at
offset 9 is `0x00` and is skipped by the parser. Here it is `0x01` and one 5-byte parameter block
follows it.

```
0xAA 0x55 0x00 0x00 0x00 0x13 0x00 0x01 0x0A 0x01 0x26 0x00 0x00 0x00 0x00 0x01 0xFF 0x55 0xAA
   0    1    2    3    4    5    6    7    8    9   10   11   12   13   14   15   16   17   18
```

| Offset | Field | Size | Hex | Decoded |
|---|---|---|---|---|
| 0–1 | START | 2 B | `AA 55` | Start sequence |
| 2–5 | Length / Offset | 4 B | `00 00 00 13` | 19 — first byte of the next packet |
| 6–7 | Step No | 2 B | `00 01` | 1 |
| 8 | Operator | 1 B | `0A` | `SET` |
| 9 | No. of Global Limit Parameters | 1 B | `01` | 1 — **now read, was skipped** |
| 10 | Parameter code | 1 B | `26` | Accumulated Capacity (Ah) — **new field** |
| 11–14 | Parameter value | 4 B | `00 00 00 00` | 0.0 Ah — **new field** |
| 15–16 | Registration OpCode | 2 B | `01 FF` | 9 parameters (STANDARD) |
| 17–18 | END | 2 B | `55 AA` | Stop sequence |

With the count byte at `0x00` the packet is byte-identical to the V0.12 `SET`, so programs that use
no global parameters are unaffected.

**Run-time effect.** Sets `batAccuCapacityAh = 0.0` **and** `registration.presDetectAccuCapacity
= 0.0`. Both, not just the first — the Ah registration trigger compares against the latter, so
leaving it stale fires a spurious registration record on the next evaluation.

### 14.5 Sample Packet — step 2, `CC DChg` 2.0 A with `V < 3.2`

24 bytes, offsets 19–42. **Nothing here is new** — it is the §6.1 layout unchanged, included
because it is the step that gives the feature its meaning.

```
0xAA 0x55 0x00 0x00 0x00 0x2B 0x00 0x02 0x05 0x40 0x00 0x00 0x00 0x01 0x32 0x52 0x40 0x4C 0xCC 0xCD 0x00 0x00 0x55 0xAA
  19   20   21   22   23   24   25   26   27   28   29   30   31   32   33   34   35   36   37   38   39   40   41   42
```

| Offset | Field | Size | Hex | Decoded |
|---|---|---|---|---|
| 19–20 | START | 2 B | `AA 55` | Start sequence |
| 21–24 | Length / Offset | 4 B | `00 00 00 2B` | 43 |
| 25–26 | Step No | 2 B | `00 02` | 2 |
| 27 | Operator | 1 B | `05` | `CC DChg` |
| 28–31 | Param Value (Current) | 4 B | `40 00 00 00` | 2.0 A |
| 32 | No. of Cutoff Conditions | 1 B | `01` | 1 |
| 33 | Cutoff Condition | 1 B | `32` | Voltage |
| 34 | Logic | 1 B | `52` | `<` |
| 35–38 | Limit Value | 4 B | `40 4C CC CD` | 3.2 V |
| 39 | Action Type | 1 B | `00` | Blank — end the step |
| 40 | No. of Registration parameters | 1 B | `00` | 0 |
| 41–42 | END | 2 B | `55 AA` | Stop sequence |

### 14.6 Sample Packet — step 3, `LOCKAh` with multiplier 1.0

15 bytes, offsets 43–57. `Length / Offset` carries **58** (`0x0000003A`).

Same shape as `GOTO` (§6.4): operator, one fixed-size payload, end. **No cutoff-condition count and
no registration count** — `LOCKAh` is instantaneous.

```
0xAA 0x55 0x00 0x00 0x00 0x3A 0x00 0x03 0x16 0x3F 0x80 0x00 0x00 0x55 0xAA
  43   44   45   46   47   48   49   50   51   52   53   54   55   56   57
```

> The operator byte at offset 51 is **`0x16`**. It read `0x14` until 2026-09-21 — if you are
> comparing against an older capture or an older copy of this document, that is the difference.

| Offset | Field | Size | Hex | Decoded |
|---|---|---|---|---|
| 43–44 | START | 2 B | `AA 55` | Start sequence |
| 45–48 | Length / Offset | 4 B | `00 00 00 3A` | 58 — first byte of the next packet |
| 49–50 | Step No | 2 B | `00 03` | 3 |
| 51 | Operator | 1 B | `16` | `LOCKAh` — renumbered from `0x14` on 2026-09-21 |
| 52–55 | Nominal Value (Multiplier) | 4 B | `3F 80 00 00` | 1.0 (= 100 %) |
| 56–57 | END | 2 B | `55 AA` | Stop sequence |

**Run-time effect.** The Secondary computes
`lockedCapacityAh = fabsf(batAccuCapacityAh) × 1.0`, sets its validity flag, and requests the next
step. No debounce is applied — the step ends on entry.

The `fabsf()` matters: step 2 is a discharge, so the counter is negative when this runs (manual
p.180 stores the reference unsigned). Without it the reference is −8, the `PERCAh` threshold becomes
−10.4, and a charging step is already above it on the first sample.

### 14.7 Sample Packet — step 4, `SET Ah = 0`

19 bytes, offsets 58–76. Byte-identical to step 1 but for the step number and the offset.

```
0xAA 0x55 0x00 0x00 0x00 0x4D 0x00 0x04 0x0A 0x01 0x26 0x00 0x00 0x00 0x00 0x01 0xFF 0x55 0xAA
  58   59   60   61   62   63   64   65   66   67   68   69   70   71   72   73   74   75   76
```

`Length / Offset` carries **77** (`0x0000004D`). Field meanings are exactly those of §14.4.

> **A trap this packet caught.** An earlier draft of the V0.13 sheet had this packet at 18 bytes,
> with the operator byte `0x0A` missing — and the chain still balanced, because 58 + 18 = 76 was
> exactly what the `Length / Offset` cell declared. The chain validates a packet's declared size
> against the next packet's position; it never checks what the operator requires. The Secondary
> reads offset 8 as the operator unconditionally (`STEP_DATA_OPERATOR_INDEX`), so it would have
> decoded `0x01` — `CC Chg` — and run the rest as a current setpoint. Silent wrong behaviour, not a
> parse error.
>
> **Validate packet length per operator, not just offset continuity.**

### 14.8 Sample Packet — step 5, `CCCV Chg` 1.6 A / 4.1 V with `I < 1.0`

28 bytes, offsets 77–104. **Nothing here is new** — the §6.4 two-nominal layout unchanged.

```
0xAA 0x55 0x00 0x00 0x00 0x69 0x00 0x05 0x04 0x3F 0xCC 0xCC 0xCD 0x40 0x83 0x33 0x33 0x01 0x31 0x52 0x3F 0x80 0x00 0x00 0x00 0x00 0x55 0xAA
  77   78   79   80   81   82   83   84   85   86   87   88   89   90   91   92   93   94   95   96   97   98   99  100  101  102  103  104
```

| Offset | Field | Size | Hex | Decoded |
|---|---|---|---|---|
| 77–78 | START | 2 B | `AA 55` | Start sequence |
| 79–82 | Length / Offset | 4 B | `00 00 00 69` | 105 |
| 83–84 | Step No | 2 B | `00 05` | 5 |
| 85 | Operator | 1 B | `04` | `CCCV Chg` |
| 86–89 | 1st Param Value (Current) | 4 B | `3F CC CC CD` | 1.6 A |
| 90–93 | 2nd Param Value (Voltage) | 4 B | `40 83 33 33` | 4.1 V |
| 94 | No. of Cutoff Conditions | 1 B | `01` | 1 |
| 95 | Cutoff Condition | 1 B | `31` | Current |
| 96 | Logic | 1 B | `52` | `<` |
| 97–100 | Limit Value | 4 B | `3F 80 00 00` | 1.0 A |
| 101 | Action Type | 1 B | `00` | Blank — end the step |
| 102 | No. of Registration parameters | 1 B | `00` | 0 |
| 103–104 | END | 2 B | `55 AA` | Stop sequence |

### 14.9 Sample Packet — step 6, `CC Chg` 1.0 A with `> 130 PERCAh`

24 bytes, offsets 105–128. `Length / Offset` carries **129** (`0x00000081`).

**The packet layout is unchanged from §6.1.** Only the cutoff-condition code is new — `0x3E` where
a `0x3A` (Accumulated Capacity) would otherwise sit.

```
0xAA 0x55 0x00 0x00 0x00 0x81 0x00 0x06 0x01 0x3F 0x80 0x00 0x00 0x01 0x3E 0x51 0x43 0x02 0x00 0x00 0x00 0x00 0x55 0xAA
 105  106  107  108  109  110  111  112  113  114  115  116  117  118  119  120  121  122  123  124  125  126  127  128
```

| Offset | Field | Size | Hex | Decoded |
|---|---|---|---|---|
| 105–106 | START | 2 B | `AA 55` | Start sequence |
| 107–110 | Length / Offset | 4 B | `00 00 00 81` | 129 |
| 111–112 | Step No | 2 B | `00 06` | 6 |
| 113 | Operator | 1 B | `01` | `CC Chg` |
| 114–117 | Param Value (Current) | 4 B | `3F 80 00 00` | 1.0 A |
| 118 | No. of Cutoff Conditions | 1 B | `01` | 1 |
| 119 | Cutoff Condition | 1 B | `3E` | `PERCAh` — **new code, the only new byte** |
| 120 | Logic | 1 B | `51` | `>` |
| 121–124 | Limit Value | 4 B | `43 02 00 00` | 130.0 (= 130 %) |
| 125 | Action Type | 1 B | `00` | Blank — end the step |
| 126 | No. of Registration parameters | 1 B | `00` | 0 |
| 127–128 | END | 2 B | `55 AA` | Stop sequence |

**Run-time effect.** `threshold = (130.0 / 100) × lockedCapacityAh`, compared against
`fabsf(batAccuCapacityAh)` through the standard six-logic switch and the standard `limitExCount`
debounce (`LIMIT_EX_COUNTER` = 5). If `LOCKAh` never ran — including after a `GOTO` or a Q9 jump
past it — the step raises an error and stops; it must never treat the reference as zero.

### 14.10 Sample Packet — step 7, `CV Chg` 10.2 V ending at `0.8 CNom` (new in V0.15)

29 bytes, offsets 129–157. `Length / Offset` carries **158** (`0x0000009E`).

**Nothing in this packet is new.** It is the §6.1 one-nominal layout with one cutoff block (§7) and
one inline registration pair (§9.1). What it shows is **how a `CNom`-relative value is encoded**.

```
0xAA 0x55 0x00 0x00 0x00 0x9E 0x00 0x07 0x02 0x41 0x23 0x33 0x33 0x01 0x3B 0x56 0x42 0xA0 0x00 0x00 0x00 0x01 0x29 0x41 0x20 0x00 0x00 0x55 0xAA
 129  130  131  132  133  134  135  136  137  138  139  140  141  142  143  144  145  146  147  148  149  150  151  152  153  154  155  156  157
```

| Offset | Field | Size | Hex | Decoded |
|---|---|---|---|---|
| 129–130 | START | 2 B | `AA 55` | Start sequence |
| 131–134 | Length / Offset | 4 B | `00 00 00 9E` | 158 — first byte of the next packet |
| 135–136 | Step No | 2 B | `00 07` | 7 |
| 137 | Operator | 1 B | `02` | `CV Chg` |
| 138–141 | Param Value (Voltage) | 4 B | `41 23 33 33` | 10.2 V |
| 142 | No. of Cutoff Conditions | 1 B | `01` | 1 |
| 143 | Cutoff Condition | 1 B | `3B` | **Step Capacity** |
| 144 | Logic | 1 B | `56` | `=` — see below |
| 145–148 | Limit Value | 4 B | `42 A0 00 00` | **80.0 Ah** = 0.8 × CNom (100 Ah) |
| 149 | Action Type | 1 B | `00` | Blank — end the step |
| 150 | No. of Registration parameters | 1 B | `01` | 1 |
| 151 | Registration Type | 1 B | `29` | Step Capacity |
| 152–155 | Registration Value | 4 B | `41 20 00 00` | **10.0 Ah** = 0.1 × CNom — float, not ms (§11 W-9) |
| 156–157 | END | 2 B | `55 AA` | Stop sequence |

**How `CNom` is encoded: it isn't.** There is no `CNom` cutoff code, registration code or flag.
The encoder — the Web App — multiplies by the battery's nominal capacity **before sending** and puts
an absolute amp-hour value on the wire: `0.8 CNom` → 80.0 Ah, `0.1 CNom` → 10.0 Ah. The Secondary
sees only "Step Capacity = 80 Ah" and has no idea a percentage of rated capacity was ever involved.
That is why V0.15 needs no firmware change.

Two consequences an encoder has to own:

- **The multiplication happens once, at encode time.** If the battery's nominal capacity is changed
  after a program is uploaded, the program still ends at the old 80 Ah. Re-encode the program to pick
  up a new CNom.
- **The encoder needs CNom to encode at all.** A program that uses `CNom` cannot be encoded for a
  battery whose nominal capacity has not been entered.

**Why Step Capacity (`0x3B`) and not Accumulated Capacity (`0x3A`).** A `CNom` limit counts the
amp-hours put in **during this step**: 80 Ah from the moment step 7 starts. Step Capacity is that
counter. The Secondary resets `batStepCapacityAh` to 0 whenever a step is loaded
(`btsSecUart.c:229`, `:345`, alongside the step timer) and restores it on power-fail resume
(`:3933`). Compare with the note under §14.2: the **Accumulated** counter that `PERCAh` reads is
program-global and runs across steps. Encoding a `CNom` limit as `0x3A` would make step 7 end early
or immediately, depending on what steps 5–6 had already added.

**Why `=` is safe here.** Exact equality on a float counter that rises in small steps would
normally never match. The Secondary does not implement it that way: for Step Capacity, `=` means
**"reached"** — `>=` while charging, `<=` while discharging (`btsSecApp.c:2212-2226`) — with the usual
`LIMIT_EX_COUNTER` debounce. So step 7 ends when the step counter reaches 80 Ah. `>=` (`0x53`) would
behave the same for this charging step.

**Run-time effect.** Hold 10.2 V; log a record each time the step capacity passes another 10 Ah
(10, 20, … 80 — about 8 records, via `STEP_CAPACITY_REGISTRATION`, `btsSecApp.c:3407`); end the
step when 80 Ah has gone in.

### 14.11 Sample Packet — step 8, `STO`

11 bytes, offsets 158–168. Unchanged apart from its position: it was step 7 at offsets 129–139 until
V0.14. Included to close the chain.

```
0xAA 0x55 0xFF 0xFF 0xFF 0xFF 0x00 0x08 0x0B 0x55 0xAA
 158  159  160  161  162  163  164  165  166  167  168
```

`FF FF FF FF` in `Length / Offset` marks the last step of the program.

### 14.12 `PAU` is not changed

An earlier revision of this section rebuilt `PAU` to carry standard cutoff-condition blocks, so that
the capacity lock could ride on it as a limit. **That is withdrawn.** `PAU` keeps the bare
4-byte millisecond layout of §6.3 exactly as documented — not one byte changes, and no existing
program needs re-encoding.

The reason is not only cost. A limit is a condition that gets *evaluated* — sampled, compared,
debounced, eventually satisfied. Locking a capacity does none of that: it fires once on step entry,
writes a value, and ends the step. That is instruction behaviour, which is why it is now an
operator. BM expresses it as a limit only because its program table has no other column to put it
in; §1–§13 of this document are not affected by our choosing differently.

> **A hazard that no longer exists.** Under the withdrawn design, every plain pause had to be
> re-encoded with logic `0x53` (`>=`) rather than `0x51` (`>`), because the generic time check
> implements `>=` as today's `PAU` behaviour and `>` as one millisecond tick later. Getting that
> byte wrong would have silently lengthened every pause in every existing program, with no error and
> no failing test. With `PAU` untouched, the risk is gone rather than managed.

### 14.13 Validation

Rejected on the Web App, again on the Primary as a backstop, and again by the Secondary parser.
Rejection uses the existing `REASON` mechanism of [`bm_program_v3.2.md`](bm_program_v3.2.md).

| Rule | Reject when |
|---|---|
| V2 | `PERCAh` (`0x3E`) appears on `PAU` or on `LOCKAh` |
| V5 | `PERCAh` limit value ≤ 0 |
| V6 | `SET` global parameter code is not `0x26` or `0x2A` |
| V7 | `SET` parameter count runs the packet past its declared length |
| V9 | `LOCKAh` nominal value ≤ 0 |
| V10 | `LOCKAh` packet length is not exactly 15 bytes — and, generalised, any fixed-shape operator whose packet length does not match its operator (see the trap in §14.7) |

Rule numbers V1, V3, V4 and V8 were retired with the withdrawn `PAU` design; the surviving numbers
are left unchanged so earlier review notes still resolve.

`LOCKAh` also stays **out of** the Q9 live-step-amend whitelist (`liveStep.c:17`) — amending an
instantaneous step mid-run is meaningless.

### 14.14 Known open point

The `LOCKAh` multiplier semantics (`reference = |Ah| × value`) are **our interpretation** of what BM
does with `> 1 AhDef`. BM only ever shows the value `1`, and its literal grammar arguably means the
inverse (`|Ah| ÷ value`). At `1.0` both readings agree, so the disagreement is confined to values
nobody has been observed to use — and since we will never import BM programs, the only cost of being
wrong is that a number means something different on our machine than on theirs.

---

## 15. V0.13 → V0.14 — the operator renumber (2026-09-21)

`Program Packet V0.14.xlsx` is a copy of V0.13 with the `Op-Codes` operator table extended and one
sample-packet byte corrected. **V0.13.xlsx is unchanged** and remains on disk.

| Op-code | V0.13 | V0.14 |
|---|---|---|
| `0x01`–`0x13` | CC Chg … CV DChg | **identical** |
| `0x14` | `LOCKAh` | **`PRODUCER`** |
| `0x15` | — (firmware `IDLE`, declaration-only) | **`CC_RECHG`** |
| `0x16` | — | **`LOCKAh`** |

Cells changed in the workbook: `Op-Codes!B22:C24` (three operator rows) and
`AhDef-PercAh Sample Packets!J16` (the LOCKAh operator byte, `0x14` → `0x16`). Nothing else.
`AhDef-PercAh Sample Packets!P7` still reads `14` and is **correct** — that row is the byte-offset
ruler, not packet data.

Changed in this document: the title and source path, the §5 op-code table, §14.1's operator value and
change list, and §14.6's sample packet bytes plus its offset-51 row. §13 is left as written — it
records the V0.12 → V0.13 comparison and was accurate for that comparison.

### What this does and does not mean

**`PRODUCER` and `CC_RECHG` have numbers and nothing else.** No behaviour is specified anywhere, on
any board. The Secondary's parser rejects both. Treat any guess about what they do as a guess.

**`CC_RECHG` is unfinished business, not a finished decision.** It is expected to correspond to
BM4's `RCH` (recharge) — *"identical to CHA, plus the recharge relay is set"* — but the BTS-600 gap
analysis records that **no distinct recharge relay or mode exists on this hardware**, and ME FRD
`Q-47` had proposed representing `RCH` as *a charge opcode plus a recharge flag* rather than as its
own op-code. Allocating `0x15` answers that question differently. Confirm the hardware posture and
close `Q-47` explicitly before implementing it.

**`LOCKAh` moving is a breaking wire change.** Nothing was in the field — `LOCKAh` shipped
2026-09-18 and has never been HIL-tested — so the move was free at the time it was made. It stops
being free the moment a program containing `LOCKAh` is stored or an encoder ships against `0x14`.

Design record: `docs/superpowers/specs/2026-09-21-operator-opcode-renumber-design.md`.

*(This section describes V0.14 as it stood. The sheet names it cites, `AhDef-PercAh …`, became
`Reference Program Steps 2` / `Sample Packets 2` in V0.15 — see §16.)*

---

## 16. V0.14 → V0.15 — a `CNom` example step (2026-09-23)

`Program Packet V0.15.xlsx` extends our own two sample sheets with one step. **No op-code, cutoff
code, registration code, logic code or packet layout changed, so no firmware changed.**
V0.14.xlsx is unchanged and remains on disk.

Checked cell by cell against V0.14:

| V0.14 sheet | V0.15 sheet | Cells | Verdict |
|---|---|---|---|
| `Reference Program Steps` | same | 82 → 82 | identical |
| `Sample Packets` | same | 710 → 710 | identical |
| `TABLE Sample Packet` | same | 273 → 273 | identical |
| `AhDef-PercAh Reference Steps` | **`Reference Program Steps 2`** | 47 → 56 | renamed; battery context added; step 7 `CV Chg` added; `STO` → step 8; two stale notes removed |
| `AhDef-PercAh Sample Packets` | **`Sample Packets 2`** | 412 → 498 | renamed; steps 1–6 byte-identical; step 7 `CV Chg` packet added at 129–157; `STO` moved to 158–168 as step 8 |
| `Op-Codes` | same | 181 → 183 | `PERCAh (Percentage Ah)` / `0x3E` row added to the cutoff table (`E16:F16`); the registration table below it moved down two rows, **no value changed** |

**The two notes removed from the reference sheet** were stale, and removing them was correct:

- *"Normative source: …2026-09-17-percah-ahdef-design.md — if that spec and this sheet disagree,
  the spec wins."* That rule still holds; it lives in the banner of §14 instead of in the workbook.
- *"Step 10 carries no new feature. It is the reference for re-encoding pauses in EXISTING
  programs under the new PAU layout…"* This belonged to the **withdrawn** `PAU` redesign (§14.12).
  There has been no step 10 and no new `PAU` layout since 2026-09-18.

**Changed in this document:** the title, version banner, source path and sheet list; the §14
banner (the sheet names, and the stale "not yet implemented" wording, since `LOCKAh`/`PERCAh` were
implemented on 2026-09-18); a note under §7 for the `PERCAh` workbook row; §11 W-9 and W-10;
§14.2 (battery context, steps 7–8); §14.3 (packet map); **new §14.10** (the `CV Chg` step and how
`CNom` is encoded); §14.11 (`STO`, now step 8); §14.12–§14.14 (renumbered from §14.11–§14.13, no
content change); this section. §1–§10, §12, §13 and §15 are unchanged.

### What this does and does not mean

**No firmware work.** Step 7 uses Step Capacity `0x3B`, Logic `=` `0x56` and registration
`0x29` — all parsed and executed by the Secondary today. **Not HIL-tested**, like everything else in §14.

**The work is on the encoder side.** `CNom` is a Web App concept: the Web App must know the
battery's nominal capacity and multiply by it when it encodes a program (§14.10). Whether the Web App
does this today has not been checked from the firmware side.
