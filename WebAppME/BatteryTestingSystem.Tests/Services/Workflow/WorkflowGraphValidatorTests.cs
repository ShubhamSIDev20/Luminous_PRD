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
