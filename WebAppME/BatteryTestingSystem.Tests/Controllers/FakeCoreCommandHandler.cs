using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Tests.Controllers;

/// <summary>
/// Minimal, fully-controllable stand-in for IChannelCommandHandler used to exercise
/// DeviceController.Core* / DeviceMcpTools against the new ChannelList-based CommonRequest
/// without touching real hardware. Every result the Core* methods branch on is configurable;
/// everything else throws NotImplementedException so an unexpected call fails loudly.
/// </summary>
public class FakeCoreCommandHandler : IChannelCommandHandler
{
    public bool HwReady { get; set; } = true;
    public CommonResponse<bool> SetProgramResult { get; set; } = CommonResponse<bool>.Ok(true);
    public CommonResponse<bool> SetBatteryResult { get; set; } = CommonResponse<bool>.Ok(true);
    public CommonResponse<bool> TransferDbcResult { get; set; } = CommonResponse<bool>.Ok(true);
    public CommonResponse<bool> StartResult { get; set; } = CommonResponse<bool>.Ok(true);
    public CommonResponse<bool> StopResult { get; set; } = CommonResponse<bool>.Ok(true);
    public CommonResponse<bool> PauseResult { get; set; } = CommonResponse<bool>.Ok(true);
    public CommonResponse<bool> ContinueResult { get; set; } = CommonResponse<bool>.Ok(true);

    public int SetProgramCallCount { get; private set; }
    public int SetBatteryCallCount { get; private set; }
    public int TransferDbcCallCount { get; private set; }

    public ChannelDto Channel { get; set; } = new();
    public ProgramDTO Program { get; set; } = null!;
    public List<StepModel> ExpandedProgramSteps => new();
    public StepModel? LastLiveStepUpdate => null;
    public BatteryDTO Battery { get; set; } = null!;
    public SessionRecordDto Session { get; set; } = new();
    public CalibrationDto calibration { get; set; } = null!;
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() => throw new NotImplementedException();
    public recordRequest RealTime { get; set; } = new() { RealTimeRecord = new RealTimeRecordDto() };
    public DbcRecord dbcData { get; set; } = new();
    public bool commandinterrupt { get; set; }
    public bool IsConnected => true;
    public IAlarmService? Alarms { get; set; }
    public event Action? OnChannelChanged;
    public CancellationTokenSource _cts { get; set; } = null!;
    public Channel<recordStoreRequest> _StoreQueue { get; set; } = System.Threading.Channels.Channel.CreateUnbounded<recordStoreRequest>();

    public Task InitializeAsync() => throw new NotImplementedException();
    public void EnqueueForStore(recordStoreRequest request, Dictionary<string, object>? dbcRecord) => throw new NotImplementedException();
    public Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StartProgram() => Task.FromResult(StartResult);
    public Task<CommonResponse<bool>> StopProgram() => Task.FromResult(StopResult);
    public Task<CommonResponse<bool>> PauseProgram() => Task.FromResult(PauseResult);
    public Task<CommonResponse<bool>> ContinueProgram() => Task.FromResult(ContinueResult);
    public Task<CommonResponse<bool>> TimeSyn() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ResetSystem() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() =>
        Task.FromResult(HwReady ? CommonResponse<bool>.Ok(true) : CommonResponse<bool>.Fail("Hardware not ready"));
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO batteryParams)
    {
        SetBatteryCallCount++;
        return Task.FromResult(SetBatteryResult);
    }
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO programStepsDto, BatteryDTO? battery = null)
    {
        SetProgramCallCount++;
        LastBatteryPassedToSetProgram = battery;
        return Task.FromResult(SetProgramResult);
    }

    public BatteryDTO? LastBatteryPassedToSetProgram { get; private set; }
    public Task<CommonResponse<bool>> LiveStepUpdateAsync(StepModel editedStep) => throw new NotImplementedException();
    public Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingDetails() => throw new NotImplementedException();
    public Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryConfigDetails() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> UnregisterAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToCalibrationAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SendLiveCurrentandVoltage() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CalibrationPointPreset(CalibrationQueryId queryId, float value, CalibrationRange? range = null) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetGainOffset(CalibrationQueryId queryId, float gain, float offset, CalibrationRange? range = null) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CancelCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopCalibration(bool force = false) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopverifyCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<CalibrationData>> PreviousCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> TransferDbcFile(DbcFileRecordDto? port1Dbc = null, DbcFileRecordDto? port2Dbc = null, DbcFileRecordDto? port3Dbc = null)
    {
        TransferDbcCallCount++;
        return Task.FromResult(TransferDbcResult);
    }
}
