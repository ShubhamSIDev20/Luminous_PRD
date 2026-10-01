# ADR-28: Zero-fill unaddressed CAN slots; block selection for ch 5-8 deferred
> Date: 2026-08-14 | Session: #10 | Status: Accepted, revisit before ch 2+ — **item 1 resolved by ADR-32 (2026-08-19)**
> File: `DECISIONS/2026-08-14_can-slot-zero-fill-block-selection.md`

---

> **UPDATE (session #12, 2026-08-19):** Item 1 below ("revisit before channel 2
> is wired") is now resolved — see ADR-32. `can_mgr.c` merges every SET frame
> against a per-block shadow buffer instead of transmitting a partially
> zero-filled frame. Item 2 (the Block 1/Block 2 CAN-ID ambiguity for
> channels 5-8) remains open and out of scope — Secondary 1's 4 channels are
> all in Block 1.

Two open items from the CAN spec (`Ref Docs/master_slave_can_v1.0.md`), resolved
by developer policy for this single-channel iteration only:
- **Unaddressed slots in a 64-byte SET_VALUES frame are left zero-filled**, even
  though byte `+8 = 0x00` is an *active* `CMD_STO`. Safe today because no second
  channel exists to receive it. **Must be revisited before channel 2 is wired.**
- **`me_can_block_for_channel()`** maps ch 1-4→Block1, 5-8→Block2 and a block's
  frame is sent only when it has an active circuit — forward-compatible
  plumbing. The underlying CAN-ID ambiguity between the two blocks (both use
  `Dev#,1`/`Dev#,2`) is **not** resolved; only channel 1 is ever exercised.

**Related**
- Supersedes: none
- Relates to: ADR-32 (resolves item 1), ADR-25, ADR-27
