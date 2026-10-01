# T-52 — New Operator: CC_ReChg (0x15)

**Session:** [sessions/2026-09-15_1000_cc-rechg-opcode.md](../sessions/2026-09-15_1000_cc-rechg-opcode.md)
**Status:** Done
**Branch:** `feature/cc-rechg-opcode-0x14` (name predates a mid-task hex correction, kept as-is)

## Ask
Add a new step operator `CC_ReChg`, hex `0x15` (user first said `0x14`, corrected mid-turn — `0x14` is already `PRODUCER`), encoded and processed exactly like the existing `CC_Chg` operator.

## What was done
`CC_RECHG = 21` (`0x15`) registered everywhere `CC_CHG` is, mirroring its shape exactly:
- `Components/UI/Program/OperatorConstants.cs` — const, `Operators` list entry, `ToName`, `LimitActionAllowed`, `NominalConfig.Configs` (identical single-Current-field shape, ACN-family units accepted).
- `Services/PacketAnalyzer.cs` / `PacketAnalyzerNoReverse.cs` — operator-name decode helper (both unused elsewhere in the app, kept consistent anyway).
- `docs/PROTOCOL.md` §18.4 opcode table + §15.7 heading range.
- `HardwareSimulator/program_decoder.py` (`OP_CC_RECHG`, `CHARGE_OPS`, `DEFAULT_OP_NOMINAL_COUNT`) and `simulator.py` (own separate `OperatorCode` enum + `CHARGE_OPERATORS`, genuinely live at `simulator.py:496-497`). `core_engine.py` needed no change — consumes `CHARGE_OPS` generically.

No changes needed to `DecoderService`'s encoding dispatch switch: any opcode not explicitly special-cased (`SET`/`REG`/`TABLE`/`PAU`/`GOTO`/`STO`) already falls through to the generic `ProcessDefaultOperator` path that `CC_CHG` itself uses — confirmed by reading the switch before writing any code, not assumed.

## Tests
775 .NET tests (+4: opcode-value + byte-parity vs `CC_CHG`, ACN5 battery-unit resolution, editor unit-acceptance). 18 Python tests (+1: charge-direction telemetry parity with `CC_CHG`).

## Verification
`dotnet build`: 0 errors. `dotnet test`: 775/775. `python -m pytest tests -q`: 18/18.
