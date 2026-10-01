using System.Threading.Tasks;
using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// ServerSessionStorageService's constructor calls ServiceLocator.GetScoped&lt;IConfigStorageService&gt;()
/// unconditionally (to preload any DB-persisted component state), and ServiceLocator throws if
/// no provider was ever configured. This fake, plus a minimal DI container wired through
/// ServiceLocator.SetProvider, is what lets the real ServerSessionStorageService construct
/// cleanly inside a test process.
/// </summary>
public class FakeConfigStorageService : IConfigStorageService
{
    public Task<CommonResponse<ConfigurationEntity>> GetConfigurationAsync(string key) =>
        Task.FromResult(CommonResponse<ConfigurationEntity>.Fail("not found"));

    public Task<CommonResponse<List<ConfigurationEntity>>> GetConfigurationsAsync() =>
        Task.FromResult(CommonResponse<List<ConfigurationEntity>>.Ok(new List<ConfigurationEntity>()));

    public Task<CommonResponse<bool>> SaveConfigurationAsync(ConfigurationEntity config) =>
        Task.FromResult(CommonResponse<bool>.Ok(true));

    public Task<CommonResponse<bool>> DeleteConfigurationAsync(string key) =>
        Task.FromResult(CommonResponse<bool>.Ok(true));
}
