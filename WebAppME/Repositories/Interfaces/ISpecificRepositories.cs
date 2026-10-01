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

public interface IDeviceChannelRepository : IRepository<Device>
{
    Task<CommonResponse<List<ChannelDto>>> GetChannelsAsync();
    Task<CommonResponse<ChannelDto>> GetChannelAsync(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> InsertAsync(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> UpdateRegistration(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> UpdateAsync(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> UpdateIsDeleteAsync(ChannelDto channel, bool IsDelete = false);
    Task<CommonResponse<ChannelDto>> AllowCicuitAsync(ChannelDto channel, bool IsRegistred);
    Task<CommonResponse<ChannelDto>> DeleteAsync(ChannelDto channel);
    Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(ChannelDto channel, ManufacturingDetailDTO Manufacturing);
    Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(ChannelDto channel, FactoryConfigDetailDTO Factory);
    Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(ChannelDto channel);
    Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(ChannelDto channel);

    Task<SecondaryBoard> GetOrCreateBoardAsync(int deviceId, int boardNumber);
    Task EnsureBoardOneAsync(int deviceId);

    #region Calibration 

    Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(ChannelDto channel, CalibrationDataPoint calibration);
    Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(ChannelDto channel);
    Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(ChannelDto channel);

    #endregion
}

public interface IProgramRepository : IRepository<BtsPrograms>
{
    Task<CommonResponse<SessionRecordDto>> GetLastSessionAsync(ChannelDto cuitDto);
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
    Task<CommonResponse<IEnumerable<DbcFileRecord>>> GetAllAsync();
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