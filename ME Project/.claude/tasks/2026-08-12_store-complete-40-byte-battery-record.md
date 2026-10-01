# T-50: Store the complete 40-byte battery record per Secondary/Channel
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #9)
> File: `tasks/2026-08-12_store-complete-40-byte-battery-record.md`

---

## Description
`me_battery_t` 7 → 12 fields (impedance, break voltage, nominal voltage,
energy density, battery ID). **The 18-byte tail was being silently discarded
on every battery packet** because the parser "tolerated a longer payload". A
payload under 40 is now refused and NACKed rather than half-stored.
`circuit_store.c` needed no change — it holds the struct by value. Both Data
Manager and Core Logic log all 12 fields. Test-first; the captured 46-byte
frame is asserted literally. ADR-21.

## Progress Log
- **2026-08-12** (Session #9): Implemented. **NOT hardware-verified.**

## Related
- Relates to: ADR-21, T-46
