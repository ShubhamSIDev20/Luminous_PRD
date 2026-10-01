# Session #14 — 2026-08-20 11:22–12:00 IST — Periodic registration retry for pending circuits
> Agent: Claude Code (Sonnet 5) | Status: ✅ Complete, hardware-verified

---

## Goal
Deploy `me-primary:latest` (v0.0.2) to board `172.16.18.167` via SSH/`docker
compose` and confirm it registers against the Web Application at
`172.16.18.164`. During that check, the developer identified a real gap:
a circuit awaiting manual web-app approval (server Value `0x00`) is never
retried once a sibling channel on the same connection succeeds — fix it.

## What Happened

**Part 1 — deploy via `/ssh-terminal` skill.**
- Connected `torizon@172.16.18.167` (password auth via `plink.exe`/`pscp.exe`
  — no `sshpass` on this machine, PuTTY was the available non-interactive
  password-auth path).
- Found `/home/torizon/ME-Primary/docker-compose.yml` used `"--channel"`
  (singular) — the binary (v0.0.2, built after PR #10's `channel_list.c` fix)
  only accepts `--channels` (plural, list). Container was crash-looping.
  Fixed the compose file directly on-device.
- Brought the stack up: secondary 1 channel 1 (circuit `0x11`) got Value
  `0x02` (Already Registered); channels 2/3/4 (`0x12`/`0x13`/`0x14`) got
  Value `0x00` (Failed — new circuits, awaiting operator approval).
  Connection then sat in `IDLE` forever with channels 2-4 never retried.

**Part 2 — bug report + fix (ADR-34).**
Developer clarified the requirement: a first-time circuit defaults to
unapproved in the Web Application; the board must keep re-sending its
registration request until an operator approves it. That periodic retry
did not exist — `idle_loop()` never re-sent registration for anything
(ADR-10's rule), and `comm_thread_main()` only retries **all** channels on a
full reconnect, which does not happen once any one channel is up.

Ran `superpowers:brainstorming` (mandatory gate for a behavior change) and
classified it **Bounded** — the flow already exists in
`comm_thread.c`/`circuit_registry.h`. Clarified with the developer:
retry interval **10 seconds**, **no retry cap** (retries forever until
approved or the process stops).

**Implementation** (`me-primary/src/threads/comm_thread.c` only):
- `now_ms()` — new, `clock_gettime(CLOCK_MONOTONIC, ...)`, mirrors
  `can_mgr.c`'s existing `now_us()`.
- `s_last_reg_attempt_ms[ME_MAX_CHANNELS]` — new, stamped after every
  `do_registration()` call (initial pass and retry alike).
- `retry_pending_registrations()` — new; each `idle_loop()` wake
  (poll timeout or real traffic), re-sends registration for any configured
  channel where `me_registry_is_registered()` is false and 10s have
  elapsed since its last attempt. Already-admitted circuits are never
  touched again.
- Updated the `idle_loop()`/header comments that documented the old
  "registration frame is NEVER re-sent from here" rule.

**Verification:**
- `.\build.ps1` — compiles clean (`-Werror`), static aarch64 ELF.
- `.\build-native.ps1` — 229/229 host checks pass, unchanged (`comm_thread.c`
  is Linux-only, excluded from the native build, same as before this change).
- **Hardware**: side-loaded the freshly built binary onto the board
  (`pscp` → `docker run --rm -i --network host -v ...:/me_primary:ro
  debian:bookworm-slim`, alongside the stopped production compose stack, to
  avoid needing a new GHCR release just to test). Registered
  `--secondary 1 --channels 5,6,7,8` (guaranteed-unseen circuits): all four
  got Value `0x00` initially; the connection-level reconnect loop kept
  retrying all four until circuit `0x18` was approved mid-loop and the board
  entered `IDLE` with `0x15`/`0x16`/`0x17` still unregistered. **Exactly
  10 seconds later**, `retry_pending_registrations()` fired, re-sent
  registration for those three circuits only (not `0x18`), and all three
  succeeded once approved. This is precisely the "one channel already up,
  others still pending" case ADR-32 (multi-channel) made reachable and this
  fix targets.
- Restored the production `docker-compose.yml` stack afterward (still on the
  unmodified v0.0.2 GHCR image — this fix is source-only until a new
  version is released, see T-64).

## Outcome
- **ADR-34** written (extends ADR-10, does not reverse it).
- **T-64** opened: cut a new GHCR release (e.g. v0.0.3) including this fix.
- Compose file bug (`--channel` → `--channels`) fixed in **two** places:
  on-device at `/home/torizon/ME-Primary/docker-compose.yml`, and in the
  repo's own `me-primary/docker-compose.yml` template (same typo, checked
  in — never caught because it apparently hasn't been exercised since
  ADR-32's multi-channel work landed). Left the repo template's channel
  value as `"1"` (single channel) — unrelated scope, deployment-specific.

## Related
- Extends: ADR-10, ADR-32
- Relates to: T-63 (still open — GHCR `:latest` is still the debug image),
  T-64 (new)
