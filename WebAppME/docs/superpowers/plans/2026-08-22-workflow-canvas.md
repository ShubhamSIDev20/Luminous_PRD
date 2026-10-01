# Workflow Canvas Playground Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an experimental n8n-style pan/zoom canvas where an operator arranges device → secondary board → channel → battery nodes, attaches a program and optional DBC to each channel, saves the arrangement as a named layout, and watches live telemetry animate the graph.

**Architecture:** All decision logic lives in pure, DI-free C# classes (`WorkflowGraphJson`, `WorkflowGraphValidator`, `WorkflowGraphBuilder`, `WorkflowAutoLayout`) that are unit-tested before any UI exists. The UI is Blazor components for nodes and an SVG overlay for edges; pan, zoom and drag run entirely in a new `workflow-canvas.js` and only report committed results back to C#. Live telemetry never triggers a Blazor re-render — it is pushed to the DOM as CSS custom properties through one batched interop call per coalesced tick.

**Tech Stack:** .NET 8, Blazor Server (`InteractiveServer`), EF Core 8 + SQLite, xUnit + bUnit 1.32.7, plain hand-written CSS, vanilla ES module JS.

**Spec:** [docs/superpowers/specs/2026-08-22-workflow-canvas-design.md](../specs/2026-08-22-workflow-canvas-design.md)

---

## Global Constraints

- **Branch:** all work happens on `feat/workflow-canvas-experiment`. **Never merge to `main`** during implementation.
- **The canvas records intent only.** No task may call a transfer, start, stop, or any other hardware command. If a task seems to need one, stop and ask.
- **No new NuGet or npm packages.** Everything uses what `BatteryTestingSystem.csproj` and `BatteryTestingSystem.Tests.csproj` already reference.
- **CSS goes only in `wwwroot/css/workflow-canvas.css`.** Never edit `app.css` or `app.min.css` — they have no build pipeline (ADR-2). Never add a Tailwind utility class to a workflow-canvas component that is not already used elsewhere in this repo; unprecedented utilities silently render as absent.
- **Every colour comes from an existing CSS variable.** No literal hex values. Theme vars are raw HSL triplets, so the form is `hsl(var(--status-charge) / 0.55)`.
- **All animation is compositor-only:** animate `stroke-dashoffset`, `opacity`, and `transform`. Never animate `box-shadow`, `height`, `width`, `top`, or `left`.
- **Pointer-move events never cross to C#.** Only committed results (drop position, completed connection, selection) invoke .NET.
- **`CircuitStatus.Countinue` is misspelled in the source enum** (`Models/Enums/CircuitEnums.cs:54`) while the CSS variable is `--status-continue`. Never derive a CSS name with `status.ToString().ToLower()`; always go through `WorkflowStatusCss.Name(...)` (Task 9).
- **`SchemaVersion` for the layout document is `1`** for this entire plan, declared once as `WorkflowGraph.CurrentVersion`.
- **Test commands:** `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj` runs everything; `--filter FullyQualifiedName~<ClassName>` runs one class. Build with `dotnet build`. The app must not be running during a build — it holds a file lock on the output assembly.
- **Commit after every task**, using the message given in that task's final step.

---

## Deviations from the spec (found while planning — the spec is wrong on these three points)

1. **DI registration is in `Extensions/ServiceCollectionExtensions.cs` (around lines 97-101), not `Program.cs`.** The spec §3.2 names the wrong file.
2. **`Components/App.razor` is a fifth modified shared file.** It hardcodes every `<link rel="stylesheet">` (lines 9-10) and `<script src>` (lines 11-26); the new CSS and JS must be registered there. The spec §3.2 lists only four files.
3. **`Repository<T>.AddAsync` does not call `SaveChangesAsync`** (pinned by an existing test in `RepositoryTests.cs`). `WorkflowLayoutRepository` must call it explicitly or writes are silently lost.

---

## File Structure

**Pure logic — no DI, no DB, no Razor. Written and tested first (Tasks 1-4).**

| File | Responsibility |
|---|---|
| `Models/DTOs/Workflow/WorkflowGraph.cs` | The saved document: `WorkflowGraph`, `WorkflowNode`, `WorkflowEdge`, `NodeAttachment`, `CanvasViewport`, `NodeKind`, `EdgeKind` |
| `Models/DTOs/Workflow/TopologySnapshot.cs` | A DB-free view of real hardware, so the builder and validator never touch EF |
| `Services/Implementations/Workflow/WorkflowGraphJson.cs` | Serialize / deserialize with fixed options and version checking |
| `Services/Implementations/Workflow/WorkflowGraphValidator.cs` | Which nodes may be added and which connections are legal |
| `Services/Implementations/Workflow/WorkflowGraphBuilder.cs` | Auto-import: `TopologyDevice` + chosen channel ids → `WorkflowGraph` |
| `Services/Implementations/Workflow/WorkflowAutoLayout.cs` | Assigns x/y in device → board → channel → battery columns |
| `Services/Implementations/Workflow/WorkflowEdgeGeometry.cs` | Bezier path `d` attribute maths (Task 10) |
| `Services/Implementations/Workflow/WorkflowStatusCss.cs` | `CircuitStatus` → CSS token, handling the `Countinue` typo (Task 9) |

**Persistence (Tasks 5-6).**

| File | Responsibility |
|---|---|
| `Models/Entities/WorkflowLayout.cs` | The single new table |
| `Repositories/Interfaces/IWorkflowLayoutRepository.cs` | Layout CRUD contract |
| `Repositories/Implementations/WorkflowLayoutRepository.cs` | EF implementation over `Repository<T>` |
| `Services/Interfaces/IWorkflowLayoutService.cs` | What the page consumes |
| `Services/Implementations/Workflow/WorkflowLayoutService.cs` | Save/load orchestration, stale-node resolution, version refusal |

**UI (Tasks 7-16).**

| File | Responsibility |
|---|---|
| `Components/Pages/Workflows/WorkflowCanvasPage.razor` | Page shell: toolbar, three-pane split, owns the graph state |
| `Components/UI/WorkflowCanvas/CanvasSurface.razor` | Viewport; hosts nodes and the edge layer |
| `Components/UI/WorkflowCanvas/CanvasNode.razor` | Shared node chrome: position, selection ring, ports |
| `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor` | Device body |
| `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor` | Board body + channel picker |
| `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` | Status pill, readouts, program/DBC badges |
| `Components/UI/WorkflowCanvas/Nodes/BatteryNode.razor` | Battery body |
| `Components/UI/WorkflowCanvas/BatteryGlyph.razor` | Animated pack/cell SVG |
| `Components/UI/WorkflowCanvas/EdgeLayer.razor` | One SVG overlay for every edge |
| `Components/UI/WorkflowCanvas/PalettePanel.razor` | Right rail |
| `Components/UI/WorkflowCanvas/PropertiesDock.razor` | Left dock |
| `Components/UI/WorkflowCanvas/Minimap.razor` | Overview + viewport rectangle |
| `Components/UI/WorkflowCanvas/LayoutSwitcher.razor` | Named layout list, save/rename/delete |
| `wwwroot/js/workflow-canvas.js` | Pan, zoom, snap, node drag, connection drag, telemetry apply |
| `wwwroot/css/workflow-canvas.css` | Every style and keyframe for the canvas |
| `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs` | Filtered, coalesced telemetry → one interop call |

**Modified shared files:** `Components/Layout/MainLayout.razor`, `Components/Layout/Navbar.razor`, `Components/App.razor`, `Extensions/ServiceCollectionExtensions.cs`, `appsettings.Development.json`, `Data/AppDbContext.cs` (one `DbSet`), plus one generated migration.

---

### Task 1: The graph document and its JSON round-trip

**Files:**
- Create: `Models/DTOs/Workflow/WorkflowGraph.cs`
- Create: `Models/DTOs/Workflow/TopologySnapshot.cs`
- Create: `Services/Implementations/Workflow/WorkflowGraphJson.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphJsonTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `WorkflowGraph`, `WorkflowNode`, `WorkflowEdge`, `NodeAttachment`, `CanvasViewport`, `NodeKind`, `EdgeKind`, `TopologySnapshot`, `TopologyDevice`, `TopologyBoard`, `TopologyChannel`, `WorkflowGraph.CurrentVersion` (const int = 1), `WorkflowGraphJson.Serialize(WorkflowGraph) -> string`, `WorkflowGraphJson.TryDeserialize(string?, out WorkflowGraph?, out string?) -> bool`.

- [ ] **Step 1: Write the failing tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphJsonTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The layout document is stored as an opaque JSON blob in one column, so serialization IS the
/// schema. These tests are the only thing standing between a renamed property and every saved
/// layout silently losing data.
/// </summary>
public class WorkflowGraphJsonTests
{
    private static WorkflowGraph Sample() => new(
        Version: WorkflowGraph.CurrentVersion,
        Nodes: new List<WorkflowNode>
        {
            new("n1", NodeKind.Device,  10,   0, 0, null),
            new("n2", NodeKind.Board,   20, 240, 0, null),
            new("n3", NodeKind.Channel, 30, 480, 0, new NodeAttachment(7, 8, null)),
            new("n4", NodeKind.Battery, 40, 720, 0, new NodeAttachment(null, null, 9)),
        },
        Edges: new List<WorkflowEdge>
        {
            new("e1", "n1", "n2", EdgeKind.Topology),
            new("e2", "n4", "n3", EdgeKind.Power),
        },
        Viewport: new CanvasViewport(-100, 50, 0.75));

    [Fact]
    public void RoundTrip_PreservesEveryField()
    {
        var original = Sample();

        Assert.True(WorkflowGraphJson.TryDeserialize(
            WorkflowGraphJson.Serialize(original), out var restored, out var error));

        Assert.Null(error);
        Assert.Equal(original.Version, restored!.Version);
        Assert.Equal(original.Viewport, restored.Viewport);
        Assert.Equal(original.Nodes, restored.Nodes);   // records compare structurally
        Assert.Equal(original.Edges, restored.Edges);
    }

    [Fact]
    public void RoundTrip_KeepsANullAttachmentDistinctFromAPartiallyFilledOne()
    {
        // "no configuration at all" and "a DBC but no program" are different states.
        // Conflating them would silently discard a user's DBC-only setup.
        var graph = Sample() with
        {
            Nodes = new List<WorkflowNode>
            {
                new("a", NodeKind.Channel, 1, 0, 0, null),
                new("b", NodeKind.Channel, 2, 0, 0, new NodeAttachment(null, 5, null)),
            }
        };

        Assert.True(WorkflowGraphJson.TryDeserialize(
            WorkflowGraphJson.Serialize(graph), out var restored, out _));

        Assert.Null(restored!.Nodes[0].Attach);
        Assert.Equal(5, restored.Nodes[1].Attach!.DbcFileId);
        Assert.Null(restored.Nodes[1].Attach!.ProgramId);
    }

    [Fact]
    public void Deserialize_RefusesAMismatchedSchemaVersion_WithoutThrowing()
    {
        var json = WorkflowGraphJson.Serialize(Sample() with { Version = 99 });

        Assert.False(WorkflowGraphJson.TryDeserialize(json, out var restored, out var error));

        Assert.Null(restored);
        Assert.Contains("version", error!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"Nodes\": [ }")]
    public void Deserialize_ReturnsFalseOnGarbage_RatherThanThrowing(string junk)
    {
        // A corrupted blob must degrade to "this layout will not open", never to an exception
        // that takes the whole Blazor circuit down.
        Assert.False(WorkflowGraphJson.TryDeserialize(junk, out var restored, out var error));
        Assert.Null(restored);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void EnumsAreWrittenAsNames_SoAReorderedEnumCannotCorruptSavedLayouts()
    {
        var json = WorkflowGraphJson.Serialize(Sample());

        Assert.Contains("\"Battery\"", json);
        Assert.Contains("\"Power\"", json);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphJsonTests`
Expected: FAIL — compilation errors: `WorkflowGraph` and `WorkflowGraphJson` do not exist.

- [ ] **Step 3: Write the document types**

Create `Models/DTOs/Workflow/WorkflowGraph.cs`:

```csharp
namespace BatteryTestingSystem.Models.DTOs.Workflow;

public enum NodeKind { Device, Board, Channel, Battery }

public enum EdgeKind { Topology, Power }

/// <summary>
/// What a channel node has configured on it. Program and DBC are properties rather than nodes
/// (spec D5) so a 64-channel layout does not become hundreds of crossing edges.
/// </summary>
public record NodeAttachment(int? ProgramId, int? DbcFileId, int? BatteryTypeId);

public record CanvasViewport(double PanX, double PanY, double Zoom)
{
    public static CanvasViewport Default => new(0, 0, 1);
}

/// <summary>
/// EntityId is a SOFT reference to the real Device / SecondaryBoard / Channel / BatteryType row.
/// There is deliberately no foreign key (spec 4.3): deleting hardware must never cascade into,
/// or fail because of, a saved layout. Unresolvable ids become dimmed "stale" nodes on load.
/// </summary>
public record WorkflowNode(
    string Id,
    NodeKind Kind,
    long? EntityId,
    double X,
    double Y,
    NodeAttachment? Attach);

public record WorkflowEdge(string Id, string FromNodeId, string ToNodeId, EdgeKind Kind);

public record WorkflowGraph(
    int Version,
    List<WorkflowNode> Nodes,
    List<WorkflowEdge> Edges,
    CanvasViewport Viewport)
{
    /// <summary>Bump whenever the node or edge shape changes. Old blobs are then refused with a
    /// clear message instead of deserializing into garbage.</summary>
    public const int CurrentVersion = 1;

    public static WorkflowGraph Empty => new(CurrentVersion, new(), new(), CanvasViewport.Default);
}
```

Create `Models/DTOs/Workflow/TopologySnapshot.cs`:

```csharp
namespace BatteryTestingSystem.Models.DTOs.Workflow;

/// <summary>
/// A DB-free picture of real hardware. The builder and validator take this rather than an
/// AppDbContext, which is what keeps them pure and unit-testable — the same reason
/// CircuitSelectionLogic was extracted from DashboardView in T-38.
/// </summary>
public record TopologyChannel(long ChannelId, int ChannelNumber);

public record TopologyBoard(long BoardId, int BoardNumber, IReadOnlyList<TopologyChannel> Channels);

public record TopologyDevice(int DeviceId, string DeviceName, IReadOnlyList<TopologyBoard> Boards);

public record TopologySnapshot(IReadOnlyList<TopologyDevice> Devices)
{
    public static TopologySnapshot Empty => new(Array.Empty<TopologyDevice>());

    public TopologyDevice? FindDeviceOwningBoard(long boardId) =>
        Devices.FirstOrDefault(d => d.Boards.Any(b => b.BoardId == boardId));

    public TopologyDevice? FindDeviceOwningChannel(long channelId) =>
        Devices.FirstOrDefault(d => d.Boards.Any(b => b.Channels.Any(c => c.ChannelId == channelId)));

    public bool HasDevice(int deviceId) => Devices.Any(d => d.DeviceId == deviceId);

    public bool HasBoard(long boardId) => FindDeviceOwningBoard(boardId) is not null;

    public bool HasChannel(long channelId) => FindDeviceOwningChannel(channelId) is not null;
}
```

- [ ] **Step 4: Write the serializer**

Create `Services/Implementations/Workflow/WorkflowGraphJson.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// The one place that knows how a WorkflowGraph becomes text. Enums are written as names so a
/// future reordering of NodeKind cannot silently reinterpret every saved layout.
/// </summary>
public static class WorkflowGraphJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,                        // keep C# casing, stable on disk
        DefaultIgnoreCondition = JsonIgnoreCondition.Never, // null is meaningful, always write it
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string Serialize(WorkflowGraph graph) =>
        JsonSerializer.Serialize(graph, Options);

    public static bool TryDeserialize(string? json, out WorkflowGraph? graph, out string? error)
    {
        graph = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The saved layout is empty.";
            return false;
        }

        WorkflowGraph? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WorkflowGraph>(json, Options);
        }
        catch (JsonException ex)
        {
            error = $"The saved layout could not be read: {ex.Message}";
            return false;
        }

        if (parsed is null)
        {
            error = "The saved layout could not be read.";
            return false;
        }

        if (parsed.Version != WorkflowGraph.CurrentVersion)
        {
            error = $"This layout was saved by a different version of the canvas "
                  + $"(schema version {parsed.Version}, this build expects "
                  + $"{WorkflowGraph.CurrentVersion}). It cannot be opened.";
            return false;
        }

        graph = parsed;
        return true;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphJsonTests`
Expected: PASS — 8 test cases (4 facts + 4 theory rows).

- [ ] **Step 6: Commit**

```bash
git add Models/DTOs/Workflow/ Services/Implementations/Workflow/WorkflowGraphJson.cs BatteryTestingSystem.Tests/Services/Workflow/
git commit -m "feat(workflow-canvas): graph document types and versioned JSON round-trip"
```

---

### Task 2: The graph validator

Every rule about what may be placed and what may be wired lives here, so the UI never has to
decide. Invalid gestures are refused with a message the page shows as a toast.

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowGraphValidator.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphValidatorTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowNode`, `WorkflowEdge`, `NodeKind`, `EdgeKind`, `TopologySnapshot` (Task 1).
- Produces: `GraphRuleResult` (record with `bool IsValid`, `string? Error`, static `Ok`, static `Fail(string)`), `WorkflowGraphValidator.CanAddNode(WorkflowGraph, WorkflowNode) -> GraphRuleResult`, `WorkflowGraphValidator.CanConnect(WorkflowGraph, string fromNodeId, string toNodeId, TopologySnapshot) -> GraphRuleResult`, `WorkflowGraphValidator.CanAttach(WorkflowGraph, string nodeId) -> GraphRuleResult`.

- [ ] **Step 1: Write the failing tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphValidatorTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Spec 5.6. These rules are the only thing preventing a layout that claims hardware which does
/// not exist — a board of device A feeding a channel of device B, two batteries on one channel,
/// or the same channel placed twice and therefore counted twice by the telemetry bridge.
/// </summary>
public class WorkflowGraphValidatorTests
{
    // Device 1: board 10 with channels 100, 101.  Device 2: board 20 with channel 200.
    private static TopologySnapshot Topology() => new(new[]
    {
        new TopologyDevice(1, "Dev-1", new[]
        {
            new TopologyBoard(10, 1, new[] { new TopologyChannel(100, 1), new TopologyChannel(101, 2) }),
        }),
        new TopologyDevice(2, "Dev-2", new[]
        {
            new TopologyBoard(20, 1, new[] { new TopologyChannel(200, 1) }),
        }),
    });

    private static WorkflowGraph GraphWith(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    private static WorkflowNode Node(string id, NodeKind kind, long? entityId) =>
        new(id, kind, entityId, 0, 0, null);

    // ================================================================ CanAddNode

    [Fact]
    public void CanAddNode_AllowsAChannelThatIsNotYetOnTheCanvas()
    {
        var result = WorkflowGraphValidator.CanAddNode(
            GraphWith(), Node("c1", NodeKind.Channel, 100));

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void CanAddNode_RejectsTheSameChannelTwice()
    {
        // Two nodes for one channel would double-count it in the telemetry id set and let the
        // user attach two conflicting programs to the same physical hardware.
        var graph = GraphWith(Node("c1", NodeKind.Channel, 100));

        var result = WorkflowGraphValidator.CanAddNode(graph, Node("c2", NodeKind.Channel, 100));

        Assert.False(result.IsValid);
        Assert.Contains("already on the canvas", result.Error!);
    }

    [Fact]
    public void CanAddNode_RejectsTheSameDeviceOrBoardTwice()
    {
        var graph = GraphWith(
            Node("d1", NodeKind.Device, 1),
            Node("b1", NodeKind.Board, 10));

        Assert.False(WorkflowGraphValidator.CanAddNode(graph, Node("d2", NodeKind.Device, 1)).IsValid);
        Assert.False(WorkflowGraphValidator.CanAddNode(graph, Node("b2", NodeKind.Board, 10)).IsValid);
    }

    [Fact]
    public void CanAddNode_AllowsManyBatteryNodes_BecauseTheyAreNotUniqueHardware()
    {
        // A battery node is a placeholder for "a battery of this type", not a serial-numbered
        // unit, so the same BatteryTypeId may legitimately appear on many channels.
        var graph = GraphWith(Node("bat1", NodeKind.Battery, 9));

        Assert.True(WorkflowGraphValidator.CanAddNode(graph, Node("bat2", NodeKind.Battery, 9)).IsValid);
    }

    [Fact]
    public void CanAddNode_RejectsADuplicateNodeId()
    {
        var graph = GraphWith(Node("c1", NodeKind.Channel, 100));

        var result = WorkflowGraphValidator.CanAddNode(graph, Node("c1", NodeKind.Channel, 101));

        Assert.False(result.IsValid);
        Assert.Contains("id", result.Error!, System.StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================ CanConnect

    [Fact]
    public void CanConnect_AllowsDeviceToItsOwnBoard()
    {
        var graph = GraphWith(Node("d1", NodeKind.Device, 1), Node("b1", NodeKind.Board, 10));

        Assert.True(WorkflowGraphValidator.CanConnect(graph, "d1", "b1", Topology()).IsValid);
    }

    [Fact]
    public void CanConnect_AllowsBoardToItsOwnChannel()
    {
        var graph = GraphWith(Node("b1", NodeKind.Board, 10), Node("c1", NodeKind.Channel, 100));

        Assert.True(WorkflowGraphValidator.CanConnect(graph, "b1", "c1", Topology()).IsValid);
    }

    [Fact]
    public void CanConnect_RejectsABoardWiredToAnotherDevicesChannel()
    {
        // The headline rule. Board 10 belongs to device 1; channel 200 belongs to device 2.
        var graph = GraphWith(Node("b1", NodeKind.Board, 10), Node("c200", NodeKind.Channel, 200));

        var result = WorkflowGraphValidator.CanConnect(graph, "b1", "c200", Topology());

        Assert.False(result.IsValid);
        Assert.Contains("different device", result.Error!);
    }

    [Fact]
    public void CanConnect_RejectsADeviceWiredStraightToAChannel_SkippingTheBoard()
    {
        var graph = GraphWith(Node("d1", NodeKind.Device, 1), Node("c1", NodeKind.Channel, 100));

        var result = WorkflowGraphValidator.CanConnect(graph, "d1", "c1", Topology());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CanConnect_AllowsOneBatteryPerChannel()
    {
        var graph = GraphWith(Node("bat1", NodeKind.Battery, 9), Node("c1", NodeKind.Channel, 100));

        Assert.True(WorkflowGraphValidator.CanConnect(graph, "bat1", "c1", Topology()).IsValid);
    }

    [Fact]
    public void CanConnect_RejectsASecondBatteryOnTheSameChannel()
    {
        var graph = GraphWith(
            Node("bat1", NodeKind.Battery, 9),
            Node("bat2", NodeKind.Battery, 9),
            Node("c1", NodeKind.Channel, 100)) with
        {
            Edges = new List<WorkflowEdge> { new("e1", "bat1", "c1", EdgeKind.Power) }
        };

        var result = WorkflowGraphValidator.CanConnect(graph, "bat2", "c1", Topology());

        Assert.False(result.IsValid);
        Assert.Contains("already has a battery", result.Error!);
    }

    [Fact]
    public void CanConnect_RejectsADuplicateOfAnExistingEdge()
    {
        var graph = GraphWith(Node("d1", NodeKind.Device, 1), Node("b1", NodeKind.Board, 10)) with
        {
            Edges = new List<WorkflowEdge> { new("e1", "d1", "b1", EdgeKind.Topology) }
        };

        var result = WorkflowGraphValidator.CanConnect(graph, "d1", "b1", Topology());

        Assert.False(result.IsValid);
        Assert.Contains("already connected", result.Error!);
    }

    [Fact]
    public void CanConnect_RejectsANodeConnectedToItself()
    {
        var graph = GraphWith(Node("b1", NodeKind.Board, 10));

        Assert.False(WorkflowGraphValidator.CanConnect(graph, "b1", "b1", Topology()).IsValid);
    }

    [Fact]
    public void CanConnect_RejectsAnUnknownNodeId_RatherThanThrowing()
    {
        // JS supplies these ids. A stale id after a delete must be a refusal, not a crash.
        var graph = GraphWith(Node("b1", NodeKind.Board, 10));

        var result = WorkflowGraphValidator.CanConnect(graph, "b1", "ghost", Topology());

        Assert.False(result.IsValid);
        Assert.Contains("no longer on the canvas", result.Error!);
    }

    [Fact]
    public void CanConnect_RejectsAChannelWiredToAChannel()
    {
        var graph = GraphWith(Node("c1", NodeKind.Channel, 100), Node("c2", NodeKind.Channel, 101));

        Assert.False(WorkflowGraphValidator.CanConnect(graph, "c1", "c2", Topology()).IsValid);
    }

    // ================================================================ CanAttach

    [Fact]
    public void CanAttach_AllowsAChannelNode()
    {
        var graph = GraphWith(Node("c1", NodeKind.Channel, 100));

        Assert.True(WorkflowGraphValidator.CanAttach(graph, "c1").IsValid);
    }

    [Theory]
    [InlineData(NodeKind.Device)]
    [InlineData(NodeKind.Board)]
    [InlineData(NodeKind.Battery)]
    public void CanAttach_RejectsEveryNonChannelNode(NodeKind kind)
    {
        // Programs and DBC files are channel configuration. Attaching one to a device node would
        // be meaningless but silently storable, so it is refused at the boundary.
        var graph = GraphWith(Node("n1", kind, 1));

        var result = WorkflowGraphValidator.CanAttach(graph, "n1");

        Assert.False(result.IsValid);
        Assert.Contains("channel", result.Error!, System.StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphValidatorTests`
Expected: FAIL — `WorkflowGraphValidator` and `GraphRuleResult` do not exist.

- [ ] **Step 3: Write the validator**

Create `Services/Implementations/Workflow/WorkflowGraphValidator.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record GraphRuleResult(bool IsValid, string? Error)
{
    public static GraphRuleResult Ok { get; } = new(true, null);

    public static GraphRuleResult Fail(string error) => new(false, error);
}

/// <summary>
/// Every rule about what may be placed on the canvas and what may be wired to what. Pure and
/// DI-free so it can be exhaustively unit-tested; the page only reports the message it returns.
/// </summary>
public static class WorkflowGraphValidator
{
    public static GraphRuleResult CanAddNode(WorkflowGraph graph, WorkflowNode node)
    {
        if (graph.Nodes.Any(n => n.Id == node.Id))
            return GraphRuleResult.Fail($"A node with id '{node.Id}' is already on the canvas.");

        // Battery nodes stand for "a battery of this type", not a serial-numbered unit, so the
        // same BatteryTypeId may legitimately appear many times. Real hardware may not.
        if (node.Kind == NodeKind.Battery || node.EntityId is null)
            return GraphRuleResult.Ok;

        var duplicate = graph.Nodes.Any(n => n.Kind == node.Kind && n.EntityId == node.EntityId);

        return duplicate
            ? GraphRuleResult.Fail($"That {node.Kind.ToString().ToLowerInvariant()} is already on the canvas.")
            : GraphRuleResult.Ok;
    }

    public static GraphRuleResult CanConnect(
        WorkflowGraph graph, string fromNodeId, string toNodeId, TopologySnapshot topology)
    {
        if (fromNodeId == toNodeId)
            return GraphRuleResult.Fail("A node cannot be connected to itself.");

        var from = graph.Nodes.FirstOrDefault(n => n.Id == fromNodeId);
        var to = graph.Nodes.FirstOrDefault(n => n.Id == toNodeId);

        // JS supplies these ids; one can go stale between a delete and the drop landing.
        if (from is null || to is null)
            return GraphRuleResult.Fail("One of those nodes is no longer on the canvas.");

        var alreadyConnected = graph.Edges.Any(e =>
            (e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId) ||
            (e.FromNodeId == toNodeId && e.ToNodeId == fromNodeId));

        if (alreadyConnected)
            return GraphRuleResult.Fail("Those two nodes are already connected.");

        return (from.Kind, to.Kind) switch
        {
            (NodeKind.Device, NodeKind.Board) => SameDevice(from, to, topology),
            (NodeKind.Board, NodeKind.Channel) => SameDevice(from, to, topology),
            (NodeKind.Battery, NodeKind.Channel) => BatteryFree(graph, to),
            (NodeKind.Channel, NodeKind.Battery) => BatteryFree(graph, from),
            _ => GraphRuleResult.Fail(
                $"A {from.Kind.ToString().ToLowerInvariant()} cannot connect to a "
              + $"{to.Kind.ToString().ToLowerInvariant()}. Connect device to board, "
              + "board to channel, and battery to channel."),
        };
    }

    public static GraphRuleResult CanAttach(WorkflowGraph graph, string nodeId)
    {
        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);

        if (node is null)
            return GraphRuleResult.Fail("That node is no longer on the canvas.");

        return node.Kind == NodeKind.Channel
            ? GraphRuleResult.Ok
            : GraphRuleResult.Fail("A program or DBC file can only be attached to a channel.");
    }

    /// <summary>
    /// The headline topology rule: a topology edge may only join two pieces of the SAME physical
    /// device. Wiring device A's board to device B's channel would produce a layout describing
    /// hardware that does not exist.
    /// </summary>
    private static GraphRuleResult SameDevice(
        WorkflowNode from, WorkflowNode to, TopologySnapshot topology)
    {
        var fromDevice = OwningDeviceId(from, topology);
        var toDevice = OwningDeviceId(to, topology);

        if (fromDevice is null || toDevice is null)
            return GraphRuleResult.Fail(
                "That hardware no longer exists in the database, so it cannot be connected.");

        return fromDevice == toDevice
            ? GraphRuleResult.Ok
            : GraphRuleResult.Fail("Those two belong to a different device and cannot be connected.");
    }

    private static int? OwningDeviceId(WorkflowNode node, TopologySnapshot topology)
    {
        if (node.EntityId is not { } id) return null;

        return node.Kind switch
        {
            NodeKind.Device => topology.HasDevice((int)id) ? (int)id : null,
            NodeKind.Board => topology.FindDeviceOwningBoard(id)?.DeviceId,
            NodeKind.Channel => topology.FindDeviceOwningChannel(id)?.DeviceId,
            _ => null,
        };
    }

    private static GraphRuleResult BatteryFree(WorkflowGraph graph, WorkflowNode channel)
    {
        var hasBattery = graph.Edges.Any(e =>
            e.Kind == EdgeKind.Power &&
            (e.FromNodeId == channel.Id || e.ToNodeId == channel.Id));

        return hasBattery
            ? GraphRuleResult.Fail("That channel already has a battery attached.")
            : GraphRuleResult.Ok;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphValidatorTests`
Expected: PASS — 19 test cases.

- [ ] **Step 5: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowGraphValidator.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphValidatorTests.cs
git commit -m "feat(workflow-canvas): graph validation rules for placement and connection"
```

---

### Task 3: Auto-import — topology to graph

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowGraphBuilder.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphBuilderTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowNode`, `WorkflowEdge`, `NodeKind`, `EdgeKind`, `TopologyDevice`, `TopologyBoard`, `TopologyChannel` (Task 1).
- Produces: `WorkflowGraphBuilder.BuildForDevice(TopologyDevice device, IReadOnlyCollection<long>? channelIds = null) -> WorkflowGraph`, and the id conventions every later task relies on: device node id `"dev-{DeviceId}"`, board node id `"brd-{BoardId}"`, channel node id `"chn-{ChannelId}"`, edge id `"edge-{FromNodeId}--{ToNodeId}"`.

Passing `null` for `channelIds` means "every channel"; passing a set means "only these", which is
how the operator takes just the channels that need to work (spec 5.2).

- [ ] **Step 1: Write the failing tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Auto-import is what makes the experiment reachable — wiring 64 channels by hand is tedious
/// enough that nobody would get far enough to judge the interaction. Node ids produced here are
/// a contract: the telemetry bridge and the JS layer both parse them.
/// </summary>
public class WorkflowGraphBuilderTests
{
    private static TopologyDevice Device(int boards = 2, int channelsPerBoard = 2)
    {
        var boardList = new List<TopologyBoard>();
        for (var b = 1; b <= boards; b++)
        {
            var channels = Enumerable.Range(1, channelsPerBoard)
                .Select(c => new TopologyChannel(b * 100 + c, c))
                .ToList();
            boardList.Add(new TopologyBoard(b * 10, b, channels));
        }
        return new TopologyDevice(1, "Dev-1", boardList);
    }

    [Fact]
    public void BuildForDevice_CreatesOneNodePerDeviceBoardAndChannel()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(Device(boards: 2, channelsPerBoard: 2));

        Assert.Single(graph.Nodes.Where(n => n.Kind == NodeKind.Device));
        Assert.Equal(2, graph.Nodes.Count(n => n.Kind == NodeKind.Board));
        Assert.Equal(4, graph.Nodes.Count(n => n.Kind == NodeKind.Channel));
    }

    [Fact]
    public void BuildForDevice_UsesTheDocumentedIdConventions()
    {
        // These exact strings are parsed by the telemetry bridge and the JS layer. Changing the
        // format here breaks both silently, so it is pinned.
        var graph = WorkflowGraphBuilder.BuildForDevice(Device(boards: 1, channelsPerBoard: 1));

        Assert.Contains(graph.Nodes, n => n.Id == "dev-1");
        Assert.Contains(graph.Nodes, n => n.Id == "brd-10");
        Assert.Contains(graph.Nodes, n => n.Id == "chn-101");
        Assert.Contains(graph.Edges, e => e.Id == "edge-dev-1--brd-10");
    }

    [Fact]
    public void BuildForDevice_WiresDeviceToEachBoardAndEachBoardToItsOwnChannels()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(Device(boards: 2, channelsPerBoard: 2));

        Assert.Equal(2, graph.Edges.Count(e => e.FromNodeId == "dev-1"));
        Assert.Contains(graph.Edges, e => e.FromNodeId == "brd-10" && e.ToNodeId == "chn-101");
        Assert.Contains(graph.Edges, e => e.FromNodeId == "brd-20" && e.ToNodeId == "chn-201");

        // No cross-wiring: board 10 must never reach a channel of board 20.
        Assert.DoesNotContain(graph.Edges, e => e.FromNodeId == "brd-10" && e.ToNodeId == "chn-201");
    }

    [Fact]
    public void BuildForDevice_MarksEveryEdgeAsTopology_NeverPower()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(Device());

        Assert.All(graph.Edges, e => Assert.Equal(EdgeKind.Topology, e.Kind));
    }

    [Fact]
    public void BuildForDevice_CreatesNoBatteryNodesAndNoAttachments()
    {
        // Auto-import describes hardware only. Batteries and programs are the operator's intent
        // and must never be invented for them.
        var graph = WorkflowGraphBuilder.BuildForDevice(Device());

        Assert.DoesNotContain(graph.Nodes, n => n.Kind == NodeKind.Battery);
        Assert.All(graph.Nodes, n => Assert.Null(n.Attach));
    }

    [Fact]
    public void BuildForDevice_WithAChannelSubset_TakesOnlyThoseChannels()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(
            Device(boards: 2, channelsPerBoard: 2), new List<long> { 101, 202 });

        var channelIds = graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel)
            .Select(n => n.EntityId)
            .ToList();

        Assert.Equal(new long?[] { 101, 202 }, channelIds);
    }

    [Fact]
    public void BuildForDevice_DropsABoardThatHasNoSelectedChannels()
    {
        // An empty board node would be visual noise the operator did not ask for.
        var graph = WorkflowGraphBuilder.BuildForDevice(
            Device(boards: 2, channelsPerBoard: 2), new List<long> { 101 });

        Assert.Single(graph.Nodes.Where(n => n.Kind == NodeKind.Board));
        Assert.Contains(graph.Nodes, n => n.Id == "brd-10");
        Assert.DoesNotContain(graph.Nodes, n => n.Id == "brd-20");
    }

    [Fact]
    public void BuildForDevice_HandlesADeviceWithZeroBoards_WithoutThrowing()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(
            new TopologyDevice(7, "Empty", new List<TopologyBoard>()));

        Assert.Single(graph.Nodes);
        Assert.Equal("dev-7", graph.Nodes[0].Id);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void BuildForDevice_StampsTheCurrentSchemaVersion()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(Device());

        Assert.Equal(WorkflowGraph.CurrentVersion, graph.Version);
    }

    [Fact]
    public void BuildForDevice_ProducesAGraphThatPassesItsOwnValidator()
    {
        // The builder must never emit something the validator would have refused; otherwise
        // auto-import can create a layout the user cannot recreate by hand.
        var device = Device(boards: 2, channelsPerBoard: 2);
        var topology = new TopologySnapshot(new[] { device });
        var graph = WorkflowGraphBuilder.BuildForDevice(device);

        var rebuilt = WorkflowGraph.Empty;
        foreach (var node in graph.Nodes)
        {
            Assert.True(WorkflowGraphValidator.CanAddNode(rebuilt, node).IsValid);
            rebuilt = rebuilt with { Nodes = rebuilt.Nodes.Append(node).ToList() };
        }

        foreach (var edge in graph.Edges)
        {
            var check = WorkflowGraphValidator.CanConnect(
                rebuilt, edge.FromNodeId, edge.ToNodeId, topology);
            Assert.True(check.IsValid, check.Error);
            rebuilt = rebuilt with { Edges = rebuilt.Edges.Append(edge).ToList() };
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphBuilderTests`
Expected: FAIL — `WorkflowGraphBuilder` does not exist.

- [ ] **Step 3: Write the builder**

Create `Services/Implementations/Workflow/WorkflowGraphBuilder.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Turns real hardware topology into a graph. Positions are all zero here — WorkflowAutoLayout
/// assigns them — so that placement and arrangement stay independently testable.
/// </summary>
public static class WorkflowGraphBuilder
{
    public static string DeviceNodeId(int deviceId) => $"dev-{deviceId}";

    public static string BoardNodeId(long boardId) => $"brd-{boardId}";

    public static string ChannelNodeId(long channelId) => $"chn-{channelId}";

    public static string EdgeId(string fromNodeId, string toNodeId) =>
        $"edge-{fromNodeId}--{toNodeId}";

    /// <summary>
    /// Builds device -> board -> channel nodes and their topology edges.
    /// <paramref name="channelIds"/> null means every channel; a set means only those, and any
    /// board left with no selected channel is dropped rather than rendered empty.
    /// </summary>
    public static WorkflowGraph BuildForDevice(
        TopologyDevice device, IReadOnlyCollection<long>? channelIds = null)
    {
        var nodes = new List<WorkflowNode>();
        var edges = new List<WorkflowEdge>();

        var deviceNodeId = DeviceNodeId(device.DeviceId);
        nodes.Add(new WorkflowNode(deviceNodeId, NodeKind.Device, device.DeviceId, 0, 0, null));

        foreach (var board in device.Boards)
        {
            var selected = channelIds is null
                ? board.Channels
                : board.Channels.Where(c => channelIds.Contains(c.ChannelId)).ToList();

            if (selected.Count == 0) continue;

            var boardNodeId = BoardNodeId(board.BoardId);
            nodes.Add(new WorkflowNode(boardNodeId, NodeKind.Board, board.BoardId, 0, 0, null));
            edges.Add(new WorkflowEdge(
                EdgeId(deviceNodeId, boardNodeId), deviceNodeId, boardNodeId, EdgeKind.Topology));

            foreach (var channel in selected)
            {
                var channelNodeId = ChannelNodeId(channel.ChannelId);
                nodes.Add(new WorkflowNode(
                    channelNodeId, NodeKind.Channel, channel.ChannelId, 0, 0, null));
                edges.Add(new WorkflowEdge(
                    EdgeId(boardNodeId, channelNodeId), boardNodeId, channelNodeId, EdgeKind.Topology));
            }
        }

        return new WorkflowGraph(
            WorkflowGraph.CurrentVersion, nodes, edges, CanvasViewport.Default);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphBuilderTests`
Expected: PASS — 10 tests.

- [ ] **Step 5: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowGraphBuilder.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphBuilderTests.cs
git commit -m "feat(workflow-canvas): auto-import builder from device topology"
```

---

### Task 4: Auto-layout — assigning positions

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowAutoLayout.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowNode`, `NodeKind` (Task 1).
- Produces: `WorkflowAutoLayout.Apply(WorkflowGraph) -> WorkflowGraph`, plus the public constants later tasks size their CSS against: `WorkflowAutoLayout.ColumnWidth` (= 280), `RowHeight` (= 120), `NodeWidth` (= 200), `NodeHeight` (= 96).

Layout is column-per-kind: devices at x = 0, boards at x = 280, channels at x = 560, batteries at
x = 840. Rows are assigned per column in the order nodes appear, so output is deterministic.

- [ ] **Step 1: Write the failing tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Auto-layout only has to be predictable and non-overlapping. It is not trying to be a graph
/// drawing algorithm — the operator rearranges by hand afterwards, and a layout that jumps
/// around between runs would make that impossible.
/// </summary>
public class WorkflowAutoLayoutTests
{
    private static WorkflowNode Node(string id, NodeKind kind) => new(id, kind, 1, 0, 0, null);

    private static WorkflowGraph Graph(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    [Fact]
    public void Apply_PutsEachKindInItsOwnColumn()
    {
        var result = WorkflowAutoLayout.Apply(Graph(
            Node("d", NodeKind.Device),
            Node("b", NodeKind.Board),
            Node("c", NodeKind.Channel),
            Node("bat", NodeKind.Battery)));

        double X(string id) => result.Nodes.Single(n => n.Id == id).X;

        Assert.Equal(0, X("d"));
        Assert.Equal(WorkflowAutoLayout.ColumnWidth, X("b"));
        Assert.Equal(WorkflowAutoLayout.ColumnWidth * 2, X("c"));
        Assert.Equal(WorkflowAutoLayout.ColumnWidth * 3, X("bat"));
    }

    [Fact]
    public void Apply_StacksNodesWithinAColumnWithoutOverlapping()
    {
        var result = WorkflowAutoLayout.Apply(Graph(
            Node("c1", NodeKind.Channel),
            Node("c2", NodeKind.Channel),
            Node("c3", NodeKind.Channel)));

        var ys = result.Nodes.Select(n => n.Y).OrderBy(y => y).ToList();

        Assert.Equal(3, ys.Distinct().Count());
        for (var i = 1; i < ys.Count; i++)
            Assert.True(ys[i] - ys[i - 1] >= WorkflowAutoLayout.NodeHeight,
                "adjacent nodes in a column must be at least one node-height apart");
    }

    [Fact]
    public void Apply_IsDeterministic_ForIdenticalInput()
    {
        // Two runs that disagree would move the operator's carefully arranged canvas every time
        // they re-imported.
        var graph = Graph(
            Node("c1", NodeKind.Channel), Node("c2", NodeKind.Channel), Node("d", NodeKind.Device));

        var first = WorkflowAutoLayout.Apply(graph);
        var second = WorkflowAutoLayout.Apply(graph);

        Assert.Equal(first.Nodes, second.Nodes);
    }

    [Fact]
    public void Apply_NeverPlacesTwoNodesAtTheSamePoint()
    {
        var nodes = Enumerable.Range(1, 64)
            .Select(i => Node($"c{i}", NodeKind.Channel))
            .Concat(Enumerable.Range(1, 8).Select(i => Node($"b{i}", NodeKind.Board)))
            .ToArray();

        var result = WorkflowAutoLayout.Apply(Graph(nodes));

        var points = result.Nodes.Select(n => (n.X, n.Y)).ToList();
        Assert.Equal(points.Count, points.Distinct().Count());
    }

    [Fact]
    public void Apply_PreservesNodeIdentityAndAttachments()
    {
        var result = WorkflowAutoLayout.Apply(Graph(
            new WorkflowNode("c1", NodeKind.Channel, 42, 0, 0, new NodeAttachment(7, null, null))));

        var node = result.Nodes.Single();
        Assert.Equal("c1", node.Id);
        Assert.Equal(42, node.EntityId);
        Assert.Equal(7, node.Attach!.ProgramId);
    }

    [Fact]
    public void Apply_LeavesEdgesAndViewportUntouched()
    {
        var graph = Graph(Node("d", NodeKind.Device)) with
        {
            Edges = new List<WorkflowEdge> { new("e1", "d", "b", EdgeKind.Topology) },
            Viewport = new CanvasViewport(5, 6, 0.5),
        };

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

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowAutoLayoutTests`
Expected: FAIL — `WorkflowAutoLayout` does not exist.

- [ ] **Step 3: Write the layout**

Create `Services/Implementations/Workflow/WorkflowAutoLayout.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Column-per-kind arrangement: device, board, channel, battery, left to right. Deliberately
/// simple and deterministic — the operator rearranges by hand, and a clever algorithm that moved
/// things differently on each import would fight them.
/// </summary>
public static class WorkflowAutoLayout
{
    public const double ColumnWidth = 280;
    public const double RowHeight = 120;
    public const double NodeWidth = 200;
    public const double NodeHeight = 96;

    public static WorkflowGraph Apply(WorkflowGraph graph)
    {
        var rowsUsed = new Dictionary<NodeKind, int>();

        var positioned = graph.Nodes.Select(node =>
        {
            var row = rowsUsed.TryGetValue(node.Kind, out var used) ? used : 0;
            rowsUsed[node.Kind] = row + 1;

            return node with
            {
                X = ColumnOf(node.Kind) * ColumnWidth,
                Y = row * RowHeight,
            };
        }).ToList();

        return graph with { Nodes = positioned };
    }

    private static int ColumnOf(NodeKind kind) => kind switch
    {
        NodeKind.Device => 0,
        NodeKind.Board => 1,
        NodeKind.Channel => 2,
        NodeKind.Battery => 3,
        _ => 0,
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowAutoLayoutTests`
Expected: PASS — 7 tests.

- [ ] **Step 5: Run the whole suite to confirm nothing regressed**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: PASS — the pre-existing tests plus the new ones from Tasks 1-4.

- [ ] **Step 6: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowAutoLayout.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs
git commit -m "feat(workflow-canvas): deterministic column auto-layout"
```

---

### Task 5: The WorkflowLayout table and repository

**Files:**
- Create: `Models/Entities/WorkflowLayout.cs`
- Create: `Repositories/Interfaces/IWorkflowLayoutRepository.cs`
- Create: `Repositories/Implementations/WorkflowLayoutRepository.cs`
- Modify: `Data/AppDbContext.cs` — add one `DbSet` beside the others (around line 49)
- Create: `Migrations/<generated>_AddWorkflowLayout.cs` (via `dotnet ef`)
- Test: `BatteryTestingSystem.Tests/Repositories/WorkflowLayoutRepositoryTests.cs`

**Interfaces:**
- Consumes: `BaseEntity` (existing, supplies `CreatedBy`/`UpdatedBy`/`CreatedAt`/`UpdatedAt`/`IsDeleted`), `Repository<T>` (existing generic base).
- Produces: `WorkflowLayout` entity; `IWorkflowLayoutRepository` with `Task<List<WorkflowLayout>> ListForUserAsync(string userId)`, `Task<WorkflowLayout?> GetForUserAsync(long id, string userId)`, `Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout)`, `Task<bool> SoftDeleteAsync(long id, string userId)`.

**Critical:** `Repository<T>.AddAsync` stages but does **not** save (pinned by an existing test in
`RepositoryTests.cs`). Every write method here must call `SaveChangesAsync` itself.

- [ ] **Step 1: Write the failing tests**

Create `BatteryTestingSystem.Tests/Repositories/WorkflowLayoutRepositoryTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BatteryTestingSystem.Tests.Repositories;

/// <summary>
/// Same approach as the existing RepositoryTests: a real SQLite in-memory provider rather than a
/// fake DbSet, because the bugs worth catching here are ownership filtering and whether
/// SaveChangesAsync actually ran.
/// </summary>
public class WorkflowLayoutRepositoryTests
{
    private static (SqliteConnection Conn, AppDbContext Ctx) NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        ctx.Database.EnsureCreated();

        return (conn, ctx);
    }

    private static WorkflowLayout Layout(string name, string owner, string json = "{}") => new()
    {
        Name = name,
        OwnerUserId = owner,
        LayoutJson = json,
        SchemaVersion = 1,
    };

    [Fact]
    public async Task UpsertAsync_PersistsANewLayout_AndAssignsAnId()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("First", "user-a"));

        Assert.True(saved.Id > 0);
        // A second context over the same connection proves it really hit the database rather
        // than only the change tracker.
        using var verify = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        Assert.Equal("First", verify.WorkflowLayouts.Single().Name);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesAnExistingLayoutInPlace_RatherThanInsertingASecondRow()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("First", "user-a", "{\"v\":1}"));
        saved.LayoutJson = "{\"v\":2}";
        await repo.UpsertAsync(saved);

        Assert.Equal(1, await ctx.WorkflowLayouts.CountAsync());
        Assert.Equal("{\"v\":2}", (await ctx.WorkflowLayouts.SingleAsync()).LayoutJson);
    }

    [Fact]
    public async Task UpsertAsync_StampsUpdatedAt()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("First", "user-a"));
        var firstStamp = saved.UpdatedAt;

        saved.Name = "Renamed";
        var updated = await repo.UpsertAsync(saved);

        Assert.True(updated.UpdatedAt >= firstStamp);
    }

    [Fact]
    public async Task ListForUserAsync_ReturnsOnlyThatUsersLayouts()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        await repo.UpsertAsync(Layout("Mine", "user-a"));
        await repo.UpsertAsync(Layout("Theirs", "user-b"));

        var mine = await repo.ListForUserAsync("user-a");

        Assert.Single(mine);
        Assert.Equal("Mine", mine[0].Name);
    }

    [Fact]
    public async Task GetForUserAsync_RefusesAnotherUsersLayout()
    {
        // Layouts are per-user in v1. Returning someone else's by id would be a quiet
        // authorization hole the UI has no reason to guard against.
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var theirs = await repo.UpsertAsync(Layout("Theirs", "user-b"));

        Assert.Null(await repo.GetForUserAsync(theirs.Id, "user-a"));
        Assert.NotNull(await repo.GetForUserAsync(theirs.Id, "user-b"));
    }

    [Fact]
    public async Task SoftDeleteAsync_HidesTheLayoutFromListAndGet_ButKeepsTheRow()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var saved = await repo.UpsertAsync(Layout("Doomed", "user-a"));

        Assert.True(await repo.SoftDeleteAsync(saved.Id, "user-a"));

        Assert.Empty(await repo.ListForUserAsync("user-a"));
        Assert.Null(await repo.GetForUserAsync(saved.Id, "user-a"));
        Assert.Equal(1, await ctx.WorkflowLayouts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsFalseForAnotherUsersLayout()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var theirs = await repo.UpsertAsync(Layout("Theirs", "user-b"));

        Assert.False(await repo.SoftDeleteAsync(theirs.Id, "user-a"));
        Assert.Single(await repo.ListForUserAsync("user-b"));
    }

    [Fact]
    public async Task ListForUserAsync_ReturnsMostRecentlyUpdatedFirst()
    {
        var (conn, ctx) = NewContext();
        using var _ = conn;
        var repo = new WorkflowLayoutRepository(ctx);

        var older = await repo.UpsertAsync(Layout("Older", "user-a"));
        older.UpdatedAt = older.UpdatedAt.AddHours(-2);
        await repo.UpsertAsync(older);
        await repo.UpsertAsync(Layout("Newer", "user-a"));

        var list = await repo.ListForUserAsync("user-a");

        Assert.Equal("Newer", list[0].Name);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowLayoutRepositoryTests`
Expected: FAIL — `WorkflowLayout` and `WorkflowLayoutRepository` do not exist.

- [ ] **Step 3: Write the entity**

Create `Models/Entities/WorkflowLayout.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    /// <summary>
    /// One saved canvas arrangement. The graph itself is an opaque JSON document (spec D7) so the
    /// node shape can change freely during the experiment without a migration each time.
    /// There are deliberately no foreign keys to Device / Channel / Program: ids inside the blob
    /// are soft references, and deleting hardware must never break or cascade into a layout.
    /// </summary>
    [Table(name: "WorkflowLayouts", Schema = "Device")]
    public class WorkflowLayout : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(400)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(450)]                        // matches AspNetUsers.Id length
        public string OwnerUserId { get; set; } = string.Empty;

        [Required]
        public string LayoutJson { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = 1;
    }
}
```

- [ ] **Step 4: Register the DbSet**

In `Data/AppDbContext.cs`, beside the other `DbSet` declarations (after the `ExportRecords` line
around line 49), add:

```csharp
        public DbSet<WorkflowLayout> WorkflowLayouts { get; set; } = null!;
```

- [ ] **Step 5: Write the repository**

Create `Repositories/Interfaces/IWorkflowLayoutRepository.cs`:

```csharp
using BatteryTestingSystem.Models.Entities;

namespace BatteryTestingSystem.Repositories.Interfaces;

public interface IWorkflowLayoutRepository
{
    Task<List<WorkflowLayout>> ListForUserAsync(string userId);

    Task<WorkflowLayout?> GetForUserAsync(long id, string userId);

    Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout);

    Task<bool> SoftDeleteAsync(long id, string userId);
}
```

Create `Repositories/Implementations/WorkflowLayoutRepository.cs`:

```csharp
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations;

/// <summary>
/// Every query filters on OwnerUserId as well as id — layouts are per-user in v1, and an id-only
/// lookup would quietly hand one user another user's layout.
/// Note: Repository&lt;T&gt;.AddAsync only stages the entity, so this class calls SaveChangesAsync
/// itself (see the contract pinned in RepositoryTests).
/// </summary>
public class WorkflowLayoutRepository : IWorkflowLayoutRepository
{
    private readonly AppDbContext _context;

    public WorkflowLayoutRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<WorkflowLayout>> ListForUserAsync(string userId) =>
        await _context.WorkflowLayouts
            .Where(l => l.OwnerUserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.UpdatedAt)
            .ToListAsync();

    public async Task<WorkflowLayout?> GetForUserAsync(long id, string userId) =>
        await _context.WorkflowLayouts
            .FirstOrDefaultAsync(l => l.Id == id && l.OwnerUserId == userId && !l.IsDeleted);

    public async Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout)
    {
        layout.UpdatedAt = DateTime.Now;

        if (layout.Id == 0)
        {
            layout.CreatedAt = DateTime.Now;
            await _context.WorkflowLayouts.AddAsync(layout);
        }
        else
        {
            _context.WorkflowLayouts.Update(layout);
        }

        await _context.SaveChangesAsync();
        return layout;
    }

    public async Task<bool> SoftDeleteAsync(long id, string userId)
    {
        var layout = await GetForUserAsync(id, userId);
        if (layout is null) return false;

        layout.IsDeleted = true;
        layout.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowLayoutRepositoryTests`
Expected: PASS — 8 tests.

If `SoftDeleteAsync_HidesTheLayout...` fails on `IgnoreQueryFilters`, the context has no global
query filter for `IsDeleted`; that is fine — the assertion still holds because the repository
filters explicitly. Leave the call in place; it is a no-op when no filter exists.

- [ ] **Step 7: Generate the migration**

Make sure the app is not running (it locks the output assembly), then:

```bash
dotnet ef migrations add AddWorkflowLayout
```

Open the generated `Migrations/<timestamp>_AddWorkflowLayout.cs` and confirm it creates exactly
one table, `WorkflowLayouts`, with **no** foreign keys. If it contains anything else, a stray
model change has been picked up — revert with `dotnet ef migrations remove` and investigate
before continuing.

- [ ] **Step 8: Apply and verify the migration**

```bash
dotnet ef database update
dotnet build
```

Expected: build succeeds, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add Models/Entities/WorkflowLayout.cs Repositories/ Data/AppDbContext.cs Migrations/ BatteryTestingSystem.Tests/Repositories/WorkflowLayoutRepositoryTests.cs
git commit -m "feat(workflow-canvas): WorkflowLayout entity, repository and migration"
```

---

### Task 6: WorkflowLayoutService — save, load, and stale-node resolution

This is the highest-risk task in the plan. A layout referencing hardware that has since been
deleted is guaranteed to happen, and the failure mode must be a dimmed node, never an exception.

**Files:**
- Create: `Services/Interfaces/IWorkflowLayoutService.cs`
- Create: `Services/Implementations/Workflow/WorkflowLayoutService.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowLayoutServiceTests.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/FakeWorkflowLayoutRepository.cs`

**Interfaces:**
- Consumes: `IWorkflowLayoutRepository`, `WorkflowLayout` (Task 5); `WorkflowGraph`, `TopologySnapshot`, `NodeKind` (Task 1); `WorkflowGraphJson` (Task 1).
- Produces:
  - `record LayoutSummary(long Id, string Name, string? Description, DateTime UpdatedAt)`
  - `record LayoutLoadResult(bool Ok, WorkflowGraph? Graph, IReadOnlyList<string> StaleNodeIds, string? Error)`
  - `IWorkflowLayoutService` with `Task<IReadOnlyList<LayoutSummary>> ListAsync(string userId)`, `Task<LayoutLoadResult> LoadAsync(long id, string userId, TopologySnapshot topology)`, `Task<long> SaveAsync(long? id, string name, string? description, WorkflowGraph graph, string userId)`, `Task<bool> DeleteAsync(long id, string userId)`
  - `WorkflowLayoutService.FindStaleNodeIds(WorkflowGraph, TopologySnapshot) -> IReadOnlyList<string>` (public and static so it can be tested directly)

- [ ] **Step 1: Write the fake repository**

Create `BatteryTestingSystem.Tests/Services/Workflow/FakeWorkflowLayoutRepository.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// An in-memory stand-in so the service's orchestration can be tested without EF. The repository
/// itself is covered separately against real SQLite in WorkflowLayoutRepositoryTests.
/// </summary>
public class FakeWorkflowLayoutRepository : IWorkflowLayoutRepository
{
    private readonly List<WorkflowLayout> _rows = new();
    private long _nextId = 1;

    public IReadOnlyList<WorkflowLayout> Rows => _rows;

    public Task<List<WorkflowLayout>> ListForUserAsync(string userId) =>
        Task.FromResult(_rows
            .Where(l => l.OwnerUserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.UpdatedAt)
            .ToList());

    public Task<WorkflowLayout?> GetForUserAsync(long id, string userId) =>
        Task.FromResult(_rows.FirstOrDefault(
            l => l.Id == id && l.OwnerUserId == userId && !l.IsDeleted));

    public Task<WorkflowLayout> UpsertAsync(WorkflowLayout layout)
    {
        layout.UpdatedAt = DateTime.Now;

        if (layout.Id == 0)
        {
            layout.Id = _nextId++;
            _rows.Add(layout);
        }
        else
        {
            _rows.RemoveAll(l => l.Id == layout.Id);
            _rows.Add(layout);
        }

        return Task.FromResult(layout);
    }

    public async Task<bool> SoftDeleteAsync(long id, string userId)
    {
        var row = await GetForUserAsync(id, userId);
        if (row is null) return false;
        row.IsDeleted = true;
        return true;
    }
}
```

- [ ] **Step 2: Write the failing service tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowLayoutServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Spec 4.3 and 8. The stale-node tests here are the highest-value tests in the whole feature:
/// a saved layout WILL eventually reference deleted hardware, and the required behaviour is a
/// dimmed node plus a warning, never a thrown exception that kills the Blazor circuit.
/// </summary>
public class WorkflowLayoutServiceTests
{
    private const string User = "user-a";

    // Device 1 / board 10 / channels 100, 101 all exist. Anything else does not.
    private static TopologySnapshot Topology() => new(new[]
    {
        new TopologyDevice(1, "Dev-1", new[]
        {
            new TopologyBoard(10, 1, new[]
            {
                new TopologyChannel(100, 1),
                new TopologyChannel(101, 2),
            }),
        }),
    });

    private static WorkflowGraph GraphWith(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    private static WorkflowNode Node(string id, NodeKind kind, long? entityId) =>
        new(id, kind, entityId, 0, 0, null);

    private static (WorkflowLayoutService Service, FakeWorkflowLayoutRepository Repo) NewService()
    {
        var repo = new FakeWorkflowLayoutRepository();
        return (new WorkflowLayoutService(repo), repo);
    }

    // ============================================================ FindStaleNodeIds (pure)

    [Fact]
    public void FindStaleNodeIds_ReturnsNothingWhenEveryReferenceResolves()
    {
        var graph = GraphWith(
            Node("d", NodeKind.Device, 1),
            Node("b", NodeKind.Board, 10),
            Node("c", NodeKind.Channel, 100));

        Assert.Empty(WorkflowLayoutService.FindStaleNodeIds(graph, Topology()));
    }

    [Fact]
    public void FindStaleNodeIds_FlagsAChannelThatNoLongerExists()
    {
        var graph = GraphWith(
            Node("c-ok", NodeKind.Channel, 100),
            Node("c-gone", NodeKind.Channel, 999));

        var stale = WorkflowLayoutService.FindStaleNodeIds(graph, Topology());

        Assert.Equal(new[] { "c-gone" }, stale);
    }

    [Fact]
    public void FindStaleNodeIds_FlagsDeletedDevicesAndBoardsToo()
    {
        var graph = GraphWith(
            Node("d-gone", NodeKind.Device, 77),
            Node("b-gone", NodeKind.Board, 88));

        var stale = WorkflowLayoutService.FindStaleNodeIds(graph, Topology());

        Assert.Equal(2, stale.Count);
        Assert.Contains("d-gone", stale);
        Assert.Contains("b-gone", stale);
    }

    [Fact]
    public void FindStaleNodeIds_NeverFlagsABatteryNode()
    {
        // Battery nodes reference a BatteryType, not hardware topology, so the topology snapshot
        // cannot say anything about them. Flagging one would dim every battery on the canvas.
        var graph = GraphWith(Node("bat", NodeKind.Battery, 4242));

        Assert.Empty(WorkflowLayoutService.FindStaleNodeIds(graph, Topology()));
    }

    [Fact]
    public void FindStaleNodeIds_HandlesAnEmptyTopology_WithoutThrowing()
    {
        // Happens legitimately: the canvas opens before any device has registered.
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));

        Assert.Equal(new[] { "c" }, WorkflowLayoutService.FindStaleNodeIds(graph, TopologySnapshot.Empty));
    }

    // ============================================================ Save / Load

    [Fact]
    public async Task SaveThenLoad_RoundTripsTheGraph()
    {
        var (service, _) = NewService();
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));

        var id = await service.SaveAsync(null, "My layout", "notes", graph, User);
        var loaded = await service.LoadAsync(id, User, Topology());

        Assert.True(loaded.Ok);
        Assert.Null(loaded.Error);
        Assert.Equal(graph.Nodes, loaded.Graph!.Nodes);
        Assert.Empty(loaded.StaleNodeIds);
    }

    [Fact]
    public async Task SaveAsync_WithAnExistingId_UpdatesRatherThanCreating()
    {
        var (service, repo) = NewService();

        var id = await service.SaveAsync(null, "First", null, WorkflowGraph.Empty, User);
        await service.SaveAsync(id, "Renamed", null, WorkflowGraph.Empty, User);

        Assert.Single(repo.Rows);
        Assert.Equal("Renamed", repo.Rows[0].Name);
    }

    [Fact]
    public async Task SaveAsync_StampsTheCurrentSchemaVersion()
    {
        var (service, repo) = NewService();

        await service.SaveAsync(null, "First", null, WorkflowGraph.Empty, User);

        Assert.Equal(WorkflowGraph.CurrentVersion, repo.Rows[0].SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_ReturnsStaleIdsInsteadOfThrowing_WhenHardwareWasDeleted()
    {
        // THE test. A device deleted after the layout was saved must produce a dimmed node.
        var (service, _) = NewService();
        var graph = GraphWith(
            Node("c-ok", NodeKind.Channel, 100),
            Node("c-gone", NodeKind.Channel, 999));

        var id = await service.SaveAsync(null, "Mixed", null, graph, User);
        var loaded = await service.LoadAsync(id, User, Topology());

        Assert.True(loaded.Ok);
        Assert.Equal(2, loaded.Graph!.Nodes.Count);      // nothing is dropped
        Assert.Equal(new[] { "c-gone" }, loaded.StaleNodeIds);
    }

    [Fact]
    public async Task LoadAsync_RefusesALayoutSavedUnderADifferentSchemaVersion()
    {
        var (service, repo) = NewService();
        await repo.UpsertAsync(new WorkflowLayout
        {
            Name = "Ancient",
            OwnerUserId = User,
            SchemaVersion = 99,
            LayoutJson = WorkflowGraphJson.Serialize(WorkflowGraph.Empty with { Version = 99 }),
        });

        var loaded = await service.LoadAsync(repo.Rows[0].Id, User, Topology());

        Assert.False(loaded.Ok);
        Assert.Null(loaded.Graph);
        Assert.Contains("version", loaded.Error!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_ReturnsAnErrorForACorruptedBlob_RatherThanThrowing()
    {
        var (service, repo) = NewService();
        await repo.UpsertAsync(new WorkflowLayout
        {
            Name = "Corrupt",
            OwnerUserId = User,
            SchemaVersion = WorkflowGraph.CurrentVersion,
            LayoutJson = "{ this is not json",
        });

        var loaded = await service.LoadAsync(repo.Rows[0].Id, User, Topology());

        Assert.False(loaded.Ok);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Error));
    }

    [Fact]
    public async Task LoadAsync_ReturnsAnErrorForAMissingOrForeignLayout()
    {
        var (service, _) = NewService();
        var id = await service.SaveAsync(null, "Mine", null, WorkflowGraph.Empty, User);

        var missing = await service.LoadAsync(9999, User, Topology());
        var foreign = await service.LoadAsync(id, "someone-else", Topology());

        Assert.False(missing.Ok);
        Assert.False(foreign.Ok);
    }

    [Fact]
    public async Task ListAsync_ReturnsSummariesForThatUserOnly()
    {
        var (service, _) = NewService();
        await service.SaveAsync(null, "Mine", "d", WorkflowGraph.Empty, User);
        await service.SaveAsync(null, "Theirs", null, WorkflowGraph.Empty, "user-b");

        var list = await service.ListAsync(User);

        Assert.Single(list);
        Assert.Equal("Mine", list[0].Name);
        Assert.Equal("d", list[0].Description);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheLayoutFromTheList()
    {
        var (service, _) = NewService();
        var id = await service.SaveAsync(null, "Doomed", null, WorkflowGraph.Empty, User);

        Assert.True(await service.DeleteAsync(id, User));
        Assert.Empty(await service.ListAsync(User));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowLayoutServiceTests`
Expected: FAIL — `WorkflowLayoutService` and `IWorkflowLayoutService` do not exist.

- [ ] **Step 4: Write the interface**

Create `Services/Interfaces/IWorkflowLayoutService.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Interfaces;

public record LayoutSummary(long Id, string Name, string? Description, DateTime UpdatedAt);

/// <summary>
/// Ok=false means the layout could not be opened at all (missing, foreign, corrupt, or saved by
/// another schema version). Ok=true with a non-empty StaleNodeIds means it opened fine but some
/// nodes point at hardware that no longer exists — those render dimmed, they are not an error.
/// </summary>
public record LayoutLoadResult(
    bool Ok,
    WorkflowGraph? Graph,
    IReadOnlyList<string> StaleNodeIds,
    string? Error)
{
    public static LayoutLoadResult Failed(string error) =>
        new(false, null, Array.Empty<string>(), error);
}

public interface IWorkflowLayoutService
{
    Task<IReadOnlyList<LayoutSummary>> ListAsync(string userId);

    Task<LayoutLoadResult> LoadAsync(long id, string userId, TopologySnapshot topology);

    /// <summary>Pass id = null to create; an existing id to update in place. Returns the id.</summary>
    Task<long> SaveAsync(
        long? id, string name, string? description, WorkflowGraph graph, string userId);

    Task<bool> DeleteAsync(long id, string userId);
}
```

- [ ] **Step 5: Write the service**

Create `Services/Implementations/Workflow/WorkflowLayoutService.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public class WorkflowLayoutService : IWorkflowLayoutService
{
    private readonly IWorkflowLayoutRepository _repository;

    public WorkflowLayoutService(IWorkflowLayoutRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<LayoutSummary>> ListAsync(string userId)
    {
        var rows = await _repository.ListForUserAsync(userId);
        return rows
            .Select(r => new LayoutSummary(r.Id, r.Name, r.Description, r.UpdatedAt))
            .ToList();
    }

    public async Task<LayoutLoadResult> LoadAsync(
        long id, string userId, TopologySnapshot topology)
    {
        var row = await _repository.GetForUserAsync(id, userId);

        if (row is null)
            return LayoutLoadResult.Failed("That layout no longer exists.");

        if (!WorkflowGraphJson.TryDeserialize(row.LayoutJson, out var graph, out var error))
            return LayoutLoadResult.Failed(error!);

        return new LayoutLoadResult(true, graph, FindStaleNodeIds(graph!, topology), null);
    }

    public async Task<long> SaveAsync(
        long? id, string name, string? description, WorkflowGraph graph, string userId)
    {
        var row = id is { } existingId
            ? await _repository.GetForUserAsync(existingId, userId)
            : null;

        row ??= new WorkflowLayout { OwnerUserId = userId };

        row.Name = name;
        row.Description = description;
        row.LayoutJson = WorkflowGraphJson.Serialize(graph);
        row.SchemaVersion = WorkflowGraph.CurrentVersion;

        var saved = await _repository.UpsertAsync(row);
        return saved.Id;
    }

    public Task<bool> DeleteAsync(long id, string userId) =>
        _repository.SoftDeleteAsync(id, userId);

    /// <summary>
    /// Node ids whose soft reference no longer resolves against live hardware. These render
    /// dimmed with a warning badge and are excluded from the telemetry subscription — they are
    /// never dropped from the graph, because silently deleting a user's work is worse than
    /// showing them a grey node. Battery nodes are exempt: they reference a BatteryType, which
    /// the topology snapshot knows nothing about.
    /// </summary>
    public static IReadOnlyList<string> FindStaleNodeIds(
        WorkflowGraph graph, TopologySnapshot topology) =>
        graph.Nodes
            .Where(n => !Resolves(n, topology))
            .Select(n => n.Id)
            .ToList();

    private static bool Resolves(WorkflowNode node, TopologySnapshot topology)
    {
        if (node.Kind == NodeKind.Battery) return true;
        if (node.EntityId is not { } id) return false;

        return node.Kind switch
        {
            NodeKind.Device => topology.HasDevice((int)id),
            NodeKind.Board => topology.HasBoard(id),
            NodeKind.Channel => topology.HasChannel(id),
            _ => true,
        };
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowLayoutServiceTests`
Expected: PASS — 14 tests.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: PASS, no regressions.

- [ ] **Step 8: Commit**

```bash
git add Services/Interfaces/IWorkflowLayoutService.cs Services/Implementations/Workflow/WorkflowLayoutService.cs BatteryTestingSystem.Tests/Services/Workflow/
git commit -m "feat(workflow-canvas): layout service with stale-node resolution and version refusal"
```

---

### Task 7: Feature flags, DI, the Workflows menu entry, and an empty page

First visible milestone: the Workflows tab opens, the legacy dashboard is hidden, and the page
lists saved layouts. Nothing is drawn on a canvas yet.

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowFeatureOptions.cs`
- Create: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Create: `Services/Implementations/Workflow/WorkflowTopologyProvider.cs`
- Modify: `appsettings.Development.json` and `appsettings.json` — add the `Features` section
- Modify: `Extensions/ServiceCollectionExtensions.cs:~101` — register the new services
- Modify: `Components/Layout/MainLayout.razor:147` — the `_menuItems` list
- Modify: `Components/Layout/Navbar.razor:12` — the logo click target
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowFeatureOptionsTests.cs`

**Interfaces:**
- Consumes: `IWorkflowLayoutService`, `LayoutSummary` (Task 6); `TopologySnapshot` (Task 1); existing `TabService`, `NavMenuItem`, `ToastService`, `AppDbContext`.
- Produces: `WorkflowFeatureOptions` with `bool WorkflowCanvas` and `bool LegacyDashboard` and `const string SectionName = "Features"`; `IWorkflowTopologyProvider.GetSnapshotAsync() -> Task<TopologySnapshot>` and its EF implementation `WorkflowTopologyProvider`; the page component type `WorkflowCanvasPage`.

**Why `_menuItems` must change shape:** it is currently a field initializer, which cannot read
injected configuration. It becomes a field assigned in `OnInitialized`.

- [ ] **Step 1: Write the failing options test**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowFeatureOptionsTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The flags decide whether the experiment is visible at all and whether the old dashboard is
/// reachable from the menu. A missing config section must default to "old dashboard on, canvas
/// off" so that a deployment without the section behaves exactly like main.
/// </summary>
public class WorkflowFeatureOptionsTests
{
    private static WorkflowFeatureOptions Bind(params (string Key, string Value)[] pairs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p =>
                new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

        var options = new WorkflowFeatureOptions();
        config.GetSection(WorkflowFeatureOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Defaults_HideTheCanvasAndKeepTheLegacyDashboard()
    {
        var options = Bind();

        Assert.False(options.WorkflowCanvas);
        Assert.True(options.LegacyDashboard);
    }

    [Fact]
    public void ReadsBothFlagsFromTheFeaturesSection()
    {
        var options = Bind(
            ("Features:WorkflowCanvas", "true"),
            ("Features:LegacyDashboard", "false"));

        Assert.True(options.WorkflowCanvas);
        Assert.False(options.LegacyDashboard);
    }

    [Fact]
    public void AnAbsentFlagKeepsItsDefault_WhenTheOtherIsSet()
    {
        var options = Bind(("Features:WorkflowCanvas", "true"));

        Assert.True(options.WorkflowCanvas);
        Assert.True(options.LegacyDashboard);
    }
}
```

Add `using System.Linq;` at the top if the compiler asks for it.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowFeatureOptionsTests`
Expected: FAIL — `WorkflowFeatureOptions` does not exist.

- [ ] **Step 3: Write the options class**

Create `Services/Implementations/Workflow/WorkflowFeatureOptions.cs`:

```csharp
namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Defaults deliberately reproduce main's behaviour: canvas hidden, legacy dashboard visible.
/// A deployment that never heard of this feature must behave exactly as it did before.
/// </summary>
public class WorkflowFeatureOptions
{
    public const string SectionName = "Features";

    public bool WorkflowCanvas { get; set; } = false;

    public bool LegacyDashboard { get; set; } = true;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowFeatureOptionsTests`
Expected: PASS — 3 tests.

- [ ] **Step 5: Write the topology provider**

Create `Services/Implementations/Workflow/WorkflowTopologyProvider.cs`:

```csharp
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public interface IWorkflowTopologyProvider
{
    Task<TopologySnapshot> GetSnapshotAsync();
}

/// <summary>
/// The single bridge between EF and the pure workflow classes. Everything downstream takes a
/// TopologySnapshot, which is why the builder, validator and service need no database at all.
/// One query per table, projected immediately — this runs on the Blazor circuit's shared
/// AppDbContext, so it must never be called concurrently with itself (ADR-6).
/// </summary>
public class WorkflowTopologyProvider : IWorkflowTopologyProvider
{
    private readonly AppDbContext _context;

    public WorkflowTopologyProvider(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TopologySnapshot> GetSnapshotAsync()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Select(d => new { d.DeviceID, d.DeviceName })
            .ToListAsync();

        var boards = await _context.SecondaryBoards
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new { b.Id, b.DeviceId, b.BoardNumber })
            .ToListAsync();

        var channels = await _context.Channels
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Id, c.SecondaryBoardId, c.ChannelNumber })
            .ToListAsync();

        var snapshot = devices
            .Select(d => new TopologyDevice(
                d.DeviceID,
                d.DeviceName ?? $"Device {d.DeviceID}",
                boards
                    .Where(b => b.DeviceId == d.DeviceID)
                    .OrderBy(b => b.BoardNumber)
                    .Select(b => new TopologyBoard(
                        b.Id,
                        b.BoardNumber,
                        channels
                            .Where(c => c.SecondaryBoardId == b.Id)
                            .OrderBy(c => c.ChannelNumber)
                            .Select(c => new TopologyChannel(c.Id, c.ChannelNumber))
                            .ToList()))
                    .ToList()))
            .OrderBy(d => d.DeviceId)
            .ToList();

        return new TopologySnapshot(snapshot);
    }
}
```

If `Device.DeviceID` or `SecondaryBoard.BoardNumber` do not compile with these exact names, check
`Models/Entities/Device.cs` and `Models/Entities/SecondaryBoard.cs` and use the real ones — the
entity property is `DeviceID` (capital ID) on `Device` but `DeviceId` on `SecondaryBoard`.

- [ ] **Step 6: Register everything in DI**

In `Extensions/ServiceCollectionExtensions.cs`, immediately after the existing
`services.AddScoped<IDbcService, DbcService>();` line (around line 101), add:

```csharp
        // Workflow canvas (experimental — feat/workflow-canvas-experiment)
        services.AddScoped<IWorkflowLayoutRepository, WorkflowLayoutRepository>();
        services.AddScoped<IWorkflowLayoutService, WorkflowLayoutService>();
        services.AddScoped<IWorkflowTopologyProvider, WorkflowTopologyProvider>();
```

Add the matching `using BatteryTestingSystem.Services.Implementations.Workflow;` and
`using BatteryTestingSystem.Repositories.Interfaces;` at the top of the file if they are missing.

In `Program.cs`, wherever the builder configures services (before `builder.Build()`), bind the
options:

```csharp
builder.Services.Configure<WorkflowFeatureOptions>(
    builder.Configuration.GetSection(WorkflowFeatureOptions.SectionName));
```

- [ ] **Step 7: Add the config section**

In `appsettings.Development.json`, add at the top level:

```jsonc
  "Features": {
    "WorkflowCanvas": true,
    "LegacyDashboard": false
  }
```

In `appsettings.json`, add the same section but with production-safe values, so a real deployment
is unaffected by the experiment:

```jsonc
  "Features": {
    "WorkflowCanvas": false,
    "LegacyDashboard": true
  }
```

- [ ] **Step 8: Write the page shell**

Create `Components/Pages/Workflows/WorkflowCanvasPage.razor`:

```razor
@page "/workflows"
@attribute [Authorize]
@rendermode InteractiveServer

@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Services.Interfaces
@using BatteryTestingSystem.Services.Implementations.Workflow

@inject IWorkflowLayoutService LayoutService
@inject IWorkflowTopologyProvider TopologyProvider
@inject ToastService Toast
@inject AuthenticationStateProvider AuthState

<PageTitle>Workflows</PageTitle>

<div class="wf-shell">
    <div class="wf-toolbar">
        <span class="font-semibold">Workflows</span>
        <span class="opacity-60">@_layouts.Count saved layout(s)</span>
        <span class="opacity-60">@_topology.Devices.Count device(s) available</span>
    </div>

    <div class="wf-body">
        <div class="wf-dock">Properties</div>
        <div class="wf-canvas-host">
            @if (_loading)
            {
                <p class="opacity-60 p-4">Loading…</p>
            }
            else
            {
                <p class="opacity-60 p-4">Canvas goes here.</p>
            }
        </div>
        <div class="wf-palette">Palette</div>
    </div>
</div>

@code {
    private IReadOnlyList<LayoutSummary> _layouts = Array.Empty<LayoutSummary>();
    private TopologySnapshot _topology = TopologySnapshot.Empty;
    private string _userId = string.Empty;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        _userId = auth.User.Identity?.Name ?? "unknown";

        // Sequential, never Task.WhenAll: both calls share the circuit's single AppDbContext,
        // and running them concurrently reproduces the "second operation" bug from ADR-6.
        _topology = await TopologyProvider.GetSnapshotAsync();
        _layouts = await LayoutService.ListAsync(_userId);

        _loading = false;
    }
}
```

- [ ] **Step 9: Wire the menu entry and hide the legacy dashboard**

In `Components/Layout/MainLayout.razor`:

1. Add to the `@using` block at the top:
```razor
@using BatteryTestingSystem.Components.Pages.Workflows
@using BatteryTestingSystem.Services.Implementations.Workflow
@using Microsoft.Extensions.Options
```

2. Add an inject beside the others:
```razor
@inject IOptions<WorkflowFeatureOptions> Features
```

3. Replace the `private List<NavMenuItem> _menuItems = new() { ... };` **field initializer** at
line 147 with an empty field plus construction in `OnInitialized`. Keep every existing entry
exactly as it is; only the `Display` entry becomes conditional and only the `Workflows` entry is
new:

```csharp
    private List<NavMenuItem> _menuItems = new();

    protected override void OnInitialized()
    {
        var features = Features.Value;

        if (features.LegacyDashboard)
        {
            _menuItems.Add(new NavMenuItem
            {
                Title = "Display",
                Url = "/",
                Icon = Lucide.Fullscreen,
                ComponentType = typeof(DashboardView),
                Unique = true
            });
        }

        if (features.WorkflowCanvas)
        {
            _menuItems.Add(new NavMenuItem
            {
                Title = "Workflows",
                Url = "/workflows",
                Icon = Lucide.Workflow,
                ComponentType = typeof(WorkflowCanvasPage),
                Unique = true,
                keepAlive = true        // rebuilding the graph on every tab switch is wasteful
            });
        }

        // ... then Add() each remaining existing entry (Circuits, Battery, Program, ...) in the
        // same order and with the same property values they already have.
    }
```

If `MainLayout` already overrides `OnInitialized` or `OnInitializedAsync`, put this code at the
top of the existing override rather than adding a second one.

4. In `Components/Layout/Navbar.razor:12`, the logo click currently navigates to `DashboardView`.
Make the target follow the flag. Add `[Parameter] public NavMenuItem? HomeTarget { get; set; }`
to `Navbar`, pass it from `MainLayout` (`HomeTarget="@_menuItems.FirstOrDefault()"`), and change
the logo's `@onclick` to `@(() => Navigate(HomeTarget ?? _fallbackHome))` where `_fallbackHome`
is the Circuits entry. With `LegacyDashboard=false`, the first menu item is Workflows, so the
logo lands there.

- [ ] **Step 10: Build and verify in the browser**

```bash
dotnet build
dotnet run
```

Verify by hand:
1. The top navigation shows **Workflows** and does **not** show **Display**.
2. Clicking Workflows opens a tab showing the toolbar with a layout count and device count.
3. Clicking the logo lands on Workflows, not a blank page.
4. Navigating directly to `/Dashboard` still renders the old dashboard — it is hidden, not deleted.
5. Set `"LegacyDashboard": true` in `appsettings.Development.json`, restart, and confirm
   **Display** returns and still works. Set it back to `false`.
6. The browser console shows no errors.

- [ ] **Step 11: Commit**

```bash
git add Services/Implementations/Workflow/ Components/Pages/Workflows/ Extensions/ServiceCollectionExtensions.cs Program.cs appsettings.json appsettings.Development.json Components/Layout/MainLayout.razor Components/Layout/Navbar.razor BatteryTestingSystem.Tests/Services/Workflow/WorkflowFeatureOptionsTests.cs
git commit -m "feat(workflow-canvas): feature flags, Workflows menu entry and page shell"
```

---

### Task 8: The stylesheet and the pan/zoom JS module

**Files:**
- Create: `wwwroot/css/workflow-canvas.css`
- Create: `wwwroot/js/workflow-canvas.js`
- Modify: `Components/App.razor` — one `<link>` after line 10, one `<script>` after line 26
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — initialise and dispose the module

**Interfaces:**
- Consumes: the page shell from Task 7.
- Produces the JS surface every later task calls, on `window.workflowCanvas`:
  - `init(hostElement, dotNetRef, options) -> void` — options `{ snap: number, minZoom: number, maxZoom: number }`
  - `dispose(hostElement) -> void`
  - `fitToContent(hostElement) -> void`
  - `setSnap(hostElement, snapPx) -> void`
  - `getViewport(hostElement) -> { panX, panY, zoom }`
  - `setViewport(hostElement, panX, panY, zoom) -> void`
  - `applyTelemetry(hostElement, entries) -> void` (implemented in Task 15; a no-op stub here)
- .NET callbacks the module invokes on `dotNetRef` (added in Task 11; declared now so the contract is fixed): `OnNodeMoved(string nodeId, double x, double y)`, `OnConnect(string fromNodeId, string toNodeId)`, `OnSelect(string? nodeId)`, `OnViewportChanged(double panX, double panY, double zoom)`.

There is no unit test here — this is CSS and DOM interaction. The gate is the browser check in
Step 5.

- [ ] **Step 1: Write the stylesheet**

Create `wwwroot/css/workflow-canvas.css`. Every colour is a theme variable; no hex literals.

```css
/* Workflow canvas (experimental).
   Loaded only on the Workflows tab. Written by hand because app.min.css has no rebuild
   pipeline (ADR-2), so new Tailwind utilities are unavailable.
   Theme vars are raw HSL triplets, e.g. --status-charge: 51 100% 50%, which is why every
   colour below is written as hsl(var(--x) / alpha). */

.wf-shell {
    display: flex;
    flex-direction: column;
    height: calc(100vh - 8rem);
    min-height: 32rem;
    color: hsl(var(--foreground));
    background: hsl(var(--background));
}

.wf-toolbar {
    display: flex;
    align-items: center;
    gap: 1rem;
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid hsl(var(--border));
    flex: 0 0 auto;
}

.wf-body {
    display: flex;
    flex: 1 1 auto;
    min-height: 0;      /* without this the flex children refuse to scroll */
}

.wf-dock,
.wf-palette {
    flex: 0 0 16rem;
    overflow-y: auto;
    padding: 0.75rem;
    background: hsl(var(--card));
    transition: flex-basis 150ms ease;
}

.wf-dock { border-right: 1px solid hsl(var(--border)); }
.wf-palette { border-left: 1px solid hsl(var(--border)); }

.wf-dock--collapsed,
.wf-palette--collapsed { flex-basis: 2.5rem; overflow: hidden; }

/* ---------------------------------------------------------------- canvas viewport */

.wf-canvas-host {
    position: relative;
    flex: 1 1 auto;
    overflow: hidden;
    min-width: 0;
    cursor: grab;
    background-color: hsl(var(--background));
    /* Grid dots. background-position is updated by JS as the canvas pans, so the grid
       appears to move with the content without re-rendering anything. */
    background-image: radial-gradient(hsl(var(--border)) 1px, transparent 1px);
    background-size: 24px 24px;
}

.wf-canvas-host.wf-panning { cursor: grabbing; }

/* The single transformed layer. Pan and zoom change ONE transform here, never the
   individual nodes, so the browser can composite the whole scene on the GPU. */
.wf-world {
    position: absolute;
    top: 0;
    left: 0;
    transform-origin: 0 0;
    will-change: transform;
}

/* ---------------------------------------------------------------- nodes */

.wf-node {
    position: absolute;
    box-sizing: border-box;
    width: 200px;
    border: 1px solid hsl(var(--border));
    border-radius: 0.5rem;
    background: hsl(var(--card));
    color: hsl(var(--card-foreground));
    font-size: 0.8125rem;
    user-select: none;
    /* Position is a transform, not top/left, so dragging never triggers layout. */
    will-change: transform;
}

.wf-node__header {
    display: flex;
    align-items: center;
    gap: 0.375rem;
    padding: 0.375rem 0.5rem;
    border-bottom: 1px solid hsl(var(--border));
    cursor: move;
    font-weight: 600;
}

.wf-node__body { padding: 0.5rem; display: grid; gap: 0.25rem; }

.wf-node--selected { border-color: hsl(var(--primary)); }

.wf-node--selected::after {
    content: "";
    position: absolute;
    inset: -3px;
    border-radius: 0.625rem;
    border: 2px solid hsl(var(--primary) / 0.6);
    pointer-events: none;
}

/* A node whose EntityId no longer resolves. Dimmed, never removed. */
.wf-node--stale { opacity: 0.45; border-style: dashed; }

.wf-node--device { width: 240px; }
.wf-node--battery { width: 140px; }

/* Connection ports */
.wf-port {
    position: absolute;
    top: 50%;
    width: 12px;
    height: 12px;
    margin-top: -6px;
    border-radius: 9999px;
    border: 2px solid hsl(var(--border));
    background: hsl(var(--card));
    cursor: crosshair;
}

.wf-port--out { right: -7px; }
.wf-port--in { left: -7px; }
.wf-port:hover { border-color: hsl(var(--primary)); }

/* ---------------------------------------------------------------- edges */

.wf-edge-layer {
    position: absolute;
    top: 0;
    left: 0;
    overflow: visible;
    pointer-events: none;
}

.wf-edge {
    fill: none;
    stroke: hsl(var(--border));
    stroke-width: 2;
}

.wf-edge--power { stroke-width: 3; }

/* ---------------------------------------------------------------- ghost connection */

.wf-ghost-edge {
    fill: none;
    stroke: hsl(var(--primary) / 0.8);
    stroke-width: 2;
    stroke-dasharray: 4 4;
}
```

- [ ] **Step 2: Write the JS module**

Create `wwwroot/js/workflow-canvas.js`. Pan and zoom stay entirely here — no pointer event
reaches .NET.

```javascript
// Workflow canvas interaction layer (experimental).
//
// The contract with C#: this file owns everything transient — pan, zoom, drag-in-progress,
// hover. C# owns committed state. Only four things ever cross back: a node's final dropped
// position, a completed connection, a selection change, and the viewport after a pan/zoom.
//
// This is not an optimisation. Blazor Server sends every event over SignalR, so a pointermove
// handler in C# would be a network round-trip per frame.

(function () {
    "use strict";

    const states = new WeakMap();

    function stateOf(host) {
        return states.get(host);
    }

    function applyTransform(s) {
        s.world.style.transform =
            `translate(${s.panX}px, ${s.panY}px) scale(${s.zoom})`;
        // Keep the dot grid locked to the content.
        s.host.style.backgroundPosition = `${s.panX}px ${s.panY}px`;
        s.host.style.backgroundSize = `${24 * s.zoom}px ${24 * s.zoom}px`;
    }

    function notifyViewport(s) {
        if (!s.dotNet) return;
        s.dotNet.invokeMethodAsync("OnViewportChanged", s.panX, s.panY, s.zoom);
    }

    function onPointerDown(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;

        // Ports and node headers are handled in Task 11; ignore them here.
        if (e.target.closest(".wf-port") || e.target.closest(".wf-node__header")) return;

        if (e.target.closest(".wf-node")) return;   // clicking a node is not a pan

        s.panning = true;
        s.startX = e.clientX - s.panX;
        s.startY = e.clientY - s.panY;
        s.host.classList.add("wf-panning");
        s.host.setPointerCapture(e.pointerId);
    }

    function onPointerMove(e) {
        const s = stateOf(e.currentTarget);
        if (!s || !s.panning) return;

        s.panX = e.clientX - s.startX;
        s.panY = e.clientY - s.startY;
        applyTransform(s);          // no .NET call — this is the whole point
    }

    function onPointerUp(e) {
        const s = stateOf(e.currentTarget);
        if (!s || !s.panning) return;

        s.panning = false;
        s.host.classList.remove("wf-panning");
        try { s.host.releasePointerCapture(e.pointerId); } catch { /* already released */ }
        notifyViewport(s);          // one call, on release
    }

    function onWheel(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;

        e.preventDefault();

        const rect = s.host.getBoundingClientRect();
        const px = e.clientX - rect.left;
        const py = e.clientY - rect.top;

        const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
        const next = Math.min(s.maxZoom, Math.max(s.minZoom, s.zoom * factor));
        if (next === s.zoom) return;

        // Keep the point under the cursor stationary while zooming.
        s.panX = px - ((px - s.panX) * next) / s.zoom;
        s.panY = py - ((py - s.panY) * next) / s.zoom;
        s.zoom = next;

        applyTransform(s);

        clearTimeout(s.wheelTimer);
        s.wheelTimer = setTimeout(() => notifyViewport(s), 200);  // debounce the commit
    }

    window.workflowCanvas = {
        init(host, dotNetRef, options) {
            if (!host || states.has(host)) return;

            const world = host.querySelector(".wf-world");
            if (!world) {
                console.error("[workflowCanvas] host has no .wf-world child");
                return;
            }

            const opts = options || {};
            const s = {
                host, world,
                dotNet: dotNetRef,
                panX: 0, panY: 0, zoom: 1,
                panning: false, startX: 0, startY: 0,
                snap: opts.snap || 0,
                minZoom: opts.minZoom || 0.2,
                maxZoom: opts.maxZoom || 2.5,
                wheelTimer: 0,
                handlers: {},
            };

            s.handlers.down = onPointerDown;
            s.handlers.move = onPointerMove;
            s.handlers.up = onPointerUp;
            s.handlers.wheel = onWheel;

            host.addEventListener("pointerdown", s.handlers.down);
            host.addEventListener("pointermove", s.handlers.move);
            host.addEventListener("pointerup", s.handlers.up);
            host.addEventListener("pointercancel", s.handlers.up);
            host.addEventListener("wheel", s.handlers.wheel, { passive: false });

            states.set(host, s);
            applyTransform(s);
        },

        dispose(host) {
            const s = stateOf(host);
            if (!s) return;

            host.removeEventListener("pointerdown", s.handlers.down);
            host.removeEventListener("pointermove", s.handlers.move);
            host.removeEventListener("pointerup", s.handlers.up);
            host.removeEventListener("pointercancel", s.handlers.up);
            host.removeEventListener("wheel", s.handlers.wheel);

            clearTimeout(s.wheelTimer);
            states.delete(host);
        },

        setSnap(host, snapPx) {
            const s = stateOf(host);
            if (s) s.snap = snapPx || 0;
        },

        getViewport(host) {
            const s = stateOf(host);
            return s ? { panX: s.panX, panY: s.panY, zoom: s.zoom } : { panX: 0, panY: 0, zoom: 1 };
        },

        setViewport(host, panX, panY, zoom) {
            const s = stateOf(host);
            if (!s) return;
            s.panX = panX;
            s.panY = panY;
            s.zoom = Math.min(s.maxZoom, Math.max(s.minZoom, zoom || 1));
            applyTransform(s);
        },

        fitToContent(host) {
            const s = stateOf(host);
            if (!s) return;

            const nodes = s.world.querySelectorAll(".wf-node");
            if (nodes.length === 0) {
                this.setViewport(host, 0, 0, 1);
                return;
            }

            // Measure in world coordinates by reading the inline transform we set ourselves,
            // rather than getBoundingClientRect, which is already zoomed.
            let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
            nodes.forEach(n => {
                const x = parseFloat(n.dataset.x || "0");
                const y = parseFloat(n.dataset.y || "0");
                minX = Math.min(minX, x);
                minY = Math.min(minY, y);
                maxX = Math.max(maxX, x + n.offsetWidth);
                maxY = Math.max(maxY, y + n.offsetHeight);
            });

            const pad = 40;
            const rect = s.host.getBoundingClientRect();
            const zoom = Math.min(
                s.maxZoom,
                Math.max(s.minZoom,
                    Math.min(rect.width / (maxX - minX + pad * 2),
                             rect.height / (maxY - minY + pad * 2))));

            this.setViewport(host, pad - minX * zoom, pad - minY * zoom, zoom);
            notifyViewport(stateOf(host));
        },

        // Implemented in Task 15. Declared now so the surface is stable.
        applyTelemetry(_host, _entries) { },
    };
})();
```

- [ ] **Step 3: Register the assets**

In `Components/App.razor`, after the `app.min.css` link (line 9):

```html
    <link rel="stylesheet" href="css/workflow-canvas.css?version=0.1" />
```

and after the last `<script>` in the body block (after `alarm-sound.js`, line 26):

```html
    <script src="js/workflow-canvas.js?version=0.1"></script>
```

Bump the `?version=` query on every later change to this file, matching the convention already
used by the other scripts — browsers cache these aggressively and a stale copy produces
symptoms that look exactly like broken C#.

- [ ] **Step 4: Initialise from the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, add `@implements IAsyncDisposable` and
`@inject IJSRuntime JS`, wrap the canvas host in the required markup, and manage the lifecycle:

```razor
<div class="wf-canvas-host" @ref="_canvasHost">
    <div class="wf-world">
        <p class="opacity-60 p-4">Canvas goes here.</p>
    </div>
</div>
```

```csharp
    private ElementReference _canvasHost;
    private DotNetObjectReference<WorkflowCanvasPage>? _selfRef;
    private bool _jsReady;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _selfRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("workflowCanvas.init", _canvasHost, _selfRef,
            new { snap = 24, minZoom = 0.2, maxZoom = 2.5 });
        _jsReady = true;
    }

    [JSInvokable]
    public Task OnViewportChanged(double panX, double panY, double zoom)
    {
        // Stored, not rendered: the viewport is saved with the layout but changing it must never
        // cause a re-render, or panning would round-trip to the server.
        _viewport = new CanvasViewport(panX, panY, zoom);
        return Task.CompletedTask;
    }

    private CanvasViewport _viewport = CanvasViewport.Default;

    public async ValueTask DisposeAsync()
    {
        if (_jsReady)
        {
            try { await JS.InvokeVoidAsync("workflowCanvas.dispose", _canvasHost); }
            catch (JSDisconnectedException) { /* circuit already gone */ }
        }
        _selfRef?.Dispose();
    }
```

Add `@using Microsoft.JSInterop` to the page.

- [ ] **Step 5: Build and verify in the browser**

```bash
dotnet build
dotnet run
```

Verify by hand on the Workflows tab:
1. The dot grid renders and the three panes are laid out left/centre/right.
2. Dragging on empty canvas pans; the grid moves with it; the cursor becomes a grabbing hand.
3. The mouse wheel zooms toward the cursor, and the point under the cursor stays put.
4. Zoom stops at roughly 0.2x and 2.5x.
5. Switch to another tab and back — no console errors, panning still works (`dispose` then
   `init` ran cleanly).
6. Toggle the app's dark/light theme — the canvas background, border and grid all follow it.
7. The console shows no `[workflowCanvas]` errors.

- [ ] **Step 6: Commit**

```bash
git add wwwroot/css/workflow-canvas.css wwwroot/js/workflow-canvas.js Components/App.razor Components/Pages/Workflows/WorkflowCanvasPage.razor
git commit -m "feat(workflow-canvas): stylesheet and pan/zoom interaction module"
```

---

### Task 9: Node components and the status-to-CSS mapping

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowStatusCss.cs`
- Create: `Components/UI/WorkflowCanvas/CanvasNode.razor`
- Create: `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor`
- Create: `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`
- Create: `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`
- Create: `Components/UI/WorkflowCanvas/Nodes/BatteryNode.razor`
- Create: `Components/UI/WorkflowCanvas/CanvasSurface.razor`
- Modify: `wwwroot/css/workflow-canvas.css` — append the status-colour rules
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — render `CanvasSurface`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowStatusCssTests.cs`
- Test: `BatteryTestingSystem.Tests/Components/WorkflowNodeTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowNode`, `NodeKind` (Task 1); `WorkflowAutoLayout.NodeWidth/NodeHeight` (Task 4); `CircuitStatus` (existing, `Models/Enums/CircuitEnums.cs`).
- Produces:
  - `WorkflowStatusCss.Name(CircuitStatus) -> string` — the CSS token, e.g. `"continue"`
  - `WorkflowStatusCss.Var(CircuitStatus) -> string` — e.g. `"--status-continue"`
  - `CanvasNode` component, parameters: `WorkflowNode Node`, `bool IsSelected`, `bool IsStale`, `string Title`, `RenderFragment ChildContent`, `bool ShowInPort`, `bool ShowOutPort`
  - `CanvasSurface` component, parameters: `WorkflowGraph Graph`, `IReadOnlyCollection<string> StaleNodeIds`, `string? SelectedNodeId`, `Func<WorkflowNode, string> LabelFor`, `Func<int, string> ProgramLabel`, `Func<int, string> DbcLabel`
  - Every node renders `data-node-id`, `data-x`, `data-y` attributes, which `workflow-canvas.js` reads

**The trap this task exists to close:** `CircuitStatus.Countinue` is misspelled in the source enum
but the CSS variable is `--status-continue`. `status.ToString().ToLower()` yields `countinue`,
which matches no variable, so the node renders unstyled with no error anywhere.

- [ ] **Step 1: Write the failing status-mapping test**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowStatusCssTests.cs`:

```csharp
using System;
using System.Linq;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// CircuitStatus.Countinue is misspelled in the enum while the CSS variable is
/// --status-continue. Deriving the name with ToString().ToLower() produces "countinue", which
/// matches nothing and renders an unstyled node with no error in the console or the build.
/// This mapping is the only place that discrepancy is allowed to exist.
/// </summary>
public class WorkflowStatusCssTests
{
    [Theory]
    [InlineData(CircuitStatus.Idle, "idle")]
    [InlineData(CircuitStatus.Charge, "charge")]
    [InlineData(CircuitStatus.Discharging, "discharging")]
    [InlineData(CircuitStatus.Pause, "pause")]
    [InlineData(CircuitStatus.Countinue, "continue")]     // the misspelling, corrected
    [InlineData(CircuitStatus.Interrupt, "interrupt")]
    [InlineData(CircuitStatus.Error, "error")]
    [InlineData(CircuitStatus.Msg, "msg")]
    [InlineData(CircuitStatus.Offline, "offline")]
    public void Name_MapsEveryStatusToItsCssToken(CircuitStatus status, string expected)
    {
        Assert.Equal(expected, WorkflowStatusCss.Name(status));
    }

    [Fact]
    public void Var_WrapsTheTokenInTheThemeVariableName()
    {
        Assert.Equal("--status-continue", WorkflowStatusCss.Var(CircuitStatus.Countinue));
    }

    [Fact]
    public void EveryEnumMemberIsMapped_SoANewStatusCannotSlipThroughUnstyled()
    {
        // If someone adds a status to the enum without adding it here, this fails loudly
        // instead of shipping a node with no colour.
        foreach (CircuitStatus status in Enum.GetValues<CircuitStatus>())
        {
            var name = WorkflowStatusCss.Name(status);
            Assert.False(string.IsNullOrWhiteSpace(name), $"{status} has no CSS token");
        }
    }

    [Fact]
    public void EveryMappedTokenExistsInTheStylesheet()
    {
        // Guards the other half of the pairing: a token with no matching CSS variable is just
        // as invisible as no token at all. Reads app.css because that is where the theme vars
        // are declared (both files carry the same set - ADR-2).
        var cssPath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot", "css", "app.css");

        if (!System.IO.File.Exists(cssPath)) return;   // skip if the layout differs on CI

        var css = System.IO.File.ReadAllText(cssPath);

        foreach (CircuitStatus status in Enum.GetValues<CircuitStatus>())
        {
            var variable = WorkflowStatusCss.Var(status);
            Assert.True(css.Contains(variable + ":"), $"{variable} is not declared in app.css");
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowStatusCssTests`
Expected: FAIL — `WorkflowStatusCss` does not exist.

- [ ] **Step 3: Write the mapping**

Create `Services/Implementations/Workflow/WorkflowStatusCss.cs`:

```csharp
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// CircuitStatus to CSS token. Never call status.ToString().ToLower() instead of this:
/// CircuitStatus.Countinue is misspelled in the enum while the theme variable is
/// --status-continue, so the naive version silently produces an unstyled node.
/// </summary>
public static class WorkflowStatusCss
{
    public static string Name(CircuitStatus status) => status switch
    {
        CircuitStatus.Idle => "idle",
        CircuitStatus.Charge => "charge",
        CircuitStatus.Discharging => "discharging",
        CircuitStatus.Pause => "pause",
        CircuitStatus.Countinue => "continue",      // enum typo, CSS variable is correct
        CircuitStatus.Interrupt => "interrupt",
        CircuitStatus.Error => "error",
        CircuitStatus.Msg => "msg",
        CircuitStatus.Offline => "offline",
        _ => "idle",
    };

    public static string Var(CircuitStatus status) => $"--status-{Name(status)}";
}
```

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowStatusCssTests`
Expected: PASS — 12 test cases.

- [ ] **Step 5: Write the shared node chrome**

Create `Components/UI/WorkflowCanvas/CanvasNode.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

@* Shared chrome for every node kind: absolute positioning, selection ring, ports, drag handle.
   Position is applied as a transform rather than top/left so that dragging a node never causes
   the browser to run layout. data-node-id / data-x / data-y are read by workflow-canvas.js. *@

<div class="wf-node wf-node--@Node.Kind.ToString().ToLowerInvariant() @(IsSelected ? "wf-node--selected" : "") @(IsStale ? "wf-node--stale" : "")"
     style="transform: translate(@(Node.X.ToString(Culture))px, @(Node.Y.ToString(Culture))px);"
     data-node-id="@Node.Id"
     data-x="@Node.X.ToString(Culture)"
     data-y="@Node.Y.ToString(Culture)">

    <div class="wf-node__header">
        @if (IsStale)
        {
            <span title="This hardware no longer exists in the database">&#9888;</span>
        }
        <span class="truncate">@Title</span>
    </div>

    <div class="wf-node__body">
        @ChildContent
    </div>

    @if (ShowInPort)
    {
        <div class="wf-port wf-port--in" data-port="in" data-node-id="@Node.Id"></div>
    }
    @if (ShowOutPort)
    {
        <div class="wf-port wf-port--out" data-port="out" data-node-id="@Node.Id"></div>
    }
</div>

@code {
    /// <summary>Invariant culture matters: a comma decimal separator produces an invalid CSS
    /// transform on a machine with e.g. a German locale, and the node silently stacks at 0,0.</summary>
    private static readonly System.Globalization.CultureInfo Culture =
        System.Globalization.CultureInfo.InvariantCulture;

    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public bool ShowInPort { get; set; } = true;
    [Parameter] public bool ShowOutPort { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 6: Write the four node bodies**

Create `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale"
            ShowInPort="false" ShowOutPort="true">
    <div class="flex items-center justify-between">
        <span class="opacity-60">Device</span>
        <span>@(IsOnline ? "online" : "offline")</span>
    </div>
    <div class="opacity-60">@BoardCount board(s)</div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Device";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public bool IsOnline { get; set; }
    [Parameter] public int BoardCount { get; set; }
}
```

Create `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale">
    <div class="opacity-60">@ChannelCount channel(s) placed</div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Board";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public int ChannelCount { get; set; }
}
```

Create `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

@* Voltage, current and status are placeholders until Task 15 — the telemetry bridge writes them
   directly into the DOM as CSS variables and text, never through a Blazor re-render. The
   data-role attributes below are how it finds them. *@

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale">
    <div class="flex items-center justify-between">
        <span class="wf-status-pill" data-role="status">@InitialStatusText</span>
        <span class="opacity-60" data-role="soc">--%</span>
    </div>
    <div class="flex items-center justify-between opacity-80">
        <span data-role="voltage">-- V</span>
        <span data-role="current">-- A</span>
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
}
```

Create `Components/UI/WorkflowCanvas/Nodes/BatteryNode.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

@* BatteryGlyph arrives in Task 14; this is a plain placeholder so the node exists and can be
   positioned, selected and wired first. *@

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale"
            ShowInPort="false" ShowOutPort="true">
    <div class="opacity-60" data-role="soc">--%</div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Battery";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
}
```

- [ ] **Step 7: Write the surface**

Create `Components/UI/WorkflowCanvas/CanvasSurface.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

@* Renders every node inside .wf-world. The edge layer is added in Task 10.
   This component re-renders only on STRUCTURAL change — a node added, removed or selected.
   Telemetry never comes through here (spec 6). *@

<div class="wf-world">
    @foreach (var node in Graph.Nodes)
    {
        var stale = StaleNodeIds.Contains(node.Id);
        var selected = node.Id == SelectedNodeId;

        switch (node.Kind)
        {
            case NodeKind.Device:
                <DeviceNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                            IsSelected="selected" IsStale="stale"
                            BoardCount="@CountChildren(node.Id)" />
                break;

            case NodeKind.Board:
                <BoardNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                           IsSelected="selected" IsStale="stale"
                           ChannelCount="@CountChildren(node.Id)" />
                break;

            case NodeKind.Channel:
                <ChannelNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                             IsSelected="selected" IsStale="stale"
                             ProgramName="@(node.Attach?.ProgramId is { } p ? ProgramLabel(p) : null)"
                             DbcName="@(node.Attach?.DbcFileId is { } d ? DbcLabel(d) : null)" />
                break;

            case NodeKind.Battery:
                <BatteryNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                             IsSelected="selected" IsStale="stale" />
                break;
        }
    }
</div>

@code {
    [Parameter, EditorRequired] public WorkflowGraph Graph { get; set; } = WorkflowGraph.Empty;
    [Parameter] public IReadOnlyCollection<string> StaleNodeIds { get; set; } = Array.Empty<string>();
    [Parameter] public string? SelectedNodeId { get; set; }

    /// <summary>Human label for a node. Supplied by the page, which owns the lookup tables.</summary>
    [Parameter] public Func<WorkflowNode, string> LabelFor { get; set; } = n => n.Id;
    [Parameter] public Func<int, string> ProgramLabel { get; set; } = id => $"Program {id}";
    [Parameter] public Func<int, string> DbcLabel { get; set; } = id => $"DBC {id}";

    private int CountChildren(string nodeId) =>
        Graph.Edges.Count(e => e.FromNodeId == nodeId && e.Kind == EdgeKind.Topology);
}
```

- [ ] **Step 8: Append the node styles**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- status + badges */

.wf-status-pill {
    display: inline-block;
    padding: 0 0.375rem;
    border-radius: 9999px;
    font-size: 0.6875rem;
    text-transform: uppercase;
    letter-spacing: 0.02em;
    /* --wf-status is written by the telemetry bridge (Task 15); the fallback keeps the pill
       readable before the first tick arrives. */
    background: hsl(var(--wf-status, var(--status-idle)) / 0.18);
    color: hsl(var(--wf-status, var(--status-idle)));
}

.wf-badge {
    display: inline-block;
    max-width: 100%;
    padding: 0 0.3125rem;
    border-radius: 0.25rem;
    font-size: 0.6875rem;
    background: hsl(var(--muted));
    color: hsl(var(--muted-foreground));
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
```

- [ ] **Step 9: Write the bUnit render tests**

Create `BatteryTestingSystem.Tests/Components/WorkflowNodeTests.cs`:

```csharp
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// bUnit, following the ChannelFilterTests precedent. These pin the DOM contract the JS layer
/// depends on: data-node-id, data-x and data-y are how workflow-canvas.js finds and measures
/// nodes, so renaming them would break dragging with no compiler error.
/// </summary>
public class WorkflowNodeTests : TestContext
{
    private static WorkflowNode Node(string id = "chn-1", NodeKind kind = NodeKind.Channel) =>
        new(id, kind, 1, 120, 240, null);

    [Fact]
    public void CanvasNode_EmitsTheDataAttributesTheJsLayerReads()
    {
        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "Ch 1"));

        var el = cut.Find(".wf-node");
        Assert.Equal("chn-1", el.GetAttribute("data-node-id"));
        Assert.Equal("120", el.GetAttribute("data-x"));
        Assert.Equal("240", el.GetAttribute("data-y"));
    }

    [Fact]
    public void CanvasNode_PositionsWithATransform_NotTopLeft()
    {
        // top/left would force a layout pass on every drag frame.
        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "Ch 1"));

        var style = cut.Find(".wf-node").GetAttribute("style") ?? "";
        Assert.Contains("translate(120px, 240px)", style);
        Assert.DoesNotContain("left:", style);
    }

    [Fact]
    public void CanvasNode_MarksAStaleNode_AndWarnsInTheHeader()
    {
        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "Ch 1")
            .Add(x => x.IsStale, true));

        Assert.Contains("wf-node--stale", cut.Find(".wf-node").ClassName);
        Assert.Contains("no longer exists", cut.Markup);
    }

    [Fact]
    public void CanvasNode_AddsTheSelectionClassOnlyWhenSelected()
    {
        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "Ch 1"));

        Assert.DoesNotContain("wf-node--selected", cut.Find(".wf-node").ClassName);

        cut.SetParametersAndRender(p => p.Add(x => x.IsSelected, true));

        Assert.Contains("wf-node--selected", cut.Find(".wf-node").ClassName);
    }

    [Fact]
    public void ChannelNode_ShowsProgramAndDbcBadgesOnlyWhenAttached()
    {
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "Ch 1"));

        Assert.Empty(cut.FindAll(".wf-badge"));

        cut.SetParametersAndRender(p => p
            .Add(x => x.ProgramName, "Cycle-A")
            .Add(x => x.DbcName, "pack.dbc"));

        Assert.Equal(2, cut.FindAll(".wf-badge").Count);
        Assert.Contains("Cycle-A", cut.Markup);
    }

    [Fact]
    public void ChannelNode_EmitsTheDataRoleHooksTheTelemetryBridgeTargets()
    {
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "Ch 1"));

        Assert.NotNull(cut.Find("[data-role='status']"));
        Assert.NotNull(cut.Find("[data-role='voltage']"));
        Assert.NotNull(cut.Find("[data-role='current']"));
        Assert.NotNull(cut.Find("[data-role='soc']"));
    }

    [Fact]
    public void CanvasSurface_RendersOneElementPerNode()
    {
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new()
            {
                Node("dev-1", NodeKind.Device),
                Node("brd-1", NodeKind.Board),
                Node("chn-1", NodeKind.Channel),
                Node("bat-1", NodeKind.Battery),
            }
        };

        var cut = RenderComponent<CanvasSurface>(p => p.Add(x => x.Graph, graph));

        Assert.Equal(4, cut.FindAll(".wf-node").Count);
    }

    [Fact]
    public void CanvasSurface_MarksOnlyTheStaleNodes()
    {
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new() { Node("chn-1"), Node("chn-2") }
        };

        var cut = RenderComponent<CanvasSurface>(p => p
            .Add(x => x.Graph, graph)
            .Add(x => x.StaleNodeIds, new[] { "chn-2" }));

        Assert.Single(cut.FindAll(".wf-node--stale"));
    }
}
```

- [ ] **Step 10: Run the component tests**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowNodeTests`
Expected: PASS — 8 tests.

If bUnit cannot resolve the components, confirm `BatteryTestingSystem.Tests.csproj` references
the web project and that `_Imports.razor` in `Components/UI/WorkflowCanvas/` (create one if the
folder needs it) brings in `BatteryTestingSystem.Components.UI.WorkflowCanvas`.

- [ ] **Step 11: Render the surface from the page**

In `WorkflowCanvasPage.razor`, replace the placeholder `<div class="wf-world">…</div>` with:

```razor
<CanvasSurface Graph="_graph"
               StaleNodeIds="_staleNodeIds"
               SelectedNodeId="_selectedNodeId"
               LabelFor="LabelFor" />
```

and add the backing state plus a temporary way to see something:

```csharp
    private WorkflowGraph _graph = WorkflowGraph.Empty;
    private IReadOnlyList<string> _staleNodeIds = Array.Empty<string>();
    private string? _selectedNodeId;

    private string LabelFor(WorkflowNode node) => node.Kind switch
    {
        NodeKind.Device => _topology.Devices
            .FirstOrDefault(d => d.DeviceId == node.EntityId)?.DeviceName ?? $"Device {node.EntityId}",
        NodeKind.Board => $"Board {node.EntityId}",
        NodeKind.Channel => $"Channel {node.EntityId}",
        NodeKind.Battery => "Battery",
        _ => node.Id,
    };

    /// <summary>Temporary: proves the surface renders. Replaced by the real palette in Task 12.</summary>
    private void LoadFirstDeviceForPreview()
    {
        var device = _topology.Devices.FirstOrDefault();
        if (device is null) return;

        _graph = WorkflowAutoLayout.Apply(WorkflowGraphBuilder.BuildForDevice(device));
    }
```

Call `LoadFirstDeviceForPreview()` at the end of `OnInitializedAsync`, and add a toolbar button
`<button class="wf-btn" @onclick="LoadFirstDeviceForPreview">Preview first device</button>`.

- [ ] **Step 12: Build and verify in the browser**

```bash
dotnet build
dotnet run
```

Verify by hand with the simulator running (`python HardwareSimulator/run_sim.py -d 1 -n 8`):
1. The Workflows tab shows device, board and channel nodes in three columns.
2. Nodes do not overlap; channels stack vertically.
3. Panning and zooming move all the nodes together as one scene.
4. Node text, borders and background follow the light/dark theme.
5. Ports are visible on the left and right edges of the nodes.
6. No console errors.

- [ ] **Step 13: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowStatusCss.cs Components/UI/WorkflowCanvas/ Components/Pages/Workflows/ wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/
git commit -m "feat(workflow-canvas): node components, canvas surface and status CSS mapping"
```

---

### Task 10: Edge geometry and the SVG edge layer

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowEdgeGeometry.cs`
- Create: `Components/UI/WorkflowCanvas/EdgeLayer.razor`
- Modify: `Components/UI/WorkflowCanvas/CanvasSurface.razor` — render the layer beneath the nodes
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowEdgeGeometryTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowEdge`, `WorkflowNode`, `EdgeKind` (Task 1); `WorkflowAutoLayout.NodeWidth` (Task 4).
- Produces:
  - `record EdgeEndpoints(double X1, double Y1, double X2, double Y2)`
  - `WorkflowEdgeGeometry.PortPoints(WorkflowNode from, WorkflowNode to, double fromWidth, double fromHeight, double toHeight) -> EdgeEndpoints`
  - `WorkflowEdgeGeometry.BezierPath(EdgeEndpoints) -> string` — an SVG `d` attribute
  - `WorkflowEdgeGeometry.NodeHeightFor(NodeKind) -> double`, `NodeWidthFor(NodeKind) -> double`
  - `EdgeLayer` component, parameters `WorkflowGraph Graph`, `IReadOnlyCollection<string> StaleNodeIds`

One `<svg>` holds every edge. One `<svg>` per edge would create hundreds of stacking contexts and
make the whole scene repaint on any change.

- [ ] **Step 1: Write the failing geometry tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowEdgeGeometryTests.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Path maths is the one part of the rendering that can be tested without a browser, and it is
/// also where an invariant-culture slip silently produces "M0,0 C1,5..." with commas in the
/// numbers and an invisible edge.
/// </summary>
public class WorkflowEdgeGeometryTests
{
    private static WorkflowNode At(double x, double y, NodeKind kind = NodeKind.Channel) =>
        new("n", kind, 1, x, y, null);

    [Fact]
    public void PortPoints_LeavesTheRightEdgeOfTheSourceAndEntersTheLeftEdgeOfTheTarget()
    {
        var p = WorkflowEdgeGeometry.PortPoints(
            At(0, 0), At(300, 100), fromWidth: 200, fromHeight: 96, toHeight: 96);

        Assert.Equal(200, p.X1);    // right edge of a node at x=0, width 200
        Assert.Equal(48, p.Y1);     // vertical centre
        Assert.Equal(300, p.X2);    // left edge of the target
        Assert.Equal(148, p.Y2);    // target y + half its height
    }

    [Fact]
    public void BezierPath_ProducesAValidCubicPath()
    {
        var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(0, 0, 100, 50));

        Assert.StartsWith("M 0 0 C ", d);
        Assert.EndsWith(" 100 50", d);
    }

    [Fact]
    public void BezierPath_UsesInvariantCulture()
    {
        // On a de-DE machine "0.5" formats as "0,5", which makes the whole d attribute invalid
        // and the edge simply does not draw — with no error anywhere.
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture =
            new System.Globalization.CultureInfo("de-DE");
        try
        {
            var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(0.5, 1.5, 2.5, 3.5));
            Assert.DoesNotContain(",", d.Replace("C", ""));
            Assert.Contains("0.5", d);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void BezierPath_ControlPointsPullHorizontally_SoEdgesLeaveAndArriveFlat()
    {
        // Horizontal tangents are what make the graph read as left-to-right flow rather than
        // a bundle of diagonal lines.
        var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(0, 0, 200, 100));
        var parts = d.Replace("M ", "").Replace("C ", "").Split(' ',
            System.StringSplitOptions.RemoveEmptyEntries);

        // parts: x0 y0 c1x c1y c2x c2y x1 y1
        Assert.Equal("0", parts[1]);        // start y
        Assert.Equal(parts[1], parts[3]);   // first control point shares the start y
        Assert.Equal(parts[7], parts[5]);   // second control point shares the end y
    }

    [Fact]
    public void BezierPath_HandlesABackwardsEdgeWithoutCollapsing()
    {
        // The operator can drag a battery to the left of its channel; the curve must still bow
        // out rather than degenerate into a straight overlap.
        var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(300, 0, 0, 0));

        Assert.StartsWith("M 300 0 C ", d);
        Assert.Contains(" 0 0", d);
    }

    [Theory]
    [InlineData(NodeKind.Device, 240)]
    [InlineData(NodeKind.Board, 200)]
    [InlineData(NodeKind.Channel, 200)]
    [InlineData(NodeKind.Battery, 140)]
    public void NodeWidthFor_MatchesTheStylesheet(NodeKind kind, double expected)
    {
        // These must agree with .wf-node / .wf-node--device / .wf-node--battery in
        // workflow-canvas.css, or every edge attaches slightly off the node.
        Assert.Equal(expected, WorkflowEdgeGeometry.NodeWidthFor(kind));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowEdgeGeometryTests`
Expected: FAIL — `WorkflowEdgeGeometry` does not exist.

- [ ] **Step 3: Write the geometry**

Create `Services/Implementations/Workflow/WorkflowEdgeGeometry.cs`:

```csharp
using System.Globalization;
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record EdgeEndpoints(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Where an edge starts and ends, and the cubic path between them. Kept out of the Razor file so
/// the maths is testable — and so the invariant-culture formatting lives in exactly one place.
/// The widths here MUST match workflow-canvas.css; a test pins them.
/// </summary>
public static class WorkflowEdgeGeometry
{
    public static double NodeWidthFor(NodeKind kind) => kind switch
    {
        NodeKind.Device => 240,
        NodeKind.Battery => 140,
        _ => 200,
    };

    /// <summary>Approximate rendered height. Only the vertical centre matters, so being a few
    /// pixels out shifts the attachment point imperceptibly.</summary>
    public static double NodeHeightFor(NodeKind kind) => kind switch
    {
        NodeKind.Channel => 108,
        NodeKind.Battery => 72,
        _ => 88,
    };

    public static EdgeEndpoints PortPoints(
        WorkflowNode from, WorkflowNode to, double fromWidth, double fromHeight, double toHeight) =>
        new(from.X + fromWidth, from.Y + fromHeight / 2, to.X, to.Y + toHeight / 2);

    public static EdgeEndpoints PortPoints(WorkflowNode from, WorkflowNode to) =>
        PortPoints(from, to,
            NodeWidthFor(from.Kind), NodeHeightFor(from.Kind), NodeHeightFor(to.Kind));

    /// <summary>
    /// A cubic with horizontal tangents at both ends, so edges leave and arrive flat and the
    /// graph reads as left-to-right flow. The control offset grows with horizontal distance but
    /// never collapses, so a backwards edge still bows out instead of overlapping itself.
    /// </summary>
    public static string BezierPath(EdgeEndpoints p)
    {
        var dx = Math.Max(60, Math.Abs(p.X2 - p.X1) * 0.5);

        var c1x = p.X1 + dx;
        var c2x = p.X2 - dx;

        return string.Create(CultureInfo.InvariantCulture,
            $"M {p.X1} {p.Y1} C {c1x} {p.Y1} {c2x} {p.Y2} {p.X2} {p.Y2}");
    }
}
```

- [ ] **Step 4: Run to verify the tests pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowEdgeGeometryTests`
Expected: PASS — 9 test cases.

- [ ] **Step 5: Write the edge layer**

Create `Components/UI/WorkflowCanvas/EdgeLayer.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Services.Implementations.Workflow

@* ONE svg for every edge. One svg per edge would create a stacking context each and force the
   whole scene to repaint whenever any single edge changed.
   overflow:visible plus a zero-size viewport lets the paths extend anywhere in world space
   without the svg having to be resized as the graph grows. *@

<svg class="wf-edge-layer" width="1" height="1" aria-hidden="true">
    @foreach (var edge in Graph.Edges)
    {
        var from = Node(edge.FromNodeId);
        var to = Node(edge.ToNodeId);

        @* An edge can outlive its node for one render after a delete. Skip rather than throw. *@
        if (from is null || to is null) { continue; }

        var d = WorkflowEdgeGeometry.BezierPath(WorkflowEdgeGeometry.PortPoints(from, to));
        var stale = StaleNodeIds.Contains(from.Id) || StaleNodeIds.Contains(to.Id);

        <path class="wf-edge @(edge.Kind == EdgeKind.Power ? "wf-edge--power" : "") @(stale ? "wf-edge--stale" : "")"
              d="@d"
              data-edge-id="@edge.Id"
              data-from="@edge.FromNodeId"
              data-to="@edge.ToNodeId" />
    }
</svg>

@code {
    [Parameter, EditorRequired] public WorkflowGraph Graph { get; set; } = WorkflowGraph.Empty;
    [Parameter] public IReadOnlyCollection<string> StaleNodeIds { get; set; } = Array.Empty<string>();

    private WorkflowNode? Node(string id) => Graph.Nodes.FirstOrDefault(n => n.Id == id);
}
```

- [ ] **Step 6: Render it under the nodes**

In `CanvasSurface.razor`, put the edge layer first inside `.wf-world` so it paints beneath the
nodes:

```razor
<div class="wf-world">
    <EdgeLayer Graph="Graph" StaleNodeIds="StaleNodeIds" />

    @foreach (var node in Graph.Nodes)
    {
        ...unchanged...
    }
</div>
```

- [ ] **Step 7: Add the stale-edge style**

Append to `wwwroot/css/workflow-canvas.css`:

```css
.wf-edge--stale { opacity: 0.35; stroke-dasharray: 6 4; }
```

- [ ] **Step 8: Build and verify in the browser**

```bash
dotnet build
dotnet run
```

Verify with `Preview first device`:
1. Curved edges connect device to boards and boards to channels.
2. Edges attach at the vertical centre of each node's right and left edges.
3. Edges stay attached while panning and zooming.
4. Edges paint behind the nodes, not over them.
5. No console errors.

- [ ] **Step 9: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowEdgeGeometry.cs Components/UI/WorkflowCanvas/ wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/Services/Workflow/WorkflowEdgeGeometryTests.cs
git commit -m "feat(workflow-canvas): SVG edge layer with tested bezier geometry"
```

---

### Task 11: Node dragging, selection, and connecting

**Files:**
- Modify: `wwwroot/js/workflow-canvas.js` — drag, connect and select handlers
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — the four `[JSInvokable]` callbacks
- Modify: `wwwroot/css/workflow-canvas.css` — ghost-edge and drag styles
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphMutationsTests.cs`
- Create: `Services/Implementations/Workflow/WorkflowGraphMutations.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowEdge` (Task 1); `WorkflowGraphValidator`, `GraphRuleResult` (Task 2); `WorkflowGraphBuilder.EdgeId` (Task 3); the JS module (Task 8).
- Produces:
  - `WorkflowGraphMutations.MoveNode(WorkflowGraph, string nodeId, double x, double y) -> WorkflowGraph`
  - `WorkflowGraphMutations.AddEdge(WorkflowGraph, string fromId, string toId, EdgeKind) -> WorkflowGraph`
  - `WorkflowGraphMutations.AddNode(WorkflowGraph, WorkflowNode) -> WorkflowGraph` (used by Task 12)
  - `WorkflowGraphMutations.SetAttachment(WorkflowGraph, string nodeId, NodeAttachment?) -> WorkflowGraph` (used by Task 13)
  - `WorkflowGraphMutations.RemoveNode(WorkflowGraph, string nodeId) -> WorkflowGraph` (also drops attached edges)
  - `WorkflowGraphMutations.SnapToGrid(double value, int gridSize) -> double`
  - `WorkflowGraphMutations.EdgeKindFor(NodeKind from, NodeKind to) -> EdgeKind`
  - Page callbacks: `OnNodeMoved`, `OnConnect`, `OnSelect`, `OnViewportChanged`

- [ ] **Step 1: Write the failing mutation tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphMutationsTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Graph edits are pure functions returning a new graph, not in-place mutation, so the page can
/// swap state atomically and a failed validation leaves the previous graph completely untouched.
/// </summary>
public class WorkflowGraphMutationsTests
{
    private static WorkflowNode Node(string id, NodeKind kind = NodeKind.Channel) =>
        new(id, kind, 1, 0, 0, null);

    private static WorkflowGraph GraphWith(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    [Fact]
    public void MoveNode_UpdatesOnlyTheTargetsPosition()
    {
        var graph = GraphWith(Node("a"), Node("b"));

        var moved = WorkflowGraphMutations.MoveNode(graph, "a", 120, 240);

        Assert.Equal(120, moved.Nodes.Single(n => n.Id == "a").X);
        Assert.Equal(240, moved.Nodes.Single(n => n.Id == "a").Y);
        Assert.Equal(0, moved.Nodes.Single(n => n.Id == "b").X);
    }

    [Fact]
    public void MoveNode_IgnoresAnUnknownId_RatherThanThrowing()
    {
        // JS can report a move for a node deleted in the same instant.
        var graph = GraphWith(Node("a"));

        var moved = WorkflowGraphMutations.MoveNode(graph, "ghost", 5, 5);

        Assert.Equal(graph.Nodes, moved.Nodes);
    }

    [Fact]
    public void MoveNode_DoesNotMutateTheOriginalGraph()
    {
        var graph = GraphWith(Node("a"));

        WorkflowGraphMutations.MoveNode(graph, "a", 99, 99);

        Assert.Equal(0, graph.Nodes.Single().X);
    }

    [Theory]
    [InlineData(0, 24, 0)]
    [InlineData(11, 24, 0)]
    [InlineData(13, 24, 24)]
    [InlineData(36, 24, 48)]
    [InlineData(-11, 24, 0)]
    [InlineData(-13, 24, -24)]
    [InlineData(37, 0, 37)]      // grid 0 means snapping is off
    public void SnapToGrid_RoundsToTheNearestMultiple(double input, int grid, double expected)
    {
        Assert.Equal(expected, WorkflowGraphMutations.SnapToGrid(input, grid));
    }

    [Fact]
    public void AddEdge_AppendsAnEdgeWithTheBuilderIdConvention()
    {
        var graph = GraphWith(Node("a", NodeKind.Board), Node("b"));

        var result = WorkflowGraphMutations.AddEdge(graph, "a", "b", EdgeKind.Topology);

        Assert.Single(result.Edges);
        Assert.Equal("edge-a--b", result.Edges[0].Id);
        Assert.Equal(EdgeKind.Topology, result.Edges[0].Kind);
    }

    [Fact]
    public void RemoveNode_AlsoRemovesEveryEdgeTouchingIt()
    {
        // An edge referencing a deleted node would render as nothing and confuse the validator.
        var graph = GraphWith(Node("a"), Node("b"), Node("c")) with
        {
            Edges = new List<WorkflowEdge>
            {
                new("e1", "a", "b", EdgeKind.Topology),
                new("e2", "b", "c", EdgeKind.Topology),
            }
        };

        var result = WorkflowGraphMutations.RemoveNode(graph, "b");

        Assert.Equal(2, result.Nodes.Count);
        Assert.Empty(result.Edges);
    }

    [Theory]
    [InlineData(NodeKind.Device, NodeKind.Board, EdgeKind.Topology)]
    [InlineData(NodeKind.Board, NodeKind.Channel, EdgeKind.Topology)]
    [InlineData(NodeKind.Battery, NodeKind.Channel, EdgeKind.Power)]
    [InlineData(NodeKind.Channel, NodeKind.Battery, EdgeKind.Power)]
    public void EdgeKindFor_ClassifiesTheConnection(NodeKind from, NodeKind to, EdgeKind expected)
    {
        // Power edges are the animated ones. Misclassifying a topology edge as Power would make
        // structure appear to be flowing current.
        Assert.Equal(expected, WorkflowGraphMutations.EdgeKindFor(from, to));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphMutationsTests`
Expected: FAIL — `WorkflowGraphMutations` does not exist.

- [ ] **Step 3: Write the mutations**

Create `Services/Implementations/Workflow/WorkflowGraphMutations.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Pure graph edits. Every method returns a new graph rather than mutating in place, so the page
/// can swap state atomically and a rejected gesture leaves the previous graph untouched.
/// </summary>
public static class WorkflowGraphMutations
{
    public static WorkflowGraph MoveNode(WorkflowGraph graph, string nodeId, double x, double y) =>
        graph with
        {
            Nodes = graph.Nodes
                .Select(n => n.Id == nodeId ? n with { X = x, Y = y } : n)
                .ToList()
        };

    public static WorkflowGraph AddEdge(
        WorkflowGraph graph, string fromNodeId, string toNodeId, EdgeKind kind) =>
        graph with
        {
            Edges = graph.Edges
                .Append(new WorkflowEdge(
                    WorkflowGraphBuilder.EdgeId(fromNodeId, toNodeId), fromNodeId, toNodeId, kind))
                .ToList()
        };

    public static WorkflowGraph AddNode(WorkflowGraph graph, WorkflowNode node) =>
        graph with { Nodes = graph.Nodes.Append(node).ToList() };

    public static WorkflowGraph RemoveNode(WorkflowGraph graph, string nodeId) =>
        graph with
        {
            Nodes = graph.Nodes.Where(n => n.Id != nodeId).ToList(),
            Edges = graph.Edges
                .Where(e => e.FromNodeId != nodeId && e.ToNodeId != nodeId)
                .ToList(),
        };

    public static WorkflowGraph SetAttachment(
        WorkflowGraph graph, string nodeId, NodeAttachment? attachment) =>
        graph with
        {
            Nodes = graph.Nodes
                .Select(n => n.Id == nodeId ? n with { Attach = attachment } : n)
                .ToList()
        };

    /// <summary>Grid size 0 disables snapping.</summary>
    public static double SnapToGrid(double value, int gridSize) =>
        gridSize <= 0 ? value : Math.Round(value / gridSize) * gridSize;

    /// <summary>
    /// Power edges are the animated ones; topology edges are static structure. Anything
    /// involving a battery is Power, everything else is Topology.
    /// </summary>
    public static EdgeKind EdgeKindFor(NodeKind from, NodeKind to) =>
        from == NodeKind.Battery || to == NodeKind.Battery
            ? EdgeKind.Power
            : EdgeKind.Topology;
}
```

- [ ] **Step 4: Run to verify the tests pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphMutationsTests`
Expected: PASS — 17 test cases.

- [ ] **Step 5: Add drag, connect and select to the JS module**

In `wwwroot/js/workflow-canvas.js`, add these handlers inside the IIFE and wire them in `init`.
Replace the early-return guards in `onPointerDown` with real behaviour:

```javascript
    // ---- node dragging -----------------------------------------------------
    //
    // The node is moved by writing its transform directly. C# is told once, on release. A
    // pointermove that invoked .NET would be one SignalR round-trip per frame.

    function beginNodeDrag(s, e) {
        const nodeEl = e.target.closest(".wf-node");
        if (!nodeEl) return false;

        s.drag = {
            el: nodeEl,
            id: nodeEl.dataset.nodeId,
            startX: parseFloat(nodeEl.dataset.x || "0"),
            startY: parseFloat(nodeEl.dataset.y || "0"),
            pointerX: e.clientX,
            pointerY: e.clientY,
        };

        nodeEl.classList.add("wf-node--dragging");
        s.host.setPointerCapture(e.pointerId);
        return true;
    }

    function moveNodeDrag(s, e) {
        const d = s.drag;
        // Divide by zoom: a 10px pointer move at 0.5x zoom is 20px in world space.
        const x = d.startX + (e.clientX - d.pointerX) / s.zoom;
        const y = d.startY + (e.clientY - d.pointerY) / s.zoom;

        d.el.style.transform = `translate(${x}px, ${y}px)`;
        d.lastX = x;
        d.lastY = y;

        redrawEdgesFor(s, d.id, x, y);
    }

    function endNodeDrag(s, e) {
        const d = s.drag;
        s.drag = null;
        d.el.classList.remove("wf-node--dragging");
        try { s.host.releasePointerCapture(e.pointerId); } catch { }

        const snap = s.snap;
        const x = snap ? Math.round((d.lastX ?? d.startX) / snap) * snap : (d.lastX ?? d.startX);
        const y = snap ? Math.round((d.lastY ?? d.startY) / snap) * snap : (d.lastY ?? d.startY);

        d.el.style.transform = `translate(${x}px, ${y}px)`;
        d.el.dataset.x = x;
        d.el.dataset.y = y;
        redrawEdgesFor(s, d.id, x, y);

        if (s.dotNet) s.dotNet.invokeMethodAsync("OnNodeMoved", d.id, x, y);
    }

    // Recompute only the paths touching this node, in JS, so a drag does not re-render Blazor.
    // The C# geometry in WorkflowEdgeGeometry is the source of truth; this mirrors it and the
    // authoritative version is re-rendered by Blazor on drop.
    function redrawEdgesFor(s, nodeId, x, y) {
        const layer = s.world.querySelector(".wf-edge-layer");
        if (!layer) return;

        layer.querySelectorAll(`path[data-from="${nodeId}"], path[data-to="${nodeId}"]`)
            .forEach(path => {
                const fromEl = s.world.querySelector(`.wf-node[data-node-id="${path.dataset.from}"]`);
                const toEl = s.world.querySelector(`.wf-node[data-node-id="${path.dataset.to}"]`);
                if (!fromEl || !toEl) return;

                const fx = path.dataset.from === nodeId ? x : parseFloat(fromEl.dataset.x || "0");
                const fy = path.dataset.from === nodeId ? y : parseFloat(fromEl.dataset.y || "0");
                const tx = path.dataset.to === nodeId ? x : parseFloat(toEl.dataset.x || "0");
                const ty = path.dataset.to === nodeId ? y : parseFloat(toEl.dataset.y || "0");

                path.setAttribute("d", cubic(
                    fx + fromEl.offsetWidth, fy + fromEl.offsetHeight / 2,
                    tx, ty + toEl.offsetHeight / 2));
            });
    }

    // Mirrors WorkflowEdgeGeometry.BezierPath. Keep the two in step.
    function cubic(x1, y1, x2, y2) {
        const dx = Math.max(60, Math.abs(x2 - x1) * 0.5);
        return `M ${x1} ${y1} C ${x1 + dx} ${y1} ${x2 - dx} ${y2} ${x2} ${y2}`;
    }

    // ---- connection dragging -----------------------------------------------

    function beginConnect(s, e) {
        const port = e.target.closest(".wf-port");
        if (!port) return false;

        const nodeEl = port.closest(".wf-node");
        s.connect = {
            fromId: nodeEl.dataset.nodeId,
            x1: parseFloat(nodeEl.dataset.x || "0") + nodeEl.offsetWidth,
            y1: parseFloat(nodeEl.dataset.y || "0") + nodeEl.offsetHeight / 2,
        };

        const layer = s.world.querySelector(".wf-edge-layer");
        s.connect.ghost = document.createElementNS("http://www.w3.org/2000/svg", "path");
        s.connect.ghost.setAttribute("class", "wf-ghost-edge");
        layer.appendChild(s.connect.ghost);

        s.host.setPointerCapture(e.pointerId);
        return true;
    }

    function moveConnect(s, e) {
        const rect = s.host.getBoundingClientRect();
        const x = (e.clientX - rect.left - s.panX) / s.zoom;
        const y = (e.clientY - rect.top - s.panY) / s.zoom;
        s.connect.ghost.setAttribute("d", cubic(s.connect.x1, s.connect.y1, x, y));
    }

    function endConnect(s, e) {
        const c = s.connect;
        s.connect = null;
        c.ghost.remove();
        try { s.host.releasePointerCapture(e.pointerId); } catch { }

        const dropped = document.elementFromPoint(e.clientX, e.clientY);
        const targetNode = dropped && dropped.closest(".wf-node");
        if (!targetNode) return;                       // dropped on empty space: no-op

        const toId = targetNode.dataset.nodeId;
        if (toId === c.fromId) return;

        // C# validates. JS never decides whether a connection is legal.
        if (s.dotNet) s.dotNet.invokeMethodAsync("OnConnect", c.fromId, toId);
    }
```

Then restructure `onPointerDown` / `onPointerMove` / `onPointerUp` to dispatch in priority order
— port, then node header, then pan:

```javascript
    function onPointerDown(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;

        if (beginConnect(s, e)) return;

        if (e.target.closest(".wf-node__header") && beginNodeDrag(s, e)) return;

        const nodeEl = e.target.closest(".wf-node");
        if (nodeEl) {
            if (s.dotNet) s.dotNet.invokeMethodAsync("OnSelect", nodeEl.dataset.nodeId);
            return;
        }

        if (s.dotNet) s.dotNet.invokeMethodAsync("OnSelect", null);   // clicking empty deselects

        s.panning = true;
        s.startX = e.clientX - s.panX;
        s.startY = e.clientY - s.panY;
        s.host.classList.add("wf-panning");
        s.host.setPointerCapture(e.pointerId);
    }

    function onPointerMove(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;
        if (s.connect) { moveConnect(s, e); return; }
        if (s.drag) { moveNodeDrag(s, e); return; }
        if (!s.panning) return;

        s.panX = e.clientX - s.startX;
        s.panY = e.clientY - s.startY;
        applyTransform(s);
    }

    function onPointerUp(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;
        if (s.connect) { endConnect(s, e); return; }
        if (s.drag) { endNodeDrag(s, e); return; }
        if (!s.panning) return;

        s.panning = false;
        s.host.classList.remove("wf-panning");
        try { s.host.releasePointerCapture(e.pointerId); } catch { }
        notifyViewport(s);
    }
```

Add `drag: null, connect: null,` to the state object created in `init`, and bump the
`?version=` on the script tag in `App.razor` to `0.2`.

- [ ] **Step 6: Add the drag style**

Append to `wwwroot/css/workflow-canvas.css`:

```css
.wf-node--dragging { opacity: 0.85; z-index: 10; }
```

- [ ] **Step 7: Handle the callbacks in the page**

Add to `WorkflowCanvasPage.razor`:

```csharp
    [JSInvokable]
    public Task OnNodeMoved(string nodeId, double x, double y)
    {
        // JS already snapped and already painted the node at this position. This only records
        // it so the next Save and the next structural re-render agree with what is on screen.
        _graph = WorkflowGraphMutations.MoveNode(_graph, nodeId, x, y);
        _dirty = true;
        return Task.CompletedTask;          // deliberately no StateHasChanged
    }

    [JSInvokable]
    public async Task OnConnect(string fromNodeId, string toNodeId)
    {
        var check = WorkflowGraphValidator.CanConnect(_graph, fromNodeId, toNodeId, _topology);
        if (!check.IsValid)
        {
            Toast.ShowError(check.Error!);
            return;
        }

        var from = _graph.Nodes.First(n => n.Id == fromNodeId);
        var to = _graph.Nodes.First(n => n.Id == toNodeId);

        _graph = WorkflowGraphMutations.AddEdge(
            _graph, fromNodeId, toNodeId, WorkflowGraphMutations.EdgeKindFor(from.Kind, to.Kind));
        _dirty = true;

        await InvokeAsync(StateHasChanged);   // structural change: re-render is correct here
    }

    [JSInvokable]
    public async Task OnSelect(string? nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        await InvokeAsync(StateHasChanged);
    }

    private bool _dirty;
```

Check the real `ToastService` method name before using `ShowError` — match whatever
`DashboardView.razor` calls.

- [ ] **Step 8: Build and verify in the browser**

```bash
dotnet build
dotnet run
```

Verify by hand:
1. Dragging a node **header** moves that node; dragging empty canvas still pans.
2. Edges follow the node smoothly while dragging, with no visible lag.
3. On release the node snaps to the 24px grid.
4. Dragging at 0.5x zoom moves the node the same distance under the cursor as at 1x.
5. Dragging from a port draws a dashed ghost curve that follows the cursor.
6. Dropping the ghost on a valid target creates an edge.
7. Dropping on an invalid target (a board of another device) shows a toast and creates nothing.
8. Clicking a node adds the selection ring; clicking empty canvas clears it.
9. Drag 20 nodes around in sequence — no console errors and no growing lag.

- [ ] **Step 9: Commit**

```bash
git add wwwroot/js/workflow-canvas.js wwwroot/css/workflow-canvas.css Components/App.razor Components/Pages/Workflows/WorkflowCanvasPage.razor Services/Implementations/Workflow/WorkflowGraphMutations.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowGraphMutationsTests.cs
git commit -m "feat(workflow-canvas): node dragging, selection and validated connections"
```

---

### Task 12: The palette — placing hardware and auto-import

**Files:**
- Create: `Components/UI/WorkflowCanvas/PalettePanel.razor`
- Create: `Models/DTOs/Workflow/PaletteCatalog.cs`
- Modify: `Services/Implementations/Workflow/WorkflowTopologyProvider.cs` — add the catalog query
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — host the palette, handle placement
- Modify: `wwwroot/css/workflow-canvas.css` — palette styles
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs`
- Create: `Services/Implementations/Workflow/WorkflowPlacement.cs`

**Interfaces:**
- Consumes: `TopologySnapshot`, `WorkflowGraph`, `NodeKind` (Task 1); `WorkflowGraphBuilder` (Task 3); `WorkflowAutoLayout` (Task 4); `WorkflowGraphValidator` (Task 2); `WorkflowGraphMutations` (Task 11).
- Produces:
  - `record PaletteProgram(int Id, string Name)`, `record PaletteDbc(int Id, string Name)`, `record PaletteBatteryType(int Id, string Name)`
  - `record PaletteCatalog(IReadOnlyList<PaletteProgram> Programs, IReadOnlyList<PaletteDbc> DbcFiles, IReadOnlyList<PaletteBatteryType> BatteryTypes)` with `static PaletteCatalog Empty`
  - `IWorkflowTopologyProvider.GetCatalogAsync() -> Task<PaletteCatalog>`
  - `WorkflowPlacement.PlaceDevice(WorkflowGraph, TopologyDevice, IReadOnlyCollection<long>? channelIds, double originX, double originY) -> WorkflowGraph`
  - `WorkflowPlacement.PlaceBattery(WorkflowGraph, int batteryTypeId, double x, double y) -> WorkflowGraph`
  - `WorkflowPlacement.NextFreeRow(WorkflowGraph, NodeKind) -> double`
  - `PalettePanel` component with `TopologySnapshot Topology`, `PaletteCatalog Catalog`, `WorkflowGraph Graph`, `EventCallback<(TopologyDevice, IReadOnlyCollection<long>?)> OnPlaceDevice`, `EventCallback<int> OnPlaceBattery`

- [ ] **Step 1: Write the failing placement tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Placing hardware onto a canvas that already has some. The rules that matter: never duplicate
/// a device or channel already present, never clobber the positions of what is already there,
/// and offset the newcomer so it does not land exactly on top of existing nodes.
/// </summary>
public class WorkflowPlacementTests
{
    private static TopologyDevice Device(int id = 1, int channels = 2) => new(
        id, $"Dev-{id}", new[]
        {
            new TopologyBoard(id * 10, 1,
                Enumerable.Range(1, channels)
                    .Select(c => new TopologyChannel(id * 100 + c, c))
                    .ToList()),
        });

    [Fact]
    public void PlaceDevice_AddsTheWholeSubtreeToAnEmptyGraph()
    {
        var result = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(), null, 0, 0);

        Assert.Equal(1, result.Nodes.Count(n => n.Kind == NodeKind.Device));
        Assert.Equal(1, result.Nodes.Count(n => n.Kind == NodeKind.Board));
        Assert.Equal(2, result.Nodes.Count(n => n.Kind == NodeKind.Channel));
        Assert.Equal(3, result.Edges.Count);
    }

    [Fact]
    public void PlaceDevice_SkipsNodesAlreadyOnTheCanvas_RatherThanDuplicatingThem()
    {
        // Dropping the same device twice must be harmless. The validator forbids duplicates, so
        // a naive implementation would either throw or produce an invalid graph.
        var once = WorkflowPlacement.PlaceDevice(WorkflowGraph.Empty, Device(), null, 0, 0);

        var twice = WorkflowPlacement.PlaceDevice(once, Device(), null, 0, 0);

        Assert.Equal(once.Nodes.Count, twice.Nodes.Count);
        Assert.Equal(once.Edges.Count, twice.Edges.Count);
    }

    [Fact]
    public void PlaceDevice_AddsOnlyTheMissingChannels_WhenTheDeviceIsPartiallyPlaced()
    {
        var partial = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(channels: 3), new List<long> { 101 }, 0, 0);

        var full = WorkflowPlacement.PlaceDevice(partial, Device(channels: 3), null, 0, 0);

        Assert.Equal(3, full.Nodes.Count(n => n.Kind == NodeKind.Channel));
        Assert.Equal(3, full.Nodes.Select(n => n.Id).Distinct().Count(id => id.StartsWith("chn-")));
    }

    [Fact]
    public void PlaceDevice_PreservesThePositionsOfExistingNodes()
    {
        // The operator has arranged the canvas by hand. Placing a second device must not
        // re-run auto-layout over everything and undo that work.
        var first = WorkflowPlacement.PlaceDevice(WorkflowGraph.Empty, Device(1), null, 0, 0);
        first = WorkflowGraphMutations.MoveNode(first, "dev-1", 999, 888);

        var second = WorkflowPlacement.PlaceDevice(first, Device(2), null, 0, 400);

        var moved = second.Nodes.Single(n => n.Id == "dev-1");
        Assert.Equal(999, moved.X);
        Assert.Equal(888, moved.Y);
    }

    [Fact]
    public void PlaceDevice_OffsetsTheNewSubtreeByTheGivenOrigin()
    {
        var result = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(), null, originX: 100, originY: 50);

        var device = result.Nodes.Single(n => n.Kind == NodeKind.Device);
        Assert.Equal(100, device.X);
        Assert.Equal(50, device.Y);
    }

    [Fact]
    public void PlaceBattery_AddsABatteryNodeWithAUniqueId()
    {
        var graph = WorkflowPlacement.PlaceBattery(WorkflowGraph.Empty, 7, 10, 20);
        var twice = WorkflowPlacement.PlaceBattery(graph, 7, 30, 40);

        Assert.Equal(2, twice.Nodes.Count(n => n.Kind == NodeKind.Battery));
        Assert.Equal(2, twice.Nodes.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public void PlaceBattery_RecordsTheBatteryTypeOnBothEntityIdAndAttachment()
    {
        var graph = WorkflowPlacement.PlaceBattery(WorkflowGraph.Empty, 7, 0, 0);

        var battery = graph.Nodes.Single();
        Assert.Equal(7, battery.EntityId);
        Assert.Equal(7, battery.Attach!.BatteryTypeId);
    }

    [Fact]
    public void NextFreeRow_ReturnsZeroForAnEmptyColumn()
    {
        Assert.Equal(0, WorkflowPlacement.NextFreeRow(WorkflowGraph.Empty, NodeKind.Battery));
    }

    [Fact]
    public void NextFreeRow_ClearsTheLowestNodeInThatColumn()
    {
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new List<WorkflowNode>
            {
                new("b1", NodeKind.Battery, 1, 0, 0, null),
                new("b2", NodeKind.Battery, 1, 0, 500, null),
                new("c1", NodeKind.Channel, 1, 0, 9000, null),   // different column, ignored
            }
        };

        Assert.True(WorkflowPlacement.NextFreeRow(graph, NodeKind.Battery) > 500);
        Assert.True(WorkflowPlacement.NextFreeRow(graph, NodeKind.Battery) < 9000);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowPlacementTests`
Expected: FAIL — `WorkflowPlacement` does not exist.

- [ ] **Step 3: Write the placement logic**

Create `Services/Implementations/Workflow/WorkflowPlacement.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Merges newly placed hardware into an existing canvas. The rule that shapes all of this:
/// never touch a node the operator has already positioned. Auto-layout runs only over the
/// nodes being added, then the whole subtree is translated to the drop origin.
/// </summary>
public static class WorkflowPlacement
{
    public static WorkflowGraph PlaceDevice(
        WorkflowGraph graph,
        TopologyDevice device,
        IReadOnlyCollection<long>? channelIds,
        double originX,
        double originY)
    {
        var incoming = WorkflowAutoLayout.Apply(
            WorkflowGraphBuilder.BuildForDevice(device, channelIds));

        var existingNodeIds = graph.Nodes.Select(n => n.Id).ToHashSet();
        var existingEdgeIds = graph.Edges.Select(e => e.Id).ToHashSet();

        var newNodes = incoming.Nodes
            .Where(n => !existingNodeIds.Contains(n.Id))
            .Select(n => n with { X = n.X + originX, Y = n.Y + originY })
            .ToList();

        // An edge may be new even when both its nodes already exist — e.g. the board was
        // placed earlier and this pass adds one more of its channels.
        var newEdges = incoming.Edges
            .Where(e => !existingEdgeIds.Contains(e.Id))
            .Where(e => Reachable(e.FromNodeId) && Reachable(e.ToNodeId))
            .ToList();

        bool Reachable(string id) =>
            existingNodeIds.Contains(id) || newNodes.Any(n => n.Id == id);

        return graph with
        {
            Nodes = graph.Nodes.Concat(newNodes).ToList(),
            Edges = graph.Edges.Concat(newEdges).ToList(),
        };
    }

    public static WorkflowGraph PlaceBattery(
        WorkflowGraph graph, int batteryTypeId, double x, double y)
    {
        // Battery nodes are not unique hardware, so several may share a BatteryTypeId. The id
        // therefore has to be made unique from the graph rather than from the entity.
        var index = graph.Nodes.Count(n => n.Kind == NodeKind.Battery) + 1;
        var id = $"bat-{batteryTypeId}-{index}";

        while (graph.Nodes.Any(n => n.Id == id))
        {
            index++;
            id = $"bat-{batteryTypeId}-{index}";
        }

        return WorkflowGraphMutations.AddNode(graph, new WorkflowNode(
            id, NodeKind.Battery, batteryTypeId, x, y,
            new NodeAttachment(null, null, batteryTypeId)));
    }

    /// <summary>The y just below the lowest node currently in that column.</summary>
    public static double NextFreeRow(WorkflowGraph graph, NodeKind kind)
    {
        var inColumn = graph.Nodes.Where(n => n.Kind == kind).ToList();
        return inColumn.Count == 0
            ? 0
            : inColumn.Max(n => n.Y) + WorkflowAutoLayout.RowHeight;
    }
}
```

- [ ] **Step 4: Run to verify the tests pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowPlacementTests`
Expected: PASS — 9 tests.

- [ ] **Step 5: Add the catalog DTO and query**

Create `Models/DTOs/Workflow/PaletteCatalog.cs`:

```csharp
namespace BatteryTestingSystem.Models.DTOs.Workflow;

public record PaletteProgram(int Id, string Name);

public record PaletteDbc(int Id, string Name);

public record PaletteBatteryType(int Id, string Name);

/// <summary>Everything the right-hand rail can offer, other than hardware topology.</summary>
public record PaletteCatalog(
    IReadOnlyList<PaletteProgram> Programs,
    IReadOnlyList<PaletteDbc> DbcFiles,
    IReadOnlyList<PaletteBatteryType> BatteryTypes)
{
    public static PaletteCatalog Empty => new(
        Array.Empty<PaletteProgram>(),
        Array.Empty<PaletteDbc>(),
        Array.Empty<PaletteBatteryType>());
}
```

Add to `IWorkflowTopologyProvider` and implement in `WorkflowTopologyProvider`:

```csharp
    Task<PaletteCatalog> GetCatalogAsync();
```

```csharp
    public async Task<PaletteCatalog> GetCatalogAsync()
    {
        var programs = await _context.Programs
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => new PaletteProgram(p.Id, p.ProgramName))
            .ToListAsync();

        var dbcs = await _context.dbcFileRecords
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Select(d => new PaletteDbc(d.Id, d.FileName))
            .ToListAsync();

        var batteryTypes = await _context.BatteryTypes
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Select(b => new PaletteBatteryType(b.Id, b.TypeName))
            .ToListAsync();

        return new PaletteCatalog(programs, dbcs, batteryTypes);
    }
```

The property names `ProgramName`, `FileName`, `TypeName` and the `Id` types are guesses from the
entity names — open `Models/Entities/Program/`, `Models/Entities/DbcFileRecord.cs` and
`Models/Entities/BatteryType.cs` and use the real ones. If an `Id` is `long`, change the DTO to
`long` and update `NodeAttachment` usage accordingly (it already stores `int?`; cast at the
boundary rather than widening the document schema, which would require a `SchemaVersion` bump).

- [ ] **Step 6: Write the palette**

Create `Components/UI/WorkflowCanvas/PalettePanel.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

@* Right rail. Lists real DB entities. Dropping a device does NOT dump all of its channels —
   each device expands to a channel picker so the operator takes only what needs to work
   (spec 5.2). Auto-import is the bulk shortcut. *@

<div class="wf-palette-inner">
    <div class="wf-palette__tabs">
        @foreach (var tab in new[] { "Devices", "Batteries", "Programs", "DBC" })
        {
            <button class="wf-tab @(_tab == tab ? "wf-tab--active" : "")"
                    @onclick="() => _tab = tab">@tab</button>
        }
    </div>

    @if (_tab == "Devices")
    {
        @foreach (var device in Topology.Devices)
        {
            var placed = IsPlaced(device);
            <div class="wf-palette__item">
                <div class="flex items-center justify-between gap-2">
                    <button class="truncate text-left flex-1"
                            @onclick="() => Toggle(device.DeviceId)"
                            title="Show channels">
                        @device.DeviceName
                    </button>
                    <button class="wf-btn wf-btn--sm"
                            disabled="@placed"
                            title="@(placed ? "Already fully placed" : "Place every channel")"
                            @onclick="() => OnPlaceDevice.InvokeAsync((device, null))">
                        All
                    </button>
                </div>

                @if (_expanded.Contains(device.DeviceId))
                {
                    <div class="wf-palette__channels">
                        @foreach (var board in device.Boards)
                        {
                            <div class="opacity-60 mt-1">Board @board.BoardNumber</div>
                            <div class="flex flex-wrap gap-1">
                                @foreach (var channel in board.Channels)
                                {
                                    var onCanvas = IsChannelPlaced(channel.ChannelId);
                                    <button class="wf-chip @(onCanvas ? "wf-chip--on" : "")"
                                            disabled="@onCanvas"
                                            @onclick="() => PlaceOne(device, channel.ChannelId)">
                                        @channel.ChannelNumber
                                    </button>
                                }
                            </div>
                        }
                    </div>
                }
            </div>
        }

        @if (Topology.Devices.Count == 0)
        {
            <p class="opacity-60">No devices registered yet.</p>
        }
    }
    else if (_tab == "Batteries")
    {
        @foreach (var battery in Catalog.BatteryTypes)
        {
            <button class="wf-palette__item w-full text-left"
                    @onclick="() => OnPlaceBattery.InvokeAsync(battery.Id)">
                @battery.Name
            </button>
        }
    }
    else if (_tab == "Programs")
    {
        <p class="opacity-60 mb-2">Select a channel, then attach a program from the properties panel.</p>
        @foreach (var program in Catalog.Programs)
        {
            <div class="wf-palette__item">@program.Name</div>
        }
    }
    else
    {
        <p class="opacity-60 mb-2">Select a channel, then attach a DBC from the properties panel.</p>
        @foreach (var dbc in Catalog.DbcFiles)
        {
            <div class="wf-palette__item">@dbc.Name</div>
        }
    }
</div>

@code {
    [Parameter, EditorRequired] public TopologySnapshot Topology { get; set; } = TopologySnapshot.Empty;
    [Parameter, EditorRequired] public PaletteCatalog Catalog { get; set; } = PaletteCatalog.Empty;
    [Parameter, EditorRequired] public WorkflowGraph Graph { get; set; } = WorkflowGraph.Empty;
    [Parameter] public EventCallback<(TopologyDevice Device, IReadOnlyCollection<long>? Channels)> OnPlaceDevice { get; set; }
    [Parameter] public EventCallback<int> OnPlaceBattery { get; set; }

    private string _tab = "Devices";
    private readonly HashSet<int> _expanded = new();

    private void Toggle(int deviceId)
    {
        if (!_expanded.Add(deviceId)) _expanded.Remove(deviceId);
    }

    private Task PlaceOne(TopologyDevice device, long channelId) =>
        OnPlaceDevice.InvokeAsync((device, new[] { channelId }));

    private bool IsChannelPlaced(long channelId) =>
        Graph.Nodes.Any(n => n.Kind == NodeKind.Channel && n.EntityId == channelId);

    private bool IsPlaced(TopologyDevice device) =>
        device.Boards.SelectMany(b => b.Channels).All(c => IsChannelPlaced(c.ChannelId));
}
```

- [ ] **Step 7: Add palette styles**

Append to `wwwroot/css/workflow-canvas.css`:

```css
.wf-palette__tabs { display: flex; gap: 0.25rem; margin-bottom: 0.5rem; flex-wrap: wrap; }

.wf-tab {
    padding: 0.125rem 0.5rem;
    border-radius: 0.25rem;
    font-size: 0.75rem;
    color: hsl(var(--muted-foreground));
}

.wf-tab--active { background: hsl(var(--primary) / 0.15); color: hsl(var(--primary)); }

.wf-palette__item {
    padding: 0.375rem 0.5rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.375rem;
    margin-bottom: 0.375rem;
    font-size: 0.8125rem;
}

.wf-palette__channels { margin-top: 0.375rem; font-size: 0.75rem; }

.wf-chip {
    min-width: 1.75rem;
    padding: 0.0625rem 0.375rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.25rem;
    font-size: 0.75rem;
}

.wf-chip:hover:not(:disabled) { border-color: hsl(var(--primary)); }
.wf-chip--on, .wf-chip:disabled { opacity: 0.4; cursor: not-allowed; }

.wf-btn {
    padding: 0.1875rem 0.625rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.375rem;
    font-size: 0.8125rem;
    background: hsl(var(--card));
}

.wf-btn:hover:not(:disabled) { border-color: hsl(var(--primary)); }
.wf-btn:disabled { opacity: 0.45; cursor: not-allowed; }
.wf-btn--sm { padding: 0 0.375rem; font-size: 0.6875rem; }
```

- [ ] **Step 8: Wire the palette into the page**

Replace the `<div class="wf-palette">Palette</div>` placeholder with:

```razor
<div class="wf-palette">
    <PalettePanel Topology="_topology" Catalog="_catalog" Graph="_graph"
                  OnPlaceDevice="HandlePlaceDevice"
                  OnPlaceBattery="HandlePlaceBattery" />
</div>
```

and add:

```csharp
    private PaletteCatalog _catalog = PaletteCatalog.Empty;

    private async Task HandlePlaceDevice(
        (TopologyDevice Device, IReadOnlyCollection<long>? Channels) placement)
    {
        var originY = WorkflowPlacement.NextFreeRow(_graph, NodeKind.Device);

        _graph = WorkflowPlacement.PlaceDevice(
            _graph, placement.Device, placement.Channels, 0, originY);

        _dirty = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandlePlaceBattery(int batteryTypeId)
    {
        var x = WorkflowAutoLayout.ColumnWidth * 3;
        var y = WorkflowPlacement.NextFreeRow(_graph, NodeKind.Battery);

        _graph = WorkflowPlacement.PlaceBattery(_graph, batteryTypeId, x, y);
        _dirty = true;
        await InvokeAsync(StateHasChanged);
    }
```

Load the catalog in `OnInitializedAsync`, after the topology and still sequentially:

```csharp
        _catalog = await TopologyProvider.GetCatalogAsync();
```

Remove the temporary `LoadFirstDeviceForPreview` button and method added in Task 9.

- [ ] **Step 9: Build and verify in the browser**

With `python HardwareSimulator/run_sim.py -d 2 -n 8` running:
1. The Devices tab lists both simulated devices.
2. Expanding a device shows its boards and numbered channel chips.
3. Clicking a channel chip places just that channel plus its board and device.
4. The chip becomes disabled once its channel is on the canvas.
5. **All** places the entire device; clicking **All** a second time adds nothing and does not error.
6. Placing a second device puts it below the first, and the first device's nodes do not move.
7. Drag a node somewhere deliberate, then place another device — the dragged node stays put.
8. The Batteries tab places a battery node in the right-hand column.
9. No console errors.

- [ ] **Step 10: Commit**

```bash
git add Models/DTOs/Workflow/PaletteCatalog.cs Services/Implementations/Workflow/ Components/UI/WorkflowCanvas/PalettePanel.razor Components/Pages/Workflows/ wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/Services/Workflow/WorkflowPlacementTests.cs
git commit -m "feat(workflow-canvas): palette with per-channel placement and bulk auto-import"
```

---

### Task 13: The properties dock

**Files:**
- Create: `Components/UI/WorkflowCanvas/PropertiesDock.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — host the dock
- Modify: `wwwroot/css/workflow-canvas.css` — dock styles
- Test: `BatteryTestingSystem.Tests/Components/PropertiesDockTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `WorkflowNode`, `NodeAttachment`, `PaletteCatalog` (Tasks 1, 12); `WorkflowGraphValidator.CanAttach` (Task 2); `WorkflowGraphMutations.SetAttachment` / `RemoveNode` (Task 11).
- Produces: `PropertiesDock` component with `WorkflowNode? Node`, `PaletteCatalog Catalog`, `bool IsStale`, `EventCallback<NodeAttachment?> OnAttachmentChanged`, `EventCallback<string> OnDeleteNode`.

- [ ] **Step 1: Write the failing component tests**

Create `BatteryTestingSystem.Tests/Components/PropertiesDockTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The dock is the only way to attach a program or DBC, so its gating is a real rule and not
/// cosmetic: those pickers must not appear for a device, board or battery node.
/// </summary>
public class PropertiesDockTests : TestContext
{
    private static PaletteCatalog Catalog() => new(
        new List<PaletteProgram> { new(1, "Cycle-A"), new(2, "Cycle-B") },
        new List<PaletteDbc> { new(5, "pack.dbc") },
        new List<PaletteBatteryType> { new(9, "LFP 48V") });

    private static WorkflowNode Node(NodeKind kind = NodeKind.Channel, NodeAttachment? attach = null) =>
        new("n1", kind, 100, 0, 0, attach);

    [Fact]
    public void ShowsAnEmptyStateWhenNothingIsSelected()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, (WorkflowNode?)null)
            .Add(x => x.Catalog, Catalog()));

        Assert.Contains("Select a node", cut.Markup);
    }

    [Fact]
    public void ShowsProgramAndDbcPickersForAChannel()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog()));

        Assert.NotNull(cut.Find("select[data-role='program']"));
        Assert.NotNull(cut.Find("select[data-role='dbc']"));
    }

    [Theory]
    [InlineData(NodeKind.Device)]
    [InlineData(NodeKind.Board)]
    [InlineData(NodeKind.Battery)]
    public void HidesThePickersForEveryNonChannelNode(NodeKind kind)
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node(kind))
            .Add(x => x.Catalog, Catalog()));

        Assert.Empty(cut.FindAll("select[data-role='program']"));
        Assert.Empty(cut.FindAll("select[data-role='dbc']"));
    }

    [Fact]
    public void PreselectsTheCurrentlyAttachedProgram()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node(attach: new NodeAttachment(2, null, null)))
            .Add(x => x.Catalog, Catalog()));

        Assert.Equal("2", cut.Find("select[data-role='program']").GetAttribute("value"));
    }

    [Fact]
    public void ChangingTheProgramRaisesTheCallbackWithTheNewAttachment()
    {
        NodeAttachment? captured = null;

        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog())
            .Add(x => x.OnAttachmentChanged, (NodeAttachment? a) => captured = a));

        cut.Find("select[data-role='program']").Change("1");

        Assert.Equal(1, captured!.ProgramId);
    }

    [Fact]
    public void ClearingTheProgramKeepsTheDbcAttachment()
    {
        // Selecting the blank option must clear one field, not wipe the whole attachment.
        NodeAttachment? captured = null;

        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node(attach: new NodeAttachment(1, 5, null)))
            .Add(x => x.Catalog, Catalog())
            .Add(x => x.OnAttachmentChanged, (NodeAttachment? a) => captured = a));

        cut.Find("select[data-role='program']").Change("");

        Assert.Null(captured!.ProgramId);
        Assert.Equal(5, captured.DbcFileId);
    }

    [Fact]
    public void WarnsWhenTheSelectedNodeIsStale()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog())
            .Add(x => x.IsStale, true));

        Assert.Contains("no longer exists", cut.Markup);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~PropertiesDockTests`
Expected: FAIL — `PropertiesDock` does not exist.

- [ ] **Step 3: Write the dock**

Create `Components/UI/WorkflowCanvas/PropertiesDock.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow

@* Left dock. Program and DBC are channel properties (spec D5), so the pickers only appear for a
   channel node — the same rule WorkflowGraphValidator.CanAttach enforces server-side. *@

@if (Node is null)
{
    <p class="opacity-60">Select a node to see its properties.</p>
}
else
{
    <div class="wf-dock__title">@Node.Kind</div>

    @if (IsStale)
    {
        <p class="wf-warning">
            This hardware no longer exists in the database. The node is kept so nothing is lost,
            but it shows no live data.
        </p>
    }

    <dl class="wf-dock__grid">
        <dt>Node</dt><dd class="truncate" title="@Node.Id">@Node.Id</dd>
        <dt>Entity</dt><dd>@(Node.EntityId?.ToString() ?? "-")</dd>
        <dt>Position</dt><dd>@((int)Node.X), @((int)Node.Y)</dd>
    </dl>

    @if (Node.Kind == NodeKind.Channel)
    {
        <label class="wf-dock__label" for="wf-program">Program</label>
        <select id="wf-program" class="wf-select" data-role="program"
                value="@(Node.Attach?.ProgramId?.ToString() ?? "")"
                @onchange="OnProgramChanged">
            <option value="">(none)</option>
            @foreach (var program in Catalog.Programs)
            {
                <option value="@program.Id">@program.Name</option>
            }
        </select>

        <label class="wf-dock__label" for="wf-dbc">DBC file (optional)</label>
        <select id="wf-dbc" class="wf-select" data-role="dbc"
                value="@(Node.Attach?.DbcFileId?.ToString() ?? "")"
                @onchange="OnDbcChanged">
            <option value="">(none)</option>
            @foreach (var dbc in Catalog.DbcFiles)
            {
                <option value="@dbc.Id">@dbc.Name</option>
            }
        </select>
    }

    <button class="wf-btn wf-btn--danger mt-3" @onclick="() => OnDeleteNode.InvokeAsync(Node.Id)">
        Remove from canvas
    </button>
}

@code {
    [Parameter] public WorkflowNode? Node { get; set; }
    [Parameter, EditorRequired] public PaletteCatalog Catalog { get; set; } = PaletteCatalog.Empty;
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public EventCallback<NodeAttachment?> OnAttachmentChanged { get; set; }
    [Parameter] public EventCallback<string> OnDeleteNode { get; set; }

    private NodeAttachment Current => Node?.Attach ?? new NodeAttachment(null, null, null);

    private Task OnProgramChanged(ChangeEventArgs e) =>
        OnAttachmentChanged.InvokeAsync(Current with { ProgramId = ParseId(e.Value) });

    private Task OnDbcChanged(ChangeEventArgs e) =>
        OnAttachmentChanged.InvokeAsync(Current with { DbcFileId = ParseId(e.Value) });

    private static int? ParseId(object? value) =>
        int.TryParse(value?.ToString(), out var id) ? id : null;
}
```

- [ ] **Step 4: Add the dock styles**

Append to `wwwroot/css/workflow-canvas.css`:

```css
.wf-dock__title { font-weight: 600; margin-bottom: 0.5rem; }

.wf-dock__grid {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 0.125rem 0.5rem;
    font-size: 0.75rem;
    margin-bottom: 0.75rem;
}

.wf-dock__grid dt { color: hsl(var(--muted-foreground)); }

.wf-dock__label {
    display: block;
    font-size: 0.75rem;
    color: hsl(var(--muted-foreground));
    margin: 0.5rem 0 0.125rem;
}

.wf-select {
    width: 100%;
    padding: 0.25rem 0.375rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.375rem;
    background: hsl(var(--background));
    color: hsl(var(--foreground));
    font-size: 0.8125rem;
}

.wf-warning {
    padding: 0.375rem 0.5rem;
    border-radius: 0.375rem;
    font-size: 0.75rem;
    background: hsl(var(--status-interrupt) / 0.15);
    color: hsl(var(--status-interrupt));
    margin-bottom: 0.5rem;
}

.wf-btn--danger { border-color: hsl(var(--status-error) / 0.5); color: hsl(var(--status-error)); }
```

- [ ] **Step 5: Wire the dock into the page**

Replace `<div class="wf-dock">Properties</div>` with:

```razor
<div class="wf-dock">
    <PropertiesDock Node="SelectedNode"
                    Catalog="_catalog"
                    IsStale="@(_selectedNodeId is not null && _staleNodeIds.Contains(_selectedNodeId))"
                    OnAttachmentChanged="HandleAttachmentChanged"
                    OnDeleteNode="HandleDeleteNode" />
</div>
```

and add:

```csharp
    private WorkflowNode? SelectedNode =>
        _selectedNodeId is null ? null : _graph.Nodes.FirstOrDefault(n => n.Id == _selectedNodeId);

    private async Task HandleAttachmentChanged(NodeAttachment? attachment)
    {
        if (_selectedNodeId is null) return;

        // Re-check server-side rather than trusting that the UI only offered this for a channel.
        var check = WorkflowGraphValidator.CanAttach(_graph, _selectedNodeId);
        if (!check.IsValid)
        {
            Toast.ShowError(check.Error!);
            return;
        }

        _graph = WorkflowGraphMutations.SetAttachment(_graph, _selectedNodeId, attachment);
        _dirty = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleDeleteNode(string nodeId)
    {
        _graph = WorkflowGraphMutations.RemoveNode(_graph, nodeId);
        if (_selectedNodeId == nodeId) _selectedNodeId = null;
        _dirty = true;
        await InvokeAsync(StateHasChanged);
    }
```

- [ ] **Step 6: Run the component tests**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~PropertiesDockTests`
Expected: PASS — 9 test cases.

- [ ] **Step 7: Build and verify in the browser**

1. Clicking a channel shows program and DBC pickers; clicking a device shows neither.
2. Choosing a program adds a badge to the channel node immediately.
3. Choosing a DBC adds a second badge; clearing the program leaves the DBC badge in place.
4. **Remove from canvas** deletes the node and every edge attached to it.
5. Deleting the selected node clears the dock back to its empty state.
6. No console errors.

- [ ] **Step 8: Add the panel and board collapse (spec 5.1 and 5.3)**

Two separate collapses. Both exist to keep a 64-channel graph readable, which the spec's risk
table names as the main mitigation for the biggest risk in the feature.

**Panel collapse** — the CSS classes already exist from Task 8. Wire the toggles in
`WorkflowCanvasPage.razor`:

```razor
<div class="wf-dock @(_dockOpen ? "" : "wf-dock--collapsed")">
    <button class="wf-collapse" title="@(_dockOpen ? "Collapse" : "Expand") properties"
            @onclick="() => _dockOpen = !_dockOpen">@(_dockOpen ? "‹" : "›")</button>
    @if (_dockOpen)
    {
        <PropertiesDock ... />
    }
</div>
```

```csharp
    private bool _dockOpen = true;
    private bool _paletteOpen = true;
```

Mirror it for `.wf-palette` with `_paletteOpen` and the arrows reversed (`›` / `‹`).

**Board collapse** — a collapsed board hides its channel nodes and their edges. This is a *view*
filter, never a graph edit: the nodes stay in the document and are saved as normal, so collapsing
can never lose the operator's work.

Add to `WorkflowGraphMutations.cs`:

```csharp
    /// <summary>
    /// A view-only projection that hides the channels of collapsed boards. The returned graph is
    /// for rendering ONLY — never save it, or collapsing a board would silently delete its
    /// channels from the layout.
    /// </summary>
    public static WorkflowGraph HideCollapsedBoards(
        WorkflowGraph graph, IReadOnlyCollection<string> collapsedBoardIds)
    {
        if (collapsedBoardIds.Count == 0) return graph;

        var hiddenNodeIds = graph.Edges
            .Where(e => e.Kind == EdgeKind.Topology && collapsedBoardIds.Contains(e.FromNodeId))
            .Select(e => e.ToNodeId)
            .ToHashSet();

        if (hiddenNodeIds.Count == 0) return graph;

        // Also hide any battery hanging off a hidden channel, or it would float unconnected.
        var hiddenBatteries = graph.Edges
            .Where(e => e.Kind == EdgeKind.Power)
            .Where(e => hiddenNodeIds.Contains(e.FromNodeId) || hiddenNodeIds.Contains(e.ToNodeId))
            .Select(e => hiddenNodeIds.Contains(e.FromNodeId) ? e.ToNodeId : e.FromNodeId)
            .ToHashSet();

        hiddenNodeIds.UnionWith(hiddenBatteries);

        return graph with
        {
            Nodes = graph.Nodes.Where(n => !hiddenNodeIds.Contains(n.Id)).ToList(),
            Edges = graph.Edges
                .Where(e => !hiddenNodeIds.Contains(e.FromNodeId) && !hiddenNodeIds.Contains(e.ToNodeId))
                .ToList(),
        };
    }
```

Add these tests to `WorkflowGraphMutationsTests.cs`:

```csharp
    [Fact]
    public void HideCollapsedBoards_RemovesTheChannelsOfACollapsedBoardFromTheView()
    {
        var graph = GraphWith(
            Node("brd-1", NodeKind.Board), Node("chn-1"), Node("chn-2")) with
        {
            Edges = new List<WorkflowEdge>
            {
                new("e1", "brd-1", "chn-1", EdgeKind.Topology),
                new("e2", "brd-1", "chn-2", EdgeKind.Topology),
            }
        };

        var view = WorkflowGraphMutations.HideCollapsedBoards(graph, new[] { "brd-1" });

        Assert.Single(view.Nodes);
        Assert.Equal("brd-1", view.Nodes[0].Id);
        Assert.Empty(view.Edges);
    }

    [Fact]
    public void HideCollapsedBoards_AlsoHidesABatteryLeftDanglingByTheHiddenChannel()
    {
        var graph = GraphWith(
            Node("brd-1", NodeKind.Board), Node("chn-1"), Node("bat-1", NodeKind.Battery)) with
        {
            Edges = new List<WorkflowEdge>
            {
                new("e1", "brd-1", "chn-1", EdgeKind.Topology),
                new("e2", "bat-1", "chn-1", EdgeKind.Power),
            }
        };

        var view = WorkflowGraphMutations.HideCollapsedBoards(graph, new[] { "brd-1" });

        Assert.DoesNotContain(view.Nodes, n => n.Id == "bat-1");
    }

    [Fact]
    public void HideCollapsedBoards_DoesNotMutateTheSourceGraph()
    {
        // The critical property: this is a view, so the saved document must be untouched.
        var graph = GraphWith(Node("brd-1", NodeKind.Board), Node("chn-1")) with
        {
            Edges = new List<WorkflowEdge> { new("e1", "brd-1", "chn-1", EdgeKind.Topology) }
        };

        WorkflowGraphMutations.HideCollapsedBoards(graph, new[] { "brd-1" });

        Assert.Equal(2, graph.Nodes.Count);
    }

    [Fact]
    public void HideCollapsedBoards_ReturnsTheGraphUnchangedWhenNothingIsCollapsed()
    {
        var graph = GraphWith(Node("brd-1", NodeKind.Board), Node("chn-1"));

        Assert.Same(graph, WorkflowGraphMutations.HideCollapsedBoards(graph, Array.Empty<string>()));
    }
```

In `BoardNode.razor`, add the toggle:

```razor
    [Parameter] public bool IsCollapsed { get; set; }
    [Parameter] public EventCallback<string> OnToggleCollapse { get; set; }
```

```razor
    <button class="wf-btn wf-btn--sm" @onclick="() => OnToggleCollapse.InvokeAsync(Node.Id)">
        @(IsCollapsed ? "Expand" : "Collapse")
    </button>
```

In the page, keep a `HashSet<string> _collapsedBoardIds`, pass the **view** graph to
`CanvasSurface` and `Minimap` while keeping `_graph` as the source of truth for saving:

```csharp
    private readonly HashSet<string> _collapsedBoardIds = new();

    /// <summary>Render from this; SAVE from _graph. Never confuse the two.</summary>
    private WorkflowGraph ViewGraph =>
        WorkflowGraphMutations.HideCollapsedBoards(_graph, _collapsedBoardIds);

    private async Task ToggleBoardCollapse(string boardNodeId)
    {
        if (!_collapsedBoardIds.Add(boardNodeId)) _collapsedBoardIds.Remove(boardNodeId);
        await InvokeAsync(StateHasChanged);
    }
```

Leave `RefreshSubscription()` reading `_graph`, not `ViewGraph` — a collapsed channel is hidden,
not removed, and should keep receiving telemetry so expanding it shows live data immediately.

Add the collapse-button style:

```css
.wf-collapse {
    float: right;
    padding: 0 0.25rem;
    color: hsl(var(--muted-foreground));
}
```

- [ ] **Step 9: Verify the collapse behaviour**

1. Collapsing the properties dock and the palette shrinks each to a narrow rail; the canvas grows.
2. Collapsing a board hides its channels and their edges; the board node stays.
3. A battery attached to a hidden channel is hidden too — no floating orphan.
4. Expanding restores everything in its original positions.
5. **Save while a board is collapsed, reload the layout, expand it — every channel is still
   there.** This is the failure the "view, not edit" design exists to prevent; verify it
   deliberately.
6. The minimap reflects the collapsed view.

- [ ] **Step 10: Run the tests**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowGraphMutationsTests`
Expected: PASS — 21 test cases (17 from Task 11 plus the 4 added here).

- [ ] **Step 11: Commit**

```bash
git add Components/UI/WorkflowCanvas/ Components/Pages/Workflows/ Services/Implementations/Workflow/WorkflowGraphMutations.cs wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/
git commit -m "feat(workflow-canvas): properties dock, panel rails and board collapse"
```

---

### Task 14: BatteryGlyph and the animation layer

This is the task the whole experiment is being judged on. Every animation here is
compositor-only; the naive version of each is a per-frame repaint that turns a 64-node canvas
into a space heater.

**Files:**
- Create: `Components/UI/WorkflowCanvas/BatteryGlyph.razor`
- Modify: `Components/UI/WorkflowCanvas/Nodes/BatteryNode.razor` — use the glyph
- Modify: `wwwroot/css/workflow-canvas.css` — keyframes, glow, flow, shimmer
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — animation toggle in the toolbar
- Test: `BatteryTestingSystem.Tests/Components/BatteryGlyphTests.cs`

**Interfaces:**
- Consumes: `CircuitStatus` (existing); `WorkflowStatusCss` (Task 9).
- Produces: `BatteryGlyph` component with `int CellCount` (default 1), `double StateOfCharge` (0-1), `CircuitStatus Status`, `bool IsCharging`; a `.wf-anim-off` root class that disables every animation; and the CSS custom-property contract the telemetry bridge writes in Task 15:
  - `--wf-status` — an HSL triplet, e.g. `51 100% 50%`
  - `--wf-soc` — `0`..`1`, drives `scaleY`
  - `--wf-flow` — `1` forward, `-1` reverse, `0` still

- [ ] **Step 1: Write the failing component tests**

Create `BatteryTestingSystem.Tests/Components/BatteryGlyphTests.cs`:

```csharp
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.Enums;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The glyph's entire API is CSS custom properties, so that the telemetry bridge can update a
/// battery by writing a variable instead of causing a Blazor re-render. These tests pin that
/// contract — particularly that fill is expressed as --wf-soc for a scaleY transform rather than
/// as a height, which would repaint every frame.
/// </summary>
public class BatteryGlyphTests : TestContext
{
    [Fact]
    public void RendersOneCellByDefault()
    {
        var cut = RenderComponent<BatteryGlyph>();

        Assert.Single(cut.FindAll(".wf-cell"));
    }

    [Fact]
    public void RendersAPackOfCellsWhenAsked()
    {
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.CellCount, 4));

        Assert.Equal(4, cut.FindAll(".wf-cell").Count);
    }

    [Fact]
    public void ExposesStateOfChargeAsTheWfSocVariable()
    {
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, 0.42));

        var style = cut.Find(".wf-battery").GetAttribute("style") ?? "";
        Assert.Contains("--wf-soc: 0.42", style);
    }

    [Fact]
    public void ClampsStateOfChargeIntoZeroToOne()
    {
        // Telemetry can legitimately report a slightly out-of-range SoC. A value above 1 would
        // overflow the cell outline; a negative one would flip the transform.
        var high = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, 1.8));
        var low = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, -0.5));

        Assert.Contains("--wf-soc: 1", high.Find(".wf-battery").GetAttribute("style"));
        Assert.Contains("--wf-soc: 0", low.Find(".wf-battery").GetAttribute("style"));
    }

    [Fact]
    public void UsesTheCorrectedStatusVariable_NotTheMisspelledEnumName()
    {
        // CircuitStatus.Countinue -> --status-continue. The naive ToString().ToLower() would
        // emit --status-countinue, which is declared nowhere.
        var cut = RenderComponent<BatteryGlyph>(p => p
            .Add(x => x.Status, CircuitStatus.Countinue));

        var style = cut.Find(".wf-battery").GetAttribute("style") ?? "";
        Assert.Contains("--status-continue", style);
        Assert.DoesNotContain("countinue", style);
    }

    [Fact]
    public void MarksChargingSoTheShimmerCanRun()
    {
        var idle = RenderComponent<BatteryGlyph>(p => p.Add(x => x.IsCharging, false));
        var charging = RenderComponent<BatteryGlyph>(p => p.Add(x => x.IsCharging, true));

        Assert.DoesNotContain("wf-battery--charging", idle.Find(".wf-battery").ClassName);
        Assert.Contains("wf-battery--charging", charging.Find(".wf-battery").ClassName);
    }

    [Fact]
    public void ShowsTheFaultTreatmentOnError()
    {
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.Status, CircuitStatus.Error));

        Assert.Contains("wf-battery--fault", cut.Find(".wf-battery").ClassName);
    }

    [Fact]
    public void NeverUsesAnAnimatedHeightOrBoxShadowInline()
    {
        // Guards the performance decision at the point where it is easiest to undo by accident.
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, 0.6));

        var markup = cut.Markup;
        Assert.DoesNotContain("height: 60%", markup);
        Assert.DoesNotContain("box-shadow:", markup);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~BatteryGlyphTests`
Expected: FAIL — `BatteryGlyph` does not exist.

- [ ] **Step 3: Write the glyph**

Create `Components/UI/WorkflowCanvas/BatteryGlyph.razor`:

```razor
@using System.Globalization
@using BatteryTestingSystem.Models.Enums
@using BatteryTestingSystem.Services.Implementations.Workflow

@* The glyph's whole API is CSS custom properties. The telemetry bridge updates a battery by
   writing --wf-soc and --wf-status onto this element from JS — it never re-renders this
   component. Fill is a scaleY transform, not a height, so it animates on the compositor. *@

<div class="wf-battery @(IsCharging ? "wf-battery--charging" : "") @(Status == CircuitStatus.Error ? "wf-battery--fault" : "")"
     style="--wf-soc: @Soc; --wf-status: var(@WorkflowStatusCss.Var(Status));"
     data-role="battery">

    @for (var i = 0; i < Math.Max(1, CellCount); i++)
    {
        <div class="wf-cell">
            <div class="wf-cell__fill"></div>
            <div class="wf-cell__shimmer"></div>
        </div>
    }
</div>

@code {
    [Parameter] public int CellCount { get; set; } = 1;
    [Parameter] public double StateOfCharge { get; set; }
    [Parameter] public CircuitStatus Status { get; set; } = CircuitStatus.Offline;
    [Parameter] public bool IsCharging { get; set; }

    /// <summary>Clamped and formatted invariantly — a comma decimal separator would make the
    /// custom property unparseable and the fill would silently sit at zero.</summary>
    private string Soc =>
        Math.Clamp(StateOfCharge, 0, 1).ToString("0.####", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Use it in the battery node**

Replace the placeholder body of `Components/UI/WorkflowCanvas/Nodes/BatteryNode.razor`:

```razor
@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Models.Enums

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale"
            ShowInPort="false" ShowOutPort="true">
    <BatteryGlyph CellCount="@CellCount" StateOfCharge="@StateOfCharge"
                  Status="@Status" IsCharging="@IsCharging" />
    <div class="text-center opacity-70" data-role="soc">--%</div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Battery";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public int CellCount { get; set; } = 1;
    [Parameter] public double StateOfCharge { get; set; }
    [Parameter] public CircuitStatus Status { get; set; } = CircuitStatus.Offline;
    [Parameter] public bool IsCharging { get; set; }
}
```

- [ ] **Step 5: Write the animation CSS**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ================================================================ animation
   Every animation below runs on the compositor: only transform, opacity and
   stroke-dashoffset are animated. Animating box-shadow or height instead would repaint the
   whole node on every frame, which at 64 nodes is the difference between smooth and unusable.

   --wf-status, --wf-soc and --wf-flow are written directly onto elements by the telemetry
   bridge (Task 15). CSS does everything else, so live data never causes a Blazor re-render. */

/* ---------------------------------------------------------------- battery glyph */

.wf-battery {
    display: flex;
    gap: 2px;
    justify-content: center;
    padding: 0.25rem 0;
    --wf-soc: 0;
    --wf-status: var(--status-offline);
}

.wf-cell {
    position: relative;
    width: 18px;
    height: 34px;
    border: 2px solid hsl(var(--wf-status) / 0.7);
    border-radius: 3px;
    overflow: hidden;
    background: hsl(var(--muted) / 0.4);
}

/* The terminal nub. */
.wf-cell::before {
    content: "";
    position: absolute;
    top: -4px;
    left: 50%;
    width: 8px;
    height: 3px;
    margin-left: -4px;
    border-radius: 1px;
    background: hsl(var(--wf-status) / 0.7);
}

/* Fill grows from the bottom via scaleY. transform-origin is what makes it fill upward
   instead of stretching from the middle. */
.wf-cell__fill {
    position: absolute;
    inset: 0;
    transform-origin: 50% 100%;
    transform: scaleY(var(--wf-soc));
    background: hsl(var(--wf-status) / 0.75);
    transition: transform 400ms ease-out;
}

/* Charging shimmer: a translucent band travelling up the cell. Paused rather than removed,
   so toggling charge state costs nothing. */
.wf-cell__shimmer {
    position: absolute;
    inset: 0;
    background: linear-gradient(
        to top,
        transparent 0%,
        hsl(var(--wf-status) / 0.55) 45%,
        transparent 90%);
    transform: translateY(100%);
    animation: wf-shimmer 1.6s linear infinite;
    animation-play-state: paused;
    opacity: 0;
}

.wf-battery--charging .wf-cell__shimmer {
    animation-play-state: running;
    opacity: 1;
}

@keyframes wf-shimmer {
    from { transform: translateY(100%); }
    to   { transform: translateY(-100%); }
}

/* Fault: diagonal hatching, no motion. An error state that pulses reads as activity. */
.wf-battery--fault .wf-cell {
    background-image: repeating-linear-gradient(
        45deg,
        hsl(var(--status-error) / 0.35) 0 3px,
        transparent 3px 6px);
}

/* ---------------------------------------------------------------- node glow

   The glow lives on a pseudo-element with a STATIC box-shadow; only its opacity animates.
   Animating the box-shadow itself would repaint the node every frame. */

.wf-node::before {
    content: "";
    position: absolute;
    inset: -2px;
    border-radius: 0.625rem;
    box-shadow: 0 0 12px 2px hsl(var(--wf-status, var(--status-idle)) / 0.75);
    opacity: 0;
    pointer-events: none;
    transition: opacity 300ms ease;
}

/* Only actively working channels glow, and only they animate. An idle canvas is completely
   still, which is what keeps a 64-node graph readable. */
.wf-node--active::before {
    opacity: 0.55;
    animation: wf-pulse 2.4s ease-in-out infinite;
}

@keyframes wf-pulse {
    0%, 100% { opacity: 0.30; }
    50%      { opacity: 0.75; }
}

/* ---------------------------------------------------------------- edge flow

   Dash offset animation is compositor-friendly, so 60 flowing edges cost about what one does.
   --wf-flow is +1 (charging, toward the battery), -1 (discharging) or 0 (still). */

.wf-edge--power {
    stroke: hsl(var(--wf-status, var(--border)));
    stroke-dasharray: 10 8;
    stroke-dashoffset: 0;
}

.wf-edge--flowing {
    animation: wf-flow 1.1s linear infinite;
    animation-direction: normal;
    filter: drop-shadow(0 0 4px hsl(var(--wf-status) / 0.6));
}

/* Reverse means discharging: current leaving the battery. */
.wf-edge--flowing-reverse { animation-direction: reverse; }

@keyframes wf-flow {
    from { stroke-dashoffset: 36; }
    to   { stroke-dashoffset: 0; }
}

/* ---------------------------------------------------------------- motion off

   Two switches, both required. The media query respects the operating system setting; the
   .wf-anim-off class is the toolbar toggle, which doubles as the diagnostic when judging
   whether the canvas feels smooth. */

@media (prefers-reduced-motion: reduce) {
    .wf-cell__shimmer,
    .wf-node--active::before,
    .wf-edge--flowing {
        animation: none !important;
    }

    .wf-cell__fill { transition: none; }
}

.wf-anim-off .wf-cell__shimmer,
.wf-anim-off .wf-node--active::before,
.wf-anim-off .wf-edge--flowing {
    animation: none !important;
}

.wf-anim-off .wf-edge--flowing { filter: none; }
```

- [ ] **Step 6: Add the toolbar toggle**

In `WorkflowCanvasPage.razor`, put the class on the shell root and add the button:

```razor
<div class="wf-shell @(_animations ? "" : "wf-anim-off")">
```

```razor
        <button class="wf-btn" @onclick="() => _animations = !_animations">
            @(_animations ? "Animations on" : "Animations off")
        </button>
        <button class="wf-btn" @onclick="FitToContent">Fit</button>
```

```csharp
    private bool _animations = true;

    private async Task FitToContent()
    {
        if (_jsReady) await JS.InvokeVoidAsync("workflowCanvas.fitToContent", _canvasHost);
    }
```

- [ ] **Step 7: Run the component tests**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~BatteryGlyphTests`
Expected: PASS — 8 tests.

- [ ] **Step 8: Build and verify in the browser**

Because no telemetry is wired yet (Task 15), drive the animation by hand from the browser
console to confirm the CSS works:

```javascript
// Make one channel node glow, one power edge flow, one battery fill and shimmer.
const n = document.querySelector('.wf-node--channel');
n.style.setProperty('--wf-status', '51 100% 50%');
n.classList.add('wf-node--active');

const b = document.querySelector('.wf-battery');
b.style.setProperty('--wf-soc', '0.7');
b.style.setProperty('--wf-status', '51 100% 50%');
b.classList.add('wf-battery--charging');

const e = document.querySelector('.wf-edge--power');
if (e) { e.style.setProperty('--wf-status', '51 100% 50%'); e.classList.add('wf-edge--flowing'); }
```

Verify:
1. The channel node pulses with a soft coloured glow.
2. The battery fills to 70% and a shimmer band travels upward.
3. The power edge's dashes flow; adding `wf-edge--flowing-reverse` reverses the direction.
4. **Animations off** in the toolbar stops all three instantly; **on** resumes them.
5. Enable "reduce motion" in the OS accessibility settings and reload — nothing animates.
6. Switch the app theme — every animated colour follows it.
7. Open DevTools → Performance, record five seconds with animations running, and confirm no
   `Layout` or `Paint` entries recur per frame. Only `Composite Layers` should repeat. **If
   Paint recurs every frame, an animation has drifted off the compositor — find it before
   continuing.**

- [ ] **Step 9: Commit**

```bash
git add Components/UI/WorkflowCanvas/ Components/Pages/Workflows/ wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/Components/BatteryGlyphTests.cs
git commit -m "feat(workflow-canvas): animated battery glyph, node glow and edge flow"
```

---

### Task 15: The telemetry bridge

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Create: `Models/DTOs/Workflow/TelemetryEntry.cs`
- Modify: `wwwroot/js/workflow-canvas.js` — implement `applyTelemetry`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — own the bridge lifecycle
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `WorkflowGraph`, `NodeKind` (Task 1); `WorkflowStatusCss` (Task 9); `WorkflowGraphBuilder.ChannelNodeId` (Task 3); existing `CircuitStatus`, `ChannelManager`, `CoalescingRunner`.
- Produces:
  - `record TelemetryEntry(string NodeId, string StatusHsl, string StatusName, double Soc, int Flow, double Voltage, double Current)`
  - `WorkflowTelemetryBridge.BuildEntries(WorkflowGraph, IReadOnlyDictionary<long, ChannelTelemetry>) -> IReadOnlyList<TelemetryEntry>` (pure, tested)
  - `record ChannelTelemetry(CircuitStatus Status, double Soc, double Voltage, double Current)`
  - `WorkflowTelemetryBridge.OnCanvasChannelIds(WorkflowGraph, IReadOnlyCollection<string> staleNodeIds) -> HashSet<long>` (pure, tested)
  - `WorkflowTelemetryBridge.FlowFor(CircuitStatus, double current) -> int`

Only `BuildEntries`, `OnCanvasChannelIds` and `FlowFor` are unit-tested. The `ChannelManager`
subscription itself is not: it is a `BackgroundService` owning real TCP/UDP listeners and cannot
be constructed in a unit test — the same constraint documented in `CircuitSelectionLogicTests`.

- [ ] **Step 1: Write the failing tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Spec 6. The bridge is what keeps a 64-channel canvas usable on Blazor Server: it filters
/// telemetry to the channels actually placed, and turns each tick into CSS-ready values so the
/// JS layer only has to write custom properties. Nothing here may cause a re-render.
/// </summary>
public class WorkflowTelemetryBridgeTests
{
    private static WorkflowGraph GraphWith(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    private static WorkflowNode Channel(long id) =>
        new(WorkflowGraphBuilder.ChannelNodeId(id), NodeKind.Channel, id, 0, 0, null);

    // ============================================================ OnCanvasChannelIds

    [Fact]
    public void OnCanvasChannelIds_ReturnsOnlyChannelsThatArePlaced()
    {
        // The whole cost argument: subscription size tracks what the operator placed, not the
        // 640 circuits the app may know about.
        var graph = GraphWith(
            Channel(100),
            Channel(101),
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null));

        var ids = WorkflowTelemetryBridge.OnCanvasChannelIds(graph, Array.Empty<string>());

        Assert.Equal(new[] { 100L, 101L }, ids.OrderBy(i => i));
    }

    [Fact]
    public void OnCanvasChannelIds_ExcludesStaleNodes()
    {
        // A stale node points at deleted hardware; subscribing for it would be a permanent
        // no-data slot in every tick.
        var graph = GraphWith(Channel(100), Channel(999));

        var ids = WorkflowTelemetryBridge.OnCanvasChannelIds(
            graph, new[] { WorkflowGraphBuilder.ChannelNodeId(999) });

        Assert.Equal(new[] { 100L }, ids);
    }

    [Fact]
    public void OnCanvasChannelIds_IsEmptyForAnEmptyGraph()
    {
        Assert.Empty(WorkflowTelemetryBridge.OnCanvasChannelIds(
            WorkflowGraph.Empty, Array.Empty<string>()));
    }

    // ============================================================ FlowFor

    [Theory]
    [InlineData(CircuitStatus.Charge, 12.5, 1)]
    [InlineData(CircuitStatus.Discharging, 12.5, -1)]
    [InlineData(CircuitStatus.Discharging, -12.5, -1)]
    [InlineData(CircuitStatus.Idle, 0, 0)]
    [InlineData(CircuitStatus.Pause, 5, 0)]
    [InlineData(CircuitStatus.Offline, 0, 0)]
    [InlineData(CircuitStatus.Error, 3, 0)]
    public void FlowFor_OnlyChargeAndDischargeProduceMotion(
        CircuitStatus status, double current, int expected)
    {
        // A paused or errored channel must be completely still. Motion means "current is
        // flowing right now" — if it means anything less, the animation stops carrying meaning.
        Assert.Equal(expected, WorkflowTelemetryBridge.FlowFor(status, current));
    }

    [Fact]
    public void FlowFor_ChargeWithNoCurrentIsStill()
    {
        // Status says charge but the current has dropped to zero — a finished CV tail.
        Assert.Equal(0, WorkflowTelemetryBridge.FlowFor(CircuitStatus.Charge, 0));
    }

    // ============================================================ BuildEntries

    [Fact]
    public void BuildEntries_ProducesOneEntryPerPlacedChannelWithData()
    {
        var graph = GraphWith(Channel(100), Channel(101));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        var entries = WorkflowTelemetryBridge.BuildEntries(graph, data);

        Assert.Single(entries);
        Assert.Equal("chn-100", entries[0].NodeId);
    }

    [Fact]
    public void BuildEntries_EmitsTheCorrectedStatusName()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Countinue, 0.5, 3.7, 1),
        };

        var entries = WorkflowTelemetryBridge.BuildEntries(graph, data);

        Assert.Equal("continue", entries[0].StatusName);
    }

    [Fact]
    public void BuildEntries_EmitsAnHslTripletNotAVariableName()
    {
        // JS writes this straight into --wf-status, which is consumed as hsl(var(--wf-status)).
        // A "--status-charge" string there would produce hsl(--status-charge) and no colour.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        var hsl = WorkflowTelemetryBridge.BuildEntries(graph, data)[0].StatusHsl;

        Assert.DoesNotContain("--", hsl);
        Assert.Matches(@"^\d+(\.\d+)? \d+(\.\d+)?% \d+(\.\d+)?%$", hsl);
    }

    [Fact]
    public void BuildEntries_ClampsStateOfChargeIntoRange()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 1.4, 3.7, 10),
        };

        Assert.Equal(1.0, WorkflowTelemetryBridge.BuildEntries(graph, data)[0].Soc);
    }

    [Fact]
    public void BuildEntries_SkipsChannelsWithNoTelemetryYet()
    {
        // A placed channel whose device has not registered yet must simply not appear in the
        // batch, leaving its node showing the "--" placeholders.
        var graph = GraphWith(Channel(100), Channel(101));

        var entries = WorkflowTelemetryBridge.BuildEntries(
            graph, new Dictionary<long, ChannelTelemetry>());

        Assert.Empty(entries);
    }

    [Fact]
    public void BuildEntries_IgnoresTelemetryForChannelsNotOnTheCanvas()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
            [999] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        Assert.Single(WorkflowTelemetryBridge.BuildEntries(graph, data));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowTelemetryBridgeTests`
Expected: FAIL — `WorkflowTelemetryBridge` does not exist.

- [ ] **Step 3: Write the DTO**

Create `Models/DTOs/Workflow/TelemetryEntry.cs`:

```csharp
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs.Workflow;

/// <summary>What one channel currently reads. Assembled by the page from ChannelManager.</summary>
public record ChannelTelemetry(CircuitStatus Status, double Soc, double Voltage, double Current);

/// <summary>
/// One CSS-ready row, serialized straight to JS. StatusHsl is a raw triplet such as
/// "51 100% 50%" because the stylesheet consumes it as hsl(var(--wf-status) / alpha) —
/// sending a variable name instead would produce no colour and no error.
/// Flow: 1 charging, -1 discharging, 0 still.
/// </summary>
public record TelemetryEntry(
    string NodeId,
    string StatusHsl,
    string StatusName,
    double Soc,
    int Flow,
    double Voltage,
    double Current);
```

- [ ] **Step 4: Write the bridge**

Create `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Turns raw channel telemetry into CSS-ready rows for one batched interop call.
///
/// The design constraint (spec 6): telemetry must NEVER trigger a Blazor re-render. At 64
/// channels and 5 Hz, re-rendering would be 320 component diffs a second over SignalR. Instead
/// each tick becomes a single JS call that writes CSS custom properties, and the stylesheet does
/// the colouring, glowing, flowing and filling.
///
/// The theme HSL triplets are duplicated here from app.css because a server-side component
/// cannot read a CSS variable's computed value. A test pins that every variable exists; if a
/// theme colour is ever changed, change it here too.
/// </summary>
public static class WorkflowTelemetryBridge
{
    private static readonly Dictionary<string, string> StatusHslByName = new()
    {
        ["idle"] = "0 0% 62%",
        ["charge"] = "51 100% 50%",
        ["discharging"] = "33 100% 50%",
        ["pause"] = "207 90% 54%",
        ["continue"] = "122 39% 49%",
        ["interrupt"] = "37 75% 35%",
        ["error"] = "4 90% 58%",
        ["msg"] = "187 100% 42%",
        ["offline"] = "0 0% 62%",
    };

    /// <summary>
    /// The channels worth subscribing to: placed, and not stale. Recompute whenever the graph
    /// changes — this set is the entire cost-control mechanism for the canvas.
    /// </summary>
    public static HashSet<long> OnCanvasChannelIds(
        WorkflowGraph graph, IReadOnlyCollection<string> staleNodeIds) =>
        graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel)
            .Where(n => !staleNodeIds.Contains(n.Id))
            .Where(n => n.EntityId is not null)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

    /// <summary>
    /// 1 charging, -1 discharging, 0 still. Only Charge and Discharging move, and only when
    /// current is actually flowing — a paused or errored channel that still animated would make
    /// the motion meaningless.
    /// </summary>
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

    public static IReadOnlyList<TelemetryEntry> BuildEntries(
        WorkflowGraph graph, IReadOnlyDictionary<long, ChannelTelemetry> telemetry)
    {
        var entries = new List<TelemetryEntry>();

        foreach (var node in graph.Nodes.Where(n => n.Kind == NodeKind.Channel))
        {
            if (node.EntityId is not { } channelId) continue;
            if (!telemetry.TryGetValue(channelId, out var reading)) continue;

            var name = WorkflowStatusCss.Name(reading.Status);

            entries.Add(new TelemetryEntry(
                NodeId: node.Id,
                StatusHsl: StatusHslByName.TryGetValue(name, out var hsl) ? hsl : StatusHslByName["idle"],
                StatusName: name,
                Soc: Math.Clamp(reading.Soc, 0, 1),
                Flow: FlowFor(reading.Status, reading.Current),
                Voltage: reading.Voltage,
                Current: reading.Current));
        }

        return entries;
    }
}
```

- [ ] **Step 5: Run to verify the tests pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~WorkflowTelemetryBridgeTests`
Expected: PASS — 18 test cases.

- [ ] **Step 6: Implement applyTelemetry in JS**

In `wwwroot/js/workflow-canvas.js`, replace the stub:

```javascript
        // One call per coalesced tick. Everything here is a CSS custom property write or a
        // class toggle — no layout reads, no Blazor involvement, no re-render.
        applyTelemetry(host, entries) {
            const s = stateOf(host);
            if (!s || !entries) return;

            for (const e of entries) {
                const node = s.world.querySelector(`.wf-node[data-node-id="${e.nodeId}"]`);
                if (!node) continue;

                node.style.setProperty("--wf-status", e.statusHsl);
                node.classList.toggle("wf-node--active", e.flow !== 0);

                setText(node, "status", e.statusName);
                setText(node, "voltage", e.voltage.toFixed(2) + " V");
                setText(node, "current", e.current.toFixed(2) + " A");
                setText(node, "soc", Math.round(e.soc * 100) + "%");

                // The battery wired to this channel, plus the edge between them.
                applyToPowerNeighbours(s, e);
            }
        },
```

and add these helpers inside the IIFE:

```javascript
    function setText(node, role, value) {
        const el = node.querySelector(`[data-role="${role}"]`);
        if (el && el.textContent !== value) el.textContent = value;
    }

    // A channel's status drives the battery on the other end of its power edge, and the edge
    // itself. Both are found by walking the rendered DOM rather than re-querying C#.
    function applyToPowerNeighbours(s, e) {
        const layer = s.world.querySelector(".wf-edge-layer");
        if (!layer) return;

        const edges = layer.querySelectorAll(
            `path.wf-edge--power[data-from="${e.nodeId}"], path.wf-edge--power[data-to="${e.nodeId}"]`);

        edges.forEach(path => {
            path.style.setProperty("--wf-status", e.statusHsl);
            path.classList.toggle("wf-edge--flowing", e.flow !== 0);
            path.classList.toggle("wf-edge--flowing-reverse", e.flow < 0);

            const otherId = path.dataset.from === e.nodeId ? path.dataset.to : path.dataset.from;
            const other = s.world.querySelector(`.wf-node[data-node-id="${otherId}"]`);
            if (!other) return;

            const battery = other.querySelector('[data-role="battery"]');
            if (!battery) return;

            battery.style.setProperty("--wf-status", e.statusHsl);
            battery.style.setProperty("--wf-soc", String(e.soc));
            battery.classList.toggle("wf-battery--charging", e.flow > 0);
            battery.classList.toggle("wf-battery--fault", e.statusName === "error");

            setText(other, "soc", Math.round(e.soc * 100) + "%");
        });
    }
```

Bump the script `?version=` in `App.razor` to `0.3`.

- [ ] **Step 7: Wire the subscription in the page**

Add to `WorkflowCanvasPage.razor`. The `ChannelManager` event name, its argument type, and how a
channel's voltage/current/SoC are read must be copied from `DashboardView.razor` — it already
does exactly this and is the reference implementation.

```csharp
@inject ChannelManager CM

    private CoalescingRunner? _telemetryRunner;
    private HashSet<long> _subscribedChannelIds = new();

    private void StartTelemetry()
    {
        // CoalescingRunner is mandatory here, not optional: a plain async void handler on a
        // hardware event re-introduces the "second operation on this context" bug (ADR-6).
        _telemetryRunner = new CoalescingRunner(TimeSpan.FromMilliseconds(200), PushTelemetryAsync);
        CM.HardwareManagerChanged += OnHardwareChanged;
    }

    private void OnHardwareChanged(object? sender, EventArgs e) => _telemetryRunner?.Request();

    private async Task PushTelemetryAsync()
    {
        if (!_jsReady || _subscribedChannelIds.Count == 0) return;

        // Read the current values for the subscribed channels only. Copy the exact accessor
        // DashboardView uses for RealTime.RealTimeRecord.
        var readings = new Dictionary<long, ChannelTelemetry>();
        foreach (var channelId in _subscribedChannelIds)
        {
            var circuit = CM.FindCircuitByChannelId(channelId);   // use the real lookup
            if (circuit is null) continue;

            var record = circuit.RealTime.RealTimeRecord;
            readings[channelId] = new ChannelTelemetry(
                record.CircuitStatus, record.SocFraction, record.Voltage, record.Current);
        }

        var entries = WorkflowTelemetryBridge.BuildEntries(_graph, readings);
        if (entries.Count == 0) return;

        // ONE interop call for the whole batch, and no StateHasChanged anywhere in this method.
        await JS.InvokeVoidAsync("workflowCanvas.applyTelemetry", _canvasHost, entries);
    }

    /// <summary>Call after every structural graph change.</summary>
    private void RefreshSubscription() =>
        _subscribedChannelIds = WorkflowTelemetryBridge.OnCanvasChannelIds(_graph, _staleNodeIds);
```

Call `StartTelemetry()` at the end of `OnAfterRenderAsync(firstRender: true)`, call
`RefreshSubscription()` at the end of every method that changes the graph (`HandlePlaceDevice`,
`HandlePlaceBattery`, `HandleDeleteNode`, `OnConnect`, and the load path in Task 16), and unhook
in `DisposeAsync`:

```csharp
        CM.HardwareManagerChanged -= OnHardwareChanged;
        _telemetryRunner?.Dispose();
```

If `CoalescingRunner`'s constructor or `FindCircuitByChannelId` do not exist with these exact
signatures, read `Services/Implementations/CoalescingRunner.cs` and `Services/ChannelManager.cs`
and adapt — the shape of the solution is what matters: coalesce, then one batched call.

- [ ] **Step 8: Build and verify in the browser**

With `python HardwareSimulator/run_sim.py -d 1 -n 8` running and a program started on a channel:
1. Placed channel nodes show live voltage, current, SoC and a status pill.
2. A charging channel glows and its status pill turns the charge colour.
3. Attach a battery to a charging channel — the power edge flows **toward** the battery and the
   glyph fills and shimmers.
4. Discharging reverses the flow direction.
5. Pausing the channel stops all motion but keeps the colour.
6. A channel that is not on the canvas costs nothing — remove one and confirm its data stops
   arriving.
7. Open DevTools → Network → WS and watch the SignalR frames while telemetry updates. **There
   must be no steady stream of render frames.** A burst on click is correct; a continuous
   stream while merely watching means something calls `StateHasChanged` on telemetry.
8. Record a Performance profile for five seconds: no per-frame `Layout` or `Paint`.

- [ ] **Step 9: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowTelemetryBridge.cs Models/DTOs/Workflow/TelemetryEntry.cs wwwroot/js/workflow-canvas.js Components/ BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): coalesced telemetry bridge writing CSS variables"
```

---

### Task 16: Named layouts and the minimap

**Files:**
- Create: `Components/UI/WorkflowCanvas/LayoutSwitcher.razor`
- Create: `Components/UI/WorkflowCanvas/Minimap.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` — save/load/delete wiring
- Modify: `wwwroot/js/workflow-canvas.js` — minimap viewport reporting
- Modify: `wwwroot/css/workflow-canvas.css` — switcher and minimap styles
- Test: `BatteryTestingSystem.Tests/Components/LayoutSwitcherTests.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/MinimapProjectionTests.cs`
- Create: `Services/Implementations/Workflow/MinimapProjection.cs`

**Interfaces:**
- Consumes: `IWorkflowLayoutService`, `LayoutSummary`, `LayoutLoadResult` (Task 6); `WorkflowGraph`, `CanvasViewport` (Task 1); `WorkflowEdgeGeometry.NodeWidthFor` (Task 10).
- Produces:
  - `record MinimapBox(double X, double Y, double Width, double Height)`
  - `MinimapProjection.ContentBounds(WorkflowGraph) -> MinimapBox`
  - `MinimapProjection.Scale(MinimapBox content, double mapWidth, double mapHeight) -> double`
  - `MinimapProjection.NodeBoxes(WorkflowGraph, MinimapBox content, double scale) -> IReadOnlyList<MinimapBox>`
  - `LayoutSwitcher` component: `IReadOnlyList<LayoutSummary> Layouts`, `long? CurrentId`, `bool IsDirty`, `EventCallback<long> OnLoad`, `EventCallback<string> OnSaveAs`, `EventCallback OnSave`, `EventCallback<long> OnDelete`
  - `Minimap` component: `WorkflowGraph Graph`, `CanvasViewport Viewport`

- [ ] **Step 1: Write the failing projection tests**

Create `BatteryTestingSystem.Tests/Services/Workflow/MinimapProjectionTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The minimap is pure projection maths: content bounds, a scale that fits them into a fixed
/// box, and a scaled rectangle per node. Divide-by-zero on a single-node or empty graph is the
/// obvious failure and is pinned here.
/// </summary>
public class MinimapProjectionTests
{
    private static WorkflowGraph GraphAt(params (double X, double Y)[] points) =>
        WorkflowGraph.Empty with
        {
            Nodes = points
                .Select((p, i) => new WorkflowNode($"n{i}", NodeKind.Channel, i, p.X, p.Y, null))
                .ToList()
        };

    [Fact]
    public void ContentBounds_SpansEveryNodeIncludingItsWidth()
    {
        var bounds = MinimapProjection.ContentBounds(GraphAt((0, 0), (400, 200)));

        Assert.Equal(0, bounds.X);
        Assert.Equal(0, bounds.Y);
        Assert.True(bounds.Width >= 600, "must include the width of the right-most node");
        Assert.True(bounds.Height >= 200);
    }

    [Fact]
    public void ContentBounds_HandlesNegativeCoordinates()
    {
        // Nodes can be dragged left of the origin; the minimap must not clip them.
        var bounds = MinimapProjection.ContentBounds(GraphAt((-300, -150), (0, 0)));

        Assert.Equal(-300, bounds.X);
        Assert.Equal(-150, bounds.Y);
    }

    [Fact]
    public void ContentBounds_ReturnsAUnitBoxForAnEmptyGraph()
    {
        // A zero-size box would make Scale divide by zero.
        var bounds = MinimapProjection.ContentBounds(WorkflowGraph.Empty);

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void Scale_FitsTheContentInsideTheMap()
    {
        var content = new MinimapBox(0, 0, 2000, 1000);

        var scale = MinimapProjection.Scale(content, 200, 120);

        Assert.True(content.Width * scale <= 200.001);
        Assert.True(content.Height * scale <= 120.001);
    }

    [Fact]
    public void Scale_UsesTheLimitingDimension()
    {
        // 2000x100 into 200x120 is width-limited: 0.1, not 1.2.
        Assert.Equal(0.1, MinimapProjection.Scale(new MinimapBox(0, 0, 2000, 100), 200, 120), 3);
    }

    [Fact]
    public void Scale_NeverReturnsZeroOrInfinity()
    {
        Assert.True(MinimapProjection.Scale(new MinimapBox(0, 0, 0, 0), 200, 120) > 0);
        Assert.True(double.IsFinite(MinimapProjection.Scale(new MinimapBox(0, 0, 0, 0), 200, 120)));
    }

    [Fact]
    public void NodeBoxes_TranslatesToTheOriginThenScales()
    {
        var graph = GraphAt((-100, -50), (100, 50));
        var content = MinimapProjection.ContentBounds(graph);

        var boxes = MinimapProjection.NodeBoxes(graph, content, 0.1);

        // The top-left-most node must land at 0,0 in map space.
        Assert.Equal(0, boxes[0].X, 3);
        Assert.Equal(0, boxes[0].Y, 3);
        Assert.Equal(20, boxes[1].X, 3);    // (100 - -100) * 0.1
    }

    [Fact]
    public void NodeBoxes_ReturnsOneBoxPerNode()
    {
        var graph = GraphAt((0, 0), (10, 10), (20, 20));
        var content = MinimapProjection.ContentBounds(graph);

        Assert.Equal(3, MinimapProjection.NodeBoxes(graph, content, 0.1).Count);
    }
}
```

Add `using System.Linq;` if the compiler asks.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~MinimapProjectionTests`
Expected: FAIL — `MinimapProjection` does not exist.

- [ ] **Step 3: Write the projection**

Create `Services/Implementations/Workflow/MinimapProjection.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record MinimapBox(double X, double Y, double Width, double Height);

/// <summary>
/// World coordinates to minimap coordinates. Pure maths, kept out of the Razor file so the
/// degenerate cases — empty graph, single node, everything on one row — are testable rather
/// than discovered as a divide-by-zero in the browser.
/// </summary>
public static class MinimapProjection
{
    private const double MinExtent = 1;

    public static MinimapBox ContentBounds(WorkflowGraph graph)
    {
        if (graph.Nodes.Count == 0) return new MinimapBox(0, 0, MinExtent, MinExtent);

        var minX = graph.Nodes.Min(n => n.X);
        var minY = graph.Nodes.Min(n => n.Y);
        var maxX = graph.Nodes.Max(n => n.X + WorkflowEdgeGeometry.NodeWidthFor(n.Kind));
        var maxY = graph.Nodes.Max(n => n.Y + WorkflowEdgeGeometry.NodeHeightFor(n.Kind));

        return new MinimapBox(
            minX, minY,
            Math.Max(MinExtent, maxX - minX),
            Math.Max(MinExtent, maxY - minY));
    }

    public static double Scale(MinimapBox content, double mapWidth, double mapHeight)
    {
        var width = Math.Max(MinExtent, content.Width);
        var height = Math.Max(MinExtent, content.Height);

        return Math.Min(mapWidth / width, mapHeight / height);
    }

    public static IReadOnlyList<MinimapBox> NodeBoxes(
        WorkflowGraph graph, MinimapBox content, double scale) =>
        graph.Nodes
            .Select(n => new MinimapBox(
                (n.X - content.X) * scale,
                (n.Y - content.Y) * scale,
                Math.Max(1, WorkflowEdgeGeometry.NodeWidthFor(n.Kind) * scale),
                Math.Max(1, WorkflowEdgeGeometry.NodeHeightFor(n.Kind) * scale)))
            .ToList();
}
```

- [ ] **Step 4: Run to verify the tests pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~MinimapProjectionTests`
Expected: PASS — 8 tests.

- [ ] **Step 5: Write the minimap**

Create `Components/UI/WorkflowCanvas/Minimap.razor`:

```razor
@using System.Globalization
@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Services.Implementations.Workflow

@* Overview only — not interactive in v1. It re-renders on structural change, which is exactly
   when the node positions it draws can actually differ. *@

<div class="wf-minimap" aria-hidden="true">
    <svg width="@MapWidth" height="@MapHeight">
        @foreach (var box in _boxes)
        {
            <rect x="@F(box.X)" y="@F(box.Y)" width="@F(box.Width)" height="@F(box.Height)"
                  class="wf-minimap__node" rx="1" />
        }
    </svg>
</div>

@code {
    private const double MapWidth = 180;
    private const double MapHeight = 110;

    [Parameter, EditorRequired] public WorkflowGraph Graph { get; set; } = WorkflowGraph.Empty;

    private IReadOnlyList<MinimapBox> _boxes = Array.Empty<MinimapBox>();

    protected override void OnParametersSet()
    {
        var content = MinimapProjection.ContentBounds(Graph);
        var scale = MinimapProjection.Scale(content, MapWidth, MapHeight);
        _boxes = MinimapProjection.NodeBoxes(Graph, content, scale);
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 6: Write the layout switcher**

Create `Components/UI/WorkflowCanvas/LayoutSwitcher.razor`:

```razor
@using BatteryTestingSystem.Services.Interfaces

<div class="wf-switcher">
    <select class="wf-select" value="@(CurrentId?.ToString() ?? "")" @onchange="HandleSelect">
        <option value="">(unsaved layout)</option>
        @foreach (var layout in Layouts)
        {
            <option value="@layout.Id">@layout.Name</option>
        }
    </select>

    <button class="wf-btn" disabled="@(!IsDirty || CurrentId is null)" @onclick="() => OnSave.InvokeAsync()">
        Save@(IsDirty ? " *" : "")
    </button>

    @if (_naming)
    {
        <input class="wf-input" placeholder="Layout name" @bind="_newName" @bind:event="oninput" />
        <button class="wf-btn" disabled="@string.IsNullOrWhiteSpace(_newName)" @onclick="ConfirmSaveAs">Create</button>
        <button class="wf-btn" @onclick="() => _naming = false">Cancel</button>
    }
    else
    {
        <button class="wf-btn" @onclick="() => { _naming = true; _newName = string.Empty; }">Save as…</button>
    }

    @if (CurrentId is { } id)
    {
        <button class="wf-btn wf-btn--danger" @onclick="() => OnDelete.InvokeAsync(id)">Delete</button>
    }
</div>

@code {
    [Parameter] public IReadOnlyList<LayoutSummary> Layouts { get; set; } = Array.Empty<LayoutSummary>();
    [Parameter] public long? CurrentId { get; set; }
    [Parameter] public bool IsDirty { get; set; }
    [Parameter] public EventCallback<long> OnLoad { get; set; }
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback<string> OnSaveAs { get; set; }
    [Parameter] public EventCallback<long> OnDelete { get; set; }

    private bool _naming;
    private string _newName = string.Empty;

    private Task HandleSelect(ChangeEventArgs e) =>
        long.TryParse(e.Value?.ToString(), out var id) ? OnLoad.InvokeAsync(id) : Task.CompletedTask;

    private async Task ConfirmSaveAs()
    {
        var name = _newName.Trim();
        if (name.Length == 0) return;

        _naming = false;
        await OnSaveAs.InvokeAsync(name);
    }
}
```

- [ ] **Step 7: Write the switcher tests**

Create `BatteryTestingSystem.Tests/Components/LayoutSwitcherTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Services.Interfaces;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Save gating is the point of these tests: Save must be impossible when there is nothing to
/// save or no layout to save into, so the operator cannot silently create empty rows.
/// </summary>
public class LayoutSwitcherTests : TestContext
{
    private static IReadOnlyList<LayoutSummary> Layouts() => new List<LayoutSummary>
    {
        new(1, "Bench A", null, DateTime.Now),
        new(2, "Bench B", null, DateTime.Now),
    };

    [Fact]
    public void ListsEverySavedLayoutPlusAnUnsavedOption()
    {
        var cut = RenderComponent<LayoutSwitcher>(p => p.Add(x => x.Layouts, Layouts()));

        Assert.Equal(3, cut.FindAll("option").Count);
    }

    [Fact]
    public void SaveIsDisabledWhenNothingHasChanged()
    {
        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, 1L)
            .Add(x => x.IsDirty, false));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void SaveIsDisabledWhenThereIsNoCurrentLayout_EvenIfDirty()
    {
        // An unsaved canvas has no row to update; the operator must use Save as… instead.
        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, (long?)null)
            .Add(x => x.IsDirty, true));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void SaveIsEnabledAndMarkedWhenDirtyWithACurrentLayout()
    {
        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, 1L)
            .Add(x => x.IsDirty, true));

        var save = cut.Find("button");
        Assert.False(save.HasAttribute("disabled"));
        Assert.Contains("*", save.TextContent);
    }

    [Fact]
    public void SelectingALayoutRaisesOnLoadWithItsId()
    {
        long? loaded = null;

        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.OnLoad, (long id) => loaded = id));

        cut.Find("select").Change("2");

        Assert.Equal(2L, loaded);
    }

    [Fact]
    public void SaveAsRequiresANonEmptyName()
    {
        string? saved = null;

        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.OnSaveAs, (string n) => saved = n));

        cut.FindAll("button").Last().Click();       // "Save as…"
        cut.Find("input").Input("   ");

        Assert.True(cut.FindAll("button").First(b => b.TextContent == "Create").HasAttribute("disabled"));
        Assert.Null(saved);
    }

    [Fact]
    public void SaveAsTrimsTheNameBeforeRaisingTheCallback()
    {
        string? saved = null;

        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.OnSaveAs, (string n) => saved = n));

        cut.FindAll("button").Last().Click();
        cut.Find("input").Input("  Bench C  ");
        cut.FindAll("button").First(b => b.TextContent == "Create").Click();

        Assert.Equal("Bench C", saved);
    }

    [Fact]
    public void DeleteAppearsOnlyWhenALayoutIsOpen()
    {
        var unsaved = RenderComponent<LayoutSwitcher>(p => p.Add(x => x.Layouts, Layouts()));
        Assert.DoesNotContain("Delete", unsaved.Markup);

        var open = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, 1L));
        Assert.Contains("Delete", open.Markup);
    }
}
```

Add `using System.Linq;` for `.Last()` / `.First()`.

- [ ] **Step 8: Add the styles**

Append to `wwwroot/css/workflow-canvas.css`:

```css
.wf-switcher { display: flex; align-items: center; gap: 0.375rem; flex-wrap: wrap; }

.wf-switcher .wf-select { width: auto; min-width: 10rem; }

.wf-input {
    padding: 0.25rem 0.375rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.375rem;
    background: hsl(var(--background));
    color: hsl(var(--foreground));
    font-size: 0.8125rem;
}

.wf-minimap {
    position: absolute;
    right: 0.75rem;
    bottom: 0.75rem;
    padding: 0.25rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.375rem;
    background: hsl(var(--card) / 0.9);
    pointer-events: none;
}

.wf-minimap__node { fill: hsl(var(--muted-foreground) / 0.65); }
```

- [ ] **Step 9: Wire save and load into the page**

Add to the toolbar in `WorkflowCanvasPage.razor`:

```razor
        <LayoutSwitcher Layouts="_layouts" CurrentId="_currentLayoutId" IsDirty="_dirty"
                        OnLoad="HandleLoad" OnSave="HandleSave"
                        OnSaveAs="HandleSaveAs" OnDelete="HandleDelete" />
```

Put the minimap inside `.wf-canvas-host`, as a sibling of `.wf-world` so the canvas transform
does not move it:

```razor
    <Minimap Graph="_graph" />
```

and add:

```csharp
    private long? _currentLayoutId;

    private async Task HandleLoad(long id)
    {
        var result = await LayoutService.LoadAsync(id, _userId, _topology);

        if (!result.Ok)
        {
            Toast.ShowError(result.Error!);
            return;
        }

        _graph = result.Graph!;
        _staleNodeIds = result.StaleNodeIds;
        _currentLayoutId = id;
        _selectedNodeId = null;
        _dirty = false;

        RefreshSubscription();

        if (result.StaleNodeIds.Count > 0)
        {
            Toast.ShowWarning(
                $"{result.StaleNodeIds.Count} node(s) refer to hardware that no longer exists. "
              + "They are shown dimmed and receive no live data.");
        }

        await InvokeAsync(StateHasChanged);

        // Restore the saved viewport after the DOM exists, or fitToContent measures nothing.
        if (_jsReady)
        {
            await JS.InvokeVoidAsync("workflowCanvas.setViewport", _canvasHost,
                _graph.Viewport.PanX, _graph.Viewport.PanY, _graph.Viewport.Zoom);
        }
    }

    private async Task HandleSave()
    {
        if (_currentLayoutId is not { } id) return;

        var name = _layouts.FirstOrDefault(l => l.Id == id)?.Name ?? "Layout";
        await PersistAsync(id, name);
    }

    private async Task HandleSaveAs(string name) => await PersistAsync(null, name);

    private async Task PersistAsync(long? id, string name)
    {
        // The viewport lives in JS while the operator pans, so pull the current value rather
        // than trusting the copy in _graph.
        var viewport = _viewport;

        var savedId = await LayoutService.SaveAsync(
            id, name, null, _graph with { Viewport = viewport }, _userId);

        _currentLayoutId = savedId;
        _dirty = false;
        _layouts = await LayoutService.ListAsync(_userId);

        Toast.ShowSuccess($"Saved '{name}'.");
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleDelete(long id)
    {
        if (!await LayoutService.DeleteAsync(id, _userId)) return;

        if (_currentLayoutId == id)
        {
            _currentLayoutId = null;
            _graph = WorkflowGraph.Empty;
            _staleNodeIds = Array.Empty<string>();
            _selectedNodeId = null;
            RefreshSubscription();
        }

        _layouts = await LayoutService.ListAsync(_userId);
        await InvokeAsync(StateHasChanged);
    }
```

Match `Toast.ShowWarning` / `ShowSuccess` to the real `ToastService` method names used in
`DashboardView.razor`.

- [ ] **Step 10: Run all the new component tests**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter FullyQualifiedName~LayoutSwitcherTests`
Expected: PASS — 8 tests.

- [ ] **Step 11: Build and verify in the browser**

1. Place a device, attach a program and a battery, pan somewhere distinctive.
2. **Save as…** with a name — a toast confirms and the name appears in the dropdown.
3. Reload the page, pick the layout from the dropdown — nodes, positions, attachments, edges and
   the pan/zoom position all come back.
4. Move a node; the Save button gains its `*`. Click Save; the `*` clears.
5. Create a second layout and switch between them — each keeps its own arrangement.
6. The minimap shows a scaled overview and updates as nodes are added or moved.
7. **The stale path:** save a layout, delete one of its channels from the Circuits page, then
   reload the layout. The node must appear dimmed with a warning badge, a toast must report the
   count, and **the page must not error**. This is the behaviour the whole soft-reference design
   exists to produce — verify it deliberately.
8. Delete a layout; it disappears from the dropdown and the canvas clears.
9. No console errors.

- [ ] **Step 12: Commit**

```bash
git add Components/UI/WorkflowCanvas/ Components/Pages/Workflows/ Services/Implementations/Workflow/MinimapProjection.cs wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/
git commit -m "feat(workflow-canvas): named layout save/load and minimap"
```

---

### Task 17: Verification pass, documentation, and the experiment verdict

The feature is code-complete. This task is about deciding whether it earned its place.

**Files:**
- Create: `docs/workflow-canvas-experiment.md`
- Modify: `.claude/sessions/<current session file>.md`
- Modify: `.claude/tasks/2026-08-22_workflow-canvas-playground.md`
- Modify: `.claude/AGENT.md`, `.claude/TASKS.md`, `.claude/SESSION.md`, `.claude/CODEBASE_MAP.md`

- [ ] **Step 1: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: every pre-existing test still passes, plus roughly 130 new cases from Tasks 1-16.
**Any pre-existing failure is a regression from this branch — fix it before continuing.**

- [ ] **Step 2: Run the scale test**

```bash
python HardwareSimulator/run_sim.py -d 10 -n 64
```

Then in the app: create a layout, auto-import a full 64-channel device, attach batteries to at
least 8 channels, start programs on them, and leave it running for five minutes.

Record honest answers — these are the experiment's actual output:

| Question | Answer |
|---|---|
| Is a 64-channel graph readable, or spaghetti? | |
| Frame rate while panning with animations on (DevTools FPS meter)? | |
| Any per-frame Layout or Paint in a Performance profile? | |
| SignalR traffic while merely watching telemetry — quiet, or a stream? | |
| Server CPU with the canvas open vs. the old dashboard open? | |
| Time to arrange a useful 8-channel layout by hand? | |
| Does the flow animation communicate charge direction at a glance? | |
| Would you use this instead of the dashboard? | |

- [ ] **Step 3: Run the failure-path checks**

1. Delete a device that a saved layout references; reload the layout — dimmed nodes, a toast, no
   error.
2. Stop the simulator mid-session — nodes go offline and all motion stops; nothing throws.
3. Open the canvas with no devices registered at all — an empty palette and an empty canvas, no
   error.
4. Open the same layout in two browser tabs, edit and save in both — the last save wins, and
   neither tab errors.
5. Set `Features:WorkflowCanvas` to `false`, restart — the menu entry is gone and the rest of the
   app is unaffected.
6. Set `Features:LegacyDashboard` back to `true` — the old dashboard returns, fully working.

- [ ] **Step 4: Write the experiment report**

Create `docs/workflow-canvas-experiment.md` containing: what was built, the Step 2 measurements
verbatim, what worked, what did not, what it would take to make this production-ready, and a
recommendation — keep, keep with changes, or delete the branch. Be blunt; a report that
concludes "it is fine" without numbers is worthless.

- [ ] **Step 5: Update the agent memory**

Per `CLAUDE.md`, update `.claude/`:
- the current session file — what was built, files created, discoveries;
- `.claude/tasks/2026-08-22_workflow-canvas-playground.md` — mark T-47 complete with the verdict;
- `.claude/TASKS.md` — move T-47 from Active to Recently Completed;
- `.claude/CODEBASE_MAP.md` — the new `Components/UI/WorkflowCanvas/`,
  `Services/Implementations/Workflow/`, `workflow-canvas.css` / `.js`, the `WorkflowLayouts`
  table, and the two gotchas worth carrying forward: the `CircuitStatus.Countinue` /
  `--status-continue` mismatch, and the rule that telemetry writes CSS variables rather than
  re-rendering;
- `.claude/AGENT.md` — bump the header timestamp and session number, and update the live-state table.

- [ ] **Step 6: Commit**

```bash
git add docs/workflow-canvas-experiment.md .claude/
git commit -m "docs(workflow-canvas): experiment report and memory update"
```

- [ ] **Step 7: Stop and report — do not merge**

Present the report to the user. **The branch stays unmerged** until they decide. If the verdict
is "keep", merging is a separate, explicitly requested task; if it is "delete", the four modified
shared files revert cleanly and the migration is dropped with
`dotnet ef migrations remove` after `dotnet ef database update <previous-migration>`.

---

## Task Summary

| # | Task | Kind | New tests |
|---|---|---|---|
| 1 | Graph document + JSON round-trip | pure | 8 |
| 2 | Graph validator | pure | 19 |
| 3 | Auto-import builder | pure | 10 |
| 4 | Auto-layout | pure | 7 |
| 5 | Entity, repository, migration | EF | 8 |
| 6 | Layout service + stale resolution | service | 14 |
| 7 | Flags, DI, menu, page shell | wiring | 3 |
| 8 | Stylesheet + pan/zoom JS | UI | browser |
| 9 | Node components + status CSS | UI | 20 |
| 10 | Edge geometry + SVG layer | mixed | 9 |
| 11 | Drag, select, connect | mixed | 17 |
| 12 | Palette + placement | mixed | 9 |
| 13 | Properties dock, panel rails, board collapse | UI | 13 |
| 14 | Battery glyph + animations | UI | 8 |
| 15 | Telemetry bridge | mixed | 18 |
| 16 | Layout switcher + minimap | mixed | 16 |
| 17 | Verification + report | manual | — |

Tasks 1-6 are pure logic and persistence with no UI at all; a reviewer can accept or reject each
independently. Task 7 is the first thing visible in a browser. Tasks 8-16 each add one
interaction and each end with an explicit browser check, because the thing being evaluated —
whether this feels good — cannot be asserted in a test.
