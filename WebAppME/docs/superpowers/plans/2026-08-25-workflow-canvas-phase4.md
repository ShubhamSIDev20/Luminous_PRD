# Workflow Canvas Phase 4 — Board Consolidation & Animation Correctness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Shrink the Board node from a full card into a small online/offline "port" chip attached to its device's lane (D18/D19), and make the flow/pulse animation require the program to actually be running (D20).

**Architecture:** Both changes are additive and low-risk by design. The Board node's **position** and its **presence in the persisted graph** do not change at all — `WorkflowAutoLayout` already places every Board node in its own fixed column immediately to the right of its Device (`ColumnWidth = 280px`), so shrinking `BoardNode.razor`'s markup into a compact chip achieves the "extra node removed from the canvas" outcome the user asked for without touching layout math, edge geometry, `NodeKind`, or any saved-layout JSON. The animation fix is a pure logic change to `WorkflowTelemetryBridge.FlowFor`.

**Tech Stack:** C# / .NET 8, Blazor Server, xUnit, vanilla JS/CSS (no new dependencies).

**Spec:** `docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md` (Sections 1-2, decisions D18-D20)

## Global Constraints

- Never modify `DashboardView.razor` or `TransferDialog.razor` (standing constraint since Phase 1).
- `NodeKind.Board` stays in the enum and in every saved layout's JSON — this phase is a rendering-layer change only, no migration.
- No layout-algorithm change: `WorkflowAutoLayout`'s existing Board column positioning is untouched.
- No edge-geometry change: `EdgeLayer`/`WorkflowEdgeGeometry` are untouched — a Board node stays a normal top-level `.wf-node`, just visually smaller.
- Follow strict TDD: write failing test → verify red → implement → verify green → full suite → (for browser-visible work) live browser verification → commit, one commit per task.
- Update the `.claude/` memory files before ending the session, per this repo's mandatory workflow.

---

### Task 1: Flow animation requires `ProgramStatus.Running` (D20)

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: nothing new — `ChannelTelemetry.ProgramStatus` (existing field, `ProgramRunningStatus`).
- Produces: `WorkflowTelemetryBridge.FlowFor(CircuitStatus status, double current, ProgramRunningStatus programStatus)` — signature change from the existing 2-arg `FlowFor`. Every caller (`BuildEntries`) and every existing test call site must be updated in this task.

- [ ] **Step 1: Write the failing tests**

In `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`, replace the existing `FlowFor_OnlyChargeAndDischargeProduceMotion` theory and `FlowFor_ChargeWithNoCurrentIsStill` test with:

```csharp
    [Theory]
    [InlineData(CircuitStatus.Charge, 12.5, ProgramRunningStatus.Running, 1)]
    [InlineData(CircuitStatus.Discharging, 12.5, ProgramRunningStatus.Running, -1)]
    [InlineData(CircuitStatus.Discharging, -12.5, ProgramRunningStatus.Running, -1)]
    [InlineData(CircuitStatus.Idle, 0, ProgramRunningStatus.Running, 0)]
    [InlineData(CircuitStatus.Pause, 5, ProgramRunningStatus.Running, 0)]
    [InlineData(CircuitStatus.Offline, 0, ProgramRunningStatus.Running, 0)]
    [InlineData(CircuitStatus.Error, 3, ProgramRunningStatus.Running, 0)]
    public void FlowFor_OnlyChargeAndDischargeProduceMotionWhileRunning(
        CircuitStatus status, double current, ProgramRunningStatus programStatus, int expected)
    {
        // A paused or errored channel must be completely still. Motion means "current is
        // flowing right now, as part of an actually-running program" — if it means anything
        // less, the animation stops carrying meaning.
        Assert.Equal(expected, WorkflowTelemetryBridge.FlowFor(status, current, programStatus));
    }

    [Fact]
    public void FlowFor_ChargeWithNoCurrentIsStill()
    {
        // Status says charge but the current has dropped to zero — a finished CV tail.
        Assert.Equal(0, WorkflowTelemetryBridge.FlowFor(
            CircuitStatus.Charge, 0, ProgramRunningStatus.Running));
    }

    [Fact]
    public void FlowFor_ReturnsZeroWhenProgramIsNotRunningEvenWithChargeStatusAndCurrent()
    {
        // The bug this fixes: a Charge/Discharging reading with real current used to animate
        // regardless of whether a program was actually running. D20 requires both.
        Assert.Equal(0, WorkflowTelemetryBridge.FlowFor(
            CircuitStatus.Charge, 12.5, ProgramRunningStatus.Stop));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: compile error — `FlowFor` does not have an overload taking 3 arguments.

- [ ] **Step 3: Update `FlowFor` and its call site**

In `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, replace:

```csharp
    public static int FlowFor(CircuitStatus status, double current)
    {
        if (Math.Abs(current) < 0.001) return 0;

        return status switch
        {
            CircuitStatus.Charge => 1,
            CircuitStatus.Discharging => -1,
            _ => 0,
        };
    }
```

with:

```csharp
    public static int FlowFor(CircuitStatus status, double current, ProgramRunningStatus programStatus)
    {
        if (programStatus != ProgramRunningStatus.Running) return 0;
        if (Math.Abs(current) < 0.001) return 0;

        return status switch
        {
            CircuitStatus.Charge => 1,
            CircuitStatus.Discharging => -1,
            _ => 0,
        };
    }
```

Update the doc comment above it (currently "1 charging, -1 discharging, 0 still. Only Charge and
Discharging move, and only when current is actually flowing...") to add: "...and only while the
channel's program is actually running — a nonzero current on a stopped program must not animate."

In the same file's `BuildEntries`, change:

```csharp
                Flow: FlowFor(reading.Status, reading.Current),
```

to:

```csharp
                Flow: FlowFor(reading.Status, reading.Current, reading.ProgramStatus),
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: PASS.

- [ ] **Step 5: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS, 505 or more (2 new tests added, 1 replaced test renamed).

- [ ] **Step 6: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowTelemetryBridge.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "fix(workflow-canvas): flow animation requires ProgramStatus.Running (D20)

FlowFor previously animated any Charge/Discharging reading with
nonzero current, regardless of whether a program was actually
running. A channel sitting at a stale nonzero current reading on a
stopped program would pulse and flow as if it were live. FlowFor now
takes the reading's ProgramRunningStatus and returns 0 unless it is
Running."
```

---

### Task 2: Per-board online/offline determination (D19 data)

**Files:**
- Modify: `Models/DTOs/Workflow/TelemetryEntry.cs` (the `NodeRollup` record)
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs` (`BuildRollups`)
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `ChannelTelemetry.IsConnected` (existing field, added in Phase 3).
- Produces: `NodeRollup(string NodeId, string OnlineText, bool IsOnline)` — `IsOnline` is a new third field. For `NodeKind.Device` rollups, `OnlineText` keeps its existing `"N / M online"` format and `IsOnline` is always `true` (unused by any Device-facing UI — Device's own online treatment does not change in this phase). For `NodeKind.Board` rollups, `OnlineText` becomes `"Online"` or `"Offline"`, and `IsOnline` is `true` when **any** placed, non-offline-telemetry channel on that board reports `IsConnected == true`.

- [ ] **Step 1: Write the failing tests**

Add to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`, in the `BuildRollups` section:

```csharp
    [Fact]
    public void BuildRollups_BoardIsOnlineWhenAnyChannelIsConnected()
    {
        var graph = GraphWith(
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100), Channel(101), Channel(102));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
            [101] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: true),
            [102] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
        };

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(graph, data, Children));

        Assert.Equal("Online", rollup.OnlineText);
        Assert.True(rollup.IsOnline);
    }

    [Fact]
    public void BuildRollups_BoardIsOfflineWhenNoChannelIsConnected()
    {
        var graph = GraphWith(
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100), Channel(101), Channel(102));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
            [101] = new(CircuitStatus.Offline, 0, 0, 0, IsConnected: false),
            [102] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
        };

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(graph, data, Children));

        Assert.Equal("Offline", rollup.OnlineText);
        Assert.False(rollup.IsOnline);
    }

    [Fact]
    public void BuildRollups_BoardWithNoTelemetryAtAllIsOffline()
    {
        var graph = GraphWith(
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100));

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children));

        Assert.Equal("Offline", rollup.OnlineText);
        Assert.False(rollup.IsOnline);
    }

    [Fact]
    public void BuildRollups_DeviceOnlineTextFormatIsUnchangedAndIsOnlineIsAlwaysTrue()
    {
        // D19 only changes how a BOARD is presented; a Device rollup keeps its existing
        // "N / M online" wording exactly as it was before this phase.
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            Channel(100), Channel(101));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
            [101] = new(CircuitStatus.Offline, 0, 0, 0),
        };

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(graph, data, Children));

        Assert.Equal("1 / 2 online", rollup.OnlineText);
        Assert.True(rollup.IsOnline);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: compile error — `NodeRollup` does not have an `IsOnline` member.

- [ ] **Step 3: Add the field and implement the branch**

In `Models/DTOs/Workflow/TelemetryEntry.cs`, replace:

```csharp
public record NodeRollup(string NodeId, string OnlineText);
```

with:

```csharp
public record NodeRollup(string NodeId, string OnlineText, bool IsOnline);
```

In `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, replace the body of the
`foreach (var node in graph.Nodes)` loop inside `BuildRollups`:

```csharp
            var children = childChannelIds(node.Kind, entityId).Where(placed.Contains).ToList();
            var online = children.Count(id =>
                telemetry.TryGetValue(id, out var reading) && reading.Status != CircuitStatus.Offline);

            rollups.Add(new NodeRollup(node.Id, $"{online} / {children.Count} online"));
```

with:

```csharp
            var children = childChannelIds(node.Kind, entityId).Where(placed.Contains).ToList();

            if (node.Kind == NodeKind.Board)
            {
                var anyConnected = children.Any(id =>
                    telemetry.TryGetValue(id, out var reading) && reading.IsConnected);
                rollups.Add(new NodeRollup(node.Id, anyConnected ? "Online" : "Offline", anyConnected));
                continue;
            }

            var online = children.Count(id =>
                telemetry.TryGetValue(id, out var reading) && reading.Status != CircuitStatus.Offline);

            rollups.Add(new NodeRollup(node.Id, $"{online} / {children.Count} online", true));
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: PASS.

- [ ] **Step 5: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Models/DTOs/Workflow/TelemetryEntry.cs Services/Implementations/Workflow/WorkflowTelemetryBridge.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): per-board online/offline determination (D19)

NodeRollup gains an IsOnline bool. A Board rollup's OnlineText becomes
a plain Online/Offline word (driven by whether any of its placed
channels reports IsConnected), replacing the old N/M-channels-online
count that Task 3 is about to remove the display space for. Device
rollups are completely unchanged."
```

---

### Task 3: Compact board-slot chip (D18)

**Files:**
- Modify: `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`
- Modify: `Components/UI/WorkflowCanvas/CanvasSurface.razor`
- Modify: `wwwroot/css/workflow-canvas.css`
- Modify: `wwwroot/js/workflow-canvas.js`

**Interfaces:**
- Consumes: `NodeRollup.IsOnline` (Task 2) via the existing `rollups` array already pushed to JS each telemetry tick (`applyTelemetry(host, entries, rollups)` — no new JS entry point, no new C#→JS call).
- Produces: no new public API — this is a pure rendering/styling change. `BoardNode`'s existing `OnToggleCollapse`/`OnSelectChildren` parameters (from Phases 2 and 3) keep their exact signatures.

**Implementation note:** the Board node stays exactly where `WorkflowAutoLayout` already puts it
(its own column, 280px right of the Device) — this task does not touch layout math or edge
geometry at all. "Removing the extra node from the canvas" is achieved by shrinking what that
node *looks like*, not by moving or deleting it from the graph.

- [ ] **Step 1: Shrink the `BoardNode` markup**

Replace the entire contents of `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Models.DTOs.Workflow

@* A board is a small port chip attached to its device's lane, not a full card (D18) - it still
   exists as its own positioned .wf-node (WorkflowAutoLayout is untouched), it just renders
   compact. Online/offline is written by JS into data-role="online", same no-rerender contract
   every other live value on this canvas already follows. *@

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale"
            ShowInPort="true" ShowOutPort="true"
            OnHeaderClick="OnSelectChildren">
    <div class="flex items-center justify-between">
        <span class="wf-status-pill" data-role="online">--</span>
        <button class="wf-btn wf-btn--sm" title="@(IsCollapsed ? "Expand channels" : "Collapse channels")"
                @onclick="() => OnToggleCollapse.InvokeAsync(Node.Id)">
            @(IsCollapsed ? "+" : "-")
        </button>
    </div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Board";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public bool IsCollapsed { get; set; }
    [Parameter] public EventCallback<string> OnToggleCollapse { get; set; }
    [Parameter] public EventCallback<string> OnSelectChildren { get; set; }
}
```

Note this drops the `ChannelCount` parameter entirely (the "N channel(s) placed" line is gone —
that information is still visible per-channel and via the Device's own rollup). `CanvasSurface`
must stop passing it (Step 2).

- [ ] **Step 2: Update `CanvasSurface`'s Board case**

In `Components/UI/WorkflowCanvas/CanvasSurface.razor`, replace:

```razor
            case NodeKind.Board:
                <BoardNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                           IsSelected="selected" IsStale="stale"
                           ChannelCount="@ChildCountFor(node.Id)"
                           IsCollapsed="@CollapsedBoardIds.Contains(node.Id)"
                           OnToggleCollapse="OnToggleBoardCollapse"
                           OnSelectChildren="OnSelectNodeChildren" />
                break;
```

with:

```razor
            case NodeKind.Board:
                <BoardNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                           IsSelected="selected" IsStale="stale"
                           IsCollapsed="@CollapsedBoardIds.Contains(node.Id)"
                           OnToggleCollapse="OnToggleBoardCollapse"
                           OnSelectChildren="OnSelectNodeChildren" />
                break;
```

- [ ] **Step 3: Add the compact-size and offline-tint CSS**

Append to `wwwroot/css/workflow-canvas.css`, near the existing `.wf-node--device`/`.wf-node--battery`
width rules:

```css
.wf-node--board {
    width: 96px;
}

.wf-node--board .wf-status-pill {
    text-transform: none;
}
```

- [ ] **Step 4: Toggle an offline look from the rollup**

In `wwwroot/js/workflow-canvas.js`, inside the existing rollup-application block (where
`setText(node, "online", r.onlineText)` already runs), add a class toggle so a Board's
online/offline state gets a visual treatment beyond the pill's own color — check the current
`applyTelemetry` rollup loop and extend it:

```javascript
            if (rollups) {
                for (const r of rollups) {
                    const node = s.world.querySelector(`.wf-node[data-node-id="${r.nodeId}"]`);
                    if (node) {
                        setText(node, "online", r.onlineText);
                        node.style.setProperty("--wf-status", r.isOnline ? "122 39% 49%" : "0 0% 62%");
                    }
                }
            }
```

(`122 39% 49%` and `0 0% 62%` are the existing "continue"/"offline" HSL triplets already defined
in `WorkflowTelemetryBridge.StatusHslByName` — this reuses the palette, it does not invent a new
color. This line runs for both Device and Board rollups; setting `--wf-status` on a Device node
here has no visible effect today since nothing on `DeviceNode` reads that variable, so this is
safe to apply unconditionally rather than branching on node kind.)

- [ ] **Step 5: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS — this task touches no C# test-covered logic (Tasks 1-2 already covered the data
side), but a full run confirms no bUnit test constructs `<BoardNode ChannelCount="...">` (which
would now fail to compile).

- [ ] **Step 6: Verify in the browser**

Stop the app, rebuild, restart, hard-reload (`.razor`/`.js`/`.css` all changed). Load a saved
layout with a full device placed. Confirm:
- The Board card is now a small ~96px-wide chip instead of the old full-width card, sitting in the
  same place as before (immediately right of the Device).
- The chip shows "Online" or "Offline" (not the old "N/M online" count) and its pill color matches
  online (green) / offline (grey).
- Clicking the chip's header still selects all of that board's channels (Phase 3 behavior,
  unchanged).
- Clicking the +/- button still collapses/expands that board's channel nodes on the canvas.
- With the animation fix from Task 1 also live: a channel with a genuinely idle/stopped program
  never pulses or shows a flowing edge, even if its last current reading was nonzero.

- [ ] **Step 7: Commit**

```bash
git add Components/UI/WorkflowCanvas/Nodes/BoardNode.razor Components/UI/WorkflowCanvas/CanvasSurface.razor wwwroot/css/workflow-canvas.css wwwroot/js/workflow-canvas.js
git commit -m "feat(workflow-canvas): compact board-slot chip replaces the full board card (D18)

BoardNode shrinks from a full card (title, channel count, N/M online
text, Collapse button) to a small ~96px chip showing just Online/
Offline (Task 2's per-board determination) and a compact collapse
toggle. The node's position, its existence in the persisted graph,
and the layout algorithm are all completely unchanged - only what it
renders as got smaller, which is what removes the visual clutter the
user asked about without any migration or edge-geometry risk.

Live-verified against the simulator: chip renders at the expected
size and position, online/offline coloring tracks IsConnected,
header-click-selects-children and collapse/expand both still work."
```

---

## Phase 4 Completion Checklist

- [ ] `FlowFor` requires `ProgramStatus.Running`; a nonzero-current Charge/Discharging reading on a stopped program never animates
- [ ] `NodeRollup.IsOnline` exists; Board rollups report a plain Online/Offline word, Device rollups are byte-for-byte unchanged in wording
- [ ] `BoardNode` renders as a compact chip at its existing position, no layout or edge-geometry code touched
- [ ] Board header-click-select-children and collapse/expand both still work live against the simulator
- [ ] Full suite green
- [ ] `main` has no commits from this work; `DashboardView.razor`/`TransferDialog.razor` remain byte-identical to `main`
