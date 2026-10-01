# ADR-31: CAN-FD RPMsg round-trip latency logged per Secondary, microseconds
> Date: 2026-08-19 | Session: #11 | Status: Accepted
> File: `DECISIONS/2026-08-19_can-rpmsg-rtt-latency-logging.md`

---

**Decision:** `can_mgr.c` timestamps each successful TX (`CLOCK_MONOTONIC`,
`s_tx_us[secondary]`) and logs the gap to the matching validated reply as
`can: S%u RTT %u us`.

**Reason:** Developer asked to make the CAN manager↔M7 turnaround directly
observable during hardware testing. Kept deliberately short/always-on
(not gated behind `ME_RPMSG_DEBUG_LOG`) at today's single-channel volume.

**Impact:** ⚠️ If a retry sends a second TX before the first is answered,
the logged latency is against the most recent send, not necessarily the
one that produced the reply — acceptable approximation, not exact
correlation. Revisit if multi-channel makes this ambiguous.

**Related**
- Supersedes: none
- Relates to: ADR-29, ADR-32
