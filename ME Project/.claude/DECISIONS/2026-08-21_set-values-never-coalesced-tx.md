# ADR-39: SET_VALUES CAN-FD transmission is never coalesced

> Date: 2026-08-21 | Session: #16 | Status: Accepted — ✅ HARDWARE-VERIFIED
> File: `DECISIONS/2026-08-21_set-values-never-coalesced-tx.md`

## Context

ADR-35 introduced a 50ms coalescing window (`ME_CAN_TX_COALESCE_US`) applied
to *every* physical CAN-FD block transmission — SET-triggered or
READ-triggered alike — so that up to 4 channels sharing one block would not
each put a redundant frame on the wire.

During hardware verification on 2026-08-20/21, the developer asked how many
`SET_COMMAND` (`0x021`) frames actually reached the M7 during a 4-channel
CCChg/STOP run, and specifically requested only the SET-triggered physical
transmissions be logged (`log_tx_frame()` in `can_mgr.c`). That log showed
only 3 physical frames for 8 logical per-channel SET events (4× CCChg, 4×
STOP): ch3 and ch4's own SET calls were coalesced away at the moment they
occurred, with their data reaching the wire only later, piggy-backed on a
transmission triggered by a *different* channel's READ poll — a call this
logging did not cover.

When the log was widened to print every physical transmission regardless of
trigger, the developer's core objection surfaced: **there was no log
evidence that ch3/ch4's setpoints ever reached the M7 as their own SET
event** — only an indirect inference from RX feedback, and that RX feedback
turned out to be static/canned test data unrelated to the actual setpoint
value sent (confirmed with the M7 test-rig owner), so it could not be used as
proof either.

## Decision

`handle_can_tx()` in `can_mgr.c` no longer applies `ME_CAN_TX_COALESCE_US` to
SET-triggered calls. A SET always merges into the per-Secondary/per-block
shadow buffer **and** always produces an immediate physical CAN-FD
transmission — no matter how recently another frame went out for that block.

READ/poll-triggered calls are unaffected: they still coalesce to one physical
frame per 50ms per block, since a READ never carries new data — it only
re-announces the shadow buffer that the last SET (or the initial zero state)
already established.

```c
/* SET frames are exempt (ADR-39): a setpoint update from a newly decoded
 * program step must reach the M7 immediately, never silently absorbed
 * into a later, unrelated transmission. */
const uint32_t now = now_us();
if (function5 != ME_CAN_FUNC_SET
    && (now - s_last_tx_us[secondary][block_ix]) < ME_CAN_TX_COALESCE_US) {
    s_active_ch[secondary] |= (uint8_t)(1u << (channel - 1u));
    s_tx_coalesced++;
    return;
}
```

This does not reopen the bug ADR-32/ADR-35 fixed (one channel's frame
zero-stomping another's live setpoint): every physical SET transmission still
sends the full merged `shadow` buffer, not a single-channel frame, so a
channel whose SET didn't trigger this particular send still has its last
known state correctly represented in whichever frame actually goes out.

## Consequences

- Up to 4 near-simultaneous SET calls (e.g. all 4 channels starting CCChg in
  the same tick) can now each produce their own physical frame in quick
  succession, rather than collapsing to one. This is more CAN-FD traffic than
  ADR-35's original design, but it is bounded (at most 4 extra frames per
  block per real event) and trades a small amount of bus bandwidth for a hard
  guarantee that no setpoint update is ever silently absorbed into someone
  else's transmission.
- READ/poll coalescing (the bulk of the traffic — 100ms period × up to 4
  channels) is untouched, so overall bus load is not meaningfully higher.
- `log_tx_frame()` (formerly `log_tx_set_frame()`) now only prints
  SET-triggered transmissions (`ME_CAN_TX_FRAME_LOG`), per developer request
  — READ-triggered frames (CAN ID `...22`) never carry new data and were
  judged not useful to see right now. Flip the gate back if that changes.
- `log_rx_frame()` (`ME_CAN_RX_FRAME_LOG`) is off as of this session: the
  bench M7 test rig replies with the same static per-channel V/A values
  regardless of the setpoint sent, so dumping RX content has no diagnostic
  value until a real Secondary (or a rig that echoes the actual setpoint) is
  on the bus.

## Verification

Hardware-verified on `172.16.18.167` post-fix: every channel's SET now
produces its own logged `TX SET to M7` frame with the correct per-channel
slot content (voltage, current, command), confirmed byte-for-byte against
the decoded slot values — not inferred from RX feedback.
