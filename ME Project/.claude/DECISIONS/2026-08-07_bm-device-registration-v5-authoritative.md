# ADR-4: `bm_device_registration_v5.0` is authoritative; ICD §3.4 response is wrong
> Date: 2026-08-07 | Session: #2 | Status: Accepted
> File: `DECISIONS/2026-08-07_bm-device-registration-v5-authoritative.md`

---

**Context:** Three descriptions of the registration frame existed and they did
not agree. ICD §3.2 shows a generic header `Start │ DeviceID │ CircuitID │
QueryID`; ICD §3.4 shows `Start │ SubCmd │ Reserved │ DeviceID │ CircuitID`;
the developer supplied a third reading in which byte 2 is a payload length.

**Decision:** `Docs/bm_device_registration_v5.0.md` plus the developer's
clarification is authoritative. The request layout is
`Start │ QueryID │ Length │ DeviceID │ CircuitID │ Name[16] │ IP[4] │ MAC[6] │
CRC[2]` = 33 bytes, and the **response is 7 bytes with a CRC**.

**Reason:** The length byte settles the field order by arithmetic rather than
preference: it counts DeviceID through MAC, which is `1+1+16+4+6 = 28 = 0x1C`,
and `3 + 28 + 2 = 33`. That is self-consistent only if DeviceID begins at offset
3. Under ICD §3.2's ordering there would be no length field at all. ICD §3.4's
*offsets* are therefore right, though its field names are not.

**ICD §3.4 is wrong about the response.** It documents 5 bytes with no CRC; the
real response is 7 bytes carrying a CRC-16 over bytes 0–4.

**Impact:**
- ✅ Field order is settled by evidence, not judgement
- ✅ The length byte is *computed* from field sizes in `proto_defs.h`, never
  hardcoded as `0x1C`, so it cannot drift
- ⚠️ **`WebAppDocs/ICD.md` §3.4 still contains the wrong response layout**
  (5 bytes, no CRC). The developer decided on 2026-08-07 **not** to correct it.
  Any future module built from that document inherits the error — always check
  `Docs/bm_device_registration_v5.0.md` first. Do not re-propose fixing the ICD.
- ⚠️ The registration payload's own detail sheet is still only in the source
  Excel; the 16/4/6 field sizes come from ICD §3.4 and agree with `LEN = 28`

**Related**
- Supersedes: none
- Relates to: ADR-9, ADR-20, T-8
