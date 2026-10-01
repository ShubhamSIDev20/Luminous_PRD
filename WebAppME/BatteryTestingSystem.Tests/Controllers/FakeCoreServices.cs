using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using Microsoft.AspNetCore.Components.Forms;

namespace BatteryTestingSystem.Tests.Controllers;

/// <summary>
/// Fakes for the three lookup services CoreSendProgram depends on. Only the members that
/// method actually calls are wired up; everything else throws so an unexpected call is loud.
/// </summary>
public class FakeProgramServices : IProgramServices
{
    public CommonResponse<ProgramDTO> ProgramResult { get; set; } = CommonResponse<ProgramDTO>.Ok(new ProgramDTO());
    public int GetProgramCallCount { get; private set; }

    public Task<CommonResponse<ProgramDTO>> GetProgramAsync(long programId)
    {
        GetProgramCallCount++;
        return Task.FromResult(ProgramResult);
    }

    public Task<CommonResponse<SessionRecordDto>> GetLastSessionAsync(ChannelDto cuitDto) => throw new NotImplementedException();
    public Task<CommonResponse<List<ProgramDTO>>> GetProgramsAsync() => throw new NotImplementedException();
    public Task<CommonResponse<ProgramDTO>> GetProgramByNameAsync(string programName) => throw new NotImplementedException();
    public Task<CommonResponse<ProgramDTO>> CreateProgramAsync(CreateProgramRequest create) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> UpdateProgramAsync(ProgramDTO program) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> DeleteProgramAsync(long programId) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> RecoverProgramAsync(long programId) => throw new NotImplementedException();
    public Task<CommonResponse<List<SessionRecordDto>>> GetSessionsAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CreateSession(SessionRecordDto session) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> EndSession(SessionRecordDto session) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> AddOrUpdateSession(SessionRecordDto session) => throw new NotImplementedException();
    public Task<CommonResponse<RegistrationStandardsDTO>> CreateRegistrationStandardAsync(RegistrationStandardsDTO create) => throw new NotImplementedException();
    public Task<CommonResponse<List<RegistrationStandardsDTO>>> GetRegistrationStandardAsync() => throw new NotImplementedException();
    public Task<CommonResponse<RegistrationStandardsDTO>> UpdateRegistrationStandardAsync(RegistrationStandardsDTO dto) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> RemoveRegistrationStandardAsync(int id) => throw new NotImplementedException();
}

public class FakeBatteryServices : IBatteryServices
{
    public CommonResponse<BatteryDTO> BatteryResult { get; set; } = CommonResponse<BatteryDTO>.Ok(new BatteryDTO());
    public int GetBatteryCallCount { get; private set; }

    public Task<CommonResponse<BatteryDTO>> GetBattery(long batteryId)
    {
        GetBatteryCallCount++;
        return Task.FromResult(BatteryResult);
    }

    public Task<CommonResponse<List<BatteryDTO>>> GetBatteries() => throw new NotImplementedException();
    public Task<CommonResponse<BatteryDTO>> CreateOrUpdateBattery(BatteryDTO battery) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> BatteryDelete(long batteryId) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId) => throw new NotImplementedException();
}

public class FakeDbcService : IDbcService
{
    public CommonResponse<DbcFileRecordDto> DbcResult { get; set; } = CommonResponse<DbcFileRecordDto>.Ok(new DbcFileRecordDto());
    public int GetByIdCallCount { get; private set; }

    public Task<CommonResponse<DbcFileRecordDto>> GetByIdAsync(long id)
    {
        GetByIdCallCount++;
        return Task.FromResult(DbcResult);
    }

    public Task<CommonResponse<List<DbcFileRecordDto>>> GetAllAsync() => throw new NotImplementedException();
    public Task<CommonResponse<List<DbcFileRecordDto>>> GetByBatteryIdAsync(long batteryId) => throw new NotImplementedException();
    public Task<CommonResponse<DbcFileRecordDto>> UploadAsync(DbcFileRequest request, IBrowserFile file) => throw new NotImplementedException();
    public Task<CommonResponse<DbcFileRecordDto>> UpdateMetaAsync(DbcFileRequest request) => throw new NotImplementedException();
    public Task<CommonResponse<DbcFileRecordDto>> UpdateDbcDatabaseAsync(long id, DbcDatabase database) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> DeleteAsync(long id, string? deletedBy = null) => throw new NotImplementedException();
    public Task<CommonResponse<string>> GetFilePathAsync(long id) => throw new NotImplementedException();
}
