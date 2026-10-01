# T-42: me_frame_resolve_len() — locate a frame boundary by CRC when the layout table is wrong
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #8)
> File: `tasks/2026-08-12_frame-resolve-len-crc-backstop.md`

---

## Description
ADR-19. The T-41 mis-split left 4 orphan bytes that would have desynced
every following frame on the connection; this makes that class of bug
non-fatal and logs a WARN naming both lengths so the table gets fixed. Also
splits coalesced `0xAA` frames, which previously had no boundary at all. 6
new checks.

## Progress Log
- **2026-08-12** (Session #8): Implemented alongside T-41.

## Related
- Relates to: ADR-19, T-41, T-44
