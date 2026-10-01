using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Controllers;
using BatteryTestingSystem.Models.APIModels;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using Xunit;

namespace BatteryTestingSystem.Tests.Controllers;

/// <summary>
/// Covers DeviceController's Core* helpers against the ChannelList-based CommonRequest
/// (replacing the old single ChannelNumber / ChannelRange model): bulk channel fan-out,
/// missing-handler handling, and the invalid-ChannelList error path added alongside the
/// ChannelList migration (a request with no valid channels must produce an error message
/// for that request and continue processing the rest of the batch, not throw and 500 it).
/// </summary>
public class DeviceControllerTests
{
    private static ChannelManager NewChannelManager() =>
        new(() => throw new NotImplementedException(), new EventBusService(), null!);

    private static string Key(int deviceId, int board, int channel) => $"{deviceId}-{board}-{channel}";

    private static CommonRequest Req(int deviceId, List<int> channels, int board = 1) => new()
    {
        DeviceID = deviceId,
        SecondaryBoardNumber = board,
        ChannelList = channels,
    };

    // ── CoreStart: bulk channel expansion ───────────────────────────

    [Fact]
    public async Task CoreStart_ExpandsChannelListAcrossAllMatchingHandlers()
    {
        var cm = NewChannelManager();
        var handlers = new[] { 1, 2, 3 }.ToDictionary(ch => ch, ch => new FakeCoreCommandHandler());
        foreach (var (ch, handler) in handlers)
            cm._devices[Key(1, 1, ch)] = handler;

        var messages = await DeviceController.CoreStart(cm, new List<CommonRequest> { Req(1, new List<int> { 1, 2, 3 }) });

        Assert.Equal(3, messages.Count);
        Assert.All(messages, m => Assert.Contains("Success", m));
    }

    [Fact]
    public async Task CoreStart_MissingHandler_ReportsPerChannelWithoutAborting()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler();
        // channel 2 has no registered handler

        var messages = await DeviceController.CoreStart(cm, new List<CommonRequest> { Req(1, new List<int> { 1, 2 }) });

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Contains(Key(1, 1, 1)) && m.Contains("Success"));
        Assert.Contains(messages, m => m.Contains(Key(1, 1, 2)) && m.Contains("Handler not found"));
    }

    [Fact]
    public async Task CoreStart_EmptyChannelList_DoesNotThrow_ReportsErrorForThatRequestOnly()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler();

        var request = new List<CommonRequest>
        {
            Req(1, new List<int>()),           // invalid: no channels
            Req(1, new List<int> { 1 }),        // valid, should still be processed
        };

        var messages = await DeviceController.CoreStart(cm, request);

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Contains("ChannelList"));
        Assert.Contains(messages, m => m.Contains(Key(1, 1, 1)) && m.Contains("Success"));
    }

    [Fact]
    public async Task CoreStop_PropagatesFailureMessageFromHandler()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler
        {
            StopResult = CommonResponse<bool>.Fail("comm timeout")
        };

        var messages = await DeviceController.CoreStop(cm, new List<CommonRequest> { Req(1, new List<int> { 1 }) });

        Assert.Single(messages);
        Assert.Contains("Failed: comm timeout", messages[0]);
    }

    [Fact]
    public async Task CorePause_And_CoreContinue_ExpandChannelList()
    {
        var cm = NewChannelManager();
        cm._devices[Key(2, 1, 10)] = new FakeCoreCommandHandler();
        cm._devices[Key(2, 1, 11)] = new FakeCoreCommandHandler();

        var pauseMessages = await DeviceController.CorePause(cm, new List<CommonRequest> { Req(2, new List<int> { 10, 11 }) });
        var continueMessages = await DeviceController.CoreContinue(cm, new List<CommonRequest> { Req(2, new List<int> { 10, 11 }) });

        Assert.Equal(2, pauseMessages.Count);
        Assert.Equal(2, continueMessages.Count);
        Assert.All(pauseMessages, m => Assert.Contains("Success", m));
        Assert.All(continueMessages, m => Assert.Contains("Success", m));
    }

    // ── CoreSendProgram: per-channel dispatch + shared lookups ──────

    [Fact]
    public async Task CoreSendProgram_SendsProgramBatteryAndDbcToEachChannel()
    {
        var cm = NewChannelManager();
        var h1 = new FakeCoreCommandHandler();
        var h2 = new FakeCoreCommandHandler();
        cm._devices[Key(1, 1, 1)] = h1;
        cm._devices[Key(1, 1, 2)] = h2;

        var programServices = new FakeProgramServices();
        var batteryServices = new FakeBatteryServices();
        var dbcService = new FakeDbcService();

        var request = new List<CommonRequest>
        {
            new()
            {
                DeviceID = 1,
                SecondaryBoardNumber = 1,
                ChannelList = new List<int> { 1, 2 },
                ProgramId = 10,
                BatteryId = 3,
                dbcId = 2,
            }
        };

        var messages = await DeviceController.CoreSendProgram(cm, programServices, batteryServices, dbcService, request);

        Assert.Equal(1, h1.SetProgramCallCount);
        Assert.Equal(1, h1.SetBatteryCallCount);
        Assert.Equal(1, h1.TransferDbcCallCount);
        Assert.Equal(1, h2.SetProgramCallCount);
        Assert.Equal(1, h2.SetBatteryCallCount);
        Assert.Equal(1, h2.TransferDbcCallCount);
        Assert.Contains(messages, m => m.Contains($"Program : {Key(1, 1, 1)} -> Success"));
        Assert.Contains(messages, m => m.Contains($"Program : {Key(1, 1, 2)} -> Success"));
    }

    [Fact]
    public async Task CoreSendProgram_PassesTheResolvedBatteryIntoSetProgramAsync()
    {
        // The selected battery must reach SetProgramAsync, not just SetBatteryParamAsync — this
        // is what lets the encoder resolve ACN5/VN nominal values against it (see
        // BatteryUnitResolver / docs/manual-extract/VNC-ACN-battery-parameters.md).
        var cm = NewChannelManager();
        var handler = new FakeCoreCommandHandler();
        cm._devices[Key(1, 1, 1)] = handler;

        var batteryServices = new FakeBatteryServices
        {
            BatteryResult = CommonResponse<BatteryDTO>.Ok(new BatteryDTO
            {
                Name = "Test",
                Producer = "Test",
                MaximumVoltage = 100,
                BreakVoltage = 1,
                NominalCapacity = 100,
                NumberOfCells = 6,
            })
        };

        var request = new List<CommonRequest>
        {
            new() { DeviceID = 1, SecondaryBoardNumber = 1, ChannelList = new List<int> { 1 }, ProgramId = 10, BatteryId = 3 }
        };

        await DeviceController.CoreSendProgram(cm, new FakeProgramServices(), batteryServices, new FakeDbcService(), request);

        Assert.NotNull(handler.LastBatteryPassedToSetProgram);
        Assert.Equal(100, handler.LastBatteryPassedToSetProgram!.NominalCapacity);
        Assert.Equal(6, handler.LastBatteryPassedToSetProgram!.NumberOfCells);
    }

    [Fact]
    public async Task CoreSendProgram_FetchesProgramBatteryDbcOnlyOncePerRequest_NotPerChannel()
    {
        // Regression guard: the ChannelList refactor moved these lookups inside the per-channel
        // loop, turning one DB fetch per bulk request into one per channel (N+1). They must be
        // fetched once per request and reused across every expanded channel.
        var cm = NewChannelManager();
        for (int ch = 1; ch <= 5; ch++)
            cm._devices[Key(1, 1, ch)] = new FakeCoreCommandHandler();

        var programServices = new FakeProgramServices();
        var batteryServices = new FakeBatteryServices();
        var dbcService = new FakeDbcService();

        var request = new List<CommonRequest>
        {
            new()
            {
                DeviceID = 1,
                SecondaryBoardNumber = 1,
                ChannelList = Enumerable.Range(1, 5).ToList(),
                ProgramId = 10,
                BatteryId = 3,
                dbcId = 2,
            }
        };

        await DeviceController.CoreSendProgram(cm, programServices, batteryServices, dbcService, request);

        Assert.Equal(1, programServices.GetProgramCallCount);
        Assert.Equal(1, batteryServices.GetBatteryCallCount);
        Assert.Equal(1, dbcService.GetByIdCallCount);
    }

    [Fact]
    public async Task CoreSendProgram_SkipsProgramBatteryDbc_WhenIdsNotProvided()
    {
        var cm = NewChannelManager();
        var handler = new FakeCoreCommandHandler();
        cm._devices[Key(1, 1, 1)] = handler;

        var programServices = new FakeProgramServices();
        var batteryServices = new FakeBatteryServices();
        var dbcService = new FakeDbcService();

        var request = new List<CommonRequest>
        {
            new() { DeviceID = 1, SecondaryBoardNumber = 1, ChannelList = new List<int> { 1 } }
        };

        var messages = await DeviceController.CoreSendProgram(cm, programServices, batteryServices, dbcService, request);

        Assert.Equal(0, programServices.GetProgramCallCount);
        Assert.Equal(0, batteryServices.GetBatteryCallCount);
        Assert.Equal(0, dbcService.GetByIdCallCount);
        Assert.Equal(0, handler.SetProgramCallCount);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task CoreSendProgram_HardwareNotReady_SkipsChannelEntirely()
    {
        var cm = NewChannelManager();
        var handler = new FakeCoreCommandHandler { HwReady = false };
        cm._devices[Key(1, 1, 1)] = handler;

        var request = new List<CommonRequest>
        {
            new() { DeviceID = 1, SecondaryBoardNumber = 1, ChannelList = new List<int> { 1 }, ProgramId = 10 }
        };

        var messages = await DeviceController.CoreSendProgram(
            cm, new FakeProgramServices(), new FakeBatteryServices(), new FakeDbcService(), request);

        Assert.Single(messages);
        Assert.Contains("Hardware not ready", messages[0]);
        Assert.Equal(0, handler.SetProgramCallCount);
    }

    [Fact]
    public async Task CoreSendProgram_EmptyChannelList_ReportsErrorAndDoesNotThrow()
    {
        var cm = NewChannelManager();
        var request = new List<CommonRequest>
        {
            new() { DeviceID = 1, SecondaryBoardNumber = 1, ChannelList = new List<int>(), ProgramId = 10 }
        };

        var messages = await DeviceController.CoreSendProgram(
            cm, new FakeProgramServices(), new FakeBatteryServices(), new FakeDbcService(), request);

        Assert.Single(messages);
        Assert.Contains("ChannelList", messages[0]);
    }

    // ── CoreLiveData ─────────────────────────────────────────────────

    [Fact]
    public async Task CoreLiveData_ReturnsRecordForEachExpandedChannel()
    {
        var cm = NewChannelManager();
        cm._devices[Key(1, 1, 1)] = new FakeCoreCommandHandler();
        cm._devices[Key(1, 1, 2)] = new FakeCoreCommandHandler();

        var data = await DeviceController.CoreLiveData(cm, new List<CommonRequest> { Req(1, new List<int> { 1, 2 }) });

        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task CoreLiveData_InvalidChannelList_SkipsRequestInsteadOfThrowing()
    {
        var cm = NewChannelManager();
        var data = await DeviceController.CoreLiveData(cm, new List<CommonRequest> { Req(1, new List<int>()) });

        Assert.Empty(data);
    }
}
