# ADR-12: Every queue send has a finite timeout and drops on expiry
> Date: 2026-08-11 | Session: #4 | Status: Accepted
> File: `DECISIONS/2026-08-11_queue-send-finite-timeout-drop-on-expiry.md`

---

**Context:** Four threads sending to each other's queues can deadlock: Core
Logic blocked sending to the Data Manager while the Data Manager is blocked
sending to Core Logic.

**Decision:** No unbounded send anywhere. 50 ms for control and request
messages, 500 ms for bulk chunks, 200 ms on every receive. On expiry the message
is dropped, `q->dropped` increments, and the drop is logged with the queue name
and message type.

**Consequences:** Deadlock is impossible by construction rather than by
argument. Congestion becomes a visible, counted loss. `me_queues_report()` at
shutdown logs every counter, and a non-zero drop count is raised to WARN — that
is the first place to look when a frame "never arrived".
Chunked transfers additionally carry an absolute `offset` that the receiver
verifies, so an abandoned transfer is detected instead of silently assembling a
program with a hole in it.

**Related**
- Supersedes: none
- Relates to: ADR-11, ADR-15
