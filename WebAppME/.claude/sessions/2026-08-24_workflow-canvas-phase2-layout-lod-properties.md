# Session #28 — Workflow Canvas Phase 2: lane layout, LOD, node properties (T-47 follow-up)

**Date:** 2026-08-24
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` — ⚠️ NOT MERGED, permanent side branch (D9-D16)
**Task:** [T-47](../tasks/2026-08-22_workflow-canvas-playground.md)
**Status:** ✅ All 8 Phase 2 tasks complete, live-verified. 465 tests (up from 408 at session start), 8 commits `adae69f`..`9813434`.

## Goal

Phase 1 (session #27) fixed the branch-isolation and correctness gaps. This session implemented
Phase 2 of the dashboard-parity plan (`docs/superpowers/plans/2026-08-24-workflow-canvas-phase2.md`,
spec sections 5-6, D10-D11): replace the illegible single-column auto-layout with per-device lanes,
add zoom-driven level-of-detail so hundreds of channels fit on one screen, and let the operator
choose which live properties a channel node's face shows.

## What was done

**Task 1 — lane layout.** `WorkflowAutoLayout.Apply` rewritten: a device's boards stack vertically,
each board's channels fill a row of up to `ChannelsPerRow = 8` columns beside it (8 because a
physical secondary board has at most 8 channels — the `AddSecondaryBoardAndChannel` migration's own
"1-8 backfill"). Old tests fully rewritten — the column-per-kind assertions describe the algorithm
being replaced, not something to keep passing.

**Task 2 — lane stacking + battery rail.** Found and fixed a bug the new layout exposes:
`NextFreeRow(graph, NodeKind.Device)` looked only at device-node Y, which is always 0 within a lane
(every device node sits at its own lane's local origin) — so it never reflected how tall a lane's
boards/channels actually grew, and a second device would land on top of the first's channel grid.
New `NextFreeLaneY` clears the full height of every node on the canvas. Also fixed the battery
column's fixed X (`ColumnWidth*3` = 840px), which now lands inside a lane's channel grid (extends
to 2800px) — new `BatteryRailX` clears the widest possible lane.

**Task 3 — level of detail.** Three zoom tiers (`>=0.8` full card, `0.4-0.8` header+first row,
`<0.4` colour tile) stamped as `.wf-world[data-lod]` purely from `applyTransform` (the single
choke point for every pan/zoom/setViewport path). `CanvasNode` gained a `title` attribute so a
tile still identifies itself on hover. This is what actually answers "see it all on one screen" —
lane layout alone still leaves 640 channels too tall.

**Task 4 — property catalog.** `WorkflowNodeProperties` exposes 17 of the dashboard's 28
`CardPreviewData` keys — deliberately not all 28. `ChannelTelemetry` covers every `RealTimeProperties`
key (12) plus 5 of 13 `ProgramProperties` keys; it has none of the 3 `ConfigProperties` keys
(different data source, never loaded) and no clean split for `Error`/`SystemError` (already merged
into one `ErrorText`). Offering the other 11 would show "--" forever.

**Task 5 — config model.** `WorkflowNodeConfig` with a pure `Toggle` method (add/remove/refuse-at-cap),
extracted for unit testing rather than embedded in the picker — matches the repo's established
`CircuitSelectionLogic` extraction pattern.

**Task 6 — bridge formatting.** `WorkflowTelemetryBridge.BuildNodeProperties` turns a reading into
all 17 keys as pre-formatted strings, added to `TelemetryEntry.Properties`.

**Task 7 — configurable node face.** `ChannelNode`'s four fixed Voltage/Current/Power/Temperature
rows become a loop over `SelectedProperties`; JS's four hardcoded `setText` calls become a generic
loop over `e.properties`. Default reproduces the shipped look exactly (browser-verified
pixel-identical against the saved "Test" layout).

**Task 8 — picker + persistence.** `NodePropertyPicker`, a floating panel toggled from a new
"Node fields" toolbar button, persisted via `ServerSessionStorageService` under
`{UserId}_canvas_node_config` — same mechanism and `Permanent:true` the dashboard's card config
already uses, separate key since the two surfaces cap/default differently.

## Where the plan was wrong (4 corrections, all found while executing)

1. **A test literal (`1.2345`) hit a floating-point boundary.** Not exactly representable in
   `double`, so `F3` rounded down to `1.234` rather than the naively expected `1.235`. My own bug
   in the plan's test code, not an implementation bug — replaced with values clear of the boundary.
2. **A real namespace collision.** Adding `BatteryTestingSystem.Tests.Models` (Task 5) shadowed
   production's `BatteryTestingSystem.Models` for an existing unqualified reference in
   `RepositoryTests.cs` (`Models.Entities.ApplicationUser`) — C#'s namespace lookup prefers the
   nearer sibling, silently resolving to the wrong (nonexistent) namespace and failing to compile.
   Fixed with a `global::`-qualified reference at the one call site.
3. **Task 7's JS step assumed voltage/current roles had already been removed** in an earlier
   session. They hadn't — all four hardcoded `setText(node, "voltage"|"current"|"power"|"temperature", ...)`
   calls needed removing, not just power/temperature.
4. **A pre-existing test file the plan's File Structure missed entirely**: `WorkflowNodeTests.cs`
   asserted the old fixed `data-role="voltage"`/`"current"` slots existed. Same class of gap as
   Phase 1's missed `WorkflowLayoutRepositoryTests` — updated to the new `prop-Voltage`/`prop-Current`
   contract (a deliberate behavior change, not a regression).

## Gotchas discovered

- **Battery placement could not be live-verified** — this test database has no battery types
  configured. `BatteryRailX_ClearsTheWidestPossibleChannelGrid` (unit test) is the available proof
  for that specific claim, consistent with declining destructive DB actions the environment doesn't
  support.
- **A Blazor Server checkbox toggle is not synchronously reflected in derived UI text.** Reading
  `.wf-property-picker p`'s "N / 6 selected" text immediately after a synthetic `.click()` raced
  the SignalR round-trip and read a stale value once; the native `.checked` property itself updates
  instantly (browser-only), but anything server-rendered from the new state needs to wait for the
  round-trip. Not a bug — a verification-script timing lesson for this app's rendering model.
- **The browser JS cache bit again** (as in sessions #26/#27) — `navigate_page {type: reload,
  ignoreCache: true}` is required after every `wwwroot/js` or `wwwroot/css` edit before trusting
  what's rendered; a plain reload can silently serve stale JS.

## Verification

465/465 tests green (up from 408), all 8 tasks live-verified in a real browser against the real
`AppDbContext`-backed database and the running `run_sim.py -d 10 -n 64` simulator: a full
64-channel device now renders as a compact 8-row lane instead of a ~13,000px column; two devices
stack with zero overlap; all three LOD tiers confirmed at their exact zoom thresholds with a
MutationObserver showing 0 `childList` mutations across an 11-step zoom sweep; the property picker
end-to-end (default 4/6 → check 2 more → cap-disable 11 boxes → node face picks up all 6 → survives
a full page reload).

## Files changed

Created: `Services/Implementations/Workflow/WorkflowNodeProperties.cs`,
`Models/DTOs/Workflow/WorkflowNodeConfig.cs`, `Components/UI/WorkflowCanvas/NodePropertyPicker.razor`,
5 new test files (`WorkflowNodePropertiesTests`, `WorkflowNodeConfigTests`, `CanvasNodeTests`,
`ChannelNodeTests`, `NodePropertyPickerTests`).
Modified: `Services/Implementations/Workflow/WorkflowAutoLayout.cs` (full rewrite),
`WorkflowPlacement.cs`, `WorkflowTelemetryBridge.cs`, `Models/DTOs/Workflow/TelemetryEntry.cs`,
`Components/UI/WorkflowCanvas/{CanvasNode,CanvasSurface}.razor`,
`Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`, `Components/Pages/Workflows/WorkflowCanvasPage.razor`,
`wwwroot/js/workflow-canvas.js`, `wwwroot/css/workflow-canvas.css`,
`BatteryTestingSystem.Tests/{Services/Workflow/WorkflowAutoLayoutTests,WorkflowPlacementTests,
WorkflowTelemetryBridgeTests, Components/WorkflowNodeTests, Repositories/RepositoryTests}.cs`.

## Next step

Phase 3 (marquee selection, `ChannelActionExecutor`, Start/Stop/Pause/Continue/Transfer actions,
system-error reset) is specced (D12-D13) but not planned or started. Same brainstorm→spec→plan→
execute cycle as the prior two phases.
