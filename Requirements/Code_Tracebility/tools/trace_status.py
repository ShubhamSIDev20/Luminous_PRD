"""
trace_status.py - the per-requirement implementation verdict.

THIS FILE IS THE ONE PLACE TO EDIT when code changes.

Every requirement in ME_Primary_SRS_V0.1 gets exactly one row here. The
requirement text, applicability, category and SRS status are NOT repeated -
they are read live from ../../ME_Primary_SRS_V0.1/tools/me_srs_data.py, so this
file can never disagree with the SRS about what a requirement says.

What this file adds is the three things the SRS cannot know:

    verdict   - where the me-primary code actually stands
    evidence  - the file/symbol that proves it, or the gap that denies it
    wp        - which work package in ME_Work_Packages_and_Estimates.md owns it

Verdict vocabulary (see README.md for the full definitions):

    DONE     implemented AND covered by a host test or a hardware run
    PARTIAL  some of it is implemented; `evidence` must name the remaining gap
    TODO     nothing in the code addresses it
    BLOCKED  cannot be implemented until a TBD/OI in the register is closed
    N/A-BM   Battery Manager scope - no me-primary obligation
    DEFERRED Phase 2
    INFO     an Information row: states a fact, imposes no code obligation

A verdict of BLOCKED still carries an effort estimate through its work
package - "blocked" means the work cannot START, not that it is free.
"""

# --------------------------------------------------------------- defaults ---
# Applied to a whole numeric range, then overridden below by SPECIFIC.
# Keeping the bulk here is what makes the 353 rows reviewable: a section that
# is uniformly unbuilt says so once.
#
# (first, last, verdict, evidence, wp)
RANGES = [
    (  1,  10, "TODO",   "No GPIO code exists. `src/` has no digital-input path.", "WP-P14"),
    ( 11,  21, "TODO",   "No GPIO code exists. `src/` has no digital-output path.", "WP-P15"),
    ( 22,  23, "TODO",   "No bank-type table and no change-over sequencing exist.", "WP-P28"),
    ( 24,  31, "TODO",   "No RTC device access. `util/log.c` timestamps log lines only.", "WP-P13"),
    ( 32,  37, "TODO",   "No Modbus stack and no RS-485 port code exist.", "WP-P16"),
    ( 38,  47, "TODO",   "No Modbus stack and no RS-485 port code exist.", "WP-P17"),
    ( 48,  62, "TODO",   "Not addressed by the current host-link implementation.", "WP-P23"),
    ( 63, 131, "TODO",   "No user-configurable CAN layer. `can_frame.c` is the fixed "
                         "internal-bus codec only, not a configurable port.", "WP-P18"),
    (132, 159, "TODO",   "No per-channel analog limit validation and no exception "
                         "machinery exist.", "WP-P19"),
    (160, 166, "TODO",   "No calibration storage, procedure or apply path exists.", "WP-P20"),
    (167, 232, "TODO",   "Operator not decoded by `proto/step_decode.c` and not "
                         "executed by `exec/step_engine.c`.", "WP-P02"),
    (233, 241, "TODO",   "No program integrity check, readback or download gating.", "WP-P21"),
    (242, 250, "TODO",   "Addressing exists; discovery and full 64-channel scale do not.", "WP-P27"),
    (251, 260, "TODO",   "No enrolment handshake on the internal bus. Circuits are "
                         "admitted only by the host-side `0xDD` registration.", "WP-P01"),
    (261, 270, "TODO",   "Transport exists; bus supervision and budgets do not.", "WP-P25"),
    (271, 282, "TODO",   "Not addressed by the current host-link implementation.", "WP-P23"),
    (283, 288, "TODO",   "No test-record producer. UDP 10001 carries nothing.", "WP-P05"),
    (289, 296, "TODO",   "Command parsed but no behaviour behind it.", "WP-P22"),
    (297, 303, "TODO",   "No fault classification, scope or reporting model exists.", "WP-P10"),
    (304, 311, "TODO",   "No timing analysis, budget or headroom measurement exists.", "WP-P26"),
    (312, 319, "TODO",   "No persistence and no startup/shutdown state machine.", "WP-P09"),
    (320, 325, "TODO",   "Logging is unstructured stderr only. No store, no retrieval.", "WP-P12"),
    (326, 340, "TODO",   "Battery Manager scope.", "WP-B01"),
    (341, 345, "TODO",   "No power-fail detection and no snapshot exist.", "WP-P08"),
    (346, 353, "TODO",   "Cut-off engine covers one type of the thirteen.", "WP-P04"),
]

# ---------------------------------------------------------------- specific ---
# One entry per requirement whose verdict is NOT the range default, or whose
# evidence must name a file, a symbol or a named gap.
#
# rid -> (verdict, evidence, wp)
SPECIFIC = {
    # ---- 1.0 / 2.0 / 3.0  Digital I/O and relays -------------------------
    5:   ("BLOCKED", "Needs TBD-01 (time-stamp resolution) before it can be built "
                     "or tested.", "WP-P14"),
    10:  ("DEFERRED", "Phase 2.", "WP-P18"),
    20:  ("DEFERRED", "Phase 2.", "WP-P18"),
    21:  ("BLOCKED", "Needs TBD-02 (safe-state time) and OI-05 (what 'safe' means).",
          "WP-P15"),
    23:  ("BLOCKED", "Needs TBD-03 (dead time) and OI-06 (who sequences it).", "WP-P28"),

    # ---- 4.0  Real Time Clock --------------------------------------------
    25:  ("PARTIAL", "`util/log.c` prefixes every line with a timestamp, but not the "
                     "required Epoch + `YYYY-MM-DD HH:MM:SS` pair, and log lines are "
                     "not the same thing as test-record time stamps.", "WP-P13"),
    26:  ("PARTIAL", "`0xEE` Q5 Sync Time is parsed with its big-endian epoch "
                     "(`proto/control_frame.c`) but deliberately NOT applied - "
                     "`core_logic.c` logs `not applied - the board clock is owned by "
                     "Torizon OS`.", "WP-P13"),
    30:  ("BLOCKED", "Needs TBD-04 (permitted Primary-to-Secondary offset).", "WP-P13"),

    # ---- 5.0  RS-485 ------------------------------------------------------
    36:  ("BLOCKED", "Needs TBD-05 (64-channel register map) and OI-07.", "WP-P16"),
    41:  ("BLOCKED", "Needs TBD-06 (permitted data/stop/parity combinations).", "WP-P17"),
    43:  ("BLOCKED", "Needs TBD-07 (function codes).", "WP-P17"),
    44:  ("BLOCKED", "Needs TBD-08 (response timeout).", "WP-P17"),
    45:  ("BLOCKED", "Needs TBD-09 (bus device count).", "WP-P17"),
    46:  ("BLOCKED", "Needs TBD-10 and TBD-11 (slave address range).", "WP-P17"),

    # ---- 6.0  Ethernet ----------------------------------------------------
    49:  ("DONE", "`net/tcp_client.c` - non-blocking connect with timeout, full-send, "
                  "timed recv. Hardware-verified 2026-08-07 (T-6).", "WP-P23"),
    50:  ("DONE", "IPv4 only, by design - `inet_pton` on dotted-quad literals, no "
                  "`getaddrinfo` (ADR-8).", "WP-P23"),
    51:  ("DEFERRED", "Phase 2.", "WP-P24"),
    52:  ("PARTIAL", "The board does obtain a DHCP address, but that is Torizon OS, "
                     "not me-primary. No DHCP control or reporting in the application.",
          "WP-P24"),
    53:  ("DONE", "`platform/netinfo.c` `me_netinfo_read()` reads the real MAC via "
                  "`SIOCGIFHWADDR`; MAC `00:14:2d:ef:86:e2` observed on hardware.",
          "WP-P24"),
    54:  ("PARTIAL", "The MAC is read and placed in the 33-byte `0xDD` registration "
                     "payload (`proto/reg_frame.c`), so the Battery Manager receives "
                     "it once. There is no query that lets it re-read it on demand.",
          "WP-P24"),
    55:  ("TODO", "`0xDD` Q4 IP configuration is unimplemented (T-10).", "WP-P24"),
    56:  ("TODO", "`0xDD` Q4 IP configuration is unimplemented (T-10).", "WP-P24"),
    57:  ("PARTIAL", "Programming (`0xBB` Q4) and live parameters (`0xCC` on UDP "
                     "10000) work. Configuration is limited to the `0xAA` Q5 battery "
                     "record - Q1-Q4 and Q6 are specified but unanswered. Calibration "
                     "(`0xA0`) is entirely unimplemented (T-12).", "WP-P23"),
    58:  ("PARTIAL", "One link does carry everything, but only 4 channels of one "
                     "Secondary have ever been exercised (T-62). No per-channel "
                     "fairness or starvation guarantee exists.", "WP-P27"),
    59:  ("BLOCKED", "Needs TBD-12 (session count) and OI-08 (multi-operator rule).",
          "WP-P23"),
    60:  ("PARTIAL", "Structurally true - `core_logic.c` ticks every engine "
                     "independently of the socket, and `comm_thread.c` reconnects with "
                     "backoff without touching engine state. Never tested by pulling "
                     "the link mid-test.", "WP-P23"),
    61:  ("BLOCKED", "Needs TBD-13 (buffering duration).", "WP-P23"),

    # ---- 7.0  User-configurable CAN ---------------------------------------
    65:  ("BLOCKED", "Needs TBD-14 (which port is the internal bus) and OI-09.",
          "WP-P18"),
    95:  ("DEFERRED", "Phase 2.", "WP-P18"),
    117: ("DEFERRED", "Phase 2.", "WP-P18"),

    # ---- 8.0  Analog inputs -----------------------------------------------
    140: ("BLOCKED", "Needs OI-12 (achievable aggregate sampling rate) and TBD-22.",
          "WP-B13"),
    143: ("BLOCKED", "Needs TBD-15 (the 'near 0 V' threshold).", "WP-P19"),
    145: ("BLOCKED", "Needs TBD-16 and OI-13 (there is no calibration procedure to "
                     "implement - BTS 8.1.5 was empty).", "WP-P20"),
    162: ("BLOCKED", "Needs OI-14 (how calibration binds to a board identity).",
          "WP-P20"),
    163: ("BLOCKED", "Needs OI-15 (persistence medium and format).", "WP-P07"),
    166: ("PARTIAL", "`proto/can_frame.c` `me_can_pack_set()` already commands "
                     "current as an engineering value - a little-endian IEEE-754 float "
                     "in amperes - and never a raw DAC code. Voltage is not commanded "
                     "at all: `step_decode.h` has no voltage field for CCChg (T-66).",
          "WP-P29"),

    # ---- 10.0  Programming -------------------------------------------------
    168: ("PARTIAL", "`core_logic.c` holds `s_exec[64]`, one `me_exec_ctx_t` per "
                     "circuit, each with its own `step_index`, `step_run_ms`, "
                     "`program_run_ms` and retry counter. Cycle counters do not exist "
                     "yet, and only 4 circuits can be admitted today.", "WP-P02"),
    169: ("PARTIAL", "Independent by construction - `service_engines()` ticks each "
                     "context separately and they share no mutable state. Never "
                     "demonstrated with more than 4 channels.", "WP-P27"),
    174: ("PARTIAL", "3 of the 13 listed operators are decoded and executed: SET "
                     "(`0x0A`), CCChg (`0x01`, the constant-current charge case of "
                     "CHA) and STOP (`0x0B`) - `proto/step_decode.h` "
                     "`me_step_operator_t`. PAU, DCH, INT, BEG, CYC, GOTO, REG, ERR, "
                     "MSG and TABLE are all rejected with "
                     "`ME_STEP_DECODE_UNSUPPORTED_OPERATOR`.", "WP-P02"),
    175: ("DEFERRED", "Phase 2.", "WP-P02"),
    177: ("PARTIAL", "SET is decoded - `step_decode.c` extracts the 13-bit "
                     "`registration_type` mask - and `step_engine.c` emits its "
                     "SET_VALUES CAN frame. The mask itself is not acted on, and "
                     "OI-17 means nobody has stated what SET should do.", "WP-P02"),
    201: ("PARTIAL", "`me_exec_force_stop()` sends a `CMD_STO` SET_VALUES frame and "
                     "moves that one circuit to `ME_EXEC_STOPPED`; other circuits are "
                     "untouched. Sub-programs (REQ_203) do not exist.", "WP-P02"),
    187: ("PARTIAL", "CCChg implements the constant-current charge case only: nominal "
                     "current in amperes, a single TIME cut-off, no Action, and the "
                     "Registration parameter list decoded but not acted on "
                     "(`step_decode.h` comment: 'Decoded fully; not yet acted on by "
                     "step_engine').", "WP-P03"),
    189: ("PARTIAL", "Constant-current charge to a limit works, but only a TIME limit "
                     "- Voltage, Ah and Temperature limits are rejected by "
                     "`ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE`. No Action is executed "
                     "on the crossing; the engine advances to the next step.",
          "WP-P03"),
    191: ("TODO", "No constant-voltage mode. `step_decode.h`'s `me_step_t` has no "
                  "nominal-voltage field.", "WP-P03"),
    194: ("TODO", "No CC-then-CV transition.", "WP-P03"),
    198: ("TODO", "No temperature source binding exists.", "WP-P03"),
    199: ("TODO", "DCH is rejected with `ME_STEP_DECODE_UNSUPPORTED_OPERATOR`.",
          "WP-P03"),
    150: ("TODO", "No Max Charging Current is stored, so no clamp is applied to a "
                  "program's requested current.", "WP-P03"),
    155: ("TODO", "No Max Discharging Current is stored, so no clamp is applied.",
          "WP-P03"),
    216: ("BLOCKED", "Needs OI-22 (is 16-way nesting a Phase 1 commitment at all - it "
                     "was never built in BTS either).", "WP-P02"),
    217: ("BLOCKED", "Needs OI-22.", "WP-P02"),
    222: ("PARTIAL", "`step_engine.c` does advance to the next step when the current "
                     "step's cut-off is met and nothing else is configured - but only "
                     "for a TIME cut-off, and 'Action is blank' is not a decision it "
                     "makes, because no Action is ever decoded.", "WP-P02"),
    225: ("TODO", "`core_logic.c` logs `Continue is not implemented yet` and still "
                  "returns an `0x01` ack to the Battery Manager.", "WP-P22"),

    # ---- 11.0  Program compilation and download ----------------------------
    236: ("PARTIAL", "Each `0xBB` Q4 packet is acked `0x01`/`0x00` "
                     "(`comm_thread.c` `send_ack()`), so a failure is signalled - but "
                     "the ack carries no reason code and names no channel beyond the "
                     "CircuitID it echoes.", "WP-P21"),
    237: ("PARTIAL", "`store/circuit_store.c` reserves `ME_PROGRAM_BUF_SIZE` per slot "
                     "for all 64 slots. The size itself is unagreed (TBD-17) and the "
                     "resulting 261 MB `.bss` has never been checked against the "
                     "container memory limit (T-27).", "WP-P21"),
    239: ("TODO", "A `0xBB` Q1 is-ready query is answered unconditionally "
                  "(`handle_program_handshake()`), and a Q4 packet for a running "
                  "circuit is accepted. Nothing consults execution state.", "WP-P21"),
    240: ("PARTIAL", "`me_chain_is_complete()` is checked before a Start, so an "
                     "unterminated chain will not run (ADR-14), and every `nextIndex` "
                     "is range-checked by the walker. There is no whole-program "
                     "checksum and no comparison against what the Battery Manager "
                     "sent.", "WP-P21"),
    241: ("TODO", "No read-back path. `ME_MSG_REQ_PROGRAM` serves Core Logic "
                  "internally, never the host.", "WP-P21"),

    # ---- 12.0  Topology, addressing, capacity ------------------------------
    242: ("PARTIAL", "`ME_MAX_SECONDARIES` is 8 and all 64 slots exist, but only "
                     "Secondary 1 has ever been addressed (T-62).", "WP-P27"),
    243: ("PARTIAL", "The address space holds 8 channels per board. The CAN block "
                     "selection for channels 5-8 is explicitly deferred (ADR-28) - "
                     "`me_can_block_for_channel()` maps 1-4 to Block 1 and 5-8 to "
                     "Block 2, but only Block 1 has been exercised.", "WP-P27"),
    244: ("PARTIAL", "Program, battery record, raw config and an execution context "
                     "exist per slot for all 64. Calibration data is not held at all.",
          "WP-P27"),
    245: ("DONE", "`proto/proto_defs.h` `ME_CIRCUIT_ID(sec, ch)`, 1-based nibbles, "
                  "`0x11`-`0x88`. `store/circuit_store.c` `me_circuit_slot()` maps it "
                  "to 0-63. Covered by `tests/test_circuit_store.c`.", "WP-P27"),
    246: ("DONE", "`me_circuit_slot()` returns `ME_SLOT_INVALID` and never folds to "
                  "slot 0 (ADR-13). `0x00`, `0x01`, `0x10`, `0x09`, `0x90`, `0xFF` "
                  "are all asserted as rejected in `tests/test_circuit_store.c`, plus "
                  "a nibble-swap and row-boundary aliasing sweep in "
                  "`tests/test_circuit_registry.c`.", "WP-P27"),
    247: ("BLOCKED", "Needs TBD-18 / OI-24. The encoding admits 15x15; storage and "
                     "validation cap at 8x8. Two ME documents disagree.", "WP-P27"),
    248: ("PARTIAL", "Every frame and most log lines carry the CircuitID. Test "
                     "records and error reports do not exist yet, so they cannot "
                     "carry it.", "WP-P27"),
    249: ("PARTIAL", "The system does run with one Secondary and 4 channels. It works "
                     "because the population is passed on the command line "
                     "(`--channels`), not because it was discovered.", "WP-P01"),

    # ---- 13.0  Secondary enrolment and supervision -------------------------
    251: ("BLOCKED", "Needs OI-26. This is the largest single gap between the code as "
                     "built and the SRS: circuits are admitted only by the host-side "
                     "`0xDD` handshake in `comm_thread.c`, so the board learns its "
                     "population from `--channels` and never from the bus.", "WP-P01"),
    253: ("PARTIAL", "`store/circuit_registry.c` `me_registry_is_registered()` is "
                     "consulted in `route_frame()` before any frame reaches a queue "
                     "(ADR-17), so an unadmitted circuit is never commanded. But "
                     "'registered with the host' is not 'enrolled on the bus' - the "
                     "check does not know whether a Secondary is physically there.",
          "WP-P01"),
    255: ("BLOCKED", "Needs TBD-19 (loss declaration time).", "WP-P01"),
    257: ("BLOCKED", "Needs OI-25 (is hot-swap a supported field operation).", "WP-P01"),
    260: ("BLOCKED", "Needs OI-26. Partially advanced by ADR-32 - Secondary 1 now "
                     "registers channels 1-4 independently over one TCP connection - "
                     "but that is host-side registration, not bus enrolment.",
          "WP-P01"),

    # ---- 14.0  Internal CAN bus --------------------------------------------
    261: ("DONE", "A single shared path to all Secondaries: `threads/can_mgr.c` over "
                  "`platform/rpmsg_link.c` (`/dev/ttyRPMSG30`) to the M7, which owns "
                  "the physical CAN-FD controller (ADR-29). Partially "
                  "hardware-verified 2026-08-19.", "WP-P25"),
    262: ("DONE", "CAN FD - 64-byte data frames, `ME_CAN_FRAME_LEN` in "
                  "`proto/can_frame.h`, DLC table asserted in "
                  "`tests/test_rpmsg_frame.c`.", "WP-P25"),
    263: ("BLOCKED", "Needs TBD-20 and TBD-21. No bit rate is recorded anywhere; the "
                     "M7 firmware sets it and nothing on the A53 states it.", "WP-P25"),
    264: ("BLOCKED", "Needs TBD-22 and OI-12 (the load budget does not exist).",
          "WP-P25"),
    265: ("TODO", "CAN identifiers come from `me_can_block_for_channel()` by block, "
                  "not by priority class. No arbitration analysis has been done.",
          "WP-P25"),
    266: ("PARTIAL", "The round trip IS measured - `can_mgr.c` stamps `s_tx_us[]` and "
                     "logs RPMsg RTT per Secondary in microseconds (ADR-31), which is "
                     "exactly the instrument this requirement needs. There is no "
                     "budget to compare it against (TBD-23) and no worst-case run.",
          "WP-P25"),
    267: ("BLOCKED", "Needs TBD-24. `step_engine.c` does count missed responses and "
                     "goes `ME_EXEC_CHANNEL_OFFLINE` after "
                     "`ME_EXEC_RETRY_LIMIT` (3) - a 3-strike rule at a 200 ms "
                     "response timeout - but that is a lost-response rule, not a "
                     "stale-data rule, and the boundary is unspecified.", "WP-P25"),
    268: ("TODO", "No bus-off detection. The A53 never sees the CAN controller; the "
                  "M7 owns it and reports nothing about its state.", "WP-P25"),
    269: ("PARTIAL", "`step_engine.c` counts missed responses per circuit and "
                     "`can_mgr.c` logs discarded echo frames (ADR-38). There are no "
                     "per-Secondary error-frame or retransmission counters, and "
                     "nothing is reported to the Battery Manager.", "WP-P25"),
    270: ("DONE", "True by construction - no host frame reaches `can_mgr.c`'s "
                  "transport configuration, and the bit rate lives in M7 firmware "
                  "the host cannot address.", "WP-P25"),

    # ---- 15.0  Host interface ----------------------------------------------
    271: ("DONE", "`comm_thread.c` is the TCP client and initiates the connection. "
                  "Hardware-verified 2026-08-07 (T-6) and again 2026-08-10 (T-21).",
          "WP-P23"),
    272: ("DONE", "Port 9999, `ME_TCP_PORT` in `proto/proto_defs.h`. "
                  "Hardware-verified.", "WP-P23"),
    273: ("DONE", "`comm_thread.c` - `ME_BACKOFF_MIN_MS` 1000, `ME_BACKOFF_MAX_MS` "
                  "30000, doubling on each failure and reset to the minimum on a "
                  "healthy connection. The process never exits on a link failure. "
                  "Exactly as specified.", "WP-P23"),
    274: ("DONE", "UDP 10000, `net/udp_sock.c` + `sys_init.c` `udp_live_dest`. The "
                  "86-byte `0xCC` frame is sent there by `drain_outbound()`.",
          "WP-P06"),
    275: ("PARTIAL", "The whole path exists - `sys_init.c` opens the socket and builds "
                     "`udp_session_dest`, `data_mgr.c` holds a 64-deep session ring "
                     "and `ring_flush()` forwards to `g_q_comm` - but nothing "
                     "produces a test record, so UDP 10001 carries no traffic (T-29).",
          "WP-P05"),
    276: ("DONE", "`proto/crc16.c` - CRC-16/Modbus, poly `0xA001`, init `0xFFFF`, no "
                  "final inversion. Reference vector `\"123456789\" -> 0x4B37` "
                  "asserted in `tests/test_crc16.c`.", "WP-P23"),
    277: ("PARTIAL", "Big-endian is implemented and hardware-verified in BOTH "
                     "directions (ADR-9: request CRC `0x369E` -> `36 9E`, response "
                     "`0xBE15` -> `BE 15`), and `ME_CRC_ORDER_DEFAULT` in `crc16.h` "
                     "is the single source of truth. TBD-25 / OI-28 are still "
                     "formally open because a second ME document records a "
                     "little-endian capture - the code is settled, the specification "
                     "is not.", "WP-P23"),
    278: ("DONE", "`comm_thread.c` `consume_rx_buffer()` plus "
                  "`proto/frame_router.c` `me_frame_resolve_len()` (ADR-19). Both the "
                  "coalesced-read and the split-frame cases are asserted in "
                  "`tests/test_frame_router.c`, including two named truncation "
                  "guards.", "WP-P23"),
    279: ("DONE", "`comm_thread.c` `frame_crc_ok()` logs computed-vs-carried in both "
                  "byte orders and returns; the connection is never closed on a bad "
                  "CRC.", "WP-P23"),
    280: ("PARTIAL", "An unrecognised command group is logged and dropped, which "
                     "satisfies the first half. The second half is violated today: a "
                     "well-formed `0xEE` frame carrying a QueryID outside the six "
                     "documented is acked `0x01` in `route_frame()` before "
                     "`me_control_parse()` rejects it on the Core Logic thread, which "
                     "has no path back to the socket (T-45).", "WP-P23"),
    281: ("TODO", "Every ack means 'queued to the owning thread', never 'executed' - "
                  "stated explicitly in the implementation reference. Closing this is "
                  "the same seam as T-45: Core Logic needs an outbound message type "
                  "through `g_q_comm`.", "WP-P23"),
    282: ("DEFERRED", "Phase 2. UDP 10002/10003 discovery is documented in "
                      "`bm_device_registration_v5.0.md` but unimplemented (T-10).",
          "WP-P24"),

    # ---- 16.0  Live data and test records ----------------------------------
    283: ("PARTIAL", "The 86-byte `0xCC` frame has a field for nearly all of this and "
                     "`realtime_frame.c` packs it with all 80 payload offsets asserted "
                     "in `tests/test_realtime_frame.c`. What the engine actually "
                     "fills is step number, both statuses, both run times, current, "
                     "voltage and operator code (`me_exec_output_t`). Temperature, "
                     "power, the capacity and energy accumulators, cycle and table "
                     "position, registration type and digital I/O state are sent as "
                     "zero.", "WP-P06"),
    284: ("DONE", "`util/msgq.c` sends with a finite timeout and drops on expiry "
                  "(ADR-12); real-time frames use the control timeout, not the bulk "
                  "one, precisely so a stale frame is dropped rather than queued "
                  "ahead of a fresh one. Drops are counted per queue and logged at "
                  "WARN by `me_queues_report()`.", "WP-P06"),
    285: ("TODO", "No test record exists to guarantee. Depends on WP-P05.", "WP-P05"),
    286: ("BLOCKED", "Needs TBD-26 (permitted registration types and interval range). "
                     "Note the raw material is already decoded: `step_decode.c` "
                     "extracts SET's 13-bit registration mask and CCChg's trailing "
                     "registration-parameter list (types `0x21`-`0x2D`), and neither "
                     "is acted on.", "WP-P05"),
    287: ("DONE", "`realtime_frame.c` `me_put_f32_be()` / `me_get_f32_be()` - IEEE "
                  "754 single precision, big-endian, via `memcpy` through a "
                  "`uint32_t` rather than a type-punning cast. The spec's own worked "
                  "example `95.6f -> 42 BF 33 33` is the golden test.", "WP-P06"),
    288: ("TODO", "The `0xEE` Q1 Session ID IS parsed (`control_frame.c`, "
                  "`has_session_id`) and logged, but deliberately not stored - the "
                  "`0xCC` frame has no field for it and no test record exists to "
                  "carry it.", "WP-P05"),

    # ---- 17.0  Test control commands ---------------------------------------
    289: ("PARTIAL", "All six are parsed and acked (`proto/control_frame.c`, all six "
                     "asserted in `tests/test_control_frame.c`). Only two are "
                     "implemented: Start loads the program and battery record and "
                     "starts the engine; Stop calls `me_exec_force_stop()`. "
                     "`core_logic.c` logs `Pause`, `Continue` and `Reset` as `not "
                     "implemented yet`, and Sync Time as `not applied`.", "WP-P22"),
    290: ("PARTIAL", "Start and Stop act on exactly the named circuit and no other. "
                     "Pause and Continue do not act at all.", "WP-P22"),
    291: ("BLOCKED", "Needs OI-29. Today `route_frame()` gates EVERY `0xEE` and "
                     "`0xAA` frame on its CircuitID, including the system-scoped Sync "
                     "Time and Reset - harmless only because Sync Time is a no-op "
                     "(T-34).", "WP-P22"),
    293: ("TODO", "Start checks only that a program is complete. Configuration, "
                  "calibration and Secondary enrolment are not consulted, because "
                  "none of the three exists as state to consult.", "WP-P22"),
    294: ("BLOCKED", "Needs OI-05 (no source document defines the safe state). Stop "
                     "does send `CMD_STO` and does not wait for confirmation before "
                     "moving to `ME_EXEC_STOPPED`.", "WP-P22"),
    295: ("BLOCKED", "Needs TBD-27 / OI-30. The current behaviour is a deliberate "
                     "restart - `core_logic.c` zeroes the slot and reloads on a fresh "
                     "Start, commented as intentional so the flow can be "
                     "re-demonstrated. That may be the wrong default for a multi-day "
                     "test.", "WP-P22"),
    296: ("BLOCKED", "Needs OI-05.", "WP-P22"),

    # ---- 18.0  Fault containment -------------------------------------------
    297: ("BLOCKED", "Needs OI-11 (the error catalogue does not exist).", "WP-P10"),
    298: ("PARTIAL", "Per-circuit isolation is real - each `me_exec_ctx_t` fails to "
                     "`ME_EXEC_CHANNEL_OFFLINE` or `ME_EXEC_DECODE_ERROR` on its own "
                     "without touching its neighbours. But there is no notion of a "
                     "classified 'channel fault' to inhibit on.", "WP-P10"),
    300: ("BLOCKED", "Needs OI-05.", "WP-P10"),
    301: ("PARTIAL", "True today because failures are per-context and a Start on any "
                     "other circuit is independent. Untested and unclassified.",
          "WP-P10"),
    302: ("BLOCKED", "Needs OI-11.", "WP-P11"),

    # ---- 19.0  Performance and timing budgets -------------------------------
    305: ("PARTIAL", "`core_logic.c` runs a 10 ms loop and `service_engines()` ticks "
                     "every circuit on every pass, so the re-evaluation interval today "
                     "is 10 ms of engine time with a 100 ms CAN poll period "
                     "(`ME_EXEC_POLL_PERIOD_MS`). Whether that satisfies the "
                     "requirement is unknowable until TBD-28 is answered - "
                     "OI-31 calls this the single most architecturally significant "
                     "unknown in the SRS.", "WP-P26"),
    306: ("BLOCKED", "Needs TBD-29. Core Logic IS pinned to CPU3 "
                     "(`pthread_attr_setaffinity_np`, ADR-30) which is the mechanism "
                     "for bounding jitter, but the CPU is not yet kernel-isolated "
                     "(T-60) and jitter has never been measured.", "WP-P26"),
    307: ("BLOCKED", "Needs TBD-30.", "WP-P26"),
    308: ("BLOCKED", "Needs TBD-31.", "WP-P26"),
    309: ("PARTIAL", "The footprint IS bounded and known before start - every buffer "
                     "is static (ADR-11/rule 3) - but `readelf` reports 261 MB of "
                     "`.bss` and it has never been checked against the container "
                     "limit (T-27). TBD-32 gives no budget to check it against.",
          "WP-P26"),
    310: ("DONE", "No `malloc`/`free` anywhere in `src/`. Cross-cutting rule 3, and a "
                  "documented project constraint.", "WP-P26"),
    311: ("BLOCKED", "Needs OI-03 (is a Linux A53 the right host for a 64-channel "
                     "control loop at all) before a WCET analysis has a target to "
                     "analyse.", "WP-P26"),

    # ---- 20.0  Startup, shutdown, persistence ------------------------------
    317: ("TODO", "No version string is reported anywhere, and no Secondary "
                  "software version is known because nothing enrols a Secondary.",
          "WP-P30"),
    318: ("TODO", "No compatibility gate. Needs WP-P01 to have a version to "
                  "refuse on.", "WP-P30"),
    303: ("BLOCKED", "Needs OI-05. Note that deadlock is already impossible by "
                     "construction - every queue send has a finite timeout and "
                     "drops on expiry (ADR-12) - so what is missing is detection "
                     "of a stall that is NOT a deadlock, and the safe-state "
                     "action to take when one is found.", "WP-P31"),
    312: ("TODO", "`main.c` starts all four threads and the comm thread registers "
                  "immediately. No enrolment step and no configuration validation "
                  "gate the first command.", "WP-P09"),
    313: ("BLOCKED", "Needs OI-15. Everything is in volatile memory today - a restart "
                     "loses every program, battery record and raw config.", "WP-P07"),
    316: ("PARTIAL", "`main.c` handles `SIGINT`/`SIGTERM` by setting one stop flag, "
                     "joins the threads in reverse start order and reports queue drop "
                     "counts. It does not bring any channel to a safe state and has "
                     "no records to flush.", "WP-P09"),
    319: ("BLOCKED", "Needs TBD-33.", "WP-P09"),

    # ---- 21.0  Diagnostics and logging -------------------------------------
    320: ("BLOCKED", "Needs OI-11. BTS asked 'how do we indicate this error?' in nine "
                     "places and never answered; nine ME requirements depend on the "
                     "one answer.", "WP-P11"),
    321: ("BLOCKED", "Needs TBD-34. `util/log.c` writes to stderr only - nothing is "
                     "retained across a restart.", "WP-P12"),
    324: ("PARTIAL", "Logging cannot currently delay anything because there is no log "
                     "storage to block on, and the hex dump is the deliberate primary "
                     "field diagnostic. The property is accidental, not designed, and "
                     "there is no logging-failure report.", "WP-P12"),

    # ---- 22.0  Battery Manager application ---------------------------------
    340: ("DEFERRED", "Phase 2.", "WP-B16"),

    # ---- 23.0  Power fail ---------------------------------------------------
    341: ("BLOCKED", "Needs TBD-35 from the hardware architect - the guaranteed "
                     "warning time is a property of the Primary Board's "
                     "power-supervisory circuit, and no software design is possible "
                     "without it.", "WP-P08"),
    342: ("BLOCKED", "Needs OI-15 (medium, on-media format, partial-write handling).",
          "WP-P08"),
    344: ("BLOCKED", "Needs OI-34 - the client has directed automatic resume, and has "
                     "flagged that this may change after a safety discussion. Do not "
                     "build it until that is settled.", "WP-P08"),

    # ---- 24.0  Cut-off condition handling -----------------------------------
    346: ("PARTIAL", "1 of the 13 types is implemented. `step_decode.c` accepts only "
                     "TIME (`0x39`) and returns "
                     "`ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE` for Current, Voltage, "
                     "Power, both Capacities, both Energies, Temperature, Accumulated "
                     "Capacity, Step Capacity, Accumulated Energy and Step Energy.",
          "WP-P04"),
    347: ("BLOCKED", "Needs TBD-36. Today a wire count of 2-15 is rejected outright "
                     "with `ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED`, and a count "
                     "above 15 with `ME_STEP_DECODE_TOO_MANY_CUTOFFS` - deliberately "
                     "refusing rather than folding to 0, because the old firmware "
                     "produced a step that never ends that way.", "WP-P04"),
    348: ("PARTIAL", "2 of the 6 comparators: `>` (`0x51`) and `>=` (`0x53`), carried "
                     "as `cutoff_inclusive` in `me_step_t`. Less-than, "
                     "less-than-or-equal, not-equal and equal-to all return "
                     "`ME_STEP_DECODE_UNSUPPORTED_COMPARATOR`.", "WP-P04"),
    349: ("BLOCKED", "Needs TBD-37. There is no debounce at all - `step_engine.c` "
                     "acts on the first evaluation in which the comparison is true.",
          "WP-P04"),
    350: ("PARTIAL", "The engine ends the step and advances. None of GOTO, ERR or MSG "
                     "exists, and no action type is decoded from the step body "
                     "(`ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE` guards the six "
                     "documented values but nothing consumes them).", "WP-P04"),
    351: ("DONE", "The Primary evaluates every cut-off in `exec/step_engine.c` from "
                  "the feedback the Secondary reports (`me_can_feedback_t` -> "
                  "`me_exec_on_response()`); the Secondary evaluates nothing. Proven "
                  "on the host in `tests/test_step_engine.c`, which asserts a cutoff "
                  "firing at the exact millisecond.", "WP-P04"),
    352: ("BLOCKED", "Needs the 'Need to check' status in the SRS to be resolved, and "
                     "is moot until REQ_347 allows more than one condition.",
          "WP-P04"),
    353: ("BLOCKED", "Needs TBD-35/TBD-37 and OI-15 - it is a property of the "
                     "power-fail snapshot that does not exist.", "WP-P08"),
}


# ------------------------------------------------------------------ BM map ---
# The 51 requirements whose Applicability is `BM` place no obligation on
# me-primary, but they are still somebody's work - the one Battery Manager
# engineer's. Each is routed to the BM work package that will build it, so the
# web-application estimate is traceable to requirements the same way the
# Primary estimate is. Verified complete against the SRS: exactly these 51.
BM_MAP = {
    # digital I/O enable/disable, analog limits and rates, calibration screens
    6:   "WP-B07", 16:  "WP-B07",
    132: "WP-B07", 133: "WP-B07", 136: "WP-B07", 137: "WP-B07",
    146: "WP-B07", 151: "WP-B07", 156: "WP-B07", 338: "WP-B07",
    # RS-485 publication selection
    37:  "WP-B10",
    # CAN message and signal configuration
    96:  "WP-B09", 97:  "WP-B09", 98:  "WP-B09", 100: "WP-B09",
    106: "WP-B09", 107: "WP-B09", 108: "WP-B09",
    121: "WP-B09", 123: "WP-B09", 125: "WP-B09",
    # Program authoring: the step/operator data rules the editor must enforce
    170: "WP-B02", 171: "WP-B02", 172: "WP-B02",
    179: "WP-B02", 180: "WP-B02", 183: "WP-B02",
    188: "WP-B02", 190: "WP-B02", 192: "WP-B02",
    202: "WP-B02", 205: "WP-B02",
    233: "WP-B02", 234: "WP-B02", 235: "WP-B02", 328: "WP-B02",
    # Program library, reuse, assignment tracking
    238: "WP-B03", 329: "WP-B03", 330: "WP-B03",
    # Views
    326: "WP-B01", 327: "WP-B01", 339: "WP-B01",
    # Group operations and confirmation
    331: "WP-B04", 332: "WP-B04",
    # Trends and scope-aware alarms
    333: "WP-B05", 337: "WP-B05",
    # Test records
    334: "WP-B06", 335: "WP-B06", 336: "WP-B06",
    # Enrolment/version presentation
    250: "WP-B15",
    # Phase 2
    340: "WP-B16",
}


def verdict_for(rid_num):
    """Return (verdict, evidence, wp) for one numeric requirement id."""
    if rid_num in SPECIFIC:
        return SPECIFIC[rid_num]
    for first, last, verdict, evidence, wp in RANGES:
        if first <= rid_num <= last:
            return (verdict, evidence, wp)
    raise KeyError("no verdict defined for ME_SW_REQ_%d" % rid_num)


def bm_package_for(rid_num):
    """Owning Battery Manager work package for a BM-scope requirement."""
    return BM_MAP.get(rid_num, "WP-B01")
