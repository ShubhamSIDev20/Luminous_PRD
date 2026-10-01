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

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// Minimal stand-in for IChannelCommandHandler - CircuitSelectionLogic (T-38) only ever reads
/// Channel and RealTime, so every other member either returns a harmless default or throws
/// NotImplementedException, making a test that touches one an immediate, loud failure rather
/// than a silent null.
/// </summary>
public class FakeChannelCommandHandler : IChannelCommandHandler
{
    public static FakeChannelCommandHandler Circuit(
        int deviceId, int board, int channel,
        CircuitStatus status = CircuitStatus.Idle,
        ProgramRunningStatus programStatus = ProgramRunningStatus.Stop) => new()
    {
        Channel = new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = board, ChannelNumber = channel },
        RealTime = new recordRequest
        {
            RealTimeRecord = new RealTimeRecordDto { CircuitStatus = status, ProgramStatus = programStatus }
        }
    };

    public ChannelDto Channel { get; set; } = new();
    public ProgramDTO Program { get; set; } = null!;
    public List<StepModel> ExpandedProgramSteps => new();
    public StepModel? LastLiveStepUpdate => null;
    public BatteryDTO Battery { get; set; } = null!;
    public SessionRecordDto Session { get; set; } = null!;
    public CalibrationDto calibration { get; set; } = null!;
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() => throw new NotImplementedException();
    public recordRequest RealTime { get; set; } = new();
    public DbcRecord dbcData { get; set; } = null!;
    public bool commandinterrupt { get; set; }
    public bool IsConnected => true;
    public IAlarmService? Alarms { get; set; }
    public event Action? OnChannelChanged;
    public CancellationTokenSource _cts { get; set; } = null!;
    public Channel<recordStoreRequest> _StoreQueue { get; set; } = System.Threading.Channels.Channel.CreateUnbounded<recordStoreRequest>();

    public Task InitializeAsync() => throw new NotImplementedException();
    public void EnqueueForStore(recordStoreRequest request, Dictionary<string, object>? dbcRecord) => throw new NotImplementedException();
    public Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StartProgram() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopProgram() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> PauseProgram() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ContinueProgram() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> TimeSyn() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ResetSystem() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO batteryParams) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO programStepsDto, BatteryDTO? battery = null) => throw new NotImplementedException();
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
    public Task<CommonResponse<bool>> TransferDbcFile(DbcFileRecordDto? port1Dbc = null, DbcFileRecordDto? port2Dbc = null, DbcFileRecordDto? port3Dbc = null) => throw new NotImplementedException();
}
