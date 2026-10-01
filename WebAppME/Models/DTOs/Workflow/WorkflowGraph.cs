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
/// long? because Channel.Id and SecondaryBoard.Id are long; Device.DeviceID is int and widens.
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
