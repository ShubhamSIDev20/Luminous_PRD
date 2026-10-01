# T-34: Decide whether device-scoped 0xEE/0xAA queries should bypass the circuit gate
> Created: 2026-08-11 | Status: Backlog (🟢 Low)
> File: `tasks/2026-08-11_device-scoped-queries-bypass-circuit-gate.md`

---

## Description
`ME_CTRL_SYNC_TIME` (Q5) sets the board clock and `ME_CTRL_RESET` (Q6) is
plausibly device-scoped, but `route_frame()` gates both on CircuitID
(ADR-17).

## Why / Context
**Downgraded 2026-08-12** (was 🟡): the Web App conforms to this board's
CircuitIDs, so a device-scoped frame will carry an accepted one and the gate
will pass it. Zero impact today — Sync Time is a logged no-op. Keep the note
so it is not rediscovered as a mystery when someone implements Sync Time and
finds the clock never syncs. Fix shape if ever needed: a named
`frame_is_circuit_scoped(fi)` predicate.

## Progress Log
- **2026-08-12**: Downgraded from 🟡 to 🟢 following T-33's closure (Web App
  conforms to board CircuitIDs).
- **2026-08-11** (Session #6): Opened alongside ADR-17.

## Related
- Relates to: ADR-17, T-33
