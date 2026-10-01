using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using Serilog;
using System.IO;
using System.Net.Sockets;
using System.Threading.Channels;

namespace BatteryTestingSystem.Services.Interfaces
{
    public interface IChannelCommandHandler
    {
        ChannelDto Channel { get; set; }
        ProgramDTO Program { get; set; }
        List<StepModel> ExpandedProgramSteps { get; }
        StepModel? LastLiveStepUpdate { get; }
        BatteryDTO Battery { get; set; }
        SessionRecordDto Session{ get; set; }
        CalibrationDto calibration { get; set; }
        TcpClient _tcpClient { get; }
        DeviceConnection Connection { get; set; }
        void MarkDisconnected();
        recordRequest RealTime { get; set; }
        DbcRecord dbcData { get; set; }
        bool commandinterrupt { get; set; }
        bool IsConnected { get; }

        IAlarmService? Alarms { get; set; }

        event Action? OnChannelChanged;
        CancellationTokenSource _cts { get; set; }
        System.Threading.Channels.Channel<recordStoreRequest> _StoreQueue { get; set; }
        Task InitializeAsync();
        void EnqueueForStore(recordStoreRequest request, Dictionary<string, object>? dbcRecord);
        Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command);
        Task<CommonResponse<bool>> StartProgram();
        Task<CommonResponse<bool>> StopProgram();
        Task<CommonResponse<bool>> PauseProgram();
        Task<CommonResponse<bool>> ContinueProgram();
        Task<CommonResponse<bool>> TimeSyn();
        Task<CommonResponse<bool>> ResetSystem();
        Task<CommonResponse<bool>> JumpToStepAsync(int stepNumber);
        Task<CommonResponse<bool>> HWReadyToReadWriteAsync();
        Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams);
        Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramStepsDTO, BatteryDTO? Battery = null);
        Task<CommonResponse<bool>> LiveStepUpdateAsync(StepModel editedStep);
        Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingDetails();
        Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryConfigDetails();
        Task<CommonResponse<bool>> UnregisterAsync();

        #region CalibrationCommands
        Task<CommonResponse<bool>> HWReadyToCalibrationAsync();
        Task<CommonResponse<bool>> SendLiveCurrentandVoltage();
        Task<CommonResponse<bool>> CalibrationPointPreset(CalibrationQueryId queryId, float value, CalibrationRange? range = null); // for high low verify all in one method
        Task<CommonResponse<bool>> SetGainOffset(CalibrationQueryId queryId, float gain, float offset, CalibrationRange? range = null);
        Task<CommonResponse<bool>> CancelCalibration();
        Task<CommonResponse<bool>> StopCalibration(bool force = false);
        Task<CommonResponse<bool>> StopverifyCalibration();
        Task<CommonResponse<CalibrationData>> PreviousCalibration();

        #endregion

        #region DBC
        Task<CommonResponse<bool>> TransferDbcFile(
            DbcFileRecordDto? port1Dbc = null,
            DbcFileRecordDto? port2Dbc = null,
            DbcFileRecordDto? port3Dbc = null);
        #endregion
    }
}
