# T-22: Collapse dead ME_COMM_REGISTERED/ME_COMM_MONITORING into a live ME_COMM_IDLE
> Created: 2026-08-10 | Status: Done (2026-08-10, Session #3)
> File: `tasks/2026-08-10_collapse-dead-comm-states.md`

---

## Description
Both states were unreferenced except inside `me_comm_state_name()`; folded
into one live `ME_COMM_IDLE`, `monitor()` renamed `idle_loop()`.

## Progress Log
- **2026-08-10** (Session #3): Implemented alongside T-21 (ADR-10).

## Related
- Relates to: ADR-10, T-21
