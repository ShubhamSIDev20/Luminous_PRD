using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Services.Implementations
{
    public class DeviceCircuitServices : IDeviceCircuitServices
    {
        private IDeviceCircuitRepository _deviceCircuitRepository;
        public DeviceCircuitServices(IDeviceCircuitRepository deviceCircuitRepository) 
        {
            _deviceCircuitRepository = deviceCircuitRepository;
        }

        public async Task<CommonResponse<CircuitDto>> AllowCicuitAsync(CircuitDto circuit, bool IsRegistred)
        {
            return await _deviceCircuitRepository.AllowCicuitAsync(circuit, IsRegistred);
        }

        public async Task<CommonResponse<CircuitDto>> DeleteAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.DeleteAsync(circuit);
        }

        public async Task<CommonResponse<CircuitDto>> GetCircuitAsync(CircuitDto circuit)
        {
           return await _deviceCircuitRepository.GetCircuitAsync(circuit);
        }

        public async Task<CommonResponse<List<CircuitDto>>> GetCircuitsAsync()
        {
            return await _deviceCircuitRepository.GetCircuitsAsync();
        }

   

        public async Task<CommonResponse<CircuitDto>> InsertAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.InsertAsync(circuit);
        }

        public async Task<CommonResponse<CircuitDto>> UpdateRegistration(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.UpdateRegistration(circuit);
        }

        public async Task<CommonResponse<CircuitDto>> UpdateAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.UpdateAsync(circuit);
        }

        public async Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(CircuitDto circuit, FactoryConfigDetailDTO Factory)
        {
            return await _deviceCircuitRepository.UpdateFactoryAsync(circuit, Factory);
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(CircuitDto circuit, ManufacturingDetailDTO Manufacturing)
        {
            return await _deviceCircuitRepository.UpdateManufacturingAsync(circuit, Manufacturing);
        }
        public async Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.GetFactoryAsync(circuit);
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.GetManufacturingAsync(circuit);
        }

        public async Task<CommonResponse<CircuitDto>> UpdateIsDeleteAsync(CircuitDto circuit, bool IsDelete = false)
        {
            return await _deviceCircuitRepository.UpdateIsDeleteAsync(circuit, IsDelete);
        }

        #region Calibration
        public async Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(CircuitDto circuit, CalibrationDataPoint calibration)
        {
            return await _deviceCircuitRepository.CreateOrUpdateCalibrationDataAsync(circuit, calibration);
        }

        public async Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.GetCalibrationDataAsync(circuit);
        }

        public async Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(CircuitDto circuit)
        {
            return await _deviceCircuitRepository.GetAllCalibrationDataAsync(circuit);
        }

        #endregion
    }
}
