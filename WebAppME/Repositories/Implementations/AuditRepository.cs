using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;
using UAParser;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class AuditRepository : Repository<AuditLog>, IAuditRepository
    {
        private readonly AppDbContext _AppDbcontext;

        public AuditRepository(AppDbContext context) : base(context)
        {
            _AppDbcontext = context;
        }

        public async Task<CommonResponse<List<AuditLog>>> GetAuditRecordsAsync(AuditLogQueryParameters? request)
        {
            try
            {
                IQueryable<AuditLog> query = _AppDbcontext.AuditLogs.AsNoTracking();

                if (request != null)
                {
                    if (request.Module.HasValue)
                        query = query.Where(l => l.Module == request.Module.Value);

                    if (request.Action.HasValue)
                        query = query.Where(l => l.Action == request.Action.Value);

                    if (!string.IsNullOrWhiteSpace(request.User))
                        query = query.Where(l => l.User!.Contains(request.User));

                    if (request.FromDate.HasValue)
                        query = query.Where(l => l.Timestamp >= request.FromDate.Value);

                    if (request.EndDate.HasValue)
                        query = query.Where(l => l.Timestamp <= request.EndDate.Value);
                }

                query = query.OrderByDescending(l => l.Timestamp);

                var logs = await query.ToListAsync();

                if (logs == null || !logs.Any())
                    return CommonResponse<List<AuditLog>>.Fail("No audit logs found matching the criteria.");

                return CommonResponse<List<AuditLog>>.Ok(logs);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<AuditLog>>.Fail($"Error fetching audit logs: {ex.Message}");
            }
        }


        public async Task<CommonResponse<bool>> LogEventAsync(AuditLog record)
        {
            if (record == null)
                return CommonResponse<bool>.Fail("Audit log cannot be null");

            try
            {
                if (string.IsNullOrEmpty(record.IPAddress))
                    record.IPAddress = CurrentUser.IpAddress;
                if (string.IsNullOrEmpty(record.UserAgent))
                    record.UserAgent = CurrentUser.UserAgent != null ? JsonConvert.SerializeObject(CurrentUser.UserAgent) : "{}";
                if (string.IsNullOrEmpty(record.User))
                    record.User = CurrentUser.UserName;

                await _AppDbcontext.AuditLogs.AddAsync(record);

                var cutoffDate = DateTime.UtcNow.AddMonths(-3);
                var oldLogs = await _AppDbcontext.AuditLogs
                    .Where(l => l.Timestamp < cutoffDate)
                    .ToListAsync();

                if (oldLogs.Any())
                    _AppDbcontext.AuditLogs.RemoveRange(oldLogs);

                await _AppDbcontext.SaveChangesAsync();

                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Error logging audit event: {ex.Message}");
            }
        }
    }
}