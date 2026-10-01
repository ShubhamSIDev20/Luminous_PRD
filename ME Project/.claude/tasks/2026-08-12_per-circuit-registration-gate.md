# T-32: Per-circuit registration gate
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #6)
> File: `tasks/2026-08-12_per-circuit-registration-gate.md`

---

## Description
`store/circuit_registry.[ch]`, written by `do_registration()` on a
`0x01`/`0x02` reply, read by `route_frame()` before any queue send.
Test-first, 12 new checks (111→123). Range-validated `--secondary`/`--channel`
(they silently truncated). Code-reviewed: no Critical, 4 Important applied.
ADR-17.

## Progress Log
- **2026-08-12** (Session #6): Implemented. **NOT hardware-verified.**

## Related
- Relates to: ADR-17, T-33, T-34, T-35, T-36
