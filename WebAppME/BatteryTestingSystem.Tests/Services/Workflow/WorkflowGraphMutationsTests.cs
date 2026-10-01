using System;
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

    // ================================================================ HideCollapsedBoards

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
}
