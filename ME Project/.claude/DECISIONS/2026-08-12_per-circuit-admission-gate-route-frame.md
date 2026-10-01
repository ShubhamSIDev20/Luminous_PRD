# ADR-17: Frames are admitted per-circuit, gated in `route_frame()` alone
> Date: 2026-08-12 | Session: #6 | Status: Accepted — interim scope **known and accepted** (updated 2026-08-12, T-33 closed; registration model extended to multiple channels per connection by ADR-32, 2026-08-19)
> File: `DECISIONS/2026-08-12_per-circuit-admission-gate-route-frame.md`

---

> **UPDATE (session #12, 2026-08-19):** The "functionally single-circuit"
> finding below is no longer accurate as written. `--channels` (ADR-32) lets
> one connection register every operator-configured channel of a Secondary,
> independently. The gate mechanism itself (`route_frame()`, one 64-slot
> registry) is unchanged; only the number of circuits one connection can
> admit changed. Still true: only the channels named on the command line can
> ever register — a CAN-side self-announcing handshake still does not exist.

**Context:** The requirement is that a Secondary's Channel must register before
the board handles Program, Command or Config packets for it; anything else is
ignored. Before this, `route_frame()` checked only that the CircuitID was
*structurally* valid (nibbles 1–8) — a well-formed but never-registered circuit
was processed exactly like a live one.

**Decision:** A separate 64-slot table, `src/store/circuit_registry.c`, holding
one bool per circuit. `route_frame()` drops any frame whose CircuitID is not in
it, **before** the frame reaches a queue. `do_registration()` is the only writer:
on a `0xDD` success reply (`0x01` Registered or `0x02` Already Registered) it
marks its own configured Secondary/Channel.

**Why the gate lives in `route_frame()` and nowhere else:** every inbound frame
funnels through that one function before reaching a queue, so one check covers
`0xAA`, `0xBB` and `0xEE` alike, no consumer thread repeats it, and no future
message type can bypass it. Three copies in `data_mgr`/`core_logic` would drift.

**Why a separate module from `circuit_store`:** `circuit_store` answers *what has
this circuit sent*; this answers *may we act on it at all*. Data question vs
access-control question — folding them puts admission logic in a file whose
header says it is about storage.

**Why the registry is NOT cleared on disconnect:** no frame can arrive without a
connection, and `idle_loop()` is only entered after registration succeeds, so
there is no window where an unregistered frame is accepted. Clearing would also
wipe CAN-registered circuits on an unrelated TCP blip once that writer exists.

**Impact:**
- ✅ A frame for an unregistered circuit is dropped with a WARN naming the
  circuit, rather than silently acted upon.
- ℹ️ **The board is functionally single-circuit until CAN registration lands.**
  The `0xDD` frame carries one CircuitID and is sent once per connection, so 1 of
  64 circuits registers and the other 63 are refused — while `g_program[64]`,
  `g_cl_program[64]` and `g_demo[64]` are all built for 64. Deliberate and
  temporary; the flow works for the circuit passed via `--secondary`/`--channel`
  (`deploy.ps1` defaults to `0x11`).

  **Resolved as accepted, 2026-08-12:** the developer closed T-33 with *"Web
  application team will work as compliance with the current ME code"* — the Web
  App conforms to this board's CircuitID rather than the reverse. This was
  previously logged as an ⚠️ interim scope **risk**; it is now a **known
  constraint**. It remains true and remains worth reading before touching
  `circuit_registry.c` or adding the CAN-side writer, but it does not block the
  hardware run and must not be re-raised as a blocker. T-35 (a
  `--register-circuits` escape hatch) is won't-do for the same reason.
- ℹ️ Device-scoped commands (`0xEE` Q5 Sync Time, Q6 Reset) are gated as if they
  were circuit-scoped. Harmless today — Sync Time is a logged no-op — and lower
  risk since the Web App conforms to this board's CircuitIDs, but it will still
  present as a clock that never syncs once Sync Time is implemented. Open Item 6.
- ⚠️ The gate's *ordering* is not host-testable: `comm_thread.c` is LINUX-ONLY and
  excluded from `build-native.ps1`. The table is tested; the placement is not.

**Related**
- Supersedes: none (updated in place — see the 2026-08-19 UPDATE above)
- Relates to: ADR-32, T-33, T-34, T-35, T-36
