# T-38: Confirm the 0xBB Q1 frame length against a captured hex dump
> Created: 2026-08-12 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-12_confirm-bb-q1-frame-length.md`

---

## Description
`ME_PRG_Q1_LEN = 6` rests on the layout, **not on a captured frame**.
**Downgraded from 🔴 by ADR-19:** a wrong value is no longer a connection-wide
desync — `me_frame_resolve_len()` recovers the boundary from the CRC and logs
`RECOVERED, but the length table … needs fixing`. Still worth confirming; now
folded into the broader **T-44** audit.

## Why / Context
The developer's 2026-08-12 log has the answer in its `inbound TCP data` lines.

## Progress Log
- **2026-08-12** (Session #7/#8): Opened alongside the `0xBB` handshake work
  (ADR-18); downgraded in severity once ADR-19's CRC backstop landed.

## Related
- Relates to: T-44, ADR-18, ADR-19
