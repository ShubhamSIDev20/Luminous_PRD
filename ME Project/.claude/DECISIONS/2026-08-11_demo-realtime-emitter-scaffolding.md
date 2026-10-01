# ADR-16: The demo real-time emitter is scaffolding, quarantined in one file
> Date: 2026-08-11 | Session: #4 | Status: Accepted — ⚠️ TEMPORARY
> File: `DECISIONS/2026-08-11_demo-realtime-emitter-scaffolding.md`

---

**Context:** Step execution is not written, so nothing produces real-time data,
so the Communication → UDP 10000 path could not be observed working end to end.

**Decision:** Core Logic synthesises a `0xCC` frame once per second for 60
seconds with the Current field ramping 1.0 → 60.0 A, then a final frame carrying
Idle status, then the circuit parks in `ME_TEST_STOPPED`. All of it lives in
`src/threads/demo_realtime.c` behind `ME_DEMO_REALTIME`.

**Why Current:** it is payload offset 17 of `bm_measured_param_v5.2`, a field the
Web Application already renders. A moving number needs no change on their side.

**Why one file behind one macro:** removal must be a file deletion, not an
untangling. `me_execute_program()` in `core_logic.c` is the seam — its entire
body is `me_demo_arm(circuit_id)`.

**Delete this when real step execution lands.**

**Related**
- Supersedes: none
- Relates to: ADR-15, ADR-27 (deletes this)
