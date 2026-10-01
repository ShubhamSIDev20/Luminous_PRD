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

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Unlike FakeChannelCommandHandler (whose action methods throw on purpose - CircuitSelectionLogic
/// never calls them), this fake implements Start/Stop/Pause/Continue for real, with a delay long
/// enough that two overlapping calls are reliably observable via a shared concurrency counter -
/// the only way to prove "same device runs sequentially, different devices run in parallel"
/// without depending on real timing precision.
/// </summary>
public class RecordingChannelCommandHandler : IChannelCommandHandler
{
    private readonly ConcurrencyProbe _probe;

    public static RecordingChannelCommandHandler Circuit(int deviceId, int board, int channel, ConcurrencyProbe probe) =>
        new(probe) { Channel = new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = board, ChannelNumber = channel } };

    private RecordingChannelCommandHandler(ConcurrencyProbe probe) => _probe = probe;

    public ChannelDto Channel { get; set; } = new();
    public ProgramDTO Program { get; set; } = null!;
    public List<StepModel> ExpandedProgramSteps => new();
    public StepModel? LastLiveStepUpdate => null;
    public BatteryDTO Battery { get; set; } = null!;
    public SessionRecordDto Session { get; set; } = null!;
    public CalibrationDto calibration { get; set; } = null!;
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() { }
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
    public async Task<CommonResponse<bool>> StartProgram() => await _probe.RunAsync(Channel.DeviceID);
    public async Task<CommonResponse<bool>> StopProgram() => await _probe.RunAsync(Channel.DeviceID);
    public async Task<CommonResponse<bool>> PauseProgram() => await _probe.RunAsync(Channel.DeviceID);
    public async Task<CommonResponse<bool>> ContinueProgram() => await _probe.RunAsync(Channel.DeviceID);
    public Task<CommonResponse<bool>> TimeSyn() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ResetSystem() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramStepsDTO, BatteryDTO? Battery = null) => throw new NotImplementedException();
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

/// <summary>Tracks how many calls for a given device are in flight at once. Two calls for the
/// SAME device must never overlap (peak count 1); calls for DIFFERENT devices must be able to
/// overlap (peak count > 1) - the whole point of the per-device SemaphoreSlim pattern.</summary>
public class ConcurrencyProbe
{
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _activePerDevice = new();
    public readonly Dictionary<int, int> PeakPerDevice = new();

    public async Task<CommonResponse<bool>> RunAsync(int deviceId)
    {
        lock (_lock)
        {
            _activePerDevice[deviceId] = _activePerDevice.GetValueOrDefault(deviceId) + 1;
            PeakPerDevice[deviceId] = Math.Max(PeakPerDevice.GetValueOrDefault(deviceId), _activePerDevice[deviceId]);
        }

        await Task.Delay(50);

        lock (_lock) { _activePerDevice[deviceId]--; }
        return CommonResponse<bool>.Ok(true);
    }
}

/// <summary>Records which action method was called, for ChannelActionExecutorTests' routing
/// test - the four action methods on RecordingChannelCommandHandler are otherwise
/// indistinguishable from outside.</summary>
public class TrackingHandler : IChannelCommandHandler
{
    private readonly List<string> _calls;
    public TrackingHandler(List<string> calls) => _calls = calls;

    public ChannelDto Channel { get; set; } = new() { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 1 };
    public ProgramDTO Program { get; set; } = null!;
    public List<StepModel> ExpandedProgramSteps => new();
    public StepModel? LastLiveStepUpdate => null;
    public BatteryDTO Battery { get; set; } = null!;
    public SessionRecordDto Session { get; set; } = null!;
    public CalibrationDto calibration { get; set; } = null!;
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() { }
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
    public Task<CommonResponse<bool>> StartProgram() { _calls.Add("start"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> StopProgram() { _calls.Add("stop"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> PauseProgram() { _calls.Add("pause"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> ContinueProgram() { _calls.Add("continue"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> TimeSyn() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ResetSystem() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramStepsDTO, BatteryDTO? Battery = null) => throw new NotImplementedException();
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
