using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations
{
    public class UserCircuitAccessService : IUserCircuitAccessService
    {
        private readonly IUserCircuitAccessRepository _repo;

        public UserCircuitAccessService(IUserCircuitAccessRepository repo)
        {
            _repo = repo;
        }

        public Task<CommonResponse<List<UserCircuitAccessDto>>> GetByUserIdAsync(string userId)
            => _repo.GetByUserIdAsync(userId);

        public Task<CommonResponse<bool>> SetUserCircuitAccessAsync(string userId, List<(int DeviceId, int CircuitId)> circuits)
            => _repo.SetUserCircuitAccessAsync(userId, circuits);

        public Task<CommonResponse<List<UserCircuitAccessDto>>> GetAllAsync()
            => _repo.GetAllAsync();
    }
}
