# T-10: HardwareSimulator Chunk Reassembly + CoreEngine
> Session: #6 | Completed: 2026-08-11

## Problem
1. `HardwareSimulator/simulator.py` received chunked program/DBC uploads (the real `ChannelCommandHandler.cs` protocol chunks payloads to ≤1400 bytes per TCP message) but logged and **discarded** every chunk — `DeviceCircuit.program_steps` was always empty.
2. The simulator's telemetry generator (`build_realtime_packet`) emitted fully random current/voltage/temperature, completely disconnected from any uploaded program — no real step execution.

## Solution
- `program_decoder.py`: `ChunkReassembler` (buffers chunks until the expected count) + `decode_program_steps` (parses the real `0xAA55`/offset/`0x55AA` byte format, resolving a genuine encode-side ambiguity — zero-limit steps write no count byte at all — via a bounded candidate-search validated against the step's known total length).
- `dbc_decoder.py`: `decode_dbc_signals`, mirrors `DbcDatabase.BuildMultiPortPayload`'s 3-port header + message/signal block layout.
- `core_engine.py`: `CoreEngine` state machine — executes setpoint operators (CC_CHG/CV_CHG/CP_CHG/CCCV_CHG + `_DCHG`), TABLE row profiles, PAU duration, GOTO/STO, and Limit-based cutoff conditions (Voltage/Current/Power/Capacity/Energy/Temperature/Time), replacing random telemetry with setpoint-tracking dummy values.
- `simulator.py`: wired reassembly into `handle_program_command`, wired CoreEngine into `handle_control_command` (START/STOP) and `data_sender` (per-tick), fixed the real `OperatorCode` enum (previous values didn't match `OperatorConstants.cs`), fixed a pre-existing realtime-packet field-size bug (Cycle/Table block was 20 bytes, decoder expects 10).

## Files
- New: `HardwareSimulator/program_decoder.py`, `HardwareSimulator/dbc_decoder.py`, `HardwareSimulator/core_engine.py`, `HardwareSimulator/requirements-dev.txt`, `HardwareSimulator/tests/*` (3 test files, 16 tests)
- Modified: `HardwareSimulator/simulator.py`

## Verification
- `pytest HardwareSimulator/tests` — 16/16 passing
- `dotnet test BatteryTestingSystem.sln` — 29/29 passing (regression check, no C# touched)
- Manual smoke test through the real `handle_program_command`/`handle_control_command` entry points confirmed correct decode, correct CoreEngine step advancement on a Voltage limit, and an exact 86-byte realtime packet matching the decoder's offset table.

## Follow-ups (not in this task's scope)
- `_registrations_to_bitmask` in `simulator.py` is a stub — real RStandards-to-opcode-bitmask mapping deferred (see ADR-3 / design doc).
- DBC signal decode result isn't consumed by anything downstream yet.
- User still needs to visually verify on the live Dashboard (agent cannot log in — see prior sessions).
