# Multi-Channel Support for Secondary 1 (Channels 1-4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the me-primary board register, control, program, and CAN-FD-communicate with all 4 physical channels of Secondary 1 (CircuitIDs 0x11-0x14) instead of exactly one, with no channel able to corrupt another's CAN-FD command frame.

**Architecture:** Most of the pipeline (circuit registry, program storage, control dispatch, step execution, CAN-FD RX demux) already operates on the full 64-circuit-slot model and needs no changes. Two real changes are needed: (1) the registration/CLI layer moves from one hardcoded circuit_id to a configurable list of channels registered independently over the single TCP connection, and (2) the CAN-FD TX path gains a per-block "shadow" merge so that four independently-timed channels sharing one 64-byte CAN-FD block frame never zero-stomp each other's setpoint.

**Tech Stack:** C (gnu11 for the board cross-build, c11 for host protocol tests), pthreads, static aarch64 ELF via Arm GNU Toolchain, MinGW-w64 GCC for host tests, PowerShell build scripts (`build.ps1`, `build-native.ps1`).

**Spec:** `Docs/specs/2026-08-19-multi-channel-secondary1-design.md`

## Global Constraints

- `ME_MAX_SECONDARIES = 8`, `ME_MAX_CHANNELS = 8`, `ME_MAX_CIRCUITS = 64` (`src/proto/proto_defs.h:232-234`) — data structures are generalized to these constants; only Secondary 1 / channels 1-4 are exercised and verified in this task.
- CircuitID nibbles are 1-based: `ME_CIRCUIT_ID(sec, ch)` = `(sec << 4) | ch`. A malformed value must be rejected, never folded to a valid slot.
- Never declare a `me_msgq_t` as a local (~1 MB, overflows a default thread stack) — not touched by this plan, noted for awareness.
- `-Werror` on both builds. `-std=gnu11` for the cross-build (needs `_DEFAULT_SOURCE`), `-std=c11` for the host test build.
- Run `.\build-native.ps1` after any change under `me-primary/src/proto`, `me-primary/src/util`, `me-primary/src/exec`, or `me-primary/src/store`. Run `.\build.ps1` after any change under `me-primary/src` at all (including Linux-only files). Report the check count; a compiling cross-build is never reported as "tested" on its own.
- Only `.\deploy.ps1` against real hardware (board 172.16.18.167) proves the system works end to end — no claim of CAN-FD-merge correctness rests on host tests alone.
- The board is the only deliverable; no code targets Windows as a production output.

---

### Task 1: Channel-list CLI parser (pure logic, host-tested)

**Files:**
- Create: `me-primary/src/util/channel_list.h`
- Create: `me-primary/src/util/channel_list.c`
- Create: `me-primary/tests/test_channel_list.c`
- Modify: `me-primary/tests/test_util.h` (add `run_channel_list_tests(void);` declaration, alphabetically near the other `run_*` declarations)
- Modify: `me-primary/tests/test_main.c` (add `run_channel_list_tests();` call)
- Modify: `me-primary/build-native.ps1` (add `src\util\channel_list.c` and `tests\test_channel_list.c` to `$sources`)
- Modify: `me-primary/build.ps1` (add `src\util\channel_list.c` to `$sources` — the board build needs it too, since `main.c` will call it in Task 3)

**Interfaces:**
- Produces: `me_channel_list_result_t` enum (`ME_CHANNEL_LIST_OK`, `_EMPTY`, `_TOO_MANY`, `_OUT_OF_RANGE`, `_DUPLICATE`, `_MALFORMED`); `me_channel_list_result_t me_channel_list_parse(const char *str, uint8_t *out, uint8_t *count_out)` — parses a comma-separated list like `"1,2,3,4"` into `out[0..*count_out)` in input order, `out` must have room for `ME_MAX_CHANNELS` entries; `const char *me_channel_list_result_name(me_channel_list_result_t r)`.

- [ ] **Step 1: Write the failing tests**

Create `me-primary/tests/test_channel_list.c`:

```c
/*
 * test_channel_list.c - CLI channel-list parsing ("1,2,3,4" -> array).
 */
#include "test_util.h"
#include "../src/util/channel_list.h"

static void test_parses_simple_list(void)
{
    TEST_CASE("channel_list: parses a plain comma-separated list in order");
    uint8_t out[8];
    uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_OK, me_channel_list_parse("1,2,3,4", out, &count));
    CHECK_EQ_U(4, count);
    CHECK_EQ_U(1, out[0]); CHECK_EQ_U(2, out[1]);
    CHECK_EQ_U(3, out[2]); CHECK_EQ_U(4, out[3]);
}

static void test_parses_single_channel(void)
{
    TEST_CASE("channel_list: parses a single channel (default CLI shape)");
    uint8_t out[8];
    uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_OK, me_channel_list_parse("1", out, &count));
    CHECK_EQ_U(1, count);
    CHECK_EQ_U(1, out[0]);
}

static void test_rejects_empty_string(void)
{
    TEST_CASE("channel_list: rejects an empty string");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_EMPTY, me_channel_list_parse("", out, &count));
    CHECK_EQ_U(0, count);
}

static void test_rejects_out_of_range_channel(void)
{
    TEST_CASE("channel_list: rejects a channel outside 1..8, does not truncate");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_OUT_OF_RANGE, me_channel_list_parse("1,9", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_OUT_OF_RANGE, me_channel_list_parse("99", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_OUT_OF_RANGE, me_channel_list_parse("0", out, &count));
}

static void test_rejects_too_many_entries(void)
{
    TEST_CASE("channel_list: rejects more than ME_MAX_CHANNELS entries");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_TOO_MANY,
               me_channel_list_parse("1,2,3,4,5,6,7,8,1", out, &count));
}

static void test_rejects_duplicate_channel(void)
{
    TEST_CASE("channel_list: rejects a duplicated channel");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_DUPLICATE, me_channel_list_parse("1,2,1", out, &count));
}

static void test_rejects_malformed_input(void)
{
    TEST_CASE("channel_list: rejects non-numeric input and stray commas");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse("1,x", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse("1,", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse(",1", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse("1,,2", out, &count));
}

void run_channel_list_tests(void)
{
    test_parses_simple_list();
    test_parses_single_channel();
    test_rejects_empty_string();
    test_rejects_out_of_range_channel();
    test_rejects_too_many_entries();
    test_rejects_duplicate_channel();
    test_rejects_malformed_input();
}
```

Create the empty header so the test file at least compiles its `#include` to a stub (no function bodies yet):

```c
/* me-primary/src/util/channel_list.h - placeholder, filled in Step 3. */
#ifndef ME_CHANNEL_LIST_H
#define ME_CHANNEL_LIST_H
#include <stdint.h>
typedef enum { ME_CHANNEL_LIST_OK = 0 } me_channel_list_result_t;
me_channel_list_result_t me_channel_list_parse(const char *str, uint8_t *out, uint8_t *count_out);
const char *me_channel_list_result_name(me_channel_list_result_t r);
#endif
```

Add the declaration to `me-primary/tests/test_util.h` (near the other `void run_*_tests(void);` lines):

```c
void run_channel_list_tests(void);
```

Add the call to `me-primary/tests/test_main.c` (inside `main()`, alongside the other `run_*_tests();` calls):

```c
    run_channel_list_tests();
```

Add both new source files to `me-primary/build-native.ps1`'s `$sources` array (alongside the existing `src\util\*.c` and `tests\test_*.c` entries):

```powershell
    "$PSScriptRoot\src\util\channel_list.c"
    ...
    "$PSScriptRoot\tests\test_channel_list.c"
```

Add the new source file to `me-primary/build.ps1`'s `$sources` array (alongside the existing `src\util\*.c` entries):

```powershell
    "$PSScriptRoot\src\util\channel_list.c"
```

- [ ] **Step 2: Run the tests to confirm they fail for the right reason**

Run: `.\build-native.ps1`
Expected: build fails — `channel_list.c` does not exist yet (only the placeholder header does), so the linker/compiler reports a missing source or missing symbols (`me_channel_list_parse` undefined).

- [ ] **Step 3: Write the real header and implementation**

Replace the placeholder `me-primary/src/util/channel_list.h` with:

```c
/*
 * channel_list.h - parses a CLI channel list (e.g. "1,2,3,4") into a
 * validated, order-preserving array.
 *
 * PURE LOGIC: no sockets, no platform headers - host-testable via
 * build-native.ps1, same rule as proto/ and store/.
 */
#ifndef ME_CHANNEL_LIST_H
#define ME_CHANNEL_LIST_H

#include <stdint.h>

#include "../proto/proto_defs.h"

typedef enum {
    ME_CHANNEL_LIST_OK = 0,
    ME_CHANNEL_LIST_EMPTY,        /* the string had no entries at all    */
    ME_CHANNEL_LIST_TOO_MANY,     /* more than ME_MAX_CHANNELS entries   */
    ME_CHANNEL_LIST_OUT_OF_RANGE, /* an entry was not 1..ME_MAX_CHANNELS */
    ME_CHANNEL_LIST_DUPLICATE,    /* the same channel appeared twice     */
    ME_CHANNEL_LIST_MALFORMED     /* not a plain decimal integer list    */
} me_channel_list_result_t;

/*
 * Parses str (e.g. "1,2,3,4") into out[0..*count_out), preserving input
 * order. out must have room for ME_MAX_CHANNELS entries. Rejects instead of
 * truncating or silently deduplicating - out-of-range input must never be
 * folded onto a valid channel, same discipline ME_CIRCUIT_ID's callers
 * already follow for --secondary/--channel (see main.c parse_args).
 *
 * *count_out is always set (0 on any failure) so a caller that ignores the
 * return value still sees an empty list rather than garbage.
 */
me_channel_list_result_t me_channel_list_parse(const char *str,
                                               uint8_t *out,
                                               uint8_t *count_out);

const char *me_channel_list_result_name(me_channel_list_result_t r);

#endif /* ME_CHANNEL_LIST_H */
```

Create `me-primary/src/util/channel_list.c`:

```c
/* channel_list.c - see channel_list.h. */
#include "channel_list.h"

const char *me_channel_list_result_name(me_channel_list_result_t r)
{
    switch (r) {
    case ME_CHANNEL_LIST_OK:           return "OK";
    case ME_CHANNEL_LIST_EMPTY:        return "EMPTY";
    case ME_CHANNEL_LIST_TOO_MANY:     return "TOO_MANY";
    case ME_CHANNEL_LIST_OUT_OF_RANGE: return "OUT_OF_RANGE";
    case ME_CHANNEL_LIST_DUPLICATE:    return "DUPLICATE";
    case ME_CHANNEL_LIST_MALFORMED:    return "MALFORMED";
    default:                           return "UNKNOWN";
    }
}

me_channel_list_result_t me_channel_list_parse(const char *str,
                                               uint8_t *out,
                                               uint8_t *count_out)
{
    *count_out = 0u;
    if (str == NULL || str[0] == '\0') {
        return ME_CHANNEL_LIST_EMPTY;
    }

    const char *p = str;
    while (*p != '\0') {
        if (*count_out >= ME_MAX_CHANNELS) {
            return ME_CHANNEL_LIST_TOO_MANY;
        }
        if (*p < '0' || *p > '9') {
            return ME_CHANNEL_LIST_MALFORMED;
        }

        long v = 0;
        while (*p >= '0' && *p <= '9') {
            v = (v * 10) + (*p - '0');
            p++;
            /* Clamp rather than let a long run of digits overflow `long`:
             * any clamped value is still > ME_MAX_CHANNELS, so it still
             * resolves to OUT_OF_RANGE below instead of undefined behavior. */
            if (v > 255) { v = 256; }
        }

        if (v < 1 || v > (long)ME_MAX_CHANNELS) {
            return ME_CHANNEL_LIST_OUT_OF_RANGE;
        }

        const uint8_t ch = (uint8_t)v;
        for (uint8_t i = 0; i < *count_out; i++) {
            if (out[i] == ch) {
                return ME_CHANNEL_LIST_DUPLICATE;
            }
        }
        out[*count_out] = ch;
        (*count_out)++;

        if (*p == ',') {
            p++;
            if (*p == '\0') {
                return ME_CHANNEL_LIST_MALFORMED; /* trailing comma */
            }
        } else if (*p != '\0') {
            return ME_CHANNEL_LIST_MALFORMED;
        }
    }

    if (*count_out == 0u) {
        return ME_CHANNEL_LIST_EMPTY;
    }
    return ME_CHANNEL_LIST_OK;
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `.\build-native.ps1`
Expected: `RESULT: PASS`, check count increased by the 7 new assertion groups in `test_channel_list.c`. Report the before/after check count.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/util/channel_list.h me-primary/src/util/channel_list.c \
        me-primary/tests/test_channel_list.c me-primary/tests/test_util.h \
        me-primary/tests/test_main.c me-primary/build-native.ps1 me-primary/build.ps1
git commit -m "Add host-testable CLI channel-list parser for --channels"
```

---

### Task 2: CAN-FD block-merge helper (pure logic, host-tested)

**Files:**
- Modify: `me-primary/src/proto/can_frame.h` (add `me_can_merge_slot` declaration)
- Modify: `me-primary/src/proto/can_frame.c` (add `me_can_merge_slot` implementation)
- Modify: `me-primary/tests/test_can_frame.c` (add 2 new test functions + register them)

**Interfaces:**
- Consumes: `me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out)` (existing, `can_frame.h:67`), `ME_CAN_SLOT_LEN` (existing, `can_frame.h:20`).
- Produces: `void me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN], const uint8_t incoming64[ME_CAN_FRAME_LEN], uint8_t channel_num)` — copies only `incoming64`'s 16-byte slot for `channel_num` into `block64`, leaving every other slot in `block64` untouched. Task 4 (can_mgr.c) calls this directly.

- [ ] **Step 1: Write the failing tests**

Add to `me-primary/tests/test_can_frame.c` (new functions, above `run_can_frame_tests`):

```c
static void test_merge_slot_preserves_other_channels(void)
{
    TEST_CASE("can_frame: merge_slot updates only the target channel's slot");
    uint8_t block[ME_CAN_FRAME_LEN];
    memset(block, 0xAA, sizeof(block)); /* sentinel: must survive outside slot 1 */

    me_can_setpoint_t sp = { .channel_num = 2, .command = ME_CAN_CMD_CHA,
                             .set_voltage = 0.0f, .set_current = 7.5f };
    uint8_t incoming[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, incoming);

    me_can_merge_slot(block, incoming, 2);

    for (int i = 0; i < 16; i++)  { CHECK_BYTE(block, i, 0xAA); }        /* ch1 slot untouched */
    for (int i = 16; i < 32; i++) { CHECK_BYTE(block, i, incoming[i]); } /* ch2 slot updated   */
    for (int i = 32; i < 64; i++) { CHECK_BYTE(block, i, 0xAA); }        /* ch3/ch4 untouched  */
}

static void test_merge_slot_stop_overwrites_stale_charge(void)
{
    TEST_CASE("can_frame: merge_slot lets an explicit STO overwrite a stale CHA slot");
    uint8_t block[ME_CAN_FRAME_LEN];
    memset(block, 0, sizeof(block));

    me_can_setpoint_t charge = { .channel_num = 1, .command = ME_CAN_CMD_CHA,
                                 .set_voltage = 0.0f, .set_current = 12.0f };
    uint8_t charge_frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&charge, charge_frame);
    me_can_merge_slot(block, charge_frame, 1);
    CHECK_BYTE(block, 8, ME_CAN_CMD_CHA);

    me_can_setpoint_t stop = { .channel_num = 1, .command = ME_CAN_CMD_STO,
                              .set_voltage = 0.0f, .set_current = 0.0f };
    uint8_t stop_frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&stop, stop_frame);
    me_can_merge_slot(block, stop_frame, 1);

    CHECK_BYTE(block, 8, ME_CAN_CMD_STO);
    CHECK_BYTE(block, 6, 0x00); CHECK_BYTE(block, 7, 0x00); /* current back to 0.0f */
}
```

Add both calls inside `run_can_frame_tests(void)`:

```c
    test_merge_slot_preserves_other_channels();
    test_merge_slot_stop_overwrites_stale_charge();
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `.\build-native.ps1`
Expected: compile failure — `me_can_merge_slot` is not declared/defined yet.

- [ ] **Step 3: Implement `me_can_merge_slot`**

Add to `me-primary/src/proto/can_frame.h`, after the `me_can_pack_set` declaration:

```c
/*
 * Merges the 16-byte slot for channel_num out of incoming64 into block64,
 * leaving every other channel's slot in block64 untouched. Both buffers are
 * ME_CAN_FRAME_LEN bytes. incoming64 is typically a freshly-packed
 * single-channel frame from me_can_pack_set(); block64 is the running,
 * per-Secondary-per-block shadow state the caller retransmits.
 *
 * This is what lets four independently-timed channels share one 64-byte
 * CAN-FD block frame without one channel's zero-filled slots stomping
 * another's live setpoint - see can_mgr.c handle_can_tx() and
 * Docs/specs/2026-08-19-multi-channel-secondary1-design.md section 4.
 */
void me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN],
                       const uint8_t incoming64[ME_CAN_FRAME_LEN],
                       uint8_t channel_num);
```

Add to `me-primary/src/proto/can_frame.c`, after `me_can_pack_read`:

```c
void me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN],
                       const uint8_t incoming64[ME_CAN_FRAME_LEN],
                       uint8_t channel_num)
{
    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    const size_t off = (size_t)slot * ME_CAN_SLOT_LEN;
    memcpy(&block64[off], &incoming64[off], ME_CAN_SLOT_LEN);
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `.\build-native.ps1`
Expected: `RESULT: PASS`, check count increased by the 2 new test functions' assertions (68 checks: 16+16+32 byte checks plus 2 command-byte and 2 float-byte checks across both tests). Report the before/after check count.

- [ ] **Step 5: Commit**

```bash
git add me-primary/src/proto/can_frame.h me-primary/src/proto/can_frame.c \
        me-primary/tests/test_can_frame.c
git commit -m "Add me_can_merge_slot for per-block CAN-FD SET_VALUES coalescing"
```

---

### Task 3: Multi-channel registration (CLI, sys_init, comm_thread)

**Files:**
- Modify: `me-primary/src/sys_init.h:19-49` (config struct: `channel` scalar → `channels[]`/`channel_count`)
- Modify: `me-primary/src/sys_init.c:11-25,72-90` (defaults + reg_request template)
- Modify: `me-primary/src/main.c` (usage text, `parse_args`, `--channels` flag)
- Modify: `me-primary/src/threads/comm_thread.c:94-259,709-764` (`do_registration`, `print_registered_banner`, `comm_thread_main`)
- Modify: `me-primary/src/store/circuit_registry.h:36-40` (doc comment — scope limit no longer accurate as written)

**Interfaces:**
- Consumes: `me_channel_list_parse`/`me_channel_list_result_name` from Task 1 (`src/util/channel_list.h`); `ME_CIRCUIT_ID`, `ME_MAX_CHANNELS` from `proto_defs.h` (existing); `me_registry_mark_registered(uint8_t circuit_id)`, `me_post_reg_send(uint8_t circuit_id)` (existing, unchanged signatures).
- Produces: `me_config_t.channels[ME_MAX_CHANNELS]` + `me_config_t.channel_count` (replaces `me_config_t.channel`); `do_registration(me_system_t *sys, int fd, uint8_t circuit_id)` (new signature, was `(sys, fd)`); `s_registered` now means "at least one configured channel registered this process" (semantics change, same variable/getter name `me_comm_is_registered()`).

This task has no host-testable deliverable of its own (`main.c`, `sys_init.c`, `comm_thread.c` are Linux-only and excluded from `build-native.ps1` per project convention) — verification here is `.\build.ps1` compiling clean under `-Werror`. Do not report this as "tested"; report "compiles clean."

- [ ] **Step 1: Update the config struct**

In `me-primary/src/sys_init.h`, inside `me_config_t` (around line 24-25), replace:

```c
    uint8_t        secondary;
    uint8_t        channel;
```

with:

```c
    uint8_t        secondary;
    uint8_t        channels[ME_MAX_CHANNELS];
    uint8_t        channel_count;
```

This needs `ME_MAX_CHANNELS`, which comes from `proto/proto_defs.h` via the existing `#include "proto/reg_frame.h"` in this file (unchanged, no new include needed — `reg_frame.h` already includes `proto_defs.h`).

- [ ] **Step 2: Update `me_config_defaults` and the reg_request template**

In `me-primary/src/sys_init.c`, replace this line in `me_config_defaults`:

```c
    cfg->channel             = 1;
```

with:

```c
    cfg->channels[0]         = 1;
    cfg->channel_count       = 1;
```

In `me_sys_init`, replace the registration-request-building block (currently sets `sys->reg_request.circuit_id` from `cfg->secondary`/`cfg->channel`, then logs it):

```c
    memset(&sys->reg_request, 0, sizeof(sys->reg_request));
    sys->reg_request.device_id  = cfg->device_id;
    sys->reg_request.circuit_id = ME_CIRCUIT_ID(cfg->secondary, cfg->channel);
    snprintf(sys->reg_request.device_name, sizeof(sys->reg_request.device_name),
             "%s", cfg->device_name);
    memcpy(sys->reg_request.ip, sys->net.ip, ME_REG_IP_LEN);
    memcpy(sys->reg_request.mac, sys->net.mac, ME_REG_MAC_LEN);

    ME_LOGI("system init: device %u, circuit 0x%02X (secondary %u, channel %u), name \"%s\"",
            sys->reg_request.device_id,
            sys->reg_request.circuit_id,
            ME_CIRCUIT_SECONDARY(sys->reg_request.circuit_id),
            ME_CIRCUIT_CHANNEL(sys->reg_request.circuit_id),
            sys->reg_request.device_name);
```

with:

```c
    /*
     * Template only: device_id/name/ip/mac are shared by every channel this
     * process registers (confirmed one device_id per board, not per
     * channel). circuit_id is intentionally left at 0 here - do_registration()
     * in comm_thread.c builds a per-attempt copy with circuit_id set to
     * ME_CIRCUIT_ID(cfg.secondary, cfg.channels[i]) for each configured
     * channel. See Docs/specs/2026-08-19-multi-channel-secondary1-design.md.
     */
    memset(&sys->reg_request, 0, sizeof(sys->reg_request));
    sys->reg_request.device_id  = cfg->device_id;
    snprintf(sys->reg_request.device_name, sizeof(sys->reg_request.device_name),
             "%s", cfg->device_name);
    memcpy(sys->reg_request.ip, sys->net.ip, ME_REG_IP_LEN);
    memcpy(sys->reg_request.mac, sys->net.mac, ME_REG_MAC_LEN);

    char chlist[64] = { 0 };
    size_t chlist_len = 0;
    for (uint8_t i = 0; i < cfg->channel_count; i++) {
        chlist_len += (size_t)snprintf(chlist + chlist_len, sizeof(chlist) - chlist_len,
                                       "%s%u", (i == 0) ? "" : ",",
                                       (unsigned)cfg->channels[i]);
    }

    ME_LOGI("system init: device %u, secondary %u, channels [%s], name \"%s\"",
            sys->reg_request.device_id, (unsigned)cfg->secondary, chlist,
            sys->reg_request.device_name);
```

- [ ] **Step 3: Update the CLI in `main.c`**

Add the include near the other project headers at the top of `me-primary/src/main.c`:

```c
#include "util/channel_list.h"
```

In the `usage()` function, replace:

```c
        "  --channel <n>       CircuitID lower nibble     (default 1)\n"
```

with:

```c
        "  --channels <list>   Comma-separated channels, e.g. 1,2,3,4 (default 1)\n"
```

In `parse_args()`, replace the `--channel` branch:

```c
        } else if (strcmp(a, "--channel") == 0) {
            NEED_VALUE();
            cfg->channel = (uint8_t)atoi(argv[++i]);
```

with:

```c
        } else if (strcmp(a, "--channels") == 0) {
            NEED_VALUE();
            uint8_t parsed[ME_MAX_CHANNELS];
            uint8_t parsed_count = 0;
            const me_channel_list_result_t r =
                me_channel_list_parse(argv[++i], parsed, &parsed_count);
            if (r != ME_CHANNEL_LIST_OK) {
                fprintf(stderr, "error: --channels invalid (%s): \"%s\"\n\n",
                        me_channel_list_result_name(r), argv[i]);
                return false;
            }
            memcpy(cfg->channels, parsed, parsed_count);
            cfg->channel_count = parsed_count;
```

Remove the now-obsolete post-loop validation block (channel range is already enforced inside `me_channel_list_parse`):

```c
    if (cfg->channel < 1u || cfg->channel > ME_MAX_CHANNELS) {
        fprintf(stderr, "error: --channel must be 1-%u (got %u)\n\n",
                (unsigned)ME_MAX_CHANNELS, (unsigned)cfg->channel);
        return false;
    }
```

The `--secondary` validation block directly above it is unchanged.

- [ ] **Step 4: Refactor `do_registration` and `print_registered_banner` to take an explicit circuit_id**

In `me-primary/src/threads/comm_thread.c`, replace `print_registered_banner`:

```c
static void print_registered_banner(const me_system_t *sys,
                                    const me_reg_response_t *rsp)
{
    const uint8_t ckt = sys->reg_request.circuit_id;
    printf("\n");
    printf("======================================================\n");
    printf("  DEVICE REGISTERED\n");
    printf("------------------------------------------------------\n");
    printf("  Device number   : %u\n", sys->reg_request.device_id);
    printf("  Circuit number  : 0x%02X  (secondary %u, channel %u)\n",
           ckt, ME_CIRCUIT_SECONDARY(ckt), ME_CIRCUIT_CHANNEL(ckt));
    printf("  Device name     : %s\n", sys->reg_request.device_name);
    printf("  Server response : 0x%02X (%s)\n",
           rsp->value, me_reg_value_name(rsp->value));
    printf("======================================================\n\n");
    fflush(stdout);
}
```

with:

```c
static void print_registered_banner(const me_reg_request_t *req,
                                    const me_reg_response_t *rsp)
{
    const uint8_t ckt = req->circuit_id;
    printf("\n");
    printf("======================================================\n");
    printf("  DEVICE REGISTERED\n");
    printf("------------------------------------------------------\n");
    printf("  Device number   : %u\n", req->device_id);
    printf("  Circuit number  : 0x%02X  (secondary %u, channel %u)\n",
           ckt, ME_CIRCUIT_SECONDARY(ckt), ME_CIRCUIT_CHANNEL(ckt));
    printf("  Device name     : %s\n", req->device_name);
    printf("  Server response : 0x%02X (%s)\n",
           rsp->value, me_reg_value_name(rsp->value));
    printf("======================================================\n\n");
    fflush(stdout);
}
```

Replace the `do_registration` signature and body's uses of `sys->reg_request`:

```c
static bool do_registration(me_system_t *sys, int fd)
{
    uint8_t frame[ME_REG_REQUEST_LEN];
    const size_t frame_len = me_reg_pack_request(&sys->reg_request, frame,
                                                 sys->cfg.crc_order);
```

with:

```c
static bool do_registration(me_system_t *sys, int fd, uint8_t circuit_id)
{
    me_reg_request_t req = sys->reg_request;
    req.circuit_id = circuit_id;

    uint8_t frame[ME_REG_REQUEST_LEN];
    const size_t frame_len = me_reg_pack_request(&req, frame,
                                                 sys->cfg.crc_order);
```

Then, still inside `do_registration`, replace every remaining `sys->reg_request` with `req`:

- `me_reg_parse_response(rx, (size_t)n, &sys->reg_request, ...)` → `me_reg_parse_response(rx, (size_t)n, &req, ...)`
- The echo-mismatch log's `sys->reg_request.device_id, sys->reg_request.circuit_id` → `req.device_id, req.circuit_id`
- `me_registry_mark_registered(sys->reg_request.circuit_id)` → `me_registry_mark_registered(req.circuit_id)` (and the log line right after it, which prints `sys->reg_request.circuit_id`, → `req.circuit_id`)
- `print_registered_banner(sys, &rsp)` → `print_registered_banner(&req, &rsp)`
- `me_post_reg_send(sys->reg_request.circuit_id)` → `me_post_reg_send(req.circuit_id)`

- [ ] **Step 5: Loop over configured channels in `comm_thread_main`**

Replace:

```c
        /* Registration is sent exactly once per connection. On a reconnect the
         * server answers 0x02 (Already Registered), which is success. */
        s_state = ME_COMM_REGISTERING;
        ME_LOGI("state: %s", me_comm_state_name(s_state));

        if (do_registration(sys, fd)) {
            s_registered = 1;
            backoff_ms   = ME_BACKOFF_MIN_MS; /* healthy again */
            idle_loop(sys, fd);
        } else {
            ME_LOGW("state: %s failed, retrying in %d ms",
                    me_comm_state_name(ME_COMM_REGISTERING), backoff_ms);
        }
```

with:

```c
        /*
         * One 0xDD registration per configured channel, sent serially over
         * this single TCP connection - the wire is one stream, so attempts
         * are serialized, but no channel's outcome gates another's: a
         * rejection on one channel does not stop the rest from being
         * attempted or from running. On a reconnect every configured
         * channel is re-registered; the server answers 0x02 (Already
         * Registered) for ones already admitted, which is success.
         */
        s_state = ME_COMM_REGISTERING;
        ME_LOGI("state: %s (%u channel(s))", me_comm_state_name(s_state),
                (unsigned)sys->cfg.channel_count);

        unsigned registered_count = 0u;
        for (uint8_t i = 0; i < sys->cfg.channel_count; i++) {
            const uint8_t circuit_id =
                ME_CIRCUIT_ID(sys->cfg.secondary, sys->cfg.channels[i]);
            if (do_registration(sys, fd, circuit_id)) {
                registered_count++;
            } else {
                ME_LOGW("state: %s failed for circuit 0x%02X "
                        "(secondary %u, channel %u)",
                        me_comm_state_name(ME_COMM_REGISTERING), circuit_id,
                        (unsigned)sys->cfg.secondary,
                        (unsigned)sys->cfg.channels[i]);
            }
        }

        if (registered_count > 0u) {
            s_registered = 1;
            backoff_ms   = ME_BACKOFF_MIN_MS; /* healthy again */
            idle_loop(sys, fd);
        } else {
            ME_LOGW("state: %s failed for every configured channel, "
                    "retrying in %d ms",
                    me_comm_state_name(ME_COMM_REGISTERING), backoff_ms);
        }
```

Add `#include "../proto/proto_defs.h"` to `comm_thread.c`'s include block (for `ME_CIRCUIT_ID`) — do not rely on it arriving transitively through another header.

Update the top-of-file doc comment (lines 14-17), which currently reads:

```
 * Reaching IDLE also admits this board's own Secondary/Channel into the
 * circuit registry, which is what allows route_frame() to accept data for it.
 * Frames for any other circuit are dropped until something registers it -
 * today nothing else does; a future CAN-side handshake will.
```

to:

```
 * Reaching IDLE also admits every one of this board's configured
 * Secondary/Channels (--channels) into the circuit registry, which is what
 * allows route_frame() to accept data for them. Frames for any other
 * circuit are dropped until something registers it - today nothing else
 * does; a future CAN-side handshake will.
```

- [ ] **Step 6: Update the `circuit_registry.h` scope-limit comment**

In `me-primary/src/store/circuit_registry.h`, the comment block currently reads (lines 36-40):

```
 * KNOWN SCOPE LIMIT, deliberate and temporary: the 0xDD frame carries ONE
 * CircuitID and the board sends ONE registration per connection, so today
 * exactly 1 of the 64 circuits can ever be registered and the other 63 are
 * refused. That is intentional until the CAN-side handshake above lands -
 * but it means this board is functionally single-circuit in the meantime.
```

Replace with:

```
 * KNOWN SCOPE LIMIT, deliberate and temporary: the board sends one 0xDD
 * registration per operator-configured channel (--channels) per connection,
 * so only the Secondary/Channels named on the command line can ever be
 * registered - anything else is refused. That is intentional until the
 * CAN-side handshake above lands, which would let a circuit self-announce
 * without being explicitly configured.
```

- [ ] **Step 7: Cross-build and confirm it compiles clean**

Run: `.\build.ps1`
Expected: compiles clean under `-Werror`, produces a static aarch64 ELF. Report "compiles clean" — this is not hardware verification.

- [ ] **Step 8: Commit**

```bash
git add me-primary/src/sys_init.h me-primary/src/sys_init.c me-primary/src/main.c \
        me-primary/src/threads/comm_thread.c me-primary/src/store/circuit_registry.h
git commit -m "Register all configured channels of a Secondary over one TCP connection"
```

---

### Task 4: CAN-FD TX coalescing wiring (can_mgr.c)

`⚠ HUMAN REVIEW REQUIRED — hardware-critical change.` Design approved by developer 2026-08-19; this task wires the already-tested (Task 2) merge helper into the live CAN-FD transmit path. Confirm on hardware per Task 5 before trusting it under real load.

**Files:**
- Modify: `me-primary/src/threads/can_mgr.c:62-75` (add shadow buffer storage)
- Modify: `me-primary/src/threads/can_mgr.c:94-166` (`handle_can_tx` — apply the merge for SET frames only)

**Interfaces:**
- Consumes: `me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN], const uint8_t incoming64[ME_CAN_FRAME_LEN], uint8_t channel_num)` from Task 2; `me_can_block_t me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out)` (existing); `ME_CAN_FUNC_SET` (existing, `can_frame.h:40`).

This task has no host-testable deliverable of its own (`can_mgr.c` is Linux-only, excluded from `build-native.ps1`) — the merge logic itself was already proven by Task 2's host tests. Verification here is `.\build.ps1` compiling clean, then the Task 5 hardware checklist.

- [ ] **Step 1: Add the per-Secondary, per-block shadow buffer**

In `me-primary/src/threads/can_mgr.c`, after the existing `s_active_ch`/`s_tx_us` declarations (around line 75), add:

```c
/*
 * Per-Secondary, per-block "last known good" SET_VALUES state. Channels 1-4
 * share Block 1; channels 5-8 share Block 2 (me_can_block_for_channel).
 * Four independently-timed channels sharing one 64-byte block frame cannot
 * each transmit their own frame with the other three channels' slots
 * zero-filled - a zero-filled slot means CMD_STO to whichever channel owns
 * it, which would stop an unrelated, currently-active channel. handle_can_tx
 * merges each SET frame's own slot into this buffer and transmits the whole
 * buffer instead, so every other channel's last commanded state survives.
 *
 * Index 0 in the outer dimension is unused (Secondary numbers are 1-based,
 * same convention as s_active_ch). Index 0/1 in the middle dimension is
 * Block 1 / Block 2 (me_can_block_t is 1-based; store at block - 1).
 *
 * Zero-initialized at process start, which is the correct default: an
 * all-zero slot means CMD_STO / 0 A for a channel that has never sent a
 * setpoint, which is safe. me_exec_force_stop() (step_engine.c) always
 * sends an explicit CMD_STO before a channel goes idle, so a channel that
 * stops never leaves a stale non-zero setpoint behind in its own slot.
 */
static uint8_t s_set_shadow[ME_MAX_SECONDARIES + 1u][2][ME_CAN_FRAME_LEN];
```

- [ ] **Step 2: Apply the merge in `handle_can_tx` for SET frames only**

In `handle_can_tx`, replace:

```c
    memset(&can, 0, sizeof(can));
    can.timestamp_ms = 0u;            /* M7 sequence counter; unused for SET */
    can.can_id       = m->offset;     /* 11-bit ID, set by Core Logic        */
    can.is_extended  = 0u;            /* standard identifier                 */
    can.is_fd        = 1u;
    can.brs          = 1u;
    can.esi          = 0u;
    can.data_len     = ME_CAN_PAYLOAD_BYTES;
    can.dlc          = me_rpmsg_dlc_for_len(can.data_len);
    memcpy(can.data, m->payload, ME_CAN_PAYLOAD_BYTES);
```

with:

```c
    /*
     * function5 is the low 5 bits of the CAN ID Core Logic already built
     * into m->offset (me_can_id() = circuit_num6 << 5 | function5) - no new
     * message field needed to tell a SET frame from a READ/poll frame.
     */
    const uint8_t function5 = (uint8_t)(m->offset & 0x1Fu);
    const uint8_t *tx_data  = m->payload;

    if (function5 == ME_CAN_FUNC_SET) {
        uint8_t slot; /* unused: me_can_merge_slot() recomputes it from channel_num */
        const me_can_block_t block = me_can_block_for_channel(channel, &slot);
        (void)slot;
        uint8_t *shadow = s_set_shadow[secondary][block - 1];
        me_can_merge_slot(shadow, m->payload, channel);
        tx_data = shadow;
    }

    memset(&can, 0, sizeof(can));
    can.timestamp_ms = 0u;            /* M7 sequence counter; unused for SET */
    can.can_id       = m->offset;     /* 11-bit ID, set by Core Logic        */
    can.is_extended  = 0u;            /* standard identifier                 */
    can.is_fd        = 1u;
    can.brs          = 1u;
    can.esi          = 0u;
    can.data_len     = ME_CAN_PAYLOAD_BYTES;
    can.dlc          = me_rpmsg_dlc_for_len(can.data_len);
    memcpy(can.data, tx_data, ME_CAN_PAYLOAD_BYTES);
```

Note `channel` and `secondary` are already computed earlier in `handle_can_tx` (existing lines 101-102) before the length/range validation this new code sits after.

- [ ] **Step 3: Cross-build and confirm it compiles clean**

Run: `.\build.ps1`
Expected: compiles clean under `-Werror`. Report "compiles clean" — the merge logic's correctness was already proven on the host in Task 2; this step only proves the wiring compiles.

- [ ] **Step 4: Commit**

```bash
git add me-primary/src/threads/can_mgr.c
git commit -m "Coalesce per-channel SET_VALUES frames into a per-block shadow buffer"
```

---

### Task 5: Full build verification and hardware-in-the-loop checklist

No new code in this task — it exists to make sure Tasks 1-4 are verified together, and to hand the developer an explicit hardware test script, per the project rule that only `deploy.ps1` against real hardware proves the system works.

- [ ] **Step 1: Run the full host test suite**

Run: `.\build-native.ps1`
Expected: `RESULT: PASS`. Report the total check count (should be the pre-existing count plus Task 1's 7 tests and Task 2's 2 tests).

- [ ] **Step 2: Run the full cross-build**

Run: `.\build.ps1`
Expected: compiles clean under `-Werror`, static aarch64 ELF produced. Report "compiles clean" — do not call this "tested."

- [ ] **Step 3: Hand the developer the hardware verification script**

This step is performed by the developer, not the agent (no network path to hardware from this machine). Report these exact steps back to the developer rather than attempting them:

1. Deploy with all 4 channels configured:
   `.\deploy.ps1 -BoardIP 172.16.18.167 -ServerIP <web-app-ip>` with the board invoked as `me_primary --server <web-app-ip> --secondary 1 --channels 1,2,3,4 ...`
2. Confirm the registration banner prints once per channel (4 banners), and that a channel intentionally misconfigured (e.g. temporarily passing `--channels 1,2,3,9` to trigger a CLI rejection) fails cleanly with the `--channels invalid` error from Task 3 Step 3, without registering any channel.
3. Send 4 different battery-testing programs to circuits `0x11`/`0x12`/`0x13`/`0x14` from the Web Application, and start them at staggered times (not simultaneously).
4. Watch the CAN-FD RTT logs (`can: S%u RTT %u us`, added in the prior RPMsg latency session) and/or a bus trace, and confirm Channel 1's setpoint is never reset to STO when Channel 2, 3, or 4's program starts, stops, or changes its own setpoint. This is the scenario Task 4 exists to prevent.
5. Stop one channel (e.g. Channel 2) while the others keep running; confirm the other channels' setpoints are unaffected and Channel 2's slot in the shared block correctly shows STO afterward.

- [ ] **Step 4: Commit only if Steps 1-2 required fixes**

If no code changed in this task, there is nothing to commit — proceed to Task 6. If a build fix was needed, commit it with a message describing what broke and why.

---

### Task 6: Update project memory docs

**Files:**
- Modify: `ME Project/.claude/SESSION.md`
- Modify: `ME Project/.claude/TASKS.md`
- Modify: `ME Project/.claude/DECISIONS.md`
- Modify: `ME Project/.claude/CODEBASE_MAP.md`

This project maintains a `.claude/` memory folder that previous sessions have kept in sync after each significant change (see commit history, e.g. Session #11's sync after the RPMsg latency work). This task keeps that convention.

- [ ] **Step 1: Add an ADR to `DECISIONS.md`**

Record the CAN-FD shadow-merge decision (the one genuinely new architectural call in this plan): per-block shadow buffer in `can_mgr.c`, merging only SET-function frames, leaving READ/poll frames as independent per-channel passthrough. Include the "why" (four channels sharing one physical block cannot each transmit a frame with the others' slots zero-filled) and the safety argument (`me_exec_force_stop()` always sends an explicit STO before going idle).

- [ ] **Step 2: Update `TASKS.md`**

Mark the multi-channel work done, referencing this plan and its spec.

- [ ] **Step 3: Update `SESSION.md`**

Add a compact session entry: what changed (registration loop, CLI `--channels`, CAN-FD shadow merge), what's still pending (hardware-in-the-loop verification per Task 5 Step 3, if not yet run by the developer at the time this task executes).

- [ ] **Step 4: Update `CODEBASE_MAP.md`**

Add `src/util/channel_list.c/h` to the file listing; note the `handle_can_tx` shadow-merge addition in `can_mgr.c`'s entry; update `me_config_t`'s documented shape (`channels[]`/`channel_count` instead of `channel`).

- [ ] **Step 5: Commit**

```bash
git add "ME Project/.claude/SESSION.md" "ME Project/.claude/TASKS.md" \
        "ME Project/.claude/DECISIONS.md" "ME Project/.claude/CODEBASE_MAP.md"
git commit -m "Sync .claude/ memory docs after Secondary 1 multi-channel work"
```
