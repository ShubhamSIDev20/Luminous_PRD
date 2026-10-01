# T-7: Confirm the real CRC byte order
> Created: 2026-08-07 | Status: Done (2026-08-07, Session #2) — later overturned
> File: `tasks/2026-08-07_confirm-real-crc-byte-order.md`

---

## Description
CRC byte order was confirmed little-endian in session #2 based on a
successful registration with no `--crc-order` flag. **This was overturned on
2026-08-10 (session #3, ADR-9)** — the developer specified high byte first
and the code now sends big-endian. See ADR-9 for the full reasoning and why
the session #2 argument was unsound (it never proved the request-direction
half).

## Progress Log
- **2026-08-10** (Session #3): Overturned by ADR-9 — see T-20.
- **2026-08-07** (Session #2): Closed as little-endian (ADR-5).

## Related
- Relates to: ADR-5, ADR-9, T-20
