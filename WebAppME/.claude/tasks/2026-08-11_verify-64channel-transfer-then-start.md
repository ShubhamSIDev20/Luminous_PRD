# T-6: Verify device-multiplexing at full 64-channel scale (transfer-all-then-start-all)
> Created: 2026-08-11 | Session: #3 | Status: ✅ Done

## Goal
Prior testing (Session #2 / plan Task 5) only exercised Start on 4 of 64 channels that already had a program pre-assigned. User asked: "dump all 64 program and start all and then check how its working... frist you need to transfer all program then you can start all" — i.e. verify the full workflow (bulk Transfer to all 64 channels, THEN bulk Start all 64) works correctly at full scale, not just Start alone on a small pre-configured subset.

## What was done
- Reconfigured `HardwareSimulator/config.json` to `device_count: 1, channels_per_device: 64` (backed up original 10/4 config to `config.json.bak` — **not yet restored, see Follow-ups**).
- Ran simulator (`nohup python simulator.py -i 500 > sim_output.log 2>&1 &`).
- Automated the Dashboard UI via browser tooling: selected all 64 channel cards (virtualized grid — required in-viewport-filtered click loop + incremental scroll to reach 100% coverage), opened the bulk context menu, ran **Transfer** (Program: Test, Battery: Li-ion, DBC Port 1: whiteKnightBMSUpload) across all 64, then ran bulk **Start** across the same 64-selection.

## Result
- Bulk Transfer: **64 succeeded, 0 failed** (verified via the results dialog's per-circuit sub-step breakdown — IsReady, Program Transfer, Battery Transfer, DBC Transfer all green for all 64).
- Bulk Start: all 64 cards turned to "Charging"; **all 64 session IDs verified unique** (zero collisions) via DOM scrape.
- Simulator log: unique `addressByte` per board/channel confirmed on START commands (e.g. `addr=0x85` for board=8 channel=5); zero `Errors` across 4 consecutive 30s `[STATS]` samples over ~2 min, steady packet/command throughput, no retransmits.
- Browser console: zero errors.
- **User then independently re-ran/observed the same workflow themselves and confirmed:** *"i have tested all that is working as expected"* (session ended early due to session length limit before the user could run cleanup/stop).

## Follow-ups / Not done
- 64 channels were left in **Charging** state — never explicitly stopped this round.
- `HardwareSimulator/config.json` was **not restored** from `config.json.bak` (still at 1-device/64-channel test config, not the original 10-device/4-channel baseline). Check this first if picking up hardware-simulator work.
- Simulator process may still be running (started via `nohup ... &`, not explicitly killed).
- A stray `HardwareSimulator/dashboard_snapshot.txt` (a11y snapshot dump used mid-debugging) may still be sitting untracked in the repo — check `git status --short` and remove if so.

## Conclusion
This closes out end-to-end verification of the whole device-connection-multiplexing feature series (ADR-1, Sessions #1-#2 fixes) at full 64-channel scale, for the specific transfer→start workflow the user called out as untested. User has confirmed it works as expected.
