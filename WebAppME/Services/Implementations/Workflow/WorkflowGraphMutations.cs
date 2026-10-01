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
}
