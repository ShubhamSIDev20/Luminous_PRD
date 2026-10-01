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
        double originY,
        IReadOnlyList<string> visibleProperties)
    {
        // Positions are always computed from the device's FULL physical channel set, never just
        // the channels this particular call is adding. A channel added later must land in the
        // same row/column its channel number always implies - continuing beside its board's
        // already-placed siblings - rather than being auto-laid-out as if it were its board's
        // only channel (which always computes column 0, row 0) and then dropped wherever this
        // call's origin happens to be, which used to land far BELOW the existing board entirely
        // (HandlePlaceDevice always passed NextFreeLaneY as the origin, a value meant only for a
        // brand new device lane).
        var allChannelIds = device.Boards.SelectMany(b => b.Channels).Select(c => c.ChannelId).ToList();
        var incoming = WorkflowAutoLayout.Apply(
            WorkflowGraphBuilder.BuildForDevice(device, allChannelIds), visibleProperties);

        var existingNodeIds = graph.Nodes.Select(n => n.Id).ToHashSet();
        var existingEdgeIds = graph.Edges.Select(e => e.Id).ToHashSet();

        // channelIds still gates which channels this call may actually insert - null means every
        // channel. A channel the caller excluded (e.g. claimed by another workflow) must never be
        // added, even though computing its position above is what keeps its siblings from
        // shifting into its slot once it is later freed and placed for real.
        var allowedChannels = channelIds is null ? null : new HashSet<long>(channelIds);

        var keptChannelIds = incoming.Nodes
            .Where(n => n.Kind == NodeKind.Channel)
            .Where(n => existingNodeIds.Contains(n.Id)
                || allowedChannels is null
                || (n.EntityId is { } id && allowedChannels.Contains(id)))
            .Select(n => n.Id)
            .ToHashSet();

        // A board with none of its channels kept this call (every one claimed elsewhere) must
        // not appear as an empty shell with no children.
        var boardsWithAKeptChannel = incoming.Edges
            .Where(e => e.Kind == EdgeKind.Topology && keptChannelIds.Contains(e.ToNodeId))
            .Select(e => e.FromNodeId)
            .ToHashSet();

        var newNodes = incoming.Nodes
            .Where(n => !existingNodeIds.Contains(n.Id))
            .Where(n => n.Kind != NodeKind.Channel || keptChannelIds.Contains(n.Id))
            .Where(n => n.Kind != NodeKind.Board || boardsWithAKeptChannel.Contains(n.Id))
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
    public static double NextFreeRow(WorkflowGraph graph, NodeKind kind, IReadOnlyList<string> visibleProperties)
    {
        var inColumn = graph.Nodes.Where(n => n.Kind == kind).ToList();
        return inColumn.Count == 0
            ? 0
            : inColumn.Max(n => n.Y) + WorkflowAutoLayout.RowHeight(visibleProperties);
    }

    /// <summary>The Y just below the lowest node anywhere on the canvas — for stacking whole
    /// device LANES one below another. NextFreeRow(graph, NodeKind.Device) is insufficient here:
    /// every device node sits at its own lane's local Y=0 (WorkflowAutoLayout.Apply), so it never
    /// reflects how many rows of boards/channels that lane actually grew to.</summary>
    public static double NextFreeLaneY(WorkflowGraph graph, IReadOnlyList<string> visibleProperties) =>
        graph.Nodes.Count == 0 ? 0 : graph.Nodes.Max(n => n.Y) + WorkflowAutoLayout.RowHeight(visibleProperties);

    /// <summary>Fixed X for the battery column, clear of the widest possible lane (2 columns of
    /// header room plus a full ChannelsPerRow-wide channel row, plus one gutter column) so a
    /// battery never lands inside a device's channel grid regardless of that device's shape.</summary>
    public const double BatteryRailX =
        WorkflowAutoLayout.ColumnWidth * 2
        + WorkflowAutoLayout.ChannelsPerRow * WorkflowAutoLayout.ColumnWidth
        + WorkflowAutoLayout.ColumnWidth;
}
