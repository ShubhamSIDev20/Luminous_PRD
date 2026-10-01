using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations
{
    public class BatteryServices : IBatteryServices
    {
        private readonly IBatteryRepository _batteryRepository;
        public BatteryServices(IBatteryRepository batteryRepository)
        {
            _batteryRepository = batteryRepository;
        }
        public async Task<CommonResponse<bool>> BatteryDelete(long BatteryId)
        {
            return await _batteryRepository.BatteryDelete(BatteryId);
        }

        public async Task<CommonResponse<BatteryDTO>> CreateOrUpdateBattery(BatteryDTO battery)
        {
            return await _batteryRepository.CreateOrUpdateBattery(battery);
        }

        public async Task<CommonResponse<List<BatteryDTO>>> GetBatteries()
        {
            return await _batteryRepository.GetBatteries();
        }

        public async Task<CommonResponse<BatteryDTO>> GetBattery(long BatteryId)
        {
            return await _batteryRepository.GetBattery(BatteryId);
        }

        public async Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(
            long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId)
        {
            return await _batteryRepository.UpdatePortAssignmentsAsync(batteryId, port1DbcId, port2DbcId, port3DbcId);
        }
    }
}
