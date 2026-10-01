# Workflow Canvas Playground — Design

**Date:** 2026-08-22
**Branch:** `feat/workflow-canvas-experiment` (experimental — do not merge to `main` until the UX is judged worth keeping)
**Status:** Approved design, ready for implementation planning

---

## 1. Purpose

An experimental, n8n-style canvas where an operator drags devices, secondary boards, channels and batteries onto a pan/zoom surface, wires them together, attaches a program and an optional DBC file to each channel, and saves the arrangement as a named layout. Live telemetry animates the graph: channel nodes take their status colour, battery glyphs fill and shimmer according to state of charge, and power edges flow toward or away from the battery depending on the sign of current.

The goal of the branch is to answer one question: **does this interaction model feel good enough to replace or supplement the existing dashboard?** Everything in this design is chosen to make that question cheap to answer and cheap to abandon.

### Non-goals

Undo/redo, layout sharing between users, applying anything to hardware, the analytics page, REST endpoints, and restoring the Tailwind build pipeline are all out of scope for v1. The analytics dashboard is a separate project and will get its own spec.

---

## 2. Decisions taken

| # | Decision | Rationale |
|---|---|---|
| D1 | Scope is canvas + persistence + navigation change. Analytics is a separate later spec. | The analytics page shares no code with the canvas, only the theme. Bundling them would delay the experiment. |
| D2 | The canvas records **intent only**. Saving never transfers a program or touches hardware. | Running a test continues to go through the existing dashboard transfer/start flow. Zero risk to working hardware code. |
| D3 | Styling ships as one standalone hand-written `wwwroot/css/workflow-canvas.css`. | `wwwroot/css/app.min.css` has no rebuild pipeline (ADR-2), so new Tailwind utilities are unavailable. Theme CSS variables are raw HSL triplets, so plain CSS can consume them and keep light/dark theming. |
| D4 | Nodes are Blazor components, edges are SVG, pan/zoom/drag is custom JS. | Blazor Server cannot round-trip pointer moves. Follows the existing `windowDragManager.js` precedent. |
| D5 | Device, Board, Channel and Battery are nodes. Program and DBC are properties on the channel node. | The battery needs to be a node to carry the animated SVG and a glowing power edge. Program and DBC as nodes would produce hundreds of crossing edges at 64 channels. |
| D6 | Live data reuses `ChannelManager` + `DashboardRenderBatcher`, filtered to on-canvas channels. | No second polling path; cost scales with what the user placed. |
| D7 | Persistence is a single table with a JSON document column. | One revertible migration on a branch that may be deleted; the node schema can change freely during the experiment. |
| D8 | v1 includes auto-import, snap-to-grid, minimap, and multiple named layouts. Undo/redo deferred. | Auto-import makes the experiment reachable; grid and minimap materially affect whether it "feels good", which is the thing under evaluation. |

---

## 3. Architecture

All decision-making logic lives in plain C# classes with no Razor and no DB access, so it can be unit-tested. This follows the precedent set by `Services/Implementations/CircuitSelectionLogic.cs`, which was extracted from the dashboard for the same reason (T-38).

### 3.1 New files

```
Components/Pages/Workflows/
  WorkflowCanvasPage.razor          page shell: toolbar, layout switcher, three-pane split

Components/UI/WorkflowCanvas/
  CanvasSurface.razor               pan/zoom viewport; owns the transform, hosts children
  EdgeLayer.razor                   one SVG overlay drawing every edge
  CanvasNode.razor                  shared node chrome: selection ring, ports, drag handle
  Nodes/DeviceNode.razor
  Nodes/BoardNode.razor
  Nodes/ChannelNode.razor           status pill, V/I/SoC readout, program + DBC badges
  Nodes/BatteryNode.razor
  BatteryGlyph.razor                animated pack/cell SVG driven by CSS variables
  PalettePanel.razor                right rail: devices, batteries, programs, DBC files
  PropertiesDock.razor              left dock: properties of the selected node
  Minimap.razor

Models/Entities/WorkflowLayout.cs
Models/DTOs/Workflow/WorkflowGraph.cs

Services/Interfaces/IWorkflowLayoutService.cs
Services/Implementations/Workflow/
  WorkflowGraphBuilder.cs           pure: DB topology -> WorkflowGraph (auto-import)
  WorkflowAutoLayout.cs             pure: assigns x/y in a device -> board -> channel -> battery flow
  WorkflowGraphValidator.cs         pure: legal connections, duplicates, orphan rules
  WorkflowLayoutService.cs          orchestration + persistence + stale resolution
  WorkflowTelemetryBridge.cs        ChannelManager subscription, filtered and coalesced

Repositories/Implementations/WorkflowLayoutRepository.cs   (follows Repository.cs)

wwwroot/js/workflow-canvas.js
wwwroot/css/workflow-canvas.css

Migrations/<timestamp>_AddWorkflowLayout.cs
```

### 3.2 Files modified

Five shared files are touched, a handful of lines each, which keeps the branch cheap to rebase:

- `Components/Layout/MainLayout.razor` — add the `Workflows` menu entry, gate the `Display` entry on the `Features:LegacyDashboard` flag.
- `Components/Layout/Navbar.razor` — logo click targets `WorkflowCanvasPage` instead of `DashboardView` while the legacy dashboard is hidden.
- `Extensions/ServiceCollectionExtensions.cs` (~line 101) — DI registration for the new service, repository and topology provider. **Not `Program.cs`** — that file does not hold the scoped registrations; `Program.cs` only binds the options.
- `Components/App.razor` — the new stylesheet `<link>` and script `<script>`; every asset is hardcoded there.
- `appsettings.Development.json` — the `Features` section.

`DashboardView.razor` and every file under `Components/UI/Dashboard/` are untouched: not moved, not renamed, not deleted.

### 3.3 The JS / C# contract

JS owns transient interaction; C# owns committed state. This is the load-bearing performance decision of the whole design.

| Event | Handled by | Crosses to C# |
|---|---|---|
| Pan, zoom, node drag in progress | JS only, CSS transforms | never |
| Node drop (final position) | JS | once — `OnNodeMoved(nodeId, x, y)` |
| Connection drag in progress | JS draws a ghost path | never |
| Connection dropped on a port | JS | once — `OnConnect(fromId, toId)` |
| Selection click | JS | once — `OnSelect(nodeId)` |
| Live telemetry to glow / fill | C# | one batched interop call per tick, writing CSS variables |

Three round-trips per gesture rather than hundreds.

---

## 4. Data model

### 4.1 Entity

```csharp
public class WorkflowLayout : BaseEntity
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public string OwnerUserId { get; set; }   // per-user; no sharing in v1
    public string LayoutJson { get; set; }    // serialized WorkflowGraph
    public int SchemaVersion { get; set; }
}
```

### 4.2 Document shape

```csharp
record WorkflowGraph(int Version, List<WorkflowNode> Nodes, List<WorkflowEdge> Edges, CanvasViewport Viewport);
record WorkflowNode(string Id, NodeKind Kind, long? EntityId, double X, double Y, NodeAttachment? Attach);
record WorkflowEdge(string Id, string FromNodeId, string ToNodeId, EdgeKind Kind);
record NodeAttachment(int? ProgramId, int? DbcFileId, int? BatteryTypeId);
record CanvasViewport(double PanX, double PanY, double Zoom);

enum NodeKind { Device, Board, Channel, Battery }
enum EdgeKind { Topology, Power }
```

### 4.3 Two deliberate choices

**`EntityId` is `long?` because `Channel.Id` and `SecondaryBoard.Id` are `long`; `Device.DeviceID` is `int` and widens. It is a soft reference, never a foreign key.** A saved layout points at a channel by id, but nothing in the schema enforces it, so deleting a device can neither fail nor cascade into layouts. On load, `WorkflowLayoutService` resolves every `EntityId` against the live database and marks unresolved nodes as *stale*: rendered dimmed with a warning badge, excluded from the telemetry subscription, and never silently dropped. A layout referencing deleted hardware is guaranteed to happen eventually; the failure mode must be a grey node, not a broken page.

**`SchemaVersion` guards against mid-experiment shape changes.** The node record *will* change while the design is being iterated on. On mismatch the layout refuses to load with "saved by an older version" rather than deserializing into garbage.

### 4.4 Access path

The page talks to `IWorkflowLayoutService` through Blazor DI. No REST controllers: nothing external consumes this, and controllers on a throwaway branch are surface area that would have to be deleted later.

---

## 5. Interaction and visual language

### 5.1 Shell

```
+--------------------------------------------------------------+
| toolbar: [layout v] [Save] [Auto-import v] [grid] [anim] [fit]|
+------------+----------------------------------+--------------+
| PROPERTIES |            CANVAS                |   PALETTE    |
|   DOCK     |   (pan / zoom / snap grid)       |  Devices     |
| (selected  |                                  |  Batteries   |
|  node)     |                     +----------+ |  Programs    |
|            |                     | minimap  | |  DBC files   |
+------------+---------------------+----------+-+--------------+
```

Both side panels collapse to icon rails so the canvas can run near full width.

### 5.2 Placement

The palette lists real database entities. Dragging a device onto the canvas drops a Device node plus its Board nodes; each Board node then offers a channel picker, so the operator takes only the channels that need to work. Auto-import is the bulk alternative that materialises a whole device at once via `WorkflowGraphBuilder` + `WorkflowAutoLayout`.

### 5.3 Node hierarchy

| Node | Size | Content |
|---|---|---|
| Device | large | name, IP/port, online dot, board count |
| Board | medium | index, channel count, collapse toggle |
| Channel | medium | channel number, status pill, V/I/SoC readout, program and DBC badges |
| Battery | small | animated `BatteryGlyph`, SoC percentage, pack/cell label |

### 5.4 Edges

`Topology` edges (device to board to channel) are thin, neutral and static — they express structure, not action. `Power` edges (battery to channel) carry the animation: a bezier whose dash pattern flows toward the battery when charging and away when discharging, coloured from `--status-charge` / `--status-discharging`, and completely still when idle. Motion and direction carry real meaning rather than decoration.

### 5.5 Animation technique

Technique is part of the design, because the naive implementation of each of these is a per-frame repaint:

- **Flow** is an animated `stroke-dashoffset` on the SVG path. It runs on the compositor, so sixty flowing edges cost roughly what one does. Animating path geometry would repaint each frame.
- **Glow** is a pseudo-element carrying a *static* `box-shadow` whose `opacity` alone is animated. Animating `box-shadow` directly forces a repaint per frame.
- **Battery fill** is `transform: scaleY(var(--soc))`, not an animated `height`.
- All animation sits behind `@media (prefers-reduced-motion: reduce)` and a toolbar toggle. The toggle doubles as a diagnostic when judging whether the canvas feels smooth.

`BatteryGlyph` renders either a pack (a series of cells) or a single cell, with fill height from SoC, fill colour from status, a charging shimmer band held at `animation-play-state: paused` unless current is positive, and a fault cross-hatch on error. Its entire API is CSS custom properties (`--soc`, `--status-hue`), so C# updates variables and never re-renders it.

Every colour derives from an existing theme variable. No literal hex values.

### 5.6 Validation

`WorkflowGraphValidator` is pure and unit-tested. Rules:

- a channel appears at most once per layout;
- a channel takes at most one battery;
- topology edges must match real hardware parentage, so a board belonging to device A cannot wire to a channel belonging to device B;
- program and DBC attach only to channel nodes;
- no cycles.

Invalid drops are refused with a toast, never silently discarded.

---

## 6. Live data flow

The constraint that shapes this section: **telemetry must never trigger a Blazor re-render.**

```
ChannelManager (existing)
  -> WorkflowTelemetryBridge (new, scoped to the canvas)
       - filters to a HashSet of on-canvas channel ids
       - coalesces via the existing CoalescingRunner / DashboardRenderBatcher (~5 Hz)
       - one interop call per tick:
           workflowCanvas.applyTelemetry([{ nodeId, status, soc, currentSign }, ...])
             -> JS sets CSS custom properties on the node and edge elements
                  -> CSS performs the colouring, glowing, flowing and filling
```

Blazor re-renders only on structural change: a node added, removed, selected, or a property edited. A channel moving from Charge to Pause is a CSS variable write, not a render. At 64 channels and 5 Hz this is the difference between a smooth canvas and an unusable one on Blazor Server.

The bridge subscribes when the canvas opens and disposes when the tab closes. The on-canvas id set is recomputed whenever the graph changes. Stale nodes never enter the set.

---

## 7. Navigation

```jsonc
// appsettings.Development.json
"Features": {
  "WorkflowCanvas": true,
  "LegacyDashboard": false
}
```

`Features:WorkflowCanvas` gates whether the Workflows menu entry is registered at all, so the entire experiment can be switched off without reverting code. `Features:LegacyDashboard` gates the existing `Display` entry. Both are read once in `MainLayout` when the menu list is built.

New menu entry in `MainLayout.razor`:

```csharp
new NavMenuItem {
    Title = "Workflows", Url = "/workflows",
    Icon = Lucide.Workflow,
    ComponentType = typeof(WorkflowCanvasPage),
    Unique = true, keepAlive = true
}
```

`keepAlive` prevents the graph being rebuilt on every tab switch.

The existing `Display` entry is gated on `Features:LegacyDashboard` rather than removed, so the old dashboard can be restored instantly by flipping a flag. `@page "/Dashboard"` keeps working either way — the component is hidden from the menu, not from the router. While the legacy dashboard is hidden, the logo click in `Navbar.razor` targets the Workflows canvas.

---

## 8. Testing

Unit tests go in the existing `BatteryTestingSystem.Tests` project (29 currently green), all against the pure classes. No UI automation.

| Class | Behaviour pinned |
|---|---|
| `WorkflowGraphBuilder` | auto-import produces the correct Device/Board/Channel nodes and edges for a device; a device with zero boards is handled |
| `WorkflowAutoLayout` | no overlapping nodes; output is deterministic for identical input |
| `WorkflowGraphValidator` | rejects duplicate channel, second battery on a channel, cross-device topology edge, cycle, and program attached to a non-channel node |
| `WorkflowLayoutService` | JSON round-trip is lossless; a deleted channel id resolves to a stale node rather than throwing; a mismatched `SchemaVersion` is refused cleanly |

The `WorkflowLayoutService` stale-node case is the highest-value test: it is the failure mode most likely to occur in real use.

Manual verification covers what tests cannot judge — whether a 64-channel graph is readable, whether the animation is smooth, and whether the interaction model feels good.

---

## 9. Risks

| Risk | Mitigation |
|---|---|
| Blazor Server latency makes the canvas sluggish | All transient interaction is JS-side and telemetry bypasses rendering. If it remains sluggish, that is itself a valid answer to the experiment. |
| A 64-channel graph is unreadable | Board-level collapse, minimap, animation toggle, auto-layout. It may still fail, which is worth learning early. |
| Hand-written CSS drifts from the theme | Every colour comes from an existing `--status-*` or theme variable. |
| The branch rots against `main` | Almost every file is new; only five shared files are touched, by a handful of lines each. |
| A saved layout references deleted hardware | Soft references plus stale-node rendering, pinned by a unit test. |

---

## 10. Exit criteria

The experiment succeeds if an operator can auto-import a 64-channel device, prune it to the channels they care about, attach batteries and programs, save and reload the layout, and watch live telemetry animate it smoothly — and prefers doing that to the existing dashboard. If any of those fails, the branch is deleted and the four modified lines revert cleanly.
