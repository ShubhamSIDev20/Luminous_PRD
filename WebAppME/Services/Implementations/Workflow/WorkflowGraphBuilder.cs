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

    /// <summary>
    /// Every channel EntityId currently on the canvas, as a set.
    ///
    /// Callers that need "is this channel placed?" for many channels MUST build this once and do
    /// O(1) lookups against it. The palette previously answered that question with a fresh
    /// Graph.Nodes scan per channel, which at 11 devices x 64 channels x a 640-node graph is
    /// ~450k comparisons on every single render - and the palette re-renders on every telemetry
    /// tick, which is what made the canvas hang.
    /// </summary>
    public static HashSet<long> PlacedChannelIds(WorkflowGraph graph) =>
        graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel && n.EntityId is not null)
            .Select(n => n.EntityId!.Value)
            .ToHashSet();

    /// <summary>
    /// A device's own board nodes, in a deterministic order (by EntityId) — the order pins render
    /// in on the device card and the order edges attach to those pins in, so both sides of the
    /// rendering must call this rather than each inventing their own ordering.
    /// </summary>
    public static IReadOnlyList<WorkflowNode> BoardsForDevice(WorkflowGraph graph, string deviceNodeId)
    {
        var boardIds = graph.Edges
            .Where(e => e.Kind == EdgeKind.Topology && e.FromNodeId == deviceNodeId)
            .Select(e => e.ToNodeId)
            .ToHashSet();

        return graph.Nodes
            .Where(n => n.Kind == NodeKind.Board && boardIds.Contains(n.Id))
            .OrderBy(n => n.EntityId)
            .ToList();
    }
}
