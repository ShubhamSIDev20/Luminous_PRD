# T-66: Decide whether CCChg's SET_VALUES should ever carry a non-zero voltage
> Created: 2026-08-20 | Status: Backlog
> File: `tasks/2026-08-20_ccchg-voltage-always-zero.md`

---

## Description
`step_engine.c`'s `send_setpoint()` always sends `sp.set_voltage = 0.0f` for
a `CCChg` step - confirmed against `step_decode.h`: `me_step_t`'s CCChg
fields carry only `nominal_current_a`, no voltage field exists in the
decoded step at all. Raised by the developer during session #15; not yet
resolved whether this is intentional (constant-current mode, Secondary
applies its own CV limit internally) or a gap in `step_decode.c`/the
program-packet format that should carry a voltage target somewhere not
currently decoded.

## Why / Context
Matters for what `Set Voltage` actually means to the Secondary hardware in
CHA/CMD_CHA mode - worth confirming against `program_packet_v0.12.md` and
the old firmware's `stepData.c` before deciding either way.

## Progress Log
- **2026-08-20**: Raised during session #15's CAN-FD debugging; deferred to
  its own task since it's a decode-scope question, not a bug with a known fix.

## Related
- Relates to: ADR-25 (SET_VALUES/READ_VALUES two-phase exchange)
