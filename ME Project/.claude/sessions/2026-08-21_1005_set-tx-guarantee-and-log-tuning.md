# Session #16 — 2026-08-21 10:05–11:15 IST — SET_VALUES always-transmit guarantee (ADR-39) + diagnostic log tuning
> Agent: Claude Code (Sonnet 5) | Status: ✅ Complete, hardware-verified

---

## Goal

Continuation of session #15's coalescing work. Developer asked why only one
`0x021` (SET_COMMAND) frame appeared in the log for a 4-channel CCChg/STOP
step instead of the expected 4, and wanted the actual SET frame bytes printed
so the answer could be checked against evidence instead of theory.

## What Happened

**Diagnostic — SET-only TX log added.** Added `log_tx_set_frame()` /
`log_tx_frame()` to `can_mgr.c`, gated by `ME_CAN_TX_FRAME_LOG`, decoding
every physical block frame's 4 slots via `me_can_parse_feedback()` (works on
both directions per its own doc comment). First pass logged only
SET-triggered physical sends.

**Finding — coalescing hid ch3/ch4's SET transmissions.** With that log, only
3 physical `0x021` frames appeared for 8 logical SET events (4× CCChg start,
4× STOP). ch3/ch4's own SET calls were coalesced away at the moment of the
call (ADR-35's 50ms window), so no log line existed proving their setpoint
reached the wire as its own event. Widening the log to print every physical
transmission (SET- or READ-triggered) would have closed this gap, but the
developer's real objection was structural, not visibility: a SET should never
be allowed to be silently absorbed into someone else's later transmission at
all.

**Root-cause check — RX feedback is not valid evidence.** Before concluding
anything from RX, the developer confirmed the bench M7 test rig sends the
same static per-channel V/A values on every reply regardless of the actual
setpoint transmitted (his teammate hard-coded this on the rig side). This
retroactively invalidated an earlier claim (this session and the last) that
RX `state 0x01` transitions proved a channel's setpoint had landed — they
only proved *a* command byte was seen, not that the specific setpoint value
arrived. `log_rx_frame()` was switched off (`ME_CAN_RX_FRAME_LOG` → 0) since
its content currently carries no diagnostic value.

**Fix — ADR-39: SET_VALUES transmission is never coalesced.**
`handle_can_tx()`'s 50ms rate-limit now applies only to READ/poll-triggered
calls. Every SET call — merged into the shadow buffer as before — also
always produces an immediate physical CAN-FD transmission, regardless of how
recently another frame went out for that block. READ polling is unaffected
and still coalesces normally. See `DECISIONS/2026-08-21_set-values-never-coalesced-tx.md`.

**Log tuning per developer request, iterative:**
- `log_tx_frame()` now prints only SET-triggered sends (READ-triggered
  `...22` frames excluded — no longer useful once SET always transmits on
  its own).
- `core_logic.c`'s `on_program_loaded()` initially grew a
  "PROGRAM STEP N EXTRACTED" banner + hex dump for every step of every
  channel's program (previously step 1 only) at the developer's request, then
  had two banner fields ("Steps in program", "Step length") trimmed, then was
  removed entirely at the developer's final request — asked to confirm scope
  (all steps vs. just step 3) rather than guess, since hardcoding a skip for
  step 3 specifically would have baked one test program's shape into the
  code. `on_program_loaded()` is back to a single one-line log summary;
  step_engine.c's own `enter_step()` already logs its own error if a step
  can't be fetched/decoded, so nothing is lost.

## Verification

- `.\build-native.ps1`: 229 checks run, 0 failed (every round of this
  session's edits).
- `.\build.ps1`: clean aarch64 static ELF (every round).
- Hardware, `172.16.18.167`, `me-secondary_1` container (bind-mounted
  `me_primary_new`, on-device `docker-compose.yml` per the standing
  workflow): registered all 4 channels, ran the 4-channel program, confirmed
  every channel's SET now produces its own logged, byte-verified physical
  frame.

## Related
- Builds on: ADR-35 (coalescing), ADR-38 (echo filter), session #15
- New: ADR-39 (SET never coalesced)
- T-64 (GHCR release) now also needs to cover this session's changes
