# Registration Success Semantics and IDLE State — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the board treat registration response `0x02` (Already
Registered) as success alongside `0x01`, so it stops re-registering every 30
seconds, and make the post-registration idle state explicit in the code and
the logs.

**Architecture:** The success test moves from a bare `==` comparison in
`reg_frame.c` into a named predicate in `proto_defs.h`, keeping it inside the
pure, host-testable `src/proto/` boundary (ADR-7). The communication thread's
`me_comm_state_t` enum — currently dead code — is wired up for real, with the
unused `ME_COMM_REGISTERED` and `ME_COMM_MONITORING` collapsed into a single
`ME_COMM_IDLE`.

**Tech Stack:** C (`-std=c11` host tests via MinGW, `-std=gnu11` board build
via `aarch64-none-linux-gnu`), custom assertion harness in `tests/test_util.h`,
PowerShell build scripts.

**Spec:** `Docs/specs/2026-08-10-registration-idle-state-design.md`

## Global Constraints

- **This project is not a git repository.** The developer declined `git init`
  on 2026-08-06. Every "commit" step in the standard plan format is therefore
  replaced by a **build-and-verify checkpoint**. Do not run `git` commands.
- **Tests are written first.** Write the failing test, run it, confirm it
  fails *for the right reason*, then implement. A test that passes before the
  implementation is a broken test.
- **`src/proto/` must stay free of socket and platform headers** (ADR-7), or
  the host test suite stops building.
- Both builds run with `-Wall -Wextra -Werror`. Any warning is a hard failure.
- **CRC byte order is out of scope.** The board is still running the
  pre-ADR-9 binary. Do not change `ME_CRC_ORDER_DEFAULT` or any CRC code in
  this plan. Tests that need a CRC order pass one **explicitly**.
- No magic protocol numbers outside `src/proto/proto_defs.h`.
- Working directory for all commands: `me-primary/`.

---

### Task 1: Success predicate and parse semantics

**Files:**
- Modify: `me-primary/src/proto/proto_defs.h` (add predicate after line 41)
- Modify: `me-primary/src/proto/reg_frame.c:69`
- Modify: `me-primary/src/proto/reg_frame.h:46` (comment only)
- Test: `me-primary/tests/test_reg_frame.c`

**Interfaces:**
- Consumes: `ME_REG_VALUE_FAILED` (`0x00`), `ME_REG_VALUE_REGISTERED`
  (`0x01`), `ME_REG_VALUE_ALREADY_REGISTERED` (`0x02`) — already defined in
  `proto_defs.h:39-41`.
- Produces: `ME_REG_VALUE_IS_SUCCESS(v)` — function-like macro, takes any
  integer value byte, evaluates to a truthy int. Used by Task 2's tests and
  available to any later command group.

- [ ] **Step 1: Write the failing predicate test**

Add to `me-primary/tests/test_reg_frame.c`, immediately after the
`demo_request()` helper (before the `/* ---- circuit encoding ---- */`
banner):

```c
/* ------------------------------------------------------ value semantics -- */

static void test_value_success_predicate(void)
{
    /* 0x02 is a SUCCESS response: it means the Web Application already holds a
     * registration for this device. Reading it as a rejection made the board
     * re-register every 30 seconds (observed on hardware 2026-08-10). */
    TEST_CASE("value: 0x01 and 0x02 are success; 0x00 and unknown are not");

    CHECK(!ME_REG_VALUE_IS_SUCCESS(ME_REG_VALUE_FAILED));
    CHECK(ME_REG_VALUE_IS_SUCCESS(ME_REG_VALUE_REGISTERED));
    CHECK(ME_REG_VALUE_IS_SUCCESS(ME_REG_VALUE_ALREADY_REGISTERED));
    CHECK(!ME_REG_VALUE_IS_SUCCESS(0x03));
    CHECK(!ME_REG_VALUE_IS_SUCCESS(0xFF));
}
```

- [ ] **Step 2: Write the failing parse test for 0x02**

Add to `me-primary/tests/test_reg_frame.c`, directly after the existing
`test_parse_accepts_success_response()` function:

```c
static void test_parse_accepts_already_registered(void)
{
    /* The exact defect: server said 0x02, board called it a rejection. */
    TEST_CASE("parse: Value 0x02 (Already Registered) means registered");
    const me_reg_request_t sent = demo_request();
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_ALREADY_REGISTERED,
                   ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    const me_reg_parse_result_t r = me_reg_parse_response(
        buf, sizeof(buf), &sent, ME_CRC_ORDER_LE, &rsp);

    CHECK_EQ_U(ME_REG_PARSE_OK, r);
    CHECK(rsp.registered);
    CHECK(!rsp.echo_mismatch);
    CHECK_EQ_U(ME_REG_VALUE_ALREADY_REGISTERED, rsp.value);
}
```

`build_response()` is a static helper already defined in this file above the
response-parsing tests. Both new functions must be placed **after** it, or
move them below it — C requires the declaration first.

> **Placement note:** `test_value_success_predicate` does not use
> `build_response`, so it can go near the top as described in Step 1.
> `test_parse_accepts_already_registered` **must** come after
> `build_response()`.

- [ ] **Step 3: Fix the existing test whose assertion inverts**

In `me-primary/tests/test_reg_frame.c`, find
`test_parse_rejects_non_success_values()`. It currently asserts
`!rsp.registered` for `0x02`, which is exactly the bug being fixed. Replace
its value array and case name.

Replace:

```c
    const uint8_t values[] = { ME_REG_VALUE_FAILED,
                               ME_REG_VALUE_ALREADY_REGISTERED,
                               0xFF };
    const me_reg_request_t sent = demo_request();

    TEST_CASE("parse: values other than 0x01 are not registered");
```

With:

```c
    /* 0x02 deliberately absent: it moved to the success set on 2026-08-10.
     * 0x03 stands in for "unrecognised but well-formed". */
    const uint8_t values[] = { ME_REG_VALUE_FAILED, 0x03, 0xFF };
    const me_reg_request_t sent = demo_request();

    TEST_CASE("parse: 0x00 and unrecognised values are not registered");
```

- [ ] **Step 4: Register the two new tests in the runner**

In `me-primary/tests/test_reg_frame.c`, inside `run_reg_frame_tests()`,
replace:

```c
    test_circuit_id_packs_two_nibbles();
```

with:

```c
    test_value_success_predicate();
    test_circuit_id_packs_two_nibbles();
```

and replace:

```c
    test_parse_accepts_success_response();
```

with:

```c
    test_parse_accepts_success_response();
    test_parse_accepts_already_registered();
```

- [ ] **Step 5: Run the tests and confirm they fail for the right reason**

Run:

```powershell
.\build-native.ps1
```

Expected: the **build fails** with an error naming
`ME_REG_VALUE_IS_SUCCESS` as undeclared, because the macro does not exist
yet. That is the correct first failure.

If the build instead succeeds and only assertions fail, the macro was already
added — back out the implementation and re-run, because a test must be seen
failing before it is trusted.

- [ ] **Step 6: Add the predicate**

In `me-primary/src/proto/proto_defs.h`, immediately after the
`ME_REG_VALUE_ALREADY_REGISTERED` definition (currently line 41) and before
the `/* ---- registration request layout ---- */` banner:

```c
/*
 * Both 0x01 (Registered) and 0x02 (Already Registered) mean the device IS
 * registered - 0x02 only adds that the Web Application already held a record
 * for it. Only 0x00 (Failed) and unrecognised values are rejections.
 *
 * This is not a hypothetical distinction: treating 0x02 as a rejection made
 * the board close the connection and re-register every 30 seconds, observed
 * on hardware 2026-08-10.
 */
#define ME_REG_VALUE_IS_SUCCESS(v)                                            \
    ((uint8_t)(v) == ME_REG_VALUE_REGISTERED ||                               \
     (uint8_t)(v) == ME_REG_VALUE_ALREADY_REGISTERED)
```

- [ ] **Step 7: Run the tests again**

Run:

```powershell
.\build-native.ps1
```

Expected: the build now succeeds, and **`test_parse_accepts_already_registered`
FAILS** on `CHECK(rsp.registered)` — the predicate exists but
`me_reg_parse_response` does not use it yet. `test_value_success_predicate`
should PASS.

This intermediate state is the point of the exercise: it proves the parse test
is actually exercising the parser and not the macro.

- [ ] **Step 8: Use the predicate in the parser**

In `me-primary/src/proto/reg_frame.c`, replace line 69:

```c
    out->registered = (out->value == ME_REG_VALUE_REGISTERED);
```

with:

```c
    out->registered = ME_REG_VALUE_IS_SUCCESS(out->value);
```

- [ ] **Step 9: Update the struct field comment**

In `me-primary/src/proto/reg_frame.h`, replace line 46:

```c
    bool    registered;    /* value == ME_REG_VALUE_REGISTERED */
```

with:

```c
    bool    registered;    /* server considers the device registered:
                            * value is 0x01 or 0x02 (ME_REG_VALUE_IS_SUCCESS).
                            * Read `value` to tell the two apart. */
```

- [ ] **Step 10: Build-and-verify checkpoint**

Run both layers:

```powershell
.\build-native.ps1
.\build.ps1
```

Expected:
- `build-native.ps1` → `RESULT: PASS`, check count risen from 50 to **52**
  (two new `TEST_CASE` blocks; the harness counts cases, not assertions)
- `build.ps1` → `Class: ELF64`, `Machine: AArch64`, no warnings

Do not proceed to Task 2 until both are clean.

---

### Task 2: Golden hardware response replay

**Files:**
- Test: `me-primary/tests/test_reg_frame.c`

**Interfaces:**
- Consumes: `me_reg_parse_response()`, `ME_REG_VALUE_ALREADY_REGISTERED`,
  `ME_CIRCUIT_ID()`, `ME_REG_RESPONSE_LEN` — all existing.
- Produces: nothing consumed by later tasks. This is a pure regression guard.

**Why separate from Task 1:** Task 1 proves the *semantics*. This proves the
board handles the *literal bytes the server actually sent*, including its CRC
convention. A reviewer could reasonably accept one and reject the other.

- [ ] **Step 1: Write the failing golden test**

Add to `me-primary/tests/test_reg_frame.c`, directly after
`test_parse_accepts_already_registered()`:

```c
/*
 * Golden vector: the exact 7-byte response captured from the Web Application
 * on 2026-08-10, replayed byte for byte.
 *
 *   DD 01 01 11 02 15 BE
 *   |  |  |  |  |  +--+-- CRC-16/Modbus over bytes 0..4 = 0xBE15
 *   |  |  |  |  +-------- Value 0x02, Already Registered
 *   |  |  |  +----------- CircuitID echo 0x11
 *   |  |  +-------------- DeviceID echo 0x01
 *   |  +----------------- QueryID 0x01
 *   +-------------------- Start 0xDD
 *
 * Parsed with an EXPLICIT little-endian order because that is what the server
 * actually sent ("15 BE" is the low byte first). Deliberately NOT
 * ME_CRC_ORDER_DEFAULT: this test replays hardware and must not change meaning
 * if the default order is revisited.
 */
static void test_parse_matches_captured_hardware_response(void)
{
    TEST_CASE("parse: the captured 0x02 response registers the device");

    static const uint8_t golden[ME_REG_RESPONSE_LEN] = {
        0xDD, 0x01, 0x01, 0x11, 0x02, 0x15, 0xBE
    };

    me_reg_request_t sent;
    memset(&sent, 0, sizeof(sent));
    sent.device_id  = 0x01;
    sent.circuit_id = ME_CIRCUIT_ID(1, 1);

    me_reg_response_t rsp;
    const me_reg_parse_result_t r = me_reg_parse_response(
        golden, sizeof(golden), &sent, ME_CRC_ORDER_LE, &rsp);

    CHECK_EQ_U(ME_REG_PARSE_OK, r);
    CHECK(rsp.registered);
    CHECK(!rsp.echo_mismatch);
    CHECK_EQ_U(ME_REG_VALUE_ALREADY_REGISTERED, rsp.value);
    CHECK_EQ_U(0x01, rsp.device_id);
    CHECK_EQ_U(0x11, rsp.circuit_id);
}
```

- [ ] **Step 2: Register it in the runner**

In `run_reg_frame_tests()`, replace:

```c
    test_parse_accepts_already_registered();
```

with:

```c
    test_parse_accepts_already_registered();
    test_parse_matches_captured_hardware_response();
```

- [ ] **Step 3: Run and verify**

Run:

```powershell
.\build-native.ps1
```

Expected: `RESULT: PASS`, check count **53**.

This test passes immediately because Task 1 already implemented the behaviour
— it is a regression guard, not a driver. That is legitimate here and is the
one exception to "watch it fail first" in this plan: its purpose is to pin the
observed bytes, and it was written after the semantics were settled
deliberately.

If it *fails*, the CRC value or an offset in the golden array is wrong — do
not "fix" it by changing the expected values without recomputing
CRC-16/Modbus over `DD 01 01 11 02` independently.

- [ ] **Step 4: Build-and-verify checkpoint**

Run:

```powershell
.\build.ps1
```

Expected: clean cross-build, `ELF64` / `AArch64`.

---

### Task 3: Wire up the state machine with ME_COMM_IDLE

**Files:**
- Modify: `me-primary/src/comm_thread.h:16-24`
- Modify: `me-primary/src/comm_thread.c:38-48` (name function),
  `:189` (`monitor` → `idle_loop`), `:248-295` (thread main)

**Interfaces:**
- Consumes: nothing from Tasks 1–2 at compile time; depends on Task 1 only
  behaviourally (`do_registration()` now returns true for `0x02`).
- Produces: `ME_COMM_IDLE` enum value; `me_comm_state_name()` becomes live
  rather than dead code.

**Verified before starting:** `ME_COMM_REGISTERED` and `ME_COMM_MONITORING`
appear **only** inside `me_comm_state_name()` (`comm_thread.c:43-44`) and the
enum itself (`comm_thread.h:19-20`). Nothing else in the codebase references
them, so removing them is safe. `monitor()` is called from exactly one site,
`comm_thread.c:277`.

**No host test.** `comm_thread.c` is Linux-only and deliberately excluded from
`build-native.ps1` (ADR-7). Verification for this task is a clean cross-build
plus developer-run hardware observation. Do not claim this task is "tested".

- [ ] **Step 1: Collapse the enum**

In `me-primary/src/comm_thread.h`, replace:

```c
typedef enum {
    ME_COMM_CONNECTING = 0,
    ME_COMM_REGISTERING,
    ME_COMM_REGISTERED,
    ME_COMM_MONITORING,
    ME_COMM_STOPPED
} me_comm_state_t;
```

with:

```c
/*
 * ME_COMM_REGISTERED and ME_COMM_MONITORING were separate states that nothing
 * ever distinguished. They are one thing: registered, connection held open and
 * polled. IDLE is the name the protocol documentation uses.
 */
typedef enum {
    ME_COMM_CONNECTING = 0,
    ME_COMM_REGISTERING,
    ME_COMM_IDLE,    /* registered; TCP held open and polled */
    ME_COMM_STOPPED
} me_comm_state_t;
```

- [ ] **Step 2: Update the name function**

In `me-primary/src/comm_thread.c`, replace:

```c
    case ME_COMM_REGISTERED:  return "REGISTERED";
    case ME_COMM_MONITORING:  return "MONITORING";
```

with:

```c
    case ME_COMM_IDLE:        return "IDLE";
```

- [ ] **Step 3: Add the state variable**

In `me-primary/src/comm_thread.c`, replace:

```c
static volatile sig_atomic_t s_stop_requested = 0;
static volatile sig_atomic_t s_registered     = 0;
```

with:

```c
static volatile sig_atomic_t s_stop_requested = 0;
static volatile sig_atomic_t s_registered     = 0;

/* Owned by the communication thread alone - never touched from a signal
 * handler, so it needs no volatile/sig_atomic_t treatment. */
static me_comm_state_t s_state = ME_COMM_CONNECTING;
```

- [ ] **Step 4: Rename `monitor()` to `idle_loop()` and set the state**

In `me-primary/src/comm_thread.c`, replace the function's comment and
signature:

```c
/*
 * Hold the connection open, watching all three sockets. Returns when the peer
 * disconnects or a stop is requested.
 */
static void monitor(me_system_t *sys, int tcp_fd)
{
    ME_LOGI("state: MONITORING - watching TCP and UDP %u/%u",
            ME_PORT_UDP_LIVE, ME_PORT_UDP_SESSION);
```

with:

```c
/*
 * Registered and idle: hold the connection open and watch all three sockets.
 * The registration frame is NEVER re-sent from here - that is what makes the
 * "register once per connection" rule structural rather than flag-based.
 * Returns when the peer disconnects or a stop is requested.
 */
static void idle_loop(me_system_t *sys, int tcp_fd)
{
    s_state = ME_COMM_IDLE;
    ME_LOGI("state: %s - registered, holding connection, watching TCP and "
            "UDP %u/%u",
            me_comm_state_name(s_state),
            ME_PORT_UDP_LIVE, ME_PORT_UDP_SESSION);
```

- [ ] **Step 5: Rename the log prefixes inside the idle loop**

Still inside `idle_loop()`, replace each `"monitor: ..."` log prefix with
`"idle: ..."`. There are five occurrences:

```c
            ME_LOGE("monitor: poll failed: %s", strerror(errno));
            ME_LOGW("monitor: TCP connection lost");
                ME_LOGW("monitor: TCP connection closed by peer");
            ME_LOGI("monitor: %d unsolicited byte(s) on TCP", (int)n);
                ME_LOGW("monitor: unexpected %d-byte datagram on UDP %u",
```

become:

```c
            ME_LOGE("idle: poll failed: %s", strerror(errno));
            ME_LOGW("idle: TCP connection lost");
                ME_LOGW("idle: TCP connection closed by peer");
            ME_LOGI("idle: %d unsolicited byte(s) on TCP", (int)n);
                ME_LOGW("idle: unexpected %d-byte datagram on UDP %u",
```

- [ ] **Step 6: Drive the state from the thread main loop**

In `me-primary/src/comm_thread.c`, replace the body of the `while` loop in
`comm_thread_main()` from the `ME_LOGI("state: CONNECTING ...")` line through
the `else` branch:

```c
        ME_LOGI("state: CONNECTING to %s:%u",
                sys->cfg.server_ip, sys->cfg.server_port);

        const int fd = me_tcp_connect(sys->cfg.server_ip,
                                      sys->cfg.server_port,
                                      sys->cfg.connect_timeout_ms);
        if (fd < 0) {
            if (s_stop_requested) {
                break;
            }
            ME_LOGW("state: CONNECTING failed, retrying in %d ms", backoff_ms);
            interruptible_sleep_ms(backoff_ms);
            backoff_ms = (backoff_ms * 2 > ME_BACKOFF_MAX_MS) ? ME_BACKOFF_MAX_MS
                                                              : backoff_ms * 2;
            continue;
        }

        ME_LOGI("state: REGISTERING");
        if (do_registration(sys, fd)) {
            s_registered = 1;
            backoff_ms   = ME_BACKOFF_MIN_MS; /* healthy again */
            monitor(sys, fd);
        } else {
            ME_LOGW("state: REGISTERING failed, retrying in %d ms", backoff_ms);
        }
```

with:

```c
        s_state = ME_COMM_CONNECTING;
        ME_LOGI("state: %s to %s:%u", me_comm_state_name(s_state),
                sys->cfg.server_ip, sys->cfg.server_port);

        const int fd = me_tcp_connect(sys->cfg.server_ip,
                                      sys->cfg.server_port,
                                      sys->cfg.connect_timeout_ms);
        if (fd < 0) {
            if (s_stop_requested) {
                break;
            }
            ME_LOGW("state: %s failed, retrying in %d ms",
                    me_comm_state_name(s_state), backoff_ms);
            interruptible_sleep_ms(backoff_ms);
            backoff_ms = (backoff_ms * 2 > ME_BACKOFF_MAX_MS) ? ME_BACKOFF_MAX_MS
                                                              : backoff_ms * 2;
            continue;
        }

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

- [ ] **Step 7: Set the terminal state**

In `me-primary/src/comm_thread.c`, replace the end of `comm_thread_main()`:

```c
    ME_LOGI("comm thread: stopped");
    return NULL;
```

with:

```c
    s_state = ME_COMM_STOPPED;
    ME_LOGI("comm thread: stopped (state: %s)", me_comm_state_name(s_state));
    return NULL;
```

- [ ] **Step 8: Reset state on start**

In `me-primary/src/comm_thread.c`, in `me_comm_thread_start()`, replace:

```c
    s_stop_requested = 0;
```

with:

```c
    s_stop_requested = 0;
    s_state          = ME_COMM_CONNECTING;
```

- [ ] **Step 9: Build-and-verify checkpoint**

Run both layers:

```powershell
.\build-native.ps1
.\build.ps1
```

Expected:
- `build-native.ps1` → still `RESULT: PASS` with **53** checks. This task
  touches no host-built file, so an unchanged result is the correct outcome.
- `build.ps1` → clean, `ELF64` / `AArch64`, no warnings. Watch specifically
  for an "unused variable" or "enumeration value not handled in switch"
  warning — both are `-Werror` failures here.

---

### Task 4: Documentation sync

**Files:**
- Modify: `me-primary/README.md`
- Modify: `.claude/DECISIONS.md` (new ADR-10)
- Modify: `.claude/CODEBASE_MAP.md`
- Modify: `.claude/TASKS.md`
- Modify: `.claude/AGENT.md` (live-state table)

**Interfaces:** none — documentation only.

- [ ] **Step 1: Add ADR-10 to `.claude/DECISIONS.md`**

Insert immediately after the `# DECISIONS.md` header block and its `---`,
before `## ADR-9`:

```markdown
## ADR-10: Response Value 0x02 is success; the board idles after registering
**Date:** 2026-08-10 | **Session:** #3 | **Status:** Accepted (developer directive)

**Context:** On hardware the Web Application answered `0x02`
(Already Registered). The board classified it as a rejection, closed the
connection, and re-registered every 30 seconds indefinitely.

**Decision:** `0x01` and `0x02` are both registration success, expressed as
`ME_REG_VALUE_IS_SUCCESS()` in `proto_defs.h`. After success the board enters
`ME_COMM_IDLE` and holds the connection without re-registering. Registration is
sent exactly once per TCP connection; on a reconnect it is sent again on the
new socket.

**Reason:** `0x02` means the server already holds the registration — the
device is registered either way. Re-registering per connection (rather than
never again) keeps the server's socket-to-device binding valid, and costs one
round trip answered by `0x02`.

**Impact:**
- ✅ No more 30-second re-registration loop
- ✅ `me_comm_state_t` is live code; logs show a real `IDLE` state
- ⚠️ `ME_COMM_REGISTERED` and `ME_COMM_MONITORING` removed — both were dead
- ⚠️ Not hardware-verified yet; needs a `deploy.ps1` run
```

- [ ] **Step 2: Update the `.claude/` state files**

In `.claude/CODEBASE_MAP.md`, add to the Gotchas list:

```markdown
- **Response Value `0x02` is SUCCESS**, not a rejection (ADR-10). Use
  `ME_REG_VALUE_IS_SUCCESS()`; never compare against
  `ME_REG_VALUE_REGISTERED` directly. Getting this wrong caused a 30-second
  re-registration loop on 2026-08-10.
```

In `.claude/TASKS.md`, add to the status table:

```markdown
| Registration success values | `0x01` and `0x02` both register; board then idles (ADR-10). Not yet hardware-verified. |
```

In `.claude/AGENT.md`, update the `DECISIONS.md` row of the live-state table
to read `**10 ADRs. ADR-10: 0x02 is success, board idles after registering.**`

- [ ] **Step 3: Update `me-primary/README.md`**

Add a third entry under the "Two things that will bite you" section, and
rename that heading to "Three things that will bite you":

```markdown
### 3. Response `0x02` means success, not failure

`0x01` (Registered) and `0x02` (Already Registered) are **both** success. The
board registers once per TCP connection and then idles with the connection
held open — it does not re-send the registration frame. On a reconnect it
registers again on the new socket and the server answers `0x02`.

Only `0x00` (Failed) and unrecognised values retry, with the usual 1s→30s
capped backoff.
```

- [ ] **Step 4: Final verification**

Run both layers one last time:

```powershell
.\build-native.ps1
.\build.ps1
```

Expected: `RESULT: PASS` (53 checks), clean `ELF64` / `AArch64` cross-build.

Then report to the developer that hardware verification is required and has
**not** been performed:

```powershell
.\deploy.ps1 -BoardIP <board-ip> -ServerIP <web-app-ip>
```

Hardware acceptance criteria:
1. The `DEVICE REGISTERED` banner appears showing
   `Server response : 0x02 (Already Registered)`
2. The log shows `state: IDLE - registered, holding connection`
3. No further `registration request` hex dump appears while the connection
   stays up — the 30-second loop is gone

---

## Verification ladder

| Layer | Command | What it proves |
|---|---|---|
| Unit tests | `.\build-native.ps1` | Success predicate and parse semantics; the captured `0x02` response registers |
| Cross-build | `.\build.ps1` | Compiles clean under `-Werror`; static aarch64 ELF |
| **End-to-end** | `.\deploy.ps1` | **The only proof the fix works. Developer-run — Claude Code has no network path to the board.** |

Do not describe this work as verified on the strength of the first two rows.
