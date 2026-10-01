using BatteryTestingSystem.Models.DTOs;

namespace BatteryTestingSystem.Services.Interfaces;

public interface ISchedulerService
{
    Task<CommonResponse<List<ProgramScheduleDto>>> GetAllSchedulesAsync();
    Task<CommonResponse<ProgramScheduleDto>> CreateScheduleAsync(CreateScheduleRequest request);
    Task<CommonResponse<bool>> DeleteScheduleAsync(long scheduleId);
    Task<CommonResponse<bool>> ToggleActiveAsync(long scheduleId, bool isActive);
    Task<CommonResponse<List<ScheduleExecutionLogDto>>> GetLogsAsync(long? scheduleId = null);

    // Called by Hangfire job
    Task ExecuteScheduleAsync(long scheduleId);
}
