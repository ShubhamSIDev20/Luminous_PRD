# ADR-34: `idle_loop()` periodically retries registration for a circuit still awaiting approval
> Date: 2026-08-20 | Session: #14 | Status: Accepted — ⚠️ hardware-critical, hardware-verified 2026-08-20

---

> **Verification.** Against the real Web Application at `172.16.18.164:9999`,
> from board `172.16.18.167`: registering `--secondary 1 --channels 5,6,7,8`
> (deliberately unseen circuits) got Value `0x00` (Failed) on all four. The
> connection-level reconnect loop kept re-registering all four every attempt
> until circuit `0x18` was approved mid-loop (Value `0x01`) and the board
> entered `IDLE` with `0x15`/`0x16`/`0x17` still unregistered. **10 seconds
> later**, `retry_pending_registrations()` fired, re-sent registration for
> exactly those three circuits (not `0x18`), and all three succeeded once
> approved. Full log kept in `sessions/2026-08-20_1122_periodic-registration-retry.md`.

**Decision:** `idle_loop()` (`comm_thread.c`) now re-sends the `0xDD`
registration frame for any configured circuit that is **not yet** admitted
(`me_registry_is_registered(circuit_id) == false`), every `ME_REG_RETRY_MS`
(10000 ms), for as long as the connection stays up. A circuit that has
already succeeded (`me_registry_mark_registered()` ran for it) is never
touched again — the retry is gated per-circuit on the registry, not a
blanket "re-register everyone" timer.

**Reason:** Developer clarified the actual product requirement: a device/
circuit registering for the first time defaults to **unapproved** in the Web
Application — an operator must manually approve it. The board is expected to
keep sending its registration request periodically until that approval
happens and the server starts answering success. This was not happening: once
*any* configured channel on a connection succeeded, `comm_thread_main()`
entered `idle_loop(sys, fd)` and held that connection indefinitely (ADR-32).
`idle_loop()` itself never re-sent registration for anything (that rule
dates to ADR-10) — so a circuit that failed in the same registration pass a
sibling circuit succeeded in was stuck unregistered until a full TCP
reconnect, which does not happen on a healthy connection.

**Why this does not reopen ADR-10:** ADR-10 fixed a different bug — the board
once treated Value `0x02` (**success**, "already registered") as a
*rejection* and reconnected/re-registered a **working** circuit every 30s
forever. This ADR's retry is the opposite shape: it only ever touches a
circuit `me_registry_is_registered()` reports **false** for. A circuit that
reads `0x01`/`0x02` is marked registered immediately (existing code,
untouched) and is permanently excluded from every future retry pass on this
connection — there is no path back to ADR-10's bug.

**Design:**
- `now_ms()` (new, `comm_thread.c`) — `clock_gettime(CLOCK_MONOTONIC, ...)`,
  same pattern as `can_mgr.c`'s existing `now_us()`.
- `s_last_reg_attempt_ms[ME_MAX_CHANNELS]` (new, file-scope) — stamped after
  every `do_registration()` call, both the initial per-connection pass in
  `comm_thread_main()` and the new retry in `idle_loop()`.
- `retry_pending_registrations()` (new) — one pass over the configured
  channels each time `idle_loop()`'s existing `poll(..., 500)` wakes
  (real traffic or the 500 ms timeout alike); no-ops when nothing is due.
- No retry cap — matches "device must send periodically till it gets
  registered." Retries forever until approved or the process stops.

**Impact:**
- ✅ 229 host checks still passing — `comm_thread.c` is Linux-only
  (sockets/threads), excluded from `build-native.ps1` same as before this
  change; no new host-testable surface was added or lost.
- ✅ `.\build.ps1` compiles clean (`-Werror`).
- ✅ Hardware-verified 2026-08-20 (see Verification above) — including the
  specific "one channel already up, others still pending" case ADR-32
  introduced (multi-channel per connection) and this ADR was written for.
- ⚠️ Known, accepted trade-off carried forward unchanged: `do_registration()`
  blocks synchronously on `recv()` up to `response_timeout_ms` (default
  5000 ms). A retry attempt briefly pauses `idle_loop()`'s poll of other
  traffic — the same blocking behavior the original registration pass
  already had, just recurring every ≥10s while a circuit stays pending.
- Not yet in a published GHCR image — this fix lives in `develop`/`main`
  source only until a release is cut. Verified on hardware via a
  side-loaded binary (`docker run -v ...:/me_primary:ro debian:bookworm-slim`),
  not through the production `docker-compose.yml` stack, to avoid needing a
  new GHCR version just to test.

**Related**
- Extends: ADR-10 (does not reverse it — see "Why this does not reopen
  ADR-10" above)
- Depends on: ADR-32 (multi-channel per connection; this is what makes
  "one channel already up, others still pending" a reachable state)
- Relates to: `.claude/store/circuit_registry.h`'s
  `me_registry_is_registered()`, which is the sole gate making this safe
