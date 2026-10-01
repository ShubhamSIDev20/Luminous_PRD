using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record GraphRuleResult(bool IsValid, string? Error)
{
    public static GraphRuleResult Ok { get; } = new(true, null);

    public static GraphRuleResult Fail(string error) => new(false, error);
}

/// <summary>
/// Every rule about what may be placed on the canvas and what may be wired to what. Pure and
/// DI-free so it can be exhaustively unit-tested; the page only reports the message it returns.
/// </summary>
public static class WorkflowGraphValidator
{
    public static GraphRuleResult CanAddNode(WorkflowGraph graph, WorkflowNode node)
    {
        if (graph.Nodes.Any(n => n.Id == node.Id))
            return GraphRuleResult.Fail($"A node with id '{node.Id}' is already on the canvas.");

        // Battery nodes stand for "a battery of this type", not a serial-numbered unit, so the
        // same BatteryTypeId may legitimately appear many times. Real hardware may not.
        if (node.Kind == NodeKind.Battery || node.EntityId is null)
            return GraphRuleResult.Ok;

        var duplicate = graph.Nodes.Any(n => n.Kind == node.Kind && n.EntityId == node.EntityId);

        return duplicate
            ? GraphRuleResult.Fail($"That {node.Kind.ToString().ToLowerInvariant()} is already on the canvas.")
            : GraphRuleResult.Ok;
    }

    public static GraphRuleResult CanConnect(
        WorkflowGraph graph, string fromNodeId, string toNodeId, TopologySnapshot topology)
    {
        if (fromNodeId == toNodeId)
            return GraphRuleResult.Fail("A node cannot be connected to itself.");

        var from = graph.Nodes.FirstOrDefault(n => n.Id == fromNodeId);
        var to = graph.Nodes.FirstOrDefault(n => n.Id == toNodeId);

        // JS supplies these ids; one can go stale between a delete and the drop landing.
        if (from is null || to is null)
            return GraphRuleResult.Fail("One of those nodes is no longer on the canvas.");

        var alreadyConnected = graph.Edges.Any(e =>
            (e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId) ||
            (e.FromNodeId == toNodeId && e.ToNodeId == fromNodeId));

        if (alreadyConnected)
            return GraphRuleResult.Fail("Those two nodes are already connected.");

        return (from.Kind, to.Kind) switch
        {
            (NodeKind.Device, NodeKind.Board) => SameDevice(from, to, topology),
            (NodeKind.Board, NodeKind.Channel) => SameDevice(from, to, topology),
            (NodeKind.Battery, NodeKind.Channel) => BatteryFree(graph, to),
            (NodeKind.Channel, NodeKind.Battery) => BatteryFree(graph, from),
            _ => GraphRuleResult.Fail(
                $"A {from.Kind.ToString().ToLowerInvariant()} cannot connect to a "
              + $"{to.Kind.ToString().ToLowerInvariant()}. Connect device to board, "
              + "board to channel, and battery to channel."),
        };
    }

    public static GraphRuleResult CanAttach(WorkflowGraph graph, string nodeId)
    {
        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);

        if (node is null)
            return GraphRuleResult.Fail("That node is no longer on the canvas.");

        return node.Kind == NodeKind.Channel
            ? GraphRuleResult.Ok
            : GraphRuleResult.Fail("A program or DBC file can only be attached to a channel.");
    }

    /// <summary>
    /// The headline topology rule: a topology edge may only join two pieces of the SAME physical
    /// device. Wiring device A's board to device B's channel would produce a layout describing
    /// hardware that does not exist.
    /// </summary>
    private static GraphRuleResult SameDevice(
        WorkflowNode from, WorkflowNode to, TopologySnapshot topology)
    {
        var fromDevice = OwningDeviceId(from, topology);
        var toDevice = OwningDeviceId(to, topology);

        if (fromDevice is null || toDevice is null)
            return GraphRuleResult.Fail(
                "That hardware no longer exists in the database, so it cannot be connected.");

        return fromDevice == toDevice
            ? GraphRuleResult.Ok
            : GraphRuleResult.Fail("Those two belong to a different device and cannot be connected.");
    }

    private static int? OwningDeviceId(WorkflowNode node, TopologySnapshot topology)
    {
        if (node.EntityId is not { } id) return null;

        return node.Kind switch
        {
            NodeKind.Device => topology.HasDevice((int)id) ? (int)id : null,
            NodeKind.Board => topology.FindDeviceOwningBoard(id)?.DeviceId,
            NodeKind.Channel => topology.FindDeviceOwningChannel(id)?.DeviceId,
            _ => null,
        };
    }

    private static GraphRuleResult BatteryFree(WorkflowGraph graph, WorkflowNode channel)
    {
        var hasBattery = graph.Edges.Any(e =>
            e.Kind == EdgeKind.Power &&
            (e.FromNodeId == channel.Id || e.ToNodeId == channel.Id));

        return hasBattery
            ? GraphRuleResult.Fail("That channel already has a battery attached.")
            : GraphRuleResult.Ok;
    }
}
