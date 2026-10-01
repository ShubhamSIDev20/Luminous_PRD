using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Entities.Program;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.repositories.Interfaces;
using BatteryTestingSystem.Repositories.Implementations;
using System.Data;
using System.Security.AccessControl;
using System.Threading.Tasks;

namespace BatteryTestingSystem.Repositories.Interfaces;

public interface IDeviceCircuitRepository : IRepository<Device>
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

public interface IProgramRepository : IRepository<BtsPrograms>
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
    Task<CommonResponse<bool>> AddOrUpdateSession(SessionRecordDto session);
    Task<CommonResponse<bool>> CreateSession(SessionRecordDto session);
    Task<CommonResponse<bool>> EndSession(SessionRecordDto session);
   

    #region Registration 

    Task<CommonResponse<RegistrationStandardsDTO>> CreateRegistrationStandardAsync(RegistrationStandardsDTO create);
    Task<CommonResponse<List<RegistrationStandardsDTO>>> GetRegistrationStandardAsync();
    Task<CommonResponse<RegistrationStandardsDTO>> UpdateRegistrationStandardAsync(RegistrationStandardsDTO dto);
    Task<CommonResponse<bool>> RemoveRegistrationStandardAsync(int id);

    #endregion
}

public interface IBatteryRepository : IRepository<Batteries>
{
    Task<CommonResponse<List<BatteryDTO>>> GetBatteries();
    Task<CommonResponse<BatteryDTO>> GetBattery(long BatteryId);
    Task<CommonResponse<BatteryDTO>> CreateOrUpdateBattery(BatteryDTO battery);
    Task<CommonResponse<bool>> BatteryDelete(long BatteryId);
    Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId);

}
public interface IDbcRepository
{
    Task<CommonResponse<IEnumerable<DbcFileRecord>>> GetByBatteryIdAsync(long batteryId);
    Task<CommonResponse<DbcFileRecord?>> GetByIdAsync(long id);
    Task<CommonResponse<bool>> ExistsAsync(long batteryId, string name, string version, long? excludeId = null);
    Task<CommonResponse<DbcFileRecord?>> AddAsync(DbcFileRecord entity);
    Task<CommonResponse<bool>> UpdateAsync(DbcFileRecord entity);
    Task<CommonResponse<bool>> DeleteAsync(long id);
}


public interface IConfigStorageRepository
{
    Task<CommonResponse<ConfigurationEntity>> GetConfigurationAsync(string key);
    Task<CommonResponse<List<ConfigurationEntity>>> GetConfigurationsAsync();
    Task<CommonResponse<bool>> SaveConfigurationAsync(ConfigurationEntity config);
    Task<CommonResponse<bool>> DeleteConfigurationAsync(string key);
}

public interface ICodeMessageRepository
{
    Task<List<CodeMessage>> GetAllAsync();
    Task<List<CodeMessage>> GetByTypeAsync(int type);
    Task<CodeMessage?> GetByIdAsync(int id);
    Task<CodeMessage?> GetByIndexAndTypeAsync(int index, int type);
    Task<CodeMessage> CreateAsync(CodeMessage codeMessage);
    Task<CodeMessage> UpdateAsync(CodeMessage codeMessage);
    Task<bool> DeleteAsync(int id);
    Task<bool> SaveChangesAsync();
}

public interface IAuditRepository
{
    Task<CommonResponse<List<AuditLog>>> GetAuditRecordsAsync(AuditLogQueryParameters? request);
    Task<CommonResponse<bool>> LogEventAsync(AuditLog record);

}

//public interface IDeviceSettingsRepository
//{
//    Task<CommonResponse<DeviceSetting>> GetDeviceSettingsAsync(DeviceSetting settings);
//    Task<CommonResponse<bool>> SaveDeviceSettingsAsync(DeviceSetting settings);
//    Task<CommonResponse<List<DeviceSetting>>> GetAllDeviceSettingsAsync();
//}
public interface IUserCircuitAccessRepository
{
    Task<CommonResponse<List<UserCircuitAccessDto>>> GetByUserIdAsync(string userId);
    Task<CommonResponse<bool>> SetUserCircuitAccessAsync(string userId, List<(int DeviceId, int CircuitId)> circuits);
    Task<CommonResponse<List<UserCircuitAccessDto>>> GetAllAsync();
}