# Session — T-47 Phase 3: Marquee Selection + Hardware Actions

**Date:** 2026-08-25
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` (permanent, never merged to `main` — D14/D15)

## Goal

Implement spec sections 5-6 ("Marquee selection" and "Actions", D12/D13) — the piece that
reverses the original D2 decision ("canvas records intent only") into a real
hardware-operating surface. Plan: `docs/superpowers/plans/2026-08-25-workflow-canvas-phase3.md`.

## Status

✅ All 6 tasks complete, live-verified end to end against the running hardware simulator.
505 tests (up from 491 at Phase 2's end), 6 commits `8c36f65`..`70788c9`.

## What was done

1. **`WorkflowSelectionLogic`** (`Services/Implementations/Workflow/WorkflowSelectionLogic.cs`) —
   canvas-native mirror of `DashboardView.UpdateSelectedCircuits`'s eligibility rule (reject
   Offline; first eligible circuit becomes the anchor; subsequent additions must match the
   anchor's `(Status, ProgramStatus)` exactly). `CanAdd` (single) + `ApplyBulk` (marquee/header
   select-all, reports `Added`/`Skipped`). 11 tests. Commit `8c36f65`.
2. **`WorkflowActionRules`** (`Services/Implementations/Workflow/WorkflowActionRules.cs`) —
   exact mirror of `DashboardView.CanContextAction`, minus calibration. Required adding
   `bool IsConnected = true` to the `ChannelTelemetry` record (`Models/DTOs/Workflow/TelemetryEntry.cs`)
   and wiring `circuit.IsConnected` into `PushTelemetryAsync`. 15 tests. Commit `bdd935d`.
3. **`ChannelActionExecutor`** (`Services/Implementations/Workflow/ChannelActionExecutor.cs`) —
   extracts `DashboardView.DoAction`'s per-device `SemaphoreSlim` concurrency pattern (same
   device sequential, different devices parallel) into a testable, instantiable class — the
   dashboard's own copy has zero direct unit tests since it lives in a Razor `@code` block.
   Required a new `RecordingChannelCommandHandler`/`ConcurrencyProbe`/`TrackingHandler` test-fake
   trio (the existing `FakeChannelCommandHandler`'s action methods deliberately throw —
   `CircuitSelectionLogic` never calls them). 7 tests including a wall-clock overlap assertion.
   Commit `5cd47dd`.
4. **Multi-select data model** — `CanvasSurface.SelectedNodeId: string?` →
   `SelectedNodeIds: IReadOnlyCollection<string>`; page's `_selectedNodeId` → `_selectedNodeIds:
   HashSet<string>` + computed `_primarySelectedNodeId` (dock stays single-node per D5); new
   `OnHeaderClick`/`OnSelectChildren` passthrough chain (`CanvasNode` → `BoardNode`/`DeviceNode`
   → `CanvasSurface` → page's `OnSelectNodeChildren`) so clicking a Board/Device header selects
   all its channels. `OnSelect` JSInvokable gained an `additive` bool param (ctrl/shift-click
   toggles instead of replacing). New `_selectedReadings: Dictionary<string, ChannelTelemetry>`
   kept in sync from `PushTelemetryAsync`. Live-verified: header select-all (8/8), ctrl-click
   removal, empty-canvas clear. Commit `0e38e37`.
5. **Marquee drag + pan remap** (JS/CSS only, no C# surface — matches every prior interaction
   feature on this branch). Left-drag on empty canvas now draws a `.wf-marquee` rubber-band
   overlay and reports intersecting `chn-*` nodes to `OnMarqueeSelect` on release; panning moved
   to middle-mouse-button or Space+drag (tracked via document-level `keydown`/`keyup` for
   `spaceHeld`). A near-zero-movement release still clears the selection like the old
   click-to-deselect. Live-verified via synthetic `PointerEvent`/`KeyboardEvent` dispatch: marquee
   draw+select, click-to-clear, middle-button pan, Space+drag pan — all confirmed by reading
   `.wf-world`'s computed transform before/after. Commit `29d3813`.
6. **`SelectionActionBar`** (`Components/UI/WorkflowCanvas/SelectionActionBar.razor`) — floating
   bar shown whenever `_selectedNodeIds.Count > 0`; 5 buttons gated by `WorkflowActionRules
   .CanPerform` via a `Func<string,bool>` param; makes no eligibility decisions itself (4 bUnit
   tests). Wired into the page via `CanRunAction`/`HandleSelectionAction`
   (Start/Stop/Pause/Continue route through `ChannelActionExecutor`; Transfer opens the existing,
   **unmodified** `TransferDialog`), reusing `Processing.razor` for progress exactly as
   `DashboardView` does. Live-verified end to end: correct button enablement on an idle bench
   (Start/Stop/Transfer enabled, Pause/Continue disabled), Start dispatching real
   `StartProgram()` calls with a 2-succeeded/2-failed result correctly aggregated into a toast
   (failure was a genuine hardware-side "no program loaded" response, not a wiring bug), Transfer
   opening the real dialog titled "Transfer to 4 Circuit(s)". Commit `70788c9`.

## Verification

- Full suite: 505/505 passing throughout (started at 491).
- Confirmed via `git diff main..feat/workflow-canvas-experiment -- Components/Pages/Home/DashboardView.razor Components/UI/Dashboard/TransferDialog.razor` → 0 lines: both files remain byte-identical to `main`.
- Confirmed `main` (HEAD `e59dc86`) has received no commits from this branch's work.
- Deferred (per the plan's explicit scope note, not an oversight): the right-click context menu, and the system-error reset button (deferred since Phase 1).

## Gotchas / discoveries

- Browser login: entering passwords to authenticate is a hard-blocked action regardless of
  explicit user instruction or provided credentials — asked the user to log in manually in the
  open browser tab each time a fresh app instance required a session.
- Synthetic `PointerEvent`/`KeyboardEvent` dispatch via `evaluate_script` works fine for
  verifying JS-only interaction code (marquee, pan-gesture remap) without a real mouse — but a
  DOM read taken in the *same* `evaluate_script` call as a `dotNet.invokeMethodAsync(...)` call
  can race the async SignalR round-trip and read stale state; re-checking in a separate tool call
  (which incurs enough round-trip latency) shows the settled result. Not a product bug — a
  verification-methodology trap worth remembering for any future JS→Blazor round-trip check.
- `ChannelActionExecutor.RunAsync`'s dispatch switch throws `ArgumentOutOfRangeException` for
  `"transfer"` deliberately — Transfer never reaches the executor, it opens `TransferDialog`
  directly from the page's `HandleSelectionAction`.

## Next

Phase 3 is the last planned phase per the current spec. Awaiting the user's
`finishing-a-development-branch` choice (branch is permanent/never-merge per D14/D15, so
"Keep as-is" is the expected answer, consistent with every prior phase).
