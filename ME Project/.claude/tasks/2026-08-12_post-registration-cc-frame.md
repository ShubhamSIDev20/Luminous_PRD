# T-52: One-shot 0xCC frame after successful registration
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #9)
> File: `tasks/2026-08-12_post-registration-cc-frame.md`

---

## Description
Temporary demo scaffolding so the Web Application has live data before the
CAN Manager exists. Built by a pure, host-tested
`me_realtime_pack_post_registration()` in `realtime_frame.c` (not the
Linux-only demo file, so the exact 86 bytes could be proven on the laptop);
asserted byte-for-byte against the developer's frame including CRC `0xF261`.
Its own message buffer — it is the only `demo_realtime` function called from
the Communication thread. ADR-23.

## Progress Log
- **2026-08-12** (Session #9): Implemented. **NOT hardware-verified.**

## Related
- Relates to: ADR-23
