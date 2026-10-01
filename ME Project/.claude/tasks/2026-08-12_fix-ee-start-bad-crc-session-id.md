# T-41: Fix the 0xEE Start BAD_CRC — Q1 carries a 4-byte Session ID
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #8)
> File: `tasks/2026-08-12_fix-ee-start-bad-crc-session-id.md`

---

## Description
The control doc did not list it. Reported from hardware:
`EE 01 11 01 01 11 35 F3 62 E7` rejected while its CRC was correct, because
the frame was split at 6 bytes. `me_control_t` gained
`session_id`/`has_session_id`; `expected_len` returns 10 for Q1; Core Logic
logs the ID. Regression test uses the literal captured bytes.
`Ref Docs/bm_control_v3.0.md` amended.

## Progress Log
- **2026-08-12** (Session #8): Fixed and verified against the captured
  frame.

## Related
- Relates to: ADR-19, T-42
