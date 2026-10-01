using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Lane layout: one device's boards stack vertically, and each board's channels fill a row of up
/// to ChannelsPerRow columns beside it. 8 is not an arbitrary UI choice — a physical secondary
/// board has at most 8 channels (see the AddSecondaryBoardAndChannel migration's own "1-8
/// backfill"), so a real board's channels always land on exactly one row and the wrap only ever
/// triggers on a hypothetical oversized board.
///
/// Apply's only caller, WorkflowPlacement.PlaceDevice, always passes one device's freshly-built
/// subgraph (0-64 nodes), so a full edge scan per call has no scale concern. The algorithm still
/// handles bare/orphan graphs (no device, channels with no board) defensively — several tests,
/// and any future direct caller, may not hand it a full topology tree.
/// </summary>
public static class WorkflowAutoLayout
{
    public const double ColumnWidth = 280;
    public const double NodeWidth = 200;
    public const int ChannelsPerRow = 8;

    /// <summary>
    /// A channel battery cell with no sections at all, measured in the browser.
    ///
    /// Breakdown: cap 20, cell-body border+padding 14, cell side padding 2, content padding 8,
    /// header row 18 + 2 margin, battery-spec strip 12, footer 17 + 5 margin, badge strip 16.
    ///
    /// CanvasNode's shared card chrome is suppressed for channels (.wf-node--channel in
    /// workflow-canvas.css): a channel IS the battery, so the node border, background, header
    /// padding/border and body padding would draw a second frame around the outline and push the
    /// cap 15.6px clear of the cell. Removing it accounts for exactly the 37px this constant
    /// dropped from 151 - node border 2, header padding 12, header border 1, body padding 16,
    /// body grid gap 4, cell top padding 2.
    ///
    /// The battery strip and the badge strip are BOTH unconditional. They have to be: this height
    /// is computed from the selected-property list alone, before any telemetry exists, so a strip
    /// that appeared only when a battery or program happened to be attached would make those
    /// channels taller than this says — attaching their edges off-centre and letting rows overlap.
    /// The badge strip was measured at 22.5px of unpredicted growth before it was reserved.
    /// </summary>
    public const double ChannelCellChromeHeight = 114;

    /// <summary>One section heading (12px) plus its margins (5 above, 2 below). Measured.</summary>
    public const double ChannelSectionHeadingHeight = 19;

    /// <summary>
    /// One rendered row, measured. Primary Data and the labelled sections came out IDENTICAL at
    /// 15px, so there is deliberately one constant rather than two — what differs between the
    /// sections is how many rows a given number of keys needs, not how tall a row is.
    /// </summary>
    public const double ChannelCellRowHeight = 15;

    /// <summary>The grid/flex gap between consecutive rows, measured. N rows cost N-1 gaps.</summary>
    public const double ChannelCellRowGap = 1;

    /// <summary>
    /// The tallest a channel card can ever get — base chrome plus the rows the property picker adds
    /// at its cap (WorkflowNodeProperties.MaxVisible). Used only as an upper bound in tests and
    /// docs now that RowHeight tracks the CURRENTLY selected properties rather than this worst
    /// case (see RowHeight) — a layout spaced for fewer properties than are now selected can still
    /// overlap until re-arranged (the toolbar's Re-arrange button, or automatically whenever the
    /// property selection itself changes — see WorkflowCanvasPage.HandleNodeConfigChanged).
    /// </summary>
    public static readonly double MaxChannelNodeHeight =
        ChannelNodeHeight(WorkflowNodeProperties.AvailableKeys);

    /// <summary>
    /// Rendered height of a channel battery cell showing exactly these properties.
    ///
    /// This takes the KEYS, not a count, and that is not incidental. Primary Data packs two values
    /// per row while Configuration and Program Data take one each, and every non-empty section adds
    /// a heading — so three primary keys and three program keys produce visibly different cells and
    /// a count cannot express the height. The previous ChannelNodeHeight(int) was therefore removed
    /// rather than kept as an overload: a silent fallback to a count would reintroduce exactly the
    /// desynchronisation this fixes.
    ///
    /// The single source of truth for that height: WorkflowEdgeGeometry needs it to attach an edge
    /// at the cell's vertical centre, MinimapProjection to scale the node, and Apply to space the
    /// rows. Section membership and row shape come from WorkflowNodeProperties.SectionsFor, the
    /// same call the component renders from, so the two cannot disagree.
    /// </summary>
    public static double ChannelNodeHeight(IReadOnlyList<string> visibleProperties)
    {
        var height = ChannelCellChromeHeight;

        foreach (var section in WorkflowNodeProperties.SectionsFor(visibleProperties))
        {
            var rows = section.TwoPerRow
                ? Math.Ceiling(section.Keys.Count / 2d)
                : section.Keys.Count;

            height += ChannelSectionHeadingHeight
                + rows * ChannelCellRowHeight
                + (rows - 1) * ChannelCellRowGap;
        }

        return height;
    }

    /// <summary>
    /// One row's vertical stride: the tallest a card actually needs to show EXACTLY these
    /// properties, plus a visual gutter, so a full row never touches the next. Deliberately NOT a
    /// worst-case constant any more — it tracks the CURRENTLY selected property list, so a canvas
    /// spaced for a short card does not carry a permanent, oversized gap once the user picks
    /// fewer fields (or vice-versa). Because of that, any already-placed row's spacing only stays
    /// correct until the selection changes; WorkflowCanvasPage.HandleNodeConfigChanged re-runs
    /// Apply over the whole graph whenever it does (and the toolbar's Re-arrange button does the
    /// same on demand).
    /// </summary>
    public static double RowHeight(IReadOnlyList<string> visibleProperties) =>
        ChannelNodeHeight(visibleProperties) + 26;

    private const double BoardColumnX = ColumnWidth;
    private const double ChannelStartX = ColumnWidth * 2;

    /// <summary>
    /// Lays out every device lane on the graph, each anchored at that DEVICE NODE'S OWN existing
    /// (X, Y) rather than a shared origin — so a fresh single-device subgraph (built with a brand
    /// new Device node at (0,0), the shape WorkflowPlacement.PlaceDevice always passes) still comes
    /// out local/relative exactly as before, while calling this directly over an ALREADY-PLACED
    /// multi-device graph (WorkflowCanvasPage.ReArrange's use) repacks each device's boards/
    /// channels around its own existing position instead of collapsing every device into one lane
    /// at the origin - the previous single-device-only version only ever looked at the FIRST
    /// device node found, silently leaving every other device's boards uninitialised/unmoved.
    /// </summary>
    public static WorkflowGraph Apply(WorkflowGraph graph, IReadOnlyList<string> visibleProperties)
    {
        var rowHeight = RowHeight(visibleProperties);
        var positions = new Dictionary<string, (double X, double Y)>();
        var assignedChannelIds = new HashSet<string>();
        double trailingY = 0;

        foreach (var device in graph.Nodes.Where(n => n.Kind == NodeKind.Device))
        {
            double cursorY = device.Y;

            var boardIds = graph.Edges
                .Where(e => e.FromNodeId == device.Id && e.Kind == EdgeKind.Topology)
                .Select(e => e.ToNodeId)
                .ToHashSet();

            foreach (var board in graph.Nodes.Where(n => n.Kind == NodeKind.Board && boardIds.Contains(n.Id)))
            {
                var channelIds = graph.Edges
                    .Where(e => e.FromNodeId == board.Id && e.Kind == EdgeKind.Topology)
                    .Select(e => e.ToNodeId)
                    .ToHashSet();

                var channels = graph.Nodes
                    .Where(n => n.Kind == NodeKind.Channel && channelIds.Contains(n.Id))
                    .ToList();

                positions[board.Id] = (device.X + BoardColumnX, cursorY);

                for (var i = 0; i < channels.Count; i++)
                {
                    var row = i / ChannelsPerRow;
                    var col = i % ChannelsPerRow;
                    positions[channels[i].Id] =
                        (device.X + ChannelStartX + col * ColumnWidth, cursorY + row * rowHeight);
                    assignedChannelIds.Add(channels[i].Id);
                }

                var rowsUsed = channels.Count == 0
                    ? 1
                    : (int)Math.Ceiling(channels.Count / (double)ChannelsPerRow);
                cursorY += rowsUsed * rowHeight;
            }

            trailingY = Math.Max(trailingY, cursorY);
        }

        // Orphan channels: no board claimed them (and so no device either). A bare test graph, or
        // a future graph shape this algorithm has not been taught about — never silently dropped
        // or overlapped. Trails after every real device lane rather than belonging to one.
        var orphanChannels = graph.Nodes
            .Where(n => n.Kind == NodeKind.Channel && !assignedChannelIds.Contains(n.Id))
            .ToList();

        for (var i = 0; i < orphanChannels.Count; i++)
        {
            var row = i / ChannelsPerRow;
            var col = i % ChannelsPerRow;
            positions[orphanChannels[i].Id] = (ChannelStartX + col * ColumnWidth, trailingY + row * rowHeight);
        }

        var positioned = graph.Nodes
            .Select(node => positions.TryGetValue(node.Id, out var pos) ? node with { X = pos.X, Y = pos.Y } : node)
            .ToList();

        return graph with { Nodes = positioned };
    }
}
