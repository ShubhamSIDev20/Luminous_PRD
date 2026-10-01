# ADR-9: CRC goes on the wire BIG-ENDIAN (high byte first) — supersedes ADR-5
> Date: 2026-08-10 | Session: #3 | Status: Accepted — **✅ HARDWARE-VERIFIED 2026-08-10 09:38 UTC, both directions**
> File: `DECISIONS/2026-08-10_crc-big-endian-supersedes-adr5.md`

---

> **Verification.** Request CRC `0x369E` transmitted as `36 9E`; response CRC
> `0xBE15` received as `BE 15`. Both recomputed independently from the captured
> frames. The Web Application team changed the **server** to emit
> `CRC_HI CRC_LO` the same day — earlier captures showed `15 BE`. Both ends are
> now big-endian and the TX/RX split described below was never needed.

**Context:** The developer specified that the CRC trailer must be transmitted
**high byte first**. The frame the board was emitting ended `... E2 BE AD` for a
CRC-16/Modbus value of `0xADBE` — low byte first. The required frame ends
`... E2 AD BE`.

This reverses ADR-5's 2026-08-07 resolution, which had recorded the order as
little-endian in both directions on the strength of a successful hardware
registration.

**Decision:** `ME_CRC_ORDER_DEFAULT` in `src/proto/crc16.h` is
`ME_CRC_ORDER_BE`, and it is the **single source of truth**. `sys_init.c`, the
`--crc-order` help text and `deploy.ps1`'s `-CrcOrder` all resolve through it
rather than spelling the order out again. The runtime `--crc-order` switch is
retained as a diagnostic, unchanged.

**Reason:** `Docs/bm_device_registration_v5.0.md` writes the trailer as
`CRC_HI CRC_LO`, and ADR-4 already established that document as authoritative
where it conflicts with `WebAppDocs/ICD.md` §3.2 ("appended Little Endian").
This change makes the code consistent with the authoritative document and with
the developer's explicit instruction.

The ADR-5 evidence does not block this, because it was weaker than it read: a
successful registration only proves the server did not *reject* the request —
it proves nothing if the server never validates the request CRC. The three
scattered `"le"` defaults were also a latent defect in their own right; they are
now one constant.

**Impact:**
- ✅ Requests match `bm_device_registration_v5.0` and the developer's spec
- ✅ The exact 33-byte hardware frame is pinned as a golden vector in
  `tests/test_reg_frame.c`, so the trailer cannot silently regress
- ✅ One constant now governs every default; the three-copy drift risk is gone
- ✅ **Resolved by coordination, not code.** The server was observed replying
  little-endian twice on 2026-08-10 (`15 BE`), which would have broken the
  response direction. The Web Application team changed the server to
  `CRC_HI CRC_LO` the same day, and the 09:38 UTC run confirms `BE 15`. Both
  ends are big-endian; the TX/RX split was never needed. It remains the
  fallback if the server ever diverges again — `me_config_t` carries a single
  `crc_order`, and the pack and parse call sites each take the order as a
  parameter, so the split is small.

**Related**
- Supersedes: ADR-5
- Relates to: ADR-4, ADR-10
