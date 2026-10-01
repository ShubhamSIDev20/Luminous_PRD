# T-53 — Jump to Program Step (BM Program v3.2, Q10)

**Session:** [sessions/2026-09-16_1100_jump-to-program-step.md](../sessions/2026-09-16_1100_jump-to-program-step.md)
**Status:** Done (code + tests) — live UI click-through not verified, no browser tool this session
**Branch:** `feature/jump-to-program-step`

## Ask
Add a "Jump to Step..." action to the Dashboard's per-circuit right-click context menu, enabled only
while a program is running, that sends the hardware's new Q10 query (`bm_program_v3.2.md`,
`StartByte.Program` `0x0A`) to abandon the current step and resume from an operator-supplied step
number — with client-side validation ("Step not valid. Kindly enter a valid step.") before any
hardware round-trip. Plan-first per explicit request (used `EnterPlanMode`/`ExitPlanMode`).

## What was done
- `Models/Enums/CircuitEnums.cs`: `ProgramDataQuery.JumpToStep = 0x0A`, new `JumpToStepReason` enum
  (6 values, `0x00`-`0x05`).
- `Services/DecoderService.cs`: `TryDecode<T>` gained a dedicated `(Program, 0x0A)` case that decodes
  both `STATUS` and `REASON` (existing Program-family queries only ever had `STATUS`) and returns a
  REASON-specific `Fail(...)` message on rejection.
- `Utils/JumpStepValidator.cs` (new): pure static step-number bounds check, shared by both the
  dialog (fast client feedback) and `ChannelCommandHandler.JumpToStepAsync` (defense in depth) so
  they can't disagree.
- `Services/Implementations/ChannelCommandHandler.cs`: new `JumpToStepAsync(int stepNumber)`.
- `Services/Interfaces/IChannelCommandHandler.cs` + all 5 other implementers stubbed.
- `Components/UI/Dashboard/JumpToStepDialog.razor` (new): step-number prompt dialog, built from the
  existing `Dialog`/`DialogContent` primitives + `INumber`.
- `Components/Pages/Home/DashboardView.razor`: new context-menu entry + `"jump"` `CanContextAction`
  case (enabled only for `Running` + `Charge`/`Discharging`) + `OpenJumpDialog` targeting the
  specific right-clicked circuit (not the checkbox multi-select).
- `docs/PROTOCOL.md` §6.6 + `docs/manual-extract/bm_program_v3.2.md` (the source doc, saved verbatim
  per this repo's existing mirror-external-docs practice).

## Deliberately not done
- Q9 (live step update) — not requested, doc covers it but out of scope here.
- Workflow Canvas wiring (`ChannelActionExecutor`) — separate action surface, not touched.
- `HardwareSimulator` does not implement Q10 — confirmed no `0x0A` anywhere in it. A live
  round-trip test needs a simulator update (optional follow-up) or real hardware.

## Verification
`dotnet build`: 0 errors. `dotnet test`: **793/793** (+18). App verified to start clean (log checked
for errors), `/Login`/`/Dashboard` respond correctly. **Not verified**: no browser automation tool
was available this session, so the actual menu entry / dialog / Q10 round-trip was never
click-tested — this is unit-verified only, told to the user explicitly.
