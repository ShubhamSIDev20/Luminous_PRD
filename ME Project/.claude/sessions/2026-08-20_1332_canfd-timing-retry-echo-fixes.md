# Session #15 — 2026-08-20 13:32–16:45 IST — CAN-FD real-time execution fixes: timing drift, SET repeat, one-frame-per-block, M7 echo
> Agent: Claude Code (Sonnet 5) | Status: ✅ Complete, hardware-verified

---

## Goal
Continuation of session #14's board deploy. Developer ran the 4-channel
battery-test program on real CAN-FD hardware (M7 link now live via
`--device /dev/ttyRPMSG30`, first time ever exercised in a container) and
reported: execution not real-time (some channels fast, some slow), CAN
frames "dropped", and later, once logs were reviewed together: SET_VALUES
sent repeatedly, and multiple different current/voltage values for what
should be one read query.

## What Happened

**Bug 1 — step timing drift (ADR-36).** `step_engine.c`'s `advance_time()`
skipped accumulating `step_run_ms`/`program_run_ms` whenever a CAN response
was outstanding. Every missed response cost a channel a full
`ME_EXEC_RESPONSE_TIMEOUT_MS` (200 ms) of step time. Two channels that
missed 184 responses each lost ~37s of step time; two channels with zero
misses lost none — same program, different progress. Contradicts the design
doc verbatim (`step_run_ms`: "frozen unless RUNNING"; TIME cutoff "needs
only the local clock, not CAN feedback"). Fixed: accumulate unconditionally;
`me_exec_on_response()`/`me_exec_on_response_timeout()` no longer touch
`last_tick_ms`.

**Investigation — "CAN frames dropped".** Turned out to be a missing
`--device /dev/ttyRPMSG30` mapping (the char device existed on the host but
was never passed into the container) — `can_mgr` was running in degraded
"count and drop" mode with the M7 link down. Fixed on-device and in the
repo's `me-primary/docker-compose.yml` (`devices:` entry added). Once
mapped, CAN was live for the first time this project has run it in a
container: 936 genuine RTTs, median 3.15ms, 0 drops — "dropped" was a
red herring once the real link was up.

**Bug 2 — CAN ID addressing, briefly doubted, reconfirmed correct.**
Developer proposed encoding per-channel addresses into the CAN ID's 6-bit
circuit field. Re-read `Ref Docs/master_slave_can_v1.0.md` together: `y` =
Secondary/circuit number (64 circuits), channels addressed by payload slot
(64 circuits × 4 channels = 256, the doc's own stated arithmetic — only
works if circuit = module, not channel). Developer agreed: no change needed;
existing `me_can_id(secondary, function)` was already correct.

**Bug 3 — "we have to send only one CANFD frame with all 4 channel data"
(ADR-35).** `handle_can_tx()` only routed SET_VALUES through the per-block
shadow buffer (ADR-32); READ/poll frames transmitted `me_can_pack_read()`'s
near-empty buffer as-is — reintroducing the zero-fill danger ADR-32 fixed,
just via the READ path, on every 100ms poll per channel. Fixed: READ now
transmits `s_set_shadow` too. Added a 50ms per-block TX rate limit
(`ME_CAN_TX_COALESCE_US`) so up to 4 independently-timed channels collapse
to one physical frame per block — verified 1874 TX requests → 470 physical
frames in one run.

**Bug 4 — "SET CAN command is getting sent multiple times... it should only
happen after decoding the new program step" (ADR-37).**
`me_exec_on_response_timeout()`'s retry branch re-sent `SET_VALUES` (not
`READ_VALUES`) when a SET's own response timed out — violating
`master_slave_can_v1.0.md`'s explicit rule that the Secondary latches a
setpoint on receipt and "the Master must not re-issue a setpoint merely to
obtain feedback." Fixed: always retry with `send_poll()`. Updated
`tests/test_step_engine.c` (it asserted the old, wrong behavior).

**Bug 5 — "multiple current and voltage values for the same read query"
(ADR-38).** Added `ME_CAN_RX_FRAME_LOG` (unconditional RX dump, before any
validation/rejection) at the developer's request, replacing the RTT log
line. Found: the M7 hands our own transmitted frames back to us on the
identical CAN ID a genuine reply uses (`RPMSG_PROTOCOL.md` documents
`GET_FRAME` length-80 only as "Streamed CAN1 RX frame" — no origin field).
388 RX frames in one run split 191/193 between our own echoed setpoint and
genuine per-channel feedback. Fixed, per developer's explicit design
("separate SET buffer / separate READ buffer, share the received buffer
with Core Logic"): new `s_read_feedback[secondary][block]`, written only
when inbound bytes differ from `s_set_shadow` (a match is definitionally our
own echo, since READ now transmits shadow's bytes too, per ADR-35).
`forward_to_core()` now takes `s_read_feedback`, never the raw inbound
frame.

**Verification of bugs 3-5 together:** re-ran the program after all three
landed. Shutdown counters: `tx 461 (coalesced 747), rx 461 (unmatched 0,
echo 0)` — clean 1:1 tx:rx, 0 missed responses, 0 offline. Cross-checked the
raw dump directly (not just the counter): 455/461 frames matched the
genuine feedback signature, 0 matched the echo signature — the echo did not
recur this run. Caveat recorded in ADR-38: the discard branch itself was
never exercised since no echo occurred, so the filter's correctness under an
actual echo remains unconfirmed, though the logic is straightforward
(`memcmp` against a buffer we control the writes to).

**Workflow change (developer instruction):** going forward, deploy via
`/home/torizon/ME-Primary/me_primary_new` (bind-mounted over the compose
service's `/me_primary`) and `docker compose up -d` from that directory —
not ad-hoc `docker run` / a separate `me_primary_test` container. Adopted
for the rest of this session.

## Outcome
- ADR-35, ADR-36, ADR-37, ADR-38 written (ADR-34 was session #14).
- `docker-compose.yml` (repo) gained the `/dev/ttyRPMSG30` device mapping.
- All builds clean (`-Werror` both targets), 229/229 host checks (one test
  updated for ADR-37's behavior change).
- Board `172.16.18.167`, container `me-primary`: RPMsg link open, all 4
  channels registered, hardware-verified clean run as described above.
- Still open: whether the M7-side echo can recur under different traffic
  patterns (untested), and whether the CCChg voltage-always-0.0 question
  (raised, not yet resolved) needs a decode-layer change.

## Related
- Extends: ADR-25, ADR-28, ADR-32
- Relates to: T-64 (none of this session's fixes are in a published GHCR
  image yet either)
