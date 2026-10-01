# ADR-38: Separate SET/READ CAN-FD buffers in `can_mgr.c`; the split doubles as an M7-echo filter
> Date: 2026-08-20 | Session: #14 | Status: Accepted — ⚠️ hardware-critical, ✅ hardware-verified (with one caveat, see Impact)

---

**Context.** Live CAN-FD capture (session #14, `ME_CAN_RX_FRAME_LOG`) showed the
M7 handing every frame **we** transmit back to us on the identical CAN ID a
genuine Secondary reply uses (`RPMSG_PROTOCOL.md` §4 documents `GET_FRAME`
length-80 only as *"Streamed CAN1 RX frame"* — no field marks origin). Of 388
accepted RX frames in one run, 191 (49%) were our own commanded setpoint
(`V=0.0, A=2.0` uniform across all 4 slots, byte 8 = `CMD_CHA`) echoed straight
back; 193 (50%) were genuine, per-channel-distinct feedback. `handle_stream_frame()`
forwarded both indiscriminately to Core Logic — roughly half of every
channel's reported voltage/current was our own echoed command, not real
measured data.

**Decision.** Two buffers per Secondary/block, not one:
- `s_set_shadow[secondary][block]` (existing, ADR-32) — what we **transmit**
  for both SET_VALUES and READ_VALUES.
- `s_read_feedback[secondary][block]` (new) — what we **forward to Core
  Logic**. `handle_stream_frame()` only writes an inbound frame into this
  buffer when its 64 bytes differ from `s_set_shadow[secondary][block]` at
  that instant; `forward_to_core()` is called with `s_read_feedback`, never
  the raw inbound frame.

**Why comparing against `s_set_shadow` is a correct echo filter, not a
heuristic:** `handle_can_tx()` transmits `s_set_shadow`'s own bytes for
*both* SET and READ (ADR-35) — so an echo of either is byte-identical to
`s_set_shadow` at the moment it returns. Genuine feedback carries real
measured voltage/current from the Secondary and essentially never
coincidentally equals our own just-commanded setpoint. A `memcmp` match is
therefore definitionally our own transmission looped back.

**Impact:**
- ✅ 229 host checks still passing; `can_mgr.c` is Linux-only, excluded from
  `build-native.ps1` (same as every prior change to this file).
- ✅ `.\build.ps1` compiles clean (`-Werror`).
- New `s_rx_echo` counter, reported at shutdown alongside `tx`/`rx`/`ack`.
- ✅ **Hardware-verified 2026-08-20**, immediately after deploy: shutdown
  counters read `tx 461 (... coalesced 747), ack ok 461 fail 0, rx 461
  (unmatched 0, echo 0)` — a clean 1:1 tx:rx ratio (previously ~1:2 with the
  49/50 echo split). Cross-checked directly against the raw frame dump, not
  just the counter: all 461 RX frames that run matched the genuine
  per-channel-distinct feedback signature; **zero** matched the old echo
  signature (`V=0.0, A=2.0` uniform). 0 missed responses, 0 channels offline.
- ⚠️ **Caveat, stated plainly:** because no echo occurred in that run, the
  `memcmp` discard branch itself was never exercised — the result proves the
  pipeline is clean end-to-end, not that the filter code path correctly
  rejects an echo when one occurs. Best working theory (unconfirmed): ADR-37
  landed in the same deploy and likely removed the actual trigger (repeated
  same-ID SET_VALUES retransmission) for whatever M7-side behavior produced
  the echo, so there was nothing left for this filter to catch that run. The
  filter stays in as a defensive backstop regardless of which explanation is
  right.
- Root cause of the echo itself is still believed to be M7-side CAN-
  controller self-reception/loopback, not a documented RPMsg protocol
  behavior — worth raising with whoever owns the M7 firmware independent of
  this ADR, especially since it may not recur now that ADR-37 is in.

**Related**
- Extends: ADR-32 (`s_set_shadow`), ADR-35 (READ reads shadow back)
- Depends on: `master_slave_can_v1.0.md`'s documented CAN ID layout
  (`y`=circuit/Secondary number, 6 bits; channel via payload slot, not ID) —
  reconfirmed correct in this session after a brief mid-session doubt
- Relates to: `Ref Docs/RPMSG_PROTOCOL.md` §4 (`GET_FRAME`, no origin field)
