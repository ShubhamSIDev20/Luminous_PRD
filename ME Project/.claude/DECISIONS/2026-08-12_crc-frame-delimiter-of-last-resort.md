# ADR-19: The CRC is the frame delimiter of last resort
> Date: 2026-08-12 | Session: #8 | Status: Accepted — **amended by #9** — not hardware-verified
> File: `DECISIONS/2026-08-12_crc-frame-delimiter-of-last-resort.md`

---

> ### ⚠️ AMENDMENT (session #9, 2026-08-12): step 2 is no longer gated on `layout == 0`
>
> Step 2 below was written as *"if the table has NO entry, try the whole read"*, on
> the reasoning that "no entry" and "an entry that failed to verify" are different
> states. **That gate was itself a bug, and adding a correct table entry exposed
> it.**
>
> When `0xAA` Q5 gained its real 46-byte entry (ADR-22), a **short** `0xAA` frame
> had `layout == 46` — *larger than what had arrived*. So step 1 could not use it,
> the `layout == 0` gate excluded it from step 2, and it fell straight into the
> shortest-first scan, which truncated it at a planted 6-byte CRC match. The
> pre-existing regression test
> `test_resolve_len_does_not_truncate_on_a_planted_short_crc` caught it
> immediately — it used a `0xAA` frame as its vehicle.
>
> **A layout entry that overshoots what has arrived says nothing about where the
> frame ends, so it must not forfeit the whole-read protection.** Step 2 now runs
> for every start byte whenever the entire available buffer checksums, and sets
> `*out_scanned` by the same rule as the scan: `(layout != 0 && resolved != layout)`.
>
> The ordering still favours the layout, because step 1 already returns for any
> frame whose entry both fits and checksums — which is what splits genuinely
> coalesced frames. Reaching step 2 means that failed.
>
> **The general lesson, worth more than the fix:** adding a *correct* entry to the
> length table made a *different* frame parse worse. After adding any entry, check
> that a frame shorter than it still resolves. Guarded by
> `test_resolve_len_does_not_truncate_when_the_layout_overshoots`.
>
> Two tests changed vehicle from `0xAA` to `0xA0` calibration, which is now the
> genuinely lengthless start byte:
> `test_resolve_len_takes_a_lengthless_frame_whole` and
> `test_undeterminable_length_reports_zero`. Their intent is unchanged.

**Context:** A hardware run rejected this frame as `BAD_CRC`:

```
EE 01 11 01 01 11 35 F3 62 E7        (0xEE Q1 Start, 10 bytes)
```

The CRC was correct. `bm_control_v3.0.md` listed Q1 Start as *"Payload: None"*, so
`me_frame_expected_len()` returned **6**, `consume_rx_buffer()` handed
`route_frame()` only `EE 01 11 01 01 11`, and the CRC was computed over
`EE 01 11 01` and compared against `01 11` — **the first two bytes of a 4-byte
Session ID nobody knew was there.**

**The damage was not one frame.** `off += 6` left `35 F3 62 E7` in the reassembly
buffer, below `ME_FRAME_MIN_LEN`, so `consume_rx_buffer()` waited for more. The
next frame was appended behind that debris and parsed at a 4-byte offset —
"unrecognised start byte", forever, for the life of the connection. **One wrong
entry in a length table silently destroys every subsequent frame.**

**Root cause, stated precisely:** this protocol has **no length field** in most
frames, so `me_frame_expected_len()` is not parsing — it is *recalling documented
layout*. Every entry is an assumption, and ADR-4 already establishes that the
documents are wrong sometimes. There was no recovery path from a wrong one.

**Decision:** `me_frame_resolve_len()` in `frame_router.c`. The CRC is the only
delimiter the protocol actually provides, so use it as the backstop:

1. Take the layout length if the frame checksums there — the normal path.
2. **If the table has NO entry (`layout == 0`), try the whole read** — the
   documented "one frame per write" behaviour — before scanning anything.
3. Otherwise scan `[ME_FRAME_MIN_LEN, avail]` **shortest-first** for a length
   whose trailing two bytes are its own CRC.
4. Otherwise return the layout length unchanged — exactly the old behaviour, so a
   genuinely corrupt frame is still reported as corrupt.

**Step 2 is not cosmetic — it was added after code review caught a Critical
defect in the first version of this ADR.** `layout == 0` and a wrong non-zero
`layout` are *different states*: the first means "the protocol encodes no length
and the table correctly has no opinion" (`0xAA`, `0xA0`, unimplemented `0xBB`
queries), the second means "the table has an opinion which is probably wrong".
Conflating them made the scan the **primary** path for every ordinary `0xAA`
frame, where a coincidental short CRC match inside its own payload would truncate
it and orphan the remainder — reproducing the very desync this ADR exists to
prevent, from the opposite direction. It also made `*out_scanned` unconditionally
true for `0xAA` (`total != 0` always), so the "fix your table" WARN fired on every
config frame. Regression tests:
`test_resolve_len_does_not_truncate_on_a_planted_short_crc` and
`test_resolve_len_takes_a_lengthless_frame_whole`.

`*out_scanned` is therefore `(layout != 0 && total != layout)` — a real entry that
was really wrong, and nothing else.

`*out_scanned` is set only when step 2 disagreed with the table, and
`consume_rx_buffer()` logs that at **WARN naming both lengths**. Recovery must not
be silent, or the table stays wrong forever.

**Why shortest-first:** a longer coincidental match would swallow the following
frame whole. Shortest-first can only ever truncate one frame; longest-first can
consume several.

**Why declared lengths are never scanned** (`length_is_declared()`): `0xDD` is
fixed-size and `0xBB` Q4 states its own payload length. Their payloads are program
step data, in which a coincidental CRC match could place a boundary *inside* a
step. For those a checksum failure is reported honestly rather than papered over.

**Consequences:**
- ✅ The `0xEE` Start bug cannot recur for any frame type, including ones not yet
  discovered — which is the point. Adding one more table entry would have fixed
  only this frame.
- ✅ Coalesced `0xAA` frames can now be split. Previously the protocol gave no
  boundary at all and the whole read became one frame (see the old note on
  `me_frame_expected_len()` returning 0).
- ⚠️ **A short prefix could coincidentally checksum** — roughly 1 in 65536 per
  candidate length. Now reached only after steps 1 and 2 have both failed, so the
  frame was already unparseable by the old rules; a truncation costs nothing extra.
- ⚠️ **A partially-received frame is the residual risk.** If `avail` is short and
  some prefix verifies, the scan returns it instead of waiting. Steps 1 and 2
  mitigate it; it is not eliminated. This cannot bite a frame type with a correct
  table entry, which is the argument for finishing **T-44**.
- ⚠️ Cost is O(avail) CRC computations on a frame that fails step 1. Bounded by
  `ME_RX_BUF_SIZE` and only on the error path, so it cannot slow the normal one.

**Alternatives rejected:**
- *Just fix the table entry for Q1* — fixes one frame and leaves the mechanism
  that turns a documentation gap into a connection-wide desync fully intact.
- *Resynchronise by scanning for the next known start byte* — `0xAA`, `0xBB`,
  `0xCC`, `0xDD`, `0xEE` all occur inside payloads (`0xAA`/`0x55` are the step
  chain's own markers, ADR-14), so this finds false starts constantly.
- *Flush the buffer on any CRC failure* — loses the frames behind the bad one and
  cannot distinguish corruption from a mis-split.

**Related**
- Supersedes: none (amended in place by session #9, see amendment above)
- Relates to: ADR-4, ADR-14, ADR-22, T-41, T-42, T-44
