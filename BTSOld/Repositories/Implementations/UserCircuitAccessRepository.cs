using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations
{

    public class UserCircuitAccessRepository : IUserCircuitAccessRepository
    {
        private readonly AppDbContext _db;

        public UserCircuitAccessRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<CommonResponse<List<UserCircuitAccessDto>>> GetByUserIdAsync(string userId)
        {
            try
            {
                var list = await _db.UserCircuitAccesses
                    .Where(x => x.UserId == userId)
                    .Select(x => new UserCircuitAccessDto
                    {
                        Id = x.Id,
                        UserId = x.UserId,
                        DeviceId = x.DeviceId,
                        CircuitId = x.CircuitId
                    })
                    .ToListAsync();

                return new CommonResponse<List<UserCircuitAccessDto>> { Success = true, Data = list };
            }
            catch (Exception ex)
            {
                return new CommonResponse<List<UserCircuitAccessDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<CommonResponse<bool>> SetUserCircuitAccessAsync(string userId, List<(int DeviceId, int CircuitId)> circuits)
        {
            try
            {
                // Remove existing
                var existing = _db.UserCircuitAccesses.Where(x => x.UserId == userId);
                _db.UserCircuitAccesses.RemoveRange(existing);

                // Add new
                foreach (var (deviceId, circuitId) in circuits)
                {
                    _db.UserCircuitAccesses.Add(new UserCircuitAccess
                    {
                        UserId = userId,
                        DeviceId = deviceId,
                        CircuitId = circuitId
                    });
                }

                await _db.SaveChangesAsync();
                return new CommonResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new CommonResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<CommonResponse<List<UserCircuitAccessDto>>> GetAllAsync()
        {
            try
            {
                var list = await _db.UserCircuitAccesses
                    .Select(x => new UserCircuitAccessDto
                    {
                        Id = x.Id,
                        UserId = x.UserId,
                        DeviceId = x.DeviceId,
                        CircuitId = x.CircuitId
                    })
                    .ToListAsync();

                return new CommonResponse<List<UserCircuitAccessDto>> { Success = true, Data = list };
            }
            catch (Exception ex)
            {
                return new CommonResponse<List<UserCircuitAccessDto>> { Success = false, Message = ex.Message };
            }
        }
    }
    
}
