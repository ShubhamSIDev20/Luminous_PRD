using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class ConfigStorageRepository : Repository<ConfigurationEntity>, IConfigStorageRepository
    {
        private readonly AppDbContext _AppDbcontext;

        public ConfigStorageRepository(AppDbContext context) : base(context)
        {
            _AppDbcontext = context;
        }

        public async Task<CommonResponse<bool>> DeleteConfigurationAsync(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return CommonResponse<bool>.Fail("Configuration key cannot be null or empty");
                }

                var config = await _AppDbcontext.ConfigurationEntitys
                    .FirstOrDefaultAsync(c => c.Key == key);

                if (config == null)
                {
                    return CommonResponse<bool>.Fail($"Configuration with key '{key}' not found");
                }

                _AppDbcontext.ConfigurationEntitys.Remove(config);
                await _AppDbcontext.SaveChangesAsync();

                return CommonResponse<bool>.Ok(true, "Configuration deleted successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Error deleting configuration: {ex.Message}");
            }
        }

        public async Task<CommonResponse<ConfigurationEntity>> GetConfigurationAsync(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return CommonResponse<ConfigurationEntity>.Fail("Configuration key cannot be null or empty");
                }

                var config = await _AppDbcontext.ConfigurationEntitys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == key);

                if (config == null)
                {
                    return CommonResponse<ConfigurationEntity>.Fail($"Configuration with key '{key}' not found");
                }

                return CommonResponse<ConfigurationEntity>.Ok(config);
            }
            catch (Exception ex)
            {
                return CommonResponse<ConfigurationEntity>.Fail($"Error retrieving configuration: {ex.Message}");
            }
        }

        public async Task<CommonResponse<List<ConfigurationEntity>>> GetConfigurationsAsync()
        {
            try
            {
                var configs = await _AppDbcontext.ConfigurationEntitys
                    .AsNoTracking()
                    .ToListAsync();
                
                if (configs == null || configs.Count == 0)
                {
                    return CommonResponse<List<ConfigurationEntity>>.Fail("No configurations found");
                }

                return CommonResponse<List<ConfigurationEntity>>.Ok(configs);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<ConfigurationEntity>>.Fail($"Error retrieving configurations: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> SaveConfigurationAsync(ConfigurationEntity config)
        {
            try
            {
                if (config == null)
                {
                    return CommonResponse<bool>.Fail("Configuration cannot be null");
                }

                if (string.IsNullOrWhiteSpace(config.Key))
                {
                    return CommonResponse<bool>.Fail("Configuration key cannot be null or empty");
                }

                var existingConfig = await _AppDbcontext.ConfigurationEntitys
                    .FirstOrDefaultAsync(c => c.Key == config.Key);

                if (existingConfig != null)
                {
                    // Update existing configuration
                    existingConfig.Value = config.Value;
                    _AppDbcontext.ConfigurationEntitys.Update(existingConfig);
                }
                else
                {
                    // Add new configuration
                    await _AppDbcontext.ConfigurationEntitys.AddAsync(config);
                }

                await _AppDbcontext.SaveChangesAsync();
                return CommonResponse<bool>.Ok(true, "Configuration saved successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Error saving configuration: {ex.Message}");
            }
        }
    }
}