# Session — New Operator: CC_ReChg (0x15)

> Started: 2026-09-15T10:00:00Z
> Status: Complete — implemented, tested, build/tests green.

## Goal
User asked to add a new step operator, `CC_ReChg`, initially specifying hex `0x14` then correcting mid-turn to `0x15` (0x14 is already `PRODUCER` — confirmed against `docs/PROTOCOL.md` §18.4 before implementing), encoded and processed identically to the existing `CC_Chg` operator.

## Research (read-then-verify, not assumed from memory)
Traced the full `CC_CHG` path via Grep/Read across the codebase to find every place a new operator needs registering:
- `DecoderService.ConvertProgramIntoBytesPackets`'s dispatch `switch (step.OperatorCode)` only special-cases `SET`/`REG`/`TABLE`/`PAU`/`GOTO`/`STO` — everything else, `CC_CHG` included, falls through to `default: ProgramBuilder.ProcessDefaultOperator`. So a new opcode gets byte-identical encoding to `CC_CHG` for free, provided it isn't added to one of those special cases.
- Editor-side acceptance (`NominalConfig.Configs`), unit validation (`ValidationHelper.ValidateNominal`/`ValidateLimit`), the operator picker's category grouping (`OperatorSelect.razor`), and badge rendering (`OperatorBadge.razor`) are all driven generically off `OperatorConstants.Operators`/`NominalConfig.Configs` — no per-opcode branches to update beyond registering the new constant in those tables.
- `Services/DecoderService.cs`'s `ConvertProgramBackup` method also has a hardcoded per-opcode branch chain, but has **zero call sites anywhere in the app** (confirmed via Grep) — genuinely dead code, left untouched.
- `HardwareSimulator` mirrors the C# encoder in two independent places: `program_decoder.py`'s `OP_*`/`CHARGE_OPS`/`DEFAULT_OP_NOMINAL_COUNT` (decode-side, used by `core_engine.py`'s setpoint simulation) and `simulator.py`'s own separate `OperatorCode` enum + `CHARGE_OPERATORS` set (used for the device's own charge/discharge capacity split). Both needed the new opcode; `core_engine.py` itself needed no change since it consumes `CHARGE_OPS` generically.

## Implementation
`CC_RECHG = 21` (`0x15`) added everywhere `CC_CHG` (`0x01`) already exists, mirroring its shape exactly (single "Current" nominal field, `A` + `BatteryUnitResolver.AcnUnits`, `Category="charge"`, `Color="charge"` — reusing the existing charge color to stay inside ADR-2's "no CSS rebuild pipeline" constraint rather than inventing a new one):
- `Components/UI/Program/OperatorConstants.cs`: new `const byte CC_RECHG = 21`, `Operators` list entry ("CC ReChg"), `ToName` switch case, `LimitActionAllowed[CC_RECHG] = true`, `NominalConfig.Configs[CC_RECHG]` (identical Current field to `CC_CHG`).
- `Services/PacketAnalyzer.cs` / `PacketAnalyzerNoReverse.cs`: added the `GetOperatorName` case (these two decode-helper classes have no call sites in the app either, but kept consistent rather than left to silently mislabel a real opcode as "UNKNOWN").
- `docs/PROTOCOL.md`: §18.4 opcode table gained `21 | 0x15 | CC_RECHG | Charge`; §15.7's heading range extended to include opcode 21; new §14.4 was *not* touched (that's the ACN/VNC/§12.3 section, unrelated).
- `HardwareSimulator/program_decoder.py`: `OP_CC_RECHG = 21`, added to `CHARGE_OPS` and `DEFAULT_OP_NOMINAL_COUNT` (count 1, same as `OP_CC_CHG`).
- `HardwareSimulator/simulator.py`: added `CC_RECHG = 21` to its own `OperatorCode` enum and to `CHARGE_OPERATORS` (this set is genuinely live — `simulator.py:496-497` uses it to decide whether a step's accumulated capacity counts as charge or discharge).
- `HardwareSimulator/core_engine.py`: **no change needed** — `_tick_setpoint`/`_dummy_voltage_ramp` already key off `step["operator"] in CHARGE_OPS`, imported from `program_decoder.py`.

## Tests (775 .NET, up from 771; 18 Python, up from 17)
- `ProgramToBytePacketTests.cs`: `CcRechg_IsOpcode0x15_AndEncodesByteIdenticallyToCcChgApartFromTheOpcodeByte` (pins the opcode value itself plus byte-for-byte wire parity with an equivalent `CC_CHG` step) and `CcRechg_NominalValue_Acn5_ResolvesAgainstTheBatteryJustLikeCcChg` (battery-relative unit resolution works identically).
- `ValidationHelperAcnVncTests.cs`: `ValidateNominal_CcRechgCurrentField_AcceptsAcnFamilyAlongsidePlainAmps_JustLikeCcChg` (editor-side unit acceptance matches `CC_CHG`).
- `HardwareSimulator/tests/test_core_engine.py`: `test_cc_rechg_is_simulated_identically_to_cc_chg` (charge-direction telemetry split matches `CC_CHG`).

## Verification
- `dotnet build BatteryTestingSystem.sln`: 0 errors.
- `dotnet test`: 775/775 passing (+4).
- `python -m pytest tests -q` (HardwareSimulator): 18/18 passing (+1).

## Branch / PR
Branched fresh off `main` (`feature/cc-rechg-opcode-0x14` — named before the mid-turn hex correction to `0x15`; kept the branch name as-is rather than renaming mid-flight, since the branch name itself has no functional effect) — never on `main`, per the standing instruction. Committed; PR still needs `gh auth login` in this environment (same blocker as the prior docs-sync session) or manual creation via the GitHub compare link.
