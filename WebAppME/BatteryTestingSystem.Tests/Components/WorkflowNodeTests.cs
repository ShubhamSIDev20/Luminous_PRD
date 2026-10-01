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

        // Voltage/current are no longer fixed roles — they are the default two of the
        // configurable prop-{key} slots (Phase 2, spec D11). The status word moved to
        // 'circuit-status' when the node became a battery cell: the bridge writes "STOP" there
        // for an idle circuit, which the old 'status' pill never did.
        Assert.NotNull(cut.Find("[data-role='circuit-status']"));
        Assert.NotNull(cut.Find("[data-role='prop-Voltage']"));
        Assert.NotNull(cut.Find("[data-role='prop-Current']"));
        Assert.NotNull(cut.Find("[data-role='soc']"));
    }

    [Fact]
    public void CanvasSurface_RendersOneElementPerNode_ExceptBoardsWhichAreDevicePinsNotNodes()
    {
        // A board is a small pin dot on its own device's card (D18-revised), not a rendered
        // .wf-node of its own — this graph has 4 nodes but only 3 render as .wf-node.
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new()
            {
                Node("dev-1", NodeKind.Device),
                Node("brd-1", NodeKind.Board),
                Node("chn-1", NodeKind.Channel),
                Node("bat-1", NodeKind.Battery),
            },
            Edges = new()
            {
                new WorkflowEdge("e1", "dev-1", "brd-1", EdgeKind.Topology),
            },
        };

        var cut = RenderComponent<CanvasSurface>(p => p.Add(x => x.Graph, graph));

        Assert.Equal(3, cut.FindAll(".wf-node").Count);
        Assert.NotNull(cut.Find(".wf-pin"));
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
