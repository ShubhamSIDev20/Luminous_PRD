# SESSION.md — Session Index
> Updated: 2026-08-21T11:15+05:30
> Full detail for any session lives in `sessions/{filename}` — this file only points to it.

---

## 📍 Current Session
**File:** [sessions/2026-08-21_1005_set-tx-guarantee-and-log-tuning.md](sessions/2026-08-21_1005_set-tx-guarantee-and-log-tuning.md)
**Goal:** Diagnosed why ch3/ch4's SET_VALUES had no log evidence of reaching the M7 (ADR-35 coalescing), found RX feedback couldn't be used as proof instead (bench rig sends static canned values), fixed it structurally — SET is now never coalesced (ADR-39) — and tuned diagnostic logging per developer iteration.
**Status:** ✅ Complete, hardware-verified — fixes not yet in a published GHCR image (T-64)

---

## 🕒 Session History
<!-- Newest first. One row per session, ever — added when a session ends (or a new one starts). -->
| # | Date | Goal | File | Agent | Status |
|---|------|------|------|-------|--------|
| 15 | 2026-08-20 | First real CAN-FD hardware run (M7 link live) — timing drift, SET repeat, frame coalescing, M7 echo (ADR-35/36/37/38) | [sessions/2026-08-20_1332_canfd-timing-retry-echo-fixes.md](sessions/2026-08-20_1332_canfd-timing-retry-echo-fixes.md) | Claude Code (Sonnet 5) | ✅ Complete |
| 14 | 2026-08-20 | Deployed v0.0.2 via SSH; periodic registration retry for pending circuits | [sessions/2026-08-20_1122_periodic-registration-retry.md](sessions/2026-08-20_1122_periodic-registration-retry.md) | Claude Code (Sonnet 5) | ✅ Complete |
| 13 | 2026-08-20 | Dockerfile temporarily `scratch` → `debian:bookworm-slim` for on-device debugging | [sessions/2026-08-20_1007_debian-bookworm-slim-debug-image.md](sessions/2026-08-20_1007_debian-bookworm-slim-debug-image.md) | Claude Code (Sonnet 5) | ✅ Complete |
| 12 | 2026-08-19 | Secondary 1 multi-channel (1-4) support — registration, CAN-FD SET_VALUES coalescing | [sessions/2026-08-19_1512_secondary1-multi-channel-support.md](sessions/2026-08-19_1512_secondary1-multi-channel-support.md) | Claude Code (Sonnet 5) | ✅ Complete |
| 11 | 2026-08-18/19 | Real RPMsg CAN transport; CPU3 core isolation + CI/CD; RPMsg RTT latency log | [sessions/2026-08-18_0000_rpmsg-can-transport-core-isolation-rtt-log.md](sessions/2026-08-18_0000_rpmsg-can-transport-core-isolation-rtt-log.md) | Claude Code (Sonnet 5) | ✅ Complete |
| 10 | 2026-08-14/15 | Real program-step execution (SET/CCChg/STOP); Git adopted | [sessions/2026-08-14_0000_step-execution-git-adoption.md](sessions/2026-08-14_0000_step-execution-git-adoption.md) | Claude Code (Sonnet 5) | ✅ Complete |
| 9 | 2026-08-12 17:40–21:25 | `bm_config_v6.0.md` transcription; 40-byte battery record; `0xAA` length table; post-reg `0xCC` demo frame | [sessions/2026-08-12_1740_battery-config-bm-config-v6.md](sessions/2026-08-12_1740_battery-config-bm-config-v6.md) | Claude Code (Opus 5) | ✅ Complete |
| 8 | 2026-08-12 14:34–15:25 | Fix `0xEE` Start BAD_CRC (Session ID); CRC as frame-boundary backstop; `0xAA`/`0xEE` acks | [sessions/2026-08-12_1434_ee-session-id-bug-crc-frame-delimiter.md](sessions/2026-08-12_1434_ee-session-id-bug-crc-frame-delimiter.md) | Claude Code (Opus 5) | ✅ Complete |
| 7 | 2026-08-12 12:42–13:55 | Board answers `0xBB` Q1/Q3/Q4, behind the admission gate | [sessions/2026-08-12_1242_bb-handshake-reply.md](sessions/2026-08-12_1242_bb-handshake-reply.md) | Claude Code (Opus 5) | ✅ Complete |
| 6 | 2026-08-12 10:46–11:55 | `comm_thread` relocation; per-circuit registration gate | [sessions/2026-08-12_1046_comm-thread-relocation-registration-gate.md](sessions/2026-08-12_1046_comm-thread-relocation-registration-gate.md) | Claude Code (Opus 5) | ✅ Complete |
| 5 | 2026-08-12 10:26 | Memory sync only — no source touched | [sessions/2026-08-12_1026_memory-sync-only.md](sessions/2026-08-12_1026_memory-sync-only.md) | Claude Code (Opus 5) | ✅ Complete |
| 4 | 2026-08-11 15:20–18:10 | 4-thread BTS base (Comm/Core Logic/Data Mgr/CAN Mgr) + demo emitter | [sessions/2026-08-11_1520_bts-4-thread-base.md](sessions/2026-08-11_1520_bts-4-thread-base.md) | Claude Code (Opus 5) | ✅ Complete |
| 3 | 2026-08-10 11:15–16:25 | CRC byte order → big-endian; response `0x02` treated as success | [sessions/2026-08-10_1115_crc-byte-order-and-idle-fix.md](sessions/2026-08-10_1115_crc-byte-order-and-idle-fix.md) | Claude Code (Opus 5) | ✅ Complete |
| 2 | 2026-08-07 16:15–17:25 | Registration milestone implemented and hardware-verified end to end | [sessions/2026-08-07_1615_registration-milestone.md](sessions/2026-08-07_1615_registration-milestone.md) | Claude Code (Opus 5) | ✅ Complete |

Session #1 (2026-08-06, bring-up/toolchain decisions ADR-1/2/3) predates the
per-session-file convention adopted in this restructuring; its outcomes live
in `DECISIONS/` (ADR-1, ADR-2, ADR-3), not in a `sessions/` file.
