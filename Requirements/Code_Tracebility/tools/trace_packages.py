"""
trace_packages.py - work packages and effort estimates.

A work package is the unit that gets scheduled. Requirements are traced to one
of these by `trace_status.py`; the requirement count per package is DERIVED,
never typed here, so it cannot drift.

--------------------------------------------------------------------------
ESTIMATION BASIS - read this before quoting any number downstream
--------------------------------------------------------------------------

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
"""

# id, title, owner, impl_days, hil_days, priority, blockers, note
PACKAGES = [
    # ================================================== PRIMARY BOARD ======
    ("WP-P01", "Secondary Board enrolment and supervision over the internal bus",
     "Primary", 6, 3, "P1",
     "OI-26, OI-25, TBD-19",
     "The largest single gap between the code as built and the SRS. Today the "
     "board learns its population from `--channels` on the command line and is "
     "admitted only by the host-side `0xDD` handshake; nothing on the CAN bus "
     "announces itself. Everything in section 13, and REQ_293's "
     "'enrolled Secondary' precondition, waits on this."),

    ("WP-P02", "Complete the operator set: PAU, DCH, INT, BEG/CYC, GOTO, REG, ERR, MSG, TABLE",
     "Primary", 12, 5, "P1",
     "OI-16 (REG), OI-17 (SET), OI-20 (DCH/INT/ERR/MSG), OI-22 (cycle nesting)",
     "3 of 13 operators are built. This is the biggest functional package and "
     "the one most exposed to specification risk: four separate open issues say "
     "that nobody has written down what these operators do. BTS covered DCH - "
     "as complex as CHA - in a single line. Do not start the sub-program and "
     "cycle-nesting parts before OI-22 is answered; it was never built in BTS "
     "either, so committing to it in Phase 1 is a real and avoidable cost."),

    ("WP-P03", "CHA/DCH control modes: constant-voltage, CC-to-CV, non-time limits, current clamping",
     "Primary", 6, 4, "P1",
     "OI-20, and REQ_198 needs a temperature source binding",
     "CCChg today is constant-current only, with a TIME cut-off and no Action. "
     "This package adds the CV mode, the CC-then-CV transition of REQ_194, the "
     "Voltage/Ah/Temperature limits, and - separately important - the "
     "REQ_150/REQ_155 clamp that stops a program commanding more current than "
     "the channel's configured maximum. The clamp cannot be built before "
     "WP-P19 stores that maximum."),

    ("WP-P04", "Cut-off condition engine: 13 condition types, 6 comparators, multiple per step, debounce",
     "Primary", 5, 2, "P1",
     "TBD-36 (how many per step), TBD-37 (debounce count)",
     "1 of 13 types and 2 of 6 comparators today, one condition per step, no "
     "debounce at all. The refusal behaviour already in `step_decode.c` is "
     "correct and should be preserved: a wire count above the cap is rejected "
     "rather than folded to 0, because the old firmware produced a step that "
     "never ends that way. REQ_349's debounce is cheap to add and materially "
     "changes behaviour on a noisy channel - do not defer it as cosmetic."),

    ("WP-P05", "Test record production and delivery on UDP 10001",
     "Primary", 4, 2, "P1",
     "TBD-26 (registration types and interval range)",
     "The transport is already built end to end - socket, destination, a "
     "64-deep ring in `data_mgr.c` and `ring_flush()` - and carries nothing "
     "(T-29). The raw material is also already decoded and thrown away: SET's "
     "13-bit registration mask and CCChg's registration-parameter list "
     "(`0x21`-`0x2D`). This package is mostly about deciding what a record IS, "
     "which is why TBD-26 gates it. REQ_285's no-silent-loss guarantee and "
     "REQ_288's session identity land here."),

    ("WP-P06", "Live data completeness: fill the zeroed 0xCC fields",
     "Primary", 3, 1, "P2",
     "none",
     "The 86-byte frame already has a field for nearly everything REQ_283 asks "
     "for, and all 80 payload offsets are asserted on the host. What the engine "
     "fills is step number, both statuses, both run times, current, voltage and "
     "operator code; temperature, power, the four capacity/energy accumulators, "
     "cycle and table position, registration type and digital I/O state go out "
     "as zero. Cheap, visible to the operator, and independent of everything "
     "else - a good early win."),

    ("WP-P07", "Non-volatile persistence of configuration, calibration and programs",
     "Primary", 5, 2, "P1",
     "OI-15 (medium, on-media format, partial-write handling)",
     "Everything is volatile today; a restart loses every program, battery "
     "record and raw config. For 64 channels that is a large amount of operator "
     "work lost to a power blip. OI-15 must settle the medium and the "
     "partial-write rule first - a torn write that yields a plausible-looking "
     "calibration constant is worse than no persistence."),

    ("WP-P08", "Power-fail detection, snapshot and automatic resume",
     "Primary", 5, 3, "P2",
     "TBD-35 (guaranteed warning time - HARDWARE), OI-15, OI-34 (safety of automatic resume)",
     "Blocked on hardware, not software: TBD-35 is a property of the Primary "
     "Board's power-supervisory circuit and no software design is possible "
     "without the number. OI-34 is live - the client has directed automatic "
     "resume and has flagged that the decision may change after a safety "
     "discussion, and REQ_314 forbids exactly what REQ_344 requires. Do not "
     "build until both are closed."),

    ("WP-P09", "Startup gating, commanded shutdown, restart semantics",
     "Primary", 3, 2, "P2",
     "TBD-33 (time to ready), OI-05 (safe state)",
     "`main.c` today starts all four threads and registers immediately, with no "
     "enrolment or configuration gate before the first command can land, and "
     "shutdown drops the stop flag and joins without bringing any channel to a "
     "safe state. Depends on WP-P01 for the enrolment gate and WP-P07 for the "
     "restart record."),

    ("WP-P10", "Fault containment: classify every fault by scope and act on the scope",
     "Primary", 4, 2, "P1",
     "OI-05 (safe state), OI-11 (error catalogue)",
     "Per-circuit isolation is genuinely real - an engine fails to "
     "`CHANNEL_OFFLINE` or `DECODE_ERROR` without touching its neighbours - but "
     "there is no notion of a classified fault to inhibit on, and no "
     "board-scope or system-scope concept at all. Both blockers are answered by "
     "one conversation each; this package is cheap once they are."),

    ("WP-P11", "Error code catalogue and structured error reporting to the Battery Manager",
     "Primary", 3, 1, "P1",
     "OI-11",
     "BTS asked 'how do we indicate this error?' in nine separate places and "
     "never answered. Nine ME requirements depend on the single answer, and "
     "REQ_82's structured record - code, severity, subsystem, channel, time - "
     "is the shape everything else reports through. Answering OI-11 is the "
     "highest-leverage hour available in this whole plan."),

    ("WP-P12", "Persistent, retrievable, filterable log with per-channel history",
     "Primary", 4, 1, "P2",
     "TBD-34 (retention)",
     "`util/log.c` writes to stderr and nothing survives a restart. REQ_324's "
     "'logging shall never delay a control decision' is accidentally true today "
     "because there is nothing to block on - adding storage is exactly what "
     "puts it at risk, so the write path must stay off the control thread by "
     "design rather than by luck."),

    ("WP-P13", "Real Time Clock: I2C device, dual-format time stamps, sync from host, distribution to Secondaries",
     "Primary", 4, 3, "P1",
     "TBD-04 (permitted Primary-to-Secondary offset)",
     "HARDWARE-CRITICAL - I2C register access; needs the datasheet and "
     "hardware-in-the-loop verification. `0xEE` Q5 Sync Time is already parsed "
     "with its big-endian epoch and deliberately not applied. Note REQ_31: a "
     "clock correction must not put a discontinuity into a running test's "
     "elapsed time - which is straightforward now, because `step_engine.c` "
     "takes `now_ms` as a parameter and never reads a clock itself, and much "
     "harder if that property is ever lost."),

    ("WP-P14", "Primary digital inputs: edge detection, time stamping, enable/disable, Secondary input read-through",
     "Primary", 3, 2, "P2",
     "TBD-01 (time-stamp resolution)",
     "HARDWARE-CRITICAL - GPIO. No digital-input path exists in `src/` at all. "
     "The Secondary read-through half of REQ_9 depends on WP-P01."),

    ("WP-P15", "Primary digital outputs: level command, enable/disable, safe state on link loss",
     "Primary", 3, 2, "P2",
     "TBD-02 (safe-state time), OI-05 (what 'safe' is)",
     "HARDWARE-CRITICAL - GPIO. REQ_21's safe state on link loss or fault is "
     "the safety-relevant half and should not be built from a guess about what "
     "'safe' means."),

    ("WP-P16", "RS485_1: Modbus RTU slave towards a third-party HMI",
     "Primary", 6, 3, "P3",
     "TBD-05 (register map for 64 channels), OI-07",
     "No Modbus stack and no RS-485 port code exist. OI-07 is the real problem, "
     "not the protocol: 64 channels times REQ_35's parameter list does not fit "
     "a flat register map, so the map is either paged, or operator-selected, or "
     "a published subset - and the HMI integrator needs that fixed before "
     "anything is written."),

    ("WP-P17", "RS485_2: Modbus master reading third-party devices, with value binding to channels",
     "Primary", 6, 3, "P3",
     "TBD-06, TBD-07, TBD-08, TBD-09, TBD-10, TBD-11",
     "Six open numbers, all inherited verbatim from BTS as 'XXX'. REQ_47 - "
     "binding a value read here to a named quantity of a channel, usable as a "
     "Nominal Value or Limit source - is the interesting requirement and the "
     "one that touches the step engine."),

    ("WP-P18", "User-configurable CAN ports: port/message/signal configuration, subscribe, publish, MTO",
     "Primary", 15, 6, "P3",
     "TBD-14 (which port), OI-09 (physically distinct?), OI-10 (11-bit, 29-bit or both)",
     "69 requirements, the largest block in the SRS. `proto/can_frame.c` is NOT "
     "a starting point - it is the fixed 64-byte internal-bus codec, "
     "little-endian, with a hard-coded block/slot layout, and sharing anything "
     "with a configurable-port layer would break the one-endianness-per-file "
     "rule the project enforces deliberately. OI-09 is a hardware question that "
     "can invalidate REQ_270: if a user port shares a controller or transceiver "
     "with the internal bus, the internal bus cannot be kept unexposed."),

    ("WP-P19", "Per-channel analog limit configuration, validation and exception handling",
     "Primary", 5, 3, "P1",
     "TBD-15 ('near 0 V' threshold)",
     "No per-channel analog limits are stored at all, which is why WP-P03's "
     "current clamp has nothing to clamp against. This package brings in Max "
     "Battery Voltage, Max Charging/Discharging Current, the sampling rates, "
     "and the six named exceptions - each of which must disable exactly its own "
     "channel and leave the other 63 running."),

    ("WP-P20", "Calibration: voltage, current and analog output, per channel, bound to board identity, persisted",
     "Primary", 6, 4, "P2",
     "TBD-16 and OI-13 (there is no procedure to implement), OI-14 (board binding)",
     "There is genuinely nothing to carry forward - BTS section 8.1.5 contained "
     "two requirement numbers with empty statements. OI-14 is the field hazard: "
     "wrong calibration constants after a board swap produce measurements that "
     "look completely plausible. `0xA0` calibration frames are recognised by "
     "the router and unimplemented (T-12). Depends on WP-P07 for persistence "
     "and WP-P01 for the board identity to bind to."),

    ("WP-P21", "Program management: integrity verification, download gating, read-back, storage limits",
     "Primary", 4, 2, "P2",
     "TBD-17 (maximum program size per channel), OI-23",
     "Completeness is checked via the chain terminator and every `nextIndex` is "
     "range-checked, which is the load-bearing half of REQ_240. Missing: a "
     "whole-program integrity check, refusing a download to a running channel "
     "(REQ_239 - today a `0xBB` Q1 is answered unconditionally and a Q4 packet "
     "for a running circuit is accepted), and any read-back path. TBD-17 also "
     "closes T-27: the per-slot buffer size is what produces the unverified "
     "261 MB `.bss`."),

    ("WP-P22", "Test control completion: Pause, Continue, Reset, group operations, Start preconditions, safe state",
     "Primary", 5, 3, "P1",
     "OI-05 (safe state), TBD-27/OI-30 (Start on a completed test), OI-29 (command scoping)",
     "All six commands parse and ack; only Start and Stop do anything. "
     "`core_logic.c` logs Pause, Continue and Reset as `not implemented yet` "
     "AFTER the frame has already been acked `0x01` - so the Battery Manager is "
     "currently told that a Pause succeeded. Fixing the ack semantics is "
     "WP-P23; fixing the behaviour is here. REQ_293's precondition check needs "
     "WP-P01, WP-P19 and WP-P20 to have state worth checking."),

    ("WP-P23", "Host link hardening: ack semantics, unrecognised-query NACK, session limit, link-loss buffering",
     "Primary", 5, 2, "P1",
     "TBD-12 (session count), TBD-13 (buffering duration), OI-08",
     "Most of section 15 is already DONE and hardware-verified - client role, "
     "port, backoff, CRC, reassembly, CRC-delimited framing. What remains is "
     "the honesty of the replies: REQ_281 wants 'accepted' distinguished from "
     "'executed', and REQ_280 forbids acking an unrecognised query as "
     "successful, which the board does today (T-45). Both are the same seam - "
     "Core Logic needs an outbound message type through `g_q_comm` - and "
     "closing it is also what lets an `0xEE` ack ever mean 'the test started'."),

    ("WP-P24", "Network configuration from the host: static IP, DHCP control, MAC read-back, remaining 0xDD queries",
     "Primary", 4, 2, "P3",
     "none",
     "`0xDD` Q2 delete, Q3 discovery and Q4 IP configuration are documented in "
     "`bm_device_registration_v5.0.md` and unimplemented (T-10). The MAC is "
     "already read from the real interface and shipped in the registration "
     "payload; what is missing is a query that re-reads it on demand."),

    ("WP-P25", "Internal CAN bus robustness: bus-off recovery, stale-data detection, priority IDs, error counters, load budget",
     "Primary", 5, 3, "P1",
     "TBD-20, TBD-21 (bit rates), TBD-22 (load budget), TBD-24 (staleness), OI-27",
     "The transport works and is partially hardware-verified, and the RTT "
     "instrument REQ_266 needs already exists (ADR-31, microseconds per "
     "Secondary). The gap is that the A53 never sees the CAN controller - the "
     "M7 owns it and reports nothing about its state - so bus-off detection "
     "needs an M7-side change and a new RPMsg message, which is why this is "
     "not a pure A53 package. OI-27 is the first question to answer in the "
     "whole register: the bit rates set the ceiling on every channel's "
     "sampling and registration rate."),

    ("WP-P26", "Performance, timing and memory budgets: control interval, jitter, headroom, WCET, .bss verification",
     "Primary", 4, 5, "P1",
     "TBD-28 (control interval), TBD-29, TBD-30, TBD-31, TBD-32, OI-03, OI-31",
     "Mostly measurement, which is why `hil` exceeds `impl`. OI-31 calls "
     "TBD-28 the single most architecturally significant unknown in the SRS: it "
     "decides whether a containerised Linux A53 can host the control loop of 64 "
     "channels at all, and therefore whether the current architecture is "
     "viable. CPU3 pinning is in place (ADR-30) but the CPU is not yet "
     "kernel-isolated (T-60), so no jitter number measured today would be the "
     "real one. Also closes T-27's 261 MB `.bss` question."),

    ("WP-P27", "Scale out to 64 channels: 8 Secondaries x 8 channels, CAN Block 2, capacity proof",
     "Primary", 5, 4, "P1",
     "TBD-18 / OI-24 (is the address range 8x8 or 15x15)",
     "The addressing, the 64 storage slots and the 64 engine contexts all "
     "exist and are host-tested; what has never run is more than one Secondary "
     "or more than 4 channels. `me_can_block_for_channel()` maps channels 5-8 "
     "to Block 2 but only Block 1 has been exercised (ADR-28). Settle OI-24 "
     "before this, not after: it decides a wire format, and the two ME "
     "documents currently disagree."),

    ("WP-P28", "Charge/discharge change-over: bank type per channel and enforced dead time",
     "Primary", 2, 2, "P2",
     "TBD-03 (dead time), OI-06 (who sequences it)",
     "HARDWARE-CRITICAL and safety-relevant. Small in code, entirely dependent "
     "on OI-06: if the Primary sequences the change-over then bus latency eats "
     "into the dead time and the margin must be designed for; if the Secondary "
     "sequences it, this package is almost nothing. BTS used 500 ms on the "
     "Secondary. Do not guess."),

    ("WP-P29", "Analog output commanded as engineering values (close out the voltage half)",
     "Primary", 1, 1, "P3",
     "T-66 (should CCChg ever carry a non-zero voltage)",
     "Already true for current - `me_can_pack_set()` sends amperes as a float "
     "and never a raw DAC code. Voltage is simply not commanded: `step_decode.h` "
     "has no voltage field for CCChg, which may well be correct for a "
     "constant-current mode. Rolls up naturally into WP-P03."),

    ("WP-P30", "Version reporting and Secondary compatibility gate",
     "Primary", 2, 1, "P2",
     "none beyond WP-P01",
     "REQ_317/REQ_318. Needs enrolment to have a version to report and a board "
     "to refuse."),

    ("WP-P31", "Control-software stall detection driving all channels to safe state",
     "Primary", 3, 2, "P1",
     "OI-05",
     "REQ_303 and the resilience half of REQ_324. Safety-relevant: a stalled "
     "control thread with contactors closed is the worst failure this system "
     "has. Note the existing design already makes deadlock impossible by "
     "construction - every queue send has a finite timeout and drops on expiry "
     "(ADR-12) - so this package is about detecting a stall that is not a "
     "deadlock, and about what to do when one is found."),

    ("WP-P32", "Close out the known open code items",
     "Primary", 3, 2, "P1",
     "none",
     "T-44 audit the frame-length table against captured frames (a wrong entry "
     "is recoverable via the CRC backstop but logs itself and should be "
     "fixed), T-45 the premature `0xEE` ack, T-46 watch for a battery payload "
     "that is not 40 bytes, T-40 watch for `0xBB` Q4 CRC rejections, T-48 the "
     "`0xAA` Q7-Q10 broadcast frames with their 2-byte header, T-38/T-39 the "
     "`0xBB` Q1 length and Q2 reply decision, T-27 the `.bss` check, T-63 "
     "revert the Dockerfile to `scratch` before the next production release, "
     "T-65 confirm the M7 echo root cause. Several of these are watch items "
     "that close for free on the next hardware run. **No SRS requirement "
     "traces to this package, by design** - it is code-hygiene debt from the "
     "T-register rather than requirement coverage, and it is listed here so "
     "its cost appears in the schedule instead of being absorbed silently."),

    # =========================================== BATTERY MANAGER WEB APP ===
    ("WP-B01", "Channel overview and per-channel detail views for 64 channels",
     "BM", 10, 0, "P1",
     "none",
     "REQ_326/327/339 and REQ_250's hard rule: a channel that does not exist "
     "must not be shown as merely idle. The overview is the screen the whole "
     "product is judged on, and 64 live channels is a different design problem "
     "from BTS's one."),

    ("WP-B02", "Program editor and validator covering every operator and action",
     "BM", 20, 0, "P1",
     "OI-16, OI-17, OI-20, OI-21, OI-22, OI-18",
     "The single largest BM package. REQ_328 requires validation against every "
     "rule in section 10 BEFORE download, which means the editor has to encode "
     "the BM-side data rules that ~30 requirements in section 10.3 state - the "
     "PAU blank-nominal rule, the CHA nominal/limit pairing rules, the STO "
     "no-parameters rule and so on. Five open issues describe operator "
     "semantics that nobody has written down; the editor cannot validate what "
     "is undefined, so this package tracks OI closure as closely as WP-P02 "
     "does."),

    ("WP-B03", "Program library, reuse across channels and sessions, assignment tracking",
     "BM", 8, 0, "P1",
     "none",
     "REQ_329/238/330 plus REQ_241's compare-with-Primary. REQ_330's warning - "
     "the Program stored on the Primary differs from the one the BM believes is "
     "there - needs WP-P21's read-back on the Primary side."),

    ("WP-B04", "Group operations across channels with per-channel outcome reporting",
     "BM", 6, 0, "P1",
     "none",
     "REQ_292/331/332. REQ_332's explicit confirmation before stopping or "
     "restarting a running test matters more here than anywhere: on a "
     "64-channel shared machine one mis-click destroys somebody else's week."),

    ("WP-B05", "Live trends and scope-aware alarm and fault display",
     "BM", 8, 0, "P2",
     "OI-11 (error catalogue)",
     "REQ_333/337. A channel fault must LOOK different from a Secondary Board "
     "fault and from a system fault, which is only possible once WP-P10 and "
     "WP-P11 give faults a scope and a code."),

    ("WP-B06", "Test record storage, integrity marking and documented export",
     "BM", 10, 0, "P1",
     "TBD-26",
     "REQ_334/335/336 plus REQ_288's session identity. REQ_335 is the one to "
     "get right: a record containing a reported data loss, a restart gap or a "
     "time correction must be marked, so an incomplete record can never be "
     "read as a complete one. Pairs with WP-P05."),

    ("WP-B07", "Per-channel configuration workflows for sections 8 and 9",
     "BM", 10, 0, "P1",
     "TBD-15, and the digital I/O enable rows need WP-P14/WP-P15",
     "REQ_338 plus the BM-scope configuration rows: Max Battery Voltage, Max "
     "Charging/Discharging Current, both sampling rates, the analog output "
     "ranges, and the digital input/output enable flags of REQ_6 and REQ_16. "
     "Pairs with WP-P19."),

    ("WP-B08", "Calibration workflows per channel",
     "BM", 6, 0, "P2",
     "TBD-16, OI-13, OI-14",
     "Cannot start before the procedure exists. Pairs with WP-P20, which owns "
     "REQ_145, REQ_160, REQ_161 and REQ_162 - all `BM + Primary`, so this "
     "package is their Battery Manager half and traces no requirement "
     "exclusively."),

    ("WP-B09", "CAN port, message and signal configuration UI with full validation",
     "BM", 18, 0, "P3",
     "OI-10 (identifier width), TBD-14, OI-07",
     "Section 7's BM half - and the validation is the work, not the forms: "
     "unique enumerations across all ports, REQ_126's contiguous start-bit/"
     "length arithmetic, the 0x000-0x7FF identifier range, periodicity in 5 ms "
     "multiples between 10 and 2000 ms, and the MTO minimum of three times "
     "periodicity. Pairs with WP-P18 and should not lead it."),

    ("WP-B10", "RS-485 configuration UI for both ports",
     "BM", 5, 0, "P3",
     "TBD-05 through TBD-11",
     "Pairs with WP-P16/WP-P17."),

    ("WP-B11", "Network configuration UI: static IP, DHCP, MAC display",
     "BM", 3, 0, "P3",
     "none",
     "Pairs with WP-P24, which owns REQ_54, REQ_55 and REQ_56 - all "
     "`BM + Primary`, so this package is their Battery Manager half and traces "
     "no requirement exclusively."),

    ("WP-B12", "Log retrieval and filtering by channel, board, severity and time",
     "BM", 5, 0, "P2",
     "TBD-34",
     "REQ_323 is `BM + Primary` and is owned by WP-P12, which has to serve the "
     "logs before anything can filter them; this package is its Battery Manager "
     "half and traces no requirement exclusively."),

    ("WP-B13", "Reject unachievable sampling and registration rate combinations",
     "BM", 5, 0, "P1",
     "OI-12, TBD-22, TBD-30",
     "REQ_140 requires the BM to refuse a configuration whose total data rate "
     "would exceed the internal bus or Primary processing budget, and to say "
     "WHICH limit was exceeded. BTS permitted 1 ms sampling on one channel; 64 "
     "channels at 1 ms is 64,000 samples per second across a shared bus. Until "
     "the bus load calculation exists the BM cannot validate anything here, so "
     "this package is genuinely blocked rather than merely unstarted."),

    ("WP-B14", "Error catalogue presentation with meaning, scope, severity and operator action",
     "BM", 4, 0, "P1",
     "OI-11",
     "REQ_320's catalogue has to be authored once and then rendered. REQ_82, "
     "REQ_302 and REQ_320 are all `BM + Primary` and are owned by WP-P11/WP-P10 "
     "on the reporting side, so this package is their Battery Manager half and "
     "traces no requirement exclusively."),

    ("WP-B15", "Enrolment and software version display",
     "BM", 4, 0, "P2",
     "OI-26",
     "REQ_259/317/250. Pairs with WP-P01 and WP-P30."),

    ("WP-B16", "Access control and per-action audit trail",
     "BM", 6, 0, "Phase 2",
     "OI-33",
     "REQ_340 is Phase 2 and excluded from the Phase 1 totals. Recorded here "
     "because OI-33 notes no source document mentions access control at all, "
     "and a shared 64-channel laboratory machine is exactly where it matters."),

    ("WP-B17", "Wire-protocol conformance: CRC order, float encoding, ack semantics, session IDs",
     "BM", 5, 0, "P1",
     "TBD-25 / OI-28 (CRC byte order)",
     "The Primary side is settled and hardware-verified as big-endian in both "
     "directions (ADR-9), and the standing decision is that the Battery Manager "
     "conforms to the board (T-33). This package is the BM-side work to match "
     "it, plus honouring REQ_281's accepted-versus-executed distinction once "
     "WP-P23 provides it - the BM must stop reporting a test as started when "
     "only the command was queued. **No SRS requirement traces here "
     "exclusively**: REQ_276, REQ_277, REQ_281, REQ_287 and REQ_288 are all "
     "`BM + Primary`, and a requirement gets exactly one owner in this matrix, "
     "so they sit with the Primary package that carries their remaining work. "
     "This package is the Battery Manager's half of those five."),
]
