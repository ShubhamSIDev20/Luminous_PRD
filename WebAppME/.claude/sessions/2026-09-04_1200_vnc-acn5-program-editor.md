# Session — VNC/ACN5 Battery-Relative Nominal Values (Program Editor)

> Started: 2026-09-04T12:00:00Z
> Status: Complete — plan approved, implemented, tested.

## Goal
User asked to explore the VNC/ACN5 features documented in `docs/BM_Manual_eng.pdf` (~363 pages, "Battery Manager User Manual") and implement them in the Program Editor: charge/discharge steps should be able to use battery-relative units, resolved against the **selected battery** at **transfer time**. Explicitly asked for a written plan first (no implementation) and for the PDF to be processed into something reusable/searchable so it never needs re-parsing.

## Phase 1 — Manual extraction + research (no code changes)
- Extracted the full PDF to plain text: `docs/manual-extract/BM_Manual_eng.txt` (`pdftotext -layout`, 363 pages, form-feed page breaks — `pdftotext` is available at `/mingw64/bin/pdftotext` in this environment).
- Wrote a curated, page-cited reference: `docs/manual-extract/VNC-ACN-battery-parameters.md` — the exact formulas, worked examples (manual p.26-27, p.159-160, p.170-172, p.174-175, p.186), and the mapping to `BatteryDTO` fields. **Reuse this file first** for any future battery-parameter question — only re-run `pdftotext` if the PDF itself changes.
- Traced the existing code end-to-end via Gortex (`search_symbols`/`get_symbol_source`/`get_callers`) to find the exact gap: `OperatorConstants.ValidUnits` had no ACN*/VNC tokens; `NominalConfig.Configs`/`ValidationHelper.ValidateNominal` hardcoded exactly one required unit per field (would reject "10 ACn5" with "Unit must be A"); `ProgramBuilder.ExtractFloatAsByteArraySafe`/`TryParseUnit` had zero unit-scaling beyond time units; the whole encode chain (`ChannelCommandHandler.SetProgramAsync` → `DecoderService.ConvertProgramIntoBytesPackets` → `ProgramBuilder.Process*`) had **no battery parameter anywhere**, even though `DeviceController.CoreSendProgram` already fetches the selected battery (just never passed it past `SetBatteryParamAsync`).
- Wrote the full plan to `docs/VNC-ACN5-Implementation-Plan.md`, asked 3 clarifying questions via AskUserQuestion (missing-battery behavior → fail that channel cleanly; scope → all 3 columns — Nominal Value/Limit/Registration — now, not just Nominal Value; editor UX → accept multiple units in the same text field, no new UI controls), all answered with the recommended option. User approved the plan as written.

## Phase 2 — Implementation (approved plan, no deviations)

### Formulas (see the manual reference doc for full derivation)
- `ACN`/`ACN1`/`ACN2`/`ACN4`/`ACN5`/`ACN10`/`ACN20` (current): `value × (battery.NominalCapacity / X hours)`. Bare `ACN` defaults to 5h, same as `ACN5` (a.k.a. "C5").
- `VNC` (a.k.a. `VnC`) (voltage): `value × battery.NumberOfCells`.
- Both resolve to plain `A`/`V` *before* reaching the existing wire-encoding helpers — the hardware protocol has no native concept of them.

### New file
- `Utils/BatteryUnitResolver.cs` — `Resolve(value, unit, battery)` returns `(float, string)?` (null = pass-through for non-battery-relative units), `ResolveToken(string, battery)` for the "value unit" string form used everywhere in `ProgramBuilder`. Throws `BatteryUnitResolutionException` when a battery-relative unit is used but the battery (or its needed field) is missing/zero.

### Threaded `BatteryDTO? battery = null` through (all optional, so no source-breaking changes):
`DeviceController.CoreSendProgram` (now passes `battery.Data` into `SetProgramAsync`, not just `SetBatteryParamAsync`) → `IChannelCommandHandler.SetProgramAsync` / `ChannelCommandHandler.SetProgramAsync` (catches `BatteryUnitResolutionException` here and turns it into `CommonResponse<bool>.Fail(...)` — the one central catch point, so `TransferDialog.razor`/`SchedulerService.cs`/`DeviceController` didn't need their own try/catch) → `DecoderService.ConvertProgramIntoBytesPackets` (both overloads) → `ProgramBuilder.ProcessDefaultOperator`/`ProcessPauOperator`/`ProcessGotoOperator`/`ProcessTableOperator` → `ProcessNominalValues`/`ProcessLimitsWithActions`/`ProcessStandardLimit`/`AddRegistrations`/`TryParseRegistration`. Also threaded through the other two real `SetProgramAsync` call sites that already have a selected battery in scope: `TransferDialog.razor`'s `HandleTransfer` and `SchedulerService.ExecuteScheduleAsync`.

### Bug found and fixed along the way
`ProgramBuilder.TryParseRegistration` extracted the unit by stripping every digit character from the *whole* input string (`input.Where(c => !char.IsDigit(c) ...)`), not just splitting value from unit — so `"ACN5"` became `"ACN"` and `"ACN10"` became `"ACN1"` (silently colliding with the wrong hour divisor). Rewrote it to split on whitespace first (matching how `ProcessStandardLimit`/`ProcessNominalValues` already parse "value unit" pairs) — pinned by `Registration_Acn10_IsNotMangledIntoAcn1ByDigitStripping`.

### Editor-side change
`NominalField` gained `ExtraUnits`/`AcceptedUnits` (was a single required `Unit` string); `NominalConfig.Configs`'s Current-bearing fields now accept `BatteryUnitResolver.AcnUnits` alongside `"A"`, Voltage-bearing fields accept `BatteryUnitResolver.VncUnits` alongside `"V"`. `ValidationHelper.ValidateNominal`'s unit check changed from exact-equality to set-membership. `ValidateLimit` needed **no** change — it validates against the global `OperatorConstants.ValidUnits` list (now including the 8 new tokens), not per-field.
`OperatorConstants.TryParseUnit` and `TryParseRegistration`'s unit switches extended so ACN*/VNC map to the same `CutoffCondition.Current`/`Voltage` and `RegistrationType.Current`/`Voltage` bytes as `"A"`/`"V"`.

### Tests added (751/751 passing, up from 696)
- `BatteryTestingSystem.Tests/Utils/BatteryUnitResolverTests.cs` (31 tests) — formulas pinned against the manual's own worked numbers, pass-through, missing/zero-battery exceptions.
- `BatteryTestingSystem.Tests/Services/ProgramToBytePacketTests.cs` — 8 new tests: ACN5/VNC nominal-value/limit/registration steps encode byte-identically to the equivalent plain A/V step; missing battery throws; the `ACN10`-not-mangled-to-`ACN1` regression guard.
- `BatteryTestingSystem.Tests/Controllers/DeviceControllerTests.cs` — 1 new test confirming `CoreSendProgram` passes the resolved battery into `SetProgramAsync` (added `LastBatteryPassedToSetProgram` tracking to `FakeCoreCommandHandler`).
- `BatteryTestingSystem.Tests/Components/ValidationHelperAcnVncTests.cs` (16 tests) — editor accepts ACN*/VNC alongside plain units per field, rejects them on the wrong-quantity field (VNC on Current, ACN5 on Voltage).

### Files changed
- New: `Utils/BatteryUnitResolver.cs`, `docs/manual-extract/BM_Manual_eng.txt`, `docs/manual-extract/VNC-ACN-battery-parameters.md`, `docs/VNC-ACN5-Implementation-Plan.md`, 4 new test files (above).
- Modified: `Components/UI/Program/OperatorConstants.cs`, `Components/UI/Program/ValidationHelper.cs`, `Services/ProgramBuilder.cs`, `Services/DecoderService.cs`, `Services/Interfaces/IChannelCommandHandler.cs`, `Services/Implementations/ChannelCommandHandler.cs`, `Components/UI/Dashboard/PreviewChannelCommandHandler.cs`, `Controllers/DeviceController.cs`, `Components/UI/Dashboard/TransferDialog.razor`, `Services/Implementations/SchedulerService.cs`, 3 test-fake files (`FakeCoreCommandHandler.cs`, `FakeChannelCommandHandler.cs`, `RecordingChannelCommandHandler.cs`), `.claude/CONTEXT/api/device.md`.

## Scope explicitly not implemented (documented for later, per user's original ask which was VNC/ACN5-only)
The broader `INTERN[]` battery-parameter table (manual §12.3, p.159-160) — `UGas`, `UMax`, `INom`, `ICrank`, `ChargeF`, `Rin`, `CutOff`, `UNom`, `EDensity` as bare nominal-value/limit tokens — maps 1:1 to `BatteryDTO` fields and could reuse the same `BatteryUnitResolver` pattern if wanted later. Also out of scope: `PERCCN_P`/`PERCCN_C`/`PercAh`/`AhDef` (PAU-only percentage-of-capacity limits).

## Build/test status at end of session
- `dotnet build BatteryTestingSystem.sln`: **0 errors**
- `dotnet test`: **751 passed, 0 failed, 0 skipped**
