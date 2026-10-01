using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;

namespace BatteryTestingSystem.Repositories.Interfaces
{
    /// <summary>
    /// Persistence for operator alarms. An alarm is "active" while
    /// AcknowledgedAtUtc IS NULL AND ClearedAtUtc IS NULL.
    /// </summary>
    public interface IAlarmRepository
    {
        Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey);
        Task<AlarmLog> InsertAsync(AlarmLog alarm);
        Task SaveAsync(AlarmLog alarm);
        Task<List<AlarmLog>> GetActiveAsync(int take);
        Task<int> CountUnacknowledgedAsync();
        Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc);
        Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc);
        Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc);
        Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request);
        Task<int> PruneAsync(DateTime cutoffUtc, int batchSize);
    }
}
