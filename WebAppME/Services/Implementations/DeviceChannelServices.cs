using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Services.Implementations
{
    public class DeviceChannelServices : IDeviceChannelServices
    {
        private IDeviceChannelRepository _deviceChannelRepository;
        public DeviceChannelServices(IDeviceChannelRepository deviceChannelRepository)
        {
            _deviceChannelRepository = deviceChannelRepository;
        }

        public async Task<CommonResponse<ChannelDto>> AllowCicuitAsync(ChannelDto channel, bool IsRegistred)
        {
            return await _deviceChannelRepository.AllowCicuitAsync(channel, IsRegistred);
        }

        public async Task<CommonResponse<ChannelDto>> DeleteAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.DeleteAsync(channel);
        }

        public async Task<CommonResponse<ChannelDto>> GetChannelAsync(ChannelDto channel)
        {
           return await _deviceChannelRepository.GetChannelAsync(channel);
        }

        public async Task<CommonResponse<List<ChannelDto>>> GetChannelsAsync()
        {
            return await _deviceChannelRepository.GetChannelsAsync();
        }



        public async Task<CommonResponse<ChannelDto>> InsertAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.InsertAsync(channel);
        }

        public async Task<CommonResponse<ChannelDto>> UpdateRegistration(ChannelDto channel)
        {
            return await _deviceChannelRepository.UpdateRegistration(channel);
        }

        public async Task<CommonResponse<ChannelDto>> UpdateAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.UpdateAsync(channel);
        }

        public async Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(ChannelDto channel, FactoryConfigDetailDTO Factory)
        {
            return await _deviceChannelRepository.UpdateFactoryAsync(channel, Factory);
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(ChannelDto channel, ManufacturingDetailDTO Manufacturing)
        {
            return await _deviceChannelRepository.UpdateManufacturingAsync(channel, Manufacturing);
        }
        public async Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.GetFactoryAsync(channel);
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.GetManufacturingAsync(channel);
        }

        public async Task<CommonResponse<ChannelDto>> UpdateIsDeleteAsync(ChannelDto channel, bool IsDelete = false)
        {
            return await _deviceChannelRepository.UpdateIsDeleteAsync(channel, IsDelete);
        }

        #region Calibration
        public async Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(ChannelDto channel, CalibrationDataPoint calibration)
        {
            return await _deviceChannelRepository.CreateOrUpdateCalibrationDataAsync(channel, calibration);
        }

        public async Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.GetCalibrationDataAsync(channel);
        }

        public async Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(ChannelDto channel)
        {
            return await _deviceChannelRepository.GetAllCalibrationDataAsync(channel);
        }

        #endregion
    }
}
