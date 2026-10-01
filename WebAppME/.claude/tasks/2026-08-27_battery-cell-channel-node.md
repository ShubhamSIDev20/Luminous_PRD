# T-48 — Channel node as a battery cell (workflow canvas)

> Created: 2026-08-27T11:00:00Z
> Branch: `feat/workflow-canvas-experiment` (experimental — never merge to `main` without an explicit decision, per T-47 D14/D15)
> Status: ✅ **Complete and live-verified.** 639 tests (from 597). 9 feature commits + spec/plan,
> plus 7 follow-up rounds through `1c83501` (latest: row spacing now tracks the selected field
> list instead of a worst-case constant, and a channel added later lands beside its already-placed
> siblings instead of in a new lane below everything).
> Spec: [docs/superpowers/specs/2026-08-27-workflow-canvas-battery-cell-node-design.md](../../docs/superpowers/specs/2026-08-27-workflow-canvas-battery-cell-node-design.md)
> Session: [sessions/2026-08-27_1100_workflow-canvas-battery-cell-design.md](../sessions/2026-08-27_1100_workflow-canvas-battery-cell-design.md)

---

## Goal

Redraw the canvas channel node as a battery cell that carries its own live data — the login page's
cap/body/well/gradient/glow, channel number stamped on the cap, the dashboard card's
`RenderNormal` arrangement inside, and a segment-march fill animation driven by `CircuitStatus`
(idle shows **STOP**), with the fill translucent enough that every value stays readable at any
user-chosen status colour.

## Approved design in one line

Shape A (battery *is* the node) · channel number on the cap · march variant A2 (motion in the
empty region above the real SoC, mirrored downward inside the fill for discharge) · fill treatment
T1 (20% alpha) · Primary Data two unlabelled columns, left/right aligned · Configuration and
Program Data as labelled rows · everything in normal flow inside a CSS/Tailwind frame.

## Progress

- [x] Explore existing channel node, canvas geometry, telemetry bridge, login battery, status tokens
- [x] Eight rounds of rendered mockups; 10 design decisions taken with the user
- [x] Design spec written and committed (`d32c8f8`, amended `b36abdd` + reserved-strip fix)
- [x] User approved; implementation plan written (`4d8203a`) — 10 tasks
- [x] Task 1 — `StateOfChargeEstimator` (7 tests) `dcb…`
- [x] Task 2 — SoC + battery ratings through telemetry (5 tests) `e8efa69`
- [x] Task 3 — `WorkflowNodeProperties.SectionsFor` (5 tests) `79b80b3`
- [x] Task 4 — `CanvasNode.HeaderContent` slot (3 tests) `8abf8b2`
- [x] Task 5 — `ChannelBatteryCell` + `ChannelNode` rewrite (13 tests) `c0ba8c4`
- [x] Task 6 — cell CSS + rewritten LOD tiers + version bump `18cb7b7`
- [x] Task 7 — JS telemetry bridge drives the cell `3787d4e`
- [x] Task 8+9 — measured height (9 points, all exact) + geometry threading `5f1dcde`
- [x] Task 10 — live verification, found and fixed 4 defects `69168ed`

**State:** ✅ 635 tests, build clean, live-verified against the running app at a 1-device /
8-channel simulator bench. Final measured geometry: chrome **114**, heading 19, row 15, gap 1;
all 28 properties = **520px**. Edges attach at 0px deviation; no overlap or text spill at any
field count.

**Defects found by the live look (all fixed):** duplicate unit in Primary Data (the bridge already
writes value+unit); a 15.6px cap-to-cell gap and a visible card border, both from `CanvasNode`'s
shared card chrome being wrong for a node that IS the battery; and a badge strip adding 22.5px the
height formula could not predict. The "red fixed cap" was not a defect — it is a saved user
override for the idle status colour.

**Not verified live:** the charge/discharge segment march on real hardware. The bench was idle and
starting a program energises a battery, so it was verified mechanically on a DOM clone instead.

## Known hazards for the implementation (all detailed in the spec)

1. **`ChannelNodeHeight(int)` must become `ChannelNodeHeight(IReadOnlyList<string>)`** — height now
   depends on *which* properties are selected, not how many. Ripples to `WorkflowAutoLayout`,
   `WorkflowEdgeGeometry`, `MinimapProjection`, `WorkflowPlacement`, `CanvasSurface.razor`.
   Run `verify_change` first.
2. **The cap must carry `wf-node__header`** — `workflow-canvas.js:373` gates node dragging on that
   selector. No unit test can catch the regression; needs a real CDP click.
3. **LOD tier-1 CSS becomes a no-op** — it hides `.wf-node__body > :nth-child(n+2)`, and the new
   body has one child. Tiers must be rewritten.
4. **`.wf-cell*` is already owned by `BatteryGlyph`** — use `.wf-bcell*` or every battery node
   gets restyled.
5. **Measure the height constants in a browser before writing them into C#** — session 36's
   `106 + 16` was correct at exactly three rows and nowhere else.
6. **Bump `?version=` on the canvas JS/CSS in `App.razor`** — otherwise the new markup ships
   against cached assets and renders unstyled.
7. **Status hue must come from `WorkflowStatusCss.Var`**, never `status.ToString().ToLower()` —
   `CircuitStatus.Countinue` is misspelled while the variable is `--status-continue`.

## Open questions for the user

1. `Idle` → **STOP** in the cell only, or everywhere (legend + filter bar too)?
2. Battery spec line (`50 Ah · 3.7 V · 1 cell` from `Batteries`) — currently out of scope, needs a
   new data path. Wanted?
3. SoC `%` chip in the header, or on the cap next to the channel number?

## Relationship to T-47

A follow-on sub-project of T-47 (Workflow Canvas Playground), same branch. T-47's own scope is
closed; this is new presentation work arising from the user's live testing in session 36.
