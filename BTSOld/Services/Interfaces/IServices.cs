using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using Microsoft.AspNetCore.Components.Forms;

namespace BatteryTestingSystem.Services.Interfaces;

public interface IAuditService
{
    Task LogEventAsync(AuditLogDto auditEntry);
    Task <CommonResponse<List<AuditLogDto>>> GetAuditLogsAsync(AuditLogQueryParameters parameters);

}

public interface IDeviceCircuitServices 
{
    Task<CommonResponse<List<CircuitDto>>> GetCircuitsAsync();
    Task<CommonResponse<CircuitDto>> GetCircuitAsync(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> InsertAsync(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> UpdateRegistration(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> UpdateAsync(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> UpdateIsDeleteAsync(CircuitDto circuit, bool IsDelete = false);
    Task<CommonResponse<CircuitDto>> AllowCicuitAsync(CircuitDto circuit, bool IsRegistred);
    Task<CommonResponse<CircuitDto>> DeleteAsync(CircuitDto circuit);
    Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(CircuitDto circuit, ManufacturingDetailDTO Manufacturing);
    Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(CircuitDto circuit, FactoryConfigDetailDTO Factory);
    Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(CircuitDto circuit);
    Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(CircuitDto circuit);

    #region Calibration 

    Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(CircuitDto circuit, CalibrationDataPoint calibration);
    Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(CircuitDto circuit);
    Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(CircuitDto circuit);

    #endregion
}

public interface IProgramServices
{
    Task<CommonResponse<SessionRecordDto>> GetLastSessionAsync(CircuitDto cuitDto);
    Task<CommonResponse<List<ProgramDTO>>> GetProgramsAsync();
    Task<CommonResponse<ProgramDTO>> GetProgramAsync(long programId);
    Task<CommonResponse<ProgramDTO>> GetProgramByNameAsync(string programName);
    Task<CommonResponse<ProgramDTO>> CreateProgramAsync(CreateProgramRequest create);
    Task<CommonResponse<bool>> UpdateProgramAsync(ProgramDTO program);
    Task<CommonResponse<bool>> DeleteProgramAsync(long programId);
    Task<CommonResponse<bool>> RecoverProgramAsync(long programId);
    Task<CommonResponse<List<SessionRecordDto>>> GetSessionsAsync();

    Task<CommonResponse<bool>> CreateSession(SessionRecordDto session);
    Task<CommonResponse<bool>> EndSession(SessionRecordDto session);
    Task<CommonResponse<bool>> AddOrUpdateSession(SessionRecordDto session);


    #region Registration 

    Task<CommonResponse<RegistrationStandardsDTO>> CreateRegistrationStandardAsync(RegistrationStandardsDTO create);
    Task<CommonResponse<List<RegistrationStandardsDTO>>> GetRegistrationStandardAsync();
    Task<CommonResponse<RegistrationStandardsDTO>> UpdateRegistrationStandardAsync(RegistrationStandardsDTO dto);
    Task<CommonResponse<bool>> RemoveRegistrationStandardAsync(int id);

    #endregion
}

public interface IBatteryServices
{
    Task<CommonResponse<List<BatteryDTO>>> GetBatteries();
    Task<CommonResponse<BatteryDTO>> GetBattery(long BatteryId);
    Task<CommonResponse<BatteryDTO>> CreateOrUpdateBattery(BatteryDTO battery);
    Task<CommonResponse<bool>> BatteryDelete(long BatteryId);
    Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId);

}

public interface IDbcService
{
    Task<CommonResponse<List<DbcFileRecordDto>>> GetByBatteryIdAsync(long batteryId);
    Task<CommonResponse<DbcFileRecordDto>> GetByIdAsync(long id);
    Task<CommonResponse<DbcFileRecordDto>> UploadAsync(DbcFileRequest request, IBrowserFile file);
    Task<CommonResponse<DbcFileRecordDto>> UpdateMetaAsync(DbcFileRequest request);
    Task<CommonResponse<DbcFileRecordDto>> UpdateDbcDatabaseAsync(long Id, DbcDatabase database);
    Task<CommonResponse<bool>> DeleteAsync(long id, string? deletedBy = null);
    Task<CommonResponse<string>> GetFilePathAsync(long id);
}

public interface IConfigStorageService
{
    Task<CommonResponse<ConfigurationEntity>> GetConfigurationAsync(string key);
    Task<CommonResponse<List<ConfigurationEntity>>> GetConfigurationsAsync();
    Task<CommonResponse<bool>> SaveConfigurationAsync(ConfigurationEntity config);
    Task<CommonResponse<bool>> DeleteConfigurationAsync(string key);
}

public interface ICodeMessageService
{
    Task<CommonResponse<List<CodeMessageDto>>> GetAllAsync();
    Task<CommonResponse<List<CodeMessageDto>>> GetErrorsAsync();
    Task<CommonResponse<List<CodeMessageDto>>> GetMessagesAsync();
    Task<CommonResponse<CodeMessageDto>> GetByIdAsync(int id);
    Task<CommonResponse<bool>> SaveErrorsAsync(List<CodeMessageDto> errors);
    Task<CommonResponse<bool>> SaveMessagesAsync(List<CodeMessageDto> messages);
    Task<CommonResponse<bool>> DeleteAsync(int id);
}

//public interface IDeviceSettingService
//{
//    Task<CommonResponse<List<DeviceSettingsDto>>> GetAllAsync();
//    Task<CommonResponse<DeviceSettingsDto>> GetByIdAsync(DeviceSettingsDto setting);
//    Task<CommonResponse<bool>> SaveAsync(DeviceSettingsDto setting);
//    //Task<CommonResponse<bool>> DeleteAsync(int id);
//}

public interface IUserCircuitAccessService
{
    Task<CommonResponse<List<UserCircuitAccessDto>>> GetByUserIdAsync(string userId);
    Task<CommonResponse<bool>> SetUserCircuitAccessAsync(string userId, List<(int DeviceId, int CircuitId)> circuits);
    Task<CommonResponse<List<UserCircuitAccessDto>>> GetAllAsync();
}
