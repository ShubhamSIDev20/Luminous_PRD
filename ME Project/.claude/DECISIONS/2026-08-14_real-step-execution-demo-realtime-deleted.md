# ADR-27: Real program-step execution implemented; `demo_realtime` deleted
> Date: 2026-08-14/15 | Session: #10 | Status: Accepted, NOT hardware-verified
> File: `DECISIONS/2026-08-14_real-step-execution-demo-realtime-deleted.md`

---

`me_execute_program()` (the seam from ADR-16) now starts a real per-circuit
`step_engine` instead of arming the 1 Hz demo ramp. Scope: `SET`→`CCChg`→`STOP`,
one TIME cutoff, one circuit, no physical CAN device.

- **`step_decode` / `can_frame` / `step_engine` are pure, host-tested modules**
  (no sockets, no platform headers) — same discipline as `proto/` (ADR-7).
  `step_engine`'s clock is a **parameter** (`now_ms`), never read internally, so
  a 10-second cutoff is provable in microseconds of host test time.
- **CAN-FD exchange is two-phase, per the developer's correction of the initial
  reading**: `SET_VALUES` applies a setpoint (Secondary **latches** it, no
  keep-alive needed); `READ_VALUES` polls read-only. **The Secondary answers
  every Master frame**, both function codes — one retry/offline counter (3
  strikes → `ME_EXEC_CHANNEL_OFFLINE`) covers both.
- **Timing:** 10ms engine tick (cutoffs) · 100ms CAN poll · 1000ms `0xCC` emit.
  Meets the architecture doc's 50ms cutoff-latency budget since a TIME cutoff
  needs no CAN feedback.
- **`can_mgr.c` fabricates feedback** (no physical CAN device): echoes the last
  commanded setpoint, voltage drifts slowly, so numbers on the Web App
  correlate with the program instead of being a fixed constant or independent
  ramp.
- **`demo_realtime.c/.h` deleted.** The one thing worth keeping — the
  post-registration one-shot `0xCC` frame — moved to `threads/post_reg.[ch]`
  and is now **permanent**, not scaffolding.

**Consequences:**
- ✅ 189 host checks passing (was 158); both builds clean
- ⚠️ Only `SET`/`CCChg`/`STOP` and a TIME cutoff are implemented — the rest of
  the BTS-600 operator set (`A1_Operator_Reference.md`) is out of scope
- ⚠️ **NOT hardware-verified.** No physical CAN device exists yet.
- See ADR-28 for the two open multi-channel items this design deliberately deferred.

Design: `Docs/specs/2026-08-14-step-execution-design.md`.
Plan: `Docs/plans/2026-08-14-step-execution-plan.md` (13 TDD tasks).

**Related**
- Supersedes: ADR-16 (the demo real-time emitter)
- Relates to: ADR-28, ADR-25, ADR-26, ADR-29, T-24, T-54
