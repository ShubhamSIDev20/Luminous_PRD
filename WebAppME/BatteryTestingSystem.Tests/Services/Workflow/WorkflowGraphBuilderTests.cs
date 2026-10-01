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

    // ============================================================ BoardsForDevice

    [Fact]
    public void BoardsForDevice_ReturnsOnlyThatDevicesBoards()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(Device(boards: 2, channelsPerBoard: 1));

        var boards = WorkflowGraphBuilder.BoardsForDevice(graph, WorkflowGraphBuilder.DeviceNodeId(1));

        Assert.Equal(2, boards.Count);
        Assert.All(boards, b => Assert.Equal(NodeKind.Board, b.Kind));
    }

    [Fact]
    public void BoardsForDevice_OrdersByEntityIdRegardlessOfNodeListOrder()
    {
        // BuildForDevice(boards: 2) creates board entity ids 10 then 20 (b * 10) — construct the
        // graph with them deliberately reversed in the node list to prove the ordering is by
        // EntityId, not by list position.
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new List<WorkflowNode>
            {
                new("dev-1", NodeKind.Device, 1, 0, 0, null),
                new("brd-20", NodeKind.Board, 20, 0, 0, null),
                new("brd-10", NodeKind.Board, 10, 0, 0, null),
            },
            Edges = new List<WorkflowEdge>
            {
                new("e1", "dev-1", "brd-20", EdgeKind.Topology),
                new("e2", "dev-1", "brd-10", EdgeKind.Topology),
            },
        };

        var boards = WorkflowGraphBuilder.BoardsForDevice(graph, "dev-1");

        Assert.Equal(new[] { "brd-10", "brd-20" }, boards.Select(b => b.Id));
    }

    [Fact]
    public void BoardsForDevice_ReturnsEmptyForADeviceWithNoBoards()
    {
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new List<WorkflowNode> { new("dev-9", NodeKind.Device, 9, 0, 0, null) },
        };

        Assert.Empty(WorkflowGraphBuilder.BoardsForDevice(graph, "dev-9"));
    }

    // ============================================================ PlacedChannelIds

    [Fact]
    public void PlacedChannelIds_ReturnsEveryPlacedChannelsEntityId()
    {
        var graph = WorkflowGraphBuilder.BuildForDevice(Device(boards: 2, channelsPerBoard: 2));

        var placed = WorkflowGraphBuilder.PlacedChannelIds(graph);

        // Device(boards:2, channelsPerBoard:2) creates channel ids b*100+c => 101,102,201,202
        Assert.Equal(new[] { 101L, 102L, 201L, 202L }, placed.OrderBy(id => id));
    }

    [Fact]
    public void PlacedChannelIds_IgnoresNonChannelNodes()
    {
        // Device and Board nodes carry EntityIds too - including them would make a device's
        // own id collide with a channel id and wrongly grey out a placeable channel.
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new List<WorkflowNode>
            {
                new("dev-1", NodeKind.Device, 1, 0, 0, null),
                new("brd-10", NodeKind.Board, 10, 0, 0, null),
                new("bat-1", NodeKind.Battery, 7, 0, 0, null),
                new("chn-100", NodeKind.Channel, 100, 0, 0, null),
            },
        };

        Assert.Equal(new[] { 100L }, WorkflowGraphBuilder.PlacedChannelIds(graph));
    }

    [Fact]
    public void PlacedChannelIds_IsEmptyForAnEmptyGraph()
    {
        Assert.Empty(WorkflowGraphBuilder.PlacedChannelIds(WorkflowGraph.Empty));
    }

    [Fact]
    public void PlacedChannelIds_SkipsAChannelNodeWithNoEntityId()
    {
        var graph = WorkflowGraph.Empty with
        {
            Nodes = new List<WorkflowNode> { new("chn-x", NodeKind.Channel, null, 0, 0, null) },
        };

        Assert.Empty(WorkflowGraphBuilder.PlacedChannelIds(graph));
    }
}
