# Session — §12.3 Battery Parameters (bare INTERN[] tokens) + Ramp/Resistance exploration

> Started: 2026-09-08T10:00:00Z
> Status: Complete — explored, planned, approved, implemented.

## Goal
Follow-on from the VNC/ACN5 session (2026-09-04). User asked to explore two more manual features — "Ramp and Resistance" and "12.3 Using Battery Parameters" — and check how each could be added to the Program Editor **without changing `ProgramBuilder`'s wire-encoding byte protocol**, the same way VNC/ACN5 were added.

## Phase 1 — Exploration (research only, no code)
Re-used the existing extraction (`docs/manual-extract/BM_Manual_eng.txt`) — no PDF re-parse needed. Cross-checked every claim against `docs/PROTOCOL.md` (this app's own wire-format spec, not the manual) before answering, since the manual describes a different/older "Battery Manager" software generation than what this codebase implements.

- **Ramp — genuinely blocked, not just deferred.** `PROTOCOL.md` §15.7 defines nominal values as a flat list of untyped `[Value 4B BE]` floats; the *operator code* is what tells the firmware which quantity to regulate. No ramp convention (start/end/duration triple, a ramp flag) exists anywhere in the spec, and `HardwareSimulator` has zero real ramp support (the one "ramp" hit is an unrelated telemetry-cosmetics helper in `core_engine.py`). A ramp needs genuine new firmware/wire capability, not a client-side resolve step.
- **Resistance — also blocked, same root cause.** §15.8 (`CutoffCondition`) and §15.11 (`RegistrationType`) list Current/Voltage/Power/Capacity/Energy/Temperature/Time — no Resistance byte anywhere. §18.4's 20 defined opcodes have no `CR_CHG`/`CR_DCHG`. Regulating to constant resistance is a distinct closed-loop control mode the firmware would need to run; there's no existing operator to silently redirect a resistance value into, unlike `ACNx`→`A`/`VNC`→`V` which land in an *already-supported* quantity.
- **§12.3 "Using Battery Parameters" — this one works.** 10 of its 11 `INTERN[]` tokens (all but `Rin`, which inherits the Resistance blocker) map to quantities the wire already carries (V/A/plain float), and crucially they're used **bare** (no multiplier — "the Ah counter is set to CNom"), which is exactly what `ProgramBuilder`'s existing `SET`-variable substitution already does. Wrote the plan, got explicit approval, implemented same session.

## Phase 2 — Implementation (approved plan, no deviations)

### Design: reuse existing machinery, don't add a new resolver
Unlike `ACNx`/`VNC` (needed a formula applied at encode time, so `BatteryUnitResolver` had to be threaded as a parameter through every `ProgramBuilder.Process*` method), these 10 tokens need **only substitution** — `ProcessNominalValues`/`ProcessStandardLimit`/`AddRegistrations` already do a by-name `GlobalVariable` lookup for user-defined `SET` variables. Feeding the battery's fields into that *same* list as synthetic `GlobalVariable` entries makes every consumer work with **zero changes** to any of them.

### Changes
- `Utils/BatteryUnitResolver.cs` — new `InternVariableNames` (10 names) and `GetBatteryGlobalVariables(BatteryDTO)` → `List<GlobalVariable>` (`CNom`→`NominalCapacity`/Ah, `NoCell`→`NumberOfCells`, `UGas`/`UMax`/`UNom`/`CutOff`→Gassing/Maximum/Nominal/Break Voltage all /V, `INom`/`ICrank`→Nominal/ColdCranking Current both /A, `ChargeF`/`EDensity`→ChargeFactor/EnergyDensity both unitless). `Rin` deliberately omitted.
- `Services/DecoderService.cs` — one `AddRange` in `ConvertProgramIntoBytesPackets` right after `ExtractGlobalVariables`, gated on `battery != null`, skipping any name a `SET` step already defines (user intent always wins on collision).
- `Components/Pages/Programs/ProgramEditor.razor` — `ValidateProgram()` appends the same 10 names as placeholder `GlobalVariable`s (`Value="0"`) **after** the SET-duplicate-name check (so a placeholder can never falsely trigger "duplicate variable") and only if not already SET-defined, so bare `INom`/`CNom`/etc. validate green while authoring with no battery selected yet — same battery-agnostic-programs philosophy as `ACNx`/`VNC`.

### Bug found and fixed along the way (user explicitly asked to re-check blur/AutoCorrect after the VNC/ACN5 session's `AutoCorrect` bug)
Re-audited `NominalValuesEditor.razor`'s `AutoCorrect` (the fix from the VNC/ACN5 session) against the new bare-variable case. Traced precisely: RegistrationsEditor's and LimitActionPairEditor's blur handlers don't have the analogous bug (Registrations' single-token path doesn't touch `GlobalVariables`; Limits builds its corrected value from `ValidationHelper.ValidateLimit`'s own return, not a separate string-mangling function). `NominalValuesEditor.AutoCorrect`'s matched-variable branch, though, still unconditionally forced `unit = field.Unit` whenever the matched variable had *any* unit — for `INom`/`ICrank`/`UGas`/`UMax`/`UNom`/`CutOff` used in their *correct* matching field this was a harmless no-op (`ProcessNominalValues` always resolves via the variable's own stored `Unit` anyway, never the literal text after it), but for `CNom` (Ah) — which has no matching Nominal Value field at all — it produced a misleading `"CNom A"` display after blur. Fixed: a matched variable's own unit is intrinsic to its definition, so `AutoCorrect` no longer appends any suffix to one at all (`unit = null`) — cosmetic-only fix, `ProcessNominalValues`'s resolution was already correct either way, confirmed no encode-time behavior change for the matching cases.

### Tests (770/770 passing, up from 751)
- `BatteryUnitResolverTests.cs` — 3 new: all 10 names/values/units returned correctly, `Rin` excluded, `InternVariableNames` matches the returned names.
- `ProgramToBytePacketTests.cs` — 5 new: bare `INom` nominal value / `"> UGas"` limit / bare `CNom` registration all encode byte-identically to the plain-value equivalent; a `SET`-defined `INom` wins over the implicit battery value on collision; a bare `INom` with no battery/SET match leaves the value unresolved (pre-existing `ExtractFloatAsByteArraySafe`-returns-null behavior, not a new failure mode).
- `ValidationHelperAcnVncTests.cs` — 6 new: `INom`/`UGas` accepted in their matching field, `CNom` correctly rejected in a Current field ("must have unit A"), all 7 Voltage/Current/Ah tokens accepted as bare Limits, an arbitrary unknown bare word still rejected.

### Files changed
- New: `docs/Battery-Parameters-Intern-Table-Plan.md` (the approved plan, now marked implemented).
- Modified: `Utils/BatteryUnitResolver.cs`, `Services/DecoderService.cs`, `Components/Pages/Programs/ProgramEditor.razor`, `Components/UI/Program/NominalValuesEditor.razor` (AutoCorrect fix), `BatteryTestingSystem.Tests/Utils/BatteryUnitResolverTests.cs`, `BatteryTestingSystem.Tests/Services/ProgramToBytePacketTests.cs`, `BatteryTestingSystem.Tests/Components/ValidationHelperAcnVncTests.cs`, `docs/manual-extract/VNC-ACN-battery-parameters.md` (§7 updated: INTERN table struck through as done, Ramp/Resistance findings recorded), `.claude/CONTEXT/api/device.md`.

## Explicitly not implemented (confirmed blocked, not just deferred)
Ramp and Resistance — would require actual firmware capability plus new wire-format bytes (new operator codes, new `CutoffCondition`/`RegistrationType` values), which is a protocol/hardware conversation outside what the Program Editor's software layer can decide alone. `Rin` (internal resistance) is excluded from the §12.3 work for the same reason.

## Build/test status at end of session
- `dotnet build BatteryTestingSystem.sln`: **0 errors**
- `dotnet test`: **770 passed, 0 failed, 0 skipped**
