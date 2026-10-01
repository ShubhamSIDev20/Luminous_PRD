using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Path maths is the one part of the rendering that can be tested without a browser, and it is
/// also where an invariant-culture slip silently produces "M0,0 C1,5..." with commas in the
/// numbers and an invisible edge.
/// </summary>
public class WorkflowEdgeGeometryTests
{
    private static WorkflowNode At(double x, double y, NodeKind kind = NodeKind.Channel) =>
        new("n", kind, 1, x, y, null);

    [Fact]
    public void PortPoints_LeavesTheRightEdgeOfTheSourceAndEntersTheLeftEdgeOfTheTarget()
    {
        var p = WorkflowEdgeGeometry.PortPoints(
            At(0, 0), At(300, 100), fromWidth: 200, fromHeight: 96, toHeight: 96);

        Assert.Equal(200, p.X1);    // right edge of a node at x=0, width 200
        Assert.Equal(48, p.Y1);     // vertical centre
        Assert.Equal(300, p.X2);    // left edge of the target
        Assert.Equal(148, p.Y2);    // target y + half its height
    }

    [Fact]
    public void BezierPath_ProducesAValidCubicPath()
    {
        var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(0, 0, 100, 50));

        Assert.StartsWith("M 0 0 C ", d);
        Assert.EndsWith(" 100 50", d);
    }

    [Fact]
    public void BezierPath_UsesInvariantCulture()
    {
        // On a de-DE machine "0.5" formats as "0,5", which makes the whole d attribute invalid
        // and the edge simply does not draw — with no error anywhere.
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture =
            new System.Globalization.CultureInfo("de-DE");
        try
        {
            var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(0.5, 1.5, 2.5, 3.5));
            Assert.DoesNotContain(",", d.Replace("C", ""));
            Assert.Contains("0.5", d);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void BezierPath_ControlPointsPullHorizontally_SoEdgesLeaveAndArriveFlat()
    {
        // Horizontal tangents are what make the graph read as left-to-right flow rather than
        // a bundle of diagonal lines.
        var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(0, 0, 200, 100));
        var parts = d.Replace("M ", "").Replace("C ", "").Split(' ',
            System.StringSplitOptions.RemoveEmptyEntries);

        // parts: x0 y0 c1x c1y c2x c2y x1 y1
        Assert.Equal("0", parts[1]);        // start y
        Assert.Equal(parts[1], parts[3]);   // first control point shares the start y
        Assert.Equal(parts[7], parts[5]);   // second control point shares the end y
    }

    [Fact]
    public void BezierPath_HandlesABackwardsEdgeWithoutCollapsing()
    {
        // The operator can drag a battery to the left of its channel; the curve must still bow
        // out rather than degenerate into a straight overlap.
        var d = WorkflowEdgeGeometry.BezierPath(new EdgeEndpoints(300, 0, 0, 0));

        Assert.StartsWith("M 300 0 C ", d);
        Assert.Contains(" 0 0", d);
    }

    [Theory]
    [InlineData(NodeKind.Device, 240)]
    [InlineData(NodeKind.Board, 200)]
    [InlineData(NodeKind.Channel, 200)]
    [InlineData(NodeKind.Battery, 140)]
    public void NodeWidthFor_MatchesTheStylesheet(NodeKind kind, double expected)
    {
        // These must agree with .wf-node / .wf-node--device / .wf-node--battery in
        // workflow-canvas.css, or every edge attaches slightly off the node.
        Assert.Equal(expected, WorkflowEdgeGeometry.NodeWidthFor(kind));
    }

    // ============================================================ BoardPinPoint

    [Fact]
    public void BoardPinPoint_SitsOnTheDevicesRightEdge()
    {
        var device = At(0, 0, NodeKind.Device);

        var (x, _) = WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex: 0, totalSlots: 1);

        Assert.Equal(WorkflowEdgeGeometry.NodeWidthFor(NodeKind.Device), x);
    }

    [Fact]
    public void BoardPinPoint_CentresASingleSlotVertically()
    {
        var device = At(0, 0, NodeKind.Device);

        var (_, y) = WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex: 0, totalSlots: 1);

        Assert.Equal(WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Device) / 2, y);
    }

    [Fact]
    public void BoardPinPoint_SpreadsMultipleSlotsEvenlyAndInOrder()
    {
        var device = At(0, 0, NodeKind.Device);
        var height = WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Device);

        var (_, y0) = WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex: 0, totalSlots: 4);
        var (_, y1) = WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex: 1, totalSlots: 4);
        var (_, y3) = WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex: 3, totalSlots: 4);

        Assert.Equal(height / 8, y0);
        Assert.Equal(height * 3 / 8, y1);
        Assert.Equal(height * 7 / 8, y3);
        Assert.True(y0 < y1);
        Assert.True(y1 < y3);
    }

    [Fact]
    public void BoardPinPoint_OffsetsByTheDevicesOwnPosition()
    {
        var device = At(500, 300, NodeKind.Device);

        var (x, y) = WorkflowEdgeGeometry.BoardPinPoint(device, slotIndex: 0, totalSlots: 1);

        Assert.Equal(500 + WorkflowEdgeGeometry.NodeWidthFor(NodeKind.Device), x);
        Assert.Equal(300 + WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Device) / 2, y);
    }

    // ---------------------------------------------- channel height tracks the property picker

    [Fact]
    public void ChannelHeight_GrowsWithTheVisibleProperties()
    {
        // Primary Data renders two values per row, so one and two primary keys are the same height.
        var none = WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel, System.Array.Empty<string>());
        var two = WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel, new[] { "Voltage", "Current" });
        var atCap = WorkflowEdgeGeometry.NodeHeightFor(
            NodeKind.Channel, WorkflowNodeProperties.AvailableKeys);

        Assert.True(two > none);
        Assert.True(atCap > two);
        Assert.Equal(WorkflowAutoLayout.MaxChannelNodeHeight, atCap);
    }

    [Fact]
    public void ChannelHeight_MatchesTheLayoutsOwnFormula()
    {
        // One source of truth. This geometry previously hardcoded 108 while the layout computed
        // 154 at the 6-property cap, so edges attached 23px above the card's real centre and the
        // error moved whenever the user changed which fields were shown.
        // Every prefix of the catalogue, so a section boundary being crossed cannot hide a
        // disagreement between the two.
        for (var n = 0; n <= WorkflowNodeProperties.AvailableKeys.Count; n++)
        {
            var keys = WorkflowNodeProperties.AvailableKeys.Take(n).ToList();

            Assert.Equal(
                WorkflowAutoLayout.ChannelNodeHeight(keys),
                WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel, keys));
        }
    }

    [Fact]
    public void PortPoints_AttachesToTheChannelsRealCentre_AtTheCap()
    {
        var p = WorkflowEdgeGeometry.PortPoints(
            At(0, 0, NodeKind.Device), At(300, 0),
            visibleChannelProperties: WorkflowNodeProperties.AvailableKeys);

        Assert.Equal(WorkflowAutoLayout.MaxChannelNodeHeight / 2, p.Y2);
        Assert.NotEqual(54, p.Y2);   // the old hardcoded 108 / 2
    }

    [Fact]
    public void ChannelHeight_TreatsNoSelectionAsEmpty_RatherThanShrinkingTheCell()
    {
        // null means "no properties", not "unknown" - a caller that has no selection to hand must
        // still get the real chrome height, or its edges attach above the cell.
        Assert.Equal(
            WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel, System.Array.Empty<string>()),
            WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel));
    }

    [Fact]
    public void ChannelHeight_SameKeyCountInDifferentSectionsDiffers()
    {
        // Why the parameter is a list and not an int: three primary keys share two rows, three
        // program keys take three labelled rows.
        Assert.NotEqual(
            WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel, new[] { "Voltage", "Current", "Power" }),
            WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel, new[] { "CycleNumber", "CycleStatus", "StepNumber" }));
    }
}
