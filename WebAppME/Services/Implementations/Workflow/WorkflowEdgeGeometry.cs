using System.Globalization;
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record EdgeEndpoints(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Where an edge starts and ends, and the cubic path between them. Kept out of the Razor file so
/// the maths is testable — and so the invariant-culture formatting lives in exactly one place.
/// The widths here MUST match workflow-canvas.css; a test pins them.
/// </summary>
public static class WorkflowEdgeGeometry
{
    public static double NodeWidthFor(NodeKind kind) => kind switch
    {
        NodeKind.Device => 240,
        NodeKind.Battery => 140,
        _ => 200,
    };

    /// <summary>
    /// Rendered height, used to attach an edge to a card's vertical centre.
    ///
    /// A channel cell grows with the measurements shown, so its height is NOT a constant: it comes
    /// from WorkflowAutoLayout.ChannelNodeHeight, the same formula the layout uses. This used to be
    /// a hardcoded 108 with a comment claiming a few pixels out was imperceptible — true for the
    /// old fixed card, wrong once the property picker could take it to 154px, which attached every
    /// edge 23px above where the card actually centred.
    ///
    /// It now takes the KEY LIST rather than a count, because Primary Data packs two values per row
    /// while the labelled sections take one each — so the same number of properties can produce
    /// different heights. Null means "no properties", not "unknown".
    /// </summary>
    public static double NodeHeightFor(
        NodeKind kind, IReadOnlyList<string>? visibleChannelProperties = null) => kind switch
    {
        NodeKind.Channel => WorkflowAutoLayout.ChannelNodeHeight(
            visibleChannelProperties ?? Array.Empty<string>()),
        NodeKind.Battery => 72,
        _ => 88,
    };

    public static EdgeEndpoints PortPoints(
        WorkflowNode from, WorkflowNode to, double fromWidth, double fromHeight, double toHeight) =>
        new(from.X + fromWidth, from.Y + fromHeight / 2, to.X, to.Y + toHeight / 2);

    public static EdgeEndpoints PortPoints(
        WorkflowNode from, WorkflowNode to,
        IReadOnlyList<string>? visibleChannelProperties = null) =>
        PortPoints(from, to,
            NodeWidthFor(from.Kind),
            NodeHeightFor(from.Kind, visibleChannelProperties),
            NodeHeightFor(to.Kind, visibleChannelProperties));

    /// <summary>
    /// Where a board's pin dot sits on its device's right edge, evenly spaced across the device
    /// card's height by slot index — a board is no longer its own rendered node (D18-revised), so
    /// every edge that used to leave a Board node now leaves from this point on its Device instead.
    /// </summary>
    public static (double X, double Y) BoardPinPoint(WorkflowNode device, int slotIndex, int totalSlots)
    {
        var height = NodeHeightFor(NodeKind.Device);
        var x = device.X + NodeWidthFor(NodeKind.Device);
        var y = device.Y + (slotIndex + 0.5) / totalSlots * height;
        return (x, y);
    }

    /// <summary>
    /// A cubic with horizontal tangents at both ends, so edges leave and arrive flat and the
    /// graph reads as left-to-right flow. The control offset grows with horizontal distance but
    /// never collapses, so a backwards edge still bows out instead of overlapping itself.
    /// </summary>
    public static string BezierPath(EdgeEndpoints p)
    {
        var dx = Math.Max(60, Math.Abs(p.X2 - p.X1) * 0.5);

        var c1x = p.X1 + dx;
        var c2x = p.X2 - dx;

        return string.Create(CultureInfo.InvariantCulture,
            $"M {p.X1} {p.Y1} C {c1x} {p.Y1} {c2x} {p.Y2} {p.X2} {p.Y2}");
    }
}
