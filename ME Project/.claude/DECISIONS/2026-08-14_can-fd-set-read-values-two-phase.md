# ADR-25: CAN-FD `SET_VALUES`/`READ_VALUES` is a two-phase, developer-clarified exchange
> Date: 2026-08-14 | Session: #10 | Status: Accepted
> File: `DECISIONS/2026-08-14_can-fd-set-read-values-two-phase.md`

---

**Context:** First reading of `Master-Slave_CAN_V1.0.xlsx` concluded
`READ_VALUES` was Slave-only (no separate "read" command existed). The
developer corrected this — `READ_VALUES` is Master-issued too, sharing the
64-byte payload shape with `SET_VALUES`; the Secondary picks its action from
the function code in the CAN ID, not the payload.

**Decision:** Record both corrections in `Ref Docs/master_slave_can_v1.0.md`
with evidence (sheet title says "SET/**GET**_VALUES"; Normal Mode rule requires
the Query to carry `INT Parameter number`). Also recorded: the Secondary
**latches** the setpoint (no periodic re-send) and **answers every** Master
frame, not just reads.

**Consequence:** `step_engine` sends `SET_VALUES` once per step entry (not
periodically) and treats SET/READ responses identically for retry accounting.
See ADR-27.

**Related**
- Supersedes: an incorrect first reading of the CAN spec (never shipped)
- Relates to: ADR-27, ADR-28
