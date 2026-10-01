# Software Requirements Specification — ME Primary Board

**Document ID:** ME-SRS-PRI-001 · **Version:** 0.1 (draft) · **Date:** 2026-07-27
**Standard:** ISO/IEC/IEEE 29148:2018
**Target:** ME Primary Board — NXP `MIMX8ML8CVNKZAB` (i.MX 8M Plus), Linux on the
Cortex-A53 cluster, RTOS on the Cortex-M7
**Companion documents:** `01A_SRS_ME_Primary_Annex_Traceability_and_Core_Allocation_v0.1.md`
(§6 Traceability, §7 Core Allocation) · `02_SRS_ME_Secondary_Board_v0.1.md` ·
`03_ICD_ME_Interfaces_v0.1.md` · `00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` (gate document)

> **Reading note — reserved decisions.** Every requirement in this document is written
> protocol-agnostically (*it says what information must move and what must happen, but
> never how the bytes are arranged or which wire carries them*) and at **board** level. No requirement states or implies which
> core executes it, which core owns the CAN controller, how the cores communicate, how
> information is encoded or framed, or which transport carries it. Those are decisions
> **D-01 … D-06**, reserved to the software architect. Each requirement carries an
> `Alloc` attribute whose value is `TBD` unless a source forces the allocation.

---

## 1. Introduction

### 1.1 Purpose

This document specifies the software requirements for the **ME Primary Board**. The
Primary Board is the system controller of the ME battery test system: it receives test
programs from the Web Application, decodes and executes the step sequencing itself, and
drives up to **eight** Secondary Boards over CAN, sending each only the control data
needed to charge or discharge its battery.

The document is written for the software architect, the implementation team, the test
team and the client's review authority. It is a **complete functional specification of
intent**; it deliberately stops short of design.

### 1.2 Scope

**In scope.** All Primary-Board-resident software behaviour: startup and self-test, the
operating state machine, Secondary Board discovery and supervision, program reception,
program decoding, the step execution engine implementing the complete BTS-600 program
language, concurrent execution across up to 8 Secondaries, command dispatch, telemetry
aggregation, system-level safety supervision, fault management, data logging and
retrieval, time synchronisation, configuration and calibration management, firmware
update, diagnostics, power-fail recovery, security, and the **Modbus module**.

**Out of scope.** Secondary Board internal behaviour (see `ME-SRS-SEC-001`); Web
Application internal behaviour; wire formats and encodings (see `ME-ICD-001` and
D-04/D-05); hardware design; the legacy external CAN + DBC port set — see Open Issue
**#29** and §1.6.

### 1.3 Product identity — "ME"

**"ME" is the name of this new Ador project.** It is *not* the Digatron "ME" circuit
type that appears in the Battery Manager manual. Consequently:

- The BTS-600 program language in `BM_Manual_eng 2.pdf` Ch.12 is used as the
  **authoritative functional reference** for what the ME system must do. It is **not** a
  binary or wire compatibility contract.
- The manual's statement that *"For ME circuits… a maximum number of 15 (slave) circuits
  can be arranged in parallel"* (BM §12.4.8.14) refers to Digatron's ME and is **not
  binding**. The ME limit is **8 Secondary Boards** (§2.2).
- Named entities from the manual (registration formats, message numbers, operator
  mnemonics) are adopted for functional equivalence and operator familiarity, not for
  interoperability with Battery Manager.

### 1.4 Definitions

| Term | Definition |
|---|---|
| **Primary Board** | The ME system controller specified by this document. Referred to as "Main Controller" in the hardware specification. |
| **Secondary Board** | An STM32-based digital controller driving one test circuit's transistor bank. Referred to as the "Digital Controller Transcard" / "DSP controller" in the hardware specification. |
| **Channel / Circuit** | One test circuit. One Secondary Board = one channel (assumption A-05). |
| **Web Application** | The host application from which programs, configuration and commands originate and to which telemetry and logged data are delivered. |
| **Program** | An ordered sequence of steps in the BTS-600 program language. |
| **Step** | One program line: Label, Operator, Nominal Value, Limit, Action, Registration. |
| **Operator** | The step's function code (`CHA`, `DCH`, `PAU`, `SET`, `CYC`, …). Full catalog in §4.7. |
| **Nominal Value** | The setpoint or parameter of a step (current, voltage, power, resistance, ramp, variable assignment, …). *In plain terms: the number the step aims at — "charge at 10 A" makes 10 A the nominal value.* |
| **Limit** | The condition that ends a step or triggers its Action. |
| **Action** | What happens when a Limit is met (continue, `INT`, `STO`, `GOTO`, procedure, `ERR`, `MSG`). |
| **Registration** | A recorded measurement sample, and the rules governing when samples are recorded. *In plain terms: one saved row of measurements in the test record. Not to be confused with a device announcing itself on the network — see `ME-SRS-WEB-001` §1.4.* |
| **Registration format** | The named set of measurement channels written in each registration record. |
| **Session** | One execution of one program on one channel, with its associated registration data. |
| **Safe state** | The channel condition in which no energy is transferred to or from the battery. |
| **Latched fault** | A fault that persists after its cause clears, until explicitly cleared. *In plain terms: the fault stays raised even once the problem goes away, so somebody has to acknowledge it.* |
| **Presence** | A Secondary Board's state of being enumerated and responsive. *In plain terms: the Primary has found it, knows what it is, and it is still answering.* |

### 1.5 Acronyms

`ADC` Analog-to-Digital Converter · `CAN` Controller Area Network · `CAN-FD` CAN with
Flexible Data-rate · `CC` Constant Current · `CP` Constant Power · `CV` Constant Voltage ·
`DAC` Digital-to-Analog Converter · `DBC` CAN database file · `E-stop` Emergency Stop ·
`ECC` Error-Correcting Code (*memory that detects and repairs single-bit corruption by itself*) · `EIS` Electrochemical Impedance Spectroscopy ·
`HAB` High Assurance Boot (*the chip refuses to run firmware that is not correctly signed*) · `ICD` Interface Control Document · `LNT` Transistor-bank
voltage feedback, charge mode (Vrect+) · `NVM` Non-Volatile Memory · `PFA` Power-Fail
Action (*what the board does in the moment it detects the supply is about to disappear*) · `POST` Power-On Self-Test · `RTC` Real-Time Clock · `RTOS` Real-Time Operating
System · `SoC` System on Chip · `TCM` Tightly Coupled Memory (*small fast memory wired directly to one core, so its access time never varies*) · `TSN` Time-Sensitive
Networking (*Ethernet extensions that give a message a guaranteed delivery deadline*) · `ZNT` Transistor-bank voltage feedback, discharge mode (Vrect−)

### 1.6 References

| Tag | Document |
|---|---|
| **HW** | `Digital_Controller_Specs_V1.04_RemarkAdded-10-Feb-2026 (1) - Copy.xlsx` — client hardware specification (Secondary Board). Sheets: *Digital Controller*, *Error code*, *Configuration*, *Calibration*, *Coding Guidelines*, *Queries & Implement*, *Version* |
| **BM** | `BM_Manual_eng 2.pdf` — Digatron Battery Manager User Manual 99.3, rel. 2022-2, 363 pp. **Ch.12 BTS-600 Programs, pp.156–219** is the functional baseline |
| **SOC** | NXP `IMX8MPIEC` Rev.1, 08/2021 — *i.MX 8M Plus Applications Processor Datasheet for Industrial Products*. Covers `MIMX8ML8CVNKZAB` |
| **LSRS** | `BTS_Primary_SW_Requirement_Analysis V1.7.xlsx` — legacy combined Primary+Secondary SRS, 281 requirements. Reference only |
| **WAD** | `WebAppDocs/` — existing Ador BTS server documentation set (SRS, ADD, DDD, DBD, **ICD**, RTM, ARCHITECTURE_CAPACITY, CMP, PMP, QAP, RMP, VVP) |
| **CODE-P** | `D:\Projects\BTS_VS_CODE\BTS_Primary_SOM` — legacy Primary firmware (SAM9X60). Evidence only |
| **CODE-S** | `D:\Projects\BTS_VS_CODE\BTS_SEC_FW_V201` — legacy Secondary firmware (STM32H7). Evidence only |
| **CODE-C** | `D:\Projects\BTS_VS_CODE\BTS_PRIM_COM_V106` — legacy COM Controller. **Board deleted in ME**; evidence only |
| **GATE** | `00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` — source-coverage, open-issue and conflict registers |
| **NEW — ME** | Requirement arises from the ME architecture with no precedent in any source |

**Precedence when sources conflict:** HW > BM > LSRS. Conflicts are never resolved
silently; each is carried in the conflict register (§8.2) with a `<TBD-nn>` marker where
a value cannot be determined.

**Legacy code status.** Legacy firmware is treated strictly as *evidence of implemented
intent to be confirmed*, never as a requirement. ME is a new project; the old code shows
only how a feature was previously realised.

### 1.7 Requirement conventions

- **ID:** `SRS-PRI-<AREA>-<NNN>` where `<AREA>` is the coverage-checklist code.
- **"shall"** denotes a binding requirement. One requirement per statement.
- **`Source`** cites document + section, a legacy code artefact, or `NEW — ME`.
- **`Alloc`** is the Cortex-A53 / Cortex-M7 allocation. `TBD` = pending **D-01**.
  A non-`TBD` value appears only where a source forces it, and the source is cited.
- **`<TBD-nn>`** marks a numeric value that could not be sourced; each is registered in §8.3.
- **Open Issue #N** references the register in §8.1.

---

## 2. Overall Description

### 2.1 Product perspective

The ME system replaces the BTS system. Three changes drive this specification:

| # | BTS | ME |
|---|---|---|
| 1 | Primary = SAM9X60 SOM **+ separate STM32H7 COM Controller** | Primary = single i.MX 8M Plus; **no COM Controller** |
| 2 | 1 Primary : **1** Secondary, over **UART** | 1 Primary : **up to 8** Secondaries, over **CAN** |
| 3 | Step sequencing executed **on the Secondary** | Step sequencing executed **on the Primary**; Secondaries receive minimum control data |

The third change is the most consequential. In BTS the Secondary held the program and
ran the step engine (CODE-S `btsSecApp.c`, `stepData.c`, `cycleTable.c`). In ME the
Primary owns program storage, decoding, sequencing, cycle and branch control, limit
evaluation and registration triggering; each Secondary becomes a regulated
charge/discharge actuator with local protection.

### 2.2 System context

```
                    ┌──────────────────────────┐
                    │    Web Application       │
                    └────────────┬─────────────┘
                                 │  IF-A  (TCP + UDP; details = D-05)
                                 │  programs, config, commands, calibration,
                                 │  telemetry, logged data, events
                    ┌────────────┴─────────────┐
                    │      PRIMARY BOARD       │
                    │  MIMX8ML8CVNKZAB         │
                    │  A53 cluster: Linux      │
                    │  Cortex-M7: RTOS         │
                    │  (allocation = D-01)     │
                    └────────────┬─────────────┘
                                 │  IF-B  (CAN; details = D-04)
        ┌──────────┬──────────┬──┴───────┬──────────┬─────────┐
        │          │          │          │          │         │
   ┌────┴───┐ ┌───┴────┐ ┌───┴────┐    ...     ┌───┴────┐
   │ SEC 1  │ │ SEC 2  │ │ SEC 3  │            │ SEC 8  │
   │ STM32  │ │ STM32  │ │ STM32  │            │ STM32  │
   └────┬───┘ └───┬────┘ └───┬────┘            └───┬────┘
     battery   battery    battery                battery

   Additional Primary interface:
        IF-D  Modbus module (Open Issue #29 answer: handled by the Primary Board)
```

### 2.3 SoC constraint envelope

From **SOC**. Capability is a *constraint envelope* (*a list of what the chip makes
possible — the outer boundary the design must stay inside, not a list of things the
design must use*), not a requirement: no requirement in this document exists merely
because the silicon offers a peripheral.

| Resource | Provision | Bearing on this SRS |
|---|---|---|
| Cortex-A53 | 4 cores ≤ 1.6 GHz, 512 KB L2 **with ECC** | Host for Linux |
| Cortex-M7 | 1 core ≤ 800 MHz, **256 KB TCM**, 32 KB L1-I/D | Runs an RTOS (given). Workload = **D-01** |
| **FlexCAN** | **2 instances**, CAN-FD and CAN 2.0B | One serves IF-B. **At most one spare** → conflict **C-16**, Open Issue **#29** |
| Ethernet | `ENET` 1 Gb (IEEE 1588) + `ENET_QOS` 1 Gb (**TSN**, 802.1Qbv/Qbu) | IF-A transport; TSN need = Open Issue **#30** |
| UART | 4 (≤ 5 Mbps) | Modbus serial + console; external transceivers required → **C-17** |
| I²C / eCSPI | 6 / 3 | Board peripherals, external ADC if fitted |
| DRAM | 32-bit LPDDR4-4000 / DDR4-3200, ≤ 8 GB, **Inline ECC** | Program and buffer capacity |
| On-chip RAM | 868 KB (OCRAM 576 KB + OCRAM_A 256 KB + OCRAM_S 36 KB), ECC-capable | |
| Storage | eMMC 5.1 ×2, SD 3.0 ×3, raw NAND (BCH-62), SPI-NOR ×3, FlexSPI Octal **XIP** | Which is fitted = Open Issue **#3** |
| Watchdog | **3** (`WDOG1/2/3`), two compare points each, external WDOG line | P1, P22 |
| Timers | 6 × 32-bit GPT with compare/capture | Step timing, registration timing |
| RTC | **Secure RTC in SNVS** (on-die) | Battery-backed board RTC = Open Issue **#16** |
| Temperature | TMU + 2 probes, programmable trip points | P22 |
| Security | TrustZone, **HAB**, **CAAM** (AES/RSA/ECC/TRNG/RTIC, 32 KB secure memory), SNVS, RDC, CSU, TZASC, OCOTP eFuse, Secure JTAG | P20, P24 |
| **ADC** | **None (no general-purpose ADC)** | Any Primary analog input needs external silicon → Open Issue **#28**, conflict **C-18** |
| Temp grade | Industrial, Tj −40 … +105 °C | §5.4 |

### 2.4 User classes

| Class | Interaction | Source |
|---|---|---|
| **Operator** | Starts/stops/interrupts/continues tests, acknowledges faults, views live and historical data — **entirely via the Web Application** | BM §2; WAD FR-001…011 |
| **Test engineer** | Authors and assigns programs, selects registration formats, defines batteries | BM §12; WAD |
| **Service / commissioning engineer** | Calibration, configuration, per-channel setup, diagnostics, firmware update | HW *Configuration*, *Calibration* |
| **Manufacturing** | Factory provisioning: identity, versions, calibration dates | HW Config #1–4, #44–47 |
| **Web Application (software actor)** | Sole external command and data peer over IF-A | WAD/ICD |
| **Secondary Board (software actor)** | Commanded peer over IF-B, ×8 | HW §7 |

The Primary Board has **no local human-machine interface** in any source. See Open Issue **#3**.

### 2.5 Operating environment

Linux on the A53 cluster; an RTOS on the Cortex-M7. Industrial temperature grade
(Tj −40 … +105 °C). Cabinet-mounted alongside up to 8 Secondary Boards, each fed
230 V AC from its heatsink base board (HW *Digital Controller* #1). Network-attached to
the Web Application. The Primary is not assumed to be physically accessible during
normal operation.

### 2.6 Design and implementation constraints

| ID | Constraint | Source |
|---|---|---|
| C-01 | Maximum **8** Secondary Boards per Primary | Project brief |
| C-02 | Primary ↔ Secondary link is **CAN**; layer details reserved | Brief; HW §7; **D-04** |
| C-03 | Web App ↔ Primary uses **TCP and UDP**; assignment reserved | Brief; **D-05** |
| C-04 | The Primary decodes programs and executes step sequencing | Brief |
| C-05 | Only the **minimum necessary control data** goes to a Secondary | Brief; **D-03** |
| C-06 | **No COM Controller board exists** | Architect instruction, 2026-07-27 |
| C-07 | An RTOS runs on the Cortex-M7 | Architect instruction, 2026-07-27 |
| C-08 | Function allocation across A53/M7 is undecided | **D-01**, **D-02**, **D-06** |
| C-09 | Layered software architecture, AUTOSAR-like (Application / Interface / BSW / COM) | HW *Digital Controller* #9 |
| C-10 | No dynamic memory allocation after initialisation without explicit approval | Team engineering policy |
| C-11 | No source file exceeds 2000 lines | Team engineering policy |
| C-12 | MISRA-C applicability undecided | Open Issue **#26** |
| C-13 | Configuration parameters ≤ **200**; error codes ≤ **100** | HW *Digital Controller* #11, #12 |

### 2.7 Assumptions and dependencies

| ID | Assumption |
|---|---|
| A-01 | One Secondary Board = one test channel. |
| A-02 | The Web Application is the sole source of programs, configuration and operator commands. |
| A-03 | "Secondary Board" ≡ the Digital Controller Transcard of **HW**; "Main Controller" in **HW** ≡ the Primary Board. |
| A-04 | Web-App-facing functional behaviour follows the **WAD** baseline except where the ME topology requires otherwise. Subject to Open Issue **#2**. |
| A-05 | The BTS-600 language of **BM** Ch.12 defines required functional behaviour, not wire compatibility (§1.3). |
| A-06 | Legacy code is evidence of intent, never a requirement. |
| A-07 | Every requirement's `Alloc` is `TBD` unless a source forces it; the M7-runs-an-RTOS constraint is **not** an allocation decision. |
| A-08 | Where no source exists, this SRS records the gap rather than inventing a requirement. |
| A-09 | The legacy external CAN + DBC port set is **out of ME scope** (Open Issue **#29**); the Modbus module is **in** scope and Primary-hosted. |
| A-10 | Secondary Boards are electrically independent; no shared-rectifier coupling is assumed. Subject to Open Issue **#24**. |

---

## 3. External Interface Requirements

### 3.1 Hardware interfaces

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-HW-001 | The Primary Board shall communicate with up to 8 Secondary Boards over a CAN interface. | Brief; HW §7 | TBD |
| SRS-PRI-HW-002 | The Primary Board shall communicate with the Web Application over an Ethernet interface. | Brief; WAD/ICD | TBD |
| SRS-PRI-HW-003 | The Primary Board shall provide a Modbus interface. | Open Issue #29 resolution | TBD |
| SRS-PRI-HW-004 | The Primary Board shall retain all persistent data in non-volatile storage that survives loss of supply. | HW *Digital Controller* #67; LSRS | TBD |
| SRS-PRI-HW-005 | The Primary Board shall service a hardware watchdog such that an unserviced watchdog causes a board reset. | HW *Digital Controller* #10; SOC (WDOG1/2/3) | TBD |
| SRS-PRI-HW-006 | The Primary Board shall detect an impending loss of supply and signal it to software before supply voltage falls below the level required for correct operation. | HW *Digital Controller* #10, #63 | TBD |
| SRS-PRI-HW-007 | The Primary Board shall obtain wall-clock time from a real-time clock. | LSRS SW_REQ_19–23; SOC (SNVS RTC) | TBD |
| SRS-PRI-HW-008 | The Primary Board shall record `<TBD-16>` (whether the RTC is battery-backed and retains time across supply loss) — see Open Issue #16. | Unsourced | TBD |

### 3.2 Software interfaces

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-SW-001 | The Primary Board shall present exactly one logical interface to the Web Application (IF-A) carrying all program, configuration, control, calibration, telemetry, logged-data and event traffic. | WAD/ICD; brief | TBD |
| SRS-PRI-SW-002 | The Primary Board shall present one logical interface per Secondary Board (IF-B), addressed individually. | Brief; **D-04** | TBD |
| SRS-PRI-SW-003 | The Primary Board shall implement the message set defined in `ME-ICD-001` for IF-A and IF-B. | ME-ICD-001 | TBD |
| SRS-PRI-SW-004 | The Primary Board shall be implemented in a layered architecture separating Application, Interface, Basic Software and Communication concerns. | HW *Digital Controller* #9 | TBD |

### 3.3 Communication interfaces

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-CM-001 | The Primary Board shall detect corrupted messages on every external interface and discard them without acting on their content. | WAD/ICD §3.2 (CRC-16/Modbus); CODE-P | TBD |
| SRS-PRI-CM-002 | The Primary Board shall acknowledge or negatively acknowledge every command received on IF-A that requires a response. | WAD/ICD §3.4; CODE-P | TBD |
| SRS-PRI-CM-003 | The Primary Board shall reject a command whose parameters are outside their permitted range, and report the rejection with a reason. | CODE-S `en_ERR_INVALID_CMD_PRIM_TO_SEC`; HW error `0x0E` | TBD |
| SRS-PRI-CM-004 | The Primary Board shall detect the loss of the Web Application link. | CODE-P `ERR_NETWORK_CONN_FAIL` | TBD |
| SRS-PRI-CM-005 | The Primary Board shall detect the loss of each Secondary Board link independently. | HW Config #9; CODE-S | TBD |
| SRS-PRI-CM-006 | The Primary Board shall treat a Secondary Board as unresponsive when no valid message has been received from it within a configurable timeout, default **100 ms**. | HW Config #9 | TBD |
| SRS-PRI-CM-007 | The Primary Board shall not allow the failure of one Secondary Board link to prevent communication with the remaining Secondary Boards. | NEW — ME | TBD |
| SRS-PRI-CM-008 | The Primary Board shall timestamp every message it originates on IF-A that carries measurement or event data. | BM §12.4.1; WAD/ICD §6 | TBD |

### 3.4 User interfaces

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-UI-001 | The Primary Board shall expose all operator functions through the Web Application; it shall not require a local human-machine interface for any operational function. | BM §2; WAD; §2.4 | TBD |
| SRS-PRI-UI-002 | The Primary Board shall provide a diagnostic access path usable by service personnel independently of the Web Application link. | HW *Digital Controller* #13 | TBD |

---

## 4. Functional Requirements

### 4.1 P1 — System startup, initialization, self-test, shutdown

**Coverage:** ◐ Partial. Startup and watchdog behaviour are sourced; POST content and
orderly shutdown are not — Open Issue **#12**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P1-001 | The Primary Board shall complete initialization and reach a defined operating state after every application of supply or reset. | CODE-P `BTS_APP_STATE_INIT` | TBD |
| SRS-PRI-P1-002 | The Primary Board shall place every controlled channel in the safe state before enabling any Secondary Board output. | CODE-S `relayDefaultState()`; BM §12.4 safety note | TBD |
| SRS-PRI-P1-003 | The Primary Board shall verify the integrity of its persistent configuration during initialization. | HW *Digital Controller* #12; CODE-P `eeprom.c` | TBD |
| SRS-PRI-P1-004 | The Primary Board shall apply factory default configuration values for any configuration item whose stored value fails its integrity check, and shall log the substitution. | LSRS SW_REQ_23 | TBD |
| SRS-PRI-P1-005 | The Primary Board shall verify the integrity of its persistent calibration data during initialization. | HW *Calibration* | TBD |
| SRS-PRI-P1-006 | The Primary Board shall raise a fault and remain out of the Ready state if calibration data fails its integrity check. | HW *Calibration*; derived | TBD |
| SRS-PRI-P1-007 | The Primary Board shall verify that the real-time clock holds a plausible time during initialization, and shall raise a fault if it does not. | LSRS SW_REQ_23 | TBD |
| SRS-PRI-P1-008 | The Primary Board shall execute a power-on self-test before entering the Ready state. | HW *Digital Controller* #10; Open Issue #12 | TBD |
| SRS-PRI-P1-009 | The Primary Board shall report the outcome of every power-on self-test to the Web Application when the link is available, and shall retain the outcome for later retrieval when it is not. | derived from P22, P16 | TBD |
| SRS-PRI-P1-010 | The Primary Board shall not enter the Ready state while any power-on self-test is failed. | derived | TBD |
| SRS-PRI-P1-011 | The Primary Board shall determine, during initialization, whether the preceding shutdown was orderly or was caused by loss of supply, reset or watchdog expiry. | CODE-P `power_fail.c`; HW #63–65 | TBD |
| SRS-PRI-P1-012 | The Primary Board shall make the reason for its most recent restart available to the Web Application. | derived from P22 | TBD |
| SRS-PRI-P1-013 | The Primary Board shall enumerate the connected Secondary Boards during initialization before accepting any program start command. | NEW — ME; §4.3; `ICD` B-NOD-01 | TBD |
| SRS-PRI-P1-014 | The Primary Board shall report its own identity, hardware version, bootloader version and application software version on request. | HW Config #1–4 | TBD |
| SRS-PRI-P1-015 | The Primary Board shall accept a shutdown request and, on receiving one, bring every channel to the safe state, flush all pending log and registration data to non-volatile storage, and then signal that it is safe to remove supply. | Open Issue #12 | TBD |
| SRS-PRI-P1-016 | The Primary Board shall record `<TBD-17>` (the enumerated content of the power-on self-test for each subsystem) — see Open Issue #12. | Unsourced | TBD |

### 4.2 P2 — Operating mode / state machine

**Coverage:** ◐ Partial. Channel states are sourced from BM §2.10 and the legacy
firmware; a board-level state machine spanning 8 channels is new; Maintenance mode is
unsourced — Open Issue **#13**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P2-001 | The Primary Board shall maintain a board-level operating state, distinct from and additional to the per-channel states. | NEW — ME | TBD |
| SRS-PRI-P2-002 | The Primary Board shall implement the board-level states Initializing, Ready, Running, Degraded, Fault and Maintenance. | NEW — ME; Open Issue #13 | TBD |
| SRS-PRI-P2-003 | The Primary Board shall enter Ready when initialization completes with no failed self-test and no active board-level fault. | derived | TBD |
| SRS-PRI-P2-004 | The Primary Board shall enter Running when at least one channel is executing a program. | derived | TBD |
| SRS-PRI-P2-005 | The Primary Board shall enter Degraded when it can continue to control at least one channel but at least one enumerated Secondary Board is unavailable. | NEW — ME | TBD |
| SRS-PRI-P2-006 | The Primary Board shall enter Fault when it can no longer safely control any channel. | derived | TBD |
| SRS-PRI-P2-007 | The Primary Board shall maintain an independent operating state for each of the up to 8 channels. | NEW — ME | TBD |
| SRS-PRI-P2-008 | The Primary Board shall implement the per-channel states Idle, Charge, Discharge, Pause, Continue, Interrupt, Error, Message and Offline. | BM §2.10; CODE-S `circuitState_t`; WAD/ICD §9.1 | TBD |
| SRS-PRI-P2-009 | The Primary Board shall implement the per-channel states Recharge, Deferred Start, Reset and Zero. | BM §2.10 | TBD |
| SRS-PRI-P2-010 | The Primary Board shall permit a channel to change state only through a defined transition. | derived | TBD |
| SRS-PRI-P2-011 | The Primary Board shall reject any command that is not valid in the addressed channel's current state, and shall report the rejection with a reason. | derived; SRS-PRI-CM-003 | TBD |
| SRS-PRI-P2-012 | The Primary Board shall report every board-level and per-channel state change to the Web Application. | WAD/ICD §5.2; `ICD` A-TLM-02 | TBD |
| SRS-PRI-P2-013 | The Primary Board shall record every board-level and per-channel state change in its event log. | derived from P22 | TBD |
| SRS-PRI-P2-014 | The Primary Board shall not allow a channel in the Error state to leave that state until its fault is cleared in accordance with §4.12. | derived | TBD |
| SRS-PRI-P2-015 | The Primary Board shall record `<TBD-18>` (entry conditions, exit conditions and permitted operations for Maintenance mode, and its effect on running programs) — see Open Issue #13. | Unsourced | TBD |

### 4.3 P3 — Secondary Board discovery, enumeration, addressing, presence monitoring

**Coverage:** ◆ New to ME. BTS was 1:1 over UART with no enumeration. Addressing is
reserved to **D-04**; enumeration policy is unsourced — Open Issue **#14**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P3-001 | The Primary Board shall support up to 8 Secondary Boards concurrently. | Brief | TBD |
| SRS-PRI-P3-002 | The Primary Board shall determine, after initialization, which Secondary Boards are present. | NEW — ME | TBD |
| SRS-PRI-P3-003 | The Primary Board shall address each Secondary Board individually and unambiguously. | NEW — ME; **D-04** | TBD |
| SRS-PRI-P3-004 | The Primary Board shall obtain from each present Secondary Board its node identity, hardware version, bootloader version, application software version and type number. | HW Config #1–4; `ICD` A-NOD-02 | TBD |
| SRS-PRI-P3-005 | The Primary Board shall detect the presence of two Secondary Boards claiming the same node identity, and shall raise a fault identifying the conflict. | NEW — ME; `ICD` A-NOD-04 | TBD |
| SRS-PRI-P3-006 | The Primary Board shall not dispatch any control command to a Secondary Board that it has not successfully enumerated. | NEW — ME | TBD |
| SRS-PRI-P3-007 | The Primary Board shall monitor the presence of every enumerated Secondary Board continuously while that board is enumerated. | HW Config #9; `ICD` B-NOD-03 | TBD |
| SRS-PRI-P3-008 | The Primary Board shall declare an enumerated Secondary Board absent when no valid message has been received from it within the configured CAN timeout, default **100 ms**. | HW Config #9; `ICD` B-NOD-04 | TBD |
| SRS-PRI-P3-009 | The Primary Board shall report every change in a Secondary Board's presence to the Web Application. | NEW — ME; `ICD` A-EVT-04, A-NOD-03 | TBD |
| SRS-PRI-P3-010 | The Primary Board shall record every change in a Secondary Board's presence in its event log. | derived from P22 | TBD |
| SRS-PRI-P3-011 | The Primary Board shall make the current presence and identity of all 8 channel positions available to the Web Application as a single aggregated view. | NEW — ME; `ICD` A-NOD-01, A-NOD-02, A-TLM-02 | TBD |
| SRS-PRI-P3-012 | The Primary Board shall verify that a Secondary Board's reported application software version is compatible with its own before permitting a program to run on that channel, and shall raise a fault if it is not. | derived from P20; `ICD` A-NOD-02 | TBD |
| SRS-PRI-P3-013 | The Primary Board shall record `<TBD-19>` (how a Secondary Board acquires its node identity: strap/DIP, Primary assignment, or provisioned value) — see Open Issue #22 and **D-04**. | Unsourced | TBD |
| SRS-PRI-P3-014 | The Primary Board shall record `<TBD-20>` (whether Secondary Boards may be added or removed while the Primary is running, and the required behaviour if so) — see Open Issue #14. | Unsourced | TBD |
| SRS-PRI-P3-015 | The Primary Board shall record `<TBD-21>` (whether a program may be started when fewer than the configured number of Secondary Boards are present) — see Open Issue #14. | Unsourced | TBD |

### 4.4 P4 — Program reception from the Web Application

**Coverage:** ● Strong. Program transfer is well evidenced; versioning and rollback are
not — Open Issue **#25**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P4-001 | The Primary Board shall accept a test program transferred from the Web Application. | CODE-P `ProgramQueryID_t`; WAD FR-005 | TBD |
| SRS-PRI-P4-002 | The Primary Board shall indicate to the Web Application, on request, whether it is ready to receive a program. | CODE-P `ProgramQueryID_t` (is-ready); WAD/ICD §3.6 `0x01`; `ICD` A-PRG-01 | TBD |
| SRS-PRI-P4-003 | The Primary Board shall accept program metadata comprising at least program identity, program version, program name and total step count, before accepting step content. | CODE-P; WAD/ICD §3.6 `0x03`; `ICD` A-PRG-02 | TBD |
| SRS-PRI-P4-004 | The Primary Board shall accept a program in multiple transfer units and reassemble it in the correct order. | CODE-P (1 MB program buffer) | TBD |
| SRS-PRI-P4-005 | The Primary Board shall detect a missing, duplicated or out-of-order transfer unit during program reception and shall reject the transfer. | derived | TBD |
| SRS-PRI-P4-006 | The Primary Board shall verify the integrity of a received program before accepting it. | CODE-P (packet CRC) | TBD |
| SRS-PRI-P4-007 | The Primary Board shall reject a program whose received step count does not equal the step count declared in its metadata. | derived; `ICD` A-PRG-05 | TBD |
| SRS-PRI-P4-008 | The Primary Board shall report the outcome of every program transfer to the Web Application, distinguishing acceptance from each defined rejection reason. | derived; WAD/ICD; `ICD` A-PRG-05 | TBD |
| SRS-PRI-P4-009 | The Primary Board shall not overwrite or modify a program that is currently executing. | derived; `ICD` A-PRG-08 | TBD |
| SRS-PRI-P4-010 | The Primary Board shall store an accepted program in non-volatile storage. | Open Issue #25; HW #67 | TBD |
| SRS-PRI-P4-011 | The Primary Board shall retain stored programs across a restart. | Open Issue #25 | TBD |
| SRS-PRI-P4-012 | The Primary Board shall accept battery parameter data associated with a program, comprising nominal capacity, number of cells, gassing voltage, maximum voltage, nominal current, cold-cranking current, charge factor, internal resistance, cut-off voltage, nominal voltage and energy density. | BM §12.3 (`INTERN[17]`…`INTERN[27]`) | TBD |
| SRS-PRI-P4-013 | The Primary Board shall accept a registration format definition associated with a program. | BM §12.4.1; `ICD` A-PRG-09 | TBD |
| SRS-PRI-P4-014 | The Primary Board shall make the metadata of every stored program available to the Web Application on request. | CODE-P (read-metadata); `ICD` A-PRG-06 | TBD |
| SRS-PRI-P4-015 | The Primary Board shall return the content of a stored program to the Web Application on request. | CODE-P (read-saved-program); `ICD` A-PRG-07 | TBD |
| SRS-PRI-P4-016 | The Primary Board shall reject a program that would exceed its program storage capacity, and shall report the rejection. | derived; Open Issue #25; `ICD` A-PRG-05 | TBD |
| SRS-PRI-P4-017 | The Primary Board shall accept a program while other channels are executing programs. | NEW — ME | TBD |
| SRS-PRI-P4-018 | The Primary Board shall record `<TBD-22>` (the number of programs that shall be concurrently resident, and whether program versioning and rollback are required) — see Open Issue #25. | Unsourced | TBD |

### 4.5 P5 — Program parsing and decoding; program / step / setpoint data model

**Coverage:** ● Strong. BM Ch.12 defines the complete model; §4.5 and §4.7 together
specify it. Battery-parameter resolution is open — Open Issue **#6**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P5-001 | The Primary Board shall decode a received program into an internal representation suitable for execution, without assistance from any Secondary Board. | Brief; GAP §2 | TBD |
| SRS-PRI-P5-002 | The Primary Board shall represent each program step with the fields Label, Operator, Nominal Value, Limit, Action and Registration. | BM §12.4 | TBD |
| SRS-PRI-P5-003 | The Primary Board shall support more than one Limit, each with its own Action, within a single step. | BM §12.4.4, §12.4.7 example 10 | TBD |
| SRS-PRI-P5-004 | The Primary Board shall support more than one Nominal Value within a single step. | BM §12.4.3 (parallel functions) | TBD |
| SRS-PRI-P5-005 | The Primary Board shall support more than one Registration specification within a single step, and shall treat their order as insignificant. | BM §12.4.6 | TBD |
| SRS-PRI-P5-006 | The Primary Board shall interpret a step whose Label field contains an exclamation mark as a comment or disabled step, and shall not execute it. | BM §12.1 | TBD |
| SRS-PRI-P5-007 | The Primary Board shall validate every decoded step against the operator, nominal-value, limit, action and registration definitions of §4.7 before permitting the program to execute. | derived | TBD |
| SRS-PRI-P5-008 | The Primary Board shall reject a program containing an unrecognised operator, and shall identify the offending step. | CODE-P `ERR_INVALID_PROG_STEP`; WAD/ICD §9.2 bit 16 | TBD |
| SRS-PRI-P5-009 | The Primary Board shall reject a program containing a step with an operator/column combination that the operator does not permit, and shall identify the offending step. | BM §12.4.2 (column applicability) | TBD |
| SRS-PRI-P5-010 | The Primary Board shall reject a program in which a charge, recharge or discharge step specifies a voltage nominal value without also specifying a current, power or resistance nominal value. | BM §12.4.3 | TBD |
| SRS-PRI-P5-011 | The Primary Board shall reject a program containing a `GOTO` whose destination label does not exist at the same program level as the jump. | BM §12.4.5, §12.5.3 | TBD |
| SRS-PRI-P5-012 | The Primary Board shall reject a program that uses any of the reserved label names listed in §4.7.9. | BM §12.5.3 | TBD |
| SRS-PRI-P5-013 | The Primary Board shall reject a program containing an unterminated cycle or a `CYC` without a matching `BEG`. | BM §12.5.1; derived | TBD |
| SRS-PRI-P5-014 | The Primary Board shall reject a program whose cycle nesting exceeds the supported depth. | BM §12.5.1; conflict **C-13** | TBD |
| SRS-PRI-P5-015 | The Primary Board shall reject a program that references a procedure that is not available to it. | BM §12.5.2 | TBD |
| SRS-PRI-P5-016 | The Primary Board shall reject a program that references a nominal-value table that is not available to it. | BM §12.4.8.9 | TBD |
| SRS-PRI-P5-017 | The Primary Board shall reject a program that references a registration format that is not available to it. | BM §12.4.1 | TBD |
| SRS-PRI-P5-018 | The Primary Board shall report every program validation failure to the Web Application with the offending step number and a defined reason code. | BM §9 (compiler error indication); derived | TBD |
| SRS-PRI-P5-019 | The Primary Board shall resolve every battery-parameter-relative nominal value and limit to an absolute value using the battery parameter data associated with the program. | BM §12.3, §12.4.3 | TBD |
| SRS-PRI-P5-020 | The Primary Board shall reject a program that uses a battery-parameter-relative value for which the required battery parameter has not been supplied. | derived from BM §12.3 | TBD |
| SRS-PRI-P5-021 | The Primary Board shall support program variables defined by the `SET` operator, and shall substitute a variable's current value wherever that variable is referenced. | BM §12.4.8.1 | TBD |
| SRS-PRI-P5-022 | The Primary Board shall apply an assignment to a variable whose name matches a channel unit to that channel's counter. | BM §12.4.8.1 | TBD |
| SRS-PRI-P5-023 | The Primary Board shall support the use of a variable as the repetition count of a cycle. | BM §12.5.1 | TBD |
| SRS-PRI-P5-024 | The Primary Board shall represent every setpoint with sufficient numeric range and resolution to express the full configured range of the target channel without loss of commanded accuracy. | derived; HW §5 | TBD |
| SRS-PRI-P5-025 | The Primary Board shall record `<TBD-23>` (whether battery-parameter-relative values are resolved by the Primary at runtime against the live battery record or pre-computed to literals by the Web Application) — see Open Issue #6. | Unsourced | TBD |
| SRS-PRI-P5-026 | The Primary Board shall record `<TBD-13>` (whether the supported cycle structure is 16 distinct cycles, 16 levels of nesting, or another bound) — see conflict **C-13**. | Conflict C-13 | TBD |

### 4.6 P6 — Program scheduling and assignment to Secondary Boards

**Coverage:** ◆ New to ME. Nearest analogues are BM's Dispo List and the Web App's
program scheduler; multi-node assignment semantics are unsourced — Open Issue **#15**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P6-001 | The Primary Board shall accept an assignment of a stored program to one or more identified channels. | Brief | TBD |
| SRS-PRI-P6-002 | The Primary Board shall accept assignments for several channels within a single request. | Brief | TBD |
| SRS-PRI-P6-003 | The Primary Board shall reject an assignment addressed to a channel whose Secondary Board is not present. | derived from SRS-PRI-P3-006 | TBD |
| SRS-PRI-P6-004 | The Primary Board shall reject an assignment addressed to a channel that is currently executing a program. | derived | TBD |
| SRS-PRI-P6-005 | The Primary Board shall associate with each assignment the battery parameter data and registration format to be used for that channel. | BM §12.3, §12.4.1 | TBD |
| SRS-PRI-P6-006 | The Primary Board shall maintain, for each channel, the identity and version of the assigned program. | derived; BM §12.4.8.16 (version-sensitive synchronisation) | TBD |
| SRS-PRI-P6-007 | The Primary Board shall permit the same program to be assigned to more than one channel concurrently. | Brief | TBD |
| SRS-PRI-P6-008 | The Primary Board shall permit different programs to be assigned to different channels concurrently. | Brief | TBD |
| SRS-PRI-P6-009 | The Primary Board shall accept a queue of assignments for a channel and execute them in the order given, starting the next only after the current one has terminated. | BM §10 Dispo List; BM §12.4.5 (`STO` note) | TBD |
| SRS-PRI-P6-010 | The Primary Board shall continue with the next queued assignment for a channel after the current program terminates by `STO`. | BM §12.4.5 | TBD |
| SRS-PRI-P6-011 | The Primary Board shall accept a deferred start time for an assignment and shall not begin execution before that time. | BM §2.10 (Deferred Start) | TBD |
| SRS-PRI-P6-012 | The Primary Board shall accept a start-from-step instruction for an assignment and shall begin execution at the identified step. | BM §2; CODE-P | TBD |
| SRS-PRI-P6-013 | The Primary Board shall report the assignment state of every channel to the Web Application. | derived | TBD |
| SRS-PRI-P6-014 | The Primary Board shall record `<TBD-24>` (whether a single program instance may span several channels as one coordinated run, or whether each channel always executes its own independent instance) — see Open Issue #15. | Unsourced | TBD |

### 4.7 P7 — Step execution engine

**Coverage:** ● Strong — the richest area. BM Ch.12 is transcribed in full in the gate
document's Appendix A and specified normatively below.

**Scope decision (2026-07-27):** *all* BTS-600 operators are in scope; there is no
phasing. Operators whose semantics exist only in the unavailable
`BM_PM_BTS600_New_Operators.pdf` are in scope structurally, with their detailed
behaviour carried as `<TBD-nn>` — Open Issue **#27**.

#### 4.7.1 Engine core

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-001 | The Primary Board shall execute the steps of an assigned program in ascending step order except where an Action, `GOTO`, cycle, procedure or special label directs otherwise. | BM §12.4 | TBD |
| SRS-PRI-P7-002 | The Primary Board shall execute the step sequencing itself and shall not delegate step sequencing to any Secondary Board. | Brief | TBD |
| SRS-PRI-P7-003 | The Primary Board shall, on entering a step, establish that step's nominal values, limits, actions and registration rules before commanding any change in channel output. | derived | TBD |
| SRS-PRI-P7-004 | The Primary Board shall evaluate every active limit of the current step at a rate of at least once per **100 ms**. | BM §12.4.4 ("the check of these limits takes place with the maximum measuring speed, i.e. every 100 ms") | TBD |
| SRS-PRI-P7-005 | The Primary Board shall terminate the running step when any active limit of that step is met, except where the associated Action is `MSG` or `ERR`. | BM §12.4.7 example 10 | TBD |
| SRS-PRI-P7-006 | The Primary Board shall execute the Action associated with the limit that was met. | BM §12.4.5 | TBD |
| SRS-PRI-P7-007 | The Primary Board shall proceed to the next step when the Action associated with the met limit is empty. | BM §12.4.5 | TBD |
| SRS-PRI-P7-008 | The Primary Board shall maintain, for each channel, the current step number, the elapsed step time and the elapsed program time. | WAD/ICD §5.2 | TBD |
| SRS-PRI-P7-009 | The Primary Board shall maintain, for each channel, the counters `Ah`, `Wh`, `AhStep`, `WhStep`, `AhPrev`, `WhPrev`, `AhCha`, `WhCha`, `AhDch`, `WhDch`, `AhBal` and `AhStat`. | BM §12.4.1 | TBD |
| SRS-PRI-P7-010 | The Primary Board shall compute `AhStat` as the accumulated charge ampere-hours divided by the charge factor, less the accumulated discharge ampere-hours. | BM §12.4.1 | TBD |
| SRS-PRI-P7-011 | The Primary Board shall compute `AhBal` as accumulated charge ampere-hours less accumulated discharge ampere-hours, clamped at not less than zero. | BM §12.4.1 | TBD |
| SRS-PRI-P7-012 | The Primary Board shall permit `Ah` and `Wh` to take negative values when discharge predominates. | BM §12.4.1 | TBD |
| SRS-PRI-P7-013 | The Primary Board shall reset the step counters `AhStep` and `WhStep` at the start of each step, and shall carry their previous values into `AhPrev` and `WhPrev`. | BM §12.4.1 | TBD |
| SRS-PRI-P7-014 | The Primary Board shall support a minimum step duration of **0.1 s**. | BM §12.4.8.9 ("for a single circuit you can work with step times of down to 0.1 seconds") | TBD |

#### 4.7.2 Operators — energy transfer and flow control

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-015 | The Primary Board shall implement operator `PAU`, holding the channel with no energy transfer and with power contactors released until the step's limit is met. | BM §12.4.2 | TBD |
| SRS-PRI-P7-016 | The Primary Board shall implement operator `CHA`, charging the battery to the step's nominal value until the step's limit is met, with the charge relay set. | BM §12.4.2 | TBD |
| SRS-PRI-P7-017 | The Primary Board shall implement operator `RCH`, behaving as `CHA` and additionally setting the recharge relay and causing the charge indicator to blink. | BM §12.4.2 | TBD |
| SRS-PRI-P7-018 | The Primary Board shall implement operator `DCH`, discharging the battery to the step's nominal value until the step's limit is met, with the discharge relay set. | BM §12.4.2 | TBD |
| SRS-PRI-P7-019 | The Primary Board shall implement operator `INT`, interrupting the program and holding the channel until an explicit continue command is received. | BM §12.4.2 | TBD |
| SRS-PRI-P7-020 | The Primary Board shall implement operator `STO`, terminating the running program on that channel. | BM §12.4.2 | TBD |
| SRS-PRI-P7-021 | The Primary Board shall, on `STO`, continue with the next queued assignment for that channel and shall not terminate the queue. | BM §12.4.5 | TBD |
| SRS-PRI-P7-022 | The Primary Board shall implement operator `GOTO`, transferring execution to the step whose Label matches the given destination. | BM §12.4.2, §12.5.3 | TBD |
| SRS-PRI-P7-023 | The Primary Board shall accept a `GOTO` destination given in the Nominal Value column when `GOTO` is used as an operator, and in the Action column when used as an action. | BM §12.5.3 | TBD |
| SRS-PRI-P7-024 | The Primary Board shall permit a `GOTO` to enter or leave a cycle. | BM §12.5.3 | TBD |
| SRS-PRI-P7-025 | The Primary Board shall not permit a `GOTO` to cross into or out of a procedure. | BM §12.4.5, §12.5.3 | TBD |
| SRS-PRI-P7-026 | The Primary Board shall implement operator `RET`, returning from a procedure to the next higher program level. | BM §12.4.2 | TBD |
| SRS-PRI-P7-027 | The Primary Board shall implement operator `ALIM`, causing all subsequent current limits expressed in `A` or `mA` to respect the sign of the measured current. | BM §12.4.2 | TBD |
| SRS-PRI-P7-028 | The Primary Board shall, in the absence of `ALIM`, evaluate current limits expressed in `A` or `mA` without regard to sign. | BM §12.4.2, §12.4.4 | TBD |

#### 4.7.3 Operators — cycles and structure

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-029 | The Primary Board shall implement operator `BEG`, marking the beginning of a cycle and binding the cycle name given in the Nominal Value column. | BM §12.4.2, §12.5.1 | TBD |
| SRS-PRI-P7-030 | The Primary Board shall implement operator `CYC`, marking the end of a cycle and repeating the enclosed steps the number of times given in the Nominal Value column. | BM §12.4.2, §12.5.1 | TBD |
| SRS-PRI-P7-031 | The Primary Board shall maintain an independent repetition counter for each cycle in a program. | BM §12.5.1 | TBD |
| SRS-PRI-P7-032 | The Primary Board shall support interlaced cycles. | BM §12.5.1 | TBD |
| SRS-PRI-P7-033 | The Primary Board shall accept a cycle repetition count supplied as a program variable. | BM §12.5.1 | TBD |
| SRS-PRI-P7-034 | The Primary Board shall accept a preset value for a named cycle's counter established by a `SET` operator, and shall begin that cycle from the preset value. | BM §12.4.8.1, §12.5.3 | TBD |
| SRS-PRI-P7-035 | The Primary Board shall report the current cycle number of each running channel to the Web Application. | WAD/ICD §5.2 | TBD |
| SRS-PRI-P7-036 | The Primary Board shall implement procedures as named reusable step sequences invocable by name in the Operator column. | BM §12.5.2 | TBD |
| SRS-PRI-P7-037 | The Primary Board shall, on completing a procedure invoked from a step, continue with the step following the invoking step. | BM §12.5.2 | TBD |
| SRS-PRI-P7-038 | The Primary Board shall, on completing a procedure invoked as an Action, continue with the step following the step that triggered the Action. | BM §12.4.5 | TBD |
| SRS-PRI-P7-039 | The Primary Board shall maintain the program level of execution so that a registration-format change made inside a procedure applies only within that procedure and the enclosing level's format is restored on return. | BM §12.4.1 | TBD |
| SRS-PRI-P7-040 | The Primary Board shall report, for a channel executing a procedure step, both the procedure step being executed and the invoking step in the main program. | BM §12.5.2 | TBD |

#### 4.7.4 Operators — values, counters and registration control

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-041 | The Primary Board shall implement operator `SET` for the assignment of registration formats, counters, variables, timers and global nominal values and limits. | BM §12.4.2, §12.4.8.1, §12.4.8.2 | TBD |
| SRS-PRI-P7-042 | The Primary Board shall implement operator `REG`, recording one registration in the format named in that step. | BM §12.4.2, §12.4.8.3 | TBD |
| SRS-PRI-P7-043 | The Primary Board shall, for a `REG` step with no registration format named, use the format most recently established by `SET`. | BM §12.4.8.3 | TBD |
| SRS-PRI-P7-044 | The Primary Board shall, for a `REG` step naming a test section in the Nominal Value column, record that registration into the named test section. | BM §12.4.8.3 | TBD |
| SRS-PRI-P7-045 | The Primary Board shall implement operator `FILE`, directing subsequent registrations of that channel into the test section named in the Nominal Value column. | BM §12.4.2, §12.4.8.12 | TBD |
| SRS-PRI-P7-046 | The Primary Board shall reject a program in which operator `FILE` names a test section as `Test` without a distinguishing suffix. | BM §12.4.8.12 | TBD |
| SRS-PRI-P7-047 | The Primary Board shall, when `FILE` is used inside a cycle with the same nominal value as the enclosing `BEG`, create a distinct sequentially numbered test section for each cycle repetition. | BM §12.4.8.12 | TBD |
| SRS-PRI-P7-048 | The Primary Board shall implement operator `ADD`, adding the values of the channels named in the Nominal Value column. | BM §12.4.2 | TBD |
| SRS-PRI-P7-049 | The Primary Board shall implement operator `SUB`, subtracting the value of the second named channel from the first. | BM §12.4.2 | TBD |
| SRS-PRI-P7-050 | The Primary Board shall implement operator `CLEAR`, deleting all registrations, limits and global setpoints previously established by `SET`, while leaving the established registration format unchanged. | BM §12.4.8.10 | TBD |
| SRS-PRI-P7-051 | The Primary Board shall implement operator `SAVE`, storing all counters of the channels in the current registration format into the test section named in the Nominal Value column. | BM §12.4.8.7 | TBD |
| SRS-PRI-P7-052 | The Primary Board shall implement operator `REST`, restoring counters previously stored by `SAVE` from the test section named in the Nominal Value column. | BM §12.4.8.8 | TBD |
| SRS-PRI-P7-053 | The Primary Board shall restore under `REST` only those channels present in the registration format declared before the `REST` step. | BM §12.4.8.8 | TBD |
| SRS-PRI-P7-054 | The Primary Board shall, when the data required by a `REST` step is unavailable, either hold the program at that step or interrupt it, and shall report the condition. | BM §12.4.8.8 | TBD |
| SRS-PRI-P7-055 | The Primary Board shall implement operator `SETMUX`, enabling the additional multiplexer channels for serial data acquisition. | BM §12.4.2 | TBD |
| SRS-PRI-P7-056 | The Primary Board shall implement operator `FILTER`, applying the named digital filter to the channels of the attribution list. | BM §12.4.2, §12.4.8.11 | TBD |
| SRS-PRI-P7-057 | The Primary Board shall record `<TBD-25>` (the definition of the available digital filters, which BM states may be defined only by the equipment manufacturer) — see Open Issue #5. | BM §12.4.8.11 | TBD |

#### 4.7.5 Operators — messaging, tasks and external processes

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-058 | The Primary Board shall implement operator `ERR`, recording the numbered message given in the Nominal Value column, interrupting the program, and asserting the channel's error indication and error relay output. | BM §12.4.8.5 | TBD |
| SRS-PRI-P7-059 | The Primary Board shall, on receiving a continue command after an `ERR`, resume execution at the same position within the same step. | BM §12.4.7 example 10 | TBD |
| SRS-PRI-P7-060 | The Primary Board shall implement operator `MSG`, recording the numbered message given in the Nominal Value column without interrupting the program. | BM §12.4.8.6 | TBD |
| SRS-PRI-P7-061 | The Primary Board shall support `ERR` and `MSG` used as Actions as well as operators. | BM §12.4.5 | TBD |
| SRS-PRI-P7-062 | The Primary Board shall, where a step has two limits and the limit carrying `ERR` or `MSG` is met first, record the message and then continue to await the other limit. | BM §12.4.8.5, §12.4.8.6 | TBD |
| SRS-PRI-P7-063 | The Primary Board shall apply the same `ERR` and `MSG` semantics to global limits as to step limits. | BM §12.4.8.5, §12.4.8.6 | TBD |
| SRS-PRI-P7-064 | The Primary Board shall hold a message table associating message numbers with message texts. | BM §12.4.8.5 | TBD |
| SRS-PRI-P7-065 | The Primary Board shall provide by default the messages numbered 1 "Limit reached!", 2 "Voltage limit reached!", 3 "Temperature limit reached!", 4 "Current limit reached!", 5 "Capacity limit reached!" and 6 "External control signal activated!". | BM §12.4.8.5, §12.4.8.6 | TBD |
| SRS-PRI-P7-066 | The Primary Board shall accept additions and modifications to the message table from the Web Application. | BM §12.4.8.5 | TBD |
| SRS-PRI-P7-067 | The Primary Board shall implement operator `TASK`, activating a named process that runs in parallel with the program on that channel. | BM §12.4.2, §12.4.8.4 | TBD |
| SRS-PRI-P7-068 | The Primary Board shall support at least **12** concurrent parallel processes per program. | BM §12.4.8.4 | TBD |
| SRS-PRI-P7-069 | The Primary Board shall implement task `OUW`, enabling the calculation of resistance and power from the measured voltage and current. | BM §12.4.3, §12.4.8.4 | TBD |
| SRS-PRI-P7-070 | The Primary Board shall implement tasks `CGRE`, `CLESS` and `TCONTR`, controlling a temperature-dependent relay output. | BM §12.4.3, §12.4.8.4 | TBD |
| SRS-PRI-P7-071 | The Primary Board shall reject a program in which a safety-related task is invoked by `TASK` before operator `PARALLEL` has been declared. | BM §12.4.8.4, §12.4.8.14 | TBD |
| SRS-PRI-P7-072 | The Primary Board shall implement operator `PROT`, starting an external reporting process and passing it the circuit identity, battery identity and session identity. | BM §12.4.2 | TBD |
| SRS-PRI-P7-073 | The Primary Board shall report, during execution of a `PROT` step and in the registration data, that the `PROT` command was executed. | BM §12.4.2 | TBD |
| SRS-PRI-P7-074 | The Primary Board shall implement operator `BATT`, presenting a constant voltage with an upper and a lower current limit, the units being volts, amperes and amperes respectively. | BM §12.4.2 | TBD |

#### 4.7.6 Operators — tables, ranging, paralleling, synchronisation, EIS

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-075 | The Primary Board shall implement operator `TABLE`, executing the sequence of setpoints held in the named nominal-value table. | BM §12.4.8.9 | TBD |
| SRS-PRI-P7-076 | The Primary Board shall interpret each nominal-value table line as an ordered set of duration, current, power and voltage, of which any subset from one to four values may be present. | BM §12.4.8.9 | TBD |
| SRS-PRI-P7-077 | The Primary Board shall interpret a nominal-value table duration without an explicit unit as seconds, and shall accept explicit units of seconds, minutes and hours. | BM §12.4.8.9 | TBD |
| SRS-PRI-P7-078 | The Primary Board shall treat a nominal-value table line specifying only duration and voltage as a charge step irrespective of the sign of the voltage value. | BM §12.4.8.9 | TBD |
| SRS-PRI-P7-079 | The Primary Board shall treat negative current and power values in a nominal-value table as discharge. | BM §12.4.8.9 | TBD |
| SRS-PRI-P7-080 | The Primary Board shall apply the scaling factors `Factor_I`, `Factor_P` and `Factor_U` to the corresponding values of a nominal-value table when they are present in the step. | BM §12.4.3, §12.4.8.9 C | TBD |
| SRS-PRI-P7-081 | The Primary Board shall apply the table clamps `Ap`, `An`, `Vp`, `Vn`, `Wp` and `Wn` when present, limiting table-derived setpoints to the given maxima and minima. | BM §12.4.8.9 E | TBD |
| SRS-PRI-P7-082 | The Primary Board shall, where a table clamp is absent, limit table-derived setpoints to the values held in the table or to the channel's physical maxima and minima. | BM §12.4.8.9 E | TBD |
| SRS-PRI-P7-083 | The Primary Board shall restart a nominal-value table from its beginning when the table is exhausted and no limit of the step has been met. | BM §12.4.8.9 D | TBD |
| SRS-PRI-P7-084 | The Primary Board shall report the current table step number of a running channel to the Web Application. | WAD/ICD §5.2 | TBD |
| SRS-PRI-P7-085 | The Primary Board shall implement operator `RANGE`, selecting the measurement and control range of a channel. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-086 | The Primary Board shall enable automatic range selection at every program start. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-087 | The Primary Board shall, under automatic range selection, choose for each new step the range with the lowest boundary sufficient for that step's current setpoint. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-088 | The Primary Board shall, under automatic range selection and in the absence of a current setpoint, select range 1. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-089 | The Primary Board shall, on a `RANGE` step naming a range number, select that range manually and retain manual selection for all following steps until `RANGE 0` restores automatic selection. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-090 | The Primary Board shall not change range within a step. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-091 | The Primary Board shall not apply automatic range selection to steps whose nominal value is a ramp, a power value, or a nominal-value table. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-092 | The Primary Board shall effect a range change at the beginning of the next step, having first commanded the channel current to zero. | BM §12.4.8.13 | TBD |
| SRS-PRI-P7-093 | The Primary Board shall record `<TBD-12>` (the number of ranges, their boundaries as a proportion of maximum current, and the changeover hysteresis) — see conflict **C-12**. | Conflict C-12 | TBD |
| SRS-PRI-P7-094 | The Primary Board shall implement operator `PARALLEL`, reserving the channels named in the Nominal Value column for parallel operation with the channel running the program. | BM §12.4.8.14 | TBD |
| SRS-PRI-P7-095 | The Primary Board shall reject a `PARALLEL` operation in which any named channel is not in the stopped state, and shall place the initiating channel in the interrupt state. | BM §12.4.8.14 | TBD |
| SRS-PRI-P7-096 | The Primary Board shall divide the nominal value of a step among all channels participating in a parallel group. | BM §12.4.8.14 | TBD |
| SRS-PRI-P7-097 | The Primary Board shall not permit a channel reserved for parallel operation to be started by another assignment while that reservation holds. | BM §12.4.8.14 | TBD |
| SRS-PRI-P7-098 | The Primary Board shall retain a parallel reservation after the master program terminates normally, and shall release it only on an explicit stop of the master channel. | BM §12.4.8.14 | TBD |
| SRS-PRI-P7-099 | The Primary Board shall report a distinct reason for each defined parallel-operation failure, namely current below setpoint, setpoint above the summed maxima of the group, and a named channel already running. | BM §12.4.8.14 | TBD |
| SRS-PRI-P7-100 | The Primary Board shall support a parallel group of up to 8 channels. | Brief; §1.3 | TBD |
| SRS-PRI-P7-101 | The Primary Board shall implement operator `SYNCLine`, holding a channel at that step until every channel of its synchronisation group has reached a `SYNCLine` or `SYNCProgram` step, and then releasing them together. | BM §12.4.8.16 | TBD |
| SRS-PRI-P7-102 | The Primary Board shall implement operator `SYNCProgram`, behaving as `SYNCLine` but synchronising only those channels running the same program name and the same program version. | BM §12.4.8.16 | TBD |
| SRS-PRI-P7-103 | The Primary Board shall maintain a synchronisation group membership for each channel. | BM §12.4.8.16 | TBD |
| SRS-PRI-P7-104 | The Primary Board shall release a synchronisation barrier and report the condition when a member channel of the group cannot reach the barrier. | NEW — ME; derived from §4.14 | TBD |
| SRS-PRI-P7-105 | The Primary Board shall implement operator `EIS`, executing an impedance-spectroscopy step with the parameters `ADC`, `VDC`, frequency in `mHz`, `Hz` or `kHz`, `AAcMax`, `VAcMax`, `VAcMin`, `mVideal`, `EIStime`, `EISperi` and `AACstart`. | BM §12.4.8.15 | TBD |
| SRS-PRI-P7-106 | The Primary Board shall interpret a single frequency value in an `EIS` step as a measurement at that frequency, and two values as a spectrum from the lower to the higher with 8 measurements per decade. | BM §12.4.8.15 | TBD |
| SRS-PRI-P7-107 | The Primary Board shall apply the `EIS` parameter defaults `AAcMax` 2 A, `VAcMax` 20 V, `VAcMin` −2 V, `mVideal` 10 mV, `EIStime` 10 s, `EISperi` 3 and `AACstart` 20 mA when the corresponding parameter is absent. | BM §12.4.8.15 | TBD |
| SRS-PRI-P7-108 | The Primary Board shall treat an `EIS` step with `ADC` absent or zero as a pause step, a positive `ADC` as charge and a negative `ADC` as discharge. | BM §12.4.8.15 | TBD |
| SRS-PRI-P7-109 | The Primary Board shall reject an `EIS` step defining `VDC` without `ADC`. | BM §12.4.8.15 | TBD |
| SRS-PRI-P7-110 | The Primary Board shall reject an `EIS` step whose time limit is not greater than the duration of the specified measurement. | BM §12.4.8.15 | TBD |

#### 4.7.7 Operators pending external definition

All eight operators below are in ME scope. Their detailed semantics exist only in
`BM_PM_BTS600_New_Operators.pdf`, which is not available — Open Issue **#27**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-111 | The Primary Board shall implement operator `PAUA`, whose detailed behaviour is `<TBD-26>`. | BM §12.4.2; Open Issue #27 | TBD |
| SRS-PRI-P7-112 | The Primary Board shall implement operator `PAUO`, whose detailed behaviour is `<TBD-27>`. | BM §12.4.2; Open Issue #27 | TBD |
| SRS-PRI-P7-113 | The Primary Board shall implement operator `IRANGE`, selecting the current measurement and control range, whose detailed behaviour is `<TBD-28>`. | BM §12.4.2, §12.4.8.13 (partial); Open Issue #27 | TBD |
| SRS-PRI-P7-114 | The Primary Board shall implement operator `URANGE`, selecting the voltage measurement and control range, whose detailed behaviour is `<TBD-29>`. | BM §12.4.2; Open Issue #27 | TBD |
| SRS-PRI-P7-115 | The Primary Board shall implement operator `OUTA`, controlling a digital output from the program, whose detailed behaviour is `<TBD-30>`. | BM §12.4.2; HW *Digital Controller* #4–11 remark; Open Issues #19, #27 | TBD |
| SRS-PRI-P7-116 | The Primary Board shall implement operator `OUTB`, controlling a digital output from the program, whose detailed behaviour is `<TBD-31>`. | BM §12.4.2; HW *Digital Controller* #4–11 remark; Open Issues #19, #27 | TBD |
| SRS-PRI-P7-117 | The Primary Board shall implement operator `ISOEXT`, whose detailed behaviour is `<TBD-32>`. | BM §12.4.2; Open Issue #27 | TBD |
| SRS-PRI-P7-118 | The Primary Board shall implement operator `ISOINT`, whose detailed behaviour is `<TBD-33>`. | BM §12.4.2; Open Issue #27 | TBD |
| SRS-PRI-P7-119 | The Primary Board shall, when the range selected by `IRANGE` or `URANGE` differs from the active range, effect the physical range change at the beginning of the next step. | BM §12.4.8.13 | TBD |

#### 4.7.8 Nominal values

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-120 | The Primary Board shall support constant-current nominal values expressed in `A` and `mA`. | BM §12.4.3 | TBD |
| SRS-PRI-P7-121 | The Primary Board shall support constant-voltage nominal values expressed in `V`. | BM §12.4.3 | TBD |
| SRS-PRI-P7-122 | The Primary Board shall support constant-power nominal values expressed in `Watt`, and shall not accept `W` as the power unit. | BM §12.4.3 | TBD |
| SRS-PRI-P7-123 | The Primary Board shall support constant-resistance nominal values expressed in `Ohm`. | BM §12.4.3 | TBD |
| SRS-PRI-P7-124 | The Primary Board shall support the battery-relative current nominal values `ACn1`, `ACn2`, `ACn4`, `ACn5`, `ACn10` and `ACn20`, resolving each against the battery's nominal capacity and the stated hour rate. | BM §12.4.3 | TBD |
| SRS-PRI-P7-125 | The Primary Board shall support the battery-relative voltage nominal value `VnC`, resolving it as the given volts per cell multiplied by the battery's number of cells. | BM §12.4.3 | TBD |
| SRS-PRI-P7-126 | The Primary Board shall not accept a battery-relative nominal value as a ramp endpoint. | BM §12.4.3 | TBD |
| SRS-PRI-P7-127 | The Primary Board shall support ramp nominal values defined by a start value, an end value and a ramp duration, for current, voltage, power and resistance. | BM §12.4.3 | TBD |
| SRS-PRI-P7-128 | The Primary Board shall, where a ramp completes before the step's limit is met, hold the ramp end value for the remainder of the step. | BM §12.4.7 example 7 | TBD |
| SRS-PRI-P7-129 | The Primary Board shall, where the step's limit is met before the ramp completes, terminate the step and give the limit priority over the ramp duration. | BM §12.4.7 example 7 | TBD |
| SRS-PRI-P7-130 | The Primary Board shall support two simultaneous nominal values in one step and shall command the channel so that neither is exceeded. | BM §12.4.3 | TBD |
| SRS-PRI-P7-131 | The Primary Board shall, for a charge step with current and voltage nominal values, regulate current until the voltage nominal value is reached and thereafter regulate voltage with reducing current. | BM §12.4.3, §12.4.7 example 5 | TBD |
| SRS-PRI-P7-132 | The Primary Board shall, for a discharge step with power and voltage nominal values, regulate power until the voltage nominal value is reached and thereafter hold the voltage constant. | BM §12.4.3 | TBD |
| SRS-PRI-P7-133 | The Primary Board shall support the timers `TIMER1`, `TIMER2` and `TIMER3`, set and started by a `SET` operator and running concurrently with the program. | BM §12.4.3, §12.4.4 | TBD |
| SRS-PRI-P7-134 | The Primary Board shall support the global limits `MaxCHAI`, `MaxDCHI`, `MaxChaU`, `MaxDchU`, `MaxChaW` and `MaxDchW`, applying each to every step of the program. | BM §12.4.3, §12.4.8.2 | TBD |
| SRS-PRI-P7-135 | The Primary Board shall regulate the channel to a global nominal value when a step's own setpoint would otherwise exceed it. | BM §12.4.8.2 | TBD |
| SRS-PRI-P7-136 | The Primary Board shall terminate the program when a global limit with no associated Action is reached. | BM §12.4.8.2 | TBD |

#### 4.7.9 Limits

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-137 | The Primary Board shall support an elapsed-step-time limit expressed in seconds, minutes or hours, or as hours, minutes and seconds. | BM §12.4.4 | TBD |
| SRS-PRI-P7-138 | The Primary Board shall interpret a time limit as elapsed time within the program step and not as time of day. | BM §12.4.4 | TBD |
| SRS-PRI-P7-139 | The Primary Board shall support a lower limit on any registration channel, met when that channel's value falls below the stated value. | BM §12.4.4 | TBD |
| SRS-PRI-P7-140 | The Primary Board shall support an upper limit on any registration channel, met when that channel's value rises above the stated value. | BM §12.4.4 | TBD |
| SRS-PRI-P7-141 | The Primary Board shall support a delta limit, met when the change in a channel's value since the start of the step exceeds the stated value. | BM §12.4.4 | TBD |
| SRS-PRI-P7-142 | The Primary Board shall support the conjunction of two or more limits, met only when every conjoined condition is satisfied. | BM §12.4.4, §12.4.7 example 11 | TBD |
| SRS-PRI-P7-143 | The Primary Board shall associate the Action of a conjoined limit chain with the last condition of that chain. | BM §12.4.7 example 11 | TBD |
| SRS-PRI-P7-144 | The Primary Board shall treat limit channels grouped by a common unit as conjoined when used within a conjoined limit chain. | BM §12.4.7 example 11 | TBD |
| SRS-PRI-P7-145 | The Primary Board shall support the gradient limits `GradT` and `GradTm`, met when temperature rises by more than the stated amount per hour and per minute respectively. | BM §12.4.4 | TBD |
| SRS-PRI-P7-146 | The Primary Board shall support the gradient limits `GradU`, `GradUm` and `GradI`, met when the corresponding quantity changes by more than the stated amount in the stated period. | BM §12.4.4 | TBD |
| SRS-PRI-P7-147 | The Primary Board shall re-establish the gradient reference value once per hour for hour-based gradient limits and once per minute for minute-based gradient limits. | BM §12.4.4 | TBD |
| SRS-PRI-P7-148 | The Primary Board shall evaluate gradient limits against every new measurement sample. | BM §12.4.4 | TBD |
| SRS-PRI-P7-149 | The Primary Board shall support the limit `deltaV`, evaluating the rate of change of the voltage channel or of a designated general real channel. | BM §12.4.4 | TBD |
| SRS-PRI-P7-150 | The Primary Board shall support a configurable time base for `deltaV`, expressed in milliseconds as the interval between two measuring points. | BM §12.4.4 | TBD |
| SRS-PRI-P7-151 | The Primary Board shall support `deltaV` evaluation across a designated contiguous set of general real channels, identified by a start channel index and a channel count. | BM §12.4.4 | TBD |
| SRS-PRI-P7-152 | The Primary Board shall support the timer limits `Timer1`, `Timer2` and `Timer3`, met when the corresponding timer expires. | BM §12.4.4 | TBD |
| SRS-PRI-P7-153 | The Primary Board shall skip a step whose only limit is a timer that has already expired, and shall continue with the following step. | BM §12.4.4 | TBD |
| SRS-PRI-P7-154 | The Primary Board shall support the limit `AhDef`, recording the present ampere-hour value as the reference capacity and terminating the step immediately. | BM §12.4.4 | TBD |
| SRS-PRI-P7-155 | The Primary Board shall record the `AhDef` reference capacity as an unsigned value. | BM §12.4.4 | TBD |
| SRS-PRI-P7-156 | The Primary Board shall support the limit `PercAh`, met when the ampere-hour counter reaches the stated percentage of the `AhDef` reference capacity. | BM §12.4.4 | TBD |
| SRS-PRI-P7-157 | The Primary Board shall support the limit `PERCCN_P`, met when the capacity accumulated at the end of the previous step reaches the stated percentage of the battery's nominal capacity. | BM §12.4.4 | TBD |
| SRS-PRI-P7-158 | The Primary Board shall support the limit `PERCCN_C`, met when the capacity accumulated in the present step reaches the stated percentage of the battery's nominal capacity. | BM §12.4.4 | TBD |
| SRS-PRI-P7-159 | The Primary Board shall evaluate `PERCCN_P` and `PERCCN_C` without regard to the sign of the ampere-hour value. | BM §12.4.4 | TBD |
| SRS-PRI-P7-160 | The Primary Board shall reject a program using the limits `AhDef`, `PercAh`, `PERCCN_P` or `PERCCN_C` with any operator other than `PAU`. | BM §12.4.4 | TBD |
| SRS-PRI-P7-161 | The Primary Board shall support the limits `A_notAbs` and `mA_notAbs`, evaluating current limits with regard to the sign of the measured current. | BM §12.4.4 | TBD |
| SRS-PRI-P7-162 | The Primary Board shall support the derived limits `ACN5`, `VNC`, `OHM` and `WATT`, each evaluated from the present measured values. | BM §12.4.4 | TBD |
| SRS-PRI-P7-163 | The Primary Board shall support the limits `ABATT` and `VBATT`. | BM §12.4.4 | TBD |
| SRS-PRI-P7-164 | The Primary Board shall record `<TBD-34>` (the definition of limits `ABATT` and `VBATT`, described in BM only as customer-specific options) — see Open Issue #5. | BM §12.4.4 | TBD |
| SRS-PRI-P7-165 | The Primary Board shall permit any of the battery parameters of §4.4 to be used as a limit value. | BM §12.3 | TBD |

#### 4.7.10 Actions

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-166 | The Primary Board shall, for a met limit with an empty Action, execute the next step. | BM §12.4.5 | TBD |
| SRS-PRI-P7-167 | The Primary Board shall, for a met limit with Action `INT`, interrupt the program and hold the channel until an explicit continue command is received. | BM §12.4.5 | TBD |
| SRS-PRI-P7-168 | The Primary Board shall, for a met limit with Action `STO`, stop the program on that channel. | BM §12.4.5 | TBD |
| SRS-PRI-P7-169 | The Primary Board shall, for a met limit with Action `GOTO`, transfer execution to the step bearing the named label. | BM §12.4.5 | TBD |
| SRS-PRI-P7-170 | The Primary Board shall, for a met limit whose Action names a procedure, execute that procedure and then continue with the step following the triggering step. | BM §12.4.5 | TBD |
| SRS-PRI-P7-171 | The Primary Board shall support conditional branching by permitting each limit of a step to carry a distinct `GOTO` Action. | BM §12.5.3 | TBD |

#### 4.7.11 Registration triggers

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-172 | The Primary Board shall record a registration at the beginning and at the end of every step for which no registration specification is given. | BM §12.4.6 | TBD |
| SRS-PRI-P7-173 | The Primary Board shall support a delta registration trigger, recording whenever a named channel changes by the stated amount. | BM §12.4.6 | TBD |
| SRS-PRI-P7-174 | The Primary Board shall support a periodic registration trigger, recording at the stated time interval. | BM §12.4.6 | TBD |
| SRS-PRI-P7-175 | The Primary Board shall support a threshold registration trigger, recording when a named channel crosses the stated value in the stated direction. | BM §12.4.6 | TBD |
| SRS-PRI-P7-176 | The Primary Board shall support a gated delta registration trigger, applying a delta or periodic trigger only after a stated gating condition has been satisfied. | BM §12.4.6 | TBD |
| SRS-PRI-P7-177 | The Primary Board shall support a bounded-count registration trigger, recording exactly the stated number of registrations at the highest available resolution. | BM §12.4.6 | TBD |
| SRS-PRI-P7-178 | The Primary Board shall support a registration resolution of **0.1 s**. | BM §12.4.6 | TBD |
| SRS-PRI-P7-179 | The Primary Board shall combine multiple registration triggers within one step, recording whenever any of them is satisfied. | BM §12.4.6 | TBD |
| SRS-PRI-P7-180 | The Primary Board shall apply the registration format named in a step's Registration column to that step only, and shall revert to the format established by `SET` for the following step. | BM §12.4.1 | TBD |
| SRS-PRI-P7-181 | The Primary Board shall support the registration control variable `RLevel` with the values 0, 1, 2, 3 and 4. | BM §12.4.1 | TBD |
| SRS-PRI-P7-182 | The Primary Board shall, for `RLevel` 0, suppress all registrations. | BM §12.4.1 | TBD |
| SRS-PRI-P7-183 | The Primary Board shall, for `RLevel` 1, record step-change registrations only. | BM §12.4.1 | TBD |
| SRS-PRI-P7-184 | The Primary Board shall, for `RLevel` 2, record both data and step-change registrations. | BM §12.4.1 | TBD |
| SRS-PRI-P7-185 | The Primary Board shall, for `RLevel` 3, suppress step-change registrations and record data registrations only at the end of a step. | BM §12.4.1 | TBD |
| SRS-PRI-P7-186 | The Primary Board shall, for `RLevel` 4, suppress step-change registrations and record no data registration at the end of a step. | BM §12.4.1 | TBD |
| SRS-PRI-P7-187 | The Primary Board shall accept a change of `RLevel` at any point in a program and apply it from that point. | BM §12.4.1 | TBD |

#### 4.7.12 Special labels

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P7-188 | The Primary Board shall support the special label `ONERROR`, transferring execution to the step bearing the named label when an error occurs. | BM §12.5.4 | TBD |
| SRS-PRI-P7-189 | The Primary Board shall execute all steps from an `ONERROR` destination up to the next `STO`. | BM §12.5.4 | TBD |
| SRS-PRI-P7-190 | The Primary Board shall support the special label `ONEXIT`, executing the steps under the named label after the program ends. | BM §12.5.4 | TBD |
| SRS-PRI-P7-191 | The Primary Board shall reject a program whose `ONERROR` handler does not begin with a `CLEAR` step. | BM §12.5.4 | TBD |
| SRS-PRI-P7-192 | The Primary Board shall reject a program that uses any of the following as a label: `V, A, mA, Ah, mAh, AhCha, AhLad, mAhCha, mAhLad, AhDch, AhEla, mAhDch, mAhEla, AhStep, AhPas, mAhStep, mAhPas, AhPrev, AhPrec, mAhPrev, mAhPrec, AhStat, mAhStat, AhBal, mAhBal, Wh, mWh, WhCha, WhLad, mWhCha, mWhLad, WhDch, WhEla, mWhDch, mWhEla, WhStep, WhPas, mWhStep, mWhPas, WhPrev, WhPrec, mWhPrev, mWhPrec`. | BM §12.5.3 | TBD |

### 4.8 P8 — Concurrent execution and coordination of up to 8 Secondaries

**Coverage:** ◆◐. Concurrency is new to ME; BM supplies the coordination semantics
(`PARALLEL`, `SYNCLine`, `SYNCProgram`) specified in §4.7.6. Independence policy is
open — Open Issue **#16**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P8-001 | The Primary Board shall execute programs on up to 8 channels concurrently. | Brief | TBD |
| SRS-PRI-P8-002 | The Primary Board shall execute each channel's program independently of the others except where `PARALLEL`, `SYNCLine` or `SYNCProgram` establishes a coupling. | Brief; BM §12.4.8.14, §12.4.8.16 | TBD |
| SRS-PRI-P8-003 | The Primary Board shall maintain a separate execution context per channel, comprising at least the current step, step elapsed time, program elapsed time, cycle counters, timers, variables, counters, active registration format and active range. | derived | TBD |
| SRS-PRI-P8-004 | The Primary Board shall meet the 100 ms limit-evaluation rate of SRS-PRI-P7-004 for every running channel simultaneously. | BM §12.4.4; derived | TBD |
| SRS-PRI-P8-005 | The Primary Board shall meet the 0.1 s registration resolution of SRS-PRI-P7-178 for every running channel simultaneously. | BM §12.4.6; derived | TBD |
| SRS-PRI-P8-006 | The Primary Board shall not allow the termination of a program on one channel to affect the execution of programs on other channels, except where a coordination operator couples them. | NEW — ME | TBD |
| SRS-PRI-P8-007 | The Primary Board shall not allow a fault on one channel to affect the execution of programs on other channels, except where a coordination operator couples them or where a system-level condition of §4.11 applies. | NEW — ME | TBD |
| SRS-PRI-P8-008 | The Primary Board shall not allow the loss of one Secondary Board to affect the execution of programs on other channels, except where a coordination operator couples them. | NEW — ME | TBD |
| SRS-PRI-P8-009 | The Primary Board shall start programs on channels named in a single multi-channel start request such that the difference between their start instants does not exceed `<TBD-35>`. | Open Issue #16 | TBD |
| SRS-PRI-P8-010 | The Primary Board shall report the execution state of all 8 channel positions to the Web Application as a single aggregated view. | NEW — ME | TBD |
| SRS-PRI-P8-011 | The Primary Board shall record `<TBD-36>` (whether a multi-channel assignment runs as independent parallel instances or as one step-synchronised run) — see Open Issue #16. | Unsourced | TBD |

### 4.9 P9 — Command dispatch to Secondary Boards

**Coverage:** ● Strong for the command set; content of the control data is reserved to **D-03**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P9-001 | The Primary Board shall dispatch to each Secondary Board only the control data necessary for that board to charge or discharge its battery. | Brief; **D-03** | TBD |
| SRS-PRI-P9-002 | The Primary Board shall command a Secondary Board to enter charge mode. | HW §7; CODE-S `controlCmdQueryID_t` | TBD |
| SRS-PRI-P9-003 | The Primary Board shall command a Secondary Board to enter discharge mode. | HW §7; CODE-S | TBD |
| SRS-PRI-P9-004 | The Primary Board shall command a Secondary Board to enter pause, with no energy transfer. | HW §7; CODE-S | TBD |
| SRS-PRI-P9-005 | The Primary Board shall command a Secondary Board to stop. | HW §7; CODE-S | TBD |
| SRS-PRI-P9-006 | The Primary Board shall command a Secondary Board to interrupt. | HW §7; CODE-S | TBD |
| SRS-PRI-P9-007 | The Primary Board shall command a Secondary Board to continue from interrupt or pause. | HW §7; CODE-S | TBD |
| SRS-PRI-P9-008 | The Primary Board shall command a Secondary Board to resume after a power-fail event. | CODE-S (power-resume) | TBD |
| SRS-PRI-P9-009 | The Primary Board shall command the setpoint to be regulated by a Secondary Board, together with the regulation mode to which it applies. | Brief; **D-03** | TBD |
| SRS-PRI-P9-010 | The Primary Board shall command the safety limits to be enforced locally by a Secondary Board. | PSPE §4.4 (evidence); **D-03** | TBD |
| SRS-PRI-P9-011 | The Primary Board shall command the measurement and control range to be applied by a Secondary Board. | BM §12.4.8.13; §4.7.6 | TBD |
| SRS-PRI-P9-012 | The Primary Board shall command the state of a Secondary Board's digital outputs. | HW *Digital Controller* #4–11; CODE-S (DO selection) | TBD |
| SRS-PRI-P9-013 | The Primary Board shall confirm that each dispatched command has been accepted by the addressed Secondary Board before treating it as applied. | CODE-S (ACK/NACK) | TBD |
| SRS-PRI-P9-014 | The Primary Board shall detect the rejection of a dispatched command and shall raise a fault identifying the command and the channel. | CODE-S `en_ERR_INVALID_CMD_PRIM_TO_SEC`; HW error `0x0E` | TBD |
| SRS-PRI-P9-015 | The Primary Board shall detect the absence of a response to a dispatched command within a configurable timeout and shall treat the command as not applied. | HW Config #9 | TBD |
| SRS-PRI-P9-016 | The Primary Board shall not dispatch a setpoint that exceeds the addressed channel's configured maximum current, maximum voltage or maximum power. | HW Config #5–8 | TBD |
| SRS-PRI-P9-017 | The Primary Board shall command every channel to its safe state on entering the board-level Fault state. | derived from §4.13 | TBD |
| SRS-PRI-P9-018 | The Primary Board shall record `<TBD-03>` (the exact content of the minimum necessary control data) — see **D-03**. | **D-03** | TBD |

### 4.10 P10 — Telemetry acquisition from Secondaries; aggregation and forwarding

**Coverage:** ● Strong. Rates conflict across four sources — conflict **C-05**; the
8-node aggregate budget is open — Open Issue **#18**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P10-001 | The Primary Board shall acquire measured values from every present Secondary Board. | HW §7 | TBD |
| SRS-PRI-P10-002 | The Primary Board shall acquire from each Secondary Board at least the measured current, measured battery voltage, measured battery temperature, transistor-bank voltage feedback in charge mode, transistor-bank voltage feedback in discharge mode, heatsink temperature and digital input and output state. | HW §4, §7 | TBD |
| SRS-PRI-P10-003 | The Primary Board shall acquire from each Secondary Board its operating state, its active fault set and its latest error code. | HW §7, *Error code*; WAD/ICD §5.2 | TBD |
| SRS-PRI-P10-004 | The Primary Board shall acquire measured values from each Secondary Board at a configurable interval. | HW §6; CODE-S | TBD |
| SRS-PRI-P10-005 | The Primary Board shall derive, for each channel, the instantaneous power from the measured current and voltage. | BM §12.4.8.4 (`OUW`); WAD/ICD §5.2 | TBD |
| SRS-PRI-P10-006 | The Primary Board shall derive, for each channel, the instantaneous resistance from the measured current and voltage. | BM §12.4.8.4 (`OUW`) | TBD |
| SRS-PRI-P10-007 | The Primary Board shall integrate, for each channel, the measured current and power into the counters of SRS-PRI-P7-009. | BM §12.4.1 | TBD |
| SRS-PRI-P10-008 | The Primary Board shall detect a measured value that is stale by more than a configurable age and shall not use it for limit evaluation. | derived from SRS-PRI-CM-006 | TBD |
| SRS-PRI-P10-009 | The Primary Board shall aggregate the telemetry of all channels into a single view for delivery to the Web Application. | NEW — ME | TBD |
| SRS-PRI-P10-010 | The Primary Board shall transmit per-channel live telemetry to the Web Application at a configurable interval. | WAD/ICD §5; CODE-S | TBD |
| SRS-PRI-P10-011 | The Primary Board shall include in each live telemetry record the channel identity, the current step number, the program state, the channel state, the error code, the active fault set, the step elapsed time, the program elapsed time, the measured current, voltage and temperature, the derived power, the accumulated, charge, discharge and step capacities, the accumulated, charge, discharge and step energies, the active operator, the cycle number, the table step number and the digital input and output state. | WAD/ICD §5.2 | TBD |
| SRS-PRI-P10-012 | The Primary Board shall record registrations in accordance with the active registration format and the active registration triggers of the running step. | BM §12.4.1, §12.4.6 | TBD |
| SRS-PRI-P10-013 | The Primary Board shall support the registration formats `SIMPLE`, `STANDARD`, `CHANREG`, `CHAREG`, `DCHREG`, `CYCLE`, `CYCLEC` and `GSM`, each recording the channel set defined in BM §12.4.1. | BM §12.4.1 | TBD |
| SRS-PRI-P10-014 | The Primary Board shall accept, store and apply user-defined registration formats. | BM §12.4.1 | TBD |
| SRS-PRI-P10-015 | The Primary Board shall forward recorded registrations to the Web Application for persistent storage. | WAD/ICD §6 | TBD |
| SRS-PRI-P10-016 | The Primary Board shall sustain the configured telemetry and registration rates for all 8 channels simultaneously without loss of records. | NEW — ME; Open Issue #18 | TBD |
| SRS-PRI-P10-017 | The Primary Board shall detect the loss of a telemetry or registration record and shall report the loss. | derived | TBD |
| SRS-PRI-P10-018 | The Primary Board shall record `<TBD-05>` (the required per-channel registration interval and the acceptable end-to-end telemetry latency to the Web Application) — see conflict **C-05** and Open Issue #18. | Conflict C-05 | TBD |

### 4.11 P11 — Real-time monitoring, limit checking and safety supervision at system level

**Coverage:** ◐ Partial. Per-channel limit supervision is sourced; cross-channel
system-level supervision is new — Open Issue **#24**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P11-001 | The Primary Board shall evaluate every configured global limit against every running channel continuously. | BM §12.4.8.2 | TBD |
| SRS-PRI-P11-002 | The Primary Board shall evaluate the configured maximum current, maximum voltage and minimum voltage of each channel against that channel's measured values continuously. | HW Config #5–8 | TBD |
| SRS-PRI-P11-003 | The Primary Board shall command a channel to its safe state when that channel's measured current exceeds its configured maximum current. | HW *Error code* 8; HW Config #5 | TBD |
| SRS-PRI-P11-004 | The Primary Board shall command a channel to its safe state when that channel's measured voltage exceeds its configured maximum voltage. | HW *Error code* 9; HW Config #7 | TBD |
| SRS-PRI-P11-005 | The Primary Board shall command a channel to its safe state when that channel's measured battery temperature exceeds its configured limit. | HW §4.6; BM §12.4.5 | TBD |
| SRS-PRI-P11-006 | The Primary Board shall command a channel to its safe state when that channel's heatsink temperature exceeds its configured limit. | HW *Error code* 3 | TBD |
| SRS-PRI-P11-007 | The Primary Board shall detect a channel reporting that its commanded current setpoint cannot be reached, and shall treat the condition as a fault. | HW *Error code* 6; HW Config #10–11 | TBD |
| SRS-PRI-P11-008 | The Primary Board shall detect a channel reporting that its commanded voltage setpoint cannot be reached, and shall treat the condition as a fault. | HW *Error code* 7 | TBD |
| SRS-PRI-P11-009 | The Primary Board shall detect a channel reporting reversed battery power-connection polarity, and shall not permit that channel to start a program. | HW *Error code* `0x0A`; HW §4.1 | TBD |
| SRS-PRI-P11-010 | The Primary Board shall detect a channel reporting reversed battery sense-connection polarity, and shall not permit that channel to start a program. | HW *Error code* `0x0B`; HW §4.4 | TBD |
| SRS-PRI-P11-011 | The Primary Board shall detect a channel reporting a difference between its power-connection voltage and its sense-connection voltage greater than the configured limit, and shall treat the condition as a fault. | HW *Error code* `0x0C`; HW §4.1 | TBD |
| SRS-PRI-P11-012 | The Primary Board shall supervise the aggregate of all channels against configured system-level limits. | NEW — ME; Open Issue #24 | TBD |
| SRS-PRI-P11-013 | The Primary Board shall escalate a channel-level condition to a system-level condition when a configured escalation criterion is met. | NEW — ME; Open Issue #24 | TBD |
| SRS-PRI-P11-014 | The Primary Board shall record `<TBD-37>` (the cross-channel quantities to be supervised at system level and the escalation criteria from channel fault to system fault) — see Open Issue #24. | Unsourced | TBD |

### 4.12 P12 — Fault detection, classification, escalation, latching, acknowledgement, recovery

**Coverage:** ◐ Partial. The fault set is well sourced; latch, acknowledge and clear
semantics are defined in no source — Open Issue **#17**. Four incompatible code spaces
exist — conflict **C-14**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P12-001 | The Primary Board shall maintain a single fault-code space for the whole ME system. | Conflict C-14 | TBD |
| SRS-PRI-P12-002 | The Primary Board shall support at least 100 distinct fault codes. | HW *Digital Controller* #11 | TBD |
| SRS-PRI-P12-003 | The Primary Board shall represent each fault reported by a Secondary Board using the fault-code space of SRS-PRI-P12-001. | HW *Error code*; conflict C-14 | TBD |
| SRS-PRI-P12-004 | The Primary Board shall distinguish system faults, which arise from equipment malfunction, from user messages, which arise from program `ERR` and `MSG` operators. | BM §12.4.8.5, §12.4.8.6; CODE-S | TBD |
| SRS-PRI-P12-005 | The Primary Board shall detect and record each of the fault conditions enumerated in the hardware specification's error-code list, namely charge-mode transistor-bank voltage feedback error, discharge-mode transistor-bank voltage feedback error, heatsink over-temperature, current measurement limit, voltage measurement limit, current setpoint unreachable, voltage setpoint unreachable, over-current, over-voltage, power-connection polarity error, sense-connection polarity error, power-to-sense voltage difference beyond limit, loss of communication from the Primary, unknown command from the Primary, current regulator output out of range, voltage regulator output out of range, flashing not possible, power-fail detection, unstable input power, flash erase failure, programming verification failure, error reading non-volatile memory, and loss of main temperature feedback. | HW *Error code* 1…`0x17` | TBD |
| SRS-PRI-P12-006 | The Primary Board shall detect and record the loss of the Web Application link as a fault. | CODE-P `ERR_NETWORK_CONN_FAIL` | TBD |
| SRS-PRI-P12-007 | The Primary Board shall detect and record an invalid program step as a fault. | CODE-P `ERR_INVALID_PROG_STEP` | TBD |
| SRS-PRI-P12-008 | The Primary Board shall classify every fault by severity, distinguishing at least a warning, which does not stop a program, from a trip, which places the affected channel in its safe state. | HW Config #23–42 (warning vs limit); derived | TBD |
| SRS-PRI-P12-009 | The Primary Board shall associate every fault with the channel to which it applies, or with the board where it is not channel-specific. | derived | TBD |
| SRS-PRI-P12-010 | The Primary Board shall latch a fault classified as latching, retaining it after its cause has cleared. | Open Issue #17 | TBD |
| SRS-PRI-P12-011 | The Primary Board shall clear a latched fault only on an explicit acknowledgement. | Open Issue #17 | TBD |
| SRS-PRI-P12-012 | The Primary Board shall reject an acknowledgement of a latched fault whose cause is still present, and shall report the rejection. | derived | TBD |
| SRS-PRI-P12-013 | The Primary Board shall report every fault to the Web Application on detection, and shall report its clearance. | WAD/ICD §9.2 | TBD |
| SRS-PRI-P12-014 | The Primary Board shall record every fault detection, escalation, acknowledgement and clearance in its event log. | derived from §4.22 | TBD |
| SRS-PRI-P12-015 | The Primary Board shall include the active fault set of each channel in that channel's telemetry. | WAD/ICD §5.2 | TBD |
| SRS-PRI-P12-016 | The Primary Board shall transfer execution to a program's `ONERROR` handler, where one is defined, when a fault occurs on the channel running that program. | BM §12.5.4 | TBD |
| SRS-PRI-P12-017 | The Primary Board shall record `<TBD-14>` (the unified fault-code space and the mapping from the hardware specification's error codes, the legacy firmware bitmask and the BM message numbers into it) — see conflict **C-14**. | Conflict C-14 | TBD |
| SRS-PRI-P12-018 | The Primary Board shall record `<TBD-38>` (which faults latch, who may acknowledge them, and whether acknowledgement requires an operator action distinct from a command) — see Open Issue #17. | Unsourced | TBD |

### 4.13 P13 — Emergency stop and safe-state behaviour

**Coverage:** ○ Absent for the emergency-stop input. **No source in the reference set
defines an emergency-stop source for ME.** The hardware specification lists all four
Secondary digital inputs as "Spare" and records that the dual-bank circuit has no
motherboard digital-input connections. BM default message 6 — *"External control signal
activated!"* — implies such an input exists in comparable systems but does not define it.
**No emergency-stop requirements are identified — see Open Issue #8.** The safe-state
requirements below are sourced and are specified.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P13-001 | The Primary Board shall define, for every channel, a safe state in which no energy is transferred to or from the battery. | CODE-S `relayDefaultState()`; BM §12.4 | TBD |
| SRS-PRI-P13-002 | The Primary Board shall command a channel to its safe state on any fault classified as a trip for that channel. | derived from SRS-PRI-P12-008 | TBD |
| SRS-PRI-P13-003 | The Primary Board shall command every channel to its safe state on entering the board-level Fault state. | derived | TBD |
| SRS-PRI-P13-004 | The Primary Board shall command every channel to its safe state before an orderly shutdown completes. | SRS-PRI-P1-015 | TBD |
| SRS-PRI-P13-005 | The Primary Board shall accept a stop-all command that places every channel in its safe state, and shall act on it irrespective of the state of any running program. | derived; BM §2 | TBD |
| SRS-PRI-P13-006 | The Primary Board shall not require the Web Application link to be available in order to place a channel in its safe state. | derived from §4.14 | TBD |
| SRS-PRI-P13-007 | The Primary Board shall report entry into the safe state, and the reason for it, to the Web Application and to its event log. | derived | TBD |
| SRS-PRI-P13-008 | The Primary Board shall record `<TBD-08>` (the emergency-stop input source, its electrical behaviour, and the required safe-state output configuration in terms of setpoint and relay positions) — see Open Issue #8. | Unsourced | TBD |

### 4.14 P14 — Communication loss handling

**Coverage:** ◐ Partial. Both link-loss cases are evidenced; per-node degradation policy
for 8 nodes is unsourced — Open Issues **#14**, **#24**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P14-001 | The Primary Board shall continue to execute running programs when the Web Application link is lost. | CODE-P `WEBAPP_RECONNECT_TIMEOUT_MS`; PSPE §5.2 | TBD |
| SRS-PRI-P14-002 | The Primary Board shall buffer telemetry and registration data that cannot be delivered while the Web Application link is lost. | HW *Digital Controller* #67 | TBD |
| SRS-PRI-P14-003 | The Primary Board shall attempt to re-establish the Web Application link continuously while it is lost. | CODE-P | TBD |
| SRS-PRI-P14-004 | The Primary Board shall deliver buffered data to the Web Application after the link is re-established. | HW *Digital Controller* #67; §4.16 | TBD |
| SRS-PRI-P14-005 | The Primary Board shall take the configured action for prolonged loss of the Web Application link when that loss exceeds a configurable duration, default **300 s**. | CODE-P `WEBAPP_RECONNECT_TIMEOUT_MS` = 300 000 ms | TBD |
| SRS-PRI-P14-006 | The Primary Board shall report the duration of the most recent Web Application link outage after the link is re-established. | derived | TBD |
| SRS-PRI-P14-007 | The Primary Board shall detect the loss of each Secondary Board independently, within the configured CAN timeout. | HW Config #9 | TBD |
| SRS-PRI-P14-008 | The Primary Board shall raise a fault against the affected channel when a Secondary Board is lost. | HW *Error code* `0x0D` | TBD |
| SRS-PRI-P14-009 | The Primary Board shall suspend the execution of the program on a channel whose Secondary Board is lost. | NEW — ME | TBD |
| SRS-PRI-P14-010 | The Primary Board shall continue to execute programs on all channels whose Secondary Boards remain present when one Secondary Board is lost. | NEW — ME | TBD |
| SRS-PRI-P14-011 | The Primary Board shall attempt to re-establish communication with a lost Secondary Board continuously while that board remains enumerated. | derived | TBD |
| SRS-PRI-P14-012 | The Primary Board shall verify a recovered Secondary Board's identity, software version and configuration before resuming any program on that channel. | derived from SRS-PRI-P3-004 | TBD |
| SRS-PRI-P14-013 | The Primary Board shall report the loss and the recovery of every Secondary Board to the Web Application and to its event log. | derived | TBD |
| SRS-PRI-P14-014 | The Primary Board shall release any synchronisation barrier that a lost Secondary Board's channel cannot reach, and shall report the release. | SRS-PRI-P7-104 | TBD |
| SRS-PRI-P14-015 | The Primary Board shall record `<TBD-39>` (whether a suspended program resumes automatically on recovery of its Secondary Board or requires operator confirmation, and the action required for prolonged loss of the Web Application link) — see Open Issues #14, #12. | Unsourced | TBD |

### 4.15 P15 — Data logging and storage

**Coverage:** ◐ Partial. What is logged and at what rate is well sourced from BM;
retention, capacity and exhaustion policy are unsourced — Open Issue **#11**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P15-001 | The Primary Board shall record registration data for every running channel in accordance with the active registration format and triggers. | BM §12.4.1, §12.4.6 | TBD |
| SRS-PRI-P15-002 | The Primary Board shall store registration data in non-volatile storage. | HW *Digital Controller* #67 | TBD |
| SRS-PRI-P15-003 | The Primary Board shall associate every registration record with its session identity, channel identity, step number, active operator and timestamp. | WAD/ICD §6.2 | TBD |
| SRS-PRI-P15-004 | The Primary Board shall record step-change registrations marking the beginning and the end of every step, subject to the active `RLevel`. | BM §12.4.1 | TBD |
| SRS-PRI-P15-005 | The Primary Board shall record every user message raised by an `ERR` or `MSG` operator into the registration data of the affected channel. | BM §12.4.8.5, §12.4.8.6 | TBD |
| SRS-PRI-P15-006 | The Primary Board shall record every fault into the registration data of the affected channel. | WAD/ICD §6.3 | TBD |
| SRS-PRI-P15-007 | The Primary Board shall record registration data into the test section currently selected for that channel. | BM §12.4.8.12 | TBD |
| SRS-PRI-P15-008 | The Primary Board shall support multiple test sections within a single session. | BM §12.4.8.12 | TBD |
| SRS-PRI-P15-009 | The Primary Board shall retain stored registration data across a restart. | HW *Digital Controller* #65 | TBD |
| SRS-PRI-P15-010 | The Primary Board shall monitor the free capacity of its registration data storage continuously. | derived; Open Issue #11 | TBD |
| SRS-PRI-P15-011 | The Primary Board shall report a warning to the Web Application when free storage capacity falls below a configurable threshold. | derived; Open Issue #11 | TBD |
| SRS-PRI-P15-012 | The Primary Board shall refuse to start a program when free storage capacity is insufficient for that program, and shall report the refusal. | derived; Open Issue #11 | TBD |
| SRS-PRI-P15-013 | The Primary Board shall take the configured action when registration data storage is exhausted while a program is running, and shall report the condition. | Open Issue #11 | TBD |
| SRS-PRI-P15-014 | The Primary Board shall not corrupt previously stored registration data when storage is exhausted. | derived | TBD |
| SRS-PRI-P15-015 | The Primary Board shall record `<TBD-40>` (storage medium, capacity, retention period, and the required behaviour on exhaustion: refuse to start, stop running programs, overwrite oldest, or alarm only) — see Open Issues #3, #11. | Unsourced | TBD |

### 4.16 P16 — Data upload and retrieval by the Web Application

**Coverage:** ◐ Partial. Retrieval is evidenced; buffer depth and reconciliation protocol
are unsourced — Open Issues **#11**, **#12**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P16-001 | The Primary Board shall deliver stored registration data to the Web Application on request. | WAD/ICD §6 | TBD |
| SRS-PRI-P16-002 | The Primary Board shall permit the Web Application to request registration data by session, by channel and by time range. | derived from WAD/DBD | TBD |
| SRS-PRI-P16-003 | The Primary Board shall deliver registration data in the order in which it was recorded. | derived | TBD |
| SRS-PRI-P16-004 | The Primary Board shall indicate to the Web Application which stored registration data has not yet been acknowledged as received. | derived; Open Issue #12 | TBD |
| SRS-PRI-P16-005 | The Primary Board shall retain registration data until the Web Application has acknowledged its receipt or the configured retention policy releases it. | Open Issue #11 | TBD |
| SRS-PRI-P16-006 | The Primary Board shall deliver buffered registration data accumulated during a Web Application link outage without duplicating records already delivered. | HW *Digital Controller* #67 | TBD |
| SRS-PRI-P16-007 | The Primary Board shall deliver buffered data without interrupting the delivery of live telemetry. | derived | TBD |
| SRS-PRI-P16-008 | The Primary Board shall report the volume of buffered data awaiting delivery to the Web Application. | derived | TBD |
| SRS-PRI-P16-009 | The Primary Board shall make its event log available to the Web Application on request. | derived from §4.22 | TBD |
| SRS-PRI-P16-010 | The Primary Board shall record `<TBD-41>` (buffering duration and depth to be sustained during a Web Application link outage, and the reconciliation protocol on reconnection) — see Open Issue #12. | Unsourced | TBD |

### 4.17 P17 — Time synchronization and timestamping

**Coverage:** ● Strong. Board-level RTC backup and network time availability are open —
Open Issue **#16**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P17-001 | The Primary Board shall maintain a wall-clock time. | LSRS SW_REQ_19–23; SOC (SNVS RTC) | TBD |
| SRS-PRI-P17-002 | The Primary Board shall accept a time synchronisation from the Web Application and shall set its wall-clock time from it. | CODE-P (sync-time); WAD/ICD §3.5 `0x06` | TBD |
| SRS-PRI-P17-003 | The Primary Board shall distribute time synchronisation to every present Secondary Board. | CODE-S (sync-time) | TBD |
| SRS-PRI-P17-004 | The Primary Board shall timestamp every registration record with its wall-clock time. | BM §12.4.1; WAD/ICD §6 | TBD |
| SRS-PRI-P17-005 | The Primary Board shall timestamp every event-log entry with its wall-clock time. | derived from §4.22 | TBD |
| SRS-PRI-P17-006 | The Primary Board shall maintain a monotonic time base for step timing, limit evaluation and registration triggering that is unaffected by a change of wall-clock time. | derived; BM §12.4.4 | TBD |
| SRS-PRI-P17-007 | The Primary Board shall not alter the elapsed step time or elapsed program time of a running program when its wall-clock time is set. | derived | TBD |
| SRS-PRI-P17-008 | The Primary Board shall record a time discontinuity in its event log when its wall-clock time is set by more than a configurable amount. | derived | TBD |
| SRS-PRI-P17-009 | The Primary Board shall report that its wall-clock time is unsynchronised when no time synchronisation has been received since the most recent restart and the real-time clock does not hold a plausible time. | SRS-PRI-P1-007 | TBD |
| SRS-PRI-P17-010 | The Primary Board shall record `<TBD-16>` (whether a battery-backed real-time clock is fitted and whether network time is available) — see Open Issue #16. | Unsourced | TBD |

### 4.18 P18 — Configuration management

**Coverage:** ● Strong. Per-channel scaling to 8 channels and restore-to-defaults are
open — Open Issue **#22**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P18-001 | The Primary Board shall hold a system configuration set and a separate configuration set for each of the 8 channels. | HW *Configuration*; NEW — ME | TBD |
| SRS-PRI-P18-002 | The Primary Board shall support at least 200 configuration parameters. | HW *Digital Controller* #12 | TBD |
| SRS-PRI-P18-003 | The Primary Board shall accept configuration parameter values from the Web Application. | HW *Digital Controller* #12; WAD/ICD §3.5 | TBD |
| SRS-PRI-P18-004 | The Primary Board shall relay configuration parameters destined for a Secondary Board to that board. | HW *Digital Controller* #12 ("Host PC shall send data to Main and it will be redirected to DSP controller") | TBD |
| SRS-PRI-P18-005 | The Primary Board shall validate every configuration value against its permitted range before applying it, and shall reject a value outside that range with a reason. | derived | TBD |
| SRS-PRI-P18-006 | The Primary Board shall hold, for each channel, the type number, bootloader software version, application software version and hardware version. | HW Config #1–4 | TBD |
| SRS-PRI-P18-007 | The Primary Board shall hold, for each channel, the maximum charge current, maximum discharge current, maximum voltage and minimum voltage. | HW Config #5–8 | TBD |
| SRS-PRI-P18-008 | The Primary Board shall hold, for each channel, the communication timeout, default **100 ms**. | HW Config #9 | TBD |
| SRS-PRI-P18-009 | The Primary Board shall hold, for each channel, the current setpoint tolerance band for error declaration, default **0.1 %** of full-scale current. | HW Config #10 | TBD |
| SRS-PRI-P18-010 | The Primary Board shall hold, for each channel, the current settling timeout, default **1000 ms**. | HW Config #11 | TBD |
| SRS-PRI-P18-011 | The Primary Board shall hold, for each channel, the current ramp time, default **10 ms**, and the voltage ramp time, default **10 ms**. | HW Config #12, #13 | TBD |
| SRS-PRI-P18-012 | The Primary Board shall hold, for each channel, the charge relay on time, off time and wait time, defaults **100 ms**, **100 ms** and **50 ms**. | HW Config #16–18 | TBD |
| SRS-PRI-P18-013 | The Primary Board shall hold, for each channel, the discharge relay on time, off time and wait time, defaults **100 ms**, **100 ms** and **50 ms**. | HW Config #19–21 | TBD |
| SRS-PRI-P18-014 | The Primary Board shall hold, for each channel, the lower limit, upper limit, warning limit and hysteresis of each of five health-check channels. | HW Config #23–42 | TBD |
| SRS-PRI-P18-015 | The Primary Board shall hold, for each channel, the calibration dates of charge current, charge voltage, discharge current and discharge voltage. | HW Config #44–47 | TBD |
| SRS-PRI-P18-016 | The Primary Board shall hold, for each channel, the software configuration selector permitting up to 255 selectable options. | HW Config #15 | TBD |
| SRS-PRI-P18-017 | The Primary Board shall hold, for each channel, the transistor-bank type selection. | HW §5 remark ("transistor selection option is present in factory settings software") | TBD |
| SRS-PRI-P18-018 | The Primary Board shall store all configuration in non-volatile storage and shall retain it across a restart. | HW *Digital Controller* #12; CODE-P `eeprom.c` | TBD |
| SRS-PRI-P18-019 | The Primary Board shall protect stored configuration with an integrity check. | SRS-PRI-P1-003 | TBD |
| SRS-PRI-P18-020 | The Primary Board shall hold a factory default value for every configuration parameter. | HW *Configuration* (Default Value column) | TBD |
| SRS-PRI-P18-021 | The Primary Board shall restore all configuration parameters, or an identified subset, to their factory default values on request. | Open Issue #22 | TBD |
| SRS-PRI-P18-022 | The Primary Board shall deliver the current value of any configuration parameter to the Web Application on request. | WAD/ICD §3.5 `0x02`–`0x04` | TBD |
| SRS-PRI-P18-023 | The Primary Board shall record every configuration change in its event log, together with the previous value and the new value. | derived from §4.22 | TBD |
| SRS-PRI-P18-024 | The Primary Board shall reject a configuration change to a channel that is currently executing a program where that change would affect the running program, and shall report the rejection. | derived | TBD |
| SRS-PRI-P18-025 | The Primary Board shall record `<TBD-20>` (the health-check channel definitions of Open Issue #20 and the wear and integrity policy for non-volatile configuration storage of Open Issue #22). | Unsourced | TBD |

### 4.19 P19 — User / operator interaction via the Web Application

**Coverage:** ● Strong. Which Web Application is open — Open Issue **#2**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P19-001 | The Primary Board shall accept a start command for an identified channel. | BM §2.4; WAD/ICD §3.7 `0x01` | TBD |
| SRS-PRI-P19-002 | The Primary Board shall accept a stop command for an identified channel. | BM §2.5; WAD/ICD §3.7 `0x02` | TBD |
| SRS-PRI-P19-003 | The Primary Board shall accept a pause command for an identified channel. | BM §2.6; WAD/ICD §3.7 `0x03` | TBD |
| SRS-PRI-P19-004 | The Primary Board shall accept a continue command for an identified channel. | BM §2.7; WAD/ICD §3.7 `0x04` | TBD |
| SRS-PRI-P19-005 | The Primary Board shall accept an interrupt command for an identified channel. | BM §2.8 | TBD |
| SRS-PRI-P19-006 | The Primary Board shall accept a reset command. | BM §2.9; WAD/ICD §3.7 `0x06` | TBD |
| SRS-PRI-P19-007 | The Primary Board shall accept any of the commands of SRS-PRI-P19-001 to SRS-PRI-P19-005 addressed to several channels in a single request, and shall apply it to each addressed channel. | Brief; WAD/ICD §7.3 | TBD |
| SRS-PRI-P19-008 | The Primary Board shall accept a session identity and a session name with a start command, and shall associate them with the resulting session. | BM §2.4; WAD/ICD §6.2 | TBD |
| SRS-PRI-P19-009 | The Primary Board shall accept a battery identity with a start command and shall associate it with the resulting session. | WAD/ICD §7.3 | TBD |
| SRS-PRI-P19-010 | The Primary Board shall accept a start-from-step instruction with a start command. | BM §2; CODE-P | TBD |
| SRS-PRI-P19-011 | The Primary Board shall accept a deferred start time with a start command. | BM §2.10 (Deferred Start) | TBD |
| SRS-PRI-P19-012 | The Primary Board shall accept a fault acknowledgement for an identified channel or for the board. | Open Issue #17 | TBD |
| SRS-PRI-P19-013 | The Primary Board shall reject a command addressed to a channel that is not present, and shall report the rejection. | SRS-PRI-P3-006 | TBD |
| SRS-PRI-P19-014 | The Primary Board shall report the outcome of every command per addressed channel, so that a partial success within a multi-channel request is distinguishable. | WAD/ICD §7.3 | TBD |
| SRS-PRI-P19-015 | The Primary Board shall deliver a live view of all channels to the Web Application comprising the data of SRS-PRI-P10-011. | BM §6 Status View; WAD/ICD §5.2 | TBD |
| SRS-PRI-P19-016 | The Primary Board shall deliver, for a channel executing a program, the identity and version of the running program and the step currently executing. | BM §6; WAD/ICD §5.2 | TBD |
| SRS-PRI-P19-017 | The Primary Board shall accept from the Web Application the message table additions and modifications of SRS-PRI-P7-066. | BM §12.4.8.5 | TBD |

### 4.20 P20 — Firmware update

**Coverage:** ○ Contested. The project brief mandates firmware update including Secondary
update over CAN; the hardware specification's bootloader row carries the client remark
*"This is not required as discussed earlier."* The brief governs ME scope, so
requirements are specified — but conflict **C-06** and Open Issue **#9** remain open, and
integrity, rollback and authorisation values are `<TBD>`.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P20-001 | The Primary Board shall accept a firmware image for itself from the Web Application. | Brief P20 | TBD |
| SRS-PRI-P20-002 | The Primary Board shall verify the integrity of a received firmware image before installing it. | Brief P20; SOC (HAB, CAAM) | TBD |
| SRS-PRI-P20-003 | The Primary Board shall verify the authenticity of a received firmware image before installing it. | SOC (HAB); Open Issue #10 | TBD |
| SRS-PRI-P20-004 | The Primary Board shall reject a firmware image that fails its integrity or authenticity check, and shall report the rejection without installing it. | derived | TBD |
| SRS-PRI-P20-005 | The Primary Board shall refuse to install its own firmware while any channel is executing a program. | derived | TBD |
| SRS-PRI-P20-006 | The Primary Board shall retain the ability to boot a working firmware image if an update fails. | Brief P20; Open Issue #9 | TBD |
| SRS-PRI-P20-007 | The Primary Board shall report the outcome of every firmware update attempt. | derived | TBD |
| SRS-PRI-P20-008 | The Primary Board shall accept a firmware image destined for an identified Secondary Board. | Brief P20 | TBD |
| SRS-PRI-P20-009 | The Primary Board shall transfer a Secondary Board firmware image to the addressed Secondary Board over the CAN interface. | Brief P20 | TBD |
| SRS-PRI-P20-010 | The Primary Board shall refuse to update a Secondary Board that is executing a program. | derived | TBD |
| SRS-PRI-P20-011 | The Primary Board shall place a channel in its safe state before updating that channel's Secondary Board. | derived | TBD |
| SRS-PRI-P20-012 | The Primary Board shall verify that a Secondary Board firmware update completed successfully, and shall report the outcome. | HW *Error code* `0x15` (verify programming failed) | TBD |
| SRS-PRI-P20-013 | The Primary Board shall detect and report a Secondary Board firmware update failure, distinguishing at least the conditions flashing not possible, flash erase failed and programming verification failed. | HW *Error code* `0x11`, `0x14`, `0x15` | TBD |
| SRS-PRI-P20-014 | The Primary Board shall re-verify a Secondary Board's identity and software version after an update of that board. | SRS-PRI-P3-004 | TBD |
| SRS-PRI-P20-015 | The Primary Board shall update Secondary Boards one at a time unless a source establishes otherwise. | derived | TBD |
| SRS-PRI-P20-016 | The Primary Board shall record every firmware update attempt and its outcome in its event log. | derived from §4.22 | TBD |
| SRS-PRI-P20-017 | The Primary Board shall record `<TBD-09>` (whether Secondary firmware update over CAN is required, whether the Secondary bootloader is field-updatable, whether rollback is required, and the required integrity and authenticity mechanism) — see Open Issue #9 and conflict **C-06**. | Conflict C-06 | TBD |

### 4.21 P21 — Calibration data management and distribution

**Coverage:** ● Strong. Per-range calibration scope is open — Open Issue **#15**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P21-001 | The Primary Board shall hold calibration data for each of the 8 channels. | HW *Calibration*; NEW — ME | TBD |
| SRS-PRI-P21-002 | The Primary Board shall accept calibration data from the Web Application and relay it to the addressed Secondary Board. | HW *Digital Controller* #12 | TBD |
| SRS-PRI-P21-003 | The Primary Board shall hold, per channel, the charge current calibration gain and offset and the discharge current calibration gain and offset. | HW *Calibration* #1, #2, #5, #6 | TBD |
| SRS-PRI-P21-004 | The Primary Board shall hold, per channel, the charge voltage calibration gain and offset and the discharge voltage calibration gain and offset. | HW *Calibration* #3, #4, #7, #8 | TBD |
| SRS-PRI-P21-005 | The Primary Board shall hold, per channel, the current regulator proportional, integral and derivative parameters. | HW *Calibration* #10–12 | TBD |
| SRS-PRI-P21-006 | The Primary Board shall hold, per channel, the voltage regulator proportional, integral and derivative parameters. | HW *Calibration* #13–15 | TBD |
| SRS-PRI-P21-007 | The Primary Board shall hold, per channel, the external temperature sensor calibration gain and offset. | HW *Calibration* #17, #18 | TBD |
| SRS-PRI-P21-008 | The Primary Board shall hold, per channel, the internal heatsink temperature calibration gain and offset. | HW *Calibration* #20, #21 | TBD |
| SRS-PRI-P21-009 | The Primary Board shall hold, per channel, the charge-mode transistor-bank voltage feedback calibration gain and offset. | HW *Calibration* #22, #23 | TBD |
| SRS-PRI-P21-010 | The Primary Board shall hold, per channel, the discharge-mode transistor-bank voltage feedback calibration gain and offset. | HW *Calibration* #24, #25 | TBD |
| SRS-PRI-P21-011 | The Primary Board shall support a guided calibration procedure in which the Web Application commands a channel to a calibration point and reads back the measured value and the raw converter count. | WAD/ICD §3.8, §5.3; CODE-P `CalibrationQueryID_t` | TBD |
| SRS-PRI-P21-012 | The Primary Board shall deliver live calibration data comprising the measured current, the raw current converter count, the measured voltage and the raw voltage converter count during a calibration procedure. | WAD/ICD §5.3 | TBD |
| SRS-PRI-P21-013 | The Primary Board shall support the cancellation of a calibration procedure, restoring the previously stored calibration data. | WAD/ICD §3.8 `0x0F` | TBD |
| SRS-PRI-P21-014 | The Primary Board shall support a verification pass that reports the deviation of measured values from applied references without altering stored calibration data. | WAD/ICD §3.8 `0x11`–`0x13` | TBD |
| SRS-PRI-P21-015 | The Primary Board shall deliver the previously stored calibration data of a channel on request. | WAD/ICD §3.8 `0x14` | TBD |
| SRS-PRI-P21-016 | The Primary Board shall record the date of each calibration operation against the affected quantity. | HW Config #44–47 | TBD |
| SRS-PRI-P21-017 | The Primary Board shall store calibration data in non-volatile storage, protected by an integrity check, and shall retain it across a restart. | HW *Calibration*; SRS-PRI-P1-005 | TBD |
| SRS-PRI-P21-018 | The Primary Board shall refuse to start a calibration procedure on a channel that is executing a program. | derived | TBD |
| SRS-PRI-P21-019 | The Primary Board shall place a channel in a defined calibration state for the duration of a calibration procedure on that channel, and shall report that state. | derived; Open Issue #13 | TBD |
| SRS-PRI-P21-020 | The Primary Board shall record every calibration change in its event log. | derived from §4.22 | TBD |
| SRS-PRI-P21-021 | The Primary Board shall record `<TBD-42>` (whether per-range calibration comprising a full range and four sub-ranges is required for ME) — see Open Issue #15. | Unsourced | TBD |

### 4.22 P22 — Diagnostics, health monitoring, event log, audit trail

**Coverage:** ◐ Partial. Health-check limits exist but the channels are undefined —
Open Issue **#20**; a Primary-resident event log is unsourced — Open Issue **#17**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P22-001 | The Primary Board shall maintain an event log. | Open Issue #17; BM §1.7 Event View | TBD |
| SRS-PRI-P22-002 | The Primary Board shall record in the event log every state change, fault detection and clearance, command received, configuration change, calibration change, firmware update, Secondary Board presence change, restart and time discontinuity. | derived from §§4.2, 4.12, 4.14, 4.17–4.21 | TBD |
| SRS-PRI-P22-003 | The Primary Board shall record each event-log entry with a timestamp, a severity, an originating subsystem and, where applicable, the affected channel. | derived | TBD |
| SRS-PRI-P22-004 | The Primary Board shall store the event log in non-volatile storage and shall retain it across a restart. | derived | TBD |
| SRS-PRI-P22-005 | The Primary Board shall record in the event log the identity of the originator of every command that changes system state. | WAD (AuditLogs); Open Issue #10 | TBD |
| SRS-PRI-P22-006 | The Primary Board shall not permit the deletion or modification of an existing event-log entry. | derived from audit-trail intent | TBD |
| SRS-PRI-P22-007 | The Primary Board shall continue to record events when the event log is full, discarding the oldest entries, and shall report that discarding has begun. | derived; Open Issue #11 | TBD |
| SRS-PRI-P22-008 | The Primary Board shall monitor its own processor load, memory utilisation and storage utilisation continuously. | derived; Open Issue #20 | TBD |
| SRS-PRI-P22-009 | The Primary Board shall monitor its own die temperature and shall raise a fault when it exceeds a configured trip point. | SOC (TMU, programmable trip points) | TBD |
| SRS-PRI-P22-010 | The Primary Board shall detect and record a memory error reported by an error-correcting memory subsystem. | SOC (DDR inline ECC, L2 ECC, OCRAM ECC) | TBD |
| SRS-PRI-P22-011 | The Primary Board shall evaluate each of the five health-check channels of each Secondary Board against its configured lower limit, upper limit, warning limit and hysteresis. | HW Config #23–42 | TBD |
| SRS-PRI-P22-012 | The Primary Board shall raise a warning when a health-check channel crosses its warning limit, and a trip when it crosses its lower or upper limit. | HW Config #23–42; SRS-PRI-P12-008 | TBD |
| SRS-PRI-P22-013 | The Primary Board shall apply the configured hysteresis when determining that a health-check channel has returned within limits. | HW Config #23–42 | TBD |
| SRS-PRI-P22-014 | The Primary Board shall deliver a health summary of itself and of every channel to the Web Application on request. | derived | TBD |
| SRS-PRI-P22-015 | The Primary Board shall provide a diagnostic mode in which raw converter counts of a channel can be read without altering calibration data. | HW §4 remarks ("we need adc count display to check this feature") | TBD |
| SRS-PRI-P22-016 | The Primary Board shall record `<TBD-20>` (the physical quantity assigned to each of health-check channels 1 to 5 and the action required on a warning and on a limit breach) — see Open Issue #20. | Unsourced | TBD |

### 4.23 P23 — Power-fail detection and program recovery after restart

**Coverage:** ● Strong for mechanism, but the hardware specification lists all three
power-fail behaviours as *pending* — Open Issue **#23**.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P23-001 | The Primary Board shall detect an impending loss of supply. | HW *Digital Controller* #10, #63; CODE-P `power_fail.c` | TBD |
| SRS-PRI-P23-002 | The Primary Board shall command every channel to its safe state on detecting an impending loss of supply. | HW #63; CODE-S | TBD |
| SRS-PRI-P23-003 | The Primary Board shall persist, on detecting an impending loss of supply, sufficient state to resume each running program, comprising at least the program identity and version, the current step, the elapsed step time, the elapsed program time, all cycle counters, all timers, all variables and all counters. | HW #64; CODE-S `progBackUp_t` | TBD |
| SRS-PRI-P23-004 | The Primary Board shall persist, on detecting an impending loss of supply, all registration data not yet written to non-volatile storage. | HW #65 | TBD |
| SRS-PRI-P23-005 | The Primary Board shall complete the persistence actions of SRS-PRI-P23-003 and SRS-PRI-P23-004 within the available supply hold-up time. | HW #63; derived | TBD |
| SRS-PRI-P23-006 | The Primary Board shall record a power-fail event in its event log. | HW *Error code* `0x12`; derived | TBD |
| SRS-PRI-P23-007 | The Primary Board shall detect, after a restart, that the preceding shutdown was caused by loss of supply. | SRS-PRI-P1-011 | TBD |
| SRS-PRI-P23-008 | The Primary Board shall verify the integrity of persisted program state before using it to resume a program. | derived | TBD |
| SRS-PRI-P23-009 | The Primary Board shall verify that each channel's Secondary Board is present, identified and consistent with the persisted state before resuming a program on that channel. | SRS-PRI-P14-012 | TBD |
| SRS-PRI-P23-010 | The Primary Board shall restore the persisted counters, cycle counters, timers and variables of a resumed program. | HW #64; BM §12.5.3 | TBD |
| SRS-PRI-P23-011 | The Primary Board shall report to the Web Application, after a restart following loss of supply, which programs are resumable and which are not. | derived | TBD |
| SRS-PRI-P23-012 | The Primary Board shall record the resumption or the abandonment of every program after a restart in its event log. | derived | TBD |
| SRS-PRI-P23-013 | The Primary Board shall detect unstable input supply and shall raise a fault. | HW *Error code* `0x13` | TBD |
| SRS-PRI-P23-014 | The Primary Board shall record `<TBD-23a>` (the supply hold-up time available for persistence, and whether a program resumes automatically after restart or requires operator confirmation) — see Open Issue #23. | Unsourced | TBD |

### 4.24 P24 — Security and access control

**Coverage:** ◐ Partial. The SoC's security capability is now sourced from **SOC**; the
*requirement* to use it is not — Open Issue **#10**. All access-control evidence is
Web-Application-side.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P24-001 | The Primary Board shall accept commands that change system state only from an authenticated peer. | Open Issue #10 | TBD |
| SRS-PRI-P24-002 | The Primary Board shall record the authenticated identity of the originator of every state-changing command in its event log. | SRS-PRI-P22-005 | TBD |
| SRS-PRI-P24-003 | The Primary Board shall reject a state-changing command from an unauthenticated peer, and shall record the rejection. | derived | TBD |
| SRS-PRI-P24-004 | The Primary Board shall distinguish the privilege required for operational commands, for configuration changes, for calibration changes and for firmware update, and shall reject a command whose originator lacks the required privilege. | BM §7 (departments, groups, rights, users); WAD (roles) | TBD |
| SRS-PRI-P24-005 | The Primary Board shall protect the confidentiality and integrity of data in transit on the Web Application interface. | Open Issue #10; SOC (CAAM) | TBD |
| SRS-PRI-P24-006 | The Primary Board shall verify the authenticity of its own firmware at every boot. | SOC (HAB); SRS-PRI-P20-003 | TBD |
| SRS-PRI-P24-007 | The Primary Board shall protect stored credentials and cryptographic keys against retrieval over any external interface. | SOC (OCOTP, CAAM secure memory, SNVS) | TBD |
| SRS-PRI-P24-008 | The Primary Board shall not expose a debug interface that permits the alteration of firmware or configuration in a production configuration. | SOC (Secure JTAG) | TBD |
| SRS-PRI-P24-009 | The Primary Board shall isolate the Modbus interface from the Web Application interface such that traffic on one cannot alter the security state of the other. | derived; §4.25 | TBD |
| SRS-PRI-P24-010 | The Primary Board shall record `<TBD-10>` (whether the Primary authenticates the Web Application and enforces roles itself or relies on a trusted isolated network; whether secure boot must be enabled in production; and which security standard, if any, applies) — see Open Issue #10. | Unsourced | TBD |

### 4.25 P25 — Modbus module

**Coverage:** ◐ Directed. The architect's instruction of 2026-07-27 places the Modbus
module on the Primary Board. Functional evidence is the legacy COM Controller
implementation; the ME register model, role and physical layer are unsourced.

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-P25-001 | The Primary Board shall host the Modbus module. | Architect instruction, 2026-07-27 (Open Issue #29) | TBD |
| SRS-PRI-P25-002 | The Primary Board shall implement the Modbus protocol over a serial physical layer. | CODE-C `ModSlave.c`, `RS485_Slave.c`; LSRS | TBD |
| SRS-PRI-P25-003 | The Primary Board shall expose, through the Modbus module, the measured values of every channel. | CODE-C `comApp.c`; LSRS | TBD |
| SRS-PRI-P25-004 | The Primary Board shall expose, through the Modbus module, the operating state and active fault set of every channel. | CODE-C; LSRS | TBD |
| SRS-PRI-P25-005 | The Primary Board shall expose, through the Modbus module, the board-level operating state. | derived | TBD |
| SRS-PRI-P25-006 | The Primary Board shall hold a Modbus address, a baud rate, a parity setting and a stop-bit setting as configuration parameters. | LSRS (RS485 configuration); HW *Configuration* | TBD |
| SRS-PRI-P25-007 | The Primary Board shall validate every Modbus request and shall return the defined Modbus exception response for an unsupported function code, an illegal data address or an illegal data value. | Modbus application protocol; derived | TBD |
| SRS-PRI-P25-008 | The Primary Board shall not allow a Modbus request to place any channel in an unsafe condition. | derived from §4.13 | TBD |
| SRS-PRI-P25-009 | The Primary Board shall not allow a failure or a timeout on the Modbus interface to affect program execution on any channel. | derived from SRS-PRI-CM-007 | TBD |
| SRS-PRI-P25-010 | The Primary Board shall record Modbus communication faults in its event log. | derived | TBD |
| SRS-PRI-P25-011 | The Primary Board shall record `<TBD-43>` (whether the Primary acts as Modbus server, Modbus client or both; the physical layer and its electrical standard; the complete register map; and whether Modbus TCP is additionally required). | Unsourced | TBD |
| SRS-PRI-P25-012 | The Primary Board shall record `<TBD-44>` (whether write access through the Modbus module is permitted, and if so to which items). | Unsourced | TBD |

---

## 5. Non-Functional Requirements

### 5.1 Performance and timing

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-001 | The Primary Board shall evaluate the active limits of every running channel at least once per **100 ms**. | BM §12.4.4 | TBD |
| SRS-PRI-NF-002 | The Primary Board shall support a registration resolution of **0.1 s** on every running channel. | BM §12.4.6 | TBD |
| SRS-PRI-NF-003 | The Primary Board shall support a minimum program step duration of **0.1 s**. | BM §12.4.8.9 | TBD |
| SRS-PRI-NF-004 | The Primary Board shall meet the timing of SRS-PRI-NF-001 to SRS-PRI-NF-003 with all 8 channels running concurrently. | NEW — ME | TBD |
| SRS-PRI-NF-005 | The Primary Board shall detect the loss of a Secondary Board within the configured communication timeout, default **100 ms**. | HW Config #9 | TBD |
| SRS-PRI-NF-006 | The Primary Board shall command a channel to its safe state within `<TBD-45>` of detecting a trip condition on that channel. | Open Issue #8 | TBD |
| SRS-PRI-NF-007 | The Primary Board shall complete initialization and reach the Ready state within `<TBD-46>` of the application of supply. | Unsourced | TBD |
| SRS-PRI-NF-008 | The Primary Board shall not lose any registration record while sustaining the configured registration rate on all 8 channels. | SRS-PRI-P10-016 | TBD |
| SRS-PRI-NF-009 | The Primary Board shall record `<TBD-05>` (the required per-channel registration interval and the acceptable end-to-end telemetry latency) — see conflict **C-05**. | Conflict C-05 | TBD |

### 5.2 Safety

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-010 | The Primary Board shall treat the safe state of a channel as its default, entering it whenever the correct condition of that channel cannot be established. | BM §12.4 safety notes | TBD |
| SRS-PRI-NF-011 | The Primary Board shall not rely on program limits as the sole protection of a battery or of the equipment. | BM §12.4 ("the limit criteria in a program should not be the only security measures in a system") | TBD |
| SRS-PRI-NF-012 | The Primary Board shall retain the ability to place a channel in its safe state when the Web Application link is unavailable. | SRS-PRI-P13-006 | TBD |
| SRS-PRI-NF-013 | The Primary Board shall report, whenever a program stops rather than terminating the queue, that subsequent queued assignments will still execute. | BM §12.4.5 ("the STO action does not terminate the test process… this may cause an exhaustive discharge") | TBD |
| SRS-PRI-NF-014 | The Primary Board shall not resume energy transfer on a channel after a fault without an explicit command. | derived | TBD |
| SRS-PRI-NF-015 | The Primary Board shall record `<TBD-47>` (the safety integrity requirement applicable to the ME system, if any, and the resulting constraints on architecture and verification). | Unsourced | TBD |

### 5.3 Reliability and availability

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-016 | The Primary Board shall continue to control the remaining channels when any single Secondary Board fails. | SRS-PRI-P8-008 | TBD |
| SRS-PRI-NF-017 | The Primary Board shall recover to a defined state after an unexpected restart without operator intervention. | SRS-PRI-P1-001 | TBD |
| SRS-PRI-NF-018 | The Primary Board shall not lose persisted configuration, calibration, program or registration data as a result of an unexpected loss of supply. | §4.23 | TBD |
| SRS-PRI-NF-019 | The Primary Board shall detect its own failure to make progress and shall cause a reset. | HW *Digital Controller* #10; SOC (WDOG) | TBD |
| SRS-PRI-NF-020 | The Primary Board shall bound the memory used by every buffer and queue, and shall not fail as a result of a sustained input rate exceeding its processing rate. | C-10; derived | TBD |

### 5.4 Environmental and platform

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-021 | The Primary Board software shall operate correctly across the industrial junction-temperature range of the specified processor, −40 °C to +105 °C. | SOC (ordering table, `C` grade) | TBD |
| SRS-PRI-NF-022 | The Primary Board software shall operate on the NXP `MIMX8ML8CVNKZAB` with Linux on the Cortex-A53 cluster and an RTOS on the Cortex-M7. | Brief; architect instruction | Forced (SOC) |

### 5.5 Security

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-023 | The Primary Board shall fail closed, rejecting a state-changing command, when it cannot establish the authorisation of that command. | §4.24 | TBD |
| SRS-PRI-NF-024 | The Primary Board shall not log credentials, keys or other secrets. | Team engineering policy | TBD |
| SRS-PRI-NF-025 | The Primary Board shall not weaken the security state of the system as a result of a firmware update. | §4.20 | TBD |

### 5.6 Maintainability

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-026 | The Primary Board software shall be organised in layers separating Application, Interface, Basic Software and Communication concerns. | HW *Digital Controller* #9 | TBD |
| SRS-PRI-NF-027 | No source file of the Primary Board software shall exceed 2000 lines. | C-11 | TBD |
| SRS-PRI-NF-028 | The Primary Board software shall not allocate memory dynamically after initialization. | C-10 | TBD |
| SRS-PRI-NF-029 | The Primary Board shall report its own software version, and the version of every interface it implements, on request. | SRS-PRI-P1-014 | TBD |
| SRS-PRI-NF-030 | The Primary Board shall record `<TBD-26a>` (whether MISRA-C compliance is required, and for which modules) — see Open Issue #26. | Unsourced | TBD |

### 5.7 Diagnosability

| ID | Requirement | Source | Alloc |
|---|---|---|---|
| SRS-PRI-NF-031 | The Primary Board shall provide a diagnostic log obtainable without interrupting program execution. | §4.22 | TBD |
| SRS-PRI-NF-032 | The Primary Board shall provide sufficient information in its event log to reconstruct the sequence of events preceding any fault. | §4.22 | TBD |
| SRS-PRI-NF-033 | The Primary Board shall support the reading of raw converter counts for diagnostic purposes. | SRS-PRI-P22-015 | TBD |

---

## 6. Traceability Matrix

See the companion annex:
`01A_SRS_ME_Primary_Annex_Traceability_and_Core_Allocation_v0.1.md` §A1.

Every requirement in this document additionally carries its source inline in the
`Source` column of its table, so the annex provides the reverse mapping — from source
document and section to requirement — and the coverage roll-up.

---

## 7. Core Allocation — Open

See the companion annex:
`01A_SRS_ME_Primary_Annex_Traceability_and_Core_Allocation_v0.1.md` §A2.

The annex lists every requirement in this document with an empty allocation column, so
that the architect can resolve **D-01** in a single pass. (*This SoC has two different
kinds of CPU core on one chip — four general-purpose A53 cores and one real-time M7
core. Deciding which piece of software runs on which is what D-01 means.*) Exactly one requirement in
this document has a forced allocation: **SRS-PRI-NF-022**, forced by the specified part
number.

---

## 8. Open Issues / TBD Register

### 8.1 Open issues

Issues **#1**, **#4**, **#5**, **#27** and **#29** were resolved by the architect on
2026-07-27; their resolutions are incorporated above. The remainder stand.

| # | Issue | Requirements affected |
|---|---|---|
| ~~1~~ | ~~Is ME the Digatron ME circuit type?~~ **CLOSED** — ME is this project's name; BM is a functional reference, not a compatibility contract. | §1.3, SRS-PRI-P7-100 |
| **2** | Which Web Application is the peer — the existing Ador server evolved, Digatron BM4, or a new build? | §4.19, all of IF-A |
| **3** | No ME Primary Board hardware specification exists. Board-level unknowns: storage medium and capacity, DRAM size, battery-backed RTC, Ethernet ports wired, CAN transceivers fitted, indicators, digital I/O, power-fail circuit. | SRS-PRI-HW-004, -007, -008; P15; P17 |
| ~~4~~ | ~~Is the COM Controller retained?~~ **CLOSED** — no COM Controller in ME. | §2.1, §2.3, §4.25 |
| ~~5~~ | ~~Which operators are in Phase 1?~~ **CLOSED** — all BTS-600 operators are in scope, no phasing. | §4.7 |
| **6** | Are battery-parameter-relative values resolved by the Primary at runtime or pre-computed by the Web Application? | SRS-PRI-P5-019, -025 |
| **7** | Procedures (BM) or PRODUCER inline expansion (Web App) as the reuse model? | SRS-PRI-P7-036 to -040 |
| **8** | Emergency-stop input source and required safe-state output configuration. | §4.13, SRS-PRI-NF-006 |
| **9** | Confirm Secondary firmware update over CAN is required; bootloader field-updatability; rollback; integrity mechanism. | §4.20 |
| **10** | Primary-side security: authenticate the Web Application and enforce roles, or rely on a trusted isolated network? Secure boot in production? Applicable standard? | §4.24 |
| **11** | Data retention period and storage-exhaustion policy. | §4.15, §4.16 |
| **12** | Buffering duration and depth during Web App link loss; reconciliation on reconnect; orderly shutdown; startup self-test content. | SRS-PRI-P1-008, -016; §4.16 |
| **13** | Maintenance mode: entry, exit, permitted operations, effect on running programs. | SRS-PRI-P2-002, -015 |
| **14** | Secondary presence policy: identity acquisition, hot-plug, behaviour of the other seven on one node's loss, resume policy. | §4.3, §4.14 |
| **15** | Program assignment semantics across channels; retention of per-range calibration. | SRS-PRI-P6-014, SRS-PRI-P21-021 |
| **16** | Synchronised versus independent multi-channel execution; multi-channel start skew; battery-backed RTC; network time. | §4.8, §4.17 |
| **17** | Fault latch, acknowledge and clear semantics; escalation from channel to system fault; is a Primary-resident event log required? | §4.12, §4.22 |
| **18** | Telemetry rate budget across 8 nodes and acceptable end-to-end latency. | §4.10, §5.1 |
| **19** | Is program-controlled digital output in scope, and via which operators? | SRS-PRI-P7-115, -116; SRS-PRI-P9-012 |
| **20** | Health-check channels 1 to 5: physical quantity of each, and action on warning versus limit. | SRS-PRI-P18-014, SRS-PRI-P22-011 to -016 |
| **21** | The hardware specification's ten unresolved hardware queries. | §4.9, §4.22 |
| **22** | Secondary node identity source; actual digital I/O counts; NVM wear and integrity policy; restore-to-defaults behaviour. | SRS-PRI-P3-013, SRS-PRI-P18-021, -025 |
| **23** | Power-fail: hold-up time, what must be persisted, automatic versus confirmed resume. | §4.23 |
| **24** | Cross-channel system-level supervision and escalation policy. | §4.11 |
| **25** | Programs concurrently resident; survival across reboot; versioning and rollback. | SRS-PRI-P4-018 |
| **26** | MISRA-C applicability. | SRS-PRI-NF-030 |
| ~~27~~ | ~~Missing operator PDF.~~ **PARTIALLY CLOSED** — all eight operators are in ME scope (§4.7.7). Their detailed semantics remain unavailable; `<TBD-26>`…`<TBD-33>` stand and the document is still requested. | §4.7.7 |
| **28** | Does the Primary need analog input of its own? The SoC has no general-purpose ADC. | SRS-PRI-HW-006; §2.3 |
| ~~29~~ | ~~External CAN / Modbus scope.~~ **CLOSED for Modbus** — hosted by the Primary Board (§4.25). External CAN and DBC are **excluded from ME scope**; reopen if required. | §4.25, A-09 |
| **30** | Is deterministic Ethernet (TSN) or IEEE 1588 required for the Web App link or for time distribution? | §4.17; **D-05** |
| **31** | Must ME reproduce the BM registration formats and their byte layouts, or may it define its own? Now bears only on data-exchange convenience, since §1.3 removes the compatibility contract. | SRS-PRI-P10-013, -014 |

### 8.2 Conflict register

Conflicts are carried, not resolved. Full analysis is in the gate document §5.

| ID | Conflict | Bearing on this document |
|---|---|---|
| **C-04** | Charge↔discharge transition time: 10 ms (HW §6) vs 23 ms measured vs 500 ms switch-over delay (LSRS) | SRS-PRI-P18-011 to -013 |
| **C-05** | Registration interval: ≤500 µs vs 10 ms vs 1 ms vs 100 ms implemented vs BM's 0.1 s floor | SRS-PRI-P10-018, SRS-PRI-NF-009 |
| **C-06** | Secondary bootloader required (HW spec) vs "not required as discussed earlier" (client remark) vs required over CAN (brief) | §4.20 |
| **C-07** | Battery voltage: ≤100 V capability vs 80 V present rig vs 18 V default | SRS-PRI-P18-007 |
| **C-08** | Maximum current: ≤200 A capability vs 100 A default | SRS-PRI-P18-007 |
| **C-10** | CAN bit rate 250 kbps (Primary↔Secondary) vs 125 k–1 M (legacy external CAN) — different buses, not to be conflated | **D-04** |
| **C-12** | Auto-ranging: 4 ranges at 50/10/1/0.1 % of Imax (HW) vs gains 1/2/4/8 (LSRS, legacy code) vs BM ranges 1–4 with no in-step change and ~50 ms transit | SRS-PRI-P7-085 to -093 |
| **C-13** | Cycle nesting: BM's "up to 16 different interlaced cycles" vs single active loop implemented vs 16 cycles / depth 4 in progress | SRS-PRI-P5-014, -026; SRS-PRI-P7-032 |
| **C-14** | Four incompatible code spaces: HW error list 1…`0x17`; legacy 17-bit bitmask; BM ER1–ER10; BM user message numbers 1–6 (runtime-editable) | SRS-PRI-P12-001, -017 |
| **C-16** | External CAN ports: 3 on the deleted COM Controller vs at most 1 spare FlexCAN on the SoC | A-09; Open Issue #29 |
| **C-17** | RS485/Modbus: 2 dedicated ports on the deleted COM Controller vs 4 shared SoC UARTs needing external transceivers | §4.25 |
| **C-18** | Primary analog input inherited from LSRS vs no general-purpose ADC on the SoC | Open Issue #28 |

### 8.3 TBD register

| Tag | Value required | Raised by |
|---|---|---|
| `<TBD-03>` | Exact content of the minimum necessary control data | SRS-PRI-P9-018 · **D-03** |
| `<TBD-05>` | Required per-channel registration interval and acceptable end-to-end telemetry latency | SRS-PRI-P10-018, SRS-PRI-NF-009 · **C-05** |
| `<TBD-08>` | Emergency-stop input source and safe-state output configuration | SRS-PRI-P13-008 · #8 |
| `<TBD-09>` | Secondary firmware-update requirement, bootloader field-updatability, rollback, integrity mechanism | SRS-PRI-P20-017 · #9, C-06 |
| `<TBD-10>` | Primary-side authentication, authorisation, transport security, production secure boot, applicable standard | SRS-PRI-P24-010 · #10 |
| `<TBD-12>` | Range count, boundaries and changeover hysteresis | SRS-PRI-P7-093 · C-12 |
| `<TBD-13>` | Supported cycle structure: 16 distinct cycles, 16 nesting levels, or other | SRS-PRI-P5-026 · C-13 |
| `<TBD-14>` | Unified fault-code space and mapping from the four legacy code spaces | SRS-PRI-P12-017 · C-14 |
| `<TBD-16>` | Battery-backed RTC fitted; network time available | SRS-PRI-HW-008, SRS-PRI-P17-010 · #16 |
| `<TBD-17>` | Enumerated power-on self-test content per subsystem | SRS-PRI-P1-016 · #12 |
| `<TBD-18>` | Maintenance mode entry, exit, permitted operations, effect on running programs | SRS-PRI-P2-015 · #13 |
| `<TBD-19>` | Secondary node identity acquisition method | SRS-PRI-P3-013 · #22, **D-04** |
| `<TBD-20>` | Hot-plug requirement · health-check channel definitions and actions | SRS-PRI-P3-014, SRS-PRI-P18-025, SRS-PRI-P22-016 · #14, #20 |
| `<TBD-21>` | May a program start with fewer than the configured number of Secondaries present? | SRS-PRI-P3-015 · #14 |
| `<TBD-22>` | Concurrently resident program count; versioning and rollback requirement | SRS-PRI-P4-018 · #25 |
| `<TBD-23>` | Battery-parameter resolution: Primary at runtime or Web App pre-computed | SRS-PRI-P5-025 · #6 |
| `<TBD-23a>` | Supply hold-up time; automatic versus confirmed program resume | SRS-PRI-P23-014 · #23 |
| `<TBD-24>` | Whether one program instance may span several channels | SRS-PRI-P6-014 · #15 |
| `<TBD-25>` | Definition of the available digital filters | SRS-PRI-P7-057 · #5 |
| `<TBD-26>` | Detailed semantics of operator `PAUA` | SRS-PRI-P7-111 · #27 |
| `<TBD-26a>` | MISRA-C applicability and scope | SRS-PRI-NF-030 · #26 |
| `<TBD-27>` | Detailed semantics of operator `PAUO` | SRS-PRI-P7-112 · #27 |
| `<TBD-28>` | Detailed semantics of operator `IRANGE` — current measurement and control range selection. Partially sourced from BM §12.4.8.13; interacts with conflict **C-12** | SRS-PRI-P7-113 · #27, C-12 |
| `<TBD-29>` | Detailed semantics of operator `URANGE` — voltage measurement and control range selection | SRS-PRI-P7-114 · #27 |
| `<TBD-30>` | Detailed semantics of operator `OUTA` — program-controlled digital output | SRS-PRI-P7-115 · #19, #27 |
| `<TBD-31>` | Detailed semantics of operator `OUTB` — program-controlled digital output | SRS-PRI-P7-116 · #19, #27 |
| `<TBD-32>` | Detailed semantics of operator `ISOEXT` | SRS-PRI-P7-117 · #27 |
| `<TBD-33>` | Detailed semantics of operator `ISOINT` | SRS-PRI-P7-118 · #27 |
| `<TBD-34>` | Definition of limits `ABATT` and `VBATT` | SRS-PRI-P7-164 · #5 |
| `<TBD-35>` | Permitted skew between channel starts in a multi-channel start | SRS-PRI-P8-009 · #16 |
| `<TBD-36>` | Independent versus step-synchronised multi-channel assignment | SRS-PRI-P8-011 · #16 |
| `<TBD-37>` | Cross-channel supervised quantities and escalation criteria | SRS-PRI-P11-014 · #24 |
| `<TBD-38>` | Which faults latch; who may acknowledge; whether acknowledgement needs an operator action | SRS-PRI-P12-018 · #17 |
| `<TBD-39>` | Automatic versus confirmed resume after Secondary recovery; action on prolonged Web App link loss | SRS-PRI-P14-015 · #14, #12 |
| `<TBD-40>` | Storage medium, capacity, retention period, exhaustion behaviour | SRS-PRI-P15-015 · #3, #11 |
| `<TBD-41>` | Buffering duration and depth; reconciliation protocol | SRS-PRI-P16-010 · #12 |
| `<TBD-42>` | Whether per-range calibration is required for ME | SRS-PRI-P21-021 · #15 |
| `<TBD-43>` | Modbus role, physical layer, register map, Modbus TCP requirement | SRS-PRI-P25-011 · #29 |
| `<TBD-44>` | Whether Modbus write access is permitted and to which items | SRS-PRI-P25-012 · #29 |
| `<TBD-45>` | Maximum time from trip detection to safe state | SRS-PRI-NF-006 · #8 |
| `<TBD-46>` | Maximum time from supply application to Ready | SRS-PRI-NF-007 |
| `<TBD-47>` | Applicable safety integrity requirement | SRS-PRI-NF-015 |

> **Note on the tag `<TBD-26a>`.** The suffix is an artefact: `<TBD-26>` was already
> taken by operator `PAUA` when the MISRA-C question was added. The tag name is retained
> so that references to it stay valid; the row now sits in numeric sequence above.
> Renumbering is deferred to the next revision.

### 8.4 Reserved architect decisions

| ID | Decision | Status in this document |
|---|---|---|
| **D-01** | Allocation of functions between the Cortex-A53 cluster and the Cortex-M7, in particular real-time step execution | Every requirement carries `Alloc: TBD`. Annex §A2 is the single-pass resolution sheet. |
| **D-02** | Which core owns the CAN controller and driver stack | Not stated or implied anywhere. |
| **D-03** | Exact content of the minimum necessary control data | `<TBD-03>`; §4.9 specifies *what information* moves, never its content layout. |
| **D-04** | CAN layer details: 2.0B versus FD, bit rate, node addressing, higher-layer protocol, frame formats | Not stated. Node addressing appears only as a functional need (SRS-PRI-P3-003). |
| **D-05** | Web App interface details: TCP versus UDP assignment, ports, framing, serialization, program file format | Not stated. §3 and §4 specify information and direction only. |
| **D-06** | Inter-core communication mechanism | Not stated; dependent on D-01. |

---

*End of ME-SRS-PRI-001 v0.1. Requirement count: 620. Companion annex holds §6 and §7.*
