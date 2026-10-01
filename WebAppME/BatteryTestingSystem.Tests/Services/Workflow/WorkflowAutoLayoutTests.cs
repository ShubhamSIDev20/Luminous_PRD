using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Lane layout (spec section 5, D10): one device per lane, its boards stacked vertically, each
/// board's channels filling a row of up to ChannelsPerRow columns — mirroring the physical
/// 8-channels-per-board reality so the wrap means something. Apply is called by
/// WorkflowPlacement.PlaceDevice with exactly one device's freshly-built subgraph; these tests
/// also cover bare/orphan graphs defensively, since several construct graphs without a full
/// device-board-channel tree.
/// </summary>
public class WorkflowAutoLayoutTests
{
    // The worst-case property list every pre-existing test in this file was written against,
    // back when RowHeight was a fixed worst-case constant. Kept as an explicit alias so those
    // tests still exercise the exact same numbers now that RowHeight/Apply take it as a parameter.
    private static readonly IReadOnlyList<string> AvailableKeys = WorkflowNodeProperties.AvailableKeys;

    private static WorkflowGraph Graph(
        IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowEdge>? edges = null) =>
        WorkflowGraph.Empty with
        {
            Nodes = nodes.ToList(),
            Edges = (edges ?? Enumerable.Empty<WorkflowEdge>()).ToList(),
        };

    private static WorkflowNode Device(string id = "dev-1") => new(id, NodeKind.Device, 1, 0, 0, null);
    private static WorkflowNode Board(string id) => new(id, NodeKind.Board, 1, 0, 0, null);
    private static WorkflowNode Channel(string id) => new(id, NodeKind.Channel, 1, 0, 0, null);

    private static WorkflowEdge Topology(string from, string to) =>
        new($"edge-{from}--{to}", from, to, EdgeKind.Topology);

    /// <summary>Builds one device with `boardChannelCounts.Length` boards, each with the given
    /// number of channels, fully wired with topology edges — the exact shape
    /// WorkflowGraphBuilder.BuildForDevice produces.</summary>
    private static WorkflowGraph DeviceGraph(params int[] boardChannelCounts)
    {
        var nodes = new List<WorkflowNode> { Device() };
        var edges = new List<WorkflowEdge>();

        for (var b = 0; b < boardChannelCounts.Length; b++)
        {
            var boardId = $"brd-{b}";
            nodes.Add(Board(boardId));
            edges.Add(Topology("dev-1", boardId));

            for (var c = 0; c < boardChannelCounts[b]; c++)
            {
                var channelId = $"chn-{b}-{c}";
                nodes.Add(Channel(channelId));
                edges.Add(Topology(boardId, channelId));
            }
        }

        return Graph(nodes, edges);
    }

    [Fact]
    public void Apply_PlacesTheDeviceAtTheLaneOrigin()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8), AvailableKeys);

        var device = result.Nodes.Single(n => n.Kind == NodeKind.Device);
        Assert.Equal(0, device.X);
        Assert.Equal(0, device.Y);
    }

    [Fact]
    public void Apply_PlacesEveryBoardInTheSameColumn()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8, 8, 8), AvailableKeys);

        var boardXs = result.Nodes.Where(n => n.Kind == NodeKind.Board).Select(n => n.X).Distinct();
        Assert.Equal(new[] { WorkflowAutoLayout.ColumnWidth }, boardXs);
    }

    [Fact]
    public void Apply_StacksBoardsVerticallyWithoutOverlapping()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8, 8, 8), AvailableKeys);

        var boardYs = result.Nodes.Where(n => n.Kind == NodeKind.Board)
            .Select(n => n.Y).OrderBy(y => y).ToList();

        Assert.Equal(3, boardYs.Distinct().Count());
        for (var i = 1; i < boardYs.Count; i++)
            Assert.True(boardYs[i] - boardYs[i - 1] >= WorkflowAutoLayout.RowHeight(AvailableKeys));
    }

    [Fact]
    public void Apply_FillsAnEightChannelBoardAsOneRow()
    {
        // The common real case: exactly one physical board's worth of channels lands on one row,
        // beside its board node — the wrap never triggers for real hardware.
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8), AvailableKeys);

        var board = result.Nodes.Single(n => n.Kind == NodeKind.Board);
        var channels = result.Nodes.Where(n => n.Kind == NodeKind.Channel).ToList();

        Assert.All(channels, c => Assert.Equal(board.Y, c.Y));
        Assert.Equal(8, channels.Select(c => c.X).Distinct().Count());
    }

    [Fact]
    public void Apply_WrapsAChannelRowPastEightColumns()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(10), AvailableKeys);

        var board = result.Nodes.Single(n => n.Kind == NodeKind.Board);
        var channels = result.Nodes.Where(n => n.Kind == NodeKind.Channel)
            .OrderBy(n => n.Id).ToList();

        // First 8 share the board's row; the 9th and 10th drop to the next row.
        Assert.All(channels.Take(8), c => Assert.Equal(board.Y, c.Y));
        Assert.All(channels.Skip(8), c => Assert.Equal(board.Y + WorkflowAutoLayout.RowHeight(AvailableKeys), c.Y));
    }

    [Fact]
    public void Apply_MovesTheNextBoardBelowTheWrappedRowsOfThePreviousOne()
    {
        // Board 0 has 10 channels (2 rows); board 1 must start below BOTH of those rows, not
        // immediately below board 0's own row — the exact overlap the old column layout risked.
        var result = WorkflowAutoLayout.Apply(DeviceGraph(10, 8), AvailableKeys);

        var boards = result.Nodes.Where(n => n.Kind == NodeKind.Board)
            .OrderBy(n => n.Id).ToList();

        Assert.Equal(0, boards[0].Y);
        Assert.Equal(2 * WorkflowAutoLayout.RowHeight(AvailableKeys), boards[1].Y);
    }

    [Fact]
    public void Apply_GivesAChannellessBoardExactlyOneRowOfHeight()
    {
        var result = WorkflowAutoLayout.Apply(DeviceGraph(0, 8), AvailableKeys);

        var boards = result.Nodes.Where(n => n.Kind == NodeKind.Board)
            .OrderBy(n => n.Id).ToList();

        Assert.Equal(WorkflowAutoLayout.RowHeight(AvailableKeys), boards[1].Y);
    }

    [Fact]
    public void Apply_PlacesOrphanChannelsInTheirOwnTrailingGrid_RatherThanOverlappingAnything()
    {
        // A bare graph with no device/board/edges at all — defensive robustness, not a real
        // canvas shape, but Apply must never crash or double-place on unexpected input.
        var graph = Graph(new[] { Channel("c1"), Channel("c2"), Channel("c3") });

        var result = WorkflowAutoLayout.Apply(graph, AvailableKeys);

        var points = result.Nodes.Select(n => (n.X, n.Y)).ToList();
        Assert.Equal(points.Count, points.Distinct().Count());
    }

    [Fact]
    public void Apply_IsDeterministic_ForIdenticalInput()
    {
        var graph = DeviceGraph(8, 8);

        var first = WorkflowAutoLayout.Apply(graph, AvailableKeys);
        var second = WorkflowAutoLayout.Apply(graph, AvailableKeys);

        Assert.Equal(first.Nodes, second.Nodes);
    }

    [Fact]
    public void Apply_NeverPlacesTwoNodesAtTheSamePoint_AtFullDeviceScale()
    {
        // A real device: 8 boards of 8 channels each, matching the AddSecondaryBoardAndChannel
        // 1-8 convention this whole algorithm is built around.
        var result = WorkflowAutoLayout.Apply(DeviceGraph(8, 8, 8, 8, 8, 8, 8, 8), AvailableKeys);

        var points = result.Nodes.Select(n => (n.X, n.Y)).ToList();
        Assert.Equal(points.Count, points.Distinct().Count());
    }

    [Fact]
    public void Apply_PreservesNodeIdentityAndAttachments()
    {
        var graph = Graph(new[]
        {
            Device(), Board("brd-0"),
            new WorkflowNode("chn-0-0", NodeKind.Channel, 42, 0, 0, new NodeAttachment(7, null, null)),
        }, new[] { Topology("dev-1", "brd-0"), Topology("brd-0", "chn-0-0") });

        var result = WorkflowAutoLayout.Apply(graph, AvailableKeys);

        var channel = result.Nodes.Single(n => n.Kind == NodeKind.Channel);
        Assert.Equal("chn-0-0", channel.Id);
        Assert.Equal(42, channel.EntityId);
        Assert.Equal(7, channel.Attach!.ProgramId);
    }

    [Fact]
    public void Apply_LeavesEdgesAndViewportUntouched()
    {
        var graph = DeviceGraph(8) with { Viewport = new CanvasViewport(5, 6, 0.5) };

        var result = WorkflowAutoLayout.Apply(graph, AvailableKeys);

        Assert.Equal(graph.Edges, result.Edges);
        Assert.Equal(graph.Viewport, result.Viewport);
    }

    [Fact]
    public void Apply_HandlesAnEmptyGraph()
    {
        var result = WorkflowAutoLayout.Apply(WorkflowGraph.Empty, AvailableKeys);

        Assert.Empty(result.Nodes);
    }

    // ============================================================ row spacing invariant

    /// <summary>
    /// EVERY expected value here was measured in a real browser against the rendered battery cell,
    /// not derived on paper — nine selections, each matching to the pixel. Session 36's predecessor
    /// constants (106 base, 16 per row) reproduced the right answer at exactly three rows and
    /// nowhere else, because one measurement cannot pin a multi-parameter model.
    ///
    /// Measured breakdown of the 114px chrome: cap 20, cell-body border+padding 14, cell side
    /// padding 2, content padding 8, header row 18 + 2 margin, battery strip 12, footer 17 + 5
    /// margin, badge strip 16. CanvasNode's card chrome is suppressed for channels, which is
    /// exactly the 37px this dropped from an earlier 151.
    ///
    /// Each non-empty section costs 19 (a 12px heading plus 7px of margin) plus its rows, where a
    /// row is 15px and consecutive rows are separated by a 1px gap. Primary Data packs two values
    /// per row; Configuration and Program Data take one each.
    ///
    /// The line boxes these depend on are pinned with explicit line-height in
    /// workflow-canvas.css precisely so these stay integers instead of tracking whatever
    /// line-height:normal resolves to for the current font.
    /// </summary>
    [Theory]
    [InlineData(new string[0], 114)]                                              // chrome only
    [InlineData(new[] { "Voltage" }, 148)]                                        // 1 primary  -> 1 row
    [InlineData(new[] { "Voltage", "Current" }, 148)]                             // 2 primary  -> same row
    [InlineData(new[] { "Voltage", "Current", "Power" }, 164)]                    // 3 primary  -> 2 rows
    [InlineData(new[] { "BatteryID" }, 148)]                                      // 1 config   -> 1 row
    [InlineData(new[] { "BatteryID", "ProgramID", "SessionID" }, 180)]            // 3 config   -> 3 rows
    [InlineData(new[] { "CycleNumber", "CycleStatus" }, 164)]                     // 2 program  -> 2 rows
    [InlineData(new[] { "Voltage", "Current", "Power", "Temperature", "BatteryID" }, 198)]  // 2 sections
    public void ChannelNodeHeight_MatchesTheMeasuredRenderedCell(string[] keys, double expected)
    {
        Assert.Equal(expected, WorkflowAutoLayout.ChannelNodeHeight(keys));
    }

    [Fact]
    public void ChannelNodeHeight_DependsOnWhichKeysAreSelectedNotHowMany()
    {
        // The reason the signature had to stop taking an int. Three primary keys share two rows;
        // three program keys take three labelled rows. Same count, different cell.
        Assert.NotEqual(
            WorkflowAutoLayout.ChannelNodeHeight(new[] { "Voltage", "Current", "Power" }),
            WorkflowAutoLayout.ChannelNodeHeight(new[] { "CycleNumber", "CycleStatus", "StepNumber" }));
    }

    [Fact]
    public void MaxChannelNodeHeight_MatchesTheMeasuredRenderedCard()
    {
        // All 28 keys: 114 chrome + (19 + 95) primary + (19 + 47) config + (19 + 207) program.
        // Browser-measured at 520, re-verified after the card chrome was suppressed.
        Assert.Equal(
            WorkflowAutoLayout.ChannelNodeHeight(WorkflowNodeProperties.AvailableKeys),
            WorkflowAutoLayout.MaxChannelNodeHeight);
        Assert.Equal(520d, WorkflowAutoLayout.MaxChannelNodeHeight);
    }

    [Fact]
    public void RowHeight_ClearsTheTallestPossibleChannelNode()
    {
        // THE overlap invariant. RowHeight was 120 against a real 154px card, so picking 5-6 node
        // properties made every channel spill 34px into the row beneath it. A row MUST be taller
        // than the tallest card the property picker can produce, or nodes overlap by construction.
        Assert.True(
            WorkflowAutoLayout.RowHeight(AvailableKeys) > WorkflowAutoLayout.MaxChannelNodeHeight,
            $"RowHeight ({WorkflowAutoLayout.RowHeight(AvailableKeys)}) must exceed the tallest channel node "
            + $"({WorkflowAutoLayout.MaxChannelNodeHeight}) or rows collide.");
    }

    [Fact]
    public void Apply_LeavesNoVerticalOverlapBetweenStackedChannelRows()
    {
        // Two boards of 8 channels each: board 2's channels must start below the bottom edge of
        // board 1's tallest possible channel card, not merely below its Y origin.
        var nodes = new List<WorkflowNode> { Device() };
        var edges = new List<WorkflowEdge>();
        foreach (var b in new[] { 1, 2 })
        {
            nodes.Add(Board($"brd-{b}"));
            edges.Add(new WorkflowEdge($"e-d{b}", "dev-1", $"brd-{b}", EdgeKind.Topology));
            for (var c = 1; c <= 8; c++)
            {
                nodes.Add(Channel($"chn-{b}-{c}"));
                edges.Add(new WorkflowEdge($"e-{b}-{c}", $"brd-{b}", $"chn-{b}-{c}", EdgeKind.Topology));
            }
        }

        var result = WorkflowAutoLayout.Apply(Graph(nodes, edges), AvailableKeys);

        var rowYs = result.Nodes
            .Where(n => n.Kind == NodeKind.Channel)
            .Select(n => n.Y)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        Assert.True(rowYs.Count >= 2, "expected at least two distinct channel rows");
        for (var i = 1; i < rowYs.Count; i++)
        {
            Assert.True(
                rowYs[i] - rowYs[i - 1] >= WorkflowAutoLayout.MaxChannelNodeHeight,
                $"rows at Y={rowYs[i - 1]} and Y={rowYs[i]} are only {rowYs[i] - rowYs[i - 1]}px "
                + $"apart, which overlaps a {WorkflowAutoLayout.MaxChannelNodeHeight}px card.");
        }
    }

    // ============================================================ dynamic RowHeight (regression)

    [Fact]
    public void RowHeight_ShrinksWhenFewerPropertiesAreSelected()
    {
        // Regression. RowHeight used to be a fixed worst-case constant, so removing fields left
        // the gap between rows permanently sized for the field count that happened to be selected
        // when the layout was last arranged - the reported bug: fewer fields, bigger visible gap.
        Assert.True(
            WorkflowAutoLayout.RowHeight(new[] { "Voltage" })
            < WorkflowAutoLayout.RowHeight(AvailableKeys));
    }

    [Fact]
    public void Apply_RepackedWithFewerProperties_TightensAnAlreadyPlacedBoardsRow()
    {
        // The same regression, exercised end to end: a graph auto-laid-out for the worst case,
        // then re-run (as WorkflowCanvasPage.HandleNodeConfigChanged/ReArrange do) against a
        // shorter property list, must move board 2's row UP to match - not leave it where the
        // worst-case spacing put it.
        var wide = WorkflowAutoLayout.Apply(DeviceGraph(8, 8), AvailableKeys);
        var narrowProps = new[] { "Voltage" };

        var repacked = WorkflowAutoLayout.Apply(wide, narrowProps);

        var boards = repacked.Nodes.Where(n => n.Kind == NodeKind.Board).OrderBy(n => n.Id).ToList();
        Assert.Equal(WorkflowAutoLayout.RowHeight(narrowProps), boards[1].Y);
        Assert.True(boards[1].Y < WorkflowAutoLayout.RowHeight(AvailableKeys));
    }

    // ============================================================ multi-device Apply (regression)

    [Fact]
    public void Apply_AnchorsEachDeviceAtItsOwnExistingPosition_NotAtASharedOrigin()
    {
        // Regression. Apply only ever positioned the FIRST device node it found at (0,0) and
        // left every other device's boards/channels wherever they already were - so calling it
        // over a whole multi-device canvas (WorkflowCanvasPage.ReArrange, and now also the
        // automatic repack on a field-selection change) silently did nothing for every device
        // past the first, rather than repacking each one's own lane in place.
        var first = DeviceGraph(8);
        var second = new WorkflowGraph(
            WorkflowGraph.CurrentVersion,
            new List<WorkflowNode> { new("dev-2", NodeKind.Device, 2, 0, 900, null) },
            new List<WorkflowEdge>(),
            CanvasViewport.Default);
        // Board/channels for device 2, already placed at Y=900 (a second lane below the first).
        var board2 = new WorkflowNode("brd-9", NodeKind.Board, 2, WorkflowAutoLayout.ColumnWidth, 900, null);
        var chn2 = new WorkflowNode("chn-9-0", NodeKind.Channel, 2, WorkflowAutoLayout.ColumnWidth * 2, 900, null);
        var combined = first with
        {
            Nodes = first.Nodes
                .Concat(second.Nodes)
                .Append(board2)
                .Append(chn2)
                .ToList(),
            Edges = first.Edges
                .Append(new WorkflowEdge("e-dev2-brd9", "dev-2", "brd-9", EdgeKind.Topology))
                .Append(new WorkflowEdge("e-brd9-chn9", "brd-9", "chn-9-0", EdgeKind.Topology))
                .ToList(),
        };

        var result = WorkflowAutoLayout.Apply(combined, AvailableKeys);

        var dev2Board = result.Nodes.Single(n => n.Id == "brd-9");
        // Device 2's own lane must still be anchored near Y=900 (its device node's position),
        // not collapsed back onto device 1's lane at Y=0.
        Assert.Equal(900, dev2Board.Y);
    }
}
