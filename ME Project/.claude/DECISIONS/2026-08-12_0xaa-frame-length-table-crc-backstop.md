# ADR-22: `0xAA` gets real frame-length entries; the CRC becomes its backstop
> Date: 2026-08-12 | Session: #9 | Status: Accepted — not hardware-verified
> File: `DECISIONS/2026-08-12_0xaa-frame-length-table-crc-backstop.md`

---

**Context:** `me_frame_expected_len()` returned **0** for every `0xAA` frame, with
the comment *"0xAA encodes no length anywhere in the frame"*. That is true — and it
conflated two different things. The protocol encodes no length, but the **layout**
fixes each query's size, and until `bm_config_v6.0.md` existed nobody knew what
those sizes were.

**Decision:** Fill in the table from `bm_config_v6.0.md`:

| Query | Length | Source |
|---|---|---|
| Q1–Q4 | 6 | header + CRC, no payload (§3) |
| **Q5** | **46** | 4 + 40-byte battery payload + 2 (§5.3, **confirmed against a captured frame** §5.4) |
| Q6 | 10 | header + 4-byte epoch + CRC (§3.6) |
| **Q7–Q10** | **0** | broadcast, **2-byte header** — not derivable by this arithmetic (§9.1) |

`frame_router.c` now includes `battery_frame.h` for `ME_BATTERY_FRAME_LEN` — pure
module including pure module — so the 40 stays defined in exactly one place.

**Why Q7–Q10 deliberately stay 0:** their header is `Start | QueryID`, with no
DeviceNumber and no CircuitNumber. Every `0xAA` path in this codebase assumes the
4-byte header, so their length cannot be computed the way the others are. Returning
a confidently wrong number is worse than admitting ignorance and letting the CRC
decide. For the same reason they are **not** given names in `proto_defs.h`.

**This change caused a regression, and finding it was the most valuable part.**
Adding the correct 46-byte Q5 entry made a *short* `0xAA` frame parse **worse** —
see the amendment at the top of ADR-19. A correct entry is not automatically a safe
one.

**Consequences:**
- ✅ `0xAA` frames now split by layout rather than by CRC scan: cheaper, and it
  cannot stop at a coincidental checksum inside a 40-byte payload
- ✅ Every `0xAA` query except the broadcasts has a real boundary, so ADR-19's scan
  becomes a genuine backstop for this group instead of its primary path
- ⚠️ Q1–Q4 = 6 and Q6 = 10 come from the document, **not** from a captured frame.
  Only Q5 = 46 is wire-confirmed. A wrong entry now WARNs and recovers (ADR-19), so
  this is visible rather than fatal — grep the hardware log for
  `RECOVERED, but the length table … needs fixing` (T-44)
- ⚠️ A `0xAA` broadcast frame is still mis-parsed as circuit `0x01` and dropped by
  admission control. Safe, but by accident (T-48)

**Related**
- Supersedes: none
- Relates to: ADR-19, ADR-20, T-44, T-48
