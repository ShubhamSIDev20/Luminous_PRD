# ME — SRS Source-Coverage Outline & Open Issues Register

**Document ID:** ME-PRE-001 · **Version:** 0.1 (draft) · **Date:** 2026-07-27
**Status:** Gate document — awaiting architect confirmation before SRS generation
**Purpose:** Establishes, per mandatory-checklist area, which ME requirements have
traceable source material and which do not. This is the pre-write deliverable
requested before the full SRS set is produced.

### Scope added on architect instruction, 2026-07-27

The table below records what the architect's instructions of 2026-07-27 added to this
document, and is retained as provenance for the sections and registers concerned.

| # | Change | Driver |
|---|---|---|
| 1 | **Open Issue #4 CLOSED** — no COM Controller in ME | Architect instruction |
| 2 | **New §0.3** — i.MX 8M Plus peripheral inventory from the NXP industrial datasheet | Architect instruction ("find on the internet") |
| 3 | **New §0.4** — consequences of deleting the COM Controller; three new conflicts | Derived from #1 + #2 |
| 4 | **New Appendix A** — complete BTS-600 operator/limit/registration catalog, read directly from BM_Manual pp.156–219 | Architect instruction ("refer BTS-600 completely") |
| 5 | **New Open Issues #27–#31**; **new conflicts C-16…C-18** | Findings from #2 and #4 |
| 6 | Cortex-M7 recorded as **running an RTOS** (given constraint, not a decision) | Architect instruction |

---

## 0. Source Inventory & Precedence

Declared precedence: **(3) hardware spec > (1) BM_Manual > (2) legacy SRS**.

| Tag | Source | Location | What it actually covers |
|---|---|---|---|
| **HW** | `Digital_Controller_Specs_V1.04_RemarkAdded-10-Feb-2026 (1) - Copy.xlsx` | Reference Documents | **Secondary Board only.** 7 sheets: Digital Controller (14 topics), Error code (23 defined of 100 max), Configuration (47 params), Calibration (25 params), Coding Guidelines, Queries & Implement, Version |
| **BM** | `BM_Manual_eng 2.pdf` (363 pp, Digatron Battery Manager 4, rel. 2022-2) | Reference Documents | Web Application + program language. Ch.2 control/states/errors, Ch.5 registration data, Ch.7 passwords/rights, Ch.9 circuit view, Ch.10 dispo list, **Ch.12 BTS-600 Programs (pp.156–219) — read in full, see Appendix A** |
| **SOC** | `IMX8MPIEC` Rev.1 08/2021 — *i.MX 8M Plus Applications Processor Datasheet for Industrial Products* | NXP (web) | **Added on architect instruction.** Explicitly covers `MIMX8ML8CVNKZAB`. Authoritative SoC-level peripheral inventory — see §0.3 |
| **LSRS** | `BTS_Primary_SW_Requirement_Analysis V1.7.xlsx` (281 reqs) | Reference Documents | Combined Primary+Secondary. Digital I/O, RTC, RS485/Modbus ×2, Ethernet, external CAN ×3 + DBC config model, analog in/out, transistor banks, program operators, compile/download |
| **WAD** | `WebAppDocs/` (SRS, ADD, DDD, DBD, ICD, RTM, ARCHITECTURE_CAPACITY, CMP, PMP, QAP, RMP, VVP) | Reference Documents/WebAppDocs | Existing Ador Blazor BTS server: full Web↔hardware interface baseline, session DB schema, roles, audit, capacity analysis |
| **CODE-P** | `BTS_Primary_SOM` (SAM9X60, Harmony/FreeRTOS) | D:\Projects\BTS_VS_CODE | `bts_app.*`, `networkDataHandler.*`, `prim_uart.*`, `prim_com_uart.c`, `prim_reg_uart.c`, `eeprom.c`, `power_fail.*`, `sd_card.c`, `sleepMode.c`, `timer.c` |
| **CODE-C** | `BTS_PRIM_COM_V106` (STM32H7 COM controller) | D:\Projects\BTS_VS_CODE | `comApp.c` (57 KB), `fdcan.c`, `ModSlave.c`, `RS485_Slave/Master.c`. **Board deleted in ME** — retained only as evidence of external-CAN/Modbus intent (§0.4) |
| **CODE-S** | `BTS_SEC_FW_V201` (STM32H7 Secondary) | D:\Projects\BTS_VS_CODE | `btsSecUart.c` (151 KB), `btsSecApp.c` (119 KB), `btsCalibration.c` (75 KB), `stepData.c`, `btsSecPID.c`, `ADS1256_Driver.c`, `cycleTable.c`, `emulEeprom.c` |
| **GAP** | `BTS600_Gap_Analysis/bts600_operator_gap_analysis.md` + effort estimate | D:\Projects\BTS_VS_CODE | 32-item operator catalog vs. implemented; identifies ~21 absent |
| **PSPE** | `docs/superpowers/specs/2026-07-09-primary-side-program-execution-design.md` | D:\Projects\BTS_VS_CODE | **Directly pre-figures ME change #4.** Responsibility partition, control-frame content, comms-loss fail-safe. Bears on D-03. |
| **FRM** | `BTS_Primary_SOM/docs/frame-formats/*.md` (19 files) | D:\Projects\BTS_VS_CODE | Versioned BM↔Primary and Primary↔Secondary frame definitions |
| **CMMI** | `BTS_Primary_SOM/docs/cmmi/03_SRS.md` (61 KB), `04_RTM`, `05_DDD` | D:\Projects\BTS_VS_CODE | Prior Primary SRS. **Caution:** GAP §2/§5 records that its operator-decode layer (SRS-PRG-050/051) was documented but never built. |
| **MISSING** | `BM_PM_BTS600_New_Operators.pdf` | **Not supplied** | BM §12.4.2 defers 8 operators to this document: `PAUA`, `PAUO`, `URANGE`, `IRANGE`, `OUTA`, `OUTB`, `ISOEXT`, `ISOINT`. → Open Issue **#27** |

### 0.1 Path corrections (differ from the brief)

| Brief states | Actual |
|---|---|
| `...\Documents\Projects\ME\Reference Documents` | `...\Documents\Projects\MicroME\ME Workspace\Reference Documents` |
| `...\Documents\Projects\ME Workspace\Requirements` | `...\Documents\Projects\MicroME\ME Workspace\Requirements` (exists; this file written there) |

### 0.2 Structural finding — the precedence rule barely applies to the Primary

The highest-precedence source (**HW**) is a *Secondary Board* specification. It refers
to the Primary throughout as the "Main Controller" and specifies only the digital
controller transcard that drives the transistor bank. Consequently:

- For the **Secondary SRS**, precedence operates as intended: HW is authoritative and dense.
- For the **Primary SRS**, HW contributes only interface-facing constraints
  (CAN/CANFD link, error-code relay, calibration/config relay, host-PC flashing).
  **No ME Primary Board hardware specification exists in the reference set.**
  This document partially mitigates this at *SoC* level via **SOC** (§0.3), but *board* level
  — what is actually wired to the SoC on the Ador carrier — remains unsourced.
  See Open Issue **#3** (rescoped).

---

## 0.3 Primary Board SoC — peripheral inventory

Source: **SOC** = NXP `IMX8MPIEC` Rev.1, 08/2021, Tables 1–4 and Figure 1.
The datasheet's ordering table lists `MIMX8ML8CVNKZAB` explicitly, so this is the
correct document for the specified part — not a near-neighbour derivative.

### Part number decode — `MIMX8ML8CVNKZAB`

| Field | Value | Meaning |
|---|---|---|
| `IMX8ML` | series | i.MX 8M Plus |
| `8` | part differentiator | **Quad** A53 + VPU + NPU + ISP + HiFi 4 (fullest variant) |
| `C` | temperature | **Industrial, Tj −40 to +105 °C** |
| `VN` | package | FCBGA548, 15 × 15 mm, 0.5 mm pitch |
| `KZ` | A53 frequency | **1.6 GHz** |
| `A` | fusing | standard |
| `B` | silicon revision | **Rev A1** |

### Compute

| Block | Detail |
|---|---|
| Cortex-A53 | **4 cores @ up to 1.6 GHz**, ARMv8-A 64-bit; 32 KB L1-I + 32 KB L1-D per core; **512 KB unified L2 with ECC**; NEON MPE; VFPv4-D16 FPU |
| Cortex-M7 | **1 core @ up to 800 MHz**; 32 KB L1-I + 32 KB L1-D; **256 KB TCM**. Datasheet describes it as "microcontroller available for customer application / real-time processing / Cortex-A53 complex offloading". **ME constraint: an RTOS runs on this core.** |
| HiFi 4 DSP | Cadence Tensilica, up to 800 MHz (present on the `8` variant) |
| NPU | 2.3 TOPS (present on the `8` variant) |
| GPU | GC7000UL 3D + GC520L 2D |
| VPU | 1080p60 encode/decode H.264/H.265 |

### Memory

| Block | Detail |
|---|---|
| DRAM | 32-bit **LPDDR4-4000 / DDR4-3200**, up to 8 GB, **Inline ECC on the DDR bus** |
| On-chip RAM | **868 KB** total — OCRAM 576 KB (SUPERMIX), OCRAM_A 256 KB (AUDIOMIX), OCRAM_S 36 KB. ECC supported on internal software-accessible SRAMs |
| Boot ROM | 256 KB |
| Flash | eMMC 5.1 ×2 (uSDHC1, uSDHC3); SD/SDIO 3.0 ×3 (uSDHC1/2/3); 8-bit raw NAND via GPMI with **BCH 62-bit ECC**; SPI NOR ×3; **FlexSPI** Octal/dual-Quad with **XIP — explicitly called out for Cortex-M7 low-power operation** |

### Connectivity — the areas that constrain ME

| Block | Count | Detail |
|---|---|---|
| **FlexCAN** | **2** (`FlexCAN1`, `FlexCAN2`) | **CAN-FD and CAN 2.0B.** This is the binding constraint of §0.4. |
| Ethernet | **2 × 1 Gb** | `ENET` (IEEE 1588) + `ENET_QOS` (**TSN**: 802.1Qbv scheduling, 802.1Qbu frame preemption, time-based scheduling; AVB) |
| UART | **4** | UARTv2, up to 5 Mbps |
| I²C | **6** | up to 320 kbps |
| eCSPI | **3** | full-duplex, up to 52 Mbit/s, master/slave |
| USB | **2 × USB 3.0/2.0** with PHY | |
| PCIe | **1 × Gen3, single lane** | root-complex or endpoint |
| SAI | 6 instances (SAI1/2/3/5/6/7) | I²S/TDM/AC97/DSD |
| GPIO | **5 modules × 32 bits** | with interrupt capability; IOMUXC pad control |

### Timing, control, reliability

| Block | Count | Detail |
|---|---|---|
| GPT | **6** | 32-bit free-running / set-and-forget, prescaler, compare + capture |
| PWM | **4** | 16-bit counter, 16-bit resolution, 4×16 FIFO — *audio-oriented in the datasheet description* |
| Watchdog | **3** (`WDOG1/2/3`) | two comparison points per period; one can interrupt, one drives an external WDOG line |
| SDMA | 3 Smart DMA + 1 eDMA (32-channel) | eDMA has **no peripheral DMA requests** — memory-to-memory only |
| Temperature | TMU + **2 temperature probes** | programmable trip points |
| RTC | **Secure RTC inside SNVS** | see Open Issue #16 — on-die RTC exists; *battery-backed* board-level RTC is a carrier question |

### Security

TrustZone (A53); **RDC** Resource Domain Controller (4 domains, up to 8 DDR regions);
**CSU** Central Security Unit; **TZASC** (TZC-380) on the DRAM path; **HAB** High
Assurance Boot; **CAAM** (AES/3DES/DES, RSA + ECC via PKHA, RTIC run-time integrity
checker, TRNG, 32 KB secure memory, manufacturing protection); **SNVS** (secure RTC,
security state machine, master key control, violation detection); **OCOTP** eFuse
(chip ID, crypto keys, JTAG secure mode, boot config); **Secure JTAG (SJC)**.

> This is the first concrete, citable basis for Primary-side security requirements
> (P24), which no supplied source covers. It establishes *capability*; whether ME
> must **use** it remains Open Issue **#10**.

### ⚠ Notable absence — no general-purpose ADC

The i.MX 8M Plus module list contains **no SAR/general-purpose ADC**. Analog input is
limited to the thermal monitoring unit and its two temperature probes. Any analog
acquisition on the Primary Board requires external silicon. This is consistent with
the ME architecture (Secondaries do all measurement), but it must not be assumed away
— see Open Issue **#28**.

---

## 0.4 Consequence of deleting the COM Controller

**Architect instruction:** *"there will not be any com controller but there is a
Cortex-M7 core which has RTOS running on it."*

This **closes Open Issue #4** but does not make the question go away — it relocates it.
The legacy COM Controller (`BTS_PRIM_COM_V106`, STM32H7) provided:

| Legacy COM Controller function | Legacy provision | ME provision on i.MX 8M Plus |
|---|---|---|
| External CAN ports + DBC decode | **3 × FDCAN** | **At most 1 spare FlexCAN** (2 total − 1 for the Secondary bus) → **C-16** |
| RS485 / Modbus | **2 ports** (`ModSlave.c`, `RS485_Slave.c`, `RS485_Master.c`) | 4 UARTs available; needs external RS485 transceivers → **C-17** |
| Real-time offload from the SOM | dedicated MCU | **Cortex-M7 @ 800 MHz + 256 KB TCM, running an RTOS** |

Three consequences, all recorded rather than resolved:

1. **CAN capacity is now silicon-limited.** LSRS carries ~100 requirements predicated
   on 3 external CAN ports with a full DBC configuration model. On this SoC at most one
   external CAN port survives after the Secondary bus takes one. → **C-16**, Open Issue **#29**.
2. **The M7 is a given, its workload is not.** That an RTOS runs on the M7 is now a
   stated constraint and is recorded as such. **It does not decide D-01, D-02 or D-06.**
   No requirement in either SRS will presuppose which functions land on the M7, which
   core owns the CAN driver, or how the cores communicate. The `Allocation: TBD (see D-01)`
   attribute still applies to every Primary requirement.
3. **FlexSPI XIP is called out by NXP specifically for M7 low-power operation**, and
   the M7 has only 256 KB of TCM. This is a *capability note* for the architect sizing
   D-01; it is not a requirement and implies no allocation.

---

## 1. Outline — SRS: ME Primary Board

**Coverage key:** ● Strong (multiple traceable sources) · ◐ Partial (source exists, gaps remain) · ○ Absent (no source — Open Issue) · ◆ NEW-ME (no precedent; ME topology change)

### Document skeleton (ISO/IEC/IEEE 29148)

| § | Section | Principal sources |
|---|---|---|
| 1 | Introduction — purpose, scope, definitions, acronyms, references | BM §1, WAD/SRS §1 |
| 2 | Overall Description — product perspective, block diagram, user classes, operating environment, constraints, assumptions & dependencies | BM §1/§9, WAD/ADD, SOC, brief |
| 3 | External Interface Requirements — hardware, software, communication, user | **SOC §0.3**, HW §7, WAD/ICD, LSRS §5–7 |
| 4 | Functional Requirements — by checklist area P1–P24 | below |
| 5 | Non-Functional Requirements — performance/timing, safety, reliability, security, maintainability, diagnostics | SOC (security, ECC, temp grade), HW §6/§10, WAD/ARCHITECTURE_CAPACITY, **BM 100 ms limit-check / 0.1 s registration floor (Appendix A.7)** |
| 6 | Traceability Matrix | — |
| 7 | **Core Allocation — Open** (every requirement, Allocation = "TBD (see D-01)") | — |
| 8 | Open Issues / TBD Register | §4 of this document |

### Functional coverage by checklist area

| Area | Cov. | Source material found | Gap → Open Issue |
|---|---|---|---|
| **P1** Startup, init, self-test, shutdown | ◐ | CODE-P `BTS_APP_STATE_INIT`/`SYS_Initialize`; LSRS SW_REQ_23 (RTC fail → default + log); HW §10 watchdog/auto-reset; **SOC**: HAB secure boot chain, 3 × WDOG, boot-mode pins | No POST/self-test defined for Primary; no orderly-shutdown spec → **#12** |
| **P2** Operating mode / state machine | ◐ | BM §2.10 (STOP, Interrupt, Pause, Charge, Recharge, Discharge, Error, Offline, Deferred Start, RESET, BOOT, ZERO); CODE-P `en_btsProgram*`, `en_btsCircuit*` | "Maintenance" mode unsourced → **#13**. Board-level vs per-channel state distinction is new (8 channels) |
| **P3** Secondary discovery, enumeration, addressing, presence (≤8) | ◆◐ | Analogue only: WAD/ICD UDP discovery (Web↔device); HW Config #9 CAN Timeout 100 ms; CODE-S `PRIM_SEC_COM_EX_COUNTER`; `en_ERR_PRIM_SEC_COM` | BTS was 1:1 over UART. Addressing = **D-04** (deferred). Enumeration/hot-plug/absent-node policy unsourced → **#14** |
| **P4** Program reception from Web App | ● | CODE-P `ProgramQueryID_t` (isReady, metadata, steps, data, read-metadata, read-saved, DBC steps/data), 1 MB program buffer, packet CRC; LSRS SW_REQ_278–281; WAD FR-005; FRM `bm_program_v3.0` | Versioning/rollback of stored programs unsourced → **#25** |
| **P5** Program parsing & decoding; program/step/setpoint data model | ●◆ | **Very rich.** BM Appendix A gives the complete model: 5-column step record (Label, Operator, Nominal Value, Limit, Action, Registration), 34 documented operators, 8 nominal-value classes, 24 limit forms, 7 actions, 4 registration types. CODE-S `stepData.h`; FRM `ps_program_v1.6` | Model is fully evidenced but the **Primary never decoded it** (GAP §2). 8 operators undocumented → **#27**. Battery-parameter-relative values → **#6** |
| **P6** Program scheduling & assignment to 1..8 Secondaries | ◆ | Closest analogue: BM §10 Dispo List; WAD FR-011 Program Scheduler; BM §12.4.5 STO semantics ("stops the actual test, not the remaining dispo-list tests") | Substantially new → **#15**, **#25** |
| **P7** Step execution engine (types, entry/exit, transitions, loops, nesting, branching, hold/rest) | ● | **Richest area — Appendix A is the full specification.** BM §12.4.2–12.4.8, §12.5.1–12.5.4; CODE-S `btsSecApp.c`/`cycleTable.c`; LSRS §10; GAP catalog | Operator scope for ME undecided → **#5**. Nesting depth conflict → **C-13**. Missing operator doc → **#27** |
| **P8** Concurrent execution & coordination of ≤8 | ◆◐ | BM §12.4.8.14 PARALLEL (**"For ME circuits… max 15 (slave) circuits"**); §12.4.8.16 SYNCLine/SYNCProgram (**"can be used for ME- and MBC systems"**, SyncGroup column); WAD/ARCHITECTURE_CAPACITY | Independent-vs-synchronised semantics unsourced → **#16**. BM's ME limit is 15 vs our 8 → **#1**, **#16** |
| **P9** Command dispatch to Secondaries | ● | CODE-S `controlCmdQueryID_t` (start/stop/interrupt/continue/power-resume/DO-selection); CODE-P `PRIM_UART_STATES`; HW §7 command list (CHA/DCH/PAU/CYC/INT/STO/PARALLEL/ACN) | Content = **D-03** (deferred). PSPE §4.4 is prior art |
| **P10** Telemetry acquisition, aggregation, forwarding | ● | CODE-S 500 ms real-time / 100 ms registration; measured-data payload (77→80 B, 25 fields); CODE-P REG-UDP TX queue (depth 200); WAD/ICD §5.2; HW §6 registration ≤500 µs; **BM registration-format byte budgets (Appendix A.2)** | Rate conflict → **C-05**; aggregate 8-node load → **#18** |
| **P11** Real-time monitoring, limit checking, system-level safety supervision | ◐ | BM §12.4.8.2 global limits; MaxCHAI/MaxDCHI/MaxChaU/MaxDchU/MaxChaW/MaxDchW; **BM: "The check of these limits takes place with the maximum measuring speed, i.e. every 100 ms"**; CODE-P factory limits | System-level (cross-channel) supervision is new → **#24** |
| **P12** Fault detection, classification, escalation, latching, ack, recovery | ◐ | HW Error-code sheet (23 defined, 100 max); CODE-P/S 17-bit `en_ERR_*` bitmask; WAD/ICD §9.2; BM §2.12 ER1–ER10; **BM default message numbers 1–6 (Appendix A.6)** | **Four incompatible code spaces** → **C-14** (widened). Latch/ack/recovery semantics thin → **#17** |
| **P13** Emergency stop & safe-state | ○◐ | Partial: CODE-S `relayDefaultState()`, DAC→0, STO; BM message 6 "External control signal activated!" | HW lists all 4 DIs as "Spare"; ADPL notes no motherboard DI connections on dual bank. **No E-stop defined** → **#8** |
| **P14** Communication-loss handling (Web link, Secondary node) | ◐ | CODE-P `ERR_NETWORK_CONN_FAIL`, `WEBAPP_RECONNECT_TIMEOUT_MS` = 300 000, `clearPauseCmdSentOnNetFail()`; HW Config #9 CAN timeout; HW pending item "Data save on Primary in case of PC disconnect"; PSPE §5.2/§7 | Per-node degradation policy for 8 nodes unsourced → **#14**, **#24** |
| **P15** Data logging & storage (what, rate, retention, format, exhaustion) | ◐ | CODE-P `sd_card.c`; **BM §12.4.1 registration formats with exact byte costs + RLevel 0–4 (Appendix A.2/A.3)**; WAD/DBD session SQLite schema; HW pending "DATA SAVE AFTER PFA"; **SOC**: eMMC ×2 / SD ×3 / NAND available | Retention & storage-exhaustion policy absent → **#11**. Which medium is fitted → **#3** |
| **P16** Data upload/retrieval by Web App; buffering during link loss | ◐ | WAD/ICD §6 session store; HW pending item (as above) | Buffer depth/duration/backfill protocol absent → **#12**, **#11** |
| **P17** Time synchronisation & timestamping | ● | CODE-P/S sync-time queries (config Q6, control Q5), epoch time; LSRS SW_REQ_19–23; **SOC**: SNVS secure RTC on-die, ENET IEEE 1588, ENET_QOS TSN time-based scheduling | Board-level battery-backed RTC & NTP availability → **#16** |
| **P18** Configuration management (system, per-channel, persistence, defaults, restore) | ● | HW Configuration sheet (47 params, max 200); CODE-P factory (109 B) / battery (43 B) / MFG blocks, `eeprom.c`; LSRS RS485 config; WAD FR-007 | Per-channel × 8 scaling & restore-to-defaults unsourced → **#22** |
| **P19** User/operator interaction via Web App | ● | BM §2 (start/stop/interrupt/continue/reset, start dialog, deferred start, session naming, start-from-step), §6 Status View, §10 Dispo List; **BM §12.4.8.5 ERR resume semantics**; WAD FR-001…011 | Which Web App → **#2** |
| **P20** Firmware update — Primary, and Secondaries over CAN | ○ | HW §8 bootloader row: spec requires it; ADPL remark *"This is not required as discussed earlier"*; error codes 0x11/0x14/0x15; **SOC**: HAB + CAAM + OCOTP give a citable integrity basis | **Direct conflict with brief** → **C-06**, **#9** |
| **P21** Calibration data management & distribution | ● | CODE-S `calibCmdQueryID_t` Q1–Q23; CODE-P `CALIB_DATA` incl. per-range + temperature calibration; HW Calibration sheet (25 params); FRM `bm_calibration_v4.2`, `ps_calibration_v5.2` | Retention of V5.2 per-range scheme for ME → **#15** |
| **P22** Diagnostics, health monitoring, event log, audit trail | ◐ | HW Config #23–42 Health-Check limits (5 channels); WAD AuditLogs + Serilog; BM §1.7 Event View; **SOC**: TMU trip points, 2 temp probes, DDR/L2/OCRAM ECC as health signals | Health-check channel identity & actions undefined → **#20**. Primary-resident event log unsourced → **#17** |
| **P23** Power-fail detection & program recovery/resume | ● | CODE-P `power_fail.c/h` (3-state SM, PF notify ×3), CODE-S `powerFailBackup/Resume`, `progBackUp_t`, control Q5 POWER_RESUME, EEPROM persistence; HW §10 + 3 pending items; **BM §12.5.3 counter-restore + GOTO-to-abort-point pattern** | HW marks all three as *pending* → **#23** |
| **P24** Security & access control | ◐→● | WAD: ASP.NET Identity, JWT (24 h), 3 roles, `UserCircuitAccess`, audit, column encryption; BM §7 departments/groups/rights/users; **SOC §0.3 Security — now a citable Primary-side capability basis** | Capability is now sourced; **requirement to use it is not** → **#10** |

**Totals — Primary:** ● 11 · ◐ 10 · ○ 2 · ◆ (overlaid on 4)

---

## 2. Outline — SRS: ME Secondary Board (STM32)

### Document skeleton
Identical §1–§6 and §8 to the Primary SRS; **§7 (Core Allocation) omitted** — single-core MCU.

### Functional coverage by checklist area

| Area | Cov. | Source material found | Gap → Open Issue |
|---|---|---|---|
| **S1** Startup, init, self-test, safe default outputs | ◐ | CODE-S `btsSecAppInit()` + `btsSecAppError_t` (app/ADS1256/DAC/calib/watchdog init errors), `relayDefaultState()`; HW §10 watchdog + auto-reset | No POST/self-test enumerated → **#12** |
| **S2** Operating mode / state machine | ● | CODE-S `circuitState_t` (Idle, Charge, Discharge, Pause, Continue, Interrupt, Error, Msg), `progRunState_t` (Idle, Running) | Maintenance/calibration as distinct modes → **#13** |
| **S3** Node identity & CAN-bus addressing | ○ | Only `circuitId` byte (CODE-S) and HW §7 "Isolated CAN/CANFD" | Wholly **D-04** (reserved). Identity source (DIP switch? HW "Queries" sheet asks "DIP S1 purpose?") → **#22** |
| **S4** Command reception, validation, rejection | ● | CODE-S ACK/NACK per group, CRC-16/Modbus, `en_ERR_INVALID_CMD_PRIM_TO_SEC`; HW error 0x0E "Unknown CAN command from Main controller" | — |
| **S5** Charge control loop & regulation | ● | CODE-S CC/CV/CP/CCCV charge PIDs, `btsSecPID.c`, DAC drive, charge relay, `LOOP_EX_COUNTER`; HW Calibration #10–15 PID params; HW §5 reference signal; **BM CHA/RCH semantics + CC-CV crossover (Appendix A.4)** | Reference-signal range/resolution → **C-01**, **C-02** |
| **S6** Discharge control loop & regulation | ● | As S5 + `CC/CV/CP_DChg`; LSRS §9.2–9.5 single vs dual transistor bank; **BM DCH semantics** | Discharge relay "not used" on dual bank (HW remark) → **C-11** |
| **S7** Setpoint application, ramping, slew limiting | ● | HW Config #12 I-Ramp 10 ms, #13 U-Ramp 10 ms, #16–21 relay on/off/wait times; HW §6 rise/fall ≤10 ms; LSRS SW_REQ_176–185 switch-over delay 500 ms; **BM ramp functions (start–end value + duration) and RANGE 50 ms transit time** | **Conflict** 10 ms vs 500 ms vs measured 23 ms → **C-04** |
| **S8** Measurement acquisition (V, I, T, others) — ranges, resolution, sample rates | ● | **HW §4 authoritative:** U-terminal ≤100 V, U-heatsink, LNT (Vrect+), ZNT (Vrect−), heatsink temp, battery V-sense ≤100 V (24-bit), shunt mV (75/100 mV…10 V, 24-bit), PT1000/AD590 −50…+150 °C; CODE-S ADS1256 @1 ms + moving-average filters; LSRS SW_REQ_115–128 | ADC width conflicts → **C-03**; PT100/PT1000 → **C-15**; V/I ranges → **C-07**, **C-08** |
| **S9** Local protection & interlocks | ● | HW Error sheet 1–0x17 (LNT, ZNT, heatsink over-temp, current/voltage AD limit, setpoint unreachable, over-I, over-V, power-polarity, sense-polarity, power-vs-sense Δ, CAN loss, unknown cmd, PI out-of-range, flash errors, power-fail, unstable input, EEPROM, temp-FB lost); CODE-S exceed-counters; LSRS reverse-polarity + battery-open | Battery-open detection not in HW list → carry from LSRS, flag |
| **S10** Autonomous safe-state on fault / loss of Primary comms | ◐ | HW Config #9 CAN timeout 100 ms → error; CODE-S `relayDefaultState()`, DAC→0; PSPE §5.2 (finish step → safe-stop → report `commsStatus`) | Exact behaviour (immediate trip vs finish-step) unsourced for ME → **#8**, **#14** |
| **S11** Watchdog & communication-timeout behaviour | ● | CODE-S `userI_Wdg`, `I_WDG_ENABLE`, `PRIM_SEC_COM_EX_COUNTER` 5000; HW §10 "Watchdog and autoreset with powerfail detection" (marked *not implemented, pending*) | Watchdog period/window unsourced → **#12** |
| **S12** Telemetry reporting to Primary — content, rate, triggered vs cyclic | ● | CODE-S `SEND_REAL_TIME_DATA` 500 ms, `SEND_REGISTRATION_DATA` 100 ms, `BTS_MeasuredData_t`, `registration_t` (12 quantities), `btsRegType_t` 13-bit mask; HW §7 data list; HW §6 registration ≤500 µs | **C-05** |
| **S13** Fault reporting, fault codes, latch & clear semantics | ◐ | HW Error sheet + `logSysError()`/`logMessage()`/`logUserError()`; `actionTakenOnSysError()` | Latch/clear/ack semantics not written down anywhere → **#17** |
| **S14** Calibration — storage, application, update | ● | HW Calibration sheet (I/U charge+discharge gain & offset, temp, LNT/ZNT, PID); CODE-S `btsCalibration.c`, `calibrationFormula.c`, `emulEeprom.c` (magic 0xFF11), Q1–Q23; HW Config #44–47 cal dates | Per-range calibration scope → **#15** |
| **S15** Non-volatile configuration & persistence | ● | CODE-S `emulEeprom.c`; `BTS_FactoryData_t` (109 B), `BTS_BatteryData_t` (43 B); HW Configuration sheet (max 200 params) | Wear/integrity/defaults policy → **#22** |
| **S16** Firmware update over CAN (bootloader, rollback, integrity) | ○ | HW §8: "Programming/flashing via software on the Host PC…"; ADPL: *"not required as discussed earlier"*; HW Config #2 bootloader version param; errors 0x11/0x14/0x15 | **Conflict with brief S16** → **C-06**, **#9** |
| **S17** Local indication (LEDs, connectors, test points) | ◐ | HW §3 DO1 MODE_FAILURE relay, DO2 charge relay, DO3 discharge relay, DO4–11 spare opto; CODE-S `USER_LED_EN`, `do.c`/`di.c`; **BM §12.4.8.5: ERR sets the "Error" LED and the error relay output; RCH blinks the charge LED** | HW "Queries" sheet has 10 *unresolved* hardware questions → **#21** |
| **S18** Diagnostics & self-monitoring | ◐ | HW Config #23–42 health-check limits ×5 channels; CODE-S PI-out-of-range detection, EEPROM R/W error, temp-FB-lost | Health-check channel definition absent → **#20** |

**Totals — Secondary:** ● 11 · ◐ 5 · ○ 2

---

## 3. Outline — ICD: ME Interfaces

Per the brief: **a message inventory with semantics, not a wire format.** All encoding,
IDs, byte layout and framing columns are present but empty and marked
*"Pending D-04 / D-05"*.

### Structure

| § | Content |
|---|---|
| 1 | Purpose, scope, precedence, relationship to the two SRS documents |
| 2 | Interface identification — IF-A Web App ↔ Primary; IF-B Primary ↔ Secondary (×8); IF-C external CAN / RS485 *if in scope — see **#29*** |
| 3 | Common semantics — addressing model, ACK/NACK, retry, timeout, sequence/staleness, error signalling |
| 4 | **IF-A message set** |
| 5 | **IF-B message set** |
| 6 | Timing & rate budget (aggregate for 8 nodes) |
| 7 | Failure matrix — per message: timeout, retry, escalation |
| 8 | Open Issues / Pending-Decision register (D-03, D-04, D-05) |

### Table columns (fixed for §4 and §5)

`Msg ID (logical) | Name | Purpose | Direction | Trigger / Rate | Information content | Timeout | Error behaviour | ` **`Encoding — Pending D-04/D-05`** ` | ` **`Frame/ID — Pending D-04/D-05`** ` | Source`

### IF-A — Web Application ↔ Primary Board · candidate message groups

| Group | Messages evidenced | Source |
|---|---|---|
| Discovery & network config | discover-all, device-info reply, set-IP-config, set-server-config | WAD/ICD §4 (UDP 10002/10003) |
| Registration | register, register-response (success / already-registered / failed), delete-device | WAD/ICD §3.4; CODE-P `0xDD` Q1–Q5 |
| Configuration | is-ready, read/write factory, read/write manufacturing, read/write battery, sync-time | CODE-P `ConfigQueryID_t`; FRM `bm_config_v6.0` |
| Program | is-ready, program-metadata, steps-count, program-data, read-metadata, read-saved-program, DBC-steps, DBC-data | CODE-P `ProgramQueryID_t`; FRM `bm_program_v3.0` |
| Control | start, stop, pause/interrupt, continue, sync-time, reset | CODE-P `ControlQueryID_t`; BM §2.4–2.9 |
| Telemetry (live) | real-time measured record (25 fields) | WAD/ICD §5.2; FRM `bm_measured_param_v5.2` |
| Telemetry (stored) | registration record batch, opcode-tagged | WAD/ICD §6; **BM registration formats, Appendix A.2** |
| Calibration | Q1–Q23 incl. per-range current, voltage, temperature, verify, read-previous | CODE-P `CalibrationQueryID_t`; FRM `bm_calibration_v4.2` |
| Fault/event | system-error bitmask, user error, **message number (BM 1–6 defaults, user-extensible via Maintenance → BTS-600 Messages)** | CODE-P; BM §12.4.8.5/6 |
| **NEW-ME** | per-Secondary enumeration & status roll-up; multi-node program assignment; firmware-update transfer | brief §P3/P6/P20 |

### IF-B — Primary Board ↔ Secondary Board (CAN, ×8) · candidate message groups

| Group | Messages evidenced | Source |
|---|---|---|
| Config / factory | is-ready, read/write factory, write battery, read/write manufacturing | CODE-S `configDataQueryID_t` |
| Step / control data | is-ready, step-receive, next-step, request-row, next-row, cycle-table, cycle-counts | CODE-S `progStepQueryID_t`; **content = D-03** |
| Telemetry | real-time (cyclic), registration (cyclic/triggered) | CODE-S `measuredDataQueryID_t` |
| Run command | start, stop, interrupt, continue, power-resume, DO-selection | CODE-S `controlCmdQueryID_t` |
| Calibration | Q1–Q23 (relayed) | CODE-S `calibCmdQueryID_t` |
| **NEW-ME** | node presence/heartbeat, node enumeration/address assignment, firmware-update over CAN | brief §P3/S3/S16 |

**Prior art for D-03:** PSPE §4.4 proposes a per-step control frame carrying
`cmdToken`, mode, 2 setpoints, 4 safety limits, and K cutoff conditions
(quantity, logic-op, threshold, condition-id) — with the Secondary detecting and
the Primary deciding. **Recorded as evidence only; not adopted.**

---

## 4. Open Issues Register — questions for the architect

### Scope & identity

| # | Question |
|---|---|
| **1** | **Is "ME" the Digatron/BM4 hardware type of the same name?** Now confirmed in three places by direct reading: §12.2 (Pascal-editor hardware type "ME"), §12.4.8.14 (*"For ME circuits… a maximum number of 15 (slave) circuits can be arranged in parallel mode on one (master) circuit"*), §12.4.8.16 (*"SYNCLine and SYNCProgram can be used for ME- and MBC systems"*). The hardware spec separately says *"Refer existing CAN protocol between CAN-LC and MicroMe."* Must ME be BM4-compatible as an "ME" circuit, or is the name reused for a new Ador product? **This is the highest-leverage question in the register** — it determines whether the BM4 program semantics are a compatibility contract or merely a design reference. |
| **2** | **Which Web Application?** `WebAppDocs` describes the Ador Blazor BTS server (TCP 9999 / UDP 10000–10003, SQLite sessions). Is ME's Web App that application evolved, Digatron BM4, or a new build? Determines whether IF-A requirements are *carried forward* or *newly specified*. |
| **3** | **ME Primary Board hardware specification (rescoped).** SoC-level peripherals are now sourced from the NXP datasheet (§0.3). Still unsourced at **board** level: which storage medium is fitted (eMMC / SD / NAND) and its capacity; DRAM size; whether a battery-backed RTC is present; which Ethernet ports are wired and to what PHY; how many CAN transceivers are fitted; front-panel indicators; digital I/O; power-fail detection circuit. Is there a carrier-board schematic or spec? |
| ~~**4**~~ | ~~Is the COM Controller board retained?~~ **CLOSED 2026-07-27:** No COM Controller in ME. Consequences recorded in §0.4; residual scope question is now **#29**. |
| **5** | **Which BTS-600 operators are in ME Phase 1 scope?** Appendix A.1 now gives the definitive catalog: **34 operators documented in BM_Manual**, plus **8 documented only in a missing companion PDF** (#27). GAP finds ~11 implemented, ~21 absent. LSRS SW_REQ_193 lists 13 supported; SW_REQ_194 defers RCH/LOM/EIS/POL. Please mark the ME list against Appendix A.1. |
| **6** | **Battery-parameter-relative values in scope?** BM §12.3 defines 11 battery parameters (`INTERN[17]`…`INTERN[27]`: CNom, NoCell, UGas, UMax, INom, ICrank, ChargeF, Rin, CutOff, UNom, EDensity) usable in Nominal Value, Limit and Registration columns, plus derived forms `ACn1/2/4/5/10/20`, `VnC`, `AhDef`/`PercAh`, `PERCCN_P/C`. GAP §3a shows this is unimplemented end-to-end. If in scope: does the Web App pre-compute literals, or does the Primary resolve at runtime against the live battery record? |
| **7** | **Procedures vs PRODUCER.** BM §12.5.2 defines Procedures (reusable sub-programs stored under their own program number, invoked by name in the Operator column, `RET` to return, **GOTO restricted to the same program level**, decompose/recompose in the editor). The Web App instead implements PRODUCER (inline expansion with renumbering at transfer time — WAD FR-005.6…5.11, which warns step-number GOTOs break after expansion). Which model does ME adopt? |

### Safety, security, lifecycle

| # | Question |
|---|---|
| **8** | **Is there an emergency-stop input, and what is the safe state?** HW lists all 4 Secondary digital inputs as "Spare"; ADPL notes no motherboard DI connections on the dual-bank circuit. BM default message 6 is *"External control signal activated!"*, implying an external interlock exists in BM4 systems. P13 and S10 cannot be written without a defined E-stop source and a defined safe state (DAC→0? relays open? which positions?). |
| **9** | **Firmware update over CAN — confirm it is required.** The brief mandates it (P20, S16); HW's bootloader row carries the ADPL remark *"This is not required as discussed earlier."* If required: is the Secondary bootloader field-updatable over CAN or factory-only? Rollback required? Integrity mechanism? (For the **Primary**, §0.3 now supplies a citable basis: HAB secure boot + CAAM + OCOTP.) |
| **10** | **Primary-side security — capability now known, requirement still open.** §0.3 establishes that the SoC provides TrustZone, HAB, CAAM, SNVS, RDC, CSU, TZASC, Secure JTAG and eFuse key storage. Which must ME **use**? Is the Primary on a trusted isolated network (security handled entirely by the Web App), or must it authenticate the Web App and enforce roles itself? Any standard to comply with (IEC 62443, etc.)? Must secure boot be enabled in production (an irreversible fusing decision)? |
| **11** | **Data retention & storage-exhaustion policy.** Storage medium and capacity (see #3), retention period, and behaviour when full — refuse to start / stop running tests / overwrite oldest / alarm-only? Note BM gives exact per-registration byte costs (Appendix A.2), so a retention calculation is possible once medium and rate are fixed. |
| **12** | **Buffering during Web App link loss.** For how long, at what rate, and how is the backlog reconciled on reconnect? (HW lists *"Data save on Primary in case of PC disconnect"* as pending.) Also: is an orderly shutdown sequence required, and what self-tests run at startup on each board? |
| **13** | **"Maintenance" mode.** The brief lists it in P2/S2; no source defines it. Required? Entry/exit conditions, permitted operations, effect on running tests? |

### Multi-node topology (the core ME change)

| # | Question |
|---|---|
| **14** | **Secondary presence & absence policy.** How is a node enumerated at power-up — static address (DIP/strap), assigned by the Primary, or self-announced? Is hot-plug required? What happens to a running program when its Secondary stops responding — and what happens to the *other seven*? |
| **15** | **Program assignment semantics.** Is a program assigned per-Secondary independently, or can one program instance span several Secondaries? Does the V5.2 per-range calibration scheme (Full + Range 1–4) carry into ME? |
| **16** | **Synchronised vs independent execution.** BM gives two distinct mechanisms, both ME-relevant: **SYNCLine/SYNCProgram** (barrier synchronisation across a SyncGroup — all circuits wait at the step until every member arrives, then resume together; SYNCProgram additionally requires identical program name *and version*) and **PARALLEL** (electrical paralleling of circuits on one battery, master/slave, nominal value divided across members, members must be in STOP mode at start). Which does ME require? BM caps ME parallel at **15 slaves**; ME hardware caps at 8 — reconcile. Separately: does the Primary carrier have a battery-backed RTC, and is NTP available? |
| **17** | **Fault latch / acknowledge / clear semantics.** No source defines which faults latch, who may clear them, whether a clear requires operator action, and what escalates a Secondary fault to a system-level fault. Note BM's ERR semantics are precise and worth mirroring: ERR interrupts the step, writes a numbered message, sets the Error LED and error relay, and **resumes at the same position in the same step** on operator Start. Also: is a Primary-resident event log / audit trail required independently of the Web App's? |
| **18** | **Telemetry rate budget for 8 nodes.** HW asks for ≤500 µs registration (max 1 ms by master); implementation runs 100 ms registration / 500 ms real-time; BM's own floor is 0.1 s registration resolution and 100 ms limit checking. What per-channel rate must ME sustain across 8 Secondaries simultaneously, and what end-to-end latency to the Web App is acceptable? |

### Hardware-derived detail

| # | Question |
|---|---|
| **19** | **Program-controlled digital outputs.** HW adds *"To be explored: turn ON/OFF DO from program (New Requirement)"* against DO4–DO11. In scope? If yes, via BM's `OUTA`/`OUTB` — whose semantics are in the **missing** operator PDF (#27). |
| **20** | **Health-Check channels 1–5** (HW Configuration #23–42: lower limit, upper limit, warning limit, hysteresis per channel, all defaulting to 4096). What physical quantity is each channel, and what action follows a warning vs a limit breach? |
| **21** | **Unresolved hardware queries.** HW's "Queries & Implement" sheet lists 10 open hardware questions (+5 IO; 3.3V_A; V24TX/V24RX; IO_start/IO_ON; DIP S1 purpose; can *active* become an *error* output; contactor_ACK at X4-11; DCH on motherboard / 5-pin female X4; DI/DO expansion; datalogger expansion). Which are resolved, and how? Several bear directly on S17 and #22. |
| **22** | **Secondary node identity source and I/O counts.** How does a Secondary learn its address (DIP switch? one-time provisioning? EEPROM)? Confirm actual DI/DO/relay counts — LSRS says 4 DI + 8 DO + 3 relays, HW says 4 DI + 11 DO (**C-11**). Also NV-config integrity/wear policy and restore-to-defaults behaviour. |
| **23** | **Power-fail behaviour.** HW lists "POWER FAIL ACTION", "PROGRAM START AFTER PFA" and "DATA SAVE AFTER PFA" as *pending*. Define hold-up time, what must be persisted, and whether a program auto-resumes after restart or requires operator confirmation. BM §12.5.3 documents the manual equivalent (SET counters to last registration, GOTO the abort label) — is automatic resume required, or is the BM manual-restore model acceptable? |
| **24** | **System-level supervision.** What cross-channel conditions must the Primary supervise (total power draw, shared rectifier limits, thermal), and what is the escalation policy from a single-channel fault to a system-level shutdown? |
| **25** | **Program storage on the Primary.** How many programs must be resident concurrently, must they survive reboot, and is program versioning/rollback required? (Note SYNCProgram in #16 is version-sensitive, so program *version identity* may be functionally load-bearing.) |
| **26** | **MISRA-C.** Required? For which board(s) and which modules — or safety-critical modules only? |

### Raised while sourcing the SoC datasheet and BM_Manual Ch.12

| # | Question |
|---|---|
| **27** | **`BM_PM_BTS600_New_Operators.pdf` is referenced by BM §12.4.2 but is not in the reference set.** It is the *only* documented source for eight operators: **`PAUA`, `PAUO`, `URANGE`, `IRANGE`, `OUTA`, `OUTB`, `ISOEXT`, `ISOINT`**. Three of these are directly implicated elsewhere — `IRANGE`/`URANGE` in the auto-ranging conflict (**C-12**), `OUTA`/`OUTB` in program-controlled DO (**#19**). Can this PDF be supplied? If not, these eight operators must be declared out of scope or specified from Ador's own definition — they **cannot** be written from the available sources. |
| **28** | **Analog input on the Primary Board.** The i.MX 8M Plus has **no general-purpose ADC** (§0.3). Does the ME Primary need any analog acquisition of its own (system rail monitoring, cabinet temperature, power-fail threshold sensing)? If yes, what external device provides it, and on which bus? |
| **29** | **External CAN and RS485/Modbus — in ME scope at all?** With the COM Controller deleted, LSRS's ~100 external-CAN/DBC requirements and its 2 RS485/Modbus ports have no dedicated host. The SoC allows **at most one spare FlexCAN** after the Secondary bus (**C-16**) and has 4 UARTs (**C-17**). Three options: (a) drop external CAN/Modbus from ME entirely; (b) keep a reduced set within silicon limits — state which; (c) add external controllers (e.g. SPI/PCIe CAN) — a hardware change. This single answer moves the Primary SRS size by roughly 100 requirements. |
| **30** | **TSN / IEEE 1588.** The SoC provides one plain Gb Ethernet (`ENET`, 1588) and one TSN-capable Gb Ethernet (`ENET_QOS`, 802.1Qbv/Qbu). Is deterministic Ethernet required for the Web-App link or for time distribution, or is best-effort TCP/UDP sufficient? Bears on D-05 and on P17. |
| **31** | **Registration-format compatibility.** BM defines 8 preset registration formats with exact byte costs and a user-defined format mechanism (Appendix A.2). Must ME reproduce these named formats and their byte layouts for BM4 compatibility (linked to **#1**), or may ME define its own? Drives P15 storage sizing and the IF-A telemetry message set. |

---

## 5. Conflict Register

Per the brief, every source conflict is flagged rather than silently resolved.
Declared precedence is applied where possible; where the *hardware spec conflicts
with itself*, precedence cannot resolve it and the item escalates to an open question.

| ID | Conflict | Sources | Disposition |
|---|---|---|---|
| **C-01** | Analog-output resolution: **24-bit DAC** (HW column) vs **16-bit** (HW remark) vs **20-bit** (APL remark 10-Feb-2026) vs **≥18 bits** (LSRS SW_REQ_167) | HW §5; LSRS | HW internally inconsistent → escalate. `<TBD-01>` |
| **C-02** | Reference-signal range: **−10…+10 V** (HW) vs **−2…+2 V** (ADPL remark) vs **0…10 V / −10…0 V** (LSRS SW_REQ_166, 170/171, 181/182) | HW §5; LSRS | HW internally inconsistent → escalate. `<TBD-02>` |
| **C-03** | LNT/ZNT ADC width: **12-bit internal ADC** (HW column) vs **"Internal 16-Bit ADC is used for LNT and ZNT"** (HW remark) | HW §4.2a/2b | Remark is later-dated → prefer 16-bit, confirm. `<TBD-03>` |
| **C-04** | Charge↔discharge transition: **10 ms for Imax** (HW §6) vs **measured 23 ms** (ADPL) vs **500 ms Switch-Over Delay** (LSRS SW_REQ_178, 184) | HW §6; LSRS | HW wins on precedence, but 500 ms is a *safety* delay for bank switching, not the same quantity. Needs reconciliation. `<TBD-04>` |
| **C-05** | Data-registration time: **≤500 µs** (HW §6) vs **10 ms** (HW remark) vs **"approve up to 1 ms"** (APL) vs **100 ms** implemented. **A fifth datum:** BM's own maximum registration resolution is **0.1 s**, and limit checking runs at **100 ms** | HW §6; CODE-S; **BM §12.4.6, §12.4.4** | Five values. BM's floor suggests sub-millisecond registration may be an over-specification. Drives **#18**. `<TBD-05>` |
| **C-06** | Secondary bootloader: **required** (HW §8) vs **"not required as discussed earlier"** (ADPL remark) vs **required over CAN** (ME brief P20/S16) | HW §8; brief | Brief overrides for ME scope, but confirm → **#9** |
| **C-07** | Battery voltage range: **≤100 V** (HW §4.4, LSRS) vs **"upto 80 V in current set-up"** (APL) vs **Vmax default 18 V** (HW Config #7) | HW; LSRS | Spec vs current rig vs default — distinguish *capability* from *configured value*. `<TBD-07>` |
| **C-08** | Maximum current: **≤200 A** (LSRS SW_REQ_144/149) vs **Imax default 100 A** (HW Config #5) | HW Config; LSRS | Capability vs default. `<TBD-08>` |
| **C-09** | Document revision: filename says **V1.04**; Version sheet's last entry is **1.03, dated 9-Feb-24**, while remark columns are dated **10-Feb-2026** | HW Version sheet | Brief states this file *is* current. Version sheet not maintained — note only. |
| **C-10** | CAN bit rate: **250 kbps** (HW Config #14, Primary↔Secondary) vs **125 k / 250 k / 500 k / 1 M** (LSRS SW_REQ_49, external CAN) | HW Config; LSRS | **Different buses** — must not be conflated. ME internal CAN = **D-04**. |
| **C-11** | Digital-output count: **4 DI + 8 DO + 3 relays** (LSRS) vs **4 DI + 11 DO** (HW §2/§3). ADPL further notes discharge relay "not used" on dual bank and no motherboard DI connections | HW §2/§3; LSRS | New board → HW wins, but counts must be confirmed → **#22** |
| **C-12** | Auto-ranging scheme: **4 ranges at 50 % / 10 % / 1 % / 0.1 % of Imax** (HW §14) vs **gains 1/2/4/8 → 100 % / 50 % / 25 % / 12.5 %** (LSRS SW_REQ_158–163; CODE-S `ccScaleRanges_t`). **Additionally:** BM §12.4.8.13 specifies ranges **1–4** (only 1–2 for IGBT systems), `RANGE 0` = auto, auto-select by lowest sufficient boundary, **no auto-range within a step**, none with ramps/power/TABLE, and ~**50 ms** transit between ranges | HW §14; LSRS; CODE-S; **BM §12.4.8.13** | Three incompatible schemes. HW also notes auto-ranging is **currently disabled** (PID interaction) and asks for changeover hysteresis. `<TBD-12>` |
| **C-13** | Cycle nesting: BM §12.5.1 says *"it is also possible to **interlace up to 16 different cycles**"* vs implementation **single active loop** (GAP) vs in-progress `PRIM_MAX_CYCLES` 16 / `PRIM_MAX_NEST_DEPTH` 4 | BM §12.5.1; LSRS; CODE-P/S; GAP | Direct reading confirms BM's wording is "**16 different interlaced cycles**", which reads as 16 *distinct* cycles rather than 16 *levels*. Clarify which ME must support. `<TBD-13>` |
| **C-14** | **Four incompatible message/error code spaces:** HW Error sheet (sequential 1…0x17, 23 of 100) · firmware `en_ERR_*` 17-bit bitmask (different order; mirrored in WAD/ICD §9.2) · BM §2.12 ER1–ER10 · **BM §12.4.8.5 user message numbers 1–6, operator-extensible via *Maintenance → BTS-600 Messages*** | HW; CODE-P/S; WAD; **BM** | Widened by direct reading of BM_Manual Ch.12. The BM message space is *user-editable at runtime*, which is a different kind of identifier from a fixed fault code — they must not be merged. Must be unified or explicitly mapped. High impact on P12/S13. `<TBD-14>` |
| **C-15** | Temperature sensor: **PT1000 / AD590** (HW §4.6) vs calibration parameter **"Gain Temp AD590/PT100"** (HW Calibration #17) vs firmware `RTD_100_OHM 100.0f` (**PT100**) vs remark *"Need to test with Actual PT100 sensor"* | HW; CODE-S | HW internally inconsistent (PT1000 vs PT100 in one workbook). `<TBD-15>` |
| **C-16** | **NEW.** External CAN port count: **3 FDCAN** (legacy COM Controller, CODE-C) vs **2 FlexCAN total on the SoC**, of which **1 is consumed by the 8-Secondary bus**, leaving **at most 1** | CODE-C; **SOC §0.3**; brief | Hardware-imposed; cannot be resolved in software. → **#29** |
| **C-17** | **NEW.** RS485/Modbus: **2 dedicated ports** on the legacy COM Controller (`ModSlave.c`, `RS485_Slave.c`, `RS485_Master.c`) vs **no dedicated MCU in ME**; 4 SoC UARTs exist but require external transceivers and are shared with console/debug | CODE-C; **SOC §0.3**; brief | Feasible but unallocated. → **#29** |
| **C-18** | **NEW.** Primary analog input: LSRS carries analog-in requirements inherited from the SAM9X60 design vs **i.MX 8M Plus has no general-purpose ADC** (§0.3) | LSRS; **SOC §0.3** | Requires external device or removal from scope. → **#28** |

---

## 6. Assumptions (to be confirmed)

| ID | Assumption |
|---|---|
| **A-01** | Reference documents are at `…\Projects\MicroME\ME Workspace\Reference Documents`; deliverables go to `…\Projects\MicroME\ME Workspace\Requirements`. (Both differ from the paths in the brief — §0.1.) |
| **A-02** | "Secondary Board" ≡ the Digital Controller Transcard of the hardware spec, which refers to the Primary as "Main Controller". |
| **A-03** | Because the hardware spec is Secondary-only, the effective top source for the **Primary** SRS is the NXP datasheet (SoC level) then BM_Manual, then legacy SRS, then WebAppDocs and legacy code as corroborating evidence. Board-level gap flagged as **#3**. |
| **A-04** | Web-App-facing functional behaviour carries forward from the `WebAppDocs` baseline except where ME's topology change requires otherwise. Subject to **#2**. |
| **A-05** | One Secondary Board = one test circuit/channel. (WebApp DBD/ICD model a Device as possibly holding 1–2 circuits — subject to **#15**.) |
| **A-06** | Legacy code is treated strictly as *evidence of intent to be confirmed*, never as a requirement. Where CMMI `03_SRS.md` and the code disagree, code plus GAP findings are the factual baseline. |
| **A-07** | `PSPE` is recorded as prior art bearing on **D-03** and is **not** adopted; no requirement will presuppose its partition. |
| **A-08** | Every Primary requirement receives `Allocation: TBD (see D-01)` unless the hardware spec forces the allocation, in which case the source is cited. **The Cortex-M7 running an RTOS is recorded as a given constraint and is explicitly NOT treated as an answer to D-01, D-02 or D-06.** |
| **A-09** | Where a checklist area has no source, the SRS will state *"No requirements identified — see Open Issue #N"* rather than inventing content. |
| **A-10** | **NEW.** The NXP datasheet `IMX8MPIEC` Rev.1 (industrial) is the correct reference for `MIMX8ML8CVNKZAB` — its ordering table lists the part explicitly. SoC capability is treated as a *constraint envelope*, not as a requirement: the SRS will not require a peripheral merely because the silicon has one. |
| **A-11** | **NEW.** Appendix A is a faithful transcription of BM_Manual pp.156–219 as the functional baseline for ME program execution, subject to **#1** (whether BM4 compatibility is contractual) and **#5** (which subset is in Phase 1 scope). |

---

## 7. Proposed requirement-count envelope

Indicative only — for sizing the review effort, not a commitment.

| Document | Est. requirements | Densest areas |
|---|---|---|
| SRS — ME Primary Board | ~260–320, **+~100 if #29 answer is (b) or (c)** | P7 (~60), P5 (~30), P21 (~25), P18 (~25), P10 (~20) |
| SRS — ME Secondary Board | ~180–220 | S8 (~35), S9 (~30), S5/S6 (~30), S14 (~25) |
| ICD — ME Interfaces | ~70–90 messages | IF-A ~45, IF-B ~35, NEW-ME ~10 |

---

## 8. Confirmation requested

Before the full SRS set is generated, please confirm:

1. **Coverage assessment** — are the ● / ◐ / ○ ratings in §1 and §2 acceptable, and is any ○ area actually sourced somewhere not supplied?
2. **Open Issues #1–#31** (§4) — answers, or instruction to carry them into the SRS registers as-is.
3. **Conflicts C-01…C-18** (§5) — resolutions, or instruction to flag and carry `<TBD-nn>` forward.
4. **Assumptions A-01…A-11** (§6).
5. **Priority items.** Four questions change the *shape* of the deliverable rather than its detail, and are worth answering even if the rest are deferred:
   - **#1** — is BM4 "ME" compatibility a contract or a reference?
   - **#29** — external CAN / Modbus in scope? (±100 requirements)
   - **#27** — can the missing operator PDF be supplied? (8 operators otherwise unspecifiable)
   - **#5** — which operators are in Phase 1?

---

# Appendix A — BTS-600 functional baseline (BM_Manual pp.156–219, read in full)

Transcribed from `BM_Manual_eng 2.pdf` Ch.12, printed pages 156–219 (1:1 with PDF
pages). This appendix exists so that the SRS can cite specific BM constructs, and so
the architect can mark Phase-1 scope directly against a complete list.

## A.1 Operator catalog — 34 documented + 8 undocumented

### Standard operators (§12.4.2)

| Operator | Columns used | Function |
|---|---|---|
| `SET` | Nominal Value, Registration | Sets registration formats, counters, variables, timers, global limits |
| `PAU` | Limit | Pause — power contactors dropped until limit reached |
| `CHA` | Nominal Value, Limit | Charge with nominal value until limit; **charge relay set** |
| `RCH` | Nominal Value, Limit | Recharge — as CHA, plus recharge relay set; **charge LED blinks** |
| `DCH` | Nominal Value, Limit | Discharge with nominal value until limit; **discharge relay set** |
| `INT` | — | Interrupt program; resume via front-panel Start or host |
| `STO` | — | Stop program; usable in sub-programs, may appear several times |
| `BEG` | (Nominal Value) | Cycle beginning; cycle name in Nominal Value |
| `CYC` | Nominal Value | Cycle end; nominal value = number of cycles (`10*`) |
| `GOTO` | Operator / Action | Jump; destination declared in Label column |
| `REG` | Nominal Value, Registration | Register once in the given format |
| `TASK` | — | Activate parallel process (relays, temperature, water level…) |
| `RET` | Action | Return from procedure to caller / next higher level |
| `SETMUX` | Operator | Activate display of additional multiplexer channels (serial acquisition) |
| `FILE` | Nominal Value | Set test-section (file) name for subsequent registrations |
| `ADD` | Nominal Value | Add figure values of the named channels |
| `SUB` | Nominal Value | Subtract second named channel from the first |
| `BATT` | Nominal Value | Battery simulator — constant V + upper/lower current limits (units `V`, `A`, `AG`) |
| `ONERROR` | Nominal Value | Declare error-handler label |
| `ONEXIT` | Nominal Value | Declare end-of-program handler label |
| `PROT` | — | Launch external protocol program; passes `circuit;battery;session-ID` |
| `ALIM` | — | Honour sign of current limits in all following steps (default ignores sign) |

### Special operators (§12.4.2 second table, detailed in §12.4.8)

| Operator | Function | Detail §|
|---|---|---|
| `ERR` | Message **with** program interruption; sets Error LED + error relay | 12.4.8.5 |
| `MSG` | Message **without** interruption | 12.4.8.6 |
| `TABLE` | Load nominal-value list from PC disk | 12.4.8.9 |
| `CLEAR` | Delete SET-entered registrations, limits, global setpoints (**not** the registration format) | 12.4.8.10 |
| `FILTER` | Set digital filters per channel (**definable only by Digatron personnel**) | 12.4.8.11 |
| `SYNCLine` | Barrier sync across SyncGroup, ignoring program identity | 12.4.8.16 |
| `SYNCProgram` | Barrier sync across SyncGroup, **same program name + version only** | 12.4.8.16 |
| `SAVE` | Store all battery counters as a named test section | 12.4.8.7 |
| `REST` | Restore counters previously stored by `SAVE` | 12.4.8.8 |
| `RANGE` | Manual/automatic current-range switching | 12.4.8.13 |
| `PARALLEL` | Electrical paralleling of circuits, master/slave | 12.4.8.14 |
| `EIS` | Electrochemical impedance spectroscopy step | 12.4.8.15 |

### ⚠ Operators documented only in the missing companion PDF

`PAUA` · `PAUO` · `URANGE` · `IRANGE` · `OUTA` · `OUTB` · `ISOEXT` · `ISOINT`
→ Open Issue **#27**.

## A.2 Registration formats (§12.4.1)

| Name | Units registered | Bytes per registration |
|---|---|---|
| `SIMPLE` | A, V | 12 |
| `STANDARD` | A, V, C, Ah, AhStep, Wh, WhStep | 42 |
| `CHANREG` | A, V, C, Ah, AhStep, Wh, WhStepV, V1 (all logger V1 channels) | 42 + 6 × channels |
| `CHAREG` | A, V, C, Ah, AhCha, AhStep, Wh, WhCha, WhStep | 54 |
| `DCHREG` | A, V, C, Ah, AhDch, AhStep, Wh, WhDch, WhStep | 54 |
| `CYCLE` | A, V, C, Ah, AhBal, AhStat, AhCha, AhDch, AhStep, AhPrev, Wh, WhCha, WhDch, WhStep, WhPrev | 90 |
| `CYCLEC` | as CYCLE + V1 | 90 + 6 × channels |
| `GSM` | A, A_Diff, A_Low, Ah, V, V_Drop, V_High, V_Low | 48 |

User-defined formats can be created, modified and deleted; modification affects every
program using that format. Format may be changed mid-program by a new `SET`, or for a
single step by naming a format in that step's Registration column. Inside a procedure,
a format change is **local to that procedure level**.

**Logical channels:** `Ah`/`Wh` (total, negative when discharge-dominant) ·
`AhStat` = AhCha / charge-factor − AhDch · `AhBal` = AhCha − AhDch, floored at 0 ·
`AhStep`/`WhStep` (current step) · `AhPrev`/`WhPrev` (previous step) ·
`AhCha`/`WhCha` (all charge steps so far) · `AhDch`/`WhDch` (all discharge steps so far).

## A.3 Registration control — `RLevel` (§12.4.1)

| Value | Behaviour |
|---|---|
| `0.0` | All registrations deactivated |
| `1.0` | Step changes only, no data registrations |
| `2.0` | All registrations (data + step change) |
| `3.0` | Step-change registrations suppressed; data registered only at step **end** |
| `4.0` | Step-change registrations suppressed; **no** data registration at step end |

## A.4 Nominal values (§12.4.3)

**Standard (CHA/RCH/DCH):** `A` current · `V` voltage · `Watt` power · `Ohm` resistance.
*A nominal current, power or resistance is always mandatory — voltage alone is invalid.*
(Note: `W` is rejected; the system accepts only `Watt`.)

**Battery-relative:** `ACn1`, `ACn2`, `ACn4`, `ACn5`, `ACn10`, `ACn20` (multiples of
CNom / n-hour rate) · `VnC` (volts per cell × NoCell). Not usable for ramps.

**Ramps:** start–end value plus duration, e.g. `1000-5000 Watt 300 sec`; applicable to
current, voltage, power, resistance, or combinations.

**Parallel (two nominal values in one step):** e.g. `CHA 100 V / 15 A` — CC until the
voltage target, then CV with current tapering. Neither value may be exceeded.

**Special:** `Factor_I` / `Factor_P` / `Factor_U` (table scaling) · `OUW` (enable R and
P computation, via TASK) · `CGRE` / `CLESS` / `TCONTR` (temperature-dependent relay on
`IOOUT[16]` = Rel. 8) · `TIMER1`–`TIMER3` · global limits `MaxCHAI`, `MaxDCHI`,
`MaxChaU`, `MaxDchU`, `MaxChaW`, `MaxDchW` · `Number` (message number for ERR/MSG) ·
file names for `TABLE`/`REG`/`FILE` · filter names.

**Battery parameters usable in Nominal Value, Limit and Registration (§12.3):**
`CNom` (nominal capacity), `NoCell`, `UGas` (gassing voltage), `UMax`, `INom`,
`ICrank` (cold-cranking current), `ChargeF` (charge factor), `Rin` (internal
resistance), `CutOff`, `UNom`, `EDensity` — internally `INTERN[17]`…`INTERN[27]`.
Channel *units* may be used; channel *names* may not.

## A.5 Limits (§12.4.4)

| Form | Meaning |
|---|---|
| `10 sec` / `1.9 min` / `3.8 h` / `hh:mm:ss` | Elapsed step time (integers with decimals) |
| `< 10.0 K` | Lower limit on channel K |
| `> 10.0 K` | Upper limit on channel K |
| `10.0 X` | Delta — change of X since step start exceeds 10 |
| `< 10.0 X &` | AND-link to the next limit line; action goes on the **last** line of the chain |
| `> 10 GradT` / `GradTm` | Temperature rise per hour / per minute |
| `GradU`, `GradUm`, `GradI` | Voltage/current gradient; **max one channel, requires device adjustment** |
| `deltaV` | dChannel/dt for voltage or GREAL channels; uses `GTIMER[1]`, `GREAL[100]`=TimeIdx ms, `GREAL[101]`=ChanIdx, `GREAL[102]`=ChanNum; can scan GREAL[100]–[119] |
| `Timer1`–`Timer3` | Step ends when the named timer expires |
| `> 1.0 AhDef` | Define present Ah as 100 % reference (stored in `GREAL[400]`); step ends immediately |
| `> 80 PercAh` | Ah counter reaches 80 % of the AhDef reference |
| `PERCCN_P` | Capacity at end of **previous** step reaches given % of CNom |
| `PERCCN_C` | Capacity in the **present** step reaches given % of CNom |
| `A_notAbs`, `mA_notAbs` | Sign-respecting current limits (plain `A` limits ignore sign) |
| `ABATT`, `VBATT` | Customer-specific switch function / battery voltage |
| `ACN5`, `VNC`, `OHM`, `WATT` | Derived quantities usable as limits |

Constraints: `AhDef`, `PercAh`, `PERCCN_P`, `PERCCN_C` are valid **only with the `PAU`
operator**. Discharge limits must carry a negative sign. Limit channels grouped by unit
(e.g. `> 12 V1`) are ANDed within an &-chain.

**Timing:** *"The check of these limits takes place with the maximum measuring speed,
i.e. every 100 ms."* — the only explicit control-loop timing figure in the BM manual.

## A.6 Actions (§12.4.5)

| Action | Effect |
|---|---|
| *(blank)* | Proceed to the next step |
| `INT` | Interrupt the program |
| `STO` | Stop **this** program only — *the dispo list continues with the next test* |
| `GOTO <label>` | Jump to the labelled step (**same program level only**) |
| `<procedure name>` | Run the procedure, then continue with the next step |
| `ERR` | Write numbered message, **interrupt**; resume re-enters the *same position in the same step* |
| `MSG` | Write numbered message, **do not** terminate the step |

Default message numbers: 1 *Limit reached!* · 2 *Voltage limit reached!* ·
3 *Temperature limit reached!* · 4 *Current limit reached!* · 5 *Capacity limit
reached!* · 6 *External control signal activated!* — editable and extensible at runtime
via **Maintenance → BTS-600 Messages**.

A limit always terminates the running step **except** when the action is `MSG` or `ERR`.

## A.7 Registration triggers (§12.4.6)

| Type | Example | Meaning |
|---|---|---|
| Delta | `2.0 A`, `5 min` | Register on 2 A change; register every 5 min |
| Threshold | `< 0.5 A`, `> 50.0 C` | Register when crossing the threshold |
| Range | `>> 1 h & 2.0 V`, `<< -100.0 Ah & 1.0 C` | Conditional delta registration after a gating condition |
| Maximum | `15 *`, `>> 10000 Watt 5*`, `>> 30 sec 1*` | Fixed count at highest resolution — **intervals of 0.1 s** |

Blank column ⇒ register at step start and end only. Types may be combined in one step;
entry order is insignificant.

## A.8 Structuring elements (§12.5)

- **Cycles (§12.5.1):** `BEG` … `CYC n*`; cycle named in Nominal Value; count may be a
  SET variable. *"It is, of course, also possible to interlace up to 16 different cycles."*
- **Procedures (§12.5.2):** created from selected steps, stored under their own program
  number, invoked by name in the Operator column, returned from with `RET`. Editing a
  procedure affects every program using it. Decompose/recompose supported. GOTO may not
  cross into or out of a procedure.
- **Counters and GOTO (§12.5.3):** `SET` restores cycle/Ah/Wh counters after a stop
  (Ah/Wh must be entered **negative**); `GOTO` resumes at the abort label. GOTO may be
  an Operator (destination in Nominal Value) or an Action (destination in Action).
  Conditional branching = multiple limits each with its own GOTO action.
  **Reserved label names** (may not be used): `V, A, mA, Ah, mAh, AhCha, AhLad, mAhCha,
  mAhLad, AhDch, AhEla, mAhDch, mAhEla, AhStep, AhPas, mAhStep, mAhPas, AhPrev, AhPrec,
  mAhPrev, mAhPrec, AhStat, mAhStat, AhBal, mAhBal, Wh, mWh, WhCha, WhLad, mWhCha,
  mWhLad, WhDch, WhEla, mWhDch, mWhEla, WhStep, WhPas, mWhStep, mWhPas, WhPrev, WhPrec,
  mWhPrev, mWhPrec`.
- **Special labels (§12.5.4):** `ONERROR` — jump target on error; handler placed at
  program end; **must begin with `CLEAR`**. `ONEXIT` — steps executed after program end.

## A.9 Operator detail worth carrying into requirements

- **`TASK`** — up to **12** parallel processes per program. With a Safetask, `PARALLEL`
  must be declared *before* the `TASK` step.
- **`TABLE`** — line format `X;Y;Z;U;` = duration (`sec`/`min`/`h`, default seconds),
  current A, power W, voltage V; every value semicolon-terminated; 1–4 values per line.
  Discharge values negative. Time+voltage only ⇒ treated as a **charge** step regardless
  of sign. Filename ≤ 8 chars, `.TXT`, in `…\Server\BTS-600\Table`. **Minimum step time
  0.1 s for a single circuit**, dependent on total system load. Built-in profiles:
  `fudstbl` (FUDS), `sfudstabl` (SFUDS, W/kg), `dsttbl` (DST); FUDS is 1372 steps and
  restarts if no termination condition trips. Table values may be clamped with
  `Ap`, `An`, `Vp`, `Vn`, `Wp`, `Wn`.
- **`RANGE`** — auto-range enabled at every program start; picks the lowest range whose
  boundary suffices, or range 1 if no setpoint. `RANGE 1|2|3|4` (1–2 only for IGBT)
  latches manual mode for all following steps; `RANGE 0` restores auto. **No auto-range
  within a step**, and none with ramps, power setpoints or `TABLE`. Range change takes
  effect at the start of the next step; transit ≈ **50 ms** with current forced to 0.
- **`PARALLEL`** — **MBT: max 7 slaves; ME: max 15 slaves.** Not available for TEFEU.
  All participating circuits must be in STOP mode at start and are then reserved.
  Nominal value is divided across members. Master completing normally leaves itself in
  STOP and still active in the dispo; only a **manual stop** releases parallel mode.
  Documented failure cases: *Current below set value!* (outputs not connected), setpoint
  above summed maxima, slave outside the master's cabinet, slave already running.
  Recommended placement: first program line.
- **`EIS`** — nominal values `ADC` (>0 charge, <0 discharge, 0/absent = pause),
  `VDC` (requires ADC), `mHz`/`Hz`/`kHz` (one value = single frequency; two = spectrum,
  **8 measurements per decade**), `AAcMax` (default 2 A), `VAcMax` (default 20 V),
  `VAcMin` (default −2 V), `mVideal` (default 10 mV; 3–10 mV/cell recommended),
  `EIStime` (default 10 s, minimum 3 periods), `EISperi` (default 3),
  `AACstart` (default 20 mA). Limit must be a time greater than the measurement duration.
- **`SAVE` / `REST`** — REST requires the matching named test section to exist in the
  database and the same registration format to be declared beforehand; otherwise the
  running program **waits or is interrupted**.
- **Comment / disable (§12.1)** — `!` in the Label column turns a line into a comment or
  disables an existing step (displayed yellow).
- **Global limits (§12.4.8.2)** — apply to every step; a global limit **without** an
  action terminates the program when reached.

## A.10 Safety statements from §12.4 (carry into the Safety NFR section)

The manual states explicitly that program limits *"should not be the only security
measures in a system"*; that damage prevention is the user's responsibility, *"particularly
in the case of unattended test processes"*; and recommends a pause step of several seconds
at the start of every program. It also warns that `STO` stops only the current test while
the dispo list continues — *"this may cause an exhaustive discharge!"*
