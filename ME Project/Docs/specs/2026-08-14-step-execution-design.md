# ME Primary — Battery Testing Step Execution (Design)

| Field | Value |
|---|---|
| Status | Draft — awaiting developer review |
| Scope | First 3-step battery testing program on iMX8MP: `SET`, `CCChg`, `STOP` |
| Threads touched | Core Logic, CAN Data Manager |
| Date | 2026-08-14 |

## 0. Summary

This is the first real implementation of `me_execute_program()` in `core_logic.c` —
the seam the 4-thread base was built around (`Docs/ME_Primary_BTS_Block_Diagram.md`
§6.6). Today that function arms a synthetic 1 Hz current ramp
(`demo_realtime.c`) so the message-queue path could be proven end to end
before real execution existed. This design replaces the ramp with a real
per-circuit step engine that decodes a program step, drives a simulated
DC-DC channel over CAN-FD, evaluates a time cutoff, and reports real values
in the existing `0xCC` frame.

**Scope is deliberately narrow**, per the developer's brief and the
clarifying answers below: three operators (`SET`, `CCChg`, `STOP`), one
cutoff type (time), one circuit (Secondary 1 / Channel 1), and no physical
CAN device — the CAN Data Manager fabricates feedback instead of receiving
it from hardware. Everything else the BTS-600 program language defines
(§A1 of `A1_Operator_Reference.md`) is out of scope for this iteration and
is not designed against here.

## 1. What changed from the original brief, and why

Two corrections were made during design, both confirmed against source
documents rather than assumed:

1. **There is no separate "read query" frame distinct from the protocol's
   own `READ_VALUES` function.** The CAN-FD spec (`Ref Docs/master_slave_can_v1.0.md`)
   defines exactly two Master-issued functions on this bus: `SET_VALUES
   (0x01)` and `READ_VALUES (0x02)`. Core Logic's polling *is* a
   `READ_VALUES` frame; there is no third command type to invent.

2. **Step decoding is new work for the Primary, not a port.** In the old
   BTS system the Primary never decoded step bodies — `networkDataHandler.c`
   only read the operator byte (to build a cycle table) and forwarded whole
   step packets to the Secondary over UART, which owned all decoding
   (`stepData.c`). In ME the Secondary is a dumb DC-DC board taking
   setpoints over CAN-FD; decoding therefore moves into Core Logic. The old
   Secondary code is the correct reference for step-execution *semantics*
   (byte layout, cutoff evaluation, SET/registration handling) but none of
   its transport or its "forward and let the other board decide" shape
   survives.

## 2. Protocol facts this design depends on

All of the following were extracted from source, not assumed. Two were
corrected mid-design after the developer challenged the first reading of
the CAN spec — both corrections are recorded with their evidence in
`Ref Docs/master_slave_can_v1.0.md`, and are treated as authoritative here.

### 2.1 CAN-FD Master↔Secondary exchange

Source: `Ref Docs/master_slave_can_v1.0.md` (transcribed from
`Master-Slave_CAN_V1.0.xlsx`).

- 11-bit standard identifier: `CAN_ID = (circuit6 << 5) | function5`.
- Three functions: `SET_VALUES 0x01`, `READ_VALUES 0x02`, `BROADCAST 0x03`.
- **Both `SET_VALUES` and `READ_VALUES` are Master-issued**, and use the
  **identical 64-byte payload shape**; the Secondary's action is selected
  by the function code in the identifier, not by anything in the payload.
- **The Secondary answers every Master frame**, `SET_VALUES` included —
  confirmed by the developer 2026-08-14. One request/response/retry rule
  covers both function codes.
- **The Secondary latches the setpoint** — confirmed by the developer
  2026-08-14. No keep-alive re-send is required; `SET_VALUES` is sent once
  per step entry (and once at STOP), not periodically.
- Per-channel 16-byte slot (4 channels per 64-byte frame):

  | Offset | Master (`SET_VALUES`/`READ_VALUES` request) | Slave (response) |
  |---|---|---|
  | +0..+3 | Set Voltage (float32) | Feedback Voltage (float32) |
  | +4..+7 | Set Current (float32) | Feedback Current (float32) |
  | +8 | Command | STATE |
  | +9 | Channel # | Channel # |
  | +10 | EEP Para # | INT Para # |
  | +11 | Reserved | Reserved |
  | +12..+15 | Data (float32) | Data (float32) |

- **Floats are IEEE-754 binary32, byte order Intel little-endian** — the
  opposite of every WebApp-facing frame in this codebase.
- Command byte values used here: `CMD_CHA = 0x01` (charge), `CMD_STO =
  0x00` (stop — an *active* stop, not "no change"). `CMD_RST_ERR (0x04
  outbound)` / `CMD_ERR (0x04 inbound)` share a numeric value with opposite
  meaning — kept as two separate enums in code, never one.
- One circuit only in this iteration (Secondary 1, Channel 1 → slot 0 of
  frame block 1). Slots for channels 2–4 are zero-filled — accepted for
  this iteration per the developer's decision, see trap §5.2 and open
  item 3.

### 2.2 Program step packet (WebApp → Primary, inside the existing chain format)

Source: `D:\Projects\BTS_VS_CODE\BTS_SEC_FW_V201\Core\Src\stepData.c` /
`Core\Inc\stepData.h` — the only place this was ever decoded historically.
**This supersedes `Program Packet V0.7.xlsx`, which is stale in six
documented ways** (nominal values as scaled milliamp integers instead of
float32 being the most dangerous). See `[[bts-step-format-authority]]`.

**Independently confirmed 2026-08-14** against `Program Packet
V0.12.xlsx` (`Ref Docs/program_packet_v0.12.md`), a newer WebApp-side spec:
all 19 operator codes, all 6 comparator codes, and all 13 registration-type
codes match the firmware exactly; the 2-byte width of `SET`/`REG`'s
registration selector and `GOTO`'s destination, and the big-endian float32
encoding of nominal/limit values, are confirmed against real encoded sample
bytes in that document. This design's operator and field-width claims below
now rest on two independent, agreeing sources rather than one.

The existing chain wrapper (`me_chain_fetch_step()`, unchanged) gives Core
Logic a pointer to one step's bytes:

```
0-1    AA 55                  (chain start, already stripped by the walker)
2-5    nextIndex   u32 BE     (already consumed by the walker)
6-7    stepNumber  u16 BE
8      operator    u8
9..    <operator-dependent body, decoded by THIS design>
last-2 55 AA
```

All multi-byte fields inside the step body are **big-endian** — consistent
with the rest of the WebApp-facing wire protocol, and the opposite of the
CAN-FD payload above. This asymmetry is real and must survive into the
code as two distinct pack/unpack functions, never one shared by mistake.

**`SET` (`0x0A` — confirmed against two independent sources, open item 1):**

```
9       (1 byte, skipped — "No. of Global Limit Parameters", not read)
10-11   registrationType   u16 BE, masked to 13 bits (& 0x1FFF)
```
No cutoff-count byte, no registration-parameter block. The step is
zero-duration: it applies immediately and Core Logic advances to the next
step without waiting on anything.

**`CCChg` (`0x01`):**

```
9-12    nominalValue1 (current, A)   f32 BE
13      numCutoffConditions          u8   (max 15; >15 is a decode error, not silently 0)
        per condition, repeated numCutoffConditions times:
          +0  conditionValue u8      0x39 = TIME (the only type this iteration decodes)
          +1  logic          u8      0x51 '>' | 0x53 '>=' (only these two accepted — see trap §5.4)
          +2  limitValue     u32 BE  MILLISECONDS (proven by firmware behaviour, not by
                                      the source spreadsheet's inconsistent "10.5 sec" label —
                                      see §2.3)
          +6  actionType     u8      0x00 BLANK is the only value this iteration acts on
          +7.. actionTypeValue        0 bytes when actionType is BLANK (GOTO/ERR/MSG widths
                                      are decoded defensively but not exercised this iteration)
        +0  numRegParams               u8   (max 15; ALWAYS PRESENT, even when 0 — see below)
        per param, repeated numRegParams times:
          +0  registrationType u8      0x21..0x2D (decoded and stored on me_step_t;
                                      NOT acted on this iteration — see open item 6)
          +1  value            4 bytes u32 BE ms if type==TIME(0x21), else f32 BE
```
**Corrected 2026-08-15 — the registration-parameter block IS present on
`CCChg`, and this design had it backwards.** Confirmed two ways, per the
developer's request: (a) `stepData.c`'s `default:` case — the branch every
charge/discharge operator including `CC_Chg` falls into — reads
`numCutoffConditions`, walks the cutoff loop, then reads
`numRegistrationParams` immediately after; (b) `Program Packet
V0.12.xlsx` step 2 (`CC_Chg`) encodes this byte explicitly as `0x00` at
its own packet's offset 21 — proving the byte is emitted even when there
are zero parameters, not omitted when there's nothing to say. Steps 3
(`CV_Chg`), 4 (`PAU`), 7 (`CC_DChg`) all carry the same byte with a
nonzero count. The two-group rule is exact, not an inconsistency:
**`CC_Chg`/`CV_Chg`/`CP_Chg`/`CCCV_Chg`/`CC_DChg`/`CP_DChg`/`CCCV_DChg`/
`CV_DChg`, `PAU`, and `TABLE` always carry this byte; `SET`, `STO`,
`GOTO`, `REG`, `CYC`, `BEG`, `INT`, `ERR`, `MSG` never do.** `step_decode`
therefore always reads this byte for `CCChg`, per the corrected `me_step_t`
in §3.1.

**`STO` (`0x0B`):** no body at all. **11-byte packet total**
(`AA 55 | nextIndex(4) | stepNo(2) | 0x0B(1) | 55 AA(2)` = 2+4+2+1+2 = 11).
Confirmed exactly by `Program Packet V0.12.xlsx` step 6's own byte sequence
(`AA 55 00 00 00 A4 00 06 0B 55 AA`, 11 bytes). The design's first draft
said 13, an arithmetic slip with no effect on the implementation — the
existing chain walker (`me_chain_fetch_step()`) determines step boundaries
from `nextIndex`, never from a byte count `step_decode` computes.

### 2.3 The TIME cutoff unit is milliseconds

Settled by tracing the old firmware's *behaviour*, not its documentation.
`timeCutoffConditionCheck()` compares `limitValue` directly against
`appData.appStepTimeCounter`, a counter incremented once per 1 ms timer
tick. The wire value is therefore a big-endian `uint32` count of
milliseconds. (`ps_program_v1.6.md`'s worked example labels the same
encoding "10.5 sec" for what is unambiguously 500 — that document's label
is simply wrong; the firmware, not the spreadsheet, is authoritative here.)

## 3. Module structure

```
NEW — pure logic, host-testable via build-native.ps1, no platform headers
  src/proto/step_decode.{c,h}    step packet bytes  ->  me_step_t
  src/proto/can_frame.{c,h}      me_setpoint_t / me_feedback_t <-> 64-byte CAN-FD frame
  src/exec/step_engine.{c,h}     per-circuit state machine; clock passed in as a parameter

MODIFIED — Linux-only, proven on hardware
  src/threads/core_logic.c       me_execute_program() gets a real body; ticks the engine
  src/threads/can_mgr.c          decodes SET_VALUES/READ_VALUES, replies with fabricated feedback

REMOVED
  src/threads/demo_realtime.c/.h — the 1 Hz ramp emitter (superseded by real execution)

KEPT, RELOCATED
  me_demo_send_post_registration() and me_realtime_pack_post_registration()
    move out of demo_realtime.* into their own small pair of files. They serve the
    registration flow (one frame right after a successful 0xDD registration), not
    step execution, and deleting them would silently remove a frame the Web
    Application currently receives after connect.
```

No change to `msg.h`. `ME_MSG_CAN_TX` / `ME_MSG_CAN_DATA` already exist and
already carry a `uint32_t offset` field unused for CAN traffic; this design
repurposes it to carry the 11-bit CAN identifier. The CAN Manager derives
the function code from `offset & 0x1F` — the same thing a real SocketCAN
handler will do when hardware arrives, so the shape survives that
transition rather than needing to be redesigned for it.

### 3.1 `step_decode`

```c
typedef enum {
    ME_OP_SET   = 0x0A,
    ME_OP_CCCHG = 0x01,
    ME_OP_STOP  = 0x0B,
    /* other BTS-600 operators are recognised as "unsupported" and rejected
     * with a named error, never silently misparsed */
} me_step_operator_t;

#define ME_STEP_MAX_REG_PARAMS 15u   /* mirrors the cutoff cap; same wire limit */

typedef struct {
    uint8_t registration_type;   /* 0x21..0x2D */
    float   value;               /* engineering units; for TIME (0x21) this is the
                                   * decoded millisecond count cast to float, matching
                                   * the firmware's own (float)(raw_u32 * 1.0f) cast */
} me_step_reg_param_t;

typedef struct {
    me_step_operator_t operator;
    uint16_t step_number;
    /* SET */
    uint16_t registration_type;      /* 13-bit mask */
    /* CCChg */
    float    nominal_current_a;
    uint32_t cutoff_time_ms;         /* 0 = no time cutoff decoded */
    bool     has_cutoff;
    /* CCChg — always decoded, per §2.2's correction; count may legitimately be 0 */
    uint8_t  num_reg_params;
    me_step_reg_param_t reg_params[ME_STEP_MAX_REG_PARAMS];
} me_step_t;

typedef enum {
    ME_STEP_DECODE_OK = 0,
    ME_STEP_DECODE_UNSUPPORTED_OPERATOR,
    ME_STEP_DECODE_TOO_MANY_CUTOFFS,      /* > 15 */
    ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE,
    ME_STEP_DECODE_UNSUPPORTED_COMPARATOR, /* anything but > / >= on TIME */
    ME_STEP_DECODE_TOO_MANY_REG_PARAMS,   /* > 15 */
    ME_STEP_DECODE_TRUNCATED,
} me_step_decode_result_t;

me_step_decode_result_t me_step_decode(const uint8_t *step, uint32_t step_len,
                                       me_step_t *out);
```

Every rejection path is a named, logged result — never a value that looks
plausible but isn't. A step this design doesn't understand must fail
loudly, not be half-parsed.

### 3.2 `can_frame`

```c
typedef struct {
    uint8_t  circuit_id;      /* ME CircuitID: upper nibble secondary, lower nibble channel */
    uint8_t  command;         /* ME_CAN_CMD_CHA, ME_CAN_CMD_STO, ... */
    float    set_voltage;
    float    set_current;
} me_can_setpoint_t;

typedef struct {
    uint8_t  state;           /* ME_CAN_STATE_* */
    float    feedback_voltage;
    float    feedback_current;
} me_can_feedback_t;

typedef enum { ME_CAN_BLOCK_1 = 1, ME_CAN_BLOCK_2 = 2 } me_can_block_t;

/* Maps a 1-based channel number (1..8) to which 64-byte frame it belongs to
 * and which 16-byte slot within that frame (0..3). Channels 1-4 -> Block 1;
 * 5-8 -> Block 2. Per the developer's decision (2026-08-14): Core Logic
 * calls this per active circuit and sends a block's frame only when that
 * block has at least one active circuit — it never sends a block with
 * nothing to say. This is forward-compatible plumbing for channels 5-8;
 * this iteration only ever has channel 1, so only Block 1 is ever sent. */
me_can_block_t me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out);

uint16_t me_can_id(uint8_t circuit_num6, uint8_t function5);
size_t   me_can_pack_set (const me_can_setpoint_t *sp, uint8_t *out64);
size_t   me_can_pack_read(uint8_t circuit_id, uint8_t *out64);
bool     me_can_parse_feedback(const uint8_t *frame64, me_can_feedback_t *out);
```

Only the slot for the active channel is populated; every other slot in
the 64-byte frame is left at zero. Per the developer's decision
(2026-08-14, open item 3), this is accepted for this iteration precisely
because no other channel is on the bus to receive the resulting `CMD_STO`
— see trap §5.2.

### 3.3 `step_engine`

```c
typedef enum {
    ME_EXEC_IDLE, ME_EXEC_RUNNING, ME_EXEC_STOPPED, ME_EXEC_CHANNEL_OFFLINE
} me_exec_state_t;

typedef struct {
    me_exec_state_t state;
    me_step_t       current_step;
    uint32_t        step_entry_ms;     /* engine clock value at step entry */
    uint32_t        step_run_ms;       /* frozen unless RUNNING */
    uint32_t        program_run_ms;
    uint32_t        next_poll_ms;      /* when to send the next READ_VALUES */
    uint8_t         missed_responses;  /* 0..3, for the last frame sent (SET or READ);
                                        * 3 -> ME_EXEC_CHANNEL_OFFLINE. One counter, because
                                        * §2.1 requires a response to every Master frame. */
    float           last_feedback_current;
    float           last_feedback_voltage;
} me_exec_ctx_t;

/* now_ms is a PARAMETER, never read from the system clock inside this module.
 * This is what makes a 10-second cutoff provable in microseconds of host
 * test time instead of discovered on the board. */
void me_exec_start   (me_exec_ctx_t *ctx, const uint8_t *program, uint32_t program_len,
                      uint32_t now_ms);
void me_exec_tick    (me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out);
/* Called for the response to EITHER a SET_VALUES or a READ_VALUES frame -
 * per §2.1 the Secondary answers both, with the same feedback shape. */
void me_exec_on_response(me_exec_ctx_t *ctx, const me_can_feedback_t *fb, uint32_t now_ms);
/* Called when the outstanding SET_VALUES or READ_VALUES got no response
 * within the retry window. Three consecutive misses -> ME_EXEC_CHANNEL_OFFLINE. */
void me_exec_on_response_timeout(me_exec_ctx_t *ctx, uint32_t now_ms);
```

`me_exec_output_t` is a small tagged union the caller (Core Logic) drains
after every tick: "send this CAN setpoint frame", "send this CAN read
frame", "emit this 0xCC frame", or "nothing to do". Keeping engine output
as data rather than having the engine call queue-send functions directly
is what keeps this module free of platform headers and host-testable.

## 4. Timing

One 10 ms engine tick per circuit — a single clock, with counters inside
it, rather than three independent timers that could drift apart:

| Every | What happens | Why this rate |
|---|---|---|
| 10 ms | tick: time-cutoff check, step/program run-time counters | matches `T_TIME_RES = 10 ms`, `ME_Primary_Software_Architecture.md` §8 |
| 100 ms (10th tick) | send `READ_VALUES` for the active channel | developer's answer; read-only, cannot disturb the setpoint |
| 1 s (100th tick) | emit `0xCC` real-time frame → Comm → UDP 10000 | developer's answer; same visible cadence as today's demo |
| step entry / STOP | send `SET_VALUES` once, expect one response | setpoint is latched by the Secondary — no periodic re-send; the response is still required (§2.1) and feeds the same 3-strike retry/offline counter as a poll |

This meets the architecture document's `T_CUTOFF_LATENCY ≤ 50 ms` budget: a
TIME cutoff needs only the local clock, not CAN feedback, so its resolution
is bounded by the 10 ms tick regardless of poll rate.

The tick is driven the same way `demo_realtime.c` drives its 1 Hz emitter
today: `me_exec_ms_until_next_tick()` becomes Core Logic's queue-receive
timeout (`core_logic_main()`'s `me_msgq_recv(..., timeout)` call). No timer
thread, no `timerfd`.

## 5. Traps this design accounts for

1. **Two float endiannesses in one program.** CAN-FD payloads are
   little-endian; step packets and the `0xCC` frame are big-endian.
   `can_frame.c` and `step_decode.c` must never share a float pack/unpack
   function. A host test asserts the exact 4 bytes for one known value, in
   both directions.
2. **Zero-filling unaddressed channel slots looks like commanding STOP.**
   Byte `+8` of every 16-byte CAN slot is that channel's `Command`, and
   `0x00` is `CMD_STO` — active, not "leave alone". **Accepted as a
   deliberate decision for this iteration, per the developer (2026-08-14):
   if a channel is present, its slot carries that channel's real values;
   if a channel is not present, its slot is left zero-filled — "no harm
   for now" because no second channel exists on the bus to receive that
   STOP.** `can_frame.c` builds the frame with `memset(frame, 0, 64)`
   first and then writes only the slots that have an active circuit. This
   must be revisited before a second channel is wired up — see open item 3.
3. **`0x04` is direction-dependent.** `CMD_RST_ERR` outbound vs. `CMD_ERR`
   inbound share a numeric value; kept as two separate named constants,
   never one shared enum value used both ways.
4. **Comparator footguns on TIME.** The old firmware's `<`, `<=`, `!=`
   comparators applied to a monotonically increasing time counter are true
   from t=0 — an instant, silent step exit. This design **rejects** those
   three comparators on a TIME cutoff with `ME_STEP_DECODE_UNSUPPORTED_COMPARATOR`
   rather than reproducing the footgun.
5. **`CC_Chg = 0x01` (program operator) and `CMD_CHA = 0x01` (CAN command)
   coincide numerically and mean unrelated things.** Translated through an
   explicit switch in `step_engine`, never assigned across.
6. **`>15` cutoffs silently became `0` in the old firmware** (a step that
   never ends). This design treats it as `ME_STEP_DECODE_TOO_MANY_CUTOFFS`,
   a rejected program, not a silently-never-ending one.

## 6. Open items — explicitly not resolved here

These are flagged rather than guessed at, per the project's standing
practice of surfacing `⚠ Open` items instead of picking silently.

1. ~~**Operator numbering.**~~ **RESOLVED 2026-08-14.** `Program Packet
   V0.12.xlsx` (`Ref Docs/program_packet_v0.12.md`) — a newer WebApp-side
   spec than the stale V0.7 — carries an Op-Codes sheet whose 19 operator
   codes, 6 comparator codes, and 13 registration-type codes are a
   byte-for-byte exact match to the firmware values this design already
   used (`SET=0x0A`, `CCChg=0x01`, `STO=0x0B` confirmed, along with all
   others). V0.12 also independently confirms, against real encoded sample
   bytes, three field widths this design assumed from firmware alone:
   `SET`/`REG`'s registration selector is 2 bytes, `GOTO`'s destination is
   2 bytes, and nominal/limit values are big-endian float32. One firmware
   opcode family remains incomplete in V0.12 — cutoff condition codes
   `0x3A`-`0x3D` (Accumulated/Step Capacity, Accumulated/Step Energy) are
   still absent, same gap as V0.7 — but this iteration only decodes
   `TIME (0x39)`, so it does not block. **No further confirmation against a
   captured WebApp frame is needed before the first hardware run for the
   fields this design uses.**
2. ~~**Whether a registration-parameter block follows a `CCChg` step on
   the real wire.**~~ **RESOLVED 2026-08-15.** It does, always — checked
   both ways the developer asked for. `stepData.c`'s `default:` case
   (every charge/discharge operator's branch) reads `numCutoffConditions`,
   walks the cutoff loop, then reads `numRegistrationParams` unconditionally
   right after. `Program Packet V0.12.xlsx` confirms it byte-for-byte: step
   2 (`CC_Chg`) carries the byte as `0x00` (present, zero entries), steps 3
   (`CV_Chg`), 4 (`PAU`), 7 (`CC_DChg`) carry it nonzero. It is not an
   inconsistency — it is a clean two-group rule: power operators, `PAU`,
   and `TABLE` always carry this byte (even at 0); `SET`, `STO`, `GOTO`,
   `REG`, `CYC`, `BEG`, `INT`, `ERR`, `MSG` never do. `step_decode` now
   reads it for `CCChg` per the corrected §2.2/§3.1. **Note on severity:**
   the original framing ("decode will desync at the next step") overstated
   the risk for THIS wire format specifically — ME's chain wrapper carries
   an explicit `nextIndex` that `me_chain_fetch_step()` uses to find the
   next step boundary independently of how many bytes `step_decode`
   consumes within this one, unlike the old firmware's own UART protocol
   where a misparse truly did cascade. A wrong byte-count here would have
   corrupted this step's own decoded values, not subsequent steps — but it
   is fixed now regardless, so this no longer matters in practice.
3. **How a Master addresses one channel without affecting the other three
   in its 64-byte frame.** Not defined anywhere in the CAN spec itself
   (flagged there as open item 15) — **but the developer has set policy
   for this iteration (2026-08-14): a present channel's slot carries its
   real values; an absent channel's slot is left zero-filled, "no harm for
   now" because no second channel exists to receive the resulting
   `CMD_STO`.** See trap §5.2. **Must be resolved — not merely revisited —
   before a second channel is wired up**, since zero-filling would then
   actively stop a real channel every time another channel's frame is sent.
4. **How the Master selects Block 1 (channels 1–4) vs. Block 2 (channels
   5–8)**, both of which use the same `Dev#,1`/`Dev#,2` CAN ID template in
   the spec — still genuinely unresolved at the protocol level (flagged
   there as open item 15/17 equivalents). **Send-side policy is decided
   for this iteration (2026-08-14): `can_frame` will expose
   `me_can_block_for_channel(channel_num)` returning which 64-byte block
   layout and slot index a channel maps to (1-4 → block 1, 5-8 → block 2),
   and Core Logic sends a block's frame only when that block has at least
   one active circuit.** Today only channel 1 exists, so only a Block 1
   frame is ever sent — this is forward-compatible plumbing, not a claim
   that the CAN-ID ambiguity itself is solved. One plausible resolution
   worth confirming with the hardware team: "Block 1" and "Block 2" may
   denote two separate physical Secondary devices (each handling up to 4
   channels, hence "1 Module : 4 Channel" in the spec), with `Dev#` simply
   differing per device rather than one device carrying two blocks — this
   would dissolve the ambiguity entirely, but the workbook does not state
   it and this design does not assume it.
5. **Registration type's destination.** Per the developer's answer, the
   SET-decoded registration type is destined for the UDP 10001 session
   packet — which does not exist yet (`realtime_frame.h` says explicitly
   "not built here yet"). Building that packet is **out of scope for this
   design**; this iteration stores the decoded value on the per-circuit
   context and logs it, so it is available whenever the 10001 builder is
   designed, but does not transmit it anywhere yet.
6. **Per-step registration parameters (§2.2/§3.1's `reg_params`) are
   decoded but not acted on.** These are a *different* concept from item 5
   above — item 5 is `SET`'s standing "what to log" bitmask; this is each
   `CCChg` step's own "when to log" trigger list (a threshold per
   registration channel). `step_decode` now decodes it fully rather than
   skip it, per the project's completeness bias and so a future logging
   feature needs no decoder rewrite — but `step_engine` does not build any
   registration/logging output from it this iteration. Out of scope by the
   same reasoning as item 5.

## 7. Testing

Per the project's standard (test-first) and the `.\build-native.ps1` /
`.\build.ps1` verification split documented in the project `CLAUDE.md`:

| Layer | Proves |
|---|---|
| `test_step_decode.c` (new) | SET mask extraction; CCChg + one TIME cutoff + zero registration params (mirrors V0.12 step 2's `0x00` byte exactly); CCChg with a nonzero registration-param list decodes each `{type, value}` pair correctly; STO's empty 11-byte body; truncated-buffer rejection; `>15` cutoffs rejected; `>15` registration params rejected; `<`/`<=`/`!=` on TIME rejected; the historical `[0]` vs `[i]` cutoff-index misalignment class of bug has a regression test even though this iteration only ever decodes one cutoff |
| `test_can_frame.c` (new) | Exact 64-byte output for one known setpoint (byte-for-byte); little-endian float encoding; 11-bit ID construction; `me_can_block_for_channel()` maps channels 1-4 to Block 1 and 5-8 to Block 2 with the correct slot index; SET/READ round-trip through the parser |
| `test_step_engine.c` (new) | A 10 000 ms cutoff fires at tick 10 000, not 9 990 or 10 010; full SET→CCChg→STOP sequence reaches `ME_EXEC_STOPPED`; the `SET` operator (registration type only) produces no CAN traffic at all; entering `CCChg` produces exactly one `SET_VALUES` frame; `STOP` produces one `SET_VALUES` frame carrying `CMD_STO`; 3 consecutive missed responses to EITHER frame type reach `ME_EXEC_CHANNEL_OFFLINE`; time does not accrue while a response is outstanding (mirrors the old firmware's step-timer freeze, which this design keeps deliberately since it is correct behaviour, not a bug) |
| `.\build.ps1` (existing) | Cross-compiles clean under `-Werror`, produces a static aarch64 ELF |
| `.\deploy.ps1` (developer, hardware) | The only proof of the end-to-end path: `0xCC` on UDP 10000 carrying a real step number, real run times, and a current value that traces back to the decoded program — not a synthetic ramp |

Steps 1–3 are pure logic and provable entirely on the developer's laptop
before anything touches the board, per `ADR-7` (proto/ stays free of
platform headers) and `ADR-3` (no local execution of the aarch64 binary).

## 8. Build order

1. `step_decode` + tests
2. `can_frame` + tests
3. `step_engine` + tests
4. `can_mgr` changes (Linux-only; cross-build clean, then board)
5. `core_logic` wiring — `me_execute_program()` gets its real body; `demo_realtime.c`/`.h` deleted; the post-registration one-shot relocated

Each step is independently verifiable before the next begins.
