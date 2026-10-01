# ADR-36: `step_run_ms`/`program_run_ms` accumulate unconditionally, never gated on CAN response state
> Date: 2026-08-20 | Session: #14 | Status: Accepted — ⚠️ hardware-critical, hardware-verified

---

**Decision:** `step_engine.c`'s `advance_time()` now adds elapsed wall-clock
time to `step_run_ms`/`program_run_ms` on every tick, unconditionally.
Previously it skipped the accumulation whenever `ctx->response_outstanding`
was true. `me_exec_on_response()` and `me_exec_on_response_timeout()` no
longer write `ctx->last_tick_ms` — `advance_time()` is now the only writer
outside step entry.

**Reason:** developer-reported symptom: running the identical 4-channel
program showed some channels executing "fast" and others "slow" — not
real-time. Root cause: every missed CAN response cost that channel a full
`ME_EXEC_RESPONSE_TIMEOUT_MS` (200 ms) of *step time*, because time only
advanced while no response was outstanding. Two channels that happened to
miss 184 responses each (out of a hardware run) lost roughly 37 seconds of
step time each, while two channels with zero misses lost none — same
program, wildly different progress.

This directly contradicts `Docs/specs/2026-08-14-step-execution-design.md`
§3.3/§4, verbatim:
- `uint32_t step_run_ms; /* frozen unless RUNNING */` — frozen when the
  engine is not `RUNNING`, not when a CAN response happens to be pending.
- *"a TIME cutoff needs only the local clock, not CAN feedback, so its
  resolution is bounded by the 10 ms tick regardless of poll rate"* — against
  a documented `T_CUTOFF_LATENCY <= 50 ms` budget that a 200 ms stall blows
  by 4x.

**Impact:**
- ✅ Hardware-verified 2026-08-20: re-ran the same 4-channel program after
  this fix (combined with ADR-34/35/37 landing the same day) with 0 missed
  responses observed in that run — the drift scenario could not be directly
  re-triggered to compare before/after on the same run, but the code path
  that caused it is gone by construction (there is no longer a "skip
  accumulation" branch to trigger).
- ✅ 229 host checks passing, including the existing timing-cutoff tests
  (`step_engine.c` IS host-tested — pure logic, no platform headers, unlike
  `comm_thread.c`/`can_mgr.c`).
- ✅ `.\build.ps1` compiles clean (`-Werror`).
- No change to `me_exec_tick()`'s cutoff-check logic itself, or to
  `response_outstanding`/`missed_responses` bookkeeping — only which writes
  touch `last_tick_ms`.

**Related**
- Relates to: ADR-37 (the SET-retry fix reduces how often a response goes
  missing in the first place, complementary to this ADR removing the cost of
  a miss when one does happen)
