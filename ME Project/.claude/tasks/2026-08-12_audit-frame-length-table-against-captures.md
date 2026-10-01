# T-44: Audit every remaining entry in the me_frame_expected_len() layout table against real captured frames
> Created: 2026-08-12 | Status: Backlog (🔴 High)
> File: `tasks/2026-08-12_audit-frame-length-table-against-captures.md`

---

## Description
T-41 proved one entry wrong (`0xEE` Q1) and the cost was a connection-wide
desync. **Partly closed in #9 (ADR-22):** `0xAA` went from *no entry at all*
to real lengths for all six circuit-scoped queries, and **Q5 = 46 is
confirmed against a captured frame**. Still unverified: `0xEE` Q2/Q3/Q4/Q6 = 6,
`0xBB` Q1 = 6 (T-38), `0xBB` Q3 = 8, and `0xAA` Q1–Q4 = 6 / Q6 = 10, which come
from the document but no capture.

## Why / Context
ADR-19 recovers a wrong entry rather than desyncing, and logs `RECOVERED, but
the length table … needs fixing`. **Grep the next hardware log for that WARN;
each occurrence is a wrong table entry naming itself.** ⚠️ And note what #9
learned: adding a *correct* entry broke a *different* frame (see the ADR-19
amendment). After adding any entry, check that a frame shorter than it still
resolves.

## Progress Log
- **2026-08-12** (Session #9): Partly closed by ADR-22 (all six `0xAA`
  circuit-scoped queries given real lengths, Q5 confirmed against capture).
  Remaining entries still unverified.

## Related
- Depends on: T-38 (subset — 0xBB Q1)
- Relates to: ADR-19, ADR-22, T-41
