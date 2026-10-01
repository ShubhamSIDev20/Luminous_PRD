using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The minimap is pure projection maths: content bounds, a scale that fits them into a fixed
/// box, and a scaled rectangle per node. Divide-by-zero on a single-node or empty graph is the
/// obvious failure and is pinned here.
/// </summary>
public class MinimapProjectionTests
{
    private static WorkflowGraph GraphAt(params (double X, double Y)[] points) =>
        WorkflowGraph.Empty with
        {
            Nodes = points
                .Select((p, i) => new WorkflowNode($"n{i}", NodeKind.Channel, i, p.X, p.Y, null))
                .ToList()
        };

    [Fact]
    public void ContentBounds_SpansEveryNodeIncludingItsWidth()
    {
        var bounds = MinimapProjection.ContentBounds(GraphAt((0, 0), (400, 200)));

        Assert.Equal(0, bounds.X);
        Assert.Equal(0, bounds.Y);
        Assert.True(bounds.Width >= 600, "must include the width of the right-most node");
        Assert.True(bounds.Height >= 200);
    }

    [Fact]
    public void ContentBounds_HandlesNegativeCoordinates()
    {
        // Nodes can be dragged left of the origin; the minimap must not clip them.
        var bounds = MinimapProjection.ContentBounds(GraphAt((-300, -150), (0, 0)));

        Assert.Equal(-300, bounds.X);
        Assert.Equal(-150, bounds.Y);
    }

    [Fact]
    public void ContentBounds_ReturnsAUnitBoxForAnEmptyGraph()
    {
        // A zero-size box would make Scale divide by zero.
        var bounds = MinimapProjection.ContentBounds(WorkflowGraph.Empty);

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void Scale_FitsTheContentInsideTheMap()
    {
        var content = new MinimapBox(0, 0, 2000, 1000);

        var scale = MinimapProjection.Scale(content, 200, 120);

        Assert.True(content.Width * scale <= 200.001);
        Assert.True(content.Height * scale <= 120.001);
    }

    [Fact]
    public void Scale_UsesTheLimitingDimension()
    {
        // 2000x100 into 200x120 is width-limited: 0.1, not 1.2.
        Assert.Equal(0.1, MinimapProjection.Scale(new MinimapBox(0, 0, 2000, 100), 200, 120), 3);
    }

    [Fact]
    public void Scale_NeverReturnsZeroOrInfinity()
    {
        Assert.True(MinimapProjection.Scale(new MinimapBox(0, 0, 0, 0), 200, 120) > 0);
        Assert.True(double.IsFinite(MinimapProjection.Scale(new MinimapBox(0, 0, 0, 0), 200, 120)));
    }

    [Fact]
    public void NodeBoxes_TranslatesToTheOriginThenScales()
    {
        var graph = GraphAt((-100, -50), (100, 50));
        var content = MinimapProjection.ContentBounds(graph);

        var boxes = MinimapProjection.NodeBoxes(graph, content, 0.1);

        // The top-left-most node must land at 0,0 in map space.
        Assert.Equal(0, boxes[0].X, 3);
        Assert.Equal(0, boxes[0].Y, 3);
        Assert.Equal(20, boxes[1].X, 3);    // (100 - -100) * 0.1
    }

    [Fact]
    public void NodeBoxes_ReturnsOneBoxPerNode()
    {
        var graph = GraphAt((0, 0), (10, 10), (20, 20));
        var content = MinimapProjection.ContentBounds(graph);

        Assert.Equal(3, MinimapProjection.NodeBoxes(graph, content, 0.1).Count);
    }
}
