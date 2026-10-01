# VNC / ACNx Program Editor Feature — Implementation Plan

> Status: **Implemented 2026-09-04** (approved as written; no deviations from the plan below). 751/751 tests passing, `dotnet build` clean.
> Manual reference: [`docs/manual-extract/VNC-ACN-battery-parameters.md`](manual-extract/VNC-ACN-battery-parameters.md) (page-cited excerpts, formulas). Full searchable extraction: `docs/manual-extract/BM_Manual_eng.txt`.

## 1. What we're building

Two new battery-relative nominal-value/limit units, resolved against the **selected battery** at **transfer time** (not at program-authoring time — programs stay battery-agnostic, exactly as the manual intends: "using battery parameters allows you to use your programs for multifold applications"):

| Token | Meaning | Formula | Resolves to |
|---|---|---|---|
| `ACN` (bare = 5h default), `ACN1`, `ACN2`, `ACN4`, `ACN5`, `ACN10`, `ACN20` | Current as a multiple of nominal capacity over X hours (the "C-rate" family; `ACN5` a.k.a. "C5") | `value × (battery.NominalCapacity / X)` | plain **A** |
| `VNC` (a.k.a. `VnC`) | Voltage per cell | `value × battery.NumberOfCells` | plain **V** |

Both are usable in the **Nominal Value**, **Limit**, and **Registration** columns (per your answer, implementing all three now). Example: a program step written as `"1.0 ACN5"` on a 100Ah/6-cell battery becomes `20 A`; a limit `"2.35 VNC"` becomes `14.1 V`.

**Missing-battery behavior:** if a program uses `ACNx`/`VNC` and no valid battery is resolved at transfer time, that channel's request fails with a clear per-channel error (matching the pattern already used in `DeviceController.Core*` — e.g. `"{key} -> Program uses ACN5/VNC but no valid battery selected"`), rather than silently sending an unscaled or garbage value.

## 2. Current-state findings (why this doesn't work today)

- `BatteryDTO` (`Models/DTOs/BatteryDTO.cs`) already has everything needed: `NominalCapacity` (float, Ah) and `NumberOfCells` (int). No schema/migration change.
- **No unit resolution exists anywhere.** `OperatorConstants.ValidUnits` (`Components/UI/Program/OperatorConstants.cs:84`) has no `ACN*`/`VNC` entries. `ProgramBuilder.ExtractFloatAsByteArraySafe` (`Services/ProgramBuilder.cs:494`) only special-cases time-unit multipliers (s/min/h) — every other unit defaults to multiplier `1`, so `"1.0 ACn5"` today would encode the raw float `1.0` onto the wire as if it were 1 A.
- **The editor blocks it before that anyway.** `NominalConfig.Configs` (`OperatorConstants.cs:~205`) hardcodes exactly one required unit string per field (e.g. Current field requires literally `"A"`), and `ValidationHelper.ValidateNominal` (`Components/UI/Program/ValidationHelper.cs:104`) rejects anything else with "Unit must be A".
- **Zero battery plumbing in the encode chain.** `ChannelCommandHandler.SetProgramAsync(ProgramDTO)` → `DecoderService.ConvertProgramIntoBytesPackets(steps, resolvedPrograms)` → `ProgramBuilder.ProcessDefaultOperator` → `ProcessNominalValues` / `ProcessLimitsWithActions` / `AddRegistrations`. None of these currently receive a `BatteryDTO`.
- **The battery is already available at the right call site** — `DeviceController.CoreSendProgram` (`Controllers/DeviceController.cs`) already does `await batteryServices.GetBattery((long)req.BatteryId.Value)` and calls `handler.SetBatteryParamAsync(battery.Data)` — it just never passes that same `battery.Data` into `handler.SetProgramAsync(...)`. This is the one hop that needs adding; the REST/MCP surface (`CommonRequest.BatteryId`) already carries the battery selection all the way to this point.

## 3. Design: one shared resolver, threaded through the existing pipeline

Add a single new static helper (new file `Utils/BatteryUnitResolver.cs`, alongside the existing `Utils/ChannelExpander.cs` pattern):

```csharp
public static class BatteryUnitResolver
{
    // Returns null (unchanged) if unit isn't ACN*/VNC — safe to call unconditionally.
    public static (float value, string unit)? Resolve(float value, string unit, BatteryDTO? battery)
    {
        // ACN, ACN1, ACN2, ACN4, ACN5, ACN10, ACN20 -> ("A", value * (battery.NominalCapacity / X))
        // VNC / VnC -> ("V", value * battery.NumberOfCells)
        // throws BatteryRequiredException if unit needs a battery but battery is null (caught by callers)
    }
}
```

This resolves battery-relative tokens down to plain `A`/`V` *before* they reach the existing wire-encoding helpers — the hardware protocol has no native concept of `ACNx`/`VNC`; it only ever sees Amps/Volts (confirmed by the manual's own wording and by the current encoder). This keeps `ExtractFloatAsByteArraySafe` and `TryParseUnit` (the cutoff-condition-byte mapper for Limits) completely unchanged — they just receive an already-resolved `"20 A"` instead of `"1.0 ACN5"`.

**Threading the battery through** (all as an *optional* `BatteryDTO? battery = null` parameter, so every existing call site that doesn't need it keeps compiling unchanged):

1. `DeviceController.CoreSendProgram` — pass `battery.Data` (already fetched) into `handler.SetProgramAsync(program.Data, battery.Data)`.
2. `IChannelCommandHandler.SetProgramAsync` + `ChannelCommandHandler.SetProgramAsync` — add the optional parameter; forward to `DecoderService.ConvertProgramIntoBytesPackets`.
3. `DecoderService.ConvertProgramIntoBytesPackets` (both overloads) — add the optional parameter; forward to `ProgramBuilder.ProcessDefaultOperator` / `ProcessPauOperator`.
4. `ProgramBuilder.ProcessDefaultOperator`, `ProcessNominalValues`, `ProcessLimitsWithActions`, `ProcessStandardLimit`, `ProcessPauLimit`, `AddRegistrations` — each calls `BatteryUnitResolver.Resolve(...)` right before its existing `ExtractFloatAsByteArraySafe`/`TryParseUnit` call, on the parsed value+unit pair.
5. `MCP/DeviceMcpTools.SendProgram` — no change needed; it already calls `DeviceController.CoreSendProgram`, which is where the fix lives.
6. Other `SetProgramAsync` callers (`TransferDialog.razor`'s `HandleTransfer`, `SchedulerService.ExecuteScheduleAsync`/`CreateScheduleAsync`) — both already have a selected `BatteryDTO` in scope (that's literally what "the selected battery in ProgramBuilder" refers to on the Transfer dialog); pass it through the same new optional parameter.
7. Interface-implementor blast radius (small): `ChannelCommandHandler` (real), `PreviewChannelCommandHandler` (dashboard preview), and the test doubles `FakeChannelCommandHandler`/`FakeCoreCommandHandler` — each gets the new optional parameter added to its signature (default `null`, no behavior change if omitted).

**Editor-side change** (per your answer: same text field, multiple accepted units):
- `OperatorConstants.ValidUnits` — add `ACN`, `ACN1`, `ACN2`, `ACN4`, `ACN5`, `ACN10`, `ACN20`, `VNC` (the regex-driven `UnitPattern`/`FullPattern`/`ValueUnitPattern` used by `AutoCorrect`/`AutoSpace` pick these up automatically once added — no separate regex change needed).
- `NominalField` (`OperatorConstants.cs`) — change `Unit` from a single required string to a small accepted-set (e.g. `string[] AcceptedUnits`, keeping `Unit` as the default/placeholder unit for display), so a Current field accepts `{"A", "ACN", "ACN1", "ACN2", "ACN4", "ACN5", "ACN10", "ACN20"}` and a Voltage field accepts `{"V", "VNC"}`.
- `ValidationHelper.ValidateNominal` — change the `parts[1] != field.Unit` equality check to a set-membership check against the field's accepted units.
- `ValidationHelper.ValidateLimit` (the Limit-column counterpart, not yet inspected in depth) — needs the equivalent treatment; will confirm its exact structure during implementation (it's the limit-side sibling of `ValidateNominal`, same file).

## 4. Files touched (concrete list)

**New:**
- `Utils/BatteryUnitResolver.cs` — the resolver + a small `BatteryRequiredException` (or a `(bool ok, string? error)` return shape, matching this codebase's `CommonResponse<T>`-style error handling rather than exceptions where practical).
- `BatteryTestingSystem.Tests/Utils/BatteryUnitResolverTests.cs` — formula unit tests against the manual's own worked examples (100Ah/5h→20A @ 1.0 ACN5, 6 cells @ 2.35 VNC → 14.1V, etc.).

**Modified:**
- `Components/UI/Program/OperatorConstants.cs` — `ValidUnits`, `NominalField`/`NominalConfig.Configs`.
- `Components/UI/Program/ValidationHelper.cs` — `ValidateNominal`, `ValidateLimit`.
- `Services/ProgramBuilder.cs` — `ProcessNominalValues`, `ProcessLimitsWithActions`, `ProcessStandardLimit`, `ProcessPauLimit`, `AddRegistrations`, `ProcessDefaultOperator`, `ProcessPauOperator`.
- `Services/DecoderService.cs` — both `ConvertProgramIntoBytesPackets` overloads.
- `Services/Interfaces/IChannelCommandHandler.cs` + `Services/Implementations/ChannelCommandHandler.cs` — `SetProgramAsync` signature.
- `Components/UI/Dashboard/PreviewChannelCommandHandler.cs` — signature match.
- `Controllers/DeviceController.cs` — `CoreSendProgram` passes `battery.Data` through; per-channel error message when a battery-relative unit is used without a resolvable battery.
- `Components/UI/Dashboard/TransferDialog.razor`, `Services/Implementations/SchedulerService.cs` — pass their already-selected battery through.
- `BatteryTestingSystem.Tests/Controllers/FakeCoreCommandHandler.cs`, `BatteryTestingSystem.Tests/Services/FakeChannelCommandHandler.cs` — signature match.
- `.claude/CONTEXT/api/device.md`, relevant `docs/` — document the new units once shipped.

## 5. Risks / open items to watch during implementation

- **Division by zero / unset battery fields**: a battery with `NominalCapacity == 0` or `NumberOfCells == 0` (both are valid-range-but-zero per `BatteryDTO`'s `[Range]` attributes) must be treated as "can't resolve" and produce the same clear error, not `Infinity`/`NaN` silently encoded.
- **`ValidateLimit`'s exact structure** hasn't been read yet (only `ValidateNominal` was inspected in depth) — needs a look before writing code to confirm the equivalent change point.
- **`ProcessPauOperator`** (PAU-only limits: `AhDef`, `PercAh`, `PERCCN_P/C`) is explicitly out of scope per the manual reference doc — not part of this feature, but shares the same `ProcessPauLimit` function, so the resolver must be additive there (only touching `ACNx`/`VNC` tokens) and not disturb existing PAU-limit parsing.
- **Backward compatibility**: existing programs using plain `A`/`V`/etc. must be byte-for-byte unaffected — the resolver is a no-op passthrough for any unit it doesn't recognize.
- **Scope boundary**: the broader `INTERN[]` battery-parameter table (`UGas`, `UMax`, `INom`, `ICrank`, `ChargeF`, `Rin`, `CutOff`, `UNom`, `EDensity` as bare tokens) is documented in the manual reference but intentionally **not** part of this plan — flagged as a natural follow-up using the same resolver pattern, only if wanted later.

## 6. Verification plan

- New `BatteryUnitResolverTests.cs` pinned against the manual's own worked numbers.
- Extend `ProgramToBytePacketTests.cs` (existing `DecoderService.ConvertProgramIntoBytesPackets` test file) with cases: a step using `"1.0 ACN5"` + a battery → encodes identically to a step using `"20 A"` directly (byte-for-byte comparison); a step using battery-relative units with `battery: null` → surfaces the expected error instead of encoding garbage.
- Extend `DeviceControllerTests.cs` (added this session) with a `CoreSendProgram` case: a program with `ACN5` nominal values + a selected battery → `handler.SetProgramAsync` receives correctly-scaled values; same program with no resolvable battery → per-channel error message, batch continues (reusing the per-request-error pattern from the ChannelList work).
- Editor-level: extend the `ValidationHelper`/`NominalConfig` test coverage (find existing tests for these, if any) so `"10 ACn5"` and `"2.35 VnC"` validate successfully in a Current/Voltage field respectively, and an unrelated unit (e.g. `"10 W"` in a Current-only field) still correctly fails.
- `dotnet build` + `dotnet test` clean, as with every change in this repo.
