# ADR-5: CRC byte order is runtime-selectable, defaulting to little-endian
> Date: 2026-08-07 | Session: #2 | Status: **SUPERSEDED by ADR-9 (2026-08-10) — the default is now BIG-ENDIAN**
> File: `DECISIONS/2026-08-07_crc-byte-order-runtime-selectable-le-default.md`

---

> **⚠️ The 2026-08-07 resolution below was overturned on 2026-08-10.** It is kept
> for the reasoning trail, because the flaw in it is instructive. See ADR-9.

> **Superseded resolution (2026-08-07).** Registration succeeded against the real
> Web Application (`BatteryTestingSystem`) with **no `--crc-order` flag**, i.e.
> on the LE default. It could not have succeeded on a wrong order: the request
> CRC would have failed the server's check, and `me_reg_parse_response` rejects
> any response whose CRC does not validate. Both directions were therefore
> recorded as little-endian.
>
> **Where this went wrong:** the request-direction half of that argument assumed
> the server *validates* the request CRC. A server that ignores or does not
> check it accepts either order silently, which makes a successful registration
> no evidence at all about the request direction. Only the **response**
> direction was genuinely proven, because the board's own parser strictly
> verified it. See ADR-9.
>
> The `--crc-order` flag is **kept**: it costs almost nothing, it documents the
> ambiguity for anyone who reads the source documents and reaches the opposite
> conclusion, and it remains a one-command diagnostic if another command group
> ever behaves differently. Do not remove it without a reason.

**Context:** The two source documents disagree. `WebAppDocs/ICD.md` §3.2 states
the CRC is "appended Little Endian"; `Docs/bm_device_registration_v5.0.md`
writes the trailer as `CRC_HI CRC_LO`, i.e. big-endian. No arithmetic settles
it, and the payload-detail sheet lives in an Excel file not in the workspace.

**Decision:** Byte order is a parameter (`me_crc_order_t`), defaulting to
little-endian, overridable at runtime via `--crc-order be`.

**Reason:** Either order yields a well-formed frame, so a wrong guess fails only
inside the server's checksum test — expensive to diagnose over a socket.
Little-endian is the default because the ICD is explicit and it is the Modbus
RTU convention. Making it a runtime flag turns a rebuild-and-redeploy cycle into
a single re-run. This is not speculative complexity: the ambiguity is
documented, not hypothetical.

**Impact:**
- ✅ A convention mismatch costs one re-run, not a debugging session
- ✅ On a response CRC failure the program prints the computed CRC alongside the
  value read both ways, and says outright when big-endian would have matched
- ⚠️ Once the real Web Application confirms the order, record it here and
  consider making it fixed

**Related**
- Supersedes: none
- Superseded by: ADR-9
- Relates to: ADR-9
