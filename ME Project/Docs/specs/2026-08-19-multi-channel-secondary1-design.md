# Multi-Channel Support for Secondary 1 (Channels 1-4) — Design

Status: approved by developer (2026-08-19), ready for implementation planning.

## 1. Problem

The board today registers, controls, programs, and CAN-FD-communicates with
exactly one circuit: Secondary 1 / Channel 1 (CircuitID `0x11`), configured
once at startup via `--secondary`/`--channel` CLI flags. Real hardware now has
Secondary 1 wired with 4 physical channels (CircuitIDs `0x11`/`0x12`/`0x13`/
`0x14`). The board must:

- Register all 4 channels of Secondary 1 with the Web Application.
- Accept, store, and run 4 independent battery-testing programs, one per
  channel, sent by the Web Application at arbitrary times.
- Accept and execute Start/Stop/Pause/Continue for each channel independently,
  at arbitrary times, with no ordering dependency between channels.
- Do all of the above without one channel's CAN-FD traffic corrupting
  another's, given that Channels 1-4 physically share one CAN-FD "block"
  frame on the wire.

Scope for this task: Secondary 1, channels 1-4. The underlying data
structures are generalized to the existing capacity constants
(`ME_MAX_SECONDARIES = 8`, `ME_MAX_CHANNELS = 8`), so adding channels 5-8 or a
second Secondary later requires no further rearchitecture — but only
Secondary 1 / channels 1-4 are exercised and verified in this task.

## 2. What already works (no changes)

Investigation (2026-08-19) found the "single secondary/single channel"
limitation is concentrated almost entirely in the communication/registration
layer. Everything downstream of the message queues already operates on the
full 64-circuit-slot model:

| Area | File(s) | Status |
|---|---|---|
| CircuitID nibble split | `proto/proto_defs.h:333-347` (`ME_CIRCUIT_ID`/`ME_CIRCUIT_SECONDARY`/`ME_CIRCUIT_CHANNEL`) | Already general |
| Capacity constants | `proto/proto_defs.h:227-234` (8 secondaries × 8 channels = 64) | Already general |
| Circuit registry | `store/circuit_registry.c` (`g_registered[64]`) | Already a 64-slot array; only ever populated for 1 slot today because registration is single-instance (see §3) |
| Program/step storage | `store/circuit_store.c` (`g_program[64][...]`), `threads/core_logic.c:46` (`g_cl_program[64][...]`) | Already keyed generically by circuit_id |
| Control command dispatch | `threads/core_logic.c:205-287` (`handle_control`), `147-158` (`service_engines`, ticks all 64 slots every iteration) | Already fully general |
| Step execution logic | `exec/step_engine.c`/`.h` | Pure per-instance logic (`me_exec_ctx_t *ctx`), zero global state |
| CAN-FD RX demux | `threads/can_mgr.c:202-274` (`handle_stream_frame`, loops channels 1-8) | Already forwards inbound feedback to every active channel correctly |

Four independently-timed programs and four independently-timed Start
commands for Secondary 1's channels already route and execute correctly
today, once those channels are registered.

## 3. Change 1 — Registration & CLI (communication layer)

**Current state:** `me_config_t` holds a single scalar `channel`
(`sys_init.h:25`); `me_system_t.reg_request` is one `me_reg_request_t` built
once at startup (`sys_init.h:48`); `do_registration()`
(`threads/comm_thread.c:139-259`) sends one 0xDD frame over the single TCP
connection and blocks for its response; `s_state`/`s_registered`
(`comm_thread.c:64,68`) are single flags driving one registration per
connection.

**Design:**

- `me_config_t.channel` (scalar) → `channels[ME_MAX_CHANNELS]` +
  `channel_count`, populated by a new `--channels 1,2,3,4` CLI flag replacing
  `--channel`. Parsed by a small pure helper function (host-testable, no
  socket/platform dependency — same separation already used for
  `reg_frame.c`/`can_frame.c`), validating each entry is 1..`ME_MAX_CHANNELS`
  and rejecting out-of-range values outright (matching the existing
  truncation-rejection discipline for `--secondary`/`--channel` in
  `main.c:146-163`). Default is `{1}` / count 1, so existing single-channel
  invocations and deploy scripts keep working unchanged.
- `me_reg_request_t` remains a single template: `device_id`, `device_name`,
  `ip`, `mac` are shared across all 4 channels (confirmed: one device_id per
  board, not one per channel). Only `circuit_id` varies per registration
  attempt.
- `do_registration()` is refactored to take an explicit `circuit_id`
  parameter (instead of reading `sys->reg_request.circuit_id` directly).
  `comm_thread_main()` loops over `cfg.channels[0..channel_count)`, building
  `ME_CIRCUIT_ID(cfg.secondary, cfg.channels[i])` for each and sending one
  0xDD request at a time over the single TCP connection — the wire is one
  serial stream, so attempts are serialized on the wire, but **no channel's
  success or failure gates another's**: each channel is attempted and its
  outcome recorded independently (a Channel 2 rejection does not stop Channel
  3 from being attempted, and does not prevent Channel 1 from running).
- Entering `idle_loop()` now requires **at least one** channel to have
  registered successfully this connection attempt (all-4-fail still triggers
  the existing whole-connection retry-with-backoff). `s_registered`
  (`comm_thread.c:64`) becomes "was any channel ever registered on this
  process" for the existing exit-status check in `main.c:251`.
- `me_registry_mark_registered()` (`circuit_registry.c`) and
  `me_post_reg_send()` (`threads/post_reg.c:21`) are called once per
  successfully-registered channel — both already take a `circuit_id`
  argument; no changes needed to either.
- On TCP reconnect, all configured channels are re-registered in the same
  loop (server answers `0x02` "Already Registered" for previously-admitted
  ones — same idempotent behavior as today, just repeated per channel).

## 4. Change 2 — CAN-FD SET_VALUES coalescing (hardware-critical)

`⚠ HUMAN REVIEW REQUIRED — hardware-critical change.` Approved by developer
2026-08-19 at design level; implementation still requires hardware-in-the-loop
verification per project rule before being trusted (see §5).

**The bug this closes:** `me_can_pack_set()`
(`proto/can_frame.c`/`.h:79-87`) builds a full 64-byte CAN-FD frame with only
the addressed channel's 16-byte slot filled; the other three slots in that
block are zero-filled. This is documented as safe only "while only channel 1
runs" (`can_frame.h:84`, "Revisit before a second channel is wired up").
Channels 1-4 all share **Block 1** (`me_can_block_for_channel`,
`can_frame.c:6-10`). `handle_can_tx()` (`threads/can_mgr.c:94-166`) writes
whatever 64-byte frame it is handed straight to RPMsg with no merging. Once
channels 1-4 are all live: if Channel 1 is mid-charge and Channel 2's
step_engine independently ticks and sends its own setpoint frame, that
frame's zero-filled slot for Channel 1 carries `CMD_STO = 0x00` — silently
commanding Channel 1 to stop the instant Channel 2's frame reaches the bus.
With four independently-timed channels sharing one block, this is not a rare
race — it happens on essentially every tick where more than one channel is
active.

**The fix:** a per-secondary, per-block shadow buffer in `can_mgr.c` — sized
`[ME_MAX_SECONDARIES + 1][2][ME_CAN_FRAME_LEN]` (2 = Block 1 / Block 2, so
this doesn't need to change again if channels 5-8 or Secondary 2 arrive
later; only Block 1 / Secondary 1 is exercised in this task). In
`handle_can_tx()`, for a **SET-function frame only** (`function5` decoded
from `m->offset & 0x1F`, already available with no protocol change — READ/
poll frames pass through unchanged, since they are queries, not commands, and
carry no stomping risk): extract the incoming frame's 16-byte slot for the
message's own channel, merge it into the shadow buffer at that slot (leaving
the other channels' last-known slots untouched via `me_can_block_for_channel`
for the slot index), and transmit the **whole shadow buffer**, not the
incoming frame, as the CAN-FD payload.

**Why this is safe against stale state:** `me_exec_force_stop()`
(`exec/step_engine.c:98-104`) unconditionally sends an explicit
`ME_CAN_CMD_STO` setpoint on every path that stops a channel (Stop command,
program completion, decode error, offline) before marking the context
stopped. A channel that goes idle therefore always leaves the shadow buffer
holding an explicit, correct stop for its own slot — never a value that could
be mistaken for "leave unchanged" or silently re-assert a stale charge
command.

**Where the merge logic lives:** the merge itself
(`me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN], const uint8_t
incoming64[ME_CAN_FRAME_LEN], uint8_t channel_num)`-shaped function) is added
to `can_frame.c`/`.h`, which is pure logic with no sockets or platform
headers — the same separation the file already follows. This makes it fully
host-unit-testable via `build-native.ps1`, which matters given the safety
stakes. `can_mgr.c` only owns the shadow-buffer storage and the thread
plumbing calling into it; `can_mgr.c` is a single thread draining one queue
(`drain_queue()`, `can_mgr.c:168-183`), so no locking is required — consistent
with the file's existing style.

**Explicitly out of scope:** the pre-existing Block-1/Block-2 RX ambiguity
already flagged in `handle_stream_frame()` (`can_mgr.c:249-257`, channels 5-8
sharing a CAN ID with 1-4) is not touched by this change — all four of
Secondary 1's channels live in Block 1 only, so that gap is not triggered
here.

**Explicitly deferred (not a correctness issue):** each channel's step_engine
still independently issues its own READ/poll request
(`exec/step_engine.c:75-80`, `send_poll`), so Secondary 1 receives up to 4x
redundant read-request frames per poll interval where 1 would return the
whole block's feedback. This is a bus-bandwidth inefficiency, not a
correctness bug (RX demux already forwards one reply to every active
channel, `can_mgr.c:268-274`), and is left for a future optimization pass.

## 5. Testing plan

| Layer | Command | Covers |
|---|---|---|
| Host protocol tests | `.\build-native.ps1` | New `--channels` list parser (valid lists, out-of-range entries, duplicates, malformed input). New `me_can_merge_slot()`: merging Channel 2's slot leaves Channels 1/3/4 untouched; merging an explicit STO into a slot correctly overwrites a stale "charging" slot; block selection (channels 1-4 → Block 1) unaffected. |
| Cross-build | `.\build.ps1` | Compiles clean under `-Werror`, static aarch64 ELF. Proves it builds, not that it works. |
| Hardware-in-the-loop | `.\deploy.ps1` on 172.16.18.167 (developer-run) | The only real proof. Register Secondary 1 with `--channels 1,2,3,4`; confirm each channel's admission is independent (a failure on one channel does not block the others); send 4 different battery programs to the 4 channels; start them at staggered times; confirm via CAN-FD traffic / RTT logs that Channel 1's setpoint is never clobbered by Channel 2/3/4 activity. This is exactly the scenario §4's fix exists for — it is the load-bearing test, not a formality. |

Per project convention: a passing host build is never reported as "tested" —
only `deploy.ps1` against real hardware proves the system works end to end.
This suggestion requires hardware-in-the-loop testing to verify.

## 6. Decisions confirmed with developer (2026-08-19)

1. CircuitID encoding: fixed high/low nibble split, matches existing macros.
2. Registration ordering: no dependency between channels; any channel may
   register first; failures are independent, not gating.
3. Wire protocol: control/program frames already carry circuit_id per frame
   (confirmed, no protocol change needed there).
4. Generality: build for up to 8 channels/secondary using existing capacity
   constants, even though only Secondary 1 / channels 1-4 are exercised now.
5. Hardware: real 4-channel Secondary 1 hardware is available for
   verification (board 172.16.18.167).
6. Device ID: one device_id shared by all 4 channels; circuit_id alone
   disambiguates channel.
7. CLI shape: `--channels 1,2,3,4` (explicit list), replacing `--channel`.
