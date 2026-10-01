# T-49 — VNC/ACN5 Battery-Relative Program Units

> Status: ✅ Done — 2026-09-04
> Session: [sessions/2026-09-04_1200_vnc-acn5-program-editor.md](../sessions/2026-09-04_1200_vnc-acn5-program-editor.md)
> Plan: [docs/VNC-ACN5-Implementation-Plan.md](../../docs/VNC-ACN5-Implementation-Plan.md) (approved as written, no deviations)
> Manual reference: [docs/manual-extract/VNC-ACN-battery-parameters.md](../../docs/manual-extract/VNC-ACN-battery-parameters.md)

## Request
Explore the VNC/ACN5 features in `docs/BM_Manual_eng.pdf` (~363 pages) and implement them in the Program Editor. Calculations at transfer time must use the **selected battery** in the Transfer dialog / scheduler. Plan first — no implementation until approved. Also process the PDF so it's searchable/reusable without re-parsing it every time.

## What shipped
- `docs/manual-extract/BM_Manual_eng.txt` — full PDF extracted to plain text (`pdftotext -layout`).
- `docs/manual-extract/VNC-ACN-battery-parameters.md` — curated, page-cited formula reference.
- `docs/VNC-ACN5-Implementation-Plan.md` — the approved plan (kept for record).
- New `Utils/BatteryUnitResolver.cs`: `ACN`/`ACN1`/`ACN2`/`ACN4`/`ACN5`/`ACN10`/`ACN20` → current as `value × (battery.NominalCapacity / X hours)`; `VNC` → voltage as `value × battery.NumberOfCells`. Resolves to plain A/V before the wire-encoding helpers; throws `BatteryUnitResolutionException` when no usable battery is available.
- `BatteryDTO?` threaded (optional param, no source-breaking changes) through: `DeviceController.CoreSendProgram` → `IChannelCommandHandler.SetProgramAsync`/`ChannelCommandHandler.SetProgramAsync` (catches the resolution exception here, the one central point) → `DecoderService.ConvertProgramIntoBytesPackets` (both overloads) → `ProgramBuilder.ProcessDefaultOperator`/`ProcessPauOperator`/`ProcessGotoOperator`/`ProcessTableOperator` → `ProcessNominalValues`/`ProcessLimitsWithActions`/`ProcessStandardLimit`/`AddRegistrations`/`TryParseRegistration`. Also `TransferDialog.razor`'s `HandleTransfer` and `SchedulerService.ExecuteScheduleAsync` (both already had a selected battery in scope).
- Editor: `NominalField` gained `AcceptedUnits` (was one hardcoded required unit per field); `NominalConfig.Configs`'s Current fields accept the ACN family, Voltage fields accept VNC, alongside the plain unit. `ValidationHelper.ValidateNominal` changed from exact-equality to set-membership. `OperatorConstants.ValidUnits`, `TryParseUnit`, `TryParseRegistration`'s unit switches extended.

## Bug found and fixed
`ProgramBuilder.TryParseRegistration` extracted the unit by stripping every digit character from the *whole* registration string, not just splitting value from unit — `"ACN5"` → `"ACN"`, `"ACN10"` → `"ACN1"`. Rewritten to split on whitespace first, matching every other value/unit parser in the file. Pinned by `Registration_Acn10_IsNotMangledIntoAcn1ByDigitStripping`.

## Tests
751/751 passing (up from 696): `BatteryUnitResolverTests.cs` (31), 8 new cases in `ProgramToBytePacketTests.cs`, 1 new case in `DeviceControllerTests.cs`, `ValidationHelperAcnVncTests.cs` (16, new file).

## Explicitly deferred (documented, not built)
The broader manual §12.3 `INTERN[]` battery-parameter table (`UGas`/`UMax`/`INom`/`ICrank`/`ChargeF`/`Rin`/`CutOff`/`UNom`/`EDensity` as bare tokens) and the PAU-only `PERCCN_P`/`PERCCN_C`/`PercAh`/`AhDef` limits — out of scope for the VNC/ACN5 request, but could reuse the same `BatteryUnitResolver` pattern if wanted later.
