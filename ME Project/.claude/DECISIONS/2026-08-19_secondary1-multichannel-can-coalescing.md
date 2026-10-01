# ADR-32: Secondary 1 registers all 4 channels per connection; CAN-FD SET_VALUES coalesced per block
> Date: 2026-08-19 | Session: #12 | Status: Accepted — ⚠️ hardware-critical CAN-FD change, developer-approved; not yet hardware-verified
> File: `DECISIONS/2026-08-19_secondary1-multichannel-can-coalescing.md`

---

**Decision:** Two changes, delivered together because the second only matters
once the first is exercised.

1. `--channel <n>` → `--channels <list>` (e.g. `1,2,3,4`), parsed by a new
   pure/host-tested `util/channel_list.[ch]`. `me_config_t` now carries
   `channels[ME_MAX_CHANNELS]` + `channel_count` instead of one scalar.
   `comm_thread.c`'s `do_registration()` takes an explicit `circuit_id`
   parameter and `comm_thread_main()` loops it once per configured channel
   over the single TCP connection — independently: one channel's rejection
   does not gate or block another's attempt. `idle_loop()` is entered once
   **any** configured channel registers, not only if all do.
2. `can_mgr.c` gained a per-Secondary, per-block shadow buffer
   (`s_set_shadow[secondary][block][ME_CAN_FRAME_LEN]`). `handle_can_tx()`
   merges each SET-function frame's own 16-byte slot into that buffer (via
   the new pure `me_can_merge_slot()` in `can_frame.c`) and transmits the
   merged buffer instead of the frame as received.

**Reason:** Investigation (2026-08-19) found the codebase was already
multi-circuit-capable everywhere except the communication layer —
`circuit_registry`, `circuit_store`, `core_logic.c`'s `service_engines()`
(already ticks all 64 slots), and `step_engine.c` (already pure per-instance)
needed no changes at all. The one real gap besides registration:
`me_can_pack_set()` zero-fills the other three channels' slots in a 64-byte
block, which ADR-28 already flagged as "safe only while channel 1 is the
sole channel — revisit before channel 2 is wired." With channels 1-4 all
live and independently timed, a zero-filled slot carries `CMD_STO` to
whichever channel owns it — Channel 2's frame would silently stop Channel 1
the instant it transmitted. This is **not** a rare race: it happens on
essentially every tick where more than one channel is active. **ADR-28's
deferred item is now resolved by this ADR** — the shadow buffer merges
every SET frame against the block's last-known state instead of
transmitting a partially zero-filled frame.

**Why this is safe against stale state:** `me_exec_force_stop()`
(`step_engine.c`) unconditionally sends an explicit `CMD_STO` setpoint on
every path that stops a channel (Stop command, program completion, decode
error, channel-offline) before marking its context stopped. A channel that
goes idle therefore always leaves its own shadow slot holding an explicit,
correct stop — never a stale non-zero value that keeps getting re-asserted
by another channel's activity.

**Why the merge lives in `can_frame.c`, not `can_mgr.c`:** `can_frame.c` is
pure logic (no sockets, no platform headers), so `me_can_merge_slot()` is
host-unit-tested via `build-native.ps1` — proven correct on the laptop
before it ever reaches the CAN bus, which matters given the safety stakes.
`can_mgr.c` only owns the shadow-buffer storage and the call site; it is a
single thread draining one queue, so no locking is needed.

**Impact:**
- ✅ 229 host checks passing (was 220): 7 for `channel_list_parse`, 2 for
  `me_can_merge_slot`.
- ✅ Both builds clean (`-Werror`); no changes needed in `core_logic.c`,
  `step_engine.c`, `circuit_store.c`, or `frame_router.c` — confirms the
  investigation's finding that those layers were already general.
- ⚠️ **Not yet hardware-verified.** Design doc:
  `Docs/specs/2026-08-19-multi-channel-secondary1-design.md`. Plan:
  `Docs/specs/2026-08-19-multi-channel-secondary1-plan.md`. Hardware
  checklist (register all 4, stagger-start 4 programs, confirm one channel's
  activity never resets another's setpoint, stop one channel mid-run and
  confirm only its slot goes to STO) is Task 5 of the plan — see T-62.
- ℹ️ Explicitly out of scope: the pre-existing Block 1/Block 2 RX ambiguity
  in `handle_stream_frame()` (channels 5-8 sharing a CAN ID with 1-4) is
  untouched — all four of Secondary 1's channels live in Block 1 only.
  Also deferred: each channel still sends its own redundant READ/poll
  request rather than one per block (bus-bandwidth inefficiency, not a
  correctness issue — RX demux already forwards one reply to every active
  channel).

**Related**
- Supersedes: none (resolves ADR-28's deferred item 1)
- Relates to: T-61, T-62, ADR-28, ADR-17
