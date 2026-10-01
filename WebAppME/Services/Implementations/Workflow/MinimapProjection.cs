using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record MinimapBox(double X, double Y, double Width, double Height);

/// <summary>
/// World coordinates to minimap coordinates. Pure maths, kept out of the Razor file so the
/// degenerate cases — empty graph, single node, everything on one row — are testable rather
/// than discovered as a divide-by-zero in the browser.
/// </summary>
public static class MinimapProjection
{
    private const double MinExtent = 1;

    /// <summary>
    /// The world-space extent of the graph.
    ///
    /// visibleChannelProperties must be the SAME selection the canvas renders, or the minimap's
    /// channel rectangles drift from the nodes they represent as soon as a user picks more fields.
    /// This previously relied on NodeHeightFor's default of "no properties", which silently
    /// measured every channel at its shortest.
    /// </summary>
    public static MinimapBox ContentBounds(
        WorkflowGraph graph, IReadOnlyList<string>? visibleChannelProperties = null)
    {
        if (graph.Nodes.Count == 0) return new MinimapBox(0, 0, MinExtent, MinExtent);

        var minX = graph.Nodes.Min(n => n.X);
        var minY = graph.Nodes.Min(n => n.Y);
        var maxX = graph.Nodes.Max(n => n.X + WorkflowEdgeGeometry.NodeWidthFor(n.Kind));
        var maxY = graph.Nodes.Max(n =>
            n.Y + WorkflowEdgeGeometry.NodeHeightFor(n.Kind, visibleChannelProperties));

        return new MinimapBox(
            minX, minY,
            Math.Max(MinExtent, maxX - minX),
            Math.Max(MinExtent, maxY - minY));
    }

    public static double Scale(MinimapBox content, double mapWidth, double mapHeight)
    {
        var width = Math.Max(MinExtent, content.Width);
        var height = Math.Max(MinExtent, content.Height);

        return Math.Min(mapWidth / width, mapHeight / height);
    }

    public static IReadOnlyList<MinimapBox> NodeBoxes(
        WorkflowGraph graph, MinimapBox content, double scale,
        IReadOnlyList<string>? visibleChannelProperties = null) =>
        graph.Nodes
            .Select(n => new MinimapBox(
                (n.X - content.X) * scale,
                (n.Y - content.Y) * scale,
                Math.Max(1, WorkflowEdgeGeometry.NodeWidthFor(n.Kind) * scale),
                Math.Max(1, WorkflowEdgeGeometry.NodeHeightFor(n.Kind, visibleChannelProperties) * scale)))
            .ToList();
}
