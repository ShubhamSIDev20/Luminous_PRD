# T-48: Handle 0xAA Q7–Q10 broadcast frames, or reject them deliberately
> Created: 2026-08-12 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-12_handle-aa-q7-q10-broadcast-frames.md`

---

## Description
They use a **2-byte header** — `Start | QueryID`, no DeviceNumber, no
CircuitNumber (`bm_config_v6.0.md` §9.1). Every `0xAA` path assumes 4 bytes,
so one is read as device `0x07` / circuit `0x01`; `0x01` is a malformed
CircuitID, so `me_circuit_slot()` rejects it and admission control drops the
frame.

## Why / Context
**Correct outcome, wrong reason** — nothing in the code knows these frames
exist. They are deliberately unnamed in `proto_defs.h` and return 0 from the
length table (ADR-22). Fix shape: a `frame_is_broadcast(start, query)`
predicate consulted before the header is read.

## Progress Log
- **2026-08-12** (Session #9): Identified while transcribing `bm_config_v6.0.md`
  (ADR-20) and filling the `0xAA` length table (ADR-22).

## Related
- Relates to: ADR-20, ADR-22
