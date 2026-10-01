# Session #12: Secondary 1 multi-channel (1-4) support
> Date: 2026-08-19T15:12+05:30 | Agent: Claude Code (Sonnet 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-18_0000_rpmsg-can-transport-core-isolation-rtt-log.md](2026-08-18_0000_rpmsg-can-transport-core-isolation-rtt-log.md) — real RPMsg CAN transport, CPU3 isolation, RTT logging.

---

## 🎯 Goal
Extend the board from handling exactly one Secondary/Channel to all 4
channels of Secondary 1, per developer request: independent registration,
control commands, program data and CAN-FD traffic per channel.

## ✅ Done
- **Investigated first, before designing anything.** Found the "single
  secondary/single channel" limitation was concentrated almost entirely in
  the communication layer — `circuit_registry`, `circuit_store`,
  `core_logic.c`'s `service_engines()` (already ticks all 64 slots), and
  `step_engine.c` (already pure per-instance) needed no changes at all.
- **Design + plan, developer-approved section by section:**
  `Docs/specs/2026-08-19-multi-channel-secondary1-design.md`,
  `Docs/specs/2026-08-19-multi-channel-secondary1-plan.md` (6 tasks).
- **`--channels` CLI + independent per-channel registration** (T-61, ADR-32)
  — new pure/host-tested `util/channel_list.[ch]`; `me_config_t.channel`
  scalar → `channels[]`/`channel_count`; `do_registration()` takes an
  explicit `circuit_id`; `comm_thread_main()` loops registration once per
  configured channel over the single TCP connection, independently (no
  channel gates another).
- **CAN-FD SET_VALUES coalescing** (T-61, ADR-32,
  ⚠️ developer-approved hardware-critical change) — resolves the item ADR-28
  flagged as "revisit before channel 2 is wired": `can_mgr.c` now merges
  each channel's SET frame into a per-Secondary/per-block shadow buffer via
  a new pure `me_can_merge_slot()` (`can_frame.c`), instead of transmitting
  a frame that zero-fills (and so silently CMD_STO's) the other channels
  sharing its block.
- Both build scripts (`build.ps1`, `build-native.ps1`) updated with the new
  source/test files.

## 🔬 Verification
Host suite 220→**229** checks, 0 failed (7 for `channel_list_parse`, 2 for
`me_can_merge_slot`). Cross-build clean (`-Werror`, static ELF64 AArch64).
**Not yet hardware-verified** — see T-62 for the exact checklist (register
`--channels 1,2,3,4`, stagger-start 4 programs, confirm one channel's
activity never resets another's setpoint, stop one channel mid-run and
confirm only its slot goes to STO).

## 🔄 In Progress
Nothing — the design/implementation/review/merge cycle for this session
completed in full (PR #7 merged to `main`). Hardware verification (T-62) is
the developer's next step, not an in-progress item on this session.

## 🚫 Blocked
Nothing. Only T-62's hardware run remains, and only the developer can do it.

## 🔜 Next Agent Should Do
1. **T-62 — hardware-verify this on `172.16.18.167`.** Nothing about the
   CAN-FD merge is proven against a real Secondary yet; only the developer
   can run `deploy.ps1`.
2. T-60 (isolcpus provisioning) and T-24 (full battery-test flow end to end)
   both still stand from session #11.
3. `ARCHITECTURE.md` remains stale (flagged since #11) and was not touched
   this session either.
