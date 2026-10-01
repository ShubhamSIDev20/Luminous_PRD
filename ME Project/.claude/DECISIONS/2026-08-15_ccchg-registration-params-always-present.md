# ADR-26: `CCChg`'s trailing registration-parameters block is ALWAYS present
> Date: 2026-08-15 | Session: #10 | Status: Accepted
> File: `DECISIONS/2026-08-15_ccchg-registration-params-always-present.md`

---

**Context:** Early design work (wrongly) assumed `CCChg` steps never carry a
trailing "registration parameters" list, based on a partial reading of the old
Secondary firmware (`stepData.c`).

**Decision:** Confirmed two ways: `stepData.c`'s `default:` case (every
charge/discharge operator) reads `numCutoffConditions`, then unconditionally
reads `numRegistrationParams` right after — even when it's `0`. `Program Packet
V0.12.xlsx` step 2 (`CC_Chg`) encodes that exact byte as `0x00`. The two-group
rule is clean, not inconsistent: power operators + `PAU` + `TABLE` always carry
it; `SET`/`STO`/`GOTO`/`REG`/`CYC`/`BEG`/`INT`/`ERR`/`MSG` never do.

**Consequence:** `me_step_decode()` always reads this block for `CCChg`
(§2.2/§3.1 of the design doc). Caught by re-checking against two independent
sources rather than trusting one.

**Related**
- Supersedes: none (corrects an early design assumption before it shipped)
- Relates to: ADR-25, ADR-27
