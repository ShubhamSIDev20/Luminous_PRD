# Session #11: Real RPMsg CAN transport, CPU3 isolation, RTT latency log
> Date: 2026-08-18T00:00+05:30 (compact catch-up entry covering 2026-08-18/19; exact start time not recorded) | Agent: Claude Code (Sonnet 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-14_0000_step-execution-git-adoption.md](2026-08-14_0000_step-execution-git-adoption.md) — real step execution, git adopted.
> Compact catch-up entry per developer request — covers several short
> sessions logged together rather than one each.

---

## 🎯 Goal
Catch-up: real RPMsg CAN transport, CPU3 core isolation + CI/CD, hardware
verification, CAN-FD RTT latency logging.

## ✅ Done
- **Real RPMsg CAN transport** (T-57, ADR-29) — `can_mgr.c` now talks to the
  M7 over `/dev/ttyRPMSG30` instead of fabricating responses. New:
  `platform/rpmsg_link.c` (char-device I/O), `proto/rpmsg_frame.c`
  (wrap/parse), stream reassembler (resync + overflow tested), `g_q_can`
  made pollable. Spec: `Docs/specs/2026-08-18-rpmsg-can-transport-design.md`.
- **CPU3 core isolation + production CI/CD** (T-58, ADR-30,
  ⚠️ developer-approved hardware-critical change) — `--core` CLI flag
  (default 3) pins the Core Logic thread via `pthread_attr_setaffinity_np()`
  with an isolation self-check log; one-time board provisioning runbook
  (`fw_setenv isolcpus=3 ...`); production `Dockerfile` (scratch, ARM64) +
  `docker-compose.yml` (`cpuset: 0-3`); manual `workflow_dispatch`-only
  GitHub Actions release → GHCR, hardened (branch guard, semver check, safe
  `:latest`). Spec: `Docs/specs/2026-08-19-core-isolation-cicd-design.md`.
  PR #6 merged to `main`.
- **Hardware-verified today** (board `172.16.18.167`, server `172.16.18.164`,
  via `deploy.bat` → `qflex-backend`): CPU3 pin succeeds
  (`running on CPU3`) even through the legacy deploy path. Board's
  `isolcpus` kernel provisioning is **not yet applied**
  (`NOT isolated` warning) — runbook still needs a run on this board (T-60).
- **CAN-FD RPMsg round-trip latency log** (T-59, ADR-31) — `can_mgr.c`:
  per-Secondary TX timestamp (`s_tx_us[]`) + `now_us()` (`CLOCK_MONOTONIC`),
  logged on each validated reply as `can: S%u RTT %u us`. Committed
  `2afcfcc`, pushed to `develop`.

## 🔬 Verification
Cross-build clean (`-Werror`, static ELF64 AArch64). Host suite 189→**220**
checks, 0 failed. Hardware: CPU3 pinning + registration + real RPMsg CAN
link confirmed live. RTT numbers from the new log **not yet captured live**
— feature is compiled-clean only, not hardware-tested itself.

## 🔄 In Progress
Nothing — task list below is what remains.

## 🚫 Blocked
Nothing. Only the remaining hardware steps below, and only the developer can run them.

## 🔜 Next Agent Should Do
1. Run the isolcpus provisioning runbook (T-60) on `172.16.18.167` for true
   kernel-level isolation — the pin works today but isn't yet exclusive.
2. Capture live `can: S%u RTT %u us` numbers on hardware.
3. T-24 still stands for the full battery-test flow against a real Secondary
   over the new RPMsg link — nothing beyond the above is hardware-verified
   end to end yet.
