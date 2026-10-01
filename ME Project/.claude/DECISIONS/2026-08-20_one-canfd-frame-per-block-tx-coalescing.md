# ADR-35: One physical CAN-FD frame per Secondary/block, not one per channel
> Date: 2026-08-20 | Session: #14 | Status: Accepted — ⚠️ hardware-critical, hardware-verified

---

**Decision:** Two changes to `can_mgr.c`'s `handle_can_tx()`, delivered
together:

1. **READ/poll frames now transmit `s_set_shadow` (the same per-block buffer
   SET_VALUES merges into), not `me_can_pack_read()`'s own near-empty
   buffer.** A READ is still a real 64-byte block on the wire, and the
   zero-fill danger ADR-32 fixed for SET (byte +8 = `0x00` is an active
   `CMD_STO` to whichever channel owns that slot) applies to it identically.
   Each channel polls independently every `ME_EXEC_POLL_PERIOD_MS` (100 ms),
   so a poll going out is not rare — transmitting the near-empty buffer as-is
   would blank the other three channels' setpoints on essentially every poll.
2. **A new per-Secondary/block rate limit, `ME_CAN_TX_COALESCE_US` (50 ms):**
   `handle_can_tx()` physically transmits at most once per block per window;
   a request arriving before the window elapses is counted (`s_tx_coalesced`)
   and dropped without going on the wire. The channel is still marked active
   (`s_active_ch`) even when its own transmission is coalesced away, so
   `handle_stream_frame()`'s broadcast still reaches it via whichever
   sibling channel's transmission does go out.

**Reason:** developer requirement: *"we have to send only one CANFD frame
which will have all 4 channel data."* Up to 4 channels can each
independently decide to SET or poll at any moment (one `me_exec_ctx_t` per
channel, ADR-32); without coalescing, all 4 would put their own frame on the
wire even though every block frame already carries all 4 channels' data and
the Secondary's one reply already answers every active channel
(`handle_stream_frame()`'s existing broadcast-to-mask logic).

**Why the 50 ms window is safe:** well under `step_engine.c`'s own
`ME_EXEC_POLL_PERIOD_MS` (100 ms) and `ME_EXEC_RESPONSE_TIMEOUT_MS` (200 ms),
so a genuine per-channel retry is never mistaken for a redundant send. The
window is anchored to the last *actual* transmission time (`s_last_tx_us`),
not renewed on every attempt, so it cannot starve — real wall-clock time
passing always eventually clears it regardless of how many requests arrive
in between.

**Impact:**
- ✅ Hardware-verified 2026-08-20: 1874 TX requests from Core Logic collapsed
  to 470 physical CAN-FD frames in one run.
- ✅ 229 host checks still passing; `can_mgr.c` is Linux-only, excluded from
  `build-native.ps1`, same as before this change.
- ✅ `.\build.ps1` compiles clean (`-Werror`).

**Related**
- Extends: ADR-32 (`s_set_shadow`, the zero-fill fix this generalizes to READ)
- Relates to: ADR-38 (separates what we transmit, `s_set_shadow`, from what
  we forward to Core Logic, `s_read_feedback` — this ADR is what makes that
  split meaningful, since both SET and READ transmit the same buffer)
