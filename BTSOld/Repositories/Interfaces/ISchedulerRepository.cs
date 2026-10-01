using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.repositories.Interfaces;

namespace BatteryTestingSystem.Repositories.Interfaces;

public interface ISchedulerRepository : IRepository<ProgramSchedule>
{
    Task<List<ProgramSchedule>> GetActiveSchedulesAsync();
    Task<List<ScheduleExecutionLog>> GetLogsByScheduleIdAsync(long scheduleId, int limit = 50);
    Task<List<ScheduleExecutionLog>> GetAllLogsAsync(int limit = 200);
    Task AddLogAsync(ScheduleExecutionLog log);
}
