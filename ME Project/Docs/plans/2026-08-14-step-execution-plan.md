# Step Execution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `me_execute_program()` in `core_logic.c` a real body that decodes
a `SET → CCChg → STOP` program, drives one simulated DC-DC channel over
CAN-FD, evaluates a time cutoff, and reports real values in the `0xCC`
frame — replacing the synthetic 1 Hz ramp emitter.

**Architecture:** Three new pure, host-testable modules (`step_decode`,
`can_frame`, `step_engine`) own all protocol and state-machine logic with no
platform headers. `core_logic.c` and `can_mgr.c` (Linux-only, proven only by
cross-build + hardware) become thin wiring: drain the engine's output queue
onto the existing message queues, and turn incoming CAN bytes into engine
inputs. `demo_realtime.c/.h` is deleted; its one still-needed piece (the
post-registration one-shot frame) is relocated to a small new module.

**Tech Stack:** C11 (host tests) / gnu11 (board), pthreads, the project's
existing bounded message queues (`msgq.h`) — no new dependencies, no dynamic
allocation anywhere in this plan (every buffer is fixed-size).

**Spec:** `Docs/specs/2026-08-14-step-execution-design.md` (read alongside
this plan — this plan fills in implementation-level detail the design left
at the "shape" level; every such addition is called out inline as a
**Plan decision** so it's visible and not silently assumed).

## Global Constraints

- **Two float endiannesses, never mixed.** CAN-FD payloads (`can_frame.c`):
  IEEE-754 binary32, **little-endian**. Step packets (`step_decode.c`) and
  the `0xCC` frame: IEEE-754 binary32, **big-endian**. These are separate
  pack/unpack functions in separate files — never share one.
- **The TIME cutoff limit is milliseconds, plain `uint32` big-endian** — not
  a float, not seconds. (Design doc §2.3.)
- **Timing:** 10 ms engine tick, 100 ms CAN poll, 1000 ms `0xCC` emit — all
  derived from one clock passed into the engine as a parameter, never read
  internally via `clock_gettime()`. (Design doc §4.)
- **No dynamic allocation.** Every struct in this plan is fixed-size
  (`ME_STEP_MAX_REG_PARAMS = 15`, `ME_EXEC_OUTPUT_QUEUE_LEN = 4`, etc.) —
  consistent with the project's existing no-`malloc` policy.
- **Test commands**, per the project's `CLAUDE.md`: run `.\build-native.ps1`
  (host unit tests) after every task that touches `me-primary/src/`. Run
  `.\build.ps1` (aarch64 cross-build) at the end of this plan and after any
  task touching `core_logic.c`/`can_mgr.c`/`main.c`/`comm_thread.c`. **A
  clean cross-build proves compilation only — never say "tested" for it.**
  The developer runs `.\deploy.ps1` on real hardware; that step is outside
  this plan.
- **⚠ HUMAN REVIEW NOTE (Tasks 7–8):** `step_engine`'s cutoff evaluation is
  timing logic in the sense the project's embedded guidelines flag for
  review. It runs against a **simulated** CAN bus this iteration (no
  physical actuation occurs from this code — see Task 9); the design was
  already presented to and approved by the developer in
  `Docs/specs/2026-08-14-step-execution-design.md` §4, and hardware-in-the-
  loop testing via `.\deploy.ps1` remains required before this is trusted
  against a real Secondary, per that spec's §7.

---

## File Structure

```
NEW
  me-primary/src/proto/step_decode.h / .c     (pure — host-tested)
  me-primary/src/proto/can_frame.h  / .c      (pure — host-tested)
  me-primary/src/exec/step_engine.h / .c      (pure — host-tested)
  me-primary/src/threads/post_reg.h / .c      (Linux-only — cross-build only)
  me-primary/tests/test_step_decode.c
  me-primary/tests/test_can_frame.c
  me-primary/tests/test_step_engine.c

MODIFIED
  me-primary/src/threads/core_logic.c          (real me_execute_program())
  me-primary/src/threads/can_mgr.c             (fabricated SET/READ responder)
  me-primary/src/threads/comm_thread.c         (post_reg.h, not demo_realtime.h)
  me-primary/src/main.c                        (post_reg.h init call)
  me-primary/tests/test_main.c                 (register the 3 new test suites)
  me-primary/tests/test_util.h                 (declare the 3 new run_*_tests())
  me-primary/build-native.ps1                  (add new pure sources + tests)
  me-primary/build.ps1                         (add new sources; drop demo_realtime.c)

DELETED
  me-primary/src/threads/demo_realtime.c / .h
```

---

## Task 1: `step_decode` — types, `SET`, and `STOP`

**Files:**
- Create: `me-primary/src/proto/step_decode.h`
- Create: `me-primary/src/proto/step_decode.c`
- Create: `me-primary/tests/test_step_decode.c`
- Modify: `me-primary/tests/test_util.h:96` (add `void run_step_decode_tests(void);` after the existing declarations)

**Interfaces:**
- Consumes: `me_chain_step_number()`, `me_chain_step_operator()`, `ME_STEP_HEADER_LEN` (`me-primary/src/proto/program_chain.h`, `proto_defs.h` — unchanged, already exist)
- Produces: `me_step_operator_t`, `me_step_reg_param_t`, `me_step_t`, `me_step_decode_result_t`, `me_step_decode()`, `me_step_decode_result_name()` — used by Task 3 (same file) and Task 6 (`step_engine`)

**Plan decision:** the design doc's `me_step_t` has no field recording *which*
comparator (`>` vs `>=`) a decoded cutoff used. Without it the engine cannot
reproduce the two comparators' distinct off-by-one behaviour. This task adds
`bool cutoff_inclusive` (`true` for `>=`). Also adds two decode-result values
the design didn't enumerate: `ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED`
(this iteration's `me_step_t` has one cutoff slot, not an array — 2+ cutoffs
on the wire must be rejected loudly, not silently truncated to the first)
and `ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE` (an action byte outside the six
documented values must not be guessed at a width).

- [ ] **Step 1: Write the failing test — SET and STOP happy path**

```c
/* me-primary/tests/test_step_decode.c */
#include <string.h>

#include "test_util.h"
#include "../src/proto/step_decode.h"
#include "../src/proto/program_chain.h"

/* Writes a chain-wrapped step: AA55 | nextIndex(4) | stepNo(2) | body |
 * 55AA. Returns the packet's total length. body_len may be 0. */
static uint32_t put_wrapped(uint8_t *buf, uint32_t next, uint16_t step_no,
                            const uint8_t *body, uint32_t body_len)
{
    uint32_t i = 0;
    buf[i++] = ME_STEP_START_1;
    buf[i++] = ME_STEP_START_2;
    buf[i++] = (uint8_t)(next >> 24);
    buf[i++] = (uint8_t)(next >> 16);
    buf[i++] = (uint8_t)(next >> 8);
    buf[i++] = (uint8_t)(next);
    buf[i++] = (uint8_t)(step_no >> 8);
    buf[i++] = (uint8_t)(step_no);
    memcpy(&buf[i], body, body_len);
    i += body_len;
    buf[i++] = ME_STEP_END_1;
    buf[i++] = ME_STEP_END_2;
    return i;
}

static void test_set_decodes_the_registration_mask(void)
{
    TEST_CASE("step_decode: SET extracts the 13-bit registration mask");
    /* op(0x0A) | skip(1) | regType(2, 0x01FF) - the exact V0.12 step 5 body. */
    uint8_t body[] = { 0x0A, 0x00, 0x01, 0xFF };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 5, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(ME_OP_SET, step.operator);
    CHECK_EQ_U(5, step.step_number);
    CHECK_EQ_U(0x01FF, step.registration_type);
}

static void test_set_masks_to_thirteen_bits(void)
{
    TEST_CASE("step_decode: SET masks the registration type to 13 bits");
    /* 0xFFFF & 0x1FFF = 0x1FFF - the top 3 bits are firmware-internal only. */
    uint8_t body[] = { 0x0A, 0x00, 0xFF, 0xFF };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 1, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(0x1FFF, step.registration_type);
}

static void test_stop_has_no_body(void)
{
    TEST_CASE("step_decode: STO carries no body at all - 11-byte packet");
    uint8_t body[] = { 0x0B }; /* operator byte only - no further data */
    uint8_t buf[16];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 6, body, sizeof(body));

    CHECK_EQ_U(11, len); /* matches Program Packet V0.12.xlsx step 6 exactly */

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(ME_OP_STOP, step.operator);
    CHECK_EQ_U(6, step.step_number);
}

static void test_unknown_operator_is_rejected(void)
{
    TEST_CASE("step_decode: an operator outside {SET,CCChg,STO} is rejected loudly");
    uint8_t body[] = { 0x08 }; /* PAU - a real BTS-600 operator, but out of scope here */
    uint8_t buf[16];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 1, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_OPERATOR, me_step_decode(buf, len, &step));
}

void run_step_decode_tests(void)
{
    test_set_decodes_the_registration_mask();
    test_set_masks_to_thirteen_bits();
    test_stop_has_no_body();
    test_unknown_operator_is_rejected();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1` (before `step_decode.h` exists this
will fail to *compile* — `../src/proto/step_decode.h: No such file`. That
compile failure is the expected "fails for the right reason" here, since
there is no runnable binary yet.)

- [ ] **Step 3: Create `step_decode.h`**

```c
/*
 * step_decode.h - decodes the SET/CCChg/STOP operator bodies out of a
 * chain-wrapped program step.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. Built by both
 * build-native.ps1 and build.ps1, same as program_chain.h/.c.
 *
 * Byte layout source: Docs/specs/2026-08-14-step-execution-design.md §2.2,
 * cross-checked against stepData.c (old Secondary firmware, authoritative
 * for behaviour) and Ref Docs/program_packet_v0.12.md (byte-exact samples).
 * All multi-byte fields inside a step body are BIG-ENDIAN.
 */
#ifndef ME_STEP_DECODE_H
#define ME_STEP_DECODE_H

#include <stdbool.h>
#include <stdint.h>

/* Mirrors the wire's own cap (a count byte >15 is rejected, never folded to
 * 0 - the old firmware silently produced a step that never ends this way). */
#define ME_STEP_MAX_REG_PARAMS 15u

typedef enum {
    ME_OP_SET   = 0x0A,
    ME_OP_CCCHG = 0x01,
    ME_OP_STOP  = 0x0B,
} me_step_operator_t;

/* One entry of a CCChg step's trailing "registration parameters" list -
 * the per-step "when to log" trigger list, distinct from SET's "what to
 * log" bitmask above. Decoded fully; not yet acted on by step_engine. */
typedef struct {
    uint8_t registration_type; /* 0x21..0x2D */
    float   value;             /* engineering units; for TIME (0x21) this is
                                 * the decoded millisecond count cast to
                                 * float, matching the firmware's own cast */
} me_step_reg_param_t;

typedef struct {
    me_step_operator_t operator;
    uint16_t            step_number;

    /* SET only */
    uint16_t registration_type; /* 13-bit mask, already & 0x1FFF */

    /* CCChg only */
    float    nominal_current_a;
    bool     has_cutoff;
    bool     cutoff_inclusive;  /* true = '>=' (0x53), false = '>' (0x51) */
    uint32_t cutoff_time_ms;
    uint8_t  num_reg_params;
    me_step_reg_param_t reg_params[ME_STEP_MAX_REG_PARAMS];
} me_step_t;

typedef enum {
    ME_STEP_DECODE_OK = 0,
    ME_STEP_DECODE_UNSUPPORTED_OPERATOR,
    ME_STEP_DECODE_TRUNCATED,
    ME_STEP_DECODE_TOO_MANY_CUTOFFS,             /* wire count > 15 */
    ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED, /* wire count is 2..15 */
    ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE,      /* anything but TIME (0x39) */
    ME_STEP_DECODE_UNSUPPORTED_COMPARATOR,       /* anything but > or >= */
    ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE,      /* not one of the 6 documented values */
    ME_STEP_DECODE_TOO_MANY_REG_PARAMS,          /* wire count > 15 */
} me_step_decode_result_t;

const char *me_step_decode_result_name(me_step_decode_result_t r);

/*
 * `step` must be a pointer returned by me_chain_fetch_step(), and `step_len`
 * its matching length (including the AA55/55AA sentinels) - both already
 * validated by the chain walker, which this function trusts without
 * re-checking. On any result other than ME_STEP_DECODE_OK, *out is zeroed
 * but its contents should not be used.
 */
me_step_decode_result_t me_step_decode(const uint8_t *step, uint32_t step_len,
                                       me_step_t *out);

#endif /* ME_STEP_DECODE_H */
```

- [ ] **Step 4: Create `step_decode.c` — SET, STOP, and the unsupported-operator path only**

```c
/* step_decode.c - see step_decode.h for the format sources. */
#include "step_decode.h"

#include <string.h>

#include "program_chain.h"
#include "proto_defs.h"

#define WIRE_OP_CCCHG 0x01u
#define WIRE_OP_SET   0x0Au
#define WIRE_OP_STOP  0x0Bu

#define WIRE_SET_SKIP_LEN    1u /* "No. of Global Limit Parameters", unread */
#define WIRE_SET_REGTYPE_LEN 2u

const char *me_step_decode_result_name(me_step_decode_result_t r)
{
    switch (r) {
    case ME_STEP_DECODE_OK:                           return "OK";
    case ME_STEP_DECODE_UNSUPPORTED_OPERATOR:         return "UNSUPPORTED_OPERATOR";
    case ME_STEP_DECODE_TRUNCATED:                    return "TRUNCATED";
    case ME_STEP_DECODE_TOO_MANY_CUTOFFS:             return "TOO_MANY_CUTOFFS";
    case ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED: return "MULTIPLE_CUTOFFS_UNSUPPORTED";
    case ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE:      return "UNSUPPORTED_CUTOFF_TYPE";
    case ME_STEP_DECODE_UNSUPPORTED_COMPARATOR:       return "UNSUPPORTED_COMPARATOR";
    case ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE:      return "UNSUPPORTED_ACTION_TYPE";
    case ME_STEP_DECODE_TOO_MANY_REG_PARAMS:          return "TOO_MANY_REG_PARAMS";
    default:                                          return "UNKNOWN";
    }
}

static uint16_t get_u16_be(const uint8_t *p)
{
    return (uint16_t)(((uint16_t)p[0] << 8) | (uint16_t)p[1]);
}

me_step_decode_result_t me_step_decode(const uint8_t *step, uint32_t step_len,
                                       me_step_t *out)
{
    memset(out, 0, sizeof(*out));

    /* me_chain_fetch_step() already guarantees step_len >= ME_STEP_MIN_LEN
     * and valid AA55/55AA sentinels - trusted, not re-checked here. */
    const uint32_t bound = step_len - 2u; /* exclude the trailing 55 AA */

    out->step_number = me_chain_step_number(step);
    const uint8_t raw_op = me_chain_step_operator(step);
    uint32_t offset = ME_STEP_HEADER_LEN; /* 9 - first byte after the operator */

    switch (raw_op) {
    case WIRE_OP_SET:
        out->operator = ME_OP_SET;
        if (offset + WIRE_SET_SKIP_LEN + WIRE_SET_REGTYPE_LEN > bound) {
            return ME_STEP_DECODE_TRUNCATED;
        }
        offset += WIRE_SET_SKIP_LEN;
        out->registration_type = get_u16_be(&step[offset]) & 0x1FFFu;
        return ME_STEP_DECODE_OK;

    case WIRE_OP_STOP:
        out->operator = ME_OP_STOP;
        return ME_STEP_DECODE_OK; /* no body at all */

    case WIRE_OP_CCCHG:
        /* Implemented in Task 2/3. */
        return ME_STEP_DECODE_UNSUPPORTED_OPERATOR;

    default:
        return ME_STEP_DECODE_UNSUPPORTED_OPERATOR;
    }
}
```

- [ ] **Step 5: Wire the test into the runner**

Edit `me-primary/tests/test_util.h`, adding after line 96
(`void run_realtime_frame_tests(void);`):

```c
void run_step_decode_tests(void);
```

Edit `me-primary/tests/test_main.c`, adding after line 31
(`run_realtime_frame_tests();`):

```c
    run_step_decode_tests();
```

Edit `me-primary/build-native.ps1`'s `$sources` array, adding two lines
after the `realtime_frame.c` / `test_realtime_frame.c` entries:

```
    "$PSScriptRoot\src\proto\step_decode.c"
```
```
    "$PSScriptRoot\tests\test_step_decode.c"
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: builds clean, all checks pass including the 4 new ones, `RESULT: PASS`.

- [ ] **Step 7: Commit**

```bash
git add me-primary/src/proto/step_decode.h me-primary/src/proto/step_decode.c \
        me-primary/tests/test_step_decode.c me-primary/tests/test_util.h \
        me-primary/tests/test_main.c me-primary/build-native.ps1
git commit -m "Add step_decode: SET and STOP operator decoding"
```

---

## Task 2: `step_decode` — `CCChg` nominal value and one TIME cutoff

**Files:**
- Modify: `me-primary/src/proto/step_decode.c`
- Modify: `me-primary/tests/test_step_decode.c`

**Interfaces:**
- Consumes: nothing new
- Produces: `me_step_t.nominal_current_a`, `.has_cutoff`, `.cutoff_inclusive`, `.cutoff_time_ms` now populated for `CCChg` — consumed by Task 8 (`step_engine`'s cutoff check)

- [ ] **Step 1: Write the failing tests**

Append to `me-primary/tests/test_step_decode.c`:

```c
static void test_ccchg_decodes_current_and_time_cutoff(void)
{
    TEST_CASE("step_decode: CCChg decodes nominal current and a '>' TIME cutoff");
    uint8_t body[] = {
        0x01,                   /* operator: CCChg                        */
        0x41, 0x20, 0x00, 0x00, /* nominal current = 10.0f (BE)            */
        0x01,                   /* numCutoffConditions = 1                */
        0x39,                   /* condition: TIME                        */
        0x51,                   /* logic: '>'                             */
        0x00, 0x00, 0x27, 0x10, /* limit = 10000 ms (BE u32)               */
        0x00,                   /* actionType: BLANK                      */
        0x00,                   /* numRegParams = 0 - ALWAYS present, see
                                  * Task 3's design-decision note          */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(ME_OP_CCCHG, step.operator);
    CHECK(step.nominal_current_a > 9.999f && step.nominal_current_a < 10.001f);
    CHECK(step.has_cutoff);
    CHECK(!step.cutoff_inclusive);
    CHECK_EQ_U(10000, step.cutoff_time_ms);
    CHECK_EQ_U(0, step.num_reg_params);
}

static void test_ccchg_accepts_gte_comparator(void)
{
    TEST_CASE("step_decode: CCChg accepts '>=' as well as '>'");
    uint8_t body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x53,             /* logic: '>=' */
        0x00, 0x00, 0x03, 0xE8,       /* limit = 1000 ms */
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK(step.cutoff_inclusive);
}

static void test_ccchg_rejects_lt_on_time(void)
{
    TEST_CASE("step_decode: '<' on a TIME cutoff is rejected (true from t=0)");
    uint8_t body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x52,             /* logic: '<' - rejected */
        0x00, 0x00, 0x03, 0xE8,
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_COMPARATOR, me_step_decode(buf, len, &step));
}

static void test_ccchg_rejects_non_time_cutoff(void)
{
    TEST_CASE("step_decode: a VOLTAGE cutoff is rejected - only TIME is decoded");
    /* This is Program Packet V0.12.xlsx step 2's OWN raw bytes, verbatim -
     * a real WebApp-shaped CC_Chg step with a VOLTAGE cutoff. Proves this
     * decoder correctly recognises and rejects it rather than mis-decoding
     * it as something plausible. */
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00, /* current = 1.0f */
        0x01, 0x32, 0x51,             /* VOLTAGE, '>' */
        0x41, 0x61, 0x99, 0x9A,       /* limit = 14.1f */
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE, me_step_decode(buf, len, &step));
}

static void test_ccchg_zero_cutoffs_is_valid(void)
{
    TEST_CASE("step_decode: zero cutoff conditions is a valid (if degenerate) decode");
    uint8_t body[] = { 0x01, 0x3F, 0x80, 0x00, 0x00, 0x00, 0x00 };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK(!step.has_cutoff);
}

static void test_ccchg_rejects_more_than_one_cutoff(void)
{
    TEST_CASE("step_decode: 2+ cutoffs are rejected - me_step_t has one slot");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x02, /* numCutoffConditions = 2 */
        0x39, 0x51, 0x00, 0x00, 0x03, 0xE8, 0x00,
        0x39, 0x51, 0x00, 0x00, 0x07, 0xD0, 0x00,
        0x00,
    };
    uint8_t buf[40];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED,
              me_step_decode(buf, len, &step));
}

static void test_ccchg_rejects_more_than_fifteen_cutoffs(void)
{
    TEST_CASE("step_decode: a wire count > 15 is rejected, not folded to 0");
    uint8_t body[] = { 0x01, 0x3F, 0x80, 0x00, 0x00, 16 };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TOO_MANY_CUTOFFS, me_step_decode(buf, len, &step));
}

static void test_ccchg_truncated_buffer_is_rejected(void)
{
    TEST_CASE("step_decode: a step cut off mid-cutoff is rejected, not read past");
    uint8_t body[] = { 0x01, 0x3F, 0x80, 0x00, 0x00, 0x01, 0x39 }; /* logic byte missing */
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TRUNCATED, me_step_decode(buf, len, &step));
}
```

Add all seven calls to `run_step_decode_tests()`.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: `test_ccchg_decodes_current_and_time_cutoff` and the other new
cases FAIL (CCChg still returns `ME_STEP_DECODE_UNSUPPORTED_OPERATOR`).

- [ ] **Step 3: Implement the CCChg cutoff decode**

Replace the `case WIRE_OP_CCCHG:` arm in `step_decode.c` and add the helper
functions above it:

```c
#define WIRE_COND_TIME 0x39u
#define WIRE_LOGIC_GT  0x51u
#define WIRE_LOGIC_GTE 0x53u

#define WIRE_ACTION_BLANK 0x00u
#define WIRE_ACTION_STO   0x0Bu
#define WIRE_ACTION_INT   0x0Eu
#define WIRE_ACTION_ERR   0x10u
#define WIRE_ACTION_MSG   0x11u
#define WIRE_ACTION_GOTO  0x09u

static uint32_t get_u32_be(const uint8_t *p)
{
    return ((uint32_t)p[0] << 24) | ((uint32_t)p[1] << 16)
         | ((uint32_t)p[2] << 8)  |  (uint32_t)p[3];
}

static float get_f32_be(const uint8_t *p)
{
    const uint32_t bits = get_u32_be(p);
    float v;
    memcpy(&v, &bits, sizeof(v));
    return v;
}

/* Width of a cutoff action's trailing value, in bytes. -1 = unrecognised -
 * see ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE's doc comment. */
static int action_value_width(uint8_t action)
{
    switch (action) {
    case WIRE_ACTION_BLANK:
    case WIRE_ACTION_STO:
    case WIRE_ACTION_INT:
        return 0;
    case WIRE_ACTION_ERR:
    case WIRE_ACTION_MSG:
        return 1;
    case WIRE_ACTION_GOTO:
        return 2;
    default:
        return -1;
    }
}

/* Decodes the cutoff-condition block starting at *offset. This iteration's
 * me_step_t has exactly one cutoff slot, so 2+ conditions on the wire are
 * rejected rather than silently truncated to the first - see
 * ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED. */
static me_step_decode_result_t decode_cutoffs(const uint8_t *step, uint32_t bound,
                                              uint32_t *offset, me_step_t *out)
{
    if (*offset + 1u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t num_cutoffs = step[*offset];
    (*offset)++;

    if (num_cutoffs > 15u) { return ME_STEP_DECODE_TOO_MANY_CUTOFFS; }
    if (num_cutoffs > 1u)  { return ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED; }
    if (num_cutoffs == 0u) { return ME_STEP_DECODE_OK; } /* has_cutoff already false */

    if (*offset + 2u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t condition = step[*offset];
    const uint8_t logic     = step[*offset + 1u];
    *offset += 2u;

    if (condition != WIRE_COND_TIME) { return ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE; }
    if (logic != WIRE_LOGIC_GT && logic != WIRE_LOGIC_GTE) {
        return ME_STEP_DECODE_UNSUPPORTED_COMPARATOR;
    }

    if (*offset + 4u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    out->cutoff_time_ms  = get_u32_be(&step[*offset]); /* TIME limit: plain ms, not float */
    out->cutoff_inclusive = (logic == WIRE_LOGIC_GTE);
    out->has_cutoff       = true;
    *offset += 4u;

    if (*offset + 1u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t action = step[*offset];
    (*offset)++;

    const int width = action_value_width(action);
    if (width < 0) { return ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE; }
    if (*offset + (uint32_t)width > bound) { return ME_STEP_DECODE_TRUNCATED; }
    *offset += (uint32_t)width; /* value itself unused - only BLANK is acted on */

    return ME_STEP_DECODE_OK;
}
```

Replace the `case WIRE_OP_CCCHG:` body:

```c
    case WIRE_OP_CCCHG: {
        out->operator = ME_OP_CCCHG;
        if (offset + 4u > bound) { return ME_STEP_DECODE_TRUNCATED; }
        out->nominal_current_a = get_f32_be(&step[offset]);
        offset += 4u;

        const me_step_decode_result_t r = decode_cutoffs(step, bound, &offset, out);
        if (r != ME_STEP_DECODE_OK) { return r; }

        /* Registration-params block: Task 3. */
        return ME_STEP_DECODE_OK;
    }
```

(`out->num_reg_params` stays 0 from the initial `memset` until Task 3 reads
it — every test in this task supplies zero params.)

- [ ] **Step 4: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/proto/step_decode.c me-primary/tests/test_step_decode.c
git commit -m "Decode CCChg nominal current and a single TIME cutoff"
```

---

## Task 3: `step_decode` — `CCChg` registration-parameters block

**Files:**
- Modify: `me-primary/src/proto/step_decode.c`
- Modify: `me-primary/tests/test_step_decode.c`

**Interfaces:**
- Consumes: nothing new
- Produces: `me_step_t.num_reg_params` / `.reg_params[]` fully populated for `CCChg` — not consumed by any task in this plan (design doc open item 6: decoded, not yet acted on)

- [ ] **Step 1: Write the failing tests**

Append to `me-primary/tests/test_step_decode.c`:

```c
static void test_ccchg_decodes_nonzero_reg_params(void)
{
    TEST_CASE("step_decode: CCChg decodes a nonzero registration-param list");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00, /* current = 1.0f */
        0x01, 0x39, 0x51, 0x00, 0x00, 0x03, 0xE8, 0x00, /* 1 TIME cutoff, 1000ms, '>' */
        0x02, /* numRegParams = 2 */
        0x22, 0x3D, 0xCC, 0xCC, 0xCD, /* Current, 0.1f */
        0x24, 0x42, 0x22, 0x00, 0x00, /* Temperature, 40.5f */
    };
    uint8_t buf[64];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 3, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(2, step.num_reg_params);
    CHECK_EQ_U(0x22, step.reg_params[0].registration_type);
    CHECK(step.reg_params[0].value > 0.0999f && step.reg_params[0].value < 0.1001f);
    CHECK_EQ_U(0x24, step.reg_params[1].registration_type);
    CHECK(step.reg_params[1].value > 40.499f && step.reg_params[1].value < 40.501f);
}

static void test_ccchg_v012_step2_layout_decodes_zero_reg_params(void)
{
    TEST_CASE("step_decode: V0.12 step 2's own byte-21 0x00 decodes as zero params");
    /* Same shape as Program Packet V0.12.xlsx step 2, with the VOLTAGE
     * cutoff swapped for a TIME one so this exercises the SUPPORTED path -
     * see test_ccchg_rejects_non_time_cutoff (Task 2) for the real bytes. */
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x01, 0x39, 0x51, 0x00, 0x00, 0x03, 0xE8, 0x00,
        0x00, /* numRegParams = 0 - present and zero, per V0.12 step 2 offset 21 */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(0, step.num_reg_params);
}

static void test_ccchg_rejects_more_than_fifteen_reg_params(void)
{
    TEST_CASE("step_decode: a reg-param wire count > 15 is rejected");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x00, /* zero cutoffs, to keep this test focused */
        16,   /* numRegParams = 16 */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TOO_MANY_REG_PARAMS, me_step_decode(buf, len, &step));
}

static void test_ccchg_missing_reg_param_count_is_truncated(void)
{
    TEST_CASE("step_decode: a step missing its (always-present) reg-param count is truncated");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x00, /* zero cutoffs */
        /* no trailing numRegParams byte at all */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TRUNCATED, me_step_decode(buf, len, &step));
}
```

Add all four calls to `run_step_decode_tests()`.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: the two "decodes ... reg params" cases FAIL (nothing reads the
byte yet); the two rejection cases currently pass by accident (decode
returns OK with the byte silently unread) — note in the commit message that
this is exactly the silent-misparse gap this task closes.

- [ ] **Step 3: Implement `decode_reg_params()` and call it**

Add to `step_decode.c`:

```c
/* Decodes the trailing "No. of Registration parameters" block. ALWAYS
 * present on CCChg-family operators, even when the count is 0 - confirmed
 * both against stepData.c's default: case and Program Packet V0.12.xlsx
 * step 2's own byte 21 (see Docs/specs/2026-08-14-step-execution-design.md
 * §2.2's corrected write-up). */
static me_step_decode_result_t decode_reg_params(const uint8_t *step, uint32_t bound,
                                                  uint32_t *offset, me_step_t *out)
{
    if (*offset + 1u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t count = step[*offset];
    (*offset)++;

    if (count > ME_STEP_MAX_REG_PARAMS) { return ME_STEP_DECODE_TOO_MANY_REG_PARAMS; }
    out->num_reg_params = count;

    for (uint8_t i = 0; i < count; i++) {
        if (*offset + 5u > bound) { return ME_STEP_DECODE_TRUNCATED; }
        out->reg_params[i].registration_type = step[*offset];
        out->reg_params[i].value             = get_f32_be(&step[*offset + 1u]);
        *offset += 5u;
    }
    return ME_STEP_DECODE_OK;
}
```

Replace the `/* Registration-params block: Task 3. */` line in the
`WIRE_OP_CCCHG` case with:

```c
        return decode_reg_params(step, bound, &offset, out);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/proto/step_decode.c me-primary/tests/test_step_decode.c
git commit -m "Decode CCChg's always-present registration-parameters block"
```

---

## Task 4: `can_frame` — block/slot mapping and CAN ID composition

**Files:**
- Create: `me-primary/src/proto/can_frame.h`
- Create: `me-primary/src/proto/can_frame.c`
- Create: `me-primary/tests/test_can_frame.c`
- Modify: `me-primary/tests/test_util.h` (add `void run_can_frame_tests(void);`)

**Interfaces:**
- Consumes: nothing
- Produces: `me_can_block_for_channel()`, `me_can_id()`, all `ME_CAN_*` constants — consumed by Task 5 (same file) and Task 6 (`step_engine`)

- [ ] **Step 1: Write the failing test**

```c
/* me-primary/tests/test_can_frame.c */
#include "test_util.h"
#include "../src/proto/can_frame.h"

static void test_channels_one_to_four_are_block_one(void)
{
    TEST_CASE("can_frame: channels 1-4 map to Block 1, slots 0-3 in order");
    uint8_t slot;
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(1, &slot)); CHECK_EQ_U(0, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(2, &slot)); CHECK_EQ_U(1, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(3, &slot)); CHECK_EQ_U(2, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(4, &slot)); CHECK_EQ_U(3, slot);
}

static void test_channels_five_to_eight_are_block_two(void)
{
    TEST_CASE("can_frame: channels 5-8 map to Block 2, slots 0-3 in order");
    uint8_t slot;
    CHECK_EQ_U(ME_CAN_BLOCK_2, me_can_block_for_channel(5, &slot)); CHECK_EQ_U(0, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_2, me_can_block_for_channel(8, &slot)); CHECK_EQ_U(3, slot);
}

static void test_can_id_composition(void)
{
    TEST_CASE("can_frame: CAN ID is (circuit6 << 5) | function5");
    /* Secondary 1, SET_VALUES: (1 << 5) | 1 = 0x21. */
    CHECK_EQ_U(0x21, me_can_id(1, ME_CAN_FUNC_SET));
    /* Secondary 1, READ_VALUES: (1 << 5) | 2 = 0x22. */
    CHECK_EQ_U(0x22, me_can_id(1, ME_CAN_FUNC_READ));
}

void run_can_frame_tests(void)
{
    test_channels_one_to_four_are_block_one();
    test_channels_five_to_eight_are_block_two();
    test_can_id_composition();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: fails to compile — `can_frame.h` does not exist yet.

- [ ] **Step 3: Create `can_frame.h`**

```c
/*
 * can_frame.h - CAN-FD SET_VALUES/READ_VALUES frame construction and
 * feedback parsing, for ONE physical Secondary channel per call.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers.
 *
 * Layout source: Ref Docs/master_slave_can_v1.0.md. Floats are IEEE-754
 * binary32, LITTLE-ENDIAN - the opposite of every WebApp-facing frame in
 * this codebase (step_decode.c, realtime_frame.c). This asymmetry is
 * deliberate; do not share a float pack/unpack function between the two.
 */
#ifndef ME_CAN_FRAME_H
#define ME_CAN_FRAME_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_CAN_FRAME_LEN          64u
#define ME_CAN_SLOT_LEN           16u
#define ME_CAN_CHANNELS_PER_BLOCK  4u

/* Command byte (Master -> Slave, slot offset +8). */
#define ME_CAN_CMD_STO 0x00u
#define ME_CAN_CMD_CHA 0x01u
/* Outbound-only meaning of 0x04 ("reset the last error"). Deliberately a
 * DIFFERENT named constant from ME_CAN_STATE_ERR below, even though the
 * numeric value is identical - see design doc trap "0x04 is
 * direction-dependent". Unused this iteration; defined so the trap is
 * visible in code, not only in a comment. */
#define ME_CAN_CMD_RST_ERR 0x04u

/* STATE byte (Slave -> Master, slot offset +8). */
#define ME_CAN_STATE_STO 0x00u
#define ME_CAN_STATE_CHA 0x01u
/* Inbound-only meaning of 0x04 ("the slave node is in an error state"). */
#define ME_CAN_STATE_ERR 0x04u

/* CAN-FD function codes. */
#define ME_CAN_FUNC_SET  0x01u
#define ME_CAN_FUNC_READ 0x02u

typedef struct {
    uint8_t channel_num; /* 1-based, 1..8 */
    uint8_t command;     /* ME_CAN_CMD_* */
    float   set_voltage;
    float   set_current;
} me_can_setpoint_t;

typedef struct {
    uint8_t state;            /* ME_CAN_STATE_* */
    float   feedback_voltage;
    float   feedback_current;
} me_can_feedback_t;

typedef enum { ME_CAN_BLOCK_1 = 1, ME_CAN_BLOCK_2 = 2 } me_can_block_t;

/*
 * Maps a 1-based channel (1..8) to its block and 0-based slot index within
 * that block's 64-byte frame. Channels 1-4 -> Block 1 slots 0-3; 5-8 ->
 * Block 2 slots 0-3. Per the developer's decision
 * (Docs/specs/2026-08-14-step-execution-design.md §6 item 4), the caller
 * sends a block's frame only when that block has at least one active
 * channel - this iteration only ever has channel 1, so only Block 1 is
 * ever built.
 */
me_can_block_t me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out);

/*
 * 11-bit CAN identifier: circuit_num6 (6 bits) << 5 | function5 (5 bits).
 * circuit_num6 is the CAN-FD "circuit/module number" from
 * Ref Docs/master_slave_can_v1.0.md - one per physical Secondary (DC-DC)
 * module, each handling up to 8 channels. Callers pass ME's Secondary
 * number here (ME_CIRCUIT_SECONDARY(circuit_id)), NOT the full 8-bit ME
 * CircuitID.
 */
uint16_t me_can_id(uint8_t circuit_num6, uint8_t function5);

/*
 * Builds a full 64-byte SET_VALUES frame with every slot zero except the
 * one for sp->channel_num. Per the developer's decision (design doc §5
 * trap 2 / §6 item 3), the other three slots are deliberately left
 * zero-filled: this iteration has exactly one channel on the bus, so a
 * zero-filled CMD_STO in an unaddressed slot reaches nobody. Revisit before
 * a second channel is wired up.
 */
void me_can_pack_set(const me_can_setpoint_t *sp, uint8_t out64[ME_CAN_FRAME_LEN]);

/* Builds a full 64-byte READ_VALUES request frame for one channel. Only
 * Channel # (slot offset +9) is meaningful in a request; every other byte
 * in that slot, and every other slot, is zero. */
void me_can_pack_read(uint8_t channel_num, uint8_t out64[ME_CAN_FRAME_LEN]);

/*
 * Builds a full 64-byte response frame (as if from the Secondary) for one
 * channel. Used by the CAN Data Manager's fabricator (Task 9), since no
 * physical Secondary exists yet.
 */
void me_can_pack_feedback(uint8_t channel_num, const me_can_feedback_t *fb,
                          uint8_t out64[ME_CAN_FRAME_LEN]);

/*
 * Parses the slot for channel_num out of a 64-byte frame. Works on BOTH a
 * genuine response frame (Feedback Voltage/Current, STATE) and a Master
 * request frame (Set Voltage/Current, Command) - the two share an
 * identical byte layout, only the field NAMES differ by direction. Returns
 * false if channel_num is out of range (not 1..8).
 */
bool me_can_parse_feedback(const uint8_t frame64[ME_CAN_FRAME_LEN],
                           uint8_t channel_num, me_can_feedback_t *out);

#endif /* ME_CAN_FRAME_H */
```

- [ ] **Step 4: Create `can_frame.c` — block mapping and CAN ID only**

```c
/* can_frame.c - see can_frame.h for the format source. */
#include "can_frame.h"

me_can_block_t me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out)
{
    const uint8_t zero_based = (uint8_t)(channel_num - 1u);
    *slot_out = (uint8_t)(zero_based % ME_CAN_CHANNELS_PER_BLOCK);
    return (channel_num <= ME_CAN_CHANNELS_PER_BLOCK) ? ME_CAN_BLOCK_1 : ME_CAN_BLOCK_2;
}

uint16_t me_can_id(uint8_t circuit_num6, uint8_t function5)
{
    return (uint16_t)(((uint16_t)(circuit_num6 & 0x3Fu) << 5) | (function5 & 0x1Fu));
}
```

(`me_can_pack_set`/`me_can_pack_read`/`me_can_pack_feedback`/
`me_can_parse_feedback` are implemented in Task 5 — leave them undeclared-
but-unused for now by not calling them from the test yet.)

- [ ] **Step 5: Wire the test into the runner**

Same three edits as Task 1 Step 5, for `run_can_frame_tests`, `test_can_frame.c`,
`can_frame.c`.

- [ ] **Step 6: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`.

- [ ] **Step 7: Commit**

```bash
git add me-primary/src/proto/can_frame.h me-primary/src/proto/can_frame.c \
        me-primary/tests/test_can_frame.c me-primary/tests/test_util.h \
        me-primary/tests/test_main.c me-primary/build-native.ps1
git commit -m "Add can_frame: channel-to-block mapping and CAN ID composition"
```

---

## Task 5: `can_frame` — frame packing and feedback parsing

**Files:**
- Modify: `me-primary/src/proto/can_frame.c`
- Modify: `me-primary/tests/test_can_frame.c`

**Interfaces:**
- Consumes: nothing new
- Produces: `me_can_pack_set()`, `me_can_pack_read()`, `me_can_pack_feedback()`, `me_can_parse_feedback()` — consumed by Task 6/7 (`step_engine`) and Task 9 (`can_mgr`)

- [ ] **Step 1: Write the failing tests**

Append to `test_can_frame.c`:

```c
static void test_pack_set_exact_bytes(void)
{
    TEST_CASE("can_frame: pack_set produces the exact 64 bytes for a known setpoint");
    me_can_setpoint_t sp = { .channel_num = 1, .command = ME_CAN_CMD_CHA,
                             .set_voltage = 0.0f, .set_current = 10.0f };
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, frame);

    /* 10.0f little-endian = 00 00 20 41. Slot 0 starts at byte 0. */
    CHECK_BYTE(frame, 0, 0x00); CHECK_BYTE(frame, 1, 0x00);
    CHECK_BYTE(frame, 2, 0x00); CHECK_BYTE(frame, 3, 0x00); /* voltage = 0.0f */
    CHECK_BYTE(frame, 4, 0x00); CHECK_BYTE(frame, 5, 0x00);
    CHECK_BYTE(frame, 6, 0x20); CHECK_BYTE(frame, 7, 0x41); /* current = 10.0f LE */
    CHECK_BYTE(frame, 8, ME_CAN_CMD_CHA);
    CHECK_BYTE(frame, 9, 1);
    /* Slot 1 (channel 2, unaddressed) is entirely zero. */
    for (int i = 16; i < 32; i++) { CHECK_BYTE(frame, i, 0x00); }
}

static void test_pack_read_only_sets_channel_number(void)
{
    TEST_CASE("can_frame: pack_read leaves every byte but Channel # at zero");
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_read(1, frame);

    for (int i = 0; i < 9; i++)  { CHECK_BYTE(frame, i, 0x00); }
    CHECK_BYTE(frame, 9, 1);
    for (int i = 10; i < 64; i++) { CHECK_BYTE(frame, i, 0x00); }
}

static void test_parse_feedback_round_trips_pack_feedback(void)
{
    TEST_CASE("can_frame: parse_feedback correctly reads back pack_feedback's output");
    me_can_feedback_t fb_in = { .state = ME_CAN_STATE_CHA,
                               .feedback_voltage = 14.1f,
                               .feedback_current = 9.5f };
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_feedback(3, &fb_in, frame);

    me_can_feedback_t fb_out;
    CHECK(me_can_parse_feedback(frame, 3, &fb_out));
    CHECK_EQ_U(ME_CAN_STATE_CHA, fb_out.state);
    CHECK(fb_out.feedback_voltage > 14.099f && fb_out.feedback_voltage < 14.101f);
    CHECK(fb_out.feedback_current > 9.499f && fb_out.feedback_current < 9.501f);
}

static void test_parse_feedback_rejects_bad_channel(void)
{
    TEST_CASE("can_frame: parse_feedback rejects a channel number outside 1..8");
    uint8_t frame[ME_CAN_FRAME_LEN] = { 0 };
    me_can_feedback_t fb;
    CHECK(!me_can_parse_feedback(frame, 0, &fb));
    CHECK(!me_can_parse_feedback(frame, 9, &fb));
}

static void test_set_and_read_use_different_functions_same_shape(void)
{
    TEST_CASE("can_frame: a SET request's Set Current reads back via parse_feedback");
    /* Confirms the request/response layout really is identical - the
     * fabricator (Task 9) relies on decoding a REQUEST with
     * me_can_parse_feedback(). */
    me_can_setpoint_t sp = { .channel_num = 2, .command = ME_CAN_CMD_CHA,
                             .set_voltage = 0.0f, .set_current = 5.0f };
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, frame);

    me_can_feedback_t decoded_request;
    CHECK(me_can_parse_feedback(frame, 2, &decoded_request));
    CHECK_EQ_U(ME_CAN_CMD_CHA, decoded_request.state); /* Command, read as "state" */
    CHECK(decoded_request.feedback_current > 4.999f &&
          decoded_request.feedback_current < 5.001f);  /* Set Current, read as "feedback" */
}
```

Add all five calls to `run_can_frame_tests()`.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: fails to compile — `me_can_pack_set` etc. are declared but not defined.

- [ ] **Step 3: Implement the packing and parsing functions**

Append to `can_frame.c`:

```c
#include <string.h>

static void put_f32_le(uint8_t *dst, float v)
{
    uint32_t bits;
    memcpy(&bits, &v, sizeof(bits));
    dst[0] = (uint8_t)(bits);
    dst[1] = (uint8_t)(bits >> 8);
    dst[2] = (uint8_t)(bits >> 16);
    dst[3] = (uint8_t)(bits >> 24);
}

static float get_f32_le(const uint8_t *src)
{
    const uint32_t bits = (uint32_t)src[0] | ((uint32_t)src[1] << 8)
                        | ((uint32_t)src[2] << 16) | ((uint32_t)src[3] << 24);
    float v;
    memcpy(&v, &bits, sizeof(v));
    return v;
}

void me_can_pack_set(const me_can_setpoint_t *sp, uint8_t out64[ME_CAN_FRAME_LEN])
{
    memset(out64, 0, ME_CAN_FRAME_LEN);

    uint8_t slot;
    (void)me_can_block_for_channel(sp->channel_num, &slot);
    uint8_t *p = &out64[slot * ME_CAN_SLOT_LEN];

    put_f32_le(&p[0], sp->set_voltage);
    put_f32_le(&p[4], sp->set_current);
    p[8] = sp->command;
    p[9] = sp->channel_num;
    /* +10 EEP Para# (Normal Mode = 0), +11 Reserved, +12..15 Data - all
     * zero this iteration; nothing reads or writes an internal parameter. */
}

void me_can_pack_read(uint8_t channel_num, uint8_t out64[ME_CAN_FRAME_LEN])
{
    memset(out64, 0, ME_CAN_FRAME_LEN);

    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    uint8_t *p = &out64[slot * ME_CAN_SLOT_LEN];

    p[9] = channel_num;
}

void me_can_pack_feedback(uint8_t channel_num, const me_can_feedback_t *fb,
                          uint8_t out64[ME_CAN_FRAME_LEN])
{
    memset(out64, 0, ME_CAN_FRAME_LEN);

    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    uint8_t *p = &out64[slot * ME_CAN_SLOT_LEN];

    put_f32_le(&p[0], fb->feedback_voltage);
    put_f32_le(&p[4], fb->feedback_current);
    p[8] = fb->state;
    p[9] = channel_num;
}

bool me_can_parse_feedback(const uint8_t frame64[ME_CAN_FRAME_LEN],
                           uint8_t channel_num, me_can_feedback_t *out)
{
    if (channel_num < 1u || channel_num > 8u) { return false; }

    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    const uint8_t *p = &frame64[slot * ME_CAN_SLOT_LEN];

    out->feedback_voltage = get_f32_le(&p[0]);
    out->feedback_current = get_f32_le(&p[4]);
    out->state             = p[8];
    return true;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/proto/can_frame.c me-primary/tests/test_can_frame.c
git commit -m "Implement CAN-FD frame packing and feedback parsing (little-endian)"
```

---

## Task 6: `step_engine` — types and step entry (`SET` → `CCChg`)

**Files:**
- Create: `me-primary/src/exec/step_engine.h`
- Create: `me-primary/src/exec/step_engine.c`
- Create: `me-primary/tests/test_step_engine.c`
- Modify: `me-primary/tests/test_util.h` (add `void run_step_engine_tests(void);`)

**Interfaces:**
- Consumes: `me_step_decode()`, `me_can_pack_set()`, `me_can_id()` (Tasks 1–5); `me_chain_fetch_step()`, `ME_CIRCUIT_SECONDARY()`/`ME_CIRCUIT_CHANNEL()` (existing)
- Produces: `me_exec_ctx_t`, `me_exec_output_t`, `me_exec_start()`, `me_exec_tick()`, `me_exec_state_name()` — consumed by Task 11 (`core_logic.c`)

**Plan decision:** the design doc's `me_exec_tick()` signature implies one
output per call. Two of this iteration's own outputs can legitimately be due
in the same millisecond (a `READ_VALUES` poll every 100 ms and a `0xCC` emit
every 1000 ms coincide every 10th poll). This task gives the context a small
internal FIFO (`ME_EXEC_OUTPUT_QUEUE_LEN = 4`) that `me_exec_tick()` drains
one entry per call — callers loop until they see `ME_EXEC_OUT_NONE`, the
same pattern already used for draining a message queue elsewhere in this
codebase.

- [ ] **Step 1: Write the failing test**

```c
/* me-primary/tests/test_step_engine.c */
#include <string.h>

#include "test_util.h"
#include "../src/exec/step_engine.h"
#include "../src/proto/program_chain.h"
#include "../src/proto/realtime_frame.h"

/* Writes a chain-wrapped step. Mirrors test_step_decode.c's put_wrapped(). */
static uint32_t put_step(uint8_t *buf, uint32_t off, uint32_t next,
                         uint16_t step_no, const uint8_t *body, uint32_t body_len)
{
    uint32_t i = off;
    buf[i++] = ME_STEP_START_1;
    buf[i++] = ME_STEP_START_2;
    buf[i++] = (uint8_t)(next >> 24);
    buf[i++] = (uint8_t)(next >> 16);
    buf[i++] = (uint8_t)(next >> 8);
    buf[i++] = (uint8_t)(next);
    buf[i++] = (uint8_t)(step_no >> 8);
    buf[i++] = (uint8_t)(step_no);
    memcpy(&buf[i], body, body_len);
    i += body_len;
    buf[i++] = ME_STEP_END_1;
    buf[i++] = ME_STEP_END_2;
    return i - off;
}

/* A 3-step SET -> CCChg(10A, >10000ms) -> STOP program, chain-linked. */
static uint32_t build_reference_program(uint8_t *buf)
{
    uint8_t set_body[]   = { 0x0A, 0x00, 0x01, 0xFF };
    uint8_t ccchg_body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x51, 0x00, 0x00, 0x27, 0x10, 0x00,
        0x00,
    };
    uint8_t stop_body[] = { 0x0B };

    uint32_t off = 0;
    const uint32_t len1 = put_step(buf, off, 0, 1, set_body, sizeof(set_body));
    /* fix up next-index fields now that lengths are known */
    off += len1;
    const uint32_t len2 = put_step(buf, off, 0, 2, ccchg_body, sizeof(ccchg_body));
    off += len2;
    const uint32_t len3 = put_step(buf, off, ME_STEP_TERMINATOR, 3, stop_body, sizeof(stop_body));
    off += len3;

    /* Patch nextIndex now that offsets are known (put_step wrote 0 as a
     * placeholder for steps 1 and 2). */
    uint32_t o = 0;
    buf[o + 2] = (uint8_t)((len1) >> 24); buf[o + 3] = (uint8_t)((len1) >> 16);
    buf[o + 4] = (uint8_t)((len1) >> 8);  buf[o + 5] = (uint8_t)(len1);
    o += len1;
    const uint32_t next2 = len1 + len2;
    buf[o + 2] = (uint8_t)(next2 >> 24); buf[o + 3] = (uint8_t)(next2 >> 16);
    buf[o + 4] = (uint8_t)(next2 >> 8);  buf[o + 5] = (uint8_t)(next2);

    return off; /* off already includes len1+len2+len3 - do NOT add len3 again */
}

static void test_starting_on_a_set_step_immediately_enters_ccchg(void)
{
    TEST_CASE("step_engine: SET is zero-duration - start() lands directly on CCChg's CAN_SET");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);
    CHECK_BYTE(out.can_frame, 8, ME_CAN_CMD_CHA);

    /* Nothing else due at t=0. */
    me_exec_tick(&ctx, 0, &out);
    CHECK_EQ_U(ME_EXEC_OUT_NONE, out.kind);
}

void run_step_engine_tests(void)
{
    test_starting_on_a_set_step_immediately_enters_ccchg();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: fails to compile — `step_engine.h` does not exist.

- [ ] **Step 3: Create `step_engine.h`**

```c
/*
 * step_engine.h - per-circuit battery-testing program execution.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers, no clock reads.
 * `now_ms` is a PARAMETER everywhere - this is what makes a 10-second
 * cutoff provable in microseconds of host test time instead of discovered
 * on the board. See Docs/specs/2026-08-14-step-execution-design.md §3.3.
 */
#ifndef ME_STEP_ENGINE_H
#define ME_STEP_ENGINE_H

#include <stdbool.h>
#include <stdint.h>

#include "../proto/can_frame.h"
#include "../proto/step_decode.h"

typedef enum {
    ME_EXEC_IDLE = 0,
    ME_EXEC_RUNNING,
    ME_EXEC_STOPPED,
    ME_EXEC_CHANNEL_OFFLINE,
    ME_EXEC_DECODE_ERROR,
} me_exec_state_t;

typedef enum {
    ME_EXEC_OUT_NONE = 0,
    ME_EXEC_OUT_CAN_SET,  /* send a SET_VALUES frame, expect one response  */
    ME_EXEC_OUT_CAN_READ, /* send a READ_VALUES frame, expect one response */
    ME_EXEC_OUT_REALTIME, /* emit one 0xCC frame with the fields below     */
} me_exec_output_kind_t;

typedef struct {
    me_exec_output_kind_t kind;

    /* ME_EXEC_OUT_CAN_SET / ME_EXEC_OUT_CAN_READ */
    uint16_t can_id;
    uint8_t  can_frame[ME_CAN_FRAME_LEN];

    /* ME_EXEC_OUT_REALTIME */
    uint16_t step_number;
    uint8_t  program_running; /* ME_RT_PROGRAM_*, see realtime_frame.h      */
    uint8_t  circuit_status;  /* ME_RT_CIRCUIT_*, see realtime_frame.h     */
    uint32_t step_run_ms;
    uint32_t program_run_ms;
    float    current;
    float    voltage;
    uint8_t  operator_code;
} me_exec_output_t;

#define ME_EXEC_POLL_PERIOD_MS       100u
#define ME_EXEC_REALTIME_PERIOD_MS  1000u
#define ME_EXEC_RESPONSE_TIMEOUT_MS  200u
#define ME_EXEC_RETRY_LIMIT            3u
#define ME_EXEC_OUTPUT_QUEUE_LEN       4u

typedef struct {
    me_exec_state_t state;
    uint8_t         circuit_id; /* ME CircuitID: secondary<<4 | channel */

    const uint8_t  *program;    /* caller-owned; never copied           */
    uint32_t        program_len;
    uint32_t        step_index; /* 1-based, matches me_chain_fetch_step() */

    me_step_t       current_step;
    uint32_t        last_tick_ms;
    uint32_t        step_run_ms;
    uint32_t        program_run_ms;

    bool                   response_outstanding;
    me_exec_output_kind_t  outstanding_kind;
    uint32_t               response_deadline_ms;
    uint8_t                missed_responses;

    uint32_t        next_poll_ms;
    uint32_t        next_realtime_ms;
    float           last_feedback_voltage;
    float           last_feedback_current;

    me_exec_output_t pending[ME_EXEC_OUTPUT_QUEUE_LEN];
    uint8_t          pending_head;
    uint8_t          pending_count;
} me_exec_ctx_t;

/* Starts execution of `program` (already validated complete by the chain
 * walker) on `ctx`, from step 1, at engine time now_ms. `program` must
 * outlive `ctx`. */
void me_exec_start(me_exec_ctx_t *ctx, uint8_t circuit_id, const uint8_t *program,
                   uint32_t program_len, uint32_t now_ms);

/*
 * Advances the engine to now_ms and fills *out with at most ONE thing to
 * do. Call in a loop, draining ME_EXEC_OUT_NONE, until nothing is left -
 * two of this iteration's own outputs (a poll and a realtime emit) can
 * legitimately be due in the same millisecond.
 */
void me_exec_tick(me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out);

/* Response to whichever CAN frame is currently outstanding (SET or READ -
 * both share the same retry counter, per design doc §2.1). */
void me_exec_on_response(me_exec_ctx_t *ctx, const me_can_feedback_t *fb, uint32_t now_ms);

/*
 * The outstanding CAN frame got no response within ME_EXEC_RESPONSE_TIMEOUT_MS.
 * me_exec_tick() calls this internally once the deadline passes, so callers
 * do not need their own timeout bookkeeping - it is exposed publicly only
 * so a host test can trigger it directly without stepping through many
 * 10 ms ticks.
 */
void me_exec_on_response_timeout(me_exec_ctx_t *ctx, uint32_t now_ms);

/* Sends a CMD_STO SET_VALUES frame and transitions to ME_EXEC_STOPPED
 * immediately, regardless of program position. Used both by the program's
 * own STOP operator and by an external STOP command (0xEE Q2). Idempotent. */
void me_exec_force_stop(me_exec_ctx_t *ctx, uint32_t now_ms);

const char *me_exec_state_name(me_exec_state_t s);

#endif /* ME_STEP_ENGINE_H */
```

- [ ] **Step 4: Create `step_engine.c` — `me_exec_start()`, step entry for `SET`/`CCChg`, and a minimal `me_exec_tick()`**

```c
/* step_engine.c - see step_engine.h. */
#include "step_engine.h"

#include <string.h>

#include "../proto/program_chain.h"
#include "../proto/proto_defs.h"
#include "../proto/realtime_frame.h"
#include "../util/log.h"

const char *me_exec_state_name(me_exec_state_t s)
{
    switch (s) {
    case ME_EXEC_IDLE:            return "IDLE";
    case ME_EXEC_RUNNING:         return "RUNNING";
    case ME_EXEC_STOPPED:         return "STOPPED";
    case ME_EXEC_CHANNEL_OFFLINE: return "CHANNEL_OFFLINE";
    case ME_EXEC_DECODE_ERROR:    return "DECODE_ERROR";
    default:                      return "UNKNOWN";
    }
}

static void enqueue(me_exec_ctx_t *ctx, const me_exec_output_t *o)
{
    if (ctx->pending_count >= ME_EXEC_OUTPUT_QUEUE_LEN) {
        ME_LOGE("step_engine: circuit 0x%02X - output queue full, dropping "
                "an output (kind %d)", ctx->circuit_id, (int)o->kind);
        return;
    }
    const uint8_t slot = (uint8_t)((ctx->pending_head + ctx->pending_count) %
                                   ME_EXEC_OUTPUT_QUEUE_LEN);
    ctx->pending[slot] = *o;
    ctx->pending_count++;
}

static bool dequeue(me_exec_ctx_t *ctx, me_exec_output_t *out)
{
    if (ctx->pending_count == 0u) { return false; }
    *out = ctx->pending[ctx->pending_head];
    ctx->pending_head = (uint8_t)((ctx->pending_head + 1u) % ME_EXEC_OUTPUT_QUEUE_LEN);
    ctx->pending_count--;
    return true;
}

static void build_can_output(me_exec_ctx_t *ctx, me_exec_output_kind_t kind,
                             uint8_t function5, const uint8_t frame64[ME_CAN_FRAME_LEN],
                             uint32_t now_ms)
{
    me_exec_output_t out;
    memset(&out, 0, sizeof(out));
    out.kind   = kind;
    out.can_id = me_can_id(ME_CIRCUIT_SECONDARY(ctx->circuit_id), function5);
    memcpy(out.can_frame, frame64, ME_CAN_FRAME_LEN);
    enqueue(ctx, &out);

    ctx->response_outstanding = true;
    ctx->outstanding_kind     = kind;
    ctx->response_deadline_ms = now_ms + ME_EXEC_RESPONSE_TIMEOUT_MS;
}

static void send_setpoint(me_exec_ctx_t *ctx, uint8_t command, float current_a,
                          uint32_t now_ms)
{
    me_can_setpoint_t sp;
    sp.channel_num = ME_CIRCUIT_CHANNEL(ctx->circuit_id);
    sp.command     = command;
    sp.set_voltage = 0.0f;
    sp.set_current = current_a;

    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, frame);
    build_can_output(ctx, ME_EXEC_OUT_CAN_SET, ME_CAN_FUNC_SET, frame, now_ms);
}

/*
 * NOTE: send_poll() is NOT defined in this task, even though it is a
 * natural sibling of send_setpoint() above. This build compiles with
 * -Werror, and Task 6's me_exec_tick() never calls it - an unused static
 * function is a hard build failure here, not a warning to suppress. It is
 * added in Task 7, which is where it is FIRST called (from
 * me_exec_on_response_timeout()'s retry path).
 */

static void enqueue_realtime(me_exec_ctx_t *ctx, bool final_idle)
{
    me_exec_output_t out;
    memset(&out, 0, sizeof(out));
    out.kind            = ME_EXEC_OUT_REALTIME;
    out.step_number     = ctx->current_step.step_number;
    out.program_running = final_idle ? ME_RT_PROGRAM_IDLE : ME_RT_PROGRAM_RUNNING;
    out.circuit_status  = final_idle ? ME_RT_CIRCUIT_IDLE  : ME_RT_CIRCUIT_CHARGE;
    out.step_run_ms     = ctx->step_run_ms;
    out.program_run_ms  = ctx->program_run_ms;
    out.current         = final_idle ? 0.0f : ctx->last_feedback_current;
    out.voltage         = final_idle ? 0.0f : ctx->last_feedback_voltage;
    out.operator_code   = (uint8_t)ctx->current_step.operator;
    enqueue(ctx, &out);
}

void me_exec_force_stop(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    if (ctx->state == ME_EXEC_STOPPED) { return; } /* idempotent */
    send_setpoint(ctx, ME_CAN_CMD_STO, 0.0f, now_ms);
    enqueue_realtime(ctx, true);
    ctx->state = ME_EXEC_STOPPED;
}

/* Fetches and decodes the step at ctx->step_index, then dispatches on its
 * operator. Recurses (bounded by program size) through zero-duration SET
 * steps so the caller always sees the effect of entering the next
 * CAN-producing or terminal step. Returns false on any failure. */
static bool enter_step(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    const uint8_t *step_ptr = NULL;
    uint32_t       step_len = 0;
    const me_chain_result_t cr = me_chain_fetch_step(
        ctx->program, ctx->program_len, ctx->step_index, &step_ptr, &step_len);
    if (cr != ME_CHAIN_OK) {
        ME_LOGE("step_engine: circuit 0x%02X - cannot fetch step %u: %s",
                ctx->circuit_id, (unsigned)ctx->step_index, me_chain_result_name(cr));
        return false;
    }

    me_step_t decoded;
    const me_step_decode_result_t dr = me_step_decode(step_ptr, step_len, &decoded);
    if (dr != ME_STEP_DECODE_OK) {
        ME_LOGE("step_engine: circuit 0x%02X - step %u decode failed: %s",
                ctx->circuit_id, (unsigned)ctx->step_index,
                me_step_decode_result_name(dr));
        return false;
    }

    ctx->current_step = decoded;
    ctx->step_run_ms  = 0u;
    ctx->last_tick_ms = now_ms;

    switch (decoded.operator) {
    case ME_OP_SET:
        ctx->step_index++;
        return enter_step(ctx, now_ms);

    case ME_OP_CCCHG:
        send_setpoint(ctx, ME_CAN_CMD_CHA, decoded.nominal_current_a, now_ms);
        ctx->next_poll_ms     = now_ms + ME_EXEC_POLL_PERIOD_MS;
        ctx->next_realtime_ms = now_ms + ME_EXEC_REALTIME_PERIOD_MS;
        ctx->state            = ME_EXEC_RUNNING;
        return true;

    case ME_OP_STOP:
        me_exec_force_stop(ctx, now_ms);
        return true;

    default:
        ME_LOGE("step_engine: circuit 0x%02X - step %u decoded to an "
                "unhandled operator (bug)", ctx->circuit_id,
                (unsigned)ctx->step_index);
        return false;
    }
}

void me_exec_start(me_exec_ctx_t *ctx, uint8_t circuit_id, const uint8_t *program,
                   uint32_t program_len, uint32_t now_ms)
{
    memset(ctx, 0, sizeof(*ctx));
    ctx->circuit_id   = circuit_id;
    ctx->program      = program;
    ctx->program_len  = program_len;
    ctx->step_index   = 1u;
    ctx->last_tick_ms = now_ms;

    if (!enter_step(ctx, now_ms)) {
        ctx->state = ME_EXEC_DECODE_ERROR;
    }
}

void me_exec_tick(me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out)
{
    memset(out, 0, sizeof(*out));
    if (dequeue(ctx, out)) { return; }

    if (ctx->state != ME_EXEC_RUNNING) { return; }

    /* Cutoff evaluation, polling, and the response-timeout/retry path are
     * added in Tasks 7-8. For now, RUNNING never advances on its own. */
    (void)now_ms;
}

void me_exec_on_response(me_exec_ctx_t *ctx, const me_can_feedback_t *fb, uint32_t now_ms)
{
    if (ctx->state != ME_EXEC_RUNNING) { return; }

    ctx->last_feedback_voltage = fb->feedback_voltage;
    ctx->last_feedback_current = fb->feedback_current;
    ctx->response_outstanding  = false;
    ctx->missed_responses      = 0u;
    ctx->last_tick_ms          = now_ms;
}

void me_exec_on_response_timeout(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    /* Full retry/offline logic added in Task 7. */
    (void)ctx; (void)now_ms;
}
```

- [ ] **Step 5: Wire the test into the runner**

Same pattern as Task 1 Step 5, for `run_step_engine_tests`. Add to
`build-native.ps1`'s `$sources`:

```
    "$PSScriptRoot\src\exec\step_engine.c"
```
```
    "$PSScriptRoot\tests\test_step_engine.c"
```

Note: `step_engine.c` is the first source under a new `src/exec/` directory —
create it as part of this step (`New-Item` is not required; `Write` a file
into a not-yet-existing directory creates it automatically).

- [ ] **Step 6: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`.

- [ ] **Step 7: Commit**

```bash
git add me-primary/src/exec/step_engine.h me-primary/src/exec/step_engine.c \
        me-primary/tests/test_step_engine.c me-primary/tests/test_util.h \
        me-primary/tests/test_main.c me-primary/build-native.ps1
git commit -m "Add step_engine: SET zero-duration entry into CCChg's CAN_SET output"
```

---

## Task 7: `step_engine` — response handling, retry, and channel-offline

**Files:**
- Modify: `me-primary/src/exec/step_engine.c`
- Modify: `me-primary/tests/test_step_engine.c`

**Interfaces:**
- Consumes: nothing new
- Produces: working `me_exec_on_response_timeout()` (called internally by `me_exec_tick()`)

- [ ] **Step 1: Write the failing tests**

Append to `test_step_engine.c`:

```c
static void test_missed_responses_retry_then_go_offline(void)
{
    TEST_CASE("step_engine: 3 consecutive missed responses reach CHANNEL_OFFLINE");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* drain the initial CAN_SET from entering CCChg */
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);

    /* Three timeouts, 200ms apart, with a retry (another CAN_SET) each time
     * except the third, which goes offline instead. */
    uint32_t t = 200;
    me_exec_tick(&ctx, t, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind); /* retry 1 */
    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    t += 200;
    me_exec_tick(&ctx, t, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind); /* retry 2 */
    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    t += 200;
    me_exec_tick(&ctx, t, &out);
    CHECK_EQ_U(ME_EXEC_CHANNEL_OFFLINE, ctx.state);
}

static void test_a_response_clears_the_missed_counter(void)
{
    TEST_CASE("step_engine: a response before the deadline clears missed_responses");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* the initial CAN_SET */

    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA,
                             .feedback_voltage = 12.0f, .feedback_current = 10.0f };
    me_exec_on_response(&ctx, &fb, 50);

    CHECK_EQ_U(0, ctx.missed_responses);
    CHECK(!ctx.response_outstanding);
}

static void test_time_does_not_accrue_while_a_response_is_outstanding(void)
{
    TEST_CASE("step_engine: step_run_ms is frozen while a CAN response is outstanding");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* CAN_SET sent, response_outstanding = true */

    /* Advance 5000 real ms with no response - step_run_ms must not move,
     * mirroring the old firmware's own step-timer freeze behaviour. */
    me_exec_tick(&ctx, 5000, &out);
    CHECK_EQ_U(0, ctx.step_run_ms);
}

void run_step_engine_tests(void)
{
    test_starting_on_a_set_step_immediately_enters_ccchg();
    test_missed_responses_retry_then_go_offline();
    test_a_response_clears_the_missed_counter();
    test_time_does_not_accrue_while_a_response_is_outstanding();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: `test_missed_responses_retry_then_go_offline` fails (state never
changes; `me_exec_on_response_timeout()` is currently a no-op).

- [ ] **Step 3: Implement `send_poll()`, the timeout/retry path, and wire it into `me_exec_tick()`**

Add `send_poll()` to `step_engine.c`, just above `enqueue_realtime()` (this
is its first real use - Task 6 deliberately left it out to avoid an
unused-function build failure under `-Werror`):

```c
static void send_poll(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_read(ME_CIRCUIT_CHANNEL(ctx->circuit_id), frame);
    build_can_output(ctx, ME_EXEC_OUT_CAN_READ, ME_CAN_FUNC_READ, frame, now_ms);
}
```

Replace `me_exec_on_response_timeout()` in `step_engine.c`:

```c
void me_exec_on_response_timeout(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    if (ctx->state != ME_EXEC_RUNNING)  { return; }
    if (!ctx->response_outstanding)     { return; }
    if (now_ms < ctx->response_deadline_ms) { return; }

    ctx->missed_responses++;
    ctx->response_outstanding = false; /* the retry below re-arms it */
    ctx->last_tick_ms         = now_ms;

    if (ctx->missed_responses >= ME_EXEC_RETRY_LIMIT) {
        ME_LOGE("step_engine: circuit 0x%02X - %u consecutive missed CAN "
                "responses, marking channel offline", ctx->circuit_id,
                (unsigned)ctx->missed_responses);
        ctx->state = ME_EXEC_CHANNEL_OFFLINE;
        return;
    }

    ME_LOGW("step_engine: circuit 0x%02X - missed CAN response (%u/%u), "
            "retrying", ctx->circuit_id, (unsigned)ctx->missed_responses,
            (unsigned)ME_EXEC_RETRY_LIMIT);

    if (ctx->outstanding_kind == ME_EXEC_OUT_CAN_READ) {
        send_poll(ctx, now_ms);
    } else {
        send_setpoint(ctx, ME_CAN_CMD_CHA, ctx->current_step.nominal_current_a, now_ms);
    }
}
```

Update `me_exec_tick()` to call it and re-drain before the state check:

```c
void me_exec_tick(me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out)
{
    memset(out, 0, sizeof(*out));
    if (dequeue(ctx, out)) { return; }

    me_exec_on_response_timeout(ctx, now_ms); /* no-op unless a deadline just passed */
    if (dequeue(ctx, out)) { return; }         /* a retry may have just enqueued output */

    if (ctx->state != ME_EXEC_RUNNING) { return; }

    /* Cutoff evaluation and polling are added in Task 8. */
    (void)now_ms;
}
```

Add the freeze-while-outstanding time accounting used by Task 8, now, since
the test in this task already exercises it:

```c
static void advance_time(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    if (!ctx->response_outstanding) {
        const uint32_t delta = now_ms - ctx->last_tick_ms;
        ctx->step_run_ms    += delta;
        ctx->program_run_ms += delta;
    }
    ctx->last_tick_ms = now_ms;
}
```

Call it from `me_exec_tick()`, right after the `state != RUNNING` check:

```c
    if (ctx->state != ME_EXEC_RUNNING) { return; }

    advance_time(ctx, now_ms);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/exec/step_engine.c me-primary/tests/test_step_engine.c
git commit -m "Add CAN response timeout/retry with 3-strike channel-offline detection"
```

---

## Task 8: `step_engine` — TIME cutoff, polling/realtime scheduling, advance to `STOP`

**Files:**
- Modify: `me-primary/src/exec/step_engine.c`
- Modify: `me-primary/tests/test_step_engine.c`

**Interfaces:**
- Consumes: nothing new
- Produces: a fully working `me_exec_tick()` — the complete engine, ready for Task 11's wiring

- [ ] **Step 1: Write the failing tests**

Append to `test_step_engine.c`:

```c
static void test_cutoff_fires_at_exactly_the_configured_ms(void)
{
    TEST_CASE("step_engine: a 10000ms cutoff fires at tick 10000, not 9990 or 10010");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program); /* CCChg cutoff = 10000ms, '>' */

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA, .feedback_voltage = 12.0f,
                             .feedback_current = 10.0f };
    me_exec_output_t out;

    me_exec_tick(&ctx, 0, &out); /* drain entry CAN_SET */
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);
    me_exec_on_response(&ctx, &fb, 0);

    /*
     * Tick every 100ms up to and including 10000ms, answering every poll
     * along the way - this mirrors how Core Logic actually drives the
     * engine (a steady 10ms tick; see Task 11), not a single large jump.
     *
     * This matters structurally, not just for test realism: next_poll_ms
     * advances by a FIXED period each time a poll fires (see Task 8's
     * me_exec_tick()), it does not snap forward to "catch up" to now_ms.
     * A test - or a caller - that jumps now_ms far ahead in one call would
     * see an unexpected poll fire on that same call, because
     * now_ms >= next_poll_ms stays true from a stale, unadvanced
     * next_poll_ms. Ticking at least as often as ME_EXEC_POLL_PERIOD_MS,
     * exactly as Task 11's fixed 10ms loop does, is what keeps this from
     * happening in production.
     */
    for (uint32_t t = 100; t <= 10000; t += 100) {
        me_exec_tick(&ctx, t, &out);
        if (out.kind == ME_EXEC_OUT_CAN_READ) {
            me_exec_on_response(&ctx, &fb, t);
            me_exec_tick(&ctx, t, &out); /* drains a coincident realtime emit, if any */
        }
    }

    /* '>' means the cutoff has NOT fired at exactly 10000ms - only past it. */
    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    /* One tick later, it fires: the step advances into STOP, which sends a
     * CMD_STO CAN_SET immediately. */
    me_exec_tick(&ctx, 10001, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);
    CHECK_BYTE(out.can_frame, 8, ME_CAN_CMD_STO);
    CHECK_EQ_U(ME_EXEC_STOPPED, ctx.state);
}

static void test_full_sequence_reaches_stopped_and_emits_final_idle_frame(void)
{
    TEST_CASE("step_engine: SET->CCChg->STOP reaches STOPPED with a final Idle 0xCC");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* CAN_SET into CCChg */
    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA, .feedback_voltage = 12.0f,
                             .feedback_current = 10.0f };
    me_exec_on_response(&ctx, &fb, 0);

    me_exec_tick(&ctx, 10001, &out); /* cutoff fires -> STOP's CAN_SET */
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);

    me_exec_tick(&ctx, 10001, &out); /* the final Idle 0xCC, queued alongside it */
    CHECK_EQ_U(ME_EXEC_OUT_REALTIME, out.kind);
    CHECK_EQ_U(ME_RT_PROGRAM_IDLE, out.program_running);
    CHECK_EQ_U(ME_RT_CIRCUIT_IDLE, out.circuit_status);

    CHECK_EQ_U(ME_EXEC_STOPPED, ctx.state);
}

static void test_poll_and_realtime_coincide_every_tenth_poll(void)
{
    TEST_CASE("step_engine: a poll and a realtime emit can both be due in one tick");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* entry CAN_SET */
    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA, .feedback_voltage = 12.0f,
                             .feedback_current = 10.0f };
    me_exec_on_response(&ctx, &fb, 0);

    /* next_poll_ms = 100, next_realtime_ms = 1000 (both armed at step entry).
     * Neither is due before 100ms. */
    me_exec_tick(&ctx, 50, &out);
    CHECK_EQ_U(ME_EXEC_OUT_NONE, out.kind);

    /* At 1000ms exactly, poll #10 AND the first realtime emit both fire. */
    me_exec_tick(&ctx, 1000, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_READ, out.kind);
    me_exec_on_response(&ctx, &fb, 1000);

    me_exec_tick(&ctx, 1000, &out);
    CHECK_EQ_U(ME_EXEC_OUT_REALTIME, out.kind);
}

void run_step_engine_tests(void)
{
    test_starting_on_a_set_step_immediately_enters_ccchg();
    test_missed_responses_retry_then_go_offline();
    test_a_response_clears_the_missed_counter();
    test_time_does_not_accrue_while_a_response_is_outstanding();
    test_cutoff_fires_at_exactly_the_configured_ms();
    test_full_sequence_reaches_stopped_and_emits_final_idle_frame();
    test_poll_and_realtime_coincide_every_tenth_poll();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd me-primary; .\build-native.ps1`
Expected: the three new cutoff/poll/realtime tests fail — `me_exec_tick()`
still does nothing once `RUNNING`.

- [ ] **Step 3: Implement the cutoff check and poll/realtime scheduling**

Add above `me_exec_tick()` in `step_engine.c`:

```c
static bool cutoff_due(const me_step_t *step, uint32_t step_run_ms)
{
    if (!step->has_cutoff) { return false; }
    return step->cutoff_inclusive ? (step_run_ms >= step->cutoff_time_ms)
                                  : (step_run_ms >  step->cutoff_time_ms);
}
```

Replace the body of `me_exec_tick()`:

```c
void me_exec_tick(me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out)
{
    memset(out, 0, sizeof(*out));
    if (dequeue(ctx, out)) { return; }

    me_exec_on_response_timeout(ctx, now_ms);
    if (dequeue(ctx, out)) { return; }

    if (ctx->state != ME_EXEC_RUNNING) { return; }

    advance_time(ctx, now_ms);

    if (cutoff_due(&ctx->current_step, ctx->step_run_ms)) {
        ctx->step_index++;
        if (!enter_step(ctx, now_ms)) {
            ctx->state = ME_EXEC_DECODE_ERROR;
        }
        (void)dequeue(ctx, out);
        return;
    }

    if (!ctx->response_outstanding && now_ms >= ctx->next_poll_ms) {
        send_poll(ctx, now_ms);
        ctx->next_poll_ms += ME_EXEC_POLL_PERIOD_MS;
    }
    if (now_ms >= ctx->next_realtime_ms) {
        enqueue_realtime(ctx, false);
        ctx->next_realtime_ms += ME_EXEC_REALTIME_PERIOD_MS;
    }

    (void)dequeue(ctx, out);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd me-primary; .\build-native.ps1`
Expected: all checks pass, `RESULT: PASS`. This is the last host-testable
piece of the engine — from here, every remaining task is Linux-only wiring,
verified by cross-build and, ultimately, the developer's hardware run.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/exec/step_engine.c me-primary/tests/test_step_engine.c
git commit -m "Add TIME cutoff evaluation and poll/realtime scheduling to step_engine"
```

---

## Task 9: `can_mgr.c` — fabricated `SET_VALUES`/`READ_VALUES` responder

**Files:**
- Modify: `me-primary/src/threads/can_mgr.c`

**Interfaces:**
- Consumes: `me_can_block_for_channel()`, `me_can_parse_feedback()`, `me_can_pack_feedback()`, `ME_CAN_FUNC_*`, `ME_CAN_STATE_*` (Tasks 4–5); `me_circuit_slot()`, `ME_CIRCUIT_CHANNEL()` (existing)
- Produces: `ME_MSG_CAN_DATA` responses on `g_q_core` — consumed by Task 11

This task is **Linux-only** (thread + message-queue code); it is not built
by `build-native.ps1` and has no host unit test. Verification is the
cross-build in Step 3, plus a manual read-through against the design doc's
§2.1 request/response rule.

- [ ] **Step 1: Replace `can_mgr.c`'s body**

```c
/*
 * can_mgr.c - the CAN Data Manager thread.
 *
 * No physical CAN device exists yet. This thread decodes the SET_VALUES /
 * READ_VALUES frames Core Logic sends and fabricates the Secondary's
 * response instead of talking to real hardware. Per the developer's
 * decision (Docs/specs/2026-08-14-step-execution-design.md §2.1/§5): the
 * fabricated feedback echoes the last commanded setpoint with a slowly
 * drifting voltage, so the numbers the Web Application displays visibly
 * correlate with the program rather than being fixed constants or an
 * independent ramp.
 */
#include "can_mgr.h"

#include <pthread.h>
#include <string.h>

#include "../app_queues.h"
#include "../proto/can_frame.h"
#include "../proto/proto_defs.h"
#include "../store/circuit_store.h"
#include "../util/log.h"

static me_msg_t  s_rx;
static me_msg_t  s_tx;
static pthread_t s_thread;
static bool      s_started = false;

typedef struct {
    bool  has_setpoint;
    float last_current;
    float voltage;
} can_sim_t;

static can_sim_t s_sim[ME_MAX_CIRCUITS];

#define ME_CAN_SIM_VOLTAGE_START 12.0f
#define ME_CAN_SIM_VOLTAGE_STEP   0.001f /* per READ poll - a slow, visible drift */
#define ME_CAN_SIM_VOLTAGE_MAX   14.4f

static void send_response(uint8_t circuit_id, const me_can_feedback_t *fb)
{
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_feedback(ME_CIRCUIT_CHANNEL(circuit_id), fb, frame);

    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_CAN_DATA;
    s_tx.circuit_id = circuit_id;
    s_tx.len        = ME_CAN_FRAME_LEN;
    memcpy(s_tx.payload, frame, ME_CAN_FRAME_LEN);

    if (!me_msgq_send(&g_q_core, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
        ME_LOGW("can: circuit 0x%02X - fabricated response dropped, core "
                "queue full", circuit_id);
    }
}

static void handle_set_values(const me_msg_t *m)
{
    const int slot = me_circuit_slot(m->circuit_id);
    if (slot == ME_SLOT_INVALID || m->len != ME_CAN_FRAME_LEN) {
        ME_LOGW("can: circuit 0x%02X - malformed SET_VALUES (%u bytes)",
                m->circuit_id, (unsigned)m->len);
        return;
    }
    const uint8_t channel = ME_CIRCUIT_CHANNEL(m->circuit_id);

    /* Decodes the REQUEST's own fields - request and response share an
     * identical 64-byte layout, only the field NAMES differ by direction.
     * See me_can_pack_feedback()'s header comment. */
    me_can_feedback_t req;
    if (!me_can_parse_feedback(m->payload, channel, &req)) {
        ME_LOGW("can: circuit 0x%02X - bad channel number in SET_VALUES",
                m->circuit_id);
        return;
    }

    can_sim_t *sim = &s_sim[slot];
    if (!sim->has_setpoint) {
        sim->voltage      = ME_CAN_SIM_VOLTAGE_START;
        sim->has_setpoint = true;
    }
    sim->last_current = req.feedback_current; /* the request's Set Current */

    ME_LOGI("can: circuit 0x%02X - SET_VALUES applied: command 0x%02X, "
            "current %.3f A", m->circuit_id, (unsigned)req.state,
            (double)sim->last_current);

    me_can_feedback_t response;
    response.state            = req.state; /* echo Command back as STATE */
    response.feedback_voltage = sim->voltage;
    response.feedback_current = sim->last_current;
    send_response(m->circuit_id, &response);
}

static void handle_read_values(const me_msg_t *m)
{
    const int slot = me_circuit_slot(m->circuit_id);
    if (slot == ME_SLOT_INVALID || m->len != ME_CAN_FRAME_LEN) {
        ME_LOGW("can: circuit 0x%02X - malformed READ_VALUES (%u bytes)",
                m->circuit_id, (unsigned)m->len);
        return;
    }

    can_sim_t *sim = &s_sim[slot];
    if (!sim->has_setpoint) {
        ME_LOGW("can: circuit 0x%02X - READ_VALUES before any SET_VALUES, "
                "answering with zeros", m->circuit_id);
    } else if (sim->voltage < ME_CAN_SIM_VOLTAGE_MAX) {
        sim->voltage += ME_CAN_SIM_VOLTAGE_STEP;
    }

    me_can_feedback_t response;
    response.state            = sim->has_setpoint ? ME_CAN_STATE_CHA : ME_CAN_STATE_STO;
    response.feedback_voltage = sim->voltage;
    response.feedback_current = sim->last_current;
    send_response(m->circuit_id, &response);
}

static void *can_mgr_main(void *arg)
{
    (void)arg;
    ME_LOGI("can manager thread: started (SIMULATED - no CAN interface is opened)");

    memset(s_sim, 0, sizeof(s_sim));

    while (!me_app_stop_requested()) {
        if (!me_msgq_recv(&g_q_can, &s_rx, ME_RECV_TIMEOUT_MS)) {
            continue;
        }

        switch (s_rx.type) {
        case ME_MSG_CAN_TX: {
            const uint8_t function = (uint8_t)(s_rx.offset & 0x1Fu);
            if (function == ME_CAN_FUNC_SET) {
                handle_set_values(&s_rx);
            } else if (function == ME_CAN_FUNC_READ) {
                handle_read_values(&s_rx);
            } else {
                ME_LOGW("can: circuit 0x%02X - unexpected CAN function 0x%02X",
                        s_rx.circuit_id, (unsigned)function);
            }
            break;
        }

        default:
            ME_LOGW("can: unexpected message %s", me_msg_type_name(s_rx.type));
            break;
        }
    }

    ME_LOGI("can manager thread: stopped");
    return NULL;
}

bool me_can_mgr_start(void)
{
    const int rc = pthread_create(&s_thread, NULL, can_mgr_main, NULL);
    if (rc != 0) {
        ME_LOGE("can manager thread: pthread_create failed (%d)", rc);
        return false;
    }
    s_started = true;
    return true;
}

void me_can_mgr_join(void)
{
    if (s_started) {
        (void)pthread_join(s_thread, NULL);
        s_started = false;
    }
}
```

- [ ] **Step 2: Add the new sources to `build.ps1`**

Add to the `$sources` array in `build.ps1` (`can_frame.c` — `can_mgr.c` is
already listed):

```
    "$PSScriptRoot\src\proto\can_frame.c"
```

(`step_decode.c` and `step_engine.c` are added in Task 11's build-script edit,
alongside `core_logic.c`'s own dependency on them.)

- [ ] **Step 3: Run the cross-build to verify it compiles**

Run: `cd me-primary; .\build.ps1`
Expected: compiles clean under `-Werror`, produces a static aarch64 ELF.
**This proves compilation only — say "compiles clean," never "tested."**

- [ ] **Step 4: Commit**

```bash
git add me-primary/src/threads/can_mgr.c me-primary/build.ps1
git commit -m "Fabricate SET_VALUES/READ_VALUES responses in the CAN Data Manager"
```

---

## Task 10: Relocate the post-registration frame out of `demo_realtime`

**Files:**
- Create: `me-primary/src/threads/post_reg.h`
- Create: `me-primary/src/threads/post_reg.c`

**Interfaces:**
- Consumes: `me_realtime_pack_post_registration()` (existing, unchanged, `realtime_frame.h`)
- Produces: `me_post_reg_init()`, `me_post_reg_send()` — consumed by Task 12 (`main.c`, `comm_thread.c`)

This is the one piece of `demo_realtime.c` that survives its deletion — the
one-shot frame sent right after a successful `0xDD` registration, so the Web
Application has live data before step execution produces any. It is
Linux-only (touches the message queue); no host test.

- [ ] **Step 1: Create `post_reg.h`**

```c
/*
 * post_reg.h - the one-shot 0xCC frame sent right after registration.
 *
 * Relocated out of demo_realtime.c/.h, which is deleted in Task 12: this
 * function serves the registration flow, not step execution, and deleting
 * demo_realtime.* would otherwise silently remove a frame the Web
 * Application currently receives after every successful connect.
 */
#ifndef ME_POST_REG_H
#define ME_POST_REG_H

#include <stdint.h>

#include "../proto/crc16.h"

/* Must be called once at startup before me_post_reg_send(). */
void me_post_reg_init(uint8_t device_id, me_crc_order_t crc_order);

/*
 * Sends ONE 0xCC frame for a circuit that has just registered successfully.
 * Contents are fixed - step 1, temperature 25.0 C, everything else zero,
 * both status bytes Idle - built by me_realtime_pack_post_registration(),
 * where the exact bytes are unit-tested (test_realtime_frame.c).
 *
 * Called from the COMMUNICATION thread on every successful registration.
 */
void me_post_reg_send(uint8_t circuit_id);

#endif /* ME_POST_REG_H */
```

- [ ] **Step 2: Create `post_reg.c`**

```c
/* post_reg.c - see post_reg.h. */
#include "post_reg.h"

#include <string.h>

#include "../app_queues.h"
#include "../proto/realtime_frame.h"
#include "../store/circuit_store.h"
#include "../util/log.h"

static uint8_t        s_device_id = 0x01;
static me_crc_order_t s_crc_order = ME_CRC_ORDER_DEFAULT;
static me_msg_t       s_tx; /* owned by the Communication thread only */

void me_post_reg_init(uint8_t device_id, me_crc_order_t crc_order)
{
    s_device_id = device_id;
    s_crc_order = crc_order;
}

void me_post_reg_send(uint8_t circuit_id)
{
    if (me_circuit_slot(circuit_id) == ME_SLOT_INVALID) {
        ME_LOGW("post_reg: frame skipped - CircuitID 0x%02X is not a valid "
                "secondary(1-8)/channel(1-8) pair", circuit_id);
        return;
    }

    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_REALTIME_DATA;
    s_tx.circuit_id = circuit_id;
    s_tx.len        = (uint32_t)me_realtime_pack_post_registration(
        s_device_id, circuit_id, s_tx.payload, s_crc_order);

    if (!me_msgq_send(&g_q_comm, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
        ME_LOGW("post_reg: circuit 0x%02X post-registration frame dropped - "
                "comm queue full", circuit_id);
        return;
    }

    ME_LOGI("post_reg: circuit 0x%02X - sent one %u-byte 0xCC frame to UDP "
            "%u after registration (step %u, %.1f C, all else zero)",
            circuit_id, (unsigned)s_tx.len, (unsigned)ME_PORT_UDP_LIVE,
            (unsigned)ME_RT_POST_REG_STEP_NUMBER,
            (double)ME_RT_POST_REG_TEMPERATURE);
}
```

- [ ] **Step 3: Add the new source to `build.ps1`**

Add to the `$sources` array, after `demo_realtime.c` (which is removed in
Task 12 — leave both listed for now so this task's own build passes):

```
    "$PSScriptRoot\src\threads\post_reg.c"
```

- [ ] **Step 4: Run the cross-build to verify it compiles**

Run: `cd me-primary; .\build.ps1`
Expected: compiles clean (nothing calls `me_post_reg_*` yet, so no link
error, but note `post_reg.c`'s `me_post_reg_init`/`send` are as-yet-unused
from any call site — this is expected until Task 12 wires the call sites;
the cross-build does not warn on unused *external-linkage* functions).

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/threads/post_reg.h me-primary/src/threads/post_reg.c \
        me-primary/build.ps1
git commit -m "Relocate the post-registration frame sender out of demo_realtime"
```

---

## Task 11: Wire `step_engine` into `core_logic.c`

**Files:**
- Modify: `me-primary/src/threads/core_logic.c`

**Interfaces:**
- Consumes: `me_exec_start()`, `me_exec_tick()`, `me_exec_on_response()`, `me_exec_force_stop()`, `me_exec_state_name()`, `me_exec_output_t`/`me_exec_output_kind_t` (Tasks 6–8); `me_can_parse_feedback()` (Task 5); `me_realtime_pack()` (existing, `realtime_frame.h`)
- Produces: a real `me_execute_program()`; `ME_MSG_CAN_DATA` handling — this is the task that makes the feature observable end-to-end

This task is Linux-only; verified by cross-build (Step 6) and, ultimately,
`.\deploy.ps1` on hardware (outside this plan, per the design doc's testing
table).

- [ ] **Step 1: Update includes**

In `core_logic.c`, replace:

```c
#include "demo_realtime.h"
```

with:

```c
#include "../exec/step_engine.h"
#include "../proto/can_frame.h"
#include "../proto/realtime_frame.h"

#include <time.h>
```

- [ ] **Step 2: Add the per-circuit engine array and a `now_ms()` helper**

Add after the existing `static cl_circuit_t s_cl[ME_MAX_CIRCUITS];`:

```c
static me_exec_ctx_t s_exec[ME_MAX_CIRCUITS];

static uint32_t now_ms(void)
{
    struct timespec ts;
    (void)clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint32_t)(((uint64_t)ts.tv_sec * 1000u) + (uint64_t)(ts.tv_nsec / 1000000L));
}
```

- [ ] **Step 3: Replace `me_execute_program()` and its stale comment block**

Replace:

```c
/*
 * THE SEAM FOR REAL EXECUTION.
 *
 * When program step execution is written, this is the function that grows a
 * body and demo_realtime.c is deleted. Nothing else in Core Logic knows the
 * demo exists.
 */
static void me_execute_program(uint8_t circuit_id)
{
    me_demo_arm(circuit_id);
}
```

with:

```c
static void me_execute_program(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID) { return; } /* callers already validated this */
    me_exec_start(&s_exec[slot], circuit_id, g_cl_program[slot], s_cl[slot].len,
                 now_ms());
    ME_LOGI("core: circuit 0x%02X - execution started, engine state %s",
            circuit_id, me_exec_state_name(s_exec[slot].state));
}
```

- [ ] **Step 4: Add `dispatch_output()` and the engine service loop**

Add after `me_execute_program()`:

```c
static void dispatch_output(uint8_t circuit_id, const me_exec_output_t *out)
{
    switch (out->kind) {
    case ME_EXEC_OUT_CAN_SET:
    case ME_EXEC_OUT_CAN_READ:
        memset(&s_tx, 0, sizeof(s_tx));
        s_tx.type       = ME_MSG_CAN_TX;
        s_tx.circuit_id = circuit_id;
        s_tx.offset     = out->can_id; /* repurposed to carry the 11-bit CAN
                                        * ID, per the design doc §3 */
        s_tx.len        = ME_CAN_FRAME_LEN;
        memcpy(s_tx.payload, out->can_frame, ME_CAN_FRAME_LEN);
        if (!me_msgq_send(&g_q_can, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
            ME_LOGW("core: circuit 0x%02X - CAN frame dropped, can queue full",
                    circuit_id);
        }
        break;

    case ME_EXEC_OUT_REALTIME: {
        me_realtime_t rt;
        memset(&rt, 0, sizeof(rt));
        rt.step_number     = out->step_number;
        rt.program_running = out->program_running;
        rt.circuit_status  = out->circuit_status;
        rt.step_run_ms     = out->step_run_ms;
        rt.program_run_ms  = out->program_run_ms;
        rt.current         = out->current;
        rt.voltage         = out->voltage;
        rt.operator_code   = out->operator_code;

        memset(&s_tx, 0, sizeof(s_tx));
        s_tx.type       = ME_MSG_REALTIME_DATA;
        s_tx.circuit_id = circuit_id;
        s_tx.len        = (uint32_t)me_realtime_pack(&rt, s_sys->cfg.device_id,
                                                      circuit_id, s_tx.payload,
                                                      s_sys->cfg.crc_order);
        if (!me_msgq_send(&g_q_comm, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
            ME_LOGW("core: circuit 0x%02X - realtime frame dropped, comm "
                    "queue full", circuit_id);
        }
        break;
    }

    default:
        break;
    }
}

static void service_engines(void)
{
    const uint32_t t = now_ms();
    for (int slot = 0; slot < (int)ME_MAX_CIRCUITS; slot++) {
        me_exec_output_t out;
        for (;;) {
            me_exec_tick(&s_exec[slot], t, &out);
            if (out.kind == ME_EXEC_OUT_NONE) { break; }
            dispatch_output(me_circuit_from_slot(slot), &out);
        }
    }
}
```

**Plan decision:** `s_sys->cfg.device_id`/`.crc_order` must be reachable from
`sys_init.h`'s `me_config_t`. Check `sys_init.h:19-31` — both fields already
exist there (`device_id`, `crc_order`), so no change to `sys_init.h` is
needed.

- [ ] **Step 5: Wire `ME_MSG_CAN_DATA` handling and the `STOP`/`PAUSE`/`CONTINUE` control cases**

Replace the `case ME_MSG_CAN_DATA:` arm in `handle()`:

```c
    case ME_MSG_CAN_DATA: {
        const int slot = me_circuit_slot(m->circuit_id);
        if (slot == ME_SLOT_INVALID || m->len != ME_CAN_FRAME_LEN) {
            ME_LOGW("core: circuit 0x%02X - malformed CAN_DATA (%u bytes)",
                    m->circuit_id, (unsigned)m->len);
            break;
        }
        me_can_feedback_t fb;
        if (me_can_parse_feedback(m->payload, ME_CIRCUIT_CHANNEL(m->circuit_id), &fb)) {
            me_exec_on_response(&s_exec[slot], &fb, now_ms());
        }
        break;
    }
```

Replace the `case ME_CTRL_STOP:` arm in `handle_control()`:

```c
    case ME_CTRL_STOP:
        s_cl[slot].loading = false;
        me_exec_force_stop(&s_exec[slot], now_ms());
        break;
```

Replace the `ME_CTRL_PAUSE`/`ME_CTRL_CONTINUE` arm's condition:

```c
    case ME_CTRL_PAUSE:
    case ME_CTRL_CONTINUE:
        if (s_exec[slot].state == ME_EXEC_STOPPED) {
            ME_LOGW("core: circuit 0x%02X is STOPPED - %s ignored",
                    c.circuit_id, me_control_query_name(c.query_id));
        } else {
            ME_LOGW("core: circuit 0x%02X - %s is not implemented yet",
                    c.circuit_id, me_control_query_name(c.query_id));
        }
        break;
```

- [ ] **Step 6: Update `core_logic_main()` and `me_core_logic_start()`**

Replace `core_logic_main()`'s loop body:

```c
static void *core_logic_main(void *arg)
{
    (void)arg;
    ME_LOGI("core logic thread: started");

    while (!me_app_stop_requested()) {
        /* Fixed 10 ms tick, matching the engine's own timing budget
         * (Docs/specs/2026-08-14-step-execution-design.md §4). Simpler
         * than computing a dynamic "ms until next due" across circuits,
         * and cheap at this circuit count - revisit if this thread ever
         * becomes measurably hot at full 64-circuit scale. */
        if (me_msgq_recv(&g_q_core, &s_rx, 10)) {
            handle(&s_rx);
        }
        service_engines();
    }

    ME_LOGI("core logic thread: stopped");
    return NULL;
}
```

Add to `me_core_logic_start()`, alongside the existing `memset(s_cl, 0, sizeof(s_cl));`:

```c
    memset(s_exec, 0, sizeof(s_exec));
```

- [ ] **Step 7: Update `build.ps1`'s source list**

Add to the `$sources` array, and remove the `demo_realtime.c` line (deleted
in Task 12 — if Task 12 runs immediately after this one, do both edits
together; if not, leave `demo_realtime.c` listed until then, since
`core_logic.c` no longer references it either way once this step lands):

```
    "$PSScriptRoot\src\exec\step_engine.c"
    "$PSScriptRoot\src\proto\step_decode.c"
```

- [ ] **Step 8: Run the cross-build to verify it compiles**

Run: `cd me-primary; .\build.ps1`
Expected: compiles clean under `-Werror`, produces a static aarch64 ELF.
**Compiles clean — not tested.** End-to-end proof requires `.\deploy.ps1` on
hardware, which is the developer's step, outside this plan.

- [ ] **Step 9: Commit**

```bash
git add me-primary/src/threads/core_logic.c me-primary/build.ps1
git commit -m "Wire step_engine into core_logic.c: me_execute_program() is now real"
```

---

## Task 12: Delete `demo_realtime`, swap `main.c`/`comm_thread.c` to `post_reg`

**Files:**
- Modify: `me-primary/src/main.c`
- Modify: `me-primary/src/threads/comm_thread.c`
- Modify: `me-primary/build.ps1`
- Delete: `me-primary/src/threads/demo_realtime.c`
- Delete: `me-primary/src/threads/demo_realtime.h`

**Interfaces:**
- Consumes: `me_post_reg_init()`, `me_post_reg_send()` (Task 10)
- Produces: nothing new — this is cleanup that makes the deletion of `demo_realtime.*` compile-visible if any call site were missed, per its own header's stated intent ("a forgotten demo call should break the build rather than linger")

- [ ] **Step 1: Update `main.c`**

Replace:

```c
#include "threads/demo_realtime.h"
```

with:

```c
#include "threads/post_reg.h"
```

Replace:

```c
    me_demo_init(cfg.device_id, cfg.crc_order);
```

with:

```c
    me_post_reg_init(cfg.device_id, cfg.crc_order);
```

- [ ] **Step 2: Update `comm_thread.c`**

Replace:

```c
/* TEMPORARY - for the one post-registration demo frame. Delete this include when
 * demo_realtime.* goes. */
#include "demo_realtime.h"
```

with:

```c
#include "post_reg.h"
```

Replace:

```c
    me_demo_send_post_registration(sys->reg_request.circuit_id);
```

with:

```c
    me_post_reg_send(sys->reg_request.circuit_id);
```

- [ ] **Step 3: Delete `demo_realtime.c` and `demo_realtime.h`**

```bash
git rm me-primary/src/threads/demo_realtime.c me-primary/src/threads/demo_realtime.h
```

- [ ] **Step 4: Remove `demo_realtime.c` from `build.ps1`**

Remove the line:

```
    "$PSScriptRoot\src\threads\demo_realtime.c"
```

- [ ] **Step 5: Run the cross-build to verify it compiles**

Run: `cd me-primary; .\build.ps1`
Expected: compiles clean. If any reference to `me_demo_*` or
`demo_realtime.h` remains anywhere, this step fails loudly with an
unresolved symbol or missing header — that is the intended safety net.

- [ ] **Step 6: Commit**

```bash
git add me-primary/src/main.c me-primary/src/threads/comm_thread.c me-primary/build.ps1
git commit -m "Delete demo_realtime; main.c and comm_thread.c now use post_reg"
```

---

## Task 13: Full verification and final commit

**Files:** none (verification only)

- [ ] **Step 1: Run the full host test suite**

Run: `cd me-primary; .\build-native.ps1`
Expected: every existing suite plus `run_step_decode_tests`,
`run_can_frame_tests`, and `run_step_engine_tests` pass. Report the total
check count (per the project's `CLAUDE.md` convention of reporting the
check count whenever `src/` changes).

- [ ] **Step 2: Run the full cross-build**

Run: `cd me-primary; .\build.ps1`
Expected: compiles clean under `-Werror`, static aarch64 ELF produced.

- [ ] **Step 3: Confirm no dangling references**

```bash
grep -rn "demo_realtime\|me_demo_" me-primary/src me-primary/tests
```

Expected: no matches (Task 12 should already guarantee this via the
compiler; this is a final human-legible confirmation for the PR).

- [ ] **Step 4: Report to the developer**

State plainly, per the project's standing verification rule: **"Compiles
clean and all host tests pass — this has NOT been run on hardware."** The
developer runs `.\deploy.ps1` themselves; only that run proves the `0xCC`
frame on UDP 10000 carries a real step number and a current value that
traces back to the decoded program, per the design doc's testing table.

- [ ] **Step 5: Final commit (if any stray changes remain)**

```bash
git status --short
# If clean, nothing to do. If not:
git add -A
git commit -m "Step execution implementation complete - see spec and plan docs"
```
