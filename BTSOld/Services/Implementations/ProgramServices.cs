using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BatteryTestingSystem.Services.Implementations
{
    public class ProgramServices : IProgramServices
    {
        private readonly IProgramRepository _programRepository;
        public ProgramServices(IProgramRepository programRepository)
        {
            _programRepository = programRepository;
        }

        public async Task<CommonResponse<ProgramDTO>> CreateProgramAsync(CreateProgramRequest create)
        {
            return await _programRepository.CreateProgramAsync(create);
        }

        public async Task<CommonResponse<bool>> DeleteProgramAsync(long programId)
        {
            return await _programRepository.DeleteProgramAsync(programId);
        }


        public async Task<CommonResponse<ProgramDTO>> GetProgramAsync(long programId)
        {
            return await _programRepository.GetProgramAsync(programId);
        }

        public async Task<CommonResponse<ProgramDTO>> GetProgramByNameAsync(string programName)
        {
            return await _programRepository.GetProgramByNameAsync(programName);
        }

        public async Task<CommonResponse<List<ProgramDTO>>> GetProgramsAsync()
        {
            return await _programRepository.GetProgramsAsync();
        }

        #region Session Management
        public async Task<CommonResponse<SessionRecordDto>> GetLastSessionAsync(CircuitDto cuitDto)
        {
            return await _programRepository.GetLastSessionAsync(cuitDto);
        }

        public async Task<CommonResponse<List<SessionRecordDto>>> GetSessionsAsync()
        {
            return await _programRepository.GetSessionsAsync();
        }

        public async Task<CommonResponse<bool>> AddOrUpdateSession(SessionRecordDto session)
        {
            return await _programRepository.AddOrUpdateSession(session);
        }

        public async Task<CommonResponse<bool>> CreateSession(SessionRecordDto session)
        {
            return await _programRepository.CreateSession(session);

        }
        public async Task<CommonResponse<bool>> EndSession(SessionRecordDto session)
        {
            return await _programRepository.EndSession(session);
        }
        #endregion
        public async Task<CommonResponse<bool>> RecoverProgramAsync(long programId)
        {
            return await _programRepository.RecoverProgramAsync(programId);
        }

        public async Task<CommonResponse<bool>> UpdateProgramAsync(ProgramDTO program)
        {
            return await _programRepository.UpdateProgramAsync(program);
        }

        #region Standard Registration
        public async Task<CommonResponse<bool>> RemoveRegistrationStandardAsync(int id)
        {
            return await _programRepository.RemoveRegistrationStandardAsync(id);

        }
        public async Task<CommonResponse<List<RegistrationStandardsDTO>>> GetRegistrationStandardAsync()
        {
            return await _programRepository.GetRegistrationStandardAsync();
        }
        public async Task<CommonResponse<RegistrationStandardsDTO>> UpdateRegistrationStandardAsync(RegistrationStandardsDTO dto)
        {
            return await _programRepository.UpdateRegistrationStandardAsync(dto);
        }
        public async Task<CommonResponse<RegistrationStandardsDTO>> CreateRegistrationStandardAsync(RegistrationStandardsDTO create)
        {
            return await _programRepository.CreateRegistrationStandardAsync(create);
        }

        #endregion
    }
}
