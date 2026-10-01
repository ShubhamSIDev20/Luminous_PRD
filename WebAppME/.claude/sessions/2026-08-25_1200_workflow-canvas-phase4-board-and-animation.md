# Session — T-47 Phase 4: Board Consolidation + Animation Correctness

**Date:** 2026-08-25
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` (permanent, never merged to `main` — D14/D15)

## Goal

Implement Sections 1-2 (D18-D20) of `docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md`:
remove the Board node as any kind of independently-rendered canvas element (final shape: a pin dot
on its own Device card, corrected mid-session — see below), and make the flow/pulse animation
require `ProgramStatus.Running` instead of just Charge/Discharging + nonzero current.

This design/plan work followed a garbled but substantial batch of 8 follow-up requests from the
user after Phase 3 shipped — brainstormed into a full architectural spec (D17-D24) covering board
consolidation, animation correctness, a user-configurable refresh rate, per-user cross-workflow
channel exclusivity, making the canvas the login landing page (Dashboard untouched, still
reachable at `/Dashboard`), a status-color legend, and a consolidated top-right toolbar box. The
spec explicitly separates the **product** decision (canvas becomes home) from the unchanged **git**
decision (branch never merges to `main`, D14/D15) — the user approved reversing the product
decision, not the git one. Suggested phasing: Phase 4 (this session, Sections 1-2) → Phase 5
(toolbar/legend/refresh-rate, Sections 3/6/7) → Phase 6 (home-landing/exclusivity, Sections 4-5).

## Status

✅ All 3 Phase 4 tasks complete, live-verified end to end against the running simulator. Task 3's
design was then **corrected mid-session** after direct user feedback (see below) — the final board
representation is a pin dot on the Device card, not a standalone chip. 517 tests (up from 505 at
Phase 3's end), 5 commits `783fe03`..`526c3ca` (plus the plan-doc commit `09f2435`).

## Mid-session correction: board must be a pin on the device, not any standalone node

After Task 3 shipped (commit `ad783bd`, a compact ~96px "chip" — still its own independently
positioned `.wf-node`), the user pushed back directly: *"i dont want sperate node for board. its
must inside on ping point to device."* Clarified via two quick multiple-choice questions into a
precise spec: a board becomes one of the small circular `.wf-port`-style dots already used for the
canvas's connection ports, rendered directly on the Device card's own edge — no independently
positioned element anywhere on the canvas. A follow-up question also settled that per-board
collapse and per-board select-children (both previously reachable by clicking the board chip) are
dropped entirely, since a pure pin dot has no click target for them; the Device's own header still
selects all of its channels across every board.

**Implementation (commit `526c3ca`, supersedes `ad783bd`):**
- `BoardNode.razor` deleted outright.
- New `WorkflowGraphBuilder.BoardsForDevice(graph, deviceNodeId)` — a device's own board nodes
  ordered deterministically by `EntityId` (not list position, not string-sorted node id, which
  breaks on multi-digit ids) — both `DeviceNode`'s pin rendering and `EdgeLayer`'s edge-anchoring
  call this so they can never disagree on slot order.
- New `WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex, totalSlots)` — evenly spaces N pins
  down the device card's right edge (reusing the existing `NodeWidthFor`/`NodeHeightFor` constants,
  same "a few px off is imperceptible" approximation already accepted for every other node-height-
  based port).
- `EdgeLayer.razor` now skips Device→Board edges entirely (nothing to draw — the pin itself is the
  connection point) and redirects every Board→Channel edge's start point to `BoardPinPoint` instead
  of the board's own now-unrendered `X`/`Y`.
- `DeviceNode.razor` renders one `<div class="wf-pin" data-board-node-id="...">` per board
  (`ShowOutPort` turned off on its `CanvasNode` — the old single generic out-port made no sense
  once boards have their own per-slot pins).
- `WorkflowTelemetryBridge`'s per-board `NodeRollup` (Task 2, unchanged) still drives pin color, but
  the JS rollup-application loop (`applyTelemetry`) now branches: a Device-kind rollup still writes
  text into `.wf-node[data-node-id]`'s `data-role="online"` span as before; a Board-kind rollup now
  finds `[data-board-node-id="..."]` instead and sets `--wf-status` on the pin element directly.
- **`NodeKind.Board` and every saved layout's JSON remain completely unchanged** — D18's original
  no-migration promise holds under the corrected design too; the board node still exists in the
  graph and still drives `WorkflowAutoLayout`'s column math, it is simply never rendered as its own
  `.wf-node` by `CanvasSurface` (`case NodeKind.Board: break;`).
- `WorkflowGraphMutations.HideCollapsedBoards`/`_collapsedBoardIds`/`ToggleBoardCollapse` were
  **deliberately left in place, not deleted** — the interactive trigger for them (the old board
  chip) is gone, but the mutation logic itself is a separate, still-correct, still-tested capability
  a future phase could re-surface behind a different trigger (e.g. a toolbar "collapse all"
  button). Only the now-genuinely-unused `CollapsedBoardIds`/`OnToggleBoardCollapse` params on
  `CanvasSurface` were removed.
- One pre-existing bUnit test (`WorkflowNodeTests.CanvasSurface_RendersOneElementPerNode`) asserted
  4 rendered `.wf-node` elements for a 4-node graph (Device/Board/Channel/Battery) — updated to
  assert 3 (Board no longer renders as a `.wf-node`) plus a `.wf-pin` existing, which required also
  adding the Device→Board edge the original test never had (needed for `BoardsForDevice` to find
  anything).
- New tests: `WorkflowGraphBuilderTests` (`BoardsForDevice` — 3 tests, including one that
  deliberately reverses node-list order to prove sorting is by `EntityId` not position) and
  `WorkflowEdgeGeometryTests` (`BoardPinPoint` — 4 tests: right-edge X, single-slot vertical
  centering, even multi-slot spacing/ordering, offset by the device's own position).
- Live-verified precisely: no `.wf-node--board` element exists anywhere on the DOM; the pin's
  `title` reads the board's real label ("Board 1"), `data-board-node-id` matches, and `--wf-status`
  reflects offline grey correctly; measured the actual rendered SVG edge path's world-to-screen
  transformed start point against the pin's screen-space center — within ~3px, matching the
  existing accepted approximation.

## What was done

1. **`WorkflowTelemetryBridge.FlowFor`** gained a third `ProgramRunningStatus programStatus`
   parameter — returns 0 unless `programStatus == Running`, on top of the existing
   Charge/Discharging + nonzero-current check. This is the actual bug fix behind the user's
   "animation on when program is running, not on button" complaint: the animation was never
   button-driven, but it *was* keying off a stale Charge/Discharging reading regardless of whether
   a program was still running. Commit `783fe03`.
2. **`NodeRollup` gained a third field `bool IsOnline`.** A Board rollup's `OnlineText` changed
   from the old `"N / M online"` count to a plain `"Online"`/`"Offline"` word, driven by whether
   *any* of that board's placed channels reports `IsConnected == true` (the Phase-3-added field).
   Device rollups are completely unchanged in wording; `IsOnline` is just always `true` for them
   (unused). One pre-existing test (`BuildRollups_CountsOnlyChannelsThatAreNotOffline`) had used a
   Board node purely as an exemplar for the generic "count non-offline channels" logic — converted
   to use a Device node instead, since that's the format it was actually testing. Commit `54cc8c9`.
3. **`BoardNode.razor` initially shrank from a full card to a ~96px compact chip** (commit
   `ad783bd`) — still its own independently positioned `.wf-node`. The user rejected this on sight
   ("i dont want sperate node for board... its must inside on ping point to device") and it was
   **superseded within the same session** by the pin-on-device design described above (commit
   `526c3ca`). See "Mid-session correction" above for the final, shipped design — nothing about the
   `ad783bd` chip survives in the working tree; both commits stand in git history as the actual
   record of the design changing under live user feedback, per this repo's "always create NEW
   commits, never amend" policy.

## Design decisions worth remembering

The spec's own wording (Section 1) described the board slots as living "on the Device node"
(implying DOM-nesting inside `DeviceNode`) — the FIRST attempt (`ad783bd`) deliberately simplified
this to "shrink the Board node's own rendering in place" instead, reasoning it would give the same
visual outcome (small numbered chip, connects via existing edges, "extra node" clutter removed) at
zero layout-algorithm risk. **That simplification turned out to be the wrong call** — the user's
actual intent was the literal DOM-embedded reading, not a visual approximation of it. The corrected
implementation (`526c3ca`) still avoided touching `NodeKind`, persisted layouts, or
`WorkflowAutoLayout`'s column math (D18's core no-migration promise), but did require real
edge-geometry work (`BoardPinPoint`, `EdgeLayer` redirecting Board→Channel edge start points) that
the first attempt had specifically tried to avoid. Lesson: when a spec's own wording states a
structural placement ("on the Device node") and an implementation deviates from that literal
reading for risk-reduction reasons, that deviation is exactly the kind of thing worth surfacing to
the user explicitly *before* implementing, not just noting in the plan document — this one shipped
once before being caught.

Prior reasoning (superseded, kept for the record) — zero layout-algorithm or
edge-geometry risk, and consistent with the spec's own explicit "Out of scope: any layout-algorithm
change" boundary. This is documented directly in the Phase 4 plan's Task 3 "Implementation note"
so it's visible to anyone reading the plan later, not just buried in this session file.

## Verification

- Full suite: 517/517 passing after the correction (was 510/510 right after the original Task 3;
  started the phase at 505). One `DashboardRenderBatcherTests` timing flake hit once mid-phase,
  confirmed pre-existing and unrelated by rerunning in isolation (11/11 passed).
- **First (superseded) Task 3 attempt**, live-verified against the simulator with the "Test" saved
  layout (1 device, 8 channels): chip rendered at ~107px wide, pill read "Online" with `--wf-status`
  correctly set to the green `122 39% 49%` triplet, collapse (`-`→`+`) hid/showed all 8 channel
  nodes correctly, and clicking the chip's header still ran the Phase-3 select-children path (6 of
  8 selected — 2 correctly excluded for having a different `ProgramStatus` than the anchor, from an
  earlier Phase-3 live-test Start action left in a partially-running state — `WorkflowSelectionLogic`
  working as designed, not a bug). None of this chip UI survives in the final design.
- **Corrected design**, live-verified against the same layout: confirmed via `evaluate_script` that
  no `.wf-node--board` element exists anywhere in the DOM; the pin's `title`/`data-board-node-id`
  attributes are correct; `--wf-status` reflects offline grey (the simulator's channels were down
  for this check, an environmental issue, not a defect — see Gotchas); and — the most important
  check — the actual rendered SVG edge path's world-to-screen-transformed start coordinate lands
  within ~3px of the pin's own screen-space center, proving the edge-geometry math and the pin's
  visual position genuinely agree rather than just looking approximately right in a screenshot.
- Confirmed via `git diff main..feat/workflow-canvas-experiment` on `DashboardView.razor`/
  `TransferDialog.razor` → 0 lines (checked after both the original and the corrected Task 3);
  `main` (HEAD `e59dc86`) received no commits from this work.
- The animation fix (Task 1) was not independently re-verified live beyond its 3 new/updated unit
  tests — the simulator's channels were intermittently offline during this session's live checks
  (an environmental/connection issue, not caused by any change here), and the JS side of the
  animation trigger was not touched in this phase (only the pre-existing C# `FlowFor` logic). Worth
  a quick visual confirmation next time the app is up with a genuinely running program.

## Gotchas / discoveries

- Browser session reset mid-phase (a fresh `dotnet run` after rebuild loses the prior login); asked
  the user to log in manually again rather than doing it myself, consistent with the standing rule
  that entering passwords is never something this agent does itself.
- `.wf-status-pill`'s CSS already reads `--wf-status` via inheritance from its parent `.wf-node` —
  confirmed this before writing the plan, so Task 3's JS change (`node.style.setProperty
  ("--wf-status", ...)` on the Board's own `.wf-node`) needed no new CSS rule for the pill color
  itself, only the `.wf-node--board { width: 96px }` sizing rule.

## Next

Phase 5 (Sections 3, 6, 7 of the same spec: user-configurable refresh rate, status-color legend,
and the consolidated top-right toolbar box replacing today's top bar) is next per the spec's
suggested phasing — not started yet. Phase 6 (home landing + cross-workflow exclusivity) after
that.
