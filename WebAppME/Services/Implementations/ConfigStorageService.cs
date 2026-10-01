using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Implementations;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using System.Collections.Generic;

namespace BatteryTestingSystem.Services.Implementations
{
    public class ConfigStorageService : IConfigStorageService
    {
        private IConfigStorageRepository configStorage;
        public ConfigStorageService(IConfigStorageRepository cStorage)
        {
            configStorage = cStorage;
        }
        public async Task<CommonResponse<bool>> DeleteConfigurationAsync(string key)
        {
            return await configStorage.DeleteConfigurationAsync(key);
        }

        public async Task<CommonResponse<ConfigurationEntity>> GetConfigurationAsync(string key)
        {
            return await configStorage.GetConfigurationAsync(key);
        }

        public async Task<CommonResponse<List<ConfigurationEntity>>> GetConfigurationsAsync()
        {
            return await configStorage.GetConfigurationsAsync();
        }

        public async Task<CommonResponse<bool>> SaveConfigurationAsync(ConfigurationEntity config)
        {
            return await configStorage.SaveConfigurationAsync(config);
        }
    }
}
