# RPMsg CAN Transport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the CAN Data Manager's simulated Secondary responses with a real RPMsg transport that exchanges CAN-FD frames with the M7 core over `/dev/ttyRPMSG30`.

**Architecture:** A pure byte-layout module (`src/proto/rpmsg_frame.c`) handles the two-layer wire encoding and the stream reassembly, and is unit-tested on the Windows host. A thin Linux-only module (`src/platform/rpmsg_link.c`) owns the character-device I/O. `src/threads/can_mgr.c` is rewritten into a single `poll()` loop over the (now pollable) `g_q_can` eventfd and the RPMsg fd. Core Logic's message contract is untouched.

**Tech Stack:** C11 (`-std=gnu11`), pthreads, Linux `poll()`/termios, no dynamic allocation. Cross-compiled static aarch64 via `build.ps1`; host tests via `build-native.ps1`.

**Spec:** `Docs/specs/2026-08-18-rpmsg-can-transport-design.md`

## Global Constraints

- **No dynamic allocation.** No `malloc`/`free` anywhere. All buffers are fixed-size, file-scope or stack.
- **No file exceeds 2000 lines.**
- **`src/proto/` stays pure.** No sockets, no platform headers, no threads — it must compile unchanged in `build-native.ps1`.
- **Both builds run when `src/` changes:** `.\build.ps1` **and** `.\build-native.ps1`. Report the check count. When only the cross-build ran, say "compiles clean" — never "tested".
- **`-Werror` on both builds.**
- **Every queue send takes a finite timeout** (`ME_SEND_TIMEOUT_CTRL_MS` = 50). Never add an unbounded send.
- **Never declare a `me_msgq_t` as a local** — it is ~1 MB.
- **Endianness is split by layer and must not be unified:** RPMsg header fields (`command`, `length`) are **big-endian**; `can_frame_msg_t` fields (`timestamp_ms`, `can_id`) are **little-endian**.
- **Exact protocol constants** (from `Ref Docs/RPMSG_PROTOCOL.md` v3.0):
  - `RPMSG_SYNC_BYTE` = `0xAA`, header length = **11**, max payload = **80**, max frame = **91**
  - `RPMSG_ACTION_WRITE_SET` = `0x01`, `RPMSG_ACTION_READ` = `0x02`
  - `RPMSG_RESULT_REQUEST` = `0x00`, `RPMSG_RESULT_SUCCESS` = `0x01`, `RPMSG_RESULT_FAILURE` = `0xFF`
  - `RPMSG_CMD_CAN_SET_FRAME` = `0x00100001`, `RPMSG_CMD_CAN_GET_FRAME` = `0x00100002`
  - `can_frame_msg_t` is **exactly 80 bytes**; `data[64]` starts at offset **16**
  - Device: `/dev/ttyRPMSG30`, RPMsg buffer payload 496 bytes
- **CAN identifier layout** (from `Ref Docs/master_slave_can_v1.0.md`): `CAN_ID = (circuit6 << 5) | function5`, 11-bit standard. The Secondary always replies on function `0x02`.
- **`ME_CIRCUIT_SECONDARY(id)`/`ME_CIRCUIT_CHANNEL(id)` nibbles are 1-based.** `0x11` is Secondary 1, Channel 1. A malformed ID is rejected, never folded to slot 0.

---

## File Structure

| File | Build | Responsibility |
|---|---|---|
| `src/proto/rpmsg_frame.h` / `.c` | **both** | Pure byte layout: header wrap/unwrap, `can_frame_msg_t` pack/parse, DLC table, stream reassembly. ~320 lines. |
| `tests/test_rpmsg_frame.c` | native | Host tests for the above. |
| `src/platform/rpmsg_link.h` / `.c` | cross only | `open()` + `cfmakeraw()` + full `write()` + `close()`. ~110 lines. |
| `src/threads/can_mgr.c` | cross only | Rewritten: poll loop, TX path, RX path, active-channel registry. Simulation deleted. |
| `src/app_queues.c:33` | both | One line: `g_q_can` becomes pollable. |
| `build.ps1`, `build-native.ps1` | — | Add the new sources. |

---

## Task 1: RPMsg header codec (pure)

**Files:**
- Create: `me-primary/src/proto/rpmsg_frame.h`, `me-primary/src/proto/rpmsg_frame.c`
- Create: `me-primary/tests/test_rpmsg_frame.c`
- Modify: `me-primary/tests/test_util.h` (add `void run_rpmsg_frame_tests(void);` after line 99, next to `run_step_engine_tests`)
- Modify: `me-primary/tests/test_main.c` (call `run_rpmsg_frame_tests();` after `run_step_engine_tests();`)
- Modify: `me-primary/build-native.ps1` (add `src\proto\rpmsg_frame.c` after the `can_frame.c` line, and `tests\test_rpmsg_frame.c` after the `test_step_engine.c` line)
- Modify: `me-primary/build.ps1` (add `src\proto\rpmsg_frame.c` after the `can_frame.c` line)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `size_t me_rpmsg_wrap(uint8_t action, uint32_t command, uint8_t result, const uint8_t *payload, size_t payload_len, uint8_t *out, size_t out_sz)` — returns bytes written, or `0` on error.
  - `bool me_rpmsg_parse_header(const uint8_t *buf, size_t len, me_rpmsg_hdr_t *out)`
  - `me_rpmsg_hdr_t { uint8_t action; uint32_t command; uint8_t result; uint32_t length; }`
  - Constants listed in Global Constraints.

- [ ] **Step 1: Write the failing test**

Create `me-primary/tests/test_rpmsg_frame.c`:

```c
/*
 * test_rpmsg_frame.c - host tests for the RPMsg wire codec.
 *
 * The golden vectors come from Ref Docs/RPMSG_PROTOCOL.md sections 8.2-8.4.
 * Those byte strings are the contract with the M7 firmware: if one of these
 * fails, the two cores disagree about the wire and nothing will work on
 * hardware.
 */
#include "../src/proto/rpmsg_frame.h"

#include "test_util.h"

static void test_wrap_header_golden(void)
{
    /* RPMSG_PROTOCOL.md section 8 step 2: the 11-byte header for an
     * 80-byte CAN_SET_FRAME request. */
    uint8_t payload[ME_RPMSG_PAYLOAD_MAX];
    uint8_t out[ME_RPMSG_FRAME_MAX];
    memset(payload, 0, sizeof(payload));

    TEST_CASE("rpmsg wrap: header golden vector");
    const size_t n = me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                   ME_RPMSG_CMD_CAN_SET_FRAME,
                                   ME_RPMSG_RESULT_REQUEST,
                                   payload, ME_RPMSG_CAN_MSG_LEN,
                                   out, sizeof(out));
    CHECK_EQ_U(91u, n);
    CHECK_BYTE(out, 0, 0xAA); /* sync                       */
    CHECK_BYTE(out, 1, 0x01); /* action = WRITE_SET         */
    CHECK_BYTE(out, 2, 0x00); /* command, big-endian        */
    CHECK_BYTE(out, 3, 0x10);
    CHECK_BYTE(out, 4, 0x00);
    CHECK_BYTE(out, 5, 0x01);
    CHECK_BYTE(out, 6, 0x00); /* result = REQUEST           */
    CHECK_BYTE(out, 7, 0x00); /* length = 80, big-endian    */
    CHECK_BYTE(out, 8, 0x00);
    CHECK_BYTE(out, 9, 0x00);
    CHECK_BYTE(out, 10, 0x50);
}

static void test_wrap_rejects_oversize(void)
{
    uint8_t payload[ME_RPMSG_PAYLOAD_MAX];
    uint8_t out[ME_RPMSG_FRAME_MAX];
    memset(payload, 0, sizeof(payload));

    TEST_CASE("rpmsg wrap: payload over 80 bytes is refused");
    CHECK_EQ_U(0u, me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                 ME_RPMSG_CMD_CAN_SET_FRAME,
                                 ME_RPMSG_RESULT_REQUEST,
                                 payload, ME_RPMSG_PAYLOAD_MAX + 1u,
                                 out, sizeof(out)));

    TEST_CASE("rpmsg wrap: undersized output buffer is refused");
    CHECK_EQ_U(0u, me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                 ME_RPMSG_CMD_CAN_SET_FRAME,
                                 ME_RPMSG_RESULT_REQUEST,
                                 payload, ME_RPMSG_CAN_MSG_LEN,
                                 out, 90u));
}

static void test_parse_header_ack(void)
{
    /* RPMSG_PROTOCOL.md section 8 step 4: the SET ACK. */
    const uint8_t ack[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x01, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00
    };
    me_rpmsg_hdr_t h;

    TEST_CASE("rpmsg parse: SET ACK header");
    CHECK(me_rpmsg_parse_header(ack, sizeof(ack), &h));
    CHECK_EQ_U(ME_RPMSG_ACTION_WRITE_SET, h.action);
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_SET_FRAME, h.command);
    CHECK_EQ_U(ME_RPMSG_RESULT_SUCCESS, h.result);
    CHECK_EQ_U(0u, h.length);
}

static void test_parse_header_rejects(void)
{
    uint8_t buf[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x01, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00
    };
    me_rpmsg_hdr_t h;

    TEST_CASE("rpmsg parse: wrong sync byte is refused");
    buf[0] = 0xAB;
    CHECK(!me_rpmsg_parse_header(buf, sizeof(buf), &h));

    TEST_CASE("rpmsg parse: short buffer is refused");
    buf[0] = 0xAA;
    CHECK(!me_rpmsg_parse_header(buf, ME_RPMSG_HDR_LEN - 1u, &h));

    TEST_CASE("rpmsg parse: implausible length is refused");
    buf[10] = 0x51; /* 81 > 80 */
    CHECK(!me_rpmsg_parse_header(buf, sizeof(buf), &h));
}

void run_rpmsg_frame_tests(void)
{
    printf("rpmsg_frame...\n");
    test_wrap_header_golden();
    test_wrap_rejects_oversize();
    test_parse_header_ack();
    test_parse_header_rejects();
}
```

Add to `me-primary/tests/test_util.h`, immediately after `void run_step_engine_tests(void);`:

```c
void run_rpmsg_frame_tests(void);
```

Add to `me-primary/tests/test_main.c`, immediately after `run_step_engine_tests();`:

```c
    run_rpmsg_frame_tests();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `.\build-native.ps1`
Expected: FAIL — compiler error, `src/proto/rpmsg_frame.h: No such file or directory`.

- [ ] **Step 3: Write the header**

Create `me-primary/src/proto/rpmsg_frame.h`:

```c
/*
 * rpmsg_frame.h - RPMsg wire codec for the A53 <-> M7 CAN exchange.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. All device access
 * lives in platform/rpmsg_link.c.
 *
 * Layout source: Ref Docs/RPMSG_PROTOCOL.md v3.0.
 *
 * TWO LAYERS, OPPOSITE ENDIANNESS - the easiest thing in this file to get
 * wrong:
 *
 *   RPMsg header (11 bytes)   command and length are BIG-endian
 *   can_frame_msg_t (80 B)    timestamp_ms and can_id are LITTLE-endian
 *
 * The header is a wire format defined by the protocol document; the payload
 * is the M7's native struct copied verbatim. Do not "tidy" these into one
 * byte order and do not share a helper between them.
 */
#ifndef ME_RPMSG_FRAME_H
#define ME_RPMSG_FRAME_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_RPMSG_HDR_LEN      11u
#define ME_RPMSG_PAYLOAD_MAX  80u
#define ME_RPMSG_FRAME_MAX    (ME_RPMSG_HDR_LEN + ME_RPMSG_PAYLOAD_MAX) /* 91 */

#define ME_RPMSG_SYNC_BYTE        0xAAu

#define ME_RPMSG_ACTION_WRITE_SET 0x01u
#define ME_RPMSG_ACTION_READ      0x02u

#define ME_RPMSG_RESULT_REQUEST   0x00u
#define ME_RPMSG_RESULT_SUCCESS   0x01u
#define ME_RPMSG_RESULT_FAILURE   0xFFu

#define ME_RPMSG_CMD_CAN_SET_FRAME 0x00100001u
#define ME_RPMSG_CMD_CAN_GET_FRAME 0x00100002u

/* can_frame_msg_t is exactly 80 bytes with data[64] at offset 16. */
#define ME_RPMSG_CAN_MSG_LEN   80u
#define ME_RPMSG_CAN_DATA_OFF  16u
#define ME_RPMSG_CAN_DATA_MAX  64u

typedef struct {
    uint8_t  action;
    uint32_t command;
    uint8_t  result;
    uint32_t length;
} me_rpmsg_hdr_t;

/*
 * Build one complete frame into `out`. Returns the byte count written
 * (ME_RPMSG_HDR_LEN + payload_len), or 0 if payload_len exceeds
 * ME_RPMSG_PAYLOAD_MAX or `out` is too small. `payload` may be NULL when
 * payload_len is 0.
 */
size_t me_rpmsg_wrap(uint8_t action, uint32_t command, uint8_t result,
                     const uint8_t *payload, size_t payload_len,
                     uint8_t *out, size_t out_sz);

/*
 * Decode the 11-byte header at the start of `buf`. Returns false if `len` is
 * short, the sync byte is wrong, or `length` exceeds ME_RPMSG_PAYLOAD_MAX -
 * the last of which is the recovery trigger the stream parser needs
 * (RPMSG_PROTOCOL.md section 5 step 4).
 */
bool me_rpmsg_parse_header(const uint8_t *buf, size_t len,
                           me_rpmsg_hdr_t *out);

#endif /* ME_RPMSG_FRAME_H */
```

- [ ] **Step 4: Write the minimal implementation**

Create `me-primary/src/proto/rpmsg_frame.c`:

```c
/* rpmsg_frame.c - see rpmsg_frame.h for the format source. */
#include "rpmsg_frame.h"

#include <string.h>

/* Header fields only. The payload is little-endian and must never come
 * through here. */
static void put_u32_be(uint8_t *dst, uint32_t v)
{
    dst[0] = (uint8_t)(v >> 24);
    dst[1] = (uint8_t)(v >> 16);
    dst[2] = (uint8_t)(v >> 8);
    dst[3] = (uint8_t)(v);
}

static uint32_t get_u32_be(const uint8_t *src)
{
    return ((uint32_t)src[0] << 24) | ((uint32_t)src[1] << 16)
         | ((uint32_t)src[2] << 8)  | (uint32_t)src[3];
}

size_t me_rpmsg_wrap(uint8_t action, uint32_t command, uint8_t result,
                     const uint8_t *payload, size_t payload_len,
                     uint8_t *out, size_t out_sz)
{
    if (payload_len > ME_RPMSG_PAYLOAD_MAX) {
        return 0u;
    }
    const size_t total = ME_RPMSG_HDR_LEN + payload_len;
    if (out == NULL || out_sz < total) {
        return 0u;
    }
    if (payload_len > 0u && payload == NULL) {
        return 0u;
    }

    out[0] = ME_RPMSG_SYNC_BYTE;
    out[1] = action;
    put_u32_be(&out[2], command);
    out[6] = result;
    put_u32_be(&out[7], (uint32_t)payload_len);
    if (payload_len > 0u) {
        memcpy(&out[ME_RPMSG_HDR_LEN], payload, payload_len);
    }
    return total;
}

bool me_rpmsg_parse_header(const uint8_t *buf, size_t len,
                           me_rpmsg_hdr_t *out)
{
    if (buf == NULL || out == NULL || len < ME_RPMSG_HDR_LEN) {
        return false;
    }
    if (buf[0] != ME_RPMSG_SYNC_BYTE) {
        return false;
    }
    const uint32_t length = get_u32_be(&buf[7]);
    if (length > ME_RPMSG_PAYLOAD_MAX) {
        return false;
    }

    out->action  = buf[1];
    out->command = get_u32_be(&buf[2]);
    out->result  = buf[6];
    out->length  = length;
    return true;
}
```

Add to `me-primary/build-native.ps1` after the `src\proto\can_frame.c` line:

```powershell
    "$PSScriptRoot\src\proto\rpmsg_frame.c"
```

and after the `tests\test_step_engine.c` line:

```powershell
    "$PSScriptRoot\tests\test_rpmsg_frame.c"
```

Add to `me-primary/build.ps1` after the `src\proto\can_frame.c` line:

```powershell
    "$PSScriptRoot\src\proto\rpmsg_frame.c"
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\build-native.ps1`
Expected: PASS, check count risen by 12 (192 → 204).

- [ ] **Step 6: Verify the cross-build still compiles**

Run: `.\build.ps1`
Expected: clean build, static `ELF64 AArch64` binary produced.

- [ ] **Step 7: Commit**

```bash
git add "ME Project/me-primary/src/proto/rpmsg_frame.h" "ME Project/me-primary/src/proto/rpmsg_frame.c" "ME Project/me-primary/tests/test_rpmsg_frame.c" "ME Project/me-primary/tests/test_util.h" "ME Project/me-primary/tests/test_main.c" "ME Project/me-primary/build.ps1" "ME Project/me-primary/build-native.ps1"
git commit -m "Add RPMsg header codec with golden-vector tests"
```

---

## Task 2: `can_frame_msg_t` pack/parse and the DLC table

**Files:**
- Modify: `me-primary/src/proto/rpmsg_frame.h` (append before `#endif`)
- Modify: `me-primary/src/proto/rpmsg_frame.c` (append)
- Modify: `me-primary/tests/test_rpmsg_frame.c` (add cases, call them from `run_rpmsg_frame_tests`)

**Interfaces:**
- Consumes: everything from Task 1.
- Produces:
  - `me_rpmsg_can_t { uint32_t timestamp_ms; uint32_t can_id; uint8_t is_extended; uint8_t is_fd; uint8_t brs; uint8_t esi; uint8_t dlc; uint8_t data_len; uint8_t data[64]; }`
  - `bool me_rpmsg_pack_can(const me_rpmsg_can_t *in, uint8_t out80[80])`
  - `bool me_rpmsg_parse_can(const uint8_t in80[80], me_rpmsg_can_t *out)`
  - `uint8_t me_rpmsg_dlc_for_len(uint8_t data_len)`
  - `uint8_t me_rpmsg_len_for_dlc(uint8_t dlc)`

- [ ] **Step 1: Write the failing test**

Append to `me-primary/tests/test_rpmsg_frame.c`, before `run_rpmsg_frame_tests`:

```c
static void test_pack_can_golden(void)
{
    /* RPMSG_PROTOCOL.md section 8: Classic CAN, standard ID 0x18F, 8 bytes
     * 11 22 33 44 55 66 77 88. This is the full 91-byte vector the M7
     * firmware was verified against. */
    me_rpmsg_can_t in;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];
    uint8_t out[ME_RPMSG_FRAME_MAX];
    unsigned i;

    memset(&in, 0, sizeof(in));
    in.timestamp_ms = 0u;
    in.can_id       = 0x18Fu;
    in.is_extended  = 0u;
    in.is_fd        = 0u;
    in.brs          = 0u;
    in.esi          = 0u;
    in.dlc          = 8u;
    in.data_len     = 8u;
    for (i = 0u; i < 8u; i++) {
        in.data[i] = (uint8_t)(0x11u * (i + 1u));
    }

    TEST_CASE("rpmsg can: pack the section-8 payload");
    CHECK(me_rpmsg_pack_can(&in, payload));
    CHECK_BYTE(payload, 0, 0x00); /* timestamp_ms = 0, little-endian */
    CHECK_BYTE(payload, 1, 0x00);
    CHECK_BYTE(payload, 2, 0x00);
    CHECK_BYTE(payload, 3, 0x00);
    CHECK_BYTE(payload, 4, 0x8F); /* can_id = 0x18F, LITTLE-endian   */
    CHECK_BYTE(payload, 5, 0x01);
    CHECK_BYTE(payload, 6, 0x00);
    CHECK_BYTE(payload, 7, 0x00);
    CHECK_BYTE(payload, 8, 0x00);  /* is_extended */
    CHECK_BYTE(payload, 9, 0x00);  /* is_fd       */
    CHECK_BYTE(payload, 10, 0x00); /* brs         */
    CHECK_BYTE(payload, 11, 0x00); /* esi         */
    CHECK_BYTE(payload, 12, 0x08); /* dlc         */
    CHECK_BYTE(payload, 13, 0x08); /* data_len    */
    CHECK_BYTE(payload, 14, 0x00); /* reserved    */
    CHECK_BYTE(payload, 15, 0x00);
    CHECK_BYTE(payload, 16, 0x11); /* data[0] at offset 16 */
    CHECK_BYTE(payload, 23, 0x88); /* data[7]              */

    TEST_CASE("rpmsg can: bytes past data_len are zero-filled");
    for (i = 24u; i < ME_RPMSG_CAN_MSG_LEN; i++) {
        CHECK_BYTE(payload, i, 0x00);
    }

    TEST_CASE("rpmsg can: the full 91-byte section-8 frame");
    CHECK_EQ_U(91u, me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                  ME_RPMSG_CMD_CAN_SET_FRAME,
                                  ME_RPMSG_RESULT_REQUEST,
                                  payload, ME_RPMSG_CAN_MSG_LEN,
                                  out, sizeof(out)));
    CHECK_BYTE(out, 11, 0x00); /* payload starts right after the header */
    CHECK_BYTE(out, 15, 0x8F); /* can_id low byte, at frame offset 11+4 */
    CHECK_BYTE(out, 27, 0x11); /* data[0], at frame offset 11+16        */
}

static void test_can_round_trip(void)
{
    me_rpmsg_can_t in;
    me_rpmsg_can_t back;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];
    unsigned i;

    memset(&in, 0, sizeof(in));
    in.timestamp_ms = 0x12345678u;
    in.can_id       = 0x022u; /* Secondary 1, function 0x02 */
    in.is_extended  = 0u;
    in.is_fd        = 1u;
    in.brs          = 1u;
    in.esi          = 0u;
    in.dlc          = 15u;
    in.data_len     = 64u;
    for (i = 0u; i < 64u; i++) {
        in.data[i] = (uint8_t)(0xA0u + i);
    }

    TEST_CASE("rpmsg can: pack then parse recovers every field");
    CHECK(me_rpmsg_pack_can(&in, payload));
    memset(&back, 0, sizeof(back));
    CHECK(me_rpmsg_parse_can(payload, &back));
    CHECK_EQ_U(in.timestamp_ms, back.timestamp_ms);
    CHECK_EQ_U(in.can_id, back.can_id);
    CHECK_EQ_U(in.is_extended, back.is_extended);
    CHECK_EQ_U(in.is_fd, back.is_fd);
    CHECK_EQ_U(in.brs, back.brs);
    CHECK_EQ_U(in.esi, back.esi);
    CHECK_EQ_U(in.dlc, back.dlc);
    CHECK_EQ_U(in.data_len, back.data_len);
    CHECK_MEM(back.data, in.data, 64u);
}

static void test_can_rejects_bad_len(void)
{
    me_rpmsg_can_t in;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];

    memset(&in, 0, sizeof(in));
    in.data_len = 65u; /* over the 64-byte ceiling */

    TEST_CASE("rpmsg can: data_len over 64 is refused");
    CHECK(!me_rpmsg_pack_can(&in, payload));
}

static void test_dlc_table(void)
{
    TEST_CASE("rpmsg dlc: 1:1 region, 0-8 bytes");
    CHECK_EQ_U(0u, me_rpmsg_dlc_for_len(0u));
    CHECK_EQ_U(8u, me_rpmsg_dlc_for_len(8u));

    TEST_CASE("rpmsg dlc: intermediate lengths pad up");
    CHECK_EQ_U(9u,  me_rpmsg_dlc_for_len(9u));  /* -> 12 */
    CHECK_EQ_U(9u,  me_rpmsg_dlc_for_len(12u));
    CHECK_EQ_U(10u, me_rpmsg_dlc_for_len(13u)); /* -> 16 */
    CHECK_EQ_U(10u, me_rpmsg_dlc_for_len(16u));
    CHECK_EQ_U(11u, me_rpmsg_dlc_for_len(20u));
    CHECK_EQ_U(12u, me_rpmsg_dlc_for_len(24u));
    CHECK_EQ_U(13u, me_rpmsg_dlc_for_len(32u));
    CHECK_EQ_U(14u, me_rpmsg_dlc_for_len(48u));
    CHECK_EQ_U(15u, me_rpmsg_dlc_for_len(64u));

    TEST_CASE("rpmsg dlc: the 64-byte frame this project sends");
    CHECK_EQ_U(15u, me_rpmsg_dlc_for_len(ME_CAN_PAYLOAD_BYTES));

    TEST_CASE("rpmsg dlc: reverse lookup");
    CHECK_EQ_U(8u,  me_rpmsg_len_for_dlc(8u));
    CHECK_EQ_U(12u, me_rpmsg_len_for_dlc(9u));
    CHECK_EQ_U(16u, me_rpmsg_len_for_dlc(10u));
    CHECK_EQ_U(20u, me_rpmsg_len_for_dlc(11u));
    CHECK_EQ_U(24u, me_rpmsg_len_for_dlc(12u));
    CHECK_EQ_U(32u, me_rpmsg_len_for_dlc(13u));
    CHECK_EQ_U(48u, me_rpmsg_len_for_dlc(14u));
    CHECK_EQ_U(64u, me_rpmsg_len_for_dlc(15u));

    TEST_CASE("rpmsg dlc: out-of-range dlc reads back as 0");
    CHECK_EQ_U(0u, me_rpmsg_len_for_dlc(16u));
}
```

Note `ME_CAN_PAYLOAD_BYTES` above — add this to `rpmsg_frame.h` in Step 3 as `#define ME_CAN_PAYLOAD_BYTES 64u`. It exists so the test states the intent ("the 64-byte frame this project sends") rather than repeating a bare literal.

Extend `run_rpmsg_frame_tests` to call the four new functions:

```c
void run_rpmsg_frame_tests(void)
{
    printf("rpmsg_frame...\n");
    test_wrap_header_golden();
    test_wrap_rejects_oversize();
    test_parse_header_ack();
    test_parse_header_rejects();
    test_pack_can_golden();
    test_can_round_trip();
    test_can_rejects_bad_len();
    test_dlc_table();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `.\build-native.ps1`
Expected: FAIL — `me_rpmsg_pack_can` undeclared.

- [ ] **Step 3: Add the declarations**

Append to `me-primary/src/proto/rpmsg_frame.h` before `#endif`:

```c
/* The CAN-FD payload size this project always sends: one 64-byte block
 * frame per Secondary (Ref Docs/master_slave_can_v1.0.md). */
#define ME_CAN_PAYLOAD_BYTES 64u

/*
 * The M7's native can_frame_msg_t, unpacked into host fields.
 *
 * This is NOT the wire struct - do not memcpy it onto the wire. The wire
 * form is produced by me_rpmsg_pack_can(), which writes the 80 bytes with
 * explicit little-endian stores. A packed-struct memcpy would be a silent
 * trap the day this builds for a big-endian host, and would depend on
 * compiler padding rules besides.
 */
typedef struct {
    uint32_t timestamp_ms;
    uint32_t can_id;
    uint8_t  is_extended;
    uint8_t  is_fd;
    uint8_t  brs;
    uint8_t  esi;
    uint8_t  dlc;
    uint8_t  data_len;
    uint8_t  data[ME_RPMSG_CAN_DATA_MAX];
} me_rpmsg_can_t;

/*
 * Write the 80-byte can_frame_msg_t. All multi-byte fields little-endian.
 * Bytes past in->data_len are zero-filled. Returns false if data_len
 * exceeds 64.
 */
bool me_rpmsg_pack_can(const me_rpmsg_can_t *in,
                       uint8_t out80[ME_RPMSG_CAN_MSG_LEN]);

/*
 * Read the 80-byte can_frame_msg_t back. Returns false if the encoded
 * data_len exceeds 64 - a frame the M7 should never send, so it is treated
 * as corruption rather than clamped.
 */
bool me_rpmsg_parse_can(const uint8_t in80[ME_RPMSG_CAN_MSG_LEN],
                        me_rpmsg_can_t *out);

/*
 * CAN-FD DLC code for a real byte count, padding up to the next valid
 * length (RPMSG_PROTOCOL.md section 3 table). data_len over 64 returns 15.
 */
uint8_t me_rpmsg_dlc_for_len(uint8_t data_len);

/* Real byte count for a DLC code. Returns 0 for dlc > 15. */
uint8_t me_rpmsg_len_for_dlc(uint8_t dlc);
```

- [ ] **Step 4: Write the implementation**

Append to `me-primary/src/proto/rpmsg_frame.c`:

```c
/* Payload fields only - little-endian, the M7's native order. Deliberately
 * separate from the big-endian header helpers above. */
static void put_u32_le(uint8_t *dst, uint32_t v)
{
    dst[0] = (uint8_t)(v);
    dst[1] = (uint8_t)(v >> 8);
    dst[2] = (uint8_t)(v >> 16);
    dst[3] = (uint8_t)(v >> 24);
}

static uint32_t get_u32_le(const uint8_t *src)
{
    return (uint32_t)src[0] | ((uint32_t)src[1] << 8)
         | ((uint32_t)src[2] << 16) | ((uint32_t)src[3] << 24);
}

/* Index = DLC - 9; DLC 0-8 map 1:1 and are handled arithmetically. */
static const uint8_t k_dlc_len[7] = { 12u, 16u, 20u, 24u, 32u, 48u, 64u };

uint8_t me_rpmsg_dlc_for_len(uint8_t data_len)
{
    uint8_t i;

    if (data_len <= 8u) {
        return data_len;
    }
    for (i = 0u; i < 7u; i++) {
        if (data_len <= k_dlc_len[i]) {
            return (uint8_t)(9u + i);
        }
    }
    return 15u;
}

uint8_t me_rpmsg_len_for_dlc(uint8_t dlc)
{
    if (dlc <= 8u) {
        return dlc;
    }
    if (dlc <= 15u) {
        return k_dlc_len[dlc - 9u];
    }
    return 0u;
}

bool me_rpmsg_pack_can(const me_rpmsg_can_t *in,
                       uint8_t out80[ME_RPMSG_CAN_MSG_LEN])
{
    if (in == NULL || out80 == NULL) {
        return false;
    }
    if (in->data_len > ME_RPMSG_CAN_DATA_MAX) {
        return false;
    }

    memset(out80, 0, ME_RPMSG_CAN_MSG_LEN);
    put_u32_le(&out80[0], in->timestamp_ms);
    put_u32_le(&out80[4], in->can_id);
    out80[8]  = in->is_extended;
    out80[9]  = in->is_fd;
    out80[10] = in->brs;
    out80[11] = in->esi;
    out80[12] = in->dlc;
    out80[13] = in->data_len;
    /* out80[14..15] stay zero: the reserved field. */
    if (in->data_len > 0u) {
        memcpy(&out80[ME_RPMSG_CAN_DATA_OFF], in->data, in->data_len);
    }
    return true;
}

bool me_rpmsg_parse_can(const uint8_t in80[ME_RPMSG_CAN_MSG_LEN],
                        me_rpmsg_can_t *out)
{
    if (in80 == NULL || out == NULL) {
        return false;
    }
    if (in80[13] > ME_RPMSG_CAN_DATA_MAX) {
        return false;
    }

    out->timestamp_ms = get_u32_le(&in80[0]);
    out->can_id       = get_u32_le(&in80[4]);
    out->is_extended  = in80[8];
    out->is_fd        = in80[9];
    out->brs          = in80[10];
    out->esi          = in80[11];
    out->dlc          = in80[12];
    out->data_len     = in80[13];
    memcpy(out->data, &in80[ME_RPMSG_CAN_DATA_OFF], ME_RPMSG_CAN_DATA_MAX);
    return true;
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\build-native.ps1`
Expected: PASS. Check count risen by roughly 100 (the zero-fill loop contributes 56 on its own).

- [ ] **Step 6: Commit**

```bash
git add "ME Project/me-primary/src/proto/rpmsg_frame.h" "ME Project/me-primary/src/proto/rpmsg_frame.c" "ME Project/me-primary/tests/test_rpmsg_frame.c"
git commit -m "Add can_frame_msg_t pack/parse and CAN-FD DLC table"
```

---

## Task 3: Stream reassembler

**Files:**
- Modify: `me-primary/src/proto/rpmsg_frame.h` (append before `#endif`)
- Modify: `me-primary/src/proto/rpmsg_frame.c` (append)
- Modify: `me-primary/tests/test_rpmsg_frame.c` (add cases, call them from `run_rpmsg_frame_tests`)

**Interfaces:**
- Consumes: `me_rpmsg_hdr_t`, `me_rpmsg_parse_header` from Task 1.
- Produces:
  - `me_rpmsg_stream_t` (opaque-by-convention struct, caller allocates)
  - `void me_rpmsg_stream_init(me_rpmsg_stream_t *s)`
  - `size_t me_rpmsg_stream_push(me_rpmsg_stream_t *s, const uint8_t *data, size_t len)` — returns bytes accepted
  - `bool me_rpmsg_stream_next(me_rpmsg_stream_t *s, me_rpmsg_hdr_t *hdr, const uint8_t **payload)`

**Why this is its own module:** `/dev/ttyRPMSG30` is a byte stream — one `read()` may hold half a frame, one frame, or several. Reassembly is the single most bug-prone part of this feature and is entirely pure logic, so it gets host tests instead of hardware debugging.

- [ ] **Step 1: Write the failing test**

Append to `me-primary/tests/test_rpmsg_frame.c`, before `run_rpmsg_frame_tests`:

```c
/* Build one complete GET_FRAME stream message (11 + 80 bytes) carrying the
 * given CAN ID, for the stream tests below. */
static size_t make_get_frame(uint32_t can_id, uint8_t *out,
                             size_t out_sz)
{
    me_rpmsg_can_t can;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];

    memset(&can, 0, sizeof(can));
    can.can_id   = can_id;
    can.is_fd    = 1u;
    can.brs      = 1u;
    can.dlc      = 15u;
    can.data_len = 64u;
    can.data[0]  = 0x5Au;
    (void)me_rpmsg_pack_can(&can, payload);

    return me_rpmsg_wrap(ME_RPMSG_ACTION_READ, ME_RPMSG_CMD_CAN_GET_FRAME,
                         ME_RPMSG_RESULT_SUCCESS, payload,
                         ME_RPMSG_CAN_MSG_LEN, out, out_sz);
}

static void test_stream_single_frame(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t frame[ME_RPMSG_FRAME_MAX];
    size_t n;

    me_rpmsg_stream_init(&s);
    n = make_get_frame(0x022u, frame, sizeof(frame));

    TEST_CASE("rpmsg stream: one whole frame in one push");
    CHECK_EQ_U(91u, n);
    CHECK_EQ_U(n, me_rpmsg_stream_push(&s, frame, n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_EQ_U(80u, h.length);
    CHECK(payload != NULL);
    CHECK_BYTE(payload, 4, 0x22); /* can_id 0x022, little-endian */
    CHECK_BYTE(payload, 5, 0x00);

    TEST_CASE("rpmsg stream: nothing left after the frame is taken");
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));
}

static void test_stream_split_across_reads(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t frame[ME_RPMSG_FRAME_MAX];
    size_t n;

    me_rpmsg_stream_init(&s);
    n = make_get_frame(0x042u, frame, sizeof(frame));

    TEST_CASE("rpmsg stream: a frame split mid-header yields nothing yet");
    CHECK_EQ_U(5u, me_rpmsg_stream_push(&s, frame, 5u));
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));

    TEST_CASE("rpmsg stream: split mid-payload still yields nothing");
    CHECK_EQ_U(40u, me_rpmsg_stream_push(&s, &frame[5], 40u));
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));

    TEST_CASE("rpmsg stream: the frame appears once the last byte lands");
    CHECK_EQ_U(n - 45u, me_rpmsg_stream_push(&s, &frame[45], n - 45u));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(80u, h.length);
    CHECK_BYTE(payload, 4, 0x42);
}

static void test_stream_two_frames_one_push(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX * 2u];
    size_t n1;
    size_t n2;

    me_rpmsg_stream_init(&s);
    n1 = make_get_frame(0x022u, buf, sizeof(buf));
    n2 = make_get_frame(0x042u, &buf[n1], sizeof(buf) - n1);

    TEST_CASE("rpmsg stream: two concatenated frames both come out");
    CHECK_EQ_U(n1 + n2, me_rpmsg_stream_push(&s, buf, n1 + n2));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_BYTE(payload, 4, 0x22);
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_BYTE(payload, 4, 0x42);
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));
}

static void test_stream_ack_then_frame(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX * 2u];
    const uint8_t ack[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x01, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00
    };
    size_t n;

    me_rpmsg_stream_init(&s);
    memcpy(buf, ack, sizeof(ack));
    n = make_get_frame(0x022u, &buf[sizeof(ack)], sizeof(buf) - sizeof(ack));

    TEST_CASE("rpmsg stream: a zero-length ACK is a complete frame");
    CHECK_EQ_U(sizeof(ack) + n,
               me_rpmsg_stream_push(&s, buf, sizeof(ack) + n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_SET_FRAME, h.command);
    CHECK_EQ_U(ME_RPMSG_RESULT_SUCCESS, h.result);
    CHECK_EQ_U(0u, h.length);

    TEST_CASE("rpmsg stream: the frame behind the ACK is still found");
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_EQ_U(80u, h.length);
}

static void test_stream_resyncs_on_garbage(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX + 4u];
    size_t n;

    me_rpmsg_stream_init(&s);
    buf[0] = 0x01;
    buf[1] = 0x02;
    buf[2] = 0x03;
    n = make_get_frame(0x022u, &buf[3], sizeof(buf) - 3u);

    TEST_CASE("rpmsg stream: leading garbage is discarded, frame recovered");
    CHECK_EQ_U(3u + n, me_rpmsg_stream_push(&s, buf, 3u + n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_BYTE(payload, 4, 0x22);
}

static void test_stream_skips_implausible_length(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX + ME_RPMSG_HDR_LEN];
    /* A sync byte followed by length 0x51 (81) - over the 80-byte max, so
     * this is not a real header. The parser must skip ONE byte and rescan,
     * not discard the whole buffer, or it would eat the good frame behind
     * it. */
    const uint8_t bad[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x02, 0x00, 0x10, 0x00, 0x02, 0x01, 0x00, 0x00, 0x00, 0x51
    };
    size_t n;

    me_rpmsg_stream_init(&s);
    memcpy(buf, bad, sizeof(bad));
    n = make_get_frame(0x022u, &buf[sizeof(bad)], sizeof(buf) - sizeof(bad));

    TEST_CASE("rpmsg stream: length over 80 resyncs onto the next frame");
    CHECK_EQ_U(sizeof(bad) + n,
               me_rpmsg_stream_push(&s, buf, sizeof(bad) + n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_EQ_U(80u, h.length);
    CHECK_BYTE(payload, 4, 0x22);
}

static void test_stream_overflow_is_bounded(void)
{
    me_rpmsg_stream_t s;
    uint8_t junk[256];
    size_t accepted = 0u;
    unsigned i;

    me_rpmsg_stream_init(&s);
    memset(junk, 0x00, sizeof(junk)); /* no sync byte anywhere */

    TEST_CASE("rpmsg stream: sync-less junk never overruns the buffer");
    for (i = 0u; i < 64u; i++) {
        accepted += me_rpmsg_stream_push(&s, junk, sizeof(junk));
    }
    /* 16 KB pushed into a buffer that cannot hold it: the module must have
     * dropped bytes rather than written past the end. Reaching this line
     * without a crash or a sanitizer trip is the assertion. */
    CHECK(accepted <= 64u * sizeof(junk));
    CHECK(s.used <= ME_RPMSG_STREAM_BUF_SZ);
}
```

Extend `run_rpmsg_frame_tests` with the seven new calls:

```c
    test_stream_single_frame();
    test_stream_split_across_reads();
    test_stream_two_frames_one_push();
    test_stream_ack_then_frame();
    test_stream_resyncs_on_garbage();
    test_stream_skips_implausible_length();
    test_stream_overflow_is_bounded();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `.\build-native.ps1`
Expected: FAIL — `me_rpmsg_stream_t` undeclared.

- [ ] **Step 3: Add the declarations**

Append to `me-primary/src/proto/rpmsg_frame.h` before `#endif`:

```c
/*
 * Reassembly buffer. One read() from the RPMsg device can deliver up to the
 * 496-byte buffer payload, and a partial frame may already be held, so the
 * buffer is 496 + 91 rounded up. Sized as a compile-time constant because
 * there is no dynamic allocation anywhere in this codebase.
 */
#define ME_RPMSG_STREAM_BUF_SZ 640u

typedef struct {
    uint8_t buf[ME_RPMSG_STREAM_BUF_SZ];
    size_t  used;
    /*
     * Size of the frame handed out by the last me_rpmsg_stream_next(), still
     * sitting at the front of buf[]. It is dropped at the START of the next
     * call rather than at the end of this one, because the payload pointer
     * returned to the caller points into buf[] - consuming immediately would
     * hand back a pointer to bytes already slid away.
     */
    size_t  pending;
} me_rpmsg_stream_t;

void me_rpmsg_stream_init(me_rpmsg_stream_t *s);

/*
 * Append raw bytes from one read(). Returns how many were accepted - fewer
 * than `len` means the buffer was full and the excess was dropped, which the
 * caller should log. Dropping is deliberate: growing without bound, or
 * discarding the whole buffer, both turn a transient overrun into a
 * permanent desync.
 */
size_t me_rpmsg_stream_push(me_rpmsg_stream_t *s, const uint8_t *data,
                            size_t len);

/*
 * Extract the next complete frame. Returns false when the buffer holds no
 * whole frame yet. On true, *payload points INTO the stream buffer and stays
 * valid only until the next push or next call - copy it if you need to keep
 * it. *payload is NULL when hdr->length is 0.
 *
 * Call in a loop until it returns false: one push can contain several frames.
 */
bool me_rpmsg_stream_next(me_rpmsg_stream_t *s, me_rpmsg_hdr_t *hdr,
                          const uint8_t **payload);
```

- [ ] **Step 4: Write the implementation**

Append to `me-primary/src/proto/rpmsg_frame.c`:

```c
void me_rpmsg_stream_init(me_rpmsg_stream_t *s)
{
    if (s != NULL) {
        s->used    = 0u;
        s->pending = 0u;
    }
}

size_t me_rpmsg_stream_push(me_rpmsg_stream_t *s, const uint8_t *data,
                            size_t len)
{
    size_t space;

    if (s == NULL || data == NULL || len == 0u) {
        return 0u;
    }
    space = ME_RPMSG_STREAM_BUF_SZ - s->used;
    if (len > space) {
        len = space;
    }
    if (len > 0u) {
        memcpy(&s->buf[s->used], data, len);
        s->used += len;
    }
    return len;
}

/* Drop the first `n` bytes, sliding the remainder down. */
static void stream_consume(me_rpmsg_stream_t *s, size_t n)
{
    if (n >= s->used) {
        s->used = 0u;
        return;
    }
    memmove(&s->buf[0], &s->buf[n], s->used - n);
    s->used -= n;
}

bool me_rpmsg_stream_next(me_rpmsg_stream_t *s, me_rpmsg_hdr_t *hdr,
                          const uint8_t **payload)
{
    if (s == NULL || hdr == NULL || payload == NULL) {
        return false;
    }

    /* Drop the frame handed out last call, now that the caller has finished
     * reading it. See the `pending` comment in the header. */
    if (s->pending > 0u) {
        stream_consume(s, s->pending);
        s->pending = 0u;
    }

    for (;;) {
        size_t i;
        size_t total;

        /* 1. Scan to the sync byte, discarding anything before it. */
        for (i = 0u; i < s->used; i++) {
            if (s->buf[i] == ME_RPMSG_SYNC_BYTE) {
                break;
            }
        }
        if (i > 0u) {
            stream_consume(s, i);
        }

        /* 2. Need a whole header before the length field can be trusted. */
        if (s->used < ME_RPMSG_HDR_LEN) {
            return false;
        }

        /* 3. A header that will not parse - wrong sync, or a length over 80 -
         *    means this 0xAA was payload data, not a real frame start. Skip
         *    exactly one byte and rescan (RPMSG_PROTOCOL.md section 5 step 4).
         *    Discarding more would swallow the genuine frame behind it. */
        if (!me_rpmsg_parse_header(s->buf, s->used, hdr)) {
            stream_consume(s, 1u);
            continue;
        }

        /* 4. Wait for the payload to arrive. */
        total = ME_RPMSG_HDR_LEN + (size_t)hdr->length;
        if (s->used < total) {
            return false;
        }

        /* 5. One whole frame. The payload pointer aliases the buffer, which
         *    is why the contract says it dies on the next call. */
        *payload = (hdr->length > 0u) ? &s->buf[ME_RPMSG_HDR_LEN] : NULL;

        /* The caller reads *payload before touching the stream again, so
         * consuming here would invalidate it. Instead record the frame size
         * and drop it at the START of the next call. */
        s->pending = total;
        return true;
    }
}
```

`stream_consume` is defined above `me_rpmsg_stream_next` in the file, so no
forward declaration is needed. `me_rpmsg_stream_push` does not touch
`pending`: pushing appends at `used` and never moves the front of the buffer,
so a frame handed out earlier stays exactly where the caller's pointer says it
is.

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\build-native.ps1`
Expected: PASS. Check count risen by roughly 30.

- [ ] **Step 6: Verify the cross-build still compiles**

Run: `.\build.ps1`
Expected: clean.

- [ ] **Step 7: Commit**

```bash
git add "ME Project/me-primary/src/proto/rpmsg_frame.h" "ME Project/me-primary/src/proto/rpmsg_frame.c" "ME Project/me-primary/tests/test_rpmsg_frame.c"
git commit -m "Add RPMsg stream reassembler with resync and overflow tests"
```

---

## Task 4: Device I/O layer

**Files:**
- Create: `me-primary/src/platform/rpmsg_link.h`, `me-primary/src/platform/rpmsg_link.c`
- Modify: `me-primary/build.ps1` (add `src\platform\rpmsg_link.c` after the `src\platform\netinfo.c` line)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `int me_rpmsg_open(const char *path)` — returns fd, or `-1`
  - `bool me_rpmsg_write_all(int fd, const uint8_t *buf, size_t len)`
  - `void me_rpmsg_close(int fd)`
  - `const char *me_rpmsg_device_path(void)` — `ME_RPMSG_DEV` env var, or the default
  - `#define ME_RPMSG_DEV_DEFAULT "/dev/ttyRPMSG30"`

**No host tests.** This module is `open`/`read`/`write`/`termios` and nothing else; it is Linux-only, excluded from `build-native.ps1`, and proven only by hardware-in-the-loop. Everything worth testing was deliberately pushed into `rpmsg_frame.c` in Tasks 1–3.

- [ ] **Step 1: Write the header**

Create `me-primary/src/platform/rpmsg_link.h`:

```c
/*
 * rpmsg_link.h - character-device I/O for the A53 <-> M7 RPMsg channel.
 *
 * LINUX ONLY. Not built by build-native.ps1.
 *
 * This module is deliberately thin: open, write, close, and nothing else.
 * Every byte-layout decision lives in proto/rpmsg_frame.c so that it can be
 * unit-tested on the dev laptop without a board. Keep it that way - logic
 * added here becomes logic that only hardware can test.
 *
 * Device and channel parameters: Ref Docs/RPMSG_PROTOCOL.md section 1.
 */
#ifndef ME_RPMSG_LINK_H
#define ME_RPMSG_LINK_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_RPMSG_DEV_DEFAULT "/dev/ttyRPMSG30"

/*
 * The device path: the ME_RPMSG_DEV environment variable when set and
 * non-empty, otherwise ME_RPMSG_DEV_DEFAULT. Returns a pointer that stays
 * valid for the life of the process.
 */
const char *me_rpmsg_device_path(void);

/*
 * Open the device in raw mode. Returns the fd, or -1 (already logged).
 * A -1 return is NOT fatal to the application: the caller degrades to
 * dropping CAN traffic, so a board with a stopped M7 still registers and
 * stays diagnosable.
 */
int me_rpmsg_open(const char *path);

/*
 * Write every byte, retrying on short writes and EINTR. Returns false on a
 * real error (already logged).
 *
 * The protocol wants one frame per write() (RPMSG_PROTOCOL.md section 8
 * step 3). The retry loop exists for correctness under EINTR, not to split
 * frames deliberately.
 */
bool me_rpmsg_write_all(int fd, const uint8_t *buf, size_t len);

/* Safe on -1. */
void me_rpmsg_close(int fd);

#endif /* ME_RPMSG_LINK_H */
```

- [ ] **Step 2: Write the implementation**

Create `me-primary/src/platform/rpmsg_link.c`:

```c
/* rpmsg_link.c - see rpmsg_link.h. Linux only. */
#include "rpmsg_link.h"

#include <errno.h>
#include <fcntl.h>
#include <stdlib.h>
#include <string.h>
#include <termios.h>
#include <unistd.h>

#include "../util/log.h"

const char *me_rpmsg_device_path(void)
{
    const char *env = getenv("ME_RPMSG_DEV");

    if (env != NULL && env[0] != '\0') {
        return env;
    }
    return ME_RPMSG_DEV_DEFAULT;
}

int me_rpmsg_open(const char *path)
{
    struct termios tio;
    int fd;

    if (path == NULL) {
        return -1;
    }

    fd = open(path, O_RDWR | O_NOCTTY);
    if (fd < 0) {
        ME_LOGE("rpmsg: cannot open %s: %s", path, strerror(errno));
        return -1;
    }

    /* Raw mode, matching the reference implementation's tty.setraw(). Without
     * it the line discipline would translate CR/LF and interpret control
     * bytes - and a 64-byte CAN payload contains arbitrary bytes, so any
     * translation silently corrupts frames. */
    if (tcgetattr(fd, &tio) != 0) {
        ME_LOGE("rpmsg: tcgetattr on %s failed: %s", path, strerror(errno));
        (void)close(fd);
        return -1;
    }
    cfmakeraw(&tio);
    tio.c_cc[VMIN]  = 0; /* read() returns whatever is available... */
    tio.c_cc[VTIME] = 0; /* ...without blocking on a character timer */
    if (tcsetattr(fd, TCSANOW, &tio) != 0) {
        ME_LOGE("rpmsg: tcsetattr on %s failed: %s", path, strerror(errno));
        (void)close(fd);
        return -1;
    }

    ME_LOGI("rpmsg: opened %s (fd %d)", path, fd);
    return fd;
}

bool me_rpmsg_write_all(int fd, const uint8_t *buf, size_t len)
{
    size_t done = 0u;

    if (fd < 0 || buf == NULL) {
        return false;
    }

    while (done < len) {
        const ssize_t n = write(fd, &buf[done], len - done);
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("rpmsg: write failed after %u of %u bytes: %s",
                    (unsigned)done, (unsigned)len, strerror(errno));
            return false;
        }
        done += (size_t)n;
    }
    return true;
}

void me_rpmsg_close(int fd)
{
    if (fd >= 0) {
        (void)close(fd);
    }
}
```

Add to `me-primary/build.ps1` after the `src\platform\netinfo.c` line:

```powershell
    "$PSScriptRoot\src\platform\rpmsg_link.c"
```

- [ ] **Step 3: Verify it compiles**

Run: `.\build.ps1`
Expected: clean build under `-Werror`.

Note: nothing calls this module yet, so a `-Wunused` style error is not expected — these are non-static external functions.

- [ ] **Step 4: Run the host tests to confirm nothing regressed**

Run: `.\build-native.ps1`
Expected: PASS, same check count as after Task 3.

- [ ] **Step 5: Commit**

```bash
git add "ME Project/me-primary/src/platform/rpmsg_link.h" "ME Project/me-primary/src/platform/rpmsg_link.c" "ME Project/me-primary/build.ps1"
git commit -m "Add RPMsg character-device I/O layer"
```

---

## Task 5: Make `g_q_can` pollable

**Files:**
- Modify: `me-primary/src/app_queues.c:33`

**Interfaces:**
- Consumes: nothing.
- Produces: `g_q_can` now returns a valid fd from `me_msgq_fd()` on Linux.

- [ ] **Step 1: Make the change**

In `me-primary/src/app_queues.c`, change line 33 from:

```c
    if (!me_msgq_init(&g_q_can, "can", false)) {
```

to:

```c
    /* Pollable: the CAN manager waits on this queue and the RPMsg device fd
     * in one poll(), the same way the communication thread waits on g_q_comm
     * alongside its sockets. */
    if (!me_msgq_init(&g_q_can, "can", true)) {
```

Also update the header comment in `me-primary/src/app_queues.h`, changing:

```c
 *     g_q_comm   Communication thread   (POLLABLE - carries an eventfd)
 *     g_q_core   Core Logic thread
 *     g_q_data   Data Manager thread
 *     g_q_can    CAN Data Manager thread
 *
 * g_q_comm is the only pollable one, because the communication thread is
 * already blocked in poll() on three sockets and cannot also block on a queue.
```

to:

```c
 *     g_q_comm   Communication thread   (POLLABLE - carries an eventfd)
 *     g_q_core   Core Logic thread
 *     g_q_data   Data Manager thread
 *     g_q_can    CAN Data Manager thread (POLLABLE - carries an eventfd)
 *
 * The two pollable queues belong to the two threads that must wait on a
 * descriptor at the same time: the communication thread on three sockets, and
 * the CAN manager on the RPMsg device. Neither can afford to block on a queue
 * instead.
```

- [ ] **Step 2: Verify both builds**

Run: `.\build.ps1` then `.\build-native.ps1`
Expected: both clean; host tests PASS with the same check count as Task 4. On the host the `pollable` flag is silently ignored (`evt_fd` stays -1), which is why no host test changes.

- [ ] **Step 3: Commit**

```bash
git add "ME Project/me-primary/src/app_queues.c" "ME Project/me-primary/src/app_queues.h"
git commit -m "Make g_q_can pollable for the CAN manager's poll loop"
```

---

## Task 6: Rewrite `can_mgr.c` as the RPMsg transport

**Files:**
- Modify: `me-primary/src/threads/can_mgr.c` (full rewrite)
- Modify: `me-primary/src/threads/can_mgr.h` (rewrite the header comment; the two function declarations are unchanged)

**Interfaces:**
- Consumes: all of `rpmsg_frame.h` (Tasks 1–3), all of `rpmsg_link.h` (Task 4), a pollable `g_q_can` (Task 5).
- Produces: unchanged public interface — `bool me_can_mgr_start(void)` and `void me_can_mgr_join(void)`.

**Deleted in this task:** `can_sim_t`, `s_sim[]`, `ME_CAN_SIM_VOLTAGE_START`, `ME_CAN_SIM_VOLTAGE_STEP`, `ME_CAN_SIM_VOLTAGE_MAX`, `send_response()`, `handle_set_values()`, `handle_read_values()`. Remove the now-unused `#include "../proto/can_frame.h"` only if nothing else in the file needs it — it is still needed for `ME_CAN_FRAME_LEN` and `ME_CAN_FUNC_READ`, so keep it.

- [ ] **Step 1: Rewrite the header comment**

Replace the comment block at the top of `me-primary/src/threads/can_mgr.h` (lines 1–14) with:

```c
/*
 * can_mgr.h - the CAN Data Manager thread.
 *
 * Transports CAN-FD frames between Core Logic and the M7 core over RPMsg.
 * Core Logic sends ME_MSG_CAN_TX (CAN ID in `offset`, 64-byte frame in
 * `payload`); this thread wraps it per Ref Docs/RPMSG_PROTOCOL.md and writes
 * it to /dev/ttyRPMSG30. The M7 streams the Secondary's replies back, which
 * this thread turns into ME_MSG_CAN_DATA for Core Logic.
 *
 * The M7 firmware is owned separately (me_battery_m7_core) and does the
 * actual FlexCAN work. Nothing here talks to a CAN controller.
 *
 * If the device cannot be opened the thread still runs, drains the queue and
 * counts drops, so a board with a stopped M7 stays up and diagnosable.
 *
 * Design: Docs/specs/2026-08-18-rpmsg-can-transport-design.md
 *
 * Linux-only. Not built by build-native.ps1.
 */
```

- [ ] **Step 2: Rewrite the implementation**

Replace the entire contents of `me-primary/src/threads/can_mgr.c` with:

```c
/*
 * can_mgr.c - the CAN Data Manager thread.
 *
 * One poll() loop over two sources:
 *
 *   g_q_can eventfd   ME_MSG_CAN_TX from Core Logic -> wrap -> write()
 *   rpmsg fd          read() -> reassemble -> ACK counters, or
 *                     ME_MSG_CAN_DATA -> g_q_core
 *
 * Single-threaded by design: one owner of the fd means no locking, and ACKs
 * and streamed frames are handled by the same parser.
 *
 * See can_mgr.h and Docs/specs/2026-08-18-rpmsg-can-transport-design.md.
 */
#include "can_mgr.h"

#include <errno.h>
#include <poll.h>
#include <pthread.h>
#include <string.h>

#include "../app_queues.h"
#include "../platform/rpmsg_link.h"
#include "../proto/can_frame.h"
#include "../proto/proto_defs.h"
#include "../proto/rpmsg_frame.h"
#include "../util/log.h"

/* Set to 1 to hex-dump every frame crossing the RPMsg link. Off by default:
 * at a 10 Hz poll across 8 Secondaries this is a lot of output. Mirrors
 * ME_CAN_DEBUG_LOG in core_logic.c. */
#define ME_RPMSG_DEBUG_LOG 0

/* How long poll() waits before looping, which is also how promptly the thread
 * notices a stop request. */
#define ME_CAN_POLL_TIMEOUT_MS 200

static me_msg_t  s_rx;   /* from g_q_can  - file scope: ~64 KB, not a local */
static me_msg_t  s_tx;   /* to   g_q_core - same reason                     */
static pthread_t s_thread;
static bool      s_started = false;

static int               s_fd = -1;
static me_rpmsg_stream_t s_stream;

/*
 * Which channels of each Secondary we have addressed, learned from TX.
 *
 * An inbound frame carries a CAN ID, and the ID's upper 6 bits give the
 * Secondary - but a 64-byte block frame holds four channel slots and the ID
 * says nothing about which of them are live. Core Logic addresses circuits by
 * the full 8-bit CircuitID, so the reply has to be attributed back to one.
 * Recording what we sent is the only information available to do that.
 *
 * Index: Secondary number 1..ME_MAX_SECONDARIES. Bit (channel - 1) set means
 * that channel has been addressed. Index 0 is unused - Secondary numbers are
 * 1-based, and folding an invalid 0 into a real slot is exactly the bug the
 * CircuitID rules warn about.
 */
static uint8_t s_active_ch[ME_MAX_SECONDARIES + 1u];

/* Counters, reported at shutdown next to the queue counters. */
static unsigned long s_tx_frames;
static unsigned long s_tx_dropped;
static unsigned long s_ack_ok;
static unsigned long s_ack_fail;
static unsigned long s_rx_frames;
static unsigned long s_rx_unmatched;

/* ---------------------------------------------------------------- TX ---- */

static void handle_can_tx(const me_msg_t *m)
{
    me_rpmsg_can_t can;
    uint8_t        payload[ME_RPMSG_CAN_MSG_LEN];
    uint8_t        frame[ME_RPMSG_FRAME_MAX];
    size_t         n;

    const uint8_t secondary = ME_CIRCUIT_SECONDARY(m->circuit_id);
    const uint8_t channel   = ME_CIRCUIT_CHANNEL(m->circuit_id);

    if (m->len != ME_CAN_FRAME_LEN) {
        ME_LOGW("can: circuit 0x%02X - CAN_TX with %u bytes, expected %u",
                m->circuit_id, (unsigned)m->len, (unsigned)ME_CAN_FRAME_LEN);
        s_tx_dropped++;
        return;
    }
    /* 1-based nibbles: 0 is malformed, never slot 0. */
    if (secondary == 0u || secondary > ME_MAX_SECONDARIES
        || channel == 0u || channel > ME_MAX_CHANNELS) {
        ME_LOGW("can: circuit 0x%02X - malformed CircuitID, dropped",
                m->circuit_id);
        s_tx_dropped++;
        return;
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
    memcpy(can.data, m->payload, ME_CAN_PAYLOAD_BYTES);

    if (!me_rpmsg_pack_can(&can, payload)) {
        ME_LOGW("can: circuit 0x%02X - payload pack failed", m->circuit_id);
        s_tx_dropped++;
        return;
    }

    n = me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET, ME_RPMSG_CMD_CAN_SET_FRAME,
                      ME_RPMSG_RESULT_REQUEST, payload, ME_RPMSG_CAN_MSG_LEN,
                      frame, sizeof(frame));
    if (n == 0u) {
        ME_LOGW("can: circuit 0x%02X - frame wrap failed", m->circuit_id);
        s_tx_dropped++;
        return;
    }

    if (s_fd < 0) {
        s_tx_dropped++;
        return; /* the open failure was logged once at startup */
    }

#if ME_RPMSG_DEBUG_LOG
    ME_LOGD("can: circuit 0x%02X - RPMsg TX, CAN ID 0x%03X, %u bytes",
            m->circuit_id, (unsigned)can.can_id, (unsigned)n);
    me_hex_dump(ME_LOG_DEBUG, "RPMsg TX", frame, n);
#endif

    if (!me_rpmsg_write_all(s_fd, frame, n)) {
        s_tx_dropped++;
        return;
    }

    /* Record only after the write succeeded: a channel we never actually
     * addressed must not attract replies. */
    s_active_ch[secondary] |= (uint8_t)(1u << (channel - 1u));
    s_tx_frames++;
}

static void drain_queue(void)
{
    me_msgq_ack_event(&g_q_can);

    while (me_msgq_recv(&g_q_can, &s_rx, 0)) {
        switch (s_rx.type) {
        case ME_MSG_CAN_TX:
            handle_can_tx(&s_rx);
            break;
        default:
            ME_LOGW("can: unexpected message %s",
                    me_msg_type_name(s_rx.type));
            break;
        }
    }
}

/* ---------------------------------------------------------------- RX ---- */

/* Forward one inbound 64-byte frame to Core Logic as `circuit_id`. */
static void forward_to_core(uint8_t circuit_id, const uint8_t *data64)
{
    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_CAN_DATA;
    s_tx.circuit_id = circuit_id;
    s_tx.len        = ME_CAN_FRAME_LEN;
    memcpy(s_tx.payload, data64, ME_CAN_FRAME_LEN);

    if (!me_msgq_send(&g_q_core, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
        ME_LOGW("can: circuit 0x%02X - inbound frame dropped, core queue full",
                circuit_id);
    }
}

static void handle_stream_frame(const uint8_t *payload)
{
    me_rpmsg_can_t can;
    uint8_t        secondary;
    uint8_t        function;
    uint8_t        mask;
    uint8_t        ch;

    if (!me_rpmsg_parse_can(payload, &can)) {
        ME_LOGW("can: inbound payload failed to parse");
        return;
    }

    secondary = (uint8_t)((can.can_id >> 5) & 0x3Fu);
    function  = (uint8_t)(can.can_id & 0x1Fu);

    if (can.data_len != ME_CAN_PAYLOAD_BYTES) {
        ME_LOGW("can: inbound CAN ID 0x%03X carried %u bytes, expected %u",
                (unsigned)can.can_id, (unsigned)can.data_len,
                (unsigned)ME_CAN_PAYLOAD_BYTES);
        return;
    }
    /* The Secondary always answers on function 0x02, whichever function was
     * requested (Ref Docs/master_slave_can_v1.0.md, "Functions"). Anything
     * else on this link is either our own frame looped back or a foreign
     * node. */
    if (function != ME_CAN_FUNC_READ) {
        ME_LOGW("can: inbound CAN ID 0x%03X has function 0x%02X, not a "
                "Secondary reply - ignored",
                (unsigned)can.can_id, (unsigned)function);
        return;
    }
    if (secondary == 0u || secondary > ME_MAX_SECONDARIES) {
        ME_LOGW("can: inbound CAN ID 0x%03X maps to Secondary %u, out of range",
                (unsigned)can.can_id, (unsigned)secondary);
        return;
    }

    mask = s_active_ch[secondary];
    if (mask == 0u) {
        /* A reply from a Secondary we have not addressed. Expected briefly at
         * startup if the M7 was already streaming; persistent counts mean the
         * bus has a node we do not know about. */
        s_rx_unmatched++;
        return;
    }

    /* Channels 1-4 and 5-8 are two different block frames that share one CAN
     * ID (master_slave_can_v1.0.md). With channels live in both blocks a reply
     * cannot be attributed by ID alone. Unreachable while only channel 1 runs;
     * say so loudly rather than misparse silently if that ever changes. */
    if ((mask & 0x0Fu) != 0u && (mask & 0xF0u) != 0u) {
        ME_LOGW("can: Secondary %u has channels in both blocks - inbound "
                "frames are ambiguous by CAN ID, see design doc open "
                "question 2", (unsigned)secondary);
    }

#if ME_RPMSG_DEBUG_LOG
    ME_LOGD("can: RPMsg RX, CAN ID 0x%03X, Secondary %u, channel mask 0x%02X",
            (unsigned)can.can_id, (unsigned)secondary, (unsigned)mask);
    me_hex_dump(ME_LOG_DEBUG, "CAN RX frame", can.data, ME_CAN_FRAME_LEN);
#endif

    s_rx_frames++;
    for (ch = 1u; ch <= ME_MAX_CHANNELS; ch++) {
        if ((mask & (uint8_t)(1u << (ch - 1u))) != 0u) {
            forward_to_core((uint8_t)((secondary << 4) | ch), can.data);
        }
    }
}

static void handle_ack(const me_rpmsg_hdr_t *h)
{
    if (h->result == ME_RPMSG_RESULT_SUCCESS) {
        s_ack_ok++;
        return;
    }
    s_ack_fail++;
    ME_LOGW("can: M7 rejected command 0x%08lX (result 0x%02X)",
            (unsigned long)h->command, (unsigned)h->result);
}

static void drain_device(void)
{
    uint8_t raw[ME_RPMSG_STREAM_BUF_SZ];
    ssize_t n;

    for (;;) {
        n = read(s_fd, raw, sizeof(raw));
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            if (errno != EAGAIN && errno != EWOULDBLOCK) {
                ME_LOGE("can: read failed: %s", strerror(errno));
            }
            break;
        }
        if (n == 0) {
            break;
        }
        if (me_rpmsg_stream_push(&s_stream, raw, (size_t)n) < (size_t)n) {
            ME_LOGW("can: reassembly buffer full, bytes discarded");
        }
        /* A read() smaller than the buffer means the device had nothing more
         * to give, so stop rather than spin on a second blocking read(). */
        if ((size_t)n < sizeof(raw)) {
            break;
        }
    }

    for (;;) {
        me_rpmsg_hdr_t h;
        const uint8_t *payload = NULL;

        if (!me_rpmsg_stream_next(&s_stream, &h, &payload)) {
            break;
        }
        if (h.length == 0u) {
            handle_ack(&h);
        } else if (h.command == ME_RPMSG_CMD_CAN_GET_FRAME
                   && h.length == ME_RPMSG_CAN_MSG_LEN) {
            handle_stream_frame(payload);
        } else {
            ME_LOGW("can: unexpected frame, command 0x%08lX length %lu",
                    (unsigned long)h.command, (unsigned long)h.length);
        }
    }
}

/* ------------------------------------------------------------- thread ---- */

static void *can_mgr_main(void *arg)
{
    const char *path = me_rpmsg_device_path();

    (void)arg;

    memset(s_active_ch, 0, sizeof(s_active_ch));
    me_rpmsg_stream_init(&s_stream);

    s_fd = me_rpmsg_open(path);
    if (s_fd < 0) {
        /* Degraded, not fatal: registration and Web Application traffic keep
         * working, so the board is still reachable to diagnose the M7. */
        ME_LOGE("can manager thread: %s unavailable - M7 link is down, CAN "
                "frames will be counted and dropped", path);
    } else {
        ME_LOGI("can manager thread: started, M7 link on %s", path);
    }

    while (!me_app_stop_requested()) {
        struct pollfd pfds[2];
        nfds_t        nfds = 0u;
        int           qi   = -1;
        int           di   = -1;
        int           pr;

        const int qfd = me_msgq_fd(&g_q_can);
        if (qfd >= 0) {
            qi = (int)nfds;
            pfds[nfds].fd = qfd;
            pfds[nfds].events = POLLIN;
            pfds[nfds].revents = 0;
            nfds++;
        }
        if (s_fd >= 0) {
            di = (int)nfds;
            pfds[nfds].fd = s_fd;
            pfds[nfds].events = POLLIN;
            pfds[nfds].revents = 0;
            nfds++;
        }

        if (nfds == 0u) {
            /* No eventfd and no device: nothing to poll on. Fall back to a
             * blocking queue receive so the thread still drains and still
             * notices a stop. */
            if (me_msgq_recv(&g_q_can, &s_rx, ME_RECV_TIMEOUT_MS)) {
                if (s_rx.type == ME_MSG_CAN_TX) {
                    handle_can_tx(&s_rx);
                }
            }
            continue;
        }

        pr = poll(pfds, nfds, ME_CAN_POLL_TIMEOUT_MS);
        if (pr < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("can: poll failed: %s", strerror(errno));
            break;
        }

        /* Always drain the queue: a send that raced the poll() must not wait
         * for the next wake-up. Same rule as the communication thread. */
        drain_queue();

        if (di >= 0 && (pfds[di].revents & (POLLIN | POLLHUP | POLLERR)) != 0) {
            drain_device();
        }
        (void)qi;
    }

    me_rpmsg_close(s_fd);
    s_fd = -1;

    ME_LOGI("can manager thread: stopped - tx %lu (dropped %lu), ack ok %lu "
            "fail %lu, rx %lu (unmatched %lu)",
            s_tx_frames, s_tx_dropped, s_ack_ok, s_ack_fail, s_rx_frames,
            s_rx_unmatched);
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

`drain_device()` calls `read()`, so add `#include <unistd.h>` to the include block alongside `<poll.h>`.

- [ ] **Step 3: Verify the cross-build compiles**

Run: `.\build.ps1`
Expected: clean under `-Werror`. If `ME_MAX_SECONDARIES` or `ME_MAX_CHANNELS` is undeclared, check `src/proto/proto_defs.h:234` — both are defined there alongside `ME_MAX_CIRCUITS`.

- [ ] **Step 4: Run the host tests**

Run: `.\build-native.ps1`
Expected: PASS, same check count as Task 5. `can_mgr.c` is not in the native build; this run confirms the shared `proto/` and `app_queues` changes did not regress anything.

- [ ] **Step 5: Commit**

```bash
git add "ME Project/me-primary/src/threads/can_mgr.c" "ME Project/me-primary/src/threads/can_mgr.h"
git commit -m "Replace CAN simulation with real RPMsg transport to the M7"
```

---

## Task 7: Documentation and hardware-test procedure

**Files:**
- Modify: `me-primary/README.md` (add an `ME_RPMSG_DEV` note to the environment section)
- Modify: `.claude/CODEBASE_MAP.md` (add the two new modules)
- Modify: `Docs/ME_Primary_Implementation_Reference.md` (add entries for `rpmsg_frame.c` and `rpmsg_link.c`, and rewrite the `can_mgr.c` entry)
- Modify: `Docs/specs/2026-08-18-rpmsg-can-transport-design.md` (mark Status as Implemented)

- [ ] **Step 1: Update the implementation reference**

`Docs/ME_Primary_Implementation_Reference.md` is the file CLAUDE.md points every reader at before they open source. Add one entry per new module following the existing format in that file (responsibility, public interface, internal logic, dependencies, gotchas). The gotchas that must appear:

- `rpmsg_frame.c`: the header is big-endian and the payload little-endian; do not unify them. `me_rpmsg_stream_next`'s payload pointer aliases the stream buffer and dies on the next call.
- `rpmsg_link.c`: raw mode is mandatory — a 64-byte CAN payload contains arbitrary bytes and any line-discipline translation corrupts frames.
- `can_mgr.c`: the active-channel registry is the only thing that maps an inbound CAN ID back to a full CircuitID; Block 1 / Block 2 share a CAN ID, so a Secondary with channels in both blocks is ambiguous.

- [ ] **Step 2: Document the hardware test procedure**

Append to `Docs/specs/2026-08-18-rpmsg-can-transport-design.md` a section 8:

```markdown
## 8. Hardware-in-the-loop verification

Compiling clean proves nothing about this feature. The procedure:

1. Confirm the M7 is running: `cat /sys/class/remoteproc/remoteproc0/state`
   must print `running`.
2. Confirm the device exists: `ls -l /dev/ttyRPMSG30`.
3. Run the teammate's `rpmsg_can_test.py` first, standalone. If its
   `ACK SET_FRAME SUCCESS` does not appear, the fault is below this
   application and no amount of A53 debugging will help.
4. Deploy with `deploy.ps1` and watch for `rpmsg: opened /dev/ttyRPMSG30`.
5. Start a program on circuit 0x11 and confirm at shutdown that the CAN
   manager reports non-zero `tx` and `ack ok`, and zero `ack fail`.
6. With a Secondary connected, confirm non-zero `rx` and zero `unmatched`.
   Non-zero `unmatched` means a node is replying that we never addressed.
7. To see every byte, set `ME_RPMSG_DEBUG_LOG` to 1 in `can_mgr.c` and
   rebuild. Leave it at 0 for normal runs.

Requires hardware-in-the-loop testing to verify. This suggestion cannot be
validated on the development laptop.
```

- [ ] **Step 3: Verify both builds one final time**

Run: `.\build.ps1` then `.\build-native.ps1`
Expected: cross-build clean; host tests PASS. Report the final check count.

- [ ] **Step 4: Commit**

```bash
git add "ME Project/me-primary/README.md" "ME Project/.claude/CODEBASE_MAP.md" "ME Project/Docs/ME_Primary_Implementation_Reference.md" "ME Project/Docs/specs/2026-08-18-rpmsg-can-transport-design.md"
git commit -m "Document RPMsg CAN transport and its hardware test procedure"
```

---

## Out of scope

Named here so they are not silently assumed done:

- **The M7 firmware.** Owned by another developer.
- **Retry / offline detection.** `master_slave_can_v1.0.md` specifies 3 retries then mark the Secondary offline. That belongs to Core Logic's step engine, which owns the poll cycle.
- **Recomputing `CAN_FD_Transport_Analysis.md`.** Its timings assume flat 2 Mbps; `RPMSG_PROTOCOL.md` says 500 kbps nominal / 5 Mbps data phase. Separate work.
- **Reopening the device after a failed open.** Degraded mode is permanent for the life of the process; restarting the container recovers it.
- **Block 1 / Block 2 disambiguation.** Warned about, not solved. Unreachable while only channel 1 runs.
