# ADR-3: Program byte-format decode uses bounded candidate-search, not fixed offsets
> Date: 2026-08-11 | Session: #6 | Status: Accepted

## Context
`HardwareSimulator/program_decoder.py` needs to reassemble and decode the real program-upload byte stream (`ProgramBuilder.cs`'s `0xAA55`/offset/step-bytes/`0x55AA` framing) so `CoreEngine` can execute real steps instead of dummy random telemetry.

## Problem
`ProgramBuilder.ProcessLimitsWithActions` writes **no byte at all** when a step has zero Limits — not even a `0x00` count byte — but writes a 1-byte count + limit-blocks when it has 1 or more (and PAU-operator steps never get a count byte regardless). This means a byte stream, decoded blindly in sequence, cannot tell "0 limits → the next byte is actually the Registrations count" from "N limits → the next byte is the Limits count."

We checked whether the C# side's own diagnostic re-decoder (`PacketAnalyzer.cs`) had already solved this — it hasn't. It sidesteps the problem by reading the limit/nominal counts from the original `StepModel` object passed in alongside the bytes, not from the bytes themselves. The simulator doesn't have that luxury; it only ever receives bytes over TCP.

## Decision
Resolve the ambiguity with a **bounded candidate-search validated against the step's exact known length**. The outer `0xAA55 <4-byte offset> ... 0x55AA` framing tells us precisely where the current step's bytes end. After decoding nominal values, try limit-count candidates `N = 0, 1, 2, ... 16` in order; for each, greedily parse `N` limit-blocks (self-describing once inside — an action byte's own opcode tells you if 0, 1, or 2 extra bytes follow) then the trailing Registrations block (also self-describing: count byte + `5 * count` bytes). The first `N` whose parse lands **exactly** on the step's known end is accepted. No candidate landing exactly on the end marks the step `parse_ok: False` (logged, not raised — see `program_decoder.py`'s error-handling contract).

## Why
- Matches the actual encoder behavior exactly (verified against `ProgramBuilder.cs` source, not guessed).
- Doesn't require any change to the C# sender side, which was confirmed correct-as-designed.
- The exact-length validation makes false-positive matches for real programs vanishingly unlikely (a coincidental byte pattern would have to independently satisfy both the limit-block AND registration-block self-description checks at the same offset).

## Consequences
- Decode cost is O(16) worst case per step instead of O(1) — negligible at program sizes real test programs use (tens to low hundreds of steps).
- If a future program format change makes the candidate bound of 16 insufficient (a step with >16 limits), decode will fail closed (`parse_ok: False`) rather than silently misparsing — raise the `MAX_LIMIT_CANDIDATES` constant in `program_decoder.py` if that ever happens.

## Related
Also discovered while building `CoreEngine` (same session): SET/REG operators do **not** carry current/voltage setpoints — they configure a registration bitmask (which telemetry fields get reported on the port-10001 store stream). Real setpoints come from CC_CHG/CV_CHG/CP_CHG/CCCV_CHG (+ `_DCHG` variants) and TABLE row profiles. Step termination for those operators is Limit-driven (e.g. `Voltage>=4.2V`), not fixed-duration — only PAU is pure duration-based. See `docs/superpowers/specs/2026-08-11-hardware-simulator-coreengine-design.md` for the full corrected semantics.
