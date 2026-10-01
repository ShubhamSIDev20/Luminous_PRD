# ADR-14: Program completion is detected from the chain terminator
> Date: 2026-08-11 | Session: #4 | Status: Accepted
> File: `DECISIONS/2026-08-11_program-completion-chain-terminator.md`

---

**Context:** The old firmware knew a program transfer was finished by counting
`0xBB` Q4 packets against the total announced by `0xBB` Q3. **ME no longer sends
Q3** — the Web Application sends program packets directly.

**Decision:** After appending each packet, walk the chain from offset 0 and mark
the circuit complete on reaching a step whose `nextIndex` is `0xFFFFFFFF` with a
valid end sequence.

**Consequences:** Requires the Web Application to set the terminator on the final
step. If it does not, the circuit never completes and Start refuses — so the log
message names that possibility explicitly rather than only "incomplete".
Because `nextIndex` is an **absolute** offset, the Data Manager must append
contiguously from offset 0; any inserted framing invalidates every offset.

**Related**
- Supersedes: the old firmware's Q3-count-based completion detection
- Relates to: ADR-18
