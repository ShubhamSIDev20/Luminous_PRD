# ME Primary Control Board — Software Architecture Document

| Field | Value |
|---|---|
| Document ID | ME-SAD-PRI-02 |
| Title | Primary Control Board (ME Mid Computer) — Software Architecture |
| Version | 0.1 (First draft for review) |
| Date | 2026-08-06 |
| Status | **DRAFT — not baselined** |
| Applies to | Primary Control Board only |
| Derived from | `ME-FRD-00` (System Overview), `ME-FRD-02` (Primary Board) |

> This document fills the slot reserved as **Annex A2 —
> `A2_Primary_Software_Architecture.md`** in `ME-FRD-00` §0.2.

---

## 1. Introduction

### 1.1 Purpose

The FRD says **what** the Primary must do. This document says **how the software
is organised** so that it can do it. It answers five questions:

1. What are the major pieces of software, and what is each responsible for?
2. Where does each piece run — A53 cores or the M7 core?
3. How do the pieces talk to each other?
4. How does the design meet the timing and safety requirements?
5. Which decisions are still open, and who must close them?

### 1.2 Scope

**In scope:** software structure of the Primary Control Board (ME Mid Computer).

**Out of scope:** the Web Application (`ME-FRD-01`), the DC-DC Converter Board,
the BMS Interface Board, the CAN Data Logger, hardware design, and wire-level
frame layouts (those belong to the `ME-ICD-*` series).

### 1.3 How to read this document

| If you are... | Read... |
|---|---|
| A reviewer with 10 minutes | §2 and §14 |
| The software architect | §5, §6, §13 — the decisions needing sign-off |
| A firmware developer | §7 to §11 |
| A test engineer | §9, §12, §15 |

### 1.4 Reference Documents

| Ref | Document |
|---|---|
| `RD-A` | `00_System_Overview.md` — ME-FRD-00 |
| `RD-B` | `02_Primary_Board.md` — ME-FRD-02 |
| `RD-C` | `01_Web_Application.md` — ME-FRD-01 |
| `RD-D` | `ME-ICD-*` series — interface control documents (not yet issued) |
| `RD-E` | NXP i.MX 8M Plus Reference Manual |

---

## 2. Architecture at a Glance

The Primary is the **sequencer and policy engine** of the ME system. It is the
only block that understands what a Test Program means. Everything above it is a
user interface and an archive; everything below it is a sensor or an actuator.

The whole architecture rests on **one central idea**:

> **Split the software into two domains — a small, strictly bounded Critical
> Domain that controls batteries, and a large Best-Effort Domain that does
> everything else. Never let the second one delay the first.**

Five things to remember:

| # | Principle | Why |
|---|---|---|
| 1 | **Two domains, one boundary** | Control timing must be immune to web serving, database work and report exports |
| 2 | **64 independent state machines** | One execution context per circuit, fully isolated from each other |
| 3 | **Pre-allocate everything** | No memory allocation, no file I/O on the control path — ever |
| 4 | **Three safety barriers** | Program validation → setpoint clamping → hardware protection |
| 5 | **Survive alone** | 30 days unattended, no Web App, no operator, abrupt power loss |

### 2.1 Scale the design must carry

| Parameter | Value |
|---|---|
| Circuits (channels) | 64 |
| DC-DC boards on CAN-FD 1 | 8 (8 channels each) |
| Tightest control deadline | 50 ms (cutoff latency, E-Stop reaction) |
| Continuous unattended operation | 30 days |
| Offline data buffering | ≥ 24 hours at full rate |

---

## 3. What Drives This Architecture

These are the requirements that actually forced the structure. Everything in
§5 to §12 exists to satisfy one of them.

| Driver | Requirement | Architectural consequence |
|---|---|---|
| Bounded control latency on a general-purpose OS | FR-PRI-011..018 | Two-domain split; RT scheduling; CPU isolation |
| No allocation / no file I/O on control path | FR-PRI-014, -106 | Pre-allocated pools sized at startup |
| 64 circuits, fully independent | FR-PRI-100, -101 | Per-circuit context array; no shared mutable state |
| Deterministic, reproducible execution | FR-PRI-104 | Fixed evaluation order; single-writer ownership |
| Test must run with no Web App | FR-SYS-007, FR-PRI-340 | Local persistence + store-and-forward buffer |
| Survive abrupt power loss | FR-PRI-019, -020A | Atomic write-then-rename; A/B records + checksums |
| Never resume power automatically | FR-PRI-002, -028A | Recovery-pending state on every restart |
| E-Stop within 100 ms, fail-safe | FR-PRI-400, -401 | Dedicated high-priority path + hardware backstop |
| Bounded memory, no OOM kill | FR-PRI-034A, -107 | Memory budget + reservation, measured |
| Best-effort work cannot starve control | FR-PRI-016 | Core affinity / cgroup partitioning |

**The single most important one is FR-PRI-016.** A quad-core box running 64
circuits is nowhere near its compute limit — until an operator exports a 30-day
test and the database saturates three cores and the page cache. Reserving CPU
for the control path is not optimisation; it is what makes the platform safe.

---

## 4. System Context

The Primary sits between one client above and four device buses below.

```
                    ┌──────────────────────┐
                    │   Web Application    │
                    │      (PC / GUI)      │
                    └──────────┬───────────┘
                               │ IF-1  Ethernet 1 / TCP-IP
                               │       (Primary = server)
                    ┌──────────┴───────────┐
                    │   PRIMARY BOARD      │
                    │   ME Mid Computer    │
                    │   i.MX 8M Plus       │
                    │   A53 x4  +  M7 x1   │
                    └─┬────┬────┬────┬───┬─┘
        IF-2 CAN-FD 1 │    │    │    │   │ GPIO x5
        (master)      │    │    │    │   └──► E-Stop, interlock,
                      │    │    │    │        fault out, heartbeat
        ┌─────────────┘    │    │    └──────► IF-6 RS-485 #1
        │                  │    │             Modbus RTU master
        ▼                  │    └───────────► IF-4 CAN-FD 3
  8 x DC-DC Boards         │                  CAN Data Logger (listen)
  8 channels each          └────────────────► IF-3 CAN-FD 2
  = 64 circuits                               BMS Interface Boards (listen)
```

### 4.1 Interface summary

| Ref | Peer | Medium | Primary's role | Traffic |
|---|---|---|---|---|
| IF-1 | Web Application | Ethernet / TCP | Server | Config, programs, control, telemetry, DBC |
| IF-2 | DC-DC Boards (8) | CAN-FD 1 | Master | Setpoints down, measurements up |
| IF-3 | BMS Interface Boards | CAN-FD 2 | Listener + config | Decoded pack data (log-only) |
| IF-4 | CAN Data Logger | CAN-FD 3 | Listener | Temperature, auxiliary sensors |
| IF-5 | Reserved | CAN-FD 4 | — | Future expansion |
| IF-6 | Modbus devices | RS-485 #1 | Modbus RTU master | Field sensors, control boards |
| — | Safety I/O | GPIO x5 | In + out | E-Stop, interlock, fault, heartbeat |

**IF-2 is the only interface the Primary drives as a control master.** IF-3 and
IF-4 are strictly inbound and never influence a cutoff decision — BMS data is
log-only per `RD-A` §0.6.

---

## 5. The Core Idea — Two Domains

If you remember nothing else from this document, remember this section.

The Primary's software is divided into two domains with a **hard, deliberate
boundary** between them.

```
╔══════════════════════════════════════════════════════════════╗
║  CRITICAL DOMAIN            small · bounded · real-time      ║
║                                                              ║
║  • Measurement ingest (IF-2)     • Cutoff evaluation         ║
║  • Derived quantity accumulation • Action execution          ║
║  • Setpoint dispatch & clamping  • Safety-limit enforcement  ║
║  • E-Stop / interlock response   • Watchdog servicing        ║
║                                                              ║
║  Rules: no file I/O · no unbounded loops                     ║
║         no blocking on the other domain · memory locked      ║
╚═══════════════════════════╤══════════════════════════════════╝
                            │
              ONE-WAY, NON-BLOCKING BOUNDARY
              lock-free queues + shared snapshot
              Critical never waits for Best-Effort
                            │
╔═══════════════════════════╧══════════════════════════════════╗
║  BEST-EFFORT DOMAIN         large · flexible · normal sched  ║
║                                                              ║
║  • IF-1 server (Web App link)    • Registration writer       ║
║  • Store-and-forward buffer      • Program store & validate  ║
║  • BMS / aux / Modbus ingest     • DBC handling              ║
║  • Fault & event log             • Diagnostics, FW update    ║
╚══════════════════════════════════════════════════════════════╝
```

### 5.1 The boundary rule

**Data crosses the boundary in one direction at a time, and never blocks.**

| Direction | Mechanism | Example |
|---|---|---|
| Critical → Best-Effort | Lock-free ring buffer (single producer) | Registration records, faults, telemetry samples |
| Best-Effort → Critical | Double-buffered config snapshot, swapped atomically | New program, new safety limits, START/STOP command |

If the Best-Effort domain stops consuming, the Critical domain **overwrites or
counts a drop and keeps running**. It never waits. Losing a telemetry sample is
acceptable; missing a cutoff by 200 ms is not.

### 5.2 Why this split, and not a simpler design

A single-threaded or single-priority design would work fine on the bench and
fail in the field. The failure mode is specific and well known: a 30-day test
export, a database compaction, or a firmware upload saturates the CPU and page
cache, and a cutoff is evaluated 400 ms late on 64 circuits simultaneously.

The two-domain model makes that failure structurally impossible rather than
merely unlikely.

---

## 6. Processor Allocation — A53 vs M7  ⚠ OPEN DECISION

### 6.1 The gap in the FRD

`RD-B` §2.2A describes the platform as *"Quad-core, 4 GB RAM, 32 GB, Linux"* and
is **silent on the Cortex-M7**. Every real-time requirement (FR-PRI-011 to -018)
is written as a Linux problem, and FR-PRI-017 asks for a justified choice of
kernel configuration (`Q-41`).

The actual hardware is an **i.MX 8M Plus: 4 x Cortex-A53 running Linux, plus
1 x Cortex-M7**. So the FRD does not tell us where the control path runs. That
is the decision this section frames — **and it is the architect's to make, not
this document's.**

### 6.2 Timing budget — the deciding evidence

| Deadline | Value | Source |
|---|---|---|
| Flow-control step execution | ≤ 10 ms | `T_ZERO_TIME_STEP` |
| Timer resolution | 10 ms | `T_TIME_RES` |
| Step-entry → setpoint transmitted | ≤ 20 ms | `T_STEP_DISPATCH` |
| Cutoff satisfied → action begins | ≤ 50 ms | `T_CUTOFF_LATENCY` |
| Limit violation → safe idle | ≤ 50 ms | `T_SAFETY_REACT` |
| E-Stop → power removed | ≤ 100 ms | `T_ESTOP` |

**The tightest deadline is 10 ms.** That is a comfortable figure for Linux with
`PREEMPT_RT`, which typically holds worst-case scheduling latency to well under
a millisecond on this class of SoC — roughly an order of magnitude of margin.
Nothing in this system requires microsecond determinism, because the real
control loop (current/voltage regulation) lives on the DC-DC board, not here.

### 6.3 The two options

**Option A — All control on A53 under Linux + `PREEMPT_RT`. M7 unused or
reserved.**

| | |
|---|---|
| Pro | One codebase, one language, one debugger. Far simpler build and test. |
| Pro | Timing margin is ample (10 ms deadline vs sub-ms latency). |
| Pro | CAN, Ethernet, storage all use mature mainline Linux drivers. |
| Pro | No inter-core protocol to design, version, and debug. |
| Con | Requires `PREEMPT_RT` support in the vendor BSP — confirm early. |
| Con | Discipline (no malloc, locked memory, CPU isolation) is enforced by review, not by hardware. |

**Option B — Control path on M7 (bare-metal/RTOS), Linux on A53 for everything
else.**

| | |
|---|---|
| Pro | Hard physical isolation; Linux cannot preempt the control path at all. |
| Pro | Survives a Linux crash or OOM event still regulating safely. |
| Con | Two codebases, two toolchains, two debug environments. |
| Con | Must design, document and version an A53↔M7 protocol (RPMsg over the Messaging Unit). |
| Con | M7 has limited TCM/RAM — 64 pre-allocated circuit contexts plus buffers needs sizing before committing. |
| Con | Significantly more integration effort for timing margin that is not needed. |

### 6.4 Recommendation (for architect confirmation)

**Adopt Option A as the baseline**, and reserve the M7 for a narrow, optional
safety role only:

- Independent hardware watchdog supervision of the Linux control application
  (FR-PRI-029A — *"a software-only watchdog that shares the hung process's fate
  shall not be the sole mechanism"*).
- A fail-safe path that can command all channels to safe idle if Linux stops
  responding.

This keeps the M7's genuine benefit — surviving a Linux failure — without paying
the full cost of a split control architecture for timing margin the system does
not need.

**Cost of being wrong:** low. If measured latency later proves inadequate, the
Critical Domain is already isolated behind the §5.1 boundary and can be
relocated to the M7 without redesigning the Best-Effort side.

> **⚠ ACTION REQUIRED — this decision must be closed by the software architect
> before detailed design begins.** It is recorded as `ADR-01` (§13) and `Q-41`.
> Confirm `PREEMPT_RT` availability in the NXP BSP *first* — retrofitting it
> late is a well-known way to lose a month.

---

## 7. Software Layers

Assuming the Option A baseline (§6.4). Layers are strictly ordered: a layer may
call downwards, never upwards.

```
┌─────────────────────────────────────────────────────────────┐
│ L5  APPLICATION SERVICES        (Best-Effort)               │
│     IF-1 Server · Program Store · Config Manager            │
│     Fault Log · Diagnostics · Firmware Update               │
├─────────────────────────────────────────────────────────────┤
│ L4  DATA SERVICES               (Best-Effort)               │
│     Registration Writer · Store-and-Forward Buffer          │
│     Telemetry Publisher · BMS/Aux Mapper · DBC Decoder      │
├═════════════════════════════════════════════════════════════┤
│ L3  EXECUTION CORE              (CRITICAL)                  │
│     Circuit Execution Engine x64 · Cutoff Evaluator         │
│     Action Dispatcher · Accumulators · Safety Supervisor    │
├─────────────────────────────────────────────────────────────┤
│ L2  DEVICE PROTOCOL             (Critical + Best-Effort)    │
│     IF-2 Setpoint/Measurement · IF-3 BMS · IF-4 Aux         │
│     IF-6 Modbus Master · GPIO Handler                       │
├─────────────────────────────────────────────────────────────┤
│ L1  PLATFORM ABSTRACTION                                    │
│     SocketCAN · Ethernet · Serial · GPIO · Timer · Storage  │
├─────────────────────────────────────────────────────────────┤
│ L0  LINUX (PREEMPT_RT) + NXP BSP        [+ M7 safety FW]    │
└─────────────────────────────────────────────────────────────┘
```

The **double line between L3 and L4** is the domain boundary of §5. It is the
only place where the non-blocking queue/snapshot discipline applies.

---

## 8. Components

| Component | Layer | Domain | Responsibility | Key FRD |
|---|---|---|---|---|
| **Circuit Execution Engine** | L3 | Critical | One state machine per circuit: step sequencing, cycles, procedures, variables, timers | FR-PRI-100..121 |
| **Cutoff Evaluator** | L3 | Critical | Evaluate all limits for a circuit each cycle, in fixed order | FR-PRI-160s |
| **Action Dispatcher** | L3 | Critical | Execute the action a satisfied limit selects (advance, branch, stop, interrupt, annunciate) | FR-PRI-170s |
| **Accumulator Set** | L3 | Critical | Integrate current/power into Ah and Wh per circuit, per step | FR-PRI-140, -141 |
| **Safety Supervisor** | L3 | Critical | Clamp every setpoint; enforce channel safety limits independently of the program; drive safe idle | FR-PRI-116, -370, -371 |
| **Setpoint Dispatcher** | L2/L3 | Critical | Format and transmit setpoints on IF-2; verify acknowledgement; re-affirm keep-alive | FR-PRI-110..117 |
| **Measurement Ingest** | L2/L3 | Critical | Parse IF-2 frames, scale to engineering units, timestamp, detect stale/implausible data | FR-PRI-130..138 |
| **GPIO Handler** | L2 | Critical | E-Stop and interlock inputs (fail-safe), fault and heartbeat outputs | FR-PRI-400..408 |
| **BMS/Aux Ingest** | L2/L4 | Best-Effort | Receive IF-3/IF-4, decode via DBC, map signals to circuit — log-only | FR-PRI-250s, -290s |
| **Modbus Master** | L2/L4 | Best-Effort | Poll RS-485 field devices | FR-PRI-310s |
| **Registration Writer** | L4 | Best-Effort | Build registration records per the active format and trigger; hand to buffer | FR-PRI-320s |
| **Store-and-Forward Buffer** | L4 | Best-Effort | Durable ordered queue; reserved storage budget; forward on reconnect; never delete unconfirmed | FR-PRI-340..347 |
| **Telemetry Publisher** | L4 | Best-Effort | 1 Hz live values to Web App (display only, droppable) | FR-PRI-330s |
| **IF-1 Server** | L5 | Best-Effort | Session management, authentication, command handling, program/DBC transfer | FR-PRI-040s |
| **Program Store** | L5 | Best-Effort | Receive, validate, persist programs and procedures | FR-PRI-070s |
| **Config Manager** | L5 | Best-Effort | Channel map, safety limits, board discovery, time reference | FR-PRI-050s |
| **Fault & Event Log** | L5 | Best-Effort | Persistent ≥1000 entries, survives restart and Web App outage | FR-PRI-372..377 |
| **Recovery Manager** | L5 | Both | Persist execution context; on restart place circuits in recovery-pending | FR-PRI-028A, -420s |

---

## 9. Key Runtime Flows

### 9.1 The Control Cycle — the heartbeat of the system

This loop runs at a fixed period in the Critical Domain and processes **all 64
circuits every cycle, in fixed circuit order**. Fixed order is what makes
FR-PRI-104 (determinism) achievable.

```
  ┌─► 1. Drain CAN-FD 1 receive queue
  │       parse · scale · timestamp · route to circuit
  │
  │   2. Apply pending commands from Best-Effort
  │       atomic snapshot swap (START / STOP / new program)
  │
  │   3. FOR circuit = 1 .. 64:
  │        a. Check data freshness   → stale? fault, safe idle
  │        b. Update accumulators    → Ah, Wh, timers
  │        c. Evaluate safety limits → violated? safe idle (independent
  │                                     of the program)
  │        d. Evaluate cutoffs       → in fixed declared order
  │        e. If satisfied: execute action, advance/branch step
  │        f. If step changed: resolve nominal value, CLAMP,
  │                            queue setpoint for dispatch
  │        g. Queue registration record if trigger met
  │
  │   4. Transmit queued setpoints + keep-alives on CAN-FD 1
  │   5. Push queued records/telemetry across the boundary (non-blocking)
  │   6. Service watchdog · record cycle latency and duration
  └───  7. Sleep until next period
```

**Cycle period** is set so the 50 ms cutoff-latency budget is met with margin:
period + worst-case execution + dispatch ≤ 50 ms. With `F_MEAS` still open
(`Q-62`), the period is provisional — see §14.

Steps (a)–(g) touch only that circuit's own context. **No shared mutable state
between circuits**, which is what delivers FR-PRI-101 (isolation).

### 9.2 Registration and store-and-forward

```
Control Cycle ──► lock-free ring ──► Registration Writer
                                          │
                                          ▼
                                  Durable Buffer  (reserved budget,
                                          │        ≥24 h at 64 circuits)
                        ┌─────────────────┴─────────────┐
              Web App connected              Web App absent
                        │                              │
                        ▼                              ▼
              Forward in order,            Accumulate; report occupancy
              await confirmation,          and estimated time remaining;
              then delete                  on exhaustion annunciate,
                                           mark the gap, KEEP RUNNING
```

**A data-loss condition never stops a test** (FR-PRI-345). That is a deliberate
policy choice, not an oversight.

### 9.3 Fault → safe idle

Any of these triggers the same convergent path:

```
safety-limit violation · DC-DC fault report · board comms loss ·
stale data · ERR action · operator STOP/INTERRUPT · E-Stop ·
interlock open · internal software fault
                    │
                    ▼
        Safety Supervisor: command safe idle  (≤ 50 ms; E-Stop ≤ 100 ms)
                    │
                    ▼
        Latch FAULT on affected circuit(s) — requires explicit ack
                    │
                    ▼
        Record: timestamp · circuit · code · severity · measured
                values at fault · state at fault
                    │
                    ▼
        Annunciate to Web App + persist locally + drive fault GPIO
```

**Ordering rule for internal faults (FR-PRI-378): command safe idle FIRST, then
restart.** A reset that leaves the power stage running at 40 A while the
controller is absent is worse than the fault. The DC-DC board's own setpoint
keep-alive timeout is the backstop if even that fails.

### 9.4 Startup

```
Power on
  → Platform boot (Linux + BSP)
  → Control application starts automatically, no login   (FR-PRI-026A)
  → Command ALL circuits to safe idle                    (≤ 2 s, FR-PRI-001)
  → Self-test: storage · each CAN-FD · RS-485 · Ethernet · RTC
  → Verify config + program checksums; annunciate, never silently repair
  → Load persisted execution contexts
  → Circuits that were RUNNING → RECOVERY-PENDING, never RUNNING
  → Report restart cause (power-on / commanded / watchdog / fault)
  → Enable IF-1; wait for explicit operator command
```

**Power never flows as a side effect of initialisation** (FR-PRI-002). The
system always requires a human decision to resume.

---

## 10. Concurrency and Timing Model

### 10.1 Thread / process allocation

| Unit | Domain | Priority | Period | CPU affinity |
|---|---|---|---|---|
| Control Cycle | Critical | RT, highest | Fixed (see §9.1) | Isolated core |
| CAN-FD 1 RX/TX | Critical | RT, high | Event-driven | Isolated core |
| GPIO / E-Stop monitor | Critical | RT, high | Event-driven | Isolated core |
| Registration Writer | Best-Effort | Normal+ | Event-driven | Shared cores |
| Buffer / forwarder | Best-Effort | Normal | Event-driven | Shared cores |
| IF-1 Server | Best-Effort | Normal | Event-driven | Shared cores |
| BMS / Aux / Modbus ingest | Best-Effort | Normal | Polled | Shared cores |
| Diagnostics / FW update | Best-Effort | Low | On demand | Shared cores |

### 10.2 The isolation mechanism

- **CPU partitioning** — the Critical Domain runs on reserved core(s); no
  best-effort work is schedulable there (FR-PRI-016).
- **Memory locking** — all Critical Domain memory locked resident; no paging,
  no swap (FR-PRI-013).
- **Pre-allocation** — every per-circuit structure allocated at startup, sized
  for 64 circuits; nothing allocated afterwards on the control path
  (FR-PRI-106).
- **Self-measurement** — the control path measures its own scheduling latency
  and execution time every cycle, reports maxima, and warns before a deadline
  is missed (FR-PRI-015, -379).

That last point matters more than it looks. It converts "we believe the timing
is fine" into evidence that can be shown at acceptance.

---

## 11. Data and Persistence

### 11.1 What is stored, and how durably

| Data | Volatility | Durability requirement |
|---|---|---|
| Execution context (per circuit) | Persisted periodically + on step change | Must survive abrupt power loss |
| Test programs and procedures | Persisted on receipt | Checksummed; annunciate corruption |
| Configuration and safety limits | Persisted on change | Checksummed; annunciate corruption |
| Registration buffer | Persisted continuously | Reserved budget; survives power cycle |
| Fault and event log | Persisted on write | ≥1000 entries; survives restart |
| DBC / decoding config | Persisted on receipt | Checksummed |
| Live telemetry | RAM only | Droppable by design |

### 11.2 Durability strategy

A `write()` that returned success is **not** on the disk. Every "shall survive a
power cycle" requirement depends on this being handled deliberately:

1. **Write-then-rename** for whole-file updates (atomic replacement).
2. **A/B records with checksums** for frequently updated state such as execution
   context — write the inactive slot, flush to durable storage, then switch.
3. **Explicit flush** before any state is reported as persisted (FR-PRI-019).
4. **Checksum verification on read**; corruption is annunciated, never silently
   repaired or discarded (FR-PRI-021A).
5. **Power-loss-tolerant filesystem configuration** — no manual repair step on
   the next boot (FR-PRI-022A).

### 11.3 Storage partitioning (32 GB)

Storage is divided into fixed budgets so that no function can starve another:

| Partition | Contents | Rule |
|---|---|---|
| System | OS, application, BSP | A/B slots for safe firmware update |
| Configuration | Config, programs, procedures, DBC | Small, checksummed |
| Registration buffer | Store-and-forward queue | **Reserved — no other function may consume it** |
| Logs and diagnostics | System logs, traces | **Bounded — can never displace registration data** |

Free space is monitored with a Warning threshold and a Fault threshold, both
crossed *before* logging or buffering is affected (FR-PRI-023A).

---

## 12. Safety Architecture — Three Barriers

A bad setpoint has to get through three independent barriers to reach a battery.

| Barrier | Where | Catches |
|---|---|---|
| **1. Program validation** | Best-Effort, at transfer time | An operator authoring an unsafe or malformed program |
| **2. Setpoint clamping** | Critical, at every dispatch | A corrupted program, a defect in the execution engine, a memory error |
| **3. Hardware protection** | DC-DC board, independent of Primary | Everything above, plus loss of the CAN link entirely |

Barrier 2 is the one people leave out, and it is the one that catches the faults
you cannot foresee. **The Safety Supervisor clamps every setpoint against the
channel's configured limits immediately before transmission, and annunciates any
clamping event.** A program can never command outside the safety envelope, no
matter how it got corrupted.

### 12.1 The E-Stop path

| Property | Design rule |
|---|---|
| Response time | ≤ 100 ms from assertion to power removed |
| Failure mode | **Fail-safe** — disconnected, broken, or unpowered reads as *asserted* |
| Wiring | Normally-closed / normally-energised |
| Clearing | Requires **both** physical return to safe **and** explicit operator acknowledgement |
| Independence | **Software must not be the only path.** A hardware path independent of Primary firmware is expected (`Q-27`) |

An E-Stop wired active-high fails to danger: cut the wire and the system
believes everything is fine. This is the single most commonly mis-implemented
requirement in the FRD, and it is worth a dedicated review item.

---

## 13. Design Decisions (ADRs)

Short-form architecture decision records. Each states the context, the decision,
what was rejected, and the consequence. **Status "PROPOSED" means the architect
has not yet signed it off.**

---

**ADR-01 — Processor allocation: A53-only control path**
*Status: PROPOSED — architect sign-off required (`Q-41`)*

- **Context:** i.MX 8M Plus provides A53 x4 + M7. FRD §2.2A does not say where the
  control path runs. Tightest deadline is 10 ms.
- **Decision:** Run the entire control path on A53 under Linux with
  `PREEMPT_RT`. Reserve M7 for independent watchdog supervision and a fail-safe
  safe-idle path only.
- **Rejected:** Full control path on M7 — two toolchains and an inter-core
  protocol for timing margin the system does not need.
- **Consequence:** Requires `PREEMPT_RT` in the NXP BSP. Must be confirmed
  before detailed design. Reversible, because §5.1 already isolates the domain.

---

**ADR-02 — Two-domain split with a non-blocking boundary**
*Status: PROPOSED*

- **Context:** FR-PRI-016 requires that best-effort work cannot cause a control
  timing violation.
- **Decision:** Hard split into Critical and Best-Effort domains; lock-free
  queues outbound, atomic snapshot swap inbound; Critical never blocks.
- **Rejected:** Single-priority design — fails under database or export load.
- **Consequence:** Some telemetry may be dropped under extreme load. Accepted:
  losing a display sample is preferable to missing a cutoff.

---

**ADR-03 — Pre-allocate all per-circuit resources at startup**
*Status: PROPOSED*

- **Context:** FR-PRI-014, -106, -107. A 30-day test cannot tolerate an
  allocation stall at hour 500, and 4 GB of RAM hides leaks.
- **Decision:** All 64 circuit contexts, accumulators, cycle stacks, buffers
  allocated at startup and never freed. Memory locked resident.
- **Rejected:** Dynamic allocation with a real-time-safe allocator — adds a
  failure mode for no benefit at fixed, known capacity.
- **Consequence:** Fixed memory footprint, measurable and provable at review.

---

**ADR-04 — One execution context per circuit, no shared mutable state**
*Status: PROPOSED*

- **Context:** FR-PRI-101 (isolation), FR-PRI-104 (determinism).
- **Decision:** 64 independent state machines processed in fixed order in a
  single control cycle. A circuit reads and writes only its own context.
- **Rejected:** Thread-per-circuit — 64 threads add scheduling jitter and
  non-determinism for no throughput gain at this scale.
- **Consequence:** Determinism is structural. Worst-case cycle time scales
  linearly and predictably with circuit count.

---

**ADR-05 — Setpoint clamping as an independent safety barrier**
*Status: PROPOSED*

- **Context:** FR-PRI-116. Validation at transfer time catches bad programs but
  not corruption or engine defects.
- **Decision:** The Safety Supervisor clamps every setpoint against channel
  safety limits at dispatch, independently of program validation, and
  annunciates any clamp.
- **Rejected:** Relying on validation alone.
- **Consequence:** Three independent barriers (§12). Small per-dispatch cost.

---

**ADR-06 — Never auto-resume power after restart**
*Status: PROPOSED — interacts with open question `Q-11`*

- **Context:** FR-PRI-002, -028A, -010.
- **Decision:** Every restart places previously-running circuits in
  recovery-pending. A human decides whether to resume.
- **Rejected:** Automatic resume — unsafe without knowing why the restart
  happened.
- **Consequence:** A 20-second power blip on day 19 of a 30-day unattended test
  halts that test. **`Q-11` asks whether a bounded conditional auto-resume is
  needed. If it is, that is a real feature requiring safety analysis, not a
  config flag — and it changes this ADR.**

---

**ADR-07 — Data loss never stops a test**
*Status: PROPOSED — depends on `Q-26`*

- **Context:** FR-PRI-345.
- **Decision:** On buffer exhaustion, annunciate, mark the gap precisely, and
  continue executing. Recommended policy is **discard-newest** — preserve the
  continuous record from the start of the outage.
- **Rejected:** Halting the test on buffer exhaustion.
- **Consequence:** A test record may contain a marked gap. Confirm the
  discard policy with test engineering (`Q-26`).

---

**ADR-08 — BMS and auxiliary data are log-only**
*Status: CONFIRMED by FRD (`Q-03`)*

- **Decision:** Data from IF-3 and IF-4 is never an input to a cutoff decision.
- **Consequence:** BMS ingest lives entirely in the Best-Effort domain,
  simplifying the Critical Domain considerably.

---

## 14. Open Architectural Questions

These must be closed before this document can be baselined. They are ordered by
how much rework they cause if answered late.

| # | Question | Blocks | Owner | FRD ref |
|---|---|---|---|---|
| **AQ-1** | A53-only or A53+M7 control path? Is `PREEMPT_RT` available in the NXP BSP? | Everything in §7–§11 | Software architect | `Q-41`, ADR-01 |
| **AQ-2** | What is `F_MEAS` — the measurement rate per circuit from the DC-DC boards? | Control cycle period, CAN load, buffer sizing | System architect + Secondary team | `Q-62` |
| **AQ-3** | Is bounded conditional auto-resume after a power blip required? | Recovery design, safety analysis | Test engineering + safety | `Q-11`, ADR-06 |
| **AQ-4** | Discard-oldest or discard-newest on buffer exhaustion? | Buffer implementation | Test engineering | `Q-26`, ADR-07 |
| **AQ-5** | Is there a hardware E-Stop path independent of Primary software? | Safety architecture, hardware interface | Hardware + safety | `Q-27` |
| **AQ-6** | GPIO allocation — is the proposed 5-signal mapping confirmed? | GPIO handler, wiring | Hardware | `Q-10` |
| **AQ-7** | What is Ethernet 2 for? Service network, plant network, or unused? | Network config, security posture | System architect | `Q-08`, `A-09` |
| **AQ-8** | Are the USB ports used at all? | Security posture, FW update path | System architect | `Q-09` |
| **AQ-9** | Maximum BMS Interface Boards on CAN-FD 2 — 8 or 16? | CAN-FD 2 load, buffer sizing | System architect | `Q-58`, `A-08` |
| **AQ-10** | ICDs are not yet issued. Frame layouts, IDs and scaling are unknown. | L2 device protocol implementation | Interface owners | `RD-D` |

**AQ-1 and AQ-2 are the two that matter this week.** AQ-1 determines the entire
software structure. AQ-2 sets the control cycle period, which every timing
budget in §10 depends on.

**AQ-10 is worth stating plainly:** no ICD has been issued yet. The L2 layer can
be designed and its interfaces defined, but it cannot be implemented. Structure
L2 so that frame layout is data-driven and isolated from L3, and an ICD change
late in the project costs a table edit rather than a redesign.

---

## 15. Traceability

Component groups mapped to the FRD requirement blocks they satisfy. Full
requirement-level traceability belongs in the SDD.

| FRD section | Requirements | Satisfied by |
|---|---|---|
| §2.2A.1 Real-time behaviour | FR-PRI-011..018 | §5 two-domain model, §10 concurrency, ADR-01, ADR-02 |
| §2.2A.2 Data durability | FR-PRI-019..025A | §11 persistence strategy |
| §2.2A.3 Startup & supervision | FR-PRI-026A..034A | §9.4 startup, §6.4 M7 watchdog role |
| §2.3 Startup & self-test | FR-PRI-001..010 | §9.4 startup flow |
| §2.4 Web App link | FR-PRI-040s | IF-1 Server (L5) |
| §2.5 Config & safety limits | FR-PRI-050s | Config Manager, Safety Supervisor |
| §2.6 Program reception | FR-PRI-070s | Program Store, Barrier 1 |
| §2.7 Execution engine | FR-PRI-100..121, -130..141 | §9.1 control cycle, ADR-03, ADR-04 |
| §2.9 DC-DC link | FR-PRI-110..117, -230s | Setpoint Dispatcher, Measurement Ingest |
| §2.10–2.13 BMS/Aux ingest | FR-PRI-250s..290s | Best-Effort ingest, ADR-08 |
| §2.14 Modbus | FR-PRI-310s | Modbus Master (L2/L4) |
| §2.15 Registration & telemetry | FR-PRI-320s..330s | Registration Writer, Telemetry Publisher |
| §2.16 Buffering | FR-PRI-340..347 | §9.2 store-and-forward, ADR-07 |
| §2.17 Fault handling | FR-PRI-370..380 | §9.3 fault flow, Safety Supervisor |
| §2.18 GPIO | FR-PRI-400..408 | §12.1 E-Stop path, GPIO Handler |
| §2.19 Power-fail recovery | FR-PRI-420s | Recovery Manager, ADR-06 |
| §2.24 Performance | All symbolic budgets | §10 timing model |

---

## 16. What To Do Next

A suggested order of work, shortest path to a reviewable design:

1. **Close AQ-1** — confirm `PREEMPT_RT` in the NXP BSP, and get the architect's
   decision on A53 vs M7. Nothing else is stable until this is settled.
2. **Close AQ-2** — get `F_MEAS` from the Secondary team, then fix the control
   cycle period and redo the §10 timing budget with real numbers.
3. **Prototype the latency measurement** — a bare loop on the target hardware
   under synthetic best-effort load. This is a day's work and it either
   validates ADR-01 or kills it early, which is exactly what you want.
4. **Define the L2/L3 interface** so ICD work and execution-engine work can
   proceed in parallel.
5. **Review §12 (safety) with hardware** — especially AQ-5, the independent
   E-Stop path.
6. Baseline this document and begin the SDD.

---

## Appendix A — Glossary

This document uses the terminology of `ME-FRD-00` §0.6 without redefining it.
The terms used most heavily here:

| Term | Meaning |
|---|---|
| **Circuit / Channel** | One independently controllable charge/discharge power path terminating in one battery pack. 64 in the system. |
| **Critical Domain** | The bounded-latency part of the software that controls batteries. |
| **Best-Effort Domain** | Everything else — web, database, logging, diagnostics. |
| **Safe idle** | No commanded power flow, output disabled at the DC-DC board. |
| **Registration** | Conditional sampling of measured data for permanent logging. |
| **Cutoff / Limit** | The condition that ends a step. |
| **Action** | What happens when a limit is reached. |

---

*End of document — ME-SAD-PRI-02 v0.1 DRAFT*
