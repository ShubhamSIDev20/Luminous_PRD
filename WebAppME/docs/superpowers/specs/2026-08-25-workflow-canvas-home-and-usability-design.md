# Workflow Canvas: Home Landing, Board Consolidation & Usability — Design

**Branch:** `feat/workflow-canvas-experiment` — still a permanent, never-merged-to-`main` side branch
(D14/D15 unchanged). Nothing in this spec touches that git-workflow decision.

**Builds on:** `docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md` (D1-D16),
implemented across Phases 1-3 (all complete as of 2026-08-25).

## Context

A live-usage review of the Phase-1-3 canvas surfaced 7 follow-up requests, spanning a node-model
simplification, two correctness fixes, a cross-workflow data-integrity rule, a toolbar redesign,
and — the most consequential — making this canvas the login landing experience instead of the
Dashboard's always-show-everything view. That last item is a genuine **product** decision (should
this canvas eventually replace the Dashboard's role for day-to-day monitoring), separate from the
**git** decision that this branch never merges to `main` (D14/D15, unchanged). Both are addressed
below without conflating them.

## Decisions

| # | Decision |
|---|---|
| **D17** | The Dashboard page and its menu entry are **not removed**. `WorkflowMenu.Build` swaps which `NavMenuItem` owns `Url = "/"` — "Workflows" claims it, "Display" moves to a fixed `/Dashboard` route (already supported today) and stays in the menu. Nothing about the Dashboard's own code changes. |
| **D18** | The Board node is removed as a **rendered, independently-positioned canvas node**, but `NodeKind.Board` stays in the persisted `WorkflowGraph` model unchanged — zero migration risk, no existing saved layout needs rewriting. A Device node instead renders one small numbered "board slot" chip per secondary board it has placed channels on; edges that used to run Device→Board→Channel now run directly from a slot chip to that board's channel nodes. This is a rendering-layer change only (`CanvasSurface`, `DeviceNode`, `EdgeLayer`), not a data-model change. |
| **D19** | A board slot's visual status uses the **same online/offline dot language** already used on Device and Channel nodes (not the old "N/M online" count), driven by that board's TCP link state. Clicking a slot still collapses/expands that board's channels on the canvas — the one behavior carried over from the old `BoardNode.OnToggleCollapse`. |
| **D20** | The flow/pulse animation (`wf-node--active`, `wf-edge--flowing`) gains an explicit `ProgramStatus == Running` requirement, on top of the existing Charge/Discharging + nonzero-current check. A channel that isn't actually running a program never animates, regardless of its raw current reading. |
| **D21** | Canvas telemetry push cadence becomes a user-configurable "Refresh rate" (ms), mirroring the Dashboard's per-card `CardSettings.RefreshRate`. It replaces the current fixed 250ms coalescing-flush interval with a value the user sets, surfaced in the new toolbar box (D23). |
| **D22** | **Per-user cross-workflow channel exclusivity.** A channel already placed in one of a user's *saved* workflows is disabled (greyed, with a tooltip naming which workflow holds it) in the palette while editing any of that user's other workflows. Enforced at the palette/place level, not at save time — placing a channel already claimed elsewhere is prevented before it happens, not rolled back after. |
| **D23** | The current top toolbar (Fit / Node fields / Layout switcher+Save+Save as+Delete / Animations toggle) is replaced by one floating icon-button box anchored **top-right** of the canvas — same visual language as the existing bottom-anchored Selection Action Bar. It additionally gains: a status-color legend popover, clickable status-color chips that filter the canvas (dim/hide non-matching channels, mirroring the Dashboard's own status-chip filter), and the new refresh-rate control (D21). |
| **D24** | `TabService.AddTab`'s hardcoded `navMenuItem.Title == "Display"` → relabel-as-"Home" special case is generalized to whichever item owns `Url == "/"`, so the Home tab's title/icon follow root ownership rather than a specific page's identity — required for D17 to work without a second hardcoded special case. |

## Section 1 — Board Node Consolidation (D18/D19)

**Today:** `CanvasSurface` renders one `<BoardNode>` per `NodeKind.Board` node, independently
positioned by `WorkflowAutoLayout`'s per-device-lane algorithm; edges connect Device→Board and
Board→Channel; `BoardNode` shows a live `N/M online` count (via `WorkflowNodeLabeller
.ChannelIdsForNodeEntity`) and a Collapse/Expand button that hides/shows its channel nodes
(a view-only projection — never mutates the saved graph, per the original spec's Task 13 pin).

**After:** `DeviceNode` groups its own children by board number (same
`ChannelIdsForNodeEntity(NodeKind.Board, boardId)` lookup, no new data needed) and renders one
small numbered chip per board directly inside the Device card. `CanvasSurface` stops instantiating
a separate `BoardNode` for `NodeKind.Board` entries entirely — the Board nodes still *exist* in
`WorkflowGraph.Nodes` (so `WorkflowGraphMutations`, `WorkflowAutoLayout`'s lane math, and every
existing saved layout keep working untouched), they are simply excluded from the node-rendering
switch. `EdgeLayer` re-targets any edge whose endpoint is a Board node to instead terminate at that
board's chip's screen position inside the Device card (a new small helper, since edges currently
assume both endpoints are independently-positioned top-level nodes).

Each chip shows an online/offline dot in the same CSS custom-property-driven style as Device/Channel
nodes (`--wf-status`), sourced from whether any channel on that board reports `IsConnected` (or,
if a per-board `IChannelCommandHandler`/`DeviceConnection`-level connection flag already exists,
that instead — confirm exact source during planning). Clicking a chip toggles that board's
`CollapsedBoardIds` entry, identical semantics to today's Collapse button, just relocated.

**Out of scope:** deleting `NodeKind.Board` from the enum, `BoardNode.razor` itself (kept as dead
code is wasteful — it gets deleted as part of this work, but the *node kind* and *persisted data*
do not change), and any layout-algorithm change (`WorkflowAutoLayout`'s per-device-lane math is
unaffected since it still lays out Board nodes for positioning math even though they no longer
render their own card).

## Section 2 — Animation Correctness (D20)

**Today:** `WorkflowTelemetryBridge.FlowFor(CircuitStatus status, double current)` returns
±1/0 based on `Charge`/`Discharging` + `|current| >= 0.001`; `workflow-canvas.js` toggles
`wf-node--active` and `wf-edge--flowing`/`wf-edge--flowing-reverse` off that `Flow` value alone.

**After:** `FlowFor` (or its caller in `BuildEntries`) gains a `ProgramRunningStatus programStatus`
parameter and returns `0` unless `programStatus == Running`, regardless of circuit status/current.
This is a pure logic change with a clear existing test file to extend
(`WorkflowTelemetryBridgeTests.cs`) — no JS change needed, since the JS side already just reads
whatever `Flow` value C# computed.

## Section 3 — Refresh Rate Control (D21)

**Today:** `WorkflowCanvasPage._telemetryRunner` (a `CoalescingRunner`) flushes on a fixed interval
inside the runner's own implementation (ADR-6's 250ms pattern, shared with the Dashboard's hardware
coalescing). Canvas telemetry is otherwise event-driven, not polled.

**After:** Add a `RefreshRateMs` field to the existing `WorkflowNodeConfig` (already the per-user
persisted config object serving the property picker, stored via `ServerSessionStorageService`
under `{UserId}_canvas_node_config` — reusing this key rather than inventing a second per-user
settings blob). The page's `CoalescingRunner` needs a settable flush interval — check whether
`CoalescingRunner` currently hardcodes 250ms internally or accepts it as a constructor/method
parameter; if hardcoded, it needs a small, backward-compatible change (default parameter preserving
the existing 250ms for the Dashboard's own coalescing use, which must not regress).

## Section 4 — Per-User Cross-Workflow Channel Exclusivity (D22)

**New capability on `IWorkflowLayoutService`** (or a focused new service, decided at planning time):
given a `userId` and the currently-open layout's id (nullable, for an unsaved/new layout), return
which channel `EntityId`s are already claimed by the user's *other* saved layouts, and by which
layout name (for the disabled-palette-item tooltip). This reads the raw `LayoutJson` documents for
that user (excluding the current layout id) — it does not need the full `LoadAsync`/`TopologySnapshot`
rehydration path, just the channel-node `EntityId`s out of each document's JSON.

`PalettePanel` (and whatever drag/click "place channel" affordance it exposes) consumes this map:
a channel present in it renders disabled with a tooltip ("Already placed in workflow '<name>'").
Enforcement point is the palette, not a post-save conflict check — a user is never allowed to place
a claimed channel in a second workflow to begin with.

**Open question for planning:** does this recheck live-update while a workflow is open (another of
the user's sessions saves a workflow claiming a channel this session's palette still shows as
free)? Recommend: recheck on every `SaveAsync`/`LoadAsync` of the *current* page, not via a live
cross-session subscription — simplicity over a real-time guarantee that hardware placement doesn't
actually need.

## Section 5 — Canvas as Home Landing (D17/D24)

**Today:** `WorkflowMenu.Build` assigns `Url = "/"` to the "Display" (Dashboard) `NavMenuItem` and
`Url = "/workflows"` to "Workflows". `DashboardView.razor` is separately reachable at its own
`@page "/Dashboard"` route regardless of the menu's `Url` field (confirmed by reading the file).
`TabService.AddTab` special-cases `navMenuItem.Title == "Display"` to rename that tab "Home" with a
house icon — this is the only place "home" identity is hardcoded anywhere in the nav stack.

**After:**
1. `WorkflowMenu.Build`: swap the `Url` values — "Display" gets `Url = "/Dashboard"`, "Workflows"
   gets `Url = "/"`.
2. `WorkflowCanvasPage.razor` needs a second `@page "/"` directive alongside its existing
   `@page "/workflows"` (matching the pattern `TabView.razor` already uses for its own two routes).
3. `TabService.AddTab`'s special case changes from `navMenuItem.Title == "Display"` to
   `navMenuItem.Url == "/"` (D24) — this makes "Home" tab identity follow root ownership instead of
   a specific page, so this swap (and any future one) doesn't need a second hardcoded special case.
4. Dashboard remains fully reachable via its menu entry and its `/Dashboard` route — no code inside
   `DashboardView.razor` changes at all.

**Confirm at planning time:** whether `AppLayout`'s logo-click target reads `MenuItems[0].Url` or
a separately-hardcoded path — the plan needs to trace this exactly (this design doc identified the
mechanism but did not fully verify every consumer of `NavMenuItem.Url == "/"`).

## Section 6 — Status Color Legend (part of D23)

A small popover (opened from the new toolbar box) listing every `CircuitStatus` value's swatch +
name, reusing the existing `WorkflowStatusCss`/`StatusHslByName` color source so the legend can
never drift from the actual node-face colors.

## Section 7 — Consolidated Top-Right Toolbar Box (D23)

Replaces today's top toolbar row entirely. New floating box, same CSS pattern as
`SelectionActionBar` (`position: absolute`, card background, border, shadow) but anchored
`top: 1rem; right: 1rem` instead of bottom-center. Contents, all icon buttons:
- Fit
- Node fields (opens the existing `NodePropertyPicker`)
- Layouts (opens a popover with the existing `LayoutSwitcher` combobox + Save/Save as/Delete —
  reused as-is, just relocated into a popover instead of being permanently visible in a toolbar row)
- Animations on/off toggle (unchanged behavior, relocated)
- Legend (Section 6)
- Refresh rate (Section 3)
- One clickable chip per `CircuitStatus`, filtering the canvas (dim or hide non-matching channel
  nodes — exact visual treatment decided at planning time, matching whichever the Dashboard's own
  status-chip filter already does for consistency)

## Testing Strategy

- Section 2 (animation): pure C# logic change, unit-testable exactly like the existing
  `WorkflowTelemetryBridgeTests.cs` — extend, don't replace.
- Section 4 (exclusivity): new service method is pure-logic-testable against an in-memory set of
  fake `LayoutJson` documents, no browser needed for the core rule; palette-disables-claimed-channel
  is a bUnit test on `PalettePanel`.
- Sections 1, 3, 5, 6, 7: JS/CSS/routing/config changes with no meaningful C# unit-test surface —
  browser-verified live against the simulator, matching this branch's established practice for
  every prior interaction feature (pan, zoom, drag, connect, marquee).

## Suggested Implementation Phasing

Matching how Phases 1-3 of the prior spec were each their own plan, this spec spans independent
enough concerns to warrant more than one implementation plan:

- **Phase 4 — Board consolidation + animation fix** (Sections 1, 2): the two changes with the
  largest rendering/logic footprint, best isolated from the smaller usability items.
- **Phase 5 — Toolbar redesign + legend + refresh rate** (Sections 3, 6, 7): one cohesive UI pass,
  since all three land in the same new toolbar box.
- **Phase 6 — Home landing + cross-workflow exclusivity** (Sections 4, 5): the two decisions with
  product/behavioral consequences beyond pure UI, grouped together for a focused review.

Each phase gets its own `docs/superpowers/plans/YYYY-MM-DD-workflow-canvas-phaseN.md`, TDD
task-by-task execution, and live browser verification, per this branch's established practice.

## Risks / Open Questions Carried Into Planning

1. Section 1's exact source for a board's online/offline state (per-board `DeviceConnection` flag
   vs. "any channel on this board connected") needs confirming against `ChannelManager`/`DeviceLink`
   before implementation.
2. Section 3's `CoalescingRunner` flush-interval configurability must not change the Dashboard's own
   250ms hardware-coalescing behavior (ADR-6) — shared class, scoped change.
3. Section 5's full inventory of `NavMenuItem.Url == "/"` consumers beyond `TabService.AddTab` needs
   a complete trace before swapping, to avoid missing a second hardcoded assumption.
4. Section 4's live-multi-session consistency question is explicitly deferred to "recheck on
   save/load" rather than solved with real-time cross-session sync — flag if this doesn't match the
   user's actual expectation.
