using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Implementations;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class ChannelManagerMultiplexingTests
{
    [Fact]
    public void TwoChannelSlots_OnSameDeviceConnection_ShareOneTcpClient()
    {
        var deviceConnection = new DeviceConnection();

        var channelOneHandler = new ChannelCommandHandler
        {
            Channel = new ChannelDto { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 1 },
            Connection = deviceConnection
        };

        var channelTwoHandler = new ChannelCommandHandler
        {
            Channel = new ChannelDto { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 2 },
            Connection = deviceConnection
        };

        Assert.Same(channelOneHandler.Connection, channelTwoHandler.Connection);
    }

    [Fact]
    public void MarkDisconnected_SetsIsConnectedFalse_IndependentOfOtherSlots()
    {
        var deviceConnection = new DeviceConnection();

        var channelOneHandler = new ChannelCommandHandler
        {
            Channel = new ChannelDto { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 1 },
            Connection = deviceConnection
        };

        channelOneHandler.MarkDisconnected();

        Assert.False(channelOneHandler.IsConnected);
    }
}
