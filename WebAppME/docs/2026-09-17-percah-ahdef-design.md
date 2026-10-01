# LOCKAh and PERCAh — capacity-relative step limits

**Date:** 2026-09-17 (revised same day — see §2.1; worked example replaced 2026-09-18)
**Status:** implemented 2026-09-18 (Secondary), **except §5 persistence** — see §12. HIL pending.
**Boards:** Web App · Primary (SAM9X60) · Secondary (STM32H723)
**Artifact:** https://claude.ai/artifact/YWzYK4AcsiVwfrFoubeVJw

> **Filename note.** This file keeps its original `percah-ahdef` name deliberately: that path is
> written into banner cells of `Program Packet V0.13.xlsx`. The feature is named
> **`LOCKAh` / `PERCAh`**; `AhDef` is BM's name for it and is not used in our design.

> **Worked example.** §7 is transcribed from `Program Packet V0.13.xlsx`, sheets
> `AhDef-PercAh Reference Steps` and `AhDef-PercAh Sample Packets`, authored by hand on 2026-09-18.
> That workbook is what the Web App team encodes against, so §7 follows it rather than the other
> way round. Two faults were found while re-deriving the bytes and fixed in the workbook the same
> day; §7.3 records them because the first one is a trap worth not repeating.

---

## 1. Problem

A program needs to charge a battery back by a **percentage of the capacity it actually
delivered**, not by a fixed number of amp-hours. A tired cell that gave 6 Ah gets 7.8 Ah back;
a healthy one that gave 9 Ah gets 11.7 Ah. The threshold is not knowable when the program is
written — it depends on the cell under test.

Two pieces implement this, separated by other steps:

| Name | Kind | Role |
|---|---|---|
| `LOCKAh` | **operator** (`0x14`) | Stores the present Ah counter as a 100 % reference. Ends its step immediately. |
| `PERCAh` | **cutoff condition** (`0x3E`) | Ends a step when the Ah counter reaches a given percentage of that reference. |

Worked example, adapted from `BM_Manual_eng 2.pdf` §12:

| Step | Operator | Nominal | Limit | Effect |
|---|---|---|---|---|
| 2 | `SET` | `Ah = 0` | | Ah counter reset |
| 3 | `DCH` | 2 A | `< 3.2 V` | Discharge; counter reaches **−8 Ah** |
| 4 | `LOCKAh` | `1.0` | | Store `|−8|` × 1.0 = **8 Ah**, end step at once |
| 5 | `SET` | `Ah = 0` | | Ah counter reset again |
| 6 | `CHA` | 1.6 A / 4.1 V | `< 1 A` | CCCV charge until taper |
| 7 | `CHA` | 1 A | `> 130 PERCAh` | Charge until counter passes **1.30 × 8 = 10.4 Ah** |

None of this is currently expressible in our step encoding.

---

## 2. Locked decisions

| # | Decision | Decided |
|---|---|---|
| D1 | The capacity lock is **its own operator, `LOCKAh` = `0x14`** — not a limit, and not a change to `PAU`. | 2026-09-17 (rev) |
| D2 | `PERCAh` is a cutoff condition valid on **regulating operators only** — never on `PAU`, never on `LOCKAh`. | 2026-09-17 |
| D5 | `LOCKAh` semantics: `reference = fabsf(accuAh) × nominalValue`. `1.0` = 100 %, `1.1` = 110 %. Reject `nominalValue ≤ 0`. | 2026-09-17 |
| D6 | `SET Ah = 0` resets `batAccuCapacityAh` **and** `registration.presDetectAccuCapacity` only. | 2026-09-17 |
| D7 | `PERCAh` evaluated with no reference stored → **step error, stop**. Never treat as zero. | 2026-09-17 |
| D8 | The multiplier **rides the wire**. The Secondary computes it; the Web App does not pre-resolve it. | 2026-09-17 |
| D9 | We will never import programs from a BM machine. BM compatibility is **not** a design constraint. | 2026-09-17 |
| D10 | **The Primary stays operator-blind.** No Primary validation backstop; its chain walk already handles both new packet shapes unchanged. See §6.1. | 2026-09-18 |

### 2.1 What this revision withdrew

An earlier version of this spec carried `AhDef` as a cutoff condition (`0x3F`) on a rebuilt `PAU`.
That is **withdrawn**. The reason is not only cost — it was miscategorised.

A limit is a condition that gets *evaluated*: sampled, compared, debounced, eventually satisfied.
`AhDef` does none of that. It fires once on step entry, writes a value, and ends the step. That is
instruction behaviour. BM placed it in the Limit column because BM's program table has only
Operator / Nominal Value / Limit / Action / Registration columns and there was nowhere else to put
it. We inherited that shape without needing to.

| Withdrawn | Was |
|---|---|
| D3 | `PAU` carries Time or `AhDef` only |
| D4 | `PAU` rebuilt as a cutoff-bearing operator (breaking wire change) |
| Condition `0x3F` | `AhDef` as a cutoff condition — never implemented |
| Rules V1, V3, V4, V8 | All concerned `AhDef`-on-`PAU` |

**Consequences of the withdrawal, all positive:**

- **`PAU` is completely unchanged** — not one byte. No re-encoding of existing programs, no
  coordinated release for that reason.
- **The one-tick logic-byte hazard is gone, not mitigated.** The earlier design required every
  plain pause to be re-encoded with logic `0x53` rather than `0x51`; getting it wrong would have
  silently lengthened every pause in every existing program by one millisecond tick, with no error
  and no failing test. That risk existed only because `PAU` was being re-encoded.
- One fewer new cutoff-condition code.
- `LOCKAh` joins the existing family of single-purpose minimal operators (`STO`, `GOTO`, `INT`)
  rather than forking `PAU` into two code paths.

### 2.2 Naming

Two names, two jobs. Conflating them is the likeliest misreading of this spec, so state it plainly:

| | Stores the reference | Compares against it |
|---|---|---|
| **Name** | `LOCKAh` | `PERCAh` |
| **Kind** | operator `0x14` | cutoff condition `0x3E` |
| **BM called it** | `AhDef` | `PERCAH` |
| **Runs** | once, on step entry | every sample, with debounce |

`LOCKAh` writes the number; `PERCAh` reads it. Neither replaces the other, and a program needs both.

**`AhDef` is not used in our design.** `PERCAh` keeps BM's name because, unlike `AhDef`, it was
never miscategorised: it genuinely is a limit, it lives in the Limit column, and it does what BM's
`PERCAH` does. The divergence from BM is therefore exactly one operator — the one that was wrong.

**Spelling is `PERCAh`** — capitals on `PERC`, lower-case `h` on the unit, matching `LOCKAh`. The
V0.13 workbook currently writes it `PercAh` in its cells and in two sheet names; those are to be
recased, see §8. Wherever this spec quotes a sheet name it keeps the name verbatim, so that the
reference still resolves to a tab that exists.

The word `AhDef` survives only in documents that describe **BM's** language rather than ours:
`A1_Operator_Reference.md`, `bts600_operator_gap_analysis.md`, and the `ME_FRD` set. Those keep it;
see §8.

---

## 3. Encoding changes

Reference for existing layout: `program_packet_v0.13.md`.

### 3.1 New operator — `LOCKAh` = `0x14`

`0x13` (`CV_DChg`) is the last operator in use, so `0x14` is the next wire code.

> **`IDLE` currently holds enum slot `0x14`** in `stepOperators_t` (`stepData.h:17`) and is
> **referenced nowhere in the firmware** — it appears only in the declaration. It shifts to `0x15`.
> Flagged rather than deleted; removing it is a separate call.

One guard moves with it. `stepData.c:49` reads:

```c
if (packet->operator >= CC_Chg && packet->operator <= CV_DChg) {
```

which today rejects `0x14`. It becomes `<= LOCKAh`. That guard is also why firmware without this
change fails cleanly on a `LOCKAh` step instead of misparsing it.

### 3.2 `LOCKAh` packet — 15 bytes

Same shape as `GOTO` (§6.4 of the encoding doc): operator, one fixed-size payload, end. **No
cutoff-condition count, no registration count.**

```
[0..1]   AA 55                      START
[2..5]   ·· ·· ·· ··                next-packet offset
[6..7]   step number
[8]      14                         LOCKAh
[9..12]  multiplier (float32)       1.0 = 100 %, 1.1 = 110 %
[13..14] 55 AA                      END
```

The multiplier occupies the **Nominal Value** slot, not a Limit slot — which is the visible
consequence of D1, and what the Web App editor must present.

### 3.3 New cutoff condition — `PERCAh` = `0x3E`

Extends `cutoffConditionType_t` (`stepData.h:38`), which currently ends at
`STEP_ENERGY_CUTOFF_CONDITION = 0x3D`. Enum name: `PERC_AH_CUTOFF_CONDITION`.

**No packet layout change for regulating operators.** Their cutoff block (§7 of the encoding doc)
already carries condition + logic + 4-byte value + action. `PERCAh` costs exactly one byte value —
a `0x3E` where a `0x3A` (Accumulated Capacity) would have gone.

Limit value is a percent, float32: `130.0` = 130 %.

### 3.4 `SET` — start honouring the reserved count byte

Byte 9 of a `SET` packet is already specified as *"No. of Global Limit Parameters"* and already
transmitted on every `SET`, always zero. `stepData.c:161` skips it with a bare `index++` and never
reads it.

**Before** (14 bytes, fixed):

```
[9]      00                         count byte — SKIPPED by firmware
[10..11] registration bitmask
[12..13] 55 AA
```

**After** (14 + 5N bytes):

```
[9]      N                          no. of global parameters      <-- now read
  per parameter, N times (5 bytes each):
  +0     parameter code (1 B)                                     <-- new
  +1..+4 value (4 B)                                              <-- new
[9+5N..10+5N]  registration bitmask (2 B)
[..]     55 AA
```

With `N = 0` the bytes are byte-identical to today. Nothing that works now stops working.

Parameter codes reuse the §9.1 registration value codes, so there is no second numbering scheme:

| Code | Channel | Written as | Value encoding |
|---|---|---|---|
| `0x26` | Accumulated Capacity | `SET Ah = 0` | float32 BE, amp-hours |
| `0x2A` | Accumulated Energy | `SET Wh = -640.5` | float32 BE, watt-hours |

Only `0x26` and `0x2A` are in scope. Any other code is rejected (§6).

> **Parser note.** The current `SET` case never advances `index` past the registration bitmask,
> which is harmless today because parsing ends there. With N blocks preceding the bitmask, the
> count must be read and the blocks walked before the mask is located.

---

## 4. Run-time behaviour (Secondary)

### 4.1 New state

```c
float lockedCapacityAh;    /* GREAL[400] equivalent. Unsigned, in Ah.
                              One per circuit. Cleared on program start. */
bool  lockedCapacityValid; /* false until a LOCKAh step has run (D7). */
```

Named after `LOCKAh`, not after BM's `AhDef` — a live variable should not be named for an operator
that does not exist in our language.

One slot per circuit, not a general named-variable store. The manual exposes a single global slot
and nothing in scope needs more; a variable table would be speculative.

### 4.2 `LOCKAh` execution

`LOCKAh` is instantaneous. It is handled alongside `SET` / `REG` / `BEG` in the operator dispatch
(`btsSecApp.c:2482` region), not in the cutoff-evaluation path:

```c
case LOCKAh:
    appData.lockedCapacityAh    = fabsf(appData.batAccuCapacityAh) * progData.stepData.nominalValue1;
    appData.lockedCapacityValid = true;
    progData.requestStepTimeOut   = 0;
    progData.requestStepRespCount = 0;
    btsSecReqNextProgramStep(progData.stepData.stepNumber + 1);
    regDataSend();
    break;
```

**No debounce** — there is nothing to debounce; the step ends on entry.

`fabsf()` is load-bearing: the preceding step is typically a discharge, so the counter is negative
when `LOCKAh` runs. Manual p.180 stores the reference unsigned. Without it the reference is −8, the
`PERCAh` threshold becomes −10.4, and a charging step is already above it on the first sample.

### 4.3 `PERCAh` evaluation

One new `case` in the cutoff dispatch at `btsSecApp.c:1226`:

```c
if (!appData.lockedCapacityValid) {
    /* D7: step error and stop. Never treat as a zero reference. */
    ...raise step error, halt program...
}
threshold = (limitValue / 100.0f) * appData.lockedCapacityAh;   /* 130.0/100 × 8.0 = 10.4 */
measured  = fabsf(appData.batAccuCapacityAh);                   /* sign-insensitive, p.180 */
/* then the standard six-logic switch and the standard limitExCount debounce */
```

**The debounce must be reused.** Every other measured cutoff requires the comparison to hold for
`LIMIT_EX_COUNTER` (5) consecutive evaluations before acting. A `PERCAh` that fired on the first
sample would behave differently from everything around it for no reason a user could predict.

**`batAccuCapacityAh` is program-global.** It runs from one `SET Ah = 0` to the next, not from one
step to the next, so a `PERCAh` measures every step since the last reset — see the callout in
§7.1. This is existing behaviour, not something this feature introduces, but it decides what a
`PERCAh` step actually means, and the Web App has to present it that way.

### 4.4 `SET` global parameter execution (D6)

For parameter code `0x26`:

```c
appData.batAccuCapacityAh                   = value;
appData.registration.presDetectAccuCapacity = value;   /* <-- must not be forgotten */
```

The Ah registration trigger at `btsSecApp.c:3174` emits a record when the counter has moved more
than the configured delta from `presDetectAccuCapacity`. Reset the counter to 0 and leave that
tracker at 8, and the next evaluation sees an 8 Ah jump and fires a spurious registration record.

Charge, discharge and step capacity counters are **not** touched — they have their own channel
codes (`0x27`–`0x29`) and a user who wants them reset can say so.

---

## 5. Persistence across power-fail

`lockedCapacityAh` and `lockedCapacityValid` must survive a power cut mid-program. Without them, a
resumed test either charges to the wrong capacity or halts on D7.

`progBackUp_t` (`BTS_SEC_FW_V201/Core/Inc/btsSecUart.h:259`) is serialised at **fixed byte
offsets** by `powerFailBackup()` and read back by `powerFailResume()` (`btsSecUart.c:3703`,
`:3830`), and the Primary stores it in EEPROM. Adding a field moves the Primary's parser, the
EEPROM record layout and the frame spec together.

**O1 — RESOLVED 2026-09-18.** The record is the **measured-data frame**, owned by
[`ps_measured_data_v5.md`](../../BTS_Primary_SOM/docs/frame-formats/ps_measured_data_v5.md)
(start byte `0x33`). `btsSecSendMeasuredData()` (`btsSecUart.c:3447`) serialises the live state
— `batAccuCapacityAh` lands at payload byte 33 — the Primary stores it in EEPROM, and
`powerFailBackup()` parses the same layout back on resume. There is no separate backup frame; the
telemetry frame *is* the backup.

Consequence: adding `lockedCapacityAh` requires a **`ps_measured_data_v6.md`** revision, and the
field moves on four sides at once — Secondary TX, Secondary resume-parse, Primary parser/EEPROM
record, and the COM controller if it reads past that offset. This is why persistence is sequenced
first in the implementation plan.

This is the easiest part of the feature to forget and the hardest to notice going wrong — the
failure is invisible until a mains dip during a real customer test.

---

## 6. Validation rules

Enforced on the Web App and by the Secondary parser. (Not on the Primary — see §6.1 / D10.)

> **Corrected 2026-09-18: there is no `REASON` channel for these.** An earlier revision said
> rejection would use the `REASON` mechanism of `bm_program_v3.2.md`. That mechanism belongs to
> **Q9 (live step update)** and **Q10 (jump)**, not to program download — and even there,
> `bm_program_v3.2.md` records that the `ps_program` ACK/NACK frame carries a single byte with **no
> reason field**, so every Secondary-side rejection reaches the Web App as `0x05`, "reason not
> available on the wire".
>
> **Consequence for the Web App team:** a program violating any rule below is rejected with **no
> diagnostic**. The operator sees a failure and no explanation. So the Web App must enforce these
> rules itself, at authoring time, where it can point at the offending step. The Secondary parser is
> a safety net against a malformed download, not a source of error messages.
>
> No new `bm_program` revision is needed — the wire format does not change.

| Rule | Reject when |
|---|---|
| V2 | `PERCAh` (`0x3E`) appears on `PAU` or on `LOCKAh` |
| V5 | `PERCAh` limit value ≤ 0 |
| V6 | `SET` global parameter code is not `0x26` or `0x2A` |
| V7 | `SET` parameter count runs the packet past its declared length |
| V9 | `LOCKAh` nominal value ≤ 0 |
| V10 | `LOCKAh` packet length is not exactly 15 bytes |

Rule numbers V1, V3, V4 and V8 are retired with the `PAU` design; the surviving numbers are left
unchanged so earlier review notes still resolve.

### 6.1 The Primary needs no production change — corrected 2026-09-18

An earlier revision of this section claimed the Primary's chain walk "must learn the new `SET` size
(14 + 5N) and the fixed `LOCKAh` size (15)". **That is wrong.** All four walks —
`fetchProgramStep()` (`networkDataHandler.c:1490`), `liveStepPeekOperator()` (`liveStep.c:70`),
`jumpStep.c:33` and `buildCycleTable()` — derive a packet's length from the **absolute
next-packet offset at `[2..5]`**, never from its operator:

```c
nextPacketIndex   = (buf[i+2] << 24) | (buf[i+3] << 16) | (buf[i+4] << 8) | buf[i+5];
startOfNextPacket = (nextPacketIndex != TERMINATOR) ? nextPacketIndex : totalLen;
packetLen         = startOfNextPacket - startOfThisPacket;   /* operator never consulted */
```

A 15-byte `LOCKAh` packet and a 14+5N `SET` packet therefore walk correctly **today, unchanged** —
each carries its own size in its own offset field. And `liveStepOperatorAllowed()` (`liveStep.c:17`)
is an allow-list whose `default` returns `false`, so `LOCKAh` is **already excluded** with no edit.

**Decision D10 (2026-09-18): the Primary stays operator-blind.** The V-rules are enforced on the
Web App and by the Secondary parser only; no Primary backstop is built. Adding one would make the
Primary operator-aware for the first time, which means every future operator would then need a
Primary change too — the precise property that makes this feature cost the Primary nothing. The
Primary's share of this work is **regression tests only**, locking in both behaviours above.

---

## 7. Worked bytes

Transcribed from `Program Packet V0.13.xlsx`, sheets `AhDef-PercAh Reference Steps` and
`AhDef-PercAh Sample Packets`. Two corrections were applied — see §7.3.

### 7.1 The reference program

| Step | Operator | Param | Nominal | Cutoff | Logic | Limit | Registration |
|---|---|---|---|---|---|---|---|
| 1 | `SET` | Accumulated Capacity | Ah = 0 | | | | STANDARD |
| 2 | `CC DChg` | Current | 2.0 A | Voltage | `<` | 3.2 V | |
| 3 | `LOCKAh` | Multiplier | 1.0 | | | | |
| 4 | `SET` | Accumulated Capacity | Ah = 0 | | | | STANDARD |
| 5 | `CCCV Chg` | Current / Voltage | 1.6 A / 4.1 V | Current | `<` | 1.0 A | |
| 6 | `CC Chg` | Current | 1.0 A | **`PERCAh`** | `>` | **130** | |
| 7 | `STO` | | | | | | |

The shape is the point. Step 2 is what *establishes* a capacity — the reference cannot be locked
until the cell has actually been emptied — and step 3 locks whatever step 2 delivered. Step 4 then
clears the counter so steps 5 and 6 measure charge from zero.

> **The Ah counter is program-global, not per-step.** `appData.batAccuCapacityAh` runs from one
> `SET Ah = 0` to the next, across every step in between. So step 6's `PERCAh` is measured against
> the charge accumulated over steps 5 **and** 6 together — not step 6 alone. A Web App that resets
> per step, or a reader who assumes it does, gets a program that never terminates. If step 6 alone
> were wanted, a third `SET Ah = 0` would have to sit between steps 5 and 6.

With a cell that gives up 8 Ah in step 2, steps 5+6 together end at 10.4 Ah.

### 7.2 The packets

All seven packets, contiguous from offset 0, so the `Length / Offset` chain resolves end to end.

| Step | Operator | Bytes | Offsets | Next |
|---|---|---|---|---|
| 1 | `SET` | 19 | 0–18 | `00 00 00 13` |
| 2 | `CC DChg` | 24 | 19–42 | `00 00 00 2B` |
| 3 | `LOCKAh` | 15 | 43–57 | `00 00 00 3A` |
| 4 | `SET` | 19 | 58–76 | `00 00 00 4D` |
| 5 | `CCCV Chg` | 28 | 77–104 | `00 00 00 69` |
| 6 | `CC Chg` | 24 | 105–128 | `00 00 00 81` |
| 7 | `STO` | 11 | 129–139 | `FF FF FF FF` |

**Step 1 — `SET Ah = 0`** — 19 bytes, offsets 0–18:

```
AA 55            START
00 00 00 13      next-packet offset = 19
00 01            step number 1
0A               SET
01               1 global parameter          <-- count byte, previously skipped
  26             parameter  Accumulated Capacity (Ah)
  00 00 00 00    value      0.0
01 FF            registration bitmask — 9 parameters (STANDARD)
55 AA            END
```

**Step 2 — `CC DChg` 2.0 A, limit `V < 3.2`** — 24 bytes, offsets 19–42. Unchanged encoding:

```
AA 55            START
00 00 00 2B      next-packet offset = 43
00 02            step number 2
05               CC DChg
40 00 00 00      nominal 2.0 A
01               1 cutoff condition
  32             condition  Voltage
  52             logic      <
  40 4C CC CD    value      3.2 V
  00             action     blank — end the step
00               0 registration parameters
55 AA            END
```

**Step 3 — `LOCKAh`, multiplier 1.0** — 15 bytes, offsets 43–57:

```
AA 55            START
00 00 00 3A      next-packet offset = 58
00 03            step number 3
14               LOCKAh                      <-- new operator
3F 80 00 00      multiplier 1.0  (= 100 %)
55 AA            END
```

No cutoff-condition count and no registration count: `LOCKAh` is instantaneous. Same shape as
`GOTO` — operator, one fixed payload, end.

**Step 4 — `SET Ah = 0`** — 19 bytes, offsets 58–76. Byte-identical to step 1 but for the step
number and the offset:

```
AA 55            START
00 00 00 4D      next-packet offset = 77
00 04            step number 4
0A               SET
01               1 global parameter
  26             parameter  Accumulated Capacity (Ah)
  00 00 00 00    value      0.0
01 FF            registration bitmask — 9 parameters (STANDARD)
55 AA            END
```

**Step 5 — `CCCV Chg` 1.6 A / 4.1 V, limit `I < 1.0`** — 28 bytes, offsets 77–104. Unchanged
encoding:

```
AA 55            START
00 00 00 69      next-packet offset = 105
00 05            step number 5
04               CCCV Chg
3F CC CC CD      1st nominal  1.6 A
40 83 33 33      2nd nominal  4.1 V
01               1 cutoff condition
  31             condition  Current
  52             logic      <
  3F 80 00 00    value      1.0 A
  00             action     blank — end the step
00               0 registration parameters
55 AA            END
```

**Step 6 — `CC Chg` 1.0 A, limit `> 130 PERCAh`** — 24 bytes, offsets 105–128:

```
AA 55            START
00 00 00 81      next-packet offset = 129
00 06            step number 6
01               CC Chg
3F 80 00 00      nominal 1.0 A
01               1 cutoff condition
  3E             condition  PERCAh           <-- the only new byte in this packet
  51             logic      >
  43 02 00 00    value      130.0 (= 130 %)
  00             action     blank — end the step
00               0 registration parameters
55 AA            END
```

The packet layout is **identical** to any other regulating step. One byte carries the whole feature.

**Step 7 — `STO`** — 11 bytes, offsets 129–139:

```
AA 55            START
FF FF FF FF      no next packet — last step
00 07            step number 7
0B               STO
55 AA            END
```

**`PAU` needs no worked example — its encoding does not change.**

### 7.3 Two faults found and fixed during transcription

Both were caught by re-deriving the bytes rather than copying them, and both were corrected in the
workbook by its author on 2026-09-18. §7.2 above and the workbook now agree byte for byte.

| # | Found | Corrected to | Why it mattered |
|---|---|---|---|
| C1 | Step 4's `SET` packet was 18 bytes, with no operator byte at offset 8 | 19 bytes, `0A` restored at offset 8; steps 5–7 re-chained one byte later | The Secondary reads offset 8 as the operator unconditionally (`STEP_DATA_OPERATOR_INDEX`). It would have decoded `0x01` — `CC Chg` — and run the remaining bytes as a current setpoint. Silent wrong behaviour, not a parse error. |
| C2 | Step 5's 2nd nominal was `41 66 66 66` = 14.4 V, against 4.1 V on the Reference Steps sheet | `40 83 33 33` = 4.1 V | 14.4 V was inherited from the vendor's own `CCCV Chg` sample in V0.12. A 12 V pack also contradicts step 2's 3.2 V discharge cutoff. |

> **Why C1 survived review.** The `Length / Offset` chain was *self-consistent* across the error:
> 58 + 18 = 76, exactly what the cell declared, and every downstream packet agreed. The chain
> validates a packet's declared size against the next packet's position — never against what the
> operator requires. A byte can go missing and the chain still balances. What exposed it was
> comparing step 4 against step 1, the same operator in the same program.
>
> The implementation consequence: a Web App encoder test that checks only offset continuity will
> miss this entire class of fault. **The parse test has to assert packet length per operator** —
> V10 states that for `LOCKAh`; it should be generalised to every fixed-shape operator.

One cosmetic item is left: the `AhDef-PercAh Reference Steps` banner still refers to a "step 10"
and a "new PAU layout" from the withdrawn revision, and Op-Codes cell `Q3` still reads "130 % of
the **AhDef** reference" — the last `AhDef` in the workbook, which should say `LOCKAh`.

---

## 8. Documentation updates

| Document | Change |
|---|---|
| `program_packet_v0.13.md` §14 | Rewrite for `LOCKAh` / `PERCAh`; drop the `PAU` rebuild and the `0x53` warning; carry the §7 program |
| `Program Packet V0.13.xlsx` | **Author's own file — not edited by us.** C1 and C2 of §7.3 were reported and fixed on 2026-09-18. Still open, all cosmetic: (a) Op-Codes `Q3` reads “130 % of the **AhDef** reference”, should say `LOCKAh` — the last `AhDef` in the file; (b) every `PercAh` cell recases to `PERCAh` (Op-Codes `O3`, Reference Steps `H12`/`J12`, Sample Packets row 33 header and row 35 annotation); (c) the two sheet names `AhDef-PercAh …` carry both a retired name and the old case; (d) the Reference Steps banner `B3` still describes a “step 10” and the withdrawn `PAU` layout |
| `ps_program_v1.x` | New revision: `LOCKAh` operator, `SET` step size, new `REASON` cases for V2/V5/V6/V7/V9/V10 |
| `bm_program_v3.x` | New revision: same rules at the Web App boundary |
| power-fail frame spec | Extended backup record — **document to be identified, O1** |
| `03_SRS.md` SRS-PRG-053 | Says "AhDef/PERCAh" → "LOCKAh/PERCAh" |
| `04_RTM.md` UTC-PRG-012 | Same |
| `08_UTC.md` | Same |
| `A1_Operator_Reference.md` | **Keeps `AhDef`** — it documents BM's language. Two changes: correct line 996 / constraints row 11 (`PERCAh` is not `PAU`-only — the manual's own example puts it on a `CHA` step, and p.176 spells that limit `PerAc`), and add a note that our implementation uses `LOCKAh` |
| `bts600_operator_gap_analysis.md` | Keeps `AhDef` — historical analysis of BM |
| `ME_FRD` set | Keeps `AhDef` — different product. **Flagged, not changed**: whether ME follows BTS to `LOCKAh` is a separate decision |

---

## 9. Out of scope

- **`PERCCN_P` / `PERCCN_C`.** They work from the battery's `CNom`, not from the locked reference
  (manual p.180), and need the battery-parameter record to reach the Secondary, which today it does
  not. They are genuine limits, so when built they become cutoff conditions on regulating operators
  — the same shape as `PERCAh`. See `bts600_operator_gap_analysis.md` §3a.
- **A general named-variable store for `SET`.** One reference slot is what this feature needs.
- **`&`-chained limits.**
- **`SET` parameter codes other than `0x26` and `0x2A`.**
- **Importing BM program files** (D9).

---

## 10. Open items

| # | Item | Default |
|---|---|---|
| O1 | Which frame spec owns the power-fail backup record (§5)? | A lookup, not a decision. Resolve at the start of implementation. |
| O2 | D5 is **our interpretation**. BM only ever shows `> 1 AhDef`, and its literal grammar arguably means the inverse (`|Ah| ÷ value`). At `1.0` both readings agree, so the risk is confined to values nobody has been observed to use. | Ship D5; document it as an interpretation. Since D9 rules out BM imports, the only cost of being wrong is that a number means something different on our machine than on theirs — which no longer matters to anyone. |

---

## 11. Testing

Host unit tests (Secondary), following the existing `test_liveStep.c` / `test_jumpStep.c` pattern:

| Area | Cases |
|---|---|
| `LOCKAh` parse | Valid 15-byte packet; wrong length rejected (V10); `nominalValue ≤ 0` rejected (V9); operator `0x14` accepted by the range guard |
| `LOCKAh` eval | Reference from a negative counter stores positive; multiplier applied (1.0 and 1.1); step ends on first evaluation; next step requested |
| `PERCAh` parse | Accepted on a regulating operator; rejected on `PAU` and on `LOCKAh` (V2); value ≤ 0 rejected (V5) |
| `PERCAh` eval | Threshold arithmetic at 130 %; debounce requires 6 consecutive samples; no reference → step error (D7); sign-insensitive against a negative counter |
| `SET` parse | N=0 byte-identical to old; N=1 `Ah`; N=2 `Ah`+`Wh`; N past declared length rejected (V7); unknown code rejected (V6) |
| `SET Ah = 0` | Counter and `presDetectAccuCapacity` both reset; no spurious registration record follows |
| `PAU` regression | A `PAU` packet parses and runs **byte-identically to before this change** |
| Power-fail | Reference and validity flag survive a backup/resume round trip |

**HIL, on hardware:** the full example program end to end, verifying step 7 terminates at 1.30 ×
the capacity step 3 actually delivered — not at a fixed 10.4 Ah. Plus a mains interrupt during
step 6 to confirm the resumed test still ends at the right capacity.

Every claim of correctness here is subject to hardware-in-the-loop testing. Nothing in this spec is
verified on hardware.

---

## 12. Implementation record

| Part | State | Where |
|---|---|---|
| `LOCKAh` `0x14` — enum, guard, 15-byte parse (V9, V10) | **done** 2026-09-18 | `stepData.h`, `stepData.c` |
| `PERCAh` `0x3E` — cutoff acceptance (V2, V5) | **done** | `stepData.h`, `stepData.c` |
| `SET` global-parameter block, 14+5N (V6, V7) | **done** | `stepData.h`, `stepData.c` |
| Reference / threshold / comparison arithmetic | **done** | `capacityLock.c/.h` (new, pure, host-tested) |
| `LOCKAh` execution, `PERCAh` evaluation, D7 | **done** | `btsSecApp.c` |
| `SET` execution (D6) | **done** | `btsSecApp.c` |
| Primary | **no change needed** (D10), regression-tested | `tests/host/test_newOperators.c` |
| **§5 persistence across power-fail** | **NOT DONE — deferred** | `ps_measured_data_v6.md` written; no code |
| HIL | **NOT DONE** | §11 matrix, all 12 cases pending |

**What deferring §5 means in practice.** The feature works end to end on an uninterrupted run. If
the mains drops during a `PERCAh` step, the resumed test finds no stored reference and halts with
`en_ERR_INVALID_PROG_STEP` (D7). That is the safe failure — a visible stop, not a wrongly charged
cell — but it does lose the test. Persistence is a coordinated four-board change (Secondary,
Primary, COM controller, Web App) because the telemetry frame *is* the backup record, so it was
separated deliberately rather than rushed alongside the rest.

### 12.1 Corrections made while implementing

Recorded because each was a wrong belief in an earlier revision of this spec, not a coding slip.

| # | Was | Is |
|---|---|---|
| 1 | Primary must learn the new packet sizes | All four buffer walks follow the `[2..5]` offset and never the operator — no Primary change at all (D10, §6.1) |
| 2 | Power-fail record owned by an unidentified spec (O1) | It is the measured-data frame, `ps_measured_data_v5.md` → v6 (§5) |
| 3 | New fields append at the end of that frame | They must be inserted **before** DI/DO, or they fall outside `REAL_TIME_DATA_LENGTH` and are never persisted |
| 4 | Rejections surface via the `bm_program` `REASON` byte | No such channel for these; the Web App gets no diagnostic (§6) |
| 5 | D6 concerns the Ah tracker only | The Wh channel has `presDetectAccuEnergy` and the identical spurious-record failure; both pairs are reset |
| 6 | — | Widening the parser guard **without** `case LOCKAh:` routes `LOCKAh` into the regulating-operator `default:` at `stepData.c:450`. The enum and the case must ship together. |

---
