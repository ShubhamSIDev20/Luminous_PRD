using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// WithPlaceableDevicesOnly exists because deleting circuits on the Circuits page marks the
/// CHANNELS deleted and leaves every Device row live. A bench with one real device therefore kept
/// offering ten empty devices in the palette, plus any junk device a malformed registration packet
/// had created.
/// </summary>
public class TopologySnapshotTests
{
    private static TopologyDevice Device(int id, params int[] channelsPerBoard) =>
        new(id, $"DEV-{id}", channelsPerBoard
            .Select((count, b) => new TopologyBoard(
                id * 100 + b, b + 1,
                Enumerable.Range(1, count)
                    .Select(c => new TopologyChannel(id * 1000 + b * 10 + c, c))
                    .ToList()))
            .Cast<TopologyBoard>()
            .ToList());

    [Fact]
    public void DropsDevicesWithNoChannelsAtAll()
    {
        var snap = new TopologySnapshot(new List<TopologyDevice>
        {
            Device(1, 8),
            Device(2),        // every circuit deleted
            Device(3, 0),     // a board, but nothing under it
        });

        var kept = snap.WithPlaceableDevicesOnly().Devices;

        Assert.Equal(new[] { 1 }, kept.Select(d => d.DeviceId));
    }

    [Fact]
    public void DropsEmptyBoardsButKeepsTheDeviceWhenOneBoardSurvives()
    {
        var snap = new TopologySnapshot(new List<TopologyDevice> { Device(1, 0, 8, 0) });

        var device = Assert.Single(snap.WithPlaceableDevicesOnly().Devices);

        var board = Assert.Single(device.Boards);
        Assert.Equal(8, board.Channels.Count);
    }

    [Fact]
    public void KeepsEveryDeviceThatStillHasChannels()
    {
        var snap = new TopologySnapshot(new List<TopologyDevice> { Device(1, 8), Device(2, 4) });

        Assert.Equal(2, snap.WithPlaceableDevicesOnly().Devices.Count);
    }

    [Fact]
    public void LeavesTheOriginalSnapshotUntouched()
    {
        // The unfiltered snapshot still feeds the labeller and validators: hiding a device there
        // would report already-placed hardware as stale.
        var snap = new TopologySnapshot(new List<TopologyDevice> { Device(1, 8), Device(2) });

        snap.WithPlaceableDevicesOnly();

        Assert.Equal(2, snap.Devices.Count);
        Assert.True(snap.HasDevice(2));
    }

    [Fact]
    public void AnEmptySnapshotStaysEmpty_RatherThanThrowing()
    {
        Assert.Empty(TopologySnapshot.Empty.WithPlaceableDevicesOnly().Devices);
    }
}
