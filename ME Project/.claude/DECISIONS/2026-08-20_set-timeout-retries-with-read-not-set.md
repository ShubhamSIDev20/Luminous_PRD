# ADR-37: A missed SET_VALUES response retries with READ_VALUES, never re-sends SET_VALUES
> Date: 2026-08-20 | Session: #14 | Status: Accepted — ⚠️ hardware-critical, hardware-verified

---

**Decision:** `step_engine.c`'s `me_exec_on_response_timeout()` always
retries a missed response with `send_poll()` (READ_VALUES), regardless of
whether the frame that timed out was itself a SET or a READ. Previously, a
missed SET_VALUES response retried with **another** `send_setpoint()` call —
a second live command frame, not just a re-request for feedback.

**Reason:** developer-reported: *"SET CAN command is getting sent multiple
times that should not happen. It should only happen after decoding the new
program step."* `master_slave_can_v1.0.md` (`Set_Cmd` sheet) states this
explicitly: *"The Secondary LATCHES the setpoint... No keep-alive re-send is
required, and the Master must not re-issue a setpoint merely to obtain
feedback."* A SET_VALUES frame is applied by the Secondary the instant it's
received — before it replies — so a SET that gets no response was still
applied; the setpoint is already in effect. A READ_VALUES elicits the same
feedback response (both functions share one retry/offline counter per
`me_exec_ctx_t`, unchanged) without re-commanding anything.

Every other SET_VALUES call site is correct and untouched: `enter_step()`'s
`ME_OP_CCCHG` case sends it exactly once per newly-decoded step, and
`me_exec_force_stop()` sends it once on Stop — both are genuine new commands,
not a retry of an old one.

**Impact:**
- ✅ Hardware-verified 2026-08-20: this is one of the fixes behind the run
  that showed `rx echo 0` (see ADR-38) — repeated SET frames on the wire are
  the likely (though unconfirmed) trigger for the M7's self-echo behavior;
  removing the repeats coincided with the echo disappearing in the next run.
- ✅ 229 host checks passing after updating
  `tests/test_step_engine.c`'s `test_missed_responses_retry_then_go_offline`
  to assert `ME_EXEC_OUT_CAN_READ` (not `ME_EXEC_OUT_CAN_SET`) on both
  retries — the test encoded the old, incorrect behavior.
- ✅ `.\build.ps1` compiles clean (`-Werror`).
- No change to the 3-strikes/`ME_EXEC_CHANNEL_OFFLINE` logic, `missed_responses`
  counting, or `ME_EXEC_RESPONSE_TIMEOUT_MS`/`ME_EXEC_RETRY_LIMIT`.

**Related**
- Relates to: ADR-25 (source of the "Secondary latches the setpoint" rule
  this ADR enforces on the retry path, not just the happy path)
- Relates to: ADR-36 (landed same session; together they are why the
  hardware run after both showed 0 missed responses / 0 offline)
- Relates to: ADR-38 (the echo this ADR's fix appears to have eliminated as
  a side effect, observed but not yet proven causal)
