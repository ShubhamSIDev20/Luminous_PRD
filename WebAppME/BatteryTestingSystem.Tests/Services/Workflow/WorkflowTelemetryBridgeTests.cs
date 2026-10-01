using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Spec 6. The bridge is what keeps a 64-channel canvas usable on Blazor Server: it filters
/// telemetry to the channels actually placed, and turns each tick into CSS-ready values so the
/// JS layer only has to write custom properties. Nothing here may cause a re-render.
/// </summary>
public class WorkflowTelemetryBridgeTests
{
    private static WorkflowGraph GraphWith(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    private static WorkflowNode Channel(long id) =>
        new(WorkflowGraphBuilder.ChannelNodeId(id), NodeKind.Channel, id, 0, 0, null);

    // ============================================================ OnCanvasChannelIds

    [Fact]
    public void OnCanvasChannelIds_ReturnsOnlyChannelsThatArePlaced()
    {
        // The whole cost argument: subscription size tracks what the operator placed, not the
        // 640 circuits the app may know about.
        var graph = GraphWith(
            Channel(100),
            Channel(101),
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null));

        var ids = WorkflowTelemetryBridge.OnCanvasChannelIds(graph, System.Array.Empty<string>());

        Assert.Equal(new[] { 100L, 101L }, ids.OrderBy(i => i));
    }

    [Fact]
    public void OnCanvasChannelIds_ExcludesStaleNodes()
    {
        // A stale node points at deleted hardware; subscribing for it would be a permanent
        // no-data slot in every tick.
        var graph = GraphWith(Channel(100), Channel(999));

        var ids = WorkflowTelemetryBridge.OnCanvasChannelIds(
            graph, new[] { WorkflowGraphBuilder.ChannelNodeId(999) });

        Assert.Equal(new[] { 100L }, ids);
    }

    [Fact]
    public void OnCanvasChannelIds_IsEmptyForAnEmptyGraph()
    {
        Assert.Empty(WorkflowTelemetryBridge.OnCanvasChannelIds(
            WorkflowGraph.Empty, System.Array.Empty<string>()));
    }

    // ============================================================ ShouldThrottle

    [Fact]
    public void ShouldThrottle_NeverThrottlesTheFirstEverPush()
    {
        // default(DateTime) means "no push has happened yet" - the very first tick must always
        // go through, or a freshly opened canvas would show nothing until the window elapses.
        Assert.False(WorkflowTelemetryBridge.ShouldThrottle(
            lastPushUtc: default, nowUtc: DateTime.UtcNow, refreshRateMs: 250));
    }

    [Fact]
    public void ShouldThrottle_ReturnsTrueWithinTheWindow()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMilliseconds(100);

        Assert.True(WorkflowTelemetryBridge.ShouldThrottle(last, now, refreshRateMs: 250));
    }

    [Fact]
    public void ShouldThrottle_ReturnsFalseOnceTheWindowHasElapsed()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMilliseconds(300);

        Assert.False(WorkflowTelemetryBridge.ShouldThrottle(last, now, refreshRateMs: 250));
    }

    [Fact]
    public void ShouldThrottle_ExactlyAtTheWindowBoundaryIsNotThrottled()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMilliseconds(250);

        Assert.False(WorkflowTelemetryBridge.ShouldThrottle(last, now, refreshRateMs: 250));
    }

    // ============================================================ HslFor

    [Theory]
    [InlineData(CircuitStatus.Idle, "0 0% 62%")]
    [InlineData(CircuitStatus.Charge, "51 100% 50%")]
    [InlineData(CircuitStatus.Discharging, "33 100% 50%")]
    [InlineData(CircuitStatus.Pause, "207 90% 54%")]
    [InlineData(CircuitStatus.Countinue, "122 39% 49%")]
    [InlineData(CircuitStatus.Interrupt, "37 75% 35%")]
    [InlineData(CircuitStatus.Error, "4 90% 58%")]
    [InlineData(CircuitStatus.Msg, "187 100% 42%")]
    [InlineData(CircuitStatus.Offline, "0 0% 62%")]
    public void HslFor_MatchesTheSamePaletteBuildEntriesUses(CircuitStatus status, string expected)
    {
        // A legend that drifts from the actual node-face colors would be worse than no legend -
        // it must read from the exact same source BuildEntries does, not a second copy.
        Assert.Equal(expected, WorkflowTelemetryBridge.HslFor(status));
    }

    // ============================================================ HslFor with user overrides

    [Fact]
    public void HslFor_UsesTheUsersOverrideWhenOneIsSet()
    {
        var overrides = new Dictionary<string, string> { ["charge"] = "300 80% 40%" };

        Assert.Equal("300 80% 40%", WorkflowTelemetryBridge.HslFor(CircuitStatus.Charge, overrides));
    }

    [Fact]
    public void HslFor_FallsBackToTheBuiltInPaletteForStatusesTheUserHasNotChanged()
    {
        var overrides = new Dictionary<string, string> { ["charge"] = "300 80% 40%" };

        Assert.Equal("4 90% 58%", WorkflowTelemetryBridge.HslFor(CircuitStatus.Error, overrides));
    }

    [Fact]
    public void HslFor_WithNoOverridesMatchesTheParameterlessPalette()
    {
        foreach (CircuitStatus s in Enum.GetValues(typeof(CircuitStatus)))
        {
            Assert.Equal(
                WorkflowTelemetryBridge.HslFor(s),
                WorkflowTelemetryBridge.HslFor(s, new Dictionary<string, string>()));
        }
    }

    [Fact]
    public void HslFor_IgnoresAnOverrideStoredUnderAnUnknownStatusName()
    {
        // A stale key from a renamed status must never take precedence over a real one.
        var overrides = new Dictionary<string, string> { ["not-a-status"] = "1 2% 3%" };

        Assert.Equal(
            WorkflowTelemetryBridge.HslFor(CircuitStatus.Idle),
            WorkflowTelemetryBridge.HslFor(CircuitStatus.Idle, overrides));
    }

    // ============================================================ FlowFor

    [Theory]
    [InlineData(CircuitStatus.Charge, ProgramRunningStatus.Running, 1)]
    [InlineData(CircuitStatus.Discharging, ProgramRunningStatus.Running, -1)]
    [InlineData(CircuitStatus.Idle, ProgramRunningStatus.Running, 0)]
    [InlineData(CircuitStatus.Pause, ProgramRunningStatus.Running, 0)]
    [InlineData(CircuitStatus.Offline, ProgramRunningStatus.Running, 0)]
    [InlineData(CircuitStatus.Error, ProgramRunningStatus.Running, 0)]
    public void FlowFor_OnlyChargeAndDischargeProduceMotionWhileRunning(
        CircuitStatus status, ProgramRunningStatus programStatus, int expected)
    {
        // A paused or errored channel must be completely still: motion means "this channel is
        // running a program and moving charge in this direction".
        Assert.Equal(expected, WorkflowTelemetryBridge.FlowFor(status, programStatus));
    }

    [Fact]
    public void FlowFor_AnimatesAChargingChannelEvenWhenItReportsZeroCurrent()
    {
        // Regression. This used to require |current| >= 0.001, which silently killed the
        // animation on a channel reporting Running + Charge at 0.00 A - a state this bench
        // produces, and the reason the animation appeared to have vanished. Whether the edge
        // animates answers "is this channel running?", and current does not decide that.
        Assert.Equal(1, WorkflowTelemetryBridge.FlowFor(
            CircuitStatus.Charge, ProgramRunningStatus.Running));
    }

    [Fact]
    public void FlowFor_ReturnsZeroWhenTheProgramIsNotRunning()
    {
        // D20: a Charge/Discharging reading on a stopped program must not animate.
        Assert.Equal(0, WorkflowTelemetryBridge.FlowFor(
            CircuitStatus.Charge, ProgramRunningStatus.Stop));
    }

    // ============================================================ BuildEntries

    [Fact]
    public void BuildEntries_ProducesOneEntryPerPlacedChannelWithData()
    {
        var graph = GraphWith(Channel(100), Channel(101));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        var entries = WorkflowTelemetryBridge.BuildEntries(graph, data);

        Assert.Single(entries);
        Assert.Equal("chn-100", entries[0].NodeId);
    }

    [Fact]
    public void BuildEntries_EmitsTheCorrectedStatusName()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Countinue, 0.5, 3.7, 1),
        };

        var entries = WorkflowTelemetryBridge.BuildEntries(graph, data);

        Assert.Equal("continue", entries[0].StatusName);
    }

    [Fact]
    public void BuildEntries_ADisconnectedChannelIsPaintedOfflineNotItsLastKnownStatus()
    {
        // Regression: the cell kept showing its last hardware-reported CircuitStatus (e.g. Charge,
        // still green) forever after the channel actually went offline, because IsConnected rode
        // along in ChannelTelemetry but nothing read it. DeviceChannel.razor's own dashboard card
        // already forces Offline here - the canvas must match it exactly.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10) { IsConnected = false },
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("offline", entry.StatusName);
        Assert.Equal(WorkflowTelemetryBridge.HslFor(CircuitStatus.Offline), entry.StatusHsl);
        Assert.Equal(0, entry.Flow);
    }

    [Fact]
    public void BuildEntries_ADisconnectedChannelWithAProgramStillRunningIsPaintedErrorNotOffline()
    {
        // Same DeviceChannel.razor rule: a link drop mid-test is a fault worth flagging (Error),
        // not an ordinary offline channel - the operator left a program running unattended.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10)
            {
                IsConnected = false,
                ProgramStatus = ProgramRunningStatus.Running,
            },
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("error", entry.StatusName);
    }

    [Fact]
    public void BuildEntries_AConnectedChannelKeepsItsRealStatus()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10) { IsConnected = true },
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("charge", entry.StatusName);
    }

    [Fact]
    public void BuildEntries_EmitsAnHslTripletNotAVariableName()
    {
        // JS writes this straight into --wf-status, which is consumed as hsl(var(--wf-status)).
        // A "--status-charge" string there would produce hsl(--status-charge) and no colour.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        var hsl = WorkflowTelemetryBridge.BuildEntries(graph, data)[0].StatusHsl;

        Assert.DoesNotContain("--", hsl);
        Assert.Matches(@"^\d+(\.\d+)? \d+(\.\d+)?% \d+(\.\d+)?%$", hsl);
    }

    [Fact]
    public void BuildEntries_ClampsStateOfChargeIntoRange()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 1.4, 3.7, 10),
        };

        Assert.Equal(1.0, WorkflowTelemetryBridge.BuildEntries(graph, data)[0].Soc);
    }

    [Fact]
    public void BuildEntries_UnknownSocIsFlaggedAndTextedWithAZeroFill()
    {
        // Soc null means "no battery, or no usable rated capacity". The battery cell renders an
        // explicit unknown state; Soc itself must still be a real number so the CSS custom
        // property parses.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, null, 3.7, 10),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.False(entry.SocKnown);
        Assert.Equal("--%", entry.SocText);
        Assert.Equal(0d, entry.Soc);
        Assert.Null(entry.BatteryLine);
    }

    [Fact]
    public void BuildEntries_KnownSocIsRoundedToWholePercent()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.624, 3.7, 10),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.True(entry.SocKnown);
        Assert.Equal("62%", entry.SocText);
        Assert.Equal(0.624, entry.Soc);
    }

    [Fact]
    public void BuildEntries_BatteryLineJoinsTheRatedFigures()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10,
                        NominalCapacity: 50f, NominalVoltage: 3.7f, NumberOfCells: 1),
        };

        Assert.Equal(
            "50 Ah · 3.7 V · 1 cell",
            WorkflowTelemetryBridge.BuildEntries(graph, data)[0].BatteryLine);
    }

    [Fact]
    public void BuildEntries_BatteryLinePluralisesCellsAndOmitsUnsetFigures()
    {
        // BatteryDTO's numeric fields default to 0 rather than null, so "unset" and "zero" arrive
        // identically. Showing "0 V" would read as a measurement rather than a gap.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10,
                        NominalCapacity: 100f, NominalVoltage: 0f, NumberOfCells: 4),
        };

        Assert.Equal(
            "100 Ah · 4 cells",
            WorkflowTelemetryBridge.BuildEntries(graph, data)[0].BatteryLine);
    }

    [Fact]
    public void BuildEntries_BatteryLineIsInvariantCultureFormatted()
    {
        // The line is itself separator-delimited, so a comma decimal separator would read as
        // another item in the list.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10, NominalCapacity: 2.5f),
        };

        Assert.Equal(
            "2.5 Ah",
            WorkflowTelemetryBridge.BuildEntries(graph, data)[0].BatteryLine);
    }

    [Fact]
    public void BuildEntries_SkipsChannelsWithNoTelemetryYet()
    {
        // A placed channel whose device has not registered yet must simply not appear in the
        // batch, leaving its node showing the "--" placeholders.
        var graph = GraphWith(Channel(100), Channel(101));

        var entries = WorkflowTelemetryBridge.BuildEntries(
            graph, new Dictionary<long, ChannelTelemetry>());

        Assert.Empty(entries);
    }

    [Fact]
    public void BuildEntries_IgnoresTelemetryForChannelsNotOnTheCanvas()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
            [999] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        Assert.Single(WorkflowTelemetryBridge.BuildEntries(graph, data));
    }

    // ============================================================ BuildRollups

    private static IReadOnlyCollection<long> Children(NodeKind kind, long entityId) =>
        (kind, entityId) switch
        {
            (NodeKind.Board, 76) => new[] { 100L, 101L, 102L },
            (NodeKind.Device, 1) => new[] { 100L, 101L, 102L, 103L },
            _ => System.Array.Empty<long>(),
        };

    [Fact]
    public void BuildRollups_CountsOnlyChannelsThatAreNotOffline()
    {
        // Uses a Device node: a Board's rollup is a plain Online/Offline word now (D19), not this
        // N/M count, so a Device is the exemplar for the generic "count non-offline" logic.
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            Channel(100), Channel(101), Channel(102));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
            [101] = new(CircuitStatus.Offline, 0, 0, 0),
            [102] = new(CircuitStatus.Pause, 0, 0, 0),
        };

        var rollups = WorkflowTelemetryBridge.BuildRollups(graph, data, Children);

        Assert.Equal("2 / 3 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_TreatsAPlacedChannelWithNoTelemetryAsOffline()
    {
        // A placed channel whose device never registered has no reading at all. Counting it as
        // online would report hardware that is not there.
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            Channel(100), Channel(101));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
        };

        var rollups = WorkflowTelemetryBridge.BuildRollups(graph, data, Children);

        Assert.Equal("1 / 2 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_CountsOnlyChannelsActuallyPlacedOnTheCanvas()
    {
        // Device 1 owns four channels but the operator placed two. Telemetry only ever arrives
        // for placed channels, so a denominator of 4 would report the two unplaced ones as
        // offline when the truth is that nothing is watching them.
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            Channel(100), Channel(101));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
            [101] = new(CircuitStatus.Charge, 0, 0, 0),
        };

        var rollups = WorkflowTelemetryBridge.BuildRollups(graph, data, Children);

        Assert.Equal("2 / 2 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_EmitsOneEntryPerDeviceAndBoardNode()
    {
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100));

        var rollups = WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children);

        Assert.Equal(new[] { "brd-76", "dev-1" }, rollups.Select(r => r.NodeId).OrderBy(i => i));
    }

    [Fact]
    public void BuildRollups_ReportsZeroOfZeroForAnEntityWithNoPlacedChannels()
    {
        // A device whose channels were all removed from the canvas, or a stale node.
        var graph = GraphWith(new WorkflowNode("dev-9", NodeKind.Device, 9, 0, 0, null));

        var rollups = WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children);

        Assert.Equal("0 / 0 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_SkipsNodesWithNoEntityId()
    {
        var graph = GraphWith(new WorkflowNode("dev-x", NodeKind.Device, null, 0, 0, null));

        Assert.Empty(WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children));
    }

    [Fact]
    public void BuildRollups_BoardIsOnlineWhenAnyChannelIsConnected()
    {
        var graph = GraphWith(
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100), Channel(101), Channel(102));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
            [101] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: true),
            [102] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
        };

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(graph, data, Children));

        Assert.Equal("Online", rollup.OnlineText);
        Assert.True(rollup.IsOnline);
    }

    [Fact]
    public void BuildRollups_BoardIsOfflineWhenNoChannelIsConnected()
    {
        var graph = GraphWith(
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100), Channel(101), Channel(102));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
            [101] = new(CircuitStatus.Offline, 0, 0, 0, IsConnected: false),
            [102] = new(CircuitStatus.Idle, 0, 0, 0, IsConnected: false),
        };

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(graph, data, Children));

        Assert.Equal("Offline", rollup.OnlineText);
        Assert.False(rollup.IsOnline);
    }

    [Fact]
    public void BuildRollups_BoardWithNoTelemetryAtAllIsOffline()
    {
        var graph = GraphWith(
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100));

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children));

        Assert.Equal("Offline", rollup.OnlineText);
        Assert.False(rollup.IsOnline);
    }

    [Fact]
    public void BuildRollups_DeviceOnlineTextFormatIsUnchangedAndIsOnlineIsAlwaysTrue()
    {
        // D19 only changes how a BOARD is presented; a Device rollup keeps its existing
        // "N / M online" wording exactly as it was before this phase.
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            Channel(100), Channel(101));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
            [101] = new(CircuitStatus.Offline, 0, 0, 0),
        };

        var rollup = Assert.Single(WorkflowTelemetryBridge.BuildRollups(graph, data, Children));

        Assert.Equal("1 / 2 online", rollup.OnlineText);
        Assert.True(rollup.IsOnline);
    }

    // ============================================================ BuildEntries — dock-depth fields

    [Fact]
    public void BuildEntries_CarriesPowerAndTemperatureForTheNodeFace()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10, Power: 37.0, Temperature: 28.4),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal(37.0, entry.Power);
        Assert.Equal(28.4, entry.Temperature);
    }

    [Fact]
    public void BuildEntries_CarriesCapacityAndEnergyBreakdownForTheDock()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(
                CircuitStatus.Charge, 0.5, 3.7, 10,
                AccumulatedCapacity: 1.1, ChargeCapacity: 2.2, DischargeCapacity: 3.3, StepCapacity: 4.4,
                AccumulatedEnergy: 5.5, ChargeEnergy: 6.6, DischargeEnergy: 7.7, StepEnergy: 8.8,
                CycleNumber: 3),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal(1.1, entry.AccumulatedCapacity);
        Assert.Equal(2.2, entry.ChargeCapacity);
        Assert.Equal(3.3, entry.DischargeCapacity);
        Assert.Equal(4.4, entry.StepCapacity);
        Assert.Equal(5.5, entry.AccumulatedEnergy);
        Assert.Equal(6.6, entry.ChargeEnergy);
        Assert.Equal(7.7, entry.DischargeEnergy);
        Assert.Equal(8.8, entry.StepEnergy);
        Assert.Equal(3, entry.CycleNumber);
    }

    [Fact]
    public void BuildEntries_FormatsTableProgressAsStepOverTotal()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10, TableStepNumber: 3, TableTotalRowNumber: 12),
        };

        Assert.Equal("3 / 12", WorkflowTelemetryBridge.BuildEntries(graph, data)[0].TableProgress);
    }

    [Fact]
    public void BuildEntries_FormatsRunningTimesAsHhMmSs()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(
                CircuitStatus.Charge, 0.5, 3.7, 10,
                RunningTime: TimeSpan.FromSeconds(3725),
                StepRunningTime: TimeSpan.FromSeconds(65)),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("01:02:05", entry.RunningTime);
        Assert.Equal("00:01:05", entry.StepRunningTime);
    }

    [Fact]
    public void BuildEntries_LeavesErrorTextNullWhenNoErrorIsSet()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0.5, 3.7, 10),
        };

        Assert.Null(WorkflowTelemetryBridge.BuildEntries(graph, data)[0].ErrorText);
    }

    // ============================================================ ResolveErrorText

    // ============================================================ Footer parity

    [Fact]
    public void BuildEntries_ReportsProgramStatusUsingTheDashboardsWording()
    {
        // DeviceChannel.GetProgramStatusText maps Running -> "Running" and everything else to
        // "Stop". The canvas must not invent different words for the same states.
        var graph = GraphWith(Channel(100));
        var running = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0,
                        ProgramStatus: ProgramRunningStatus.Running),
        };
        var stopped = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Idle, 0, 0, 0, ProgramStatus: ProgramRunningStatus.Stop),
        };

        Assert.Equal("Running", WorkflowTelemetryBridge.BuildEntries(graph, running)[0].ProgramStatusText);
        Assert.Equal("Stop", WorkflowTelemetryBridge.BuildEntries(graph, stopped)[0].ProgramStatusText);
    }

    [Fact]
    public void BuildEntries_FormatsTheLastUpdateAsAWallClock()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0,
                        TimeStamp: new DateTime(2026, 8, 23, 14, 5, 9)),
        };

        Assert.Equal("14:05:09", WorkflowTelemetryBridge.BuildEntries(graph, data)[0].LastUpdateText);
    }

    [Fact]
    public void BuildEntries_ShowsNoClockWhenNoReadingHasEverArrived()
    {
        // default(DateTime) means "never updated". Rendering it as 00:00:00 would look like a
        // real reading taken at midnight.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Offline, 0, 0, 0),
        };

        Assert.Equal("--", WorkflowTelemetryBridge.BuildEntries(graph, data)[0].LastUpdateText);
    }


    [Fact]
    public void ResolveErrorText_UsesTheErrorsListWhenStatusIsError()
    {
        var errors = new List<CodeMessageDto> { new() { Index = 7, Message = "Over-voltage" } };
        var messages = new List<CodeMessageDto> { new() { Index = 7, Message = "wrong list" } };

        var text = WorkflowTelemetryBridge.ResolveErrorText(
            CircuitStatus.Error, errorId: 7, systemErrorId: null, errors, messages);

        Assert.Equal("Over-voltage", text);
    }

    [Fact]
    public void ResolveErrorText_UsesTheMessagesListWhenStatusIsNotError()
    {
        var errors = new List<CodeMessageDto> { new() { Index = 7, Message = "wrong list" } };
        var messages = new List<CodeMessageDto> { new() { Index = 7, Message = "Step complete" } };

        var text = WorkflowTelemetryBridge.ResolveErrorText(
            CircuitStatus.Countinue, errorId: 7, systemErrorId: null, errors, messages);

        Assert.Equal("Step complete", text);
    }

    [Fact]
    public void ResolveErrorText_DecodesSystemErrorBitmaskWhenNoErrorIdIsSet()
    {
        // en_ERR_OVER_TEMPERATURE = 1<<2, en_ERR_OVER_VOLTAGE = 1<<6
        var text = WorkflowTelemetryBridge.ResolveErrorText(
            CircuitStatus.Error, errorId: null, systemErrorId: (1 << 2) | (1 << 6),
            new List<CodeMessageDto>(), new List<CodeMessageDto>());

        Assert.Equal("en_ERR_OVER_TEMPERATURE, en_ERR_OVER_VOLTAGE", text);
    }

    [Fact]
    public void ResolveErrorText_ReturnsNullWhenNeitherErrorIsSet()
    {
        var text = WorkflowTelemetryBridge.ResolveErrorText(
            CircuitStatus.Charge, errorId: null, systemErrorId: null,
            new List<CodeMessageDto>(), new List<CodeMessageDto>());

        Assert.Null(text);
    }

    [Fact]
    public void ResolveErrorText_ReturnsNullWhenLookupListHasNoMatchingEntry()
    {
        var text = WorkflowTelemetryBridge.ResolveErrorText(
            CircuitStatus.Error, errorId: 42, systemErrorId: null,
            new List<CodeMessageDto>(), new List<CodeMessageDto>());

        Assert.Null(text);
    }

    // ============================================================ BuildNodeProperties

    [Fact]
    public void BuildNodeProperties_ReturnsEveryAvailableKey()
    {
        var reading = new ChannelTelemetry(CircuitStatus.Charge, 0, 0, 0);

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal(BatteryTestingSystem.Services.Implementations.Workflow.WorkflowNodeProperties.AvailableKeys.OrderBy(k => k),
            properties.Keys.OrderBy(k => k));
    }

    [Fact]
    public void BuildNodeProperties_FormatsValuesWithoutRepeatingTheLabelsUnit()
    {
        var reading = new ChannelTelemetry(
            CircuitStatus.Charge, 0, Voltage: 3.7, Current: 12.456,
            Power: 45.678, Temperature: 28.44,
            AccumulatedCapacity: 1.2346, ChargeCapacity: 2.3467,
            AccumulatedEnergy: 3.4578, CycleNumber: 3,
            TableStepNumber: 4, TableTotalRowNumber: 12);

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal("3.70 V", properties["Voltage"]);
        Assert.Equal("12.46 A", properties["Current"]);
        Assert.Equal("45.68 W", properties["Power"]);
        Assert.Equal("28.4 °C", properties["Temperature"]);

        // The distinguishing part: the four capacities and four energies each carry their OWN
        // token from the hardware's vocabulary, not four identical "Ah" and four identical "Wh".
        // The node face shows values with no visible labels, so the token is what tells them apart.
        Assert.Equal("1.235 Ah", properties["AccumulatedCapacity"]);
        Assert.Equal("2.347 AhCha", properties["ChargeCapacity"]);
        Assert.Equal("3.458 Wh", properties["AccumulatedEnergy"]);
        Assert.Equal("3", properties["CycleNumber"]);
        Assert.Equal("4", properties["TableStepNumber"]);
        Assert.Equal("12", properties["TableTotalRowNumber"]);
    }

    [Fact]
    public void BuildNodeProperties_FormatsRunningTimesTheSameWayTheDockDoes()
    {
        var reading = new ChannelTelemetry(
            CircuitStatus.Charge, 0, 0, 0,
            RunningTime: TimeSpan.FromSeconds(3725),
            StepRunningTime: TimeSpan.FromSeconds(65));

        var properties = WorkflowTelemetryBridge.BuildNodeProperties(reading);

        Assert.Equal("01:02:05", properties["RunningTime"]);
        Assert.Equal("00:01:05", properties["StepRunningTime"]);
    }

    [Fact]
    public void BuildEntries_CarriesTheNodePropertiesDictionary()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 3.7, 1),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("3.70 V", entry.Properties["Voltage"]);
    }
}
