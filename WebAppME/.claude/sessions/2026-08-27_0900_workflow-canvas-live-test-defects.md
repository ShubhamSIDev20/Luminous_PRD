# Session 36 — T-47: eleven live-test defects on the workflow canvas

> Started: 2026-08-26T18:00:00Z (ran past midnight into 2026-08-27)
> Branch: `feat/workflow-canvas-experiment` (never merges to `main` — D14/D15)
> Agent: Claude (Sonnet 5)
> Status: ✅ All reported defects fixed and live-verified. 597 tests (from 589).

## Goal

The user hand-tested the canvas at a small bench (1 device / 1 board / 8 channels) and reported
defects in several batches. Everything here came from that testing — no planned work.

## Outcome

| Commit | Covers |
|---|---|
| `b5cbfe2` | toolbar clicks dead, animation toggle, palette device list, dock button, edge placement + drag, missing Online filter |
| `dce999a` | refresh rate never drove updates, field cap, wrong measured constants, duplicated units, LOD-wrong edges |
| `17c256e` | canvas could not be panned by dragging |
| `4ba9672` | device→channel flow animation, battery palette read the wrong table |

Also produced a bring-up/verification runbook as an artifact for the user to run the bench
themselves.

## The defects, and what each actually was

### 1. Toolbar / minimap / action-bar buttons dead to real clicks

`CanvasToolbar`, `Minimap` and `SelectionActionBar` are DOM children of `.wf-canvas-host`, so their
`pointerdown` bubbled into the canvas handler, fell through to `beginMarquee`, and
`setPointerCapture` on the host **stole the pointerup** — so the browser never synthesised a
`click`. It also fired `OnSelect(null)` on release, silently clearing the selection.

⚠️ **This is the important lesson of the session.** It survived every scripted verification because
a programmatic `el.click()` dispatches a click directly and skips the pointer sequence entirely.
**Scripted clicking cannot test this class of bug** — only a real input event (chrome-devtools'
CDP-level `click`) reproduces it. Any future "does this button work" check on this branch must use
the CDP click, not `.click()`.

Fixed with a `CHROME` guard in `onPointerDown` and `onWheel` (a scroll inside a popover was zooming
the canvas).

### 2. The hand tool — canvas could not be dragged

Not broken, **unreachable**. Phase 3 (D12) made a plain left-drag a marquee and moved panning to
middle-button or Space+drag, neither discoverable, so the canvas read as immovable and the wheel
looked like the only working gesture. Left-drag now pans; marquee moved behind an explicit toolbar
toggle; middle-button and Space+drag pan in *either* mode. The cursor now reports which gesture is
armed (grab / grabbing / crosshair), which was previously unguessable.

### 3. Refresh rate never drove anything

**`RefreshRateMs` was only a throttle CEILING on event-driven pushes** — nothing ever pushed on a
clock. `HardwareManagerChanged` fires on registration/accept/link-drop/command-send and
`OnChannelChanged` only when a circuit decides something changed, so an idle bench produced no
events and the canvas froze until a page reload. Setting the rate could not cause an update, which
is exactly why the control looked broken.

A `System.Threading.Timer` now pushes every `RefreshRateMs`, retimed when the user changes it.
Measured: 4 changes in 12s at 3000ms (exactly 3000ms); live movement on an idle bench with no
program running.

### 4. Node property cap

6 → 12 → removed entirely. `MaxVisible` is now `AvailableKeys.Count` (28), tied to the catalogue so
a new `CardPreviewData` field can never be permanently unselectable. Only safe because card height,
`RowHeight` and the edge attachment point all derive from it. `MaxChannelNodeHeight`/`RowHeight` had
to become `static readonly` (no longer compile-time constants).

### 5. Measured constants were wrong — and had looked right

Re-measured the card across 2/4/6/8/10/12 properties: **114/134/154/174/194/214**, i.e. `94 + 20 per
row`. The previous `106 + 16` reproduces 154 at three rows **and only at three rows** — which is why
the single six-property measurement that introduced them in sub-project B validated cleanly while
being wrong at every other count. Worth remembering: one measurement cannot pin a two-parameter
linear model.

### 6. Units on the node face

The four capacities and four energies are **not** all `Ah` and `Wh`. Per
`OperatorConstants.ValidUnits` and `MeasurementData` (0x26–0x2D) they are
`Ah`/`AhCha`/`AhDch`/`AhStep` and `Wh`/`WhCha`/`WhDch`/`WhStep`. The node face renders values with
**no visible labels**, so the unit token is the only thing distinguishing eight otherwise identical
rows. New `WorkflowNodeProperties.UnitFor`; `LabelWithoutUnit` stops the catalogue's looser "(Ah)"
appearing beside it in the detail dialog; each value carries a hover title.

⚠️ I first "fixed" this by *removing* the units as duplication — wrong reading of the report. The
user supplied the actual vocabulary and corrected it.

### 7. Edges misplaced, and not following a drag

Three separate causes, found in order:
- `WorkflowEdgeGeometry` hardcoded channel height **108** while the real card is 154+ — edges
  attached 23px high, and the error *moved* with the property picker. Height now comes from
  `WorkflowAutoLayout.ChannelNodeHeight` (one source of truth).
- `redrawEdgesFor` matched only `data-from`/`data-to`, which a Board→Channel edge never matches
  because its `from` is the **unrendered** board — so dragging a device stranded every channel edge.
  Pin edges now carry `data-pin-owner` + `data-pin-frac`.
- **Card height is partly a CSS concern.** The zoom-driven level-of-detail tiers collapse cards, which
  no server-side formula can know, so edges hung **152px** below collapsed cards. JS now re-derives
  every path from measured DOM on a tier change and after a render that resizes cards, and the pin is
  a *fraction* of card height rather than an absolute offset.

Measured result: 0px error at full detail **and** 0px at the reduced tier.

### 8. Palette listed 11 devices for a 1-device bench

Not a palette bug at first: the DB genuinely still had all 11 `Devices` rows at `IsDeleted=0` —
Circuits-page bulk delete marks **channels** deleted and leaves devices alone. Fixed properly with
`TopologySnapshot.WithPlaceableDevicesOnly()` (drops devices with no channels), applied to the
**palette only** — the labeller and validators resolve existing nodes and would report placed
hardware as stale.

### 9. Dock/palette collapse button dead when nothing selected

`.wf-collapse` was `float: right`, which leaves normal flow; the following block overlapped its box
and, being later in DOM order, painted on top and swallowed the click. That block is the
*"Select a node…"* placeholder — which is why it failed **specifically** with an empty selection,
exactly as the user described. Both panels are flex columns now.

### 10. Missing status filter labels

`offline` did render; the genuinely missing one was **`online`**, which is not a `CircuitStatus`
member at all (the dashboard defines it as the complement of Offline), so the enum loop could never
produce it. Added with a hollow swatch and a JS special case; both dim paths share one matcher.

### 11. Flow animation on the connection line

Three blockers stacked, any one of which hides it:
- `applyToPowerNeighbours` selected `.wf-edge--power` **exclusively**, so the device→channel line —
  the only edge most layouts have — was never considered.
- `stroke-dasharray` lived only on `.wf-edge--power`; animating `stroke-dashoffset` on a solid line
  is invisible. The dash pattern moved onto `.wf-edge--flowing`.
- `FlowFor` also required `|current| >= 0.001`, which killed it on a channel reporting
  Running + Charge at **0.00 A** — a state this bench produces. Direction comes from `CircuitStatus`,
  never the sign of the current, so the parameter is gone. D20 still holds: animation ⇔ running.

Verified: all 8 `brd-1->chn-*` edges flowing, computed `animationName: wf-flow`, 10/8 dash.

## Gotchas worth carrying forward

- **`?version=` on `workflow-canvas.js`/`.css` in `App.razor` is hand-maintained.** A JS/CSS edit
  ships to nobody until it is bumped — my pointer guard tested as *not applied* on a correct build
  because the browser served the cached `version=0.4`. Now at js `0.8` / css `0.5`. Same family as
  ADR-2's no-CSS-build-pipeline problem. **Bump it in the same edit as any asset change.**
- **Grep build output for `error`, not `error CS`.** A running app locks the exe and fails the build
  with `MSB3027`/`MSB3021`; an `error CS` filter reports that as a clean build. Bit me twice.
- **Measure after the render settles.** Card heights read 70px mid-render and 154/374px once the
  telemetry paint lands; two "bugs" were transient reads.
- **Compare against `/Dashboard`** (still routable with the nav entry hidden) before blaming the
  canvas — it settled one false diagnosis this session.

## Corrections I had to make mid-session

- Diagnosed a "canvas not refreshing" bug, then **retracted it** on seeing the Dashboard equally
  frozen — then the user reported their Dashboard *does* update, and the retraction was itself wrong:
  the real bug was the missing refresh clock (#3). Both the claim and the retraction were premature.
- "Fixed" the unit duplication by deleting units, when the ask was distinct unit tokens (#6).
- Added a battery node to the user's canvas trying to reproduce the animation on a power wire; they
  stopped me — the line they meant was device→channel. Discarded by reloading the layout, never saved.

## Files changed

**Created:** `BatteryTestingSystem.Tests/Services/Workflow/TopologySnapshotTests.cs`

**Modified (main):** `wwwroot/js/workflow-canvas.js`, `wwwroot/css/workflow-canvas.css`,
`Components/App.razor` (asset versions), `Components/Pages/Workflows/WorkflowCanvasPage.razor`,
`Components/UI/WorkflowCanvas/{CanvasToolbar,CanvasSurface,EdgeLayer,StatusFilterBar,PalettePanel,ChannelDetailDialog}.razor`,
`Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`,
`Services/Implementations/Workflow/{WorkflowAutoLayout,WorkflowEdgeGeometry,WorkflowNodeProperties,WorkflowTelemetryBridge,WorkflowTopologyProvider}.cs`,
`Models/DTOs/Workflow/TopologySnapshot.cs`

## Bench state / outstanding

- Bench is 1 device, 1 board, channels `1-1-1`…`1-1-8`, all Online + Allow.
- **Transfer must happen before Start** — the user corrected me on this; starting without a
  transferred program is what produced the earlier "channels report Stop immediately" mystery. It was
  never a bug.
- 10 stale `Devices` rows (plus junk `X⌂DEVICE_001_3`, DeviceID 19) are still in the DB at
  `IsDeleted=0`. Hidden from the palette now, but not deleted — a raw SQL delete was blocked by the
  permission classifier and the user has not chosen a route. DB backed up to
  `D:/MEWebApp/BtsAppdb.backup-20260826.db`.
- Per-user status-colour overrides saved **2026-08-24** are in effect (charge = magenta
  `300 100% 50%`, idle pink, offline grey-blue). Deliberate, left alone — the flowing wires render
  magenta because of it, which is the single-`HslFor`-path design working.
- `DbcParameterView` still verified live only in its empty state.
