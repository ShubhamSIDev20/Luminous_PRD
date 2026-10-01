using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations;

public class SchedulerRepository : Repository<ProgramSchedule>, ISchedulerRepository
{
    public SchedulerRepository(AppDbContext context) : base(context) { }

    public async Task<List<ProgramSchedule>> GetActiveSchedulesAsync()
    {
        return await _context.ProgramSchedules
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderByDescending(s => s.ScheduledAt)
            .ToListAsync();
    }

    public async Task<List<ScheduleExecutionLog>> GetLogsByScheduleIdAsync(long scheduleId, int limit = 50)
    {
        return await _context.ScheduleExecutionLogs
            .Where(l => l.ScheduleId == scheduleId)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ScheduleExecutionLog>> GetAllLogsAsync(int limit = 200)
    {
        return await _context.ScheduleExecutionLogs
            .Include(l => l.Schedule)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task AddLogAsync(ScheduleExecutionLog log)
    {
        await _context.ScheduleExecutionLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }
}
