# HardwareSimulator: Chunk Reassembly + CoreEngine Design

**Goal:** Fix the HardwareSimulator dropping chunked program/DBC data on the floor, then replace its pure-random telemetry generator with a CoreEngine that actually executes the uploaded program's real step sequence (with dummy sensor values) and tags output/storage by step.

**Architecture:** Two independent, sequential changes to `HardwareSimulator/simulator.py`:
1. A chunk-reassembly buffer per transfer type (program, DBC) that accumulates chunks until the expected count is reached, then decodes the full byte stream.
2. A `CoreEngine` per simulated channel that owns a step cursor + timer and drives `build_realtime_packet`'s output from the real step data instead of `random.uniform(...)`.

**Tech stack:** Python (existing `simulator.py`, no new dependencies). No changes to the C# side — `ChannelCommandHandler`'s chunking protocol is correct as-is and stays untouched.

## Global Constraints

- Do not change the wire protocol between `ChannelCommandHandler.cs` and the simulator — chunk size (1400B), command codes (`0x03/0x04` program, `0x07/0x08` DBC, `0xEE` control), and framing (`0xAA55`/offset/`0x55AA` for steps) are fixed by the real-hardware-compatible encoding in `ProgramBuilder.cs`/`DecoderService.cs`. The simulator's decoder must mirror that encoding exactly.
- No physics-based battery modeling in this pass — CoreEngine outputs setpoint-tracking dummy values (setpoint ± small noise), not a simulated battery response curve.
- No new external Python dependencies; stdlib only, consistent with the existing single-file `simulator.py`.
- Existing `enable_random` fully-random telemetry path is being replaced for channels that have a loaded program; channels with no program loaded keep today's random behavior as a fallback (nothing to step through).

---

## 1. Chunk Reassembly

**Problem:** `HardwareSimulator.handle_program_command` receives each program/DBC chunk (query `0x04`/`0x08`), logs it, and discards it. `DeviceCircuit.program_steps` is always empty; DBC content is never decoded.

**Fix:**
- On `SendProgramStepsCount` (`0x03`) / `SendDbcStepsCount` (`0x07`): reset and initialize a per-circuit `bytearray()` buffer and record the expected chunk count. Receiving a new `StepsCount` message while a transfer is in progress discards the stale partial buffer and starts fresh (handles retried/interrupted transfers).
- On each `SendProgram` (`0x04`) / `SendDbcFile` (`0x08`) chunk: append the payload to the buffer, increment a received-chunk counter.
- When received count == expected count: decode the full buffer in one pass.
  - **Program steps:** parse the `0xAA55`-header / 4-byte-offset / step-bytes / `0x55AA`-trailer framing (mirrors `ProgramBuilder.BuildPackets`), stopping at the `0xFFFFFFFF` offset sentinel. Each step's `OperatorCode` byte selects field layout (SET/REG/TABLE/PAU/GOTO/STO — mirrors `OperatorConstants`/`ProgramBuilder.Process*Operator`). Store as an ordered list of step dicts on `DeviceCircuit.program_steps`.
  - **DBC:** parse the multi-port payload (mirrors `DbcDatabase.BuildMultiPortPayload`) into `{message_id: {signal_name: (bit_offset, bit_length, scale, offset)}}` and store on `DeviceCircuit.dbc_signals`.
- **Error handling:** if the buffer's final length doesn't match what the framing implies (e.g. truncated last chunk), log a decode error and leave `program_steps`/`dbc_signals` at their previous value rather than crashing — a bad transfer must not take down the simulated channel. Add a coarse timeout (e.g. discard the partial buffer if no new chunk arrives for N seconds) so a dropped connection mid-transfer doesn't leak state into the next attempt.

**Testing:** New `HardwareSimulator/tests/test_chunk_reassembly.py` (pytest — no test infra exists yet for this project, so this introduces `pytest` as a dev-only tool; add a `HardwareSimulator/requirements-dev.txt` with `pytest`). Cases: single-chunk transfer, multi-chunk transfer reassembles identically to a hand-built reference byte stream, out-of-order-count reset (new `StepsCount` mid-transfer discards stale buffer), truncated-final-chunk leaves prior `program_steps` intact and logs rather than raising.

---

## 2. CoreEngine — Real Step Execution With Dummy Data

**Problem:** `build_realtime_packet` emits `random.uniform(...)` for current/voltage/temperature regardless of any uploaded program. `DeviceCircuit.step_number` is reset to 1 on START and never advances against real step timing/logic. `accumulated_capacity` currently accrues unconditionally every tick (`simulator.py:412`), even on idle circuits — a bug CoreEngine ownership of accumulation naturally fixes by only integrating while `RUNNING`.

**Corrected operator semantics** (verified against `ProgramBuilder.cs`/`OperatorConstants`/`NominalConfig` — this supersedes the original "SET/REG hold current/voltage" assumption):

- **Setpoint operators** — `CC_CHG`/`CC_DCHG` (Current, A), `CV_CHG`/`CV_DCHG` (Voltage, V), `CP_CHG`/`CP_DCHG` (Power, W), `CCCV_CHG`/`CCCV_DCHG` (Current A + Voltage V) — these carry the real nominal setpoint(s) CoreEngine drives dummy output from.
- **TABLE** — carries an explicit `(time_ms, A, W, V)` profile per row; CoreEngine walks rows in order, each row's A/W/V (whichever are present) is the setpoint for that row's duration.
- **SET / REG do NOT carry current/voltage setpoints.** They configure a *registration bitmask* (`RStandards`/`AddRegCount`) — which telemetry fields get reported on the Store (port 10001) stream. `REG` without its own registrations inherits the nearest preceding `SET`'s. CoreEngine tracks "active registration set" from the most recent SET/REG and uses it only to shape the port-10001 presence bitmask (see below) — it does not affect current/voltage output.
- **PAU** — holds last-emitted current/voltage/power, pure duration-based (its Limit is always a bare time value, no comparison operator by construction).
- **Step advancement for setpoint/default operators is Limit-driven, not just duration-driven.** Each tick, evaluate every step's `Limits` (`CutoffCondition` + `LogicOperator` + value) against the corresponding live telemetry field (Current/Voltage/Power/*Capacity/*Energy/Temperature/Time). When a limit's condition is met, its `Action` fires: `STO` (stop), `GOTO` (jump, bounded by loop count), `ERR`/`MSG` (set the corresponding code on telemetry, continue), `INT` (interrupt marker, continue). A step with no Limits at all falls back to a fixed max-duration guard so the engine can never hang forever on a malformed program.
- **GOTO** (as a step, not as a Limit-Action) unconditionally jumps to its target step, loop-bounded the same way.
- **STO** (as a step) unconditionally stops.

**Design:** A new `CoreEngine` class, one instance per `DeviceCircuit` that has a loaded program. Holds: `steps` (from reassembly above), `current_step_index`, `elapsed_in_step_ms`, `loop_counters` (dict keyed by GOTO target, for bounded-loop steps), `active_registrations` (set, from most recent SET/REG), `state` (`IDLE`/`RUNNING`/`PAUSED`/`STOPPED`).

**Interface:**
- `CoreEngine.load_program(steps: list[dict]) -> None` — called once reassembly (§1) completes for that circuit.
- `CoreEngine.start() -> None` — called from the existing `0xEE` START handler; resets cursor to step 0, state → `RUNNING`.
- `CoreEngine.stop() -> None` — state → `STOPPED`.
- `CoreEngine.tick(dt_ms: int) -> dict` — called once per `packet_interval_ms` tick (same cadence `data_sender` already uses). Returns the full telemetry field set needed by both packet builders: `current, voltage, power, temperature, step_number, operator, cycle_number, cycle_run_iteration, table_step_number, table_total_row_number, accumulated/charge/discharge/step capacity & energy, active_registrations`. Replaces the `random.uniform(...)` block in `build_realtime_packet`/feeds `build_store_packet` for circuits with `state == RUNNING`; circuits with no loaded program (or `IDLE`) keep the existing random fallback.

**Dummy value model** (still "dummy", not real physics — this is bookkeeping arithmetic, not a battery response model):
- Current/Voltage/Power for CC/CV/CP/CCCV steps and TABLE rows = the configured setpoint ± small bounded noise (e.g. ±1-2%).
- Capacity/Energy accumulators integrate `Current * dt` / `Power * dt` over elapsed RUNNING time (plain running-sum arithmetic, same as today's existing `accumulated_capacity` line — just correctly gated on RUNNING and reset per session/step where the real device would), so Capacity/Energy-based cutoff Limits can actually be satisfied.
- Temperature: unchanged slow random walk within a plausible band — no operator defines a temperature setpoint.

**Packet building — two distinct formats, both already wired in `simulator.py`:**
- **Port 10000 (View), `build_realtime_packet`**: fixed-offset layout matching `DecoderService.ParseRealTimeData`. **Existing bug found and fixed in this pass**: the CycleNumber/CycleRunIteration/TableStepNumber/TableTotalRowNumber/reserved block currently writes `struct.pack(">HH", cycle_number, table_step_number)` **five times** (20 bytes) where the decoder expects 5 distinct 2-byte fields (10 bytes total) — misaligning IOStatus/CRC on every packet today. Fix: emit each field individually — `CycleNumber`, `CycleRunIteration`, `TableStepNumber`, `TableTotalRowNumber`, `0` (reserved) — each as its own `struct.pack(">H", ...)`, sourced from CoreEngine's real cycle/table-row tracking instead of duplicating `device.cycle_number`/`device.table_step_number`.
- **Port 10001 (Store), `build_store_packet`**: already close to the V2 presence-bitmask format (`0x1FFF` hardcoded = opcodes 1-13 present). Change: derive the presence bitmask from CoreEngine's `active_registrations` (falling back to the current default "always report core telemetry" set when nothing is registered yet, so the log stream is never empty) instead of the hardcoded constant.

**Storage tagging:** step_number/operator/cycle/table fields ride on both outgoing packets exactly as a real device would emit them, so downstream session storage buckets data by step — satisfying "store must be according to step."

**Error handling:** an empty/absent `steps` list on `start()` leaves the circuit in `IDLE` (falls back to today's random telemetry) rather than raising. A GOTO/Limit-Action target referencing a step number that doesn't exist in the loaded program logs a warning and treats it as `STO` (stop) rather than crashing the tick loop. A step with no Limits and no natural terminator falls back to a fixed max-duration guard (prevents an infinite hang from a malformed program).

**Testing:** `HardwareSimulator/tests/test_core_engine.py`. Cases: a CC_CHG step with a `Voltage>=4.2V` Limit ends when accumulating dummy voltage crosses 4.2V (not just via time); PAU freezes output and advances after its own duration; GOTO loop runs the configured number of iterations then falls through (no infinite loop); STO halts and zeroes output; a program with an invalid GOTO/Limit-Action target degrades to STOP instead of raising; a circuit with `program_steps == []` on `start()` stays on the existing random fallback (regression guard for the no-program case); SET/REG's registration set correctly shapes the port-10001 presence bitmask; Capacity/Energy accumulate monotonically and consistently with Current/Power over elapsed RUNNING time only (not while idle); the fixed realtime-packet field-size fix produces exactly 10 bytes for the Cycle/Table block and round-trips correctly against the documented decoder offsets.

---

## Out of Scope (this pass)

- Physics-based battery voltage/SoC modeling (noted above — deliberately deferred).
- Using the decoded DBC signal map (§1) to reshape the realtime packet's byte layout to match real CAN signal encoding — this pass only decodes and stores it; wiring it into `build_realtime_packet`'s output format is a follow-up once CoreEngine's step-tagged values exist to map onto those signals.
- Any change to the C# sender side (`ChannelCommandHandler`, `ProgramBuilder`, `DecoderService`) — confirmed working as designed.
