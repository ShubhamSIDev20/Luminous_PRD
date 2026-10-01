# Registration success semantics and the IDLE state

**Date:** 2026-08-10 | **Session:** #3 | **Status:** Approved by developer
**Amends:** `Docs/specs/2026-08-07-me-primary-registration-design.md`
§3.2 (which states registration is "gated solely on `Value == 0x01`") and
§4.2 (whose state diagram shows the now-removed `REGISTERED` and `MONITORING`
states).

---

## 1. Problem

Observed on hardware, 2026-08-10:

```
[06:00:41] INFO   registration response (7 bytes)
        0000  DD 01 01 11 02                                    |.....|
[06:00:41] ERROR  registration: rejected by server - value 0x02 (Already Registered)
[06:00:41] WARN   state: REGISTERING failed, retrying in 30000 ms
```

The board registered successfully, the server replied **`0x02` Already
Registered**, and the board treated that as a rejection. It then closed the
connection and re-registered every 30 seconds, forever.

`0x02` is a **success** response. It means the Web Application already holds a
registration for this device. Once the server has answered `0x01` or `0x02`,
the device is registered and the board must stop sending the registration
frame — it should sit idle on the open connection.

### Root cause

One comparison, in `src/proto/reg_frame.c:69`:

```c
out->registered = (out->value == ME_REG_VALUE_REGISTERED);
```

`registered` is false for `0x02`, so `do_registration()` returns false
(`src/comm_thread.c:175`), which drops the connection into the failure path
(`src/comm_thread.c:279`) and the capped-backoff retry loop.

### What is NOT the cause

The board does **not** re-send the registration frame within a live
connection. `do_registration()` is called exactly once per connection, and
`monitor()` then holds that connection for its lifetime without ever
registering again. The repetition came solely from the failure path closing
and reopening the connection. This matters for the design: the fix is to stop
misclassifying `0x02`, **not** to add a "have I already registered?" guard.

---

## 2. Scope

**In scope**

- Treat response `Value` `0x01` and `0x02` as registration success
- Make the post-registration idle state explicit and observable
- Host-side tests for both

**Out of scope**

- CRC byte order. Parked at the developer's instruction on 2026-08-10; the
  board is still running the pre-ADR-9 binary. The evidence gathered from the
  log above — the server emits its response CRC **little-endian**
  (`DD 01 01 11 02` → CRC `0xBE15`, on the wire `15 BE`) — is recorded in
  ADR-9 for when the new binary is flashed. Nothing in this document depends
  on it.
- Persisting registration across process restarts
- The `0xAA` / `0xBB` / `0xEE` / `0xA0` command groups

---

## 3. Design

### 3.1 Protocol layer — a named success predicate

`0x01` and `0x02` are both success. The distinction is informational only.
The test becomes a named predicate in `src/proto/proto_defs.h`, next to the
value definitions it depends on:

```c
/*
 * Both 0x01 (Registered) and 0x02 (Already Registered) mean the device IS
 * registered; 0x02 simply says the Web Application already held a record.
 * Only 0x00 (Failed) and unrecognised values are rejections.
 */
#define ME_REG_VALUE_IS_SUCCESS(v)                                            \
    ((uint8_t)(v) == ME_REG_VALUE_REGISTERED ||                               \
     (uint8_t)(v) == ME_REG_VALUE_ALREADY_REGISTERED)
```

`me_reg_response_t.registered` (`src/proto/reg_frame.h:46`) changes meaning
from *"value was exactly 0x01"* to *"the server considers this device
registered"*:

```c
out->registered = ME_REG_VALUE_IS_SUCCESS(out->value);
```

No new struct field. The raw `value` byte is already carried, so callers that
need to tell `0x01` from `0x02` — currently only the banner — read it
directly via `me_reg_value_name()`.

This keeps the decision in `src/proto/`, which is pure logic with no sockets
or platform headers (ADR-7), so it is covered by the host test suite.

### 3.2 Parse behaviour is unchanged

A response carrying `0x00` or an unrecognised value is still
`ME_REG_PARSE_OK` — the *frame* is well formed. Only `registered` is false.
Parse results continue to describe frame validity, never business outcome.
Echo mismatches remain informational and never block registration.

### 3.3 State machine — IDLE becomes real

`me_comm_state_t` and `me_comm_state_name()` exist in `comm_thread.h` but are
dead: no variable holds a state and the logs use hardcoded strings
(`"state: CONNECTING"`, `"state: MONITORING"`). The developer approved wiring
them up rather than adding a fourth hardcoded string.

`ME_COMM_REGISTERED` and `ME_COMM_MONITORING` — both unused — collapse into a
single `ME_COMM_IDLE`:

```c
typedef enum {
    ME_COMM_CONNECTING = 0,
    ME_COMM_REGISTERING,
    ME_COMM_IDLE,     /* registered; TCP held open and polled */
    ME_COMM_STOPPED
} me_comm_state_t;
```

`comm_thread_main()` holds a `me_comm_state_t state` and logs every
transition through `me_comm_state_name()`. Operator-visible result:

```
state: IDLE - registered, holding connection, watching TCP and UDP 10000/10001
```

instead of `state: MONITORING`.

```
CONNECTING ──▶ REGISTERING ──▶ IDLE   (registered; connection held open)
     ▲              │            │
     │           failure     disconnect
     └──────────────┴────────────┘
              capped backoff, 1s → 30s
```

### 3.4 Reconnect behaviour

**Registration is sent once per TCP connection.** Decided by the developer on
2026-08-10 over the alternative of never re-registering within a process
lifetime.

While a connection is alive the frame is never re-sent — structurally, not by
a flag. If the connection drops, the board reconnects and registers again on
the new socket; the server answers `0x02`, which is now success, and the board
returns to IDLE.

**Reason:** registration is how the Web Application binds a TCP socket to a
device. A reconnected socket that never registers is anonymous, and the server
would have no way to route commands to it. Re-registering is also
self-correcting: the `0x02` reply costs one round trip and confirms the
binding.

### 3.5 Failure handling — unchanged

`0x00 Failed`, an unrecognised value, a malformed frame, a CRC mismatch, or no
response within `--timeout` all close the connection and retry with the
existing 1s→30s capped backoff. Backoff resets to 1s after a success.

### 3.6 Banner

The `DEVICE REGISTERED` banner prints for both success values and reports
which one arrived:

```
  Server response : 0x02 (Already Registered)
```

---

## 4. Files touched

| File | Change |
|---|---|
| `src/proto/proto_defs.h` | Add `ME_REG_VALUE_IS_SUCCESS()` |
| `src/proto/reg_frame.c` | `registered` uses the predicate |
| `src/proto/reg_frame.h` | Update the `registered` field comment |
| `src/comm_thread.h` | `ME_COMM_REGISTERED` + `ME_COMM_MONITORING` → `ME_COMM_IDLE` |
| `src/comm_thread.c` | Hold a state variable; log via `me_comm_state_name()` |
| `tests/test_reg_frame.c` | New cases; one existing case split (see §5) |

No change to `main.c`, `sys_init.[ch]`, `net/`, `platform/`, or either build
script. `me_comm_is_registered()` keeps its meaning ("a registration succeeded
at least once") and now also becomes true on `0x02`.

---

## 5. Tests — written first

Host-side only, run by `build-native.ps1`. No board required.

**New**

| Case | Asserts |
|---|---|
| `ME_REG_VALUE_IS_SUCCESS` truth table | `0x00`→false, `0x01`→true, `0x02`→true, `0xFF`→false |
| Parse, `Value == 0x02` | `ME_REG_PARSE_OK` **and** `registered == true` |
| Parse, `Value == 0x01` | unchanged — `PARSE_OK`, `registered == true` |
| Parse, `Value == 0x00` / `0xFF` | `PARSE_OK`, `registered == false` |
| Golden response frame | The exact 7 bytes seen on hardware — `DD 01 01 11 02 15 BE` — parse to `value == 0x02`, `registered == true`, no echo mismatch |

The golden response is asserted as a literal byte array parsed with an
**explicit** `ME_CRC_ORDER_LE` (the order the server actually used in that
capture), not with `ME_CRC_ORDER_DEFAULT`. This deliberately decouples it from
the parked CRC-order decision: it replays what the hardware sent and stays
valid whichever way the default ends up settling.

**Changed — flagged deliberately**

`test_parse_rejects_non_success_values` (`tests/test_reg_frame.c:249`) loops
over `{0x00, 0x02, 0xFF}` asserting `!rsp.registered`. `0x02` moves out of
that set. **This inverts an existing passing assertion.** It is the correct
change — the old assertion encoded the bug — but it must be reviewed as a
deliberate semantic change, not absorbed silently.

**Verification ladder** (per project convention):

| Layer | Command | Proves |
|---|---|---|
| Unit tests | `.\build-native.ps1` | Success predicate and parse semantics |
| Cross-build | `.\build.ps1` | Compiles `-Werror`, static aarch64 ELF |
| End-to-end | `.\deploy.ps1` | **Developer-run.** `0x02` → banner → IDLE, no 30s retry |

Hardware acceptance: register once, confirm the banner and `state: IDLE`, then
restart the process and confirm the second run receives `0x02` and still
reaches IDLE without entering the retry loop.

---

## 6. Success criteria

1. A response of `0x02` produces the `DEVICE REGISTERED` banner, not
   `rejected by server`
2. After success the log shows `state: IDLE` and the registration frame is not
   re-sent while the connection stays up
3. `0x00` and unrecognised values still retry with capped backoff
4. All host unit tests pass; the cross-build is clean under `-Werror`

---

## 7. Open items

None. CRC byte order is parked by developer instruction (§2) and tracked in
ADR-9, not here.
