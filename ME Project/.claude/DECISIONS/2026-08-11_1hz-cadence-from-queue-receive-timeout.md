# ADR-15: The 1 Hz cadence comes from the queue receive timeout
> Date: 2026-08-11 | Session: #4 | Status: Accepted
> File: `DECISIONS/2026-08-11_1hz-cadence-from-queue-receive-timeout.md`

---

**Context:** The demo emitter needs a 1 Hz tick. The obvious options were a
timer thread or a `timerfd` in the poll set.

**Decision:** Neither. Core Logic's `me_msgq_recv()` already had to be bounded
so a stop request is noticed within 200 ms. `me_demo_ms_until_next_tick()`
shortens that bound when a frame is due, so the existing wait *is* the timer.

**Consequences:** No fifth thread, no extra descriptor. Deadlines advance
absolutely (`next_tick_ms += ME_DEMO_TICK_MS`), not `now + 1000`, so scheduling
jitter does not accumulate across 60 ticks.

**Related**
- Supersedes: none
- Relates to: ADR-16, ADR-12
