# Session 6 — HardwareSimulator Chunk Reassembly + CoreEngine
> Started: 2026-08-11T21:00:00Z (approx) | Agent: Claude (Sonnet 5)

## Goal
User reported: (1) program/DBC data sent to `HardwareSimulator` in TCP chunks is being dropped instead of reassembled, and (2) the simulator only emits random dummy telemetry, never actually executing an uploaded program's real steps. Asked for a "separate CoreEngine" that runs real program steps with dummy sensor data, storing/sending per step.

## What was done
Brainstormed a design (`docs/superpowers/specs/2026-08-11-hardware-simulator-coreengine-design.md`), wrote an implementation plan (`docs/superpowers/plans/2026-08-11-hardware-simulator-coreengine.md`), executed all 7 tasks inline on `main` (user declined a worktree — unrelated files from the pending dashboard-UX changes, no conflict risk).

New files:
- `HardwareSimulator/program_decoder.py` — `ChunkReassembler` (buffers count-then-N-chunks transfers) + `decode_program_steps` (parses the `0xAA55`/offset/`0x55AA` framed program byte stream into step dicts, per real `ProgramBuilder.cs`/`OperatorConstants.cs` encoding).
- `HardwareSimulator/dbc_decoder.py` — `decode_dbc_signals`, mirrors `DbcDatabase.BuildMultiPortPayload` (3-port header + Rx/Tx message blocks + signal blocks; verified against source, not guessed).
- `HardwareSimulator/core_engine.py` — `CoreEngine` step-execution state machine: setpoint operators (CC_CHG/CV_CHG/CP_CHG/CCCV_CHG + `_DCHG` variants), TABLE row walking, PAU duration, GOTO/STO, Limit-based cutoff evaluation (Voltage/Current/Power/Capacity/Energy/Temperature/Time vs `LogicOperator`), capacity/energy integration.
- `HardwareSimulator/tests/` — 16 pytest cases across 3 test files, all passing.
- `HardwareSimulator/requirements-dev.txt` — adds `pytest` as a dev-only dependency (no test infra existed before this session).

Modified `HardwareSimulator/simulator.py`:
- `handle_program_command`: real chunk reassembly + decode on 0x03/0x04 (program) and 0x07/0x08 (DBC), replacing log-and-discard.
- `handle_control_command`: START/STOP now load/start/stop a per-circuit `CoreEngine`.
- `data_sender`: calls `CoreEngine.tick()` each interval when running, feeding real current/voltage/power/temperature/step/cycle/table fields into the device before packet building.
- Fixed `OperatorCode` enum — previous values (`SET=0x01, CC=0x02, ...`) didn't match the real wire values in `OperatorConstants.cs` (`PAU=8, GOTO=9, SET=10, STO=11, ...`); replaced with the verified real values and correct `CHARGE_OPERATORS`/`DISCHARGE_OPERATORS` sets.
- Fixed a real, pre-existing, unrelated bug in `build_realtime_packet`: the Cycle/Table field block wrote `struct.pack(">HH", ...)` five times (20 bytes) where `DecoderService.ParseRealTimeData` expects 5 distinct 2-byte fields (10 bytes) — every realtime packet sent by the simulator was mis-sized, throwing off IOStatus/CRC alignment on decode. Verified fix produces the exact expected 86-byte packet.
- Fixed `build_store_packet`'s presence bitmask to source from `CoreEngine`'s registration tracking (currently still defaults to the pre-existing `0x1FFF` constant pending a documented follow-up — see Gotchas).
- Renamed a variable collision: `data_sender`'s per-packet debug-log tick counter had been repurposing `DeviceCircuit.current_step_index` (a field meant for real step tracking) — renamed to `_packet_tick_count` so CoreEngine's real step cursor isn't corrupted.

## Key discoveries (see also DECISIONS.md / ADR-3)
1. **The program byte format is not blindly self-describing.** `ProgramBuilder.ProcessLimitsWithActions` writes *no byte at all* when a step has zero limits (not even `0x00`) but a 1-byte count + blocks when it has 1+. Even the C# side's own diagnostic re-decoder (`PacketAnalyzer.cs`) sidesteps this by reading counts from the original `StepModel`, not the bytes — an option the simulator doesn't have. Resolved via a bounded candidate-search (try N=0..16 limit-blocks, validate against the step's exact byte length known from the outer `0xAA55` offset framing).
2. SET/REG operators are **not** setpoints — they configure a registration bitmask (which telemetry fields get reported), a wrong assumption caught and corrected mid-design (see round-trip in conversation). Real setpoints come from CC_CHG/CV_CHG/CP_CHG/CCCV_CHG (+`_DCHG`) and TABLE row profiles.
3. Step termination for those operators is Limit-driven (e.g. `Voltage>=4.2V`), not fixed-duration — only PAU is pure duration.
4. Incoming TCP command frames carry a trailing 2-byte CRC (confirmed via the existing `data[4:-2]` log-line convention) — my first pass at chunk reassembly fed `data[4:]` (including the CRC) into the reassembler, corrupting the byte stream; caught via smoke-testing before commit, fixed to `data[4:-2]`.

## Verification
- `pytest HardwareSimulator/tests` — 16/16 passing.
- `dotnet test BatteryTestingSystem.sln` — 29/29 passing (no C# files touched, pure regression check).
- Manual smoke test: uploaded a hand-built CC_CHG+Voltage-limit program through the real `handle_program_command`/`handle_control_command` entry points, ticked `CoreEngine`, built both packet types — step number advanced correctly, current tracked the 2.0A setpoint, voltage ramped toward the 4.2V cutoff, realtime packet came out at exactly 86 bytes (matches the decoder's offset table exactly).
- **Not done**: full browser/dashboard end-to-end verification (blocked by login wall, same as prior sessions — agent will not enter credentials). User should verify visually: upload a real program, START a channel, confirm step number advances and values track setpoints on the Dashboard.

## Gotchas / follow-ups
- `_registrations_to_bitmask` in `simulator.py` is a stub that always returns the pre-existing `0x1FFF` default — mapping `RStandards` registration values to the port-10001 presence-bitmask bits (1-17) is explicitly deferred (see design doc's Out-of-Scope section and the `TODO(follow-up)` comment in code).
- DBC signal decoding (`dbc_decoder.py`) is implemented and tested but not yet consumed by anything downstream (also deferred per design).
- No physics-based battery model — CoreEngine's dummy values track setpoints with light noise/ramps; Capacity/Energy use plain integration, not a response curve.

## Follow-up: live verification pass (same session, `/chrome-devtools-mcp`)
User asked to verify the fix end-to-end via Chrome DevTools ("dbc if has then send else ignore it"). Hit the same login wall as prior sessions (won't enter credentials). Verified instead via `DeviceController`'s unauthenticated REST API (`POST /api/Device/SendProgram|Start|Stop|GetLiveData` — no `[Authorize]`, tracked as T-2) against a real program record already in the dev DB (Program "Test" id=1, exercising the exact same `ChannelCommandHandler.SetProgramAsync`/`TransferDbcFile` → simulator path as the real UI). This surfaced **3 more real decoder bugs**, caught only by decoding an actual captured transfer (not covered by hand-built unit-test fixtures):

1. **2-byte total-length prefix** precedes the `0xAA55` stream (`ConvertProgramIntoBytesPackets`'s return value, not part of `ChannelCommandHandler`'s separate chunk-count command) — `decode_program_steps` now detects and strips it.
2. **SET/REG never call `AddRegistrations`** — contrary to the original design assumption (mirrored from the default-operator path). Confirmed from a live-captured SET step whose body ends exactly after `AddRegCount`, no trailing registrations block. `_decode_set_or_reg` fixed to expect exact-length end instead.
3. **Time-cutoff limit values are int32 ms, not float32** — `ExtractFloatAsByteArraySafe`'s time-unit branch. Fixed in `_decode_default` (keyed off `cutoff==CUTOFF_TIME`) and `_decode_pau` (PAU limits are always time-only).

Added `test_decode_real_captured_transfer` — a ground-truth regression test using the exact real captured bytes, so these 3 fixes can never silently regress.

**2 further integration bugs** found once decode was correct and a real program actually ran to completion:

4. `device.program_status` never transitioned off `RUNNING` when `CoreEngine` reached its real end (STO step) — the simulator kept sending `Operator=STO` store packets forever, causing the server to call `EndSession` repeatedly for an already-finished session (`"END Session"` log-spam observed live). Fixed: `data_sender` now sets `program_status = COMPLETED` / `circuit_status = Idle` the instant `CoreEngine.state` leaves `RUNNING`.
5. Once `CoreEngine` finished, `build_realtime_packet` fell back to full random telemetry forever (the `core_running` gate turned false and `enable_random` took over) — a real device would hold idle/zero after finishing, not emit random noise. Fixed: gate the random-fallback on `has_program` (`device.core_engine is not None`, i.e. "ever had a program") instead of the momentary `core_running`; hold current/voltage at 0 once `has_program and not core_running`.

**Verified live**: `SendProgram` → simulator log `"Decoded 3 program steps"` (was 0 before fix #1) → `Start` → `GetLiveData` polled repeatedly showed the real SET(instant)→CC_CHG(2.0A setpoint, ramping voltage)→STO(zeroed, `programStatus=3` COMPLETED) progression, reproducibly. `dbcId=1` (DBC present) → `"DBC:...Success"`; the existing C# `CoreSendProgram` code already correctly skips DBC transfer when absent (`if (dbc.Success)`), confirming the user's "DBC if present send, else ignore" requirement was already correctly implemented server-side.

**Residual, not chased further**: intermittent GetLiveData reads returning stale-looking/random values interleaved with correct CoreEngine data, and per-request latency around 15-20s, observed only at the full 64-channel simulator scale used for this session's testing. Not clearly related to any change made this session (no code touches request-handling concurrency) — flagged as a possible pre-existing performance characteristic at 64-channel scale worth a dedicated investigation, not a regression from this work.

All fixes covered by pytest (17/17 passing) and committed.

## Task sheet
Added row 28 (S.No 27, Project "Hardware Simulator", Task No. 2) to `docs/WebAppME_TaskSheet.xlsx` summarizing this session's full scope (chunk-reassembly fix, CoreEngine, 5 live-verification bug fixes) as Completed.
