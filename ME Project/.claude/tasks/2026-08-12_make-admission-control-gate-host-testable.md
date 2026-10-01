# T-36: Make the admission-control *gate* host-testable
> Created: 2026-08-12 | Status: Backlog (🟢 Low)
> File: `tasks/2026-08-12_make-admission-control-gate-host-testable.md`

---

## Description
`test_circuit_registry.c` proves the table, but the ordering inside
`route_frame()` (that `ME_MSG_NONE` is checked first, that the gate precedes
the queue send) lives in `comm_thread.c`, which is LINUX-ONLY and excluded
from `build-native.ps1`.

## Why / Context
Hoisting the decision into a pure predicate would let a host test drive every
start byte through classify-then-gate. Consistent with ADR-3 as-is; this
closes the gap rather than fixing a defect.

## Progress Log
- **2026-08-12** (Session #6): Opened alongside ADR-17 (the registration
  gate).

## Related
- Relates to: ADR-17, ADR-3
