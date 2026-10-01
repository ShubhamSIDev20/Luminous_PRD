using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Node labels must read as the physical address an operator sees on the dashboard
/// (DeviceChannel.razor:1268), not the database primary key. Showing "Channel 579" for what the
/// rest of the app calls "1-8-2" makes the two surfaces impossible to cross-reference.
/// </summary>
public class WorkflowNodeLabellerTests
{
    private static TopologySnapshot Topology() => new(new List<TopologyDevice>
    {
        new(1, "SIM_DEVICE_001_1", new List<TopologyBoard>
        {
            new(76, 8, new List<TopologyChannel> { new(579, 2), new(580, 3) }),
            new(70, 1, new List<TopologyChannel> { new(500, 1) }),
        }),
        new(2, "SIM_DEVICE_002_1", new List<TopologyBoard>
        {
            new(90, 4, new List<TopologyChannel> { new(700, 5) }),
        }),
    });

    private static WorkflowNode Node(NodeKind kind, long? entityId) =>
        new($"{kind}-{entityId}", kind, entityId, 0, 0, null);

    [Fact]
    public void ChannelLabelIsTheThreePartPhysicalAddress()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("1-8-2", labeller.Label(Node(NodeKind.Channel, 579)));
    }

    [Fact]
    public void ChannelLabelDisambiguatesAcrossDevices()
    {
        // Channel number 5 on device 2 must not be confusable with any channel on device 1.
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("2-4-5", labeller.Label(Node(NodeKind.Channel, 700)));
    }

    [Fact]
    public void BoardLabelUsesTheBoardNumberNotItsPrimaryKey()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Board 8", labeller.Label(Node(NodeKind.Board, 76)));
    }

    [Fact]
    public void DeviceLabelIsTheDeviceName()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("SIM_DEVICE_001_1", labeller.Label(Node(NodeKind.Device, 1)));
    }

    [Fact]
    public void BatteryLabelIsAConstant()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Battery", labeller.Label(Node(NodeKind.Battery, 9)));
    }

    [Fact]
    public void UnresolvableIdsFallBackVisiblyRatherThanThrowing()
    {
        // A stale node points at deleted hardware. It must still render, and must look obviously
        // unresolved rather than silently borrowing another channel's address.
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Channel ?", labeller.Label(Node(NodeKind.Channel, 99999)));
        Assert.Equal("Board ?", labeller.Label(Node(NodeKind.Board, 99999)));
        Assert.Equal("Device ?", labeller.Label(Node(NodeKind.Device, 99999)));
    }

    [Fact]
    public void ANodeWithNoEntityIdFallsBackToo()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Channel ?", labeller.Label(Node(NodeKind.Channel, null)));
    }

    [Fact]
    public void TryGetChannelKeyReturnsTheCircuitMatchingTuple()
    {
        // Circuits are matched by (DeviceID, SecondaryBoardNumber, ChannelNumber), never by the
        // database Channel.Id — this lookup is what bridges the two.
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.True(labeller.TryGetChannelKey(579, out var key));
        Assert.Equal((1, 8, 2), key);
    }

    [Fact]
    public void TryGetChannelKeyFailsClosedForUnknownIds()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.False(labeller.TryGetChannelKey(99999, out _));
    }

    [Fact]
    public void ChannelIdsForNodeEntityRollsUpABoard()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal(new[] { 579L, 580L },
            labeller.ChannelIdsForNodeEntity(NodeKind.Board, 76).OrderBy(i => i));
    }

    [Fact]
    public void ChannelIdsForNodeEntityRollsUpEveryBoardOnADevice()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal(new[] { 500L, 579L, 580L },
            labeller.ChannelIdsForNodeEntity(NodeKind.Device, 1).OrderBy(i => i));
    }

    [Fact]
    public void ChannelIdsForNodeEntityIsEmptyForUnknownEntities()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Empty(labeller.ChannelIdsForNodeEntity(NodeKind.Device, 99999));
    }
}
