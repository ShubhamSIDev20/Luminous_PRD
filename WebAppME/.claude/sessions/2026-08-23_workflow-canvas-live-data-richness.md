# Session #26 — Workflow Canvas: dashboard-depth live telemetry (T-47 follow-up)

**Date:** 2026-08-23
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` — ⚠️ NOT MERGED
**Task:** [T-47](../tasks/2026-08-22_workflow-canvas-playground.md) (follow-up scope, same task file)
**Status:** ✅ Done, live-verified in browser. Commit `90d9b8d`.

## Goal

User asked to launch the app+simulator and bring "all functionality" from the existing
dashboard onto the workflow canvas. Clarified via AskUserQuestion into a bounded scope: **same
live data, no hardware actions** (preserves spec decision D2 — canvas records intent only).
Compared `DeviceChannel.razor`'s live-data surface against the canvas's `ChannelTelemetry` (only
had Status/Soc-placeholder/Voltage/Current) and proposed — approved in chat — a two-tier design:
Power+Temperature on the node face (glanceable), everything else in the properties dock on
selection (dashboard depth without cluttering the node).

## What was done

TDD: extended `ChannelTelemetry`/`TelemetryEntry` (Models/DTOs/Workflow/TelemetryEntry.cs) with
Power, Temperature, 4 capacity fields, 4 energy fields, CycleNumber, table/running-time (pre-
formatted strings), and a resolved `ErrorText`. New trailing fields on `ChannelTelemetry` all
default to zero/null so the existing positional test constructors kept compiling unchanged.

- `WorkflowTelemetryBridge.BuildEntries` now takes optional `errors`/`messages`
  (`IReadOnlyList<CodeMessageDto>`) and formats table progress (`"3 / 12"`) and durations
  (`hh:mm:ss`) so JS stays a dumb text-setter, matching how voltage/current/soc were already sent
  pre-formatted.
- New `WorkflowTelemetryBridge.ResolveErrorText` mirrors `DeviceChannel.razor`'s ErrorId/
  SystemErrorId resolution (Errors list when status==Error, else Messages list; SystemErrorId
  decoded as a `SystemError` bitmask) — **deliberately omits** the dashboard's network-disconnect
  special case (folding in `en_ERR_NETWORK_CONN_FAIL` when `Channel.IsConnected == false`), since
  the canvas has no connection-state concept to key it on. Documented as a known simplification.
- `ChannelNode.razor` gained a Power/Temperature row under Voltage/Current.
- `PropertiesDock.razor` gained a `.wf-dock__live` section (channel-only), 15 read-only rows,
  tagged `data-node-id="@Node.Id"` — found by JS the same way `.wf-node` is found, so telemetry
  writes to it are plain DOM text-sets, never a Blazor re-render.
- `workflow-canvas.js`'s `applyTelemetry` writes the two new node-face fields and calls a new
  `applyDockLiveData(s, e)` that looks up `.wf-dock__live[data-node-id="..."]` under
  `s.host.parentElement` (`.wf-body`, a sibling of `.wf-canvas-host`) and writes all 15 dock rows.
- `WorkflowCanvasPage.razor`'s `PushTelemetryAsync` now populates every new `ChannelTelemetry`
  field from `RealTimeRecordDto` and passes `ErrorMessages.Errors`/`ErrorMessages.Messages`
  (static lists in `Components/UI/Program/OperatorConstants.cs`, namespace
  `BatteryTestingSystem.Components.UI` — **not** `.Program`, despite the folder name) into
  `BuildEntries`.

9 new unit tests added to `WorkflowTelemetryBridgeTests` (dock-depth fields + `ResolveErrorText`),
3 new bUnit tests to `PropertiesDockTests` (live-data container present/gated/placeholders).
374/375 pass; 1 pre-existing flaky timing test (`DashboardRenderBatcherTests`) failed under full-
suite load, passed in isolation — same flake documented in session #25, not a regression.

## Live verification

Restarted the app (stopped PID 10524/25692 to free the build lock, `dotnet build`, relaunched via
PowerShell background process per the established pattern) with the simulator still running.
Placed a channel node in browser via chrome-devtools MCP (claude-in-chrome extension was not
connected this session — used chrome-devtools MCP instead): node face showed the new "-- W / --
°C" row; selecting the node showed the dock's full 15-row "Live data" section with correct
placeholders and the container's `data-node-id` matching the selected node. Circuits stayed
"offline" (idle bench, no program running) so live *values* weren't exercised end-to-end this
session — the wiring, gating, and DOM structure were all confirmed correct, matching the same
placeholder-until-first-event behavior already verified for voltage/current in session #25.

## Gotchas discovered

- **`ErrorMessages`'s actual namespace is `BatteryTestingSystem.Components.UI`**, not
  `.Components.UI.Program` — the file lives at `Components/UI/Program/OperatorConstants.cs` but
  the folder does not match the namespace declaration. Found by grepping the file directly rather
  than assuming folder-path-as-namespace.
- **Two hardware simulator processes were running simultaneously** (`run_sim.py` and
  `simulator.py`, both `-d 10 -n 64`) from a prior session — pre-existing environment state, not
  touched this session; left running since stopping either wasn't requested and wasn't necessary
  to verify the DOM wiring.
- `claude-in-chrome` MCP tools reported "Browser extension is not connected" this session;
  `chrome-devtools` MCP tools worked fine as the fallback for live verification.

## Next step

Unchanged from session #25: the branch stays until a decision is made on `WorkflowAutoLayout`'s
single-column scale problem. This session's live-data work is complete and additive; it does not
change that recommendation.
