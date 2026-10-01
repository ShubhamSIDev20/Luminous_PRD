# §12.3 "Using Battery Parameters" — Implementation Plan

> Status: **Implemented 2026-09-08** (approved as written; no deviations). 770/770 tests passing, `dotnet build` clean.
> Follows on from `docs/VNC-ACN5-Implementation-Plan.md` (implemented, merged to `main`). Manual reference: `docs/manual-extract/VNC-ACN-battery-parameters.md` §2, §5.
> Scope: the 10 `INTERN[]` battery-parameter tokens usable directly as bare identifiers — **not** `Rin` (internal resistance), which is blocked by the same missing-Resistance-support gap documented for the Ramp/Resistance exploration (no `CutoffCondition`/`RegistrationType` byte exists for Ω anywhere in `docs/PROTOCOL.md`).

## 1. What this is

Manual §12.3 (p.159-160): 11 battery fields are usable as **bare identifier tokens** — no multiplier number in front, unlike `ACNx`/`VNC` — directly in the Nominal Value, Limit, and Registration columns. The manual's own examples:

> *"During the whole program run, each time a current greater than `INom` or a voltage greater than `UGas` is detected, error 1 will be displayed."* (global limit)
> *"the Ah counter is set to the nominal capacity `CNom` of the battery used."* (nominal value)

| Token | Battery field | Wire-supported type |
|---|---|---|
| `CNom` | `NominalCapacity` (Ah) | ✅ (already used by `ACNx`) |
| `NoCell` | `NumberOfCells` | ✅ plain count (already used by `VNC`) |
| `UGas` | `GassingVoltage` | ✅ Voltage |
| `UMax` | `MaximumVoltage` | ✅ Voltage |
| `UNom` | `NominalVoltage` | ✅ Voltage |
| `CutOff` | `BreakVoltage` | ✅ Voltage |
| `INom` | `NominalCurrent` | ✅ Current |
| `ICrank` | `ColdCrankingCurrent` | ✅ Current |
| `ChargeF` | `ChargeFactor` | ✅ plain ratio, no wire unit needed |
| `EDensity` | `EnergyDensity` | ✅ plain float, no wire unit needed |
| ~~`Rin`~~ | ~~`Impedance`~~ | ❌ **excluded** — no Resistance byte anywhere in the protocol |

Every included token already maps to a quantity (`A`/`V`/plain float) the wire format already carries — same principle as `VNC`/`ACNx`: **resolve before encoding, change nothing about what goes on the wire.**

## 2. The key simplification: this reuses existing machinery, unlike VNC/ACN

`ACNx`/`VNC` needed a *formula* (`value × multiplier`) applied at the point of encoding, so `BatteryUnitResolver` had to be threaded as a new parameter through every `ProgramBuilder.Process*` method.

These 10 tokens need **no formula** — they're a straight name→value substitution, and `ProgramBuilder` already has exactly that mechanism for user-defined `SET` variables:

```csharp
// ProcessNominalValues (Services/ProgramBuilder.cs) — already does this today:
var gv = globalVariables.FirstOrDefault(e => string.Equals(e.Name, parts[0], StringComparison.OrdinalIgnoreCase));
if (gv != null) { value = gv.Value; unit = gv.Unit ?? string.Empty; }
```

`ProcessStandardLimit` and `AddRegistrations`/`TryParseRegistration` (via `AddRegistrations`'s pre-substitution step) do the identical lookup. **If the battery's values are injected as ordinary `GlobalVariable` entries, all three columns work with zero changes to `ProcessNominalValues`, `ProcessStandardLimit`, `ProcessPauLimit`, or `AddRegistrations`.** The `BatteryDTO? battery` parameter is already threaded everywhere it's needed, from the VNC/ACN work.

## 3. Design

### New: `BatteryUnitResolver.GetBatteryGlobalVariables(BatteryDTO battery)`
Returns the 10 tokens as `List<GlobalVariable>`:
```csharp
new GlobalVariable { Name = "CNom", Value = battery.NominalCapacity.ToString(CultureInfo.InvariantCulture), Unit = "Ah" },
new GlobalVariable { Name = "NoCell", Value = battery.NumberOfCells.ToString(CultureInfo.InvariantCulture), Unit = "" },
new GlobalVariable { Name = "UGas", Value = battery.GassingVoltage.ToString(CultureInfo.InvariantCulture), Unit = "V" },
// ... UMax, UNom, CutOff (all V), INom, ICrank (both A), ChargeF, EDensity (both unitless)
```
(`Unit = "Ah"` on `CNom` matches how the manual's own example uses it — "the Ah counter is set to CNom" — a plain Ah value, not an `ACNx`-scaled current.)

### One integration point: `DecoderService.ConvertProgramIntoBytesPackets` (base overload)
```csharp
List<GlobalVariable> globalVariables = ProgramBuilder.ExtractGlobalVariables(programStepsDTO);
if (battery != null)
{
    var batteryVars = BatteryUnitResolver.GetBatteryGlobalVariables(battery);
    // user-defined SET variables win on a name collision
    globalVariables.AddRange(batteryVars.Where(bv => !globalVariables.Any(gv => string.Equals(gv.Name, bv.Name, StringComparison.OrdinalIgnoreCase))));
}
```
That's the entire encoding-side change. Every downstream `Process*` method already resolves by name.

### Editor-side validation
`ValidationHelper.ValidateNominal`/`ValidateLimit` currently reject a bare identifier that doesn't match a `SET`-defined variable ("Variable X not defined in SET" / no match in the generic-field branch). Since programs are authored **without** a battery selected (same battery-agnostic philosophy as `ACNx`/`VNC`), the editor needs to recognize these 10 names as always-valid, independent of any real battery:

- `ProgramEditor.razor`'s `globalVariables` field (rebuilt from `SET` steps around the existing `RefreshGlobalVariables`-style logic) gets the same 10 names appended as **placeholder** entries (e.g. `Value = "0"`, correct `Unit`) purely so `ValidateNominal`/`ValidateLimit`'s existing variable-lookup (`FindVar`) succeeds. These placeholders are never encoded — real resolution only happens at transfer time via `BatteryUnitResolver.GetBatteryGlobalVariables` with the actually-selected battery.
- If a user's own `SET` step defines a variable with a colliding name (e.g. `(INom = 5 A)`), their `SET` definition must win in the editor's list too, for consistency with the encoder's precedence rule above.

No changes needed to `OperatorConstants.ValidUnits`/`NominalField.AcceptedUnits` — these tokens aren't units, they're variable names, so they flow through the *existing* generic-variable validation branch, not the unit-matching branch `ACNx`/`VNC` used.

## 4. Files touched

- `Utils/BatteryUnitResolver.cs` — add `GetBatteryGlobalVariables(BatteryDTO)` and a `public const`/array of the 10 recognized names (for the editor placeholder step).
- `Services/DecoderService.cs` — the one `AddRange` call above in `ConvertProgramIntoBytesPackets`.
- `Components/Pages/Programs/ProgramEditor.razor` — append placeholder entries to `globalVariables` alongside the `SET`-derived ones.
- `.claude/CONTEXT/api/device.md`, `docs/manual-extract/VNC-ACN-battery-parameters.md` — document once shipped; update the "out of scope" note to record `Rin` as the one remaining exclusion (Resistance gap) rather than the whole table.

## 5. Risks / edge cases

- **Name collisions with a `SET` variable**: resolved by "user-defined wins" precedence, both in the encoder and the editor's placeholder list (see above) — must be implemented identically in both places or editor validation and real encoding could disagree.
- **`NoCell`/`ChargeF`/`EDensity` in a Limit or Registration column**: these have no corresponding `CutoffCondition`/`RegistrationType` byte (they're not Current/Voltage/Capacity/Energy/Temperature/Time). `ProcessStandardLimit`/`TryParseRegistration` already default unmapped units to `RegistrationType.Time`/a falsy `TryParseUnit` respectively for *any* unrecognized unit — this is pre-existing silent-fallback behavior, not something this feature introduces, but it means a user writing `"> NoCell"` as a limit would get a nonsensical Time-tagged comparison instead of a clear error. Recommend flagging this as a known rough edge in the doc rather than adding new validation-time rejection logic (which would be extra, arguably out-of-scope defensive work not requested).
- **Case sensitivity**: manual writes `CNom`, `NoCell`, etc. with specific casing, but this app's variable lookups are already `OrdinalIgnoreCase` throughout — no special handling needed, consistent with how `ACNx`/`VNC` behave.
- **`Rin`/Resistance stays out of scope** — confirmed no wire support exists; do not attempt a workaround.

## 6. Verification plan

- New tests in `BatteryUnitResolverTests.cs`: `GetBatteryGlobalVariables` returns exactly the 10 expected names/values/units for a known `BatteryDTO`.
- Extend `ProgramToBytePacketTests.cs`: a step using bare `INom` as a Nominal Value / `"> UGas"` as a Limit / `NoCell` as a Registration encodes byte-identically to the equivalent plain-value step (same pattern as the `ACNx`/`VNC` byte-identity tests).
- New test confirming `SET`-defined variable precedence over an implicit battery variable of the same name.
- Editor-level: a `ValidationHelperAcnVncTests.cs`-style test (or a new file) confirming `"INom"`/`"UGas"`/etc. validate successfully as bare Nominal Value / Limit tokens even with no battery in scope (editor placeholder path).
- `dotnet build` + `dotnet test` clean, matching every other change in this repo.
