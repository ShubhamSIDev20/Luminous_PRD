# ADR-13: A malformed CircuitID is rejected, never folded to slot 0
> Date: 2026-08-11 | Session: #4 | Status: Accepted
> File: `DECISIONS/2026-08-11_malformed-circuitid-rejected-not-folded.md`

---

**Context:** CircuitID nibbles are **1-based** (`0x11` = Secondary 1, Channel 1),
so `0x00`, `0x01`, `0x10`, `0x09`, `0x90` and `0xFF` are all malformed. Masking
the nibbles would map several of them onto real slots.

**Decision:** `me_circuit_slot()` returns `ME_SLOT_INVALID` and every accessor
refuses. Nine rejection cases are unit-tested.

**Why it matters:** a bad CircuitID silently overwriting Secondary 1 Channel 1's
program is a corruption with no error, no log and no symptom until the wrong
test runs on real cells.

**Related**
- Supersedes: none
- Relates to: ADR-21, T-32
