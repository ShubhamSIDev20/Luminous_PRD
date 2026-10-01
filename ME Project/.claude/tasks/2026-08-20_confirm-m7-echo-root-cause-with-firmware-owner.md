# T-65: Confirm the M7 CAN-echo root cause with whoever owns the M7 firmware
> Created: 2026-08-20 | Status: Backlog
> File: `tasks/2026-08-20_confirm-m7-echo-root-cause-with-firmware-owner.md`

---

## Description
Session #15 found the M7 handing our own transmitted CAN-FD frames back to
the A53 on the identical CAN ID a genuine Secondary reply uses (see ADR-38).
`Ref Docs/RPMSG_PROTOCOL.md` documents no field distinguishing origin.
Working theory: the M7's CAN controller has self-reception/loopback enabled.
ADR-38 adds an A53-side filter (compare against `s_set_shadow`), which is a
workaround, not a fix at the source.

In the verification run right after ADR-35/36/37/38 landed together, the
echo did not recur at all (`rx echo 0`, confirmed against raw frame content,
not just the counter) - plausibly because ADR-37 (no more repeated
SET_VALUES) removed whatever traffic pattern triggered it. Unconfirmed.

## Why / Context
If the echo is genuinely gone because of ADR-37, this task may downgrade to
"keep the filter as a defensive backstop, no firmware change needed." If it
recurs under different load, the M7-side loopback setting should be found
and disabled at the source rather than relied on being filtered downstream.

## Progress Log
- **2026-08-20**: Task opened alongside ADR-38/session #15.

## Related
- Relates to: ADR-35, ADR-37, ADR-38
