# Workflow Canvas Phase 2 — Lane Layout, Level of Detail, Node Properties Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the canvas's single-column-per-kind auto-layout with a per-device lane layout, add zoom-driven level-of-detail so hundreds of channels fit on one screen, and let the operator choose which live-data fields a channel node's face shows (up to 6, from a canvas-safe subset of the dashboard's property catalog).

**Architecture:** `WorkflowAutoLayout.Apply` — currently called once per device by `WorkflowPlacement.PlaceDevice` — is rewritten so a device's boards stack vertically and each board's channels fill a row of up to 8 columns, mirroring the physical 8-channels-per-board reality. Level of detail is implemented entirely in `workflow-canvas.js`/CSS as a `data-lod` attribute driven by zoom, never as Blazor state, preserving the zero-re-render guarantee proven in Phase 1. Node-face properties become a `key → formatted string` dictionary the bridge already has all the raw data for, rendered by `ChannelNode.razor` as a dynamic loop instead of four fixed rows, with the visible subset persisted per-user via the same `ServerSessionStorageService` the dashboard's card config already uses.

**Tech Stack:** .NET 8, Blazor Server (`InteractiveServer`), xUnit, bUnit 1.32.7, vanilla JS (no framework — matches the rest of `workflow-canvas.js`).

**Spec:** [docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md](../specs/2026-08-23-workflow-canvas-dashboard-parity-design.md) — sections 5 (D10) and 6 (D11).

## Global Constraints

- **Branch `feat/workflow-canvas-experiment` is NEVER merged to `main`.** Every task commits here only.
- **Level of detail must be pure CSS/JS — never Blazor state.** A `data-lod` attribute on `.wf-world`, written only when the tier actually changes. Verify with a MutationObserver expecting 0 `childList` mutations while zooming, matching every prior telemetry verification this branch has done.
- **Channels wrap at 8 per row** (`ChannelsPerRow = 8`), because a physical secondary board has at most 8 channels — confirmed by the `AddSecondaryBoardAndChannel` migration's own "1-8 backfill". This is not an arbitrary UI choice.
- **Node-face properties are capped at 6** (D11) and drawn only from keys the canvas can actually supply data for — see Task 3's exact 17-key list. Never offer a property in the picker that will only ever render `--`.
- **The properties dock (`PropertiesDock.razor`) is unaffected.** It keeps showing all fields unconditionally; only the compact node face becomes configurable.
- **Do not touch `DashboardView.razor` or `TransferDialog.razor`** (D13, still binding in this phase).
- Run the full suite with `dotnet test BatteryTestingSystem.Tests`. `DashboardRenderBatcherTests` is known-flaky under parallel load — re-run in isolation before treating a lone failure there as a regression.
- **Stop the running app before `dotnet build`/`dotnet test`, restart after** — it locks its own build output. Launch via the **PowerShell tool** (not Bash — Bash's network sandbox is unreachable from the real browser). App listens on **port 5066**.
- **Hard-reload the browser (`ignoreCache: true`) after any `wwwroot/js` or `wwwroot/css` change** before trusting what you see — session #27 lost time twice to a cached `workflow-canvas.js`.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `Services/Implementations/Workflow/WorkflowNodeProperties.cs` | The canvas-safe property catalog: which of the dashboard's 28 keys the canvas can supply, in what order, with what default visible set. |
| `Models/DTOs/Workflow/WorkflowNodeConfig.cs` | The per-user node-face selection (`IReadOnlyList<string> VisibleProperties`) plus pure toggle-with-cap logic. |
| `Components/UI/WorkflowCanvas/NodePropertyPicker.razor` | The small floating panel the operator uses to choose up to 6 properties. |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs` | |
| `BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs` | |
| `BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs` | |
| `BatteryTestingSystem.Tests/Components/NodePropertyPickerTests.cs` | |

**Modified**

| File | Change |
|---|---|
| `Services/Implementations/Workflow/WorkflowAutoLayout.cs` | Column-per-kind → per-device lane algorithm. |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs` | Full rewrite — the old assertions describe the algorithm being replaced. |
| `Services/Implementations/Workflow/WorkflowPlacement.cs` | New `NextFreeLaneY`; new `BatteryRailX` constant. |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs` | Add tests for the two additions above; existing tests untouched. |
| `Models/DTOs/Workflow/TelemetryEntry.cs` | `TelemetryEntry` gains `Properties: IReadOnlyDictionary<string,string>`. |
| `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs` | New pure `BuildNodeProperties`; wired into `BuildEntries`. |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs` | Add `BuildNodeProperties` tests. |
| `Components/UI/WorkflowCanvas/CanvasNode.razor` | Add a `title` attribute (LOD tile tooltip). |
| `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` | Voltage/Current/Power/Temperature fixed rows → a loop over `SelectedProperties`. |
| `Components/UI/WorkflowCanvas/CanvasSurface.razor` | New `SelectedProperties` parameter, threaded to `ChannelNode`. |
| `Components/Pages/Workflows/WorkflowCanvasPage.razor` | Lane-stacking wiring; loads/saves `WorkflowNodeConfig`; renders the picker toggle + panel. |
| `wwwroot/js/workflow-canvas.js` | `data-lod` stamping in `applyTransform`; generic `prop-{key}` write loop replacing the four hardcoded ones. |
| `wwwroot/css/workflow-canvas.css` | LOD tier rules; property-picker panel styling. |

---

## Task 1: Per-device lane layout

Replaces `WorkflowAutoLayout`'s column-per-kind placement — the cause of the ~13,000px illegible column at 640 channels — with one lane per device: the device node at the local origin, its boards stacked vertically, each board's channels filling a row of up to 8 columns beside it. `Apply` is called by `WorkflowPlacement.PlaceDevice` with exactly one device's freshly-built subgraph (confirmed: it has no other caller), so the algorithm only has to get that one shape right — but is written defensively for bare/orphan graphs too, since several existing tests (and this task's own tests) construct graphs without a full device→board→channel tree.

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowAutoLayout.cs`
- Modify: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs` (full rewrite)

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowNode`, `WorkflowEdge`, `NodeKind`, `EdgeKind` (`Models/DTOs/Workflow/`).
- Produces: `WorkflowAutoLayout.Apply(WorkflowGraph) → WorkflowGraph` (signature unchanged); new public constant `WorkflowAutoLayout.ChannelsPerRow = 8`. `ColumnWidth`, `RowHeight`, `NodeWidth`, `NodeHeight` keep their names and values — Task 2 (`WorkflowPlacement`) depends on `ColumnWidth`/`RowHeight` still existing.

- [ ] **Step 1: Replace the test file**

The old tests assert column-per-kind X positions that no longer exist. Replace the entire contents of `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Lane layout (spec section 5, D10): one device per lane, its boards stacked vertically, each
/// board's channels filling a row of up to ChannelsPerRow columns — mirroring the physical
/// 8-channels-per-board reality so the wrap means something. Apply is called by
/// WorkflowPlacement.PlaceDevice with exactly one device's freshly-built subgraph; these tests
/// also cover bare/orphan graphs defensively, since several construct graphs without a full
/// device-board-channel tree.
/// </summary>
public class WorkflowAutoLayoutTests
{
    private static WorkflowGraph Graph(
        IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowEdge>? edges = null) =>
        WorkflowGraph.Empty with
        {
            Nodes = nodes.ToList(),
            Edges = (edges ?? Enumerable.Empty<WorkflowEdge>()).ToList(),
        };

    private static WorkflowNode Device(string id = "dev-1") => new(id, NodeKind.Device, 1, 0, 0, null);
    private static WorkflowNode Board(string id) => new(id, NodeKind.Board, 1, 0, 0, null);
    private static WorkflowNode Channel(string id) => new(id, NodeKind.Channel, 1, 0, 0, null);

    private static WorkflowEdge Topology(string from, string to) =>
        new($"edge-{from}--{to}", from, to, EdgeKind.Topology);

    /// <summary>Builds one device with `boardChannelCounts.Length` boards, each with the given
    /// number of channels, fully wired with topology edges — the exact shape
    /// WorkflowGraphBuilder.BuildForDevice produces.</summary>
    private static WorkflowGraph DeviceGraph(params int[] boardChannelCounts)
    {
        var nodes = new List<WorkflowNode> { Device() };
        var edges = new List<WorkflowEdge>();

        for (var b = 0; b < boardChannelCounts.Length; b++)
        {
            var boardId = $"brd-{b}";
            nodes.Add(Board(boardId));
            edges.Add(Topology("dev-1", boardId));

            for (var c = 0; c < boardChannelCounts[b]; c++)
            {
                var channelId = $"chn-{b}-{c}";
                nodes.Add(Channel(channelId));
                edges.Add(Topology(boardId, channelId));
            }
        }

        return Graph(nodes, edges);
    }

    [Fact]
    public void Apply_PlacesTheDeviceAtTheLaneOrigin()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8));

        var device = result.Nodes.Single(n => n.Kind == NodeKind.Device);
        Assert.Equal(0, device.X);
        Assert.Equal(0, device.Y);
    }

    [Fact]
    public void Apply_PlacesEveryBoardInTheSameColumn()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8, 8, 8));

        var boardXs = result.Nodes.Where(n => n.Kind == NodeKind.Board).Select(n => n.X).Distinct();
        Assert.Equal(new[] { WorkflowAutoLayout.ColumnWidth }, boardXs);
    }

    [Fact]
    public void Apply_StacksBoardsVerticallyWithoutOverlapping()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8, 8, 8));

        var boardYs = result.Nodes.Where(n => n.Kind == NodeKind.Board)
            .Select(n => n.Y).OrderBy(y => y).ToList();

        Assert.Equal(3, boardYs.Distinct().Count());
        for (var i = 1; i < boardYs.Count; i++)
            Assert.True(boardYs[i] - boardYs[i - 1] >= WorkflowAutoLayout.RowHeight);
    }

    [Fact]
    public void Apply_FillsAnEightChannelBoardAsOneRow()
    {
        // The common real case: exactly one physical board's worth of channels lands on one row,
        // beside its board node — the wrap never triggers for real hardware.
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8));

        var board = result.Nodes.Single(n => n.Kind == NodeKind.Board);
        var channels = result.Nodes.Where(n => n.Kind == NodeKind.Channel).ToList();

        Assert.All(channels, c => Assert.Equal(board.Y, c.Y));
        Assert.Equal(8, channels.Select(c => c.X).Distinct().Count());
    }

    [Fact]
    public void Apply_WrapsAChannelRowPastEightColumns()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(10));

        var board = result.Nodes.Single(n => n.Kind == NodeKind.Board);
        var channels = result.Nodes.Where(n => n.Kind == NodeKind.Channel)
            .OrderBy(n => n.Id).ToList();

        // First 8 share the board's row; the 9th and 10th drop to the next row.
        Assert.All(channels.Take(8), c => Assert.Equal(board.Y, c.Y));
        Assert.All(channels.Skip(8), c => Assert.Equal(board.Y + WorkflowAutoLayout.RowHeight, c.Y));
    }

    [Fact]
    public void Apply_MovesTheNextBoardBelowTheWrappedRowsOfThePreviousOne()
    {
        // Board 0 has 10 channels (2 rows); board 1 must start below BOTH of those rows, not
        // immediately below board 0's own row — the exact overlap the old column layout risked.
        var result = WorkflowAutoLayout.Apply(DeviceGraph(10, 8));

        var boards = result.Nodes.Where(n => n.Kind == NodeKind.Board)
            .OrderBy(n => n.Id).ToList();

        Assert.Equal(0, boards[0].Y);
        Assert.Equal(2 * WorkflowAutoLayout.RowHeight, boards[1].Y);
    }

    [Fact]
    public void Apply_GivesAChannellessBoardExactlyOneRowOfHeight()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(0, 8));

        var boards = result.Nodes.Where(n => n.Kind == NodeKind.Board)
            .OrderBy(n => n.Id).ToList();

        Assert.Equal(WorkflowAutoLayout.RowHeight, boards[1].Y);
    }

    [Fact]
    public void Apply_PlacesOrphanChannelsInTheirOwnTrailingGrid_RatherThanOverlappingAnything()
    {
        // A bare graph with no device/board/edges at all — defensive robustness, not a real
        // canvas shape, but Apply must never crash or double-place on unexpected input.
        var graph = Graph(new[] { Channel("c1"), Channel("c2"), Channel("c3") });

        var result = WorkflowAutoLayout.Apply(graph);

        var points = result.Nodes.Select(n => (n.X, n.Y)).ToList();
        Assert.Equal(points.Count, points.Distinct().Count());
    }

    [Fact]
    public void Apply_IsDeterministic_ForIdenticalInput()
    {
        var graph = DeviceGraph(8, 8);

        var first = WorkflowAutoLayout.Apply(graph);
        var second = WorkflowAutoLayout.Apply(graph);

        Assert.Equal(first.Nodes, second.Nodes);
    }

    [Fact]
    public void Apply_NeverPlacesTwoNodesAtTheSamePoint_AtFullDeviceScale()
    {
        // A real device: 8 boards of 8 channels each, matching the AddSecondaryBoardAndChannel
        // 1-8 convention this whole algorithm is built around.
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8, 8, 8, 8, 8, 8, 8, 8));

        var points = result.Nodes.Select(n => (n.X, n.Y)).ToList();
        Assert.Equal(points.Count, points.Distinct().Count());
    }

    [Fact]
    public void Apply_PreservesNodeIdentityAndAttachments()
    {
        var graph = Graph(new[]
        {
            Device(), Board("brd-0"),
            new WorkflowNode("chn-0-0", NodeKind.Channel, 42, 0, 0, new NodeAttachment(7, null, null)),
        }, new[] { Topology("dev-1", "brd-0"), Topology("brd-0", "chn-0-0") });

        var result = WorkflowAutoLayout.Apply(graph);

        var channel = result.Nodes.Single(n => n.Kind == NodeKind.Channel);
        Assert.Equal("chn-0-0", channel.Id);
        Assert.Equal(42, channel.EntityId);
        Assert.Equal(7, channel.Attach!.ProgramId);
    }

    [Fact]
    public void Apply_LeavesEdgesAndViewportUntouched()
    {
        var graph = DeviceGraph(8) with { Viewport = new CanvasViewport(5, 6, 0.5) };

        var result = WorkflowAutoLayout.Apply(graph);

        Assert.Equal(graph.Edges, result.Edges);
        Assert.Equal(graph.Viewport, result.Viewport);
    }

    [Fact]
    public void Apply_HandlesAnEmptyGraph()
    {
        var result = WorkflowAutoLayout.Apply(WorkflowGraph.Empty);

        Assert.Empty(result.Nodes);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowAutoLayoutTests"
```

Expected: FAIL — the old `ColumnOf`-based `Apply` places every board at `Y=0` (one row per kind, not per board), so `Apply_StacksBoardsVerticallyWithoutOverlapping` and most others fail.

- [ ] **Step 3: Replace the implementation**

Replace the entire contents of `Services/Implementations/Workflow/WorkflowAutoLayout.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Lane layout: one device's boards stack vertically, and each board's channels fill a row of up
/// to ChannelsPerRow columns beside it. 8 is not an arbitrary UI choice — a physical secondary
/// board has at most 8 channels (see the AddSecondaryBoardAndChannel migration's own "1-8
/// backfill"), so a real board's channels always land on exactly one row and the wrap only ever
/// triggers on a hypothetical oversized board.
///
/// Apply's only caller, WorkflowPlacement.PlaceDevice, always passes one device's freshly-built
/// subgraph (0-64 nodes), so a full edge scan per call has no scale concern. The algorithm still
/// handles bare/orphan graphs (no device, channels with no board) defensively — several tests,
/// and any future direct caller, may not hand it a full topology tree.
/// </summary>
public static class WorkflowAutoLayout
{
    public const double ColumnWidth = 280;
    public const double RowHeight = 120;
    public const double NodeWidth = 200;
    public const double NodeHeight = 96;
    public const int ChannelsPerRow = 8;

    private const double BoardColumnX = ColumnWidth;
    private const double ChannelStartX = ColumnWidth * 2;

    public static WorkflowGraph Apply(WorkflowGraph graph)
    {
        var positions = new Dictionary<string, (double X, double Y)>();
        double cursorY = 0;

        var device = graph.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Device);
        if (device is not null) positions[device.Id] = (0, 0);

        var assignedChannelIds = new HashSet<string>();

        foreach (var board in graph.Nodes.Where(n => n.Kind == NodeKind.Board))
        {
            var channelIds = graph.Edges
                .Where(e => e.FromNodeId == board.Id && e.Kind == EdgeKind.Topology)
                .Select(e => e.ToNodeId)
                .ToHashSet();

            var channels = graph.Nodes
                .Where(n => n.Kind == NodeKind.Channel && channelIds.Contains(n.Id))
                .ToList();

            positions[board.Id] = (BoardColumnX, cursorY);

            for (var i = 0; i < channels.Count; i++)
            {
                var row = i / ChannelsPerRow;
                var col = i % ChannelsPerRow;
                positions[channels[i].Id] = (ChannelStartX + col * ColumnWidth, cursorY + row * RowHeight);
                assignedChannelIds.Add(channels[i].Id);
            }

            var rowsUsed = channels.Count == 0
                ? 1
                : (int)Math.Ceiling(channels.Count / (double)ChannelsPerRow);
            cursorY += rowsUsed * RowHeight;
        }

        // Orphan channels: no board claimed them. A bare test graph, or a future graph shape
        // this algorithm has not been taught about — never silently dropped or overlapped.
        var orphanChannels = graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel && !assignedChannelIds.Contains(n.Id))
            .ToList();

        for (var i = 0; i < orphanChannels.Count; i++)
        {
            var row = i / ChannelsPerRow;
            var col = i % ChannelsPerRow;
            positions[orphanChannels[i].Id] = (ChannelStartX + col * ColumnWidth, cursorY + row * RowHeight);
        }

        var positioned = graph.Nodes
            .Select(node => positions.TryGetValue(node.Id, out var pos) ? node with { X = pos.X, Y = pos.Y } : node)
            .ToList();

        return graph with { Nodes = positioned };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowAutoLayoutTests"
```

Expected: PASS (13 tests).

- [ ] **Step 5: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS. (`WorkflowPlacementTests` should be unaffected — it never asserts board/channel X/Y, only device X/Y and node/edge counts, none of which this change alters.)

- [ ] **Step 6: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowAutoLayout.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs
git commit -m "refactor(workflow-canvas): replace column-per-kind layout with per-device lanes

WorkflowAutoLayout placed every node of a kind in one column, producing a
~13,000px illegible column at 640 channels (documented in the Task 17
report). Replaced with lane layout: boards stack vertically, each
board's channels fill a row of up to ChannelsPerRow=8 columns beside it
- 8 because a physical board has at most 8 channels, not an arbitrary
UI choice, so a real board's channels land on exactly one row.

Apply's only caller (WorkflowPlacement.PlaceDevice) always passes one
device's subgraph; the algorithm stays defensive for bare/orphan graphs
since several tests construct those directly.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Lane stacking and the battery rail

Task 1 makes a single device's lane correctly shaped, but placing a *second* device still overlaps the first: `WorkflowPlacement.NextFreeRow(graph, NodeKind.Device)` finds the max Y among **device nodes only** — and every device node sits at its own lane's local `Y=0` after translation, so it never reflects how many rows of boards/channels that lane actually grew to. Also, battery placement's fixed `X = ColumnWidth * 3` (840px) now lands **inside** a lane's channel grid, which extends to `ColumnWidth * 2 + 8 * ColumnWidth = 2800px`.

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowPlacement.cs`
- Modify: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`

**Interfaces:**
- Consumes: `WorkflowAutoLayout.ColumnWidth`, `.RowHeight`, `.ChannelsPerRow` (Task 1).
- Produces: `WorkflowPlacement.NextFreeLaneY(WorkflowGraph) → double`; `WorkflowPlacement.BatteryRailX` (constant).

- [ ] **Step 1: Write the failing tests**

Append to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs`, inside the class:

```csharp
    [Fact]
    public void NextFreeLaneY_ReturnsZeroForAnEmptyCanvas()
    {
        Assert.Equal(0, WorkflowPlacement.NextFreeLaneY(WorkflowGraph.Empty));
    }

    [Fact]
    public void NextFreeLaneY_ClearsTheFullHeightOfEveryNodeOnTheCanvas_NotJustDeviceNodes()
    {
        // The bug this exists to prevent: every device node sits at its own lane's local Y=0
        // (WorkflowAutoLayout.Apply), so looking only at device-kind Y (NextFreeRow's contract)
        // would place the next lane right on top of the previous one's channel grid tail.
        var tallLane = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(id: 1, channels: 10), null, 0, 0);

        var nextY = WorkflowPlacement.NextFreeLaneY(tallLane);

        var lowestNodeY = tallLane.Nodes.Max(n => n.Y);
        Assert.True(nextY > lowestNodeY);
    }

    [Fact]
    public void NextFreeLaneY_LeavesNoOverlapWhenUsedToPlaceASecondDevice()
    {
        var first = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(id: 1, channels: 10), null, 0, 0);

        var secondOriginY = WorkflowPlacement.NextFreeLaneY(first);
        var both = WorkflowPlacement.PlaceDevice(first, Device(id: 2, channels: 2), null, 0, secondOriginY);

        var firstLaneYs = first.Nodes.Select(n => n.Y).ToHashSet();
        var secondLaneYs = both.Nodes.Where(n => n.EntityId == 2).Select(n => n.Y).ToHashSet();

        Assert.Empty(firstLaneYs.Intersect(secondLaneYs));
    }

    [Fact]
    public void BatteryRailX_ClearsTheWidestPossibleChannelGrid()
    {
        // The widest a lane's channel grid ever gets: ChannelStartX (2 columns in) plus a full
        // ChannelsPerRow-wide row. The rail must sit at or past that, with room for a battery
        // node (BatteryAutoLayout does not itself size the node, but must not START inside it).
        var widestChannelGridRightEdge =
            WorkflowAutoLayout.ColumnWidth * 2 + WorkflowAutoLayout.ChannelsPerRow * WorkflowAutoLayout.ColumnWidth;

        Assert.True(WorkflowPlacement.BatteryRailX >= widestChannelGridRightEdge);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowPlacementTests"
```

Expected: FAIL — `NextFreeLaneY` and `BatteryRailX` do not exist.

- [ ] **Step 3: Add the two members**

In `Services/Implementations/Workflow/WorkflowPlacement.cs`, add after `NextFreeRow`:

```csharp
    /// <summary>The Y just below the lowest node anywhere on the canvas — for stacking whole
    /// device LANES one below another. NextFreeRow(graph, NodeKind.Device) is insufficient here:
    /// every device node sits at its own lane's local Y=0 (WorkflowAutoLayout.Apply), so it never
    /// reflects how many rows of boards/channels that lane actually grew to.</summary>
    public static double NextFreeLaneY(WorkflowGraph graph) =>
        graph.Nodes.Count == 0 ? 0 : graph.Nodes.Max(n => n.Y) + WorkflowAutoLayout.RowHeight;

    /// <summary>Fixed X for the battery column, clear of the widest possible lane (2 columns of
    /// header room plus a full ChannelsPerRow-wide channel row, plus one gutter column) so a
    /// battery never lands inside a device's channel grid regardless of that device's shape.</summary>
    public const double BatteryRailX =
        WorkflowAutoLayout.ColumnWidth * 2
        + WorkflowAutoLayout.ChannelsPerRow * WorkflowAutoLayout.ColumnWidth
        + WorkflowAutoLayout.ColumnWidth;
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowPlacementTests"
```

Expected: PASS (previous tests plus 4 new ones).

- [ ] **Step 5: Wire both into the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, in `HandlePlaceDevice`:

```csharp
    private async Task HandlePlaceDevice(
        (TopologyDevice Device, IReadOnlyCollection<long>? Channels) placement)
    {
        var originY = WorkflowPlacement.NextFreeLaneY(_graph);

        _graph = WorkflowPlacement.PlaceDevice(
            _graph, placement.Device, placement.Channels, 0, originY);

        _dirty = true;
        RefreshSubscription();
        await InvokeAsync(StateHasChanged);
    }
```

(Only `NextFreeRow(_graph, NodeKind.Device)` → `NextFreeLaneY(_graph)` changes.)

In `HandlePlaceBattery`:

```csharp
    private async Task HandlePlaceBattery(int batteryTypeId)
    {
        var x = WorkflowPlacement.BatteryRailX;
        var y = WorkflowPlacement.NextFreeRow(_graph, NodeKind.Battery);

        _graph = WorkflowPlacement.PlaceBattery(_graph, batteryTypeId, x, y);
        _dirty = true;
        await InvokeAsync(StateHasChanged);
    }
```

(`WorkflowAutoLayout.ColumnWidth * 3` → `WorkflowPlacement.BatteryRailX`; `NextFreeRow(_graph, NodeKind.Battery)` unchanged — batteries still stack in their own column independent of lanes.)

- [ ] **Step 6: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 7: Verify in the browser — this is the first visible payoff of Phase 2**

Stop the app, rebuild, restart via PowerShell, hard-reload the browser. Place two full devices via the palette's **All** button (e.g. `SIM_DEVICE_001_1` then `SIM_DEVICE_002_1`). Confirm:
- Each device renders as a compact 8-row-tall lane (device node, 8 boards stacked, each board's 8 channels beside it in one row) instead of a single tall column.
- The second device's lane starts clearly below the first — no overlapping nodes.
- Place a battery from the palette; confirm it renders to the right of both lanes, not inside either channel grid.

Click **Fit** and screenshot — this is the direct answer to "how does the user see it all on one screen": two devices should now occupy a wide, short rectangle instead of one impossibly tall column.

- [ ] **Step 8: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowPlacement.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs \
  Components/Pages/Workflows/WorkflowCanvasPage.razor
git commit -m "fix(workflow-canvas): stack device lanes and the battery rail without overlap

NextFreeRow(graph, NodeKind.Device) looked only at device-node Y, which
is always 0 within a lane after WorkflowAutoLayout.Apply - so it never
reflected how tall that lane's boards/channels actually grew, and a
second device would land on top of the first's channel grid. New
NextFreeLaneY clears the full height of every node on the canvas.

The battery column's fixed X (ColumnWidth*3 = 840px) also now lands
inside a lane's channel grid, which extends to 2800px. New BatteryRailX
clears the widest possible lane.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Level of detail

Three zoom-driven tiers, implemented entirely in JS/CSS so zooming never touches Blazor state. This is what turns "many short lanes" (Task 2) into "all channels visible on one screen" (the original ask) — at the smallest tier, hundreds of channels collapse to a colour wall.

**Files:**
- Modify: `wwwroot/js/workflow-canvas.js`
- Modify: `wwwroot/css/workflow-canvas.css`
- Modify: `Components/UI/WorkflowCanvas/CanvasNode.razor`
- Test: `BatteryTestingSystem.Tests/Components/CanvasNodeTests.cs` (new — the one real C# surface this task has)

**Interfaces:**
- Consumes: `s.zoom` (existing pan/zoom state in `workflow-canvas.js`), `applyTransform(s)` (existing single choke point for every pan/zoom/setViewport path — 5 call sites, confirmed).
- Produces: `.wf-world[data-lod="0|1|2"]` attribute; `CanvasNode`'s root element gains a native `title` attribute.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Components/CanvasNodeTests.cs`:

```csharp
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// At the smallest level-of-detail tier (spec section 5) a node's header/body text is hidden by
/// CSS and it renders as a plain colour tile — the title attribute is what lets an operator still
/// identify one on hover, since the CSS work itself is not something a unit test can see.
/// </summary>
public class CanvasNodeTests : TestContext
{
    [Fact]
    public void RootElementCarriesATitleAttributeForLodTileHoverIdentification()
    {
        var node = new WorkflowNode("chn-1", NodeKind.Channel, 1, 0, 0, null);

        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, node)
            .Add(x => x.Title, "1-8-2"));

        Assert.Equal("1-8-2", cut.Find(".wf-node").GetAttribute("title"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~CanvasNodeTests"
```

Expected: FAIL — no `title` attribute on the root `.wf-node` div.

- [ ] **Step 3: Add the attribute**

In `Components/UI/WorkflowCanvas/CanvasNode.razor`, add `title="@Title"` to the root `<div class="wf-node ...">` tag (alongside its existing `data-node-id`/`data-x`/`data-y` attributes):

```razor
<div class="wf-node wf-node--@Node.Kind.ToString().ToLowerInvariant() @(IsSelected ? "wf-node--selected" : "") @(IsStale ? "wf-node--stale" : "")"
     style="transform: translate(@(Node.X.ToString(Culture))px, @(Node.Y.ToString(Culture))px);"
     title="@Title"
     data-node-id="@Node.Id"
     data-x="@Node.X.ToString(Culture)"
     data-y="@Node.Y.ToString(Culture)">
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~CanvasNodeTests"
```

Expected: PASS.

- [ ] **Step 5: Stamp the LOD tier in JS**

In `wwwroot/js/workflow-canvas.js`, add a pure tier function near the top of the IIFE (after the `capture`/`release` helpers, before `applyTransform`):

```javascript
    // Level-of-detail tiers, purely zoom-driven. Never touches Blazor — a tier change is a CSS
    // attribute write, the same DOM-only contract every other live value in this file follows.
    function lodForZoom(zoom) {
        if (zoom >= 0.8) return 0;   // full card
        if (zoom >= 0.4) return 1;   // header + first property row
        return 2;                    // colour tile only
    }
```

Then extend `applyTransform` (its existing three-line body):

```javascript
    function applyTransform(s) {
        s.world.style.transform =
            `translate(${s.panX}px, ${s.panY}px) scale(${s.zoom})`;
        // Keep the dot grid locked to the content.
        s.host.style.backgroundPosition = `${s.panX}px ${s.panY}px`;
        s.host.style.backgroundSize = `${24 * s.zoom}px ${24 * s.zoom}px`;

        const lod = String(lodForZoom(s.zoom));
        if (s.world.dataset.lod !== lod) s.world.dataset.lod = lod;
    }
```

`applyTransform` is already the single choke point for every pan/zoom/`setViewport` path (5 call sites), so no other JS function needs to change.

- [ ] **Step 6: Add the CSS tiers**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- level of detail */

/* Tier 1 (zoom 0.4-0.8): header + the first body row only. .wf-node__body's children are the
   fixed status/soc row, then the property rows (Task 5), then the fixed program-status/last-
   update row, then badges - nth-child(n+2) hides everything after the first of those. */
.wf-world[data-lod="1"] .wf-node__body > :nth-child(n+2) { display: none; }

/* Tier 2 (zoom < 0.4): a pure colour tile. Header/body text is hidden, not removed - the title
   attribute (CanvasNode.razor) keeps the address available as a native tooltip on hover. */
.wf-world[data-lod="2"] .wf-node {
    width: 32px;
    height: 32px;
    padding: 0;
    overflow: hidden;
    background: hsl(var(--wf-status, var(--status-idle)));
}

.wf-world[data-lod="2"] .wf-node__header,
.wf-world[data-lod="2"] .wf-node__body {
    display: none;
}
```

- [ ] **Step 7: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 8: Verify in the browser, including the no-re-render guarantee**

Stop the app, rebuild, restart, hard-reload. Place a full device (64 channels via **All**). With the simulator running so at least one channel has a live status colour:

```javascript
document.querySelector('.wf-canvas-host').dispatchEvent(new WheelEvent('wheel', {deltaY: 400, bubbles: true}));
// or directly:
window.workflowCanvas.setViewport(document.querySelector('.wf-canvas-host'), 0, 0, 0.3);
```

Confirm `document.querySelector('.wf-world').dataset.lod === "2"` and that channel nodes have visibly shrunk to coloured tiles. Then:

```javascript
window.workflowCanvas.setViewport(document.querySelector('.wf-canvas-host'), 0, 0, 1);
```

Confirm `dataset.lod === "0"` and full cards are back. Finally, re-run the standard MutationObserver check across a rapid zoom sweep:

```javascript
let n = 0;
const obs = new MutationObserver(ms => ms.forEach(m => { if (m.type === 'childList') n++; }));
obs.observe(document.querySelector('.wf-world'), {childList: true, subtree: true});
const host = document.querySelector('.wf-canvas-host');
for (let z = 0.2; z <= 1.2; z += 0.1) window.workflowCanvas.setViewport(host, 0, 0, z);
obs.disconnect();
n; // expect 0
```

Expected: `0`.

- [ ] **Step 9: Commit**

```bash
git add wwwroot/js/workflow-canvas.js wwwroot/css/workflow-canvas.css \
  Components/UI/WorkflowCanvas/CanvasNode.razor \
  BatteryTestingSystem.Tests/Components/CanvasNodeTests.cs
git commit -m "feat(workflow-canvas): zoom-driven level of detail

Three tiers stamped as .wf-world[data-lod] purely from JS/CSS, never
Blazor state: full card at zoom >=0.8, header+first row at 0.4-0.8, a
plain colour tile below 0.4. This is what makes 'see it all on one
screen' actually work at scale - lane layout alone (prior commit) still
leaves 640 channels too tall to fit; the smallest tier collapses that to
a colour wall an operator can still spot a fault in.

CanvasNode gained a title attribute so a tile still identifies itself on
hover once its text is hidden. Verified 0 childList mutations across a
zoom sweep from 0.2x to 1.2x, matching every prior telemetry
verification on this branch.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: The node-property catalog

Before any UI, establish exactly which of the dashboard's 28 selectable properties (`CardPreviewData`, confirmed in the design spec) the canvas can actually supply live data for. This is a deliberate **subset**, not a full 28: `ChannelTelemetry` (built in Phase 1) has every `RealTimeProperties` field but only 5 of 13 `ProgramProperties` fields, and none of the 3 `ConfigProperties` fields (those come from a different data source — the channel's DB config record, never loaded into `ChannelTelemetry`). Offering an unavailable property in the picker would show `--` forever, which is exactly the kind of fabricated-looking gap this branch has avoided everywhere else (SoC, the online-count denominator).

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowNodeProperties.cs`
- Create: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs`

**Interfaces:**
- Consumes: `CardPreviewData.RealTimeProperties`, `.ProgramProperties`, `.FindPropertyMetadata(string)` (`Components/UI/Dashboard/CardPreviewData.cs`).
- Produces:
  - `WorkflowNodeProperties.AvailableKeys: IReadOnlyList<string>` (17 keys)
  - `WorkflowNodeProperties.DefaultVisibleProperties: IReadOnlyList<string>` (4 keys: `Voltage`, `Current`, `Power`, `Temperature` — reproduces exactly what shipped in session #26/#27, so a user who never opens the picker sees no change)
  - `WorkflowNodeProperties.Label(string key) → string`
  - `WorkflowNodeProperties.MaxVisible = 6`

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs`:

```csharp
using System.Linq;
using BatteryTestingSystem.Components.UI.Dashboard;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The canvas can only offer properties it can actually supply live data for. ChannelTelemetry
/// (Phase 1) covers every RealTimeProperties key but only 5 of CardPreviewData's 13
/// ProgramProperties keys, and none of its 3 ConfigProperties keys (those come from the
/// channel's DB config, never loaded into ChannelTelemetry). Offering an unavailable key would
/// show "--" forever - the exact kind of gap this branch has avoided everywhere else.
/// </summary>
public class WorkflowNodePropertiesTests
{
    [Fact]
    public void AvailableKeys_ContainsExactlySeventeenKeys()
    {
        Assert.Equal(17, WorkflowNodeProperties.AvailableKeys.Count);
    }

    [Fact]
    public void AvailableKeys_ContainsEveryRealTimeProperty()
    {
        foreach (var key in CardPreviewData.RealTimeProperties.Keys)
            Assert.Contains(key, WorkflowNodeProperties.AvailableKeys);
    }

    [Theory]
    [InlineData("CycleNumber")]
    [InlineData("TableStepNumber")]
    [InlineData("TableTotalRowNumber")]
    [InlineData("StepRunningTime")]
    [InlineData("RunningTime")]
    public void AvailableKeys_ContainsTheFiveSupportedProgramProperties(string key)
    {
        Assert.Contains(key, WorkflowNodeProperties.AvailableKeys);
    }

    [Theory]
    [InlineData("CycleStatus")]
    [InlineData("CycleRunIteration")]
    [InlineData("OperatorCode")]
    [InlineData("Storerecordcount")]
    [InlineData("Unstorerecordcount")]
    [InlineData("Error")]
    [InlineData("SystemError")]
    [InlineData("BatteryID")]
    [InlineData("ProgramID")]
    [InlineData("SessionID")]
    public void AvailableKeys_ExcludesEveryUnsupportedDashboardProperty(string key)
    {
        Assert.DoesNotContain(key, WorkflowNodeProperties.AvailableKeys);
    }

    [Fact]
    public void AvailableKeys_HasNoDuplicates()
    {
        Assert.Equal(WorkflowNodeProperties.AvailableKeys.Count,
            WorkflowNodeProperties.AvailableKeys.Distinct().Count());
    }

    [Fact]
    public void EveryAvailableKey_ResolvesRealMetadataFromTheSharedCatalog()
    {
        // Sharing CardPreviewData is the point of D11: labels can never drift between the
        // dashboard and the canvas because there is only one place they are defined.
        foreach (var key in WorkflowNodeProperties.AvailableKeys)
            Assert.NotNull(CardPreviewData.FindPropertyMetadata(key));
    }

    [Fact]
    public void DefaultVisibleProperties_ReproducesWhatAlreadyShipped()
    {
        // A user who never opens the picker must see no change from Phase 1 (90d9b8d / 7335b81).
        Assert.Equal(new[] { "Voltage", "Current", "Power", "Temperature" },
            WorkflowNodeProperties.DefaultVisibleProperties);
    }

    [Fact]
    public void DefaultVisibleProperties_IsWithinTheMaxVisibleCap()
    {
        Assert.True(WorkflowNodeProperties.DefaultVisibleProperties.Count <= WorkflowNodeProperties.MaxVisible);
    }

    [Fact]
    public void MaxVisible_IsSix()
    {
        Assert.Equal(6, WorkflowNodeProperties.MaxVisible);
    }

    [Fact]
    public void Label_ReturnsTheSharedCatalogsLabel()
    {
        Assert.Equal("Voltage (V)", WorkflowNodeProperties.Label("Voltage"));
    }

    [Fact]
    public void Label_FallsBackToTheKeyItselfForAnUnknownKey()
    {
        // Defensive only - every key actually in AvailableKeys resolves (proven above); this
        // covers a caller passing something outside that set without throwing.
        Assert.Equal("NotAKey", WorkflowNodeProperties.Label("NotAKey"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowNodePropertiesTests"
```

Expected: FAIL — `WorkflowNodeProperties` does not exist.

- [ ] **Step 3: Implement the catalog**

Create `Services/Implementations/Workflow/WorkflowNodeProperties.cs`:

```csharp
using BatteryTestingSystem.Components.UI.Dashboard;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// The subset of the dashboard's 28 selectable properties (CardPreviewData) that the canvas can
/// actually supply live data for, via ChannelTelemetry.
///
/// This is deliberately NOT the full 28. ChannelTelemetry covers every RealTimeProperties field
/// (12) plus 5 of CardPreviewData's 13 ProgramProperties fields - CycleNumber, TableStepNumber,
/// TableTotalRowNumber, StepRunningTime, RunningTime - and none of its 3 ConfigProperties fields
/// (BatteryID/ProgramID/SessionID come from the channel's DB config record, never loaded into
/// ChannelTelemetry). Error/SystemError are also excluded: the bridge already resolves both into
/// one merged ErrorText field for the dock, with no clean 1:1 split back into the catalog's two
/// separate keys. Offering any of these in the picker would show "--" forever.
///
/// Labels are read from CardPreviewData rather than duplicated (D11): the dashboard and the
/// canvas can never show different wording for the same property.
/// </summary>
public static class WorkflowNodeProperties
{
    public const int MaxVisible = 6;

    public static readonly IReadOnlyList<string> AvailableKeys = new List<string>(
        CardPreviewData.RealTimeProperties.Keys.Concat(new[]
        {
            "CycleNumber", "TableStepNumber", "TableTotalRowNumber", "StepRunningTime", "RunningTime",
        }));

    public static readonly IReadOnlyList<string> DefaultVisibleProperties =
        new List<string> { "Voltage", "Current", "Power", "Temperature" };

    public static string Label(string key) =>
        CardPreviewData.FindPropertyMetadata(key)?.Label ?? key;
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowNodePropertiesTests"
```

Expected: PASS (12 tests).

- [ ] **Step 5: Run the full suite and commit**

```bash
dotnet test BatteryTestingSystem.Tests
git add Services/Implementations/Workflow/WorkflowNodeProperties.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs
git commit -m "feat(workflow-canvas): canvas-safe subset of the dashboard's property catalog

17 of CardPreviewData's 28 keys - every RealTimeProperties field plus 5
of 13 ProgramProperties fields ChannelTelemetry actually carries.
Excludes BatteryID/ProgramID/SessionID (different data source, never
loaded into ChannelTelemetry) and Error/SystemError (already merged into
one ErrorText field with no clean split). Labels delegate to
CardPreviewData so the dashboard and canvas can never show different
wording for the same property (D11).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Node config model and toggle logic

The per-user selection state, and the pure add/remove-with-cap rule the picker UI will drive. Kept independent of any persistence or UI so it is fully unit-testable — mirrors this repo's established pattern of extracting DI-free logic (e.g. `CircuitSelectionLogic`) rather than only covering it through bUnit.

**Files:**
- Create: `Models/DTOs/Workflow/WorkflowNodeConfig.cs`
- Create: `BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs`

**Interfaces:**
- Consumes: `WorkflowNodeProperties.DefaultVisibleProperties`, `.MaxVisible` (Task 4).
- Produces:
  - `record WorkflowNodeConfig(IReadOnlyList<string> VisibleProperties)`
  - `WorkflowNodeConfig.Default: WorkflowNodeConfig` (static)
  - `WorkflowNodeConfig.Toggle(string key) → WorkflowNodeConfig` (instance method: adds if absent and under the cap, removes if present, no-ops if absent and at the cap)

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs`:

```csharp
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Models;

/// <summary>
/// The picker's whole interaction model is one method: toggling a key either adds, removes, or
/// (at the cap) refuses. Testing this independently of any UI is what makes the 6-cap a
/// guaranteed invariant rather than something a checkbox's disabled state merely suggests.
/// </summary>
public class WorkflowNodeConfigTests
{
    [Fact]
    public void Default_MatchesTheCatalogsDefault()
    {
        Assert.Equal(WorkflowNodeProperties.DefaultVisibleProperties, WorkflowNodeConfig.Default.VisibleProperties);
    }

    [Fact]
    public void Toggle_AddsAnAbsentKey()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var result = config.Toggle("Current");

        Assert.Equal(new[] { "Voltage", "Current" }, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_RemovesAPresentKey()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage", "Current" });

        var result = config.Toggle("Voltage");

        Assert.Equal(new[] { "Current" }, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_RefusesToAddPastTheCap()
    {
        var atCap = new WorkflowNodeConfig(
            Enumerable.Range(0, WorkflowNodeProperties.MaxVisible).Select(i => $"k{i}").ToList());

        var result = atCap.Toggle("one-more");

        Assert.Equal(atCap.VisibleProperties, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_StillAllowsRemovingWhenAtTheCap()
    {
        var atCap = new WorkflowNodeConfig(
            Enumerable.Range(0, WorkflowNodeProperties.MaxVisible).Select(i => $"k{i}").ToList());

        var result = atCap.Toggle("k0");

        Assert.Equal(WorkflowNodeProperties.MaxVisible - 1, result.VisibleProperties.Count);
        Assert.DoesNotContain("k0", result.VisibleProperties);
    }

    [Fact]
    public void Toggle_TogglingTheSameKeyTwiceIsANoOp()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var result = config.Toggle("Current").Toggle("Current");

        Assert.Equal(config.VisibleProperties, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_PreservesOrder_NewestLast()
    {
        var config = WorkflowNodeConfig.Default.Toggle("CycleNumber");

        Assert.Equal("CycleNumber", config.VisibleProperties[^1]);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowNodeConfigTests"
```

Expected: FAIL — `WorkflowNodeConfig` does not exist.

- [ ] **Step 3: Implement the model**

Create `Models/DTOs/Workflow/WorkflowNodeConfig.cs`:

```csharp
using BatteryTestingSystem.Services.Implementations.Workflow;

namespace BatteryTestingSystem.Models.DTOs.Workflow;

/// <summary>
/// One user's choice of which properties a channel node's face shows (spec D11). Persisted via
/// ServerSessionStorageService under "{UserName}_canvas_node_config" - a separate key from the
/// dashboard's own "{UserName}_card_config", since the two surfaces cap and default differently.
/// </summary>
public record WorkflowNodeConfig(IReadOnlyList<string> VisibleProperties)
{
    public static WorkflowNodeConfig Default { get; } =
        new(WorkflowNodeProperties.DefaultVisibleProperties);

    /// <summary>Adds an absent key (unless already at the cap), removes a present one. The single
    /// operation the picker UI drives - keeping it here rather than in the picker component makes
    /// the 6-cap a guaranteed invariant, not something a checkbox's disabled state merely
    /// suggests.</summary>
    public WorkflowNodeConfig Toggle(string key)
    {
        if (VisibleProperties.Contains(key))
            return this with { VisibleProperties = VisibleProperties.Where(k => k != key).ToList() };

        if (VisibleProperties.Count >= WorkflowNodeProperties.MaxVisible)
            return this;

        return this with { VisibleProperties = VisibleProperties.Append(key).ToList() };
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowNodeConfigTests"
```

Expected: PASS (7 tests).

- [ ] **Step 5: Run the full suite and commit**

```bash
dotnet test BatteryTestingSystem.Tests
git add Models/DTOs/Workflow/WorkflowNodeConfig.cs \
  BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs
git commit -m "feat(workflow-canvas): WorkflowNodeConfig with a testable 6-property toggle rule

One Toggle method the picker UI (next task) will drive: adds an absent
key unless at the 6-cap, removes a present one. Extracted as pure logic
rather than embedded in the picker component, matching this repo's
established pattern (CircuitSelectionLogic) - the cap becomes a
guaranteed invariant instead of something a checkbox's disabled state
merely suggests.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Bridge produces per-key formatted values

Before the node face can render a configurable list of properties, the bridge needs to expose every available key's value as a pre-formatted string — matching the established pattern that JS is a dumb text-setter and all formatting happens in C# (exactly how `TableProgress`/`RunningTime`/`StepRunningTime` already work in `TelemetryEntry`).

**Files:**
- Modify: `Models/DTOs/Workflow/TelemetryEntry.cs`
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Modify: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `ChannelTelemetry` (existing, unchanged), `WorkflowNodeProperties.AvailableKeys` (Task 4).
- Produces: `TelemetryEntry.Properties: IReadOnlyDictionary<string,string>`; `WorkflowTelemetryBridge.BuildNodeProperties(ChannelTelemetry) → IReadOnlyDictionary<string,string>` (pure, independently testable).

- [ ] **Step 1: Write the failing test**

Append to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`, inside the class:

```csharp
    // ============================================================ BuildNodeProperties

    [Fact]
    public void BuildNodeProperties_ReturnsEveryAvailableKey()
    {
        var reading = new ChannelTelemetry(CircuitStatus.Charge, 0, 0, 0);

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal(BatteryTestingSystem.Services.Implementations.Workflow.WorkflowNodeProperties.AvailableKeys.OrderBy(k => k),
            properties.Keys.OrderBy(k => k));
    }

    [Fact]
    public void BuildNodeProperties_FormatsEachKeyWithItsUnit()
    {
        var reading = new ChannelTelemetry(
            CircuitStatus.Charge, 0, Voltage: 3.7, Current: 12.456,
            Power: 45.678, Temperature: 28.44,
            AccumulatedCapacity: 1.2345, ChargeCapacity: 2.3456,
            AccumulatedEnergy: 3.4567, CycleNumber: 3,
            TableStepNumber: 4, TableTotalRowNumber: 12);

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal("3.70 V", properties["Voltage"]);
        Assert.Equal("12.46 A", properties["Current"]);
        Assert.Equal("45.68 W", properties["Power"]);
        Assert.Equal("28.4 °C", properties["Temperature"]);
        Assert.Equal("1.235 Ah", properties["AccumulatedCapacity"]);
        Assert.Equal("2.346 Ah", properties["ChargeCapacity"]);
        Assert.Equal("3.457 Wh", properties["AccumulatedEnergy"]);
        Assert.Equal("3", properties["CycleNumber"]);
        Assert.Equal("4", properties["TableStepNumber"]);
        Assert.Equal("12", properties["TableTotalRowNumber"]);
    }

    [Fact]
    public void BuildNodeProperties_FormatsRunningTimesTheSameWayTheDockDoes()
    {
        var reading = new ChannelTelemetry(
            CircuitStatus.Charge, 0, 0, 0,
            RunningTime: TimeSpan.FromSeconds(3725),
            StepRunningTime: TimeSpan.FromSeconds(65));

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal("01:02:05", properties["RunningTime"]);
        Assert.Equal("00:01:05", properties["StepRunningTime"]);
    }

    [Fact]
    public void BuildEntries_CarriesTheNodePropertiesDictionary()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 3.7, 1),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("3.70 V", entry.Properties["Voltage"]);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~BuildNodeProperties|FullyQualifiedName~CarriesTheNodePropertiesDictionary"
```

Expected: FAIL — `BuildNodeProperties` and `TelemetryEntry.Properties` do not exist.

- [ ] **Step 3: Extend TelemetryEntry**

In `Models/DTOs/Workflow/TelemetryEntry.cs`, append one field to `TelemetryEntry` (after `LastUpdateText`):

```csharp
    string LastUpdateText,
    IReadOnlyDictionary<string, string> Properties);
```

- [ ] **Step 4: Implement BuildNodeProperties and wire it into BuildEntries**

In `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, add near `FormatDuration`:

```csharp
    /// <summary>
    /// Every canvas-available property (WorkflowNodeProperties.AvailableKeys), pre-formatted with
    /// its unit — matching the established rule that JS is a dumb text-setter and all formatting
    /// happens here, the same way TableProgress/RunningTime/StepRunningTime already work.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildNodeProperties(ChannelTelemetry reading) =>
        new Dictionary<string, string>
        {
            ["Voltage"] = $"{reading.Voltage:F2} V",
            ["Current"] = $"{reading.Current:F2} A",
            ["Power"] = $"{reading.Power:F2} W",
            ["Temperature"] = $"{reading.Temperature:F1} °C",
            ["AccumulatedCapacity"] = $"{reading.AccumulatedCapacity:F3} Ah",
            ["ChargeCapacity"] = $"{reading.ChargeCapacity:F3} Ah",
            ["DischargeCapacity"] = $"{reading.DischargeCapacity:F3} Ah",
            ["StepCapacity"] = $"{reading.StepCapacity:F3} Ah",
            ["AccumulatedEnergy"] = $"{reading.AccumulatedEnergy:F3} Wh",
            ["ChargeEnergy"] = $"{reading.ChargeEnergy:F3} Wh",
            ["DischargeEnergy"] = $"{reading.DischargeEnergy:F3} Wh",
            ["StepEnergy"] = $"{reading.StepEnergy:F3} Wh",
            ["CycleNumber"] = reading.CycleNumber.ToString(),
            ["TableStepNumber"] = reading.TableStepNumber.ToString(),
            ["TableTotalRowNumber"] = reading.TableTotalRowNumber.ToString(),
            ["StepRunningTime"] = FormatDuration(reading.StepRunningTime),
            ["RunningTime"] = FormatDuration(reading.RunningTime),
        };
```

Then in `BuildEntries`, append to the `entries.Add(new TelemetryEntry(...))` call, after `LastUpdateText:`:

```csharp
                Properties: BuildNodeProperties(reading)));
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: PASS.

- [ ] **Step 6: Run the full suite and commit**

```bash
dotnet test BatteryTestingSystem.Tests
git add Models/DTOs/Workflow/TelemetryEntry.cs \
  Services/Implementations/Workflow/WorkflowTelemetryBridge.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): bridge emits every canvas property pre-formatted

BuildNodeProperties turns a ChannelTelemetry reading into all 17
WorkflowNodeProperties.AvailableKeys as ready-to-write strings, matching
the existing rule that JS is a dumb text-setter (TableProgress/
RunningTime/StepRunningTime already work this way). TelemetryEntry
carries the full dictionary regardless of what any one user has chosen
to display - the node face (next task) picks whichever keys it rendered
slots for for; unrendered keys are harmlessly ignored by setText's
querySelector-returns-null guard.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Configurable node face

Replaces `ChannelNode.razor`'s four fixed Voltage/Current/Power/Temperature slots with a loop over `SelectedProperties`, and `workflow-canvas.js`'s four hardcoded `setText` calls with a generic loop over `e.properties`. Status/SoC and Program-status/Last-update stay fixed — the dashboard's own footer isn't gated by its property picker either, and the catalog (Task 4) doesn't even list those as pickable keys.

**Default behaviour must be pixel-identical to what already shipped**: `WorkflowNodeProperties.DefaultVisibleProperties` is `Voltage, Current, Power, Temperature`, rendered two-per-row exactly as before, so a user who never opens the picker sees no change.

**Files:**
- Modify: `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`
- Modify: `Components/UI/WorkflowCanvas/CanvasSurface.razor`
- Modify: `wwwroot/js/workflow-canvas.js`
- Create: `BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs`

**Interfaces:**
- Consumes: `WorkflowNodeConfig.Default.VisibleProperties` (Task 5) as `ChannelNode`'s default parameter value.
- Produces: `ChannelNode.SelectedProperties: IReadOnlyList<string>` parameter; `CanvasSurface.SelectedProperties: IReadOnlyList<string>` parameter, threaded through.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The node face's measurement rows are driven entirely by SelectedProperties (spec D11) - status/
/// SoC and program-status/last-update stay fixed, matching how the dashboard's own footer isn't
/// gated by its property picker either.
/// </summary>
public class ChannelNodeTests : TestContext
{
    private static WorkflowNode Node() => new("chn-1", NodeKind.Channel, 1, 0, 0, null);

    [Fact]
    public void DefaultSelectionRendersExactlyFourPropertySlots()
    {
        var cut = RenderComponent<ChannelNode>(p => p.Add(x => x.Node, Node()));

        Assert.Equal(4, cut.FindAll("[data-role^='prop-']").Count);
    }

    [Fact]
    public void RendersASlotForEachSelectedPropertyByKey()
    {
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.SelectedProperties, new List<string> { "AccumulatedCapacity", "CycleNumber" }));

        Assert.NotNull(cut.Find("[data-role='prop-AccumulatedCapacity']"));
        Assert.NotNull(cut.Find("[data-role='prop-CycleNumber']"));
        Assert.Equal(2, cut.FindAll("[data-role^='prop-']").Count);
    }

    [Fact]
    public void RendersNoPropertyRowsWhenTheSelectionIsEmpty()
    {
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.SelectedProperties, new List<string>()));

        Assert.Empty(cut.FindAll("[data-role^='prop-']"));
    }

    [Fact]
    public void StatusAndSocRolesAreAlwaysPresentRegardlessOfSelection()
    {
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.SelectedProperties, new List<string>()));

        Assert.NotNull(cut.Find("[data-role='status']"));
        Assert.NotNull(cut.Find("[data-role='soc']"));
        Assert.NotNull(cut.Find("[data-role='program-status']"));
        Assert.NotNull(cut.Find("[data-role='last-update']"));
    }

    [Fact]
    public void PropertySlotsStartAsPlaceholders()
    {
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.SelectedProperties, new List<string> { "Voltage" }));

        Assert.Equal("--", cut.Find("[data-role='prop-Voltage']").TextContent);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~ChannelNodeTests"
```

Expected: FAIL — `SelectedProperties` parameter does not exist; the fixed `data-role="voltage"` etc. spans don't match `[data-role^='prop-']`.

- [ ] **Step 3: Rewrite ChannelNode.razor**

Replace `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` in full:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Services.Implementations.Workflow

@* Status/SoC and program-status/last-update are fixed - the dashboard's own card footer isn't
   gated by its property picker either, and WorkflowNodeProperties doesn't even list these as
   pickable keys. Everything between them is driven by SelectedProperties (spec D11): each key
   gets a data-role="prop-{key}" slot, written by workflow-canvas.js's generic property loop,
   never through a Blazor re-render. Two keys per row, matching the fixed layout this replaces. *@

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale">
    <div class="flex items-center justify-between">
        <span class="wf-status-pill" data-role="status">@InitialStatusText</span>
        <span class="opacity-60" data-role="soc">--%</span>
    </div>
    @for (var i = 0; i < SelectedProperties.Count; i += 2)
    {
        var first = SelectedProperties[i];
        var second = i + 1 < SelectedProperties.Count ? SelectedProperties[i + 1] : null;
        <div class="flex items-center justify-between opacity-80 text-xs">
            <span data-role="prop-@first">--</span>
            @if (second is not null)
            {
                <span data-role="prop-@second">--</span>
            }
        </div>
    }
    <div class="flex items-center justify-between opacity-60 text-xs">
        <span data-role="program-status">--</span>
        <span data-role="last-update">--</span>
    </div>
    <div class="flex items-center gap-1 flex-wrap">
        @if (ProgramName is not null)
        {
            <span class="wf-badge" title="Attached program">@ProgramName</span>
        }
        @if (DbcName is not null)
        {
            <span class="wf-badge" title="Attached DBC file">@DbcName</span>
        }
    </div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Channel";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public string InitialStatusText { get; set; } = "offline";
    [Parameter] public string? ProgramName { get; set; }
    [Parameter] public string? DbcName { get; set; }
    [Parameter] public IReadOnlyList<string> SelectedProperties { get; set; } =
        WorkflowNodeProperties.DefaultVisibleProperties;
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~ChannelNodeTests"
```

Expected: PASS (5 tests).

- [ ] **Step 5: Thread SelectedProperties through CanvasSurface**

In `Components/UI/WorkflowCanvas/CanvasSurface.razor`, add a parameter (near `ProgramLabel`/`DbcLabel`):

```csharp
    [Parameter] public IReadOnlyList<string> SelectedProperties { get; set; } =
        BatteryTestingSystem.Services.Implementations.Workflow.WorkflowNodeProperties.DefaultVisibleProperties;
```

And pass it in the `NodeKind.Channel` case:

```razor
            case NodeKind.Channel:
                <ChannelNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                             IsSelected="selected" IsStale="stale"
                             ProgramName="@(node.Attach?.ProgramId is { } p ? ProgramLabel(p) : null)"
                             DbcName="@(node.Attach?.DbcFileId is { } d ? DbcLabel(d) : null)"
                             SelectedProperties="SelectedProperties" />
                break;
```

- [ ] **Step 6: Replace the four hardcoded JS writes with a generic loop**

In `wwwroot/js/workflow-canvas.js`, inside `applyTelemetry`'s per-entry loop, remove these four lines:

```javascript
                setText(node, "power", e.power.toFixed(2) + " W");
                setText(node, "temperature", e.temperature.toFixed(1) + " °C");
```

(The `"voltage"`/`"current"` lines were already removed when those roles were renamed — confirm none of the four `setText(node, "voltage"|"current"|"power"|"temperature", ...)` calls remain.) Replace them with:

```javascript
                if (e.properties) {
                    for (const key in e.properties) {
                        setText(node, "prop-" + key, e.properties[key]);
                    }
                }
```

- [ ] **Step 7: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 8: Verify in the browser — this must look unchanged by default**

Stop the app, rebuild, restart, hard-reload. Place a registered channel exactly as in prior sessions. Confirm the node face still shows Voltage/Current on one row and Power/Temperature on the next — pixel-equivalent to before this task, since `WorkflowNodeConfig.Default` is not yet wired to persistence (Task 8) and `CanvasSurface`'s parameter defaults to the same four keys.

- [ ] **Step 9: Commit**

```bash
git add Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor \
  Components/UI/WorkflowCanvas/CanvasSurface.razor \
  wwwroot/js/workflow-canvas.js \
  BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs
git commit -m "feat(workflow-canvas): channel node face renders a configurable property list

ChannelNode's four fixed Voltage/Current/Power/Temperature slots become
a loop over SelectedProperties, two per row; workflow-canvas.js's four
hardcoded setText calls become a generic loop over the properties
dictionary from the prior commit. Status/SoC and program-status/last-
update stay fixed, matching how the dashboard's own card footer isn't
gated by its property picker either.

Default is WorkflowNodeProperties.DefaultVisibleProperties (Voltage,
Current, Power, Temperature) - identical to what already shipped, so
nothing changes for an operator until the picker (next task) is used.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: The property picker and persistence

The operator-facing piece: a small floating panel toggled from the toolbar, listing all 17 available properties with checkboxes, enforcing the 6-cap via `WorkflowNodeConfig.Toggle`, persisted per-user via the same `ServerSessionStorageService` mechanism the dashboard's card config already uses (confirmed: `SetComponentState`/`GetComponentState<T>`, already a singleton in DI, no new registration needed).

**Files:**
- Create: `Components/UI/WorkflowCanvas/NodePropertyPicker.razor`
- Create: `BatteryTestingSystem.Tests/Components/NodePropertyPickerTests.cs`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Modify: `wwwroot/css/workflow-canvas.css`

**Interfaces:**
- Consumes: `WorkflowNodeConfig` (Task 5), `WorkflowNodeProperties.AvailableKeys`/`.Label`/`.MaxVisible` (Task 4), `ServerSessionStorageService.SetComponentState`/`GetComponentState<T>` (existing, singleton).
- Produces: `NodePropertyPicker` component with `[Parameter] WorkflowNodeConfig Config`, `[Parameter] EventCallback<WorkflowNodeConfig> ConfigChanged` (standard Blazor two-way-bindable pattern).

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Components/NodePropertyPickerTests.cs`:

```csharp
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The picker is a thin UI over WorkflowNodeConfig.Toggle - these tests exercise the wiring
/// (checkbox click -> ConfigChanged fires with the toggled config), not the cap rule itself
/// (already covered by WorkflowNodeConfigTests).
/// </summary>
public class NodePropertyPickerTests : TestContext
{
    [Fact]
    public void RendersOneCheckboxPerAvailableProperty()
    {
        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, WorkflowNodeConfig.Default));

        Assert.Equal(WorkflowNodeProperties.AvailableKeys.Count, cut.FindAll("input[type=checkbox]").Count);
    }

    [Fact]
    public void ChecksExactlyTheConfiguredProperties()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, config));

        var voltageCheckbox = cut.Find($"input[data-key='Voltage']");
        var currentCheckbox = cut.Find($"input[data-key='Current']");

        Assert.True(voltageCheckbox.HasAttribute("checked"));
        Assert.False(currentCheckbox.HasAttribute("checked"));
    }

    [Fact]
    public void CheckingABoxRaisesConfigChangedWithTheToggledConfig()
    {
        WorkflowNodeConfig? captured = null;
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var cut = RenderComponent<NodePropertyPicker>(p => p
            .Add(x => x.Config, config)
            .Add(x => x.ConfigChanged, (WorkflowNodeConfig c) => captured = c));

        cut.Find("input[data-key='Current']").Change(true);

        Assert.Equal(new[] { "Voltage", "Current" }, captured!.VisibleProperties);
    }

    [Fact]
    public void UncheckingABoxRemovesThatPropertyOnly()
    {
        WorkflowNodeConfig? captured = null;
        var config = new WorkflowNodeConfig(new[] { "Voltage", "Current" });

        var cut = RenderComponent<NodePropertyPicker>(p => p
            .Add(x => x.Config, config)
            .Add(x => x.ConfigChanged, (WorkflowNodeConfig c) => captured = c));

        cut.Find("input[data-key='Voltage']").Change(false);

        Assert.Equal(new[] { "Current" }, captured!.VisibleProperties);
    }

    [Fact]
    public void DisablesEveryUncheckedBoxOnceAtTheCap()
    {
        var atCap = new WorkflowNodeConfig(WorkflowNodeProperties.AvailableKeys.Take(WorkflowNodeProperties.MaxVisible).ToList());

        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, atCap));

        var uncheckedBoxes = cut.FindAll("input[type=checkbox]")
            .Where(el => !el.HasAttribute("checked"));

        Assert.All(uncheckedBoxes, el => Assert.True(el.HasAttribute("disabled")));
    }

    [Fact]
    public void ShowsTheSharedCatalogsLabelNotTheRawKey()
    {
        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, WorkflowNodeConfig.Default));

        Assert.Contains(WorkflowNodeProperties.Label("Voltage"), cut.Markup);
    }
}
```

Note: `System.Linq` is required for `.Take`/`.Where`/`.All` — add `using System.Linq;` at the top of the test file alongside the others.

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~NodePropertyPickerTests"
```

Expected: FAIL — `NodePropertyPicker` does not exist.

- [ ] **Step 3: Implement the picker**

Create `Components/UI/WorkflowCanvas/NodePropertyPicker.razor`:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Services.Implementations.Workflow

@* Every checkbox click goes straight through WorkflowNodeConfig.Toggle, so the 6-cap is
   enforced by that method (already unit-tested), not re-implemented here. Persistence is the
   page's job (WorkflowCanvasPage), not this component's — it only reports the new config up via
   ConfigChanged, matching the standard Blazor two-way-bindable component shape. *@

<div class="wf-property-picker">
    <div class="wf-dock__title">Node properties</div>
    <p class="opacity-60 text-xs">@Config.VisibleProperties.Count / @WorkflowNodeProperties.MaxVisible selected</p>
    <div class="wf-property-picker__list">
        @foreach (var key in WorkflowNodeProperties.AvailableKeys)
        {
            var isChecked = Config.VisibleProperties.Contains(key);
            var atCap = Config.VisibleProperties.Count >= WorkflowNodeProperties.MaxVisible;
            <label class="wf-property-picker__item">
                <input type="checkbox" data-key="@key" checked="@isChecked" disabled="@(!isChecked && atCap)"
                       @onchange="@(() => ConfigChanged.InvokeAsync(Config.Toggle(key)))" />
                <span>@WorkflowNodeProperties.Label(key)</span>
            </label>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public WorkflowNodeConfig Config { get; set; } = WorkflowNodeConfig.Default;
    [Parameter] public EventCallback<WorkflowNodeConfig> ConfigChanged { get; set; }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~NodePropertyPickerTests"
```

Expected: PASS (6 tests).

- [ ] **Step 5: Add panel styling**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- node property picker */

.wf-property-picker {
    position: absolute;
    top: 3rem;
    right: 1rem;
    z-index: 20;
    width: 220px;
    max-height: 70vh;
    overflow-y: auto;
    padding: 0.75rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.5rem;
    background: hsl(var(--card));
    color: hsl(var(--card-foreground));
    box-shadow: 0 4px 16px hsl(0 0% 0% / 0.15);
}

.wf-property-picker__list { display: grid; gap: 0.25rem; margin-top: 0.5rem; }

.wf-property-picker__item {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.8125rem;
    cursor: pointer;
}

.wf-property-picker__item:has(input:disabled) { opacity: 0.4; cursor: not-allowed; }
```

- [ ] **Step 6: Wire persistence and the toolbar toggle into the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, add the injection near the existing ones:

```csharp
@inject ServerSessionStorageService SessionStorage
```

Add fields near `_animations`:

```csharp
    private WorkflowNodeConfig _nodeConfig = WorkflowNodeConfig.Default;
    private bool _propertyPickerOpen;

    private const string NodeConfigKeySuffix = "_canvas_node_config";
```

In `OnInitializedAsync`, after `_userId` is set:

```csharp
        _nodeConfig = SessionStorage.GetComponentState<WorkflowNodeConfig>(_userId + NodeConfigKeySuffix)
            ?? WorkflowNodeConfig.Default;
```

Add a handler near `HandleAttachmentChanged`:

```csharp
    private void HandleNodeConfigChanged(WorkflowNodeConfig config)
    {
        _nodeConfig = config;
        SessionStorage.SetComponentState(_userId + NodeConfigKeySuffix, _nodeConfig, Permanent: true);
    }
```

In the toolbar `<div class="wf-toolbar">`, add a toggle button after the existing **Fit** button:

```razor
        <button class="wf-btn" @onclick="() => _propertyPickerOpen = !_propertyPickerOpen">
            Node fields
        </button>
```

Pass the config into `CanvasSurface` (inside the `else` branch that renders it):

```razor
                    <CanvasSurface Graph="ViewGraph"
                                   StaleNodeIds="_staleNodeIds"
                                   SelectedNodeId="@_selectedNodeId"
                                   LabelFor="LabelFor"
                                   ProgramLabel="ProgramLabel"
                                   DbcLabel="DbcLabel"
                                   ChildCountFor="ChildCountFor"
                                   CollapsedBoardIds="_collapsedBoardIds"
                                   OnToggleBoardCollapse="ToggleBoardCollapse"
                                   SelectedProperties="_nodeConfig.VisibleProperties" />
```

Render the picker conditionally, inside `.wf-canvas-host` so its `position: absolute` anchors correctly:

```razor
            @if (_propertyPickerOpen)
            {
                <NodePropertyPicker Config="_nodeConfig" ConfigChanged="HandleNodeConfigChanged" />
            }
```

- [ ] **Step 7: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 8: Verify in the browser**

Stop the app, rebuild, restart, hard-reload. Click **Node fields**; confirm the panel opens listing 17 properties with Voltage/Current/Power/Temperature pre-checked. Check `AccumulatedCapacity`; confirm a 5th checkbox is now checked and every other unchecked box stays enabled (still under 6). Check two more to reach 6; confirm every remaining unchecked box becomes visibly disabled. Confirm a placed channel node's face now shows the 6 chosen properties, two per row, with correct live values once telemetry pushes. Reload the page entirely (not just the canvas — a full navigation) and reopen the picker; confirm the same 6 are still checked, proving persistence survived past the component's lifetime.

- [ ] **Step 9: Commit**

```bash
git add Components/UI/WorkflowCanvas/NodePropertyPicker.razor \
  Components/Pages/Workflows/WorkflowCanvasPage.razor \
  wwwroot/css/workflow-canvas.css \
  BatteryTestingSystem.Tests/Components/NodePropertyPickerTests.cs
git commit -m "feat(workflow-canvas): node property picker with per-user persistence

A floating panel (toggled from the toolbar's new 'Node fields' button)
listing all 17 WorkflowNodeProperties.AvailableKeys as checkboxes,
driving WorkflowNodeConfig.Toggle directly so the 6-cap is enforced by
already-tested logic rather than re-implemented in the component.
Persisted via ServerSessionStorageService under
'{UserId}_canvas_node_config' - the same mechanism and Permanent:true
flag the dashboard's own card config already uses, under a separate key
since the two surfaces cap and default differently.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Phase 2 Completion Checklist

- [ ] Placing a full 64-channel device renders as a compact 8-row lane, not a ~13,000px column
- [ ] A second device's lane stacks cleanly below the first, no overlap
- [ ] A battery placed after a wide lane renders clear of its channel grid
- [ ] Zooming below 0.4x collapses channel nodes to coloured tiles; `.wf-world[data-lod]` reflects 0/1/2 correctly at each threshold
- [ ] A zoom sweep from 0.2x to 1.2x produces 0 `childList` mutations
- [ ] A never-configured user sees exactly Voltage/Current/Power/Temperature on channel nodes — pixel-identical to before this phase
- [ ] The property picker lists 17 properties, enforces the 6-cap by disabling further checkboxes, and persists across a full page reload
- [ ] Full suite green (`DashboardRenderBatcherTests` flake excepted — confirm in isolation)
- [ ] `main` has no commits from this work

**Deferred to Phase 3:** marquee selection, `ChannelActionExecutor`, Start/Stop/Pause/Continue/Transfer actions, the system-error reset button.
