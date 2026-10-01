# VNC / ACNx / Battery-Parameter Nominal Values — Manual Reference

> Curated from `docs/BM_Manual_eng.pdf` ("Battery Manager User Manual", v99.3, 363 pages).
> Source: full plain-text extraction at `docs/manual-extract/BM_Manual_eng.txt` (via `pdftotext -layout`, 363 form-feed-delimited pages, ~500KB). Re-grep that file for anything not captured here — do not re-parse the PDF.
> Compiled: 2026-09-04, for the VNC/ACN5 Program Editor feature request. Page numbers below are the manual's own printed page numbers (visible in each excerpt), not PDF page indices.

## Why this file exists
The manual is ~363 pages; only a handful describe the battery-relative nominal-value/limit units (`VNC`, `ACNx`, `CNom`, and the wider `INTERN[]` battery-parameter table). This file collects every relevant excerpt with page numbers so future work never needs to re-scan the whole PDF. If a new question comes up that isn't answered here, search `BM_Manual_eng.txt` first (it's plain text, `grep -n -i "<term>" docs/manual-extract/BM_Manual_eng.txt`); only re-run `pdftotext` if the PDF itself changes.

---

## 1. Definitions (Battery Dialog fields feeding these units) — p.26-27

> "**Nominal Capacity** (BM.ini entry: UseCNom=TRUE): Enter the nominal capacity of the battery... The nominal capacity is required for using CNom (as limit or registration) and ACN or ACNX (as nominal value, limit or registration) in programs. ACN represents a current depending on the nominal capacity. ACNX represents a current depending on the nominal capacity and the time X hours and is not available for all systems."

> Example (p.26): "Step 2: If the nominal capacity is 1 Ah, the battery is charged with 1 × 1 Ah / 5 h = 0.2 A. A registration occurs every 0.2 × 1 Ah / 5 h = 0.04 A." — confirms bare `ACN` defaults to the **5-hour** divisor (same as `ACN5`).
> "Step 4: This step is terminated when 80% of the nominal capacity is reached." — a `CNom`-based **limit**, expressed as a percentage of nominal capacity directly in Ah (no time divisor).

> "**Number of Cells** (BM.ini entry: UseCell=TRUE): Enter the number of cells for the battery. The number of cells is required when using VNC (as a nominal value or limit) in programs."

> Example (p.27): "If the battery has 6 cells, the step is terminated as soon as the voltage reaches 2.48 × 6 = 14.88 V."

(Identical text repeats at p.~60-61 in a second pass over the same dialog — same content, no new info.)

---

## 2. Section 12.3 "Using Battery Parameters" — p.159-160

Full `INTERN[]` battery-parameter table (usable as **bare channel names**, not just via the `ACN`/`VNC` multiplier syntax, directly in the **Nominal Value**, **Limit**, and **Registration** columns):

| Channel   | Unit     | Explanation |
|-----------|----------|-------------|
| INTERN[17]| `CNom`   | nominal capacity |
| INTERN[18]| `NoCell` | number of cells |
| INTERN[19]| `UGas`   | gassing voltage |
| INTERN[20]| `UMax`   | maximum voltage |
| INTERN[21]| `INom`   | nominal current |
| INTERN[22]| `ICrank` | cold cranking current |
| INTERN[23]| `ChargeF`| charge factor |
| INTERN[24]| `Rin`    | internal resistance |
| INTERN[25]| `CutOff` | final voltage, switch-off voltage |
| INTERN[26]| `UNom`   | nominal voltage |
| INTERN[27]| `EDensity`| energy density |

> "Channel units can be used in the columns Nominal Value, Limit and Registration. You cannot use channel names in programs." (i.e. you write `INom` as a unit-like token, not `INTERN[21]`.)

Example 1 (global limit): "During the whole program run, each time a current greater than INom or a voltage greater than UGas is detected, error 1 will be displayed and the program will be interrupted."
Example 2 (nominal value): "the Ah counter is set to the nominal capacity CNom of the battery used."

**Scope note:** the user's ask is specifically VNC/ACN5. This full `INTERN[]` table is a superset — everything in it maps 1:1 to a `BatteryDTO` field (see §5) and could be implemented by the same mechanism later, but is **out of scope** for the initial VNC/ACN5 plan unless requested.

---

## 3. Nominal Value tables — Section 12.4.3, p.170-172

### Standard functions (p.170) — for reference, not battery-relative
| Function | Example | Description |
|---|---|---|
| Current | `1.0 A` | constant current |
| Voltage | `14.4 V` | constant voltage |
| Power | `1000 Watt` | constant power |
| Resistance | `1 Ohm` | constant resistance |

### Supplementary (battery-relative) functions — p.170-171
> "The functions are based on the battery data provided by columns Nom. Capacity and Cells of the battery definition."

| Function | Example | Description |
|---|---|---|
| 5-hour discharge current, a.k.a. **"C5"** | `10 ACn5` | "With a nominal capacity of e.g. 100 Ah — constant current control to 10×(100 Ah / 5 h) = 200 A." |
| Cell Voltage | `8 ACn1`, `8 ACn2`, `8 ACn4`, `8 ACn10`, `8 ACn20` | "Current eight times the nominal capacity / **1** [or 2, 4, 10, 20] as regulating quantity" — i.e. `value × (NominalCapacity / X hours)`, X ∈ {1,2,4,5,10,20} |
| — | `2.35 VnC` | "With a cell number of e.g. 60 the voltage is regulated to 2.35 V/cell × 60 cells = 141 V." |

(The table's OCR/layout in the PDF interleaves the `ACn1/2/4/10/20` rows with their descriptions out of strict order — the formula is unambiguous from the two worked examples above and from §4/§5 below.)

### Worked example No. 6 (p.186) — "Programs with battery parameters"
> "the nominal capacity being 100 Ah and the battery being constructed of 6 cells. The current of 1.0 Acn5, supplied for five hours, then equals 20 A, 0.05 Acn5 equals 1 A, a cell voltage of 2.35 V per cell equals 14.1 V battery voltage."

This is the canonical proof of the formula: `100 Ah / 5h = 20A`, so `1.0 ACn5 = 20A`, `0.05 ACn5 = 1A` — linear scaling confirmed. `2.35 VNC × 6 cells = 14.1V`.

> "Using battery parameters allows you to use your programs for multifold applications." — **the whole point of VNC/ACNx is that a program is written once, battery-agnostic, and re-scales automatically to whichever battery is selected when it's transferred to a channel.** This is the direct textual basis for "calculations performed using the selected battery... when transferring to a channel."

---

## 4. VNC/ACN5/CNom as Limits — p.174-175 (Section 12.4.8, the Limit operators table)

These same tokens also appear as **limit units** (terminate-the-step conditions), not just nominal (target) values:

> `ACN5` — "Current referred to the nominal capacity at 5 hours discharge"
> `VNC` — appears in the terminating-limits list alongside `VBATT`, `WATT`, `PERCCN_P`, `PERCCN_C` — "the present program step is terminated" when the limit condition is met. `VNC`'s row text reads "Voltage value referred to the nominal voltage of a cell" (i.e. `limit_value × NumberOfCells` compared against actual voltage, same scaling as the nominal-value use).
> `PERCCN_P` / `PERCCN_C` — terminate when the *previous*/*current* step's calculated capacity reaches a given **percentage of nominal capacity** (a `CNom`-relative percentage limit, distinct from the `ACNx` current-scaling family).
> `PercAh` — "the following action will be carried out when..." a percentage-of-Ah-counter condition (used with the PAU operator per p.175's closing note: "The special Limits AhDef, PerAc, PERCCN_P and PERCCN_C can be set only while using the pause operator (PAU)").

**Implication for implementation:** the value-scaling logic (battery-aware unit resolution) must run in **both** the nominal-value code path and the limit code path — see the app-side findings in §6.

---

## 5. Mapping to `BatteryDTO` (this app's model — `Models/DTOs/BatteryDTO.cs`)

| Manual token | Manual meaning | `BatteryDTO` field | Type |
|---|---|---|---|
| `CNom` | nominal capacity (Ah) | `NominalCapacity` | `float` |
| `NoCell` | number of cells | `NumberOfCells` | `int` |
| `UGas` | gassing voltage | `GassingVoltage` | `float` |
| `UMax` | maximum voltage | `MaximumVoltage` | `float` |
| `INom` | nominal current | `NominalCurrent` | `float` |
| `ICrank` | cold cranking current | `ColdCrankingCurrent` | `float` |
| `ChargeF` | charge factor | `ChargeFactor` | `float` |
| `Rin` | internal resistance | `Impedance` | `float` |
| `CutOff` | final/switch-off voltage | `BreakVoltage` | `float` |
| `UNom` | nominal voltage | `NominalVoltage` | `float` |
| `EDensity` | energy density | `EnergyDensity` | `float` |

Every field the VNC/ACNx formulas need (`NominalCapacity`, `NumberOfCells`) already exists on `BatteryDTO` — no schema/migration change needed for the in-scope feature.

---

## 6. Formulas to implement (VNC/ACNx only — in-scope for this feature)

```
ACN   (bare, no suffix)  = value × (battery.NominalCapacity / 5)     // 5h implied default
ACN1  = value × (battery.NominalCapacity / 1)
ACN2  = value × (battery.NominalCapacity / 2)
ACN4  = value × (battery.NominalCapacity / 4)
ACN5  = value × (battery.NominalCapacity / 5)   // == "C5" in the manual's own words
ACN10 = value × (battery.NominalCapacity / 10)
ACN20 = value × (battery.NominalCapacity / 20)
  → resolves to an ordinary Current (A) value once computed.

VNC (a.k.a. "VnC") = value × battery.NumberOfCells
  → resolves to an ordinary Voltage (V) value once computed.
```

Both resolve to plain `A`/`V` *before* reaching the existing wire-encoding helpers (`ExtractFloatAsByteArraySafe`, `TryParseUnit` in `Services/ProgramBuilder.cs`) — the physical protocol has no native concept of `ACNx`/`VNC`; those are purely a program-authoring convenience layer above the existing Ampere/Volt wire format. Confirmed by the manual itself ("the current of 1.0 Acn5 ... equals 20 A") and by this app's `ProcessNominalValues`/`ExtractFloatAsByteArraySafe` (no unit-scaling exists there today — see the implementation plan for the exact gap).

---

## 7. Out of scope for this feature (but documented for later)

- ~~The full `INTERN[]` table (§2) beyond `CNom`/`NoCell`~~ — **implemented 2026-09-08**, see `docs/Battery-Parameters-Intern-Table-Plan.md`. `CNom`, `NoCell`, `UGas`, `UMax`, `UNom`, `CutOff`, `INom`, `ICrank`, `ChargeF`, `EDensity` are all usable as bare identifiers now (`Utils/BatteryUnitResolver.GetBatteryGlobalVariables`). `Rin` (internal resistance) remains excluded — see below.
- **Ramp and Resistance — explored 2026-09-08, found genuinely blocked, not just out of scope.** Unlike `ACNx`/`VNC` (a pure unit conversion resolved before encoding), both would need a *new wire-format capability*: `docs/PROTOCOL.md` §15.7 defines nominal values as a flat list of untyped `[Value 4B BE]` floats — the *operator code itself* (`CC_CHG`/`CV_CHG`/`CP_CHG`/...) is what tells the firmware which physical quantity to regulate, there's no per-value type tag. §15.8 (`CutoffCondition`) and §15.11 (`RegistrationType`) list Current/Voltage/Power/Capacity/Energy/Temperature/Time — **no Resistance byte exists anywhere**, and §18.4's 20 defined opcodes have no `CR_CHG`/`CR_DCHG` (constant-resistance) equivalent. Ramp has no wire convention either (no start/end/duration triple, no ramp flag) and no real support in `HardwareSimulator` (the one "ramp" hit there is an unrelated telemetry-cosmetics helper). Both would require real firmware capability plus new operator/cutoff/registration byte values — a protocol change, not a Program Editor change. `Rin` (`Impedance`) inherits this same gap, which is why it's excluded from the `INTERN[]` work above.
- `PERCCN_P` / `PERCCN_C` / `PercAh` / `AhDef` (percentage-of-capacity limits, PAU-operator-only per p.175).
- Parallel functions, `Factor_I/P/U`, `OUW`, `CGRE`, `CLESS`, `TCONTR` (pp.171-172) — unrelated to battery-relative scaling.
