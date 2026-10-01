# Software Requirements Specification — ME Secondary Board

**Document ID:** ME-SRS-SEC-001 · **Version:** 0.1 (draft) · **Date:** 2026-07-28
**Standard:** ISO/IEC/IEEE 29148:2018
**Target:** ME Secondary Board — STM32 microcontroller, single core, bare-metal or RTOS
(part number not yet specified — `<TBD-S01>`)
**Companion documents:** `02A_SRS_ME_Secondary_Annex_Traceability_v0.1.md` (§6 Traceability) ·
`01_SRS_ME_Primary_Board_v0.1.md` · `03_ICD_ME_Interfaces_v0.1.md` ·
`00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` (gate document)

> **Reading note — reserved decisions.** Every requirement in this document is written
> protocol-agnostically. No requirement states or implies how information is encoded or
> framed on the CAN link, what the byte layout of the control data is, or how the
> Primary and Secondary divide the work of deciding when a step ends. Those are
> decisions **D-03** (content of the minimum necessary control data) and **D-04** (CAN
> layer details), reserved to the software architect.
>
> **No §7.** The Primary SRS carries a §7 *Core Allocation* because the i.MX 8M Plus is
> heterogeneous and **D-01** is open. The Secondary Board is a single-core MCU, so no
> allocation question exists and §7 is deliberately omitted. §7 below records that
> omission rather than renumbering the remaining sections, so that section numbers align
> across the two SRS documents.

---

## 1. Introduction

### 1.1 Purpose

This document specifies the software requirements for the **ME Secondary Board**. The
Secondary Board is the regulating actuator of the ME battery test system: it receives
control data from the Primary Board over CAN, drives one battery's transistor bank to
the commanded current, voltage or power, measures the battery and the bank, protects
itself and the battery locally, and reports measurements, registration data and faults
back to the Primary.

The document is written for the software architect, the implementation team, the test
team and the client's review authority. It is a **complete functional specification of
intent**; it deliberately stops short of design.

### 1.2 Scope

**In scope.** All Secondary-Board-resident software behaviour: startup, initialization
and self-test; safe default outputs; the operating state machine; node identity and CAN
addressing; command reception, validation and rejection; the charge and discharge
regulation loops (CC, CV, CP and their combinations); setpoint application, ramping and
slew limiting; charge↔discharge switch-over sequencing; measurement acquisition and
derived-quantity computation; auto-ranging; local protection and interlocks; autonomous
safe-state entry on fault or loss of Primary communication; watchdog and
communication-timeout behaviour; telemetry and registration reporting; fault reporting
and code semantics; calibration; non-volatile configuration and persistence; firmware
update over CAN; local indication via relays, digital outputs and LEDs; and diagnostics
and self-monitoring.

**Out of scope — and this is the central scope statement of the document.**

| Not specified here | Where it belongs | Why |
|---|---|---|
| Program storage, parsing and decoding | `ME-SRS-PRI-001` §4.4, §4.5 | The Primary owns the program (§2.1) |
| Step sequencing, cycles, nesting, branching, procedures | `ME-SRS-PRI-001` §4.7 | The Primary owns the step engine (§2.1) |
| The BTS-600 operator catalog and its semantics | `ME-SRS-PRI-001` §4.7 | Decoded on the Primary; the Secondary never sees an operator mnemonic |
| Registration *triggering* rules and registration *format* definitions | `ME-SRS-PRI-001` §4.10, §4.15 | The Primary decides when and what to record; the Secondary supplies the sample |
| Which party evaluates step-ending (cut-off) conditions | **D-03** | Reserved. §4.5.4 states the requirement both ways round without presupposing the split |
| Wire formats, CAN IDs, framing, bit rate, addressing scheme | `ME-ICD-001`; **D-04** | Reserved |
| Web Application behaviour; the IF-A interface | `ME-SRS-PRI-001`; `ME-ICD-001` | The Secondary has no Web Application link |
| Hardware design; transistor-bank electrical design | Client hardware specification | Not a software requirement |

**A note on the legacy Secondary firmware.** `CODE-S` (`BTS_SEC_FW_V201`) contains a
complete step-execution engine — `stepData.c` (operator parsing, cut-off conditions,
registration parameters, TABLE rows), `cycleTable.c` (16 cycles, nesting depth 4),
`btsSecApp.c` (step execution). **None of this is carried forward.** It is cited in this
document only where it is evidence of a *measurement, regulation, protection or
persistence* behaviour that the Secondary retains, and as **prior art for D-03**. Where
this document cites `CODE-S`, it never cites it as authority for step sequencing.

### 1.3 Product identity — "ME"

**"ME" is the name of this new Ador project.** It is *not* the Digatron "ME" circuit type
that appears in the Battery Manager manual. The consequences are stated in
`ME-SRS-PRI-001` §1.3 and apply identically here. For the Secondary specifically:

- `BM` is used as the **authoritative functional reference** for what a regulated
  charge/discharge circuit must do (CC/CV crossover behaviour, ramp semantics, range
  transit time, limit-check rate, error and message semantics). It is **not** a binary
  or wire compatibility contract.
- The manual's per-circuit behaviour is adopted for functional equivalence; nothing in
  this document requires interoperability with Battery Manager.

### 1.4 Definitions

| Term | Definition |
|---|---|
| **Secondary Board** | The board specified by this document. Called the "Digital Controller Transcard" / "DSP controller" in **HW**. |
| **Primary Board** | The ME system controller (`ME-SRS-PRI-001`). Called the "Main Controller" in **HW**. |
| **Channel / Circuit** | One test circuit. One Secondary Board = one channel (A-01). |
| **Transistor bank** | The linear power stage the Secondary drives via its analog reference output. |
| **Single transistor bank** | One bank used for both charge and discharge; a contactor selects the mode. Reference output unipolar (*the control voltage only ever goes one way, 0 V upward*). |
| **Dual transistor bank** | Separate NPN (charge) and PNP (discharge) banks. Reference output bipolar (*the control voltage swings both positive and negative, and its sign selects charge or discharge*). |
| **Control data** | The minimum necessary information the Primary sends the Secondary to make it charge or discharge. Content = **D-03**. |
| **Setpoint** | A commanded regulation target: current, voltage or power. |
| **Safety limit** | A bound supplied to or configured in the Secondary, whose breach causes local protective action. |
| **Cut-off condition** | A condition that ends a program step — *for example "stop when the battery reaches 14.4 V"*. Owned by the Primary; whether the Secondary evaluates any of them is **D-03**. |
| **Regulation mode** | CC, CV, CP, or a defined combination (CCCV, CCCP, CPCV, CCCPCV). *In plain terms: which quantity the board holds steady — the current (CC), the voltage (CV), or the power (CP). A combination holds one until a limit is reached, then switches to the next.* |
| **Safe state** | The channel condition in which no energy is transferred to or from the battery: analog reference output at zero and mode contactors in their default positions. Exact required output configuration is `<TBD-S02>` — Open Issue **#8**. |
| **Registration** | A recorded measurement sample delivered to the Primary for logging. |
| **Registration record** | The set of measured and derived quantities in one registration sample, selected by the registration type mask. |
| **Auto-ranging** | Switching the current-measurement gain so that a small current is measured on a more sensitive range. *In plain terms: like changing the scale on a multimeter so a small current is still read accurately.* |
| **LNT** (`Vrect+`) | Voltage across the transistor bank in charge mode. |
| **ZNT** (`Vrect−`) | Voltage across the transistor bank in discharge mode. |
| **Power connection** | The high-current battery connection, from which terminal voltage is also sensed. |
| **Sense connection** | The separate four-wire battery voltage sense connection. |
| **Latched fault** | A fault that persists after its cause clears, until explicitly cleared. |
| **Exceed counter** | A consecutive-detection counter that must reach a threshold before a condition is declared, used to reject transients. *In plain terms: the board must see the same bad reading several times in a row before it believes it, so electrical noise does not trip the test.* |

### 1.5 Acronyms

`ADC` Analog-to-Digital Converter · `AD590` Current-output temperature sensor ·
`CAN` Controller Area Network · `CAN-FD` CAN with Flexible Data-rate · `CC` Constant
Current · `CCCV` Constant Current then Constant Voltage · `CP` Constant Power ·
`CRC` Cyclic Redundancy Check · `CV` Constant Voltage · `DAC` Digital-to-Analog
Converter · `DI` Digital Input · `DO` Digital Output · `EEPROM` Electrically Erasable
Programmable Read-Only Memory · `HIL` Hardware-In-the-Loop · `MCU` Microcontroller Unit ·
`NVM` Non-Volatile Memory · `PFA` Power-Fail Action · `PID` Proportional-Integral-
Derivative (*the standard control method: it corrects using the present error, the accumulated past error, and how fast the error is changing*) · `POST` Power-On Self-Test · `PT100`/`PT1000` Platinum resistance
thermometer, 100 Ω / 1000 Ω at 0 °C · `RTD` Resistance Temperature Detector (*a sensor whose electrical resistance changes with temperature*) ·
`RTOS` Real-Time Operating System · `SoC` System on Chip

### 1.6 References

| Tag | Document |
|---|---|
| **HW** | `Digital_Controller_Specs_V1.04_RemarkAdded-10-Feb-2026 (1) - Copy.xlsx` — client hardware specification for **this board**. Sheets: *Version*, *Digital Controller*, *Error code*, *Configuration*, *Calibration*, *Coding Guidelines*, *Queries & Implement*. **This is the top-precedence source for the Secondary Board.** |
| **BM** | `BM_Manual_eng 2.pdf` — Digatron Battery Manager User Manual 99.3, rel. 2022-2, 363 pp. **Ch.12 BTS-600 Programs, pp.156–219** is the functional baseline |
| **LSRS** | `BTS_Primary_SW_Requirement_Analysis V1.7.xlsx` — legacy combined Primary+Secondary SRS, 281 requirements. Reference only |
| **CODE-S** | `D:\Projects\BTS_VS_CODE\BTS_SEC_FW_V201` — legacy Secondary firmware (STM32H7 / H723). Evidence only; see the note in §1.2 |
| **CODE-P** | `D:\Projects\BTS_VS_CODE\BTS_Primary_SOM` — legacy Primary firmware (SAM9X60). Evidence only |
| **WAD** | `WebAppDocs/` — existing Ador BTS server documentation set. Bears on the Secondary only indirectly, through the Primary |
| **PSPE** | Prior-art partition proposal for the Primary/Secondary split. **Recorded as evidence only; not adopted** (A-06) |
| **GATE** | `00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` — source-coverage, open-issue and conflict registers |
| **PRI** | `01_SRS_ME_Primary_Board_v0.1.md` — the companion Primary SRS |
| **NEW — ME** | Requirement arises from the ME architecture with no precedent in any source |

**Precedence when sources conflict:** HW > BM > LSRS. Conflicts are never resolved
silently; each is carried in the conflict register (§8.2) with a `<TBD-Snn>` marker where
a value cannot be determined.

**Where the hardware specification conflicts with itself** — and it does, in several
places, because its remark columns are dated two years after its data columns —
precedence cannot resolve the conflict. Those items escalate to Open Issues rather than
being decided in this document.

**Legacy code status.** Legacy firmware is treated strictly as *evidence of implemented
intent to be confirmed*, never as a requirement. ME is a new project; the old code shows
only how a feature was previously realised, and in the case of step execution it shows a
role the Secondary no longer has.

### 1.7 Requirement conventions

- **ID:** `SRS-SEC-<AREA>-<NNN>` where `<AREA>` is the coverage-checklist code from
  `GATE` §2 (`S1`…`S18`), or `HW`/`SW`/`CM`/`UI` in §3 and `NF` in §5.
- **"shall"** denotes a binding requirement. One requirement per statement.
- **`Source`** cites document + section, a legacy code artefact, or `NEW — ME`.
- **No `Alloc` column.** Single-core target; see the reading note.
- **`<TBD-Snn>`** marks a numeric value or policy that could not be sourced. The `S`
  namespace is deliberate: it prevents collision with the Primary SRS's `<TBD-nn>` tags,
  which are a different register. Every `<TBD-Snn>` is listed in §8.3, and each states
  the conflict or open issue it descends from.
- **Open Issue #N** references the shared register in `GATE` §4, reproduced in §8.1.
- **C-nn** references the shared **conflict** register in `GATE` §5, reproduced in §8.2.
- **CN-nn** references a **design constraint** in §2.6. This document uses the `CN`
  prefix for constraints because `C-nn` is already the conflict namespace. *Note for the
  Primary SRS: `01_…_v0.1.md` uses `C-nn` for both its §2.6 constraints and its §8.2
  conflicts. That collision should be resolved in its v0.2; it is not touched here.*
- **⚠ hardware-critical.** Requirements that govern hardware register access, real-time
  control loops, interrupt timing, safety interlocks, boot code or power sequencing are
  marked **⚠**. Every **⚠** requirement requires hardware-in-the-loop verification and
  explicit engineering approval before implementation; see §5.7.

---

## 2. Overall Description

### 2.1 Product perspective

The ME system replaces the BTS system. Three changes drive this specification; the third
is the one that reshapes this document.

| # | BTS | ME |
|---|---|---|
| 1 | Primary = SAM9X60 SOM **+ separate STM32H7 COM Controller** | Primary = single i.MX 8M Plus; **no COM Controller** |
| 2 | 1 Primary : **1** Secondary, over **UART** (two UARTs: command + registration) | 1 Primary : **up to 8** Secondaries, over a **single shared CAN bus** |
| 3 | Step sequencing executed **on the Secondary** | Step sequencing executed **on the Primary**; the Secondary receives minimum control data |

**Consequence of change 3 for this board.** The Secondary loses program storage, step
parsing, operator interpretation, cycle and branch control, and registration-trigger
logic. It keeps — and must do better than before — measurement, regulation, protection,
persistence and reporting. Its software becomes smaller in function count and more
demanding in timing and integrity.

**Consequence of change 2 for this board.** Three things follow from replacing a
point-to-point UART pair with one shared multi-drop bus:

1. The Secondary must have an **identity** and must ignore traffic addressed elsewhere.
   In BTS a `circuitId` byte existed but was decorative on a 1:1 link; in ME it is
   load-bearing.
2. The legacy **separate registration channel disappears.** `CODE-S` used `huart4` for
   commands and `huart5` exclusively for registration data. In ME both share one CAN
   bus with seven other nodes. This is the origin of Open Issue **#18**.
3. Bus bandwidth is now a **shared, contended resource**, so the Secondary's reporting
   rate is no longer its own decision — see §4.12 and §5.1.

### 2.2 System context

```
                    ┌──────────────────────────┐
                    │      PRIMARY BOARD       │
                    │      MIMX8ML8CVNKZAB     │
                    └────────────┬─────────────┘
                                 │  IF-B  (CAN, shared bus; details = D-04)
                                 │  control data, commands, config, calibration
                                 │  ◄── telemetry, registration, faults, identity
        ┌──────────┬──────────┬──┴───────┬──────────────────┐
        │          │          │          │                  │
   ┌────┴───┐ ┌───┴────┐ ┌───┴────┐     ...          ┌─────┴──┐
   │ SEC 1  │ │ SEC 2  │ │ SEC 3  │                  │ SEC 8  │
   └────┬───┘ └────────┘ └────────┘                  └────────┘
        │
        │  ── THIS DOCUMENT SPECIFIES ONE OF THESE ──
        │
   ┌────┴──────────────────────────────────────────────────┐
   │  analog reference out ──► transistor bank base drive  │
   │  charge relay ──────────► charge contactor            │
   │  discharge relay ───────► discharge contactor         │
   │  DO4…DO11 (opto) ───────► external relays  (#19)      │
   │  DO1 MODE_FAILURE ──────► error relay + Error LED     │
   │  ◄── battery V sense (4-wire, 24-bit)                 │
   │  ◄── shunt mV (24-bit, auto-ranged)                   │
   │  ◄── U terminal (power connection)                    │
   │  ◄── LNT / ZNT (bank voltage)                         │
   │  ◄── heatsink temperature                             │
   │  ◄── battery temperature (PT1000 / AD590)             │
   │  ◄── DI1…DI4 (spare) + thermostat                     │
   └───────────────────────────────────────────────────────┘
                              │
                          battery under test
```

### 2.3 Hardware constraint envelope

From **HW**, which is the specification *of this board*. Unlike the Primary — whose only
hardware source is a SoC datasheet — the Secondary has a genuine board-level
specification. It is nonetheless internally inconsistent in the places noted.

| Resource | Provision (HW) | Conflict / issue |
|---|---|---|
| Supply | 230 V AC from the heatsink base board | HW remark: the Primary+Secondary pair *"do not fit on the existing ADOR board"* |
| **Digital inputs** | 4, all marked **Spare**; DI4 annotated "Continue", DI1 annotated "Interrupt"; plus a thermostat input in firmware | No motherboard DI connections on the dual-bank circuit (ADPL) → **#8**, **#21** |
| **Digital outputs** | **11**: DO1 MODE_FAILURE (error) relay, DO2 charge relay, DO3 discharge relay, DO4–DO11 spare opto for external 24 V relays | Firmware `DO_t` exposes only **8** bits; LSRS says 8 DO + 3 relays → **C-11** |
| Analog in — U terminal | DC voltage feedback (power connection), ≤ **100 V**, 12-bit internal ADC | HW remark: *"We have not sensed the U terminal"* |
| Analog in — U heatsink | Voltage across power heatsink, safety only, *no ADC count, no error generation* | — |
| Analog in — **LNT** (`Vrect+`) | Bank voltage, charge mode; column says 12-bit internal ADC | Remark says **16-bit** → **C-03** |
| Analog in — **ZNT** (`Vrect−`) | Bank voltage, discharge mode; column says 12-bit internal ADC | Remark says **16-bit** → **C-03** |
| Analog in — heatsink temp | 12-bit internal ADC, trip on set value | Testing pending (ADPL) |
| Analog in — **battery voltage sense** | ≤ **100 V**, **24-bit ADC**, four-wire, polarity sense | Legacy used 16-bit; present rig tested to 80 V → **C-07** |
| Analog in — **shunt** | Configurable ranges 75 mV / 100 mV … 10 V, **24-bit ADC** | Ranges to be confirmed per circuit rating |
| Analog in — battery temp | PT1000 / AD590, **−50 … +150 °C**, 16-bit or 12-bit ADC | Calibration sheet says "AD590/**PT100**"; firmware says PT100 → **C-15** |
| **Analog out — reference** | **−10 … +10 V**, **24-bit DAC**; 0…+10 V unipolar (single bank) or −10…+10 V bipolar (dual bank), selectable in software | Remarks say **16-bit**, then **20-bit**; range remark says **−2…+2 V**; LSRS says ≥18-bit → **C-01**, **C-02** |
| Response time | Rise/fall ≤ **10 ms**; charge↔discharge ≤ **10 ms** at Imax; 0.1 %…100 % Imax in 0.1…10 ms | Measured 4 s rise (ADPL, pending) and 23 ms transition; LSRS mandates a **500 ms** switch-over delay → **C-04** |
| Registration time | ≤ **500 µs** | Remark 100 ms, then 10 ms, then "approve up to 1 ms"; BM's own floor is 0.1 s → **C-05** |
| **Communication** | **Isolated CAN / CAN-FD**, default **250 kbps** | Protocol undefined → **D-04** |
| Bootloader | Flashing via Host PC software, an extension of the Battery Manager tool | ADPL: *"not required as discussed earlier"*; the brief requires it → **C-06**, **#9** |
| Software architecture | Layer-wise, AUTOSAR-like: Application / Interface / BSW / COM | — |
| Watchdog | Adjustable reset/watchdog with **power-fail detection**, supply monitoring | ADPL: *"Not implemented yet. Pending."* |
| Error codes | **≤ 100**; **23 defined** (1…`0x17`) | Descriptions to be extended over the project → **C-14** |
| Configuration | **≤ 200** parameters; **47 defined** | — |
| Calibration | **25** parameters defined | Per-range scheme scope → **#15** |
| Auto-ranging | 4 ranges: 50 % / 10 % / 1 % / 0.1 % of Imax; by software or by shunt-selecting DO | Firmware and LSRS use gains 1/2/4/8 (100/50/25/12.5 %); currently **disabled** for PID interaction → **C-12** |
| Persistence | Calibration and configuration via Host PC, redirected through the Primary | Legacy uses 2 KB emulated EEPROM in flash |
| MCU | Not specified in any source | `<TBD-S01>`. `CODE-S` evidences an **STM32H723** |

**Six items in HW are marked pending or unimplemented by the client's own remarks**, and
this document specifies all of them as requirements because the brief requires them:
parameter setting, calibration, system configuration, error generation and codes,
power-fail action (including program start and data save after PFA), and auto-ranging.
HW additionally lists *"Data save on Primary in case of PC disconnect"*, which is a
Primary requirement and appears in `PRI` §4.16.

### 2.4 User classes

The Secondary Board has **no direct human user**. Every interaction reaches it through
the Primary Board.

| Class | Interaction with this board | Source |
|---|---|---|
| **Primary Board (software actor)** | Sole command and data peer. Issues control data, commands, configuration, calibration and firmware; consumes telemetry, registration and faults | HW §7 |
| **Operator** | Indirect, via the Primary and the Web Application. Sees this board's channel state, measurements and faults | BM §2 |
| **Service / commissioning engineer** | Indirect: calibration, factory configuration, per-channel setup, diagnostics. Directly, only through a local diagnostic path if one is provided (§3.4) | HW *Configuration*, *Calibration* |
| **Manufacturing** | Factory provisioning: type number, versions, identity, calibration dates | HW Config #1–4, #44–47 |
| **Maintenance technician** | Physically present at the cabinet; reads the Error LED and relay indications | HW §3; BM §12.4.8.5 |

### 2.5 Operating environment

One Secondary Board per test circuit, cabinet-mounted, fed 230 V AC from its heatsink
base board (HW *Digital Controller* #1). It drives a linear transistor bank of either
single-bank or dual-bank construction and is connected to one battery under test by a
high-current power pair and a separate four-wire sense pair. It is attached to a shared
isolated CAN bus with the Primary Board and up to seven peer Secondary Boards. It has no
network connection and no operator terminal. It is not assumed to be physically
accessible during normal operation, and it is expected to operate unattended for the
duration of a test that may run for days.

### 2.6 Design and implementation constraints

| ID | Constraint | Source |
|---|---|---|
| CN-01 | The Secondary Board controls exactly **one** test channel | A-01; brief |
| CN-02 | The Primary ↔ Secondary link is **CAN**, isolated, shared by up to 8 Secondaries; layer details reserved | HW §7; **D-04** |
| CN-03 | The Primary decodes programs and executes step sequencing; the Secondary does not | Brief; `PRI` §2.1 |
| CN-04 | Only the **minimum necessary control data** reaches this board | Brief; **D-03** |
| CN-05 | Layered software architecture, AUTOSAR-like (Application / Interface / BSW / COM) | HW *Digital Controller* #9 |
| CN-06 | **No dynamic memory allocation** after initialization without explicit engineering approval. *Note: legacy `CODE-S` `stepData.c` allocates cut-off-condition and registration-parameter arrays on the heap and defines `STEP_DATA_MEMORY_ALLOCATION_FAIL_E`. The ME role change removes the need; the constraint is stated so it is not reintroduced.* | Team engineering policy; CODE-S evidence |
| CN-07 | No source file exceeds **2000 lines** | Team engineering policy |
| CN-08 | The 49 items of HW *Coding Guidelines* apply to all source in this board | HW *Coding Guidelines* |
| CN-09 | MISRA-C applicability undecided | Open Issue **#26** |
| CN-10 | Configuration parameters ≤ **200**; error codes ≤ **100** | HW *Digital Controller* #11, #12 |
| CN-11 | Single-core MCU; no function-allocation decision exists | §1.7 reading note |
| CN-12 | The analog reference output is the **only** means by which this board delivers energy to or draws energy from the battery; all regulation acts through it | HW §5 |
| CN-13 | Recursion is not used where stack depth cannot be bounded; iterative constructions are preferred | HW *Coding Guidelines* #20 |
| CN-14 | Every state machine has a defined default case for invalid states; every interrupt vector is initialized to a handler | HW *Coding Guidelines* #32, #33 |

### 2.7 Assumptions and dependencies

| ID | Assumption |
|---|---|
| A-01 | One Secondary Board = one test channel. |
| A-02 | The Primary Board is the sole source of control data, commands, configuration and calibration, and the sole consumer of telemetry and faults. |
| A-03 | "Secondary Board" ≡ the *Digital Controller Transcard* of **HW**; "Main Controller" in **HW** ≡ the Primary Board. |
| A-04 | `BM` Ch.12 defines required per-circuit regulation and indication behaviour, not wire compatibility (§1.3). |
| A-05 | Legacy code is evidence of intent, never a requirement — and for step execution, evidence of a role now removed (§1.2). |
| A-06 | `PSPE` is prior art bearing on **D-03** and is **not** adopted; no requirement here presupposes its partition. |
| A-07 | Where no source exists, this document records the gap rather than inventing a requirement. |
| A-08 | The Secondary Board is electrically independent of its peers; no shared-rectifier coupling is assumed. Subject to Open Issue **#24**. |
| A-09 | Both single-bank and dual-bank transistor constructions must be supported by one firmware, selected by configuration. Sourced from HW §5 ("2 selectable options via s/w") and the factory-settings transistor-selection option in `CODE-S`. |
| A-10 | The board's absolute electrical ratings (maximum voltage, maximum charge and discharge current, maximum power, temperature limits, bank voltage limits, rectifier maximum) are **factory configuration**, not compile-time constants. Sourced from `BTS_FactoryData_t`. |
| A-11 | A local diagnostic access path independent of CAN is desirable and is specified in §3.4, but no source mandates one; it is flagged. |

---

## 3. External Interface Requirements

### 3.1 Hardware interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-HW-001 | The Secondary Board shall communicate with the Primary Board over an isolated CAN interface. | HW *Digital Controller* #7 |
| SRS-SEC-HW-002 | ⚠ The Secondary Board shall drive the transistor-bank base drive through an analog reference output. | HW §5 |
| SRS-SEC-HW-003 | ⚠ The Secondary Board shall be capable of producing a bipolar analog reference output when configured for a dual transistor bank, and a unipolar output when configured for a single transistor bank. | HW §5; A-09; LSRS SW_REQ_170, 171, 181, 182 |
| SRS-SEC-HW-004 | ⚠ The Secondary Board shall record `<TBD-S03>` (the analog reference output range) — see conflict **C-02**. | HW §5 (self-inconsistent) |
| SRS-SEC-HW-005 | ⚠ The Secondary Board shall record `<TBD-S04>` (the analog reference output resolution) — see conflict **C-01**. | HW §5 (self-inconsistent) |
| SRS-SEC-HW-006 | ⚠ The Secondary Board shall drive a charge-mode contactor through a relay output. | HW §3 DO2 |
| SRS-SEC-HW-007 | ⚠ The Secondary Board shall drive a discharge-mode contactor through a relay output. | HW §3 DO3 |
| SRS-SEC-HW-008 | The Secondary Board shall drive an error-indication relay output. | HW §3 DO1 (MODE_FAILURE) |
| SRS-SEC-HW-009 | The Secondary Board shall drive eight opto-isolated digital outputs intended for external 24 V relays. | HW §3 DO4–DO11 |
| SRS-SEC-HW-010 | The Secondary Board shall read four digital inputs. | HW §2; LSRS SW_REQ_1 |
| SRS-SEC-HW-011 | The Secondary Board shall read a thermostat input. | CODE-S `DI_t.Thermostat` |
| SRS-SEC-HW-012 | The Secondary Board shall record `<TBD-S05>` (the confirmed digital input, digital output and relay counts, and their assigned functions) — see conflict **C-11** and Open Issue **#22**. | HW §2/§3 vs LSRS |
| SRS-SEC-HW-013 | The Secondary Board shall measure battery voltage through a 24-bit analog-to-digital converter connected to a four-wire sense pair. | HW §4.4; LSRS SW_REQ_115 |
| SRS-SEC-HW-014 | The Secondary Board shall measure battery current through a 24-bit analog-to-digital converter connected across a shunt. | HW §4.5; LSRS SW_REQ_138 |
| SRS-SEC-HW-015 | The Secondary Board shall measure the voltage at the battery power connection. | HW §4.1 |
| SRS-SEC-HW-016 | ⚠ The Secondary Board shall measure the transistor-bank voltage in charge mode (LNT). | HW §4.2a |
| SRS-SEC-HW-017 | ⚠ The Secondary Board shall measure the transistor-bank voltage in discharge mode (ZNT). | HW §4.2b |
| SRS-SEC-HW-018 | ⚠ The Secondary Board shall measure heatsink temperature. | HW §4.3 |
| SRS-SEC-HW-019 | The Secondary Board shall measure battery temperature from an external temperature sensor. | HW §4.6 |
| SRS-SEC-HW-020 | The Secondary Board shall record `<TBD-S06>` (whether the external temperature sensor is PT100, PT1000 or AD590, and whether more than one type must be supported) — see conflict **C-15**. | HW §4.6 vs HW *Calibration* #17 vs CODE-S |
| SRS-SEC-HW-021 | The Secondary Board shall record `<TBD-S07>` (the ADC resolution used for the LNT and ZNT channels) — see conflict **C-03**. | HW §4.2 (self-inconsistent) |
| SRS-SEC-HW-022 | The Secondary Board shall retain calibration, factory configuration, battery data and manufacturing data in non-volatile storage that survives loss of supply. | HW *Digital Controller* #12; CODE-S `emulEeprom.c` |
| SRS-SEC-HW-023 | ⚠ The Secondary Board shall service a hardware watchdog such that an unserviced watchdog causes a board reset. | HW *Digital Controller* #10; CODE-S `userI_Wdg` |
| SRS-SEC-HW-024 | ⚠ The Secondary Board shall detect an impending loss of supply and signal it to software before the supply voltage falls below the level required for correct operation. | HW *Digital Controller* #10, #63 |
| SRS-SEC-HW-025 | The Secondary Board shall detect an unstable input supply condition. | HW *Error code* `0x13` |
| SRS-SEC-HW-026 | The Secondary Board shall not require any analog input channel that is unavailable in its configured hardware variant in order to reach its Ready state; an unfitted optional channel shall be reported as unavailable rather than as a fault. | NEW — ME; HW remarks on unfitted channels |

### 3.2 Software interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-SW-001 | The Secondary Board shall present exactly one logical interface to the Primary Board (IF-B), carrying all control, configuration, calibration, telemetry, registration, fault and firmware traffic. | HW §7; brief |
| SRS-SEC-SW-002 | The Secondary Board shall implement the message set defined in `ME-ICD-001` for IF-B. | ME-ICD-001 |
| SRS-SEC-SW-003 | The Secondary Board shall have no interface to the Web Application. | Brief; §2.1 |
| SRS-SEC-SW-004 | The Secondary Board shall have no interface to any peer Secondary Board. | NEW — ME; A-08 |
| SRS-SEC-SW-005 | The Secondary Board shall be implemented in a layered architecture separating Application, Interface, Basic Software and Communication concerns. | HW *Digital Controller* #9 |
| SRS-SEC-SW-006 | The Secondary Board shall isolate all hardware register access in a basic-software layer, such that no application-layer module accesses a peripheral register directly. | HW *Digital Controller* #9; CN-05 |

### 3.3 Communication interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-CM-001 | The Secondary Board shall detect corrupted messages received on IF-B and discard them without acting on their content. | CODE-S `crc16Bit.c` (CRC-16/Modbus) |
| SRS-SEC-CM-002 | The Secondary Board shall acknowledge or negatively acknowledge every command received on IF-B that requires a response. | CODE-S `*_Q_RESP_ACK` / `*_Q_RESP_NACK` |
| SRS-SEC-CM-003 | The Secondary Board shall reject a command it does not recognise, and shall report the rejection. | HW *Error code* `0x0E`; CODE-S `en_ERR_INVALID_CMD_PRIM_TO_SEC` |
| SRS-SEC-CM-004 | The Secondary Board shall reject a command whose parameters are outside their permitted range, and shall report the rejection with a reason distinguishable from an unrecognised command. | CODE-S; derived |
| SRS-SEC-CM-005 | The Secondary Board shall process only messages addressed to its own node identity, and shall ignore messages addressed to any other node without responding to them. | NEW — ME; **D-04** |
| SRS-SEC-CM-006 | The Secondary Board shall detect the loss of communication with the Primary Board. | HW *Error code* `0x0D`; CODE-S `en_ERR_PRIM_SEC_COM` |
| SRS-SEC-CM-007 | ⚠ The Secondary Board shall declare loss of Primary communication when no valid message addressed to it has been received within a configurable timeout, default **100 ms**. | HW *Configuration* #9 |
| SRS-SEC-CM-008 | The Secondary Board shall operate at a configurable CAN bit rate, default **250 kbps**. | HW *Configuration* #14 |
| SRS-SEC-CM-009 | The Secondary Board shall not transmit unsolicited traffic on IF-B at a rate that prevents any peer node from meeting its own reporting obligations. | NEW — ME; Open Issue **#18** |
| SRS-SEC-CM-010 | The Secondary Board shall recover communication automatically when the Primary Board resumes transmission, without requiring a reset. | NEW — ME |
| SRS-SEC-CM-011 | The Secondary Board shall recover from a CAN controller bus-off condition without requiring a board reset, and shall report each occurrence. (*"Bus-off" is the state a CAN controller enters after too many transmission errors: it removes itself from the bus and stops talking until it is deliberately restarted.*) | NEW — ME |
| SRS-SEC-CM-012 | The Secondary Board shall record `<TBD-S08>` (the node addressing scheme, message identifier allocation, framing and whether CAN 2.0B or CAN-FD is used) — reserved to **D-04**. | Unsourced |

### 3.4 User interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-UI-001 | The Secondary Board shall not require a local human-machine interface for any operational function. | §2.4 |
| SRS-SEC-UI-002 | The Secondary Board shall indicate an active error condition on a local indicator visible to a technician at the cabinet. | HW §3 DO1; BM §12.4.8.5 |
| SRS-SEC-UI-003 | The Secondary Board shall indicate charge activity on a local indicator. | BM §12.4.8.5 (`RCH` blinks the charge LED); CODE-S `USER_LED_EN` |
| SRS-SEC-UI-004 | The Secondary Board shall provide a diagnostic access path usable by service personnel independently of the CAN link. | HW *Digital Controller* #13; A-11 |
| SRS-SEC-UI-005 | The Secondary Board shall record `<TBD-S09>` (the required local indicator set and the meaning of each, and whether the diagnostic access path of SRS-SEC-UI-004 is required in production hardware) — see Open Issue **#21**. | Unsourced |

---
## 4. Functional Requirements

Coverage ratings are carried forward from `GATE` §2 and restated per area:
● Strong (multiple traceable sources) · ◐ Partial (source exists, gaps remain) ·
○ Absent (no source — Open Issue) · ◆ NEW-ME (no precedent; ME topology change).

### 4.1 S1 — Startup, initialization, self-test, safe default outputs

**Coverage:** ◐ Partial. The initialization sequence and its failure modes are well
evidenced by `CODE-S btsSecAppInit()`; the enumerated content of a power-on self-test is
not — Open Issue **#12**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S1-001 | ⚠ The Secondary Board shall place the analog reference output at zero before enabling the transistor-bank drive, on every application of supply and on every reset. | CODE-S `relayDefaultState()`, `DAC_MEAN_VALUE`; HW §5 |
| SRS-SEC-S1-002 | ⚠ The Secondary Board shall place the charge and discharge contactor relays in their default de-energised positions before enabling the transistor-bank drive, on every application of supply and on every reset. | CODE-S `relayDefaultState()` |
| SRS-SEC-S1-003 | ⚠ The Secondary Board shall complete SRS-SEC-S1-001 and SRS-SEC-S1-002 before any other initialization step that could affect the analog reference output or the relay outputs. | derived; safety |
| SRS-SEC-S1-004 | The Secondary Board shall complete initialization and reach a defined operating state after every application of supply or reset. | CODE-S `btsSecAppInit()` |
| SRS-SEC-S1-005 | The Secondary Board shall initialize the measurement subsystem during initialization and shall raise a distinct fault if that initialization fails. | CODE-S `ADS1256_INIT_ERROR_E` |
| SRS-SEC-S1-006 | The Secondary Board shall initialize the analog reference output subsystem during initialization and shall raise a distinct fault if that initialization fails. | CODE-S `DAC_INIT_ERROR_E` |
| SRS-SEC-S1-007 | The Secondary Board shall verify that the analog reference output can be set to a commanded value during initialization, and shall raise a distinct fault if it cannot. | CODE-S `DAC_OUT_SET_ERROR_E` |
| SRS-SEC-S1-008 | The Secondary Board shall load its calibration parameters during initialization and shall raise a distinct fault if that load fails. | CODE-S `CALIB_PARAMETER_INIT_ERROR_E`, `calibParameterInit()` |
| SRS-SEC-S1-009 | ⚠ The Secondary Board shall initialize its watchdog during initialization and shall raise a distinct fault if that initialization fails. | CODE-S `I_WDG_INIT_E`, `iWdgInit()` |
| SRS-SEC-S1-010 | The Secondary Board shall verify the integrity of its stored calibration data during initialization. | CODE-S `MAGIC_NUMBER 0xFF11`, stored CRC |
| SRS-SEC-S1-011 | The Secondary Board shall verify the integrity of its stored factory configuration during initialization. | HW *Digital Controller* #12; CODE-S `emulEeprom.c` |
| SRS-SEC-S1-012 | The Secondary Board shall apply defined default values for any calibration or configuration item whose stored value fails its integrity check, and shall record the substitution. | CODE-S default `#define` set; derived |
| SRS-SEC-S1-013 | ⚠ The Secondary Board shall not permit charge or discharge regulation while its calibration data is failing its integrity check. | derived; safety |
| SRS-SEC-S1-014 | The Secondary Board shall report the outcome of initialization to the Primary Board when the CAN link is available, and shall retain the outcome for later retrieval when it is not. | derived from S18 |
| SRS-SEC-S1-015 | The Secondary Board shall execute a power-on self-test before reporting itself ready to regulate. | HW *Digital Controller* #10; Open Issue **#12** |
| SRS-SEC-S1-016 | The Secondary Board shall not report itself ready to regulate while any power-on self-test is failed. | derived |
| SRS-SEC-S1-017 | The Secondary Board shall determine, during initialization, whether the preceding shutdown was orderly or was caused by loss of supply, reset or watchdog expiry. | CODE-S `powerFailBackup`/`powerFailResume`; HW #63–65; `ICD` B-PFA-04 |
| SRS-SEC-S1-018 | The Secondary Board shall make the reason for its most recent restart available to the Primary Board. | derived from S13, S18; `ICD` B-PFA-04 |
| SRS-SEC-S1-019 | The Secondary Board shall report its type number, hardware version, bootloader version and application software version on request. | HW *Configuration* #1–4 |
| SRS-SEC-S1-020 | The Secondary Board shall determine its configured transistor-bank construction — single bank or dual bank — during initialization, and shall configure the analog reference output polarity accordingly. | HW §5 ("2 selectable options via s/w"); A-09 |
| SRS-SEC-S1-021 | ⚠ The Secondary Board shall not accept any regulation command until initialization has completed successfully. | derived |
| SRS-SEC-S1-022 | ⚠ The Secondary Board shall measure the battery voltage and verify its polarity before energising the transistor bank for the first time after initialization. | LSRS SW_REQ_129, SW_REQ_130 |
| SRS-SEC-S1-023 | The Secondary Board shall initialize every measurement filter to a defined state during initialization, and shall not report a derived measurement until its filter holds valid data. | CODE-S `initializeAllMovingFilters()` |
| SRS-SEC-S1-024 | The Secondary Board shall record `<TBD-S10>` (the enumerated content of the power-on self-test for each subsystem, and the pass criterion for each) — see Open Issue **#12**. | Unsourced |
| SRS-SEC-S1-025 | The Secondary Board shall record `<TBD-S11>` (whether an orderly shutdown sequence is required, and what it must complete before supply is removed) — see Open Issue **#12**. | Unsourced |
| SRS-SEC-S1-026 | The Secondary Board shall complete initialization within `<TBD-S12>` of supply application. | Unsourced |

### 4.2 S2 — Operating mode and state machine

**Coverage:** ● Strong for the channel states; ◐ Partial for the board-level modes.
`CODE-S` gives `circuitState_t` and `progRunState_t`. Maintenance mode is unsourced —
Open Issue **#13**. A Calibration mode is sourced, from `calibrationState_t`.

**Reading note.** In BTS, `progRunState_t` (`prsIdle` / `prsRunning`) described *the
Secondary running a program*. In ME the Secondary runs no program. The equivalent
distinction that survives is between *not regulating* and *regulating under Primary
control*. This document specifies that distinction and does not require the legacy
enumeration to be retained; whether the legacy state values are reused on the wire is a
matter for `ME-ICD-001` and **D-03**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S2-001 | The Secondary Board shall maintain a single operating state that fully determines its output behaviour. | CN-14; derived |
| SRS-SEC-S2-002 | The Secondary Board shall implement the operating states Initializing, Ready, Regulating, Paused, Interrupted, Fault, Calibration and Firmware Update. | CODE-S `circuitState_t`, `calibrationState_t`; §4.16 |
| SRS-SEC-S2-003 | The Secondary Board shall distinguish, while Regulating, between charge regulation and discharge regulation. | CODE-S `csCharge`, `csDischarge` |
| SRS-SEC-S2-004 | The Secondary Board shall enter Ready when initialization completes with no failed self-test and no active fault. | derived |
| SRS-SEC-S2-005 | The Secondary Board shall enter Regulating only on an explicit command from the Primary Board. | CODE-S `CONTROL_CMD_Q_ID_SATRT_PROGRAM`; derived |
| SRS-SEC-S2-006 | ⚠ The Secondary Board shall enter Fault autonomously on detection of any condition listed in §4.9, without waiting for a command from the Primary Board. | HW *Error code* sheet; derived |
| SRS-SEC-S2-007 | ⚠ The Secondary Board shall be in the safe state whenever it is in Initializing, Ready, Paused, Interrupted, Fault or Firmware Update. | derived; safety |
| SRS-SEC-S2-008 | The Secondary Board shall enter Paused on command from the Primary Board and shall hold its measurement accumulators without reset while Paused. | CODE-S `csPuase`, `isProgramPauseFlag`; BM §12.4.8.3 |
| SRS-SEC-S2-009 | The Secondary Board shall resume regulation from a Paused state on a continue command, using the setpoint in force when it was paused unless the Primary Board supplies a new one. | CODE-S `csContinue`, `programContinue()` |
| SRS-SEC-S2-010 | The Secondary Board shall enter Interrupted when the Primary Board commands an interrupt, and shall remain in the safe state until commanded to continue or to stop. | CODE-S `csInt`, `CONTROL_CMD_Q_ID_INTERRUPT_PROGRAM`; BM §12.4.5 `INT` |
| SRS-SEC-S2-011 | The Secondary Board shall enter Calibration only on command from the Primary Board and only when it is not Regulating. | CODE-S `CALIB_CMD_Q_ID_START`; derived |
| SRS-SEC-S2-012 | ⚠ The Secondary Board shall reject a command to enter Calibration while Regulating, and shall report the rejection. | derived; safety |
| SRS-SEC-S2-013 | The Secondary Board shall permit a state change only through a defined transition. | CN-14 |
| SRS-SEC-S2-014 | The Secondary Board shall reject any command that is not valid in its current state and shall report the rejection with a reason. | derived; SRS-SEC-CM-004 |
| SRS-SEC-S2-015 | The Secondary Board shall report every state change to the Primary Board. | CODE-S `BTS_MeasuredData_t.circuitStatus` |
| SRS-SEC-S2-016 | The Secondary Board shall report its current state in every telemetry message. | CODE-S `BTS_MeasuredData_t.circuitStatus` |
| SRS-SEC-S2-017 | ⚠ The Secondary Board shall not leave the Fault state while any fault that latches under §4.13 remains uncleared. | derived; Open Issue **#17** |
| SRS-SEC-S2-018 | The Secondary Board shall enter a defined state on detecting an invalid or impossible internal state, shall treat that detection as a fault, and shall enter the safe state. | HW *Coding Guidelines* #32; CN-14 |
| SRS-SEC-S2-019 | The Secondary Board shall record `<TBD-S13>` (whether a Maintenance mode distinct from Calibration is required, its entry and exit conditions, and the operations permitted in it) — see Open Issue **#13**. | Unsourced |
| SRS-SEC-S2-020 | The Secondary Board shall record `<TBD-S14>` (whether the legacy channel-state values are retained on the wire, and how the ME state set maps onto the states the Web Application displays) — bears on **D-03** and `ME-ICD-001`. | Unsourced |

### 4.3 S3 — Node identity and CAN addressing

**Coverage:** ○ Absent as a specification; ◆ new to ME in substance. Legacy carried a
`circuitId` byte that was decorative on a point-to-point link. The addressing scheme is
reserved to **D-04**; the identity *source* is unsourced — Open Issue **#22**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S3-001 | The Secondary Board shall hold a node identity that is unique among the Secondary Boards on its CAN bus. | NEW — ME; CODE-S `circuitId` |
| SRS-SEC-S3-002 | The Secondary Board shall determine its node identity during initialization, before it transmits any message on IF-B. | NEW — ME |
| SRS-SEC-S3-003 | The Secondary Board shall retain its node identity across a reset and across loss of supply. | NEW — ME |
| SRS-SEC-S3-004 | The Secondary Board shall report its node identity to the Primary Board on request. | HW *Configuration* #1–4; CODE-S |
| SRS-SEC-S3-005 | The Secondary Board shall enter the safe state and report a fault if it cannot determine a valid node identity. | NEW — ME; derived |
| SRS-SEC-S3-006 | The Secondary Board shall not act on control data addressed to a different node identity. | NEW — ME; SRS-SEC-CM-005 |
| SRS-SEC-S3-007 | The Secondary Board shall respond to a Primary Board enumeration request with its node identity, type number, hardware version, bootloader version and application software version. | HW *Configuration* #1–4; §4.1; `ICD` B-NOD-02 |
| SRS-SEC-S3-008 | The Secondary Board shall provide a means by which the Primary Board can distinguish it from a peer board that has the same type number and versions. | NEW — ME |
| SRS-SEC-S3-009 | The Secondary Board shall support a configurable eight-bit software-selectable option value. | HW *Configuration* #15 (Software DIP switch, max 255) |
| SRS-SEC-S3-010 | The Secondary Board shall record `<TBD-S15>` (how the node identity is acquired: hardware strap or DIP switch, assignment by the Primary Board, or one-time provisioning into non-volatile memory) — see Open Issue **#22** and **D-04**. | Unsourced |
| SRS-SEC-S3-011 | The Secondary Board shall record `<TBD-S16>` (the purpose of hardware switch DIP S1, and whether it carries the node identity) — see Open Issue **#21**, HW *Queries* #5. | Unsourced |
| SRS-SEC-S3-012 | The Secondary Board shall record `<TBD-S17>` (whether the board must remain functional when inserted into a running bus, and any address-conflict resolution it must perform) — see Open Issue **#14**. | Unsourced |

### 4.4 S4 — Command and control-data reception, validation, rejection

**Coverage:** ● Strong. Acknowledgement, negative acknowledgement, integrity checking and
invalid-command reporting are all evidenced in `CODE-S` and in the HW error list.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S4-001 | The Secondary Board shall accept control data from the Primary Board sufficient to determine the regulation mode and the setpoint or setpoints it must apply. | Brief; **D-03**; `ICD` B-DAT-02 |
| SRS-SEC-S4-002 | The Secondary Board shall accept, with that control data, the safety limits it must enforce locally while applying it. | Brief; **D-03**; PSPE §4.4 (prior art); `ICD` B-DAT-02 |
| SRS-SEC-S4-003 | The Secondary Board shall verify the integrity of every received message before acting on it. | CODE-S `crc16Bit.c` |
| SRS-SEC-S4-004 | The Secondary Board shall verify that every received message is complete and correctly delimited before acting on it. | CODE-S `START_SEQ`/`STOP_SEQ`; **D-04** |
| SRS-SEC-S4-005 | The Secondary Board shall verify that every commanded setpoint lies within the board's configured absolute ratings before applying it. | CODE-S `BTS_FactoryData_t`; A-10 |
| SRS-SEC-S4-006 | ⚠ The Secondary Board shall reject a commanded setpoint that exceeds the configured maximum charge current, maximum discharge current, maximum voltage, minimum voltage or maximum power, shall not apply it, and shall report the rejection. | CODE-S `cktMax*`; HW *Configuration* #5–8; `ICD` B-DAT-03 |
| SRS-SEC-S4-007 | The Secondary Board shall reject a command whose identifier it does not recognise and shall report it as an unknown command. | HW *Error code* `0x0E`; `ICD` B-DAT-03 |
| SRS-SEC-S4-008 | The Secondary Board shall acknowledge each accepted command distinguishably from a rejection. | CODE-S `*_Q_RESP_ACK` / `*_Q_RESP_NACK` |
| SRS-SEC-S4-009 | The Secondary Board shall indicate to the Primary Board, on request, whether it is ready to receive configuration data. | CODE-S `CONFIG_DATA_Q_ID_IS_READY` |
| SRS-SEC-S4-010 | The Secondary Board shall indicate to the Primary Board, on request, whether it is ready to receive control data. | CODE-S `PROGRAM_STEP_Q_ID_IS_READY`; `ICD` B-DAT-01 |
| SRS-SEC-S4-011 | The Secondary Board shall accept a start command that causes it to begin regulating. | CODE-S `CONTROL_CMD_Q_ID_SATRT_PROGRAM`; HW §7; `ICD` B-CTL-01 |
| SRS-SEC-S4-012 | ⚠ The Secondary Board shall accept a stop command that causes it to stop regulating and enter the safe state. | CODE-S `CONTROL_CMD_Q_ID_STOP_PROGRAM`; HW §7 (`STO`); `ICD` B-CTL-02 |
| SRS-SEC-S4-013 | ⚠ The Secondary Board shall accept an interrupt command that causes it to stop regulating and enter the safe state while retaining its accumulated measurements. | CODE-S `CONTROL_CMD_Q_ID_INTERRUPT_PROGRAM`; HW §7 (`INT`); `ICD` B-CTL-03 |
| SRS-SEC-S4-014 | The Secondary Board shall accept a continue command that causes it to resume regulating after an interrupt or a pause. | CODE-S `CONTROL_CMD_Q_ID_CONTINUE_PROGRAM`; HW §7; `ICD` B-CTL-05 |
| SRS-SEC-S4-015 | The Secondary Board shall accept a pause command that causes it to suspend energy transfer while retaining its accumulated measurements. | CODE-S `isProgramPauseFlag`; HW §7 (`PAU`); BM §12.4.8.3; `ICD` B-CTL-04 |
| SRS-SEC-S4-016 | The Secondary Board shall accept a power-resume command that causes it to restore the operating context persisted before the last loss of supply. | CODE-S `CONTROL_CMD_Q_ID_POWER_RESUME`, `powerFailResume()` |
| SRS-SEC-S4-017 | The Secondary Board shall accept a digital-output selection command that sets the commanded state of its spare digital outputs. | CODE-S `CONTROL_CMD_Q_ID_DO_SELECTION`; HW §3 remark |
| SRS-SEC-S4-018 | The Secondary Board shall accept a time-synchronisation value from the Primary Board and shall use it to timestamp the data it reports. | CODE-S `BTS_BatteryData_t.epochTime`; LSRS SW_REQ_21; `ICD` B-NOD-06 |
| SRS-SEC-S4-019 | The Secondary Board shall accept battery parameter data from the Primary Board. | CODE-S `CONFIG_DATA_Q_ID_WRITE_BATT_DATA`, `BTS_BatteryData_t` |
| SRS-SEC-S4-020 | The Secondary Board shall not apply a new setpoint while it is in a state in which regulation is not permitted, and shall report the rejection. | derived; SRS-SEC-S2-014; `ICD` B-DAT-03 |
| SRS-SEC-S4-021 | The Secondary Board shall apply the most recently accepted setpoint and shall discard any earlier setpoint that has been superseded. | derived; `ICD` B-DAT-06 |
| SRS-SEC-S4-022 | The Secondary Board shall detect a control-data message that arrives out of the expected sequence and shall report it without acting on it. | derived; **D-03** |
| SRS-SEC-S4-023 | The Secondary Board shall report to the Primary Board when it has completed the regulation commanded by a unit of control data, so that the Primary Board can supply the next. | CODE-S `btsSecReqNextProgramStep()`, `PROG_STOP_ACK`; **D-03**; `ICD` B-DAT-04 |
| SRS-SEC-S4-024 | The Secondary Board shall re-request control data that has not arrived within a configurable timeout, default **100 ms**, for a configurable number of attempts, default **5**. | CODE-S `REQEST_STEP_TIME_OUT 100U`, `REQEST_STEP_RESP_COUNT 5U`; `ICD` B-DAT-05 |
| SRS-SEC-S4-025 | ⚠ The Secondary Board shall enter the safe state and report a fault when control data it has requested has not arrived after the configured number of attempts. | derived from SRS-SEC-S4-024; safety; `ICD` B-DAT-05 |
| SRS-SEC-S4-026 | The Secondary Board shall record `<TBD-S18>` (the exact content of the minimum necessary control data, including whether cut-off conditions and their thresholds are carried to this board) — reserved to **D-03**. | Unsourced |

### 4.5 S5 — Charge regulation

**Coverage:** ● Strong. Regulation modes, controller structure, tunable parameters,
tolerance bands and unreachable-setpoint detection are all sourced from HW and `CODE-S`;
BM supplies the crossover semantics.

#### 4.5.1 Regulation modes

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S5-001 | ⚠ The Secondary Board shall regulate charge current to a commanded constant-current setpoint. | HW §4.5; CODE-S `ccChargePid`; BM §12.4.3 |
| SRS-SEC-S5-002 | ⚠ The Secondary Board shall regulate battery voltage to a commanded constant-voltage setpoint during charge. | HW §4.4; CODE-S `cvChargePid`; BM §12.4.3 |
| SRS-SEC-S5-003 | ⚠ The Secondary Board shall regulate charge power to a commanded constant-power setpoint. | CODE-S `cpChargePid`; BM §12.4.3 |
| SRS-SEC-S5-004 | ⚠ The Secondary Board shall regulate charge in a combined constant-current-then-constant-voltage mode, given both a current setpoint and a voltage setpoint. | CODE-S `CCCV_Chg`, `isCvLoopFlag`; BM §12.4.3 |
| SRS-SEC-S5-005 | ⚠ The Secondary Board shall transfer from constant-current to constant-voltage regulation, in the combined mode, when the measured battery voltage reaches the commanded voltage setpoint. | BM §12.4.3 (CC-CV crossover); CODE-S `isCvLoopFlag` |
| SRS-SEC-S5-006 | ⚠ The Secondary Board shall effect the transfer of SRS-SEC-S5-005 without a discontinuity in the analog reference output that would cause a current transient exceeding `<TBD-S19>`. | NEW — ME; derived from HW §6 |
| SRS-SEC-S5-007 | ⚠ The Secondary Board shall limit the charge current to the commanded current setpoint while regulating in constant-voltage mode. | CODE-S `isCvLoopReachingMaxCurrent`; BM §12.4.3 |
| SRS-SEC-S5-008 | ⚠ The Secondary Board shall regulate charge in a combined constant-current-then-constant-power mode, given both a current setpoint and a power setpoint. | CODE-S `T_CCCP_Chg`; BM §12.4.3 |
| SRS-SEC-S5-009 | ⚠ The Secondary Board shall regulate charge in a combined constant-power-then-constant-voltage mode, given both a power setpoint and a voltage setpoint. | CODE-S `T_CPCV_Chg` |
| SRS-SEC-S5-010 | ⚠ The Secondary Board shall regulate charge in a combined constant-current, constant-power then constant-voltage mode, given all three setpoints. | CODE-S `T_CCCPCV_Chg` |
| SRS-SEC-S5-011 | ⚠ The Secondary Board shall select, in any combined mode, the most restrictive of the active regulation loops at every control interval. | derived from SRS-SEC-S5-004 to -010 |
| SRS-SEC-S5-012 | ⚠ The Secondary Board shall energise the charge contactor before commanding a non-zero charge reference output. | HW §3 DO2; CODE-S `isRelayOnFlag` |
| SRS-SEC-S5-013 | ⚠ The Secondary Board shall de-energise the charge contactor after the reference output has returned to zero when charge regulation ends. | HW §3 DO2; derived |

#### 4.5.2 Controller structure and tuning

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S5-014 | ⚠ The Secondary Board shall implement each regulation loop as a proportional-integral-derivative controller acting on the analog reference output. | CODE-S `btsSecPID.c`; HW *Calibration* #10–15 |
| SRS-SEC-S5-015 | The Secondary Board shall hold an independent set of proportional, integral and derivative parameters for each regulation loop, per mode and per direction. | CODE-S `BTS_FactoryData_t` (12 parameter sets) |
| SRS-SEC-S5-016 | The Secondary Board shall accept updated controller parameters as factory configuration and shall retain them in non-volatile memory. | HW *Calibration* #10–15; CODE-S |
| SRS-SEC-S5-017 | ⚠ The Secondary Board shall not accept a change to controller parameters while it is regulating. | derived; safety |
| SRS-SEC-S5-018 | ⚠ The Secondary Board shall bound each controller's integral term so that it cannot accumulate beyond the range of the analog reference output. | CODE-S `DAC_INTEGRAL_MAX`/`_MIN` |
| SRS-SEC-S5-019 | ⚠ The Secondary Board shall bound each controller's output to the configured analog reference output range. | CODE-S `DAC_MAX_SET_VALUE`/`_MIN_SET_VALUE` |
| SRS-SEC-S5-020 | ⚠ The Secondary Board shall reset every regulation loop, including its integral term, when regulation stops. | CODE-S `resetAllPidLoop()`, `resetAllFlagAfterStopProgram()` |
| SRS-SEC-S5-021 | ⚠ The Secondary Board shall reset the measurement filters associated with charge when charge regulation begins. | CODE-S `resetChaMovingFilters()` |
| SRS-SEC-S5-022 | ⚠ The Secondary Board shall detect that a regulation loop's output has been driven outside its permitted range and shall raise a fault distinguishing the current loop from the voltage loop. | HW *Error code* `0x0F`, `0x10`; CODE-S `PID_EX_COUNT 10` |
| SRS-SEC-S5-023 | The Secondary Board shall require the out-of-range condition of SRS-SEC-S5-022 to persist for a configurable number of consecutive control intervals, default **10**, before raising the fault. | CODE-S `PID_EX_COUNT 10` |

#### 4.5.3 Setpoint attainment and tolerance

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S5-024 | The Secondary Board shall determine whether the measured quantity has reached the commanded setpoint within a configurable tolerance band. | HW *Configuration* #10 (default 0.1 % of Imax) |
| SRS-SEC-S5-025 | ⚠ The Secondary Board shall raise a fault when a commanded current setpoint has not been reached within a configurable time, default **1000 ms**. | HW *Configuration* #11; HW *Error code* `6` |
| SRS-SEC-S5-026 | ⚠ The Secondary Board shall raise a fault when a commanded voltage setpoint has not been reached within a configurable time. | HW *Error code* `7` |
| SRS-SEC-S5-027 | ⚠ The Secondary Board shall raise a fault when a commanded power setpoint has not been reached within a configurable time. | CODE-S `en_ERR_POWER_SETPOINT_UNREACHABLE` |
| SRS-SEC-S5-028 | The Secondary Board shall confirm setpoint attainment against a configurable count of consecutive control intervals rather than a single sample. | CODE-S `LOOP_EX_COUNTER 2000U` |
| SRS-SEC-S5-029 | The Secondary Board shall apply separate configurable attainment tolerances for current, voltage and power. | CODE-S `CIRCUIT_CURRENT_TOLERANCE`, `CIRCUIT_VOLT_TOLERANCE`, `CIRCUIT_POWER_TOLERANCE` |
| SRS-SEC-S5-030 | The Secondary Board shall report to the Primary Board that a setpoint has been reached. | derived from S12 |
| SRS-SEC-S5-031 | The Secondary Board shall record `<TBD-S20>` (the required attainment tolerance for current, voltage and power, and whether each is expressed as a percentage of full scale or as an absolute value) — legacy values are absolute and are not a specification. | CODE-S vs HW *Configuration* #10 |

#### 4.5.4 Termination of commanded regulation

**Reserved decision.** Whether the Secondary evaluates the conditions that end a step, or
merely regulates until the Primary tells it to stop, is **D-03**. The requirements below
are written so that they hold under either resolution: the Secondary must be *capable* of
enforcing a supplied bound, and must *always* report the measurements from which the
Primary could decide. Nothing here presupposes which party decides.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S5-032 | ⚠ The Secondary Board shall stop regulating and enter the safe state when the Primary Board commands it to stop, without waiting for any local condition. | derived; brief |
| SRS-SEC-S5-033 | ⚠ The Secondary Board shall enforce every safety limit supplied to it with a unit of control data, independently of any evaluation the Primary Board performs. | derived; safety; **D-03** |
| SRS-SEC-S5-034 | The Secondary Board shall report each measured and derived quantity from which a step-ending condition could be evaluated, at the reporting rate of §4.12, whether or not it evaluates that condition itself. | derived; **D-03** |
| SRS-SEC-S5-035 | The Secondary Board shall, where it is required by **D-03** to evaluate a supplied threshold condition, support the comparison operations greater-than, less-than, greater-than-or-equal, less-than-or-equal, equal and not-equal. | CODE-S `logicType_t` (prior art for **D-03**) |
| SRS-SEC-S5-036 | The Secondary Board shall, where it evaluates a supplied threshold condition, require the condition to hold for a configurable number of consecutive evaluations, default **5**, before acting on it. | CODE-S `LIMIT_EX_COUNTER 5U` |
| SRS-SEC-S5-037 | The Secondary Board shall report which supplied condition caused it to stop regulating, where it stopped on a supplied condition. | CODE-S `cutoffCondition_t.actionType` (prior art); derived |

### 4.6 S6 — Discharge regulation

**Coverage:** ● Strong, by symmetry with S5, plus the bank-construction distinction from
LSRS §9.2–9.5.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S6-001 | ⚠ The Secondary Board shall regulate discharge current to a commanded constant-current setpoint. | CODE-S `ccDischargePid`, `CC_DChg`; BM §12.4.3 |
| SRS-SEC-S6-002 | ⚠ The Secondary Board shall regulate battery voltage to a commanded constant-voltage setpoint during discharge. | CODE-S `cvDischargePid`, `CV_DChg` |
| SRS-SEC-S6-003 | ⚠ The Secondary Board shall regulate discharge power to a commanded constant-power setpoint. | CODE-S `cpDischargePid`, `CP_DChg` |
| SRS-SEC-S6-004 | ⚠ The Secondary Board shall regulate discharge in a combined constant-current-then-constant-voltage mode. | CODE-S `CCCV_DChg` |
| SRS-SEC-S6-005 | ⚠ The Secondary Board shall regulate discharge in the combined constant-current-then-constant-power, constant-power-then-constant-voltage, and constant-current, constant-power then constant-voltage modes. | CODE-S `T_CCCP_DChg`, `T_CPCV_DChg`, `T_CCCPCV_DChg` |
| SRS-SEC-S6-006 | ⚠ The Secondary Board shall transfer between the active loops of a combined discharge mode without a discontinuity in the analog reference output that would cause a current transient exceeding `<TBD-S19>`. | NEW — ME; symmetric with SRS-SEC-S5-006 |
| SRS-SEC-S6-007 | ⚠ The Secondary Board shall stop discharge regulation and enter the safe state when the measured battery voltage falls to a commanded cut-off voltage. | BM §12.4.4; CODE-S |
| SRS-SEC-S6-008 | ⚠ The Secondary Board shall apply the requirements of §4.5.2, §4.5.3 and §4.5.4 to discharge regulation, using the controller parameters, tolerances and limits configured for discharge. | derived |
| SRS-SEC-S6-009 | ⚠ The Secondary Board shall reset the measurement filters associated with discharge when discharge regulation begins. | CODE-S `resetDchMovingFilters()` |
| SRS-SEC-S6-010 | ⚠ The Secondary Board shall, when configured for a **dual** transistor bank, produce a negative analog reference output for discharge and a positive one for charge. | LSRS SW_REQ_181, 182; HW §5 |
| SRS-SEC-S6-011 | ⚠ The Secondary Board shall, when configured for a **single** transistor bank, produce a positive analog reference output for both charge and discharge, and shall select the mode by contactor. | LSRS SW_REQ_170, 171, 172; HW §5 |
| SRS-SEC-S6-012 | ⚠ The Secondary Board shall, when configured for a single transistor bank, energise the discharge contactor for discharge and de-energise it for charge. | LSRS SW_REQ_173, 174 |
| SRS-SEC-S6-013 | The Secondary Board shall record `<TBD-S21>` (whether the discharge relay output is populated and used on the dual-bank variant, given the client remark that it is "not used, since the dual bank transistor circuit does not require this relay operation") — see conflict **C-11**. | HW §3 remark |
| SRS-SEC-S6-014 | ⚠ The Secondary Board shall measure and report discharged capacity and discharged energy separately from charged capacity and charged energy. | CODE-S `batDchCapacityAh`, `batDchEnergyWh` |

### 4.7 S7 — Setpoint application, ramping, slew limiting and mode switch-over

*Plain-language summary of this area: how fast the board is allowed to change what it is doing. "Ramping" means moving to a new value gradually instead of jumping. "Slew limiting" means capping how fast that change may happen. "Mode switch-over" is the careful sequence for turning round from charging to discharging without damaging the contactors.*

**Coverage:** ● Strong, and **the most conflicted area in the document.** Four sources
give four different transition times. All are carried; none is chosen here.

#### 4.7.1 Ramping

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S7-001 | ⚠ The Secondary Board shall apply a configurable ramp when changing the current setpoint, default **10 ms**. | HW *Configuration* #12 (I Ramp) |
| SRS-SEC-S7-002 | ⚠ The Secondary Board shall apply a configurable ramp when changing the voltage setpoint, default **10 ms**. | HW *Configuration* #13 (U Ramp) |
| SRS-SEC-S7-003 | ⚠ The Secondary Board shall reach a commanded setpoint from zero within **10 ms**. | HW §6 (rise time / fall time ≤ 10 ms) |
| SRS-SEC-S7-004 | ⚠ The Secondary Board shall return the analog reference output to zero within **10 ms** of being commanded to stop. | HW §6 (fall time ≤ 10 ms) |
| SRS-SEC-S7-005 | ⚠ The Secondary Board shall traverse from 0.1 % to 100 % of maximum current within the range **0.1 ms to 10 ms**. | HW §6 |
| SRS-SEC-S7-006 | ⚠ The Secondary Board shall execute a commanded ramp from a start value to an end value over a commanded duration, as a regulated trajectory rather than a step change. | BM §12.4.3 (ramp functions) |
| SRS-SEC-S7-007 | ⚠ The Secondary Board shall not apply automatic current-range changing while executing a ramp. | BM §12.4.8.13 |
| SRS-SEC-S7-008 | The Secondary Board shall record `<TBD-S22>` (the achievable rise and fall time, given the client's measurement of approximately 4 seconds against a 10 ms specification, marked pending) — see HW §6 remark. | HW §6 remark |

#### 4.7.2 Charge ↔ discharge switch-over

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S7-009 | ⚠ The Secondary Board shall bring the analog reference output to zero before initiating any change between charge mode and discharge mode. | LSRS SW_REQ_176, 183 |
| SRS-SEC-S7-010 | ⚠ The Secondary Board shall switch the mode contactors only after the analog reference output has reached zero. | LSRS SW_REQ_177 |
| SRS-SEC-S7-011 | ⚠ The Secondary Board shall observe a configurable switch-over delay after switching the mode contactors, before applying any non-zero reference output. | LSRS SW_REQ_178, 184 |
| SRS-SEC-S7-012 | ⚠ The Secondary Board shall apply the reference output for the new mode only after the switch-over delay has elapsed. | LSRS SW_REQ_179, 185 |
| SRS-SEC-S7-013 | ⚠ The Secondary Board shall observe a configurable charge-contactor on time, default **100 ms**; off time, default **100 ms**; and wait time, default **50 ms**. | HW *Configuration* #16–18 |
| SRS-SEC-S7-014 | ⚠ The Secondary Board shall observe a configurable discharge-contactor on time, default **100 ms**; off time, default **100 ms**; and wait time, default **50 ms**. | HW *Configuration* #19–21 |
| SRS-SEC-S7-015 | ⚠ The Secondary Board shall complete a transition between charge and discharge at maximum current within **10 ms**. | HW §6 |
| SRS-SEC-S7-016 | ⚠ The Secondary Board shall record `<TBD-S23>` (the required charge↔discharge transition time and its relationship to the contactor switch-over delay) — see conflict **C-04**. The four sourced values are **10 ms** (HW §6), **23 ms** measured (client), **500 ms** switch-over delay (LSRS), and the contactor wait times of **50 ms** (HW Config #18, #21). | HW §6; LSRS; HW *Configuration* |
| SRS-SEC-S7-017 | ⚠ The Secondary Board shall, when configured for a dual transistor bank, perform the charge↔discharge transition without contactor operation where the bank construction permits it. | HW §6 remark ("For Bipolar Heatsink, it will be automatic process") |
| SRS-SEC-S7-018 | ⚠ The Secondary Board shall not energise the charge contactor and the discharge contactor simultaneously. | derived; safety |
| SRS-SEC-S7-019 | ⚠ The Secondary Board shall enter the safe state and report a fault if it detects that both mode contactors are commanded or confirmed active simultaneously. | derived; safety |
| SRS-SEC-S7-020 | The Secondary Board shall record `<TBD-S24>` (whether a contactor acknowledgement input exists, and whether contactor position must be confirmed before energising the bank) — see Open Issue **#21**, HW *Queries* #7 (`contactor_ACK` at X4-11). | Unsourced |

#### 4.7.3 Current-range selection

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S7-021 | The Secondary Board shall support measurement of current on more than one range, so that a small current is measured with greater resolution. | HW *Digital Controller* #14; LSRS SW_REQ_158–163 |
| SRS-SEC-S7-022 | The Secondary Board shall select the current range automatically when automatic range selection is enabled. | HW #14; CODE-S `CURRENT_SCALE_EN` |
| SRS-SEC-S7-023 | The Secondary Board shall accept a commanded fixed current range, overriding automatic selection. | BM §12.4.8.13 (`IRANGE`, `RANGE`); `ICD` B-DAT-08 |
| SRS-SEC-S7-024 | The Secondary Board shall treat a commanded range value of zero as a request for automatic range selection. | BM §12.4.8.13 (`RANGE 0`); `ICD` B-DAT-08 |
| SRS-SEC-S7-025 | ⚠ The Secondary Board shall not change the current range while regulating a single unit of control data. | BM §12.4.8.13 ("no auto-range within a step"); `ICD` B-DAT-08 |
| SRS-SEC-S7-026 | ⚠ The Secondary Board shall not apply automatic range changing while regulating constant power. | BM §12.4.8.13 |
| SRS-SEC-S7-027 | The Secondary Board shall apply a configurable hysteresis at each range changeover threshold, default **5 %**. (*Hysteresis means the switch-up and switch-down points differ slightly, so a reading sitting exactly on the boundary does not flip back and forth.*) | CODE-S `CURRENT_HISTRIS_PERCENT 5.0f`; HW #14 remark (10-Feb-2026) |
| SRS-SEC-S7-028 | The Secondary Board shall scale the measured current according to the gain of the selected range, so that the reported current is independent of the range in use. | HW #14 remark; CODE-S |
| SRS-SEC-S7-029 | The Secondary Board shall apply the calibration gain and offset belonging to the selected range. | CODE-S per-range calibration parameters |
| SRS-SEC-S7-030 | ⚠ The Secondary Board shall complete a range transition within **50 ms**. | BM §12.4.8.13 (range transit time) |
| SRS-SEC-S7-031 | ⚠ The Secondary Board shall not allow a range transition to disturb the regulated quantity beyond `<TBD-S19>`. | HW #14 remark (PID interaction); NEW — ME |
| SRS-SEC-S7-032 | The Secondary Board shall report the current range in use to the Primary Board. | derived from S12 |
| SRS-SEC-S7-033 | The Secondary Board shall record `<TBD-S25>` (the required range set) — see conflict **C-12**. The three sourced schemes are HW's **50 % / 10 % / 1 % / 0.1 %** of Imax, LSRS and legacy firmware's gains **1 / 2 / 4 / 8** giving 100 % / 50 % / 25 % / 12.5 %, and BM's **ranges 1–4** with only 1–2 for IGBT systems. | HW #14; LSRS; CODE-S; BM |
| SRS-SEC-S7-034 | The Secondary Board shall record `<TBD-S26>` (whether the range is selected in software or by driving shunt-selecting digital outputs) — HW offers both. | HW #14 |
| SRS-SEC-S7-035 | The Secondary Board shall record `<TBD-S27>` (whether automatic range selection is required in ME at all, given the client remark that it is implemented, tested, and then **disabled** because it destabilised the regulation loops). | HW #14 remark |

### 4.8 S8 — Measurement acquisition and derived quantities

**Coverage:** ● Strong. HW §4 is the authoritative channel list; `CODE-S` supplies rates,
filtering and the derived-quantity computation; LSRS supplies ranges and modes.

#### 4.8.1 Acquisition

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S8-001 | ⚠ The Secondary Board shall acquire battery voltage, battery current, battery temperature, heatsink temperature, transistor-bank charge-mode voltage and transistor-bank discharge-mode voltage. | HW §4 |
| SRS-SEC-S8-002 | ⚠ The Secondary Board shall acquire the voltage at the battery power connection separately from the four-wire sense voltage. | HW §4.1 |
| SRS-SEC-S8-003 | ⚠ The Secondary Board shall acquire battery voltage and battery current at an interval of **1 ms** or shorter. | CODE-S `ADC_24BIT_READ_TIME 1U` |
| SRS-SEC-S8-004 | ⚠ The Secondary Board shall measure battery voltage over a range of at least **0 to 100 V**. | HW §4.4; LSRS SW_REQ_116 |
| SRS-SEC-S8-005 | ⚠ The Secondary Board shall measure battery voltage bipolarly, so that a reversed battery connection produces a negative reading rather than a clamped one. | LSRS SW_REQ_117, 118 |
| SRS-SEC-S8-006 | ⚠ The Secondary Board shall measure the shunt differential voltage bipolarly, so that reverse current direction is measurable. | LSRS SW_REQ_142 |
| SRS-SEC-S8-007 | The Secondary Board shall support a configurable shunt full-scale range, covering at least 75 mV and 100 mV. | HW §4.5 |
| SRS-SEC-S8-008 | The Secondary Board shall convert the measured shunt voltage to current using the configured shunt resistance for the circuit's current rating. | CODE-S `BTS_SHUNT_RESISTOR` variants |
| SRS-SEC-S8-009 | ⚠ The Secondary Board shall measure battery temperature over a range of at least **−50 °C to +150 °C**. | HW §4.6 |
| SRS-SEC-S8-010 | The Secondary Board shall detect that the external temperature sensor is absent or disconnected, and shall report that condition rather than a temperature value. | CODE-S `isPt100_Connected`; HW *Error code* `0x17` |
| SRS-SEC-S8-011 | The Secondary Board shall apply a moving-average filter to every acquired analog quantity before using it for regulation or reporting. | CODE-S `Filter.c`, `TMAvgFilterData` |
| SRS-SEC-S8-012 | The Secondary Board shall use a configurable filter length per channel. | CODE-S `FILTER_SIZE_*` (100 for battery V and I; 10 for the remainder) |
| SRS-SEC-S8-013 | ⚠ The Secondary Board shall not allow the filter length of the regulated quantity to introduce a delay that prevents the timing of §5.1 from being met. | NEW — ME; derived |
| SRS-SEC-S8-014 | The Secondary Board shall support a configurable measurement sampling rate in the range **1 ms to 1000 ms**. | LSRS SW_REQ_124, 125, 153, 154 |
| SRS-SEC-S8-015 | The Secondary Board shall reject a configured sampling rate outside its permitted range and shall report the rejection. | LSRS SW_REQ_126, 155 |
| SRS-SEC-S8-016 | ⚠ The Secondary Board shall not permit a test to run while the configured sampling rate is invalid. | LSRS SW_REQ_128, 157 |
| SRS-SEC-S8-017 | The Secondary Board shall apply the stored calibration gain and offset to every measured quantity before using or reporting it. | CODE-S `calibrationFormula.c`; §4.14 |
| SRS-SEC-S8-018 | The Secondary Board shall make the raw converter count of every measured channel available for diagnostic retrieval, in addition to the calibrated value. | HW remarks #30, #31, #34 (client requests ADC-count display) |

#### 4.8.2 Derived quantities

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S8-019 | The Secondary Board shall compute instantaneous power as the product of measured battery voltage and measured battery current. | CODE-S `batPower` |
| SRS-SEC-S8-020 | The Secondary Board shall compute accumulated charge capacity, accumulated discharge capacity, accumulated total capacity, and step capacity, in ampere-hours. | CODE-S `batChCapacityAh`, `batDchCapacityAh`, `batAccuCapacityAh`, `batStepCapacityAh` |
| SRS-SEC-S8-021 | The Secondary Board shall compute accumulated charge energy, accumulated discharge energy, accumulated total energy, and step energy, in watt-hours. | CODE-S `batChEnergyWh`, `batDchEnergyWh`, `batAccuEnergyWh`, `batStepEnergyWh` |
| SRS-SEC-S8-022 | The Secondary Board shall integrate capacity and energy using a method whose accumulated error does not exceed `<TBD-S28>`, where `<TBD-S28>` states both the permitted error and the reference test duration over which it is measured. | CODE-S trapezoidal integration (`TRAPEZOIDAL_INTE_FACTOR`); NEW — ME |
| SRS-SEC-S8-023 | The Secondary Board shall reset step capacity and step energy when a new unit of control data begins regulation. | CODE-S `batStepCapacityAh`, `batStepEnergyWh` |
| SRS-SEC-S8-024 | The Secondary Board shall not reset accumulated capacity or accumulated energy on a pause, an interrupt or a continue. | CODE-S; BM §12.4.8.3 |
| SRS-SEC-S8-025 | The Secondary Board shall reset accumulated capacity and accumulated energy only on an explicit command or when a new test begins. | derived |
| SRS-SEC-S8-026 | The Secondary Board shall maintain a total elapsed regulation time and a per-unit-of-control-data elapsed time. | CODE-S `appProgTimeCounter`, `appStepTimeCounter` |
| SRS-SEC-S8-027 | The Secondary Board shall maintain its time counters with a resolution of at least **1 ms**. | CODE-S millisecond counters |
| SRS-SEC-S8-028 | ⚠ The Secondary Board shall detect arithmetic overflow and underflow in every measurement and derived-quantity computation, including intermediate results, and shall report the detection rather than propagating an invalid value. | HW *Coding Guidelines* #35 |
| SRS-SEC-S8-029 | The Secondary Board shall not use floating-point equality or inequality comparison in any decision affecting regulation or protection. | HW *Coding Guidelines* #44 |

#### 4.8.3 Connection integrity

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S8-030 | ⚠ The Secondary Board shall detect a reversed battery power connection. | HW §4.1; HW *Error code* `0x0A` |
| SRS-SEC-S8-031 | ⚠ The Secondary Board shall detect a reversed battery sense connection. | HW §4.4; HW *Error code* `0x0B` |
| SRS-SEC-S8-032 | ⚠ The Secondary Board shall require a reversed-polarity indication to persist for a configurable count of consecutive measurements, default **3**, before declaring it. | CODE-S `REV_POLARITY_EX_COUNT 3` |
| SRS-SEC-S8-033 | ⚠ The Secondary Board shall detect a reversed connection before energising the transistor bank, and shall not energise it while the condition holds. | LSRS SW_REQ_130, 131; HW §4.4 remark |
| SRS-SEC-S8-034 | ⚠ The Secondary Board shall detect a difference between the power-connection voltage and the sense-connection voltage exceeding a configurable threshold, default **5 %**. | HW §4.1; HW *Error code* `0x0C` |
| SRS-SEC-S8-035 | ⚠ The Secondary Board shall detect an open or absent battery before energising the transistor bank, and shall not energise it while the condition holds. | LSRS SW_REQ_133, 134 |
| SRS-SEC-S8-036 | The Secondary Board shall report open-battery detection with a code distinct from reversed polarity. | LSRS SW_REQ_133; **not in the HW error list** — flagged, see §8.2 note |
| SRS-SEC-S8-037 | The Secondary Board shall record `<TBD-S29>` (the voltage threshold and dwell time that define an open battery; LSRS gives "near 0 V continuously for 2 seconds", which is not a specification). | LSRS SW_REQ_133 |
| SRS-SEC-S8-038 | The Secondary Board shall record `<TBD-S30>` (the maximum measurable battery voltage required in ME) — see conflict **C-07**. The three sourced values are **100 V** capability, **80 V** in the present rig, and a **default configured maximum of 18 V**. | HW §4.4; HW *Configuration* #7; client remark |
| SRS-SEC-S8-039 | The Secondary Board shall record `<TBD-S31>` (the maximum measurable current required in ME) — see conflict **C-08**. The sourced values are **200 A** capability and a **default configured maximum of 100 A**. | LSRS SW_REQ_144, 149; HW *Configuration* #5 |
| SRS-SEC-S8-040 | The Secondary Board shall record `<TBD-S32>` (the required measurement accuracy and resolution for each channel, which no source states). | Unsourced |

### 4.9 S9 — Local protection and interlocks

**Coverage:** ● Strong. HW's *Error code* sheet defines 23 conditions and is the
authoritative list; `CODE-S` supplies the exceed-counter discipline; `BTS_FactoryData_t`
supplies the configurable absolute ratings.

#### 4.9.1 Protective limits

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S9-001 | ⚠ The Secondary Board shall enforce a configurable maximum battery voltage and shall enter the safe state on breach. | HW *Error code* `9`; CODE-S `cktMaxVolt`; HW *Configuration* #7 |
| SRS-SEC-S9-002 | ⚠ The Secondary Board shall enforce a configurable minimum battery voltage. | CODE-S `cktMinVolt`; HW *Configuration* #8 |
| SRS-SEC-S9-003 | ⚠ The Secondary Board shall enforce a configurable maximum charge current and shall enter the safe state on breach. | HW *Error code* `8`; CODE-S `cktMaxChCurrent`; HW *Configuration* #5 |
| SRS-SEC-S9-004 | ⚠ The Secondary Board shall enforce a configurable maximum discharge current and shall enter the safe state on breach. | HW *Error code* `8`; CODE-S `cktMaxDchCurrent`; HW *Configuration* #6 |
| SRS-SEC-S9-005 | ⚠ The Secondary Board shall enforce a configurable maximum power and shall enter the safe state on breach. | CODE-S `cktMaxPower`, `en_ERR_OVER_POWER` |
| SRS-SEC-S9-006 | ⚠ The Secondary Board shall enforce a configurable maximum battery temperature and shall enter the safe state on breach. | CODE-S `cktMaxTemperature` |
| SRS-SEC-S9-007 | ⚠ The Secondary Board shall enforce a configurable minimum battery temperature. | CODE-S `cktMinTemperature` |
| SRS-SEC-S9-008 | ⚠ The Secondary Board shall enforce a configurable maximum heatsink temperature and shall enter the safe state on breach. | HW §4.3; HW *Error code* `3` |
| SRS-SEC-S9-009 | ⚠ The Secondary Board shall enforce a configurable maximum transistor-bank charge-mode voltage (LNT) and shall enter the safe state on breach. | HW §4.2a; HW *Error code* `1`; CODE-S `LnT_MaxVolt` |
| SRS-SEC-S9-010 | ⚠ The Secondary Board shall enforce a configurable maximum transistor-bank discharge-mode voltage (ZNT) and shall enter the safe state on breach. | HW §4.2b; HW *Error code* `2`; CODE-S `ZnT_MaxVolt` |
| SRS-SEC-S9-011 | ⚠ The Secondary Board shall enforce a configurable maximum system rectifier output voltage. | CODE-S `sysMaxVolt` |
| SRS-SEC-S9-012 | ⚠ The Secondary Board shall detect that the current-measurement converter has reached its conversion limit and shall enter the safe state. | HW *Error code* `4` (Current AD limit) |
| SRS-SEC-S9-013 | ⚠ The Secondary Board shall detect that the voltage-measurement converter has reached its conversion limit and shall enter the safe state. | HW *Error code* `5` (Voltage AD Limit) |
| SRS-SEC-S9-014 | ⚠ The Secondary Board shall enforce every protective limit at every measurement interval while regulating. | derived; safety |
| SRS-SEC-S9-015 | ⚠ The Secondary Board shall enforce every protective limit independently of, and in addition to, any limit the Primary Board enforces. | derived; safety; brief |
| SRS-SEC-S9-016 | ⚠ The Secondary Board shall enforce its configured absolute ratings even when the Primary Board commands a setpoint or supplies a limit that exceeds them. | derived; safety; A-10 |
| SRS-SEC-S9-017 | The Secondary Board shall hold every protective limit in non-volatile factory configuration rather than as a compile-time constant. | A-10; CODE-S `BTS_FactoryData_t` |
| SRS-SEC-S9-018 | ⚠ The Secondary Board shall reject a factory configuration whose protective limits are mutually inconsistent, and shall not regulate under it. | derived |

#### 4.9.2 Transient rejection

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S9-019 | The Secondary Board shall require a limit breach to persist for a configurable number of consecutive measurements before declaring a fault. | CODE-S exceed counters |
| SRS-SEC-S9-020 | The Secondary Board shall apply a configurable consecutive-detection count of **10** to over-voltage, over-current and over-power. | CODE-S `MAX_VOLT_EX_COUNT`, `MAX_CURR_EX_COUNT`, `MAX_POWER_EX_COUNT` |
| SRS-SEC-S9-021 | The Secondary Board shall apply a configurable consecutive-detection count of **5** to the transistor-bank voltage limits and to the temperature limits. | CODE-S `MAX_L_N_T_EX_COUNT`, `MAX_Z_N_T_EX_COUNT`, `MAX_TEMP_EX_COUNT` |
| SRS-SEC-S9-022 | ⚠ The Secondary Board shall not apply a consecutive-detection count to a condition whose immediate action is required for safety, and shall record `<TBD-S33>` (which conditions those are). | derived; safety; Open Issue **#8** |
| SRS-SEC-S9-023 | The Secondary Board shall evaluate the transistor-bank voltage limits and the temperature limits at an interval of **100 ms** or shorter. | CODE-S `CHECK_LnTnZntnTEMP_TIME 100U`; BM §12.4.4 |
| SRS-SEC-S9-024 | The Secondary Board shall reset an exceed counter when the condition it counts clears before the count is reached. | CODE-S; derived |

#### 4.9.3 Protective action

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S9-025 | ⚠ The Secondary Board shall, on declaring any fault of §4.9.1, set the analog reference output to zero. | CODE-S `actionTakenOnSysError()`, `relayDefaultState()` |
| SRS-SEC-S9-026 | ⚠ The Secondary Board shall, on declaring any fault of §4.9.1, place the mode contactors in their default de-energised positions. | CODE-S `relayDefaultState()` |
| SRS-SEC-S9-027 | ⚠ The Secondary Board shall set the analog reference output to zero before de-energising the mode contactors, so that no contactor breaks load current. | derived; safety; SRS-SEC-S7-009 |
| SRS-SEC-S9-028 | ⚠ The Secondary Board shall enter the safe state on a fault without requiring any message from the Primary Board. | derived; safety |
| SRS-SEC-S9-029 | ⚠ The Secondary Board shall enter the safe state on a fault even when the CAN link is unavailable. | derived; safety |
| SRS-SEC-S9-030 | ⚠ The Secondary Board shall activate its error-indication output on declaring a fault. | HW §3 DO1; BM §12.4.8.5 |
| SRS-SEC-S9-031 | ⚠ The Secondary Board shall enter the safe state within `<TBD-S34>` of detecting a condition requiring it. | Open Issue **#8** |
| SRS-SEC-S9-032 | The Secondary Board shall report every fault it declares to the Primary Board. | §4.13 |
| SRS-SEC-S9-033 | The Secondary Board shall record `<TBD-S02>` (the required safe-state output configuration: whether the mode contactors must be open, whether any digital output must be driven to a defined state, and what the safe state is for each transistor-bank construction) — see Open Issue **#8**. | Unsourced |

#### 4.9.4 External interlock

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S9-034 | ⚠ The Secondary Board shall enter the safe state on activation of an external interlock input, if such an input is required. | BM message 6 ("External control signal activated!"); Open Issue **#8** |
| SRS-SEC-S9-035 | ⚠ The Secondary Board shall treat the external interlock as taking precedence over any command from the Primary Board, if such an input is required. | derived; safety |
| SRS-SEC-S9-036 | The Secondary Board shall debounce every digital input over a configurable interval, default **10 ms**. | CODE-S `DI_1_DB_TIME`…`TEMP_SW_DB_TIME` |
| SRS-SEC-S9-037 | ⚠ The Secondary Board shall enter the safe state on activation of the thermostat input. | CODE-S `DI_t.Thermostat`; derived |
| SRS-SEC-S9-038 | The Secondary Board shall report the state of every digital input to the Primary Board. | CODE-S `BTS_MeasuredData_t.dI`; LSRS SW_REQ_6 |
| SRS-SEC-S9-039 | The Secondary Board shall detect a transition of a digital input in both directions. | LSRS SW_REQ_3, SW_REQ_4 |
| SRS-SEC-S9-040 | The Secondary Board shall record `<TBD-S35>` (whether an emergency-stop or external-interlock input exists on this board and which physical input carries it) — see Open Issue **#8**. HW lists all four digital inputs as *Spare*, and the client notes there are no motherboard digital-input connections on the dual-bank circuit. | HW §2; client remark |
| SRS-SEC-S9-041 | The Secondary Board shall record `<TBD-S36>` (whether the annotations "Interrupt" against digital input 1 and "Continue" against digital input 4 in HW §2 are requirements or residual documentation). | HW §2 |

---
### 4.10 S10 — Autonomous safe state on fault or loss of Primary communication

**Coverage:** ◐ Partial. Detection is fully sourced — HW *Configuration* #9 gives a 100 ms
CAN timeout and HW *Error code* `0x0D` gives the fault. The *behaviour* on detection is
not sourced for ME: whether the channel trips immediately or finishes the regulation in
progress is Open Issue **#8** and **#14**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S10-001 | ⚠ The Secondary Board shall detect the loss of communication with the Primary Board while regulating. | HW *Error code* `0x0D`; HW *Configuration* #9 |
| SRS-SEC-S10-002 | ⚠ The Secondary Board shall declare loss of Primary communication when no valid message addressed to it has been received within the configured timeout, default **100 ms**. | HW *Configuration* #9 |
| SRS-SEC-S10-003 | ⚠ The Secondary Board shall take a defined protective action on declaring loss of Primary communication, without waiting for any further message. | derived; safety |
| SRS-SEC-S10-004 | ⚠ The Secondary Board shall retain the measurements accumulated up to the moment of communication loss. | CODE-S; derived |
| SRS-SEC-S10-005 | ⚠ The Secondary Board shall retain the reason it entered the safe state, and shall report it when communication is restored. | derived from S13 |
| SRS-SEC-S10-006 | ⚠ The Secondary Board shall not resume regulation automatically when communication is restored; it shall await an explicit command. | derived; safety |
| SRS-SEC-S10-007 | ⚠ The Secondary Board shall remain able to enforce every protective limit of §4.9 while Primary communication is lost. | derived; safety |
| SRS-SEC-S10-008 | ⚠ The Secondary Board shall continue to acquire measurements while Primary communication is lost, so that the retained record is complete up to the safe-state transition. | derived |
| SRS-SEC-S10-009 | The Secondary Board shall report the duration of the communication outage when communication is restored. | NEW — ME |
| SRS-SEC-S10-010 | ⚠ The Secondary Board shall record `<TBD-S37>` (whether loss of Primary communication trips the channel immediately or permits the regulation in progress to complete before entering the safe state) — see Open Issue **#8** and **#14**. `PSPE` §5.2 proposes finish-then-safe-stop and is recorded as prior art, not adopted. | Unsourced |
| SRS-SEC-S10-011 | The Secondary Board shall record `<TBD-S38>` (whether a communication outage shorter than the configured timeout must be reported as a quality-of-service event even though it raises no fault). | Unsourced |

### 4.11 S11 — Watchdog and timing supervision

**Coverage:** ◐ Partial. The mechanism is sourced from `CODE-S` and HW §10; the client's
own remark is that HW §10 is *"Not implemented yet. Pending."* The watchdog period and
window are unsourced — Open Issue **#12**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S11-001 | ⚠ The Secondary Board shall service a hardware watchdog cyclically while it is operating correctly. | HW §10; CODE-S `iWdgReset()` |
| SRS-SEC-S11-002 | ⚠ The Secondary Board shall be reset by the watchdog if any function on which safe operation depends stops running. | HW §10; derived |
| SRS-SEC-S11-003 | ⚠ The Secondary Board shall not service the watchdog from a context that would continue to run if the regulation loop had stopped. | NEW — ME; derived |
| SRS-SEC-S11-004 | ⚠ The Secondary Board shall enter the safe state as a consequence of a watchdog reset, by the initialization requirements of §4.1. | SRS-SEC-S1-001, -002 |
| SRS-SEC-S11-005 | The Secondary Board shall record that its last restart was caused by watchdog expiry, and shall report it. | SRS-SEC-S1-017, -018; `ICD` B-PFA-04 |
| SRS-SEC-S11-006 | ⚠ The Secondary Board shall detect that the regulation loop has failed to execute within its required interval, and shall treat that as a fault. | NEW — ME; derived from §5.1 |
| SRS-SEC-S11-007 | ⚠ The Secondary Board shall detect that the measurement acquisition has failed to complete within its required interval, and shall treat that as a fault. | NEW — ME; derived from §5.1 |
| SRS-SEC-S11-008 | The Secondary Board shall bound every software timer against its permitted limits. | HW *Coding Guidelines* #27 |
| SRS-SEC-S11-009 | The Secondary Board shall not use a busy wait where a timer or synchronisation mechanism can be used. | HW *Coding Guidelines* #23 |
| SRS-SEC-S11-010 | ⚠ The Secondary Board shall record `<TBD-S39>` (the required watchdog period, and whether a windowed watchdog with a minimum as well as a maximum service interval is required) — see Open Issue **#12**. | Unsourced |
| SRS-SEC-S11-011 | The Secondary Board shall record `<TBD-S40>` (whether the watchdog and auto-reset function of HW §10, marked *not implemented, pending*, is required in the ME hardware and whether the power-supply supervisory action it describes is a hardware or a software function). | HW §10 remark |

### 4.12 S12 — Telemetry and registration reporting

**Coverage:** ● Strong in content, ● conflicted in rate. The reported quantities are fully
enumerated by `CODE-S`; the required rate has **five** sourced values — conflict **C-05** —
and the aggregate load across eight nodes is Open Issue **#18**.

#### 4.12.1 Real-time telemetry

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S12-001 | The Secondary Board shall report a real-time measurement record to the Primary Board cyclically. | CODE-S `SEND_REAL_TIME_DATA 500U`, `btsSecSendMeasuredData()`; `ICD` B-TLM-01 |
| SRS-SEC-S12-002 | The Secondary Board shall include in the real-time record the measured battery voltage, measured battery current and computed power. | CODE-S `BTS_MeasuredData_t` |
| SRS-SEC-S12-003 | The Secondary Board shall include in the real-time record its operating state and its regulation direction. | CODE-S `BTS_MeasuredData_t.circuitStatus`, `progRunStatus` |
| SRS-SEC-S12-004 | The Secondary Board shall include in the real-time record its active fault set. | CODE-S `BTS_MeasuredData_t.SystemError` |
| SRS-SEC-S12-005 | The Secondary Board shall include in the real-time record the state of its digital inputs and digital outputs. | CODE-S `BTS_MeasuredData_t.dI`, `.dO` |
| SRS-SEC-S12-006 | The Secondary Board shall include in the real-time record the elapsed regulation time and the elapsed time of the current unit of control data. | CODE-S `progStopTime`, `stepStopTime` |
| SRS-SEC-S12-007 | The Secondary Board shall include in the real-time record the accumulated capacity and energy quantities of §4.8.2. | CODE-S `progBackUp_t`; `registration_t` |
| SRS-SEC-S12-008 | The Secondary Board shall report a real-time record at a configurable interval, default **500 ms**. | CODE-S `SEND_REAL_TIME_DATA 500U`; `ICD` B-TLM-08 |
| SRS-SEC-S12-009 | The Secondary Board shall indicate in the real-time record whether the regulated current is at zero. | CODE-S `BTS_MeasuredData_t.isCC_ZeroValue` |
| SRS-SEC-S12-010 | The Secondary Board shall report the identity of the unit of control data it is currently executing, so that the Primary Board can correlate telemetry with the program step it dispatched. | CODE-S `progBackUp_t.stepNumber`; **D-03**; `ICD` B-TLM-01 |

#### 4.12.2 Registration data

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S12-011 | The Secondary Board shall report a registration record to the Primary Board at a configurable interval, default **100 ms**. | CODE-S `SEND_REGISTRATION_DATA 100U`; `ICD` B-TLM-02, B-TLM-08 |
| SRS-SEC-S12-012 | The Secondary Board shall include in a registration record only the quantities selected by a registration type value supplied by the Primary Board. | CODE-S `btsRegType_t`, `logRegistration(uint16_t regType)` |
| SRS-SEC-S12-013 | The Secondary Board shall support selection of the following thirteen measured and derived quantities for registration: elapsed time, current, voltage, temperature, power, accumulated capacity, charge capacity, discharge capacity, step capacity, accumulated energy, charge energy, discharge energy and step energy. | CODE-S `btsRegType_t` bits 0–12; `registration_t` |
| SRS-SEC-S12-014 | The Secondary Board shall support selection of the fault, message and user-error indications for registration, independently of the measured quantities. | CODE-S `BTS_REG_SYS_ERR_E`, `BTS_REG_MSG_E`, `BTS_REG_USER_ERR_E` |
| SRS-SEC-S12-015 | The Secondary Board shall timestamp every registration record. | CODE-S `registration_t.appRegTimeCounter`; SRS-SEC-S4-018 |
| SRS-SEC-S12-016 | The Secondary Board shall report a registration record on demand as well as cyclically. | CODE-S `MEASURED_DATA_REG_Q_ID_TX`; `ICD` B-TLM-03 |
| SRS-SEC-S12-017 | The Secondary Board shall report a final registration record when regulation stops, so that the record set is closed at the true end value. | CODE-S `isSendRegAfterStopFlag`, `updateRegistrationParamOnStop()`; `ICD` A-LOG-06, B-TLM-04 |
| SRS-SEC-S12-018 | The Secondary Board shall accept a change of registration type while regulating and shall apply it from the next registration record. | CODE-S `registrationType`; BM §12.4.6; `ICD` B-DAT-07 |
| SRS-SEC-S12-019 | The Secondary Board shall support at least **15** selected quantities in one registration record. | CODE-S `MAX_REG_PARAMETERS 15U`; `ICD` B-TLM-02 |
| SRS-SEC-S12-020 | ⚠ The Secondary Board shall not discard a registration record silently; if it cannot transmit one, it shall report the loss. | NEW — ME; derived; `ICD` B-TLM-06 |
| SRS-SEC-S12-021 | The Secondary Board shall buffer registration records that cannot be transmitted immediately, up to a defined capacity. | CODE-S `REG_PAYLOAD_LENGTH_MAX 1004U`; NEW — ME |
| SRS-SEC-S12-022 | The Secondary Board shall report the number of registration records it has discarded due to buffer exhaustion since the last successful report. | NEW — ME; `ICD` A-LOG-04, B-TLM-06 |
| SRS-SEC-S12-023 | ⚠ The Secondary Board shall record `<TBD-S41>` (the required registration interval) — see conflict **C-05**. The five sourced values are **≤500 µs** (HW §6), **10 ms** (client remark), **1 ms** ("need to approve up to 1 ms"), **100 ms** (implemented), and BM's own maximum registration resolution of **0.1 s**. | HW §6; CODE-S; BM §12.4.6 |
| SRS-SEC-S12-024 | The Secondary Board shall record `<TBD-S42>` (the per-channel telemetry and registration rate this board must sustain when all eight Secondary Boards are reporting on one CAN bus, and the acceptable end-to-end latency) — see Open Issue **#18**. **This is not a restatement of `<TBD-S41>`:** the legacy design gave registration a dedicated UART, so no bus-sharing budget ever existed. | NEW — ME |
| SRS-SEC-S12-025 | The Secondary Board shall record `<TBD-S43>` (the required registration buffer depth, which follows from `<TBD-S41>` and `<TBD-S42>`). | Unsourced |

### 4.13 S13 — Fault reporting, codes, latch and clear semantics

**Coverage:** ◐ Partial. The fault *set* is authoritative from HW; the fault *lifecycle* —
which faults latch, who may clear them, what clearing requires — is defined nowhere.
Open Issue **#17**. And four incompatible code spaces exist — conflict **C-14**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S13-001 | The Secondary Board shall assign a distinct code to every fault condition it can detect. | HW *Digital Controller* #11; HW *Error code* sheet |
| SRS-SEC-S13-002 | The Secondary Board shall support at least the 23 fault conditions defined in HW *Error code* and shall not exceed the sheet's ceiling of **100** codes. | HW *Error code*; HW *Digital Controller* #11 |
| SRS-SEC-S13-003 | The Secondary Board shall report its complete active fault set, not only the most recent fault. | CODE-S `SystemError` bitmask; `ICD` A-EVT-01, B-TLM-05 |
| SRS-SEC-S13-004 | The Secondary Board shall report a fault to the Primary Board at the first opportunity after declaring it, without waiting for the next cyclic report. | derived; safety; `ICD` A-EVT-01, B-TLM-05 |
| SRS-SEC-S13-005 | The Secondary Board shall distinguish a fault, which requires protective action, from a message, which is informational. | CODE-S `logSysError()` vs `logMessage()`; BM §12.4.8.5/6 |
| SRS-SEC-S13-006 | The Secondary Board shall distinguish a system fault, raised by its own detection, from a user error conveyed to it by the Primary Board. | CODE-S `logSysError()` vs `logUserError()`; `userExId` |
| SRS-SEC-S13-007 | The Secondary Board shall record the time at which each fault was declared. | derived; SRS-SEC-S4-018 |
| SRS-SEC-S13-008 | The Secondary Board shall retain its active fault set across loss of Primary communication. | SRS-SEC-S10-005 |
| SRS-SEC-S13-009 | ⚠ The Secondary Board shall retain a latched fault until it is explicitly cleared, even after the condition that caused it has ceased. | derived; Open Issue **#17** |
| SRS-SEC-S13-010 | ⚠ The Secondary Board shall not resume regulation while any latched fault is uncleared. | derived; safety |
| SRS-SEC-S13-011 | The Secondary Board shall accept a command to clear a clearable fault and shall report whether the clear succeeded. | derived; Open Issue **#17**; `ICD` A-CTL-07, B-CTL-07 |
| SRS-SEC-S13-012 | ⚠ The Secondary Board shall refuse to clear a fault whose underlying condition is still present, and shall report the refusal. | derived; safety; `ICD` A-CTL-07, B-CTL-07 |
| SRS-SEC-S13-013 | The Secondary Board shall report the number of times each fault has occurred since the last reset, for diagnostic use. | NEW — ME; §4.18 |
| SRS-SEC-S13-014 | The Secondary Board shall report a fault raised during initialization even though no regulation has been commanded. | SRS-SEC-S1-014 |
| SRS-SEC-S13-015 | The Secondary Board shall record `<TBD-S44>` (which faults latch, which are self-clearing, who may clear a latched fault, and whether clearing requires an operator action rather than a Primary Board command) — see Open Issue **#17**. | Unsourced |
| SRS-SEC-S13-016 | The Secondary Board shall record `<TBD-S45>` (the unified fault-code space) — see conflict **C-14**. The four sourced spaces are HW's sequential list `1`…`0x17`, the legacy firmware's `en_ERR_*` bitmask in a different order, BM's `ER1`–`ER10`, and BM's **user-editable** message numbers 1–6. The last of these is a runtime-editable identifier and must not be merged with a fixed fault code. | HW; CODE-S; BM |
| SRS-SEC-S13-017 | The Secondary Board shall record `<TBD-S46>` (whether it must maintain a local event log independent of the Primary Board's, and its depth and retention if so) — see Open Issue **#17**. | Unsourced |

### 4.14 S14 — Calibration

**Coverage:** ● Strong. Both HW *Calibration* (25 parameters) and `CODE-S` (a 23-command
state machine with 25 error codes and a per-range parameter set) are detailed. The open
question is scope, not mechanism — Open Issue **#15**.

#### 4.14.1 Calibration data

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S14-001 | The Secondary Board shall hold a gain and an offset for every calibrated measurement channel. | HW *Calibration*; CODE-S `btsFact_n_calib_t` |
| SRS-SEC-S14-002 | The Secondary Board shall hold independent calibration for charge current and discharge current. | HW *Calibration* #1, 2, 5, 6 |
| SRS-SEC-S14-003 | The Secondary Board shall hold independent calibration for charge voltage and discharge voltage. | HW *Calibration* #3, 4, 7, 8 |
| SRS-SEC-S14-004 | The Secondary Board shall hold calibration for the external battery temperature sensor. | HW *Calibration* #17, 18 |
| SRS-SEC-S14-005 | The Secondary Board shall hold calibration for the heatsink temperature measurement. | HW *Calibration* #20, 21 |
| SRS-SEC-S14-006 | The Secondary Board shall hold calibration for the transistor-bank charge-mode voltage (LNT). | HW *Calibration* #22, 23 |
| SRS-SEC-S14-007 | The Secondary Board shall hold calibration for the transistor-bank discharge-mode voltage (ZNT). | HW *Calibration* #24, 25 |
| SRS-SEC-S14-008 | The Secondary Board shall hold calibration for the reverse-polarity sense measurement. | CODE-S `revPolarityVoltGain`, `revPolarityVoltOffset` |
| SRS-SEC-S14-009 | The Secondary Board shall hold independent current calibration for each supported measurement range and for the automatic-range path. | CODE-S per-range gain/offset sets |
| SRS-SEC-S14-010 | The Secondary Board shall record the date and time at which each calibrated item was last calibrated. | HW *Configuration* #44–47; CODE-S `*CalibTime` |
| SRS-SEC-S14-011 | The Secondary Board shall report every calibration parameter and its calibration timestamp on request. | CODE-S `CALIB_CMD_Q_ID_READ_PARAM`, `readCalibParameters()` |
| SRS-SEC-S14-012 | The Secondary Board shall retain all calibration data in non-volatile memory. | §4.15; CODE-S `emulEeprom.c` |
| SRS-SEC-S14-013 | The Secondary Board shall hold the controller tuning parameters of §4.5.2 alongside its calibration data. | HW *Calibration* #10–15 |

#### 4.14.2 Calibration procedure

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S14-014 | The Secondary Board shall enter a calibration mode on command and shall leave it on command. | CODE-S `CALIB_CMD_Q_ID_START`, `CALIB_CMD_Q_ID_STOP` |
| SRS-SEC-S14-015 | The Secondary Board shall support a two-point calibration procedure comprising a low reference point, a high reference point and a commit step, for each calibrated quantity. | CODE-S `*_LP`, `*_HP`, `*_GO` commands |
| SRS-SEC-S14-016 | The Secondary Board shall support the two-point procedure for charge current, discharge current, charge voltage, discharge voltage and temperature. | CODE-S `CALIB_CMD_Q_ID_CHA_CURRENT_LP`…`CALIB_CMD_Q_ID_TEMP_GO` |
| SRS-SEC-S14-017 | The Secondary Board shall report live measured current, voltage and temperature, together with their raw converter counts, while in calibration mode. | CODE-S `CALIB_CMD_Q_ID_LIVE_DATA`, `sendLiveCurrentVolt()`; `ICD` B-CAL-25 |
| SRS-SEC-S14-018 | The Secondary Board shall report the live calibration data at a configurable interval, default **500 ms**. | CODE-S `LIVE_DATA_SEND_TIME 500U`; `ICD` B-CAL-25 |
| SRS-SEC-S14-019 | The Secondary Board shall abandon a calibration procedure on a cancel command, leaving the stored calibration unchanged. | CODE-S `CALIB_CMD_Q_ID_CANCEL` |
| SRS-SEC-S14-020 | The Secondary Board shall support a verification mode in which it drives a commanded charge or discharge current so that the applied calibration can be checked against an external reference. | CODE-S `CALIB_CMD_Q_ID_START_VERIFY_CHA_CURENT`, `..._DCH_...`, `..._STOP_VERIFY_...` |
| SRS-SEC-S14-021 | ⚠ The Secondary Board shall enforce every protective limit of §4.9 while in calibration mode. | CODE-S `CALIB_OVER_VOLTAGE_Error`, `CALIB_OVER_CURRENT_Error`, `CALIB_OVER_POWER_Error`, `CALIB_PID_OutOfRange_Error` |
| SRS-SEC-S14-022 | The Secondary Board shall reject a calibration point that lies outside a configurable tolerance of the expected value, default **10 %** of the set value. | CODE-S `CALIB_ERROR_SPAN 0.10f` |
| SRS-SEC-S14-023 | The Secondary Board shall require a calibration measurement to be stable over a configurable number of samples, default **1000**, before accepting a calibration point. | CODE-S `CALIB_ERROR_COUNT 1000U` |
| SRS-SEC-S14-024 | The Secondary Board shall report a distinct error for each failure mode of the calibration procedure. | CODE-S `calibrationError_t` (25 values) |
| SRS-SEC-S14-025 | ⚠ The Secondary Board shall not commit a calibration parameter set that fails its own validity check, and shall retain the previous set. | CODE-S `*_GO_Write_Error`; derived |
| SRS-SEC-S14-026 | The Secondary Board shall report the previously stored calibration values before overwriting them, so that a calibration can be reverted. | CODE-S `CALIB_CMD_Q_ID_READ_PARAM`; HW §12 |
| SRS-SEC-S14-027 | ⚠ The Secondary Board shall not enter calibration mode while regulating, and shall not accept a regulation command while in calibration mode. | SRS-SEC-S2-011, -012 |
| SRS-SEC-S14-028 | The Secondary Board shall calibrate the resistance-to-temperature conversion against defined reference resistances. | CODE-S `TEMP_LOW_RES_VALUE 18.52f`, `TEMP_HIGH_RES_VALUE 390.48f` |
| SRS-SEC-S14-029 | The Secondary Board shall record `<TBD-S47>` (whether the per-range current calibration scheme — full scale plus four ranges plus the automatic-range path — is retained in ME) — see Open Issue **#15**. | Unsourced |
| SRS-SEC-S14-030 | The Secondary Board shall record `<TBD-S48>` (the required calibration accuracy, and the accuracy of the reference equipment assumed by the procedure). | Unsourced |

### 4.15 S15 — Non-volatile configuration and persistence

**Coverage:** ● Strong for content and mechanism; ◐ Partial for policy. Wear, integrity
and restore-to-defaults behaviour is Open Issue **#22**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S15-001 | The Secondary Board shall retain its factory configuration in non-volatile memory. | HW *Configuration*; CODE-S `BTS_FactoryData_t` |
| SRS-SEC-S15-002 | The Secondary Board shall retain its calibration data in non-volatile memory. | HW *Calibration*; §4.14 |
| SRS-SEC-S15-003 | The Secondary Board shall retain the battery parameter data supplied to it in non-volatile memory. | CODE-S `BTS_BatteryData_t` |
| SRS-SEC-S15-004 | The Secondary Board shall retain manufacturing data in non-volatile memory. | CODE-S `CONFIG_DATA_Q_ID_WRITE_MANUFACT_DATA` |
| SRS-SEC-S15-005 | The Secondary Board shall support at least the **47** configuration parameters defined in HW *Configuration*, within the sheet's ceiling of **200**. | HW *Configuration*; HW *Digital Controller* #12 |
| SRS-SEC-S15-006 | The Secondary Board shall accept a write of factory configuration from the Primary Board. | CODE-S `CONFIG_DATA_Q_ID_WRITE_FACT_DATA` |
| SRS-SEC-S15-007 | The Secondary Board shall return its factory configuration to the Primary Board on request. | CODE-S `CONFIG_DATA_Q_ID_READ_FACT_DATA` |
| SRS-SEC-S15-008 | The Secondary Board shall accept a write of manufacturing data and shall return it on request. | CODE-S `CONFIG_DATA_Q_ID_WRITE_MANUFACT_DATA`, `..._READ_MANUFACT_DATA` |
| SRS-SEC-S15-009 | The Secondary Board shall protect every persisted block with an integrity check. | CODE-S `MAGIC_NUMBER 0xFF11`, stored CRC, `endHeader` |
| SRS-SEC-S15-010 | The Secondary Board shall verify the integrity of a persisted block on every read. | derived from SRS-SEC-S15-009 |
| SRS-SEC-S15-011 | The Secondary Board shall verify a persisted block after writing it, and shall report a failure to verify. | HW *Error code* `0x16` (Error while reading EEPROM); derived |
| SRS-SEC-S15-012 | The Secondary Board shall report a persistent-storage read or write failure as a fault. | HW *Error code* `0x16`; CODE-S `en_ERR_EEPROM_R_WR` |
| SRS-SEC-S15-013 | The Secondary Board shall count the number of times its persistent store has been written, and shall report that count. | CODE-S `btsFact_n_calib_t.writeCount`; `ICD` B-CFG-11 |
| SRS-SEC-S15-014 | ⚠ The Secondary Board shall not write to persistent storage while regulating, except where a requirement of §4.19 requires it. | derived; safety and endurance |
| SRS-SEC-S15-015 | ⚠ The Secondary Board shall leave a persisted block either fully updated or unchanged if supply is lost during a write. | NEW — ME; derived |
| SRS-SEC-S15-016 | The Secondary Board shall support restoring its configuration to defined default values on command. | derived; Open Issue **#22**; `ICD` A-CFG-14, B-CFG-10 |
| SRS-SEC-S15-017 | ⚠ The Secondary Board shall not permit a restore-to-defaults command to overwrite calibration data unless the command explicitly requests it. | derived; safety; `ICD` A-CFG-14, B-CFG-10 |
| SRS-SEC-S15-018 | The Secondary Board shall report which configuration items are at their default values rather than at commissioned values. | NEW — ME; derived from SRS-SEC-S1-012; `ICD` B-CFG-11 |
| SRS-SEC-S15-019 | The Secondary Board shall record `<TBD-S49>` (the endurance and wear-levelling policy for the persistent store, and the number of write cycles the design must sustain) — see Open Issue **#22**. | Unsourced |
| SRS-SEC-S15-020 | The Secondary Board shall record `<TBD-S50>` (the required default value for each configuration parameter; HW gives defaults for some and leaves others blank, and gives no minimum or maximum for any). | HW *Configuration* (Min and Max columns empty throughout) |

### 4.16 S16 — Firmware update over CAN

**Coverage:** ○ Absent as an agreed requirement, and **in direct conflict**. HW §8
specifies flashing from a host PC tool; the client remark says *"This is not required as
discussed earlier"*; the ME brief requires update over CAN. Conflict **C-06**, Open Issue
**#9**. The requirements below are written as the brief requires, and the conflict is
carried.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S16-001 | The Secondary Board shall accept an application firmware image transferred from the Primary Board over CAN. | Brief; **C-06**; `ICD` A-NOD-07 |
| SRS-SEC-S16-002 | The Secondary Board shall report its bootloader version and application version before an update begins. | HW *Configuration* #2, #3 |
| SRS-SEC-S16-003 | ⚠ The Secondary Board shall not accept a firmware update while regulating. | derived; safety; `ICD` A-NOD-08, B-FWU-01 |
| SRS-SEC-S16-004 | ⚠ The Secondary Board shall be in the safe state throughout a firmware update. | SRS-SEC-S2-007 |
| SRS-SEC-S16-005 | The Secondary Board shall accept a firmware image in multiple transfer units and shall reassemble it in the correct order. | derived; CODE-S transfer pattern; `ICD` B-FWU-02 |
| SRS-SEC-S16-006 | The Secondary Board shall detect a missing, duplicated or out-of-order transfer unit and shall reject the update. | derived; `ICD` B-FWU-02 |
| SRS-SEC-S16-007 | The Secondary Board shall verify the integrity of a complete firmware image before activating it. | HW *Error code* `0x15` (Verify programming failed); `ICD` B-FWU-04 |
| SRS-SEC-S16-008 | The Secondary Board shall verify that a firmware image is intended for its own hardware variant before activating it. | derived; `ICD` B-FWU-01 |
| SRS-SEC-S16-009 | The Secondary Board shall report a failure to erase its program memory. | HW *Error code* `0x14` (Flash Erase Failed) |
| SRS-SEC-S16-010 | The Secondary Board shall report a condition in which programming is not possible. | HW *Error code* `0x11` (Flashing not possible) |
| SRS-SEC-S16-011 | ⚠ The Secondary Board shall remain able to accept a further firmware update if an update is interrupted before completion. | derived; NEW — ME; `ICD` B-FWU-06 |
| SRS-SEC-S16-012 | ⚠ The Secondary Board shall not activate a partially received or failed firmware image. | derived; safety; `ICD` B-FWU-04 |
| SRS-SEC-S16-013 | The Secondary Board shall report the outcome of every firmware update attempt. | derived; `ICD` A-NOD-09, B-FWU-05 |
| SRS-SEC-S16-014 | The Secondary Board shall preserve its calibration data, factory configuration and node identity across a firmware update. | derived; NEW — ME |
| SRS-SEC-S16-015 | The Secondary Board shall record `<TBD-S51>` (whether firmware update over CAN is required at all) — see conflict **C-06** and Open Issue **#9**. | HW §8 vs brief |
| SRS-SEC-S16-016 | The Secondary Board shall record `<TBD-S52>` (whether the bootloader itself is field-updatable or factory-only) — see Open Issue **#9**. | Unsourced |
| SRS-SEC-S16-017 | The Secondary Board shall record `<TBD-S53>` (whether rollback to the previous application image is required, and whether two image slots must therefore exist) — see Open Issue **#9**. | Unsourced |
| SRS-SEC-S16-018 | The Secondary Board shall record `<TBD-S54>` (the required image integrity and authenticity mechanism: checksum, cryptographic hash, or signature) — see Open Issue **#9**. | Unsourced |

### 4.17 S17 — Local indication and digital outputs

**Coverage:** ◐ Partial. The output set is sourced from HW §3, but its counts conflict
with LSRS and with the firmware — conflict **C-11** — and ten hardware questions remain
open on this board's I/O — Open Issue **#21**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S17-001 | ⚠ The Secondary Board shall drive the charge contactor relay output only as required by §4.5 and §4.7. | HW §3 DO2 |
| SRS-SEC-S17-002 | ⚠ The Secondary Board shall drive the discharge contactor relay output only as required by §4.6 and §4.7. | HW §3 DO3 |
| SRS-SEC-S17-003 | The Secondary Board shall activate its error relay output whenever a fault is active. | HW §3 DO1; BM §12.4.8.5 |
| SRS-SEC-S17-004 | The Secondary Board shall deactivate its error relay output when the last active fault is cleared. | derived |
| SRS-SEC-S17-005 | The Secondary Board shall drive an error indicator visible at the cabinet in step with the error relay output. | BM §12.4.8.5 ("sets the Error LED and the error relay output") |
| SRS-SEC-S17-006 | The Secondary Board shall indicate charge activity on a local indicator while charging. | CODE-S `USER_LED_EN`; BM §12.4.8.5 |
| SRS-SEC-S17-007 | The Secondary Board shall indicate a recharge condition distinguishably from a normal charge condition on that indicator. | BM §12.4.8.5 (`RCH` blinks the charge LED) |
| SRS-SEC-S17-008 | The Secondary Board shall set each spare digital output to a commanded state on command from the Primary Board. | CODE-S `CONTROL_CMD_Q_ID_DO_SELECTION`; HW §3 remark; `ICD` B-CTL-06 |
| SRS-SEC-S17-009 | The Secondary Board shall report the actual state of every digital output to the Primary Board. | CODE-S `BTS_MeasuredData_t.dO`; LSRS SW_REQ_14 |
| SRS-SEC-S17-010 | ⚠ The Secondary Board shall set every spare digital output to its defined default state on entering the safe state, unless a requirement resolving `<TBD-S02>` states otherwise. | derived; safety |
| SRS-SEC-S17-011 | ⚠ The Secondary Board shall not permit a commanded spare-digital-output state to affect the charge contactor, the discharge contactor or the error relay. | derived; safety |
| SRS-SEC-S17-012 | The Secondary Board shall support enabling and disabling each digital output individually by configuration. | LSRS SW_REQ_13 |
| SRS-SEC-S17-013 | The Secondary Board shall record `<TBD-S55>` (whether program-controlled digital outputs are in ME scope, and if so which operators drive them) — see Open Issue **#19**. The BM operators `OUTA` and `OUTB` are in ME scope by the resolution of Open Issue **#27**, but their semantics are documented only in `BM_PM_BTS600_New_Operators.pdf`, which is not in the reference set. | HW §3 remark; Open Issues **#19**, **#27** |
| SRS-SEC-S17-014 | The Secondary Board shall record `<TBD-S05>` (the confirmed digital output count and per-output function) — see conflict **C-11**. HW §3 gives **11** outputs; the legacy firmware's output structure exposes **8**; LSRS gives 8 outputs plus 3 relays. | HW §3; CODE-S `DO_t`; LSRS |
| SRS-SEC-S17-015 | The Secondary Board shall record `<TBD-S56>` (whether the *active* output may be reconfigured as an *error* output) — see Open Issue **#21**, HW *Queries* #6. | Unsourced |

### 4.18 S18 — Diagnostics and self-monitoring

**Coverage:** ◐ Partial. HW *Configuration* #23–42 defines a five-channel health-check
framework whose channels are never identified — Open Issue **#20**.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S18-001 | The Secondary Board shall monitor five health-check channels against configurable lower, upper and warning limits with a configurable hysteresis. | HW *Configuration* #23–42 |
| SRS-SEC-S18-002 | The Secondary Board shall distinguish a health-check warning from a health-check limit breach. | HW *Configuration* #25, #26 (Warning Limit, Hysteresis) |
| SRS-SEC-S18-003 | The Secondary Board shall report every health-check warning and every health-check limit breach to the Primary Board. | derived |
| SRS-SEC-S18-004 | The Secondary Board shall monitor its own heatsink temperature continuously, whether or not it is regulating. | HW §4.3 |
| SRS-SEC-S18-005 | The Secondary Board shall detect the loss of the temperature feedback signal and shall report it. | HW *Error code* `0x17`; CODE-S `en_ERR_TEMP_FB` |
| SRS-SEC-S18-006 | The Secondary Board shall detect that a measurement channel is returning an implausible value and shall report it. | HW *Error code* `4`, `5`; derived |
| SRS-SEC-S18-007 | The Secondary Board shall detect a regulation loop whose output has saturated without the regulated quantity approaching its setpoint, and shall report it. | HW *Error code* `0x0F`, `0x10`; §4.5.2 |
| SRS-SEC-S18-008 | The Secondary Board shall report the accumulated operating time of the board. | NEW — ME |
| SRS-SEC-S18-009 | The Secondary Board shall report the accumulated number of charge and discharge contactor operations, for maintenance planning. | NEW — ME |
| SRS-SEC-S18-010 | The Secondary Board shall report its restart count and the reason for its most recent restart. | SRS-SEC-S1-017, -018 |
| SRS-SEC-S18-011 | The Secondary Board shall report the raw converter count of every measurement channel on request. | SRS-SEC-S8-018; HW remarks #30, #31, #34 |
| SRS-SEC-S18-012 | The Secondary Board shall report the commanded analog reference output value on request, expressed in converter counts as well as in engineering units. | HW remark #34 ("Kindly show whole DAC count for reference range") |
| SRS-SEC-S18-013 | The Secondary Board shall report its peak stack usage since reset, or shall report that it does not measure it. | HW *Coding Guidelines* #20; NEW — ME |
| SRS-SEC-S18-014 | The Secondary Board shall report the worst-case observed execution interval of its regulation loop since reset. | NEW — ME; §5.1 |
| SRS-SEC-S18-015 | The Secondary Board shall record `<TBD-S57>` (the physical quantity measured by each of the five health-check channels, and the action required on a warning as distinct from a limit breach) — see Open Issue **#20**. All twenty parameters default to **4096**, which is a converter count rather than an engineering value and reveals nothing about the intended channels. | HW *Configuration* #23–42 |

### 4.19 S19 — Power-fail detection, backup and resume

**Coverage:** ● Strong in evidence, ○ Absent in agreed policy. HW lists *"POWER FAIL
ACTION"*, *"PROGRAM START AFTER PFA"* and *"DATA SAVE AFTER PFA"* as **pending**;
`CODE-S` implements a backup-and-resume mechanism. Open Issue **#23**.

> **Note on this section.** `GATE` §2's checklist for the Secondary Board has no
> power-fail area; the topic is distributed across S1, S10 and S15. It is separated here
> because the source material is substantial and specific, and because splitting it
> across three areas would have hidden its open policy question. This is a deliberate
> addition to the checklist and is flagged for the reviewer.

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-S19-001 | ⚠ The Secondary Board shall detect an impending loss of supply before the supply falls below the level required for correct operation. | HW §10; HW *Error code* `0x12`; `ICD` B-PFA-01 |
| SRS-SEC-S19-002 | ⚠ The Secondary Board shall place the analog reference output at zero on detecting an impending loss of supply. | derived; safety |
| SRS-SEC-S19-003 | ⚠ The Secondary Board shall persist its regulation context on detecting an impending loss of supply. | CODE-S `powerFailBackup()`, `progBackUp_t` |
| SRS-SEC-S19-004 | The Secondary Board shall persist, as that context, the accumulated capacity and energy quantities, the elapsed regulation time, the elapsed time of the current unit of control data, the operating state, the active fault set and the registration type in force. | CODE-S `progBackUp_t`; `ICD` B-PFA-02 |
| SRS-SEC-S19-005 | ⚠ The Secondary Board shall complete the persistence of SRS-SEC-S19-003 within the supply hold-up time available to it. | HW §10; derived |
| SRS-SEC-S19-006 | The Secondary Board shall report a power-fail detection as a fault. | HW *Error code* `0x12`; CODE-S `en_ERR_POWER_FAIL`; `ICD` B-PFA-01 |
| SRS-SEC-S19-007 | The Secondary Board shall determine, on the next initialization, that the preceding shutdown was caused by loss of supply. | SRS-SEC-S1-017 |
| SRS-SEC-S19-008 | The Secondary Board shall restore the persisted regulation context on a power-resume command from the Primary Board. | CODE-S `powerFailResume()`, `CONTROL_CMD_Q_ID_POWER_RESUME`; `ICD` B-PFA-03 |
| SRS-SEC-S19-009 | ⚠ The Secondary Board shall not resume regulation after a loss of supply without an explicit command. | derived; safety |
| SRS-SEC-S19-010 | The Secondary Board shall report the persisted context to the Primary Board so that the Primary Board can decide whether to resume. | derived; brief (the Primary owns sequencing); `ICD` B-PFA-02 |
| SRS-SEC-S19-011 | ⚠ The Secondary Board shall verify the integrity of the persisted context before restoring it, and shall report it as unusable rather than restore a corrupt context. | derived; §4.15; `ICD` B-PFA-02 |
| SRS-SEC-S19-012 | ⚠ The Secondary Board shall verify, before resuming, that the battery connection is present and correctly polarised. | SRS-SEC-S8-033, -035 |
| SRS-SEC-S19-013 | The Secondary Board shall record `<TBD-S58>` (the available supply hold-up time, and what must therefore be persisted within it) — see Open Issue **#23**. | Unsourced |
| SRS-SEC-S19-014 | The Secondary Board shall record `<TBD-S59>` (whether a test resumes automatically after supply is restored or requires operator confirmation) — see Open Issue **#23**. | Unsourced |
| SRS-SEC-S19-015 | The Secondary Board shall record `<TBD-S60>` (whether the registration data acquired but not yet transmitted at the moment of supply loss must survive it) — HW lists *"DATA SAVE AFTER PFA"* as pending. | HW #65 |

---

## 5. Non-Functional Requirements

### 5.1 Performance and timing

⚠ Every requirement in this subsection governs real-time behaviour and requires
hardware-in-the-loop verification (§5.7).

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-001 | ⚠ The Secondary Board shall execute its regulation loop at an interval of **1 ms** or shorter. | CODE-S `DAC_CONTROL_TIME 1U`, `STEP_EXECUTION_TIME 1U` |
| SRS-SEC-NF-002 | ⚠ The Secondary Board shall acquire battery voltage and battery current at an interval of **1 ms** or shorter. | CODE-S `ADC_24BIT_READ_TIME 1U` |
| SRS-SEC-NF-003 | ⚠ The Secondary Board shall evaluate the transistor-bank voltage limits and the temperature limits at an interval of **100 ms** or shorter. | CODE-S `CHECK_LnTnZntnTEMP_TIME 100U`; BM §12.4.4 |
| SRS-SEC-NF-004 | ⚠ The Secondary Board shall evaluate the current, voltage and power protective limits at every measurement interval. | SRS-SEC-S9-014 |
| SRS-SEC-NF-005 | ⚠ The Secondary Board shall meet SRS-SEC-NF-001 to SRS-SEC-NF-004 while simultaneously servicing CAN traffic, reporting telemetry and reporting registration data. | NEW — ME |
| SRS-SEC-NF-006 | ⚠ The Secondary Board shall meet SRS-SEC-NF-001 to SRS-SEC-NF-004 with no dependence on the Primary Board's timing. | derived; safety |
| SRS-SEC-NF-007 | ⚠ The Secondary Board shall reach a commanded setpoint from zero within **10 ms**. | HW §6; SRS-SEC-S7-003 |
| SRS-SEC-NF-008 | ⚠ The Secondary Board shall return the analog reference output to zero within **10 ms** of a stop command or a fault detection. | HW §6; SRS-SEC-S7-004 |
| SRS-SEC-NF-009 | ⚠ The Secondary Board shall complete a range transition within **50 ms**. | BM §12.4.8.13 |
| SRS-SEC-NF-010 | ⚠ The Secondary Board shall detect loss of Primary communication within the configured timeout, default **100 ms**. | HW *Configuration* #9 |
| SRS-SEC-NF-011 | ⚠ The Secondary Board shall enter the safe state within `<TBD-S34>` of detecting a condition requiring it. | Open Issue **#8** |
| SRS-SEC-NF-012 | ⚠ The Secondary Board shall produce a registration record at the interval resolved by `<TBD-S41>`. | HW §6; conflict **C-05** |
| SRS-SEC-NF-013 | ⚠ The Secondary Board shall bound the jitter of its regulation loop interval to `<TBD-S61>`. | Unsourced; NEW — ME |
| SRS-SEC-NF-014 | ⚠ The Secondary Board shall bound the latency from a measurement being acquired to the regulation loop acting on it to `<TBD-S71>`. | Unsourced; NEW — ME |
| SRS-SEC-NF-015 | The Secondary Board shall respond to a command from the Primary Board within `<TBD-S62>`. | Unsourced |
| SRS-SEC-NF-016 | ⚠ The Secondary Board shall not allow any interrupt service routine to execute for longer than `<TBD-S72>`, and every interrupt service routine shall carry its timing constraint and expected execution time in its source documentation. | Team engineering policy; HW *Coding Guidelines* #33 |

### 5.2 Safety

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-017 | ⚠ The Secondary Board shall default to the safe state in every condition in which its correct operation cannot be established. | derived; BM §12.4 safety note |
| SRS-SEC-NF-018 | ⚠ The Secondary Board shall not depend on the Primary Board, the CAN link or the Web Application to reach the safe state. | derived; safety |
| SRS-SEC-NF-019 | ⚠ The Secondary Board shall enforce its configured absolute ratings under every commanded condition. | SRS-SEC-S9-016 |
| SRS-SEC-NF-020 | ⚠ The Secondary Board shall not break load current with a contactor. | SRS-SEC-S9-027 |
| SRS-SEC-NF-021 | ⚠ The Secondary Board shall not energise the transistor bank while the battery connection is absent, reversed or unverified. | SRS-SEC-S8-033, -035 |
| SRS-SEC-NF-022 | ⚠ The Secondary Board shall treat every fault whose safe handling is undefined as requiring the safe state. | derived; A-07 |
| SRS-SEC-NF-023 | The Secondary Board shall record `<TBD-S63>` (any applicable functional-safety integrity requirement for this board) — no source states one. | Unsourced |

### 5.3 Reliability and availability

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-024 | The Secondary Board shall operate unattended for the duration of a test that may run continuously for `<TBD-S64>`. | Unsourced; §2.5 |
| SRS-SEC-NF-025 | The Secondary Board shall recover from a transient fault without requiring a power cycle, where the fault is not one that latches. | derived |
| SRS-SEC-NF-026 | The Secondary Board shall not lose calibration data or factory configuration as a consequence of any number of resets or supply interruptions. | §4.15 |
| SRS-SEC-NF-027 | The Secondary Board shall not require its peers on the CAN bus to be present or functional in order to operate. | NEW — ME; A-08 |
| SRS-SEC-NF-028 | The Secondary Board shall initialize every variable and pointer before use, and shall check every array index against its bounds. | HW *Coding Guidelines* #26, #29 |
| SRS-SEC-NF-029 | The Secondary Board shall validate every function parameter whose range it does not control. | HW *Coding Guidelines* #25 |
| SRS-SEC-NF-030 | The Secondary Board shall handle the error return of every function whose failure affects subsequent execution. | HW *Coding Guidelines* #15 |

### 5.4 Security

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-031 | The Secondary Board shall accept configuration, calibration and firmware only from the Primary Board over IF-B. | §3.2 |
| SRS-SEC-NF-032 | The Secondary Board shall require an explicit unlocking action before accepting a write to calibration data or factory configuration. | derived; HW §12 (factory-only operations); `ICD` A-CFG-04, B-CFG-04 |
| SRS-SEC-NF-033 | The Secondary Board shall record `<TBD-S65>` (whether the CAN link must be authenticated, and whether firmware images must be cryptographically verified) — see Open Issues **#9** and **#10**. The CAN bus is not a trust boundary in any source. | Unsourced |
| SRS-SEC-NF-034 | The Secondary Board shall record `<TBD-S66>` (whether read-out protection of the microcontroller's program memory is required in production). | Unsourced |

### 5.5 Maintainability and implementation constraints

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-035 | The Secondary Board software shall be structured in the layers Application, Interface, Basic Software and Communication. | HW *Digital Controller* #9; CN-05 |
| SRS-SEC-NF-036 | No source file of the Secondary Board software shall exceed **2000 lines**. | CN-07 |
| SRS-SEC-NF-037 | The Secondary Board software shall not allocate memory dynamically after initialization. | CN-06 |
| SRS-SEC-NF-038 | The Secondary Board software shall comply with the 49 items of HW *Coding Guidelines*. | HW *Coding Guidelines*; CN-08 |
| SRS-SEC-NF-039 | The Secondary Board software shall protect every variable and every hardware object accessed from more than one concurrent context with a synchronisation mechanism. | HW *Coding Guidelines* #16, #17 |
| SRS-SEC-NF-040 | The Secondary Board software shall state the unit of every numeric quantity at its declaration. | HW *Coding Guidelines* #7 |
| SRS-SEC-NF-041 | The Secondary Board software shall bound the stack depth of every execution path. | HW *Coding Guidelines* #20; CN-13 |
| SRS-SEC-NF-042 | The Secondary Board software shall provide a default case for every state machine and shall initialize every interrupt vector to a handler. | HW *Coding Guidelines* #32, #33; CN-14 |
| SRS-SEC-NF-043 | The Secondary Board software shall reference the device datasheet or hardware abstraction layer documentation for every hardware register access, in the source. | Team engineering policy |
| SRS-SEC-NF-044 | The Secondary Board shall record `<TBD-S67>` (whether MISRA-C compliance is required, for which modules, and against which edition) — see Open Issue **#26**. | Unsourced |

### 5.6 Resource and environmental constraints

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-045 | The Secondary Board software shall fit within the program memory and data memory of the selected microcontroller with margin of `<TBD-S68>`. | Unsourced |
| SRS-SEC-NF-046 | The Secondary Board shall operate over the ambient temperature range `<TBD-S69>`. | Unsourced |
| SRS-SEC-NF-047 | The Secondary Board shall record `<TBD-S01>` (the microcontroller part number). `CODE-S` evidences an STM32H723; no source specifies the ME part. | Unsourced |
| SRS-SEC-NF-048 | The Secondary Board shall record `<TBD-S70>` (whether a real-time operating system is used and which one, or whether the software is bare-metal; no source states either). | Unsourced |

### 5.7 Verification constraints

| ID | Requirement | Source |
|---|---|---|
| SRS-SEC-NF-049 | Every requirement marked **⚠** in this document shall be verified by hardware-in-the-loop test on the target board with a real transistor bank and a real battery or equivalent load. | Team engineering policy |
| SRS-SEC-NF-050 | No requirement governing regulation timing, protective response time, contactor sequencing or interrupt latency shall be closed on the basis of simulation or code review alone. | Team engineering policy |
| SRS-SEC-NF-051 | Every fault condition of §4.9 shall be verified by injecting the condition, not by inspecting the code path that detects it. | Team engineering policy |
| SRS-SEC-NF-052 | Every requirement carrying a `<TBD-Snn>` marker shall be treated as unverifiable until the marker is resolved. | derived |
| SRS-SEC-NF-053 | The calibration procedure of §4.14 shall be verified against reference equipment whose accuracy is stated and traceable. | derived; SRS-SEC-S14-030 |

---
## 6. Traceability Matrix

See the companion annex:
`02A_SRS_ME_Secondary_Annex_Traceability_v0.1.md` §A1.

Every requirement in this document additionally carries its source inline in the `Source`
column of its table, so the annex provides the reverse mapping — from source document and
section to requirement — together with the coverage roll-up per functional area.

---

## 7. Core Allocation — not applicable

This section is retained, empty, so that section numbering aligns with
`01_SRS_ME_Primary_Board_v0.1.md`.

The Primary SRS carries a core-allocation section because the i.MX 8M Plus is a
heterogeneous multicore device and the allocation of function between the Cortex-A53
cluster and the Cortex-M7 is reserved decision **D-01**. The Secondary Board is a
single-core microcontroller. No allocation question exists, no requirement in this
document carries an `Alloc` attribute, and **D-01** does not bear on this board.

Two reserved decisions *do* bear on this board and are recorded in §8.4: **D-03**, the
content of the minimum necessary control data, and **D-04**, the CAN layer details.
**D-06**, the inter-core communication mechanism on the Primary, does not bear on this
board except insofar as it may constrain the timing with which control data arrives.

---

## 8. Open Issues / TBD Register

### 8.1 Open issues

Issues **#1**, **#4**, **#5**, **#27** and **#29** were resolved by the architect on
2026-07-27; their resolutions are incorporated above. The remainder stand. The register is
shared with `GATE` §4 and with `PRI` §8.1; the third column below states the bearing on
**this** document only.

| # | Issue | Bearing on this document |
|---|---|---|
| ~~1~~ | ~~Is ME the Digatron ME circuit type?~~ **CLOSED** — ME is this project's name; BM is a functional reference, not a compatibility contract. | §1.3. Removes any requirement to reproduce BM byte layouts or named formats on this board. |
| **2** | Which Web Application is the peer? | **No direct bearing.** This board has no Web Application interface (SRS-SEC-SW-003). |
| **3** | No ME Primary Board hardware specification exists. | **No direct bearing.** This board has its own hardware specification (**HW**). |
| ~~4~~ | ~~Is the COM Controller retained?~~ **CLOSED** — no COM Controller in ME. | §2.1. The Secondary's peer is the Primary Board directly. |
| ~~5~~ | ~~Which operators are in Phase 1?~~ **CLOSED** — all BTS-600 operators are in scope, no phasing. | Indirect: the Secondary must support every regulation mode any operator can demand — §4.5, §4.6 — and the range operators of §4.7.3. |
| **6** | Are battery-parameter-relative values resolved on the Primary or pre-computed? | Indirect. If the Primary resolves them, the Secondary receives literal setpoints and §4.4 is unaffected. If not, `<TBD-S18>` widens. SRS-SEC-S4-019. |
| **7** | Procedures or PRODUCER as the program-reuse model? | **No bearing.** Program structure is entirely Primary-side. |
| **8** | Emergency-stop input source and required safe-state output configuration. | **High.** `<TBD-S02>`, `<TBD-S34>`, `<TBD-S35>`, `<TBD-S37>`; §4.9.3, §4.9.4, SRS-SEC-NF-011. The single most load-bearing open issue for this board. |
| **9** | Confirm Secondary firmware update over CAN is required; bootloader field-updatability; rollback; integrity mechanism. | **High.** All of §4.16; `<TBD-S51>`…`<TBD-S54>`, `<TBD-S65>`. |
| **10** | Primary-side security posture. | Indirect. `<TBD-S65>` — whether the CAN link is a trust boundary follows from the system security posture. |
| **11** | Data retention and storage-exhaustion policy. | Indirect. Bears on `<TBD-S43>` (registration buffer depth) but the retention itself is Primary-side. |
| **12** | Buffering during link loss; orderly shutdown; startup self-test content. | **High.** `<TBD-S10>`, `<TBD-S11>`, `<TBD-S39>`; SRS-SEC-S1-015, -024, -025. |
| **13** | Maintenance mode. | `<TBD-S13>`; SRS-SEC-S2-002, -019. Note a Calibration mode *is* sourced and is specified (§4.14). |
| **14** | Secondary presence policy: identity acquisition, hot-plug, behaviour on loss. | **High.** `<TBD-S15>`, `<TBD-S17>`, `<TBD-S37>`; §4.3, §4.10. |
| **15** | Program assignment semantics; retention of per-range calibration. | The calibration half bears directly: `<TBD-S47>`, SRS-SEC-S14-029. The assignment half is Primary-side. |
| **16** | Synchronised versus independent multi-channel execution; time source. | Indirect. If synchronised execution is required, the Secondary's start latency and its bound become load-bearing — SRS-SEC-NF-015, `<TBD-S62>`. |
| **17** | Fault latch, acknowledge and clear semantics; local event log. | **High.** `<TBD-S44>`, `<TBD-S46>`; §4.13. |
| **18** | Telemetry rate budget across 8 nodes and acceptable latency. | **High.** `<TBD-S42>`, `<TBD-S43>`; §4.12, SRS-SEC-CM-009. Newly acute because the legacy dedicated registration channel is gone (§2.1). |
| **19** | Program-controlled digital output. | `<TBD-S55>`; SRS-SEC-S17-008, -013. |
| **20** | Health-check channels 1 to 5. | **Direct.** `<TBD-S57>`; all of §4.18. Twenty configuration parameters exist whose meaning is unknown. |
| **21** | The hardware specification's ten unresolved hardware queries. | **Direct.** `<TBD-S09>`, `<TBD-S16>`, `<TBD-S24>`, `<TBD-S56>`; §3.4, §4.3, §4.7.2, §4.17. Every one of the ten queries is about *this* board. |
| **22** | Secondary node identity source; digital I/O counts; NVM policy; restore to defaults. | **Direct.** `<TBD-S05>`, `<TBD-S15>`, `<TBD-S49>`, `<TBD-S50>`; §3.1, §4.3, §4.15. |
| **23** | Power-fail: hold-up time, what is persisted, automatic versus confirmed resume. | **Direct.** `<TBD-S58>`, `<TBD-S59>`, `<TBD-S60>`; all of §4.19. |
| **24** | Cross-channel system-level supervision. | Indirect; A-08. If shared-rectifier coupling exists, `<TBD-S30>` and SRS-SEC-S9-011 widen. |
| **25** | Program storage on the Primary. | **No bearing.** No program is stored on this board. |
| **26** | MISRA-C applicability. | `<TBD-S67>`; CN-09, SRS-SEC-NF-044. |
| ~~27~~ | ~~Missing operator PDF.~~ **PARTIALLY CLOSED** — all eight operators are in ME scope. Their semantics remain unavailable and the document is still requested. | `IRANGE` and `URANGE` bear on §4.7.3; `OUTA` and `OUTB` bear on §4.17 — `<TBD-S55>`. The remaining four (`PAUA`, `PAUO`, `ISOEXT`, `ISOINT`) may impose Secondary behaviour that cannot yet be specified. |
| **28** | Does the Primary need analog input of its own? | **No bearing.** This board has its own analog acquisition. |
| ~~29~~ | ~~External CAN / Modbus scope.~~ **CLOSED for Modbus** — hosted by the Primary Board. External CAN and DBC are excluded from ME scope. | **No bearing.** Neither reaches this board. |
| **30** | Deterministic Ethernet or IEEE 1588 on the Primary. | **No bearing** directly; if precise cross-channel time is required it reaches this board only as SRS-SEC-S4-018. |
| **31** | Must ME reproduce the BM registration formats? | Indirect. If it must, the registration quantity set of SRS-SEC-S12-013 may need to extend. Weakened by the closure of #1. |

**New issue raised by this document.**

| # | Issue | Bearing |
|---|---|---|
| **S-32** | **The legacy Secondary's step-execution role is removed, but no source states what replaces the behaviour that role provided locally.** Three concrete instances: (a) the legacy Secondary detected its own cut-off conditions within 5 consecutive evaluations at 1 ms, so a cut-off acted within ~5 ms; if the Primary now decides, the decision must cross the CAN bus twice and `<TBD-S23>`/`<TBD-S34>` must absorb that. (b) The legacy Secondary held the cycle table and restored counters after a power fail; the ME Secondary persists only its own context (§4.19) and the Primary must hold the rest. (c) The legacy Secondary requested its next step and timed out after 5 attempts at 100 ms; SRS-SEC-S4-024 keeps that behaviour, but whether it is still the right mechanism when the Primary is sequencing is part of **D-03**. | §4.4, §4.5.4, §4.10, §4.19; **D-03** |

### 8.2 Conflict register

Conflicts are carried, not resolved. Full analysis is in `GATE` §5. The table below lists
only those conflicts that bear on this document — which is most of them, because the
hardware specification is a specification of *this* board.

| ID | Conflict | Bearing on this document |
|---|---|---|
| **C-01** | Analog-output resolution: 24-bit (HW column) vs 16-bit (HW remark) vs 20-bit (client, 10-Feb-2026) vs ≥18-bit (LSRS) | `<TBD-S04>`; SRS-SEC-HW-005. HW is internally inconsistent, so precedence cannot resolve it. |
| **C-02** | Reference-signal range: −10…+10 V (HW) vs −2…+2 V (client remark) vs 0…10 V / −10…0 V (LSRS) | `<TBD-S03>`; SRS-SEC-HW-004. Also internally inconsistent. |
| **C-03** | LNT/ZNT ADC width: 12-bit (HW column) vs 16-bit (HW remark) | `<TBD-S07>`; SRS-SEC-HW-021. The remark is later-dated; prefer 16-bit, confirm. |
| **C-04** | Charge↔discharge transition: 10 ms (HW §6) vs 23 ms measured vs 500 ms switch-over delay (LSRS) vs 50 ms contactor wait (HW Config) | `<TBD-S23>`; §4.7.2. HW wins on precedence, but the 500 ms figure is a *safety* delay for bank switching and is not the same quantity — the two must be reconciled, not ranked. |
| **C-05** | Registration interval: ≤500 µs vs 10 ms vs 1 ms vs 100 ms implemented vs BM's 0.1 s floor | `<TBD-S41>`; SRS-SEC-S12-023, SRS-SEC-NF-012. Five values. |
| **C-06** | Secondary bootloader: required (HW §8) vs "not required as discussed earlier" (client) vs required over CAN (brief) | `<TBD-S51>`; all of §4.16. |
| **C-07** | Battery voltage: ≤100 V capability vs 80 V present rig vs 18 V configured default | `<TBD-S30>`; SRS-SEC-S8-004, -038. Capability, rig and configured value are three different things and are distinguished in §4.8. |
| **C-08** | Maximum current: ≤200 A capability vs 100 A configured default | `<TBD-S31>`; SRS-SEC-S8-039. |
| **C-09** | Document revision: filename says V1.04; the Version sheet's last entry is 1.03 dated 9-Feb-2024, while remark columns are dated 10-Feb-2026 | Note only. The brief states this file is current, so the 2026 remarks are treated as the latest client position wherever they contradict the 2024 data columns. This is the mechanism by which several conflicts above arise. |
| **C-10** | CAN bit rate: 250 kbps (HW Config #14, Primary↔Secondary) vs 125 k–1 M (LSRS, external CAN) | **Different buses.** Only the 250 kbps figure bears on this board — SRS-SEC-CM-008. The external-CAN figures are out of ME scope by the closure of #29. |
| **C-11** | Digital I/O counts: 4 DI + 8 DO + 3 relays (LSRS) vs 4 DI + 11 DO (HW) vs 8 output bits (legacy firmware). Client further notes the discharge relay is unused on the dual bank and there are no motherboard DI connections | `<TBD-S05>`, `<TBD-S21>`; SRS-SEC-HW-012, SRS-SEC-S6-013, SRS-SEC-S17-014. |
| **C-12** | Auto-ranging: 50/10/1/0.1 % of Imax (HW) vs gains 1/2/4/8 (LSRS, legacy firmware) vs BM ranges 1–4 with no in-step change and 50 ms transit | `<TBD-S25>`, `<TBD-S26>`, `<TBD-S27>`; §4.7.3. Three incompatible schemes, and the feature is currently **disabled** in the legacy firmware for destabilising the regulation loops. |
| **C-13** | Cycle nesting depth | **No bearing.** Cycles are Primary-side in ME. |
| **C-14** | Four incompatible code spaces: HW's `1`…`0x17`; the legacy bitmask in a different order; BM `ER1`–`ER10`; BM user-editable message numbers 1–6 | `<TBD-S45>`; §4.13. The user-editable message space is a different kind of identifier from a fixed fault code and must not be merged with it. |
| **C-15** | Temperature sensor: PT1000 / AD590 (HW §4.6) vs "AD590/PT100" (HW *Calibration* #17) vs PT100 in the legacy firmware vs "need to test with actual PT100" (client) | `<TBD-S06>`; SRS-SEC-HW-020. HW contradicts itself within one workbook. The distinction matters: PT100 and PT1000 differ by a decade in resistance and require different front-end scaling. |
| **C-16** | External CAN port count on the Primary | **No bearing.** |
| **C-17** | RS485/Modbus hosting | **No bearing.** |
| **C-18** | Primary analog input | **No bearing.** |

**Additional discrepancy found while writing this document, not previously registered.**

| ID | Discrepancy | Disposition |
|---|---|---|
| **C-19** | **Open-battery detection has no error code.** LSRS SW_REQ_133–135 requires detection of an open or absent battery before a test starts, and the legacy firmware implements a battery-voltage check. The HW *Error code* sheet's 23 defined codes include reverse polarity (`0x0A`, `0x0B`) and power-versus-sense difference (`0x0C`) but **no open-battery code**. | Carried into SRS-SEC-S8-035 and SRS-SEC-S8-036 with `<TBD-S29>`. A code must be allocated from the 77 unused positions in the HW sheet. Raised for the architect. |
| **C-20** | **The `en_ERR_*` set in the legacy firmware contains conditions absent from the HW error sheet** — `en_ERR_POWER_SETPOINT_UNREACHABLE`, `en_ERR_OVER_POWER`, `en_ERR_INVALID_PROG_STEP`, `en_ERR_NETWORK_CONN_FAIL` — while the HW sheet contains conditions absent from the firmware set. The two are not a subset relation in either direction. | Deepens **C-14**. Both sets are carried: §4.9 specifies power-related protection from the firmware evidence, §4.13 requires the HW set as a floor. `<TBD-S45>` must reconcile them. |

### 8.3 TBD register

Seventy-two values or policies could not be sourced. Each is a question for the architect
or the client, and every requirement carrying one is unverifiable until it is answered
(SRS-SEC-NF-052).

| Tag | Value required | Raised by | Descends from |
|---|---|---|---|
| `<TBD-S01>` | Secondary Board microcontroller part number | Header · SRS-SEC-NF-047 | Unsourced |
| `<TBD-S02>` | Required safe-state output configuration, per transistor-bank construction | §1.4 · SRS-SEC-S9-033 · SRS-SEC-S17-010 | **#8** |
| `<TBD-S03>` | Analog reference output range | SRS-SEC-HW-004 | **C-02** |
| `<TBD-S04>` | Analog reference output resolution | SRS-SEC-HW-005 | **C-01** |
| `<TBD-S05>` | Confirmed digital input, digital output and relay counts and functions | SRS-SEC-HW-012 · SRS-SEC-S17-014 | **C-11** · **#22** |
| `<TBD-S06>` | External temperature sensor type, and whether more than one must be supported | SRS-SEC-HW-020 | **C-15** |
| `<TBD-S07>` | ADC resolution of the LNT and ZNT channels | SRS-SEC-HW-021 | **C-03** |
| `<TBD-S08>` | CAN addressing scheme, identifier allocation, framing, and CAN 2.0B versus CAN-FD | SRS-SEC-CM-012 | **D-04** |
| `<TBD-S09>` | Required local indicator set, and whether a non-CAN diagnostic path is required in production | SRS-SEC-UI-005 | **#21** |
| `<TBD-S10>` | Enumerated power-on self-test content and pass criteria | SRS-SEC-S1-024 | **#12** |
| `<TBD-S11>` | Whether an orderly shutdown sequence is required, and its content | SRS-SEC-S1-025 | **#12** |
| `<TBD-S12>` | Maximum permitted initialization time | SRS-SEC-S1-026 | Unsourced |
| `<TBD-S13>` | Whether a Maintenance mode distinct from Calibration is required | SRS-SEC-S2-019 | **#13** |
| `<TBD-S14>` | Whether legacy channel-state values are retained on the wire, and the ME state mapping | SRS-SEC-S2-020 | **D-03** |
| `<TBD-S15>` | How the node identity is acquired | SRS-SEC-S3-010 | **#22** · **D-04** |
| `<TBD-S16>` | Purpose of hardware switch DIP S1 | SRS-SEC-S3-011 | **#21** |
| `<TBD-S17>` | Whether insertion into a running bus must be supported, and address-conflict handling | SRS-SEC-S3-012 | **#14** |
| `<TBD-S18>` | Exact content of the minimum necessary control data, including whether cut-off conditions are carried | SRS-SEC-S4-026 | **D-03** |
| `<TBD-S19>` | Maximum permitted disturbance of the regulated quantity during a loop transfer or a range transition | SRS-SEC-S5-006 · SRS-SEC-S6-006 · SRS-SEC-S7-031 | Unsourced |
| `<TBD-S20>` | Setpoint-attainment tolerance for current, voltage and power, and whether relative or absolute | SRS-SEC-S5-031 | HW Config #10 vs CODE-S |
| `<TBD-S21>` | Whether the discharge relay is populated and used on the dual-bank variant | SRS-SEC-S6-013 | **C-11** |
| `<TBD-S22>` | Achievable rise and fall time, against the 10 ms specification and the 4 s measurement | SRS-SEC-S7-008 | HW §6 remark |
| `<TBD-S23>` | Required charge↔discharge transition time, and its relationship to the contactor switch-over delay | SRS-SEC-S7-016 | **C-04** |
| `<TBD-S24>` | Whether a contactor acknowledgement input exists and must be confirmed | SRS-SEC-S7-020 | **#21** |
| `<TBD-S25>` | Required current-range set | SRS-SEC-S7-033 | **C-12** |
| `<TBD-S26>` | Whether range selection is by software or by shunt-selecting digital outputs | SRS-SEC-S7-034 | **C-12** |
| `<TBD-S27>` | Whether automatic range selection is required in ME at all | SRS-SEC-S7-035 | **C-12** |
| `<TBD-S28>` | Required capacity- and energy-integration accuracy, and the reference test duration over which it is measured | SRS-SEC-S8-022 | Unsourced |
| `<TBD-S29>` | Voltage threshold and dwell time defining an open battery | SRS-SEC-S8-037 | **C-19** |
| `<TBD-S30>` | Maximum measurable battery voltage required in ME | SRS-SEC-S8-038 | **C-07** |
| `<TBD-S31>` | Maximum measurable current required in ME | SRS-SEC-S8-039 | **C-08** |
| `<TBD-S32>` | Required measurement accuracy and resolution per channel | SRS-SEC-S8-040 | Unsourced |
| `<TBD-S33>` | Which protective conditions must act without a consecutive-detection count | SRS-SEC-S9-022 | **#8** |
| `<TBD-S34>` | Maximum time from detection to safe state | SRS-SEC-S9-031 · SRS-SEC-NF-011 | **#8** |
| `<TBD-S35>` | Whether an emergency-stop or external-interlock input exists, and which input carries it | SRS-SEC-S9-040 | **#8** |
| `<TBD-S36>` | Whether the "Interrupt" and "Continue" annotations against digital inputs 1 and 4 are requirements | SRS-SEC-S9-041 | HW §2 |
| `<TBD-S37>` | Whether loss of Primary communication trips immediately or permits the regulation in progress to complete | SRS-SEC-S10-010 | **#8** · **#14** |
| `<TBD-S38>` | Whether a sub-timeout communication outage must be reported as a quality event | SRS-SEC-S10-011 | Unsourced |
| `<TBD-S39>` | Required watchdog period, and whether a windowed watchdog is required (*a plain watchdog only checks the software has not stopped; a windowed one also rejects being fed too early, catching software that is running too fast or in the wrong order*) | SRS-SEC-S11-010 | **#12** |
| `<TBD-S40>` | Whether HW §10's watchdog and auto-reset function is required in ME hardware, and its software/hardware split | SRS-SEC-S11-011 | HW §10 remark |
| `<TBD-S41>` | Required registration interval | SRS-SEC-S12-023 · SRS-SEC-NF-012 | **C-05** |
| `<TBD-S42>` | Per-channel rate this board must sustain with all eight nodes on one bus, and acceptable latency | SRS-SEC-S12-024 | **#18** |
| `<TBD-S43>` | Required registration buffer depth | SRS-SEC-S12-025 | **#18** · **#11** |
| `<TBD-S44>` | Which faults latch, which self-clear, who may clear, and whether an operator action is required | SRS-SEC-S13-015 | **#17** |
| `<TBD-S45>` | The unified fault-code space | SRS-SEC-S13-016 | **C-14** · **C-19** · **C-20** |
| `<TBD-S46>` | Whether a local event log is required, and its depth and retention | SRS-SEC-S13-017 | **#17** |
| `<TBD-S47>` | Whether the per-range current calibration scheme is retained in ME | SRS-SEC-S14-029 | **#15** |
| `<TBD-S48>` | Required calibration accuracy, and the accuracy of the reference equipment assumed | SRS-SEC-S14-030 · SRS-SEC-NF-053 | Unsourced |
| `<TBD-S49>` | Persistent-store endurance and wear-levelling policy, and the write-cycle count to sustain | SRS-SEC-S15-019 | **#22** |
| `<TBD-S50>` | Required default, minimum and maximum for each configuration parameter | SRS-SEC-S15-020 | HW *Configuration* (Min/Max columns empty) |
| `<TBD-S51>` | Whether firmware update over CAN is required at all | SRS-SEC-S16-015 | **C-06** · **#9** |
| `<TBD-S52>` | Whether the bootloader is field-updatable or factory-only | SRS-SEC-S16-016 | **#9** |
| `<TBD-S53>` | Whether rollback to the previous image is required, and whether two image slots must exist | SRS-SEC-S16-017 | **#9** |
| `<TBD-S54>` | Required firmware image integrity and authenticity mechanism | SRS-SEC-S16-018 | **#9** |
| `<TBD-S55>` | Whether program-controlled digital outputs are in scope, and via which operators | SRS-SEC-S17-013 | **#19** · **#27** |
| `<TBD-S56>` | Whether the *active* output may be reconfigured as an *error* output | SRS-SEC-S17-015 | **#21** |
| `<TBD-S57>` | Physical quantity of each of the five health-check channels, and the action on warning versus breach | SRS-SEC-S18-015 | **#20** |
| `<TBD-S58>` | Available supply hold-up time, and what must be persisted within it | SRS-SEC-S19-013 | **#23** |
| `<TBD-S59>` | Whether a test resumes automatically after supply restoration or requires confirmation | SRS-SEC-S19-014 | **#23** |
| `<TBD-S60>` | Whether registration data acquired but not transmitted must survive a supply loss | SRS-SEC-S19-015 | **#23** |
| `<TBD-S61>` | Maximum permitted jitter of the regulation loop interval | SRS-SEC-NF-013 | Unsourced |
| `<TBD-S62>` | Maximum permitted time from a command being received to a response being sent | SRS-SEC-NF-015 | **#16** |
| `<TBD-S63>` | Any applicable functional-safety integrity requirement for this board | SRS-SEC-NF-023 | Unsourced |
| `<TBD-S64>` | Maximum continuous unattended test duration the board must sustain | SRS-SEC-NF-024 | Unsourced |
| `<TBD-S65>` | Whether the CAN link must be authenticated and firmware cryptographically verified | SRS-SEC-NF-033 | **#9** · **#10** |
| `<TBD-S66>` | Whether program-memory read-out protection is required in production | SRS-SEC-NF-034 | Unsourced |
| `<TBD-S67>` | MISRA-C applicability, scope and edition | SRS-SEC-NF-044 | **#26** |
| `<TBD-S68>` | Required program- and data-memory margin | SRS-SEC-NF-045 | Unsourced |
| `<TBD-S69>` | Ambient operating temperature range | SRS-SEC-NF-046 | Unsourced |
| `<TBD-S70>` | Whether a real-time operating system is used, and which | SRS-SEC-NF-048 | Unsourced |
| `<TBD-S71>` | Maximum permitted latency from a measurement being acquired to the regulation loop acting on it | SRS-SEC-NF-014 | Unsourced |
| `<TBD-S72>` | Maximum permitted execution time of any interrupt service routine | SRS-SEC-NF-016 | Team engineering policy |

### 8.4 Reserved architect decisions

| ID | Decision | Status in this document |
|---|---|---|
| **D-01** | Allocation of function between the Cortex-A53 cluster and the Cortex-M7 | **Does not bear on this board.** Single-core target; §7. |
| **D-02** | Which Primary core owns the CAN controller and driver stack | **Does not bear on this board.** The Secondary sees one peer regardless. |
| **D-03** | Exact content of the minimum necessary control data | `<TBD-S18>`. §4.4 specifies *what the Secondary must do with* control data and *what it must report*, never the content layout. §4.5.4 is written so that it holds whichever party evaluates step-ending conditions. `PSPE` §4.4 is recorded as prior art and is not adopted. |
| **D-04** | CAN layer details: 2.0B versus FD, bit rate, node addressing, higher-layer protocol, frame formats | `<TBD-S08>`, `<TBD-S15>`. The only sourced datum is the default bit rate of 250 kbps (SRS-SEC-CM-008). Node addressing appears only as a functional need (§4.3). |
| **D-05** | Web App interface details | **Does not bear on this board.** |
| **D-06** | Inter-core communication mechanism on the Primary | Bears on this board only through the timing with which control data arrives — see `<TBD-S42>`, `<TBD-S62>`. |

---

## 9. Deviations from the gate document

Recorded so that the reviewer can see where this document departs from the outline agreed
in `GATE` §2.

| # | Deviation | Reason |
|---|---|---|
| 1 | **§4.19 (S19) Power-fail detection, backup and resume is a new functional area.** `GATE` §2 distributes power-fail across S1, S10 and S15. | The source material is substantial and specific — HW lists three pending power-fail items, and `CODE-S` implements a backup/resume mechanism with a defined persisted structure. Splitting it across three areas would have hidden its single open policy question (**#23**). |
| 2 | **Constraints use the `CN-nn` prefix, not `C-nn`.** | `C-nn` is the conflict namespace. `PRI` uses `C-nn` for both; that collision is flagged in §1.7 for correction in the Primary's v0.2. |
| 3 | **`<TBD-Snn>` is a separate namespace from the Primary's `<TBD-nn>`.** | The two registers ask different questions; a shared numbering would have implied a correspondence that does not exist. |
| 4 | **Requirement count exceeds the `GATE` §7 envelope of 180–220.** | The envelope was indicative. Completing every sourced area — in particular the 23 HW error conditions, the 23-command calibration procedure, the 47 configuration parameters and the switch-over sequencing — produced more. No requirement was added without a source or an explicit `NEW — ME` justification. |
| 5 | **Two new conflicts (C-19, C-20) and one new open issue (S-32) are raised.** | Found while reading `CODE-S` and the HW error sheet against each other. Recorded rather than silently reconciled, per the brief. |

---

*End of ME-SRS-SEC-001 v0.1. Requirement count: 530. §7 is intentionally empty;
§6 is held in the companion annex.*
