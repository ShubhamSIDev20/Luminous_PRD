# Session #27 — Workflow Canvas Phase 1: isolation and correctness (T-47 follow-up)

**Date:** 2026-08-24
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` — ⚠️ NOT MERGED, permanent side branch (D9-D16)
**Task:** [T-47](../tasks/2026-08-22_workflow-canvas-playground.md)
**Status:** ✅ All 5 Phase 1 tasks complete, live-verified. 408 tests (up from 375), 5 commits `103d189`..`7335b81`.

## Goal

Live review of session #26's live-data work (from the user, using the app directly) surfaced six
gaps: device stuck permanently "offline", channels labelled by database id instead of physical
address, no way to see all channels on one screen, no property picker, no hardware actions, missing
card-footer parity. This reversed decision D2 ("canvas records intent only") — the user now wants
full dashboard parity. Ran brainstorming → architectural spec
(`docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md`, D9-D16) → Phase 1
plan (`docs/superpowers/plans/2026-08-23-workflow-canvas-phase1.md`) → inline execution, this
session.

Phase 1 = isolate the branch from `main` + fix three correctness defects. Layout/LOD/property-picker
(Phase 2) and marquee-select/actions (Phase 3) are not started.

## What was done

**Task 1 — WorkflowDbContext.** Split workflow persistence off `AppDbContext` into its own
`WorkflowDbContext` with its own migration history table (`__EFMigrationsHistory_Workflow`), so
`main`'s EF snapshot (`Migrations/AppDbContextModelSnapshot.cs`) never mentions the canvas again —
that file is unmergeable by nature and `main` had already shipped 3 migrations since this branch
diverged. DI and startup migration collapsed behind `AddWorkflowCanvas()` / `MigrateWorkflowCanvas()`.
**No data lost**: the new migration's DDL was byte-identical to the live table (SQLite ignored the
old `Device` schema annotation), so it was baselined into the new history table instead of run —
all 4 saved layout rows verified intact via the running app.

**Task 2 — WorkflowMenu.** Moved menu construction out of `MainLayout.razor` into
`Components/Layout/WorkflowMenu.cs`. Cut that file's diff against `main` from +47/−8 to +8/−8.

**Task 3 — WorkflowNodeLabeller.** Replaced `EntityId`-based labels ("Channel 579", "Board 76")
with the physical `1-8-2` address format the dashboard already uses everywhere. Also absorbed the
page's separate `_channelKeys` telemetry lookup (identical mapping, one index instead of two).

**Task 4 — live online counts.** `DeviceNode` declared an `IsOnline` parameter `CanvasSurface`
never passed — same unwired-parameter bug class as `SelectedNodeId` in session #25. Fixed via the
telemetry path (a `data-role="online"` text write), not by wiring the parameter, since online state
is live data and a Blazor parameter would re-render all 730 nodes per hardware event. New
`WorkflowTelemetryBridge.BuildRollups` computes `"N / M online"` per device/board.

**Task 5 — card-footer parity.** Added program status and a last-update clock to channel nodes and
the dock, matching `DeviceChannel.razor`'s footer.

**Also fixed, unplanned but blocking honest Task 3 verification:** `HandleLoad` forced its telemetry
paint (`RefreshSubscription()` → `OnHardwareChanged()`) *before* `StateHasChanged()` had rendered the
loaded nodes into the DOM, so the paint wrote into a DOM that didn't contain them yet. Loading a
saved layout showed correct labels with dead "-- V" placeholders forever (no further hardware event
was coming on the idle bench). Present since commit `90d9b8d` (session #26), not introduced this
session. Fixed by making `RefreshSubscription(forcePaint)` an opt-out and painting explicitly after
`HandleLoad`'s render + viewport restore. Not unit-tested — only manifests against a real DOM plus
JS interop.

## Where the plan was wrong (3 corrections, all found while executing)

1. **Data-loss avoidable.** Plan said drop/recreate `WorkflowLayouts`. Actually baseline-able — see
   Task 1 above.
2. **"Pass the IsOnline parameter" was wrong.** Online state is live data; routing it through a
   Blazor parameter would break the no-re-render guarantee. Went through telemetry instead, and
   Task 4's browser step added a MutationObserver check (0 childList mutations) to pin this.
3. **Rollup denominator was wrong.** Plan's `childChannelIds` counted every channel the device
   *owns* in the DB. Telemetry only ever arrives for channels *placed on the canvas* (that's the
   entire subscription cost-control mechanism), so a device with 8 of 64 channels placed would have
   shown "8 / 64 online" — reporting 56 unplaced, unwatched channels as offline. Fixed:
   `BuildRollups` intersects with `graph.Nodes` of kind Channel before counting. Caught before
   committing, not after.

## Gotchas discovered

- **Two namespace-location surprises** the plan guessed wrong, both one-line fixes: `IWorkflowTopologyProvider`
  lives in `Services.Implementations.Workflow` (beside its impl), not `Services.Interfaces` like its
  two siblings; `DashboardView` lives in `Components.Pages.Home`, not `Components.UI.Dashboard`.
- **A file the plan's File Structure section missed entirely**: `WorkflowLayoutRepositoryTests.cs`
  constructed `AppDbContext` directly for the repository under test. Mechanical fix (retarget to
  `WorkflowDbContext`) but a real gap in the plan's coverage — worth a closer File Structure pass
  next time a DbContext gets swapped.
- **A Python-based file rewrite silently stripped the UTF-8 BOM** from three C# files
  (`AppDbContext.cs`, `ServiceCollectionExtensions.cs`, `Program.cs`), producing a spurious
  first-line diff against `main` in every one of them — exactly the rebase noise Task 1 existed to
  eliminate. Caught by inspecting `git diff main` line-by-line rather than trusting `--stat`; fixed
  by restoring BOMs and resetting `AppDbContext.cs` to `git checkout main --` since its only real
  change (the deleted DbSet) left it otherwise identical. **Any future find/replace across `.cs`
  files in this repo must preserve encoding explicitly (`utf-8-sig` in Python) or verify with
  `head -c3 file | xxd -p` (expect `efbbbf`).**
- **"Does the clock advance" is untestable against a genuinely idle simulator.** A probe on
  `applyTelemetry` showed zero calls over 8s on an idle circuit — confirms the documented
  `HardwareManagerChanged`-only-fires-on-state-change behavior extends to *every* field, not just
  the ones covered by earlier sessions. Verified the write path directly instead (synthetic
  `applyTelemetry` call with a different `lastUpdateText`) rather than waiting on hardware that
  wasn't going to move.
- **`dotnet test` and `dotnet ef` both fail with MSB3027/MSB3021 if the app is still running** —
  had to stop it (via PowerShell, checking both the `dotnet.exe` launcher and the child
  `BatteryTestingSystem.exe`) before every build/migration step this session, several times, since
  each browser verification round left it running.
- **Browser JS caching bit twice**: `Test-NetConnection` + plain `navigate` reused a cached
  `workflow-canvas.js` after edits (confirmed via `window.workflowCanvas.applyTelemetry.length`
  showing the old arity). Fixed with `navigate_page {type: reload, ignoreCache: true}`. Any future
  JS-file verification in this app should hard-reload first, not just navigate.

## Verification

408/408 tests green (up from 375 at session start), all live-verified in a real browser against the
real `AppDbContext`-backed database and the running `run_sim.py -d 10 -n 64` simulator: physical
labels (`1-1-1`..`1-1-8`), device/board online counts (`8 / 8 online`), program status + advancing
write-path (confirmed via synthetic injection), and a MutationObserver confirming 0 childList
mutations across 20 rapid synthetic telemetry writes touching every new field this session added.

## Files changed

Created: `Data/WorkflowDbContext.cs`, `Extensions/WorkflowServiceCollectionExtensions.cs`,
`Components/Layout/WorkflowMenu.cs`, `Services/Implementations/Workflow/WorkflowNodeLabeller.cs`,
`Migrations/Workflow/*`, 4 new test files.
Modified: `Data/AppDbContext.cs` (net: reverted to `main`), `Extensions/ServiceCollectionExtensions.cs`
(+2 lines), `Program.cs` (+1 line), `Components/Layout/MainLayout.razor` (+8/−8),
`Repositories/Implementations/WorkflowLayoutRepository.cs`, `Models/DTOs/Workflow/TelemetryEntry.cs`,
`Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, `Components/Pages/Workflows/WorkflowCanvasPage.razor`,
`Components/UI/WorkflowCanvas/Nodes/{DeviceNode,BoardNode,ChannelNode}.razor`,
`Components/UI/WorkflowCanvas/PropertiesDock.razor`, `wwwroot/js/workflow-canvas.js`.
Deleted: `Migrations/20260822152559_AddWorkflowLayout.{cs,Designer.cs}`.

## Next step

Phase 2 (lane layout + level-of-detail + node property config) and Phase 3 (marquee selection +
`ChannelActionExecutor` + actions) are specced (D10-D13) but not planned or started. Their plans
should follow the same brainstorm→spec→plan→execute cycle, likely one plan per phase given Phase
1's size (5 tasks, ~1 day).
