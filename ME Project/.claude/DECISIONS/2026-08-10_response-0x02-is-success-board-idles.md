# ADR-10: Response Value 0x02 is success; the board idles after registering
> Date: 2026-08-10 | Session: #3 | Status: Accepted — **✅ HARDWARE-VERIFIED 2026-08-10 09:38 UTC**
> File: `DECISIONS/2026-08-10_response-0x02-is-success-board-idles.md`

---

> **Verification.** Against the real Web Application at `172.16.15.230:9999`:
> response `DD 01 01 11 02 BE 15` produced the `DEVICE REGISTERED` banner
> reporting `0x02 (Already Registered)`, followed by
> `state: IDLE - registered, holding connection`. No further registration
> frame was sent. The 30-second re-registration loop is gone.

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

The "register once" rule is **structural, not flag-based**: `do_registration()`
is called once per connection and `idle_loop()` never registers. There is no
"have I registered?" boolean to get wrong.

**Impact:**
- ✅ No more 30-second re-registration loop
- ✅ `me_comm_state_t` is live code; logs show a real `IDLE` state
- ⚠️ `ME_COMM_REGISTERED` and `ME_COMM_MONITORING` removed — both were dead
  code, referenced only inside `me_comm_state_name()`
- ✅ Also resolved the reported "TCP connection keeps disconnecting": the board
  was closing its own socket via `me_tcp_close()` on the registration-failure
  path. Not a network fault, and not a source-port problem.

**Related**
- Supersedes: the prior behavior treating `0x02` as a rejection
- Relates to: ADR-9
