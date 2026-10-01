# Session — T-47 Sub-project A: Toolbar/Palette Regression Fixes

**Date:** 2026-08-26
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` (permanent, never merged to `main` — D14/D15)

## Context

The user reported a 12-item batch of workflow-canvas problems. Brainstorming classified it as
**architectural** and decomposed it into 4 sub-projects rather than one spec:

- **A — Regression fixes** (this session): popup overlap, toolbar actions not working, canvas hangs
- **B — Canvas presentation**: status-color labels + user-customizable colors (canvas-only,
  per-user), remove the top bar and put the color bar there instead, line grid instead of dots,
  node spacing so channels don't overlap
- **C — Dashboard data parity**: missing node fields, the multi-tab detail dialog
  (Manufacturing/Factory/Battery/Calibration/DBC), DBC parameter view in the properties panel
- **D — TabViewer navigation**: make TabViewer the real navigation model with Workflows as the
  default tab

**Two decisions the user made during brainstorming that change earlier work:**
1. **Navigation model = TabViewer, not per-page routes.** This *reopens* Phase 6's D17 decision —
   the `Login.cshtml.cs` redirect-to-`/workflows` fix assumed per-page routing is the real model.
   Sub-project D must revisit it.
2. **Parity scope = "data + dialog first"** — all missing telemetry/config fields, the full
   multi-tab dialog, and the DBC view; chart/gear/context-menu deferred to a later pass.

Also clarified: item 2 said "check home dashboard" for the color-customization purpose, but the
Dashboard has per-card **field** configuration, not a status-color picker. Confirmed with the user
that canvas status-color customization is a **new** capability (canvas-only, per-user), not a
mirror of something that already exists.

## Status

✅ Sub-project A complete. 540 tests (up from 536), 1 commit `cc557c7`. Sub-projects B/C/D not
started.

## Three root causes (systematic-debugging, Phase 1 before any fix)

| Symptom (user's words) | Root cause | Origin |
|---|---|---|
| "pop up ... has no gaps its overlay" | `.wf-property-picker`, `.wf-legend`, `.wf-toolbar-popover` all declared the **identical** `top: 3rem; right: 1rem; z-index: 20` — they stacked on each other, and under the toolbar once it grew past 3rem tall | My Phase 5 |
| "action ... not working" (the new Layouts/colors/refresh-rate buttons, NOT Start/Stop) | `.wf-status-chip-row` was referenced in `CanvasToolbar.razor` from the day it was written but **never defined in CSS** — with no `display:flex` the 9 chips stacked as blocks, inflating the toolbar to ~180px and swallowing every `top:3rem` panel underneath it | My Phase 5 |
| "workflow ... hang sometime" | `PalettePanel.IsChannelPlaced` did a linear `Graph.Nodes` scan **per channel**, and `IsPlaced` called it once per channel per device → O(devices × channels × nodes) ≈ **450k comparisons per render** at 640 channels, on a component that re-renders every telemetry tick | Pre-existing, badly worsened by the scale my phases enabled |

## Fixes

1. **`.wf-toolbar-dock`** — replaced all per-panel absolute positioning with one flex-column dock
   that owns position. The toolbar and every panel are now flow children with a real `0.5rem` gap,
   so they **cannot** overlap at any toolbar height. Also folded the node-field picker into the
   dock's one-panel-open rule — it was page-owned state (`_propertyPickerOpen`, now deleted) that
   could co-open with the toolbar's own panels. The picker is passed in as a
   `RenderFragment NodeFieldsContent` so the page keeps owning its config/persistence while the
   toolbar owns layout and open-state.
2. **Added the missing `.wf-status-chip-row`** definition.
3. **`WorkflowGraphBuilder.PlacedChannelIds(graph)`** (new, 4 tests) — `PalettePanel` now builds
   the set once in `OnParametersSet` and does O(1) lookups.

## Verification

- 540/540 tests passing.
- Measured, not eyeballed: `8px` gap and `overlaps: false` for **all three** panels; toolbar back
  to a single **50px** row (was ~180px); chip row `display: flex` with all 9 chips inline.
- Every toolbar button exercised individually — animations toggle (title flips + `wf-anim-off`
  applied/removed), Fit, Node fields, Layouts, Legend, refresh rate, and the status chips
  (active-outline appears and clears).
- One-panel-open rule confirmed: opening Legend closed Layouts; opening Node fields closed both.
- **Hang fix verified at real scale**: placed 10 devices (632 channels — 8 correctly skipped as
  claimed by the "Test" layout, so Phase 6's exclusivity still holds at scale), then expanded a
  64-channel device in the palette — the exact interaction that used to hang. UI stayed
  responsive, zero console errors, no Blazor disconnect.

## Lessons worth carrying forward

- **Fourth occurrence** on this branch of "a CSS class referenced in markup that doesn't exist."
  I audited all 14 classes added across Phases 3-5 — `wf-status-chip-row` was the only missing one
  — but the pattern is now unmistakable. Add a class to the hand-written CSS *in the same edit* as
  the markup that uses it.
- **Verifying a panel "opens" is not verifying it's positioned correctly.** Phase 5 was marked
  "live-verified" because I confirmed each popover appeared; I never measured where it appeared
  relative to anything else, which is exactly how the overlap shipped. Positional/layout changes
  need measured assertions (`getBoundingClientRect` comparisons), not just existence checks.
- **A JS microbenchmark is not a proxy for a C# fix.** I initially benchmarked Set-vs-scan in the
  browser, which measured nothing about the actual server-side LINQ change. The verification that
  mattered was performing the real interaction at real scale.

## Next

Sub-project B (canvas presentation) is next, chosen because it removes the top bar and relocates
the color bar into the very toolbar/dock structure just rebuilt here — doing it now avoids
reworking that structure twice. Then C (data parity), then D (TabViewer navigation, which must
revisit Phase 6's login-redirect decision).
