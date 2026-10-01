# Session — HardwareSimulator Q10 Support (follow-up to T-53)

> Started: 2026-09-17T10:00:00Z
> Status: Complete — implemented, tested, build/tests green.

## Goal
User manually tested T-53's "Jump to Step" feature against the running app + `HardwareSimulator`:
entered a step number, got what looked like success, but the dialog's "currently on step N" never
changed. Asked to verify, then asked me to update the simulator to actually support this.

## Root cause (found before writing any code)
`HardwareSimulator/simulator.py`'s `handle_program_command` (answers every `0xBB` query) has no
branch for `0x0A` (Jump to Step). An unhandled query id still falls through to the function's shared
generic response footer, which unconditionally sends `STATUS = 0x01 (Success)` — `result` is set to
`CommandStatus.SUCCESS` once at the top of the function and never overridden by a matching branch.
So the app received a false-positive success: `ChannelCommandHandler.JumpToStepAsync` correctly
decoded `STATUS=Success` and showed "Jumped to step N", but the simulator's `CoreEngine` never
actually moved anything — nothing in the fallback path touches `current_step_index`. This is a worse
gap than "the simulator has no Q10 support" (a clean rejection) — it silently lies about success.

## Implementation
- **`HardwareSimulator/core_engine.py`** — new public `CoreEngine.jump_to_step(target_step_id) ->
  (success, reason_byte)`:
  - Rejects with `0x01` if `state != RUNNING` (mirrors `JumpToStepReason.NoProgramRunning`).
  - Rejects with `0x02` if no step in `self.steps` has that `step_id` (`StepDoesNotExist`).
  - Jumping to the already-current step is an accepted no-op (`True, 0x00`) — per the doc, this is
    explicitly *not* a "restart this step" command, so nothing resets, loop counters included.
  - Otherwise moves `current_step_index`, resets `elapsed_in_step_ms` (doc: "step elapsed time
    resets") and the `TABLE`-row cursor (defensive — stale state from a different step type would
    otherwise leak in), and resets `loop_counters[target_step_id]` to 0 *only if that step id is
    already a tracked loop target* — this dict is only ever keyed by a GOTO's landing step (a loop's
    `BEG`), so this one line is exactly "jumping onto a loop's BEG restarts its iteration count",
    the doc's one loop-counter rule this simulator's existing model can express. Jumping within or
    out of a loop correctly leaves the dict untouched (no code needed for those two cases — they
    were already no-ops on `loop_counters`).
- **`HardwareSimulator/simulator.py`**:
  - `handle_program_command` gained an `elif query_id == 0x0A` branch. Unlike every other `0xBB`
    query in this function (`STATUS`-only), Q10's response needs `STATUS`+`REASON`, so this branch
    builds and sends its own 9-byte frame (`0xBB, dev, addr, 0x0A, STATUS, REASON, 0x00, CRC_LO,
    CRC_HI` — matching the existing convention where every other query pads to the same total length
    with a trailing zero before `bind_crc16`) and `return`s early, bypassing the shared footer.
  - New `HardwareSimulator._handle_jump_to_step(device, step_number)`: checks `device.paused` first
    (set by the existing `0xEE` PAUSE command; the engine's own `state` never changes to a "paused"
    value — pause is tracked one level up, in `DeviceCircuit`, not `CoreEngine`) and rejects with
    `0x01` if so — chosen over `0x04` because the doc's own current wording for `0x01` is "no program
    is running **or paused** on this circuit", and this simulator merges the Primary+Secondary roles
    so it already locally knows about its own pause state (`0x04` is specifically for what only a
    real *Secondary* board could see, e.g. interrupt/error/message-wait, which this simulator does
    not model at all). Otherwise delegates to `CoreEngine.jump_to_step` and maps its tuple straight
    onto `(CommandStatus.SUCCESS/FAILED, reason)`.
  - Malformed frame (`< 6` bytes, i.e. no full 2-byte step number) rejects with `0x03` before ever
    touching the engine.

## Deliberately not modeled (simulator limitation, not a bug)
- `0x04` (Secondary NACK / operator-wait states) and `0x05` (a jump already in flight) have no
  simulator-side equivalent — this simulator doesn't model interrupt/error/message-wait as distinct
  states, and its TCP command handling is synchronous per message, so a real in-flight collision
  can't occur here. Both `REASON` values are still fully covered by `DecoderServiceTests.cs` on the
  C# side (added in T-53) — this session only closes the simulator half of the gap.

## Tests (26 Python, up from 18)
- `test_core_engine.py`: 5 new tests — jump moves the pointer + resets step-elapsed-time, rejects a
  nonexistent step, rejects when not running, same-step jump is a true no-op (doesn't reset
  elapsed-time either), and a jump onto a tracked loop `BEG` resets that loop's counter (this one's
  first draft actually caught a subtlety in my own test setup, not the implementation: sitting
  exactly on the `BEG` step and "jumping" to it is the same-step no-op case per the doc, so the test
  had to move the engine elsewhere first before jumping back, to actually exercise the reset path).
- `test_simulator.py` (new file): 3 tests exercising `HardwareSimulator._handle_jump_to_step`
  directly (not just `CoreEngine`) — confirms the `device.paused` gate and the wire-ready
  `(STATUS, REASON)` tuple it returns, which `test_core_engine.py` alone can't pin since `paused`
  lives on `simulator.py`'s `DeviceCircuit`, not the engine.

## Verification
- `python -m py_compile simulator.py core_engine.py program_decoder.py`: clean.
- `python -m pytest tests -q`: **26/26** passing (up from 18).
- Not re-run: the .NET side (`ChannelCommandHandler`/`DecoderService`/tests) — untouched this
  session, still the 793/793 from T-53.
- Not done: a live click-through against the running app + this updated simulator (same
  no-browser-tool limitation as T-53's session) — the user should be able to verify directly now,
  since the false-positive-success bug that motivated this session is fixed.

## Branch / PR
Same branch as T-53, `feature/jump-to-program-step` (this is a direct follow-up closing a gap that
branch's own PR already flagged as a known limitation, not a new topic) — never `main` directly.
