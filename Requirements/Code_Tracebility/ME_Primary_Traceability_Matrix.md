# ME Primary - Requirement to Code Traceability Matrix

> GENERATED FILE - do not hand-edit.
> Regenerate with `python Requirements/Code_Tracebility/tools/build_traceability.py`.
> Source of requirement text: `ME_Primary_SRS_V0.1` (read live, never copied).
> Source of verdicts: `tools/trace_status.py`.
> Code state as of: 2026-08-26, branch `develop`.

One row per requirement, in SRS order. `Verdict` is where the `ME Project/me-primary` code actually stands; `Evidence` either names the file or symbol that proves it or names the gap that denies it; `WP` is the work package in [ME_Work_Packages_and_Estimates.md](ME_Work_Packages_and_Estimates.md) that owns the remaining work.

`Pkg est` is the effort of the WHOLE owning work package in engineer-days (implement + hardware verification), not a per-requirement figure. Requirements in a package are not independently schedulable, so dividing that number by the row count would be a fiction. Use the work package document for planning and this column only for cost context while reading a row.

## Status summary

| Verdict | All 353 requirements | Of the 302 that bind the Primary |
|---|---:|---:|
| **DONE** | 19 | 19 |
| *PARTIAL* | 42 | 42 |
| TODO | 167 | 167 |
| **BLOCKED** | 52 | 52 |
| N/A-BM | 50 | 0 |
| DEFERRED | 8 | 7 |
| INFO | 15 | 15 |
| **Total** | **353** | **302** |

Read that as: of the 302 requirements that place an obligation on the Primary board, **19 are fully met and evidenced**, **42 are partially met with the gap named**, and **219 are untouched or blocked**. 65 requirements carry no code obligation at all - 15 are Information rows and 50 are Battery Manager scope - and 8 are Phase 2.

The honest headline is that **261 of 302 Primary-binding requirements still need work** (86%). What is already done is not the easy part - it is the entire host link, the frame layer, the addressing model, the storage model and a working single-channel execution path, all host-tested and much of it hardware-verified. What remains is breadth: 7 of the 24 SRS sections have no code behind them at all (1.0, 2.0, 3.0, 5.0, 7.0, 8.0, 23.0).

### By category

| Category | **DONE** | *PARTIAL* | TODO | **BLOCKED** | N/A-BM | DEFERRED | INFO | Total |
|---|---|---|---|---|---|---|---|---|
| Capacity | 0 | 4 | 0 | 5 | 0 | 0 | 0 | 9 |
| Configuration | 0 | 0 | 38 | 3 | 20 | 2 | 1 | 64 |
| Constraint | 1 | 0 | 2 | 0 | 0 | 0 | 0 | 3 |
| Data | 3 | 4 | 15 | 5 | 14 | 0 | 0 | 41 |
| Diagnostic | 1 | 2 | 19 | 3 | 0 | 0 | 0 | 25 |
| Functional | 3 | 13 | 41 | 11 | 1 | 1 | 6 | 76 |
| Interface | 8 | 5 | 22 | 6 | 1 | 4 | 8 | 54 |
| Performance | 1 | 4 | 1 | 6 | 0 | 0 | 0 | 12 |
| Safety | 2 | 9 | 26 | 13 | 2 | 1 | 0 | 53 |
| Usability | 0 | 1 | 3 | 0 | 12 | 0 | 0 | 16 |

## Matrix


### 1.0   Digital Inputs

*10 requirement(s) - TODO 5, BLOCKED 1, N/A-BM 1, DEFERRED 1, INFO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_1` | Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_2` | BM + Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_3` | Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-input path. | WP-P14 | 5d |
| `ME_SW_REQ_4` | Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-input path. | WP-P14 | 5d |
| `ME_SW_REQ_5` | Primary | Functional | Draft | **BLOCKED** | Needs TBD-01 (time-stamp resolution) before it can be built or tested. | WP-P14 | 5d |
| `ME_SW_REQ_6` | BM | Configuration | Pending | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_7` | Primary | Functional | Draft | TODO | No GPIO code exists. `src/` has no digital-input path. | WP-P14 | 5d |
| `ME_SW_REQ_8` | BM + Primary | Interface | Pending | TODO | No GPIO code exists. `src/` has no digital-input path. | WP-P14 | 5d |
| `ME_SW_REQ_9` | BM + Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-input path. | WP-P14 | 5d |
| `ME_SW_REQ_10` | BM + Primary | Interface | Deferred | DEFERRED | Phase 2. | WP-P18 | 21d |

### 2.0   Digital Outputs

*11 requirement(s) - TODO 6, BLOCKED 1, N/A-BM 1, DEFERRED 1, INFO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_11` | Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_12` | BM + Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_13` | Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-output path. | WP-P15 | 5d |
| `ME_SW_REQ_14` | Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-output path. | WP-P15 | 5d |
| `ME_SW_REQ_15` | Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-output path. | WP-P15 | 5d |
| `ME_SW_REQ_16` | BM | Configuration | Pending | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_17` | Primary | Functional | Draft | TODO | No GPIO code exists. `src/` has no digital-output path. | WP-P15 | 5d |
| `ME_SW_REQ_18` | BM + Primary | Interface | Pending | TODO | No GPIO code exists. `src/` has no digital-output path. | WP-P15 | 5d |
| `ME_SW_REQ_19` | BM + Primary | Interface | Draft | TODO | No GPIO code exists. `src/` has no digital-output path. | WP-P15 | 5d |
| `ME_SW_REQ_20` | BM + Primary | Interface | Deferred | DEFERRED | Phase 2. | WP-P18 | 21d |
| `ME_SW_REQ_21` | BM + Primary | Safety | Draft | **BLOCKED** | Needs TBD-02 (safe-state time) and OI-05 (what 'safe' means). | WP-P15 | 5d |

### 3.0   Relays   (context only - relay control is Secondary Board scope)

*2 requirement(s) - BLOCKED 1, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_22` | Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_23` | BM + Primary | Safety | Draft | **BLOCKED** | Needs TBD-03 (dead time) and OI-06 (who sequences it). | WP-P28 | 4d |

### 4.0   Real Time Clock

*8 requirement(s) - PARTIAL 2, TODO 4, BLOCKED 1, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_24` | Primary | Functional | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_25` | Primary | Data | Draft | *PARTIAL* | `util/log.c` prefixes every line with a timestamp, but not the required Epoch + `YYYY-MM-DD HH:MM:SS` pair, and log lines are not the same thing as test-record time stamps. | WP-P13 | 7d |
| `ME_SW_REQ_26` | BM + Primary | Functional | Draft | *PARTIAL* | `0xEE` Q5 Sync Time is parsed with its big-endian epoch (`proto/control_frame.c`) but deliberately NOT applied - `core_logic.c` logs `not applied - the board clock is owned by Torizon OS`. | WP-P13 | 7d |
| `ME_SW_REQ_27` | Primary | Interface | Draft | TODO | No RTC device access. `util/log.c` timestamps log lines only. | WP-P13 | 7d |
| `ME_SW_REQ_28` | Primary | Safety | Draft | TODO | No RTC device access. `util/log.c` timestamps log lines only. | WP-P13 | 7d |
| `ME_SW_REQ_29` | Primary | Data | Draft | TODO | No RTC device access. `util/log.c` timestamps log lines only. | WP-P13 | 7d |
| `ME_SW_REQ_30` | Primary | Functional | Draft | **BLOCKED** | Needs TBD-04 (permitted Primary-to-Secondary offset). | WP-P13 | 7d |
| `ME_SW_REQ_31` | Primary | Data | Draft | TODO | No RTC device access. `util/log.c` timestamps log lines only. | WP-P13 | 7d |

### 5.1   RS485_1  -  Modbus RTU slave, towards a third party HMI

*6 requirement(s) - TODO 4, BLOCKED 1, N/A-BM 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_32` | Primary | Interface | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P16 | 9d |
| `ME_SW_REQ_33` | Primary | Interface | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P16 | 9d |
| `ME_SW_REQ_34` | BM + Primary | Configuration | Pending | TODO | No Modbus stack and no RS-485 port code exist. | WP-P16 | 9d |
| `ME_SW_REQ_35` | BM + Primary | Interface | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P16 | 9d |
| `ME_SW_REQ_36` | BM + Primary | Capacity | Draft | **BLOCKED** | Needs TBD-05 (64-channel register map) and OI-07. | WP-P16 | 9d |
| `ME_SW_REQ_37` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B10. | WP-B10 | 5d |

### 5.2   RS485_2  -  reading from third party devices

*10 requirement(s) - TODO 5, BLOCKED 5*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_38` | Primary | Interface | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P17 | 9d |
| `ME_SW_REQ_39` | BM + Primary | Configuration | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P17 | 9d |
| `ME_SW_REQ_40` | BM + Primary | Configuration | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P17 | 9d |
| `ME_SW_REQ_41` | BM + Primary | Configuration | Draft | **BLOCKED** | Needs TBD-06 (permitted data/stop/parity combinations). | WP-P17 | 9d |
| `ME_SW_REQ_42` | Primary | Interface | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P17 | 9d |
| `ME_SW_REQ_43` | Primary | Interface | Draft | **BLOCKED** | Needs TBD-07 (function codes). | WP-P17 | 9d |
| `ME_SW_REQ_44` | Primary | Interface | Draft | **BLOCKED** | Needs TBD-08 (response timeout). | WP-P17 | 9d |
| `ME_SW_REQ_45` | Primary | Capacity | Draft | **BLOCKED** | Needs TBD-09 (bus device count). | WP-P17 | 9d |
| `ME_SW_REQ_46` | BM + Primary | Configuration | Pending | **BLOCKED** | Needs TBD-10 and TBD-11 (slave address range). | WP-P17 | 9d |
| `ME_SW_REQ_47` | BM + Primary | Functional | Draft | TODO | No Modbus stack and no RS-485 port code exist. | WP-P17 | 9d |

### 6.0   Ethernet Port

*15 requirement(s) - DONE 3, PARTIAL 5, TODO 3, BLOCKED 2, DEFERRED 1, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_48` | Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_49` | BM + Primary | Interface | Draft | **DONE** | `net/tcp_client.c` - non-blocking connect with timeout, full-send, timed recv. Hardware-verified 2026-08-07 (T-6). | WP-P23 | 7d |
| `ME_SW_REQ_50` | Primary | Interface | Draft | **DONE** | IPv4 only, by design - `inet_pton` on dotted-quad literals, no `getaddrinfo` (ADR-8). | WP-P23 | 7d |
| `ME_SW_REQ_51` | Primary | Interface | Deferred | DEFERRED | Phase 2. | WP-P24 | 6d |
| `ME_SW_REQ_52` | BM + Primary | Interface | Pending | *PARTIAL* | The board does obtain a DHCP address, but that is Torizon OS, not me-primary. No DHCP control or reporting in the application. | WP-P24 | 6d |
| `ME_SW_REQ_53` | Primary | Interface | Draft | **DONE** | `platform/netinfo.c` `me_netinfo_read()` reads the real MAC via `SIOCGIFHWADDR`; MAC `00:14:2d:ef:86:e2` observed on hardware. | WP-P24 | 6d |
| `ME_SW_REQ_54` | BM + Primary | Interface | Draft | *PARTIAL* | The MAC is read and placed in the 33-byte `0xDD` registration payload (`proto/reg_frame.c`), so the Battery Manager receives it once. There is no query that lets it re-read it on demand. | WP-P24 | 6d |
| `ME_SW_REQ_55` | BM + Primary | Configuration | Draft | TODO | `0xDD` Q4 IP configuration is unimplemented (T-10). | WP-P24 | 6d |
| `ME_SW_REQ_56` | BM + Primary | Configuration | Draft | TODO | `0xDD` Q4 IP configuration is unimplemented (T-10). | WP-P24 | 6d |
| `ME_SW_REQ_57` | BM + Primary | Interface | Draft | *PARTIAL* | Programming (`0xBB` Q4) and live parameters (`0xCC` on UDP 10000) work. Configuration is limited to the `0xAA` Q5 battery record - Q1-Q4 and Q6 are specified but unanswered. Calibration (`0xA0`) is entirely unimplemented (T-12). | WP-P23 | 7d |
| `ME_SW_REQ_58` | BM + Primary | Performance | Draft | *PARTIAL* | One link does carry everything, but only 4 channels of one Secondary have ever been exercised (T-62). No per-channel fairness or starvation guarantee exists. | WP-P27 | 9d |
| `ME_SW_REQ_59` | BM + Primary | Interface | Draft | **BLOCKED** | Needs TBD-12 (session count) and OI-08 (multi-operator rule). | WP-P23 | 7d |
| `ME_SW_REQ_60` | Primary | Safety | Draft | *PARTIAL* | Structurally true - `core_logic.c` ticks every engine independently of the socket, and `comm_thread.c` reconnects with backoff without touching engine state. Never tested by pulling the link mid-test. | WP-P23 | 7d |
| `ME_SW_REQ_61` | Primary | Data | Draft | **BLOCKED** | Needs TBD-13 (buffering duration). | WP-P23 | 7d |
| `ME_SW_REQ_62` | BM + Primary | Data | Draft | TODO | Not addressed by the current host-link implementation. | WP-P23 | 7d |

### 7.0   CAN Interface  -  configurable ports towards external devices

*12 requirement(s) - TODO 9, BLOCKED 1, INFO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_63` | Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_64` | BM + Primary | Interface | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_65` | BM + Primary | Interface | Draft | **BLOCKED** | Needs TBD-14 (which port is the internal bus) and OI-09. | WP-P18 | 21d |
| `ME_SW_REQ_66` | BM + Primary | Interface | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_67` | BM + Primary | Interface | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_68` | BM + Primary | Interface | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_69` | BM + Primary | Functional | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_70` | BM + Primary | Interface | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_71` | BM + Primary | Interface | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_72` | BM + Primary | Interface | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_73` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_74` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 7.1   Configuration of CAN frames

*1 requirement(s) - TODO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_75` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 7.1.1   CAN Port Configuration

*7 requirement(s) - TODO 7*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_76` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_77` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_78` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_79` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_80` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_81` | BM + Primary | Diagnostic | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_82` | BM + Primary | Diagnostic | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 7.1.1.2   Subscribed CAN Messages

*5 requirement(s) - TODO 5*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_83` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_84` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_85` | Primary | Functional | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_86` | Primary | Functional | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_87` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 7.1.1.3   Published CAN Messages

*2 requirement(s) - TODO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_88` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_89` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 7.1.2   Message Configuration

*26 requirement(s) - TODO 17, N/A-BM 7, DEFERRED 1, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_90` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_91` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_92` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_93` | Primary | Functional | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_94` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_95` | BM + Primary | Configuration | Deferred | DEFERRED | Phase 2. | WP-P18 | 21d |
| `ME_SW_REQ_96` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_97` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_98` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_99` | Primary | Functional | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_100` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_101` | BM + Primary | Configuration | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_102` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_103` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_104` | Primary | Functional | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_105` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_106` | BM | Configuration | Need to check | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_107` | BM | Configuration | Need to check | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_108` | BM | Configuration | Need to check | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_109` | Primary | Functional | Need to check | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_110` | Primary | Functional | Need to check | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_111` | BM + Primary | Configuration | Need to check | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_112` | Primary | Diagnostic | Need to check | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_113` | BM + Primary | Configuration | Need to check | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_114` | BM + Primary | Safety | Need to check | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_115` | BM + Primary | Safety | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 7.1.3   Signal Configuration

*16 requirement(s) - TODO 12, N/A-BM 3, DEFERRED 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_116` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_117` | BM + Primary | Configuration | Deferred | DEFERRED | Phase 2. | WP-P18 | 21d |
| `ME_SW_REQ_118` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_119` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_120` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_121` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_122` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_123` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_124` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_125` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B09. | WP-B09 | 18d |
| `ME_SW_REQ_126` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_127` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_128` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_129` | BM + Primary | Data | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_130` | BM + Primary | Configuration | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |
| `ME_SW_REQ_131` | BM + Primary | Data | Draft | TODO | No user-configurable CAN layer. `can_frame.c` is the fixed internal-bus codec only, not a configurable port. | WP-P18 | 21d |

### 8.1.1   Max Battery Voltage

*4 requirement(s) - TODO 2, N/A-BM 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_132` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_133` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_134` | BM + Primary | Diagnostic | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_135` | BM + Primary | Safety | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |

### 8.1.2   Voltage Sampling Rate

*5 requirement(s) - TODO 2, BLOCKED 1, N/A-BM 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_136` | BM | Configuration | Pending | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_137` | BM | Configuration | Pending | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_138` | BM + Primary | Diagnostic | Pending | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_139` | BM + Primary | Safety | Pending | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_140` | BM + Primary | Capacity | Draft | **BLOCKED** | Needs OI-12 (achievable aggregate sampling rate) and TBD-22. | WP-B13 | 5d |

### 8.1.3   Reverse Polarity Detection

*2 requirement(s) - TODO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_141` | BM + Primary | Safety | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_142` | BM + Primary | Safety | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |

### 8.1.4   Battery Open Detection

*2 requirement(s) - TODO 1, BLOCKED 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_143` | BM + Primary | Safety | Draft | **BLOCKED** | Needs TBD-15 (the 'near 0 V' threshold). | WP-P19 | 8d |
| `ME_SW_REQ_144` | BM + Primary | Safety | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |

### 8.1.5   Calibration for Battery Voltage

*1 requirement(s) - BLOCKED 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_145` | BM + Primary | Functional | Draft | **BLOCKED** | Needs TBD-16 and OI-13 (there is no calibration procedure to implement - BTS 8.1.5 was empty). | WP-P20 | 10d |

### 8.2.1   Max Charging Current

*5 requirement(s) - TODO 4, N/A-BM 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_146` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_147` | BM + Primary | Constraint | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_148` | BM + Primary | Diagnostic | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_149` | BM + Primary | Safety | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_150` | Primary | Safety | Draft | TODO | No Max Charging Current is stored, so no clamp is applied to a program's requested current. | WP-P03 | 10d |

### 8.2.2   Max Discharging Current

*5 requirement(s) - TODO 4, N/A-BM 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_151` | BM | Configuration | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_152` | BM + Primary | Constraint | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_153` | BM + Primary | Diagnostic | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_154` | BM + Primary | Safety | Draft | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_155` | Primary | Safety | Draft | TODO | No Max Discharging Current is stored, so no clamp is applied. | WP-P03 | 10d |

### 8.2.3   Current Sampling Rate

*4 requirement(s) - TODO 3, N/A-BM 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_156` | BM | Configuration | Pending | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_157` | BM + Primary | Configuration | Pending | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_158` | BM + Primary | Diagnostic | Pending | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |
| `ME_SW_REQ_159` | BM + Primary | Safety | Pending | TODO | No per-channel analog limit validation and no exception machinery exist. | WP-P19 | 8d |

### 8.2.4   Current Calibration

*4 requirement(s) - TODO 2, BLOCKED 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_160` | BM + Primary | Functional | Draft | TODO | No calibration storage, procedure or apply path exists. | WP-P20 | 10d |
| `ME_SW_REQ_161` | BM + Primary | Functional | Draft | TODO | No calibration storage, procedure or apply path exists. | WP-P20 | 10d |
| `ME_SW_REQ_162` | BM + Primary | Safety | Draft | **BLOCKED** | Needs OI-14 (how calibration binds to a board identity). | WP-P20 | 10d |
| `ME_SW_REQ_163` | Primary | Data | Draft | **BLOCKED** | Needs OI-15 (persistence medium and format). | WP-P07 | 7d |

### 9.0   Analog Output   (Primary and BM obligations only)

*3 requirement(s) - PARTIAL 1, TODO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_164` | BM + Primary | Functional | Pending | TODO | No calibration storage, procedure or apply path exists. | WP-P20 | 10d |
| `ME_SW_REQ_165` | BM + Primary | Functional | Pending | TODO | No calibration storage, procedure or apply path exists. | WP-P20 | 10d |
| `ME_SW_REQ_166` | Primary | Interface | Draft | *PARTIAL* | `proto/can_frame.c` `me_can_pack_set()` already commands current as an engineering value - a little-endian IEEE-754 float in amperes - and never a raw DAC code. Voltage is not commanded at all: `step_decode.h` has no voltage field for CCChg (T-66). | WP-P29 | 2d |

### 10.1   Introduction

*3 requirement(s) - PARTIAL 2, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_167` | BM + Primary | Functional | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_168` | Primary | Functional | Draft | *PARTIAL* | `core_logic.c` holds `s_exec[64]`, one `me_exec_ctx_t` per circuit, each with its own `step_index`, `step_run_ms`, `program_run_ms` and retry counter. Cycle counters do not exist yet, and only 4 circuits can be admitted today. | WP-P02 | 17d |
| `ME_SW_REQ_169` | Primary | Safety | Draft | *PARTIAL* | Independent by construction - `service_engines()` ticks each context separately and they share no mutable state. Never demonstrated with more than 4 channels. | WP-P27 | 9d |

### 10.2   Step

*3 requirement(s) - N/A-BM 3*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_170` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_171` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_172` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |

### 10.3   Operator

*4 requirement(s) - PARTIAL 1, DEFERRED 1, INFO 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_173` | BM + Primary | Functional | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_174` | BM + Primary | Functional | Draft | *PARTIAL* | 3 of the 13 listed operators are decoded and executed: SET (`0x0A`), CCChg (`0x01`, the constant-current charge case of CHA) and STOP (`0x0B`) - `proto/step_decode.h` `me_step_operator_t`. PAU, DCH, INT, BEG, CYC, GOTO, REG, ERR, MSG and TABLE are all rejected with `ME_STEP_DECODE_UNSUPPORTED_OPERATOR`. | WP-P02 | 17d |
| `ME_SW_REQ_175` | BM + Primary | Functional | Deferred | DEFERRED | Phase 2. | WP-P02 | 17d |
| `ME_SW_REQ_176` | BM + Primary | Functional | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |

### 10.3.1   SET

*1 requirement(s) - PARTIAL 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_177` | BM + Primary | Functional | Draft | *PARTIAL* | SET is decoded - `step_decode.c` extracts the 13-bit `registration_type` mask - and `step_engine.c` emits its SET_VALUES CAN frame. The mask itself is not acted on, and OI-17 means nobody has stated what SET should do. | WP-P02 | 17d |

### 10.3.2   PAU

*9 requirement(s) - TODO 6, N/A-BM 3*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_178` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_179` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_180` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_181` | Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_182` | Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_183` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_184` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_185` | Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_186` | Primary | Data | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |

### 10.3.3   CHA

*12 requirement(s) - PARTIAL 2, TODO 7, N/A-BM 3*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_187` | Primary | Functional | Draft | *PARTIAL* | CCChg implements the constant-current charge case only: nominal current in amperes, a single TIME cut-off, no Action, and the Registration parameter list decoded but not acted on (`step_decode.h` comment: 'Decoded fully; not yet acted on by step_engine'). | WP-P03 | 10d |
| `ME_SW_REQ_188` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_189` | Primary | Functional | Draft | *PARTIAL* | Constant-current charge to a limit works, but only a TIME limit - Voltage, Ah and Temperature limits are rejected by `ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE`. No Action is executed on the crossing; the engine advances to the next step. | WP-P03 | 10d |
| `ME_SW_REQ_190` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_191` | Primary | Functional | Draft | TODO | No constant-voltage mode. `step_decode.h`'s `me_step_t` has no nominal-voltage field. | WP-P03 | 10d |
| `ME_SW_REQ_192` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_193` | Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_194` | Primary | Functional | Draft | TODO | No CC-then-CV transition. | WP-P03 | 10d |
| `ME_SW_REQ_195` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_196` | Primary | Data | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_197` | Primary | Data | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_198` | BM + Primary | Safety | Draft | TODO | No temperature source binding exists. | WP-P03 | 10d |

### 10.3.4   DCH

*1 requirement(s) - TODO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_199` | BM + Primary | Functional | Draft | TODO | DCH is rejected with `ME_STEP_DECODE_UNSUPPORTED_OPERATOR`. | WP-P03 | 10d |

### 10.3.5   INT

*1 requirement(s) - TODO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_200` | BM + Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |

### 10.3.6   STO

*6 requirement(s) - PARTIAL 1, TODO 2, N/A-BM 2, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_201` | Primary | Functional | Draft | *PARTIAL* | `me_exec_force_stop()` sends a `CMD_STO` SET_VALUES frame and moves that one circuit to `ME_EXEC_STOPPED`; other circuits are untouched. Sub-programs (REQ_203) do not exist. | WP-P02 | 17d |
| `ME_SW_REQ_202` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_203` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_204` | BM + Primary | Functional | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_205` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_206` | Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |

### 10.3.7   BEG and CYC

*13 requirement(s) - TODO 11, BLOCKED 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_207` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_208` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_209` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_210` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_211` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_212` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_213` | BM + Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_214` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_215` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_216` | Primary | Capacity | Pending | **BLOCKED** | Needs OI-22 (is 16-way nesting a Phase 1 commitment at all - it was never built in BTS either). | WP-P02 | 17d |
| `ME_SW_REQ_217` | Primary | Functional | Pending | **BLOCKED** | Needs OI-22. | WP-P02 | 17d |
| `ME_SW_REQ_218` | Primary | Data | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_219` | Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |

### 10.4   Action

*13 requirement(s) - PARTIAL 1, TODO 11, INFO 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_220` | BM + Primary | Functional | Draft | INFO | States a fact or a scope boundary; imposes no code obligation on me-primary. | - |  |
| `ME_SW_REQ_221` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_222` | Primary | Functional | Draft | *PARTIAL* | `step_engine.c` does advance to the next step when the current step's cut-off is met and nothing else is configured - but only for a TIME cut-off, and 'Action is blank' is not a decision it makes, because no Action is ever decoded. | WP-P02 | 17d |
| `ME_SW_REQ_223` | BM + Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_224` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_225` | BM + Primary | Functional | Draft | TODO | `core_logic.c` logs `Continue is not implemented yet` and still returns an `0x01` ack to the Battery Manager. | WP-P22 | 8d |
| `ME_SW_REQ_226` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_227` | Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_228` | Primary | Functional | Pending | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_229` | Primary | Functional | Pending | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_230` | BM + Primary | Diagnostic | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_231` | BM + Primary | Functional | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |
| `ME_SW_REQ_232` | BM + Primary | Usability | Draft | TODO | Operator not decoded by `proto/step_decode.c` and not executed by `exec/step_engine.c`. | WP-P02 | 17d |

### 11.0   Program Compilation and Download

*9 requirement(s) - PARTIAL 3, TODO 2, N/A-BM 4*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_233` | BM | Functional | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_234` | BM | Interface | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_235` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_236` | BM + Primary | Usability | Draft | *PARTIAL* | Each `0xBB` Q4 packet is acked `0x01`/`0x00` (`comm_thread.c` `send_ack()`), so a failure is signalled - but the ack carries no reason code and names no channel beyond the CircuitID it echoes. | WP-P21 | 6d |
| `ME_SW_REQ_237` | Primary | Capacity | Draft | *PARTIAL* | `store/circuit_store.c` reserves `ME_PROGRAM_BUF_SIZE` per slot for all 64 slots. The size itself is unagreed (TBD-17) and the resulting 261 MB `.bss` has never been checked against the container memory limit (T-27). | WP-P21 | 6d |
| `ME_SW_REQ_238` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B03. | WP-B03 | 8d |
| `ME_SW_REQ_239` | BM + Primary | Safety | Draft | TODO | A `0xBB` Q1 is-ready query is answered unconditionally (`handle_program_handshake()`), and a Q4 packet for a running circuit is accepted. Nothing consults execution state. | WP-P21 | 6d |
| `ME_SW_REQ_240` | Primary | Safety | Draft | *PARTIAL* | `me_chain_is_complete()` is checked before a Start, so an unterminated chain will not run (ADR-14), and every `nextIndex` is range-checked by the walker. There is no whole-program checksum and no comparison against what the Battery Manager sent. | WP-P21 | 6d |
| `ME_SW_REQ_241` | BM + Primary | Data | Draft | TODO | No read-back path. `ME_MSG_REQ_PROGRAM` serves Core Logic internally, never the host. | WP-P21 | 6d |

### 12.0   System Topology, Addressing and Capacity        [new section - no BTS equivalent]

*9 requirement(s) - DONE 2, PARTIAL 5, BLOCKED 1, N/A-BM 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_242` | BM + Primary | Capacity | Draft | *PARTIAL* | `ME_MAX_SECONDARIES` is 8 and all 64 slots exist, but only Secondary 1 has ever been addressed (T-62). | WP-P27 | 9d |
| `ME_SW_REQ_243` | BM + Primary | Capacity | Draft | *PARTIAL* | The address space holds 8 channels per board. The CAN block selection for channels 5-8 is explicitly deferred (ADR-28) - `me_can_block_for_channel()` maps 1-4 to Block 1 and 5-8 to Block 2, but only Block 1 has been exercised. | WP-P27 | 9d |
| `ME_SW_REQ_244` | BM + Primary | Capacity | Draft | *PARTIAL* | Program, battery record, raw config and an execution context exist per slot for all 64. Calibration data is not held at all. | WP-P27 | 9d |
| `ME_SW_REQ_245` | BM + Primary | Data | Draft | **DONE** | `proto/proto_defs.h` `ME_CIRCUIT_ID(sec, ch)`, 1-based nibbles, `0x11`-`0x88`. `store/circuit_store.c` `me_circuit_slot()` maps it to 0-63. Covered by `tests/test_circuit_store.c`. | WP-P27 | 9d |
| `ME_SW_REQ_246` | Primary | Safety | Draft | **DONE** | `me_circuit_slot()` returns `ME_SLOT_INVALID` and never folds to slot 0 (ADR-13). `0x00`, `0x01`, `0x10`, `0x09`, `0x90`, `0xFF` are all asserted as rejected in `tests/test_circuit_store.c`, plus a nibble-swap and row-boundary aliasing sweep in `tests/test_circuit_registry.c`. | WP-P27 | 9d |
| `ME_SW_REQ_247` | BM + Primary | Data | Draft | **BLOCKED** | Needs TBD-18 / OI-24. The encoding admits 15x15; storage and validation cap at 8x8. Two ME documents disagree. | WP-P27 | 9d |
| `ME_SW_REQ_248` | BM + Primary | Data | Draft | *PARTIAL* | Every frame and most log lines carry the CircuitID. Test records and error reports do not exist yet, so they cannot carry it. | WP-P27 | 9d |
| `ME_SW_REQ_249` | BM + Primary | Functional | Draft | *PARTIAL* | The system does run with one Secondary and 4 channels. It works because the population is passed on the command line (`--channels`), not because it was discovered. | WP-P01 | 9d |
| `ME_SW_REQ_250` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B15. | WP-B15 | 4d |

### 13.0   Secondary Board Enrolment and Supervision       [new section - no BTS equivalent]

*10 requirement(s) - PARTIAL 1, TODO 5, BLOCKED 4*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_251` | Primary | Functional | Draft | **BLOCKED** | Needs OI-26. This is the largest single gap between the code as built and the SRS: circuits are admitted only by the host-side `0xDD` handshake in `comm_thread.c`, so the board learns its population from `--channels` and never from the bus. | WP-P01 | 9d |
| `ME_SW_REQ_252` | Primary | Data | Draft | TODO | No enrolment handshake on the internal bus. Circuits are admitted only by the host-side `0xDD` registration. | WP-P01 | 9d |
| `ME_SW_REQ_253` | Primary | Safety | Draft | *PARTIAL* | `store/circuit_registry.c` `me_registry_is_registered()` is consulted in `route_frame()` before any frame reaches a queue (ADR-17), so an unadmitted circuit is never commanded. But 'registered with the host' is not 'enrolled on the bus' - the check does not know whether a Secondary is physically there. | WP-P01 | 9d |
| `ME_SW_REQ_254` | BM + Primary | Safety | Draft | TODO | No enrolment handshake on the internal bus. Circuits are admitted only by the host-side `0xDD` registration. | WP-P01 | 9d |
| `ME_SW_REQ_255` | Primary | Safety | Draft | **BLOCKED** | Needs TBD-19 (loss declaration time). | WP-P01 | 9d |
| `ME_SW_REQ_256` | BM + Primary | Safety | Draft | TODO | No enrolment handshake on the internal bus. Circuits are admitted only by the host-side `0xDD` registration. | WP-P01 | 9d |
| `ME_SW_REQ_257` | Primary | Safety | Draft | **BLOCKED** | Needs OI-25 (is hot-swap a supported field operation). | WP-P01 | 9d |
| `ME_SW_REQ_258` | Primary | Safety | Draft | TODO | No enrolment handshake on the internal bus. Circuits are admitted only by the host-side `0xDD` registration. | WP-P01 | 9d |
| `ME_SW_REQ_259` | BM + Primary | Diagnostic | Draft | TODO | No enrolment handshake on the internal bus. Circuits are admitted only by the host-side `0xDD` registration. | WP-P01 | 9d |
| `ME_SW_REQ_260` | Primary | Functional | Pending | **BLOCKED** | Needs OI-26. Partially advanced by ADR-32 - Secondary 1 now registers channels 1-4 independently over one TCP connection - but that is host-side registration, not bus enrolment. | WP-P01 | 9d |

### 14.0   ME Internal CAN Bus, Primary to Secondary Boards   [new section - no BTS equivalent]

*10 requirement(s) - DONE 3, PARTIAL 2, TODO 2, BLOCKED 3*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_261` | Primary | Interface | Draft | **DONE** | A single shared path to all Secondaries: `threads/can_mgr.c` over `platform/rpmsg_link.c` (`/dev/ttyRPMSG30`) to the M7, which owns the physical CAN-FD controller (ADR-29). Partially hardware-verified 2026-08-19. | WP-P25 | 8d |
| `ME_SW_REQ_262` | Primary | Interface | Draft | **DONE** | CAN FD - 64-byte data frames, `ME_CAN_FRAME_LEN` in `proto/can_frame.h`, DLC table asserted in `tests/test_rpmsg_frame.c`. | WP-P25 | 8d |
| `ME_SW_REQ_263` | Primary | Interface | Draft | **BLOCKED** | Needs TBD-20 and TBD-21. No bit rate is recorded anywhere; the M7 firmware sets it and nothing on the A53 states it. | WP-P25 | 8d |
| `ME_SW_REQ_264` | Primary | Performance | Draft | **BLOCKED** | Needs TBD-22 and OI-12 (the load budget does not exist). | WP-P25 | 8d |
| `ME_SW_REQ_265` | Primary | Safety | Draft | TODO | CAN identifiers come from `me_can_block_for_channel()` by block, not by priority class. No arbitration analysis has been done. | WP-P25 | 8d |
| `ME_SW_REQ_266` | Primary | Performance | Draft | *PARTIAL* | The round trip IS measured - `can_mgr.c` stamps `s_tx_us[]` and logs RPMsg RTT per Secondary in microseconds (ADR-31), which is exactly the instrument this requirement needs. There is no budget to compare it against (TBD-23) and no worst-case run. | WP-P25 | 8d |
| `ME_SW_REQ_267` | Primary | Safety | Draft | **BLOCKED** | Needs TBD-24. `step_engine.c` does count missed responses and goes `ME_EXEC_CHANNEL_OFFLINE` after `ME_EXEC_RETRY_LIMIT` (3) - a 3-strike rule at a 200 ms response timeout - but that is a lost-response rule, not a stale-data rule, and the boundary is unspecified. | WP-P25 | 8d |
| `ME_SW_REQ_268` | BM + Primary | Safety | Draft | TODO | No bus-off detection. The A53 never sees the CAN controller; the M7 owns it and reports nothing about its state. | WP-P25 | 8d |
| `ME_SW_REQ_269` | BM + Primary | Diagnostic | Draft | *PARTIAL* | `step_engine.c` counts missed responses per circuit and `can_mgr.c` logs discarded echo frames (ADR-38). There are no per-Secondary error-frame or retransmission counters, and nothing is reported to the Battery Manager. | WP-P25 | 8d |
| `ME_SW_REQ_270` | BM + Primary | Safety | Draft | **DONE** | True by construction - no host frame reaches `can_mgr.c`'s transport configuration, and the bit rate lives in M7 firmware the host cannot address. | WP-P25 | 8d |

### 15.0   Host Interface, Battery Manager to Primary       [new section - no BTS equivalent]

*12 requirement(s) - DONE 7, PARTIAL 3, TODO 1, DEFERRED 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_271` | BM + Primary | Interface | Draft | **DONE** | `comm_thread.c` is the TCP client and initiates the connection. Hardware-verified 2026-08-07 (T-6) and again 2026-08-10 (T-21). | WP-P23 | 7d |
| `ME_SW_REQ_272` | BM + Primary | Interface | Draft | **DONE** | Port 9999, `ME_TCP_PORT` in `proto/proto_defs.h`. Hardware-verified. | WP-P23 | 7d |
| `ME_SW_REQ_273` | Primary | Functional | Draft | **DONE** | `comm_thread.c` - `ME_BACKOFF_MIN_MS` 1000, `ME_BACKOFF_MAX_MS` 30000, doubling on each failure and reset to the minimum on a healthy connection. The process never exits on a link failure. Exactly as specified. | WP-P23 | 7d |
| `ME_SW_REQ_274` | BM + Primary | Interface | Draft | **DONE** | UDP 10000, `net/udp_sock.c` + `sys_init.c` `udp_live_dest`. The 86-byte `0xCC` frame is sent there by `drain_outbound()`. | WP-P06 | 4d |
| `ME_SW_REQ_275` | BM + Primary | Interface | Draft | *PARTIAL* | The whole path exists - `sys_init.c` opens the socket and builds `udp_session_dest`, `data_mgr.c` holds a 64-deep session ring and `ring_flush()` forwards to `g_q_comm` - but nothing produces a test record, so UDP 10001 carries no traffic (T-29). | WP-P05 | 6d |
| `ME_SW_REQ_276` | BM + Primary | Data | Draft | **DONE** | `proto/crc16.c` - CRC-16/Modbus, poly `0xA001`, init `0xFFFF`, no final inversion. Reference vector `"123456789" -> 0x4B37` asserted in `tests/test_crc16.c`. | WP-P23 | 7d |
| `ME_SW_REQ_277` | BM + Primary | Data | Draft | *PARTIAL* | Big-endian is implemented and hardware-verified in BOTH directions (ADR-9: request CRC `0x369E` -> `36 9E`, response `0xBE15` -> `BE 15`), and `ME_CRC_ORDER_DEFAULT` in `crc16.h` is the single source of truth. TBD-25 / OI-28 are still formally open because a second ME document records a little-endian capture - the code is settled, the specification is not. | WP-P23 | 7d |
| `ME_SW_REQ_278` | Primary | Functional | Draft | **DONE** | `comm_thread.c` `consume_rx_buffer()` plus `proto/frame_router.c` `me_frame_resolve_len()` (ADR-19). Both the coalesced-read and the split-frame cases are asserted in `tests/test_frame_router.c`, including two named truncation guards. | WP-P23 | 7d |
| `ME_SW_REQ_279` | Primary | Diagnostic | Draft | **DONE** | `comm_thread.c` `frame_crc_ok()` logs computed-vs-carried in both byte orders and returns; the connection is never closed on a bad CRC. | WP-P23 | 7d |
| `ME_SW_REQ_280` | Primary | Diagnostic | Draft | *PARTIAL* | An unrecognised command group is logged and dropped, which satisfies the first half. The second half is violated today: a well-formed `0xEE` frame carrying a QueryID outside the six documented is acked `0x01` in `route_frame()` before `me_control_parse()` rejects it on the Core Logic thread, which has no path back to the socket (T-45). | WP-P23 | 7d |
| `ME_SW_REQ_281` | BM + Primary | Interface | Draft | TODO | Every ack means 'queued to the owning thread', never 'executed' - stated explicitly in the implementation reference. Closing this is the same seam as T-45: Core Logic needs an outbound message type through `g_q_comm`. | WP-P23 | 7d |
| `ME_SW_REQ_282` | BM + Primary | Interface | Deferred | DEFERRED | Phase 2. UDP 10002/10003 discovery is documented in `bm_device_registration_v5.0.md` but unimplemented (T-10). | WP-P24 | 6d |

### 16.0   Live Data and Test Record Registration           [new section - no BTS equivalent]

*6 requirement(s) - DONE 2, PARTIAL 1, TODO 2, BLOCKED 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_283` | BM + Primary | Data | Draft | *PARTIAL* | The 86-byte `0xCC` frame has a field for nearly all of this and `realtime_frame.c` packs it with all 80 payload offsets asserted in `tests/test_realtime_frame.c`. What the engine actually fills is step number, both statuses, both run times, current, voltage and operator code (`me_exec_output_t`). Temperature, power, the capacity and energy accumulators, cycle and table position, registration type and digital I/O state are sent as zero. | WP-P06 | 4d |
| `ME_SW_REQ_284` | Primary | Performance | Draft | **DONE** | `util/msgq.c` sends with a finite timeout and drops on expiry (ADR-12); real-time frames use the control timeout, not the bulk one, precisely so a stale frame is dropped rather than queued ahead of a fresh one. Drops are counted per queue and logged at WARN by `me_queues_report()`. | WP-P06 | 4d |
| `ME_SW_REQ_285` | BM + Primary | Data | Draft | TODO | No test record exists to guarantee. Depends on WP-P05. | WP-P05 | 6d |
| `ME_SW_REQ_286` | BM + Primary | Configuration | Draft | **BLOCKED** | Needs TBD-26 (permitted registration types and interval range). Note the raw material is already decoded: `step_decode.c` extracts SET's 13-bit registration mask and CCChg's trailing registration-parameter list (types `0x21`-`0x2D`), and neither is acted on. | WP-P05 | 6d |
| `ME_SW_REQ_287` | BM + Primary | Data | Draft | **DONE** | `realtime_frame.c` `me_put_f32_be()` / `me_get_f32_be()` - IEEE 754 single precision, big-endian, via `memcpy` through a `uint32_t` rather than a type-punning cast. The spec's own worked example `95.6f -> 42 BF 33 33` is the golden test. | WP-P06 | 4d |
| `ME_SW_REQ_288` | BM + Primary | Data | Draft | TODO | The `0xEE` Q1 Session ID IS parsed (`control_frame.c`, `has_session_id`) and logged, but deliberately not stored - the `0xCC` frame has no field for it and no test record exists to carry it. | WP-P05 | 6d |

### 17.0   Test Control Commands                            [new section - no BTS equivalent]

*8 requirement(s) - PARTIAL 2, TODO 2, BLOCKED 4*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_289` | BM + Primary | Functional | Draft | *PARTIAL* | All six are parsed and acked (`proto/control_frame.c`, all six asserted in `tests/test_control_frame.c`). Only two are implemented: Start loads the program and battery record and starts the engine; Stop calls `me_exec_force_stop()`. `core_logic.c` logs `Pause`, `Continue` and `Reset` as `not implemented yet`, and Sync Time as `not applied`. | WP-P22 | 8d |
| `ME_SW_REQ_290` | BM + Primary | Safety | Draft | *PARTIAL* | Start and Stop act on exactly the named circuit and no other. Pause and Continue do not act at all. | WP-P22 | 8d |
| `ME_SW_REQ_291` | BM + Primary | Interface | Draft | **BLOCKED** | Needs OI-29. Today `route_frame()` gates EVERY `0xEE` and `0xAA` frame on its CircuitID, including the system-scoped Sync Time and Reset - harmless only because Sync Time is a no-op (T-34). | WP-P22 | 8d |
| `ME_SW_REQ_292` | BM + Primary | Usability | Draft | TODO | Command parsed but no behaviour behind it. | WP-P22 | 8d |
| `ME_SW_REQ_293` | BM + Primary | Safety | Draft | TODO | Start checks only that a program is complete. Configuration, calibration and Secondary enrolment are not consulted, because none of the three exists as state to consult. | WP-P22 | 8d |
| `ME_SW_REQ_294` | BM + Primary | Safety | Draft | **BLOCKED** | Needs OI-05 (no source document defines the safe state). Stop does send `CMD_STO` and does not wait for confirmation before moving to `ME_EXEC_STOPPED`. | WP-P22 | 8d |
| `ME_SW_REQ_295` | BM + Primary | Functional | Draft | **BLOCKED** | Needs TBD-27 / OI-30. The current behaviour is a deliberate restart - `core_logic.c` zeroes the slot and reloads on a fresh Start, commented as intentional so the flow can be re-demonstrated. That may be the wrong default for a multi-day test. | WP-P22 | 8d |
| `ME_SW_REQ_296` | BM + Primary | Safety | Draft | **BLOCKED** | Needs OI-05. | WP-P22 | 8d |

### 18.0   Fault Containment and Channel Isolation          [new section - no BTS equivalent]

*7 requirement(s) - PARTIAL 2, TODO 1, BLOCKED 4*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_297` | BM + Primary | Safety | Draft | **BLOCKED** | Needs OI-11 (the error catalogue does not exist). | WP-P10 | 6d |
| `ME_SW_REQ_298` | Primary | Safety | Draft | *PARTIAL* | Per-circuit isolation is real - each `me_exec_ctx_t` fails to `ME_EXEC_CHANNEL_OFFLINE` or `ME_EXEC_DECODE_ERROR` on its own without touching its neighbours. But there is no notion of a classified 'channel fault' to inhibit on. | WP-P10 | 6d |
| `ME_SW_REQ_299` | Primary | Safety | Draft | TODO | No fault classification, scope or reporting model exists. | WP-P10 | 6d |
| `ME_SW_REQ_300` | BM + Primary | Safety | Draft | **BLOCKED** | Needs OI-05. | WP-P10 | 6d |
| `ME_SW_REQ_301` | Primary | Safety | Draft | *PARTIAL* | True today because failures are per-context and a Start on any other circuit is independent. Untested and unclassified. | WP-P10 | 6d |
| `ME_SW_REQ_302` | BM + Primary | Diagnostic | Draft | **BLOCKED** | Needs OI-11. | WP-P11 | 4d |
| `ME_SW_REQ_303` | Primary | Safety | Draft | **BLOCKED** | Needs OI-05. Note that deadlock is already impossible by construction - every queue send has a finite timeout and drops on expiry (ADR-12) - so what is missing is detection of a stall that is NOT a deadlock, and the safe-state action to take when one is found. | WP-P31 | 5d |

### 19.0   Performance, Capacity and Timing Budgets         [new section - no BTS equivalent]

*8 requirement(s) - DONE 1, PARTIAL 2, TODO 1, BLOCKED 4*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_304` | Primary | Performance | Draft | TODO | No timing analysis, budget or headroom measurement exists. | WP-P26 | 9d |
| `ME_SW_REQ_305` | Primary | Performance | Draft | *PARTIAL* | `core_logic.c` runs a 10 ms loop and `service_engines()` ticks every circuit on every pass, so the re-evaluation interval today is 10 ms of engine time with a 100 ms CAN poll period (`ME_EXEC_POLL_PERIOD_MS`). Whether that satisfies the requirement is unknowable until TBD-28 is answered - OI-31 calls this the single most architecturally significant unknown in the SRS. | WP-P26 | 9d |
| `ME_SW_REQ_306` | Primary | Performance | Draft | **BLOCKED** | Needs TBD-29. Core Logic IS pinned to CPU3 (`pthread_attr_setaffinity_np`, ADR-30) which is the mechanism for bounding jitter, but the CPU is not yet kernel-isolated (T-60) and jitter has never been measured. | WP-P26 | 9d |
| `ME_SW_REQ_307` | BM + Primary | Performance | Draft | **BLOCKED** | Needs TBD-30. | WP-P26 | 9d |
| `ME_SW_REQ_308` | Primary | Performance | Draft | **BLOCKED** | Needs TBD-31. | WP-P26 | 9d |
| `ME_SW_REQ_309` | Primary | Performance | Draft | *PARTIAL* | The footprint IS bounded and known before start - every buffer is static (ADR-11/rule 3) - but `readelf` reports 261 MB of `.bss` and it has never been checked against the container limit (T-27). TBD-32 gives no budget to check it against. | WP-P26 | 9d |
| `ME_SW_REQ_310` | Primary | Constraint | Draft | **DONE** | No `malloc`/`free` anywhere in `src/`. Cross-cutting rule 3, and a documented project constraint. | WP-P26 | 9d |
| `ME_SW_REQ_311` | Primary | Performance | Draft | **BLOCKED** | Needs OI-03 (is a Linux A53 the right host for a 64-channel control loop at all) before a WCET analysis has a target to analyse. | WP-P26 | 9d |

### 20.0   Startup, Shutdown, Persistence and Recovery      [new section - no BTS equivalent]

*8 requirement(s) - PARTIAL 1, TODO 5, BLOCKED 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_312` | Primary | Safety | Draft | TODO | `main.c` starts all four threads and the comm thread registers immediately. No enrolment step and no configuration validation gate the first command. | WP-P09 | 5d |
| `ME_SW_REQ_313` | Primary | Data | Draft | **BLOCKED** | Needs OI-15. Everything is in volatile memory today - a restart loses every program, battery record and raw config. | WP-P07 | 7d |
| `ME_SW_REQ_314` | BM + Primary | Safety | Draft | TODO | No persistence and no startup/shutdown state machine. | WP-P09 | 5d |
| `ME_SW_REQ_315` | BM + Primary | Data | Draft | TODO | No persistence and no startup/shutdown state machine. | WP-P09 | 5d |
| `ME_SW_REQ_316` | Primary | Safety | Draft | *PARTIAL* | `main.c` handles `SIGINT`/`SIGTERM` by setting one stop flag, joins the threads in reverse start order and reports queue drop counts. It does not bring any channel to a safe state and has no records to flush. | WP-P09 | 5d |
| `ME_SW_REQ_317` | BM + Primary | Diagnostic | Draft | TODO | No version string is reported anywhere, and no Secondary software version is known because nothing enrols a Secondary. | WP-P30 | 3d |
| `ME_SW_REQ_318` | BM + Primary | Safety | Draft | TODO | No compatibility gate. Needs WP-P01 to have a version to refuse on. | WP-P30 | 3d |
| `ME_SW_REQ_319` | Primary | Performance | Draft | **BLOCKED** | Needs TBD-33. | WP-P09 | 5d |

### 21.0   Diagnostics and Logging                          [new section - no BTS equivalent]

*6 requirement(s) - PARTIAL 1, TODO 3, BLOCKED 2*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_320` | BM + Primary | Diagnostic | Draft | **BLOCKED** | Needs OI-11. BTS asked 'how do we indicate this error?' in nine places and never answered; nine ME requirements depend on the one answer. | WP-P11 | 4d |
| `ME_SW_REQ_321` | Primary | Diagnostic | Draft | **BLOCKED** | Needs TBD-34. `util/log.c` writes to stderr only - nothing is retained across a restart. | WP-P12 | 5d |
| `ME_SW_REQ_322` | Primary | Diagnostic | Draft | TODO | Logging is unstructured stderr only. No store, no retrieval. | WP-P12 | 5d |
| `ME_SW_REQ_323` | BM + Primary | Usability | Draft | TODO | Logging is unstructured stderr only. No store, no retrieval. | WP-P12 | 5d |
| `ME_SW_REQ_324` | Primary | Safety | Draft | *PARTIAL* | Logging cannot currently delay anything because there is no log storage to block on, and the hex dump is the deliberate primary field diagnostic. The property is accidental, not designed, and there is no logging-failure report. | WP-P12 | 5d |
| `ME_SW_REQ_325` | Primary | Data | Draft | TODO | Logging is unstructured stderr only. No store, no retrieval. | WP-P12 | 5d |

### 22.0   Battery Manager Application                      [new section - no BTS equivalent]

*15 requirement(s) - N/A-BM 14, DEFERRED 1*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_326` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B01. | WP-B01 | 10d |
| `ME_SW_REQ_327` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B01. | WP-B01 | 10d |
| `ME_SW_REQ_328` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B02. | WP-B02 | 20d |
| `ME_SW_REQ_329` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B03. | WP-B03 | 8d |
| `ME_SW_REQ_330` | BM | Safety | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B03. | WP-B03 | 8d |
| `ME_SW_REQ_331` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B04. | WP-B04 | 6d |
| `ME_SW_REQ_332` | BM | Safety | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B04. | WP-B04 | 6d |
| `ME_SW_REQ_333` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B05. | WP-B05 | 8d |
| `ME_SW_REQ_334` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B06. | WP-B06 | 10d |
| `ME_SW_REQ_335` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B06. | WP-B06 | 10d |
| `ME_SW_REQ_336` | BM | Data | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B06. | WP-B06 | 10d |
| `ME_SW_REQ_337` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B05. | WP-B05 | 8d |
| `ME_SW_REQ_338` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B07. | WP-B07 | 10d |
| `ME_SW_REQ_339` | BM | Usability | Draft | N/A-BM | Battery Manager scope - no me-primary obligation. Built by the Battery Manager engineer under WP-B01. | WP-B01 | 10d |
| `ME_SW_REQ_340` | BM | Safety | Deferred | DEFERRED | Phase 2. | WP-B16 | 6d |

### 23.0   Power Fail                                        [new section - no BTS equivalent]

*5 requirement(s) - TODO 2, BLOCKED 3*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_341` | Primary | Safety | Draft | **BLOCKED** | Needs TBD-35 from the hardware architect - the guaranteed warning time is a property of the Primary Board's power-supervisory circuit, and no software design is possible without it. | WP-P08 | 8d |
| `ME_SW_REQ_342` | Primary | Data | Draft | **BLOCKED** | Needs OI-15 (medium, on-media format, partial-write handling). | WP-P08 | 8d |
| `ME_SW_REQ_343` | Primary | Safety | Draft | TODO | No power-fail detection and no snapshot exist. | WP-P08 | 8d |
| `ME_SW_REQ_344` | Primary | Functional | Draft | **BLOCKED** | Needs OI-34 - the client has directed automatic resume, and has flagged that this may change after a safety discussion. Do not build it until that is settled. | WP-P08 | 8d |
| `ME_SW_REQ_345` | BM + Primary | Diagnostic | Draft | TODO | No power-fail detection and no snapshot exist. | WP-P08 | 8d |

### 24.0   Cut-off Condition Handling During Program Execution   [new section - no BTS SRS equivalent; behaviour carried over from the BTS Secondary firmware and reassigned to the Primary]

*8 requirement(s) - DONE 1, PARTIAL 3, BLOCKED 4*

| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |
|---|---|---|---|---|---|---|---:|
| `ME_SW_REQ_346` | Primary | Functional | Draft | *PARTIAL* | 1 of the 13 types is implemented. `step_decode.c` accepts only TIME (`0x39`) and returns `ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE` for Current, Voltage, Power, both Capacities, both Energies, Temperature, Accumulated Capacity, Step Capacity, Accumulated Energy and Step Energy. | WP-P04 | 7d |
| `ME_SW_REQ_347` | Primary | Capacity | Draft | **BLOCKED** | Needs TBD-36. Today a wire count of 2-15 is rejected outright with `ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED`, and a count above 15 with `ME_STEP_DECODE_TOO_MANY_CUTOFFS` - deliberately refusing rather than folding to 0, because the old firmware produced a step that never ends that way. | WP-P04 | 7d |
| `ME_SW_REQ_348` | Primary | Functional | Draft | *PARTIAL* | 2 of the 6 comparators: `>` (`0x51`) and `>=` (`0x53`), carried as `cutoff_inclusive` in `me_step_t`. Less-than, less-than-or-equal, not-equal and equal-to all return `ME_STEP_DECODE_UNSUPPORTED_COMPARATOR`. | WP-P04 | 7d |
| `ME_SW_REQ_349` | Primary | Functional | Draft | **BLOCKED** | Needs TBD-37. There is no debounce at all - `step_engine.c` acts on the first evaluation in which the comparison is true. | WP-P04 | 7d |
| `ME_SW_REQ_350` | Primary | Functional | Draft | *PARTIAL* | The engine ends the step and advances. None of GOTO, ERR or MSG exists, and no action type is decoded from the step body (`ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE` guards the six documented values but nothing consumes them). | WP-P04 | 7d |
| `ME_SW_REQ_351` | Primary | Functional | Draft | **DONE** | The Primary evaluates every cut-off in `exec/step_engine.c` from the feedback the Secondary reports (`me_can_feedback_t` -> `me_exec_on_response()`); the Secondary evaluates nothing. Proven on the host in `tests/test_step_engine.c`, which asserts a cutoff firing at the exact millisecond. | WP-P04 | 7d |
| `ME_SW_REQ_352` | Primary | Functional | Need to check | **BLOCKED** | Needs the 'Need to check' status in the SRS to be resolved, and is moot until REQ_347 allows more than one condition. | WP-P04 | 7d |
| `ME_SW_REQ_353` | Primary | Functional | Draft | **BLOCKED** | Needs TBD-35/TBD-37 and OI-15 - it is a property of the power-fail snapshot that does not exist. | WP-P08 | 8d |

