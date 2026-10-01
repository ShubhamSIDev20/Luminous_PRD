# ME - Work Packages and Effort Estimates

> GENERATED FILE - do not hand-edit.
> Regenerate with `python Requirements/Code_Tracebility/tools/build_traceability.py`.
> Estimates and notes live in `tools/trace_packages.py`; requirement counts are derived from `tools/trace_status.py` and cannot drift.
> Code state as of: 2026-08-26, branch `develop`.

## Estimation basis

1.  UNIT.  One "day" is one engineer-day of ~6 productive hours, AI-assisted,
    working the way this project has worked since 2026-08-06: test-first, one
    ADR per real decision, both builds green before anything is claimed.

2.  STAFFING.  ONE engineer on the Primary board. ONE engineer on the Battery
    Manager web application. Confirmed by the developer 2026-08-26. Nothing in
    this plan can be parallelised across people, so `impl + hil` for a board
    sums directly into calendar days for that board. The two boards CAN
    proceed in parallel with each other.

3.  `impl` INCLUDES its host unit tests.  Test-first is the project standard
    (cross-cutting rule 4), so a separate "write the tests" line would be
    double counting. `impl` is: design note or ADR where a real decision is
    made, the failing test, the code, both builds green.

4.  `hil` IS SEPARATE AND IS NOT COMPRESSIBLE BY AI.  Hardware-in-the-loop
    verification needs the board, a real Secondary, an M7 build and a running
    Battery Manager on the same LAN. It is serialised on one engineer and one
    board, the addresses move between sessions, and the project's own rule is
    that a passing native build proves nothing about the target (ADR-3). This
    is the column that will not shrink, and it is why the totals are what they
    are.

5.  CALIBRATION.  These numbers are anchored on measured velocity, not
    guessed. Sessions #1-#16 ran 2026-08-06 to 2026-08-21 and delivered
    ~7.5 kLOC of source, ~4.6 kLOC of tests and 39 ADRs across roughly 13
    substantive work packages - registration, the 4-thread base, the
    admission gate, the 0xAA/0xBB/0xEE handshakes, the 40-byte battery
    record, step execution, the RPMsg CAN-FD transport, multi-channel
    registration and the CI/CD release. That is ~1.2 engineer-days per
    package where the specification was known and the hardware existed.
    Packages below are priced against that, then loaded for the two things
    that were NOT true of those sessions: hardware that does not exist yet
    (digital I/O, RS-485, RTC, analog) and specifications that do not exist
    yet (the whole TBD/OI register).

6.  BLOCKED WORK IS STILL ESTIMATED.  A blocker stops the work STARTING; it
    does not make it cheaper. Every blocked package carries its full estimate
    plus the register entry that gates it.

7.  WHAT IS EXCLUDED.  Secondary Board firmware, M7 firmware, mechanical and
    electrical bring-up, and the Phase 2 rows. Requirement-level rework if a
    TBD comes back with an answer that invalidates a design is also excluded -
    that risk is called out per package instead.

## Roll-up

### Primary board - 1 engineer

| | Engineer-days |
|---|---:|
| Implementation (includes host unit tests) | 151 |
| Hardware-in-the-loop verification | 83 |
| **Total, one engineer, serial** | **234** |
| Working months at 21 days | 11.1 |

### Battery Manager web application - 1 engineer

| | Engineer-days |
|---|---:|
| Implementation (includes host unit tests) | 127 |
| Hardware-in-the-loop verification | 0 |
| **Total, one engineer, serial** | **127** |
| Working months at 21 days | 6.0 |

### Both boards

The two engineers work in parallel, so the Phase 1 calendar is set by the longer of the two tracks, not by their sum.

| | Engineer-days |
|---|---:|
| Primary track | 234 |
| Battery Manager track | 127 |
| **Total effort, Phase 1** | **361** |
| **Calendar-critical path (the longer track)** | **234 days ~ 11.1 months** |
| Phase 2, excluded from the above | 6 |

> **The critical path is the Primary track and it is not close.** Pairing is the thing to watch: 8 of the Battery Manager packages explicitly pair with a Primary package and cannot be finished ahead of it. If the Battery Manager engineer runs out of unblocked work, the useful order is WP-B01, WP-B02, WP-B04 and WP-B06 - none of them waits on the Primary.

## Packages

### Primary board

| WP | Title | Reqs | Impl | HIL | Total | Prio | Blocked by |
|---|---|---:|---:|---:|---:|---|---|
| `WP-P01` | Secondary Board enrolment and supervision over the internal bus | 11 | 6 | 3 | **9** | P1 | OI-26, OI-25, TBD-19 |
| `WP-P02` | Complete the operator set: PAU, DCH, INT, BEG/CYC, GOTO, REG, ERR, MSG, TABLE | 42 | 12 | 5 | **17** | P1 | OI-16 (REG), OI-17 (SET), OI-20 (DCH/INT/ERR/MSG), OI-22 (cycle nesting) |
| `WP-P03` | CHA/DCH control modes: constant-voltage, CC-to-CV, non-time limits, current clamping | 8 | 6 | 4 | **10** | P1 | OI-20, and REQ_198 needs a temperature source binding |
| `WP-P04` | Cut-off condition engine: 13 condition types, 6 comparators, multiple per step, debounce | 7 | 5 | 2 | **7** | P1 | TBD-36 (how many per step), TBD-37 (debounce count) |
| `WP-P05` | Test record production and delivery on UDP 10001 | 4 | 4 | 2 | **6** | P1 | TBD-26 (registration types and interval range) |
| `WP-P06` | Live data completeness: fill the zeroed 0xCC fields | 4 | 3 | 1 | **4** | P2 | none |
| `WP-P07` | Non-volatile persistence of configuration, calibration and programs | 2 | 5 | 2 | **7** | P1 | OI-15 (medium, on-media format, partial-write handling) |
| `WP-P08` | Power-fail detection, snapshot and automatic resume | 6 | 5 | 3 | **8** | P2 | TBD-35 (guaranteed warning time - HARDWARE), OI-15, OI-34 (safety of automatic resume) |
| `WP-P09` | Startup gating, commanded shutdown, restart semantics | 5 | 3 | 2 | **5** | P2 | TBD-33 (time to ready), OI-05 (safe state) |
| `WP-P10` | Fault containment: classify every fault by scope and act on the scope | 5 | 4 | 2 | **6** | P1 | OI-05 (safe state), OI-11 (error catalogue) |
| `WP-P11` | Error code catalogue and structured error reporting to the Battery Manager | 2 | 3 | 1 | **4** | P1 | OI-11 |
| `WP-P12` | Persistent, retrievable, filterable log with per-channel history | 5 | 4 | 1 | **5** | P2 | TBD-34 (retention) |
| `WP-P13` | Real Time Clock: I2C device, dual-format time stamps, sync from host, distribution to Secondaries | 7 | 4 | 3 | **7** | P1 | TBD-04 (permitted Primary-to-Secondary offset) |
| `WP-P14` | Primary digital inputs: edge detection, time stamping, enable/disable, Secondary input read-through | 6 | 3 | 2 | **5** | P2 | TBD-01 (time-stamp resolution) |
| `WP-P15` | Primary digital outputs: level command, enable/disable, safe state on link loss | 7 | 3 | 2 | **5** | P2 | TBD-02 (safe-state time), OI-05 (what 'safe' is) |
| `WP-P16` | RS485_1: Modbus RTU slave towards a third-party HMI | 5 | 6 | 3 | **9** | P3 | TBD-05 (register map for 64 channels), OI-07 |
| `WP-P17` | RS485_2: Modbus master reading third-party devices, with value binding to channels | 10 | 6 | 3 | **9** | P3 | TBD-06, TBD-07, TBD-08, TBD-09, TBD-10, TBD-11 |
| `WP-P18` | User-configurable CAN ports: port/message/signal configuration, subscribe, publish, MTO | 58 | 15 | 6 | **21** | P3 | TBD-14 (which port), OI-09 (physically distinct?), OI-10 (11-bit, 29-bit or both) |
| `WP-P19` | Per-channel analog limit configuration, validation and exception handling | 17 | 5 | 3 | **8** | P1 | TBD-15 ('near 0 V' threshold) |
| `WP-P20` | Calibration: voltage, current and analog output, per channel, bound to board identity, persisted | 6 | 6 | 4 | **10** | P2 | TBD-16 and OI-13 (there is no procedure to implement), OI-14 (board binding) |
| `WP-P21` | Program management: integrity verification, download gating, read-back, storage limits | 5 | 4 | 2 | **6** | P2 | TBD-17 (maximum program size per channel), OI-23 |
| `WP-P22` | Test control completion: Pause, Continue, Reset, group operations, Start preconditions, safe state | 9 | 5 | 3 | **8** | P1 | OI-05 (safe state), TBD-27/OI-30 (Start on a completed test), OI-29 (command scoping) |
| `WP-P23` | Host link hardening: ack semantics, unrecognised-query NACK, session limit, link-loss buffering | 16 | 5 | 2 | **7** | P1 | TBD-12 (session count), TBD-13 (buffering duration), OI-08 |
| `WP-P24` | Network configuration from the host: static IP, DHCP control, MAC read-back, remaining 0xDD queries | 7 | 4 | 2 | **6** | P3 | none |
| `WP-P25` | Internal CAN bus robustness: bus-off recovery, stale-data detection, priority IDs, error counters, load budget | 10 | 5 | 3 | **8** | P1 | TBD-20, TBD-21 (bit rates), TBD-22 (load budget), TBD-24 (staleness), OI-27 |
| `WP-P26` | Performance, timing and memory budgets: control interval, jitter, headroom, WCET, .bss verification | 8 | 4 | 5 | **9** | P1 | TBD-28 (control interval), TBD-29, TBD-30, TBD-31, TBD-32, OI-03, OI-31 |
| `WP-P27` | Scale out to 64 channels: 8 Secondaries x 8 channels, CAN Block 2, capacity proof | 9 | 5 | 4 | **9** | P1 | TBD-18 / OI-24 (is the address range 8x8 or 15x15) |
| `WP-P28` | Charge/discharge change-over: bank type per channel and enforced dead time | 1 | 2 | 2 | **4** | P2 | TBD-03 (dead time), OI-06 (who sequences it) |
| `WP-P29` | Analog output commanded as engineering values (close out the voltage half) | 1 | 1 | 1 | **2** | P3 | T-66 (should CCChg ever carry a non-zero voltage) |
| `WP-P30` | Version reporting and Secondary compatibility gate | 2 | 2 | 1 | **3** | P2 | none beyond WP-P01 |
| `WP-P31` | Control-software stall detection driving all channels to safe state | 1 | 3 | 2 | **5** | P1 | OI-05 |
| `WP-P32` | Close out the known open code items | 0 | 3 | 2 | **5** | P1 | none |

### Battery Manager web application

| WP | Title | Reqs | Impl | HIL | Total | Prio | Blocked by |
|---|---|---:|---:|---:|---:|---|---|
| `WP-B01` | Channel overview and per-channel detail views for 64 channels | 3 | 10 | 0 | **10** | P1 | none |
| `WP-B02` | Program editor and validator covering every operator and action | 15 | 20 | 0 | **20** | P1 | OI-16, OI-17, OI-20, OI-21, OI-22, OI-18 |
| `WP-B03` | Program library, reuse across channels and sessions, assignment tracking | 3 | 8 | 0 | **8** | P1 | none |
| `WP-B04` | Group operations across channels with per-channel outcome reporting | 2 | 6 | 0 | **6** | P1 | none |
| `WP-B05` | Live trends and scope-aware alarm and fault display | 2 | 8 | 0 | **8** | P2 | OI-11 (error catalogue) |
| `WP-B06` | Test record storage, integrity marking and documented export | 3 | 10 | 0 | **10** | P1 | TBD-26 |
| `WP-B07` | Per-channel configuration workflows for sections 8 and 9 | 10 | 10 | 0 | **10** | P1 | TBD-15, and the digital I/O enable rows need WP-P14/WP-P15 |
| `WP-B08` | Calibration workflows per channel | 0 | 6 | 0 | **6** | P2 | TBD-16, OI-13, OI-14 |
| `WP-B09` | CAN port, message and signal configuration UI with full validation | 10 | 18 | 0 | **18** | P3 | OI-10 (identifier width), TBD-14, OI-07 |
| `WP-B10` | RS-485 configuration UI for both ports | 1 | 5 | 0 | **5** | P3 | TBD-05 through TBD-11 |
| `WP-B11` | Network configuration UI: static IP, DHCP, MAC display | 0 | 3 | 0 | **3** | P3 | none |
| `WP-B12` | Log retrieval and filtering by channel, board, severity and time | 0 | 5 | 0 | **5** | P2 | TBD-34 |
| `WP-B13` | Reject unachievable sampling and registration rate combinations | 1 | 5 | 0 | **5** | P1 | OI-12, TBD-22, TBD-30 |
| `WP-B14` | Error catalogue presentation with meaning, scope, severity and operator action | 0 | 4 | 0 | **4** | P1 | OI-11 |
| `WP-B15` | Enrolment and software version display | 1 | 4 | 0 | **4** | P2 | OI-26 |
| `WP-B16` | Access control and per-action audit trail | 1 | 6 | 0 | **6** | Phase 2 | OI-33 |
| `WP-B17` | Wire-protocol conformance: CRC order, float encoding, ack semantics, session IDs | 0 | 5 | 0 | **5** | P1 | TBD-25 / OI-28 (CRC byte order) |

## Packages that trace no requirement exclusively

Every requirement in this matrix has exactly ONE owning package, so a `BM + Primary` requirement sits with whichever side carries its remaining work. The packages below are therefore real, estimated work with no requirement row of their own - the other half of a shared requirement, or debt from the T-register rather than from the SRS. They are listed here so nobody reads a zero in the `Reqs` column as a package that can be deleted.

| WP | Total | Why it has no rows |
|---|---:|---|
| `WP-P32` | 5d | No SRS requirement traces to this package, by design |
| `WP-B08` | 6d | Cannot start before the procedure exists. Pairs with WP-P20, which owns REQ_145, REQ_160, REQ_161 and REQ_162 - all `BM + Primary`, so this package is their Battery Manager half and traces no requirement exclusively. |
| `WP-B11` | 3d | Pairs with WP-P24, which owns REQ_54, REQ_55 and REQ_56 - all `BM + Primary`, so this package is their Battery Manager half and traces no requirement exclusively. |
| `WP-B12` | 5d | REQ_323 is `BM + Primary` and is owned by WP-P12, which has to serve the logs before anything can filter them; this package is its Battery Manager half and traces no requirement exclusively. |
| `WP-B14` | 4d | REQ_320's catalogue has to be authored once and then rendered. REQ_82, REQ_302 and REQ_320 are all `BM + Primary` and are owned by WP-P11/WP-P10 on the reporting side, so this package is their Battery Manager half and traces no requirement exclusively. |
| `WP-B17` | 5d | No SRS requirement traces here exclusively |

## Package detail

### WP-P01 - Secondary Board enrolment and supervision over the internal bus

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 6 d implement + 3 d hardware = **9 engineer-days** |
| Priority | P1 |
| Blocked by | OI-26, OI-25, TBD-19 |
| Requirements | 11 |

The largest single gap between the code as built and the SRS. Today the board learns its population from `--channels` on the command line and is admitted only by the host-side `0xDD` handshake; nothing on the CAN bus announces itself. Everything in section 13, and REQ_293's 'enrolled Secondary' precondition, waits on this.

**Requirements covered** (PARTIAL 2, TODO 5, BLOCKED 4):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_249` | *PARTIAL* | The system shall operate correctly with fewer than the maximum number of Secondary Boards fitted, and with fewer than the maximum number of channels populated on a fitted Secondary Board. |
| `ME_SW_REQ_251` | **BLOCKED** | The Primary shall discover and enrol each Secondary Board present on the internal bus, without manual configuration of the bus population. |
| `ME_SW_REQ_252` | TODO | Enrolment of a Secondary Board shall establish and record at least: its Secondary ID, its hardware identity, its software or firmware version, the number of channels it provides, and the transistor bank type of each o... |
| `ME_SW_REQ_253` | *PARTIAL* | The Primary shall not send any channel command to a Secondary Board that is not enrolled. |
| `ME_SW_REQ_254` | TODO | If two Secondary Boards present the same Secondary ID, the Primary shall enrol neither, shall report the conflict identifying both, and shall continue to serve all correctly enrolled boards. |
| `ME_SW_REQ_255` | **BLOCKED** | The Primary shall monitor the liveness of every enrolled Secondary Board and shall declare a board lost if no valid response or periodic message is received from it within <TBD-19> ms. |
| `ME_SW_REQ_256` | TODO | When a Secondary Board is declared lost, the Primary shall place the tests of that board's channels into a defined fault state, shall report the loss naming the board and its affected channels, and shall leave the cha... |
| `ME_SW_REQ_257` | **BLOCKED** | When a previously lost Secondary Board becomes reachable again, the Primary shall re-enrol it and shall verify that its identity and capability are unchanged before returning its channels to service. |
| `ME_SW_REQ_258` | TODO | The Primary shall tolerate a Secondary Board being removed or added while the system is powered, without disturbing the tests running on other Secondary Boards. |
| `ME_SW_REQ_259` | TODO | The Primary shall report the enrolment state of every Secondary Board, and the reason for any board not being enrolled, to the Battery Manager. |
| `ME_SW_REQ_260` | **BLOCKED** | The enrolment of channels beyond the first shall be implemented before Phase 1 is considered complete. |

### WP-P02 - Complete the operator set: PAU, DCH, INT, BEG/CYC, GOTO, REG, ERR, MSG, TABLE

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 12 d implement + 5 d hardware = **17 engineer-days** |
| Priority | P1 |
| Blocked by | OI-16 (REG), OI-17 (SET), OI-20 (DCH/INT/ERR/MSG), OI-22 (cycle nesting) |
| Requirements | 42 |

3 of 13 operators are built. This is the biggest functional package and the one most exposed to specification risk: four separate open issues say that nobody has written down what these operators do. BTS covered DCH - as complex as CHA - in a single line. Do not start the sub-program and cycle-nesting parts before OI-22 is answered; it was never built in BTS either, so committing to it in Phase 1 is a real and avoidable cost.

**Requirements covered** (PARTIAL 5, TODO 34, BLOCKED 2, DEFERRED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_168` | *PARTIAL* | The Primary shall hold and execute an independent Program for each channel. Up to 64 Programs shall be able to run concurrently, each with its own step pointer, its own elapsed timers, its own accumulated quantities a... |
| `ME_SW_REQ_174` | *PARTIAL* | The following operators shall be supported: 1. SET 2. PAU 3. CHA 4. DCH 5. INT 6. STO 7. BEG 8. CYC 9. GOTO 10. REG 11. ERR 12. MSG 13. TABLE |
| `ME_SW_REQ_175` | DEFERRED | The operators RCH, LOM, EIS and POL are not supported in Phase 1. |
| `ME_SW_REQ_177` | *PARTIAL* | The Primary shall support the SET operator. |
| `ME_SW_REQ_178` | TODO | If the Operator of a Step is PAU, the Primary shall command the channel's power contactors to be dropped and shall hold that channel in the paused condition until the specified Limit value has elapsed. |
| `ME_SW_REQ_181` | TODO | If the Primary receives a PAU operator with a Limit outside the range 10.0 seconds to 99.9 hours, it shall raise OPERATOR_PAU_LIMIT_INVALID_VALUE_ERROR for that channel and the Program of that channel shall not start. |
| `ME_SW_REQ_182` | TODO | If the Primary receives a PAU operator with a Nominal Value present, it shall raise OPERATOR_PAU_INVALID_PARAMETER_ERROR for that channel and the Program of that channel shall not be executed. |
| `ME_SW_REQ_184` | TODO | If a Step with the PAU operator contains a GOTO action with a valid destination, then after execution of PAU the Program of that channel shall jump to the Step whose Label is named in the GOTO action. |
| `ME_SW_REQ_185` | TODO | If the Primary receives a Step with the PAU operator and an action other than GOTO, it shall raise OPERATOR_PAU_INVALID_ACTION_ERROR for that channel and the Program of that channel shall not start. |
| `ME_SW_REQ_186` | TODO | If a Step contains the PAU operator with a valid Registration value, registration as per that value shall be performed for that channel during the execution of PAU. |
| `ME_SW_REQ_193` | TODO | If a CHA Step has no Nominal Value, or no Limit, or neither, the Primary shall raise OPERATOR_CHA_MANDETORY_PARAMETERS_MISSING_ERROR for that channel and the Program of that channel shall not be executed. |
| `ME_SW_REQ_195` | TODO | A Step with the CHA operator shall work with an Action, or a Registration, or both, if they are given. |
| `ME_SW_REQ_196` | TODO | For the CHA operator, if a Registration value is specified, the standard parameters of that channel shall be logged with the specified registration settings. |
| `ME_SW_REQ_197` | TODO | For the CHA operator, if the Registration value is blank, the standard parameters of that channel shall be logged with the default settings. |
| `ME_SW_REQ_200` | TODO | The Primary shall support the INT operator, which shall place the channel's Program in the interrupted state. |
| `ME_SW_REQ_201` | *PARTIAL* | The STO operator shall terminate the Program of the channel executing it, and shall not affect any other channel. |
| `ME_SW_REQ_203` | TODO | It shall be possible to use the STO operator in sub-programs. |
| `ME_SW_REQ_206` | TODO | If the Primary receives a Program containing an STO operator that also carries a Nominal Value, a Limit, an Action or a Registration value, or any combination of these, it shall raise PROGRAM_STO_EXCEPTION_ERROR for t... |
| `ME_SW_REQ_207` | TODO | The Primary shall repeat the Steps following a BEG operator until the cycle is terminated by a CYC operator, independently for each channel. |
| `ME_SW_REQ_208` | TODO | The Primary shall execute the Steps of a cycle the number of times assigned to the cycle's nominal value. |
| `ME_SW_REQ_209` | TODO | The Primary shall ensure that all operations within a cycle execute sequentially unless a Step is interrupted by a GOTO operator or action. |
| `ME_SW_REQ_210` | TODO | The Primary shall ensure that all operations within a cycle execute sequentially unless a Step is interrupted by an INT operator or action. |
| `ME_SW_REQ_211` | TODO | The Primary shall ensure that all operations within a cycle execute sequentially unless a Step is interrupted by an STO operator or action. |
| `ME_SW_REQ_212` | TODO | If a GOTO action names a Step that lies outside and before the cycle, the Primary shall execute that Step and then return to the execution of the cyclic process. |
| `ME_SW_REQ_213` | TODO | It shall be possible to add a Label to Steps inside a cycle so that predefined counter values are not reset. |
| `ME_SW_REQ_214` | TODO | The Primary shall execute the Step named in a GOTO action and jump to the end of the test if the prescribed limits have been reached. |
| `ME_SW_REQ_215` | TODO | The Primary shall move to the next Step inside a cycle if the prescribed limits of the current Step have been reached and no Action has been set. |
| `ME_SW_REQ_216` | **BLOCKED** | The Primary shall support the nesting of up to 16 cycles within one channel's Program, without conflict between their counters. |
| `ME_SW_REQ_217` | **BLOCKED** | For nested cycles the Primary shall maintain a separate iteration counter for each cycle level, shall complete all iterations of an inner cycle before advancing the outer cycle, and shall reset the counters of an inne... |
| `ME_SW_REQ_218` | TODO | Cycle counters, including the predefined Ah and Wh counters, shall be maintained per channel and shall be reported per channel. |
| `ME_SW_REQ_219` | TODO | If an external condition or error is encountered, the Primary shall log the cycle status of the affected channel and abort execution of that channel's Program as per the system-defined limits. |
| `ME_SW_REQ_221` | TODO | The Primary shall perform the Action of a Step if the prescribed Limit of that Step has been reached. |
| `ME_SW_REQ_222` | *PARTIAL* | The Primary shall move to the next Step of that channel's Program if the predefined Limit is reached and the Action is blank. |
| `ME_SW_REQ_223` | TODO | The following Actions shall be supported: 1. INT 2. STO 3. GOTO 4. <Procedure Name> 5. ERR 6. MSG |
| `ME_SW_REQ_224` | TODO | When the associated Limit of the Step being executed is reached and the Action is INT, the Program of that channel shall be interrupted. |
| `ME_SW_REQ_226` | TODO | When the associated Limit of the Step being executed is reached and the Action is STO, the Primary shall terminate the Step being executed and shall advance to the next Step of that channel's Program. |
| `ME_SW_REQ_227` | TODO | When the associated Limit of the Step being executed is reached and the Action is GOTO, the Program of that channel shall jump to the Step that carries the named Jump Destination in its Label column. |
| `ME_SW_REQ_228` | TODO | When the associated Limit of the Step being executed is reached and the Action names a Procedure, the Primary shall execute the named Procedure for that channel. |
| `ME_SW_REQ_229` | TODO | After execution of a Procedure the Primary shall execute the Step following the Step from which the Procedure was called. |
| `ME_SW_REQ_230` | TODO | The Primary shall support the ERR Action. |
| `ME_SW_REQ_231` | TODO | The Primary shall support the MSG Action. |
| `ME_SW_REQ_232` | TODO | Every message or error produced by an ERR or MSG Action shall identify the channel and the Step number that produced it. |

### WP-P03 - CHA/DCH control modes: constant-voltage, CC-to-CV, non-time limits, current clamping

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 6 d implement + 4 d hardware = **10 engineer-days** |
| Priority | P1 |
| Blocked by | OI-20, and REQ_198 needs a temperature source binding |
| Requirements | 8 |

CCChg today is constant-current only, with a TIME cut-off and no Action. This package adds the CV mode, the CC-then-CV transition of REQ_194, the Voltage/Ah/Temperature limits, and - separately important - the REQ_150/REQ_155 clamp that stops a program commanding more current than the channel's configured maximum. The clamp cannot be built before WP-P19 stores that maximum.

**Requirements covered** (PARTIAL 2, TODO 6):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_150` | TODO | The Primary shall enforce, for each channel, that no commanded charging current exceeds the Max Charging Current configured for that channel, irrespective of what the channel's program requests. |
| `ME_SW_REQ_155` | TODO | The Primary shall enforce, for each channel, that no commanded discharging current exceeds the Max Discharging Current configured for that channel, irrespective of what the channel's program requests. |
| `ME_SW_REQ_187` | *PARTIAL* | When the Operator of a Step is CHA, the Primary shall perform the charging operation on that channel taking account of the Nominal Value, Limit, Action and Registration fields. |
| `ME_SW_REQ_189` | *PARTIAL* | If the Nominal Value of a CHA Step is a Current, the Primary shall charge that channel in constant-current mode until the specified Limit is reached. After the Limit is crossed it shall perform the Action if one is sp... |
| `ME_SW_REQ_191` | TODO | If the Nominal Value of a CHA Step is a Voltage, the Primary shall charge that channel in constant-voltage mode until the specified Limit is reached. After the Limit is crossed it shall perform the Action if one is sp... |
| `ME_SW_REQ_194` | TODO | If both a Current and a Voltage Nominal Value are given with their Limits in one CHA Step, the Primary shall charge that channel in constant-current mode until the specified nominal voltage is reached, then in constan... |
| `ME_SW_REQ_198` | TODO | If a CHA or DCH Step uses Temperature as its Limit, the Primary shall use the temperature source bound to that channel. If no temperature source is bound to that channel, the Program shall not start and the reason sha... |
| `ME_SW_REQ_199` | TODO | The Primary shall support the DCH operator for discharge testing. All rules stated for the CHA operator in section 10.3.3 shall apply to DCH with the direction of current reversed, and with the Max Discharging Current... |

### WP-P04 - Cut-off condition engine: 13 condition types, 6 comparators, multiple per step, debounce

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 2 d hardware = **7 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-36 (how many per step), TBD-37 (debounce count) |
| Requirements | 7 |

1 of 13 types and 2 of 6 comparators today, one condition per step, no debounce at all. The refusal behaviour already in `step_decode.c` is correct and should be preserved: a wire count above the cap is rejected rather than folded to 0, because the old firmware produced a step that never ends that way. REQ_349's debounce is cheap to add and materially changes behaviour on a noisy channel - do not defer it as cosmetic.

**Requirements covered** (DONE 1, PARTIAL 3, BLOCKED 3):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_346` | *PARTIAL* | For each Step of a channel's Program, the Primary shall support configuring one or more cut-off conditions of the following types: Current, Voltage, Power, Charge Capacity, Discharge Capacity, Charge Energy, Discharge... |
| `ME_SW_REQ_347` | **BLOCKED** | Up to <TBD-36> cut-off conditions may be configured on a single Step. |
| `ME_SW_REQ_348` | *PARTIAL* | Each cut-off condition shall be evaluated against a configured limit value using one of the following comparisons: greater-than, less-than, greater-than-or-equal, less-than-or-equal, not-equal, or equal-to. |
| `ME_SW_REQ_349` | **BLOCKED** | A cut-off condition shall be treated as met only after its comparison has been continuously true for more than <TBD-37> consecutive evaluations, so that a transient or noise-driven excursion does not trigger the condi... |
| `ME_SW_REQ_350` | *PARTIAL* | When a cut-off condition is met, the Primary shall carry out exactly one configured action for that condition: jump execution to a specified Step number (GOTO), end the Step and raise a fault with a specified error co... |
| `ME_SW_REQ_351` | **DONE** | The Primary shall evaluate every configured cut-off condition of a channel's current Step using that channel's live measurements as reported by its Secondary Board, and shall itself carry out the resulting action; the... |
| `ME_SW_REQ_352` | **BLOCKED** | Where more than one cut-off condition is configured on a Step, the Primary shall evaluate them in configuration order each cycle and act on the first condition found to be met in that pass. |

### WP-P05 - Test record production and delivery on UDP 10001

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 2 d hardware = **6 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-26 (registration types and interval range) |
| Requirements | 4 |

The transport is already built end to end - socket, destination, a 64-deep ring in `data_mgr.c` and `ring_flush()` - and carries nothing (T-29). The raw material is also already decoded and thrown away: SET's 13-bit registration mask and CCChg's registration-parameter list (`0x21`-`0x2D`). This package is mostly about deciding what a record IS, which is why TBD-26 gates it. REQ_285's no-silent-loss guarantee and REQ_288's session identity land here.

**Requirements covered** (PARTIAL 1, TODO 2, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_275` | *PARTIAL* | Test record data shall be sent from the Primary to the Battery Manager on UDP port 10001. |
| `ME_SW_REQ_285` | TODO | Test record data shall never be discarded silently. Every test record produced by the Primary shall either be delivered to the Battery Manager, or be reported as lost, identifying the channel and the time span affected. |
| `ME_SW_REQ_286` | **BLOCKED** | The registration type and registration interval shall be configurable per channel, within the range <TBD-26>. |
| `ME_SW_REQ_288` | TODO | Each test record shall carry the identifier of the test session it belongs to, so that records from a repeated test on the same channel cannot be confused with one another. |

### WP-P06 - Live data completeness: fill the zeroed 0xCC fields

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 1 d hardware = **4 engineer-days** |
| Priority | P2 |
| Blocked by | none |
| Requirements | 4 |

The 86-byte frame already has a field for nearly everything REQ_283 asks for, and all 80 payload offsets are asserted on the host. What the engine fills is step number, both statuses, both run times, current, voltage and operator code; temperature, power, the four capacity/energy accumulators, cycle and table position, registration type and digital I/O state go out as zero. Cheap, visible to the operator, and independent of everything else - a good early win.

**Requirements covered** (DONE 3, PARTIAL 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_274` | **DONE** | Live measurement data shall be sent from the Primary to the Battery Manager on UDP port 10000. |
| `ME_SW_REQ_283` | *PARTIAL* | The Primary shall report live measurement data for every running channel, each report carrying at least: step number, program running status, channel status, error identifier, step running time, program running time, ... |
| `ME_SW_REQ_284` | **DONE** | Live measurement data shall be treated as valuable only while fresh. If the outbound path is congested, the Primary shall discard the stale report rather than delay a newer one, and shall count every discard. |
| `ME_SW_REQ_287` | **DONE** | All floating-point values on any external interface shall be encoded as IEEE 754 single-precision, in the byte order fixed by ME_SW_REQ_277. |

### WP-P07 - Non-volatile persistence of configuration, calibration and programs

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 2 d hardware = **7 engineer-days** |
| Priority | P1 |
| Blocked by | OI-15 (medium, on-media format, partial-write handling) |
| Requirements | 2 |

Everything is volatile today; a restart loses every program, battery record and raw config. For 64 channels that is a large amount of operator work lost to a power blip. OI-15 must settle the medium and the partial-write rule first - a torn write that yields a plausible-looking calibration constant is worse than no persistence.

**Requirements covered** (BLOCKED 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_163` | **BLOCKED** | Calibration data shall survive a power failure and a Primary software restart. |
| `ME_SW_REQ_313` | **BLOCKED** | Channel configuration, calibration data and stored Programs shall survive a power failure and a Primary software restart. |

### WP-P08 - Power-fail detection, snapshot and automatic resume

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 3 d hardware = **8 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-35 (guaranteed warning time - HARDWARE), OI-15, OI-34 (safety of automatic resume) |
| Requirements | 6 |

Blocked on hardware, not software: TBD-35 is a property of the Primary Board's power-supervisory circuit and no software design is possible without the number. OI-34 is live - the client has directed automatic resume and has flagged that the decision may change after a safety discussion, and REQ_314 forbids exactly what REQ_344 requires. Do not build until both are closed.

**Requirements covered** (TODO 2, BLOCKED 4):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_341` | **BLOCKED** | The Primary shall detect an impending loss of input power with sufficient advance warning to complete the snapshot of ME_SW_REQ_342 for every populated channel before its own supply collapses. The minimum guaranteed w... |
| `ME_SW_REQ_342` | **BLOCKED** | On detecting an impending power loss, the Primary shall save to non-volatile memory a snapshot of every populated channel's execution state, comprising at least: the assigned Program identity, the current Step number,... |
| `ME_SW_REQ_343` | TODO | On power-up, before resuming any channel, the Primary shall validate the integrity of that channel's saved snapshot (for example by checksum). A channel whose snapshot fails validation, or for which no snapshot exists... |
| `ME_SW_REQ_344` | **BLOCKED** | For a channel whose snapshot passes validation, the Primary shall automatically restore its saved state and resume execution of its Program from the saved Step and elapsed time, without requiring operator confirmation. |
| `ME_SW_REQ_345` | TODO | Whenever a channel resumes automatically under ME_SW_REQ_344, the Primary shall report the resumption, the outage duration, and the Step it resumed from, to the Battery Manager and to that channel's event history (ME_... |
| `ME_SW_REQ_353` | **BLOCKED** | The debounce state of every active cut-off condition on a channel's current Step shall be included in the power-fail snapshot of ME_SW_REQ_342, so that a channel resumed under ME_SW_REQ_344 continues debounce counting... |

### WP-P09 - Startup gating, commanded shutdown, restart semantics

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 2 d hardware = **5 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-33 (time to ready), OI-05 (safe state) |
| Requirements | 5 |

`main.c` today starts all four threads and registers immediately, with no enrolment or configuration gate before the first command can land, and shutdown drops the stop flag and joins without bringing any channel to a safe state. Depends on WP-P01 for the enrolment gate and WP-P07 for the restart record.

**Requirements covered** (PARTIAL 1, TODO 3, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_312` | TODO | On start-up the Primary shall not command any channel until it has enrolled the Secondary Boards and validated their configuration. |
| `ME_SW_REQ_314` | TODO | After an unexpected restart, the Primary shall not silently resume any test. For every channel that was running it shall record that the test was interrupted by a restart, and shall require an explicit operator decisi... |
| `ME_SW_REQ_315` | TODO | Test records already produced before an unexpected restart shall remain retrievable, and the gap caused by the restart shall be explicit in the record. |
| `ME_SW_REQ_316` | *PARTIAL* | On a commanded shutdown the Primary shall bring every channel to its safe state, shall flush all pending test records, and shall only then stop. |
| `ME_SW_REQ_319` | **BLOCKED** | The Primary shall be ready to accept commands within <TBD-33> seconds of power being applied. |

### WP-P10 - Fault containment: classify every fault by scope and act on the scope

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 2 d hardware = **6 engineer-days** |
| Priority | P1 |
| Blocked by | OI-05 (safe state), OI-11 (error catalogue) |
| Requirements | 5 |

Per-circuit isolation is genuinely real - an engine fails to `CHANNEL_OFFLINE` or `DECODE_ERROR` without touching its neighbours - but there is no notion of a classified fault to inhibit on, and no board-scope or system-scope concept at all. Both blockers are answered by one conversation each; this package is cheap once they are.

**Requirements covered** (PARTIAL 2, TODO 1, BLOCKED 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_297` | **BLOCKED** | Every fault the Primary can detect shall be classified by scope as a channel fault, a Secondary Board fault, or a system fault. |
| `ME_SW_REQ_298` | *PARTIAL* | A channel fault shall stop or inhibit only the affected channel. All other channels shall continue unaffected. |
| `ME_SW_REQ_299` | TODO | A Secondary Board fault shall stop or inhibit only the channels of that board. The channels of all other boards shall continue unaffected. |
| `ME_SW_REQ_300` | **BLOCKED** | A system fault shall bring all channels to their safe state, and shall be reported as system-scoped so that the operator is not left looking for a faulty channel. |
| `ME_SW_REQ_301` | *PARTIAL* | A fault on one channel shall not prevent a test from being started on any other healthy channel. |

### WP-P11 - Error code catalogue and structured error reporting to the Battery Manager

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 1 d hardware = **4 engineer-days** |
| Priority | P1 |
| Blocked by | OI-11 |
| Requirements | 2 |

BTS asked 'how do we indicate this error?' in nine separate places and never answered. Nine ME requirements depend on the single answer, and REQ_82's structured record - code, severity, subsystem, channel, time - is the shape everything else reports through. Answering OI-11 is the highest-leverage hour available in this whole plan.

**Requirements covered** (BLOCKED 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_302` | **BLOCKED** | Every fault report shall carry its scope, the affected channel or board, an error code, a severity, and the time of detection. |
| `ME_SW_REQ_320` | **BLOCKED** | A catalogue of every error code the Primary can report shall be maintained, giving for each code its meaning, its scope, its severity and the recommended operator action. |

### WP-P12 - Persistent, retrievable, filterable log with per-channel history

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 1 d hardware = **5 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-34 (retention) |
| Requirements | 5 |

`util/log.c` writes to stderr and nothing survives a restart. REQ_324's 'logging shall never delay a control decision' is accidentally true today because there is nothing to block on - adding storage is exactly what puts it at risk, so the write path must stay off the control thread by design rather than by luck.

**Requirements covered** (PARTIAL 1, TODO 3, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_321` | **BLOCKED** | The Primary shall maintain a log of events, faults and operator commands, time-stamped on the system timebase, and shall retain at least <TBD-34> of history. |
| `ME_SW_REQ_322` | TODO | The Primary shall maintain a per-channel event history covering at least the start, stop, step transitions, faults and operator interventions of that channel's test. |
| `ME_SW_REQ_323` | TODO | It shall be possible to retrieve the Primary's logs through the Battery Manager, filtered by channel, by Secondary Board, by severity and by time range. |
| `ME_SW_REQ_324` | *PARTIAL* | Logging shall never delay a control decision, a safety action or the delivery of a test record. If log storage is unavailable or full, the Primary shall continue to operate and shall report the logging failure. |
| `ME_SW_REQ_325` | TODO | Log records shall be retained across a power failure and a software restart. |

### WP-P13 - Real Time Clock: I2C device, dual-format time stamps, sync from host, distribution to Secondaries

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 3 d hardware = **7 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-04 (permitted Primary-to-Secondary offset) |
| Requirements | 7 |

HARDWARE-CRITICAL - I2C register access; needs the datasheet and hardware-in-the-loop verification. `0xEE` Q5 Sync Time is already parsed with its big-endian epoch and deliberately not applied. Note REQ_31: a clock correction must not put a discontinuity into a running test's elapsed time - which is straightforward now, because `step_engine.c` takes `now_ms` as a parameter and never reads a clock itself, and much harder if that property is ever lost.

**Requirements covered** (PARTIAL 2, TODO 4, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_25` | *PARTIAL* | The Primary software shall provide a time-stamp for all system logs, containing both Epoch time and calendar time in the form YYYY-MM-DD HH:MM:SS. |
| `ME_SW_REQ_26` | *PARTIAL* | The Primary software shall synchronise its RTC with the Battery Manager application for time correction on request. |
| `ME_SW_REQ_27` | TODO | The Primary software shall communicate with the RTC device over I2C to set and retrieve time data. |
| `ME_SW_REQ_28` | TODO | If the RTC fails to provide valid time data during start-up, the Primary software shall set the time to a default value, shall log an error, and shall refuse to start any new test until valid time is available. |
| `ME_SW_REQ_29` | TODO | The Primary shall be the single time reference for the whole ME system. All test records, live data samples and log entries from all channels shall be expressed on that one timebase. |
| `ME_SW_REQ_30` | **BLOCKED** | The Primary software shall distribute system time to every enrolled Secondary Board, and shall keep the time of every Secondary within <TBD-04> ms of its own. |
| `ME_SW_REQ_31` | TODO | An RTC correction shall not introduce a discontinuity in the elapsed-time or integrated-quantity calculation of any test that is running. Every correction shall be logged with its magnitude, and any test running at th... |

### WP-P14 - Primary digital inputs: edge detection, time stamping, enable/disable, Secondary input read-through

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 2 d hardware = **5 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-01 (time-stamp resolution) |
| Requirements | 6 |

HARDWARE-CRITICAL - GPIO. No digital-input path exists in `src/` at all. The Secondary read-through half of REQ_9 depends on WP-P01.

**Requirements covered** (TODO 5, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_3` | TODO | The Primary software shall detect a voltage level change from Low to High on each of its 4 digital inputs. Low level is 0 V, High level is 24 V. |
| `ME_SW_REQ_4` | TODO | The Primary software shall detect a voltage level change from High to Low on each of its 4 digital inputs. Low level is 0 V, High level is 24 V. |
| `ME_SW_REQ_5` | **BLOCKED** | The Primary software shall time-stamp every detected digital input transition from the system time reference, with a resolution of at least <TBD-01> ms. |
| `ME_SW_REQ_7` | TODO | The Primary software shall honour the digital input enable/disable configuration and shall not act on, report or log transitions of a disabled digital input. |
| `ME_SW_REQ_8` | TODO | It shall be possible to read the state of any digital input through the Ethernet port, if that port is enabled for communication with the host. |
| `ME_SW_REQ_9` | TODO | The Primary software shall make the state of the digital inputs of every enrolled Secondary Board readable by the Battery Manager, each addressed by its Secondary ID and input index. |

### WP-P15 - Primary digital outputs: level command, enable/disable, safe state on link loss

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 2 d hardware = **5 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-02 (safe-state time), OI-05 (what 'safe' is) |
| Requirements | 7 |

HARDWARE-CRITICAL - GPIO. REQ_21's safe state on link loss or fault is the safety-relevant half and should not be built from a guess about what 'safe' means.

**Requirements covered** (TODO 6, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_13` | TODO | The Primary software shall set each of its digital outputs to 0 V or 24 V according to the logical command it receives. |
| `ME_SW_REQ_14` | TODO | If a Primary digital output is given the logical command LOW, that output shall be 0 V. |
| `ME_SW_REQ_15` | TODO | If a Primary digital output is given the logical command HIGH, that output shall be 24 V. |
| `ME_SW_REQ_17` | TODO | The Primary software shall reject any command addressed to a disabled digital output, shall leave that output unchanged, and shall report the rejection to the requester. |
| `ME_SW_REQ_18` | TODO | It shall be possible to control any digital output through the Ethernet port, if that port is enabled for communication with the host. |
| `ME_SW_REQ_19` | TODO | The Primary software shall route a digital output command addressed to a Secondary output to the Secondary Board identified in the command, and shall report to the requester whether the addressed Secondary accepted it. |
| `ME_SW_REQ_21` | **BLOCKED** | On loss of the Battery Manager link, or on detection of a Primary-level fault, the Primary software shall drive each of its digital outputs to a configured safe state within <TBD-02> ms. |

### WP-P16 - RS485_1: Modbus RTU slave towards a third-party HMI

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 6 d implement + 3 d hardware = **9 engineer-days** |
| Priority | P3 |
| Blocked by | TBD-05 (register map for 64 channels), OI-07 |
| Requirements | 5 |

No Modbus stack and no RS-485 port code exist. OI-07 is the real problem, not the protocol: 64 channels times REQ_35's parameter list does not fit a flat register map, so the map is either paged, or operator-selected, or a published subset - and the HMI integrator needs that fixed before anything is written.

**Requirements covered** (TODO 4, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_32` | TODO | The RS485_1 port shall support the MODBUS RTU protocol for interfacing with a third party HMI. |
| `ME_SW_REQ_33` | TODO | The RS485_1 port shall operate as a MODBUS slave node. |
| `ME_SW_REQ_34` | TODO | It shall be possible to configure the following for RS485_1: 1. ME Device ID 2. HMI Device ID 3. Baud Rate 4. Parity 5. Stop Bits |
| `ME_SW_REQ_35` | TODO | It shall be possible to send the following parameters to the HMI over RS485_1 using MODBUS, each qualified by the channel it belongs to: 1. Battery Voltage 2. Current 3. Test Status 4. Current Operation (Charging / Di... |
| `ME_SW_REQ_36` | **BLOCKED** | The MODBUS register map of RS485_1 shall be capable of addressing all channels of all enrolled Secondary Boards. The register layout shall be defined as <TBD-05>. |

### WP-P17 - RS485_2: Modbus master reading third-party devices, with value binding to channels

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 6 d implement + 3 d hardware = **9 engineer-days** |
| Priority | P3 |
| Blocked by | TBD-06, TBD-07, TBD-08, TBD-09, TBD-10, TBD-11 |
| Requirements | 10 |

Six open numbers, all inherited verbatim from BTS as 'XXX'. REQ_47 - binding a value read here to a named quantity of a channel, usable as a Nominal Value or Limit source - is the interesting requirement and the one that touches the step engine.

**Requirements covered** (TODO 5, BLOCKED 5):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_38` | TODO | The RS485_2 port shall be able to read data from external third party devices. |
| `ME_SW_REQ_39` | TODO | The RS485_2 port shall be configurable to be used with the MODBUS protocol. |
| `ME_SW_REQ_40` | TODO | It shall be possible to set the baud rate of the RS485_2 port up to 115200 bps. |
| `ME_SW_REQ_41` | **BLOCKED** | The MODBUS protocol on RS485_2 shall use 8 data bits, 1 stop bit, and even, odd or no parity as configured. The permitted combinations are <TBD-06>. |
| `ME_SW_REQ_42` | TODO | The MODBUS frame on RS485_2 shall consist of Address Field, Function Code, Data Field and CRC. |
| `ME_SW_REQ_43` | **BLOCKED** | The MODBUS implementation on RS485_2 shall support function codes <TBD-07>. |
| `ME_SW_REQ_44` | **BLOCKED** | The MODBUS implementation on RS485_2 shall support RTU mode with a response timeout of <TBD-08> ms. |
| `ME_SW_REQ_45` | **BLOCKED** | The RS485_2 network shall support up to <TBD-09> devices on the same bus. |
| `ME_SW_REQ_46` | **BLOCKED** | The RS485_2 network shall allow configuration of the slave address in the range <TBD-10> to <TBD-11>. |
| `ME_SW_REQ_47` | TODO | It shall be possible to bind a value read on RS485_2 to a named quantity of a specified channel, so that the bound value can be used as a Nominal Value source or a Limit source by that channel's program. |

### WP-P18 - User-configurable CAN ports: port/message/signal configuration, subscribe, publish, MTO

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 15 d implement + 6 d hardware = **21 engineer-days** |
| Priority | P3 |
| Blocked by | TBD-14 (which port), OI-09 (physically distinct?), OI-10 (11-bit, 29-bit or both) |
| Requirements | 58 |

69 requirements, the largest block in the SRS. `proto/can_frame.c` is NOT a starting point - it is the fixed 64-byte internal-bus codec, little-endian, with a hard-coded block/slot layout, and sharing anything with a configurable-port layer would break the one-endianness-per-file rule the project enforces deliberately. OI-09 is a hardware question that can invalidate REQ_270: if a user port shares a controller or transceiver with the internal bus, the internal bus cannot be kept unexposed.

**Requirements covered** (TODO 53, BLOCKED 1, DEFERRED 4):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_10` | DEFERRED | It shall be possible to transmit the value read from any digital input on any CAN port as a 1-bit signal. |
| `ME_SW_REQ_20` | DEFERRED | It shall be possible to control any digital output from any 1-bit signal received in any subscribed message on any CAN port. |
| `ME_SW_REQ_65` | **BLOCKED** | One CAN port, <TBD-14>, shall be reserved exclusively for the ME internal bus that connects the Primary to the Secondary Boards. That port shall not be available for user configuration, and the requirements of section... |
| `ME_SW_REQ_66` | TODO | All user-configurable CAN ports shall support the following baud rates: 125 kbps, 250 kbps, 500 kbps and 1 Mbps. |
| `ME_SW_REQ_67` | TODO | All user-configurable CAN ports shall support both standard (11-bit) and extended (29-bit) CAN identifiers. |
| `ME_SW_REQ_68` | TODO | It shall be possible to receive CAN messages on each user-configurable CAN port as per its configuration. |
| `ME_SW_REQ_69` | TODO | It shall be possible to extract the configured signals from messages received on any user-configurable CAN port. |
| `ME_SW_REQ_70` | TODO | It shall be possible to send received CAN message data to the Battery Manager application. |
| `ME_SW_REQ_71` | TODO | All user-configurable CAN ports shall be able to transmit messages periodically as per their configuration. |
| `ME_SW_REQ_72` | TODO | It shall be possible to send any data held by the Primary on any user-configurable CAN port. |
| `ME_SW_REQ_73` | TODO | It shall be possible to map a signal received on any user-configurable CAN port onto a named Primary data item. |
| `ME_SW_REQ_74` | TODO | It shall be possible to map a named Primary data item onto a signal of a message configured for transmission on any user-configurable CAN port. |
| `ME_SW_REQ_75` | TODO | For each user-configurable CAN port the Primary shall receive the following groups of configuration: 1. CAN Port Configuration 2. Message Configuration 3. Signal Configuration |
| `ME_SW_REQ_76` | TODO | For each user-configurable CAN port the following port configuration parameters shall exist: 1. Baud Rate 2. Subscribed CAN Messages 3. Published CAN Messages |
| `ME_SW_REQ_77` | TODO | It shall be possible to set a CAN baud rate of 125 kbps through configuration. |
| `ME_SW_REQ_78` | TODO | It shall be possible to set a CAN baud rate of 250 kbps through configuration. |
| `ME_SW_REQ_79` | TODO | It shall be possible to set a CAN baud rate of 500 kbps through configuration. |
| `ME_SW_REQ_80` | TODO | It shall be possible to set a CAN baud rate of 1 Mbps through configuration. |
| `ME_SW_REQ_81` | TODO | If the configured baud rate is not one of the supported values, the Primary shall raise an error notification and shall leave that CAN port disabled. |
| `ME_SW_REQ_82` | TODO | Every error notification raised by the Primary shall be reported to the Battery Manager as a structured error record containing at least: error code, severity, originating subsystem, affected channel or port, and time... |
| `ME_SW_REQ_83` | TODO | A message that a CAN port is expected to read and consume shall be configured as a Subscribed CAN Message by the configuration tool. |
| `ME_SW_REQ_84` | TODO | The configuration shall state the total number of messages subscribed by each CAN port. |
| `ME_SW_REQ_85` | TODO | The Primary shall receive all Subscribed CAN Messages that are configured. |
| `ME_SW_REQ_86` | TODO | Messages not configured as a Subscribed CAN Message shall not be received by the respective CAN port of the Primary. |
| `ME_SW_REQ_87` | TODO | Each Subscribed CAN Message of a given CAN port shall have a unique message identifier. |
| `ME_SW_REQ_88` | TODO | A message that a CAN port is expected to transmit shall be configured as a Published CAN Message by the configuration tool. |
| `ME_SW_REQ_89` | TODO | The configuration shall state the total number of messages published by each CAN port. |
| `ME_SW_REQ_90` | TODO | For each message the following parameters shall be configurable: 1. Message Id 2. Message Name 3. Message Type 4. DLC 5. Periodicity 6. MTO 7. MTO Exception Enable 8. MTO Exception Action |
| `ME_SW_REQ_91` | TODO | Every CAN message shall have an 11-bit message identifier configured. |
| `ME_SW_REQ_92` | TODO | Configured message identifiers shall be checked for validity. The valid range is 0x000 to 0x7FF. |
| `ME_SW_REQ_93` | TODO | Any message with an invalid identifier shall not be considered for transmission or reception. |
| `ME_SW_REQ_94` | TODO | For a given CAN port, all messages shall have a unique CAN message identifier. |
| `ME_SW_REQ_95` | DEFERRED | It shall be possible to configure a 29-bit extended message identifier, not using the J1939 protocol. |
| `ME_SW_REQ_99` | TODO | CAN messages that are configured as neither Subscribed nor Published shall be ignored by the Primary. |
| `ME_SW_REQ_102` | TODO | The Primary shall receive the configured periodicity for all messages to be transmitted by each CAN port. |
| `ME_SW_REQ_103` | TODO | The Primary shall receive the configured periodicity for all messages to be received by each CAN port. |
| `ME_SW_REQ_104` | TODO | If periodicity is not configured for a message, that message shall be ignored. |
| `ME_SW_REQ_105` | TODO | Configured periodicity shall be checked for validity. It shall be between 10 ms and 2000 ms, in multiples of 5 ms. |
| `ME_SW_REQ_109` | TODO | If the configured MTO is less than the minimum expected value for that message, or greater than 10000 ms, it shall be ignored and MTO monitoring shall not be performed for that message. |
| `ME_SW_REQ_110` | TODO | If MTO is not configured for a message, it shall be ignored and MTO shall not be monitored for that CAN message. |
| `ME_SW_REQ_111` | TODO | All Subscribed CAN Messages of all CAN ports shall support the MTO Exception Enable configuration. |
| `ME_SW_REQ_112` | TODO | If MTO Exception Enable is true for a message, an exception shall be raised on detection of the MTO for that message. |
| `ME_SW_REQ_113` | TODO | The MTO Exception Action configuration shall define the action to be taken on detection of the MTO for a message. |
| `ME_SW_REQ_114` | TODO | The following MTO Exception Actions shall be possible: 1. Stop the tests of the affected channels 2. Continue the tests of the affected channels |
| `ME_SW_REQ_115` | TODO | The configuration of every Subscribed CAN Message shall state which channels, if any, that message affects. An MTO Exception Action shall be applied only to the channels so named, and shall leave all other channels ru... |
| `ME_SW_REQ_116` | TODO | The signal configuration shall have the following parameters: 1. Signal Enumeration 2. Signal Name 3. Message ID 4. Initial Value 5. Start Bit 6. Length 7. Format 8. Scale 9. Offset |
| `ME_SW_REQ_117` | DEFERRED | It shall be possible to configure multiplexed CAN messages. |
| `ME_SW_REQ_118` | TODO | Each signal of all CAN messages of all CAN ports shall have a unique enumeration in the range 0 to 65535. |
| `ME_SW_REQ_119` | TODO | Each signal of all CAN messages of all CAN ports shall have a unique short name of not more than 30 characters. |
| `ME_SW_REQ_120` | TODO | Each CAN signal shall carry the identifier of the CAN message it belongs to. |
| `ME_SW_REQ_122` | TODO | The configured initial value of a signal shall be less than the maximum value that signal can hold. |
| `ME_SW_REQ_124` | TODO | Valid values for the start bit are 0 to 63 inclusive. |
| `ME_SW_REQ_126` | TODO | The start bit and length of the signals of one message shall be configured such that the start bit of the nth signal equals 1 + start bit of the (n-1)th signal + length of the (n-1)th signal, and the last bit of the l... |
| `ME_SW_REQ_127` | TODO | Each CAN signal longer than 8 bits shall have a Format type indicating whether the signal is stored in Intel or Motorola byte order. |
| `ME_SW_REQ_128` | TODO | It shall be possible to set the scaling factor of a signal through configuration. |
| `ME_SW_REQ_129` | TODO | The data type of the scaling factor shall be float. |
| `ME_SW_REQ_130` | TODO | It shall be possible to set the offset for each value signal through configuration. |
| `ME_SW_REQ_131` | TODO | The data type of the offset shall be float. |

### WP-P19 - Per-channel analog limit configuration, validation and exception handling

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 3 d hardware = **8 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-15 ('near 0 V' threshold) |
| Requirements | 17 |

No per-channel analog limits are stored at all, which is why WP-P03's current clamp has nothing to clamp against. This package brings in Max Battery Voltage, Max Charging/Discharging Current, the sampling rates, and the six named exceptions - each of which must disable exactly its own channel and leave the other 63 running.

**Requirements covered** (TODO 16, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_134` | TODO | If the Max Battery Voltage configured for a channel is not valid, the Primary shall raise the Invalid Max Battery Voltage exception, naming that channel. |
| `ME_SW_REQ_135` | TODO | If the Invalid Max Battery Voltage exception is detected for a channel, testing shall be disabled for that channel only. All other channels shall be unaffected. |
| `ME_SW_REQ_138` | TODO | If the configured voltage sampling rate is not valid, the Primary shall raise the Invalid Voltage Sampling Rate exception, naming the affected channel. |
| `ME_SW_REQ_139` | TODO | If the Invalid Voltage Sampling Rate exception is detected for a channel, testing shall be disabled for that channel only. |
| `ME_SW_REQ_141` | TODO | If the voltage measured on a channel is negative, that is less than 0 V, continuously for 2 seconds, the Primary shall raise the Reverse Polarity exception for that channel. |
| `ME_SW_REQ_142` | TODO | If the Reverse Polarity exception is detected for a channel, testing shall be disabled for that channel only. |
| `ME_SW_REQ_143` | **BLOCKED** | Before starting a test on a channel, if the measured battery voltage of that channel is found to be near 0 V continuously for 2 seconds, the Primary shall raise the Battery Open exception for that channel. The thresho... |
| `ME_SW_REQ_144` | TODO | If the Battery Open exception is detected for a channel, testing shall be disabled for that channel only. |
| `ME_SW_REQ_147` | TODO | The Max Charging Current configured for a channel shall not be more than 200 A. |
| `ME_SW_REQ_148` | TODO | If the Max Charging Current configured for a channel is more than 200 A, the Primary shall raise the Invalid Max Charging Current exception, naming that channel. |
| `ME_SW_REQ_149` | TODO | If the Invalid Max Charging Current exception is detected for a channel, testing shall be disabled for that channel only. |
| `ME_SW_REQ_152` | TODO | The Max Discharging Current configured for a channel shall not be more than 200 A. |
| `ME_SW_REQ_153` | TODO | If the Max Discharging Current configured for a channel is more than 200 A, the Primary shall raise the Invalid Max Discharging Current exception, naming that channel. |
| `ME_SW_REQ_154` | TODO | If the Invalid Max Discharging Current exception is detected for a channel, testing shall be disabled for that channel only. |
| `ME_SW_REQ_157` | TODO | A valid configured current sampling rate shall be from 1 ms to 1000 ms. |
| `ME_SW_REQ_158` | TODO | If the configured current sampling rate is not valid, the Primary shall raise the Invalid Current Sampling Rate exception, naming the affected channel. |
| `ME_SW_REQ_159` | TODO | If the Invalid Current Sampling Rate exception is detected for a channel, testing shall be disabled for that channel only. |

### WP-P20 - Calibration: voltage, current and analog output, per channel, bound to board identity, persisted

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 6 d implement + 4 d hardware = **10 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-16 and OI-13 (there is no procedure to implement), OI-14 (board binding) |
| Requirements | 6 |

There is genuinely nothing to carry forward - BTS section 8.1.5 contained two requirement numbers with empty statements. OI-14 is the field hazard: wrong calibration constants after a board swap produce measurements that look completely plausible. `0xA0` calibration frames are recognised by the router and unimplemented (T-12). Depends on WP-P07 for persistence and WP-P01 for the board identity to bind to.

**Requirements covered** (TODO 4, BLOCKED 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_145` | **BLOCKED** | It shall be possible to calibrate the battery voltage measurement of each channel. The calibration procedure, the number of calibration points and the acceptance criteria shall be <TBD-16>. |
| `ME_SW_REQ_160` | TODO | It shall be possible to calibrate the charging current measurement of each channel. |
| `ME_SW_REQ_161` | TODO | It shall be possible to calibrate the discharging current measurement of each channel. |
| `ME_SW_REQ_162` | **BLOCKED** | Calibration data shall be held per channel and shall be bound to the identity of the Secondary Board that owns that channel, so that replacing a Secondary Board does not silently apply the previous board's calibration... |
| `ME_SW_REQ_164` | TODO | It shall be possible to calibrate the 0 V to 10 V analog output of each channel to achieve linearity of the output. |
| `ME_SW_REQ_165` | TODO | It shall be possible to calibrate the -10 V to 0 V analog output of each channel to achieve linearity of the output. |

### WP-P21 - Program management: integrity verification, download gating, read-back, storage limits

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 2 d hardware = **6 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-17 (maximum program size per channel), OI-23 |
| Requirements | 5 |

Completeness is checked via the chain terminator and every `nextIndex` is range-checked, which is the load-bearing half of REQ_240. Missing: a whole-program integrity check, refusing a download to a running channel (REQ_239 - today a `0xBB` Q1 is answered unconditionally and a Q4 packet for a running circuit is accepted), and any read-back path. TBD-17 also closes T-27: the per-slot buffer size is what produces the unverified 261 MB `.bss`.

**Requirements covered** (PARTIAL 3, TODO 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_236` | *PARTIAL* | If a Program download fails for any reason, the user shall be notified, and the notification shall name the channel and the reason. |
| `ME_SW_REQ_237` | *PARTIAL* | The Primary shall be able to store one Program per channel, that is up to 64 Programs concurrently. |
| `ME_SW_REQ_239` | TODO | The Primary shall reject a Program download addressed to a channel whose test is running or interrupted, and shall report the rejection with its reason. It shall not modify the stored Program of that channel. |
| `ME_SW_REQ_240` | *PARTIAL* | The Primary shall verify the integrity of a stored Program before starting a test on that channel, and shall refuse to start if the Program is incomplete or fails its integrity check. |
| `ME_SW_REQ_241` | TODO | It shall be possible to read back from the Primary the Program currently stored for any channel, and to compare it with the Program held in the Battery Manager. |

### WP-P22 - Test control completion: Pause, Continue, Reset, group operations, Start preconditions, safe state

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 3 d hardware = **8 engineer-days** |
| Priority | P1 |
| Blocked by | OI-05 (safe state), TBD-27/OI-30 (Start on a completed test), OI-29 (command scoping) |
| Requirements | 9 |

All six commands parse and ack; only Start and Stop do anything. `core_logic.c` logs Pause, Continue and Reset as `not implemented yet` AFTER the frame has already been acked `0x01` - so the Battery Manager is currently told that a Pause succeeded. Fixing the ack semantics is WP-P23; fixing the behaviour is here. REQ_293's precondition check needs WP-P01, WP-P19 and WP-P20 to have state worth checking.

**Requirements covered** (PARTIAL 2, TODO 3, BLOCKED 4):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_225` | TODO | If the Primary receives a Continue command from the Battery Manager for a channel that is in the interrupted state, that channel's Program shall advance to its next Step. A Continue command shall affect only the chann... |
| `ME_SW_REQ_289` | *PARTIAL* | The Primary shall accept the commands Start, Stop, Pause, Continue, Sync Time and Reset from the Battery Manager. |
| `ME_SW_REQ_290` | *PARTIAL* | Start, Stop, Pause and Continue shall each act on exactly the channel named in the command and on no other. |
| `ME_SW_REQ_291` | **BLOCKED** | The Primary shall state, for each command, whether it is scoped to a channel, to a Secondary Board or to the whole system. Sync Time and Reset are system-scoped. |
| `ME_SW_REQ_292` | TODO | It shall be possible to start, stop, pause and continue a named group of channels, or all channels, in one operation. |
| `ME_SW_REQ_293` | TODO | The Primary shall refuse a Start command for a channel unless that channel has a complete and valid Program, valid configuration, valid calibration data, and an enrolled Secondary Board, and shall report which of thes... |
| `ME_SW_REQ_294` | **BLOCKED** | A Stop command shall bring the named channel to its defined safe state before the channel is reported as stopped. |
| `ME_SW_REQ_295` | **BLOCKED** | The behaviour of a Start command addressed to a channel that has already completed a test shall be defined as <TBD-27>. |
| `ME_SW_REQ_296` | **BLOCKED** | The system shall provide a means of bringing all channels to their safe state immediately, independently of the state of the Battery Manager link. |

### WP-P23 - Host link hardening: ack semantics, unrecognised-query NACK, session limit, link-loss buffering

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 2 d hardware = **7 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-12 (session count), TBD-13 (buffering duration), OI-08 |
| Requirements | 16 |

Most of section 15 is already DONE and hardware-verified - client role, port, backoff, CRC, reassembly, CRC-delimited framing. What remains is the honesty of the replies: REQ_281 wants 'accepted' distinguished from 'executed', and REQ_280 forbids acking an unrecognised query as successful, which the board does today (T-45). Both are the same seam - Core Logic needs an outbound message type through `g_q_comm` - and closing it is also what lets an `0xEE` ack ever mean 'the test started'.

**Requirements covered** (DONE 8, PARTIAL 4, TODO 2, BLOCKED 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_49` | **DONE** | The Ethernet port shall support the TCP/IP protocol. |
| `ME_SW_REQ_50` | **DONE** | The Primary Ethernet port shall support IPv4. |
| `ME_SW_REQ_57` | *PARTIAL* | It shall be possible to perform the following over the Primary Ethernet port using TCP/IP: 1. Configuration 2. Calibration 3. Programming 4. Reading live parameters |
| `ME_SW_REQ_59` | **BLOCKED** | The Primary software shall accept at most <TBD-12> concurrent Battery Manager sessions, and shall reject further connection attempts with a defined reason code. |
| `ME_SW_REQ_60` | *PARTIAL* | On loss of the Battery Manager link, the Primary software shall continue to execute every running program on every channel without interruption. |
| `ME_SW_REQ_61` | **BLOCKED** | While the Battery Manager link is down, the Primary software shall buffer all test record data it would have sent, for at least <TBD-13> minutes of operation at the maximum configured registration rate on all channels. |
| `ME_SW_REQ_62` | TODO | On re-establishment of the Battery Manager link, the Primary software shall deliver the buffered test record data in order, with no gaps and no duplicated records, and shall inform the Battery Manager if any data was ... |
| `ME_SW_REQ_271` | **DONE** | The Primary shall act as the TCP client and the Battery Manager shall act as the TCP server for the command and response channel. The Primary shall initiate the connection. |
| `ME_SW_REQ_272` | **DONE** | The command and response channel shall use TCP port 9999. |
| `ME_SW_REQ_273` | **DONE** | If the connection to the Battery Manager cannot be established or is lost, the Primary shall retry with an exponential backoff starting at 1 second and capped at 30 seconds, shall reset the backoff to its minimum afte... |
| `ME_SW_REQ_276` | **DONE** | Every frame exchanged between the Primary and the Battery Manager shall be protected by a CRC-16/MODBUS checksum, using polynomial 0xA001 and initial value 0xFFFF with no final inversion. |
| `ME_SW_REQ_277` | *PARTIAL* | The byte order of the CRC on the wire, and of all multi-byte fields, shall be the same in both directions, and shall be <TBD-25>. |
| `ME_SW_REQ_278` | **DONE** | The Primary shall reassemble frames correctly from the TCP byte stream, handling both the case of several frames arriving in one read and the case of one frame split across several reads. |
| `ME_SW_REQ_279` | **DONE** | A frame whose CRC does not verify shall be rejected, shall be logged with both the computed and the received checksum, and shall not close the connection. |
| `ME_SW_REQ_280` | *PARTIAL* | A frame carrying an unrecognised command group or an unrecognised query identifier shall be logged and discarded, and shall not close the connection. It shall not be acknowledged as successful. |
| `ME_SW_REQ_281` | TODO | An acknowledgement returned by the Primary shall distinguish 'accepted for execution' from 'executed', so that the Battery Manager never reports a test as started when only the command was queued. |

### WP-P24 - Network configuration from the host: static IP, DHCP control, MAC read-back, remaining 0xDD queries

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 2 d hardware = **6 engineer-days** |
| Priority | P3 |
| Blocked by | none |
| Requirements | 7 |

`0xDD` Q2 delete, Q3 discovery and Q4 IP configuration are documented in `bm_device_registration_v5.0.md` and unimplemented (T-10). The MAC is already read from the real interface and shipped in the registration payload; what is missing is a query that re-reads it on demand.

**Requirements covered** (DONE 1, PARTIAL 2, TODO 2, DEFERRED 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_51` | DEFERRED | The Primary Ethernet port shall support IPv6. |
| `ME_SW_REQ_52` | *PARTIAL* | The Primary Ethernet port shall support the DHCP protocol. |
| `ME_SW_REQ_53` | **DONE** | The Primary Ethernet port shall have a unique MAC address. |
| `ME_SW_REQ_54` | *PARTIAL* | It shall be possible to read the MAC address of the Primary Ethernet port through the Battery Manager application. |
| `ME_SW_REQ_55` | TODO | It shall be possible to set the IP address of the Primary Ethernet port manually through the Battery Manager application. |
| `ME_SW_REQ_56` | TODO | It shall be possible to enable automatic IP address assignment using DHCP. |
| `ME_SW_REQ_282` | DEFERRED | The Primary shall support device discovery, so that a Battery Manager can find Primary units on the network without their addresses being known in advance. |

### WP-P25 - Internal CAN bus robustness: bus-off recovery, stale-data detection, priority IDs, error counters, load budget

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 3 d hardware = **8 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-20, TBD-21 (bit rates), TBD-22 (load budget), TBD-24 (staleness), OI-27 |
| Requirements | 10 |

The transport works and is partially hardware-verified, and the RTT instrument REQ_266 needs already exists (ADR-31, microseconds per Secondary). The gap is that the A53 never sees the CAN controller - the M7 owns it and reports nothing about its state - so bus-off detection needs an M7-side change and a new RPMsg message, which is why this is not a pure A53 package. OI-27 is the first question to answer in the whole register: the bit rates set the ceiling on every channel's sampling and registration rate.

**Requirements covered** (DONE 3, PARTIAL 2, TODO 2, BLOCKED 3):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_261` | **DONE** | The Primary shall communicate with all Secondary Boards over a single shared CAN bus dedicated to that purpose. |
| `ME_SW_REQ_262` | **DONE** | The internal CAN bus shall use CAN FD. |
| `ME_SW_REQ_263` | **BLOCKED** | The internal CAN bus shall operate at an arbitration bit rate of <TBD-20> and a data bit rate of <TBD-21>. |
| `ME_SW_REQ_264` | **BLOCKED** | The worst-case load of the internal CAN bus, with the maximum number of Secondary Boards enrolled and all channels running at their maximum configured sampling and registration rates, shall not exceed <TBD-22> per cen... |
| `ME_SW_REQ_265` | TODO | Frames that carry a setpoint command, a stop command or a fault report shall be assigned CAN identifiers that win arbitration against frames carrying live measurement data. |
| `ME_SW_REQ_266` | *PARTIAL* | The Primary shall deliver a setpoint change to the target Secondary Board within <TBD-23> ms of deciding it, measured worst case with the bus at its budgeted load. |
| `ME_SW_REQ_267` | **BLOCKED** | The Primary shall detect that the measurement data of a channel has become stale, within <TBD-24> ms, and shall treat stale data as a fault of that channel rather than continuing to act on the last received value. |
| `ME_SW_REQ_268` | TODO | The Primary shall detect a CAN bus-off condition, shall attempt recovery, shall place all channels into a defined fault state while the bus is unusable, and shall report the condition. |
| `ME_SW_REQ_269` | *PARTIAL* | The Primary shall count and report internal CAN bus error frames, retransmissions and dropped frames, per Secondary Board. |
| `ME_SW_REQ_270` | **DONE** | The internal CAN bus configuration shall not be exposed for modification through the Battery Manager or any external interface. |

### WP-P26 - Performance, timing and memory budgets: control interval, jitter, headroom, WCET, .bss verification

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 4 d implement + 5 d hardware = **9 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-28 (control interval), TBD-29, TBD-30, TBD-31, TBD-32, OI-03, OI-31 |
| Requirements | 8 |

Mostly measurement, which is why `hil` exceeds `impl`. OI-31 calls TBD-28 the single most architecturally significant unknown in the SRS: it decides whether a containerised Linux A53 can host the control loop of 64 channels at all, and therefore whether the current architecture is viable. CPU3 pinning is in place (ADR-30) but the CPU is not yet kernel-isolated (T-60), so no jitter number measured today would be the real one. Also closes T-27's 261 MB `.bss` question.

**Requirements covered** (DONE 1, PARTIAL 2, TODO 1, BLOCKED 4):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_304` | TODO | The Primary shall sustain the full function of this specification with all supported channels running concurrently, each with an independent Program, for the maximum supported test duration. |
| `ME_SW_REQ_305` | *PARTIAL* | The Primary shall re-evaluate the control decision of every running channel at least once every <TBD-28> ms. |
| `ME_SW_REQ_306` | **BLOCKED** | The jitter on the control decision interval of any channel shall not exceed <TBD-29> ms, and shall not accumulate over the duration of a test. |
| `ME_SW_REQ_307` | **BLOCKED** | The Primary shall report live data for every running channel at the configured rate, and the aggregate live data rate for all channels shall not exceed <TBD-30>. |
| `ME_SW_REQ_308` | **BLOCKED** | The Primary software shall be shown by analysis and by measurement to have at least <TBD-31> per cent processor headroom under the worst case of ME_SW_REQ_304. |
| `ME_SW_REQ_309` | *PARTIAL* | The memory required by the Primary software, including all per-channel program, configuration, calibration and buffer storage, shall be bounded, shall be known before the software starts running, and shall fit within ... |
| `ME_SW_REQ_310` | **DONE** | The Primary software shall not allocate memory dynamically after initialisation is complete. |
| `ME_SW_REQ_311` | **BLOCKED** | The worst-case timing behaviour of the Primary software shall be analysed and documented, and shall be re-verified on the target hardware whenever the channel count, the sampling rates or the internal bus configuratio... |

### WP-P27 - Scale out to 64 channels: 8 Secondaries x 8 channels, CAN Block 2, capacity proof

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 5 d implement + 4 d hardware = **9 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-18 / OI-24 (is the address range 8x8 or 15x15) |
| Requirements | 9 |

The addressing, the 64 storage slots and the 64 engine contexts all exist and are host-tested; what has never run is more than one Secondary or more than 4 channels. `me_can_block_for_channel()` maps channels 5-8 to Block 2 but only Block 1 has been exercised (ADR-28). Settle OI-24 before this, not after: it decides a wire format, and the two ME documents currently disagree.

**Requirements covered** (DONE 2, PARTIAL 6, BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_58` | *PARTIAL* | The single Ethernet link shall carry the configuration, programming, calibration, live data and test record traffic of all enrolled Secondary Boards and all their channels concurrently, without any channel being starv... |
| `ME_SW_REQ_169` | *PARTIAL* | The progress of one channel's Program shall not be affected by the state, progress, error or completion of any other channel's Program. |
| `ME_SW_REQ_242` | *PARTIAL* | The Primary shall support up to 8 Secondary Boards. |
| `ME_SW_REQ_243` | *PARTIAL* | The Primary shall support up to 8 channels per Secondary Board. |
| `ME_SW_REQ_244` | *PARTIAL* | The Primary shall support up to 64 channels in total, and shall be able to hold independent configuration, calibration, Program and test state for every one of them simultaneously. |
| `ME_SW_REQ_245` | **DONE** | Every channel shall be identified system-wide by a single-byte Channel Address in which the upper nibble is the Secondary Board ID and the lower nibble is the channel index within that board. Both nibbles shall be 1-b... |
| `ME_SW_REQ_246` | **DONE** | The Primary shall reject a Channel Address whose Secondary nibble is 0, whose channel nibble is 0, whose Secondary nibble exceeds the supported maximum, or whose channel nibble exceeds the supported maximum. A rejecte... |
| `ME_SW_REQ_247` | **BLOCKED** | The permitted range of the Channel Address shall be consistent between the encoding definition and the validation logic. The agreed range is <TBD-18>. |
| `ME_SW_REQ_248` | *PARTIAL* | Every command, measurement, test record, log entry and error report that concerns a channel shall carry that channel's Channel Address. |

### WP-P28 - Charge/discharge change-over: bank type per channel and enforced dead time

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 2 d implement + 2 d hardware = **4 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-03 (dead time), OI-06 (who sequences it) |
| Requirements | 1 |

HARDWARE-CRITICAL and safety-relevant. Small in code, entirely dependent on OI-06: if the Primary sequences the change-over then bus latency eats into the dead time and the margin must be designed for; if the Secondary sequences it, this package is almost nothing. BTS used 500 ms on the Secondary. Do not guess.

**Requirements covered** (BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_23` | **BLOCKED** | The Primary software shall hold, for each channel, the transistor bank type (Single or Dual) of the unit driving that channel, and shall not command a transition between charging and discharging on that channel withou... |

### WP-P29 - Analog output commanded as engineering values (close out the voltage half)

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 1 d implement + 1 d hardware = **2 engineer-days** |
| Priority | P3 |
| Blocked by | T-66 (should CCChg ever carry a non-zero voltage) |
| Requirements | 1 |

Already true for current - `me_can_pack_set()` sends amperes as a float and never a raw DAC code. Voltage is simply not commanded: `step_decode.h` has no voltage field for CCChg, which may well be correct for a constant-current mode. Rolls up naturally into WP-P03.

**Requirements covered** (PARTIAL 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_166` | *PARTIAL* | The Primary shall command channel setpoints as engineering values, in amperes or volts, and shall not command raw analog output codes. Conversion from the engineering value to the analog output is Secondary Board scope. |

### WP-P30 - Version reporting and Secondary compatibility gate

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 2 d implement + 1 d hardware = **3 engineer-days** |
| Priority | P2 |
| Blocked by | none beyond WP-P01 |
| Requirements | 2 |

REQ_317/REQ_318. Needs enrolment to have a version to report and a board to refuse.

**Requirements covered** (TODO 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_317` | TODO | The Primary shall report its own software version, and the software version of every enrolled Secondary Board, to the Battery Manager. |
| `ME_SW_REQ_318` | TODO | The Primary shall refuse to admit a Secondary Board whose software version is not compatible with its own, and shall report the incompatibility rather than operating the board. |

### WP-P31 - Control-software stall detection driving all channels to safe state

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 2 d hardware = **5 engineer-days** |
| Priority | P1 |
| Blocked by | OI-05 |
| Requirements | 1 |

REQ_303 and the resilience half of REQ_324. Safety-relevant: a stalled control thread with contactors closed is the worst failure this system has. Note the existing design already makes deadlock impossible by construction - every queue send has a finite timeout and drops on expiry (ADR-12) - so this package is about detecting a stall that is not a deadlock, and about what to do when one is found.

**Requirements covered** (BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_303` | **BLOCKED** | The Primary shall detect the failure or stall of its own control software and shall bring all channels to their safe state if it occurs. |

### WP-P32 - Close out the known open code items

| | |
|---|---|
| Owner | Primary board engineer |
| Effort | 3 d implement + 2 d hardware = **5 engineer-days** |
| Priority | P1 |
| Blocked by | none |
| Requirements | 0 |

T-44 audit the frame-length table against captured frames (a wrong entry is recoverable via the CRC backstop but logs itself and should be fixed), T-45 the premature `0xEE` ack, T-46 watch for a battery payload that is not 40 bytes, T-40 watch for `0xBB` Q4 CRC rejections, T-48 the `0xAA` Q7-Q10 broadcast frames with their 2-byte header, T-38/T-39 the `0xBB` Q1 length and Q2 reply decision, T-27 the `.bss` check, T-63 revert the Dockerfile to `scratch` before the next production release, T-65 confirm the M7 echo root cause. Several of these are watch items that close for free on the next hardware run. **No SRS requirement traces to this package, by design** - it is code-hygiene debt from the T-register rather than requirement coverage, and it is listed here so its cost appears in the schedule instead of being absorbed silently.

### WP-B01 - Channel overview and per-channel detail views for 64 channels

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 10 d implement + 0 d hardware = **10 engineer-days** |
| Priority | P1 |
| Blocked by | none |
| Requirements | 3 |

REQ_326/327/339 and REQ_250's hard rule: a channel that does not exist must not be shown as merely idle. The overview is the screen the whole product is judged on, and 64 live channels is a different design problem from BTS's one.

**Requirements covered** (N/A-BM 3):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_326` | N/A-BM | The Battery Manager shall present a single overview of all channels of all enrolled Secondary Boards, showing for each channel at least: its Channel Address, whether it is populated, its test state, its current step, ... |
| `ME_SW_REQ_327` | N/A-BM | The Battery Manager shall provide a detail view for a single channel, showing its full live parameter set, its Program with the current step marked, and its recent events. |
| `ME_SW_REQ_339` | N/A-BM | The Battery Manager shall show the state of its connection to the Primary at all times, and shall make clear when displayed data is stale. |

### WP-B02 - Program editor and validator covering every operator and action

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 20 d implement + 0 d hardware = **20 engineer-days** |
| Priority | P1 |
| Blocked by | OI-16, OI-17, OI-20, OI-21, OI-22, OI-18 |
| Requirements | 15 |

The single largest BM package. REQ_328 requires validation against every rule in section 10 BEFORE download, which means the editor has to encode the BM-side data rules that ~30 requirements in section 10.3 state - the PAU blank-nominal rule, the CHA nominal/limit pairing rules, the STO no-parameters rule and so on. Five open issues describe operator semantics that nobody has written down; the editor cannot validate what is undefined, so this package tracks OI closure as closely as WP-P02 does.

**Requirements covered** (N/A-BM 15):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_170` | N/A-BM | Each Programming Step shall have a unique Step number within its Program. |
| `ME_SW_REQ_171` | N/A-BM | There shall not be any empty Step, that is a Step without any Operator, in a Program. |
| `ME_SW_REQ_172` | N/A-BM | Each Programming Step shall contain the operation to be performed. |
| `ME_SW_REQ_179` | N/A-BM | For the PAU operator the Nominal Value field must be blank. |
| `ME_SW_REQ_180` | N/A-BM | The Limit value for PAU shall be in the range 10.0 seconds to 99.9 hours. |
| `ME_SW_REQ_183` | N/A-BM | With the PAU operator, only the GOTO action shall be allowed. |
| `ME_SW_REQ_188` | N/A-BM | For the CHA operator, the Nominal Value and the Limit must both be given. |
| `ME_SW_REQ_190` | N/A-BM | For the CHA operator, if the Nominal Value is a Current then the Limit must be a Voltage, a Time or a Temperature. |
| `ME_SW_REQ_192` | N/A-BM | For the CHA operator, if the Nominal Value is a Voltage then the Limit must be a Current, an Ah value, a Time or a Temperature. |
| `ME_SW_REQ_202` | N/A-BM | The STO operator shall automatically be set at the end of each Program. |
| `ME_SW_REQ_205` | N/A-BM | If STO is used as an Operator, that Step must not have a Nominal Value, a Limit or an Action. |
| `ME_SW_REQ_233` | N/A-BM | All Steps of a Program shall be compiled by the programming tool. |
| `ME_SW_REQ_234` | N/A-BM | After successful compilation, the Program shall be downloaded over Ethernet to the Primary, addressed to the channel or channels it is intended for. |
| `ME_SW_REQ_235` | N/A-BM | If any compilation error is found in a Program, the user shall be notified and the Program shall not be downloaded. |
| `ME_SW_REQ_328` | N/A-BM | The Battery Manager shall provide an editor for authoring Programs, supporting all operators and actions of section 10, and shall validate a Program against the rules of section 10 before allowing it to be downloaded. |

### WP-B03 - Program library, reuse across channels and sessions, assignment tracking

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 8 d implement + 0 d hardware = **8 engineer-days** |
| Priority | P1 |
| Blocked by | none |
| Requirements | 3 |

REQ_329/238/330 plus REQ_241's compare-with-Primary. REQ_330's warning - the Program stored on the Primary differs from the one the BM believes is there - needs WP-P21's read-back on the Primary side.

**Requirements covered** (N/A-BM 3):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_238` | N/A-BM | It shall be possible to assign one authored Program to several channels without authoring it again. |
| `ME_SW_REQ_329` | N/A-BM | The Battery Manager shall maintain a library of authored Programs, so that a Program can be reused across channels and across sessions without being re-authored. |
| `ME_SW_REQ_330` | N/A-BM | The Battery Manager shall show which Program is assigned to each channel, and shall warn the operator when the Program stored on the Primary for a channel differs from the one the Battery Manager believes is there. |

### WP-B04 - Group operations across channels with per-channel outcome reporting

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 6 d implement + 0 d hardware = **6 engineer-days** |
| Priority | P1 |
| Blocked by | none |
| Requirements | 2 |

REQ_292/331/332. REQ_332's explicit confirmation before stopping or restarting a running test matters more here than anywhere: on a 64-channel shared machine one mis-click destroys somebody else's week.

**Requirements covered** (N/A-BM 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_331` | N/A-BM | The Battery Manager shall allow the operator to select a group of channels and apply start, stop, pause, continue and program assignment to that group in one action, and shall show the outcome for each channel in the ... |
| `ME_SW_REQ_332` | N/A-BM | The Battery Manager shall require an explicit confirmation before any action that would stop or restart a test that is already running. |

### WP-B05 - Live trends and scope-aware alarm and fault display

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 8 d implement + 0 d hardware = **8 engineer-days** |
| Priority | P2 |
| Blocked by | OI-11 (error catalogue) |
| Requirements | 2 |

REQ_333/337. A channel fault must LOOK different from a Secondary Board fault and from a system fault, which is only possible once WP-P10 and WP-P11 give faults a scope and a code.

**Requirements covered** (N/A-BM 2):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_333` | N/A-BM | The Battery Manager shall display live trends of the measured parameters of any selected channel or set of channels. |
| `ME_SW_REQ_337` | N/A-BM | The Battery Manager shall display active alarms and faults with their scope, so that a channel fault is visibly different from a Secondary Board fault and from a system fault. |

### WP-B06 - Test record storage, integrity marking and documented export

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 10 d implement + 0 d hardware = **10 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-26 |
| Requirements | 3 |

REQ_334/335/336 plus REQ_288's session identity. REQ_335 is the one to get right: a record containing a reported data loss, a restart gap or a time correction must be marked, so an incomplete record can never be read as a complete one. Pairs with WP-P05.

**Requirements covered** (N/A-BM 3):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_334` | N/A-BM | The Battery Manager shall store the complete test record of every test, identified by channel, session and time, and shall retain it after the test ends. |
| `ME_SW_REQ_335` | N/A-BM | The Battery Manager shall mark any stored test record that contains a reported data loss, a restart gap or a time correction, so that an incomplete record can never be read as a complete one. |
| `ME_SW_REQ_336` | N/A-BM | The Battery Manager shall export a test record in a documented, machine-readable format. |

### WP-B07 - Per-channel configuration workflows for sections 8 and 9

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 10 d implement + 0 d hardware = **10 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-15, and the digital I/O enable rows need WP-P14/WP-P15 |
| Requirements | 10 |

REQ_338 plus the BM-scope configuration rows: Max Battery Voltage, Max Charging/Discharging Current, both sampling rates, the analog output ranges, and the digital input/output enable flags of REQ_6 and REQ_16. Pairs with WP-P19.

**Requirements covered** (N/A-BM 10):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_6` | N/A-BM | It shall be possible to enable or disable any digital input through the Battery Manager application. |
| `ME_SW_REQ_16` | N/A-BM | It shall be possible to enable or disable any digital output through the Battery Manager application. |
| `ME_SW_REQ_132` | N/A-BM | The parameter Max Battery Voltage shall be configurable for each channel as a factory setting, before a test is started on that channel. |
| `ME_SW_REQ_133` | N/A-BM | Any voltage configured between 2.5 V and 100 V shall be treated as a valid Max Battery Voltage. |
| `ME_SW_REQ_136` | N/A-BM | It shall be possible to configure the sampling rate for voltage measurement for each channel. |
| `ME_SW_REQ_137` | N/A-BM | A valid configured voltage sampling rate shall be from 1 ms to 1000 ms. |
| `ME_SW_REQ_146` | N/A-BM | The parameter Max Charging Current shall be configurable for each channel as a factory setting. |
| `ME_SW_REQ_151` | N/A-BM | The parameter Max Discharging Current shall be configurable for each channel as a factory setting. |
| `ME_SW_REQ_156` | N/A-BM | It shall be possible to configure the sampling rate for current measurement for each channel. |
| `ME_SW_REQ_338` | N/A-BM | The Battery Manager shall provide the configuration and calibration workflows required by sections 8 and 9, on a per-channel basis. |

### WP-B08 - Calibration workflows per channel

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 6 d implement + 0 d hardware = **6 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-16, OI-13, OI-14 |
| Requirements | 0 |

Cannot start before the procedure exists. Pairs with WP-P20, which owns REQ_145, REQ_160, REQ_161 and REQ_162 - all `BM + Primary`, so this package is their Battery Manager half and traces no requirement exclusively.

### WP-B09 - CAN port, message and signal configuration UI with full validation

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 18 d implement + 0 d hardware = **18 engineer-days** |
| Priority | P3 |
| Blocked by | OI-10 (identifier width), TBD-14, OI-07 |
| Requirements | 10 |

Section 7's BM half - and the validation is the work, not the forms: unique enumerations across all ports, REQ_126's contiguous start-bit/length arithmetic, the 0x000-0x7FF identifier range, periodicity in 5 ms multiples between 10 and 2000 ms, and the MTO minimum of three times periodicity. Pairs with WP-P18 and should not lead it.

**Requirements covered** (N/A-BM 10):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_96` | N/A-BM | Every CAN message configured for a given CAN port shall have a unique message name. |
| `ME_SW_REQ_97` | N/A-BM | A CAN message name shall not be more than 30 characters long. |
| `ME_SW_REQ_98` | N/A-BM | For each CAN port, every CAN message shall be configured as either a Subscribed CAN Message or a Published CAN Message. |
| `ME_SW_REQ_100` | N/A-BM | For every CAN message it shall be possible to set a DLC of 1 to 8 bytes through configuration. |
| `ME_SW_REQ_106` | N/A-BM | A Message Time Out (MTO) shall be configurable for each Subscribed Message of every CAN port. |
| `ME_SW_REQ_107` | N/A-BM | The minimum MTO value that may be configured shall be at least three times the periodicity of that message. |
| `ME_SW_REQ_108` | N/A-BM | The maximum MTO that may be configured shall be 10000 ms. |
| `ME_SW_REQ_121` | N/A-BM | An initial value shall be configurable for each signal. |
| `ME_SW_REQ_123` | N/A-BM | Each CAN signal shall have a start bit configured. |
| `ME_SW_REQ_125` | N/A-BM | Each signal shall have a bit length configured. |

### WP-B10 - RS-485 configuration UI for both ports

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 5 d implement + 0 d hardware = **5 engineer-days** |
| Priority | P3 |
| Blocked by | TBD-05 through TBD-11 |
| Requirements | 1 |

Pairs with WP-P16/WP-P17.

**Requirements covered** (N/A-BM 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_37` | N/A-BM | It shall be possible, through the Battery Manager, to select which channels and which parameters are published on RS485_1. |

### WP-B11 - Network configuration UI: static IP, DHCP, MAC display

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 3 d implement + 0 d hardware = **3 engineer-days** |
| Priority | P3 |
| Blocked by | none |
| Requirements | 0 |

Pairs with WP-P24, which owns REQ_54, REQ_55 and REQ_56 - all `BM + Primary`, so this package is their Battery Manager half and traces no requirement exclusively.

### WP-B12 - Log retrieval and filtering by channel, board, severity and time

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 5 d implement + 0 d hardware = **5 engineer-days** |
| Priority | P2 |
| Blocked by | TBD-34 |
| Requirements | 0 |

REQ_323 is `BM + Primary` and is owned by WP-P12, which has to serve the logs before anything can filter them; this package is its Battery Manager half and traces no requirement exclusively.

### WP-B13 - Reject unachievable sampling and registration rate combinations

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 5 d implement + 0 d hardware = **5 engineer-days** |
| Priority | P1 |
| Blocked by | OI-12, TBD-22, TBD-30 |
| Requirements | 1 |

REQ_140 requires the BM to refuse a configuration whose total data rate would exceed the internal bus or Primary processing budget, and to say WHICH limit was exceeded. BTS permitted 1 ms sampling on one channel; 64 channels at 1 ms is 64,000 samples per second across a shared bus. Until the bus load calculation exists the BM cannot validate anything here, so this package is genuinely blocked rather than merely unstarted.

**Requirements covered** (BLOCKED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_140` | **BLOCKED** | The Battery Manager shall reject a combination of per-channel sampling rates whose total data rate would exceed the internal CAN bus budget or the Primary processing budget defined in section 16, and shall tell the op... |

### WP-B14 - Error catalogue presentation with meaning, scope, severity and operator action

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 4 d implement + 0 d hardware = **4 engineer-days** |
| Priority | P1 |
| Blocked by | OI-11 |
| Requirements | 0 |

REQ_320's catalogue has to be authored once and then rendered. REQ_82, REQ_302 and REQ_320 are all `BM + Primary` and are owned by WP-P11/WP-P10 on the reporting side, so this package is their Battery Manager half and traces no requirement exclusively.

### WP-B15 - Enrolment and software version display

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 4 d implement + 0 d hardware = **4 engineer-days** |
| Priority | P2 |
| Blocked by | OI-26 |
| Requirements | 1 |

REQ_259/317/250. Pairs with WP-P01 and WP-P30.

**Requirements covered** (N/A-BM 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_250` | N/A-BM | The Battery Manager shall present the actual discovered configuration - which Secondary Boards are present and which of their channels are usable - and shall not present channels that do not exist as though they were ... |

### WP-B16 - Access control and per-action audit trail

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 6 d implement + 0 d hardware = **6 engineer-days** |
| Priority | Phase 2 |
| Blocked by | OI-33 |
| Requirements | 1 |

REQ_340 is Phase 2 and excluded from the Phase 1 totals. Recorded here because OI-33 notes no source document mentions access control at all, and a shared 64-channel laboratory machine is exactly where it matters.

**Requirements covered** (DEFERRED 1):

| Req | Verdict | Statement |
|---|---|---|
| `ME_SW_REQ_340` | DEFERRED | The Battery Manager shall control access to actions that start, stop or reconfigure a test, and shall record which user performed each such action. |

### WP-B17 - Wire-protocol conformance: CRC order, float encoding, ack semantics, session IDs

| | |
|---|---|
| Owner | Battery Manager engineer |
| Effort | 5 d implement + 0 d hardware = **5 engineer-days** |
| Priority | P1 |
| Blocked by | TBD-25 / OI-28 (CRC byte order) |
| Requirements | 0 |

The Primary side is settled and hardware-verified as big-endian in both directions (ADR-9), and the standing decision is that the Battery Manager conforms to the board (T-33). This package is the BM-side work to match it, plus honouring REQ_281's accepted-versus-executed distinction once WP-P23 provides it - the BM must stop reporting a test as started when only the command was queued. **No SRS requirement traces here exclusively**: REQ_276, REQ_277, REQ_281, REQ_287 and REQ_288 are all `BM + Primary`, and a requirement gets exactly one owner in this matrix, so they sit with the Primary package that carries their remaining work. This package is the Battery Manager's half of those five.

