# ME System — Annex A2: Primary Board Software Architecture

| Field | Value |
|---|---|
| Document ID | ME-FRD-A2 |
| Title | Primary Board (ME Mid Computer) — Software Architecture |
| Version | 0.3 |
| Date | 2026-08-05 |
| Status | **DRAFT — for team review** |
| Graphical companion | `ME_Primary_Architecture.html` |
| Derived from | `00_System_Overview.md`, `02_Primary_Board.md`, `A1_Operator_Reference.md` |

---

## A2.0 About this document

This explains how the Primary Board software is organised, and why. It sits between the functional
requirements (Part 02 — *what* it must do) and the Software Design Description (*how* each component
is built).

It is short on purpose. Every statement traces to something confirmed. Where a decision is still
open, it is listed in §A2.10 instead of being assumed.

**What this architecture is built on:**

| Confirmed | Source |
|---|---|
| Quad-core CPU, 4 GB RAM, 32 GB storage, Linux | Confirmed 2026-08-05 |
| 64 circuits — 8 channels on each of 8 DC-DC boards, on CAN-FD 1 | Confirmed 2026-08-05 |
| BM4 / BTS-600 program language, all operators in scope | Confirmed 2026-08-05 → Annex A1 |
| Web App converts operators to opcodes; Primary decodes them | Confirmed 2026-08-05 → Part 00 §0.7.13 |
| Limits cannot use BMS signals — BMS data is for logging only | Confirmed 2026-08-05 |

**Not yet decided:** the measurement update rate — how often the DC-DC boards report voltage and
current. This affects bus load, integration accuracy and registration granularity. Tracked as
`Q-62`. Nothing in this architecture depends on a specific value.

---

## A2.1 The main idea

> **The Primary runs the test program and makes the decisions. The Secondary regulates current and
> voltage. The Web Application is the operator's screen and the archive.**

Test program behaviour lives in one place only, so a program runs the same way on every circuit of
every board. Regulation loops live next to the power hardware, so they do not depend on a network.

The Primary's own problem is specific to its platform:

> **Run 64 test programs at the same time, on a general-purpose operating system, while also serving
> a network client, a database uplink, three non-control field buses and a filesystem.**

On a microcontroller you get real-time behaviour partly by default — there is no heap, no scheduler
you did not write, and no page cache. On Linux none of that is true. Code that quietly breaks timing
still runs, and usually still passes testing. So the architecture has to state the rules explicitly.
That is what §A2.2 does, and everything else follows from it.

---

## A2.2 Two domains, one boundary

```
                        ┌──────────────────────┐
                        │  Web Application     │  external
                        └──────────┬───────────┘
                                   │ TCP/IP
   ┌───────────────────────────────┼──────────────────────────────────┐
   │ BEST-EFFORT DOMAIN            │   normal priority · may block    │
   │                               │   may allocate · no deadline     │
   │  ┌─────────────┐   ┌──────────┴──────┐   ┌────────────────────┐  │
   │  │ Link Server │   │  Durable Store  │   │ Field Data Services│  │
   │  └─────────────┘   └────────┬────────┘   └────────────────────┘  │
   └────────────────────────┬────┼──────────────────────────────────┬─┘
   ═══════════════ BOUNDED, NON-BLOCKING QUEUES ═══════════════════════
   ┌──────────────────── programs ↓  │  ↑ records ─────────────────────┐
   │ REAL-TIME DOMAIN                │   RT priority · locked memory   │
   │                                 │   pre-allocated · no I/O        │
   │  ┌────────┐   ┌─────────────────┴───┐   ┌──────────────────────┐  │
   │  │ Ingest │   │  Execution Engine   │   │ Setpoint Dispatcher  │  │
   │  └────────┘   │     64 contexts     │   └──────────────────────┘  │
   │               └─────────────────────┘                            │
   │  ┌──────────────────── Safety Supervisor ───────────────────────┐ │
   │  └──────────────────────────────────────────────────────────────┘ │
   └───────────┬───────────────────────────────────────┬──────────────┘
       ┌───────┴────────┐                      ┌───────┴────────┐
       │   CAN-FD 1     │                      │  GPIO          │
       │  8 bd × 8 ch   │                      │  E-Stop        │
       └────────────────┘                      └────────────────┘
```

| | **Real-Time Domain** | **Best-Effort Domain** |
|---|---|---|
| Must | Finish its work before the next measurement update, for all 64 circuits | Just make progress |
| Priority | High, and not interrupted by best-effort work | Normal |
| Memory | Allocated once at startup, locked in RAM, never allocated again while running | Normal allocation |
| I/O allowed | CAN-FD 1 and GPIO only. **No filesystem, no network, no database.** | Anything |
| If starved | A test can run outside its intended limits | A screen updates late |
| Trace | FR-PRI-011…018, -106, -107 | — |

### A2.2.1 Components

**Real-time domain — four components:**

| Component | What it does |
|---|---|
| **Measurement Ingest** | Receives CAN-FD 1 frames. Converts raw values to engineering units, timestamps them, works out which circuit each belongs to, and rejects samples that are stale or out of range. |
| **Execution Engine** — 64 contexts | For each circuit: updates the Ah/Wh totals, checks the step's limits in the order they were written, runs the action of the first limit that is met, keeps the cycle and procedure stacks, holds variables and timers, and checks whether a registration record is due. |
| **Safety Supervisor** | Checks every sample against that circuit's configured limits. Handles E-Stop and interlock. Can force the circuit to safe idle. **Separate code from the engine.** |
| **Setpoint Dispatcher** | Clamps the setpoint to the circuit's limits, sends it, waits for the acknowledgement, and re-sends it once a second. |

**Best-effort domain — three components:**

| Component | What it does |
|---|---|
| **Link Server** | The only connection to the Web Application. Handles the session, configuration, program transfer and validation, control commands, and the uplink. |
| **Durable Store** | Holds programs, procedures, configuration, safety limits, decode configuration, the registration buffer, the event log and the saved execution context. Everything checksummed and safe against power loss. |
| **Field Data Services** | CAN-FD 2 (BMS), CAN-FD 3 (auxiliary sensors), RS-485 Modbus master. Decoding is driven by the DBC. **Logging only.** |

### A2.2.2 The boundary rules

| Rule | Why |
|---|---|
| **Queues have a fixed size, in both directions** | Allocated at startup. An unbounded queue keeps growing until memory runs out. |
| **Real-time code never waits for best-effort code** | If a queue is full, the real-time side applies its overflow policy and carries on. If it waited, a slow disk write could delay a limit check. |
| **Nothing needed mid-step is on the other side of the line** | A program is loaded into its context when the circuit starts. The engine does not fetch anything after that. This is what makes FR-SYS-007 — *a test keeps running if the operator PC reboots* — true by design rather than by hope. |

> **One operator breaks the third rule.** `SAVE` and `REST` read and write named test sections that
> live in the Web Application's database (Annex A1 §A1.6.5). `RD-1` p.198 says that if the data is
> missing, the running program *waits or is interrupted*. So either the Durable Store takes over
> saved sections, or a program using `REST` cannot start while the link is down. Tracked as
> **`Q-51`**. This is a structural question, not an implementation detail.

---

## A2.3 The control cycle

The cycle runs once per measurement update, for every circuit.

```
  from CAN-FD 1                                              to CAN-FD 1
       │                                                          ▲
       ▼                                                          │
  ┌─────────┐        ┌──────────────────┐        ┌────────────────┴───┐
  │ Ingest  │───────▶│ Execution Engine │───────▶│ Setpoint Dispatcher│
  │         │        │  update totals   │        │  clamp to limits   │
  │ reject  │        │  check limits    │        │  send, wait for    │
  │ stale   │        │  run the action  │        │  ack · re-send 1/s │
  └────┬────┘        └──────────────────┘        └────────────────────┘
       │                                                    ▲
       │ the same samples                                   │ FORCE SAFE IDLE
       ▼                                                    │
  ┌──────────────────────────────────────────────────────────┴────────┐
  │ Safety Supervisor — checks every sample against the circuit's     │
  │ configured limits, using separate code from the engine            │
  └───────────────────────────────────────────────────────────────────┘
```

| Stage | Work | Trace |
|---|---|---|
| **Ingest** | Parse, convert to engineering units, timestamp, identify the circuit, reject stale samples. A stale sample is not used for limits and not added to the totals. | FR-PRI-130…138 |
| **Fork** | The same sample goes to the engine and to the supervisor at the same time. | FR-PRI-042 |
| **Execute** | Update totals using the **actual** time between samples. Check limits in written order. Run the action of the first limit that is met. Maintain cycle and procedure stacks. Check registration triggers. | FR-PRI-140…187 |
| **Dispatch** | Clamp, send, and wait for the acknowledgement before treating the step as started. Re-send once a second so a board that restarted cannot keep using an old setpoint. | FR-PRI-110…118 |
| **Hand off** | Push records and events across the boundary. Written to storage before being acknowledged, and kept until the Web App confirms receipt. | FR-PRI-320…347 |

Both paths see the same sample, but only one of them is program logic. The supervisor reaches the
dispatcher without going through the engine, so a bug in step execution — or a corrupted program —
cannot stop it working.

### A2.3.1 Two things that are easy to get wrong

**Use the actual time between samples when updating totals, not a fixed number.** If you multiply by
an assumed interval, every sample that arrives early or late adds a small error. Over a 30-day test
these add up into a wrong capacity figure, and there is nothing in the result to show it happened.
(FR-PRI-143)

**Totals must survive 30 days at full current without losing precision.** A 32-bit float stops being
able to add small amounts to a large running total well before a multi-week test finishes. The range
and precision need a calculation, not an assumption. (FR-PRI-145)

---

## A2.4 Three checks, in three components

```
  authored      ┌────────────────┐   ┌───────────────┐   ┌──────────────┐   current in
  program ─────▶│ 1  Validation  │──▶│ 2  Limits     │──▶│ 3  Clamp     │──▶ the battery
                │ on transfer    │   │ every sample  │   │ every send   │
                │ Web App, then  │   │ Safety        │   │ Dispatcher,  │
                │ again by the   │   │ Supervisor,   │   │ whatever     │
                │ Primary        │   │ always on     │   │ sent it      │
                └────────────────┘   └───────────────┘   └──────────────┘
                 a bad program        a valid program     a conversion
                 — or encoder and     driving past the    error upstream,
                 decoder disagreeing  circuit's limits    on another machine
```

| # | Check | Trace |
|---|---|---|
| 1 | **Validation** — on transfer by the Link Server, then checked again independently by the Primary | FR-PRI-062, -063 |
| 2 | **Limits** — Safety Supervisor, every sample, always on | FR-PRI-042, -043 |
| 3 | **Clamp** — Setpoint Dispatcher, every time it sends | FR-PRI-116, -189AA |

They are in three different components so that one bug cannot get past all three.

### A2.4.1 Why the supervisor is separate code

Limits inside a program are written by engineers and can be wrong. The circuit's safety limits —
maximum charge voltage, minimum discharge voltage, maximum charge and discharge current, maximum
temperature — are configured once for the physical setup and apply to every program that runs there.

If the safety check were part of the same limit-evaluation code, one bug could disable both. That is
the only reason they are separate, and it is enough.

### A2.4.2 Why the dispatcher always clamps

The Primary receives setpoints that the Web Application has already validated and converted, so
clamping again looks like repeated work. Engineers will ask why. Three reasons:

- the conversion happened on a different machine, in different code;
- a wrong nominal capacity turns `10 ACn5` into a large current that still looks reasonable;
- the clamp is the only check a conversion bug cannot get past.

### A2.4.3 Failing in the safe direction

| Behaviour | Reason | Trace |
|---|---|---|
| **E-Stop and interlock are wired so that a broken or disconnected circuit reads as pressed** | If wired the other way, cutting the wire makes the system think everything is fine | FR-PRI-401 |
| **`FAULT` stays set until someone acknowledges it** | Clearing it automatically hides the event from the operator and leaves a gap in the test record | FR-PRI-374 |
| **On an internal fault, command safe idle first, then restart** | Resetting while a circuit is set to 40 A leaves the power stage running with no controller. The 1 Hz re-send timeout is the backup if even this fails | FR-PRI-378 |

---

## A2.5 The data path

Two streams leave the Primary, and they are not equally important.

| Stream | What it is | Priority |
|---|---|---|
| **Registration records** | The test result. Numbered, gap-detectable, buffered, kept until acknowledged. | **Never dropped** |
| **Real-time telemetry** | Numbers for the screen. Can be regenerated. | Dropped first |

A live reading arriving a second late costs nothing. A missing registration record is a permanent
gap in the test result that cannot be filled later. So when bandwidth or CPU is short, telemetry
suffers and registration does not. The two are marked differently on the wire so neither end can mix
them up. (FR-PRI-329, -330)

### A2.5.1 Buffering and forwarding

Sizing, using 64 circuits with the `CYCLE` registration format:

| | |
|---|---|
| Record size | 90 bytes |
| At one record per second per circuit | ~5.8 kB/s → **~500 MB/day** |
| Reserved buffer | 8 GB → **about 16 days** |
| 30-day test never collected | ~15 GB, out of 32 GB total |

The buffer is specified as a **storage size**, not a number of days. The 32 GB device is shared with
the OS, the application and system logs, so a duration on its own cannot be implemented. It is also
why FR-PRI-024A puts a cap on log and diagnostic storage — logs must never push out registration
data.

Registration is not a steady one-per-second. Delta triggers fire faster during a fast-changing step,
and `15 *` style triggers record at 0.1 s intervals. The numbers above are for planning, not a worst
case.

### A2.5.2 Field data is kept out of the control path

BMS, auxiliary sensor and Modbus data goes into the best-effort domain and then to the Link Server
only. There is no path into the real-time domain.

This follows from the confirmed decision that limits cannot use BMS signals. It keeps a third-party
protocol away from anything that can command current into a battery, keeps the BMS Interface Board
out of the safety-critical classification, and makes the separation easy to verify. It also means
CAN-FD 2 traffic — which the Primary does not control, and which a wiring fault could turn into a
flood of messages — can only slow down logging, never execution. (FR-PRI-241, -241A, -244)

**Worth stating plainly:** the ME system cannot stop a test because of a single-cell condition.
Single-cell overvoltage and cell temperature protection are handled by the pack's own BMS, and by
the safety limits working on the pack terminal measurements.

---

## A2.6 Storage and recovery

### A2.6.1 Data is only saved when it is really saved

On Linux, a `write()` that returned success is not necessarily on the disk yet. **A record counts as
saved only once it is committed to storage**; a buffered write that a power cut would lose does not
count. Every "survives a power cycle" requirement in this document depends on this being handled
deliberately — write-then-rename, an explicit flush, A/B records with checksums, or something
equivalent, argued in the SDD. (FR-PRI-019, -020A)

### A2.6.2 Nothing restarts itself

| Event | What happens |
|---|---|
| Power lost while running | Restore the context, report it along with how long the outage was, put the circuit in **recovery-pending — not `RUNNING`** — and wait for the operator to decide |
| DC-DC board comes back after a communication loss | Do not resume. Check the board's identity and firmware against the registered configuration, and require an explicit command |
| Watchdog reset while running | Report it as a fault. Do not resume quietly |
| Control application restarted by the platform | Every circuit that was running goes to recovery-pending |

The reason, written down because it will be questioned: after an outage of unknown length the
Primary does not know the state of the pack — whether it self-discharged, whether someone moved
something, or whether the outage was caused by a fault in the first place. **Putting 40 A back into
a pack in an unknown state without a person deciding is not defensible.**

If unattended multi-week testing makes waiting impractical (`Q-11`), the answer is a *conditional*
resume with clear bounds: allowed only if the outage was shorter than a configured limit, the pack
voltage on restart is within a configured band of what it was before, and no fault was latched. That
would be an opt-in per circuit, never the default. (FR-PRI-420…430)

### A2.6.3 Two clocks

| Clock | Used for |
|---|---|
| **Monotonic counter** | All intervals, totals, timeouts and record ordering |
| **Real-time clock** | Absolute timestamps only — a label |

Changing the clock must never change a step's elapsed time, a running total, a timeout, or the order
of records. Every telemetry and registration record carries both. Almost every timing bug in a
long-running test system comes from using wall-clock time to measure an interval.
(FR-PRI-441, -442)

---

## A2.7 What the platform requires

A quad-core CPU with 4 GB is much more than this workload needs. That is the risk: on this platform,
code that breaks timing usually still runs and still passes testing.

| Requirement | What happens if it is skipped |
|---|---|
| Control code at high priority, with its own CPU allocation | An operator exports a 30-day test, the database saturates three cores, and a limit check runs late |
| Control-path memory locked in RAM | A page fault inside the control cycle |
| No memory allocation and no file I/O on the control path | Timing variation that testing does not show and that appears after hundreds of hours |
| Total memory use bounded and measured | The kernel kills the process that is controlling 64 battery packs |
| Memory use does not grow over 30 days | 4 GB hides a 1 kB-per-record leak for a day, then fails in week three |
| A record is on disk before it is acknowledged | Every "survives a power cycle" guarantee in this document becomes untrue |
| Supervision detects a **hung** process, not just a crashed one | A software watchdog inside the hung process detects nothing |
| Starts on its own — no login, no shell, no display needed | The system does not come back after a power cut |
| Safe idle commanded before the service manager stops or upgrades the application | A routine restart leaves 64 circuits powered with no controller |

(FR-PRI-011…018, -019…025A, -026A…034A)

---

## A2.8 Load at 64 circuits

Load scales directly with the measurement update rate, which is **not yet decided** (`Q-62`). The
figures below use *R* = updates per second per circuit so they can be filled in once *R* is fixed.

| Load | Formula | Example at R = 10 |
|---|---|---|
| Limit-evaluation passes | 64 × R per second | 640 / s |
| CAN-FD 1, one frame per channel | 64 × R frames/s | 640 frames/s → ~12 % of a 500 kbit/s + 2 Mbit/s bus |
| CAN-FD 1, aggregated per board | 24 × R frames/s | 240 frames/s → ~8 % |
| Registration data | independent of R — set by the triggers in the program | ~500 MB/day at 1 record/s/circuit |

The example column is illustrative only. Frame sizing assumes 500 kbit/s arbitration, 2 Mbit/s data
phase, 29-bit identifiers and about 20 bytes per channel; all of that is fixed by the ICD, not here.

Two conclusions hold at any rate:

1. **Aggregate measurements per board rather than sending one frame per channel.** With 8 channels
   per board, this cuts frame count by roughly two-thirds. The saving that matters is interrupt and
   processing load on the Primary, not bus bandwidth.
2. **Size receive queues for bursts, not averages.** If all 8 boards report at the same moment, 24
   frames arrive back to back. Separately, releasing a `SYNCLine` barrier can put 64 circuits into a
   new step at once — 64 commands plus 64 acknowledgements inside one dispatch deadline.
   (FR-PRI-495B, -495C)

Load must be measured on target hardware at the **full** 64-circuit configuration. A test at a
reduced circuit count is not evidence. (FR-PRI-495A)

---

## A2.9 Out of scope, on purpose

| Not in the Primary | Where instead, and why |
|---|---|
| The current and voltage regulation loop | The DC-DC board. Regulation belongs next to the hardware, so it does not depend on a CAN link staying up. |
| Hardware protection of the power stage | The DC-DC board. Protection must not need the Primary to be alive. |
| Speaking a battery vendor's BMS protocol | The BMS Interface Board. The Primary only maps decoded signals to a circuit. |
| Stopping a test on a single-cell condition | Nowhere, by decision — see §A2.5.2. |
| Long-term storage of results | The Web Application database. The Primary buffers; it is not the archive. |
| Depending on the Web App to run a test | Nothing in the real-time domain reaches across the boundary for anything it needs — except `REST`, which is why `Q-51` matters. |

---

## A2.10 Open questions that would change this architecture

Listed rather than hidden. These change the structure, not just a number.

| ID | Question | What it changes |
|---|---|---|
| **`Q-62`** | **How often do the DC-DC boards report measurements?** Sets bus load, how accurately totals can be integrated, and how finely registration can be triggered. *(Previously stated as 100 ms in v0.1–v0.2, taken from a line in `RD-1` about Digatron's own hardware. That was wrong — it is our decision to make.)* | Nothing structural, but every load figure in §A2.8 and the staleness timeouts in Part 02 depend on it. |
| **`Q-50`** | The opcode set does not cover every nominal-value combination in the manual — power-plus-voltage, and resistance. | **Recommend replacing the fixed opcode list with two mode fields** — main regulation mode plus transition mode. That covers every combination, includes resistance, and costs nothing before Part 03. |
| **`Q-51`** | `SAVE` / `REST` reach into the Web App database, breaking the third boundary rule. | Either the Durable Store owns saved sections, or programs using `REST` cannot start with the link down. |
| **`Q-43`** | Can a parallel group span DC-DC boards? With 8 channels per board, a 16-circuit group has to. | Whether the dispatcher has to divide setpoints and combine measurements *across* boards on a shared time base — considerably harder than within one board. |
| **`Q-11`** | Is unattended multi-week testing required? | Whether §A2.6.2 gains a conditional resume path. |
| **`Q-41`** | Which kernel configuration — standard, `PREEMPT`, or `PREEMPT_RT`? | Nothing structural, but adding `PREEMPT_RT` to a vendor BSP late in a project is a known way to lose a month. |
| **`Q-30`** | Eight in-scope operators are only documented in a manual we do not have. | Possibly the dispatcher and the DC-DC interface, if any of them implies a control mode. |

---

## A2.11 Revision history

| Ver | Date | Change |
|---|---|---|
| 0.1 | 2026-08-05 | Initial architecture. |
| 0.2 | 2026-08-05 | Restructured around three focused figures instead of one combined diagram. Added the boundary rules as an explicit table, which surfaced `Q-51`. |
| 0.3 | 2026-08-05 | **Removed the 100 ms base period.** It came from a line in `RD-1` describing Digatron's own hardware and should never have been stated as an ME parameter; the measurement rate is now open question `Q-62`. §A2.8 load figures rewritten as formulas. Language simplified throughout. |
