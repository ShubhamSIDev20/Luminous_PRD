# T-54 — HardwareSimulator Q10 Support (follow-up to T-53)

**Session:** [sessions/2026-09-17_1000_jump-to-step-simulator-support.md](../sessions/2026-09-17_1000_jump-to-step-simulator-support.md)
**Status:** Done
**Branch:** `feature/jump-to-program-step` (same as T-53)

## Ask
User live-tested T-53's Jump-to-Step feature: entering a step number appeared to succeed, but the
displayed current step never changed. Asked to verify, then to update the simulator so it actually
supports Q10.

## Root cause
`simulator.py`'s `handle_program_command` had no branch for `0x0A` — an unhandled query still falls
through to a shared generic footer that unconditionally replies `STATUS=Success`, since `result`
defaults to `CommandStatus.SUCCESS` and no branch overrides it. A **false-positive success**, not a
missing response — the app correctly showed "Jumped to step N" while nothing in the simulator moved.

## What was done
- `HardwareSimulator/core_engine.py`: new `CoreEngine.jump_to_step(target_step_id)` — moves
  `current_step_index`, resets step-elapsed-time and the TABLE-row cursor, resets a loop's iteration
  counter only when jumping onto its tracked `BEG` target, rejects a nonexistent step or a
  not-currently-running engine. Same-step jump is an accepted no-op per the doc (nothing resets).
- `HardwareSimulator/simulator.py`: `handle_program_command` gained a dedicated `0x0A` branch
  (builds/sends its own `STATUS`+`REASON` response, unlike every other `STATUS`-only query here) and
  a new `_handle_jump_to_step(device, step_number)` helper that gates on `device.paused` (the
  `0xEE` PAUSE flag — `CoreEngine.state` itself never changes for a pause) before delegating to the
  engine.
- Not modeled: `REASON 0x04`/`0x05` (Secondary-only states, in-flight collision) — this simulator
  has no equivalent state and its command handling is synchronous, so neither can genuinely occur;
  both remain covered by the C#-side `DecoderServiceTests.cs` from T-53.

## Tests
26 Python tests (+8: 5 in `test_core_engine.py`, 3 in new `test_simulator.py` exercising
`_handle_jump_to_step`'s `device.paused` gate specifically, which `CoreEngine` alone can't cover).

## Verification
`python -m py_compile` on all 3 changed files: clean. `python -m pytest tests -q`: 26/26. .NET side
untouched, still 793/793 from T-53. Not done: live click-through against the running app (same
no-browser-tool limitation as T-53) — the user is positioned to verify directly now that the
false-positive bug is fixed.
