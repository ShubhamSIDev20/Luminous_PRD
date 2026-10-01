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
    private static readonly IReadOnlyList<string> AvailableKeys = WorkflowNodeProperties.AvailableKeys;

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
            WorkflowGraph.Empty, Device(), null, 0, 0, AvailableKeys);

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
        var once = WorkflowPlacement.PlaceDevice(WorkflowGraph.Empty, Device(), null, 0, 0, AvailableKeys);

        var twice = WorkflowPlacement.PlaceDevice(once, Device(), null, 0, 0, AvailableKeys);

        Assert.Equal(once.Nodes.Count, twice.Nodes.Count);
        Assert.Equal(once.Edges.Count, twice.Edges.Count);
    }

    [Fact]
    public void PlaceDevice_AddsOnlyTheMissingChannels_WhenTheDeviceIsPartiallyPlaced()
    {
        var partial = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(channels: 3), new List<long> { 101 }, 0, 0, AvailableKeys);

        var full = WorkflowPlacement.PlaceDevice(partial, Device(channels: 3), null, 0, 0, AvailableKeys);

        Assert.Equal(3, full.Nodes.Count(n => n.Kind == NodeKind.Channel));
        Assert.Equal(3, full.Nodes.Select(n => n.Id).Distinct().Count(id => id.StartsWith("chn-")));
    }

    [Fact]
    public void PlaceDevice_AddsALaterChannelBesideItsAlreadyPlacedSiblings_NotBelowThem()
    {
        // Regression. HandlePlaceDevice always translated a newly-added channel's fresh,
        // in-isolation layout (which always computes column 0, row 0 for a single-channel
        // subgraph) by the SAME origin used for the device's very first placement - so a channel
        // added to an already-placed device landed in the same row/column as its already-placed
        // siblings, not below them.
        var device = Device(channels: 3);
        var partial = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, device, new List<long> { 101 }, 0, 0, AvailableKeys);

        var existingChannel = partial.Nodes.Single(n => n.EntityId == 101L);

        var full = WorkflowPlacement.PlaceDevice(partial, device, new List<long> { 102 }, 0, 0, AvailableKeys);

        var newChannel = full.Nodes.Single(n => n.EntityId == 102L);

        // Same board row (same Y), next column over (different X) - not shifted below.
        Assert.Equal(existingChannel.Y, newChannel.Y);
        Assert.NotEqual(existingChannel.X, newChannel.X);
    }

    [Fact]
    public void PlaceDevice_LeavesAGapForAChannelClaimedElsewhere_RatherThanShiftingItsSiblingsLeft()
    {
        var device = Device(channels: 3);
        // Channel 102 (the middle one) is never passed - simulating "claimed by another
        // workflow" - only 101 and 103 are ever placed.
        var result = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, device, new List<long> { 101, 103 }, 0, 0, AvailableKeys);

        var chn101 = result.Nodes.Single(n => n.EntityId == 101L);
        var chn103 = result.Nodes.Single(n => n.EntityId == 103L);

        // 103 sits where channel 3 always sits (two columns over from channel 1), not
        // immediately next to 101 as it would if 102's slot had been silently skipped over.
        Assert.Equal(chn101.X + 2 * WorkflowAutoLayout.ColumnWidth, chn103.X);
        Assert.Equal(2, result.Nodes.Count(n => n.Kind == NodeKind.Channel));
    }

    [Fact]
    public void PlaceDevice_PreservesThePositionsOfExistingNodes()
    {
        // The operator has arranged the canvas by hand. Placing a second device must not
        // re-run auto-layout over everything and undo that work.
        var first = WorkflowPlacement.PlaceDevice(WorkflowGraph.Empty, Device(1), null, 0, 0, AvailableKeys);
        first = WorkflowGraphMutations.MoveNode(first, "dev-1", 999, 888);

        var second = WorkflowPlacement.PlaceDevice(first, Device(2), null, 0, 400, AvailableKeys);

        var moved = second.Nodes.Single(n => n.Id == "dev-1");
        Assert.Equal(999, moved.X);
        Assert.Equal(888, moved.Y);
    }

    [Fact]
    public void PlaceDevice_OffsetsTheNewSubtreeByTheGivenOrigin()
    {
        var result = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(), null, originX: 100, originY: 50, AvailableKeys);

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
        Assert.Equal(7L, battery.EntityId);
        Assert.Equal(7, battery.Attach!.BatteryTypeId);
    }

    [Fact]
    public void NextFreeRow_ReturnsZeroForAnEmptyColumn()
    {
        Assert.Equal(0, WorkflowPlacement.NextFreeRow(WorkflowGraph.Empty, NodeKind.Battery, AvailableKeys));
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

        Assert.True(WorkflowPlacement.NextFreeRow(graph, NodeKind.Battery, AvailableKeys) > 500);
        Assert.True(WorkflowPlacement.NextFreeRow(graph, NodeKind.Battery, AvailableKeys) < 9000);
    }

    [Fact]
    public void NextFreeLaneY_ReturnsZeroForAnEmptyCanvas()
    {
        Assert.Equal(0, WorkflowPlacement.NextFreeLaneY(WorkflowGraph.Empty, AvailableKeys));
    }

    [Fact]
    public void NextFreeLaneY_ClearsTheFullHeightOfEveryNodeOnTheCanvas_NotJustDeviceNodes()
    {
        // The bug this exists to prevent: every device node sits at its own lane's local Y=0
        // (WorkflowAutoLayout.Apply), so looking only at device-kind Y (NextFreeRow's contract)
        // would place the next lane right on top of the previous one's channel grid tail.
        var tallLane = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(id: 1, channels: 10), null, 0, 0, AvailableKeys);

        var nextY = WorkflowPlacement.NextFreeLaneY(tallLane, AvailableKeys);

        var lowestNodeY = tallLane.Nodes.Max(n => n.Y);
        Assert.True(nextY > lowestNodeY);
    }

    [Fact]
    public void NextFreeLaneY_LeavesNoOverlapWhenUsedToPlaceASecondDevice()
    {
        var first = WorkflowPlacement.PlaceDevice(
            WorkflowGraph.Empty, Device(id: 1, channels: 10), null, 0, 0, AvailableKeys);

        var secondOriginY = WorkflowPlacement.NextFreeLaneY(first, AvailableKeys);
        var both = WorkflowPlacement.PlaceDevice(first, Device(id: 2, channels: 2), null, 0, secondOriginY, AvailableKeys);

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
}
