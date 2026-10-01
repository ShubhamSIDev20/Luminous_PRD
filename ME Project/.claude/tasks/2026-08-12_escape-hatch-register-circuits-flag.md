# T-35: Escape hatch for admission control — --register-circuits / --gate off
> Created: 2026-08-12 | Status: Won't Do (2026-08-12)
> File: `tasks/2026-08-12_escape-hatch-register-circuits-flag.md`

---

## Description
Existed only for the case T-33 was guarding against — a hardware session
stuck because the Web App addressed a circuit the board had not registered.

## Why / Context
With the Web App conforming to the board's CircuitID, there is nothing to
escape from. Building it now would be speculative complexity.

## Progress Log
- **2026-08-12**: Closed as won't-do, following T-33's closure.

## Related
- Relates to: T-33, ADR-17
