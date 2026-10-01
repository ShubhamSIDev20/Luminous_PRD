# T-47: Battery Impedance and Energy Density confirmed float
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #9)
> File: `tasks/2026-08-12_battery-impedance-energy-density-float-confirmed.md`

---

## Description
Developer decision, and they will instruct the Web Application team to send
float only. These were the only two 4-byte battery fields
`Config Data Frame Format V6.0.xlsx` does **not** annotate "It will be in
float", and its samples (`00 00 00 64` labelled "100 Ohm") decode sensibly
only as `uint32`; the captured frame carried `3F 80 00 00` / `40 00 00 00`
for fields the operator set to 1 and 2.

## Why / Context
The parser had already chosen float to match the wire, so **no code
changed** — only the documents and comments that described it as open. Both
encodings are 4 bytes wide, so the wrong choice would have produced a
nonsense value and **never a parse error**; that is why it was escalated.
Pinned by `test_impedance_and_energy_density_are_floats()`. `bm_config_v6.0.md`
§9.3, ADR-20.

## Progress Log
- **2026-08-12** (Session #9): Confirmed float by developer; test pinned.

## Related
- Relates to: ADR-20, T-25
