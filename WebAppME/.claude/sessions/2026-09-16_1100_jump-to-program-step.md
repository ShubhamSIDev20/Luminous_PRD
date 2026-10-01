# Session — Jump to Program Step (BM Program v3.2, Q10)

> Started: 2026-09-16T11:00:00Z
> Status: Complete — plan approved, implemented exactly as planned, build/tests green.

## Goal
User (new to this codebase, feature originally built by another engineer) attached
`bm_program_v3.2.md` — a hardware protocol doc introducing Q10 (`0x0A`, under `StartByte.Program`),
which lets a running channel jump to an arbitrary step without touching the resident program. Asked
for a UI-flow/mock-diagram plan first, explicit approval before any code, then implementation:
a "Jump to Step..." entry on the Dashboard's per-circuit right-click context menu, enabled only
while a program is running, with client-side step-number validation before any hardware round-trip
("Step not valid. Kindly enter a valid step." on an out-of-range entry).

## Plan phase
Used `EnterPlanMode` explicitly since this was a multi-file UI+backend feature with a UI-design
decision to review first. Explored (read-only) before proposing anything:
- `DashboardView.razor`'s existing context-menu + `CanContextAction`/`DoAction` pattern (Start/Stop/
  Interrupt/Continue), which the new item slots into.
- `ChannelCommandHandler.TimeSyn()` as the closest existing "simple command with a byte-array
  payload" shape to mirror.
- `DecoderService.TryDecode<T>`'s `StartByte.Program` block — found every existing query there
  (`0x01`-`0x08`) reduces to a *generic* `payload[4] == Success` boolean; Q10's response carries a
  second byte (`REASON`) that none of them have, so it needed its own case, not the generic one.
- `Components/UI/Dialog/ConfirmDialog.razor` (dialog primitives template) and
  `Components/UI/Input/INumber.razor` (existing numeric input) — both reused as-is, no new UI
  primitives introduced.
- `ChannelCommandHandler.StartProgram()`'s existing use of `Program.ProgramSteps` (an `int?` already
  tracked on the loaded program) — the exact field needed for client-side step-count validation with
  zero extra plumbing.

Plan approved via `ExitPlanMode`; full plan is in the session's plan file (not duplicated here — see
`.claude/tasks/2026-09-16_jump-to-program-step.md` for the condensed record).

## Implementation
- `Models/Enums/CircuitEnums.cs` — `ProgramDataQuery.JumpToStep = 0x0A`; new `JumpToStepReason` enum
  mirroring the doc's 6 REASON codes (`0x00`-`0x05`).
- `Services/DecoderService.cs` — new `(StartByte.Program, 0x0A)` case in `TryDecode<T>`: `STATUS`
  success falls through to the existing generic `Ok` path; rejection returns
  `CommonResponse<T>.Fail(...)` directly (mirroring how the method's own "Unknown Identity" default
  case already does an early return) with a REASON-specific message from a new private
  `JumpToStepReasonMessage` helper.
- `Utils/JumpStepValidator.cs` (new) — pure static `IsValid(int? stepNumber, int? totalSteps)` +
  `InvalidMessage` constant. Extracted as its own class (not inlined in the dialog) specifically so
  `JumpToStepDialog.razor`'s client-side check and `ChannelCommandHandler.JumpToStepAsync`'s
  server-side defense-in-depth check share one implementation and can't drift apart — this repo's
  own history (`CircuitSelectionLogic`, `BatteryUnitResolver`, `WorkflowNodeProperties`, etc.) already
  established that pattern for exactly this reason.
- `Services/Implementations/ChannelCommandHandler.cs` — new `JumpToStepAsync(int stepNumber)`:
  rejects immediately if `ProgramStatus != Running` or `JumpStepValidator.IsValid` fails (no network
  call for either), otherwise builds `StartByte.Program` + `JumpToStep` + 2-byte big-endian step
  number via `DecoderService.BuildCommand`, sends it, and on success overwrites the generic "Success"
  message with `"Jumped to step {n}."` (the REASON-derived failure message from `TryDecode` passes
  through unchanged).
- `Services/Interfaces/IChannelCommandHandler.cs` — new interface member; stubbed in all 5 other
  implementers (`PreviewChannelCommandHandler`, `FakeChannelCommandHandler`, `FakeCoreCommandHandler`,
  `RecordingChannelCommandHandler`, `TrackingHandler`) so the build didn't break.
- `Components/UI/Dashboard/JumpToStepDialog.razor` (new) — built from `Dialog`/`DialogContent`/
  `DialogHeader`/`DialogFooter` + `INumber`, mirroring `ConfirmDialog.razor`'s shape. Shows the
  circuit's current step number, resets to a clean state on every open (`OnParametersSet`), keeps the
  dialog open with an inline error on a hardware-side rejection (only closes + toasts on success) so
  the operator can see why and retry without re-opening it.
- `Components/Pages/Home/DashboardView.razor` — new `ContextMenuItem` ("Jump to Step...",
  `Lucide.Redo2` — verified this icon actually exists in the installed `Blazicons.Lucide` package
  before using it, per this branch's own established gotcha about unverified icon names) between
  Continue and the Calibration separator; new `"jump"` case in `CanContextAction` (enabled only when
  `ProgramRunningStatus.Running` **and** `CircuitStatus` is `Charge`/`Discharging` — not
  Pause/Interrupt/Msg/Error, which the doc's own 2026-09-15 correction says the hardware now rejects);
  `OpenJumpDialog(dev)` opens the dialog for the **specific right-clicked circuit**, deliberately
  bypassing the checkbox multi-select (`DoAction`'s bulk parallel-by-device pattern) since a single
  step number doesn't generalize to a bulk selection the way Start/Stop/Transfer do.
- `docs/PROTOCOL.md` — new §6.6 "Jump to Program Step (`QueryID = 0x0A`) — Implemented", condensed
  from the source doc into this repo's own byte-table style.
- `docs/manual-extract/bm_program_v3.2.md` (new) — saved the user's attached protocol doc verbatim
  (matches this repo's established practice, e.g. `VNC-ACN-battery-parameters.md`, of mirroring
  external reference material into the repo instead of relying on it being re-attached later), with
  an added "WebAppME implementation note" at the bottom clarifying **only Q10 is implemented** — Q9
  (live step update) was not requested and is out of scope.

## Deliberately out of scope (flagged, not silently dropped)
- **Q9 (live step update, `0x09`)** — the source doc covers it too, but the user only asked for Q10.
  Neither `ProgramDataQuery` nor `DecoderService` gained a Q9 case.
- **Workflow Canvas** (`ChannelActionExecutor`, `SelectionActionBar.razor`) — has its own separate
  action surface; this feature only touches the legacy Dashboard's context menu, per the plan.
- **Live end-to-end hardware/simulator round-trip** — `HardwareSimulator` does not implement Q10
  today (confirmed: no `0x0A` handling anywhere in `program_decoder.py`/`simulator.py`/
  `core_engine.py`). A real click-through verification needs either a simulator update (separate,
  optional follow-up) or real hardware. Flagged in the plan up front, not discovered as a surprise
  after implementing.

## Verification
- `dotnet build BatteryTestingSystem.sln`: 0 errors.
- `dotnet test`: **793/793** passing (+18: 11 `JumpStepValidatorTests`, 7 `DecoderServiceTests` for
  the new Q10 decode cases — success + each of the 5 REASON codes + a "no two reasons collapse onto
  the same message" regression guard).
- App verified to start cleanly (`dotnet run`, checked `logs/btsservice-*.log` for zero
  errors/exceptions) and serve `/Login` (200) / `/Dashboard` (302, correct pre-auth redirect).
- ⚠️ **Not done**: no browser automation tool was available this session (no chrome-devtools MCP), so
  the actual context-menu item, dialog, and a live Q10 round-trip were never click-tested — only
  unit-verified. Told the user explicitly rather than claiming a UI check that didn't happen.

## Branch / PR
`feature/jump-to-program-step`, branched fresh off `main` per the standing instruction (never commit
to `main` directly). Not yet pushed/PR'd as of this session's end — see the task file for status.
