using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class AlarmRepository : Repository<AlarmLog>, IAlarmRepository
    {
        private readonly AppDbContext _appDbContext;

        public AlarmRepository(AppDbContext context) : base(context)
        {
            _appDbContext = context;
        }

        public async Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey)
        {
            return await _appDbContext.AlarmLogs
                .Where(a => a.AlarmKey == alarmKey
                            && a.AcknowledgedAtUtc == null
                            && a.ClearedAtUtc == null)
                .OrderByDescending(a => a.LastSeenUtc)
                .FirstOrDefaultAsync();
        }

        public async Task<AlarmLog> InsertAsync(AlarmLog alarm)
        {
            await _appDbContext.AlarmLogs.AddAsync(alarm);
            await _appDbContext.SaveChangesAsync();
            return alarm;
        }

        public async Task SaveAsync(AlarmLog alarm)
        {
            _appDbContext.AlarmLogs.Update(alarm);
            await _appDbContext.SaveChangesAsync();
        }

        public async Task<List<AlarmLog>> GetActiveAsync(int take)
        {
            return await _appDbContext.AlarmLogs
                .AsNoTracking()
                .Where(a => a.ClearedAtUtc == null)
                .OrderByDescending(a => a.AcknowledgedAtUtc == null)
                .ThenByDescending(a => a.LastSeenUtc)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> CountUnacknowledgedAsync()
        {
            return await _appDbContext.AlarmLogs
                .CountAsync(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null);
        }

        public async Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc)
        {
            var row = await _appDbContext.AlarmLogs.FirstOrDefaultAsync(a => a.Id == id);
            if (row == null || row.AcknowledgedAtUtc != null) return false;

            row.AcknowledgedAtUtc = whenUtc;
            row.AcknowledgedBy = user;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc)
        {
            var rows = await _appDbContext.AlarmLogs
                .Where(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null)
                .ToListAsync();

            foreach (var row in rows)
            {
                row.AcknowledgedAtUtc = whenUtc;
                row.AcknowledgedBy = user;
            }

            await _appDbContext.SaveChangesAsync();
            return rows.Count;
        }

        public async Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc)
        {
            var row = await GetActiveByKeyAsync(alarmKey);
            if (row == null) return false;

            row.ClearedAtUtc = whenUtc;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request)
        {
            try
            {
                IQueryable<AlarmLog> query = _appDbContext.AlarmLogs.AsNoTracking();

                if (request != null)
                {
                    if (request.Severity.HasValue)
                        query = query.Where(a => a.Severity == request.Severity.Value);

                    if (request.Source.HasValue)
                        query = query.Where(a => a.Source == request.Source.Value);

                    if (!string.IsNullOrWhiteSpace(request.DeviceId))
                        query = query.Where(a => a.DeviceId == request.DeviceId);

                    if (request.FromDate.HasValue)
                        query = query.Where(a => a.LastSeenUtc >= request.FromDate.Value);

                    if (request.EndDate.HasValue)
                        query = query.Where(a => a.LastSeenUtc <= request.EndDate.Value);

                    if (request.OnlyUnacknowledged)
                        query = query.Where(a => a.AcknowledgedAtUtc == null);
                }

                var rows = await query.OrderByDescending(a => a.LastSeenUtc).ToListAsync();

                if (rows.Count == 0)
                    return CommonResponse<List<AlarmLog>>.Fail("No alarms found matching the criteria.");

                return CommonResponse<List<AlarmLog>>.Ok(rows);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<AlarmLog>>.Fail($"Error fetching alarms: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes at most batchSize rows older than the cutoff. A row that is still
        /// unacknowledged and uncleared is NEVER pruned regardless of age - an unresolved
        /// fault must not disappear because it is old.
        /// </summary>
        public async Task<int> PruneAsync(DateTime cutoffUtc, int batchSize)
        {
            var ids = await _appDbContext.AlarmLogs
                .Where(a => a.LastSeenUtc < cutoffUtc
                            && (a.AcknowledgedAtUtc != null || a.ClearedAtUtc != null))
                .OrderBy(a => a.LastSeenUtc)
                .Select(a => a.Id)
                .Take(batchSize)
                .ToListAsync();

            if (ids.Count == 0) return 0;

            return await _appDbContext.AlarmLogs
                .Where(a => ids.Contains(a.Id))
                .ExecuteDeleteAsync();
        }
    }
}
