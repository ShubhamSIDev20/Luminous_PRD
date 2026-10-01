using System.Collections.Generic;
using System.Threading.Tasks;
using BatteryTestingSystem.MCP;
using BatteryTestingSystem.Services;
using Xunit;

namespace BatteryTestingSystem.Tests.Controllers;

/// <summary>
/// Covers DeviceMcpTools now that it builds a single-channel CommonRequest via
/// ChannelList (the ChannelNumber property it used to set no longer exists on
/// CommonRequest). Confirms each tool addresses the same "{deviceId}-{board}-{circuitId}"
/// handler key as the REST endpoints so MCP and REST stay behaviorally identical.
/// </summary>
public class DeviceMcpToolsTests
{
    private static ChannelManager NewChannelManager() =>
        new(() => throw new System.NotImplementedException(), new EventBusService(), null!);

    private static string Key(int deviceId, int board, int channel) => $"{deviceId}-{board}-{channel}";

    private static DeviceMcpTools NewTools(ChannelManager cm) =>
        new(cm, new FakeProgramServices(), new FakeBatteryServices(), new FakeDbcService());

    [Fact]
    public async Task StartProgram_TargetsSingleChannelKey()
    {
        var cm = NewChannelManager();
        cm._devices[Key(5, 1, 3)] = new FakeCoreCommandHandler();
        var tools = NewTools(cm);

        var result = await tools.StartProgram(deviceId: 5, circuitId: 3);

        Assert.Contains(Key(5, 1, 3), result);
        Assert.Contains("Success", result);
    }

    [Fact]
    public async Task StartProgram_UnknownCircuit_ReportsHandlerNotFound()
    {
        var cm = NewChannelManager();
        var tools = NewTools(cm);

        var result = await tools.StartProgram(deviceId: 5, circuitId: 99);

        Assert.Contains(Key(5, 1, 99), result);
        Assert.Contains("Handler not found", result);
    }

    [Fact]
    public async Task StopProgram_And_PauseProgram_And_ContinueProgram_TargetSingleChannel()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler();
        var tools = NewTools(cm);

        var stop = await tools.StopProgram(1, 1);
        var pause = await tools.PauseProgram(1, 1);
        var cont = await tools.ContinueProgram(1, 1);

        Assert.Contains("Success", stop);
        Assert.Contains("Success", pause);
        Assert.Contains("Success", cont);
    }

    [Fact]
    public async Task SendProgram_DispatchesProgramBatteryAndDbcToTheOneChannel()
    {
        var cm = NewChannelManager();
        var handler = new FakeCoreCommandHandler();
        cm._devices[Key(1, 1, 4)] = handler;
        var tools = NewTools(cm);

        var result = await tools.SendProgram(deviceId: 1, circuitId: 4, programId: 10, batteryId: 3, dbcId: 2);

        Assert.Equal(1, handler.SetProgramCallCount);
        Assert.Equal(1, handler.SetBatteryCallCount);
        Assert.Equal(1, handler.TransferDbcCallCount);
        Assert.Contains($"Program : {Key(1, 1, 4)} -> Success", result);
    }

    [Fact]
    public async Task GetLiveData_ReturnsRecordForSingleChannel()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler();
        var tools = NewTools(cm);

        var result = await tools.GetLiveData(deviceId: 1, circuitId: 1);

        Assert.DoesNotContain("No live data found", result);
    }

    [Fact]
    public void GetSession_UnknownCircuit_ReturnsHandlerNotFoundMessage()
    {
        var cm = NewChannelManager();
        var tools = NewTools(cm);

        var result = tools.GetSession(deviceId: 1, circuitId: 1);

        Assert.Equal($"Handler not found for {Key(1, 1, 1)}", result);
    }

    [Fact]
    public void GetDeviceStatus_KnownCircuit_ReturnsRealtimeRecordJson()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler();
        var tools = NewTools(cm);

        var result = tools.GetDeviceStatus(deviceId: 1, circuitId: 1);

        Assert.False(result.StartsWith("Handler not found"));
    }
}
