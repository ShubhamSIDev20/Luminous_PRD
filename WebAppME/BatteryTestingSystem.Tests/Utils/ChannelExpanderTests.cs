using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.APIModels;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

public class ChannelExpanderTests
{
    private static CommonRequest Base(params int[] channels) => new()
    {
        DeviceID = 1,
        SecondaryBoardNumber = 1,
        ChannelList = channels.ToList(),
        ProgramId = 10,
        BatteryId = 3,
        dbcId = 2,
    };

    // ── GetChannels: validation ─────────────────────────────────────

    [Fact]
    public void GetChannels_NullList_Throws()
    {
        var req = new CommonRequest { DeviceID = 1, ChannelList = null! };
        Assert.Throws<ArgumentException>(() => ChannelExpander.GetChannels(req));
    }

    [Fact]
    public void GetChannels_EmptyList_Throws()
    {
        var req = Base();
        Assert.Throws<ArgumentException>(() => ChannelExpander.GetChannels(req));
    }

    [Fact]
    public void GetChannels_AllNonPositive_Throws()
    {
        var req = Base(0, -1, -5);
        Assert.Throws<ArgumentException>(() => ChannelExpander.GetChannels(req));
    }

    [Fact]
    public void GetChannels_FiltersOutNonPositiveValues()
    {
        var req = Base(0, 1, -2, 3);
        var result = ChannelExpander.GetChannels(req);

        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void GetChannels_DedupesAndSorts()
    {
        var req = Base(5, 1, 5, 3, 1);
        var result = ChannelExpander.GetChannels(req);

        Assert.Equal(new[] { 1, 3, 5 }, result);
    }

    [Fact]
    public void GetChannels_SingleChannel_ReturnsOne()
    {
        var req = Base(7);
        var result = ChannelExpander.GetChannels(req);

        Assert.Equal(new[] { 7 }, result);
    }

    [Fact]
    public void GetChannels_Over128Channels_Throws()
    {
        var req = Base(Enumerable.Range(1, 129).ToArray());
        Assert.Throws<ArgumentException>(() => ChannelExpander.GetChannels(req));
    }

    [Fact]
    public void GetChannels_Exactly128Channels_Allowed()
    {
        var req = Base(Enumerable.Range(1, 128).ToArray());
        var result = ChannelExpander.GetChannels(req);

        Assert.Equal(128, result.Count);
    }

    // ── ExpandChannels: per-channel request fan-out ─────────────────

    [Fact]
    public void ExpandChannels_MultipleChannels_ProducesOneRequestPerChannel()
    {
        var req = Base(1, 5, 10, 15);
        var result = ChannelExpander.ExpandChannels(req);

        Assert.Equal(4, result.Count);
        Assert.Equal(new[] { 1, 5, 10, 15 }, result.Select(r => r.ChannelList.Single()));
    }

    [Fact]
    public void ExpandChannels_PreservesNonChannelFields()
    {
        var req = Base(1, 2);
        var result = ChannelExpander.ExpandChannels(req);

        foreach (var r in result)
        {
            Assert.Equal(1, r.DeviceID);
            Assert.Equal(1, r.SecondaryBoardNumber);
            Assert.Equal(10, r.ProgramId);
            Assert.Equal(3, r.BatteryId);
            Assert.Equal(2, r.dbcId);
        }
    }

    [Fact]
    public void ExpandChannels_Duplicates_Deduped()
    {
        var req = Base(3, 1, 3, 2);
        var result = ChannelExpander.ExpandChannels(req);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Select(r => r.ChannelList.Single()));
    }

    [Fact]
    public void ExpandChannels_EmptyList_Throws()
    {
        var req = Base();
        Assert.Throws<ArgumentException>(() => ChannelExpander.ExpandChannels(req));
    }

    [Fact]
    public void ExpandChannels_Over128Channels_Throws()
    {
        var req = Base(Enumerable.Range(1, 129).ToArray());
        Assert.Throws<ArgumentException>(() => ChannelExpander.ExpandChannels(req));
    }
}
