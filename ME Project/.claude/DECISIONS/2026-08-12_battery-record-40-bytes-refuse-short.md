# ADR-21: The battery record is 40 bytes, and a short payload is refused
> Date: 2026-08-12 | Session: #9 | Status: Accepted — not hardware-verified
> File: `DECISIONS/2026-08-12_battery-record-40-bytes-refuse-short.md`

---

**Context:** The developer asked for battery data to be stored per Secondary/Channel
"the same way you store program data". Investigation found the storage path already
existed and worked — `route_frame()` → `q_data` → `me_battery_parse()` →
`me_store_battery_set()` → `g_battery[slot]` — and the developer's own hardware log
already showed `battery stored: 1.0 Ah, 1 cells, 1.0 V max`.

**The real defect was narrower and worse.** The frame on the wire is 46 bytes:
4 header + **40** payload + 2 CRC. `ME_BATTERY_PAYLOAD_LEN` was **22**, taken from
the legacy `storeBatteryData()` in `BTS_Primary_SOM`. And `me_battery_parse()`
documented itself as tolerating a longer payload *"so a protocol revision does not
break it"*. So **18 bytes — five real fields — were silently discarded on every
battery packet, with no warning anywhere.** A defensive rule had quietly become a
data-loss rule the moment the Web Application sent more than the old firmware did.

`Ref Docs/bm_config_v6.0.md` (ADR-20) named them: Impedance, Break Voltage, Nominal
Voltage, Energy Density, Battery ID.

**Decision:**
1. `ME_BATTERY_PAYLOAD_LEN` 22 → **40**; add `ME_BATTERY_FRAME_LEN` 46;
   `me_battery_t` widens from 7 fields to **12**.
2. **A payload under 40 is REFUSED, not half-parsed** (developer's choice when
   asked). `data_mgr.c` WARNs naming both lengths *and the document*, and
   `route_frame()` answers Q5 with `0x00`.
3. A payload **over** 40 is still accepted with the extra ignored, but now WARNs —
   it means the protocol moved.
4. Impedance and Energy Density are parsed as **`float`** — developer-confirmed
   2026-08-12, see ADR-20.

**Why refuse rather than partially populate:** a record with `valid = true` and a
zeroed nominal voltage would let a battery test execute against nonsense. The
failure would surface as a wrong test *result*, not as an error. Refusing makes the
cause nameable from one log line. The risk accepted in exchange: if the Web
Application ever sends the legacy 22-byte form, packets that used to be accepted now
fail — loudly, which is the point (T-46).

**`circuit_store.c` needed no change at all**, because it holds `me_battery_t` **by
value**. The per-circuit record simply got wider and the CircuitID→slot mapping
(ADR-13) kept working untouched. That is the payoff of storing a struct rather than
a serialised blob.

**Consequences:**
- ✅ Nothing is discarded; all 12 fields reach program execution
- ✅ Battery and program for a circuit share the same slot mapping, as asked
- ✅ Both the Data Manager and Core Logic log all 12 fields, so a wrong test result
  can be checked against the record it ran on
- ⚠️ The 22-byte form is now a hard failure (T-46)
- ⚠️ No range checking. The captured frame carried 1.0 Ah / 1 cell / 1.0 V, all
  outside the document's stated ranges, and was stored as-is. Validation is a
  separate decision nobody has asked for yet

**Related**
- Supersedes: the legacy 22-byte `ME_BATTERY_PAYLOAD_LEN`
- Relates to: ADR-13, ADR-20, T-46
