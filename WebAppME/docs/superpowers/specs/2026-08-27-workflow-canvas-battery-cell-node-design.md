# Workflow Canvas — Channel Node as a Battery Cell

**Date:** 2026-08-27
**Branch:** `feat/workflow-canvas-experiment`
**Status:** Design approved, awaiting implementation plan
**Predecessors:** `2026-08-22-workflow-canvas-design.md`, `2026-08-23-workflow-canvas-dashboard-parity-design.md`, `2026-08-25-workflow-canvas-home-and-usability-design.md`

---

## 1. Goal

Redraw the canvas channel node as a **battery cell** that carries its own live data, so a
workflow board reads as a rack of cells rather than a grid of generic cards.

Concretely:

- The node looks like the login page's battery — cap, rounded body, inner well, bottom-up
  gradient fill, glow.
- The channel number (`DeviceID-SecondaryBoardNumber-ChannelNumber`, e.g. `1-1-1`) is stamped
  **on the cap**.
- Inside the well: the same arrangement as the dashboard card's `RenderNormal` — a header, three
  labelled sections, a footer with program status and timestamp.
- The fill animates as a **segment march** while a program runs: upward while charging, downward
  while discharging, **frozen with a STOP indicator when the circuit is idle**.
- The fill is translucent so every value stays readable, at any status colour the user picks.
- The parameter list is uncapped and the cell grows to fit it.

## 2. What this replaces

`Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` (59 lines) currently renders, inside the
shared `CanvasNode` chrome:

- a status pill and an SoC percentage on one row;
- `SelectedProperties` as **unlabelled** values, two per row, unit token only, full name on
  `title` hover;
- program status and last-update on one row;
- program / DBC name badges.

Two earlier decisions are deliberately reversed here:

| Earlier decision | Now |
|---|---|
| Values carry **no visible labels** — the unit token identifies them, the `title` gives the name | Configuration and Program Data get **visible labels**; Primary Data stays unlabelled (see §4.2) |
| Node is a generic card | Node is a battery cell |

The `title` attribute stays on every value regardless, so hovering still gives the full name.

## 3. Decisions taken during design

Each of these was chosen against rendered alternatives in the visual companion.

| # | Decision | Rejected alternative |
|---|---|---|
| D1 | **The battery is the node** — one stretchable cell containing all data | Login battery at full fidelity with a data panel beside it (300px wide); battery-framed card with a phone-style indicator |
| D2 | Channel number **on the cap**, dark text on the status-coloured plate | Channel number inside the well under the cap |
| D3 | Fill tinted by **circuit status** (`--status-charge` yellow, `--status-discharging` orange, idle grey, error red), keeping the login battery's gradient structure and glow | Login page's own blue/cyan `primary → accent → secondary` gradient on every cell regardless of state |
| D4 | **Segment march**, discrete steps, one segment at a time | Sliding shimmer band (the existing `wf-shimmer`) |
| D5 | March covers **only the empty region above the real SoC** while charging, and **only the filled region below it** while discharging, restarting at the surface each cycle | March running the full cell height regardless of SoC |
| D6 | **Fixed 18px segment height**, count derived from cell height | Fixed count of 10 segments |
| D7 | **Low-alpha fill** (20%; march 15%) with text sitting directly on it | Full-strength fill with each row on a semi-opaque strip |
| D8 | SoC percentage as a **small header chip** beside a phone-style battery icon | Large `%` watermark centred behind the data |
| D9 | Primary Data **two columns, value + unit, no labels**, left column left-aligned and right column right-aligned | Two columns both right-aligned; one labelled row per value |
| D10 | Content in **normal flow**, frame built from CSS/Tailwind | Absolutely-positioned text over a fixed-size SVG |

D10 is the load-bearing one. The first mockups positioned each text block at an absolute pixel
offset inside a fixed `viewBox`; the moment the field list grew, text ran outside the outline.
Building the frame as a cap element plus a bordered body, with the content as ordinary flex
children inside padding, makes overflow structurally impossible: adding fields can only make the
cell taller.

## 4. Component design

### 4.1 Structure

A new component, `Components/UI/WorkflowCanvas/Nodes/ChannelBatteryCell.razor`, renders the cell
body. `ChannelNode.razor` composes it inside `CanvasNode`.

```
CanvasNode                        (existing: transform position, selection ring, ports, stale flag)
├── HeaderContent  ← NEW slot     .wf-node__header .wf-bcap    "1-1-1"
└── ChildContent
    └── .wf-bcell                 --wf-soc, --wf-status
        └── .wf-bcell__body       3px border, radius 12, bg card
            └── .wf-bcell__well   relative, overflow hidden, radius 8
                ├── .wf-bcell__fill    absolute, z-0, height: calc(var(--wf-soc) * 100%)
                ├── .wf-bcell__surface absolute, z-0, bottom: calc(var(--wf-soc) * 100%)
                ├── .wf-bcell__march   absolute, z-0, steps() animation
                ├── .wf-bcell__segs    absolute, z-0, repeating-linear-gradient @ 18px
                └── .wf-bcell__content relative, z-10, flex column
                    ├── header   TCP dot · circuit status text · SoC chip
                    ├── section  Primary Data      (grid, 2 cols, unlabelled)
                    ├── section  Configuration     (labelled rows)
                    ├── section  Program Data      (labelled rows)
                    └── footer   timestamp · program status
```

**`CanvasNode` gains an optional `HeaderContent` RenderFragment.** When supplied it replaces the
default title row; when absent every existing node kind renders exactly as today. Device and
Battery nodes are untouched.

**The cap must keep the `wf-node__header` class.** `wwwroot/js/workflow-canvas.js:373` begins a
node drag only when `e.target.closest(".wf-node__header")` matches. If the cap replaces the
header without carrying that class, channel nodes silently become undraggable — a regression a
unit test cannot see. The stale-hardware warning glyph moves onto the cap alongside the number.

### 4.2 Sections and labels

The three sections map **exactly** onto `CardPreviewData`'s three catalogs, which is also how
`WorkflowNodeProperties.AvailableKeys` is composed:

| Section | Catalog | Count | Rendering |
|---|---|---|---|
| Primary Data | `RealTimeProperties` | 12 | `grid grid-cols-2`, value + unit only, **no label**; even index `text-left`, odd index `text-right` |
| Configuration | `ConfigProperties` | 3 | labelled rows, `flex justify-between` |
| Program Data | `ProgramProperties` | 13 | labelled rows, `flex justify-between` |

Total 28 — every key the dashboard card offers.

Rationale for the split: realtime measurements are self-identifying through their unit token
(`Ah` vs `AhCha` vs `AhDch` vs `AhStep`), and 12 of them labelled one-per-row would double the
cell's height for no gain. Configuration and Program Data values (`S-2091`, `4 / 20`,
`00:04:12`) carry no unit and are meaningless without a name, so they get labels.

- Labels come from `WorkflowNodeProperties.LabelWithoutUnit(key)`, which strips the catalog's
  parenthesised unit so the row cannot read `Charge Capacity (Ah)  2.347 AhCha`.
- Units come from `WorkflowNodeProperties.UnitFor(key)` — the hardware's own token, not the
  catalog's flattened `Ah`/`Wh`.
- Section headings use the dashboard card's wording (`Primary Data` / `Configuration` /
  `Program Data`) and its Lucide icons (`Gauge`, `Settings`, `Cpu`).
- **A section with no selected keys renders nothing at all** — no empty heading.

### 4.3 Fill, surface and march

All three are CSS layers behind the content, driven by two custom properties the telemetry bridge
already knows how to write.

| Layer | Geometry | Notes |
|---|---|---|
| `__fill` | `height: calc(var(--wf-soc) * 100%)`, anchored bottom | `linear-gradient(to top, …)` in `hsl(var(--wf-status) / …)`, `opacity: .20` |
| `__surface` | `bottom: calc(var(--wf-soc) * 100%)`, 1.5px | the bright charge line |
| `__march` | charging: `top: 0; height: calc(100% - var(--wf-soc) * 100%)`, `transform-origin: 50% 100%`<br>discharging: `bottom: 0; height: calc(var(--wf-soc) * 100%)`, `transform-origin: 50% 0%` | `animation: … 2.6s steps(10, end) infinite`, `opacity: .15` |
| `__segs` | `repeating-linear-gradient(to top, transparent 0 16px, hsl(var(--card)/.75) 16px 18px)` | fixed 18px, so the count derives itself from cell height (D6) — no arithmetic anywhere |

Percentages rather than pixels mean the fill tracks SoC correctly at **any** cell height, so
changing the selected-property set can never desynchronise the fill from the geometry.

`steps(10, end)` on a `scaleY(0) → scaleY(1)` transform is what produces "one segment at a time":
the transform jumps in ten discrete increments instead of interpolating. One animation, no
per-segment CSS, and it composites on the GPU — consistent with the existing rule in
`workflow-canvas.css` that only `transform`, `opacity` and `stroke-dashoffset` are animated.

### 4.4 State mapping

| `CircuitStatus` | Fill colour | March | Header text |
|---|---|---|---|
| `Charge` | `--status-charge` | upward, above the surface | `Charge` |
| `Discharging` | `--status-discharging` | downward, below the surface | `Discharging` |
| `Idle` | `--status-idle` | **none — frozen** | **`STOP`** + pause glyph |
| `Pause` | `--status-pause` | none | `Pause` |
| `Countinue` | `--status-continue` | upward | `Continue` |
| `Interrupt` | `--status-interrupt` | none | `Interrupt` |
| `Error` | `--status-error` | none; diagonal hatch over the well | `Error` |
| `Msg` | `--status-msg` | none | `Msg` |
| `Offline` | `--status-offline` | none | `Offline` |

- The CSS token comes from `WorkflowStatusCss.Var(status)` — **never** from
  `status.ToString().ToLower()`, because `CircuitStatus.Countinue` is misspelled in the enum
  while the variable is `--status-continue`.
- Animation is **paused, not removed** (`animation-play-state`), matching how
  `wf-battery--charging` already works, so toggling state costs nothing.
- Motion respects the existing `prefers-reduced-motion` block in `workflow-canvas.css`.
- **`Idle` displays as `STOP`.** This is a cell-only relabel; the status legend and filter bar
  keep saying `idle`. Flagged as an open question in §9.

### 4.5 Contrast guarantee

Status colours are **user-changeable** (sub-project B), so legibility cannot depend on any
particular hue. Two rules make it hue-independent:

1. Fill alpha is capped at `.20` and march at `.15`. Over the dark well, any hue only slightly
   tints the background and never approaches the text's contrast.
2. Text colour comes from the theme's own foreground tokens, never from the status hue. Only the
   cap, the border, the circuit-status word and the SoC chip take the status colour.

Verified across charge / discharging / pause / error during design.

### 4.6 Reuse and naming

`BatteryGlyph.razor` stays as-is for the **Battery** node — it is a multi-cell pack glyph with a
different job. The new cell does not consume it.

**Class names must not collide.** `BatteryGlyph` already owns `.wf-cell`, `.wf-cell__fill` and
`.wf-cell__shimmer`. The new component uses the `.wf-bcell*` prefix throughout. Reusing `.wf-cell`
would restyle every battery node as a side effect.

## 5. Geometry — the part that ripples

### 5.1 The signature has to change

Today:

```csharp
public const double ChannelNodeBaseHeight = 94;
public const double ChannelPropertyRowHeight = 20;
public static double ChannelNodeHeight(int visibleProperties) =>
    ChannelNodeBaseHeight + ((Math.Max(0, visibleProperties) + 1) / 2) * ChannelPropertyRowHeight;
```

Height depends only on the **count** of visible properties, because every row held two
unlabelled values.

That is no longer true. Height now depends on **which** properties are selected, because the
three sections have different row shapes: Primary Data packs two per row, Configuration and
Program Data take one per row, and each non-empty section adds a heading. Six Primary keys and
six Program keys produce very different cells.

So:

```csharp
public static double ChannelNodeHeight(IReadOnlyList<string> visibleProperties)
```

Callers to update (found via `find_usages` / `verify_change` before editing):

- `WorkflowAutoLayout` — `MaxChannelNodeHeight`, `RowHeight`
- `WorkflowEdgeGeometry.NodeHeightFor(NodeKind, int visibleChannelProperties = 0)` — the `int`
  parameter threads through `PortPoints` and the device-fanout helper
- `MinimapProjection`
- `WorkflowPlacement`
- `CanvasSurface.razor` (`VisiblePropertyCount="SelectedProperties.Count"`)

`RowHeight` stays derived from the worst case
(`ChannelNodeHeight(WorkflowNodeProperties.AvailableKeys)`) so raising the cap can never
reintroduce node overlap.

### 5.2 Constants must be measured, not assumed

The formula's shape is:

```
height = cap + frame + wellPadding + header + footer
       + (primaryCount   > 0 ? heading + ceil(primaryCount / 2) * primaryRow : 0)
       + (configCount    > 0 ? heading + configCount  * labelledRow : 0)
       + (programCount   > 0 ? heading + programCount * labelledRow : 0)
```

**Every constant in it must come from a live measurement in the browser before being written
into C#.** Session 36 re-measured the current card at `94 + 20/row` after the previous pair
(`106 base, 16 per row`) turned out to be correct only at exactly three rows — a wrong constant
here reappears as node overlap at some field counts and not others, which is expensive to chase.

The implementation plan must therefore include an explicit measure-then-encode step, and a unit
test pinning the formula against the measured values.

### 5.3 Width

`WorkflowAutoLayout.NodeWidth` stays **200**. The design was drawn at 210; the extra 10px is
absorbed by the cell's own padding rather than by widening the node, so `ColumnWidth`,
`ChannelStartX` and the whole horizontal layout stay put.

### 5.4 LOD tiers need rewriting

```css
.wf-world[data-lod="1"] .wf-node__body > :nth-child(n+2) { display: none; }
```

This currently works by hiding all but the first child of `.wf-node__body`. After the change the
body has exactly **one** child — the cell — so the rule silently becomes a no-op and tier 1
renders the full cell at small zoom.

New tiers:

| Tier | Zoom | Shows |
|---|---|---|
| 0 | > 0.8 | everything |
| 1 | 0.4–0.8 | cap, outline, fill + march, header row; sections and footer hidden |
| 2 | < 0.4 | cap and outline filled with the status colour only |

## 6. Telemetry bridge

`wwwroot/js/workflow-canvas.js` already writes live values into `data-role` slots without a
Blazor re-render, and already sets `--wf-status` on the channel node (line 644) and both
`--wf-status` / `--wf-soc` on the battery at the far end of the power edge (lines 472-474).

Additions:

| What | Where |
|---|---|
| `--wf-soc` on the **channel** node | alongside the existing `--wf-status` write |
| `wf-bcell--charging` / `wf-bcell--discharging` class toggle | from `e.statusName`, driving march direction |
| `wf-bcell--fault` class toggle | on `e.statusName === "error"` |
| `data-role="circuit-status"` | header status word, `STOP` when idle |
| `data-role="soc-chip"` | header SoC percentage and the icon's inner fill width |

The existing generic `prop-{key}` loop (line 656-660) needs **no change**: it writes into
`[data-role="prop-{key}"]` wherever that slot lives, so moving slots into sections is invisible
to it. `program-status` and `last-update` slots keep their names and move to the footer.

## 6.5 State of charge must be derived — it does not exist in the schema

**This is a prerequisite, not an enhancement.** `WorkflowCanvasPage.razor:357` currently passes a
hardcoded `0.0` for SoC, with a comment recording why:

> No state-of-charge percentage exists anywhere in this schema (RealTimeRecord tracks Capacity in
> Ah, not %). The battery glyph therefore renders 0% fill in live mode until a real SoC source is
> wired — a genuine gap, not a bug.

So every fill, surface line and march in this design would sit at empty forever unless SoC is
computed. It is the first thing the implementation must fix.

### 6.5.1 The formula

Net coulomb count against the battery's rated capacity (chosen by the user over an
`AccumulatedCapacity`-only variant and a voltage-interpolation variant):

```
soc = clamp((ChargeCapacity - DischargeCapacity) / NominalCapacity, 0, 1)
```

| Input | Source |
|---|---|
| `ChargeCapacity` | live, protocol `0x07`, Ah |
| `DischargeCapacity` | live, protocol `0x08`, Ah |
| `NominalCapacity` | `Models/Entities/Batteries.cs`, Ah |

Chosen because it uses charge the hardware actually measured moving, and it is
direction-correct — it rises while charging and falls while discharging, which is exactly what
the march animation depicts.

### 6.5.2 Known limits, to be stated in code and not papered over

- **It assumes the cell began the session near empty.** If it started part-charged, the reading
  is low by that offset. It is a measure of charge moved this session, not an absolute SoC.
- **It resets whenever the hardware clears its accumulators.**
- `NominalCapacity` is a `float` and may be `0` or unset for a battery record. Division must be
  guarded: **no battery, or a non-positive `NominalCapacity`, yields no SoC at all** — the cell
  renders an explicit unknown state (`--%`, empty well, no march), never a misleading `0%` that
  looks like a flat battery. This distinction matters on a test bench.

### 6.5.3 Where it lives

A new pure static class, `Services/Implementations/Workflow/StateOfChargeEstimator.cs`:

```csharp
public static double? Estimate(double chargeCapacity, double dischargeCapacity, float? nominalCapacity)
```

Returning `double?` is what makes "unknown" representable. Pure and DI-free so it is unit-testable
without a bench, and isolated so the formula can be swapped later without touching rendering —
the limits above make a future revision likely.

`ChannelTelemetry` gains `NominalCapacity`, and `WorkflowCanvasPage`'s telemetry loop calls the
estimator instead of passing `0.0`. `circuit.Battery` is **already in scope in that loop** (it
reads `circuit.Battery?.Name` for sub-project C1), so no new query or service is needed.

### 6.5.4 Battery details are now in scope

The same `circuit.Battery` reference supplies the cell's battery line — `NominalCapacity`,
`NominalVoltage`, `NumberOfCells` and the battery's type name — rendered as a compact
`LFP · 50 Ah · 3.7 V · 1 cell` strip above the footer. These are static per session, so they ride
on `ChannelTelemetry` and are written once rather than per tick.

**The strip is always reserved, even with no battery attached** (it renders empty). Making it
conditional would make cell height depend on whether a battery happens to be wired up — which the
layout cannot know, since `ChannelNodeHeight` is computed from the selected-property list alone
and runs before any telemetry exists. A conditional row would desynchronise every edge attachment
and row spacing on a board where some channels have batteries and some do not.

## 7. Files touched

| File | Change |
|---|---|
| `Services/Implementations/Workflow/StateOfChargeEstimator.cs` | **new** — the SoC formula (§6.5) |
| `Models/DTOs/Workflow/TelemetryEntry.cs` | `ChannelTelemetry` + `TelemetryEntry` gain the battery figures; `Soc` becomes nullable-aware |
| `Components/Pages/Workflows/WorkflowCanvasPage.razor` | call the estimator instead of passing `0.0`; pass the battery line |
| `Components/UI/WorkflowCanvas/Nodes/ChannelBatteryCell.razor` | **new** — the cell |
| `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` | rewritten to compose the cell + cap |
| `Components/UI/WorkflowCanvas/CanvasNode.razor` | add optional `HeaderContent` slot |
| `Components/UI/WorkflowCanvas/CanvasSurface.razor` | pass the property list, not its count |
| `Services/Implementations/Workflow/WorkflowAutoLayout.cs` | new height formula + measured constants |
| `Services/Implementations/Workflow/WorkflowEdgeGeometry.cs` | thread the property list |
| `Services/Implementations/Workflow/MinimapProjection.cs` | same |
| `Services/Implementations/Workflow/WorkflowPlacement.cs` | same |
| `Services/Implementations/Workflow/WorkflowNodeProperties.cs` | section-grouping helper |
| `wwwroot/css/workflow-canvas.css` | `.wf-bcell*` block, march keyframes, new LOD tiers |
| `wwwroot/js/workflow-canvas.js` | SoC + march-direction + status-text writes |
| `Components/App.razor` | **bump `?version=`** on the JS and CSS |
| `BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs` | rewritten |
| `BatteryTestingSystem.Tests/Services/Workflow/*` | height / geometry / minimap tests updated |

⚠️ The `?version=` query string on the JS and CSS in `App.razor` is hand-maintained. Forgetting it
ships the new markup against cached old JS/CSS and the cell renders unstyled and unanimated.

## 8. Testing

**Unit (bUnit + xUnit), added to the existing 597:**

- cap renders the channel number and carries `wf-node__header`
- Primary Data renders two columns, no labels, alternating `text-left` / `text-right`
- Configuration and Program Data render a visible label per row, from `LabelWithoutUnit`
- an empty section renders neither heading nor container
- every selected key gets a `data-role="prop-{key}"` slot in the right section
- `ChannelNodeHeight` matches the measured constants for: none, 4 default keys, all 28,
  primary-only, program-only
- `RowHeight > ChannelNodeHeight(AvailableKeys)` — the anti-overlap invariant
- `Idle` renders `STOP`; `Countinue` resolves to `--status-continue`
- march direction class follows status; absent for idle/pause/error

**Live verification (real CDP interaction, not programmatic `.click()`):**

Session 36 established that a programmatic click cannot reproduce the canvas's pointer-capture
behaviour. So, at a real bench:

1. The measure-then-encode step of §5.2, before the constants are written.
2. No node overlap at 0, 4, 12 and 28 selected properties.
3. Text legible over the fill in charge / discharging / pause / error, in each theme.
4. Segment march visibly stepping, correct direction, restarting at the surface.
5. Idle frozen, showing `STOP`.
6. **Channel nodes still drag** (the §4.1 risk).
7. Edges still attach at the cell's true vertical centre at every field count.
8. LOD tiers 1 and 2 at real zoom levels.

## 9. Resolved decisions and remaining risk

All three questions this spec opened are now settled:

1. **`Idle` reads as `STOP` inside the cell only.** The status legend and filter bar keep saying
   `idle`, so `StatusLegend.razor`, `StatusFilterBar.razor` and the persisted filter state are
   untouched.
2. **Battery details are in scope** — see §6.5.4. They also supply the SoC derivation, which
   turned out to be a prerequisite rather than an addition.
3. **The SoC `%` chip stays in the header**, beside the phone-style battery icon. The cap carries
   the channel number only.

Remaining risk is concentrated in two places, both called out where they bite:

- The SoC formula is an approximation with stated limits (§6.5.2). It is isolated behind
  `StateOfChargeEstimator.Estimate` precisely so a better formula can replace it without
  touching any rendering code.
- The height constants (§5.2) must be measured in a browser before they are written into C#.
  This is the single most likely source of a follow-up defect round.

## 10. Out of scope

- Device and Battery node appearance
- `ChannelDetailDialog` and `DbcParameterView`
- The dashboard card itself (`DeviceChannel.razor`) — this spec borrows its arrangement, it does
  not change it
- The property picker's UI
- Login page changes
