# ME System — Annex A1: BTS-600 Program Language Reference

| Field | Value |
|---|---|
| Document ID | ME-FRD-A1 |
| Title | BTS-600 Program Language Reference — Operators, Nominal Values, Limits, Actions, Registration |
| Version | 1.0 |
| Date | 2026-08-05 |
| Status | **Reference extract — controlled** |
| Extracted from | `BM_Manual_eng 2.pdf`, Battery Manager User Manual rev 99.3, **§12 (BTS-600 Programs)**, pages 156–219 |
| Scope | **All operators in the BTS-600 section are in ME scope** (confirmed 2026-08-05) |
| Reads with | `00_System_Overview.md` §0.7 (data model), `01_Web_Application.md`, `02_Primary_Board.md` |

---

## A1.0 How to Use This Document

This is the **working reference for the ME program language**. It replaces the need to read
`BM_Manual_eng 2.pdf` §12 for day-to-day implementation work.

Every entry carries a **`p.NNN`** back-reference to the source manual page. When an entry matters
for a design decision, verify it against that page — this document is an extract, and the manual
remains authoritative if the two ever disagree.

**What this document is:**

- The complete operator, nominal-value, limit, action and registration catalogue
- The semantics of each, including the traps and asymmetries that are easy to miss
- ME-specific implementation notes: which block owns what, and how each item maps onto the
  normalised wire encoding of Part 00 §0.7.13

**What this document is not:**

- A requirements document. Requirements live in Parts 00–02; this annex is what they refer to.
- A UI specification. How the operator *enters* these things is Part 01.
- A wire format. Byte layouts live in `ME-ICD-WEB-PRI`.

### A1.0.1 Reading the ME Notes

Each operator entry ends with an **ME note** in one of these forms:

| Marker | Meaning |
|---|---|
| **→ opcode** | Normalises to the named execution-layer opcode (Part 00 §0.7.13) |
| **Pass-through** | Transferred to the Primary unchanged; the Primary implements it directly |
| **Web App only** | Handled entirely in the Web Application; never reaches the Primary |
| **⚠ Open** | Behaviour or ownership not yet settled — carries a `Q-nn` reference |

---

## A1.1 The Step Model

A BTS-600 program is a **table**. Each step is a row with seven columns, and a step may **span
several sub-rows** — one per Limit. `p.161-183`

```
┌──────┬───────┬──────────┬───────────────┬──────────┬─────────┬──────────────┐
│ Step │ Label │ Operator │ Nominal Value │  Limit   │ Action  │ Registration │
├──────┼───────┼──────────┼───────────────┼──────────┼─────────┼──────────────┤
│  1   │       │ SET      │               │          │         │ STANDARD     │
│  2   │       │ PAU      │               │ 30 sec   │         │              │
│  3   │ Chrg  │ CHA      │ 15 A          │ > 14.1 V │         │ 2.0 A        │
│      │       │          │               │ > 10 h   │ ERR 7   │              │
│      │       │          │               │ > 100 Ah │ INT     │              │
│  4   │       │ DCH      │ 1.0 A         │ < 10.5 V │         │ 5 min        │
│  5   │       │ GOTO     │ Chrg          │          │         │              │
└──────┴───────┴──────────┴───────────────┴──────────┴─────────┴──────────────┘
```

| Column | Purpose |
|---|---|
| **Step** | Sequential step number, from 1 |
| **Label** | Optional symbolic name — target of `GOTO`, `ONERROR`, `ONEXIT` |
| **Operator** | What the step does. A **procedure name** may also appear here. |
| **Nominal Value** | Meaning depends on the operator: a regulation target, a cycle name, a repeat count, a jump label, a variable assignment, a circuit list, a file name… |
| **Limit** | The condition that ends the step. **Multiple limits allowed, one per sub-row.** |
| **Action** | What happens when the limit on the *same sub-row* is reached |
| **Registration** | How often to log during this step, or a registration-format name |

### A1.1.1 Three structural facts that are easy to get wrong

1. **Limit and Action are paired per sub-row**, not per step. A step with three limits has three
   independent actions.
2. **The Nominal Value column is a typed expression, not a number.** Its type depends on the
   operator. Anything that models it as a float is wrong. `p.167-173`
3. **Two nominal values in one step is legal and meaningful** — it is how CCCV is expressed. See
   §A1.3.4. `p.172`

### A1.1.2 Recommended program opening

`RD-1` p.167 recommends, for safety, that **every program begin with a pause step of several
seconds**, giving the operator time to check the setup and abort a faulty test before power flows.

`RD-1` p.161 separately recommends beginning with a `SET` to establish a registration format —
otherwise **only current and voltage are logged**.

Both recommendations together give the canonical program opening:

```
1        SET                              STANDARD
2        PAU                    30 sec
3  ...
```

---

## A1.2 Operator Quick Index

All 42 operators. **All are in ME scope.**

| Operator | Group | One-line purpose | ME | Ref |
|---|---|---|---|---|
| `CHA` | Power | Charge at nominal value until limit | → `CCChg`/`CPChg`/`CCCVChg`… | §A1.4.1 |
| `DCH` | Power | Discharge at nominal value until limit | → `CCDChg`/`CPDChg`… | §A1.4.2 |
| `RCH` | Power | As `CHA` + recharge relay + blinking LED | → charge opcode + recharge flag | §A1.4.3 |
| `PAU` | Power | Rest — contactors dropped until limit | Pass-through | §A1.4.4 |
| `PAUA` | Power | Pause variant | ⚠ `Q-30` | §A1.4.5 |
| `PAUO` | Power | Pause variant | ⚠ `Q-30` | §A1.4.5 |
| `BATT` | Power | Battery simulator — CV with current window | Pass-through | §A1.4.6 |
| `EIS` | Power | Impedance spectroscopy step | Pass-through | §A1.4.7 |
| `TABLE` | Power | Run a nominal-value profile from file | Pass-through + profile | §A1.4.8 |
| `BEG` | Flow | Cycle begin; nominal value = cycle name | Pass-through | §A1.5.1 |
| `CYC` | Flow | Cycle end; nominal value = repeat count | Pass-through | §A1.5.1 |
| `GOTO` | Flow | Jump to a label | Pass-through | §A1.5.2 |
| `INT` | Flow | Interrupt the program | Pass-through | §A1.5.3 |
| `STO` | Flow | Stop the program | Pass-through | §A1.5.4 |
| `RET` | Flow | Return from a procedure | Pass-through | §A1.5.5 |
| `ONERROR` | Flow | Label to jump to on any error | Pass-through | §A1.5.6 |
| `ONEXIT` | Flow | Label to run after program end | Pass-through | §A1.5.6 |
| *procedure* | Flow | Call a named procedure | Pass-through | §A1.5.7 |
| `SET` | Data | Set format / variable / counter / timer / global | Pass-through | §A1.6.1 |
| `REG` | Data | Register once, optionally to a named section | Pass-through | §A1.6.2 |
| `CLEAR` | Data | Clear `SET` limits, registrations, globals | Pass-through | §A1.6.3 |
| `ADD` | Data | Add channel values | Pass-through | §A1.6.4 |
| `SUB` | Data | Subtract channel values | Pass-through | §A1.6.4 |
| `SAVE` | Data | Save battery counters to a named test section | Pass-through | §A1.6.5 |
| `REST` | Data | Restore previously saved counters | Pass-through | §A1.6.5 |
| `FILE` | Data | Name the test section for following registrations | Pass-through | §A1.6.6 |
| `SETMUX` | Data | Enable additional multiplexer channels | ⚠ `Q-33` | §A1.6.7 |
| `ERR` | Annunc. | Message + interrupt program | Pass-through | §A1.7.1 |
| `MSG` | Annunc. | Message, no interrupt | Pass-through | §A1.7.2 |
| `ALIM` | Measure | Make following current limits sign-aware | Pass-through | §A1.8.1 |
| `FILTER` | Measure | Set digital filters | Pass-through | §A1.8.2 |
| `RANGE` | Measure | Manual/auto measurement range switch | Pass-through | §A1.8.3 |
| `IRANGE` | Measure | Current range select | ⚠ `Q-30` | §A1.8.4 |
| `URANGE` | Measure | Voltage range select | ⚠ `Q-30` | §A1.8.4 |
| `ISOEXT` | Measure | Isolation measurement, external | ⚠ `Q-30` | §A1.8.4 |
| `ISOINT` | Measure | Isolation measurement, internal | ⚠ `Q-30` | §A1.8.4 |
| `OUTA` | I/O | Output control A | ⚠ `Q-30` | §A1.8.4 |
| `OUTB` | I/O | Output control B | ⚠ `Q-30` | §A1.8.4 |
| `PARALLEL` | Multi | Parallel circuits — 15 slaves per master | Pass-through | §A1.9.1 |
| `SYNCLine` | Multi | Sync circuits in a SyncGroup | Pass-through | §A1.9.2 |
| `SYNCProgram` | Multi | Sync circuits running same program+version | Pass-through | §A1.9.2 |
| `TASK` | External | Start a parallel control process (≤12) | Pass-through | §A1.10.1 |
| `PROT` | External | Launch an external host program | Web App only | §A1.10.2 |

---

## A1.3 Nominal Values

### A1.3.1 Standard functions — `CHA`, `RCH`, `DCH` `p.171`

| Function | Example | Regulation |
|---|---|---|
| Current | `1.0 A`, `500 mA` | Constant current |
| Voltage | `14.4 V` | Constant voltage |
| Power | `1000 Watt` | Current and voltage controlled to constant power |
| Resistance | `1 Ohm` | Current and voltage controlled to constant resistance |

> **`p.171`, bold in the original:** *"In all program steps with CHA, RCH or DCH operators you
> always have to enter a nominal current, power or resistance! **Never enter a voltage value
> only!**"*

**Why:** a voltage source on a battery with no current ceiling is a short circuit through the
internal resistance. Voltage is always accompanied by a current or power target — see §A1.3.4.

### A1.3.2 Supplementary functions — battery-parameter-relative `p.171`

Derived from the battery definition's **Nom. Capacity** (`CNom`) and **Cells** fields, so one
program serves many battery sizes. **Not usable for ramps.**

| Function | Example | Resolves to |
|---|---|---|
| `ACn1` | `8 ACn1` | 8 × CNom / 1 h |
| `ACn2` | `8 ACn2` | 8 × CNom / 2 h |
| `ACn4` | `8 ACn4` | 8 × CNom / 4 h |
| `ACn5` | `10 ACn5` | 10 × CNom / 5 h — the "C5" rate. CNom = 100 Ah → **200 A** |
| `ACn10` | `8 ACn10` | 8 × CNom / 10 h |
| `ACn20` | `8 ACn20` | 8 × CNom / 20 h |
| `VnC` | `2.35 VnC` | 2.35 V/cell × cell count. 60 cells → **141 V** |

**ME note:** the Web App resolves these to a numeric setpoint before transfer (FR-WEB-079Y), and
**both** the expression and the resolved number are retained (FR-SYS-016). `10 ACn5` is 200 A on a
100 Ah pack and 20 A on a 10 Ah pack — a test record showing only the expression does not state
what was done to the battery.

### A1.3.3 Ramp functions `p.172`

Define a start value, an end value, and a duration. Current, voltage, power and resistance ramps
are all supported, as are combined ramps.

```
Nominal Value:  1000-5000 Watt  300 sec
```

→ power increases linearly from 1000 W to 5000 W over 300 seconds.

**Restrictions:** supplementary (`ACn*`, `VnC`) functions cannot be used for ramps `p.171`, and
there is **no automatic range switching during a ramp** `p.206`.

### A1.3.4 Parallel functions — two nominal values in one step `p.172`

**This is how CCCV is authored.** It is not a separate operator and not a limit — it is two nominal
values in the same step.

| Combination | Example | Behaviour |
|---|---|---|
| **Voltage + Current** (CCCV charge) | `CHA` / `100 V` / `15 A` | Charge current held at 15 A until voltage reaches 100 V; then voltage held at 100 V and current tapers |
| **Power + Voltage** (CP→CV discharge) | `DCH` / `1000 Watt` / `80 V` | Discharge at 1000 W until voltage falls to 80 V; then voltage held constant |

> `p.172`: *"Controls voltage and current. Neither the nominal voltage nor the nominal current
> value may be exceeded."*

The rule is symmetric and simple: **for a charge operator neither value may be exceeded; for a
discharge operator neither may be fallen below.** The step transitions from regulating one quantity
to regulating the other when the second is met.

> ### ⚠ This resolves `Q-48` and creates `Q-50`
>
> **`Q-48` is answered.** CCCV is discriminated at authoring time by the **presence of two nominal
> values**, not by a current nominal plus a voltage *limit*. The encoder can detect it
> deterministically: two nominal values in one power step → a CCCV-class opcode.
>
> **`Q-50` is new, and it matters.** The manual documents **two** two-value combinations:
> `V + A` (CCCV) and `Watt + V` (CP→CV). The current four-opcode charge set
> (`CCChg`/`CVChg`/`CPChg`/`CCCVChg`) has **no opcode for power-plus-voltage**. Either:
> - a `CPCVChg` / `CPCVDChg` opcode pair is added; or
> - `Watt + V` is declared out of ME scope and rejected at authoring; or
> - the opcode set carries the regulation mode as **two mode fields** (primary + transition)
>   rather than as one enumerated opcode — which would cover every combination without
>   opcode inflation.
>
> **The third option is worth serious consideration.** It also absorbs `Q-46`
> (constant-resistance, `Ohm`) and any future combination without another ICD change.

**It also settles `Q-45` favourably.** Since voltage may appear as a nominal value only *alongside*
a current or power target, an authored **voltage-only** step remains forbidden (C-6 stands), and
`CVChg` is best understood as the mode the DC-DC board enters *during* a CCCV step — recommended
option 2 in Part 00 §0.10.

### A1.3.5 Table-scaling factors `p.172, p.201`

Multiply every value in a `TABLE` nominal-value list by a factor. Order of entry is arbitrary.

| Function | Scales |
|---|---|
| `Factor_I` | Current values in the table |
| `Factor_P` | Power values in the table |
| `Factor_U` | Voltage values in the table |

### A1.3.6 Table value limiting `p.203`

Clamp a `TABLE` profile's values. Entered alongside the table name.

| Parameter | Meaning | Note |
|---|---|---|
| `Ap` | Max positive (charge) current, A | |
| `An` | Max negative (discharge) current, A | **Entered as a positive value**; limits to −An |
| `Vp` | Max (charge) voltage, V | |
| `Vn` | Min (discharge) voltage, V | Minus sign required for negative voltages |
| `Wp` | Max positive (charge) power, W | |
| `Wn` | Max negative (discharge) power, W | **Entered as a positive value**; limits to −Wn |

If a parameter is omitted, the value inside the table or the circuit's physical maximum applies.

### A1.3.7 Other nominal-value entries `p.172-173`

| Entry | Used with | Meaning |
|---|---|---|
| `OUW` | `TASK` | Activates resistance and power calculation — `1.0 OUW` after `TASK` |
| `CGRE` | `TASK` | Activate temperature-dependent relay via `IOOUT[16]` (Rel. 8) |
| `CLESS` | `TASK` | Deactivate temperature-dependent relay via `IOOUT[16]` |
| `TCONTR` | `TASK` | Switch `IOOUT[16]` as a function of temperature |
| *File name* | `TABLE`, `REG`, `FILE` | Name of a nominal-value list or test section |
| *Filter name* | `FILTER` | Functional name of the filter |
| `TIMER1`..`TIMER3` | `SET` | Program timers |
| `MaxCHAI` | `SET` | General current limit for charge operations |

---

## A1.4 Power Operators

### A1.4.1 `CHA` — Charge `p.167`

| | |
|---|---|
| **Columns** | Nominal Value, Limit |
| **Effect** | Charges at the nominal value until a limit is reached. **Charge relay is set.** |
| **ME** | → `CCChg` (current), `CPChg` (power), `CCCVChg` (V+A), `CPCV…` (Watt+V — `Q-50`) |

```
3   Chrg   CHA    15 A       > 14.1 V            2.0 A
```

### A1.4.2 `DCH` — Discharge `p.167`

| | |
|---|---|
| **Columns** | Nominal Value, Limit |
| **Effect** | Discharges at the nominal value until a limit is reached. **Discharge relay is set.** |
| **ME** | → `CCDChg`, `CPDChg`, `CCCVDChg`, `CPCV…DChg` |

**Sign convention:** discharge produces negative `Ah`/`Wh`. An upper limit on discharged capacity
must be written `< −100 Ah` `p.176`.

### A1.4.3 `RCH` — Recharge `p.167`

| | |
|---|---|
| **Columns** | Nominal Value, Limit |
| **Effect** | Identical to `CHA`, **plus** the recharge relay is set and the front-panel charge LED blinks. |
| **ME** | → charge opcode **+ recharge flag** `Q-47` |

`RCH` is `CHA` with an extra relay. Allocating four more opcodes for it would double the charge
opcode space for one boolean — hence the flag proposal.

### A1.4.4 `PAU` — Pause / Rest `p.167`

| | |
|---|---|
| **Columns** | Limit |
| **Effect** | Power contactors dropped until the limit is reached. Measurement, accumulation, limit evaluation and registration all continue. |
| **ME** | Pass-through |

**`PAU` is the only operator that may carry these limits** `p.176`:
`AhDef`, `PercAh`, `PERCCN_P`, `PERCCN_C`.

### A1.4.5 `PAUA`, `PAUO` — Pause variants

Defined in `BM_PM_BTS600_New_Operators.pdf` `p.169`. **⚠ Document not available — `Q-30`.**

### A1.4.6 `BATT` — Battery Simulator `p.169`

| | |
|---|---|
| **Columns** | Nominal Value |
| **Effect** | Presets a constant voltage with an upper and lower current limit. |
| **Units** | Voltage `V`, upper current limit `A`, **lower current limit `AG`** |
| **ME** | Pass-through — requires a distinct DC-DC control mode |

```
Nominal Value:   100 V
                 150 A       ← upper current limit
                 150 AG      ← lower current limit
```

→ constant 100 V with current regulated between −150 A and +150 A.

`p.169` notes a special `TASK` may be required to adapt to the hardware.

### A1.4.7 `EIS` — Impedance Spectroscopy `p.210`

| | |
|---|---|
| **Columns** | Nominal Value (multi-line), Limit |
| **Effect** | Performs an EIS measurement step via the EIS-Meter. |
| **ME** | Pass-through — requires EIS-Meter hardware |

| Nominal value | Opt? | Meaning |
|---|---|---|
| `ADC` | Yes | DC load current, A. **`>0` = charge, `<0` = discharge, `0` or undefined = pause** |
| `VDC` | Yes | Voltage limit. **Only valid if `ADC` is also defined**, else error |
| `mHz` / `Hz` / `kHz` | No | Measurement frequency. **Two values → a spectrum swept from lower to higher, 8 measurements per decade.** One value → single-frequency measurement |
| `AAcMax` | Yes | Max AC measuring current. Default **2 A** |
| `VAcMax` | Yes | Max voltage; EIS-Meter errors above it. Default **20 V** |
| `VAcMin` | Yes | Min voltage; EIS-Meter errors below it. Default **−2 V** |
| `mVideal` | Yes | Target voltage-response amplitude. Should be **3–10 mV/cell** for linearity; 3 mV/cell recommended. Default **10 mV** |

```
1        SET                                    EisReg
2        PAU                       5 sec
3        EIS    25.0 ADC           30 min
                4.2  VDC
                10   mHz
                6.0  kHz
                2.0  AAcMax
                4.2  VAcMax
                3.0  VAcMin
4        STO
```

### A1.4.8 `TABLE` — Nominal-Value Profile `p.199-203`

| | |
|---|---|
| **Columns** | Nominal Value (table name + optional factors/limits), Limit, Registration |
| **Effect** | Loads and executes a time-indexed nominal-value list from file. |
| **ME** | Pass-through; the profile itself is transferred as data |

**File requirements** `p.200`:

| Property | Value |
|---|---|
| Extension | `.TXT` |
| Location | `C:\Digatron\Battery Manager\Server\BTS-600\Table` |
| Name length | **≤ 8 characters** |
| Line format | `X;Y;Z;U;` |

**Registration options with `TABLE`** `p.201`:

1. No entry → automatic registration on every step change
2. A time, e.g. `5 sec`
3. A current, e.g. `1 A`
4. `Ah` or `Wh`, e.g. `1 Ah`
5. `>> time1 & time2`, e.g. `>> 1 min & 5 sec`

Automatic registration on step change can be **disabled** by a preceding step
`SET RLevel = 3.0` `p.201`.

**Standard drive-cycle tables** `p.202`:

| Table file | Test | Basis |
|---|---|---|
| `fudstbl` | FUDS — Federal Urban Driving Schedule | Relative values of peak power |
| `sfudstabl` | SFUDS — simplified FUDS | **W/kg** — factor is the active-material weight, not peak power |
| `dsttbl` | DST — Dynamic Stress Test | Relative values of peak power |

FUDS requires `TASK OUW` to compute actual power. The FUDS table contains **1372 steps**; if no
termination limit is met, **the table restarts from the beginning** `p.202`.

**ME note:** a 1372-row profile that loops indefinitely until a limit fires is a genuine
capability requirement, not an edge case. It also makes `N_TABLE_ROW_MAX` (assumption `A-25`,
proposed 1000) **too small — raise it to at least 2000.**

---

## A1.5 Program-Flow Operators

### A1.5.1 `BEG` / `CYC` — Cycles `p.168, p.213`

| Operator | Nominal Value | Meaning |
|---|---|---|
| `BEG` | Cycle **name** (optional but needed to preset the counter) | Marks cycle start |
| `CYC` | Repeat count as `n*`, or a **variable** | Marks cycle end |

```
3        BEG    Outer
4        BEG    Inner
5        CHA    10 A        > 14.4 V
6        DCH    5 A         < 10.5 V
7        CYC    5*                        ← inner runs 5×
8        PAU                12 h
9        CYC    10*                       ← outer runs 10× → inner total 50×
```

| Property | Value |
|---|---|
| Interlaced (nested) cycles | **Up to 16** `p.213` |
| Variable repeat count | Supported — `SET xyz = 10` then `CYC xyz` `p.213` |
| Counter preset | Via `SET <cyclename> = n`, using the name given after `BEG` `p.191, p.218` |
| Jumping into a cycle | Permitted, and **avoids counter resets** `p.191` |

**Counter-preset idiom** `p.217-218` — used to resume an aborted test. Because stopping a test
recompiles the program and resets all counters, the operator records the counts, then presets them:

```
1        SET    Cycle = 27
                Ah    = -53.21          ← ⚠ negative sign is essential
                Wh    = -640.5
2        PAU                30 sec
3        GOTO   Break
```

> **`p.218`, emphasised in the original:** *"Please make sure that the Ah and Wh counters show
> negative values! If you forget to enter a minus sign, the battery will be discharged from
> 53.21 Ah to −80 Ah, i.e. by another 133.21 Ah!"*

### A1.5.2 `GOTO` — Jump `p.168, p.218`

| Used as | Destination goes in |
|---|---|
| **Operator** | Nominal Value column |
| **Action** | Action column |

In both cases the destination must appear in the **Label** column of the target step.

| Rule | Detail |
|---|---|
| May enter or leave a cycle | Yes `p.218` |
| **May not cross a program level** | A `GOTO` cannot jump into or out of a procedure. **The destination must be on the same program level.** `p.218` |
| Conditional branching | Use `GOTO` in the **Action** column — several limits, several destinations `p.218` |

**Reserved label names** `p.218` — a label must not be named after a registration channel:

```
V, A, mA, Ah, mAh, AhCha, AhLad, mAhCha, mAhLad, AhDch, AhEla, mAhDch, mAhEla,
AhStep, AhPas, mAhStep, mAhPas, AhPrev, AhPrec, mAhPrev, mAhPrec, AhStat, mAhStat,
AhBal, mAhBal, Wh, mWh, WhCha, WhLad, mWhCha, mWhLad, WhDch, WhEla, mWhDch,
mWhEla, WhStep, WhPas, mWhStep, mWhPas, WhPrev, WhPrec, mWhPrev, mWhPrec
```

**This list is also the alias reference.** It reveals the German-language aliases:
`AhLad` = `AhCha` (Ladung), `AhEla` = `AhDch` (Entladung), `AhPas` = `AhStep`,
`AhPrec` = `AhPrev`.

### A1.5.3 `INT` — Interrupt `p.167`

Interrupts the program. Resumed by the front-panel start button or from the host (dispo list or
tool buttons). Execution context is preserved.

Also available as an **Action** `p.181`.

### A1.5.4 `STO` — Stop `p.168`

Terminates the program. **May appear several times in one program** — it is also used to end
sub-programs, and `ONERROR` handlers run until they reach a `STO`.

Also available as an **Action** `p.181`.

### A1.5.5 `RET` — Return `p.168`

| | |
|---|---|
| **Columns** | Action |
| **Effect** | Returns from a procedure to the main program, or from any level to the next higher level. |

### A1.5.6 `ONERROR` / `ONEXIT` — Special Labels `p.219`

| Operator | Nominal Value | Effect |
|---|---|---|
| `ONERROR` | Label name | **On any error**, jump to the step carrying that label |
| `ONEXIT` | Label name | Steps under that label execute **after the program ends** |

```
6        ONERROR   Fault
...
8        STO
9  Fault CLEAR                              ← ⚠ mandatory first step
10       DCH       20 A        10 min
11       STO
```

> **`p.219`:** the handler should be placed at the **end** of the program, because every step from
> the label to the next `STO` will execute. **A `CLEAR` is required as the first step after an
> `ONERROR` jump.**

### A1.5.7 Procedures `p.214-217`

A procedure is a named, separately stored sub-program.

| Aspect | Detail |
|---|---|
| Creation | Select a step range in the editor → right-click → *Create procedure*; give it a name and a free program number |
| Invocation | Procedure name in the **Operator** column, or in the **Action** column |
| Return | Implicit at the end, or explicit via `RET` |
| Storage | Under BTS-600 → *Procedures*; edited and managed like programs |
| Decompose / recompose | Right-click → *Procedure decomposition* / *recomposition* to view or re-collapse steps `p.216` |
| Display while running | The procedure is shown in detail; a *Show main program* button toggles the view `p.216` |
| Renumbering | Following steps renumber automatically when a procedure replaces steps `p.216` |

> **`p.217`, emphasised:** *"when you have modified a procedure, all other programs using this
> procedure will also be affected."* The manual recommends decomposing, modifying, and saving under
> a **new** name instead.

> **`p.216`, emphasised:** the procedure name and title should state which battery type it is for.
> *"Using the wrong procedures may have fatal effects on your battery!"*

---

## A1.6 Data, Variable and Registration Operators

### A1.6.1 `SET` — the multi-purpose operator `p.167, p.190-191`

`SET` is the most heavily overloaded instruction in the language. Its five distinct roles:

| Role | Form | Notes |
|---|---|---|
| **Registration format** | Format name in the Registration column | See §A1.12 |
| **Variable** | `name = value` in Nominal Value | Usable anywhere a value appears |
| **Counter preset** | `Ah = -53.21`, `Wh = -640.5` | ⚠ Sign matters — see §A1.5.1 |
| **Cycle-counter preset** | `<cyclename> = 27` | Name must match the `BEG` nominal value |
| **Timer** | `TIMER1`..`TIMER3` | Started immediately; usable as a Limit |
| **Global nominal value / limit** | See below | Applies to the whole program |

> **`p.190`, important side effect:** *"If you use a variable name that is also a channel unit in
> the attribution circuit/channel, the according channel will be set to the value assigned to the
> variable."*
>
> This is how counter presetting works — and it means **variable names silently alias channel
> names**. A variable innocently called `Ah` does not create a variable; it overwrites the
> ampere-hour counter. `p.190` describes this as "advisable" for presetting counters; it is
> equally a trap for anyone naming variables casually.

**Why variables** `p.191`: a value used repeatedly can be changed in one place, and every step
referencing it adapts. Also used to preset cycle counters.

**Global nominal values and limits** `p.191`:

| Aspect | Behaviour |
|---|---|
| Global nominal value | Affects **every** program step; values will not exceed or fall below it. The circuit regulates to the limit automatically. |
| Global limit | Performs the action set on the same line. Available actions: `ERR`, `MSG`. |
| **Global limit with no action** | **Terminates the program** as soon as it is reached. |
| Interaction with `ERR`/`MSG` | Global limits behave like local limits `p.194, p.197` |
| Cleared by | `CLEAR` — §A1.6.3 |

### A1.6.2 `REG` — Register Once `p.168, p.192`

| | |
|---|---|
| **Columns** | Nominal Value (optional section name), Registration (format) |
| **Effect** | Records the registration format **once**. |

| Nominal Value | Behaviour |
|---|---|
| A name | The registration is written to a **separate test section** of that name |
| Empty | Uses the registration format last defined by `SET` |

**Use case** `p.192`: write only end-of-cycle capacity values into a separate section, so a
capacity-fade curve can be plotted without evaluating the whole registration data set. This is a
genuinely valuable pattern for cycle-life testing.

### A1.6.3 `CLEAR` — Clear `p.170, p.203`

Deletes **all registrations and all limits entered with `SET`**, plus global registration setpoints.

> **`p.203`, emphasised:** *"The operator CLEAR does not delete the set register format! The
> register format is only changed or deactivated by a new SET operator."*

Mandatory as the first step after an `ONERROR` jump `p.219`.

### A1.6.4 `ADD` / `SUB` — Arithmetic `p.168-169`

| Operator | Effect |
|---|---|
| `ADD` | Adds the figure values of the channels named in the Nominal Value column |
| `SUB` | Subtracts the second named channel's value from the first |

```
4    ADD    Sum
            5              ← Sum: 50 → 55
5    SET    Ah = Sum       ← move the result into Ah
```

`p.169`: to move the result elsewhere, use `SET`. `Sum` may be given a unit name in the
circuit/channel attribution list.

### A1.6.5 `SAVE` / `REST` — Persist and Restore Counters `p.198`

| Operator | Effect |
|---|---|
| `SAVE` | Saves **all battery data in the current registration format** to a named test section |
| `REST` | Restores previously saved counters, so a test can continue later with all counts intact |

| Rule | Detail |
|---|---|
| Section name | In the Nominal Value column, for both operators |
| Multiple sections | Several test sections per battery are permitted |
| **Format must match** | Restoration works only for channels saved under the same name **as used in the registration format, which must be declared before `REST`** |
| **Missing data** | If the `SAVE` data does not exist in the database at `REST` time, **the running program waits or is interrupted** |

**ME note:** `REST` reads from the database — i.e. from the **Web App side**. A `REST` step
therefore creates a run-time dependency of the Primary on the Web App, which sits awkwardly against
FR-SYS-007 (a test must run without the Web App). **This needs a decision — `Q-51`.** Options:
resolve `SAVE`/`REST` data at program-transfer time; hold saved sections on the Primary; or accept
that a program containing `REST` cannot start while the link is down.

### A1.6.6 `FILE` — Name the Test Section `p.168, p.204`

Directs following registrations into a named test section.

| Rule | Detail |
|---|---|
| Name | In the Nominal Value column |
| **`Test` alone is not allowed** | `Test1`, `Test2` are fine; bare `Test` is rejected `p.204` |
| **Inside a cycle** | Using the same nominal value after `BEG` and `FILE` creates a **new test section per cycle iteration**, auto-named `TSZ00001`, `TSZ00002`, `TSZ00003`… `p.204` |

### A1.6.7 `SETMUX` — Multiplexer Channels `p.168`

Activates display of additional multiplexer channels for serial data acquisition. **⚠ `Q-33`.**

---

## A1.7 Annunciation Operators

### A1.7.1 `ERR` — Error with Interrupt `p.170, p.193-196`

| | |
|---|---|
| **As operator** | Nominal Value = message number |
| **As action** | `ERR <n>` in the Action column |
| **Effect** | Interrupts the program. Sets the **Error LED** and the **error relay output** on the circuit. Writes the message into the database with registration data. |

**Behaviour with multiple limits — read carefully** `p.194`:

> *"A single limit with ERR writes the message and goes into interrupt. After resuming, the next
> step is processed. If there are two limits in the step and the ERR limit is reached first, the
> message is written and after continuing it waits for the second limit to be reached."*
>
> *"If there are two limits, it might happen that the step is left before the ERR limit is
> reached."*

So `ERR` does **not** unconditionally end the step — with two limits it interrupts, and on resume
the step continues waiting for the other limit. This asymmetry is easy to implement wrongly.

### A1.7.2 `MSG` — Message without Interrupt `p.170, p.196-197`

Identical to `ERR` except that the program **continues**.

**Behaviour with multiple limits** `p.197`:

> *"A single limit with MSG writes the message and goes to the next step. If there are two limits
> in the step and the MSG limit is reached first, the message is written and then it waits for the
> second limit to be reached."*

### A1.7.3 Default message numbers `p.194, p.197`

| Number | Message |
|---|---|
| 1 | Limit reached! |
| 2 | Voltage limit reached! |
| 3 | Temperature limit reached! |
| 4 | Current limit reached! |
| 5 | Capacity limit reached! |
| 6 | External control signal activated! |

Editable and extensible via *Maintenance → BTS-600 Messages* `p.194-195`. Numbers and texts may
both be changed, and new entries added. **ME note:** the message table is Web-App-owned
configuration; the Primary transfers only the number.

---

## A1.8 Measurement, Range and Filter Operators

### A1.8.1 `ALIM` — Sign-Aware Current Limits `p.168, p.177`

> **`p.168`:** *"Using this operator will consider leading signs in all following program steps
> with limits using units A or mA. Without ALIM, current limits are always treated regardless of
> leading signs."*

**This is the most important trap in the language.** By default a current limit is **absolute**:

| Step | Limit | Behaviour |
|---|---|---|
| Charge | `> 10 A` | Fires above +10 A — as expected |
| **Discharge** | `> 10 A` | **Also fires at −12 A**, because 12 > 10 |

Two escapes:

| Escape | Scope |
|---|---|
| `ALIM` operator | All following steps in the program |
| `A_notAbs` / `mA_notAbs` units | The individual limit |
| Explicit negative limit, e.g. `< -20 A` | The individual limit |

**ME note:** the default absolute behaviour **must be reproduced exactly** (FR-PRI-189P).
"Fixing" it would make existing BM4 programs behave differently on ME — a worse outcome than
reproducing a documented quirk.

### A1.8.2 `FILTER` — Digital Filters `p.170, p.204`

Sets digital filters for all channels in the circuit/channel attribution.

> **`p.204`:** *"The definition of filter functions can, for the time being, only be made by
> Digatron personnel."*

**ME note:** filter *definition* is a factory/vendor function; filter *selection by name* is a
program function. Scope of the ME implementation needs confirming — `Q-52`.

### A1.8.3 `RANGE` — Measurement Range Switching `p.205-206`

| Mode | How |
|---|---|
| **Automatic** | Enabled after **every program start**. Each new step selects the range with the lowest possible boundary from the current setpoint. With no setpoint, **range 1**. |
| **Manual** | `RANGE` with nominal value **1, 2, 3 or 4** (**only 1 or 2 for IGBT systems**). Remains valid for **all following steps**. |
| **Back to automatic** | `RANGE` with nominal value **0** |

**Critical behaviours** `p.206`:

- **No automatic range switch *within* a step.**
- **No automatic range switch at all** for ramps, power nominal values, or `TABLE` steps.
- A manual higher range **persists** — selecting 200 A keeps it for later steps even if a
  subsequent setpoint is 10 A, which costs accuracy.
- Range switching runs the full current-decrease and relay run-time routine; a transit
  between ranges costs about **50 ms during which current falls to 0 A**.

> **`p.206`, on the interaction with `IRANGE`:** *"The IRANGE operator can be placed anywhere in
> the program. If that means a real change of the range, then the actual physical range switch will
> be done inside the device control board at the begin of the next step."*

**ME note:** the 50 ms zero-current transit is functionally visible — it interrupts the profile.
Programs needing fast slew rates must pin the range manually. This is a real constraint for drive-
cycle (`TABLE`) testing and needs stating in the DC-DC board requirements (Part 03).

### A1.8.4 `IRANGE`, `URANGE`, `ISOEXT`, `ISOINT`, `OUTA`, `OUTB`

Defined in `BM_PM_BTS600_New_Operators.pdf` `p.169`. **⚠ Document not available — `Q-30`.**

Partial information available: `IRANGE` is referenced at `p.206` (see §A1.8.3) and appears to be
the current-specific equivalent of `RANGE`, applied at the start of the next step.

Inferred purposes, **to be confirmed against `RD-2`**:

| Operator | Likely purpose |
|---|---|
| `IRANGE` | Current measurement/output range select |
| `URANGE` | Voltage measurement range select |
| `ISOEXT` | Isolation resistance measurement, external configuration |
| `ISOINT` | Isolation resistance measurement, internal configuration |
| `OUTA` / `OUTB` | Digital or analogue auxiliary output control |

---

## A1.9 Multi-Circuit Operators

### A1.9.1 `PARALLEL` — Parallel Circuits `p.207-209`

| | |
|---|---|
| **Columns** | Nominal Value — the participating circuit(s), one per sub-row |
| **Effect** | Parallels the master circuit with the named slaves; the nominal value is **divided** among them. |

**Limits — ME circuits** `p.207`:

| Property | Value |
|---|---|
| Max slaves per master | **15** |
| MBT circuits (for contrast) | 7 max, currents > 50 A, separate charging power units, same MBT CPU |
| TEFEU circuits | **Parallel mode not possible** |

> **⚠ The manual text for ME circuits is truncated:** *"Parallel mode is only possible for circuits
> with a common A maximum number of 15 (slave) circuits…"* — a phrase is missing after "a common".
> By analogy with the MBT rule it is probably "a common CPU" or "a common cabinet"; `p.209` refers
> to a parallel circuit "outside of the cabinet of the master" as an error, which supports
> *cabinet*. **Confirm with Digatron — `Q-43`.**

> ### ⚠ `Q-43` became blocking when the ME capacity was confirmed
>
> An ME DC-DC Converter Board has **8 channels**. `RD-1` permits a parallel group of **15 slaves +
> 1 master = 16 circuits**. **A maximum-size group therefore cannot fit on a single board** — it
> must span at least two.
>
> So one of the following is true, and we do not yet know which:
>
> | Possibility | Consequence |
> |---|---|
> | Cross-board parallel groups **are** supported | Current sharing must be coordinated across two boards over CAN-FD 1, with a common measurement time base. Materially harder than within-board sharing, and a Part 03 requirement. |
> | Groups are confined to one board | Effective ME limit is **8 circuits**, not 16, and the Web App must validate against 8. |
> | Groups are confined to one *cabinet* | Depends on how many boards a cabinet holds — a packaging question that becomes a software constraint. |
>
> This must be settled before the CAN-FD 1 ICD is fixed.

**Preconditions** `p.207`:

- Power and sense connectors of the participating circuits physically paralleled
- **All participating circuits in STOP at the moment of start**
- All connected to the **same battery**

**Behaviour:**

| Event | Result |
|---|---|
| Start with a slave not in STOP | Error message; **master switches to interrupt mode** `p.207` |
| Circuits reserved | Unavailable for individual tests until the program is stopped `p.207` |
| Starting a circuit already in parallel mode | Error message `p.207` |
| Master's program ends normally | **Master remains in STOP mode and stays active in the dispo.** Parallel mode is turned off **only by a manual stop** `p.208` |
| Multiple `PARALLEL` steps | Possible but pointless — the battery wiring would have to change mid-program `p.207` |
| With `TASK` | **`PARALLEL` must be defined before `TASK`** `p.207` |

**Documented error cases** `p.208-209` — worth reproducing, as they define required diagnostics:

| Symptom | Cause | Resolution |
|---|---|---|
| *"Current below set value!"* | Circuit outputs not connected; master cannot reach the setpoint after division | Connect the parallel circuit to the same battery and continue the master |
| Target current unreachable though correctly wired | Requested current exceeds the sum of the circuits' maxima, or the sum for the selected range | Correct the program, restart the master |
| Error on starting parallel mode | Parallel circuit outside the master's cabinet, or already running another program | Choose a circuit in the same cabinet / stop the other program |
| Cannot start a circuit individually | It is in use as a parallel slave, **or** parallel mode has finished but the master was never manually stopped | Use another circuit, or manually stop the master |

**ME note:** the fourth case — *"parallel mode might already be finished but the master has not yet
been manually stopped"* — is a real operational trap. A finished parallel run silently holds up to
15 circuits reserved until someone presses stop. The ME UI should make that state obvious
(FR-WEB-079T).

### A1.9.2 `SYNCLine` / `SYNCProgram` — Synchronisation `p.212`

| | |
|---|---|
| **Columns** | None — inserted as an independent step with no parameters |
| **Effect** | All running programs of circuits in the same **SyncGroup** are held at this step until every associated program reaches it, then all continue together. |

| Operator | Synchronises |
|---|---|
| `SYNCLine` | Any circuit in the SyncGroup, **regardless of program name or version** |
| `SYNCProgram` | Only circuits running the **same program and the same program version** |

**Configuration:** only circuits with the same value in the **SyncGroup** column of the Circuit
View are synchronised `p.212`.

> `p.212`: *"The operators SYNCLine and SYNCProgram can be used for ME- and MBC systems. The old
> operator `SYNC` does no longer exist."*

**ME note:** this is a barrier across independent programs. A barrier with no liveness check is a
hang — if one SyncGroup member is stopped or faulted, the rest wait indefinitely while appearing to
run. FR-PRI-190M requires detection and annunciation.

---

## A1.10 External-Process Operators

### A1.10.1 `TASK` — Parallel Control Process `p.168, p.192-193`

| | |
|---|---|
| **Columns** | Nominal Value — a numeric value followed by the task name |
| **Effect** | Activates a program running in parallel to the BTS program, to control relays, temperatures, and similar. |
| **Limit** | **Up to 12 parallel processes** per program `p.192` |

**Standard tasks in Battery Manager** `p.193`:

| Task | Effect |
|---|---|
| `OUW` | Activates calculation of resistance and power — entered as `1.0 OUW` after `TASK`. **Required for FUDS testing.** Channel and unit name is `Watt`. |
| `CGRE` | Activates a temperature-dependent relay via output `IOOUT[16]` (Rel. 8) |
| `CLESS` | Deactivates a temperature-dependent relay via `IOOUT[16]` |
| `TCONTR` | Uses `IOOUT[16]` to switch a relay as a function of temperature |

Selection is via the help dialog in the Nominal Value column; **a numeric value must precede the
task name** `p.193`.

`p.192`: *"activation of this process is not possible without the appropriate hardware"*, and
BTS-600 supports a multitude of `TASK` enhancements available from Digatron.

**ME note:** `TASK` is an extensible actuation mechanism. `OUW` is a pure computation and cheap to
support; `CGRE`/`CLESS`/`TCONTR` drive physical relays and require the `IOOUT` mapping to be
defined. Scope per task, not per operator — `Q-34`.

### A1.10.2 `PROT` — External Host Program `p.168`

| | |
|---|---|
| **Columns** | None — no nominal values or limits are defined in a `PROT` step |
| **Effect** | Starts an external program on the host PC, e.g. to create a protocol/report. |

| Property | Detail |
|---|---|
| Configuration | Path in `BTS-600.custom.ini`, section `[MAIN]`, key `ReportExeFile` |
| Example | `ReportExeFile=C:\Digatron\Battery Manager\SpecialProgs\Prot.exe` |
| Also accepts | `.BAT` and `.CMD` files |
| Parameters passed | `circuit name;battery name;session-ID number` — e.g. `Circ31;Batt07;61` |
| Restriction | **Not possible to start different programs / batches via one ComServer** |
| Display | *"PROT Command executed!"* appears during the run and in the measurement-data message |

**ME note:** **Web App only.** This launches a process on the operator PC; the Primary needs only
to signal that the step was reached. It is also the one operator with an obvious security
consideration — an `.ini`-configured arbitrary executable path. The ME implementation should
constrain it to a configured allow-list rather than an arbitrary path — `Q-53`.

---

## A1.11 Limits

### A1.11.1 Basic limits `p.175`

| Limit | Example | Meaning |
|---|---|---|
| Time | `10 sec`, `1.9 min`, `3.8 h`, `hh:mm:ss` | Action executes when the time elapses. **Step time, not wall-clock time** `p.175`. Integer numbers with decimals, or `hh:mm:ss`. |
| Lower | `< 10.0 K` | Channel `K` falls below 10.0 |
| Upper | `> 10.0 K` | Channel `K` rises above 10.0 |

Any registration channel may be used — physical or logical: `A`, `V`, `Ah`, `Wh`, `WhDch`, …

### A1.11.2 Special limits `p.175-181`

| Limit | Meaning |
|---|---|
| `10.0 X` *(no comparator)* | **Delta from step start.** Fires when the difference between the current value of `X` and its value at step start exceeds 10. Valid for `A`, `V`, `C`, `Ah`, `AhStep`, `WhPrev`, … |
| `&` | **AND chain.** Links this limit with the one on the next line. The action after the **last** limit fires only when **all** are met. |
| `> 10 GradT` | Temperature rise > 10 °/**hour** (channel `C`) |
| `> 10 GradTm` | Temperature rise > 10 °/**minute** |
| `GradU` / `GradUm` | Voltage rise per hour / per minute |
| `GradI` / `GradIm` | Current rise per hour / per minute |
| `deltaV` | `dChannel/dt` on the voltage channel or a `GREAL` channel — see §A1.11.4 |
| `Timer1`..`Timer3` | Ends the step when the named timer elapses. **If the timer expires before the step is reached, the step is skipped** `p.176` |
| `> 1.0 AhDef` | Defines the present Ah value as **100 %** reference. **Ends the step immediately.** `1.0` = 100 %. Stored in `GREAL[400]` as an **unsigned** absolute value `p.180`. **`PAU` only.** |
| `> 80 PercAh` | Ends the step when the Ah counter reaches 80 % of the `AhDef` reference. **`PAU` only.** |
| `PERCCN_P` | Ends the step when the capacity at the end of the **previous** step reached the given % of nominal capacity `CNom`. **Sign-insensitive.** **`PAU` only.** |
| `PERCCN_C` | Ends the step when the capacity of the **present** step reaches the given % of `CNom`. **Sign-insensitive.** **`PAU` only.** |
| `A_notAbs` / `mA_notAbs` | **Sign-aware** current limits — see §A1.8.1 |
| `WATT` | Power computed from actual voltage and current |
| `OHM` | Resistance computed from actual voltage and current |
| `VNC` | Voltage referred to the nominal voltage of one cell |
| `ACN5` | Current referred to nominal capacity at the 5-hour discharge rate |
| `VBATT` | Customer option — actual battery voltage |
| `ABATT` | Customer option — switch functions |

> **`p.176`:** *"The special Limits AhDef, PerAc, PERCCN_P and PERCCN_C can be set only while using
> the pause operator (PAU)."*

> **`p.180`:** `PERCCN_P` and `PERCCN_C` have **nothing to do with** `AhDef`. They work from the
> battery value `CNom` and the `Ah` value, which must be appropriately assigned before the steps
> using them.

### A1.11.3 `&`-linked limits — detail `p.190`

```
3    CHA    10 A       < 0.5 A    &
                       > 10 h     &
                       > 100 Ah    INT      ← action on the LAST line
```

> **`p.190`:** *"To trigger an action on an &-linked limit, the entry in the Action column must be
> at the height of the last limit condition."*
>
> *"Limit channels collected by a unit (e.g. `> 12 V1`) are ANDed in connection with an &-linked
> limit."*

The second note is subtle: a limit against a **unit group** (all channels sharing unit `V1`) is
itself an AND across those channels when used in an `&` chain.

### A1.11.4 `deltaV` — detail `p.178-179`

Implements `dChannel/dt` for the standard voltage channel or for `GREAL` channels.

**Global variables used:**

| Variable | Purpose |
|---|---|
| `GTIMER[1]` | Timer |
| `GREAL[100]` | `TimeIdx` — time base in **ms** between two measuring points |
| `GREAL[101]` | `ChanIdx` — which `GREAL` channel to start with |
| `GREAL[102]` | `ChanNum` — number of channels |

| Usage | Behaviour |
|---|---|
| **Standard** | Globals not needed. Monitors the voltage at the sense input. `< -0.5 deltaV` → step ends if voltage is more than 0.5 V **below maximum**. `> 1.5 deltaV` → ends if more than 1.5 V above maximum. |
| **With gradient analysis** | If `TimeIdx` is set, it is the time base. `< -0.5 deltaV` → ends if the channel's rise is negative and smaller than 0.5. |
| **Multi-channel** | Monitors `GREAL[100]`–`GREAL[119]`, selected by `ChanIdx` and `ChanNum` |

**Note the sign convention** `p.179`: *"−0.7 is smaller than −0.5."*

### A1.11.5 Gradient limits — mechanism `p.177-178`

For `GradUm`: at step start the present voltage `Y` is captured as reference `X`. Then for one
minute, **with every new registration value — i.e. every 100 ms** — `GradUm = Y − X` is computed.
A **new reference point is set every minute** (`X = Y`), resetting `GradUm` to 0. `GradU` re-bases
**every hour** instead.

> **`p.177`:** *"These limits are only usable after a device adjustment and only for a maximum of
> one channel. Please contact Digatron for details."*
>
> **`p.178`:** *"The check of these limits takes place with the maximum measuring speed, i.e. every
> 100 ms."*

**ME note:** the 100 ms figure describes **Digatron's own BTS-600 hardware**, and applies to how
*this one limit type* is checked on their product. It is **not** a requirement on ME and must not be
read as one. The ME measurement update rate is a separate, undecided decision — `Q-62`. What this
paragraph *does* tell us is that gradient limits need a defined and documented sampling basis,
whatever rate ME chooses. The one-channel restriction also needs confirming for ME — `Q-40`.

### A1.11.6 Signed limits — reminder `p.176`

> *"When the limit is a timer that can also accept negative values such as Ah or Wh, please observe
> the respective sign. An upper limit for the discharged capacity should be entered such as
> `< -100 Ah`."*

---

## A1.12 Actions `p.181`

| Action | Effect |
|---|---|
| *(blank)* | The **next** test step is executed |
| `INT` | The program is **interrupted** |
| `STO` | The program is **stopped** |
| `GOTO <dest>` | Jump to the step whose **Label** column contains `<dest>` |
| *Procedure name* | The named procedure is loaded and executed; then the **following** program step is activated. After the procedure completes, execution continues at the next step. |
| `ERR <n>` | Message in registration data **with** program interruption — §A1.7.1 |
| `MSG <n>` | Message in registration data **without** interruption — §A1.7.2 |

---

## A1.13 Registration

### A1.13.1 Registration triggers `p.183`

| Type | Example | Meaning |
|---|---|---|
| **Delta** | `2.0 A` | Register when the current changes by 2 A |
| **Delta (time)** | `5 min` | Register every 5 minutes |
| **Threshold** | `< 0.5 A` | Register when current falls below 0.5 A |
| **Threshold** | `> 50.0 C` | Register when temperature exceeds 50 °C |
| **Range** | `>> 1 h & 2.0 V` | Register each 2 V change, starting 1 hour after step begin |
| **Range** | `<< -100.0 Ah & 1.0 C` | Register each 1 °C change, after 100 Ah discharged |
| **Maximum count** | `15 *` | Register exactly 15 times at highest A/D resolution — **0.1 s intervals** |
| **Maximum count (conditional)** | `>> 10000 Watt 5*` | Register 5 times at highest resolution once 10 kW is exceeded |
| **Maximum count (delayed)** | `>> 30 sec 1*` | Register **once**, exactly 30 s after step begin |

Several types may be **combined** in one step `p.183`.

| Special entry | Meaning |
|---|---|
| *Registration format name* | Only with `SET`, `CHA`, `DCH`, `RCH`, `PAU`, `REG`. Defines the format used. |

### A1.13.2 Defaults and `RLevel`

| Rule | Detail |
|---|---|
| **Blank Registration column** | Data is **still registered at the beginning and end of the step** `p.183` |
| **No registration format set** | **Only current and voltage are recorded** `p.161` |
| Disable auto-registration on step change | A preceding step `SET RLevel = 3.0` `p.201` |

> **`p.183`:** *"Please observe that the quantity of registered data effects the time needed for
> subsequent test evaluations. Carefully consider which registration rate to set."*

### A1.13.3 Registration formats `p.163`

| Format | Channels | Bytes/record |
|---|---|---|
| `SIMPLE` | `A`, `V` | 12 |
| `STANDARD` | `A`, `V`, `C`, `Ah`, `AhStep`, `Wh`, `WhStep` | 42 |
| `CHANREG` | `A`, `V`, `C`, `Ah`, `AhStep`, `Wh`, `WhStepV`, `V1` | 42 + 6 × logger channels |
| `CHAREG` | `A`, `V`, `C`, `Ah`, `AhCha`, `AhStep`, `Wh`, `WhCha`, `WhStep` | 54 |
| `DCHREG` | `A`, `V`, `C`, `Ah`, `AhDch`, `AhStep`, `Wh`, `WhDch`, `WhStep` | 54 |
| `CYCLE` | `A`, `V`, `C`, `Ah`, `AhBal`, `AhStat`, `AhCha`, `AhDch`, `AhStep`, `AhPrev`, `Wh`, `WhCha`, `WhDch`, `WhStep`, `WhPrev` | 90 |
| `CYCLEC` | as `CYCLE` + `V1` | 90 + 6 × logger channels |
| `GSM` | `A`, `A_Diff`, `A_Low`, `Ah`, `V`, `V_Drop`, `V_High`, `V_Low` | 48 |

Formats may be **created** and **modified** by selecting units `p.163-164`.

> **`p.164`, emphasised:** *"registration formats are used for different programs. Modifications
> will, of course, affect all programs using this format. In case of doubt, rather create a new
> format!"*

**ME sizing note:** `CYCLE` at 90 bytes/record, **64 circuits**, 1 record/s ≈ **497 MB/day**. Over a
30-day test that is **~15 GB** — roughly half the ME platform's 32 GB. This is the calculation that
drives `S_BUFFER` and the retention policy; see Part 00 §0.7.9 for the per-format table.

### A1.13.4 Registration channels (units) `p.163`

**Physical channels:**

| Unit | Quantity |
|---|---|
| `A`, `mA` | Current |
| `V`, `mV` | Voltage |
| `C` | Temperature |

**Logical channels:**

| Unit | Definition |
|---|---|
| `Ah`, `Wh` | Total ampere-hours / energy. **Negative when discharge predominates.** |
| `AhStep`, `WhStep` | Of the **current** program step |
| `AhPrev`, `WhPrev` | Of the **previous** program step |
| `AhCha`, `WhCha` | Of **all charge steps** so far |
| `AhDch`, `WhDch` | Of **all discharge steps** so far |
| `AhStat` | Total Ah **with charge factor**: `AhStat = AhCha / chargeFactor − AhDch` |
| `AhBal` | `AhCha − AhDch`, **clamped to ≥ 0** |

**Aliases** — from the reserved-label list at `p.218`:

| Canonical | German alias | Milli- forms |
|---|---|---|
| `AhCha` | `AhLad` | `mAhCha`, `mAhLad` |
| `AhDch` | `AhEla` | `mAhDch`, `mAhEla` |
| `AhStep` | `AhPas` | `mAhStep`, `mAhPas` |
| `AhPrev` | `AhPrec` | `mAhPrev`, `mAhPrec` |
| `AhStat` | — | `mAhStat` |
| `AhBal` | — | `mAhBal` |

The same pattern applies to the `Wh` family: `WhLad`, `WhEla`, `WhPas`, `WhPrec` and their
`mWh` forms.

**Other:** `V1` (logger channel group), `GREAL[n]` (global reals), `GTIMER[n]` (global timers),
`Watt` (from `TASK OUW`), `Sum` (from `ADD`/`SUB`).

**⚠ `AhStat` is the only channel that depends on a battery-definition parameter** (the charge
factor). If the charge factor is not transferred to the Primary, `AhStat` is silently wrong in
every log.

---

## A1.14 Traps and Compatibility Notes

The behaviours most likely to be implemented wrongly. Every one is documented in the manual and
several are printed there as warnings.

| # | Trap | Ref |
|---|---|---|
| 1 | **Current limits are absolute by default.** `> 10 A` fires at −12 A. Escapes: `ALIM`, `A_notAbs`. Must be reproduced exactly. | `p.177` |
| 2 | **Accumulator presets need a negative sign.** `SET Ah = 80` instead of `-80` discharges the pack by a further 133 Ah. | `p.218` |
| 3 | **`ERR`/`MSG` with two limits does not end the step.** It annunciates and, on resume, waits for the other limit. | `p.194, 197` |
| 4 | **`CLEAR` does not clear the registration format** — only `SET` changes that. | `p.203` |
| 5 | **`ONERROR` handlers require `CLEAR` as their first step.** | `p.219` |
| 6 | **A blank Registration column still logs at step start and end.** | `p.183` |
| 7 | **With no registration format, only `A` and `V` are logged.** | `p.161` |
| 8 | **A global limit with no action terminates the program.** | `p.191` |
| 9 | **A `SET` variable named after a channel unit overwrites that channel.** | `p.190` |
| 10 | **A timer that expires before its step is reached causes the step to be skipped.** | `p.176` |
| 11 | **`AhDef`/`PercAh`/`PERCCN_*` are `PAU`-only.** | `p.176` |
| 12 | **`PERCCN_*` is unrelated to `AhDef`** — it uses `CNom` directly. | `p.180` |
| 13 | **No auto range switch within a step, or for ramps / power / `TABLE`.** A manual range persists across steps. | `p.206` |
| 14 | **Range transit costs ~50 ms at zero current.** | `p.206` |
| 15 | **`GOTO` may not cross a program level** (into or out of a procedure). | `p.218` |
| 16 | **Labels may not be named after registration channels.** | `p.218` |
| 17 | **Editing a procedure changes every program that uses it.** | `p.217` |
| 18 | **Editing a registration format changes every program that uses it.** | `p.164` |
| 19 | **A finished `PARALLEL` run holds its slaves reserved until the master is manually stopped.** | `p.208` |
| 20 | **`CHA`/`RCH`/`DCH` must never carry a voltage nominal value alone.** | `p.171` |
| 21 | **The FUDS table restarts from the beginning** if no limit fires within its 1372 steps. | `p.202` |
| 22 | **`REST` waits or interrupts if the saved data is not in the database.** | `p.198` |
| 23 | **`FILE` with bare name `Test` is not allowed.** | `p.204` |
| 24 | **`PARALLEL` must precede `TASK`** where both are used. | `p.207` |

---

## A1.15 Open Questions Arising from This Extract

New questions found while producing this document. Existing questions are in Part 00 §0.10.

| ID | Question | Blocks | Priority |
|---|---|---|---|
| `Q-50` | **The opcode set does not cover all documented nominal-value combinations.** `p.172` documents both `V + A` (CCCV) and `Watt + V` (CP→CV). The four-opcode charge set has no power-plus-voltage member, and no member for `Ohm`. **Recommendation: replace the enumerated opcode with two mode fields — primary regulation mode + transition mode — which covers every combination, absorbs `Q-46`, and needs no future ICD change.** | WEB, PRI, SEC | **High — ICD blocking** |
| `Q-51` | **`SAVE`/`REST` read and write the database, which lives on the Web App side.** A program containing `REST` therefore depends on the Web App at run time, against FR-SYS-007. Resolve at transfer time, hold sections on the Primary, or accept that such programs cannot start with the link down? | PRI, WEB | High |
| `Q-52` | `FILTER` definition is stated to be a Digatron-only function `p.204`. Is ME to implement filter *definition*, or only *selection* of pre-defined filters by name? | PRI, SEC | Medium |
| `Q-53` | `PROT` launches an arbitrary executable from an `.ini` path. Recommend constraining ME to a configured allow-list rather than an arbitrary path. Confirm. | WEB | Medium |
| `Q-54` | `TASK` supports up to 12 parallel processes with vendor-extensible task types. Which standard tasks are in ME scope — `OUW` only, or the relay/temperature set (`CGRE`, `CLESS`, `TCONTR`) too? The latter requires the `IOOUT` mapping to be defined. | PRI, WEB | Medium |
| `Q-55` | `A-25` proposed `N_TABLE_ROW_MAX = 1000`. The FUDS table is **1372 steps** `p.202`. **Raise to at least 2000.** | PRI, WEB | Low — but fix now |
| `Q-56` | `GREAL[]` and `GTIMER[]` are exposed to programs as global arrays (`GREAL[100..119]`, `GREAL[400]`, `GTIMER[1]`). How many globals must ME support, and are they per-circuit or system-wide? | PRI | Medium |
| `Q-57` | `MaxCHAI` (general charge-current limit, `p.173`) overlaps with the per-circuit safety limits of FR-PRI-041. Is it a program-level convenience, or a distinct mechanism? | PRI, WEB | Medium |

---

## A1.16 Source Page Map

For verification against `BM_Manual_eng 2.pdf`.

| Manual section | Pages | Content | This annex |
|---|---|---|---|
| 12.4.1 | 161–164 | Registration formats, channels/units | §A1.13.3, §A1.13.4 |
| 12.4.2 | 167–170 | **Operator tables** (standard + special) | §A1.2, §A1.4–A1.10 |
| 12.4.3 | 170–173 | Nominal values / functions | §A1.3 |
| 12.4.4 | 174–181 | **Limits** | §A1.11 |
| 12.4.5 | 181–183 | Actions | §A1.12 |
| 12.4.6 | 183 | Registration triggers | §A1.13.1 |
| 12.4.7 | 184–190 | Program examples (incl. `&`-linked limits, p.190) | §A1.11.3 |
| 12.4.8.1 | 190 | `SET` variables | §A1.6.1 |
| 12.4.8.2 | 191 | Global nominal values / limits | §A1.6.1 |
| 12.4.8.3 | 192 | `REG` as program step | §A1.6.2 |
| 12.4.8.4 | 192–193 | `TASK` | §A1.10.1 |
| 12.4.8.5 | 193–196 | `ERR`, message table | §A1.7.1, §A1.7.3 |
| 12.4.8.6 | 196–197 | `MSG` | §A1.7.2 |
| 12.4.8.7 | 198 | `SAVE` | §A1.6.5 |
| 12.4.8.8 | 198 | `REST` | §A1.6.5 |
| 12.4.8.9 | 199–203 | `TABLE`, factors, FUDS/DST, value limiting | §A1.4.8, §A1.3.5, §A1.3.6 |
| 12.4.8.10 | 203 | `CLEAR` | §A1.6.3 |
| 12.4.8.11 | 204 | `FILTER` | §A1.8.2 |
| 12.4.8.12 | 204 | `FILE` | §A1.6.6 |
| 12.4.8.13 | 205–206 | `RANGE`, `IRANGE` | §A1.8.3 |
| 12.4.8.14 | 207–209 | `PARALLEL` + error cases | §A1.9.1 |
| 12.4.8.15 | 210 | `EIS` | §A1.4.7 |
| 12.4.8.16 | 212 | `SYNCLine` / `SYNCProgram` | §A1.9.2 |
| 12.5.1 | 213 | Cycles | §A1.5.1 |
| 12.5.2 | 214–217 | Procedures | §A1.5.7 |
| 12.5.3 | 217–218 | Counter settings, `GOTO` | §A1.5.1, §A1.5.2 |
| 12.5.4 | 219 | `ONERROR`, `ONEXIT` | §A1.5.6 |

**Not extracted** — outside the BTS-600 program language:

| Pages | Content |
|---|---|
| 156–160 | Comment lines, help dialogs, battery parameters usage |
| 184–190 | Worked program examples 1–11 (illustrative; example 11 extracted) |
| 220–235 | §13 MF-2000 programs — **different operator set, not ME** |
| 236–244 | §14 BTS-500 programs — **different operator set, not ME** |
| 245–275 | §15 PLT programs |
| 276–285 | §16 BAFOS programs |
| 286–290 | Reports, database archive viewer |

**⚠ Referenced but not available:** `BM_PM_BTS600_New_Operators.pdf` (`RD-2`) — defines `PAUA`,
`PAUO`, `URANGE`, `IRANGE`, `OUTA`, `OUTB`, `ISOEXT`, `ISOINT`. **`Q-30`.**

---

## A1.17 Revision History

| Ver | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-05 | Embedded SW Architecture | Initial extract from `BM_Manual_eng 2.pdf` §12, pages 156–219. All 42 BTS-600 operators catalogued with semantics, examples, and ME implementation notes. 24 documented traps collected. New questions `Q-50`…`Q-57`. |
