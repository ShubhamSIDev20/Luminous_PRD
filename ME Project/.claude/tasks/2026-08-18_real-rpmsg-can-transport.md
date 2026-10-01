# T-57: Real RPMsg CAN transport replacing the fabricated can_mgr.c responder
> Created: 2026-08-18 | Status: Done (2026-08-18/19, Session #11)
> File: `tasks/2026-08-18_real-rpmsg-can-transport.md`

---

## Description
Char-device I/O, wrap/parse, stream reassembler w/ resync+overflow tests,
pollable `g_q_can`. ADR-29. Closes T-30.

## Progress Log
- **2026-08-18/19** (Session #11): Implemented and hardware-verified for the
  link itself; closes T-30.

## Related
- Relates to: ADR-29
