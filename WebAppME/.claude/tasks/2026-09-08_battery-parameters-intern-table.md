# T-50 — §12.3 Battery Parameters + Ramp/Resistance Exploration

> Status: ✅ Done — 2026-09-08
> Session: [sessions/2026-09-08_1000_battery-parameters-intern-table.md](../sessions/2026-09-08_1000_battery-parameters-intern-table.md)
> Plan: [docs/Battery-Parameters-Intern-Table-Plan.md](../../docs/Battery-Parameters-Intern-Table-Plan.md) (approved as written, no deviations)
> Follow-on from: T-49 (VNC/ACN5)

## Request
Explore "Ramp and Resistance" and "12.3 Using Battery Parameters" from `docs/BM_Manual_eng.pdf` (already extracted for T-49) and check how to add them to the Program Editor **without changing `ProgramBuilder`'s wire-encoding byte protocol**, the same way VNC/ACN5 were added.

## Findings

**Ramp — blocked.** `docs/PROTOCOL.md` §15.7: nominal values are a flat list of untyped floats; the operator code alone tells the firmware what quantity to regulate. No ramp convention (start/end/duration, a ramp flag) exists anywhere in the spec; `HardwareSimulator` has no real ramp support either. A ramp needs new firmware/wire capability, not a client-side resolve step — out of scope for a Program-Editor-only change.

**Resistance — blocked, same root cause.** §15.8/§15.11 (`CutoffCondition`/`RegistrationType`) list Current/Voltage/Power/Capacity/Energy/Temperature/Time — no Resistance byte anywhere, and none of the 20 defined opcodes (§18.4) is a constant-resistance mode. Regulating to resistance is a distinct firmware control algorithm; there's no existing operator to redirect a resistance value into.

**§12.3 "Using Battery Parameters" — works.** 10 of 11 `INTERN[]` tokens map to wire-supported quantities and are used *bare* (no multiplier, unlike ACNx/VNC) — exactly what `ProgramBuilder`'s existing `SET`-variable substitution already does. `Rin` excluded (inherits the Resistance blocker).

## What shipped
- `Utils/BatteryUnitResolver.cs`: `InternVariableNames` + `GetBatteryGlobalVariables(BatteryDTO)` → the 10 tokens as `GlobalVariable` entries.
- `Services/DecoderService.cs`: one `AddRange` in `ConvertProgramIntoBytesPackets`, `SET`-variable wins on name collision.
- `Components/Pages/Programs/ProgramEditor.razor`: placeholder `GlobalVariable`s (Value="0") for editor-time validation with no battery selected, added after the SET-duplicate-name check.
- `Components/UI/Program/NominalValuesEditor.razor`: fixed a second `AutoCorrect` bug — the matched-variable branch forced `unit = field.Unit` unconditionally, producing a misleading `"CNom A"` display for a variable whose real unit (Ah) has no matching Nominal Value field. Now never appends a suffix to a matched variable (its unit is intrinsic to its definition); confirmed no encode-time behavior change for the correctly-matching tokens.

## Tests
770/770 passing (up from 751): 3 new in `BatteryUnitResolverTests.cs`, 5 new in `ProgramToBytePacketTests.cs`, 6 new in `ValidationHelperAcnVncTests.cs`.

## Docs updated
`docs/manual-extract/VNC-ACN-battery-parameters.md` §7 (INTERN table struck through as done, Ramp/Resistance findings recorded with full technical justification), `.claude/CONTEXT/api/device.md`, new `docs/Battery-Parameters-Intern-Table-Plan.md`.
