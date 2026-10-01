using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using System.Net.Sockets;

namespace BatteryTestingSystem.Components.UI.Dashboard;

/// <summary>
/// Static, no-op IChannelCommandHandler used only to drive the CardSettings / CardConfiguration
/// preview through the real DeviceChannel component - so the preview renders pixel-identical
/// to a real dashboard card instead of a hand-built approximation of it (see ADR / CardPreviewData.cs).
/// Never wired into ChannelManager - action methods are unreachable from a preview instance, so they
/// just fail rather than doing anything.
/// </summary>
public class PreviewChannelCommandHandler : IChannelCommandHandler
{
    public PreviewChannelCommandHandler()
    {
        Channel = new ChannelDto
        {
            DeviceID = 1,
            SecondaryBoardNumber = 1,
            ChannelNumber = 1,
            DeviceName = "Preview Device",
            MACID = "00:00:00:00:00:00",
            IPAddress = "192.168.1.1",
            IsRegistered = true,
            ChannelType = "Single",
        };

        Program = new ProgramDTO
        {
            ProgramId = 7,
            ProgramName = CardPreviewData.TextPreviewValues["ProgramID"],
            ProgramSteps = 24,
            CreatedBy = "preview",
            UpdatedBy = "preview",
        };

        Battery = new BatteryDTO
        {
            Id = 42,
            Name = "Preview Battery",
            Producer = "Acme",
            Quantity = 1,
            NominalVoltage = 3.7f,
            NominalCurrent = 2.5f,
            NominalCapacity = 5.0f,
            MaximumVoltage = 4.2f,
            BreakVoltage = 2.8f,
            NumberOfCells = 1,
            CreatedBy = "preview",
            UpdatedBy = "preview",
        };

        Session = new SessionRecordDto
        {
            SessionID = 1029,
            SessionName = CardPreviewData.TextPreviewValues["SessionID"],
            StartTime = DateTime.Now.AddHours(-2),
            DeviceID = 1,
            SecondaryBoardNumber = 1,
            ChannelNumber = 1,
            BatteryID = 42,
            ProgramID = 7,
            Storerecordcount = 1024,
        };

        var record = new RealTimeRecordDto
        {
            DeviceID = 1,
            SecondaryBoardNumber = 1,
            ChannelNumber = 1,
            SessionID = 1029,
            BatteryID = 42,
            ProgramID = 7,
            StepNumber = 12,
            StepRunningTime = TimeSpan.FromMinutes(12).Add(TimeSpan.FromSeconds(34)),
            RunningTime = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(45)).Add(TimeSpan.FromSeconds(10)),
            Current = (float)CardPreviewData.NumericPreviewValues["Current"],
            Voltage = (float)CardPreviewData.NumericPreviewValues["Voltage"],
            Power = (float)CardPreviewData.NumericPreviewValues["Power"],
            Temperature = (float)CardPreviewData.NumericPreviewValues["Temperature"],
            AccumulatedCapacity = (float)CardPreviewData.NumericPreviewValues["AccumulatedCapacity"],
            ChargeCapacity = (float)CardPreviewData.NumericPreviewValues["ChargeCapacity"],
            DischargeCapacity = (float)CardPreviewData.NumericPreviewValues["DischargeCapacity"],
            StepCapacity = (float)CardPreviewData.NumericPreviewValues["StepCapacity"],
            AccumulatedEnergy = (float)CardPreviewData.NumericPreviewValues["AccumulatedEnergy"],
            ChargeEnergy = (float)CardPreviewData.NumericPreviewValues["ChargeEnergy"],
            DischargeEnergy = (float)CardPreviewData.NumericPreviewValues["DischargeEnergy"],
            StepEnergy = (float)CardPreviewData.NumericPreviewValues["StepEnergy"],
            CycleNumber = 3,
            CycleRunIteration = 1,
            TableStepNumber = 4,
            TableTotalRowNumber = 120,
            ProgramStatus = ProgramRunningStatus.Running,
            CircuitStatus = CircuitStatus.Charge,
        };

        RealTime = new recordRequest { RealTimeRecord = record };
    }

    public ChannelDto Channel { get; set; }
    public ProgramDTO Program { get; set; }
    public List<StepModel> ExpandedProgramSteps { get; } = new();
    public StepModel? LastLiveStepUpdate => null;
    public BatteryDTO Battery { get; set; }
    public SessionRecordDto Session { get; set; }
    public CalibrationDto calibration { get; set; } = new();
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() { }
    public recordRequest RealTime { get; set; }
    public DbcRecord dbcData { get; set; } = new();
    public bool commandinterrupt { get; set; } = false;
    public bool IsConnected => true;

    public IAlarmService? Alarms { get; set; }
    public event Action? OnChannelChanged;

    public CancellationTokenSource _cts { get; set; } = new();
    public System.Threading.Channels.Channel<recordStoreRequest> _StoreQueue { get; set; }
        = System.Threading.Channels.Channel.CreateUnbounded<recordStoreRequest>();

    public Task InitializeAsync() => Task.CompletedTask;
    public void EnqueueForStore(recordStoreRequest request, Dictionary<string, object>? dbcRecord) { }
    public Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command) =>
        Task.FromResult(CommonResponse<T>.Fail("Preview only - no hardware connection."));
    public Task<CommonResponse<bool>> StartProgram() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> StopProgram() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> PauseProgram() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> ContinueProgram() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> TimeSyn() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> ResetSystem() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber) => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams) => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramStepsDTO, BatteryDTO? Battery = null) => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> LiveStepUpdateAsync(StepModel editedStep) => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingDetails() => Task.FromResult(CommonResponse<ManufacturingDetailDTO>.Fail("Preview only."));
    public Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryConfigDetails() => Task.FromResult(CommonResponse<FactoryConfigDetailDTO>.Fail("Preview only."));
    public Task<CommonResponse<bool>> UnregisterAsync() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));

    public Task<CommonResponse<bool>> HWReadyToCalibrationAsync() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> SendLiveCurrentandVoltage() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> CalibrationPointPreset(CalibrationQueryId queryId, float value, CalibrationRange? range = null) =>
        Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> SetGainOffset(CalibrationQueryId queryId, float gain, float offset, CalibrationRange? range = null) =>
        Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> CancelCalibration() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> StopCalibration(bool force = false) => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<bool>> StopverifyCalibration() => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
    public Task<CommonResponse<CalibrationData>> PreviousCalibration() => Task.FromResult(CommonResponse<CalibrationData>.Fail("Preview only."));

    public Task<CommonResponse<bool>> TransferDbcFile(
        DbcFileRecordDto? port1Dbc = null,
        DbcFileRecordDto? port2Dbc = null,
        DbcFileRecordDto? port3Dbc = null) => Task.FromResult(CommonResponse<bool>.Fail("Preview only."));
}
